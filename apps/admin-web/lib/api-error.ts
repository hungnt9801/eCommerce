/**
 * Đọc lỗi của API `/api/v1` thành cặp `(mã, thông điệp)` **hiển thị được cho người dùng**.
 *
 * 📌 **HÌNH DẠNG THẬT (đo được):** Host trả lỗi nghiệp vụ dưới dạng ABP:
 * ```json
 * { "error": { "code": "eCommerce:Inventory:0010", "message": "…", "details": "…" } }
 * ```
 * 📌 **Business exception trả HTTP 403** (không phải 400/409 — hành vi có sẵn của Host, xem `handover-n4` §5.4)
 * ⇒ UI **phải** đọc `error.code` mới biết vì sao, **không** chỉ hiện "403".
 * 📌 Chỉ phụ thuộc `fetch`/`Response` ⇒ dùng được ở **client component** (không kéo `next-auth` vào bundle).
 */

type AbpError = {
  error?: {
    code?: string | null;
    message?: string | null;
    details?: string | null;
    validationErrors?: Array<{ message?: string | null; members?: string[] | null }> | null;
  } | null;
  code?: string | null;
  message?: string | null;
  details?: string | null;
  /** `ValidationProblemDetails` của ASP.NET (khi `[ApiController]` tự trả 400). */
  errors?: Record<string, string[]> | null;
  title?: string | null;
};

export type ApiFailure = {
  status: number;
  /** Mã ổn định để tra cứu/tìm kiếm (`eCommerce:Inventory:0010`), `null` nếu body không phải JSON lỗi. */
  code: string | null;
  message: string;
};

function laChuoi(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0;
}

/** Một dòng thông điệp: ưu tiên `details` (nói rõ lý do) rồi tới `message`. */
function gopThongDiep(err: AbpError): string | null {
  const loi = err.error;

  if (loi?.validationErrors && loi.validationErrors.length > 0) {
    const ds = loi.validationErrors.map((v) => (laChuoi(v.message) ? v.message : "")).filter(Boolean);
    if (ds.length > 0) return ds.join(" · ");
  }

  if (err.errors) {
    const ds = Object.values(err.errors)
      .flat()
      .filter((m): m is string => laChuoi(m));
    if (ds.length > 0) return ds.join(" · ");
  }

  if (laChuoi(loi?.message)) return loi.message;
  if (laChuoi(err.message)) return err.message;
  if (laChuoi(loi?.details)) return loi.details;
  if (laChuoi(err.details)) return err.details;
  if (laChuoi(err.title)) return err.title;

  return null;
}

export function docLoiApi(body: unknown, status: number): ApiFailure {
  const err = (body ?? {}) as AbpError;

  const code = laChuoi(err.error?.code) ? err.error.code : laChuoi(err.code) ? err.code : null;

  const message = gopThongDiep(err) ?? `API trả HTTP ${status} (không có thông điệp).`;

  return { status, code, message };
}
