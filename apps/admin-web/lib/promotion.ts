/**
 * **HỢP ĐỒNG DỮ LIỆU + HÀM THUẦN của màn khuyến mãi** — `PRO-002` / `W24-3` (phiên `N95`).
 *
 * 🚨 **Type lấy TRỰC TIẾP từ `schema.d.ts` sinh tự động** (`D-33`) — **không** chép tay DTO. BE đổi tên
 * trường ⇒ `tsc` đỏ ngay thay vì UI đọc `undefined`.
 *
 * 📌 **Enum trả SỐ** (0/1/2…) — số là **hợp đồng dữ liệu**; nhãn tiếng Việt ở đây chỉ để hiển thị.
 *
 * 🚨 **ĐỒNG HỒ GIỚI HẠN (`§10.1`, `D-288`)**: trần **DUY NHẤT** lấy từ `PromotionDetailDto.Limits` (ABP Setting
 * theo tenant). **TUYỆT ĐỐI KHÔNG** hằng số `64/8/32/1024` trong `apps/admin-web/**` — `Q1` còn `Open` nên mặc
 * định chỉ là **đề xuất**. Tải chi tiết lỗi ⇒ màn ở trạng thái **error**, **không** được fallback hằng số.
 *
 * Toàn bộ hàm dưới đây là **hàm thuần** (không DOM, không I/O) ⇒ phủ được bằng `pnpm test` (Vitest node).
 */
import type { components } from "@ecommerce/api-client/schema";

type Schemas = components["schemas"];

// ── Type DTO ─────────────────────────────────────────────────────────────────────────────────────────
export type PromotionListItemDto = Schemas["eCommerce.Promotions.Contracts.PromotionListItemDto"];
export type PromotionDetailDto = Schemas["eCommerce.Promotions.Contracts.PromotionDetailDto"];
export type PromotionDto = Schemas["eCommerce.Promotions.Contracts.PromotionDto"];
export type CreatePromotionDto = Schemas["eCommerce.Promotions.Contracts.CreatePromotionDto"];
export type PromotionRuleCatalogItemDto =
  Schemas["eCommerce.Promotions.Contracts.PromotionRuleCatalogItemDto"];
export type PromotionTreeLimitsDto = Schemas["eCommerce.Promotions.Contracts.PromotionTreeLimitsDto"];
export type PromotionTreeNodeDto = Schemas["eCommerce.Promotions.Contracts.PromotionTreeNodeDto"];
export type PromotionTreeNodeInput =
  Schemas["eCommerce.Promotions.Contracts.PromotionTreeNodeInput"];
export type PromotionTreeInput = Schemas["eCommerce.Promotions.Contracts.PromotionTreeInput"];
export type PromotionTreeValidationResultDto =
  Schemas["eCommerce.Promotions.Contracts.PromotionTreeValidationResultDto"];
export type SetPromotionStatusInput =
  Schemas["eCommerce.Promotions.Contracts.SetPromotionStatusInput"];

// ── Enum (số) ──────────────────────────────────────────────────────────────────────────────────────
export type PromotionKind = Schemas["eCommerce.Promotions.PromotionKind"]; // 0 Voucher · 1 Discount · 2 FlashSale
export type PromotionStatus = Schemas["eCommerce.Promotions.PromotionStatus"]; // 0 Draft … 5 Cancelled
export type PromotionDiscountKind = Schemas["eCommerce.Promotions.PromotionDiscountKind"]; // 0 % · 1 Fixed · 2 FreeShipping
export type PromotionConditionGroupType =
  Schemas["eCommerce.Promotions.PromotionConditionGroupType"]; // 0 And · 1 Or

/** Trang kết quả chuẩn của `/api/v1` (`ApiPage<T>`): `{items,total,page,pageSize}`. */
export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };

export function toPage<T>(
  raw: { items?: T[] | null; total?: number; page?: number; pageSize?: number } | null,
): Page<T> {
  return {
    items: raw?.items ?? [],
    total: raw?.total ?? 0,
    page: raw?.page ?? 1,
    pageSize: raw?.pageSize ?? 0,
  };
}

// ── Permissions (`eCommerceConsts.Permissions.Promotions`, GroupName = "eCommerce") ────────────────
export const QUYEN = {
  view: "eCommerce.Promotions.View",
  manage: "eCommerce.Promotions.Manage",
  approve: "eCommerce.Promotions.Approve",
} as const;

// ── Nhãn enum ──────────────────────────────────────────────────────────────────────────────────────

