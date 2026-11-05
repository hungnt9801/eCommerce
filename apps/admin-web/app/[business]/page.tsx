import Link from "next/link";
import { apiGetSafe } from "@/lib/api";
import { int, money, qty, todayVn } from "@/lib/format";
import { lastDays, revenueQuery, type BaoCaoDoanhThuDto, type DiemDoanhThuDto, type TongQuanDashboardDto } from "@/lib/report";
import { StackedBarChart, type BarDiem } from "@/components/report/StackedBarChart";

/**
 * **Dashboard** (`/{business}`) — **4 KPI** của `WP-08` (`07 §WP-08`, `04 §5` màn #3).
 *
 * 🚨 **HAI LUẬT:**
 * 1. **Số đọc từ read-model** (`/api/v1/reports/dashboard`), không đếm lại từ danh sách: trang chủ mở thường
 *    xuyên, nên mỗi lần mở không được là một lần quét bảng chứng từ.
 * 2. **Không giả `0` khi chưa biết**: `ngayTongHopMoiNhat = null` ⇒ ô doanh thu hiện `—` + dòng *"chưa tổng
 *    hợp"*. `0 đồng` nghĩa là **hôm nay không bán được** — hai chuyện khác nhau và người quản lý ra quyết
 *    định khác nhau.
 */
export default async function DashboardPage({ params }: { params: Promise<{ business: string }> }) {
  const { business } = await params;

  const homNay = todayVn();
  const bayNgay = lastDays(7, homNay);

  const kpi = await apiGetSafe<TongQuanDashboardDto>("/api/v1/reports/dashboard", {
    ngay: homNay,
    doanhThuHomNay: 0,
    doanhThuBanLeHomNay: 0,
    doanhThuDonHangHomNay: 0,
    soHoaDonBanLeHomNay: 0,
    soDonHangHomNay: 0,
    tongKhaDung: 0,
    soSkuSapHet: 0,
    ngayTonKhoMoiNhat: null,
    ngayTongHopMoiNhat: null,
    dinhNghiaTien: null,
  });

  const doanhThu7Ngay = await apiGetSafe<BaoCaoDoanhThuDto>(`/api/v1/reports/revenue?${revenueQuery(bayNgay)}`, {
    tuNgay: bayNgay.tu,
    denNgay: bayNgay.den,
    tongDoanhThu: 0,
    doanhThuBanLe: 0,
    doanhThuDonHang: 0,
    tongTienVat: 0,
    tongSoChungTu: 0,
    daTongHop: false,
    ngayTongHopMoiNhat: null,
    dinhNghiaTien: null,
    diem: [],
  });

  const coSoLieuDoanhThu = kpi.ngayTongHopMoiNhat !== null && kpi.ngayTongHopMoiNhat !== undefined;
  const coAnhTonKho = kpi.ngayTonKhoMoiNhat !== null && kpi.ngayTonKhoMoiNhat !== undefined;

  const barDiem: BarDiem[] = (doanhThu7Ngay.diem ?? []).map((d: DiemDoanhThuDto) => ({
    label: d.nhan ?? "",
    a: d.doanhThuBanLe ?? 0,
    b: d.doanhThuDonHang ?? 0,
    aTen: "Bán lẻ",
    bTen: "Đơn hàng",
  }));

  const cards = [
    {
      label: "Doanh thu hôm nay",
      value: coSoLieuDoanhThu ? money(kpi.doanhThuHomNay) : "—",
      hint: coSoLieuDoanhThu ? `bán lẻ ${money(kpi.doanhThuBanLeHomNay)} · đơn ${money(kpi.doanhThuDonHangHomNay)}` : "chưa tổng hợp",
      href: `/${business}/report/revenue`,
    },
    {
      label: "Hoá đơn bán lẻ",
      value: coSoLieuDoanhThu ? int(kpi.soHoaDonBanLeHomNay) : "—",
      hint: coSoLieuDoanhThu ? "hoá đơn hôm nay" : "chưa tổng hợp",
      href: `/${business}/pos/bill/list`,
    },
    {
      label: "Đơn hàng",
      value: coSoLieuDoanhThu ? int(kpi.soDonHangHomNay) : "—",
      hint: coSoLieuDoanhThu ? "đơn hôm nay" : "chưa tổng hợp",
      href: `/${business}/order/manage`,
    },
    {
      label: "Tồn khả dụng",
      value: coAnhTonKho ? qty(kpi.tongKhaDung) : "—",
      hint: coAnhTonKho ? `${int(kpi.soSkuSapHet)} mặt hàng sắp hết` : "chưa chụp ảnh tồn",
      href: `/${business}/report/inventory`,
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-baseline justify-between">
        <div>
          <h1 className="text-[20px] font-semibold">Tổng quan</h1>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Ngày kinh doanh <b>{kpi.ngay ?? homNay}</b> (giờ Việt Nam) · mốc tổng hợp{" "}
            <b>{kpi.ngayTongHopMoiNhat ?? "chưa chạy"}</b>
          </p>
        </div>
        <Link href={`/${business}/report/revenue`} className="nh-btn">
          Báo cáo doanh thu →
        </Link>
      </div>

      <div className="grid grid-cols-4 gap-3">
        {cards.map((card) => (
          <Link key={card.label} href={card.href} className="nh-card p-4 hover:shadow-sm">
            <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
              {card.label}
            </div>
            <div className="text-[26px] font-semibold tabular mt-1">{card.value}</div>
            <div className="text-[11px] mt-1" style={{ color: "var(--text-muted)" }}>
              {card.hint}
            </div>
          </Link>
        ))}
      </div>

      <div className="nh-card p-4">
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-[15px] font-semibold">Doanh thu 7 ngày</h2>
          <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {doanhThu7Ngay.daTongHop
              ? `tổng ${money(doanhThu7Ngay.tongDoanhThu)} · ${int(doanhThu7Ngay.tongSoChungTu)} chứng từ`
              : "chưa tổng hợp"}
          </span>
        </div>
        <StackedBarChart points={barDiem} aMau="var(--primary)" bMau="#94a3b8" />
      </div>

      <div className="nh-card p-5">
        <h2 className="text-[15px] font-semibold mb-2">Đường dọc đã chạy được</h2>
        <ul className="text-[13px] space-y-1 list-disc pl-5" style={{ color: "var(--text-muted)" }}>
          <li>Đăng nhập qua OpenIddict → token chỉ nằm ở server (cookie httpOnly).</li>
          <li>Menu lọc theo quyền <b>hỏi server</b> (`/api/v1/system/permissions`), không đọc token.</li>
          <li>Hàng hoá · kho · đơn · POS · khách đọc API thật (`/api/v1/*`).</li>
          <li>
            Báo cáo đọc <b>read-model</b> (`Rpt*`) do job tổng hợp ghi mỗi 15 phút; chạy tay bằng{" "}
            <code>POST /api/v1/reports/aggregate</code>.
          </li>
          <li>
            ⚠️ Báo cáo <b>chưa</b> trừ hàng trả lại và <b>chưa</b> áp phạm vi dữ liệu (`handover-n9` §2).
          </li>
        </ul>
      </div>
    </div>
  );
}
