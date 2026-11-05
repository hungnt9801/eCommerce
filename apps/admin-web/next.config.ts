import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  // Cho phép build trong monorepo pnpm
  outputFileTracingRoot: undefined,

  /**
   * 📌 **`WP-10` — `output: "standalone"`**: `next build` sinh thêm `.next/standalone/` chứa
   * `server.js` + **đúng** node_modules mà nó cần ⇒ image production không phải mang theo cả
   * `node_modules` của workspace (xem `apps/admin-web/Dockerfile`).
   *
   * 🔑 **Không phá đường cũ:** `next start` (dùng bởi `pnpm start` và `webServer` của Playwright `WP-09`)
   * vẫn chạy **bình thường** — standalone là **đầu ra THÊM**, không thay thế `.next/`. Điều kiện kiểm:
   * 8 luồng E2E phải vẫn xanh sau thay đổi này (`tools/e2e.ps1`).
   */
  output: "standalone",
};

export default nextConfig;