const KIND_LABEL: Record<number, string> = {
  0: "Mã giảm giá",
  1: "Khuyến mãi tự động",
  2: "Flash Sale",
};

/** Nhãn loại khuyến mãi. `FlashSale` (2) vẫn hiện khi đã có dữ liệu cũ, chỉ **ẩn ở form tạo**. */
export function nhanLoai(k?: number | null): string {
  return k == null ? "—" : KIND_LABEL[k] ?? `${k}`;
}

const STATUS_LABEL: Record<number, string> = {
  0: "Nháp",
  1: "Đã duyệt",
  2: "Đang chạy",
  3: "Tạm dừng",
  4: "Hết hạn",
  5: "Đã huỷ",
};

/** Nhãn trạng thái khuyến mãi. */
export function nhanTrangThai(s?: number | null): string {
  return s == null ? "—" : STATUS_LABEL[s] ?? `${s}`;
}

const DISCOUNT_KIND_LABEL: Record<number, string> = {
  0: "Phần trăm",
  1: "Số tiền cố định",
  2: "Miễn phí vận chuyển",
};

/** Nhãn kiểu giảm giá. */
export function nhanKieuGiam(d?: number | null): string {
  return d == null ? "—" : DISCOUNT_KIND_LABEL[d] ?? `${d}`;
}

const GROUP_TYPE_LABEL: Record<number, string> = { 0: "AND", 1: "OR" };

/** Nhãn kiểu nhóm cây điều kiện (AND/OR). */
export function nhanNhom(g?: number | null): string {
  return g == null ? "—" : GROUP_TYPE_LABEL[g] ?? `${g}`;
}

// ── Rule catalog: nhãn + cờ COD + seed ──────────────────────────────────────────────────────────────

type RuleLike = {
  systemName?: string | null;
  label?: string | null;
  requiresVerifiedCod?: boolean;
  declaredCostFactor?: number;
  isSeeded?: boolean;
};

/**
 * `true` khi lá **thiếu nhãn** (`D-290`): `label` rỗng hoặc **trùng** `systemName` (BE fallback
 * `Label = SystemName`). Thiếu nhãn ⇒ hiện cảnh báo thay vì coi là "đã có nhãn".
 */
export function thieuNhanRule(rule: RuleLike | null | undefined): boolean {
  const nhan = rule?.label?.trim() ?? "";
  const ma = rule?.systemName?.trim() ?? "";
  return nhan === "" || nhan === ma;
}

/** Nhãn hiển thị cho lá: `label` nếu có, ngược lại `systemName` (kèm cờ thiếu nhãn để UI cảnh báo). */
export function nhanRule(rule: RuleLike | null | undefined): string {
  const nhan = rule?.label?.trim() ?? "";
  if (nhan !== "") return nhan;
  return rule?.systemName?.trim() ?? "—";
}

/**
 * Banner khi `IsSeeded = false` (`D-289`) — đúng câu SRS §10.1. **KHÔNG** khoá nút Lưu vì lý do này.
 * Trả `null` khi rule đã có dòng seed.
 */
export function canhBaoChiPhiSeed(rule: RuleLike | null | undefined): string | null {
  if (!rule || rule.isSeeded !== false) return null;
  const ten = nhanRule(rule);
  return `cấu hình chi phí rule «${ten}» chưa được seed — đang dùng mặc định 1/false`;
}

/** Nhãn cảnh báo cho rule cần COD **đã đối soát** (`§10.1`, `RequiresVerifiedCod`). */
export const NHAN_COD_CAN_XAC_THUC =
  "⚠️ chỉ áp dụng cho đơn COD đã đối soát (Điều kiện theo COD chưa xác thực — SHP-003/SHP-004)";

/** Hệ số chi phí khai báo của một rule theo catalog; tên ∉ catalog ⇒ **1** (khớp `TinhDeclaredCost`). */
export function hesoChiPhi(
  systemName: string | null | undefined,
  catalog: readonly RuleLike[] = [],
): number {
  const ten = systemName ?? "";
  const rule = catalog.find((r) => (r.systemName ?? "") === ten);
  const hs = rule?.declaredCostFactor ?? 0;
  return hs > 0 ? hs : 1;
}

// ── Số đếm cây (phía client — khớp `PromotionTreeValidator`) ──────────────────────────────────────────

/** Node tối thiểu để đếm cây — dùng chung cho `PromotionTreeNodeInput`/`PromotionTreeNodeDto`. */
export type CayNode = {
  id?: string | null;
  parentId?: string | null;
  isGroup?: boolean;
  ruleSystemName?: string | null;
};

