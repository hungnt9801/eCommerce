import path from "node:path";
import { defineConfig } from "vitest/config";

/**
 * **Test đơn vị của FE (`WP-09`)** — `pnpm test`.
 *
 * 🔑 Phạm vi **cố ý hẹp**: chỉ hàm **thuần** (định dạng số/ngày, dựng CSV, tính tiền tạm, nhãn trạng thái).
 * 📌 Mấy hàm này là nơi **lỗi im lặng** hay sống: `vi-VN` phụ thuộc locale máy chạy, CSV thiếu BOM, và
 * công thức tiền lệch 1 đồng so với server. Chúng **không** cần DOM ⇒ chạy `environment: "node"` để không
 * phải kéo jsdom vào CI (nhanh và ít bề mặt hỏng).
 *
 * ⚠️ **KHÔNG** thay E2E: test đơn vị không chứng minh form bấm được hay API trả 200 — việc đó ở `e2e/`.
 */
export default defineConfig({
  resolve: {
    alias: { "@": path.resolve(__dirname) },
  },
  test: {
    // `test/**` = bộ test của QA; `lib/**/*.test.ts` = test hàm thuần co-located do vai fe thêm (vùng ghi
    // của fe DENY `apps/admin-web/test/**`, xem `.claude/scopes.json`). Cả hai đều chạy qua `pnpm test`.
    include: ["test/**/*.test.ts", "lib/**/*.test.ts"],
    environment: "node",
    reporters: ["default"],
  },
});
