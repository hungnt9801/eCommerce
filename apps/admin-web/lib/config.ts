/**
 * Cấu hình môi trường của BFF (chỉ **server** — `D-12`: token không bao giờ ra trình duyệt).
 *
 * 🚨 `API_BASE_URL` phải trỏ tới **HTTP 54275** ở dev (D-16), không phải https 54274:
 * cert dev là self-signed ⇒ `fetch` của Node sẽ từ chối (`UNABLE_TO_VERIFY_LEAF_SIGNATURE`).
 *
 * 📌 **Bên trong container (`WP-10`)** thì `localhost` là **chính container**, không phải máy dev
 * ⇒ `docker-compose.yml` đặt `host.docker.internal`. Đây là lý do hai chỗ có **giá trị mặc định khác nhau**
 * nhưng **cùng một tên biến** — đừng "hợp nhất" chúng thành một hằng số.
 */
export const API_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:54275";

/**
 * Issuer OpenIddict (nguồn cấp token). Để **trống** ⇒ dùng `API_BASE_URL` (giữ nguyên hành vi `N1…N10`).
 *
 * 🔑 **Vì sao tách khỏi `API_BASE_URL`:** khi deploy, API có thể nằm sau gateway nội bộ
 * (`http://ecommerce-host:8080`) trong khi issuer công khai mà client thấy là `https://api.example.com`.
 * Token vẫn phải xin ở **issuer** — trộn hai khái niệm vào một biến là mầm lỗi *"đăng nhập được ở dev,
 * chết ở production"*.
 */
export const AUTH_ISSUER = (process.env.AUTH_ISSUER ?? "").trim().replace(/\/+$/, "") || API_BASE_URL;

/** Endpoint cấp/refresh token — **một** chỗ dựng URL (đừng rải `${...}/connect/token` khắp nơi). */
export const TOKEN_ENDPOINT = `${AUTH_ISSUER}/connect/token`;

export const OAUTH_CLIENT_ID = process.env.OAUTH_CLIENT_ID ?? "eCommerce_App";
export const OAUTH_CLIENT_SECRET = process.env.OAUTH_CLIENT_SECRET ?? "1q2w3e*";
export const OAUTH_SCOPE = process.env.OAUTH_SCOPE ?? "eCommerce offline_access";

export const DEFAULT_BUSINESS = process.env.DEFAULT_BUSINESS ?? "230222";

/**
 * Trần thời gian chờ `/health-status` của Host (`/api/health`, `WP-10`).
 *
 * 🚨 **Bắt buộc có trần:** "Host chết" không chỉ là *connection refused* (trả về ngay) mà còn là
 * **Host treo** (nhận kết nối rồi không trả lời — ví dụ DB pool cạn). Không có trần thì endpoint health
 * **treo theo** và nó thôi làm được việc duy nhất nó phải làm: trả lời trong vài giây.
 */
export const HEALTH_TIMEOUT_MS = Number(process.env.HEALTH_TIMEOUT_MS ?? 3000);
