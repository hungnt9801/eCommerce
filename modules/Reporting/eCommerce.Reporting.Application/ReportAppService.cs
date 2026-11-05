using eCommerce.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Reporting;

/// <summary>
/// **API BÁO CÁO** (`WP-08`) — `/api/v1/reports/**`, đọc từ **read-model** (`Rpt*`).
///
/// 🚨 **BA LUẬT ĐỌC:**
/// <list type="number">
///   <item>**Báo cáo doanh thu đọc bảng tổng hợp, KHÔNG đọc chứng từ sống.** Nếu đọc chứng từ thì con số sẽ
///     đổi mỗi lần có người sửa chứng từ, và **không** đối chiếu được với khoản đã chốt — cùng lý do bảng
///     ảnh chụp tồn kho tồn tại.</item>
///   <item>**Không có dữ liệu ⇒ nói "chưa tổng hợp"**, không trả `0` im lặng (`DaTongHop`/`DaCoDuLieu`).
///     📌 `0 đồng` là một **sự thật** (không bán được hàng); *"chưa chạy job"* là **chưa biết** — gộp hai thứ
///     là cách báo cáo nói dối mà không ai phát hiện.</item>
///   <item>**Không áp phạm vi dữ liệu (`D-46`)**: báo cáo là số liệu **toàn tenant** cho người có quyền
///     `eCommerce.Reports`. 📌 Nhân viên bị giới hạn `Own` mà xem được báo cáo tổng là **lỗ hổng thật** —
///     xem `handover-n9` §2: việc này **chưa** làm, và đó là lý do quyền báo cáo tách riêng.</item>
/// </list>
/// 🔐 `GET` ⇒ `eCommerce.Reports`; `POST /aggregate` ⇒ `eCommerce.Reports.Aggregate` (**ghi** read-model).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Reports.Default)]
public class ReportAppService : ApplicationService, IReportAppService
{
    private readonly IRepository<RptAggDailySales, Guid> _tongHopNgay;
    private readonly IRepository<RptFactInventorySnapshot, Guid> _anhTonKho;
    private readonly IRepository<RptWatermark, Guid> _moc;
    private readonly IRepository<Order, Guid> _donHang;
    private readonly IRepository<OrderLine, Guid> _dongDonHang;
    private readonly TongHopBaoCaoService _tongHop;
    private readonly IOptions<ReportingOptions> _tuyChon;

    public ReportAppService(
        IRepository<RptAggDailySales, Guid> tongHopNgay,
        IRepository<RptFactInventorySnapshot, Guid> anhTonKho,
        IRepository<RptWatermark, Guid> moc,
        IRepository<Order, Guid> donHang,
        IRepository<OrderLine, Guid> dongDonHang,
        TongHopBaoCaoService tongHop,
        IOptions<ReportingOptions> tuyChon)
    {
        _tongHopNgay = tongHopNgay;
        _anhTonKho = anhTonKho;
        _moc = moc;
        _donHang = donHang;
        _dongDonHang = dongDonHang;
        _tongHop = tongHop;
        _tuyChon = tuyChon;
    }

