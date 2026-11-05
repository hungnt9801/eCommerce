using eCommerce.Inventory;
using eCommerce.Orders;
using eCommerce.Pos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace eCommerce.Reporting;

/// <summary>
/// **JOB TỔNG HỢP READ-MODEL** (`WP-08`) — đọc chứng từ nguồn, ghi `RptFactSalesLines` +
/// `RptAggDailySales` + `RptFactInventorySnapshots` + các chiều, theo **mốc** (watermark) hoặc theo
/// **khoảng** (backfill).
///
/// 🚨 **BỐN BẤT BIẾN (mỗi cái đều là một lỗi đã biết của read-model):**
/// <list type="number">
///   <item>**THAY THẾ theo `(Ngày, Nguồn)`, không cộng thêm** — mỗi ngày được `DELETE` rồi `INSERT` lại
///     **trong cùng transaction**. Đây là định nghĩa của *"job chạy lại 2 lần không nhân đôi dữ liệu"*
///     (DoD `WP-08`). Cộng thêm là cách mọi read-model chết: chạy lại đúng một lượt, doanh thu gấp đôi,
///     **không lỗi nào**.</item>
///   <item>**Loại chứng từ KHÔNG phải doanh thu**: hoá đơn `Voided`, đơn `IsCancelled`. 📌 Huỷ **không xoá**
///     chứng từ (audit phải còn) ⇒ bộ lọc nằm **ở đây**, không dựa vào việc chứng từ biến mất.</item>
///   <item>**Mốc chỉ tiến** (`RptWatermark`): backfill khoảng cũ không hạ mốc, nếu không lượt định kỳ sau
///     sẽ làm lại một khoảng đã xong mỗi 15 phút.</item>
///   <item>**MỘT lượt chạy tại một thời điểm** (khoá phân tán). Hai lượt chồng nhau có thể xoá dòng vừa ghi
///     của nhau ⇒ báo cáo **thiếu** dòng mà không có lỗi.</item>
/// </list>
///
/// 📌 **Không đọc qua app service của module khác** — read-model đọc **repository** (không đi qua
/// `[Authorize]`/phân trang), đúng tiền lệ `WP-07`. Worker gọi **service này** (không gọi `*AppService` —
/// worker không có principal ⇒ `AbpAuthorizationException`, xem `BackgroundWorkerCoCheTests`).
/// </summary>
public class TongHopBaoCaoService : DomainService
{
    /// <summary>
    /// Tên khoá phân tán. ⚠️ **KHÔNG** thêm tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix`
    /// đã thêm (bài học C11/C13: tiền tố bị nhân đôi).
    /// </summary>
    public const string TenKhoaPhanTan = "report:tong-hop";

    private static readonly NguonBaoCao[] CacNguonDoanhThu = [NguonBaoCao.BanLe, NguonBaoCao.DonHang];

    private readonly IRepository<RetailBill, Guid> _hoaDonBanLe;
    private readonly IRepository<RetailBillLine, Guid> _dongHoaDonBanLe;
    private readonly IRepository<Order, Guid> _donHang;
    private readonly IRepository<OrderLine, Guid> _dongDonHang;
    private readonly IRepository<InventoryItem, Guid> _tonKho;
    private readonly IRepository<Warehouse, Guid> _kho;
    private readonly IRepository<RptFactSalesLine, Guid> _dongSuKien;
    private readonly IRepository<RptAggDailySales, Guid> _tongHopNgay;
    private readonly IRepository<RptFactInventorySnapshot, Guid> _anhTonKho;
    private readonly IRepository<RptDimDate, Guid> _dimNgay;
    private readonly IRepository<RptDimProduct, Guid> _dimSanPham;
    private readonly IRepository<RptDimStore, Guid> _dimKho;
    private readonly IRepository<RptDimStaff, Guid> _dimNhanVien;
    private readonly IRepository<RptWatermark, Guid> _moc;
    private readonly IAbpDistributedLock _khoaPhanTan;
    private readonly IOptions<ReportingOptions> _tuyChon;
    private readonly IClock _dongHo;
    private readonly IGuidGenerator _boSinhId;

