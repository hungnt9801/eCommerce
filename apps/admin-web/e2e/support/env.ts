/**
 * Cấu hình của bộ E2E (`WP-09`) — **một chỗ duy nhất** cho địa chỉ + tài khoản.
 *
 * 📌 Mọi giá trị đọc từ biến môi trường có **mặc định đúng của dev** (`D-16`: HTTP 54275) để
 * `pnpm e2e` chạy được ngay trên máy đã `docker compose up -d`.
 */
export const WEB = process.env.E2E_WEB ?? "http://localhost:3000";
export const API = process.env.E2E_API ?? "http://localhost:54275";
export const BUSINESS = process.env.E2E_BUSINESS ?? "230222";

/** Tài khoản seed của `DbMigrator` (dev). Trên production thì truyền qua biến môi trường. */
export const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? "admin@abp.io";
export const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? "1q2w3E*";

/** Client OpenIddict seed cho BFF — dùng cho các lời gọi **setup** (không phải đường người dùng). */
export const OAUTH_CLIENT_ID = process.env.OAUTH_CLIENT_ID ?? "eCommerce_App";
export const OAUTH_CLIENT_SECRET = process.env.OAUTH_CLIENT_SECRET ?? "1q2w3e*";
export const OAUTH_SCOPE = process.env.OAUTH_SCOPE ?? "eCommerce offline_access";

/** Container PostgreSQL của `docker-compose.yml` (dùng cho seed SQL — xem `e2e/support/psql.ts`). */
export const PG_CONTAINER = process.env.E2E_PG_CONTAINER ?? "ecommerce-postgres";
export const PG_USER = process.env.E2E_PG_USER ?? "ecommerce";
export const PG_DATABASE = process.env.E2E_PG_DATABASE ?? "ecommerce";

/**
 * Mã của lượt chạy — mọi dữ liệu E2E mang tiền tố này ⇒ chạy lại nhiều lần **không** đụng nhau
 * (kho/SKU/phiếu phải duy nhất trên toàn tenant).
 *
 * 🚨 **SINH Ở `global-setup.ts` VÀ GHI VÀO `e2e/.run/state.json`**, không sinh ở đây: mỗi worker của
 * Playwright là **một tiến trình riêng**, nên một hằng số sinh lúc nạp module sẽ khác nhau giữa
 * globalSetup và từng test ⇒ test tìm sản phẩm của lượt khác và đỏ vì lý do sai.
 */
export function taoRunId(): string {
  return `${new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14)}${Math.floor(Math.random() * 900 + 100)}`;
}
