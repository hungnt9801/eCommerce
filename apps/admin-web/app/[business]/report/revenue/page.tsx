import Link from "next/link";
import { ApiError, apiGet, apiGetSafe } from "@/lib/api";
import { amount, int, money, todayVn } from "@/lib/format";
import {
  csvFileName,
  lastDays,
  NGUON_LABEL,
  rangeQuery,
  revenueQuery,
  type BaoCaoDoanhThuDto,
  type DiemDoanhThuDto,
  type NguonBaoCao,
  type TongQuanDonHangDto,
  NGUON_VALUE,
} from "@/lib/report";
import { ExportCsvButton } from "@/components/report/ExportCsvButton";
import { StackedBarChart, type BarDiem } from "@/components/report/StackedBarChart";

/** Cột của bảng VÀ của CSV là **cùng một biến** — CSV không thể lệch bảng. */
const COT = ["Kỳ", "Bán lẻ", "Đơn hàng", "Tổng doanh thu", "VAT", "Số chứng từ"] as const;

/**
 * **Báo cáo doanh thu** (`/{business}/report/revenue`) — `WP-08` (`04 §5`, `07 §WP-08`).
 *
 * 📌 Đọc `GET /api/v1/reports/revenue` — **read-model** (`RptAggDailySales`), không đọc chứng từ sống:
 * con số của kỳ đã chốt **không đổi** khi ai đó sửa chứng từ hôm nay.
 * 🚨 `daTongHop = false` ⇒ hiện *"chưa tổng hợp"* (kèm mốc mới nhất + nút chạy), **KHÔNG** hiện `0 đồng`:
 * *"0"* là một sự thật (không bán được), *"chưa tổng hợp"* là **chưa biết**.
 */
