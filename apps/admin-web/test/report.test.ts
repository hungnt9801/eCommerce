import { describe, expect, it } from "vitest";
import {
  buildCsv,
  csvFileName,
  lastDays,
  NGUON_VALUE,
  rangeQuery,
  revenueQuery,
} from "../lib/report";

/**
 * **Hợp đồng của báo cáo FE** (`WP-08`, `WP-09`): CSV + tham số truy vấn.
 *
 * 🚨 Ba lỗi **im lặng** được chặn ở đây:
 * ① thiếu **BOM** ⇒ Excel bản Việt mở CSV thành `B?o c?o` (người dùng tưởng dữ liệu hỏng);
 * ② **bỏ mất `nguon = 0`** vì kiểm truthy (`0` là *bán lẻ*, giá trị **hợp lệ**) ⇒ báo cáo lặng lẽ đổi phạm vi;
 * ③ ô chứa `;`/`"`/xuống dòng **không được bọc** ⇒ CSV lệch cột và Excel đọc sai từ dòng đó trở đi.
 */
describe("buildCsv (D-36 ③)", () => {
  it("có BOM UTF-8 và phân cách `;` + CRLF", () => {
    const csv = buildCsv(["Kỳ", "Tổng"], [["2026-11-04", "30000"]]);

    expect(csv.charCodeAt(0)).toBe(0xfeff);
    expect(csv.slice(1)).toBe("Kỳ;Tổng\r\n2026-11-04;30000\r\n");
  });

  it("bọc ô có `;`, `\"` hoặc xuống dòng — và nhân đôi dấu nháy", () => {
    const csv = buildCsv(["A", "B"], [['có;chấm', 'nháy "kép"'], ["dòng\nmới", "thường"]]);

    expect(csv).toContain('"có;chấm";"nháy ""kép"""');
    expect(csv).toContain('"dòng\nmới";thường');
  });

  it("bảng rỗng vẫn có dòng tiêu đề (Excel không mở ra tệp trắng)", () => {
    const csv = buildCsv(["A", "B"], []);

    expect(csv.slice(1)).toBe("A;B\r\n");
  });
});

describe("tham số truy vấn báo cáo", () => {
  it("`nguon = 0` (bán lẻ) KHÔNG bị bỏ — 0 là giá trị hợp lệ", () => {
    expect(revenueQuery({ tu: "2026-11-01", den: "2026-11-04", nguon: NGUON_VALUE.pos })).toBe(
      "tuNgay=2026-11-01&denNgay=2026-11-04&nguon=0",
    );

    // và `nguon = 1` (đơn hàng) cũng phải đi
    expect(revenueQuery({ nguon: NGUON_VALUE.order })).toBe("nguon=1");
    // "cả hai" = KHÔNG có tham số
    expect(revenueQuery({})).toBe("");
  });

  it("`theoThang` chỉ xuất hiện khi bật; `rangeQuery` không gửi tham số rỗng", () => {
    expect(revenueQuery({ theoThang: true })).toBe("theoThang=true");
    expect(revenueQuery({ theoThang: false })).toBe("");
    expect(rangeQuery({ tu: "2026-11-01", den: "" })).toBe("tuNgay=2026-11-01");
  });

  it("`lastDays(7)` gồm CẢ hôm nay ⇒ lùi 6 ngày", () => {
    expect(lastDays(7, "2026-11-04")).toEqual({ tu: "2026-10-29", den: "2026-11-04" });
    expect(lastDays(1, "2026-11-04")).toEqual({ tu: "2026-11-04", den: "2026-11-04" });
    // qua ranh giới tháng/năm — chỗ dễ sai khi trừ ngày bằng tay
    expect(lastDays(3, "2026-01-01")).toEqual({ tu: "2025-12-30", den: "2026-01-01" });
  });

  it("tên tệp CSV có kỳ ⇒ hai lần xuất không ghi đè nhau", () => {
    expect(csvFileName("doanh-thu", { tu: "2026-11-01", den: "2026-11-04" })).toBe(
      "doanh-thu-2026-11-01-2026-11-04.csv",
    );
    expect(csvFileName("ton-kho", {})).toBe("ton-kho-dau-nay.csv");
  });
});