    /// <inheritdoc />
    public async Task<BaoCaoDoanhThuDto> GetDoanhThuAsync(DoanhThuInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var gioLech = _tuyChon.Value.GioLechUtc;
        var homNay = NgayKinhDoanh.TuUtc(Clock.Now.ToUniversalTime(), gioLech);
        var khoang = KhoangTongHop.XacDinh(null, input.TuNgay, input.DenNgay, homNay);

        var ngayTongHopMoiNhat = await LayNgayTongHopMoiNhatAsync();

        if (khoang.Rong)
        {
            return new BaoCaoDoanhThuDto
            {
                TuNgay = khoang.Tu,
                DenNgay = khoang.Den,
                DaTongHop = false,
                NgayTongHopMoiNhat = ngayTongHopMoiNhat,
                DinhNghiaTien = ReportingConsts.DinhNghiaDoanhThu
            };
        }

        var truyVan = await _tongHopNgay.GetQueryableAsync();

        var dong = await AsyncExecuter.ToListAsync(truyVan.Where(x =>
            x.BusinessDate >= khoang.Tu && x.BusinessDate <= khoang.Den));

        // 📌 Lọc NGUỒN ở bộ nhớ: `input.Nguon` là `null` phổ biến, và một `||` trong truy vấn
        // LINQ-to-Entities dễ bị dịch sai (thành `WHERE Nguon = null`) mà vẫn trả 200.
        if (input.Nguon is { } nguon)
        {
            dong = dong.Where(x => x.Nguon == nguon).ToList();
        }

        var diem = TaoDiem(khoang, dong, input.Nguon is not null, input.TheoThang);

        return new BaoCaoDoanhThuDto
        {
            TuNgay = khoang.Tu,
            DenNgay = khoang.Den,
            TongDoanhThu = diem.Sum(x => x.TongDoanhThu),
            DoanhThuBanLe = diem.Sum(x => x.DoanhThuBanLe),
            DoanhThuDonHang = diem.Sum(x => x.DoanhThuDonHang),
            TongTienVat = diem.Sum(x => x.TienVat),
            TongSoChungTu = diem.Sum(x => x.SoChungTu),
            DaTongHop = dong.Count > 0,
            NgayTongHopMoiNhat = ngayTongHopMoiNhat,
            DinhNghiaTien = ReportingConsts.DinhNghiaDoanhThu,
            Diem = diem
        };
    }

    /// <inheritdoc />
    public async Task<TongQuanDonHangDto> GetTongQuanDonHangAsync(TongQuanDonInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var gioLech = _tuyChon.Value.GioLechUtc;
        var homNay = NgayKinhDoanh.TuUtc(Clock.Now.ToUniversalTime(), gioLech);
        var khoang = KhoangTongHop.XacDinh(null, input.TuNgay, input.DenNgay, homNay);

        if (khoang.Rong)
        {
            return new TongQuanDonHangDto { TuNgay = khoang.Tu, DenNgay = khoang.Den };
        }

        var batDau = NgayKinhDoanh.BatDauUtc(khoang.Tu, gioLech);
        var ketThuc = NgayKinhDoanh.KetThucUtc(khoang.Den, gioLech);

        var truyVan = await _donHang.GetQueryableAsync();
        var don = await AsyncExecuter.ToListAsync(
            truyVan.Where(x => x.CreationTime >= batDau && x.CreationTime < ketThuc));

        var dongTheoDon = new Dictionary<Guid, decimal>();

        if (don.Count > 0)
        {
            var ids = don.Select(x => x.Id).ToList();
            var dongTruyVan = await _dongDonHang.GetQueryableAsync();
            var dong = await AsyncExecuter.ToListAsync(dongTruyVan.Where(x => ids.Contains(x.OrderId)));

            dongTheoDon = dong
                .GroupBy(x => x.OrderId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(AnhXaNguonBanHang.ThanhTienDongDonHang));
        }

        // 📌 Cùng định nghĩa tiền với báo cáo doanh thu: Σ thành tiền dòng (chưa VAT) − chiết khấu chứng từ.
        decimal GiaTriDon(Order o) => (dongTheoDon.GetValueOrDefault(o.Id) - o.DiscountTotal);

        var theoTrangThai = don
            .GroupBy(x => x.FulfillmentStatus.ToString())
            .Select(g => new NhomTrangThaiDonDto
            {
                TrangThai = g.Key,
                SoDon = g.Count(),
                GiaTriDon = g.Where(x => !x.IsCancelled).Sum(GiaTriDon)
            })
            .OrderByDescending(x => x.SoDon)
            .ThenBy(x => x.TrangThai, StringComparer.Ordinal)
            .ToList();

        return new TongQuanDonHangDto
        {
            TuNgay = khoang.Tu,
            DenNgay = khoang.Den,
            TongDon = don.Count,
            SoDonHuy = don.Count(x => x.IsCancelled),
            GiaTriDon = don.Where(x => !x.IsCancelled).Sum(GiaTriDon),
            TheoTrangThai = theoTrangThai
        };
    }

