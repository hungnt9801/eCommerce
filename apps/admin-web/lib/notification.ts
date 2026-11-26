/**
 * **HỢP ĐỒNG DỮ LIỆU + HÀM THUẦN của màn thông báo** — `NOT-001` / `WP-22` (hàng `W22-3`).
 *
 * 🚨 **Type lấy TRỰC TIẾP từ `schema.d.ts` sinh tự động** (`D-33`), **không** chép tay DTO. BE đổi tên
 * trường ⇒ `tsc` đỏ ngay thay vì UI đọc `undefined` (đúng loại lỗi im lặng mà `kiem-openapi-drift` chặn).
 *
 * 📌 **Enum trả SỐ** (0/1/2…) — số là **hợp đồng dữ liệu**; nhãn tiếng Việt ở đây chỉ để hiển thị.
 *
 * Toàn bộ hàm dưới đây là **hàm thuần** (không DOM, không I/O) ⇒ phủ được bằng `pnpm test` (Vitest node).
 */
import type { components } from "@ecommerce/api-client/schema";

type Schemas = components["schemas"];

// ── Type DTO ─────────────────────────────────────────────────────────────────────────────────────────
export type NotificationTemplateDto = Schemas["eCommerce.Notification.NotificationTemplateDto"];
export type NotificationTemplateVersionDto =
  Schemas["eCommerce.Notification.NotificationTemplateVersionDto"];
export type NotificationQueueItemDto = Schemas["eCommerce.Notification.NotificationQueueItemDto"];
export type NotificationQueueEventDto = Schemas["eCommerce.Notification.NotificationQueueEventDto"];
export type NotificationBatchDto = Schemas["eCommerce.Notification.NotificationBatchDto"];
export type NotificationPlaceholderDto =
  Schemas["eCommerce.Notification.NotificationPlaceholderDto"];

export type CreateTemplateDto = Schemas["eCommerce.Notification.CreateTemplateDto"];
export type PublishTemplateVersionDto = Schemas["eCommerce.Notification.PublishTemplateVersionDto"];
export type PreviewTemplateDto = Schemas["eCommerce.Notification.PreviewTemplateDto"];
export type PreviewResultDto = Schemas["eCommerce.Notification.PreviewResultDto"];
export type CreateBatchDto = Schemas["eCommerce.Notification.CreateBatchDto"];

/** `GET /api/v1/system/me` — dùng để biết `userId` hiện tại (chặn người tạo tự duyệt lô ở UI). */
export type MeResult = Schemas["eCommerce.HttpApi.Controllers.SystemController+MeResult"];

// ── Enum (số) ──────────────────────────────────────────────────────────────────────────────────────
export type ChannelKind = Schemas["eCommerce.Notification.ChannelKind"];
export type NotificationPurpose = Schemas["eCommerce.Notification.NotificationPurpose"];
export type NotificationTemplateStatus = Schemas["eCommerce.Notification.NotificationTemplateStatus"];
export type NotificationBodyFormat = Schemas["eCommerce.Notification.NotificationBodyFormat"];
export type NotificationQueueStatus = Schemas["eCommerce.Notification.NotificationQueueStatus"];
export type NotificationQueueEventType = Schemas["eCommerce.Notification.NotificationQueueEventType"];
export type NotificationBatchStatus = Schemas["eCommerce.Notification.NotificationBatchStatus"];
export type NotificationPlaceholderKind =
  Schemas["eCommerce.Notification.NotificationPlaceholderKind"];

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

// ── Permissions (`eCommerceConsts.Permissions.Notifications`, GroupName = "eCommerce") ───────────────
export const QUYEN = {
  templatesView: "eCommerce.Notification.Templates.View",
  templatesManage: "eCommerce.Notification.Templates.Manage",
  templatesPublish: "eCommerce.Notification.Templates.Publish",
  queueView: "eCommerce.Notification.Queue.View",
  batchesCreate: "eCommerce.Notification.Batches.Create",
  batchesApprove: "eCommerce.Notification.Batches.Approve",
} as const;

