import { describe, expect, it } from "vitest";
import {
  PLACEHOLDER_NAMES,
  canhBaoThieuTrang,
  coNgoacNhonKhongDong,
  dinhDangNgayGio,
  duocDuyetLo,
  kiemTraNoiDung,
  laLoiCauHinh,
  locTemplateTheoMucDich,
  moTaLoiThongBao,
  nhanKenh,
  nhanMucDich,
  nhanLoaiSuKien,
  nhanTrangThaiHangDoi,
  nhanTrangThaiLo,
  nhanTrangThaiTemplate,
  placeholdersSuDung,
  placeholderNgoaiWhitelist,
  rutTenPlaceholder,
  templateChuaPublish,
  toPage,
  hopLeTotalItems,
  taoQueueQueryString,
  TOTAL_ITEMS_MAX,
  TOTAL_ITEMS_MIN,
} from "./notification";

/**
 * **Hàm thuần của màn thông báo** (`lib/notification.ts`) — `NOT-001` / `WP-22`.
 *
 * 📌 **Vị trí tệp:** đặt cạnh nguồn (`lib/notification.test.ts`) thay vì `test/` vì vùng ghi của vai `fe`
 * (`.claude/scopes.json`) **DENY** `apps/admin-web/test/` (việc của QA). `vitest.config.ts` mở rộng `include`
 * để chạy cả test trong `lib/`. QA có thể chuyển sang `test/` khi cập nhật vùng ghi.
 *
 * 🚨 Vì sao test này tồn tại: các quy tắc dưới đây là **hợp đồng nghiệp vụ**, không phải chi tiết hiển thị.
 * Sai một cái là hoặc gửi ra người thật (`duocDuyetLo`), hoặc bị BE ném `0015` (`placeholder`), hoặc hiển thị
 * "rỗng" thay vì "lỗi cấu hình" (`templateChuaPublish`/`laLoiCauHinh`).
 */
describe("nhãn enum (tiếng Việt, từ số)", () => {
  it("kênh, mục đích, trạng thái template, trạng thái hàng đợi, trạng thái lô", () => {
    expect(nhanKenh(0)).toBe("Email");
    expect(nhanKenh(1)).toBe("SMS");
    expect(nhanKenh(2)).toBe("Zalo OA");
    expect(nhanKenh(null)).toBe("—");

    expect(nhanMucDich(0)).toBe("Giao dịch");
    expect(nhanMucDich(1)).toBe("Marketing");
    expect(nhanMucDich(2)).toBe("Cảnh báo vận hành");

    expect(nhanTrangThaiTemplate(0)).toBe("Nháp");
    expect(nhanTrangThaiTemplate(1)).toBe("Đã publish");
    expect(nhanTrangThaiTemplate(2)).toBe("Lưu trữ");

    expect(nhanTrangThaiHangDoi(2)).toBe("Chờ gửi");
    expect(nhanTrangThaiHangDoi(7)).toBe("Lỗi");

    expect(nhanTrangThaiLo(0)).toBe("Chờ duyệt");
    expect(nhanTrangThaiLo(3)).toBe("Đã bàn giao");
  });

  it("giá trị lạ ⇒ in chính số đó (không nuốt lỗi)", () => {
    expect(nhanKenh(99)).toBe("99");
    expect(nhanMucDich(undefined)).toBe("—");
  });
});

describe("nhãn lỗi eCommerce:Notification:00xx", () => {
  it("0012 là LỖI CẤU HÌNH và nêu hướng dẫn publish (không phải 'chưa có dữ liệu')", () => {
    const cau = moTaLoiThongBao("eCommerce:Notification:0012");
    expect(cau).toContain("LỖI CẤU HÌNH");
    expect(cau).toContain("publish");
    expect(laLoiCauHinh("eCommerce:Notification:0012")).toBe(true);
  });

  it("0015 nói rõ KHÔNG gõ tay {{...}}; 0016 nói bất biến", () => {
    expect(moTaLoiThongBao("eCommerce:Notification:0015")).toContain("KHÔNG gõ tay");
    expect(moTaLoiThongBao("eCommerce:Notification:0016")).toContain("BẤT BIẾN");
  });

  it("mã lạ/rỗng ⇒ null (UI tự hiển thị mã thô)", () => {
    expect(moTaLoiThongBao("eCommerce:Notification:9999")).toBeNull();
    expect(moTaLoiThongBao(null)).toBeNull();
    expect(laLoiCauHinh("eCommerce:Notification:0011")).toBe(false);
  });
});

describe("placeholder {{Ten}} — chống 0015", () => {
  it("rút tên, trim, giữ thứ tự, khử trùng lặp", () => {
    expect(rutTenPlaceholder("Xin chào {{ TenKhach }} đơn {{MaDon}} và {{MaDon}}")).toEqual([
      "TenKhach",
      "MaDon",
    ]);
    expect(rutTenPlaceholder(null)).toEqual([]);
    expect(rutTenPlaceholder("không có gì")).toEqual([]);
  });

  it("phát hiện tên ngoài whitelist", () => {
    expect(placeholderNgoaiWhitelist("Chào {{TenKhach}}", PLACEHOLDER_NAMES)).toEqual([]);
    expect(placeholderNgoaiWhitelist("Chào {{BiaDat}}", PLACEHOLDER_NAMES)).toEqual(["BiaDat"]);
  });

  it("phát hiện {{ chưa đóng (đang gõ dở)", () => {
    expect(coNgoacNhonKhongDong("nội dung {{MaDon")).toBe(true);
    expect(coNgoacNhonKhongDong("nội dung {{MaDon}}")).toBe(false);
    expect(coNgoacNhonKhongDong("không có")).toBe(false);
  });

  it("kiemTraNoiDung gộp tiêu đề + nội dung", () => {
    expect(kiemTraNoiDung("Đơn {{MaDon}}", "Chào {{TenKhach}}")).toEqual({
      ngoaiWhitelist: [],
      ngoacNhonMo: false,
    });
    const r = kiemTraNoiDung("Đơn {{SaiTen}}", "mở {{MaDon");
    expect(r.ngoaiWhitelist).toEqual(["SaiTen"]);
    expect(r.ngoacNhonMo).toBe(true);
  });

  it("placeholdersSuDung chỉ gửi whitelist ĐÚNG placeholder đã dùng (không gửi cả danh mục)", () => {
    const ds = placeholdersSuDung("Đơn {{MaDon}}", "Tổng {{TongTien}}");
    expect(ds.map((p) => p.name)).toEqual(["MaDon", "TongTien"]);
    expect(ds.find((p) => p.name === "TongTien")?.kind).toBe(1); // Money
    expect(placeholdersSuDung("không placeholder", "")).toEqual([]);
  });
});

