/**
 * Định dạng số/ngày theo **vi-VN** (Nhanh dùng dấu `.` phân cách nghìn, `,` phân cách thập phân).
 *
 * 🚨 **VÌ SAO KHÔNG DÙNG `number.toLocaleString()` MẶC ĐỊNH:** không truyền locale thì kết quả phụ thuộc
 * locale của máy chủ (CI/container thường `en-US`) ⇒ cùng một trang render ra `1.5` ở máy này và `1,5`
 * ở máy khác. Toàn bộ trang kho **khoá** `vi-VN` + `Asia/Ho_Chi_Minh` để số/giờ không đổi theo môi trường.
 *
 * 📌 **VÌ SAO 3 SỐ LẺ:** `WP-04b` (D-31) đã đổi 16 cột số lượng sang `decimal(18,3)`; API trả `10.000`
 * cho JSON nhưng UI **phải** hiển thị `10` — nếu in thô JSON thì người dùng thấy `10.000` và hiểu là
 * *mười nghìn*. Xem `handover-n4b` §5.3.
 */

const LOCALE = "vi-VN";
export const TIME_ZONE = "Asia/Ho_Chi_Minh";

const qtyFormatter = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 3 });
const intFormatter = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 });
const amountFormatter = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 2 });

const dateTimeFormatter = new Intl.DateTimeFormat(LOCALE, {
  timeZone: TIME_ZONE,
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

const dateFormatter = new Intl.DateTimeFormat(LOCALE, {
  timeZone: TIME_ZONE,
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

/** Số lượng: tối đa **3** số lẻ (`1,5` · `0,001` · `1.000`). */
export function qty(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  // `-0` sau khi trừ (0 − 0) sẽ in ra "-0" — che đi cho sạch bảng.
  return qtyFormatter.format(Object.is(value, -0) ? 0 : value);
}

/**
 * **Tiền**: tối đa **2** số lẻ. 📌 VND thường là số nguyên nên phần lẻ hiếm khi hiện — nhưng nếu đơn giá có
 * phần lẻ (giá chia theo kg) thì **không** được âm thầm làm tròn về đồng: cột Thành tiền và dòng Tổng phải
 * khớp nhau (`D-37`).
 */
export function amount(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  return amountFormatter.format(Object.is(value, -0) ? 0 : value);
}

/** Số nguyên (số dòng, tổng bản ghi). */
export function int(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  return intFormatter.format(value);
}

/** Tiền VND: không số lẻ (đơn vị đồng). */
export function money(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  return intFormatter.format(value);
}

export function dateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : dateTimeFormatter.format(d);
}

export function dateOnly(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : dateFormatter.format(d);
}

/**
 * Ngày (YYYY-MM-DD) → **UTC** theo giờ Việt Nam, dùng cho tham số `fromUtc`/`toUtc`.
 * 📌 `cuoiNgay = true` ⇒ 23:59:59.999 để "đến ngày 03/11" bao trùm cả ngày, không lọt phiếu chiều.
 */
export function vnDateToUtcIso(ngay: string, cuoiNgay = false): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(ngay)) return null;
  // `+07:00` cố định: Việt Nam **không** có DST — dùng offset cứng để không phụ thuộc TZ của máy chạy.
  const d = new Date(`${ngay}T${cuoiNgay ? "23:59:59.999" : "00:00:00.000"}+07:00`);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

/** Hôm nay theo giờ Việt Nam, dạng `YYYY-MM-DD` (dùng cho `defaultValue` của ô ngày). */
export function todayVn(): string {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
  return parts; // en-CA ⇒ YYYY-MM-DD
}
