using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Address.Carrier;

/// <summary>
/// **Kết cục một lượt đồng bộ mã địa chỉ hãng** — `0 DaApDung` · `1 BiChan` · `2 Loi`
/// (`ADDR-002` §3.1, `D-83`/`D-90`). Enum lưu DB **chỉ thêm ở cuối** (`D-17`).
/// </summary>
public enum CarrierAddressSyncOutcome
{
    /// <summary>Đã áp dụng (kể cả lượt **0 việc** — vẫn ghi 1 dòng, `D-83`).</summary>
    DaApDung = 0,

    /// <summary>Hàng rào `LapKeHoach` từ chối (`0506`/`0507`/`0508`/`0509`) — **0** thay đổi ánh xạ.</summary>
    BiChan = 1,

    /// <summary>Lỗi không mong đợi —— **0** thay đổi ánh xạ; chỉ lưu **loại** exception (BR-011).</summary>
    Loi = 2
}

/// <summary>
/// **BẢN GHI LÔ ĐỒNG BỘ MÃ ĐỊA CHỈ HÃNG** (`ADDR-002` §4.1, `D-83`/`D-90`) — sổ **APPEND-ONLY**.
///
/// 🔒 BẤT BIẾN:
///   • `CreationAuditedAggregateRoot` (**không** `FullAudited`) ⇒ **không** `IsDeleted`, **không** UPDATE/DELETE;
///   • **0** public setter, **0** method đổi — chỉ có factory <see cref="Ghi"/>;
///   • **mỗi lượt của mỗi hãng ĐÃ GIÀNH ĐƯỢC KHOÁ** ⇒ đúng **1** dòng (kể cả lượt lỗi adapter trước
///     `LapKeHoach` ⇒ `Outcome = Loi`). Bận khoá / chưa có adapter ⇒ **0** dòng (`D-90`/`D-236`);
///   • `SourceHash` do **server** tính trên danh mục đã chuẩn hoá — **không** nhận từ client;
///   • `ErrorType` chỉ là `ex.GetType().FullName` — **cấm** `ex.Message`/stack/payload (BR-011).
///
/// 📌 **KHÔNG** là aggregate của `AddrCarrierMappings`: bảng lô là **nhật ký**, không có FK/navigation
/// tới ánh xạ (`D-83`).
/// </summary>
public class CarrierAddressSyncRun : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Số mã tối đa của mẫu `MauCanNguoiAnhXa` (`D-234`).</summary>
    public const int SoMauToiDa = 20;

    public Guid? TenantId { get; protected set; }

    /// <summary>`Trim().ToUpperInvariant()` — cùng quy tắc `CarrierAddressMapping.CarrierCode`.</summary>
    public string CarrierCode { get; private set; } = default!;

    /// <summary>`danhMuc.Nguon`; lỗi trước khi có danh mục ⇒ định danh adapter, hoặc `null` nếu không biết.</summary>
    public string? Source { get; private set; }

    /// <summary>SHA-256 hex **thường** 64 ký tự của danh mục đã chuẩn hoá; `null` khi chưa có danh mục (`D-90`).</summary>
    public string? SourceHash { get; private set; }

    /// <summary>Lấy **trước** khi gọi adapter (`D-90`).</summary>
    public DateTime StartedAtUtc { get; private set; }

    public DateTime FinishedAtUtc { get; private set; }

    /// <summary>`job:CarrierAddressSyncWorker` (sau này: `user:{id}`).</summary>
    public string TriggeredBy { get; private set; } = default!;

    public CarrierAddressSyncOutcome Outcome { get; private set; }

    /// <summary>Mã đã NÉM khi `<see cref="CarrierAddressSyncOutcome.BiChan"/>`; `null` cho `DaApDung`/`Loi`.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>`ex.GetType().FullName` khi `Loi`; **cấm** message/stack/payload (BR-011).</summary>
    public string? ErrorType { get; private set; }

    /// <summary>Số đơn vị trong danh mục hãng của lượt (số đếm **dòng**, không phải số lượng — `D-31` không áp dụng).</summary>
    public int SoMaNhan { get; private set; }

    /// <summary>= `KeHoachDongBoMaHang.SoCapNhatTen` (số **việc**, kể cả việc trên ánh xạ đã `NghiNgo`).</summary>
    public int SoCapNhatTen { get; private set; }

    /// <summary>= `KeHoachDongBoMaHang.SoDanhDauNghiNgo` (số **việc**).</summary>
    public int SoDanhDauNghiNgo { get; private set; }

    /// <summary>= `KeHoachDongBoMaHang.SoCanNguoiAnhXa`; **bằng** số dòng con (NFR-003, `D-88`).</summary>
    public int SoCanNguoiAnhXa { get; private set; }

    /// <summary>
    /// **MẪU** ≤ 20 `CarrierAddressCode` đã `Trim`, khử trùng, sắp `Ordinal`; **`null`** khi 0 việc
    /// (không ghi `{}` — `D-234`). Danh sách **đủ** ở bảng con <see cref="CarrierAddressSyncRunUnmappedCode"/>.
    /// </summary>
    public string[]? MauCanNguoiAnhXa { get; private set; }

    protected CarrierAddressSyncRun()
    {
    }

    public static CarrierAddressSyncRun Ghi(
        string carrierCode,
        string? source,
        string? sourceHash,
        DateTime startedAtUtc,
        DateTime finishedAtUtc,
        string triggeredBy,
        CarrierAddressSyncOutcome outcome,
        string? errorCode,
        string? errorType,
        int soMaNhan,
        int soCapNhatTen,
        int soDanhDauNghiNgo,
        int soCanNguoiAnhXa,
        IEnumerable<string>? mauCanNguoiAnhXa = null,
        Guid? tenantId = null,
        Guid? id = null)
    {
        carrierCode = Check.NotNullOrWhiteSpace(carrierCode, nameof(carrierCode), maxLength: 32)
            .Trim().ToUpperInvariant();

        triggeredBy = Check.NotNullOrWhiteSpace(triggeredBy, nameof(triggeredBy), maxLength: 128).Trim();

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Outcome không hợp lệ.");
        }

        if (sourceHash is not null)
        {
            Check.NotNullOrWhiteSpace(sourceHash, nameof(sourceHash), maxLength: 64);

            if (sourceHash.Length != 64 || !sourceHash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                throw new ArgumentException("SourceHash phải là SHA-256 hex thường 64 ký tự.", nameof(sourceHash));
            }
        }

        if (soMaNhan < 0 || soCapNhatTen < 0 || soDanhDauNghiNgo < 0 || soCanNguoiAnhXa < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(soMaNhan), "Số đếm của lượt đồng bộ không được âm.");
        }

        return new CarrierAddressSyncRun
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            CarrierCode = carrierCode,
            Source = CatChuoi(source, 512, nameof(source)),
            SourceHash = sourceHash,
            StartedAtUtc = ChuanHoaUtc(startedAtUtc),
            FinishedAtUtc = ChuanHoaUtc(finishedAtUtc),
            TriggeredBy = triggeredBy,
            Outcome = outcome,
            ErrorCode = CatChuoi(errorCode, 64, nameof(errorCode)),
            ErrorType = CatChuoi(errorType, 256, nameof(errorType)),
            SoMaNhan = soMaNhan,
            SoCapNhatTen = soCapNhatTen,
            SoDanhDauNghiNgo = soDanhDauNghiNgo,
            SoCanNguoiAnhXa = soCanNguoiAnhXa,
            MauCanNguoiAnhXa = ChuanHoaMau(mauCanNguoiAnhXa)
        };
    }

    /// <summary>`null` khi rỗng — **không** ghi `{}` (`D-234`); ≤ 20 mã, khử trùng, sắp `Ordinal`.</summary>
    private static string[]? ChuanHoaMau(IEnumerable<string>? mau)
    {
        if (mau is null)
        {
            return null;
        }

        var sach = mau
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Take(SoMauToiDa)
            .ToArray();

        return sach.Length == 0 ? null : sach;
    }

    /// <summary>Chuẩn hoá chuỗi tuỳ chọn; **ném** nếu vượt độ dài cột (không cắt im lặng).</summary>
    private static string? CatChuoi(string? giaTri, int doDaiToiDa, string tenThamSo)
    {
        if (string.IsNullOrWhiteSpace(giaTri))
        {
            return null;
        }

        var sach = giaTri.Trim();

        if (sach.Length > doDaiToiDa)
        {
            throw new ArgumentException(
                $"Giá trị vượt độ dài tối đa {doDaiToiDa} ký tự.", tenThamSo);
        }

        return sach;
    }

    /// <summary>Npgsql yêu cầu `DateTime` Kind=UTC cho cột `timestamptz`.</summary>
    private static DateTime ChuanHoaUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
