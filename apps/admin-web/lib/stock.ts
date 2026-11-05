/**
 * **HỢP ĐỒNG DỮ LIỆU của màn kho** — lấy **từ `schema.d.ts` sinh tự động** (`WP-02`), không chép tay.
 *
 * 🚨 **VÌ SAO KHÔNG TỰ KHAI LẠI TYPE:** `schema.d.ts` là bản dịch của `openapi.v1.json` (12 path `/api/v1`);
 * chép tay DTO vào app thì lần sau BE đổi tên trường, `tsc` **vẫn xanh** và UI đọc `undefined` — đúng loại
 * lỗi im lặng mà `WP-09` (openapi-drift) sinh ra để chặn. Ở đây mọi trường đi qua `components["schemas"][…]`.
 *
 * 📌 Enum **nhận cả tên và số** (ASP.NET bind cả hai), nhưng UI dùng **SỐ** cho tham số lọc và POST body:
 * số là **hợp đồng dữ liệu** với Nhanh/Open API (`D-28`), tên chỉ để đọc log/DTO.
 */
import type { components } from "@ecommerce/api-client/schema";

type Schemas = components["schemas"];

export type StockDocumentDto = Schemas["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
export type StockDocumentLineDto = Schemas["eCommerce.Application.Contracts.Inventory.StockDocumentLineDto"];
export type StockBalanceDto = Schemas["eCommerce.Application.Contracts.Inventory.StockBalanceDto"];
export type CreateStockDocumentDto =
  Schemas["eCommerce.Application.Contracts.Inventory.CreateStockDocumentDto"];
export type CreateStockDocumentLineDto =
  Schemas["eCommerce.Application.Contracts.Inventory.CreateStockDocumentLineDto"];
export type WarehouseDto = Schemas["eCommerce.Application.Contracts.Inventory.WarehouseDto"];

export type StockDocumentType = Schemas["eCommerce.Inventory.StockDocumentType"];
export type StockDocumentMode = Schemas["eCommerce.Inventory.StockDocumentMode"];
export type StockDocumentDirection = Schemas["eCommerce.Inventory.StockDocumentDirection"];
export type StockDocumentStatus = Schemas["eCommerce.Inventory.StockDocumentStatus"];
export type StockCounterpartyType = Schemas["eCommerce.Inventory.StockCounterpartyType"];

/** Trang kết quả chuẩn của `/api/v1` (`ApiPage<T>`): `{items,total,page,pageSize}`. */
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };

export function toPage<T>(raw: { items?: T[] | null; total?: number; page?: number; pageSize?: number } | null): Page<T> {
  return {
    items: raw?.items ?? [],
    total: raw?.total ?? 0,
    page: raw?.page ?? 1,
    pageSize: raw?.pageSize ?? 0,
  };
}

// ── Nhãn hiển thị (giữ đúng từ ngữ Nhanh) ────────────────────────────────────────────────────────────

export const TYPE_LABEL: Record<string, string> = {
  GoodsReceipt: "Nhập kho",
  StockTransfer: "Chuyển kho",
  Stocktake: "Kiểm kho",
  CustomerReturn: "Khách trả hàng",
  GoodsIssue: "Xuất kho",
};

export const STATUS_LABEL: Record<string, string> = {
  Draft: "Nháp",
  Posted: "Đã ghi sổ",
  Cancelled: "Đã huỷ",
};

export const STATUS_VALUE = { Draft: 0, Posted: 1, Cancelled: 2 } as const;

export const DIRECTION_LABEL: Record<string, string> = {
  In: "Nhập",
  Out: "Xuất",
};

export const DIRECTION_VALUE: Record<string, StockDocumentDirection> = {
  In: 1,
  Out: 2,
};

/**
 * 15 giá trị `StockDocumentMode` **đúng danh mục Nhanh** (`D-28`): không có 7/9/11/12.
 * 📌 Nhãn ở đây chỉ để **đọc**; giá trị số mới là thứ gửi xuống API.
 */