describe("nghiệp vụ", () => {
  it("D-268: màn lô gửi lọc Purpose = Marketing phía client", () => {
    const items = [{ purpose: 0 }, { purpose: 1 }, { purpose: 1 }, { purpose: 2 }];
    expect(locTemplateTheoMucDich(items, 1)).toHaveLength(2);
    expect(locTemplateTheoMucDich(items, 1).every((x) => x.purpose === 1)).toBe(true);
    expect(locTemplateTheoMucDich(items, null)).toHaveLength(4);
  });

  it("cảnh báo thiếu trang khi tổng > số đã tải", () => {
    expect(canhBaoThieuTrang(120, 50)).toBe(true);
    expect(canhBaoThieuTrang(50, 50)).toBe(false);
    expect(canhBaoThieuTrang(0, 0)).toBe(false);
  });

  it("người tạo lô KHÔNG được tự duyệt (D-198)", () => {
    const lo = { createdByUserId: "u-1" };
    expect(duocDuyetLo(lo, "u-1")).toBe(false);
    expect(duocDuyetLo(lo, "u-2")).toBe(true);
    // Chưa biết user hiện tại ⇒ không tự khoá ở UI; server vẫn chặn.
    expect(duocDuyetLo(lo, null)).toBe(true);
    expect(duocDuyetLo(null, "u-1")).toBe(false);
  });

  it("template chưa publish ⇒ lỗi cấu hình, không phải rỗng", () => {
    expect(templateChuaPublish({ currentPublishedVersionId: null })).toBe(true);
    expect(templateChuaPublish({ currentPublishedVersionId: undefined })).toBe(true);
    expect(templateChuaPublish({ currentPublishedVersionId: "v-1" })).toBe(false);
  });
});

describe("định dạng ngày giờ Việt Nam", () => {
  it("dd/MM/yyyy HH:mm theo giờ VN (UTC+7, không DST)", () => {
    // 17:30Z ⇒ 00:30 hôm sau giờ VN.
    expect(dinhDangNgayGio("2026-11-04T17:30:00.000Z")).toBe("05/11/2026 00:30");
    expect(dinhDangNgayGio("2026-11-04T03:05:00.000Z")).toBe("04/11/2026 10:05");
  });

  it("rỗng/không hợp lệ ⇒ —", () => {
    expect(dinhDangNgayGio(null)).toBe("—");
    expect(dinhDangNgayGio("không phải ngày")).toBe("—");
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

describe("hàng đợi — nhãn timeline + query + số lượng lô", () => {
  it("nhãn loại sự kiện (timeline) tiếng Việt", () => {
    expect(nhanLoaiSuKien(0)).toBe("Xếp hàng");
    expect(nhanLoaiSuKien(3)).toBe("Đã bàn giao");
    expect(nhanLoaiSuKien(10)).toBe("Lô được duyệt");
    expect(nhanLoaiSuKien(99)).toBe("99");
    expect(nhanLoaiSuKien(null)).toBe("—");
  });

  it("taoQueueQueryString đúng tên tham số hợp đồng, bỏ tham số rỗng", () => {
    const s = taoQueueQueryString({
      status: 2,
      channelKind: 0,
      tuNgayUtc: "2026-11-01T00:00:00.000Z",
      denNgayUtc: "2026-11-30T23:59:59.999Z",
      page: 3,
      pageSize: 50,
    });
    const q = new URLSearchParams(s);
    expect(q.get("Status")).toBe("2");
    expect(q.get("ChannelKind")).toBe("0");
    expect(q.get("TuNgayUtc")).toBe("2026-11-01T00:00:00.000Z");
    expect(q.get("DenNgayUtc")).toBe("2026-11-30T23:59:59.999Z");
    expect(q.get("Page")).toBe("3");
    expect(q.get("PageSize")).toBe("50");

    const rong = new URLSearchParams(taoQueueQueryString({ status: null, channelKind: null }));
    expect(rong.has("Status")).toBe(false);
    expect(rong.has("ChannelKind")).toBe(false);
    expect(rong.get("Page")).toBe("1");
    expect(rong.get("PageSize")).toBe("50");
  });

  it("hopLeTotalItems: chỉ nhận số nguyên 1..50000", () => {
    expect(hopLeTotalItems(TOTAL_ITEMS_MIN)).toBe(true);
    expect(hopLeTotalItems(TOTAL_ITEMS_MAX)).toBe(true);
    expect(hopLeTotalItems(0)).toBe(false);
    expect(hopLeTotalItems(TOTAL_ITEMS_MAX + 1)).toBe(false);
    expect(hopLeTotalItems(1.5)).toBe(false);
    expect(hopLeTotalItems(Number.NaN)).toBe(false);
  });
});