    public TongHopBaoCaoService(
        IRepository<RetailBill, Guid> hoaDonBanLe,
        IRepository<RetailBillLine, Guid> dongHoaDonBanLe,
        IRepository<Order, Guid> donHang,
        IRepository<OrderLine, Guid> dongDonHang,
        IRepository<InventoryItem, Guid> tonKho,
        IRepository<Warehouse, Guid> kho,
        IRepository<RptFactSalesLine, Guid> dongSuKien,
        IRepository<RptAggDailySales, Guid> tongHopNgay,
        IRepository<RptFactInventorySnapshot, Guid> anhTonKho,
        IRepository<RptDimDate, Guid> dimNgay,
        IRepository<RptDimProduct, Guid> dimSanPham,
        IRepository<RptDimStore, Guid> dimKho,
        IRepository<RptDimStaff, Guid> dimNhanVien,
        IRepository<RptWatermark, Guid> moc,
        IAbpDistributedLock khoaPhanTan,
        IOptions<ReportingOptions> tuyChon,
        IClock dongHo,
        IGuidGenerator boSinhId)
    {
        _hoaDonBanLe = hoaDonBanLe;
        _dongHoaDonBanLe = dongHoaDonBanLe;
        _donHang = donHang;
        _dongDonHang = dongDonHang;
        _tonKho = tonKho;
        _kho = kho;
        _dongSuKien = dongSuKien;
        _tongHopNgay = tongHopNgay;
        _anhTonKho = anhTonKho;
        _dimNgay = dimNgay;
        _dimSanPham = dimSanPham;
        _dimKho = dimKho;
        _dimNhanVien = dimNhanVien;
        _moc = moc;
        _khoaPhanTan = khoaPhanTan;
        _tuyChon = tuyChon;
        _dongHo = dongHo;
        _boSinhId = boSinhId;
    }