    /// <inheritdoc />
    public async Task<TongQuanTonKhoDto> GetTongQuanTonKhoAsync(TongQuanTonKhoInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var (ngayChup, dong) = await LayAnhTonKhoAsync(input.Ngay);

        if (ngayChup is null)
        {
            return new TongQuanTonKhoDto { DaCoDuLieu = false };
        }

        var nguong = _tuyChon.Value.NguongSapHet;

        var theoKho = dong
            .GroupBy(x => x.WarehouseId)
            .Select(g => new DongTonKhoDto
            {
                WarehouseId = g.Key,
                SoSku = g.Select(x => x.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                OnHand = g.Sum(x => x.OnHand),
                KhaDung = g.Sum(x => x.Available),
                TamGiu = g.Sum(x => x.Reserved)
            })
            .OrderBy(x => x.WarehouseId)
            .ToList();

        var sapHet = dong
            .Where(x => x.Available <= nguong)
            .OrderBy(x => x.Available)
            .ThenBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .Take(_tuyChon.Value.SoMatHangSapHetToiDa)
            .Select(x => new MatHangSapHetDto
            {
                WarehouseId = x.WarehouseId,
                Sku = x.Sku,
                OnHand = x.OnHand,
                KhaDung = x.Available
            })
            .ToList();

        return new TongQuanTonKhoDto
        {
            NgayChup = ngayChup,
            DaCoDuLieu = true,
            SoKho = theoKho.Count,
            SoSku = dong.Select(x => x.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            TongOnHand = dong.Sum(x => x.OnHand),
            TongKhaDung = dong.Sum(x => x.Available),
            TongTamGiu = dong.Sum(x => x.Reserved),
            TheoKho = theoKho,
            SapHet = sapHet
        };
    }

    /// <inheritdoc />
    public async Task<TongQuanDashboardDto> GetDashboardAsync()
    {
        var gioLech = _tuyChon.Value.GioLechUtc;
        var homNay = NgayKinhDoanh.TuUtc(Clock.Now.ToUniversalTime(), gioLech);

        var truyVan = await _tongHopNgay.GetQueryableAsync();
        var homNayRows = await AsyncExecuter.ToListAsync(truyVan.Where(x => x.BusinessDate == homNay));

        var (ngayChup, ton) = await LayAnhTonKhoAsync(null);
        var nguong = _tuyChon.Value.NguongSapHet;

        return new TongQuanDashboardDto
        {
            Ngay = homNay,
            DoanhThuHomNay = homNayRows.Sum(x => x.Revenue),
            DoanhThuBanLeHomNay = homNayRows.Where(x => x.Nguon == NguonBaoCao.BanLe).Sum(x => x.Revenue),
            DoanhThuDonHangHomNay = homNayRows.Where(x => x.Nguon == NguonBaoCao.DonHang).Sum(x => x.Revenue),
            SoHoaDonBanLeHomNay = homNayRows.Where(x => x.Nguon == NguonBaoCao.BanLe).Sum(x => x.DocumentCount),
            SoDonHangHomNay = homNayRows.Where(x => x.Nguon == NguonBaoCao.DonHang).Sum(x => x.DocumentCount),
            TongKhaDung = ton.Sum(x => x.Available),
            SoSkuSapHet = ton.Count(x => x.Available <= nguong),
            NgayTonKhoMoiNhat = ngayChup,
            NgayTongHopMoiNhat = await LayNgayTongHopMoiNhatAsync(),
            DinhNghiaTien = ReportingConsts.DinhNghiaDoanhThu
        };
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Reports.Aggregate)]
    public Task<KetQuaTongHopDto> ChayTongHopAsync(ChayTongHopInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return _tongHop.ChayAsync(input.TuNgay, input.DenNgay, input.GhiAnhTonKho);
    }

    // ── Đọc read-model ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Dựng điểm biểu đồ: **mỗi ngày (hoặc tháng) trong khoảng đều có một điểm**, kể cả ngày 0 đồng.</summary>
    private static List<DiemDoanhThuDto> TaoDiem(
        KhoangNgay khoang,
        IReadOnlyList<RptAggDailySales> dong,
        bool chiMotNguon,
        bool theoThang)
    {
        var theoNgay = dong
            .GroupBy(x => x.BusinessDate)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    BanLe = g.Where(x => x.Nguon == NguonBaoCao.BanLe).Sum(x => x.Revenue),
                    DonHang = g.Where(x => x.Nguon == NguonBaoCao.DonHang).Sum(x => x.Revenue),
                    Vat = g.Sum(x => x.VatTotal),
                    SoChungTu = g.Sum(x => x.DocumentCount)
                });