// ── Nhãn enum ──────────────────────────────────────────────────────────────────────────────────────

const CHANNEL_LABEL: Record<number, string> = { 0: "Email", 1: "SMS", 2: "Zalo OA" };

/** Nhãn kênh gửi. */
export function nhanKenh(k?: number | null): string {
  return k == null ? "—" : CHANNEL_LABEL[k] ?? `${k}`;
}

const PURPOSE_LABEL: Record<number, string> = {
  0: "Giao dịch",
  1: "Marketing",
  2: "Cảnh báo vận hành",
};

/** Nhãn mục đích template. */
export function nhanMucDich(p?: number | null): string {
  return p == null ? "—" : PURPOSE_LABEL[p] ?? `${p}`;
}

const TEMPLATE_STATUS_LABEL: Record<number, string> = {
  0: "Nháp",
  1: "Đã publish",
  2: "Lưu trữ",
};

/** Nhãn trạng thái template. 🚨 `Archived` vẫn phải hiển thị (tin cũ còn ghim version) — không ẩn. */
export function nhanTrangThaiTemplate(s?: number | null): string {
  return s == null ? "—" : TEMPLATE_STATUS_LABEL[s] ?? `${s}`;
}

const BODY_FORMAT_LABEL: Record<number, string> = { 0: "Văn bản (Text)", 1: "HTML" };

/** Nhãn định dạng thân tin. GĐ3 chỉ `Text` (`D-199`). */
export function nhanBodyFormat(f?: number | null): string {
  return f == null ? "—" : BODY_FORMAT_LABEL[f] ?? `${f}`;
}

const QUEUE_STATUS_LABEL: Record<number, string> = {
  0: "Mới",
  1: "Chờ duyệt",
  2: "Chờ gửi",
  3: "Đang bàn giao",
  4: "Đã bàn giao",
  5: "Đã huỷ",
  6: "Hết hạn",
  7: "Lỗi",
};

/** Nhãn trạng thái mục hàng đợi. */
export function nhanTrangThaiHangDoi(s?: number | null): string {
  return s == null ? "—" : QUEUE_STATUS_LABEL[s] ?? `${s}`;
}

const QUEUE_EVENT_LABEL: Record<number, string> = {
  0: "Xếp hàng",
  1: "Sẵn sàng gửi",
  2: "Đang gửi",
  3: "Đã bàn giao",
  4: "Hết hạn thuê",
  5: "Thất bại",
  6: "Hết hạn",
  7: "Đã huỷ",
  8: "Bị khử trùng",
  9: "Gửi lại",
  10: "Lô được duyệt",
  11: "Chờ duyệt",
  12: "Hẹn gửi lại",
};

/** Nhãn loại sự kiện trong sổ append-only. */
export function nhanLoaiSuKien(e?: number | null): string {
  return e == null ? "—" : QUEUE_EVENT_LABEL[e] ?? `${e}`;
}

const BATCH_STATUS_LABEL: Record<number, string> = {
  0: "Chờ duyệt",
  1: "Đã duyệt",
  2: "Đã huỷ",
  3: "Đã bàn giao",
};

/** Nhãn trạng thái lô gửi. */
export function nhanTrangThaiLo(s?: number | null): string {
  return s == null ? "—" : BATCH_STATUS_LABEL[s] ?? `${s}`;
}

const PLACEHOLDER_KIND_LABEL: Record<number, string> = {
  0: "Văn bản",
  1: "Tiền",
  2: "Ngày giờ",
  3: "Số",
  4: "Liên kết",
};

/** Nhãn kiểu placeholder. */
export function nhanKieuPlaceholder(k?: number | null): string {
  return k == null ? "—" : PLACEHOLDER_KIND_LABEL[k] ?? `${k}`;
}

// ── Nhãn lỗi `eCommerce:Notification:00xx` ──────────────────────────────────────────────────────────
//
// 🚨 `0012` KHÔNG phải "chưa có dữ liệu" mà là **LỖI CẤU HÌNH** (template chưa publish) ⇒ câu tiếng Việt
// phải hướng dẫn publish (SRS §10 ghi chú). Ánh xạ từ `modules/Notification/…/NotificationErrorCodes.cs`.