export default async function RevenueReportPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const mot = (key: string): string => {
    const giaTri = sp[key];
    return typeof giaTri === "string" ? giaTri : "";
  };

  const homNay = todayVn();
  const macDinh = lastDays(30, homNay);

  const tu = mot("tu") || macDinh.tu;
  const den = mot("den") || macDinh.den;
  const theoThang = mot("theoThang") === "true";
  const nguonKhoa = mot("nguon");
  const nguon: NguonBaoCao | undefined =
    nguonKhoa === "pos" ? NGUON_VALUE.pos : nguonKhoa === "order" ? NGUON_VALUE.order : undefined;

  const ky = { tu, den, theoThang, nguon };

  let bc: BaoCaoDoanhThuDto | null = null;
  let loi: string | null = null;

  try {
    bc = await apiGet<BaoCaoDoanhThuDto>(`/api/v1/reports/revenue?${revenueQuery(ky)}`);
  } catch (e) {
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API";
  }

  const don = await apiGetSafe<TongQuanDonHangDto>(`/api/v1/reports/orders/summary?${rangeQuery(ky)}`, {
    tuNgay: tu,
    denNgay: den,
    tongDon: 0,
    soDonHuy: 0,
    giaTriDon: 0,
    theoTrangThai: [],
  });

  const diem: DiemDoanhThuDto[] = bc?.diem ?? [];
  const daTongHop = bc?.daTongHop === true;

  // 📌 API trả `theoTrangThai` có thể `null` (DTO `List<>` khởi tạo nhưng serializer không bảo đảm) ⇒ chuẩn hoá
  // MỘT chỗ thay vì rải `?? []` khắp JSX.
  const theoTrangThaiCuaDon = () => don.theoTrangThai ?? [];

  const barDiem: BarDiem[] = diem.map((d) => ({
    label: d.nhan ?? "",
    a: d.doanhThuBanLe ?? 0,
    b: d.doanhThuDonHang ?? 0,
    aTen: "Bán lẻ",
    bTen: "Đơn hàng",
  }));

  // CSV lấy đúng dòng đang hiện (cùng mảng `diem`), số **không** format để máy tính đọc được.
  const csvRows = diem.map((d) => [
    d.nhan ?? "",
    String(d.doanhThuBanLe ?? 0),
    String(d.doanhThuDonHang ?? 0),
    String(d.tongDoanhThu ?? 0),
    String(d.tienVat ?? 0),
    String(d.soChungTu ?? 0),
  ]);

  return (
    <div className="space-y-4">
      <div className="flex items-baseline justify-between">
        <div>
          <h1 className="text-[20px] font-semibold">Doanh thu</h1>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Từ số liệu <b>đã tổng hợp</b>. Hoá đơn đã huỷ và đơn đã huỷ <b>không</b> được tính.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <ExportCsvButton fileName={csvFileName("doanh-thu", ky)} headers={[...COT]} rows={csvRows} />
          <Link href={`/${business}/report/inventory`} className="nh-btn">
            Báo cáo tồn kho →
          </Link>
        </div>
      </div>

      <form className="nh-card p-3 flex flex-wrap items-end gap-3" action={`/${business}/report/revenue`}>
        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Từ ngày
          </span>
          <input className="nh-input mt-0.5" type="date" name="tu" defaultValue={tu} />
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Đến ngày
          </span>
          <input className="nh-input mt-0.5" type="date" name="den" defaultValue={den} />
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Nguồn
          </span>
          <select className="nh-input mt-0.5" name="nguon" defaultValue={nguonKhoa}>
            <option value="">Cả hai</option>
            <option value="pos">Bán lẻ</option>
            <option value="order">Đơn hàng</option>
          </select>
        </label>

        <label className="flex items-center gap-1 text-[12px] pb-1">
          <input type="checkbox" name="theoThang" value="true" defaultChecked={theoThang} />
          Gom theo tháng
        </label>

        <button type="submit" className="nh-btn nh-btn-primary">
          Xem
        </button>
        <Link href={`/${business}/report/revenue`} className="nh-btn">
          Mặc định 30 ngày
        </Link>

        <span className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          Mốc đã tổng hợp: <b>{bc?.ngayTongHopMoiNhat ?? "—"}</b>
        </span>
      </form>

      {loi && (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Không tải được báo cáo (mã {loi}). Kiểm tra API host ở cổng 54275 và quyền{" "}
          <code>eCommerce.Reports</code>.
        </div>
      )}

      {!loi && !daTongHop && (
        <div className="nh-card p-4 text-[13px]">
          <b>Chưa tổng hợp cho kỳ này</b> — chưa có lượt tổng hợp nào ghi số liệu (mốc đã tổng hợp:{" "}
          <b>{bc?.ngayTongHopMoiNhat ?? "chưa chạy lần nào"}</b>).
          <div className="text-[12px] mt-1" style={{ color: "var(--text-muted)" }}>
            Đây <b>không phải</b> &quot;0 đồng&quot;: job tổng hợp chạy mỗi 15 phút, hoặc người có quyền{" "}
            <code>eCommerce.Reports.Aggregate</code> gọi <code>POST /api/v1/reports/aggregate</code>.
          </div>
        </div>
      )}

      <div className="grid grid-cols-4 gap-3">
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Tổng doanh thu
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">{daTongHop ? money(bc?.tongDoanhThu) : "—"}</div>
        </div>
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Bán lẻ (POS)
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">
            {daTongHop ? money(bc?.doanhThuBanLe) : "—"}
          </div>
        </div>
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Đơn hàng
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">
            {daTongHop ? money(bc?.doanhThuDonHang) : "—"}
          </div>
        </div>
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Số chứng từ
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">{daTongHop ? int(bc?.tongSoChungTu) : "—"}</div>
        </div>
      </div>

      <div className="nh-card p-4">
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-[15px] font-semibold">Biểu đồ doanh thu</h2>
          <div className="flex items-center gap-3 text-[12px]" style={{ color: "var(--text-muted)" }}>
            <span className="inline-flex items-center gap-1">
              <span style={{ width: 10, height: 10, background: "var(--primary)", display: "inline-block" }} /> Bán lẻ
            </span>
            <span className="inline-flex items-center gap-1">
              <span style={{ width: 10, height: 10, background: "#94a3b8", display: "inline-block" }} /> Đơn hàng
            </span>
          </div>
        </div>
        <StackedBarChart points={barDiem} aMau="var(--primary)" bMau="#94a3b8" />
      </div>

      <div className="nh-card overflow-hidden">
        <table className="nh-table w-full">
          <thead>
            <tr>
              {COT.map((c) => (
                <th key={c}>{c}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {diem.length === 0 && (
              <tr>
                <td colSpan={COT.length} className="text-center py-6" style={{ color: "var(--text-muted)" }}>
                  {daTongHop ? "Kỳ này không có chứng từ nào." : "Chưa tổng hợp — chưa có số liệu để hiện."}
                </td>
              </tr>
            )}
            {diem.map((d) => (
              <tr key={d.nhan}>
                <td>{d.nhan}</td>
                <td className="tabular text-right">{amount(d.doanhThuBanLe)}</td>
                <td className="tabular text-right">{amount(d.doanhThuDonHang)}</td>
                <td className="tabular text-right font-medium">{amount(d.tongDoanhThu)}</td>
                <td className="tabular text-right">{amount(d.tienVat)}</td>
                <td className="tabular text-right">{int(d.soChungTu)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="nh-card p-4 space-y-1 text-[12px]" style={{ color: "var(--text-muted)" }}>
        <div className="font-medium" style={{ color: "var(--text)" }}>
          Định nghĩa tiền
        </div>
        <div>{bc?.dinhNghiaTien ?? "Doanh thu = Σ thành tiền dòng (chưa VAT) − chiết khấu chứng từ."}</div>
        <div>
          📌 Ngày kinh doanh tính theo <b>giờ Việt Nam (UTC+7)</b>: chứng từ phát sinh sau <b>17:00 UTC</b> thuộc
          ngày hôm sau. Vì vậy số của một ngày có thể khác báo cáo xuất theo giờ UTC.
        </div>
      </div>

      <div className="nh-card p-4">
        <div className="flex items-baseline justify-between mb-2">
          <h2 className="text-[15px] font-semibold">Đơn hàng trong kỳ</h2>
          <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {int(don.tongDon)} đơn · {int(don.soDonHuy)} huỷ · giá trị {money(don.giaTriDon)}
          </span>
        </div>

        {theoTrangThaiCuaDon().length === 0 ? (
          <div className="text-[13px]" style={{ color: "var(--text-muted)" }}>
            Không có đơn hàng nào trong kỳ.
          </div>
        ) : (
          <table className="nh-table w-full">
            <thead>
              <tr>
                <th>Trạng thái thực hiện</th>
                <th className="text-right">Số đơn</th>
                <th className="text-right">Giá trị</th>
              </tr>
            </thead>
            <tbody>
              {theoTrangThaiCuaDon().map((t) => (
                <tr key={t.trangThai}>
                  <td>{t.trangThai}</td>
                  <td className="tabular text-right">{int(t.soDon)}</td>
                  <td className="tabular text-right">{amount(t.giaTriDon)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="text-[12px] flex gap-3" style={{ color: "var(--text-muted)" }}>
        <span>
          Quyền đang dùng: <code>eCommerce.Reports</code> ({NGUON_LABEL.BanLe} · {NGUON_LABEL.DonHang})
        </span>
      </div>
    </div>
  );
}