export const MODE_OPTIONS: ReadonlyArray<{ value: StockDocumentMode; name: string; label: string }> = [
  { value: 1, name: "Delivery", label: "Giao hàng" },
  { value: 2, name: "Retail", label: "Bán lẻ" },
  { value: 3, name: "Transfer", label: "Chuyển kho" },
  { value: 4, name: "RetailGift", label: "Quà tặng HĐ bán lẻ" },
  { value: 5, name: "Supplier", label: "Nhà cung cấp" },
  { value: 6, name: "Wholesale", label: "Bán sỉ" },
  { value: 8, name: "Stocktake", label: "Kiểm kho" },
  { value: 10, name: "Other", label: "Khác" },
  { value: 13, name: "WarrantyBill", label: "Phiếu bảo hành" },
  { value: 14, name: "WarrantyCenter", label: "Trung tâm bảo hành" },
  { value: 15, name: "RepairBill", label: "Phiếu sửa chữa" },
  { value: 16, name: "WarrantyPart", label: "Linh kiện bảo hành" },
  { value: 17, name: "WholesaleGift", label: "Quà tặng HĐ bán sỉ" },
  { value: 18, name: "OrderGift", label: "Quà tặng đơn hàng" },
  { value: 19, name: "Combo", label: "Combo" },
];

/** DTO trả `Mode` dạng **tên** (`"Supplier"`) ⇒ cần bảng tra tên → nhãn. */
export function modeLabel(name: string | null | undefined): string {
  if (!name) return "—";
  return MODE_OPTIONS.find((m) => m.name === name)?.label ?? name;
}

export function modeLabelByValue(value: StockDocumentMode | null | undefined): string {
  if (value === null || value === undefined) return "Tất cả";
  return MODE_OPTIONS.find((m) => m.value === value)?.label ?? String(value);
}

export const COUNTERPARTY_LABEL: Record<string, string> = {
  Customer: "Khách hàng",
  Supplier: "Nhà cung cấp",
  InstallmentService: "Dịch vụ trả góp",
  Staff: "Nhân viên",
  Other: "Khác",
  Marketplace: "Sàn TMĐT",
};

// ── View model (đã bỏ `null` ở chỗ giao diện cần chuỗi) ─────────────────────────────────────────────

export type Bill = {
  id: string;
  number: string;
  type: string;
  status: string;
  direction: string | null;
  mode: string | null;
  warehouseId: string;
  toWarehouseId: string | null;
  occurredAtUtc: string;
  note: string | null;
  counterpartyType: string | null;
  refType: string | null;
  refId: string | null;
  discountAmount: number | null;
  paidAmount: number | null;
  idempotencyKey: string | null;
  lineCount: number;
  /** Tổng tiền hàng (`null` = có dòng chưa khai giá — D-37). */
  totalAmount: number | null;
  totalVat: number | null;
  totalPayment: number | null;
};

export function toBill(dto: StockDocumentDto): Bill {
  return {
    id: dto.id ?? "",
    number: dto.number ?? "(không số)",
    type: dto.type ?? "",
    status: dto.status ?? "Draft",
    direction: dto.direction ?? null,
    mode: dto.mode ?? null,
    warehouseId: dto.warehouseId ?? "",
    toWarehouseId: dto.toWarehouseId ?? null,
    occurredAtUtc: dto.occurredAtUtc ?? "",
    note: dto.note ?? null,
    counterpartyType: dto.counterpartyType ?? null,
    refType: dto.refType ?? null,
    refId: dto.refId ?? null,
    discountAmount: dto.discountAmount ?? null,
    paidAmount: dto.paidAmount ?? null,
    idempotencyKey: dto.idempotencyKey ?? null,
    lineCount: dto.lineCount ?? 0,
    totalAmount: dto.totalAmount ?? null,
    totalVat: dto.totalVat ?? null,
    totalPayment: dto.totalPayment ?? null,
  };
}

