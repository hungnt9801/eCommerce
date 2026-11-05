/**
 * **Dọn sau khi chạy E2E** — chỉ MỘT việc: **bật lại** cờ "buộc đổi mật khẩu" của `admin`.
 *
 * 🚨 **VÌ SAO PHẢI BẬT LẠI (không phải "cho tiện"):** `global-setup` đã tắt cờ này để đăng nhập được (dev/CI).
 * Nếu để nguyên `false`, thì lần chạy **cổng DB** kế tiếp (`tools/kiem-db.ps1` nhóm ②) sẽ báo:
 * *"ADMIN|false ⇒ quen bat lai = commit mot admin KHONG bi buoc doi mat khau"*.
 * 📌 Đó là **đúng**: cờ này là hàng rào chống tài khoản admin mặc định trên production. Bộ test **không**
 * được phép để lại trạng thái mà cổng an ninh phải đỏ — nếu không, người ta sẽ **vô hiệu hoá cổng** để
 * chạy được test (bài học lặp lại của repo: *"cong luon do thi nguoi ta hoc cach bo qua no"*).
 *
 * 📌 Hệ quả CÓ CHỦ Ý cho dev: sau `pnpm e2e`, đăng nhập bằng trình duyệt sẽ bị **buộc đổi mật khẩu** cho tới
 * lần chạy E2E kế tiếp (setup tắt lại) — đúng trạng thái mặc định của DB sau `DbMigrator`.
 */
import { psql } from "./support/psql";

export default async function globalTeardown(): Promise<void> {
  psql(`update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "UserName" = 'admin';`);

  const conTat = psql(
    `select count(*) from "AbpUsers" where "UserName" = 'admin' and not "ShouldChangePasswordOnNextLogin";`,
  )
    .split(/\r?\n/)[0]
    ?.trim();

  if (conTat !== "0") {
    throw new Error(
      `Không bật lại được cờ "buộc đổi mật khẩu" của admin (còn ${conTat} dòng TẮT) — ` +
        `lần chạy cổng DB sau sẽ đỏ ở nhóm ② và người ta sẽ tưởng cổng hỏng.`,
    );
  }

  console.log('[E2E] đã bật lại cờ "buộc đổi mật khẩu" của admin (giữ nguyên bất biến an ninh của DB).');
}
