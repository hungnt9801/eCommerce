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
    // Badge đếm của tab danh sách: `<label> (<n>)` — số hồ sơ/đơn tăng mỗi lượt ⇒ đỏ giả nếu không che.
    page.getByText(/^\(\d+\)$/),
    // Pager danh sách: "Trang <x> / <y>" + nút Trước/Sau (opacity đổi khi ở trang cuối).
    page.getByText(/^Trang \d+ \/ \d+$/),
    page.getByRole("link", { name: /Trước/ }),
    page.getByRole("link", { name: /^Sau/ }),
    ...maskNgay(page),
  ];
}

/**
 * Che chuỗi tất định-của-lượt-chạy (số đơn, tên khách, mã KM… mang `runId`).
 *
 * 📌 **Khớp CHUỖI CON** (không `exact`) — giá trị fixture thường bị ghép vào một dòng/câu có nhãn
 * (`Số: <số> · Ngày …`, `<mã> — <tên>`, `Mã: <mã>`), `exact` sẽ trượt ⇒ lộ giá trị biến động ⇒ ảnh đỏ giả.
 * Các chuỗi này đều mang `runId`/UUID nên khớp chuỗi con không che nhầm phần tĩnh.
 */
export function maskChuoi(page: Page, chuoi: Array<string | null | undefined>): Locator[] {
  return chuoi
    .filter((s): s is string => typeof s === "string" && s.trim().length > 0)
    .map((s) => page.getByText(s));
}

/**
 * **GUARD CSS — chống "ảnh trang trần"** (`UI-41` vòng 2, vá P1 do `rev` bắt).
 *
 * 🔴 Sự cố vòng 1: server cũ (build cũ) phục vụ HTML trỏ `/_next/static/css/<hash>.css` đã bị `.next` thay
 * ⇒ CSS 404 ⇒ Playwright chụp **form trần/sidebar `<ul>`** mà ảnh vẫn "xanh" (so với ảnh chuẩn cũng trần).
 * Cổng so ảnh khi đó **không** chứng minh được gì (BR-06/D-331 "đổi token ⇒ ảnh đỏ" bất khả thi).
 *
 * 🛡️ Guard này chèn một `<div class="flex">` dò vào `body`, ép layout, rồi đọc `getComputedStyle.display`.
 * Tailwind áp đúng ⇒ `.flex { display:flex }` ⇒ computed = **`flex`**. Không áp (CSS 404/chưa nạp) ⇒
 * computed = `block` (mặc định div) và **không có** `document.styleSheets` ⇒ ném lỗi rõ ngay tại ca,
 * KHÔNG ghi ảnh xanh giả. Chạy trên **mọi** ca trong `chup()`.
 *
 * 📌 Dùng `flex` (tiện ích **cấu trúc**, không phụ thuộc token) thay vì `h-4`/`w-4`: Tailwind v4 sinh tiện
 * ích theo nội dung quét lúc build, `h-4` KHÔNG có trong CSS đã biên dịch ⇒ guard dùng `h-4` sẽ đỏ giả.
 */
export async function kiemCss(page: Page): Promise<void> {
  const { kieuHienThi, soStyleSheet } = await page.evaluate(async () => {
    const div = document.createElement("div");
    div.className = "flex";
    // `fixed` + ẩn: ép Tailwind tính layout nhưng KHÔNG xê dịch trang (không làm lệch ảnh).
    div.style.position = "fixed";
    div.style.top = "0";
    div.style.left = "0";
    div.style.visibility = "hidden";
    div.setAttribute("data-qa-probe", "css");
    document.body.appendChild(div);

    // Cho stylesheet vừa inject/áp có cơ hội commit (2 frame).
    await new Promise<void>((r) => requestAnimationFrame(() => requestAnimationFrame(() => r())));

    const kieuHienThi = getComputedStyle(div).display;
    const soStyleSheet = document.styleSheets.length;
    div.remove();
    return { kieuHienThi, soStyleSheet };
  });

  expect(
    soStyleSheet,
    "Trang KHÔNG nạp stylesheet nào — build/server sai (HTML trỏ CSS 404). Dừng, sửa server/build trước.",
  ).toBeGreaterThan(0);

  expect(
    kieuHienThi,
    `Tailwind KHÔNG áp (.flex phải = "flex", đo được "${kieuHienThi}") — trang chưa có CSS: build/server sai.`,
  ).toBe("flex");
}