const LOI_THONG_BAO: Record<string, string> = {
  "eCommerce:Notification:0010":
    "Khoá chống trùng đã dùng cho nội dung khác — thao tác bị chặn để tránh tạo bản ghi trùng.",
  "eCommerce:Notification:0011": "Chuyển trạng thái không hợp lệ (không được lùi trạng thái).",
  "eCommerce:Notification:0012":
    "Template chưa được publish cho (mã · kênh · ngôn ngữ) này — đây là LỖI CẤU HÌNH, không phải “chưa có dữ liệu”. " +
    "Vào màn Mẫu thông báo để publish một version trước khi gửi.",
  "eCommerce:Notification:0013":
    "Thiếu trường bắt buộc của template khi render thử — điền dữ liệu mẫu cho mọi placeholder bắt buộc.",
  "eCommerce:Notification:0014": "Mục này đã ở trạng thái kết thúc (đã bàn giao/huỷ/hết hạn).",
  "eCommerce:Notification:0015":
    "Nội dung dùng placeholder nằm ngoài danh mục cho phép — hãy chèn placeholder từ danh sách, KHÔNG gõ tay {{...}}.",
  "eCommerce:Notification:0016":
    "Version đã publish là BẤT BIẾN — muốn đổi nội dung phải tạo version mới.",
  "eCommerce:Notification:0017": "Không tìm thấy template / mục hàng đợi / lô gửi.",
  "eCommerce:Notification:0018": "Mục thuộc lô chưa được duyệt — phải duyệt lô trước khi gửi.",
  "eCommerce:Notification:0019": "Bị khử trùng trong cửa sổ thời gian (chỉ áp cho Marketing) — không gửi lặp.",
  "eCommerce:Notification:0020":
    "Giai đoạn này chỉ hỗ trợ định dạng Văn bản (Text); HTML chưa được bật (D-199).",
};

/** Dịch mã lỗi `eCommerce:Notification:00xx` sang câu tiếng Việt; `null` nếu chưa biết mã. */
export function moTaLoiThongBao(code?: string | null): string | null {
  if (!code) return null;
  return LOI_THONG_BAO[code] ?? null;
}

/** `true` khi mã lỗi là **lỗi cấu hình** (không phải "rỗng") — hiện hướng dẫn thay vì "chưa có dữ liệu". */
export function laLoiCauHinh(code?: string | null): boolean {
  return code === "eCommerce:Notification:0012" || code === "eCommerce:Notification:0020";
}

// ── Placeholder (`{{Ten}}`, đối chiếu ordinal) ──────────────────────────────────────────────────────
//
// 🚨 **NGUỒN danh mục placeholder:** API **không** có endpoint đọc whitelist của version đang dùng
// (chỉ `PublishVersion` NHẬN whitelist và `Preview` render version đã lưu). Vì vậy danh mục dưới đây là
// **danh sách cố định**, sao từ seed hệ thống (`NotificationDataSeedContributor.MauCua`) + mở rộng tối
// thiểu; ghi `docDeviation`. Khi BE thêm endpoint đọc whitelist, thay hằng này bằng dữ liệu server.

/** Kiểu placeholder: 0 Text · 1 Money · 2 DateTime · 3 Number · 4 Url (`NotificationPlaceholderKind`). */
export const PLACEHOLDER_WHITELIST: readonly NotificationPlaceholderDto[] = [
  { name: "MaDon", kind: 0, required: true },
  { name: "TongTien", kind: 1, required: true },
  { name: "TenKhach", kind: 0, required: true },
  { name: "NgayDuKien", kind: 2, required: true },
  { name: "TieuDe", kind: 0, required: true },
  { name: "MucDo", kind: 0, required: true },
  { name: "ThoiDiem", kind: 2, required: true },
] as const;

