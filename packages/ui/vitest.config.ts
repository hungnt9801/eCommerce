import { defineConfig } from "vitest/config";

/**
 * **Vitest của gói `@ecommerce/ui`** — `pnpm --filter @ecommerce/ui test`.
 *
 * Khác `apps/admin-web/vitest.config.ts` (environment `node` cho hàm thuần),
 * gói test tương tác DOM + axe ⇒ `jsdom` (`docs/ui/01-kien-truc-ui.md` §4.5).
 *
 * ⚠️ **Không dùng `@vitejs/plugin-react@6.3.0`**: bản này cần peer `vite ^8`
 * nhưng `vitest@3.2.7` đóng gói `vite 7.3.7` ⇒ plugin nạp `vite/internal`
 * (không tồn tại ở vite 7) và Vitest không khởi động được. Đây đúng **đường
 * lùi D-329** ("esbuild của Vitest"): dùng esbuild sẵn có với JSX automatic.
 * Gói vẫn khai `@vitejs/plugin-react` ở devDeps để `WP-42`/Storybook dùng lại
 * khi Vite lên major khớp.
 *
 * Phạm vi cố ý hẹp: chỉ `src/**\/*.test.*` (vùng ghi của `ds`). Ca tích hợp
 * của QA ở `test/**` (vùng `qa`) — không trỏ tới đây.
 */
export default defineConfig({
  esbuild: { jsx: "automatic" },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
  },
});