    /// <summary>Chạy một lượt tổng hợp. `tu`/`den` = `null` ⇒ chạy tiếp từ mốc tới hôm nay.</summary>
    public async Task<KetQuaTongHopDto> ChayAsync(DateOnly? tu, DateOnly? den, bool ghiAnhTonKho)
    {
        // 🔒 MỘT lượt tại một thời điểm. Chờ 2 giây rồi bỏ: job 15 phút/lần chạy lại được ngay, còn API thì
        // trả `409` cho client (biết *"đang chạy"* thay vì tưởng mất dữ liệu).
        await using var giuKhoa = await _khoaPhanTan.TryAcquireAsync(TenKhoaPhanTan, TimeSpan.FromSeconds(2));

        if (giuKhoa is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", TenKhoaPhanTan)
                .WithData("lyDo", "Một lượt tổng hợp KHÁC đang chạy — chờ rồi thử lại.");
        }

        var gioLech = _tuyChon.Value.GioLechUtc;
        var luc = _dongHo.Now.ToUniversalTime();
        var homNay = NgayKinhDoanh.TuUtc(luc, gioLech);

        var moc = await NapMocAsync();

        // 📌 Không dùng `Min()` trên `DateOnly?` (nullable không có overload LINQ) — tự gộp để luật đọc được.
        DateOnly? mocThapNhat = null;

        foreach (var nguon in CacNguonDoanhThu)
        {
            var m = moc[nguon].LastAggregatedDate;

            if (m is not null && (mocThapNhat is null || m.Value < mocThapNhat.Value))
            {
                mocThapNhat = m;
            }
        }

        var khoang = KhoangTongHop.XacDinh(mocThapNhat, tu, den, homNay);

        var soDongSuKien = 0;
        var soDongTongHop = 0;

        // Chiều: gom trong lúc chạy rồi ghi MỘT lần (không upsert từng dòng).
        var sanPham = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var khoCuaDong = new HashSet<Guid>();
        var nhanVienCuaDong = new HashSet<Guid>();

        foreach (var ngay in khoang.CacNgay())
        {
            var ketQua = await TinhNgayAsync(ngay, gioLech);

            await _dongSuKien.DeleteAsync(x => x.BusinessDate == ngay);
            await _tongHopNgay.DeleteAsync(x => x.BusinessDate == ngay);

            if (ketQua.Dong.Count > 0)
            {
                await _dongSuKien.InsertManyAsync(ketQua.Dong.Select(x => RptFactSalesLine.TuSuKien(_boSinhId.Create(), x)));
            }

            if (ketQua.TongHop.Count > 0)
            {
                await _tongHopNgay.InsertManyAsync(
                    ketQua.TongHop.Select(x => RptAggDailySales.TuTongHop(_boSinhId.Create(), x, luc)));
            }

            soDongSuKien += ketQua.Dong.Count;
            soDongTongHop += ketQua.TongHop.Count;

            foreach (var dong in ketQua.Dong)
            {
                if (!sanPham.ContainsKey(dong.Sku))
                {
                    sanPham[dong.Sku] = dong.TenSanPham;
                }
                else if (sanPham[dong.Sku] is null && dong.TenSanPham is not null)
                {
                    sanPham[dong.Sku] = dong.TenSanPham;
                }

                if (dong.KhoId is { } k)
                {
                    khoCuaDong.Add(k);
                }

                if (dong.NhanVienId is { } nv)
                {
                    nhanVienCuaDong.Add(nv);
                }
            }

            await DamBaoDimNgayAsync(ngay);
        }

        await CapNhatChieuAsync(sanPham, khoCuaDong, nhanVienCuaDong);

        // 🚨 Ảnh tồn kho **KHÔNG** phụ thuộc khoảng doanh thu: ngày không phát sinh giao dịch vẫn phải có ảnh
        // (nếu không, báo cáo tồn kho "nhảy" về ngày cũ mà không ai biết vì sao).
        var soAnhTonKho = 0;

        if (ghiAnhTonKho && (moc[NguonBaoCao.TonKho].LastAggregatedDate is null
                             || moc[NguonBaoCao.TonKho].LastAggregatedDate < homNay))
        {
            soAnhTonKho = await ChupAnhTonKhoAsync(homNay, luc);

            moc[NguonBaoCao.TonKho].GhiNhan(homNay, luc, soAnhTonKho);
            await _moc.UpdateAsync(moc[NguonBaoCao.TonKho]);
        }

        if (!khoang.Rong)
        {
            foreach (var nguon in CacNguonDoanhThu)
            {
                // 📌 `LastRowCount` là số dòng **của lượt này** (cả hai nguồn cùng một lượt) — nó không phải
                // số dòng của riêng nguồn, nên ghi **tổng** cho cả hai để không có con số nửa thật nửa giả.
                moc[nguon].GhiNhan(khoang.Den, luc, soDongSuKien);
                await _moc.UpdateAsync(moc[nguon]);
            }
        }

        return new KetQuaTongHopDto
        {
            TuNgay = khoang.Tu,
            DenNgay = khoang.Den,
            SoNgay = khoang.SoNgay,
            Rong = khoang.Rong,
            SoDongSuKien = soDongSuKien,
            SoDongTongHop = soDongTongHop,
            SoAnhTonKho = soAnhTonKho,
            MocBanLe = moc[NguonBaoCao.BanLe].LastAggregatedDate,
            MocDonHang = moc[NguonBaoCao.DonHang].LastAggregatedDate,
            MocTonKho = moc[NguonBaoCao.TonKho].LastAggregatedDate
        };
    }

