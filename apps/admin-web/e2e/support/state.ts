/**
 * **Bộ nhớ giữa các luồng E2E** — `e2e/.run/state.json`.
 *
 * 🔑 **VÌ SAO CẦN:** 8 luồng của `WP-09` là **một ngày bán hàng liên tục** (đăng nhập → tạo SP → nhập kho →
 * bán lẻ → in → đổi trạng thái đơn → kết ca → xuất CSV). Luồng sau dùng **đúng chứng từ** của luồng trước
 * (in hoá đơn phải in hoá đơn vừa bán; kết ca phải kết ca vừa mở). Truyền qua tệp trạng thái là cách **rõ
 * ràng** nhất, thay vì mỗi luồng tự tạo dữ liệu riêng rồi không ai kiểm được đường dọc.
 *
 * 📌 `workers: 1` + tên tệp `01…08` ⇒ thứ tự **tất định**; nếu một luồng đỏ, các luồng sau báo
 * *"thiếu dữ liệu của luồng trước"* thay vì đỏ mờ mịt.
 */
import fs from "node:fs";
import path from "node:path";

export type TrangThaiE2E = {
  runId?: string;
  /** Luồng ② */
  sanPham?: { id: string; code: string; name: string; sku: string; gia: number };
  /** Luồng ③ */
  kho?: { id: string; code: string; name: string };
  phieuNhap?: { id: string; number: string };
  /** Luồng ④ */
  ca?: { id: string; number: string };
  hoaDon?: { id: string; number: string; total: number };
  /** Luồng ⑥ — seed SQL */
  donHang?: { id: string; number: string };
};

const THU_MUC = path.join(__dirname, "..", ".run");
const TEP = path.join(THU_MUC, "state.json");

export function docTrangThai(): TrangThaiE2E {
  try {
    return JSON.parse(fs.readFileSync(TEP, "utf8")) as TrangThaiE2E;
  } catch {
    return {};
  }
}

/** Ghi **bổ sung** (không xoá khoá cũ) — mỗi luồng chỉ ghi phần của mình. */
export function ghiTrangThai(them: TrangThaiE2E): TrangThaiE2E {
  const moi = { ...docTrangThai(), ...them };
  fs.mkdirSync(THU_MUC, { recursive: true });
  fs.writeFileSync(TEP, `${JSON.stringify(moi, null, 2)}\n`, "utf8");
  return moi;
}

/** Lấy một khoá **bắt buộc** — thiếu thì báo đúng luồng nào chưa chạy, không để test đỏ vì `undefined`. */
export function batBuoc<K extends keyof TrangThaiE2E>(
  khoa: K,
  luongTruoc: string,
): NonNullable<TrangThaiE2E[K]> {
  const gt = docTrangThai()[khoa];

  if (gt === undefined || gt === null) {
    throw new Error(
      `Thiếu "${String(khoa)}" trong e2e/.run/state.json — luồng ${luongTruoc} chưa chạy xong. ` +
        `Chạy lại cả bộ (\`pnpm e2e\`), đừng chạy lẻ luồng này.`,
    );
  }

  return gt as NonNullable<TrangThaiE2E[K]>;
}