export type SoDoCay = {
  nodeCount: number;
  depth: number;
  leafCount: number;
  declaredCost: number;
};

/**
 * Đếm cây phía client — phản chiếu `PromotionTreeValidator`:
 * - `nodeCount` = số node;
 * - `depth` = độ sâu lớn nhất, **gốc = 1**, con +1 (server tính lại từ chuỗi `ParentId`, không tin client);
 * - `leafCount` = số node `isGroup = false`;
 * - `declaredCost` = Σ `declaredCostFactor` của lá tra `catalog`, tên ∉ catalog ⇒ **1**.
 */
export function demCay(cay: readonly CayNode[], catalog: readonly RuleLike[] = []): SoDoCay {
  const chaCua = new Map<string, string | null>();
  for (const n of cay) {
    if (n.id) chaCua.set(n.id, n.parentId ?? null);
  }

  let depth = 0;
  for (const n of cay) {
    let d = 1;
    let cur = n.parentId ?? null;
    let buoc = 0;
    while (cur != null) {
      d++;
      if (++buoc > cay.length) break; // chu trình — `0003` lo, ở đây chỉ cần không treo
      cur = chaCua.has(cur) ? chaCua.get(cur) ?? null : null;
    }
    if (d > depth) depth = d;
  }

  let leafCount = 0;
  let declaredCost = 0;
  for (const n of cay) {
    if (!n.isGroup) {
      leafCount++;
      declaredCost += hesoChiPhi(n.ruleSystemName, catalog);
    }
  }

  return { nodeCount: cay.length, depth, leafCount, declaredCost };
}

// ── Đồng hồ giới hạn: khoá nút Lưu + câu lý do (câu `0001`/`0002` điền số từ `limits`) ────────────────

export type TranGioiHan = {
  maxNodes?: number;
  maxDepth?: number;
  maxLeaves?: number;
  maxDeclaredCombinations?: number;
};

export type MucCanhBao = "binh-thuong" | "vang" | "do";

export type KetQuaDongHo = {
  khoa: boolean;
  lyDo: string | null;
  soDo: SoDoCay;
  /** Mức cảnh báo tổ hợp (≥80% = vàng, ≥100% = đỏ) — dùng để tô màu. */
  canhBaoToHop: MucCanhBao;
};

/** Mức cảnh báo theo % trần: `≥80%` ⇒ vàng; `≥100%` ⇒ đỏ (đồng thời bị khoá). */
export function mucCanhBao(soDung: number, tran: number): MucCanhBao {
  if (!Number.isFinite(tran) || tran <= 0) return "binh-thuong";
  const pct = (soDung / tran) * 100;
  if (pct >= 100) return "do";
  if (pct >= 80) return "vang";
  return "binh-thuong";
}

/**
 * **Đồng hồ giới hạn** (`§10.1`, `D-288`): so 4 số đếm client với 4 trần `limits` **duy nhất**.
 *
 * Khoá nút Lưu khi **bất kỳ** số đếm > trần tương ứng; câu lý do = câu của `0001`/`0002` điền số từ `limits`
 * + vế **"đang có N"** (tiếng Việt), **không** hiện mã thô. Thứ tự kiểm khớp server: node → độ sâu → lá →
 * tổ hợp khai báo.
 */
export function dongHoGioiHan(
  limits: TranGioiHan,
  cay: readonly CayNode[],
  catalog: readonly RuleLike[] = [],
): KetQuaDongHo {
  const soDo = demCay(cay, catalog);
  const maxNodes = limits.maxNodes ?? 0;
  const maxDepth = limits.maxDepth ?? 0;
  const maxLeaves = limits.maxLeaves ?? 0;
  const maxDeclared = limits.maxDeclaredCombinations ?? 0;

  let lyDo: string | null = null;

  if (soDo.nodeCount > maxNodes) {
    lyDo = `Cây điều kiện vượt giới hạn cho phép (tối đa ${maxNodes} điều kiện, độ sâu ${maxDepth}). Đang có ${soDo.nodeCount} điều kiện.`;
  } else if (soDo.depth > maxDepth) {
    lyDo = `Cây điều kiện vượt giới hạn cho phép (tối đa ${maxNodes} điều kiện, độ sâu ${maxDepth}). Đang có độ sâu ${soDo.depth}.`;
  } else if (soDo.leafCount > maxLeaves) {
    lyDo = `Cây điều kiện vượt giới hạn cho phép (tối đa ${maxLeaves} lá). Đang có ${soDo.leafCount} lá.`;
  } else if (soDo.declaredCost > maxDeclared) {
    lyDo = `Cây điều kiện có quá nhiều tổ hợp cần đánh giá (vượt ${maxDeclared}). Hãy tách thành nhiều khuyến mãi nhỏ hơn. Đang có ${soDo.declaredCost} tổ hợp.`;
  }

  return {
    khoa: lyDo !== null,
    lyDo,
    soDo,
    canhBaoToHop: mucCanhBao(soDo.declaredCost, maxDeclared),
  };
}

