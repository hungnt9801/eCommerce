/**
 * **HỢP ĐỒNG DỮ LIỆU của màn ĐƠN HÀNG** (`WP-05`) — type lấy **trực tiếp** từ `schema.d.ts` (sinh tự OpenAPI),
 * không chép tay DTO. 📌 Cùng nguyên tắc `lib/stock.ts` (D-33): BE đổi tên trường ⇒ `tsc` đỏ ngay, không có
 * chuyện UI đọc `undefined` trong im lặng.
 *
 * 🔒 **Enum gửi dạng SỐ** (`D-33`): số là hợp đồng dữ liệu với Nhanh; tên chỉ để đọc log/DTO.
 */
import type { components } from "@ecommerce/api-client/schema";

type Schemas = components["schemas"];

export type OrderListItemDto = Schemas["eCommerce.Application.Contracts.Sales.OrderListItemDto"];
export type OrderDetailDto = Schemas["eCommerce.Application.Contracts.Sales.OrderDetailDto"];
export type OrderLineItemDto = Schemas["eCommerce.Application.Contracts.Sales.OrderLineItemDto"];
export type OrderTabCountsDto = Schemas["eCommerce.Application.Contracts.Sales.OrderTabCountsDto"];
export type OrderStatusDto = Schemas["eCommerce.Application.Contracts.Sales.OrderStatusDto"];
export type OrderStatusHistoryDto = Schemas["eCommerce.Application.Contracts.Sales.OrderStatusHistoryDto"];

export type FulfillmentStatus = Schemas["eCommerce.FulfillmentStatus"];
export type OrderType = Schemas["eCommerce.OrderType"];
export type OrderSource = Schemas["eCommerce.OrderSource"];
export type OrderReasonCode = Schemas["eCommerce.OrderReasonCode"];

/**
 * Trang kết quả của `/api/v1/orders` — ⚠️ **KHÁC** `/api/v1/stock/*`:
 * - kho trả `ApiPage` (`{items,total,page,pageSize}`),
 * - đơn trả `PagedResultDto` của ABP (`{items,totalCount}`).
 * 📌 Nhận **cả hai** tên (`total` / `totalCount`) ở **một** chỗ, để page không phải biết DTO nào —
 * quên `totalCount` thì `total = 0` và phân trang **im lặng** hiện "0 / 0" dù có dữ liệu.
 */
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };

export function toPage<T>(raw: {
  items?: T[] | null;
  total?: number;
  totalCount?: number;
  page?: number;
  pageSize?: number;
} | null): Page<T> {
  return {
    items: raw?.items ?? [],
    total: raw?.total ?? raw?.totalCount ?? 0,
    page: raw?.page ?? 1,
    pageSize: raw?.pageSize ?? 0,
  };
}

// ── 18 trạng thái thực hiện (đúng nhãn Nhanh — `nhanh-pos-module-map.md` §6.2) ──────────────────────

export const FULFILLMENT_LABEL: Record<number, string> = {
  0: "Mới",
  1: "Đã xác nhận",
  2: "Đang đóng gói",
  3: "Chờ thu gom",
  4: "Đang chuyển",
  5: "Thành công",
  6: "Thất bại",
  7: "Đang hoàn",
  8: "Đã hoàn",
  9: "Chờ xác nhận",
  10: "Đang xác nhận",
  11: "Hết hàng",
  12: "Đổi kho hàng",
  13: "Đã đóng gói",
  14: "Khách huỷ",
  15: "Hệ thống huỷ",
  16: "HVC huỷ",
  17: "Xác nhận hoàn",
};

/** 3 trạng thái **huỷ** — dùng cho hộp chọn "ai huỷ" của màn chi tiết (quyền `Orders.Cancel`). */
export const CANCEL_OPTIONS: ReadonlyArray<{ value: FulfillmentStatus; label: string }> = [
  { value: 14, label: "Khách huỷ" },
  { value: 15, label: "Hệ thống huỷ" },
  { value: 16, label: "HVC huỷ" },
];

/** 24 lý do nghiệp vụ của Nhanh (`OrderReasonCode`) — dùng cho select "lý do huỷ". */
export const REASON_OPTIONS: ReadonlyArray<{ value: OrderReasonCode; label: string }> = [
  { value: 1, label: "Đặt nhầm" },
  { value: 2, label: "Phí ship cao" },
  { value: 3, label: "Không muốn chuyển khoản" },
  { value: 4, label: "Đơn trùng" },
  { value: 5, label: "Không gọi được khách" },
  { value: 6, label: "Hết hàng" },
  { value: 8, label: "Chờ chuyển khoản" },
  { value: 9, label: "Khách không thích" },
  { value: 10, label: "Không hài lòng NV vận chuyển" },
  { value: 11, label: "Giao chậm" },
  { value: 12, label: "Đã mua tại cửa hàng" },
  { value: 14, label: "Sai địa chỉ" },
  { value: 16, label: "Không muốn mua nữa" },
  { value: 18, label: "Lý do khác" },
  { value: 19, label: "Không liên hệ được người gửi" },
  { value: 20, label: "Người gửi không bán online/ngoại tỉnh" },
  { value: 22, label: "Không bàn giao cho HVC" },
  { value: 23, label: "HVC lấy hàng muộn" },
  { value: 24, label: "Sai địa chỉ kho lấy" },
  { value: 25, label: "HVC làm mất hàng" },
  { value: 26, label: "Người gửi tự vận chuyển" },
  { value: 29, label: "Người gửi không xử lý đơn" },
  { value: 30, label: "Sai giá" },
  { value: 35, label: "Khách đi vắng" },
];

