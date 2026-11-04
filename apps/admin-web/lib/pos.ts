/**
 * **HỢP ĐỒNG DỮ LIỆU của màn POS** (`WP-06`) — type lấy **trực tiếp** từ `schema.d.ts` (sinh tự OpenAPI),
 * không chép tay DTO. 📌 Cùng nguyên tắc `lib/stock.ts`/`lib/order.ts` (`D-33`): BE đổi tên trường ⇒ `tsc` đỏ
 * ngay, không có chuyện UI đọc `undefined` trong im lặng.
 *
 * 🔒 **Tiền KHÔNG tính ở đây để gửi lên:** server tính bằng `TinhTienHoaDon`. UI chỉ tính **để hiển thị**
 * (khách nhìn thấy số trước khi bấm F9) — nếu hai bên lệch, **server thắng** và UI hiện số server trả về.
 */
import type { components } from "@ecommerce/api-client/schema";

type Schemas = components["schemas"];

export type RetailBillDto = Schemas["eCommerce.Application.Contracts.Pos.RetailBillDto"];
export type RetailBillLineDto = Schemas["eCommerce.Application.Contracts.Pos.RetailBillLineDto"];
export type RetailBillPaymentDto = Schemas["eCommerce.Application.Contracts.Pos.RetailBillPaymentDto"];
export type CreateRetailBillDto = Schemas["eCommerce.Application.Contracts.Pos.CreateRetailBillDto"];
export type CreateRetailBillLineDto = Schemas["eCommerce.Application.Contracts.Pos.CreateRetailBillLineDto"];
export type CreateRetailBillPaymentDto =
  Schemas["eCommerce.Application.Contracts.Pos.CreateRetailBillPaymentDto"];
export type CreateRetailBillResultDto = Schemas["eCommerce.Application.Contracts.Pos.CreateRetailBillResultDto"];
export type CashSessionDto = Schemas["eCommerce.Application.Contracts.Pos.CashSessionDto"];
export type OpenCashSessionDto = Schemas["eCommerce.Application.Contracts.Pos.OpenCashSessionDto"];
export type CloseCashSessionDto = Schemas["eCommerce.Application.Contracts.Pos.CloseCashSessionDto"];
export type RetailReturnDto = Schemas["eCommerce.Application.Contracts.Pos.RetailReturnDto"];
export type CreateRetailReturnDto = Schemas["eCommerce.Application.Contracts.Pos.CreateRetailReturnDto"];
export type CreateRetailReturnLineDto =
  Schemas["eCommerce.Application.Contracts.Pos.CreateRetailReturnLineDto"];

export type RetailPaymentMethod = Schemas["eCommerce.Pos.RetailPaymentMethod"];
export type RetailBillStatus = Schemas["eCommerce.Pos.RetailBillStatus"];

/**
 * **Phương thức thanh toán** — số là hợp đồng dữ liệu (`D-33`/`D-38`): 1 tiền mặt · 2 chuyển khoản ·
 * 3 quẹt thẻ · 4 điểm · 5 trả góp · 20 khác.
 * 📌 *"Tiền khách đưa"* **không** phải phương thức (nó là cột `customerGivenAmount`) — xem `Enums.Pos.cs`.
 */
export const PAYMENT_OPTIONS: ReadonlyArray<{ value: RetailPaymentMethod; label: string; icon: string }> = [
  { value: 1, label: "Tiền mặt", icon: "💵" },
  { value: 2, label: "Chuyển khoản", icon: "🏦" },
  { value: 3, label: "Quẹt thẻ", icon: "💳" },
  { value: 4, label: "Tiêu điểm", icon: "⭐" },
  { value: 5, label: "Trả góp", icon: "📆" },
  { value: 20, label: "Khác", icon: "•" },
];

export function paymentLabel(value: number | string | null | undefined): string {
  if (value === null || value === undefined) return "—";
  const so = typeof value === "string" ? soCuaPhuongThuc(value) : value;
  return PAYMENT_OPTIONS.find((x) => x.value === so)?.label ?? String(value);
}

export function paymentIcon(value: number | string | null | undefined): string {
  if (value === null || value === undefined) return "•";
  const so = typeof value === "string" ? soCuaPhuongThuc(value) : value;
  return PAYMENT_OPTIONS.find((x) => x.value === so)?.icon ?? "•";
}

/** Tên enum (API trả **chuỗi** cho `method` trong DTO) → số. */
function soCuaPhuongThuc(ten: string): number {
  switch (ten) {
    case "Cash":
      return 1;
    case "BankTransfer":
      return 2;
    case "Card":
      return 3;
    case "Points":
      return 4;
    case "Installment":
      return 5;
    default:
      return 20;
  }
}

export const BILL_STATUS_LABEL: Record<string, string> = {
  Completed: "Đã bán",
  Voided: "Đã huỷ",
};

/** **1 điểm = 1.000 đ** — khớp `TinhTienHoaDon.TyLeDiemMacDinh` (cấu hình được ở `WP-07`). */
export const TY_LE_DIEM = 1000;