    /// <summary>Tính **một ngày** từ chứng từ nguồn (đọc repo, không qua app service).</summary>
    private async Task<KetQuaTinhNgay> TinhNgayAsync(DateOnly ngay, int gioLech)
    {
        var batDau = NgayKinhDoanh.BatDauUtc(ngay, gioLech);
        var ketThuc = NgayKinhDoanh.KetThucUtc(ngay, gioLech);

        // 🚨 Lọc Ở SERVER theo NỬA KHOẢNG `[batDau, ketThuc)` — xem `NgayKinhDoanh.KetThucUtc`: `BETWEEN`
        // làm mất chứng từ phát sinh ở 23:59:59.5.
        var hoaDon = await _hoaDonBanLe.GetListAsync(
            x => x.Status == RetailBillStatus.Completed && x.CreationTime >= batDau && x.CreationTime < ketThuc);

        var donHang = await _donHang.GetListAsync(
            x => !x.IsCancelled && x.CreationTime >= batDau && x.CreationTime < ketThuc);

        var chungTu = new List<ChungTuBanHangNguon>();
        var dong = new List<DongBanHangNguon>();

        if (hoaDon.Count > 0)
        {
            var ids = hoaDon.Select(x => x.Id).ToList();
            var dongHoaDon = await _dongHoaDonBanLe.GetListAsync(x => ids.Contains(x.RetailBillId));

            var khongCoDong = hoaDon.Count(x => dongHoaDon.All(d => d.RetailBillId != x.Id));

            if (khongCoDong > 0)
            {
                // 🚨 KHÔNG ném: một chứng từ rỗng (dữ liệu nhập hỏng) **không được** làm chết cả ngày ⇒ job
                // không tiến mốc ⇒ **mọi báo cáo đứng im** và chỉ có log mới thấy. Bỏ qua + la lên.
                Logger.LogWarning(
                    "Tổng hợp {Ngay}: {SoChungTu} hoá đơn bán lẻ KHÔNG có dòng nào — bỏ qua (không đóng góp doanh thu).",
                    ngay, khongCoDong);
            }

            foreach (var hd in hoaDon)
            {
                var dongCuaHoaDon = dongHoaDon.Where(x => x.RetailBillId == hd.Id).ToList();

                if (dongCuaHoaDon.Count == 0)
                {
                    continue;
                }

                var (ct, ds) = AnhXaNguonBanHang.TuHoaDonBanLe(hd, dongCuaHoaDon, gioLech);

                chungTu.Add(ct);
                dong.AddRange(ds);
            }
        }

        if (donHang.Count > 0)
        {
            var ids = donHang.Select(x => x.Id).ToList();
            var dongDon = await _dongDonHang.GetListAsync(x => ids.Contains(x.OrderId));

            var khongCoDong = donHang.Count(x => dongDon.All(d => d.OrderId != x.Id));

            if (khongCoDong > 0)
            {
                Logger.LogWarning(
                    "Tổng hợp {Ngay}: {SoDon} đơn hàng KHÔNG có dòng nào — bỏ qua (không đóng góp doanh thu).",
                    ngay, khongCoDong);
            }

            foreach (var dh in donHang)
            {
                var dongCuaDon = dongDon.Where(x => x.OrderId == dh.Id).ToList();

                if (dongCuaDon.Count == 0)
                {
                    continue;
                }

                var (ct, ds) = AnhXaNguonBanHang.TuDonHang(dh, dongCuaDon, gioLech);

                chungTu.Add(ct);
                dong.AddRange(ds);
            }
        }

        return TongHopDoanhThu.TinhNgay(chungTu, dong);
    }