// ── Nhãn lỗi `eCommerce:Promotion:*` (không hiện mã thô) ─────────────────────────────────────────────
//
// Ánh xạ từ `modules/Promotions/.../PromotionErrorCodes.cs` + `PRO-002` §6.4. UI hiện **câu tiếng Việt**,
// **không** dán mã `eCommerce:Promotion:00xx` ra cho người dùng.

const LOI_PROMOTION: Record<string, string> = {
  "eCommerce:Promotion:0001":
    "Cây điều kiện vượt giới hạn cho phép (quá nhiều điều kiện / quá sâu / quá nhiều lá). Hãy rút gọn cây trước khi lưu.",
  "eCommerce:Promotion:0002":
    "Cây điều kiện có quá nhiều tổ hợp cần đánh giá. Hãy tách thành nhiều khuyến mãi nhỏ hơn.",
  "eCommerce:Promotion:0003": "Cây điều kiện có vòng lặp — không thể lưu.",
  "eCommerce:Promotion:0004":
    "Điều kiện không hợp lệ (nhóm/điều kiện lẫn lộn hoặc rule chưa được đăng ký).",
  "eCommerce:Promotion:0006":
    "Không thể thực hiện: cây chỉ sửa được khi khuyến mãi ở trạng thái Nháp, và chỉ chuyển sang Tạm dừng / Đang chạy / Đã huỷ.",
  "eCommerce:Promotion:0010":
    "Rule không còn tồn tại trong phiên bản hiện tại — hãy chọn rule khác.",
  "eCommerce:Messaging:0001": "Xung đột khoá chống trùng: cùng khoá nhưng nội dung khác.",
  "eCommerce:Messaging:0002": "Yêu cầu đang được xử lý, vui lòng thử lại sau.",
  "eCommerce:Money:0001": "Không thể tính toán hai loại tiền tệ khác nhau.",
};

/** Dịch mã lỗi `eCommerce:Promotion:*` sang câu tiếng Việt; `null` nếu chưa biết mã. */
export function moTaLoiPromotion(code?: string | null): string | null {
  if (!code) return null;
  return LOI_PROMOTION[code] ?? null;
}

// ── Dựng query danh sách (đúng tên tham số OpenAPI: `Status`/`Kind`/`Page`/`PageSize`/`Sort`) ────────

export type LocDanhSach = {
  status?: number | null;
  kind?: number | null;
  page?: number;
  pageSize?: number;
  sort?: string | null;
};

/** Dựng query string cho `GET /api/v1/promotion`. */
export function taoDanhSachQueryString(loc: LocDanhSach): string {
  const q = new URLSearchParams();
  if (loc.status != null) q.set("Status", String(loc.status));
  if (loc.kind != null) q.set("Kind", String(loc.kind));
  q.set("Page", String(loc.page ?? 1));
  q.set("PageSize", String(loc.pageSize ?? 50));
  if (loc.sort) q.set("Sort", loc.sort);
  return q.toString();
}

// ── Trạng thái đích được phép của `set-status` (Paused=3 · Active=2 · Cancelled=5) ────────────────────
export const STATUS_DICH_DUOC_PHEP: PromotionStatus[] = [3, 2, 5];

/** `true` khi `status` là đích hợp lệ cho `SetStatus` (không phải `Approved`/`Expired`). */
export function laDichSetStatusHopLe(status: number | null | undefined): boolean {
  return status != null && STATUS_DICH_DUOC_PHEP.includes(status as PromotionStatus);
}

// ── Cảnh báo tổ hợp cho tooltip/màu ─────────────────────────────────────────────────────────────────
export function nhanMucCanhBao(muc: MucCanhBao): string | null {
  if (muc === "do") return "cây sắp chạm trần — nên tách";
  if (muc === "vang") return "cây sắp chạm trần — nên tách";
  return null;
}
