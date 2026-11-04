/**
 * **Chuẩn bị MỘT LẦN** cho cả bộ E2E (`WP-09`).
 *
 * Ba việc — mỗi việc đều là một **bẫy đã trả giá** ở các phiên `N5…N9`:
 *
 * ① **Chờ Host lên.** `webServer` của Playwright bật API, nhưng thứ tự giữa `webServer` và `globalSetup`
 *    không phải hợp đồng ổn định ⇒ tự chờ (retry) thay vì tin vào thứ tự.
 * ② **Tắt cờ "buộc đổi mật khẩu" của `admin`** — `DbMigrator` **bật** cờ này (chủ ý an toàn production,
 *    `IdentityDataSeedContributor`). Không tắt ⇒ **8/8 luồng đỏ** ngay ở bước đăng nhập với thông điệp
 *    *"Sai email hoặc mật khẩu"* — **nói sai nguyên nhân**, đúng loại lỗi làm mất nửa ngày.
 * ③ **Sinh `runId`** và ghi vào `e2e/.run/state.json` (mọi dữ liệu của lượt chạy mang tiền tố này).
 *
 * 📌 Không seed nghiệp vụ ở đây: mỗi luồng tự dựng dữ liệu của mình **bằng API thật** (xem `support/api.ts`),
 * trừ đơn hàng (chưa có API tạo — `support/don-hang.ts`).
 */
import fs from "node:fs";
import path from "node:path";
import { choApi } from "./support/api";
import { taoRunId } from "./support/env";
import { psql } from "./support/psql";
import { ghiTrangThai } from "./support/state";

export default async function globalSetup(): Promise<void> {
  await choApi();

  // ② Dev/CI-only: mở khoá cờ buộc đổi mật khẩu (KHÔNG đụng mật khẩu).
  psql(`
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "UserName" = 'admin';
`);

  // `psql -t -A` với UPDATE không trả dòng; đọc lại để **đo** thay vì tin.
  const conBat = psql(`select count(*) from "AbpUsers" where "UserName" = 'admin' and "ShouldChangePasswordOnNextLogin";`)
    .split(/\r?\n/)[0]
    ?.trim();

  if (conBat !== "0") {
    throw new Error(
      `Không tắt được cờ "buộc đổi mật khẩu" của admin (còn ${conBat} dòng bật) — mọi luồng sẽ đỏ ở đăng nhập.`,
    );
  }

  const runId = taoRunId();
  const tep = path.join(__dirname, ".run", "state.json");

  // Xoá trạng thái của lượt trước: giữ lại thì luồng ② đọc `sanPham` cũ (đã tồn tại) và test đo **dữ liệu cũ**.
  if (fs.existsSync(tep)) {
    fs.rmSync(tep, { force: true });
  }

  ghiTrangThai({ runId });

  console.log(`[E2E] runId=${runId} · API sẵn sàng · admin đã mở khoá cờ đổi mật khẩu (dev/CI).`);
}
