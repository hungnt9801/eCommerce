/**
 * **Tìm `axe-core` CÓ SẴN trong repo** (`UI-41` / `U41-3`) — KHÔNG cài gói mới.
 *
 * `axe-core@4.14.1` là devDependency của `packages/ui` (dùng bởi `jest-axe`). `apps/admin-web` **không**
 * khai nó, nên `require.resolve("axe-core")` từ app sẽ hỏng. Giải từ `packages/ui/package.json` bằng
 * `createRequire` — cùng một bản đã được khoá trong `pnpm-lock.yaml`, không thêm phụ thuộc.
 */
import fs from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";

/** Đường dẫn tuyệt đối của `axe.min.js` (né exception `.js` trong `exports` bằng file trực tiếp). */
export function duongAxe(): string {
  // support/ → visual(1) → e2e(2) → admin-web(3) → apps(4) → repo(5)
  const gocRepo = path.resolve(__dirname, "..", "..", "..", "..", "..");
  const ungVien = [
    path.join(gocRepo, "packages", "ui", "package.json"),
    path.join(gocRepo, "package.json"),
  ];

  for (const pkg of ungVien) {
    if (!fs.existsSync(pkg)) continue;
    try {
      const req = createRequire(pkg);
      return req.resolve("axe-core/axe.min.js");
    } catch {
      // thử ứng viên kế tiếp
    }
  }

  throw new Error(
    "Không tìm thấy axe-core trong repo. 📌 Kiểm `packages/ui/package.json` có `axe-core` trong " +
      "devDependencies và `pnpm install` đã chạy. Không cài gói mới (U41-3).",
  );
}

export type MucAxe = "critical" | "serious" | "moderate" | "minor";

export type ViPhamAxe = { id: string; impact: string | null; help: string; nodes: number };

export type KqAxe = {
  viol: ViPhamAxe[];
  dem: Record<MucAxe, number>;
};

/** Đọc kết quả `axe.run` thành bản tóm tắt (đếm theo mức + mô tả ngắn). */
export function tomTatAxe(violations: Array<{ id: string; impact: string | null; help: string; nodes: unknown[] }>): KqAxe {
  const dem: Record<MucAxe, number> = { critical: 0, serious: 0, moderate: 0, minor: 0 };
  const viol: ViPhamAxe[] = [];

  for (const v of violations) {
    const muc = (v.impact ?? "minor") as MucAxe;
    if (muc in dem) dem[muc] += 1;
    viol.push({ id: v.id, impact: v.impact, help: v.help, nodes: v.nodes.length });
  }

  viol.sort((a, b) => a.id.localeCompare(b.id));
  return { viol, dem };
}
