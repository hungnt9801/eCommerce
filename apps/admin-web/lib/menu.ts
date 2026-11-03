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
      { label: "Tồn kho", href: "product/inventory", permission: "eCommerce.Inventory", enabled: false, note: "WP-04" },
    ],
  },
  {
    label: "Kho",
    items: [
      { label: "Xuất nhập kho", href: "inventory/bill", permission: "eCommerce.Inventory", enabled: false, note: "WP-04" },
      { label: "Kiểm kho", href: "inventory/check", permission: "eCommerce.Inventory", enabled: false, note: "WP-04" },
      { label: "Chuyển kho", href: "inventory/transfer", permission: "eCommerce.Inventory", enabled: false, note: "WP-04" },
    ],
  },
  {
    label: "Bán hàng",
    items: [
      { label: "Bán lẻ (POS)", href: "pos/bill", permission: "eCommerce.Orders", enabled: false, note: "WP-06" },
      { label: "Đơn hàng", href: "order/manage", permission: "eCommerce.Orders", enabled: false, note: "WP-05" },
      { label: "Trả hàng", href: "pos/return", permission: "eCommerce.Orders", enabled: false, note: "WP-06" },
    ],
  },
  {
    label: "Khách hàng",
    items: [
      { label: "Khách hàng", href: "customer/list", permission: "eCommerce.Customers", enabled: false, note: "WP-07" },
    ],
  },
  {
    label: "Báo cáo",
    items: [{ label: "Doanh thu", href: "report/revenue", permission: "eCommerce.Reports", enabled: false, note: "WP-08" }],
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
