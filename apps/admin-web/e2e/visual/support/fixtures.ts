/**
 * **FIXTURE TẤT ĐỊNH CHO 7 ROUTE ĐỘNG** (`UI-41` / `U41-3`, `D-333`).
 *
 * 🔑 Nguyên tắc:
 *  - Dữ liệu nền dựng bằng **API thật** (`/api/v1/*`, `/api/app/warehouse`) — không mock, không stub;
 *  - **Đơn hàng** dùng lại **ngoại lệ SQL duy nhất đã duyệt `D-49`** (`e2e/support/don-hang.ts`) — KHÔNG
 *    thêm câu SQL nào khác (`D-333` ⑥: thêm `psql` ghi ngoài `D-49` ⇒ review đỏ);
 *  - Phần nào không dựng được ⇒ ghi vào `loi[<route>]` để spec `test.skip` + testplan ghi
 *    "chưa chụp được" — **tuyệt đối không** chụp ảnh giả (route không có dữ liệu thật).
 *
 * 📌 `runId` do `e2e/global-setup.ts` sinh và ghi `e2e/.run/state.json` — **không** tự sinh lại ở đây
 * (mỗi worker là một tiến trình, hằng số nạp module sẽ khác globalSetup).
 */
import { randomUUID } from "node:crypto";
import { API } from "../../support/env";
import { layToken, nhapKho, taoKho } from "../../support/api";
import { gieoDonHang } from "../../support/don-hang";
import { docTrangThai } from "../../support/state";
import { duong } from "../../support/ui";

export type Fixture = {
  runId: string;
  warehouse: { id: string; code: string; name: string };
  sku: string;
  order?: { id: string; number: string };
  stockBill?: { id: string; number: string };
  retailBill?: { id: string; number: string };
  customer?: { id: string; name: string };
  promotion?: { id: string; code: string; name: string };
  template?: { id: string; code: string; name: string };
  /** Khoá = slug route cần dữ liệu (`07-order-detail`, `15-print-bill`…). */
  loi: Record<string, string>;
};

