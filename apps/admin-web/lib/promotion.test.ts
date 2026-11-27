import { describe, expect, it } from "vitest";
import {
  type CayNode,
  canhBaoChiPhiSeed,
  demCay,
  dongHoGioiHan,
  hesoChiPhi,
  laDichSetStatusHopLe,
  moTaLoiPromotion,
  mucCanhBao,
  nhanKieuGiam,
  nhanLoai,
  nhanNhom,
  nhanRule,
  nhanTrangThai,
  taoDanhSachQueryString,
  thieuNhanRule,
  toPage,
} from "./promotion";

/**
 * **Hàm thuần của màn khuyến mãi** (`lib/promotion.ts`) — `PRO-002` / `W24-3` (phiên `N95`).
 *
 * 📌 Đặt cạnh nguồn (`lib/promotion.test.ts`) — vùng ghi của vai `fe` (`.claude/scopes.json`) DENY
 * `apps/admin-web/test/` (việc của `qa`); `vitest.config.ts` chạy cả test trong `lib/`.
 *
 * 🚨 Vì sao test này tồn tại: "đồng hồ giới hạn" (`dongHoGioiHan`) là **hợp đồng an toàn** — khoá nút Lưu
 * khi vượt trần. Sai một cái là hoặc bấm Lưu rồi ăn lỗi `0001`/`0002`, hoặc tệ hơn là **không** khoá khi
 * lẽ ra phải khoá. Các ca dưới đây bám đúng yêu cầu `§10.1` + `D-288`/`D-289`.
 */

/** Cây 12 node: 1 nhóm gốc + 11 lá (rule ∉ catalog ⇒ hệ số 1). */
function cay12(): CayNode[] {
  const nodes: CayNode[] = [{ id: "root", parentId: null, isGroup: true }];
  for (let i = 0; i < 11; i++) {
    nodes.push({ id: `l${i}`, parentId: "root", isGroup: false, ruleSystemName: "MinOrderAmount" });
  }
  return nodes;
}

describe("nhãn enum (tiếng Việt, từ số)", () => {
  it("loại / trạng thái / kiểu giảm / nhóm AND-OR", () => {
    expect(nhanLoai(0)).toBe("Mã giảm giá");
    expect(nhanLoai(1)).toBe("Khuyến mãi tự động");
    expect(nhanLoai(2)).toBe("Flash Sale");
    expect(nhanLoai(null)).toBe("—");

    expect(nhanTrangThai(0)).toBe("Nháp");
    expect(nhanTrangThai(2)).toBe("Đang chạy");
    expect(nhanTrangThai(5)).toBe("Đã huỷ");

    expect(nhanKieuGiam(0)).toBe("Phần trăm");
    expect(nhanKieuGiam(1)).toBe("Số tiền cố định");
    expect(nhanKieuGiam(2)).toBe("Miễn phí vận chuyển");

    expect(nhanNhom(0)).toBe("AND");
    expect(nhanNhom(1)).toBe("OR");
  });

  it("giá trị lạ ⇒ in chính số đó (không nuốt lỗi)", () => {
    expect(nhanTrangThai(99)).toBe("99");
    expect(nhanNhom(undefined)).toBe("—");
  });
});

