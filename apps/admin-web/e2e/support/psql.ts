/**
 * `psql` **bên trong container** của `docker-compose.yml` — chỉ dùng cho **hai** việc mà API không có:
 *
 * ① **Tắt cờ buộc đổi mật khẩu của `admin`** trên DB vừa migrate (`DbMigrator` **bật** cờ này —
 *    `IdentityDataSeedContributor.BatBuocDoiMatKhauAsync`, đó là chủ ý an toàn cho production). Nếu không tắt
 *    thì **luồng ① đỏ** với câu *"Sai email hoặc mật khẩu"* — thông điệp **nói sai nguyên nhân** và rất khó
 *    đoán. Probe `N5…N9` đều làm bước này bằng SQL; ở đây làm **một lần** trong `global-setup`.
 *
 * ② **Seed một đơn hàng để test đổi trạng thái**: hiện **chưa có** API tạo đơn nào dùng được — đường công
 *    khai `POST /api/app/don-hang/tao-tu-checkout` đòi **sổ giải trình giá** (`SAL-002`) + **lượt giữ chỗ**
 *    (`INV-002`), tức chuỗi của storefront/cổng thanh toán, không phải việc của `WP-09`. 📌 Cùng cách đã
 *    dùng ở `tools/probe-don-hang.ps1` và **có ghi ở `handover-n10` §2** (nhận chủ: `WP-05` P1 —
 *    *"tạo đơn từ Admin"*).
 *
 * 🚨 Đây là **môi trường dev/CI** (PostgreSQL trong Docker). Script **không** chạy nếu container vắng —
 * báo lỗi rõ ràng để người đọc biết phải `docker compose up -d`, chứ không phải sửa mã nguồn.
 */
import { execFileSync } from "node:child_process";
import { PG_CONTAINER, PG_DATABASE, PG_USER } from "./env";

export function psql(sql: string): string {
  try {
    return execFileSync(
      "docker",
      [
        "exec",
        "-i",
        PG_CONTAINER,
        "psql",
        "-U",
        PG_USER,
        "-d",
        PG_DATABASE,
        "-t",
        "-A",
        "-v",
        "ON_ERROR_STOP=1",
        "-f",
        "-",
      ],
      { input: sql, encoding: "utf8" },
    ).trim();
  } catch (e) {
    const loi = e instanceof Error ? e.message : String(e);
    throw new Error(
      `Không chạy được psql trong container "${PG_CONTAINER}" (bước seed của E2E — xem e2e/support/psql.ts).\n` +
        `📌 Kiểm tra: \`docker compose up -d\` rồi \`docker compose ps\`.\nChi tiết: ${loi}`,
    );
  }
}
