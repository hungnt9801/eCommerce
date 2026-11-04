/**
 * **HỢP ĐỒNG DỮ LIỆU của màn BÁO CÁO** (`WP-08`) — type lấy **từ `schema.d.ts` sinh tự động**, không chép tay.
 *
 * 🚨 **VÌ SAO KHÔNG TỰ KHAI LẠI TYPE:** `schema.d.ts` là bản dịch của `openapi.v1.json`; chép tay DTO vào app
 * thì lần sau BE đổi tên trường, `tsc` **vẫn xanh** và UI đọc `undefined` — đúng lỗi im lặng mà `WP-09`
 * (openapi-drift) sinh ra để chặn (cùng lý do `lib/stock.ts`).
 *
 * 📌 **HAI LUẬT CỦA MÀN BÁO CÁO (đọc kỹ trước khi sửa):**
 * 1. **UI KHÔNG tự cộng/tính lại** doanh thu hay tồn khả dụng: mọi con số đến từ `Rpt*` (server đã tính bằng
 *    **một** công thức — `ReportingConsts.DinhNghiaDoanhThu`). UI cộng lại là nguồn lệch thứ hai.
 * 2. **`daTongHop = false` / `daCoDuLieu = false` KHÔNG phải `0`**: đó là *"chưa tổng hợp"*. Hiển thị `0` trong
 *    ca này là báo cáo **nói dối** — người dùng sẽ tưởng hôm nay không bán được gì.
 */
import type { components } from "@ecommerce/api-client/schema";

type Schemas = components["schemas"];

export type BaoCaoDoanhThuDto = Schemas["eCommerce.Reporting.BaoCaoDoanhThuDto"];
export type DiemDoanhThuDto = Schemas["eCommerce.Reporting.DiemDoanhThuDto"];
export type TongQuanDonHangDto = Schemas["eCommerce.Reporting.TongQuanDonHangDto"];
export type NhomTrangThaiDonDto = Schemas["eCommerce.Reporting.NhomTrangThaiDonDto"];
export type TongQuanTonKhoDto = Schemas["eCommerce.Reporting.TongQuanTonKhoDto"];
export type DongTonKhoDto = Schemas["eCommerce.Reporting.DongTonKhoDto"];
export type MatHangSapHetDto = Schemas["eCommerce.Reporting.MatHangSapHetDto"];
export type TongQuanDashboardDto = Schemas["eCommerce.Reporting.TongQuanDashboardDto"];
export type KetQuaTongHopDto = Schemas["eCommerce.Reporting.KetQuaTongHopDto"];
export type NguonBaoCao = Schemas["eCommerce.Reporting.NguonBaoCao"];

/** Nhãn nguồn — **số** là hợp đồng dữ liệu (`D-17`/`D-33`), nhãn chỉ để đọc. */
export const NGUON_LABEL: Record<string, string> = {
  BanLe: "Bán lẻ",
  DonHang: "Đơn hàng",
  TonKho: "Tồn kho",
};

export const NGUON_VALUE: Record<"all" | "pos" | "order", NguonBaoCao | undefined> = {
  all: undefined,
  pos: 0,
  order: 1,
};

export type KyBaoCao = {
  tu?: string;
  den?: string;
  theoThang?: boolean;
  nguon?: NguonBaoCao;
};

/** Chuỗi truy vấn cho `/api/v1/reports/revenue` — **chỉ gửi tham số có giá trị**. */
export function revenueQuery(ky: KyBaoCao): string {
  const q = new URLSearchParams();
  if (ky.tu) q.set("tuNgay", ky.tu);
  if (ky.den) q.set("denNgay", ky.den);
  if (ky.theoThang) q.set("theoThang", "true");
  // ⚠️ `nguon = 0` (bán lẻ) là giá trị **hợp lệ** ⇒ kiểm `!== undefined`, KHÔNG kiểm truthy.
  if (ky.nguon !== undefined) q.set("nguon", String(ky.nguon));
  return q.toString();
}

export function rangeQuery(ky: { tu?: string; den?: string }): string {
  const q = new URLSearchParams();
  if (ky.tu) q.set("tuNgay", ky.tu);
  if (ky.den) q.set("denNgay", ky.den);
  return q.toString();
}

/** Lấy `n` ngày gần nhất (dạng `YYYY-MM-DD`, giờ VN) — cho bộ lọc mặc định của Dashboard/báo cáo. */
export function lastDays(n: number, todayIso: string): { tu: string; den: string } {
  const den = new Date(`${todayIso}T00:00:00.000Z`);
  const tu = new Date(den.getTime() - (n - 1) * 24 * 60 * 60 * 1000);

  const iso = (d: Date): string => d.toISOString().slice(0, 10);

  return { tu: iso(tu), den: iso(den) };
}

/**
 * **CSV phía client** (`D-36`): có **BOM UTF-8** + phân cách `;` để Excel đọc đúng tiếng Việt.
 * 📌 Không dấu BOM thì Excel mở file UTF-8 thành `B?o c?o` — người dùng tưởng dữ liệu hỏng.
 */
export function buildCsv(headers: string[], rows: string[][]): string {
  const escape = (v: string): string => {
    const s = v ?? "";
    return /[";\n\r]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s;
  };

  const lines = [headers.map(escape).join(";"), ...rows.map((r) => r.map(escape).join(";"))];

  return `\uFEFF${lines.join("\r\n")}\r\n`;
}

/** Tên tệp CSV có ngày — để hai lần xuất không ghi đè nhau. */
export function csvFileName(prefix: string, ky: { tu?: string; den?: string }): string {
  return `${prefix}-${ky.tu ?? "dau"}-${ky.den ?? "nay"}.csv`;
}