describe("rule catalog — nhãn + cờ seed (D-289/D-290)", () => {
  it("thiếu nhãn khi label rỗng HOẶC trùng systemName (BE fallback)", () => {
    expect(thieuNhanRule({ systemName: "MinOrderAmount", label: "Giá trị đơn tối thiểu" })).toBe(false);
    expect(thieuNhanRule({ systemName: "X", label: "X" })).toBe(true);
    expect(thieuNhanRule({ systemName: "X", label: "" })).toBe(true);
    expect(thieuNhanRule({ systemName: "X", label: null })).toBe(true);
    // Hiển thị thô systemName khi thiếu nhãn — UI tự gắn cảnh báo.
    expect(nhanRule({ systemName: "X", label: "X" })).toBe("X");
    expect(nhanRule({ systemName: "X", label: "Nhãn" })).toBe("Nhãn");
  });

  it("isSeeded === false ⇒ banner đúng câu SRS, KHÔNG khoá nút Lưu", () => {
    const rule = {
      systemName: "MinOrderAmount",
      label: "Giá trị đơn tối thiểu",
      isSeeded: false,
      declaredCostFactor: 1,
    };
    const banner = canhBaoChiPhiSeed(rule);
    expect(banner).toContain("chưa được seed");
    expect(banner).toContain("Giá trị đơn tối thiểu");
    expect(banner).toContain("mặc định 1/false");

    // isSeeded = false KHÔNG tham gia vào khoá Lưu.
    const r = dongHoGioiHan(
      { maxNodes: 64, maxDepth: 8, maxLeaves: 32, maxDeclaredCombinations: 1024 },
      [
        { id: "r", parentId: null, isGroup: true },
        { id: "a", parentId: "r", isGroup: false, ruleSystemName: "MinOrderAmount" },
      ],
      [rule],
    );
    expect(r.khoa).toBe(false);

    // isSeeded = true ⇒ không banner.
    expect(canhBaoChiPhiSeed({ ...rule, isSeeded: true })).toBeNull();
  });
});

describe("declaredCost — tên ∉ catalog ⇒ 1 (khớp TinhDeclaredCost)", () => {
  it("cộng hệ số theo catalog, tên lạ ⇒ 1", () => {
    const catalog = [
      { systemName: "CustomerGroup", declaredCostFactor: 4 },
      { systemName: "MinOrderAmount", declaredCostFactor: 1 },
      { systemName: "HeSoKhong", declaredCostFactor: 0 }, // ≤ 0 ⇒ coi như 1
    ];
    const cay: CayNode[] = [
      { id: "r", parentId: null, isGroup: true },
      { id: "a", parentId: "r", isGroup: false, ruleSystemName: "CustomerGroup" },
      { id: "b", parentId: "r", isGroup: false, ruleSystemName: "KHONG_CO_TRONG_CATALOG" },
      { id: "c", parentId: "r", isGroup: false, ruleSystemName: "HeSoKhong" },
    ];
    expect(demCay(cay, catalog).declaredCost).toBe(4 + 1 + 1);
    expect(hesoChiPhi("KHONG_CO_TRONG_CATALOG", catalog)).toBe(1);
    expect(hesoChiPhi("HeSoKhong", catalog)).toBe(1);
    expect(hesoChiPhi("CustomerGroup", catalog)).toBe(4);
    // Không truyền catalog ⇒ mọi lá hệ số 1.
    expect(demCay(cay).declaredCost).toBe(3);
  });
});

describe("demCay — nodeCount · depth(gốc=1) · leafCount", () => {
  it("gốc = 1, con +1 (khớp server tính lại từ ParentId)", () => {
    const cay: CayNode[] = [
      { id: "r", parentId: null, isGroup: true },
      { id: "g", parentId: "r", isGroup: true },
      { id: "l1", parentId: "g", isGroup: false },
      { id: "l2", parentId: "g", isGroup: false },
    ];
    const so = demCay(cay);
    expect(so.nodeCount).toBe(4);
    expect(so.depth).toBe(3);
    expect(so.leafCount).toBe(2);
  });

  it("cây rỗng ⇒ 0", () => {
    expect(demCay([])).toEqual({ nodeCount: 0, depth: 0, leafCount: 0, declaredCost: 0 });
  });
});

