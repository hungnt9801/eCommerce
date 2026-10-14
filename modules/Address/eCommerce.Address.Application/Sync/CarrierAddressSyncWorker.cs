using eCommerce.Address.Carrier;
using eCommerce.Address.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace eCommerce.Address;

/// <summary>
/// **Job đồng bộ danh mục mã địa chỉ của HÃNG VẬN CHUYỂN** (`ADDR-002`).
///
/// 🚨 MẶC ĐỊNH **TẮT** (`Address:CarrierSync:Enabled = false`) — cùng lý do như job đồng bộ ĐVHC:
///   job gọi **API bên ngoài** rồi **sửa dữ liệu dùng để tạo vận đơn**. Bật mặc định nghĩa là một dịch vụ
///   ngoài có thể **làm hỏng luồng giao hàng** mà không ai chủ động quyết định.
///
/// 🚧 TRẠNG THÁI THẬT (trung thực): **CHƯA CÓ ADAPTER NÀO** cho GHN/GHTK vì **chưa có tài liệu API**.
///   Job xử lý đúng cách: nếu hãng được cấu hình mà **không có adapter** ⇒ ghi log **Warning** nêu rõ
///   *"chưa có adapter — cần tài liệu API của hãng"* và **bỏ qua hãng đó**, KHÔNG giả vờ thành công.
///   Khi có tài liệu ⇒ chỉ cần viết một lớp mỏng `IKhungDiaChiHangSource`; **toàn bộ luật đã xong và có test**.
///
/// 🔒 HÀNG RÀO (đã test ở <see cref="CarrierAddressMappingSynchronizer"/>): danh mục rỗng · quá ít đơn vị ·
///   **đánh dấu nghi ngờ vượt 20%**. Khi hàng rào chặn ⇒ **không áp dụng gì** + log **Error**.
///   Lý do: đánh dấu nghi ngờ làm ánh xạ **không dùng được** ⇒ nếu áp dụng danh mục khuyết thì
///   **mọi vận đơn mới fail-closed** ⇒ sập luồng giao hàng.
/// </summary>
public class CarrierAddressSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp đồng bộ. 1 ngày — danh mục địa chỉ của hãng thay đổi chậm.</summary>
    public const int NhipMacDinhGiay = 86_400;

    public const string CauHinhBat = "Address:CarrierSync:Enabled";
    public const string CauHinhNhip = "Address:CarrierSync:PeriodSeconds";

    /// <summary>Danh sách mã hãng cần đồng bộ, phân tách bằng dấu phẩy (ví dụ `GHN,GHTK`). Mặc định RỖNG.</summary>
    public const string CauHinhHang = "Address:CarrierSync:Carriers";

    /// <summary>Tiền tố khoá phân tán (mỗi hãng một khoá riêng).</summary>
    public const string TienToKhoa = "address:carrier-sync";

    public CarrierAddressSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // ⚠️ KHÔNG khai lại `Logger` — lớp cơ sở đã có (bài học CS0108).
        Timer.Period = NhipMacDinhGiay * 1000;

        // RunOnStart = true: đọc cấu hình NGAY (nếu để mặc định 1 ngày thì cấu hình không có tác dụng
        // ở lượt đầu — lỗi #23 đã gặp ở job đồng bộ ĐVHC). An toàn vì job MẶC ĐỊNH TẮT.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var cauHinh = workerContext.ServiceProvider.GetRequiredService<IConfiguration>();

        var nhipGiay = cauHinh.GetValue(CauHinhNhip, NhipMacDinhGiay);

        if (nhipGiay > 0 && Timer.Period != nhipGiay * 1000)
        {
            Timer.Period = nhipGiay * 1000;
        }

        if (!cauHinh.GetValue(CauHinhBat, defaultValue: false))
        {
            Logger.LogDebug("Đồng bộ mã hãng đang TẮT ({CauHinh} = false).", CauHinhBat);
            return;
        }

        var hangCanDongBo = (cauHinh[CauHinhHang] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (hangCanDongBo.Count == 0)
        {
            Logger.LogWarning(
                "Đồng bộ mã hãng ĐANG BẬT nhưng chưa cấu hình hãng nào ({CauHinh} rỗng) ⇒ không làm gì.",
                CauHinhHang);
            return;
        }

        var cacAdapter = workerContext.ServiceProvider.GetServices<IKhungDiaChiHangSource>().ToList();

        foreach (var hang in hangCanDongBo)
        {
            var adapter = cacAdapter.FirstOrDefault(a =>
                string.Equals(a.CarrierCode, hang, StringComparison.OrdinalIgnoreCase));

            if (adapter is null)
            {
                // 🚧 ĐÂY LÀ ĐIỂM TRUNG THỰC: không có adapter nghĩa là CHƯA CÓ TÀI LIỆU API của hãng.
                // Phải NÓI RÕ và BỎ QUA — tuyệt đối không giả vờ đã đồng bộ thành công.
                Logger.LogWarning(
                    "CHƯA CÓ ADAPTER cho hãng {Hang} ⇒ bỏ qua. Cần tài liệu API danh mục địa chỉ của hãng " +
                    "để viết một lớp mỏng IKhungDiaChiHangSource (toàn bộ luật đồng bộ đã xong và có test).",
                    hang);
                continue;
            }

            await DongBoMotHangAsync(workerContext, hang, adapter);
        }
    }

    private async Task DongBoMotHangAsync(
        PeriodicBackgroundWorkerContext workerContext,
        string hang,
        IKhungDiaChiHangSource adapter)
    {
        var khoa = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();
        var tenKhoa = $"{TienToKhoa}:{hang.ToLowerInvariant()}";

        await using var handle = await khoa.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            Logger.LogWarning("Không giành được khoá {TenKhoa}; bỏ qua lượt đồng bộ mã hãng {Hang}.", tenKhoa, hang);
            return;
        }

        var repo = workerContext.ServiceProvider.GetRequiredService<IRepository<CarrierAddressMapping, Guid>>();
        var dongHo = workerContext.ServiceProvider.GetRequiredService<IClock>();

        try
        {
            var danhMuc = await adapter.TaiDanhMucAsync();
            var tatCa = await repo.GetListAsync();
            var hienCo = tatCa
                .Where(x => string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 🚨 Hàng rào ở đây: ném lỗi ⇒ nhảy xuống catch ⇒ KHÔNG áp dụng gì.
            var keHoach = CarrierAddressMappingSynchronizer.LapKeHoach(hang, hienCo, danhMuc);

            if (!keHoach.CoViec)
            {
                Logger.LogInformation(
                    "Đồng bộ mã hãng {Hang}: không có thay đổi (nguồn {Nguon}, {SoDonVi} đơn vị trong danh mục).",
                    hang, danhMuc.Nguon, danhMuc.DonVis.Count);
                return;
            }

            var canCapNhat = CarrierAddressMappingSynchronizer.ApDung(keHoach, dongHo.Now);

            if (canCapNhat.Count > 0)
            {
                await repo.UpdateManyAsync(canCapNhat, autoSave: true);
            }

            Logger.LogInformation(
                "Đồng bộ mã hãng {Hang} xong (nguồn {Nguon}): ~{SoDoiTen} đổi tên, -{SoNghiNgo} đánh dấu nghi ngờ, " +
                "⚠ {SoCanAnhXa} mã MỚI cần người ánh xạ (hệ thống KHÔNG tự đoán theo tên).",
                hang, danhMuc.Nguon, keHoach.SoCapNhatTen,
                keHoach.SoDanhDauNghiNgo, keHoach.SoCanNguoiAnhXa);

            if (keHoach.SoCanNguoiAnhXa > 0)
            {
                // Warning vì đây là VIỆC CẦN NGƯỜI XỬ LÝ: mã mới chưa ánh xạ thì KHÔNG tạo được vận đơn
                // tới khu vực đó (resolver fail-closed với `eCommerce:Address:0501`).
                Logger.LogWarning(
                    "Hãng {Hang} có {SoLuong} mã địa chỉ MỚI chưa được ánh xạ ⇒ các khu vực đó CHƯA tạo được " +
                    "vận đơn. Mã cần ánh xạ: {DanhSachMa}",
                    hang, keHoach.SoCanNguoiAnhXa,
                    string.Join(", ", keHoach.Viec
                        .Where(v => v.Loai == LoaiViecMaHang.CanNguoiAnhXa)
                        .Select(v => v.CarrierAddressCode)
                        .Take(50)));
            }
        }
        catch (BusinessException ex)
        {
            // 🚨 HÀNG RÀO ĐÃ CHẶN ⇒ không áp dụng gì; phải ồn ào để người vận hành biết đồng bộ đang KHÔNG chạy.
            Logger.LogError(
                "🚨 Đồng bộ mã hãng {Hang} BỊ TỪ CHỐI (mã {Ma}). KHÔNG áp dụng thay đổi nào. Chi tiết: {DuLieu}",
                hang, ex.Code, string.Join("; ", ex.Data.Keys.Cast<object>().Select(k => $"{k}={ex.Data[k]}")));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Đồng bộ mã hãng {Hang} thất bại (lỗi không mong đợi). Không áp dụng thay đổi nào.", hang);
        }
    }
}
