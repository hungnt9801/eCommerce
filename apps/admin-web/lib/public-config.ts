/**
 * Cấu hình **công khai** — giá trị này đi vào bundle trình duyệt, nên **chỉ** chứa thứ không bí mật.
 *
 * 🔒 `D-12`: token, `AUTH_SECRET`, `OAUTH_CLIENT_SECRET` **không bao giờ** được đặt tiền tố `NEXT_PUBLIC_`.
 * Nếu một ngày cần thêm biến ở đây, câu hỏi kiểm tra là: *"lộ nó trên DevTools có sao không?"* —
 * câu trả lời phải là **không sao** thì mới thêm.
 */
export const APP_NAME = process.env.NEXT_PUBLIC_APP_NAME ?? "eCommerce";

/**
 * ⚠️ `NEXT_PUBLIC_*` được **NHÚNG LÚC BUILD**, không đọc lúc chạy.
 *
 * 📌 Hệ quả đo được ở `WP-10`: đặt `NEXT_PUBLIC_APP_NAME` bằng `environment:` của compose **không có tác dụng**
 * — phải truyền qua `build.args` (xem `apps/admin-web/Dockerfile` + `docker-compose.yml`).
 * Biến **server** (`API_BASE_URL`, `AUTH_*`, `HEALTH_TIMEOUT_MS`) thì ngược lại: đọc lúc chạy, không cần build lại.
 */