/**
 * Chụp ảnh chuẩn của **viewport 1440×900** sau khi:
 *  - **GUARD CSS** (`kiemCss`) — trang phải thật có Tailwind, nếu không ⇒ đỏ ngay (chống ảnh trần);
 *  - chờ font tải xong (`next/font` — chữ nét khác nhau giữa 2 lần chụp là đỏ giả);
 *  - tắt thanh cuộn (độ dài nội dung đổi theo dữ liệu ⇒ thumb đổi);
 *  - `networkidle` (POS terminal là client component tự gọi API sau khi mount).
 */
export async function chup(page: Page, ten: string, mask: Locator[]): Promise<void> {
  await page.waitForLoadState("networkidle").catch(() => {
    // 📌 Trang có polling/SSE sẽ không bao giờ "idle" — chờ mềm, không làm đỏ vì lý do hạ tầng.
  });

  // 🔴 GUARD CSS — chạy TRƯỚC mọi phép đo ảnh; trang trần ⇒ đỏ tại đây.
  await kiemCss(page);

  await page.evaluate(async () => {
    await document.fonts.ready;
  });

  await page.addStyleTag({
    content:
      "html,body,*{scrollbar-width:none!important}" +
      "::-webkit-scrollbar{width:0!important;height:0!important;display:none!important}" +
      // 🧩 D-337 (`U41-4`): chuẩn hoá bề rộng Ô ĐẾM của 5 tab `customer/list` (`#16`).
      // Mask Playwright tô theo **bounding box** của locator; `maskChung` che chữ số `(n)` (`getByText(/^\(\d+\)$/)`),
      // nhưng khung ô đếm co giãn theo **số chữ số** của `n` (1 → 2/3/… chữ số khi hồ sơ tích lũy) ⇒ nhãn 5 tab
      // dịch 3–6 px ⇒ lệch ảnh 2266 px (ratio 0.01 > 0.001). Ép **min-width** cố định cho `<span.opacity-80>` trong
      // `a.nh-btn` (ô đếm) ⇒ bounding box tất định; vẫn giữ phủ so ảnh cho nhãn/active-tab (không mask hàng tab).
      // ⚠️ GỠ rule khi có `PageTabs` badge bề rộng cố định (`U45-2` + chuyển `#16` ở `U48-3`); latent `#4`/`#6`/`#8`
      // ghi nợ có chủ ở testplan (§2.1 `D-337`).
      "main a.nh-btn span.opacity-80{display:inline-block;min-width:3.25rem;text-align:right;overflow:hidden}" +
      // 🧩 D-338 (`U41-4`): chuẩn hoá bề rộng BADGE ĐẾM của tab danh sách (`#4`/`#6`/`#8`) — cùng cơ chế `D-337`.
      // Badge `<span class="rounded-full tabular">{count}</span>` rộng theo **số chữ số** của `count`; DB dev không reset
      // (E2E trong `-DayDu` chạy trước, tạo thêm dòng mỗi lượt) ⇒ `9 → 10 → …` ⇒ badge rộng ra ⇒ nhãn các tab dịch
      // ngang ⇒ ảnh đỏ giả (đo được: `#6` 6597 px, ratio 0.01 > 0.001). `maskChung` chỉ TÔ chữ số `(n)`/`.tabular`,
      // KHÔNG chặn dịch layout. Ép `min-width` + `text-align:center` ⇒ bounding box badge tất định; vẫn giữ phủ so ảnh
      // cho nhãn/active-tab/màu (KHÔNG mask cả hàng tab). Điều kiện gỡ: có DB tất định (`U42-4`/`U51-4`) hoặc `PageTabs`
      // badge bề rộng cố định (`U45-2`, chuyển màn `U48-3`). Header bảng vẫn drift ⇒ mask `thead` **theo route** trong spec.
      "main a span.rounded-full{display:inline-block;min-width:2.5rem;text-align:center}",
  });

  await expect(page).toHaveScreenshot(ten, {
    mask,
    animations: "disabled",
    caret: "hide",
    // F-03 (P2 chấp nhận): `fullPage: true` KHÔNG tất định ở đây — các trang danh sách (product/inventory,
    // inventory/bill, pos/bill/list, customer/list…) có `tbody` dài BIẾN ĐỔI theo dữ liệu API bên dưới màn
    // hình (DB không reset, fixture thêm dòng mỗi lượt ⇒ chiều cao trang đổi). Đo được: 8/27 rồi 7/27 ca đỏ
    // khi bật fullPage. Giữ **viewport 1440×900** tất định; hồi quy nội dung cuộn xử lý ở cổng visual CI
    // (U42-4/U51-4) khi có DB tất định. Ghi F-03 = P2 chấp nhận trong `docs/tests/UI-41-testplan.md`.
    fullPage: false,
  });
}
