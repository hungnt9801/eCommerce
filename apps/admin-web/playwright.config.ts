import path from "node:path";
import { defineConfig, devices } from "@playwright/test";
import { API, BUSINESS, WEB } from "./e2e/support/env";

/**
 * **`WP-09` — E2E Playwright, 8 luồng của một ngày bán hàng.**
 *
 * 🔒 **HAI QUYẾT ĐỊNH Ở TỆP NÀY (đừng "tối ưu" mà bỏ):**
 *
 * ① **`workers: 1` + thứ tự tên tệp `01…08`** — 8 luồng là **một chuỗi** (bán rồi mới in được, mở ca rồi mới
 *    kết ca được). Chạy song song thì chúng tranh nhau **một** ca thu ngân / **một** read-model báo cáo, và
 *    lỗi hiện ra dưới dạng *"flaky"* — thứ tệ nhất trong bộ test, vì nó dạy người ta bỏ qua màu đỏ.
 *
 * ② **`webServer` bật CẢ Host lẫn Next** — test E2E mà phải nhớ bật 2 tiến trình bằng tay thì nó **không
 *    phải cổng**: hôm sau sẽ có người chạy `pnpm e2e` với Host của **build cũ** và nhận kết luận sai.
 *    📌 `reuseExistingServer: true` để dev không phải tắt server đang chạy; `tools/e2e.ps1` **giết** tiến
 *    trình cũ trước khi gọi, nên "reuse" ở CI/luồng cổng không bao giờ là build cũ.
 *
 * ⚠️ **Điều kiện trước khi chạy:** `dotnet build eCommerce.slnx` (Host có binary) và `pnpm build`
 *    (Next có `.next/BUILD_ID`). Thiếu một trong hai ⇒ `webServer` báo lỗi khởi động — đọc dòng đầu của nó.
 */
const HOST_BIN = path.join(__dirname, "..", "..", "src", "eCommerce.HttpApi.Host", "bin", "Debug", "net10.0");

export default defineConfig({
  testDir: "./e2e/tests",
  globalSetup: "./e2e/global-setup.ts",
  globalTeardown: "./e2e/global-teardown.ts",

  // ① — xem đầu tệp
  workers: 1,
  fullyParallel: false,

  timeout: 60_000,
  expect: { timeout: 15_000 },

  forbidOnly: !!process.env.CI,
  // 📌 Retry **ở CI** (máy chậm/CPU chia sẻ). Ở máy dev retry = che flaky ⇒ để 0.
  retries: process.env.CI ? 1 : 0,

  reporter: [
    ["list"],
    // 📌 Báo cáo HTML để ở `e2e/.run/` (gitignore) — artifact CI tự upload, không commit.
    ["html", { open: "never", outputFolder: "e2e/.run/html-report" }],
  ],

  use: {
    baseURL: WEB,
    locale: "vi-VN",
    timezoneId: "Asia/Ho_Chi_Minh",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "off",
  },

  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],

  webServer: [
    {
      // 📌 Chạy **DLL đã build** (không `dotnet run`): `dotnet run` tự build lại ⇒ test đo một binary khác
      // với binary vừa qua `dotnet build`/`kiem-tat-ca.ps1`.
      command: "dotnet eCommerce.HttpApi.Host.dll",
      cwd: HOST_BIN,
      url: `${API}/health-status`,
      reuseExistingServer: true,
      timeout: 180_000,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: API,
        AuthServer__Authority: API,
      },
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      // 📌 `next start` = bản **production build** (đúng bản sẽ deploy); `next dev` che lỗi build/hydration.
      command: "pnpm start",
      cwd: __dirname,
      url: `${WEB}/login`,
      reuseExistingServer: true,
      timeout: 120_000,
      stdout: "pipe",
      stderr: "pipe",
    },
  ],
});

/** Doanh nghiệp dùng trong URL — export lại cho các tệp test đọc dễ hơn. */
export const DOANH_NGHIEP = BUSINESS;