/** Tên các placeholder trong danh mục cố định (để đối chiếu nhanh). */
export const PLACEHOLDER_NAMES: readonly string[] = PLACEHOLDER_WHITELIST.map((p) => p.name ?? "");

/**
 * Rút **tên** placeholder `{{Ten}}` trong chuỗi (trim, giữ thứ tự, khử trùng lặp) — phản chiếu
 * `NotificationRenderer.RutTenPlaceholder` bên BE. Không có `}}` ⇒ bỏ qua (coi là chuỗi thường).
 */
export function rutTenPlaceholder(text?: string | null): string[] {
  if (!text) return [];
  const out: string[] = [];
  let i = 0;
  while (i < text.length) {
    const mo = text.indexOf("{{", i);
    if (mo < 0) break;
    const dong = text.indexOf("}}", mo + 2);
    if (dong < 0) break;
    const ten = text.slice(mo + 2, dong).trim();
    if (ten && !out.includes(ten)) out.push(ten);
    i = dong + 2;
  }
  return out;
}

/** Tên placeholder xuất hiện trong chuỗi nhưng **không** thuộc whitelist ⇒ sẽ gây `0015`. */
export function placeholderNgoaiWhitelist(
  text: string | null | undefined,
  whitelist: Iterable<string> = PLACEHOLDER_NAMES,
): string[] {
  const choPhep = new Set(whitelist);
  return rutTenPlaceholder(text).filter((ten) => !choPhep.has(ten));
}

/** `true` khi có dấu `{{` chưa đóng bằng `}}` (người dùng đang gõ tay dở) — cảnh báo chặn publish. */
export function coNgoacNhonKhongDong(text?: string | null): boolean {
  if (!text) return false;
  let i = 0;
  while (i < text.length) {
    const mo = text.indexOf("{{", i);
    if (mo < 0) return false;
    const dong = text.indexOf("}}", mo + 2);
    if (dong < 0) return true;
    i = dong + 2;
  }
  return false;
}

/**
 * Kết quả kiểm nội dung soạn thảo: placeholder ngoài whitelist + dấu `{{` chưa đóng. Dùng để **chặn gửi**
 * ở client (chống `0015`) — server vẫn là cửa chặn thật.
 */
export function kiemTraNoiDung(
  tieuDe: string | null | undefined,
  noiDung: string | null | undefined,
  whitelist: Iterable<string> = PLACEHOLDER_NAMES,
): { ngoaiWhitelist: string[]; ngoacNhonMo: boolean } {
  const gop = `${tieuDe ?? ""}\n${noiDung ?? ""}`;
  const ngoai = Array.from(new Set(placeholderNgoaiWhitelist(gop, whitelist)));
  return { ngoaiWhitelist: ngoai, ngoacNhonMo: coNgoacNhonKhongDong(gop) };
}

/**
 * Whitelist gửi kèm `PublishVersion` = **đúng** các placeholder được dùng trong tiêu đề/nội dung, lấy
 * `kind`/`required` từ danh mục cố định. Dùng đúng tập này ⇒ body không bao giờ tham chiếu placeholder
 * ngoài whitelist (chống `0015`).
 */
export function placeholdersSuDung(
  tieuDe: string | null | undefined,
  noiDung: string | null | undefined,
  whitelist: readonly NotificationPlaceholderDto[] = PLACEHOLDER_WHITELIST,
): NotificationPlaceholderDto[] {
  const dung = new Set([...rutTenPlaceholder(tieuDe), ...rutTenPlaceholder(noiDung)]);
  return whitelist
    .filter((p) => p.name && dung.has(p.name))
    .map((p) => ({ name: p.name, kind: p.kind, required: p.required }));
}

// ── Hàm nghiệp vụ thuần ─────────────────────────────────────────────────────────────────────────────

/**
 * `D-268`: màn **lô gửi** chỉ được chọn template `Purpose = Marketing` — **lọc phía client** từ
 * `GetTemplatesAsync` (BE chưa có `GetTemplatesForBatch`). `purpose == null` ⇒ trả nguyên danh sách.
 */
