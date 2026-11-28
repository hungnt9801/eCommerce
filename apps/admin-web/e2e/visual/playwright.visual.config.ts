import path from "node:path";
import { defineConfig, devices } from "@playwright/test";
import cauHinhGoc from "../../playwright.config";

/**
 * **`UI-41` / `U41-3` — harness so ảnh 27 route** (`D-331`, `D-332`).
 *
 * 🔑 **KẾ THỪA root config** (`apps/admin-web/playwright.config.ts`) rồi CHỈ override phần so ảnh:
 * `webServer` (một nguồn khởi động + `reuseExistingServer`), `baseURL`, `locale`, `timezoneId`,
 * `workers: 1` được giữ nguyên. Root config **không** bị sửa (`D-332` ⑥: sửa root ⇒ đỏ).
 *
 * 📌 Root config khai `testDir: "./e2e/tests"` và `globalSetup`/`globalTeardown` **tương đối với root**;
 * khi spread sang tệp này, đường dẫn tương đối sẽ được Playwright giải theo **thư mục tệp config này**
 * ⇒ phải override bằng `../global-setup.ts` / `../global-teardown.ts` (đúng `D-332`).
 *
 * 🔒 **CỐ ĐỊNH PHÉP ĐO** (`D-331`): viewport 1440×900 · `deviceScaleFactor: 1` · `locale vi-VN` ·
 * `timezoneId Asia/Ho_Chi_Minh` · `animations: "disabled"` · `maxDiffPixelRatio: 0.001`.
 * ⚠️ RỦI RO ĐÃ GHI: ảnh sinh trên **Windows** (font Segoe UI/Inter fallback khác Linux) — tái sinh trên
 * CI Linux khi cổng visual vào (`U42-4`/`U51-4`), xem `docs/tests/UI-41-testplan.md`.
 *
 * Ảnh chuẩn: `e2e/visual/__screenshots__/<slug>.png` (git **commit**). Kết quả chạy: `e2e/visual/.run/`
 * (gitignore qua `e2e/visual/.run/.gitignore`).
 */
const VIEWPORT = { width: 1440, height: 900 } as const;

export default defineConfig({
  ...cauHinhGoc,

  testDir: "./specs",
  globalSetup: "../global-setup.ts",
  globalTeardown: "../global-teardown.ts",
  outputDir: "./.run/test-results",

  // 📌 Báo cáo HTML phải nằm trong `.run` (gitignore). Nếu để thừa kế `e2e/.run/html-report` của root,
  // Playwright giải theo **thư mục config này** ⇒ sinh rác `e2e/visual/e2e/.run/` không bị gitignore.
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: path.resolve(__dirname, ".run", "html-report") }],
  ],

  // 📌 Tên ảnh do spec đặt (không kèm `-win32`): D-331 muốn MỘT bộ ảnh dùng chung, tái sinh theo nền tảng.
  snapshotPathTemplate: "{testDir}/../__screenshots__/{arg}{ext}",

  workers: 1,
  fullyParallel: false,
  timeout: 90_000,

  expect: {
    ...cauHinhGoc.expect,
    toHaveScreenshot: {
      // D-331: ngưỡng nhỏ để bắt lệch bố cục/màu, vẫn không đỏ giả vì khử răng cưa font.
      maxDiffPixelRatio: 0.001,
      animations: "disabled",
      caret: "hide",
      scale: "css",
    },
  },

  use: {
    ...cauHinhGoc.use,
    locale: "vi-VN",
    timezoneId: "Asia/Ho_Chi_Minh",
    viewport: VIEWPORT,
    deviceScaleFactor: 1,
    // Ảnh chuẩn do `toHaveScreenshot` quản; tắt screenshot/trace/video để không sinh rác trong `.run`.
    screenshot: "off",
    trace: "off",
    video: "off",
  },

  projects: [
    {
      name: "visual",
      use: {
        ...devices["Desktop Chrome"],
        viewport: VIEWPORT,
        deviceScaleFactor: 1,
      },
    },
  ],
});
