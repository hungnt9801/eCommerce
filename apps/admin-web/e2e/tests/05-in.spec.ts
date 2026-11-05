import { expect, test } from "@playwright/test";
import { BUSINESS } from "../support/env";
import { batBuoc } from "../support/state";
import { dangNhap, soVn } from "../support/ui";

/**
 * **Luồng ⑤ — In hoá đơn** (`07 §WP-09`, `04 §5` màn #12 · `WP-06`).
 *
 * Ba điều chỉ đo được trên **trang in thật**:
 * ① số hoá đơn + **tổng phải trả** in ra **khớp chứng từ** (số đọc từ DTO của server, UI không tính lại);
 * ② nút **In** gọi `window.print()` (đo bằng cách **thay** `window.print` — in thật trong headless là no-op);
 * ③ trang tự bật hộp thoại in **một lần** sau khi render (`PrintButton`, chờ 400 ms cho font/bảng).
 */
test.describe("⑤ In hoá đơn", () => {
  test("trang in đúng số/tổng tiền và gọi window.print()", async ({ page }) => {
    // 📌 Thay `window.print` TRƯỚC khi trang chạy: headless in thật không có tác dụng, đo "đã gọi" mới có nghĩa.
    await page.addInitScript(() => {
      (window as unknown as { __soLanIn: number }).__soLanIn = 0;
      window.print = () => {
        (window as unknown as { __soLanIn: number }).__soLanIn += 1;
      };
    });

    await dangNhap(page);

    const hoaDon = batBuoc("hoaDon", "④ bán lẻ");

    await page.goto(`/${BUSINESS}/print/bill/${hoaDon.id}`);

    // 📌 Phải SCOPE vào khung in: chữ "HOÁ ĐƠN BÁN LẺ" trùng (không phân biệt hoa/thường) với mục menu
    // *"Hoá đơn bán lẻ"* ở sidebar ⇒ tìm toàn trang là strict-mode đỏ dù trang in đúng.
    const khungIn = page.locator("div.p-4").filter({ hasText: "HOÁ ĐƠN BÁN LẺ" }).first();
    await expect(khungIn).toBeVisible();
    await expect(khungIn).toContainText(hoaDon.number);
    await expect(khungIn).toContainText("PHẢI TRẢ");
    await expect(khungIn).toContainText(`${soVn(hoaDon.total)} đ`);

    // ③ `PrintButton` tự gọi `window.print()` một lần sau ~400 ms.
    await expect
      .poll(async () => page.evaluate(() => (window as unknown as { __soLanIn: number }).__soLanIn), {
        timeout: 10_000,
      })
      .toBeGreaterThan(0);

    // ② Bấm nút "In" ⇒ gọi thêm đúng một lần (nút còn sống, không bị `useEffect` vô hiệu hoá).
    const truoc = await page.evaluate(() => (window as unknown as { __soLanIn: number }).__soLanIn);
    await page.getByRole("button", { name: "In", exact: true }).click();
    const sau = await page.evaluate(() => (window as unknown as { __soLanIn: number }).__soLanIn);
    expect(sau).toBe(truoc + 1);
  });
});
