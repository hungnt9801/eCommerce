/**
 * **HỢP ĐỒNG DỮ LIỆU của màn KHÁCH HÀNG + NHÂN VIÊN** (`WP-07`) — type lấy **trực tiếp** từ `schema.d.ts`
 * (sinh tự OpenAPI), không chép tay DTO. 📌 Cùng nguyên tắc `lib/stock.ts`/`lib/order.ts`/`lib/pos.ts`
 * (`D-33`): BE đổi tên trường ⇒ `tsc` đỏ ngay, không có chuyện UI đọc `undefined` trong im lặng.
 *
 * 🔒 **Enum:** DTO trả **chuỗi** (`"Retail"`, `"Call"`) cho dễ đọc; bảng nhãn dưới đây là **một** chỗ tra
 * (không rải `if` trong từng page — thêm loại mới mà quên một chỗ là màn hình hiện mã thô).
 */
import type { components } from "@ecommerce/api-client/schema";

type Schemas = components["schemas"];

/**
 * 🔑 **`Req<T>` = bỏ tính "tuỳ chọn" của thuộc tính** — schema OpenAPI sinh `prop?: T` cho mọi thuộc tính
 * **không** có `[Required]`, kể cả thuộc tính server **luôn** trả (ví dụ `id`, `status`).
 * 📌 Không bọc `Req` thì mỗi lần đọc `k.id` phải viết `k.id!` — và **`!` rải khắp page** là cách chắc chắn
 * nhất để một trường **thật sự** nullable lọt qua mà không ai kiểm.
 * ⚠️ `Req` **không** làm mất `| null`: trường nullable vẫn là `T | null` ⇒ vẫn phải xử lý (đúng ý).
 */
type Req<T> = Required<T>;

/** Phần tử của một mảng (đã bỏ `null`): `Elm<Foo[] | null>` ⇒ `Foo`. */
type Elm<T> = NonNullable<T> extends ReadonlyArray<infer U> ? U : never;

type RawCustomer = Schemas["eCommerce.Customers.CustomerDto"];
type RawDetail = Schemas["eCommerce.Customers.CustomerDetailDto"];
type RawTabCounts = Schemas["eCommerce.Customers.CustomerTabCountsDto"];
type RawMatrix = Schemas["eCommerce.Customers.PermissionMatrixDto"];

export type CustomerDto = Req<RawCustomer>;
export type CustomerDetailDto = Req<RawDetail>;
export type CustomerTabCountsDto = Req<RawTabCounts>;
export type CustomerTabDto = Req<Elm<RawTabCounts["tabs"]>>;
export type CongNoHoaDonDto = Req<Elm<RawDetail["congNo"]>>;
export type CustomerCareDto = Req<Schemas["eCommerce.Customers.CustomerCareDto"]>;
export type CustomerLevelDto = Req<Schemas["eCommerce.Customers.CustomerLevelDto"]>;
export type DoiChieuSoDuDto = Req<Schemas["eCommerce.Customers.DoiChieuSoDuDto"]>;

export type StaffDto = Req<Schemas["eCommerce.Customers.StaffDto"]>;
export type StoreDto = Req<Schemas["eCommerce.Customers.StoreDto"]>;
export type DepartmentDto = Req<Schemas["eCommerce.Customers.DepartmentDto"]>;
export type RoleDataScopeDto = Req<Schemas["eCommerce.Customers.RoleDataScopeDto"]>;

export type PermissionMatrixDto = Req<RawMatrix>;
export type PermissionGroupDto = Req<Elm<RawMatrix["groups"]>>;
export type PermissionDto = Req<Elm<PermissionGroupDto["permissions"]>>;

export type PhamViCuaToiDto = Req<Schemas["eCommerce.Customers.PhamViCuaToiDto"]>;

/** Loại khách — DTO trả **chuỗi** (`"Retail"`), bảng nhãn tra theo tên. */
export const CUSTOMER_TYPE_LABEL: Record<string, string> = {
  Retail: "Khách lẻ",
  Wholesale: "Khách buôn",
  Agency: "Đại lý",
};

/** 5 loại chăm sóc — đúng danh mục của `CustomerCareType` (`WP-07`). */
export const CARE_TYPE_OPTIONS: ReadonlyArray<{ value: number; name: string; label: string }> = [
  { value: 0, name: "Call", label: "Gọi điện" },
  { value: 1, name: "Message", label: "Nhắn tin" },
  { value: 2, name: "Email", label: "Email" },
  { value: 3, name: "Meeting", label: "Gặp trực tiếp" },
  { value: 4, name: "Other", label: "Khác" },
];

export function careLabel(name: string | null | undefined): string {
  if (!name) return "—";
  return CARE_TYPE_OPTIONS.find((x) => x.name === name)?.label ?? name;
}

export function customerTypeLabel(value: string | null | undefined): string {
  if (!value) return "—";
  return CUSTOMER_TYPE_LABEL[value] ?? value;
}

/**
 * Nhãn **phạm vi dữ liệu** — 📌 đúng 3 giá trị của `PhamViDuLieu`.
 * 🔑 Nhãn nói **hệ quả** (*"chỉ dữ liệu của tôi"*) chứ không chỉ tên enum: người vận hành đang chọn
 * *"ai thấy gì"*, không chọn *"Own"*.
 */
export const SCOPE_OPTIONS: ReadonlyArray<{ value: number; name: string; label: string; note: string }> = [
  { value: 0, name: "All", label: "Tất cả", note: "Thấy dữ liệu của cả doanh nghiệp" },
  { value: 1, name: "Store", label: "Của cửa hàng", note: "Chỉ dữ liệu thuộc kho của cửa hàng mình" },
  { value: 2, name: "Own", label: "Chỉ của tôi", note: "Chỉ bản ghi do chính mình tạo" },
];

/**
 * Các **tab của màn chi tiết khách** — 5 tab theo `07 §WP-07`.
 * 📌 Định nghĩa ở **một** chỗ: page render tab từ mảng này, và `?tab=` được kiểm bằng chính nó ⇒
 * không có chuyện tab hiện ra mà không có nội dung (hoặc ngược lại).
 */
export const CUSTOMER_DETAIL_TABS: ReadonlyArray<{ key: string; label: string }> = [
  { key: "tong-quan", label: "Tổng quan" },
  { key: "don-hang", label: "Đơn hàng" },
  { key: "hoa-don", label: "Hoá đơn bán lẻ" },
  { key: "cong-no", label: "Công nợ" },
  { key: "cham-soc", label: "Chăm sóc" },
];

export function detailTab(key: string | null | undefined): string {
  return CUSTOMER_DETAIL_TABS.some((x) => x.key === key) ? (key as string) : "tong-quan";
}

/** Trang kết quả của ABP (`PagedResultDto`) — nhận cả `totalCount`/`total` (cùng lý do `lib/order.ts`). */
export type Page<T> = { items: T[]; total: number };

export function toPage<T>(raw: { items?: T[] | null; totalCount?: number; total?: number } | null): Page<T> {
  return { items: raw?.items ?? [], total: raw?.totalCount ?? raw?.total ?? 0 };
}