/** Gọi API thật bằng token seed — **hạ tầng**, không phải đường người dùng (như `support/api.ts`). */
async function goi<T>(method: string, duongDan: string, body?: unknown): Promise<T> {
  const res = await fetch(`${API}${duongDan}`, {
    method,
    headers: {
      Authorization: `Bearer ${await layToken()}`,
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  const raw = await res.text();

  if (!res.ok) {
    throw new Error(`${method} ${duongDan} → HTTP ${res.status}: ${raw.slice(0, 400)}`);
  }

  return (raw.length === 0 ? null : JSON.parse(raw)) as T;
}

function ghiLoi(loi: Record<string, string>, route: string, e: unknown): void {
  loi[route] = e instanceof Error ? e.message : String(e);
}

/**
 * Dựng **một bộ** fixture cho một lượt chạy. Mỗi lời gọi tạo kho riêng ⇒ chạy song song/nhiều lần không
 * đụng nhau (cùng lý do `support/api.ts` `taoKho`).
 */
export async function taoFixture(): Promise<Fixture> {
  const runId = docTrangThai().runId;

  if (!runId) {
    throw new Error(
      "Thiếu runId trong e2e/.run/state.json — `global-setup.ts` chưa chạy. " +
        "Chạy `playwright test -c e2e/visual/playwright.visual.config.ts` (globalSetup kế thừa root config).",
    );
  }

  // 🔑 Hậu tố **mỗi lời gọi** — `02-dong` (ảnh) và `03-axe` (a11y) đều gọi `taoFixture()` trong CÙNG một
  // lượt Playwright, nên mã kho/SKU/phiếu/đơn/KM/mẫu phải khác nhau giữa hai lời gọi. Nếu chỉ dùng `runId`,
  // lời gọi thứ hai đỏ ngay ở `taoKho` (unique `(TenantId, Code)`). Điện thoại cũng phải khác **giữa các
  // lượt chạy** (số cố định `0900000000` đã thuộc khách của lượt trước ⇒ API trả 403 "khách đã tồn tại").
  const nhan = randomUUID().replace(/-/g, "").slice(0, 8);
  const sku = `QA-${runId}-${nhan}`;
  const warehouse = await taoKho(`QA-${runId}-${nhan}`, `Kho QA ${runId} ${nhan}`);
  const fixture: Fixture = { runId, warehouse, sku, loi: {} };

  // Số điện thoại 10 chữ số, **duy nhất** (8 chữ số lấy từ UUID + tiền tố `09`).
  const dienThoai = `09${randomUUID().replace(/\D/g, "").padEnd(8, "0").slice(0, 8)}`;

  // ① Đơn hàng — ngoại lệ SQL DUY NHẤT của `D-49` (đơn chưa có API tạo dùng được).
  try {
    fixture.order = gieoDonHang(sku, `${runId}-${nhan}`);
  } catch (e) {
    ghiLoi(fixture.loi, "07-order-detail", e);
  }

  // ② Phiếu kho (tạo + ghi sổ) — nền cho chi tiết phiếu + mẫu in phiếu.
  try {
    const phieu = await nhapKho({
      warehouseId: warehouse.id,
      sku,
      quantity: 10,
      unitPrice: 15000,
      number: `PN-${runId}-${nhan}`,
    });
    fixture.stockBill = phieu;
  } catch (e) {
    ghiLoi(fixture.loi, "10-inventory-bill-detail", e);
    ghiLoi(fixture.loi, "11-inventory-bill-print", e);
  }

  // ③ Hoá đơn bán lẻ (in khổ hẹp) — cần ca đang mở + tồn vừa nhập.
  try {
    const ca = await goi<{ id: string }>("POST", "/api/v1/pos/sessions/open", {
      warehouseId: warehouse.id,
      openingCash: 0,
    });

    const ketQua = await goi<{ bill: { id: string; number: string } }>("POST", "/api/v1/pos/bills", {
      clientBillId: randomUUID(),
      warehouseId: warehouse.id,
      cashSessionId: ca.id,
      lines: [{ sku, quantity: 1, unitPrice: 15000 }],
      payments: [{ method: 1, amount: 15000 }], // 1 = Cash (`Enums.Pos.cs`)
    });

    fixture.retailBill = ketQua.bill;
  } catch (e) {
    ghiLoi(fixture.loi, "15-print-bill", e);
  }

  // ④ Khách hàng.
  try {
    const khach = await goi<{ id: string; name: string }>("POST", "/api/v1/customers", {
      name: `Khách QA ${runId}`,
      phone: dienThoai,
      idempotencyKey: `kh-${runId}`,
    });
    fixture.customer = { id: khach.id, name: khach.name };
  } catch (e) {
    ghiLoi(fixture.loi, "17-customer-detail", e);
  }

  // ⑤ Khuyến mãi — % phải kèm `maxDiscountAmount` (rule domain; probe 29/11/2026).
  try {
    const batDau = new Date(Date.now() - 24 * 3600 * 1000).toISOString();
    const ketThuc = new Date(Date.now() + 30 * 24 * 3600 * 1000).toISOString();

    const km = await goi<{ id: string; code: string; name: string }>("POST", "/api/v1/promotion", {
      code: `QA-${runId}-${nhan}`,
      name: `KM QA ${runId}`,
      kind: 1, // Discount
      isCumulative: false,
      priority: 1,
      startsAtUtc: batDau,
      endsAtUtc: ketThuc,
      discountKind: 0, // Percentage
      discountValue: 10,
      maxDiscountAmount: 50000,
      currency: "VND",
    });
    fixture.promotion = { id: km.id, code: km.code, name: km.name };
  } catch (e) {
    ghiLoi(fixture.loi, "23-promotion-detail", e);
  }

  // ⑥ Mẫu thông báo (Text — `D-199`).
  try {
    const mau = await goi<{ id: string; code: string; name: string }>("POST", "/api/v1/notifications/templates", {
      code: `QA-${runId}-${nhan}`,
      name: `Mẫu QA ${runId}`,
      channelKind: 0, // Email
      locale: "vi",
      purpose: 0, // Transactional
      bodyFormat: 0, // Text
    });
    fixture.template = { id: mau.id, code: mau.code, name: mau.name };
  } catch (e) {
    ghiLoi(fixture.loi, "26-template-detail", e);
  }

  return fixture;
}

/** Đường dẫn POS gắn **đúng** kho của fixture (nếu để trống, trang chọn kho active đầu tiên — không tất định). */
export function duongPos(fixture: Fixture): string {
  return `${duong("/pos/bill")}?warehouseId=${fixture.warehouse.id}`;
}