export type BillLine = {
  id: string;
  sku: string;
  quantity: number;
  vatPercent: number | null;
  imeiSerials: string | null;
  unitId: string | null;
  batchId: string | null;
  /** Đơn giá lúc lập phiếu (`null` = chưa khai giá). */
  unitPrice: number | null;
  /** Thành tiền = SL × đơn giá (server tính bằng `TinhTienChungTu`). */
  amount: number | null;
  vatAmount: number | null;
};

export function toLine(dto: StockDocumentLineDto): BillLine {
  return {
    id: dto.id ?? "",
    sku: dto.sku ?? "",
    quantity: dto.quantity ?? 0,
    vatPercent: dto.vatPercent ?? null,
    imeiSerials: dto.imeiSerials ?? null,
    unitId: dto.unitId ?? null,
    batchId: dto.batchId ?? null,
    unitPrice: dto.unitPrice ?? null,
    amount: dto.amount ?? null,
    vatAmount: dto.vatAmount ?? null,
  };
}

/** 12 số của một dòng tồn kho (10 số lưu + 2 số **suy ra** — API tính, UI **không** tính lại). */
export type Balance = {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  sku: string;
  onHand: number;
  reserved: number;
  transferring: number;
  shipping: number;
  holding: number;
  damaged: number;
  warranty: number;
  warrantyHolding: number;
  preOrder: number;
  waitingInbound: number;
  inWarehouse: number;
  available: number;
};

export function toBalance(dto: StockBalanceDto): Balance {
  return {
    warehouseId: dto.warehouseId ?? "",
    warehouseCode: dto.warehouseCode ?? "",
    warehouseName: dto.warehouseName ?? "",
    sku: dto.sku ?? "",
    onHand: dto.onHand ?? 0,
    reserved: dto.reserved ?? 0,
    transferring: dto.transferring ?? 0,
    shipping: dto.shipping ?? 0,
    holding: dto.holding ?? 0,
    damaged: dto.damaged ?? 0,
    warranty: dto.warranty ?? 0,
    warrantyHolding: dto.warrantyHolding ?? 0,
    preOrder: dto.preOrder ?? 0,
    waitingInbound: dto.waitingInbound ?? 0,
    inWarehouse: dto.inWarehouse ?? 0,
    available: dto.available ?? 0,
  };
}

/** Cộng số lượng của các dòng — dùng cho dòng tổng ở form tạo phiếu. */
export function sumQuantity(lines: ReadonlyArray<{ quantity: number }>): number {
  // 📌 Cộng trên số thực rồi mới format: format từng dòng rồi cộng chuỗi là nguồn lệch số.
  return lines.reduce((tong, dong) => tong + (Number.isFinite(dong.quantity) ? dong.quantity : 0), 0);
}

// ── Tiền (D-37) ──────────────────────────────────────────────────────────────────────────────────────

/**
 * Làm tròn **tiền** 2 số lẻ, half-away-from-zero — **giống** `TinhTienChungTu.LamTronTien` ở BE.
 * ⚠️ Đây là **bản dự phóng** cho form (trước khi lưu). Sau khi lưu, UI **chỉ đọc** `amount`/`totalAmount`
 * do server trả — không lấy bản dự phóng này làm số chính thức.
 */
export function lamTronTien(v: number): number {
  return (Math.sign(v) * Math.round((Math.abs(v) + Number.EPSILON) * 100)) / 100;
}

/** Thành tiền dòng = SL × đơn giá; `null` khi chưa khai giá (KHÔNG phải 0 — D-37). */
export function thanhTienDong(quantity: number, unitPrice: number | null): number | null {
  if (unitPrice === null || !Number.isFinite(unitPrice)) return null;
  return lamTronTien(quantity * unitPrice);
}

