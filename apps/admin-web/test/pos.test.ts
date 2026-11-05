import { describe, expect, it } from "vitest";
import { thanhPayload, tinhTam, TY_LE_DIEM, type TabHoaDon } from "../lib/pos";

/**
 * **Tiền của POS — hàm thuần** (`WP-06` / `WP-09`).
 *
 * 🔒 Nguyên tắc `D-42` ②: **UI chỉ HIỂN THỊ**, số tiền do `TinhTienHoaDon` của server quyết. `tinhTam`
 * phải cho **cùng kết quả** để thu ngân không thấy một đằng, in ra một nẻo. Và `thanhPayload` **không được**
 * gửi tổng tiền — nhận tiền từ client là mở đường sửa giá.
 */
function tab(du: Partial<TabHoaDon> = {}): TabHoaDon {
  return {
    clientBillId: "11111111-1111-4111-8111-111111111111",
    lines: [],
    discountAmount: 0,
    pointsUsed: 0,
    customerGivenAmount: 0,
    payments: [],
    trangThai: "draft",
    ...du,
  };
}

describe("tinhTam", () => {
  it("2 món × 15.000, thu đủ tiền mặt ⇒ phải trả 30.000, không nợ không thối", () => {
    const tong = tinhTam(
      tab({
        lines: [{ sku: "SP", quantity: 2, unitPrice: 15000, discountAmount: 0 }],
        payments: [{ method: 1, amount: 30000 }],
      }),
    );

    expect(tong.tienHang).toBe(30000);
    expect(tong.tienVat).toBe(0);
    expect(tong.phaiTra).toBe(30000);
    expect(tong.daTra).toBe(30000);
    expect(tong.conNo).toBe(0);
    expect(tong.tienThua).toBe(0);
  });

  it("VAT tính trên THÀNH TIỀN DÒNG sau chiết khấu dòng", () => {
    // 100.000 − 10.000 = 90.000; VAT 8% = 7.200
    const tong = tinhTam(
      tab({ lines: [{ sku: "SP", quantity: 1, unitPrice: 100000, discountAmount: 10000, vatPercent: 8 }] }),
    );

    expect(tong.chietKhauDong).toBe(10000);
    expect(tong.tienVat).toBeCloseTo(7200, 2);
    expect(tong.phaiTra).toBeCloseTo(97200, 2);
  });

  it("chiết khấu TỔNG và tiền điểm trừ ở bước cuối; `phải trả` không âm", () => {
    const tong = tinhTam(
      tab({
        lines: [{ sku: "SP", quantity: 1, unitPrice: 100000, discountAmount: 0 }],
        discountAmount: 20000,
        pointsUsed: 10,
      }),
    );

    expect(tong.chietKhauHoaDon).toBe(20000);
    expect(tong.tienDiem).toBe(10 * TY_LE_DIEM);
    expect(tong.phaiTra).toBe(70000);

    // điểm/chiết khấu vượt tiền hàng ⇒ 0, KHÔNG âm (server cũng từ chối; UI hiện âm là báo số vô nghĩa)
    const am = tinhTam(
      tab({ lines: [{ sku: "SP", quantity: 1, unitPrice: 1000, discountAmount: 0 }], discountAmount: 5000 }),
    );
    expect(am.phaiTra).toBe(0);
  });

  it("tiền thối và còn nợ là HAI nhánh loại trừ nhau", () => {
    const thoi = tinhTam(
      tab({
        lines: [{ sku: "SP", quantity: 1, unitPrice: 30000, discountAmount: 0 }],
        payments: [{ method: 1, amount: 500000 }],
      }),
    );
    expect(thoi.tienThua).toBe(470000);
    expect(thoi.conNo).toBe(0);

    const no = tinhTam(
      tab({
        lines: [{ sku: "SP", quantity: 1, unitPrice: 30000, discountAmount: 0 }],
        payments: [{ method: 1, amount: 10000 }],
      }),
    );
    expect(no.tienThua).toBe(0);
    expect(no.conNo).toBe(20000);
  });
});

describe("thanhPayload", () => {
  it("KHÔNG gửi tổng tiền do client tính (server là nguồn sự thật)", () => {
    const payload = thanhPayload(
      tab({
        lines: [{ sku: "SP", quantity: 2, unitPrice: 15000, discountAmount: 0, productName: "Bánh" }],
        payments: [{ method: 1, amount: 30000 }],
      }),
      "kho-1",
      "ca-1",
    ) as unknown as Record<string, unknown>;

    expect(payload).not.toHaveProperty("totalPayment");
    expect(payload).not.toHaveProperty("paidAmount");
    expect(payload).not.toHaveProperty("debtAmount");

    expect(payload.clientBillId).toBe("11111111-1111-4111-8111-111111111111");
    expect(payload.warehouseId).toBe("kho-1");
    expect(payload.cashSessionId).toBe("ca-1");
    expect(payload.lines).toEqual([
      { sku: "SP", quantity: 2, unitPrice: 15000, discountAmount: 0, vatPercent: null, productName: "Bánh" },
    ]);
    expect(payload.payments).toEqual([{ method: 1, amount: 30000, referenceCode: null }]);
  });

  it("`dueDate` (ngày VN) đổi sang UTC **cuối ngày** — không lùi mất một ngày", () => {
    const payload = thanhPayload(tab({ dueDate: "2026-11-10" }), "kho-1") as unknown as {
      dueDateUtc: string | null;
    };

    expect(payload.dueDateUtc).toBe("2026-11-10T16:59:59.000Z");
  });
});