/** Dòng hàng **đang soạn** trên tab hoá đơn (chưa gửi server). */
export type DongDraft = {
  sku: string;
  productName?: string | null;
  quantity: number;
  unitPrice: number;
  /** Chiết khấu **theo dòng** — dùng cái này **hoặc** `discountAmount` của hoá đơn, không cả hai. */
  discountAmount: number;
  vatPercent?: number | null;
};

export type ThanhToanDraft = {
  method: RetailPaymentMethod;
  amount: number;
  referenceCode?: string;
};

/** Một **tab hoá đơn** — lưu vào IndexedDB nên F5 **không** mất tab (yêu cầu `04 §6.3`). */
export type TabHoaDon = {
  /** 🚨 Khoá idempotency do **máy POS** sinh, KHÔNG đổi giữa các lần gửi (offline retry). */
  clientBillId: string;
  /**
   * Mốc tạo tab (ISO) — **dùng để giữ THỨ TỰ tab sau khi F5**.
   * 🚨 VÌ SAO CẦN: IndexedDB `getAll()` trả theo **thứ tự khoá** (`clientBillId` là UUID ngẫu nhiên) ⇒ không có
   * mốc này thì sau khi tải lại, tab **đổi chỗ** so với lúc thu ngân đang làm (đã đo: tab có dòng hàng nhảy
   * sang vị trí 2) ⇒ tưởng mất hàng. `null` với tab tạo trước khi có trường này.
   */
  taoLuc?: string | null;
  lines: DongDraft[];
  customerId?: string | null;
  customerName?: string | null;
  /** Chiết khấu **tổng hoá đơn** (F6) — bị server chặn (`0004`) nếu có chiết khấu dòng. */
  discountAmount: number;
  pointsUsed: number;
  /** Tiền mặt khách **đưa** (để tính tiền thối). */
  customerGivenAmount: number;
  /** Hạn thanh toán (bắt buộc khi còn nợ) — `YYYY-MM-DD`. */
  dueDate?: string | null;
  note?: string | null;
  payments: ThanhToanDraft[];
  /** `draft` = đang soạn; `offline` = đã lưu máy, CHƯA đồng bộ; `error` = server từ chối. */
  trangThai: "draft" | "saving" | "offline" | "error";
  /** Số hoá đơn server cấp (sau khi lưu) hoặc thông báo lỗi. */
  ketQua?: string | null;
  loi?: string | null;
};

/** Trang kết quả của `/api/v1/pos/*` — 📌 `PagedResultDto` của ABP (`{items,totalCount}`). */
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

export function tabMoi(): TabHoaDon {
  return {
    clientBillId: crypto.randomUUID(),
    taoLuc: new Date().toISOString(),
    lines: [],
    discountAmount: 0,
    pointsUsed: 0,
    customerGivenAmount: 0,
    payments: [],
    trangThai: "draft",
  };
}

// ── TÍNH TIỀN ĐỂ HIỂN THỊ (server vẫn là nguồn sự thật) ─────────────────────────────────────────────

export type TongTamTinh = {
  tienHang: number;
  chietKhauDong: number;
  tienVat: number;
  chietKhauHoaDon: number;
  tienDiem: number;
  phaiTra: number;
  daTra: number;
  tienThua: number;
  conNo: number;
};

/**
 * Tính tổng **để hiện trên màn hình** — **cùng công thức** `TinhTienHoaDon` (VAT trên thành tiền dòng sau
 * chiết khấu dòng; chiết khấu tổng và điểm trừ ở bước cuối).
 * ⚠️ Đây **không** phải nơi quyết định số tiền: nếu lệch, server thắng và UI hiển thị lại số server trả.
 */
export function tinhTam(tab: TabHoaDon): TongTamTinh {
  let tienHang = 0;
  let chietKhauDong = 0;
  let tienVat = 0;

  for (const d of tab.lines) {
    const thanhTien = d.quantity * d.unitPrice - d.discountAmount;
    tienHang += thanhTien;
    chietKhauDong += d.discountAmount;

    if (d.vatPercent !== null && d.vatPercent !== undefined) {
      tienVat += (thanhTien * d.vatPercent) / 100;
    }
  }

  const tienDiem = tab.pointsUsed * TY_LE_DIEM;
  const phaiTra = Math.max(0, tienHang - tab.discountAmount - tienDiem + tienVat);
  const daTra = tab.payments.reduce((tong, p) => tong + p.amount, 0);
  const tienThua = daTra > phaiTra ? daTra - phaiTra : 0;
  const conNo = daTra < phaiTra ? phaiTra - daTra : 0;

  return {
    tienHang,
    chietKhauDong,
    tienVat,
    chietKhauHoaDon: tab.discountAmount,
    tienDiem,
    phaiTra,
    daTra,
    tienThua,
    conNo,
  };
}

// ── IndexedDB: tab hoá đơn + hàng đợi offline ───────────────────────────────────────────────────────