export function locTemplateTheoMucDich<T extends { purpose?: number | null }>(
  items: readonly T[],
  purpose: NotificationPurpose | null | undefined,
): T[] {
  if (purpose == null) return [...items];
  return items.filter((t) => t.purpose === purpose);
}

/**
 * Có bản ghi **chưa tải** (phân trang) ⇒ cảnh báo "đang hiển thị X/Y — bộ lọc/đếm chỉ tính phần đã tải".
 * Không được im lặng: "tổng sau lọc" mà thiếu trang là loại sai số khó phát hiện (`design-doc-guidelines §5` 9).
 */
export function canhBaoThieuTrang(totalCount: number, soDaTai: number): boolean {
  return totalCount > soDaTai;
}

/**
 * Người **tạo lô KHÔNG được tự duyệt** lô (`D-198`; BE ném `0011`). `false` khi trùng người tạo.
 * Nếu chưa biết user hiện tại ⇒ trả `true` (UI không tự khoá; server vẫn là cửa chặn thật).
 */
export function duocDuyetLo(
  batch: { createdByUserId?: string | null } | null | undefined,
  currentUserId: string | null | undefined,
): boolean {
  if (!batch) return false;
  if (!currentUserId) return true;
  return batch.createdByUserId !== currentUserId;
}

/** Template **chưa có version Published** ⇒ lỗi cấu hình (nguyên nhân thật của `0012`), không phải "rỗng". */
export function templateChuaPublish(t: {
  currentPublishedVersionId?: string | null;
}): boolean {
  return !t.currentPublishedVersionId;
}

// ── Định dạng ngày giờ Việt Nam (dd/MM/yyyy HH:mm) ──────────────────────────────────────────────────

const NGAY_GIO_FORMAT = new Intl.DateTimeFormat("vi-VN", {
  timeZone: "Asia/Ho_Chi_Minh",
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

/** ISO (UTC) → `dd/MM/yyyy HH:mm` theo giờ Việt Nam; `—` nếu rỗng/không hợp lệ. */
export function dinhDangNgayGio(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  const parts = NGAY_GIO_FORMAT.formatToParts(d);
  const lay = (loai: Intl.DateTimeFormatPartTypes): string =>
    parts.find((p) => p.type === loai)?.value ?? "";
  return `${lay("day")}/${lay("month")}/${lay("year")} ${lay("hour")}:${lay("minute")}`;
}

// ── Hàng đợi: dựng query + kiểm số lượng lô ───────────────────────────────────────────────────────────

/** Bộ lọc trang hàng đợi (đã đổi ngày VN → UTC trước khi truyền). */
export type QueueLoc = {
  status?: number | null;
  channelKind?: number | null;
  tuNgayUtc?: string | null;
  denNgayUtc?: string | null;
  page?: number;
  pageSize?: number;
};

/**
 * Dựng query string cho `GET /api/v1/notifications/queue`. Tên tham số **đúng** hợp đồng OpenAPI
 * (`Status` · `ChannelKind` · `TuNgayUtc` · `DenNgayUtc` · `Page` · `PageSize`).
 */
export function taoQueueQueryString(loc: QueueLoc): string {
  const q = new URLSearchParams();
  if (loc.status != null) q.set("Status", String(loc.status));
  if (loc.channelKind != null) q.set("ChannelKind", String(loc.channelKind));
  if (loc.tuNgayUtc) q.set("TuNgayUtc", loc.tuNgayUtc);
  if (loc.denNgayUtc) q.set("DenNgayUtc", loc.denNgayUtc);
  q.set("Page", String(loc.page ?? 1));
  q.set("PageSize", String(loc.pageSize ?? 50));
  return q.toString();
}

/** Số người nhận của một lô: **1..50000** (client chặn trước; BE vẫn là cửa chặn thật). */
export const TOTAL_ITEMS_MIN = 1;
export const TOTAL_ITEMS_MAX = 50000;

export function hopLeTotalItems(n: number): boolean {
  return Number.isInteger(n) && n >= TOTAL_ITEMS_MIN && n <= TOTAL_ITEMS_MAX;
}
