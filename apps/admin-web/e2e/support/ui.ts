/**
 * Đường đi **của người dùng** trong bộ E2E — đăng nhập, URL theo doanh nghiệp, định dạng số/ngày.
 *
 * 🔒 `dangNhap` cố ý dùng **form + nút bấm** (không gọi `signIn()` bằng JS): luồng ① đo đúng chuỗi
 * form → server action → OpenIddict → cookie httpOnly. Gọi tắt bằng JS thì mất luôn luồng đó và bộ E2E
 * sẽ "xanh" ngay cả khi form đăng nhập hỏng.
 */
import { expect, type Page } from "@playwright/test";
import { ADMIN_EMAIL, ADMIN_PASSWORD, BUSINESS } from "./env";

/** `/{business}...` — mọi route Web Admin đều có doanh nghiệp trên **path** (`D-11`). */
export const duong = (p: string): string => `/${BUSINESS}${p}`;

export async function dangNhap(page: Page): Promise<void> {
  await page.goto("/login");
  await page.locator('input[name="username"]').fill(ADMIN_EMAIL);
  await page.locator('input[name="password"]').fill(ADMIN_PASSWORD);
  await page.getByRole("button", { name: "Đăng nhập" }).click();

  await expect(page).toHaveURL(new RegExp(`/${BUSINESS}$`), { timeout: 30_000 });
  await expect(page.getByRole("heading", { name: "Tổng quan" })).toBeVisible();
}

/** Hôm nay theo **giờ kinh doanh Việt Nam** (`D-47` ④) — cùng công thức `todayVn()` của app. */
export function homNayVn(): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
}

/** Số theo `vi-VN` — **cùng** `Intl.NumberFormat` mà UI dùng (`lib/format.ts`). */
export function soVn(n: number): string {
  return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(n);
}

/** Số lượng theo `vi-VN` (tối đa 3 số lẻ — `D-31`). */
export function soLuongVn(n: number): string {
  return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 3 }).format(n);
}