describe("dongHoGioiHan — đồng hồ giới hạn (khoá nút Lưu)", () => {
  const cay = cay12(); // 12 node, 11 lá

  it("12 node với trần {10,5,8,64} ⇒ KHOÁ, câu chứa '10', không hiện mã thô", () => {
    const r = dongHoGioiHan(
      { maxNodes: 10, maxDepth: 5, maxLeaves: 8, maxDeclaredCombinations: 64 },
      cay,
    );
    expect(r.khoa).toBe(true);
    expect(r.lyDo).toContain("10");
    expect(r.lyDo).not.toContain("eCommerce:Promotion");
  });

  it("cùng cây với trần {64,8,32,1024} ⇒ KHÔNG khoá", () => {
    const r = dongHoGioiHan(
      { maxNodes: 64, maxDepth: 8, maxLeaves: 32, maxDeclaredCombinations: 1024 },
      cay,
    );
    expect(r.khoa).toBe(false);
    expect(r.lyDo).toBeNull();
  });

  it("vượt trần tổ hợp khai báo ⇒ câu 0002 + 'tách thành nhiều khuyến mãi nhỏ'", () => {
    const r = dongHoGioiHan(
      { maxNodes: 64, maxDepth: 8, maxLeaves: 32, maxDeclaredCombinations: 5 },
      cay,
    );
    expect(r.khoa).toBe(true);
    expect(r.lyDo).toContain("tách thành nhiều khuyến mãi nhỏ");
    expect(r.canhBaoToHop).toBe("do");
  });

  it("mức cảnh báo: ≥80% = vàng, =100% = đỏ", () => {
    expect(mucCanhBao(8, 10)).toBe("vang");
    expect(mucCanhBao(10, 10)).toBe("do");
    expect(mucCanhBao(7, 10)).toBe("binh-thuong");
    expect(mucCanhBao(12, 10)).toBe("do");
  });
});

describe("lỗi theo mã — không hiện mã thô", () => {
  it("0001/0002 ra câu tiếng Việt, mã lạ ⇒ null", () => {
    expect(moTaLoiPromotion("eCommerce:Promotion:0001")).not.toContain("eCommerce:Promotion");
    expect(moTaLoiPromotion("eCommerce:Promotion:0002")).toContain("tách");
    expect(moTaLoiPromotion("eCommerce:Promotion:0006")).toContain("Nháp");
    expect(moTaLoiPromotion("eCommerce:Promotion:9999")).toBeNull();
    expect(moTaLoiPromotion(null)).toBeNull();
  });
});

describe("set-status chỉ 3 đích hợp lệ (Paused/Active/Cancelled)", () => {
  it("Active(2)/Paused(3)/Cancelled(5) ok; Approved(1)/Expired(4) không", () => {
    expect(laDichSetStatusHopLe(2)).toBe(true);
    expect(laDichSetStatusHopLe(3)).toBe(true);
    expect(laDichSetStatusHopLe(5)).toBe(true);
    expect(laDichSetStatusHopLe(1)).toBe(false);
    expect(laDichSetStatusHopLe(4)).toBe(false);
    expect(laDichSetStatusHopLe(null)).toBe(false);
  });
});

describe("query danh sách — đúng tên tham số hợp đồng", () => {
  it("Status/Kind/Page/PageSize/Sort; bỏ tham số rỗng", () => {
    const q = new URLSearchParams(taoDanhSachQueryString({ status: 2, kind: 1, page: 3, pageSize: 50 }));
    expect(q.get("Status")).toBe("2");
    expect(q.get("Kind")).toBe("1");
    expect(q.get("Page")).toBe("3");
    expect(q.get("PageSize")).toBe("50");

    const rong = new URLSearchParams(taoDanhSachQueryString({ status: null, kind: null }));
    expect(rong.has("Status")).toBe(false);
    expect(rong.has("Kind")).toBe(false);
    expect(rong.get("Page")).toBe("1");
    expect(rong.get("PageSize")).toBe("50");
  });
});

describe("toPage — chuẩn hoá ApiPage", () => {
  it("null ⇒ trang rỗng; có dữ liệu ⇒ giữ nguyên", () => {
    expect(toPage<number>(null)).toEqual({ items: [], total: 0, page: 1, pageSize: 0 });
    expect(toPage({ items: [1, 2], total: 2, page: 1, pageSize: 50 })).toEqual({
      items: [1, 2],
      total: 2,
      page: 1,
      pageSize: 50,
    });
  });
});
