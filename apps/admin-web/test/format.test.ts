import { describe, expect, it } from "vitest";
import { amount, dateOnly, dateTime, int, money, qty, todayVn, vnDateToUtcIso } from "../lib/format";

/**
 * **Định dạng số/ngày (`vi-VN`)** — `lib/format.ts`.
 *
 * 🚨 Vì sao test này tồn tại: `format.ts` khoá `vi-VN` **có chủ ý** — dùng `toLocaleString()` không tham số
 * thì CI (`en-US`) in `1.5` còn máy dev in `1,5`, và cùng một trang render ra hai con số khác nhau. Test
 * khẳng định **đúng chữ số của vi-VN** để đổi locale là đỏ ngay.
 */
describe("định dạng số (vi-VN)", () => {
  it("số lượng: tối đa 3 số lẻ, phân cách nghìn bằng dấu chấm", () => {
    expect(qty(10)).toBe("10");
    expect(qty(1.5)).toBe("1,5");
    expect(qty(0.001)).toBe("0,001");
    expect(qty(1000)).toBe("1.000");
    expect(qty(1234.567)).toBe("1.234,567");
  });

  it("số lượng: `null`/`NaN` ⇒ —, và `-0` không được in ra “-0”", () => {
    expect(qty(null)).toBe("—");
    expect(qty(undefined)).toBe("—");
    expect(qty(Number.NaN)).toBe("—");
    expect(qty(-0)).toBe("0");
    expect(qty(0 - 0)).toBe("0");
  });

  it("tiền (2 số lẻ) và tiền VND (0 số lẻ) là HAI hàm khác nhau", () => {
    expect(amount(1234.5)).toBe("1.234,5");
    expect(amount(1234)).toBe("1.234");
    expect(money(1470000)).toBe("1.470.000");
    // 📌 `money` làm tròn 0 số lẻ — dùng cho VND; `amount` giữ 2 số lẻ cho đơn giá chia theo kg.
    expect(money(1234.56)).toBe("1.235");
    expect(int(1234567)).toBe("1.234.567");
  });
});

describe("định dạng ngày (Asia/Ho_Chi_Minh)", () => {
  it("múi giờ CỐ ĐỊNH: 17:30Z là 00:30 hôm sau theo giờ VN", () => {
    const chuoi = dateTime("2026-11-04T17:30:00.000Z");
    expect(chuoi).toContain("05/11/2026");
    expect(chuoi).toContain("00:30");
  });

  it("ngày không có giờ + giá trị hỏng", () => {
    expect(dateOnly("2026-11-04T10:00:00.000Z")).toBe("04/11/2026");
    expect(dateOnly(null)).toBe("—");
    expect(dateOnly("không-phải-ngày")).toBe("—");
  });

  it("`vnDateToUtcIso`: biên ngày theo +07:00, `cuoiNgay` phủ hết ngày", () => {
    expect(vnDateToUtcIso("2026-11-04")).toBe("2026-11-03T17:00:00.000Z");
    expect(vnDateToUtcIso("2026-11-04", true)).toBe("2026-11-04T16:59:59.999Z");
    expect(vnDateToUtcIso("04/11/2026")).toBeNull();
  });

  it("`todayVn()` trả YYYY-MM-DD (đúng định dạng tham số của báo cáo)", () => {
    expect(todayVn()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    // 📌 Phải là ngày của **giờ VN**, không phải UTC: 17:00Z ngày 04/11 là 05/11 ở VN.
    expect(new Date(`${todayVn()}T00:00:00+07:00`).getTime()).toBeLessThanOrEqual(Date.now());
  });
});