/** **10 loại đơn** đo được của Nhanh (`OrderType`). */
export const TYPE_OPTIONS: ReadonlyArray<{ value: OrderType; label: string }> = [
  { value: 1, label: "Giao hàng tận nhà" },
  { value: 2, label: "Mua tại quầy" },
  { value: 3, label: "Đặt trước" },
  { value: 5, label: "Đổi quà" },
  { value: 10, label: "Xin báo giá" },
  { value: 12, label: "Đổi sản phẩm" },
  { value: 14, label: "Khách trả lại hàng" },
  { value: 15, label: "Hàng chuyển kho" },
  { value: 16, label: "Đơn hoàn một phần" },
  { value: 17, label: "Đền bù mất hàng" },
];

/** **14 nguồn đơn** đo được của Nhanh (`OrderSource`). */
export const SOURCE_OPTIONS: ReadonlyArray<{ value: OrderSource; label: string }> = [
  { value: 1, label: "Admin" },
  { value: 2, label: "Website" },
  { value: 10, label: "API" },
  { value: 20, label: "Facebook" },
  { value: 21, label: "Instagram" },
  { value: 41, label: "Lazada" },
  { value: 42, label: "Shopee" },
  { value: 43, label: "Sendo" },
  { value: 45, label: "Tiki" },
  { value: 48, label: "TikTok Shop" },
  { value: 49, label: "Zalo OA" },
  { value: 50, label: "Shopee chat" },
  { value: 51, label: "Lazada chat" },
  { value: 52, label: "Zalo cá nhân" },
];

export const PAYMENT_LABEL: Record<string, string> = {
  Unpaid: "Chưa trả",
  Pending: "Chờ thanh toán",
  Paid: "Đã trả",
  PartiallyRefunded: "Hoàn một phần",
  Refunded: "Đã hoàn tiền",
  Failed: "Thanh toán lỗi",
};

export const RETURN_LABEL: Record<string, string> = {
  Open: "Không trả",
  ReturnRequested: "Yêu cầu trả",
  ReturnApproved: "Đã duyệt trả",
  ReturnRejected: "Từ chối trả",
  Returned: "Đã trả",
  Cancelled: "Đã huỷ",
  PartiallyReturned: "Trả một phần",
};

export function typeLabel(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return TYPE_OPTIONS.find((x) => x.value === value)?.label ?? String(value);
}

export function sourceLabel(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return SOURCE_OPTIONS.find((x) => x.value === value)?.label ?? String(value);
}

export function fulfillmentLabel(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return FULFILLMENT_LABEL[value] ?? String(value);
}

export function reasonLabel(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return REASON_OPTIONS.find((x) => x.value === value)?.label ?? String(value);
}

// ── Trạng thái → màu badge (cùng bảng màu của `components/inventory/Badges.tsx`) ─────────────────────

export type BadgeTone = "success" | "warn" | "danger" | "info" | "muted";

/** Nhóm màu suy từ **trạng thái thực hiện** — không phải từ nhãn (nhãn có thể đổi câu chữ). */
export function toneOf(status: number | null | undefined, isCancelled: boolean): BadgeTone {
  if (isCancelled) return "danger";

  switch (status) {
    case 5:
      return "success";
    case 6:
    case 8:
      return "danger";
    case 7:
    case 11:
    case 17:
      return "warn";
    case 3:
    case 4:
    case 9:
    case 10:
    case 12:
      return "info";
    default:
      return "muted";
  }
}

// ── Chuỗi truy vấn ──────────────────────────────────────────────────────────────────────────────────

export type OrderFilter = {
  tab?: string;
  q?: string;
  fulfillmentStatus?: FulfillmentStatus;
  type?: OrderType;
  source?: OrderSource;
  warehouseId?: string;
  fromUtc?: string | null;
  toUtc?: string | null;
  page?: number;
  pageSize?: number;
};

export function orderQueryString(filter: OrderFilter): string {
  const q = new URLSearchParams();

  if (filter.tab) q.set("tab", filter.tab);
  if (filter.q) q.set("q", filter.q);
  // ⚠️ `status = 0` là giá trị **hợp lệ** ⇒ kiểm `!== undefined`, KHÔNG kiểm truthy.
  if (filter.fulfillmentStatus !== undefined) q.set("fulfillmentStatus", String(filter.fulfillmentStatus));
  if (filter.type !== undefined) q.set("type", String(filter.type));
  if (filter.source !== undefined) q.set("source", String(filter.source));
  if (filter.warehouseId) q.set("warehouseId", filter.warehouseId);
  if (filter.fromUtc) q.set("fromUtc", filter.fromUtc);
  if (filter.toUtc) q.set("toUtc", filter.toUtc);
  q.set("page", String(filter.page ?? 1));
  q.set("pageSize", String(filter.pageSize ?? 20));

  return q.toString();
}

/**
 * Các bước **đi tiếp được** — UI dùng `detail.allowedTransitions` do **server** trả (suy từ bảng cạnh của
 * domain). Hàm này chỉ để render ổn định theo thứ tự enum, KHÔNG tự quyết định luật.
 */
export function sortTransitions(list: readonly FulfillmentStatus[] | null | undefined): FulfillmentStatus[] {
  return [...(list ?? [])].sort((a, b) => a - b);
}
