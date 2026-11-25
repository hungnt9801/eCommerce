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
using Volo.Abp.Uow;

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
///   mã rỗng/`null`/trùng sau `Trim` (`D-84`/`D-91`, `0509`) · **đánh dấu nghi ngờ vượt 20 %** (`D-87`).
///   Khi hàng rào chặn ⇒ **không áp dụng gì** + ghi dòng lô `BiChan` + log **Error**.
///
/// 📌 **MỖI LƯỢT ĐÃ GIÀNH ĐƯỢC KHOÁ ⇒ ĐÚNG 1 DÒNG `AddrCarrierSyncRuns`** (`D-90`): gồm cả lượt lỗi adapter
///   trước `LapKeHoach` (`Outcome = Loi`, `ErrorType` = tên kiểu exception — **cấm** lưu thông điệp).
///   Bận khoá / chưa có adapter ⇒ **0** dòng (`D-236`).
///
/// 🔑 **MỘT Unit of Work** bao `UpdateManyAsync` + dòng lô + các dòng con, `CompleteAsync` **một** lần: ghi
///   dòng lô ném ⇒ **rollback** ⇒ **0** ánh xạ đổi (BR-014, TC-07). Dòng `BiChan`/`Loi` ghi trong UoW
///   **riêng** (`requiresNew`).
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

    /// <summary>Giá trị `TriggeredBy` của đường job.</summary>
    public const string TriggeredByJob = "job:CarrierAddressSyncWorker";

    /// <summary>Mã hàng rào của `LapKeHoach` ⇒ dòng lô `BiChan` (mọi `BusinessException` mã khác ⇒ `Loi`).</summary>
    private static readonly HashSet<string> MaHangRao = new(StringComparer.Ordinal)
    {
        CarrierAddressErrorCodes.DanhMucHangRong,          // 0506
        CarrierAddressErrorCodes.DanhMucHangQuaIt,         // 0507
        CarrierAddressErrorCodes.BienMatHangLoat,          // 0508
        CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe   // 0509 (D-84/D-91)
    };

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
        // 📌 **LOG VÔ ĐIỀU KIỆN Ở ĐẦU MỖI LƯỢT** — 🚨 **BẮT BUỘC cho mọi job** (`inv-001` §27.3 + §43.4):
        // nếu chỉ log khi *"có việc"* thì *"im lặng"* **KHÔNG** phân biệt được *"job không chạy"* với
        // *"job chạy mà không có việc"* ⇒ **mọi phép kiểm chứng job mất giá trị**. Dòng này được thêm vì
        // **cơ chế** `BackgroundWorkerCoCheTests` (sau khi siết luật) phát hiện worker này **chưa** có nó.
        Logger.LogInformation("JOB CarrierAddressSyncWorker: bắt đầu một lượt (mốc {Moc}).", DateTime.UtcNow);

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
        var sp = workerContext.ServiceProvider;

        var khoa = sp.GetRequiredService<IAbpDistributedLock>();
        var tenKhoa = $"{TienToKhoa}:{hang.ToLowerInvariant()}";

        await using var handle = await khoa.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            // `D-90`: **0** dòng lô — không giành được quyền ghi thì chưa có "lượt".
            Logger.LogWarning("Không giành được khoá {TenKhoa}; bỏ qua lượt đồng bộ mã hãng {Hang}.", tenKhoa, hang);
            return;
        }

        var repo = sp.GetRequiredService<IRepository<CarrierAddressMapping, Guid>>();
        var loRepo = sp.GetRequiredService<IRepository<CarrierAddressSyncRun, Guid>>();
        var dongConRepo = sp.GetRequiredService<IRepository<CarrierAddressSyncRunUnmappedCode, Guid>>();
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var dongHo = sp.GetRequiredService<IClock>();

        // 🔒 `startedAtUtc` lấy TRƯỚC adapter (`D-90`): adapter treo/lỗi vẫn có mốc bắt đầu thật.
        var startedAtUtc = dongHo.Now;

        DanhMucDiaChiHang? danhMuc = null;
        string? sourceHash = null;

        try
        {
            // ── Nạp danh mục hãng NGOÀI transaction (D12) ──
            danhMuc = await adapter.TaiDanhMucAsync();

            // ── Quét `D-84`/`D-91` TRƯỚC KHI BĂM (P1 vòng DOCREV 1): mã `null`/rỗng/trùng ⇒ NÉM `0509`
            //    ⇒ dòng lô `BiChan`, thay vì `NullReferenceException` ⇒ `Loi` (băm trước sẽ NRE).
            var daChuanHoa = CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(danhMuc);

            sourceHash = CarrierAddressSourceHash.Tinh(hang, daChuanHoa);

            // ── UoW DUY NHẤT: đọc + áp dụng + dòng lô + dòng con, `CompleteAsync` MỘT lần (BR-014) ──
            using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

            var tatCa = await repo.GetListAsync();
            var hienCo = tatCa
                .Where(x => string.Equals(x.CarrierCode, hang, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 🚨 Hàng rào ở đây: ném ⇒ `using` bỏ UoW (rollback) ⇒ KHÔNG áp dụng gì.
            var keHoach = CarrierAddressMappingSynchronizer.LapKeHoach(
                hang, hienCo, danhMuc with { DonVis = daChuanHoa });

            // KHÔNG còn early-return khi 0 việc: lượt 0 việc VẪN ghi dòng `DaApDung` (`D-83`).
            var canCapNhat = CarrierAddressMappingSynchronizer.ApDung(keHoach, dongHo.Now);

            if (canCapNhat.Count > 0)
            {
                // `autoSave: false` — commit duy nhất ở `uow.CompleteAsync()` bên dưới.
                await repo.UpdateManyAsync(canCapNhat, autoSave: false);
            }

            var viecCanAnhXa = keHoach.Viec
                .Where(v => v.Loai == LoaiViecMaHang.CanNguoiAnhXa)
                .ToList();

            var lo = CarrierAddressSyncRun.Ghi(
                hang,
                danhMuc.Nguon,
                sourceHash,
                startedAtUtc,
                dongHo.Now,
                TriggeredByJob,
                CarrierAddressSyncOutcome.DaApDung,
                errorCode: null,
                errorType: null,
                soMaNhan: danhMuc.DonVis?.Count ?? 0,
                soCapNhatTen: keHoach.SoCapNhatTen,
                soDanhDauNghiNgo: keHoach.SoDanhDauNghiNgo,
                soCanNguoiAnhXa: keHoach.SoCanNguoiAnhXa,
                mauCanNguoiAnhXa: viecCanAnhXa.Select(v => v.CarrierAddressCode),
                tenantId: null);

            await loRepo.InsertAsync(lo, autoSave: false);

            if (viecCanAnhXa.Count > 0)
            {
                // 📌 Số dòng con **bằng** `SoCanNguoiAnhXa` (NFR-003, `D-88`) — đủ cấp/mã/tên.
                var dongCon = viecCanAnhXa
                    .Select(v => CarrierAddressSyncRunUnmappedCode.Ghi(
                        lo.Id,
                        hang,
                        v.CarrierLevel,
                        v.CarrierAddressCode,
                        v.TenMoi,
                        tenantId: null))
                    .ToList();

                await dongConRepo.InsertManyAsync(dongCon, autoSave: false);
            }

            await uow.CompleteAsync();

            Logger.LogInformation(
                "Đồng bộ mã hãng {Hang} xong (nguồn {Nguon}, hash {Hash}): ~{SoDoiTen} đổi tên, " +
                "-{SoNghiNgo} đánh dấu nghi ngờ, ⚠ {SoCanAnhXa} mã MỚI cần người ánh xạ (hệ thống KHÔNG tự đoán theo tên).",
                hang, danhMuc.Nguon, sourceHash, keHoach.SoCapNhatTen,
                keHoach.SoDanhDauNghiNgo, keHoach.SoCanNguoiAnhXa);

            if (keHoach.SoCanNguoiAnhXa > 0)
            {
                // Warning vì đây là VIỆC CẦN NGƯỜI XỬ LÝ: mã mới chưa ánh xạ thì KHÔNG tạo được vận đơn
                // tới khu vực đó (resolver fail-closed với `eCommerce:Address:0501`).
                Logger.LogWarning(
                    "Hãng {Hang} có {SoLuong} mã địa chỉ MỚI chưa được ánh xạ ⇒ các khu vực đó CHƯA tạo được " +
                    "vận đơn. Mã cần ánh xạ: {DanhSachMa}",
                    hang, keHoach.SoCanNguoiAnhXa,
                    string.Join(", ", viecCanAnhXa.Select(v => v.CarrierAddressCode).Take(50)));
            }
        }
        catch (BusinessException ex) when (ex.Code is { } maHangRao && MaHangRao.Contains(maHangRao))
        {
            // 🚨 HÀNG RÀO ĐÃ CHẶN (`D-90`): chỉ BusinessException **mang mã hàng rào** ⇒ `BiChan`;
            //    mọi BusinessException khác (do adapter ném) ⇒ nhánh dưới ⇒ `Loi`.
            await GhiLoThatBaiAsync(
                uowManager, loRepo, dongHo, hang, CarrierAddressSyncOutcome.BiChan,
                ex.Code, errorType: null, danhMuc?.Nguon, sourceHash,
                soMaNhan: danhMuc?.DonVis?.Count ?? 0, startedAtUtc);

            Logger.LogError(
                "🚨 Đồng bộ mã hãng {Hang} BỊ TỪ CHỐI (mã {Ma}). KHÔNG áp dụng thay đổi nào. Chi tiết: {DuLieu}",
                hang, ex.Code, string.Join("; ", ex.Data.Keys.Cast<object>().Select(k => $"{k}={ex.Data[k]}")));
        }
        catch (Exception ex)
        {
            // `Loi`: mọi exception khác — adapter/HTTP/timeout/DB/business mã khác. `SourceHash = null`
            // (D-90), `ErrorType` = tên kiểu (BR-011: **không** lưu message/stack/payload).
            var nguon = danhMuc?.Nguon ?? adapter.GetType().FullName;

            await GhiLoThatBaiAsync(
                uowManager, loRepo, dongHo, hang, CarrierAddressSyncOutcome.Loi,
                errorCode: null, ex.GetType().FullName, nguon, sourceHash: null,
                soMaNhan: 0, startedAtUtc);

            Logger.LogError(ex, "Đồng bộ mã hãng {Hang} thất bại (lỗi không mong đợi). Không áp dụng thay đổi nào.", hang);
        }
    }

    /// <summary>
    /// Ghi dòng lô `BiChan`/`Loi` trong UoW **riêng** (`requiresNew`, `CancellationToken.None`) — lượt bị
    /// từ chối/lỗi vẫn để lại dấu vết (`D-90`). Ghi thất bại ⇒ log `Error` **riêng**, **không** che exception
    /// gốc và **không** ném ra khỏi worker (nhịp sau thử lại như cũ).
    /// </summary>
    private async Task GhiLoThatBaiAsync(
        IUnitOfWorkManager uowManager,
        IRepository<CarrierAddressSyncRun, Guid> loRepo,
        IClock dongHo,
        string hang,
        CarrierAddressSyncOutcome outcome,
        string? errorCode,
        string? errorType,
        string? source,
        string? sourceHash,
        int soMaNhan,
        DateTime startedAtUtc)
    {
        try
        {
            using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

            var lo = CarrierAddressSyncRun.Ghi(
                hang,
                source,
                sourceHash,
                startedAtUtc,
                dongHo.Now,
                TriggeredByJob,
                outcome,
                errorCode,
                errorType,
                soMaNhan,
                soCapNhatTen: 0,
                soDanhDauNghiNgo: 0,
                soCanNguoiAnhXa: 0,
                mauCanNguoiAnhXa: null,
                tenantId: null);

            await loRepo.InsertAsync(lo, autoSave: true, cancellationToken: CancellationToken.None);
            await uow.CompleteAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "KHÔNG ghi được dòng lô đồng bộ mã hãng {Hang} cho lượt {Outcome} — mất dấu vết lượt (D-90).",
                hang, outcome);
        }
    }
}
