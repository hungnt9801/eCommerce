import { expect, test } from "@playwright/test";
import { ADMIN_EMAIL, ADMIN_PASSWORD, BUSINESS } from "../support/env";

/**
 * **Luồng ① — Đăng nhập** (`07 §WP-09`, `04 §5` màn #1).
 *
 * Đo **hai chiều**: sai mật khẩu phải **nói ra lý do** (không phải màn hình trắng/`500`), đúng thì vào
 * Dashboard **4 KPI**. Kèm một khẳng định bảo mật của `D-12`: **token không nằm ở `localStorage`**.
 */
test.describe("① Đăng nhập", () => {
  test("sai mật khẩu ⇒ báo lỗi · đúng ⇒ vào Tổng quan (4 KPI)", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByText("eCommerce Admin")).toBeVisible();

    // ── Chiều SAI ────────────────────────────────────────────────────────────────────────────────────
    await page.locator('input[name="username"]').fill(ADMIN_EMAIL);
    await page.locator('input[name="password"]').fill("mat-khau-sai-e2e");
    await page.getByRole("button", { name: "Đăng nhập" }).click();

    await expect(page).toHaveURL(/\/login\?error=invalid/);
    await expect(page.getByText(/Sai email hoặc mật khẩu/)).toBeVisible();

    // ── Chiều ĐÚNG ───────────────────────────────────────────────────────────────────────────────────
    await page.locator('input[name="password"]').fill(ADMIN_PASSWORD);
    await page.getByRole("button", { name: "Đăng nhập" }).click();

    await expect(page).toHaveURL(new RegExp(`/${BUSINESS}$`));
    await expect(page.getByRole("heading", { name: "Tổng quan" })).toBeVisible();

    for (const nhan of ["Doanh thu hôm nay", "Hoá đơn bán lẻ", "Đơn hàng", "Tồn khả dụng"]) {
      // 📌 `.first()`: cùng chữ này còn ở **menu trái** ("Hoá đơn bán lẻ", "Đơn hàng", "Tồn kho") — tìm
      // theo chữ mà không giới hạn là bắt strict-mode đỏ dù trang đúng. Ở đây kiểm **thẻ KPI** hiện ra.
      await expect(page.getByText(nhan, { exact: true }).first()).toBeVisible();
    }

    // 🔒 `D-12`: token chỉ sống trong cookie httpOnly của Auth.js — access token KHÔNG được nằm ở localStorage.
    const khoaLocal = await page.evaluate(() => Object.keys(window.localStorage));
    expect(khoaLocal.join(",").toLowerCase()).not.toContain("token");

    // Và cookie session phải là httpOnly (đọc từ `document.cookie` sẽ KHÔNG thấy nó).
    const cookieJs = await page.evaluate(() => document.cookie);
    expect(cookieJs).not.toContain("session-token");
  });
});