        if (!theoThang)
        {
            return khoang.CacNgay()
                .Select(ngay =>
                {
                    var co = theoNgay.GetValueOrDefault(ngay);

                    return new DiemDoanhThuDto
                    {
                        Nhan = ngay.ToString("yyyy-MM-dd"),
                        TuNgay = ngay,
                        DenNgay = ngay,
                        DoanhThuBanLe = co?.BanLe ?? 0m,
                        DoanhThuDonHang = co?.DonHang ?? 0m,
                        TongDoanhThu = (co?.BanLe ?? 0m) + (co?.DonHang ?? 0m),
                        TienVat = co?.Vat ?? 0m,
                        SoChungTu = co?.SoChungTu ?? 0
                    };
                })
                .ToList();
        }

        // 📌 Gom theo THÁNG bằng cách cộng từng NGÀY: cộng trực tiếp theo `Month` của dòng tổng hợp sẽ bỏ sót
        // ngày 0 đồng (tháng không có giao dịch không xuất hiện ⇒ biểu đồ 12 tháng bị khuyết cột).
        var theoThangMap = new Dictionary<int, DiemDoanhThuDto>();

        foreach (var ngay in khoang.CacNgay())
        {
            var khoa = (ngay.Year * 100) + ngay.Month;
            var co = theoNgay.GetValueOrDefault(ngay);

            if (!theoThangMap.TryGetValue(khoa, out var diem))
            {
                var dauThang = new DateOnly(ngay.Year, ngay.Month, 1);
                var cuoiThang = dauThang.AddMonths(1).AddDays(-1);

                diem = new DiemDoanhThuDto
                {
                    Nhan = dauThang.ToString("yyyy-MM"),
                    TuNgay = dauThang,
                    DenNgay = cuoiThang
                };

                theoThangMap[khoa] = diem;
            }

            if (co is not null)
            {
                diem.DoanhThuBanLe += co.BanLe;
                diem.DoanhThuDonHang += co.DonHang;
                diem.TienVat += co.Vat;
                diem.SoChungTu += co.SoChungTu;
            }
        }

        var ket = theoThangMap.Values.OrderBy(x => x.Nhan, StringComparer.Ordinal).ToList();

        foreach (var diem in ket)
        {
            diem.TongDoanhThu = diem.DoanhThuBanLe + diem.DoanhThuDonHang;
        }

        // 📌 `chiMotNguon` chỉ để tài liệu hoá ý định; giá trị của nguồn bị lọc đã là 0 từ trước.
        _ = chiMotNguon;

        return ket;
    }

    /// <summary>Ảnh tồn kho **của một ngày** (hoặc ngày mới nhất có ảnh).</summary>
    private async Task<(DateOnly? Ngay, List<RptFactInventorySnapshot> Dong)> LayAnhTonKhoAsync(DateOnly? ngay)
    {
        var truyVan = await _anhTonKho.GetQueryableAsync();

        var ngayChup = ngay;

        if (ngayChup is null)
        {
            var dsNgay = await AsyncExecuter.ToListAsync(
                truyVan.Select(x => x.SnapshotDate).Distinct().OrderByDescending(x => x).Take(1));

            if (dsNgay.Count == 0)
            {
                return (null, new List<RptFactInventorySnapshot>());
            }

            ngayChup = dsNgay[0];
        }

        var ngayLoc = ngayChup!.Value;
        var dong = await AsyncExecuter.ToListAsync(truyVan.Where(x => x.SnapshotDate == ngayLoc));

        return (ngayLoc, dong);
    }

    private async Task<DateOnly?> LayNgayTongHopMoiNhatAsync()
    {
        var moc = await _moc.GetListAsync();

        DateOnly? ket = null;

        foreach (var m in moc.Where(x => x.Nguon != NguonBaoCao.TonKho))
        {
            if (m.LastAggregatedDate is { } ngay && (ket is null || ngay > ket.Value))
            {
                ket = ngay;
            }
        }

        return ket;
    }
}
