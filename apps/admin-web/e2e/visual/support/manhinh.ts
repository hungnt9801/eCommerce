/**
 * **ỔN ĐỊNH PHÉP ĐO ẢNH** (`UI-41` / `U41-3`, `D-331`, `D-333`).
 *
 * 🔑 27 ảnh chuẩn phải **so lại được** ở phiên khác. Những thứ sau thay đổi một cách hợp pháp giữa các
 * lần chạy và **không** phải bất biến giao diện, nên phải `mask` (Playwright tô khối màu lên vùng đó):
 *
 * ① **Số liệu** — `.tabular` (tiền, số lượng, đếm) và các câu đếm kiểu `12 dòng`, `1.234 hồ sơ`.
 * ② **Ngày/giờ** — hôm nay khác ngày mai; `dd/MM/yyyy[ HH:mm]` và `yyyy-MM-dd` (mốc tổng hợp).
 * ③ **Dòng dữ liệu** — `table tbody` (bảng danh sách do API trả; bộ E2E/E2E khác thêm dòng mỗi lượt).
 * ④ **Chuỗi mang `runId`** — số đơn/phiếu/mã khuyến mãi của fixture (truyền qua `maskChuoi`).
 *
 * 📌 KHÔNG mask tiêu đề, tab, nhãn, khung/thẻ, màu token: đó chính là thứ cổng so ảnh phải bắt (`WP-42`
 * đổi token ⇒ ảnh phải đỏ nếu hình đổi). Chỉ che **giá trị biến động**, không che bố cục.
 */
import { expect, type Locator, type Page } from "@playwright/test";

/** Ngày/giờ hiển thị: `dd/MM/yyyy`, `dd/MM/yyyy HH:mm[:ss]`, `yyyy-MM-dd` (mốc `ngayTongHopMoiNhat`). */
export function maskNgay(page: Page): Locator[] {
  return [
    page.getByText(/^\d{4}-\d{2}-\d{2}$/),
    page.getByText(/^\d{2}\/\d{2}\/\d{4}( \d{2}:\d{2}(:\d{2})?)?$/),
  ];
}

/** Che số liệu + ngày + dòng bảng — dùng cho **mọi** route có dữ liệu API. */
export function maskChung(page: Page): Locator[] {
  return [
    page.locator("main table tbody"),
    page.locator("main .tabular"),
    page.getByText(/^\d[\d.,]* (hồ sơ|hoá đơn|dòng|tin|mẫu|khuyến mãi|phiếu|đơn)\.?$/),
    page.getByText(/^Đang hiển thị \d+\/\d+ mẫu/),
    ...maskNgay(page),
  ];
}

/** Che đúng một chuỗi tất định-của-lượt-chạy (số đơn, tên khách, mã KM… mang `runId`). */
export function maskChuoi(page: Page, chuoi: Array<string | null | undefined>): Locator[] {
  return chuoi
    .filter((s): s is string => typeof s === "string" && s.trim().length > 0)
    .map((s) => page.getByText(s, { exact: true }));
}

/**
 * Chụp ảnh chuẩn của **viewport** sau khi:
 *  - chờ font tải xong (`next/font` — chữ nét khác nhau giữa 2 lần chụp là đỏ giả);
 *  - tắt thanh cuộn (độ dài nội dung đổi theo dữ liệu ⇒ thumb đổi);
 *  - `networkidle` (POS terminal là client component tự gọi API sau khi mount).
 */
export async function chup(page: Page, ten: string, mask: Locator[]): Promise<void> {
  await page.waitForLoadState("networkidle").catch(() => {
    // 📌 Trang có polling/SSE sẽ không bao giờ "idle" — chờ mềm, không làm đỏ vì lý do hạ tầng.
  });

  await page.evaluate(async () => {
    await document.fonts.ready;
  });

  await page.addStyleTag({
    content:
      "html,body,*{scrollbar-width:none!important}" +
      "::-webkit-scrollbar{width:0!important;height:0!important;display:none!important}",
  });

  await expect(page).toHaveScreenshot(ten, {
    mask,
    animations: "disabled",
    caret: "hide",
    fullPage: false,
  });
}