const DB_NAME = "pos_bus_default";
const DB_VERSION = 1;
const STORE_TABS = "posBills";
const STORE_OUTBOX = "posOutbox";

/**
 * Mở DB. 📌 Dùng **IndexedDB** (không `localStorage`): F5 không mất tab **và** dữ liệu sống qua cả khi
 * tab trình duyệt bị đóng; `localStorage` giới hạn 5 MB và chặn đồng bộ (đúng lý do Nhanh lưu `posBills`
 * trong IndexedDB — `nhanh-pos-module-map.md` §5.1).
 */
function moDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (typeof indexedDB === "undefined") {
      reject(new Error("Trình duyệt KHÔNG hỗ trợ IndexedDB ⇒ tab hoá đơn sẽ mất khi F5."));
      return;
    }

    const yeuCau = indexedDB.open(DB_NAME, DB_VERSION);

    yeuCau.onupgradeneeded = () => {
      const db = yeuCau.result;
      if (!db.objectStoreNames.contains(STORE_TABS)) {
        db.createObjectStore(STORE_TABS, { keyPath: "clientBillId" });
      }
      if (!db.objectStoreNames.contains(STORE_OUTBOX)) {
        db.createObjectStore(STORE_OUTBOX, { keyPath: "clientBillId" });
      }
    };

    yeuCau.onsuccess = () => resolve(yeuCau.result);
    yeuCau.onerror = () => reject(yeuCau.error ?? new Error("Không mở được IndexedDB."));
  });
}

async function docStore<T>(store: string): Promise<T[]> {
  const db = await moDb();

  return new Promise((resolve, reject) => {
    const gd = db.transaction(store, "readonly").objectStore(store).getAll();
    gd.onsuccess = () => resolve((gd.result ?? []) as T[]);
    gd.onerror = () => reject(gd.error);
  });
}

async function ghiStore<T extends { clientBillId: string }>(store: string, value: T): Promise<void> {
  const db = await moDb();

  return new Promise((resolve, reject) => {
    const tx = db.transaction(store, "readwrite");
    tx.objectStore(store).put(value);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

async function xoaStore(store: string, clientBillId: string): Promise<void> {
  const db = await moDb();

  return new Promise((resolve, reject) => {
    const tx = db.transaction(store, "readwrite");
    tx.objectStore(store).delete(clientBillId);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

/** Đọc các tab đã lưu (F5 khôi phục). Sắp xếp theo `clientBillId` để thứ tự ổn định giữa các lần tải. */
export const docTabs = () => docStore<TabHoaDon>(STORE_TABS);
export const luuTab = (tab: TabHoaDon) => ghiStore(STORE_TABS, tab);
export const xoaTab = (clientBillId: string) => xoaStore(STORE_TABS, clientBillId);

/** Hàng đợi **chưa đồng bộ** (rớt mạng / server 5xx) — chỉ chứa tab đã đóng gói đủ payload. */
export type OutboxItem = { clientBillId: string; payload: CreateRetailBillDto; luc: string };

export const docOutbox = () => docStore<OutboxItem>(STORE_OUTBOX);
export const dayVaoOutbox = (item: OutboxItem) => ghiStore(STORE_OUTBOX, item);
export const xoaOutbox = (clientBillId: string) => xoaStore(STORE_OUTBOX, clientBillId);

// ── Đóng gói payload gửi server ────────────────────────────────────────────────────────────────────

/**
 * Tab → payload `POST /api/v1/pos/bills`.
 * 🚨 **KHÔNG gửi** `totalPayment`/`paidAmount`/`debtAmount`: server **tự tính** (`TinhTienHoaDon`);
 * nhận tiền từ client là mở đường sửa giá.
 */
export function thanhPayload(tab: TabHoaDon, warehouseId: string, cashSessionId?: string | null): CreateRetailBillDto {
  return {
    clientBillId: tab.clientBillId,
    warehouseId,
    cashSessionId: cashSessionId ?? null,
    customerId: tab.customerId ?? null,
    discountAmount: tab.discountAmount,
    pointsUsed: tab.pointsUsed,
    customerGivenAmount: tab.customerGivenAmount,
    // 📌 Hạn thanh toán chỉ có nghĩa khi CÒN NỢ; vẫn gửi khi khách có hẹn (server bỏ qua nếu không nợ).
    dueDateUtc: tab.dueDate ? new Date(`${tab.dueDate}T23:59:59.000+07:00`).toISOString() : null,
    note: tab.note ?? null,
    lines: tab.lines.map((d) => ({
      sku: d.sku,
      quantity: d.quantity,
      unitPrice: d.unitPrice,
      discountAmount: d.discountAmount,
      vatPercent: d.vatPercent ?? null,
      productName: d.productName ?? null,
    })),
    payments: tab.payments.map((p) => ({
      method: p.method,
      amount: p.amount,
      referenceCode: p.referenceCode ?? null,
    })),
  } as CreateRetailBillDto;
}
