/**
 * **27 ảnh chuẩn — phần TĨNH (19 route)** (`UI-41` / `U41-3`, `D-331`).
 *
 * Mỗi ca = một route của `docs/ui/00-kiem-ke.md` §2.2 (bỏ dòng #1 `/` vì chỉ `redirect`):
 *  ① đi đúng đường người dùng (`dangNhap` qua form → cookie httpOnly, `support/ui.ts`);
 *  ② khẳng định **nội dung thật** (tiêu đề/nhãn của route) — ảnh không bao giờ là ca rỗng;
 *  ③ `expect(page).toHaveScreenshot()` tên tất định `<số>-<slug>.png`.
 *
 * 📌 8 route động (cần id/query) nằm ở `02-dong.spec.ts`. Tổng = 19 + 8 = **27**.
 */
import { expect, test, type Locator, type Page } from "@playwright/test";
import { dangNhap, duong } from "../../support/ui";
import { chup, maskChung } from "../support/manhinh";

type Ca = {
  slug: string;
  ten: string;
  /** `null` = trang `/login` (chưa có phiên — KHÔNG đăng nhập trước). */
  url: string | null;
  kiem: (page: Page) => Promise<void>;
  mask?: (page: Page) => Locator[];
};

const CAS: Ca[] = [
  {
    slug: "02-login",
    ten: "#2 /login — form đăng nhập",
    url: "/login",
    kiem: async (page) => {
      await expect(page.getByText("Quản trị bán hàng đa kênh")).toBeVisible();
      await expect(page.getByRole("button", { name: "Đăng nhập" })).toBeVisible();
    },
  },
  {
    slug: "03-dashboard",
    ten: "#3 dashboard — 4 KPI + biểu đồ 7 ngày",
    url: duong(""),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Tổng quan" })).toBeVisible();
      await expect(page.getByRole("heading", { name: "Doanh thu 7 ngày" })).toBeVisible();
    },
    // KPI + biểu đồ + mốc ngày phụ thuộc dữ liệu/read-model: che GIÁ TRỊ, giữ nhãn + khung thẻ.
    mask: (page) => [
      page.locator("main .grid.grid-cols-4"),
      page.locator('main svg[role="img"]'),
      page.locator("main p b"),
      page.getByText(/^tổng .* chứng từ$|^chưa tổng hợp$/),
    ],
  },
  {
    slug: "04-product-item",
    ten: "#4 product/item — DS sản phẩm + tab",
    url: duong("/product/item"),
    kiem: async (page) => {
      await expect(page.getByPlaceholder("Tên, mã sản phẩm, SKU…")).toBeVisible();
      await expect(page.getByRole("button", { name: "Lọc" })).toBeVisible();
    },
    // Badge đếm của tab "Tất cả" + dòng tab: số hồ sơ tăng mỗi lượt ⇒ nhãn tab dịch ⇒ đỏ giả nếu chỉ che badge.
    // Che cả dòng tiêu đề (h1 + tổng) và dòng tab để bề rộng không đổi giữa các lượt (F-02 P2 chấp nhận).
    mask: (page) => [
      page.locator("main .flex.flex-wrap.items-center.gap-2"),
      page.locator("main .flex.flex-wrap.gap-1"),
      page.locator("main a span.rounded-full"),
      // D-338 (đo 2 lượt): bảng `nh-table` auto-layout — bề rộng cột co theo nội dung `tbody` (DB dev trôi)
      // ⇒ nhãn header dịch (diff 1680 px). `maskChung` chỉ che `tbody`; mask `thead`. Gỡ khi DB tất định.
      page.locator("main table thead"),
    ],
  },
  {
    slug: "05-product-inventory",
    ten: "#5 product/inventory — tồn kho 9 chiều",
    url: duong("/product/inventory"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Tồn kho" })).toBeVisible();
    },
  },
  {
    slug: "06-order-manage",
    ten: "#6 order/manage — DS đơn + 14 tab",
    url: duong("/order/manage"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Đơn hàng" })).toBeVisible();
      await expect(page.getByText("hàng đợi việc thật của nhân viên")).toBeVisible();
    },
    // D-338: bảng `<table class="w-full">` auto-layout, `thead th` KHÔNG cố định bề rộng ⇒ bề rộng cột co theo
    // nội dung `tbody` (DB dev trôi mỗi lượt E2E) ⇒ nhãn cột dịch ngang ⇒ ảnh đỏ giả nếu không che header.
    // `maskChung` chỉ che `tbody`, không chặn dịch layout của `thead`; mask `thead` (chỉ TÔ, không đổi layout).
    // Gỡ khi có DB tất định (`U42-4`/`U51-4`).
    mask: (page) => [page.locator("main table thead")],
  },
  {
    slug: "08-inventory-bill",
    ten: "#8 inventory/bill — DS phiếu kho",
    url: duong("/inventory/bill"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Phiếu kho" })).toBeVisible();
      await expect(page.getByRole("link", { name: "+ Tạo phiếu" })).toBeVisible();
    },
  },
  {
    slug: "09-inventory-bill-new",
    ten: "#9 inventory/bill/new — tạo phiếu kho",
    url: duong("/inventory/bill/new"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Tạo phiếu kho" })).toBeVisible();
    },
    // `<select>` hiển thị giá trị đang chọn: kho đầu tiên có thể là kho fixture (mang runId).
    mask: (page) => [page.locator("main select"), page.locator('main input[type="date"]')],
  },
  {
    slug: "13-pos-bill-list",
    ten: "#13 pos/bill/list — DS hoá đơn bán lẻ",
    url: duong("/pos/bill/list"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Hoá đơn bán lẻ" })).toBeVisible();
    },
    // D-338 (đo 2 lượt: --update → E2E → clean): bảng hoá đơn auto-layout, bề rộng cột co theo nội dung
    // `tbody` (DB dev trôi) ⇒ nhãn header dịch (diff 2267 px). `maskChung` chỉ che `tbody`; mask `thead`.
    mask: (page) => [page.locator("main table thead")],
  },
  {
    slug: "14-pos-return",
    ten: "#14 pos/return — trả hàng từ hoá đơn",
    url: duong("/pos/return"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Trả hàng" })).toBeVisible();
      await expect(page.getByPlaceholder("POS-20261104-0001")).toBeVisible();
    },
  },
  {
    slug: "16-customer-list",
    ten: "#16 customer/list — DS khách + 5 tab",
    url: duong("/customer/list"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Khách hàng" })).toBeVisible();
    },
  },
  {
    slug: "18-settings-staff",
    ten: "#18 settings/staff — nhân viên + ma trận quyền",
    url: duong("/settings/staff"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Nhân viên & quyền" })).toBeVisible();
    },
  },
  {
    slug: "19-report-revenue",
    ten: "#19 report/revenue — báo cáo doanh thu + biểu đồ",
    url: duong("/report/revenue"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Doanh thu", exact: true })).toBeVisible();
      await expect(page.getByRole("heading", { name: "Biểu đồ doanh thu" })).toBeVisible();
    },
    // Khoảng ngày mặc định = 30 ngày cuộn ⇒ đổi theo NGÀY; mốc tổng hợp + số + biểu đồ đổi theo dữ liệu.
    mask: (page) => [
      page.locator('main input[type="date"]'),
      page.locator("main form b"),
      page.locator('main svg[role="img"]'),
      page.getByText(/đơn · .*huỷ · giá trị/),
      // D-338 (đo 2 lượt): bảng tổng hợp auto-layout — bề rộng cột co theo nội dung `tbody` (DB dev trôi)
      // ⇒ nhãn header dịch (diff 1710 px). `maskChung` chỉ che `tbody`; mask `thead`.
      page.locator("main table thead"),
    ],
  },
  {
    slug: "20-report-inventory",
    ten: "#20 report/inventory — báo cáo tồn (ảnh chụp mới nhất)",
    url: duong("/report/inventory"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Tồn kho" })).toBeVisible();
      await expect(page.getByText("Ngày chụp (để trống = mới nhất)")).toBeVisible();
    },
    mask: (page) => [page.locator('main input[type="date"]'), page.locator("main form b")],
  },
  {
    slug: "21-promotion",
    ten: "#21 promotion — DS khuyến mãi",
    url: duong("/promotion"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Khuyến mãi" })).toBeVisible();
    },
    // D-338 (đo 2 lượt): bảng `nh-table` auto-layout — bề rộng cột co theo nội dung `tbody` (DB dev trôi)
    // ⇒ nhãn header dịch. `maskChung` chỉ che `tbody`; mask `thead`. Gỡ khi DB tất định (`U42-4`/`U51-4`).
    mask: (page) => [page.locator("main table thead")],
  },
  {
    slug: "22-promotion-new",
    ten: "#22 promotion/new — tạo khuyến mãi",
    url: duong("/promotion/new"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Tạo khuyến mãi" })).toBeVisible();
    },
    mask: (page) => [page.locator('main input[type="date"]')],
  },
  {
    slug: "24-templates",
    ten: "#24 notifications/templates — DS mẫu thông báo",
    url: duong("/notifications/templates"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Mẫu thông báo" })).toBeVisible();
    },
    // D-338 (đo 2 lượt): bảng mẫu auto-layout — bề rộng cột co theo nội dung `tbody` (DB dev trôi)
    // ⇒ nhãn header dịch. `maskChung` chỉ che `tbody`; mask `thead`. Gỡ khi DB tất định (`U42-4`/`U51-4`).
    mask: (page) => [page.locator("main table thead")],
  },
  {
    slug: "25-templates-new",
    ten: "#25 notifications/templates/new — tạo mẫu",
    url: duong("/notifications/templates/new"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Tạo mẫu thông báo" })).toBeVisible();
    },
  },
  {
    slug: "27-notifications-queue",
    ten: "#27 notifications/queue — hàng đợi + timeline",
    url: duong("/notifications/queue"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Hàng đợi thông báo" })).toBeVisible();
    },
  },
  {
    slug: "28-notifications-batches",
    ten: "#28 notifications/batches — tạo/duyệt lô",
    url: duong("/notifications/batches"),
    kiem: async (page) => {
      await expect(page.getByRole("heading", { name: "Lô gửi thông báo" })).toBeVisible();
    },
    // Dropdown mẫu hiển thị mẫu đầu tiên — có thể là mẫu fixture (mang runId).
    mask: (page) => [page.locator("main select")],
  },
];

for (const ca of CAS) {
  test(ca.ten, async ({ page }) => {
    if (ca.url === null) {
      throw new Error("Ca tĩnh phải có URL.");
    }

    if (ca.url !== "/login") {
      await dangNhap(page);
    }

    await page.goto(ca.url);
    await ca.kiem(page);
    await chup(page, `${ca.slug}.png`, [...maskChung(page), ...(ca.mask?.(page) ?? [])]);
  });
}