/** Tiền VAT dòng = thành tiền × %VAT/100; `null` khi thiếu giá hoặc thiếu %VAT. */
export function tienVatDong(thanhTien: number | null, vatPercent: number | null): number | null {
  if (thanhTien === null || vatPercent === null || !Number.isFinite(vatPercent)) return null;
  return lamTronTien((thanhTien * vatPercent) / 100);
}

export type TienPhieu = {
  /** `null` = có dòng chưa khai giá ⇒ không biết tổng (D-37). */
  tienHang: number | null;
  vat: number | null;
  thanhToan: number | null;
};

/**
 * Tổng **dự phóng** của form. Luật giống BE (một nguồn: `TinhTienChungTu`):
 * cộng **từng dòng đã làm tròn**; **một** dòng thiếu giá ⇒ tổng là `null`; `null` nếu không dòng nào khai VAT.
 */
export function tongTienPhieu(
  lines: ReadonlyArray<{ quantity: number; unitPrice: number | null; vatPercent: number | null }>,
  chietKhau: number,
): TienPhieu {
  let tienHang: number | null = 0;
  let vat: number | null = 0;
  let coDong = false;
  let coVat = false;

  for (const dong of lines) {
    coDong = true;

    const thanhTien = thanhTienDong(dong.quantity, dong.unitPrice);

    if (thanhTien === null) {
      tienHang = null;
    } else if (tienHang !== null) {
      tienHang = lamTronTien(tienHang + thanhTien);
    }

    if (dong.vatPercent !== null) {
      const tien = tienVatDong(thanhTien, dong.vatPercent);

      if (tien === null) {
        vat = null;
      } else if (vat !== null) {
        vat = lamTronTien(vat + tien);
        coVat = true;
      }
    }
  }

  const tongVat = coVat ? vat : null;
  const thanhToan =
    tienHang === null ? null : lamTronTien(tienHang + (tongVat ?? 0) - (Number.isFinite(chietKhau) ? chietKhau : 0));

  return { tienHang: coDong ? tienHang : null, vat: tongVat, thanhToan };
}

// ── "Chọn kiểu giao dịch → form động" (04 §5 màn #8) ────────────────────────────────────────────────

/**
 * Mỗi mục là **một cặp `(Type, Mode, Direction)` hợp lệ theo entity** (`StockDocument.ChuanHoaMode`):
 * `Transfer` chỉ đi với `StockTransfer`, `Stocktake` chỉ đi với `Stocktake`, `GoodsIssue` **bắt buộc** có mode.
 * 📌 Nhờ bảng này, form **không thể** gửi tổ hợp sai ⇒ người dùng không nhận `eCommerce:Inventory:00xx` sau khi bấm Lưu.
 */
export type BillKind = {
  key: string;
  label: string;
  prefix: string;
  type: StockDocumentType;
  mode: StockDocumentMode | null;
  direction: "In" | "Out" | null;
  counterpartyType: StockCounterpartyType | null;
  /** Có ô "kho đích" (chỉ chuyển kho). */
  needsTargetWarehouse?: boolean;
  hint?: string;
};

