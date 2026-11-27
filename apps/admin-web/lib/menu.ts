export type MenuItem = {
  label: string;
  href: string;
  permission?: string;
  /** `false` = chưa làm — hiện mờ, KHÔNG phải link chết (bài học "menu nói dối" của repo). */
  enabled: boolean;
  note?: string;
};

export type MenuGroup = {
  label: string;
  items: MenuItem[];
};

/**
 * Menu sidebar — bản rút gọn của 180 mục Nhanh (docs/nhanh/01 §4), chỉ mở những màn **đã có**.
 * Mục chưa làm để `enabled: false` + nhãn "sắp có" ⇒ người dùng không bấm vào 404.
 */
export const MENU: MenuGroup[] = [
  {
    label: "Tổng quan",
    items: [{ label: "Dashboard", href: "", enabled: true }],
  },
  {
    label: "Hàng hóa",
    items: [
      { label: "Sản phẩm", href: "product/item", permission: "eCommerce.Products", enabled: true },
      { label: "Thương hiệu", href: "product/brand", permission: "eCommerce.Products", enabled: false, note: "sắp có" },
      { label: "Danh mục", href: "product/category", permission: "eCommerce.Categories", enabled: false, note: "sắp có" },
      { label: "Tồn kho", href: "product/inventory", permission: "eCommerce.Inventory", enabled: true },
    ],
  },
  {
    label: "Kho",
    items: [
      { label: "Xuất nhập kho", href: "inventory/bill", permission: "eCommerce.Inventory", enabled: true },
      { label: "Kiểm kho", href: "inventory/check", permission: "eCommerce.Inventory", enabled: false, note: "chưa làm" },
      {
        label: "Chuyển kho",
        href: "inventory/bill/new?kind=transfer",
        permission: "eCommerce.Inventory",
        enabled: true,
      },
    ],
  },
  {
    label: "Bán hàng",
    items: [
      // ✅ `WP-06` — POS + trả hàng đã có màn thật; quyền tách theo việc (`eCommerce.Pos.Sell`/`.Return`),
      // nhưng **menu** chỉ cần quyền **đọc** (`eCommerce.Pos`) — thiếu quyền ghi thì nút trong màn báo mã lỗi.
      { label: "Bán lẻ (POS)", href: "pos/bill", permission: "eCommerce.Pos", enabled: true },
      { label: "Đơn hàng", href: "order/manage", permission: "eCommerce.Orders", enabled: true },
      { label: "Trả hàng", href: "pos/return", permission: "eCommerce.Pos", enabled: true },
      { label: "Hoá đơn bán lẻ", href: "pos/bill/list", permission: "eCommerce.Pos", enabled: true },
    ],
  },
  {
    label: "Khuyến mãi",
    items: [
      // ✅ `PRO-002` W24-3 — màn dựng cây điều kiện (list · tạo · chi tiết). Menu mở bằng quyền ĐỌC; nút
      // tạo/lưu cây kiểm `Promotions.Manage`, nút duyệt kiểm `Promotions.Approve` riêng trong màn.
      {
        label: "Khuyến mãi",
        href: "promotion",
        permission: "eCommerce.Promotions.View",
        enabled: true,
      },
    ],
  },
  {
    label: "Khách hàng",
    items: [
      // ✅ `WP-07` — DS khách 5 tab phân khúc + chi tiết 5 tab.
      { label: "Khách hàng", href: "customer/list", permission: "eCommerce.Customers", enabled: true },
      // 🔒 Quyền **riêng** `eCommerce.Settings` (không dùng `eCommerce.Customers`): màn này đổi **phạm vi
      // dữ liệu** và **cấp quyền** — người sửa hồ sơ khách không đương nhiên được nới quyền của mình.
      { label: "Nhân viên & quyền", href: "settings/staff", permission: "eCommerce.Settings", enabled: true },
    ],
  },
  {
    label: "Báo cáo",
    items: [
      // ✅ `WP-08` — doanh thu + tồn kho đọc read-model (`Rpt*`) do job tổng hợp ghi.
      // 🔒 Quyền **riêng** `eCommerce.Reports` (không dùng `eCommerce.Pos`/`eCommerce.Orders`): báo cáo là số
      // liệu **toàn tenant**, và người bán hàng không đương nhiên được xem doanh thu của cả cửa hàng.
      { label: "Doanh thu", href: "report/revenue", permission: "eCommerce.Reports", enabled: true },
      { label: "Tồn kho", href: "report/inventory", permission: "eCommerce.Reports", enabled: true },
    ],
  },
  {
    label: "Thông báo",
    items: [
      // ✅ `WP-22` W22-3 — Mẫu thông báo (NOT-001), quyền đọc `Notification.Templates.View`.
      {
        label: "Mẫu thông báo",
        href: "notifications/templates",
        permission: "eCommerce.Notification.Templates.View",
        enabled: true,
      },
      // ✅ Hàng đợi gửi — quyền đọc `Notification.Queue.View` (người nhận đã che, D-191).
      {
        label: "Hàng đợi gửi",
        href: "notifications/queue",
        permission: "eCommerce.Notification.Queue.View",
        enabled: true,
      },
      // ✅ Lô gửi — tạo/duyệt tách quyền (D-198). Menu mở bằng quyền TẠO; nút duyệt kiểm riêng trong màn.
      {
        label: "Lô gửi",
        href: "notifications/batches",
        permission: "eCommerce.Notification.Batches.Create",
        enabled: true,
      },
    ],
  },
];

/** Quyền cần hỏi server để dựng menu (gom 1 lần, không N+1). */
const permissionSet = new Set<string>();
for (const group of MENU) {
  for (const item of group.items) {
    if (item.permission) {
      permissionSet.add(item.permission);
    }
  }
}
export const MENU_PERMISSIONS: string[] = Array.from(permissionSet).sort();