    /// <summary>Chụp ảnh tồn kho **mới nhất** và gán vào `ngay` (thay thế ảnh cũ của ngày đó).</summary>
    private async Task<int> ChupAnhTonKhoAsync(DateOnly ngay, DateTime luc)
    {
        var ton = await _tonKho.GetListAsync();

        await _anhTonKho.DeleteAsync(x => x.SnapshotDate == ngay);

        var anh = ton
            .Select(t => RptFactInventorySnapshot.TuTonKho(
                _boSinhId.Create(),
                ngay,
                t.WarehouseId,
                t.Sku,
                t.OnHand,
                t.Reserved,
                t.Transferring,
                t.Shipping,
                t.Holding,
                t.Damaged,
                t.Warranty,
                t.WarrantyHolding,
                t.PreOrder,
                t.WaitingInbound,
                luc))
            .ToList();

        if (anh.Count > 0)
        {
            await _anhTonKho.InsertManyAsync(anh);
        }

        return anh.Count;
    }

    private async Task<Dictionary<NguonBaoCao, RptWatermark>> NapMocAsync()
    {
        var tatCa = await _moc.GetListAsync();
        var ket = new Dictionary<NguonBaoCao, RptWatermark>();

        foreach (var nguon in Enum.GetValues<NguonBaoCao>())
        {
            var moc = tatCa.FirstOrDefault(x => x.Nguon == nguon);

            if (moc is null)
            {
                moc = new RptWatermark(_boSinhId.Create(), nguon);
                await _moc.InsertAsync(moc);
            }

            ket[nguon] = moc;
        }

        return ket;
    }

    private async Task DamBaoDimNgayAsync(DateOnly ngay)
    {
        var coNgay = await _dimNgay.FirstOrDefaultAsync(x => x.BusinessDate == ngay);

        if (coNgay is null)
        {
            await _dimNgay.InsertAsync(new RptDimDate(_boSinhId.Create(), ngay));
        }
    }

    private async Task CapNhatChieuAsync(
        IReadOnlyDictionary<string, string?> sanPham,
        IReadOnlyCollection<Guid> khoCuaDong,
        IReadOnlyCollection<Guid> nhanVienCuaDong)
    {
        if (sanPham.Count > 0)
        {
            var skus = sanPham.Keys.ToList();
            var coSan = (await _dimSanPham.GetListAsync(x => skus.Contains(x.Sku)))
                .ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

            foreach (var (sku, ten) in sanPham)
            {
                if (coSan.TryGetValue(sku, out var dim))
                {
                    dim.CapNhatTen(ten);
                    await _dimSanPham.UpdateAsync(dim);
                }
                else
                {
                    await _dimSanPham.InsertAsync(new RptDimProduct(_boSinhId.Create(), sku, ten));
                }
            }
        }

        if (khoCuaDong.Count > 0)
        {
            var tatCaKho = await _kho.GetListAsync();
            var coSan = (await _dimKho.GetListAsync()).ToDictionary(x => x.WarehouseId);

            foreach (var khoId in khoCuaDong)
            {
                var nguon = tatCaKho.FirstOrDefault(x => x.Id == khoId);

                if (coSan.TryGetValue(khoId, out var dim))
                {
                    dim.CapNhat(nguon?.Code, nguon?.Name);
                    await _dimKho.UpdateAsync(dim);
                }
                else
                {
                    await _dimKho.InsertAsync(new RptDimStore(_boSinhId.Create(), khoId, nguon?.Code, nguon?.Name));
                }
            }
        }

        if (nhanVienCuaDong.Count > 0)
        {
            var coSan = (await _dimNhanVien.GetListAsync()).Select(x => x.StaffId).ToHashSet();

            foreach (var nv in nhanVienCuaDong.Where(x => !coSan.Contains(x)))
            {
                // 📌 Tên nhân viên nằm ở module `Customers` (`SetStaffs`) và `WP-08` **không** tham chiếu nó —
                // ghi `null` rồi để màn nhân viên (`WP-07`) bổ sung sau, thay vì bịa tên từ GUID.
                await _dimNhanVien.InsertAsync(new RptDimStaff(_boSinhId.Create(), nv));
            }
        }
    }
}