export const BILL_KINDS: ReadonlyArray<BillKind> = [
  {
    key: "receipt-supplier",
    label: "Nhập từ nhà cung cấp",
    prefix: "PN",
    type: 0,
    mode: 5,
    direction: "In",
    counterpartyType: 2,
  },
  {
    key: "receipt-other",
    label: "Nhập kho khác",
    prefix: "PN",
    type: 0,
    mode: 10,
    direction: "In",
    counterpartyType: 20,
  },
  {
    key: "customer-return",
    label: "Khách trả hàng",
    prefix: "PT",
    type: 3,
    mode: null,
    direction: "In",
    counterpartyType: 1,
  },
  {
    key: "issue-retail",
    label: "Xuất bán lẻ",
    prefix: "PX",
    type: 4,
    mode: 2,
    direction: "Out",
    counterpartyType: 1,
  },
  {
    key: "issue-delivery",
    label: "Xuất giao hàng",
    prefix: "PX",
    type: 4,
    mode: 1,
    direction: "Out",
    counterpartyType: 1,
  },
  {
    key: "issue-supplier",
    label: "Xuất trả nhà cung cấp",
    prefix: "PX",
    type: 4,
    mode: 5,
    direction: "Out",
    counterpartyType: 2,
  },
  {
    key: "issue-other",
    label: "Xuất kho khác",
    prefix: "PX",
    type: 4,
    mode: 10,
    direction: "Out",
    counterpartyType: 20,
  },
  {
    key: "transfer",
    label: "Chuyển kho",
    prefix: "PCK",
    type: 1,
    mode: 3,
    direction: "Out",
    counterpartyType: null,
    needsTargetWarehouse: true,
    hint: "Phiếu chuyển kho là **xuất ở kho nguồn**; bút toán nhập vào kho đích sinh khi ghi sổ.",
  },
  {
    key: "stocktake",
    label: "Kiểm kho",
    prefix: "PKK",
    type: 2,
    mode: 8,
    direction: null,
    counterpartyType: null,
    hint: "Số lượng ở đây là **số ĐẾM ĐƯỢC thực tế**, không phải số lệch — hệ thống tự tính lệch khi ghi sổ.",
  },
];

export function findKind(key: string): BillKind {
  return BILL_KINDS.find((k) => k.key === key) ?? BILL_KINDS[0];
}

/** Số phiếu gợi ý: `PN-20261103-4821` — chỉ là **gợi ý**, người dùng sửa được (số là duy nhất trên tenant). */
export function suggestNumber(prefix: string, now = new Date()): string {
  const ngay = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  })
    .format(now)
    .replaceAll("-", "");

  const ngauNhien = Math.floor(1000 + Math.random() * 9000);
  return `${prefix}-${ngay}-${ngauNhien}`;
}

/** Đổi tiền tố khi người dùng đổi kiểu giao dịch (giữ nguyên phần ngày + đuôi). */
export function withPrefix(number: string, prefix: string): string {
  const phan = number.split("-");
  if (phan.length < 2) return `${prefix}-${number}`;
  return [prefix, ...phan.slice(1)].join("-");
}

// ── Chuỗi truy vấn ──────────────────────────────────────────────────────────────────────────────────

export type BillFilter = {
  direction?: StockDocumentDirection;
  status?: StockDocumentStatus;
  mode?: StockDocumentMode;
  warehouseId?: string;
  fromUtc?: string | null;
  toUtc?: string | null;
  page?: number;
  pageSize?: number;
};

export function billQueryString(filter: BillFilter): string {
  const q = new URLSearchParams();

  // ⚠️ `status=0` (Draft) là giá trị **hợp lệ** — kiểm `!== undefined`, KHÔNG kiểm truthy.
  if (filter.direction !== undefined) q.set("direction", String(filter.direction));
  if (filter.status !== undefined) q.set("status", String(filter.status));
  if (filter.mode !== undefined) q.set("mode", String(filter.mode));
  if (filter.warehouseId) q.set("warehouseId", filter.warehouseId);
  if (filter.fromUtc) q.set("fromUtc", filter.fromUtc);
  if (filter.toUtc) q.set("toUtc", filter.toUtc);
  q.set("page", String(filter.page ?? 1));
  q.set("pageSize", String(filter.pageSize ?? 20));

  return q.toString();
}

export function balanceQueryString(filter: {
  warehouseId?: string;
  sku?: string;
  page?: number;
  pageSize?: number;
}): string {
  const q = new URLSearchParams();
  if (filter.warehouseId) q.set("warehouseId", filter.warehouseId);
  if (filter.sku) q.set("sku", filter.sku);
  q.set("page", String(filter.page ?? 1));
  q.set("pageSize", String(filter.pageSize ?? 50));
  return q.toString();
}
