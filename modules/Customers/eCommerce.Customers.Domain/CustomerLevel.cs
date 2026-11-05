using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Customers;

/// <summary>
/// 🏅 **CẤP KHÁCH HÀNG** (`CusLevels`) — `WP-07` (`07 §WP-07`: *"`CustomerLevel` (ngưỡng)"*).
///
/// 🔑 **CẤP LÀ DỮ LIỆU, KHÔNG PHẢI ENUM:** ngưỡng lên cấp do **doanh nghiệp tự đặt** (VIP 5 triệu, VIP 10 triệu…)
/// và đổi theo thời gian. 📌 Một enum `Bac = 1, Vang = 2, KimCuong = 3` trong mã nghĩa là mỗi lần đổi ngưỡng
/// phải **deploy** — và không ai đổi được chính sách bán hàng của mình.
///
/// 🚨 **`MinAccumulatedAmount` là NGƯỠNG DUY NHẤT** để xếp cấp: nếu có thêm một điều kiện "điểm" nữa thì
/// hai cấp có thể **cùng đúng** và cấp của khách **đổi theo thứ tự đọc DB** — đó là lý do `NghiepVuKhach`
/// có luật sắp xếp **tất định** (`MinAccumulatedAmount` → `SortOrder` → `Id`).
/// </summary>
public class CustomerLevel : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiMaToiDa = 32;
    public const int DoDaiTenToiDa = 128;

    public Guid? TenantId { get; protected set; }

    /// <summary>Mã cấp (duy nhất trong tenant) — dùng trong báo cáo/hội thoại, ổn định hơn `Name`.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; protected set; } = default!;

    /// <summary>Ngưỡng **tiền tích luỹ** (≥ 0) để đạt cấp này.</summary>
    public decimal MinAccumulatedAmount { get; protected set; }

    /// <summary>% chiết khấu mặc định cho cấp (0 = không chiết khấu). 📌 Chỉ **thông tin** ở `WP-07`:
    /// chưa nối vào bảng giá (`Pricing`) — đừng báo *"khách VIP đã được giảm giá tự động"*.</summary>
    public decimal DiscountPercent { get; protected set; }

    /// <summary>Thứ tự hiển thị tất định (khi hai cấp trùng ngưỡng).</summary>
    public int SortOrder { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public string? Note { get; set; }

    public const int DoDaiGhiChuToiDa = 512;

    protected CustomerLevel() { }

    public CustomerLevel(Guid id, string code, string name, decimal minAccumulatedAmount, decimal discountPercent = 0m,
        int sortOrder = 0, string? note = null) : base(id)
    {
        DatMa(code);
        DatTen(name);
        DatNguong(minAccumulatedAmount, discountPercent);
        SortOrder = sortOrder;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void DatMa(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(code));
        }

        var sach = code.Trim();

        if (sach.Length > DoDaiMaToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(code)).WithData("toiDa", DoDaiMaToiDa);
        }

        Code = sach;
    }

    public void DatTen(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(name));
        }

        var sach = name.Trim();

        if (sach.Length > DoDaiTenToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(name)).WithData("toiDa", DoDaiTenToiDa);
        }

        Name = sach;
    }

    /// <summary>
    /// Đặt **ngưỡng** lên cấp + % chiết khấu.
    /// 🚨 **% ngoài `0..100` ⇒ NÉM:** một cấp giảm 120% là **bán lỗ có hệ thống** — và nó sẽ được áp cho
    /// mọi khách của cấp đó, không có cảnh báo nào.
    /// </summary>
    public void DatNguong(decimal minAccumulatedAmount, decimal discountPercent)
    {
        if (minAccumulatedAmount < 0m)
        {
            throw new BusinessException(CustomerErrorCodes.SoTienKhongHopLe)
                .WithData("minAccumulatedAmount", minAccumulatedAmount)
                .WithData("lyDo", "Ngưỡng tiền tích luỹ KHÔNG được âm.");
        }

        if (discountPercent is < 0m or > 100m)
        {
            throw new BusinessException(CustomerErrorCodes.SoTienKhongHopLe)
                .WithData("discountPercent", discountPercent)
                .WithData("lyDo", "% chiết khấu của cấp phải trong khoảng 0..100.");
        }

        MinAccumulatedAmount = NghiepVuKhach.LamTron(minAccumulatedAmount);
        DiscountPercent = Math.Round(discountPercent, 2, MidpointRounding.AwayFromZero);
    }

    public void Bat() => IsActive = true;

    public void Tat() => IsActive = false;

    /// <summary>Thứ tự hiển thị — cho phép **0** hoặc âm (âm = xếp trước) nhưng không nhận `int.MinValue`
    /// (tránh tràn khi tính khoảng sắp xếp).</summary>
    public void DatThuTu(int sortOrder)
    {
        if (sortOrder == int.MinValue)
        {
            throw new BusinessException(CustomerErrorCodes.SoTienKhongHopLe)
                .WithData("sortOrder", sortOrder)
                .WithData("lyDo", "Thứ tự không hợp lệ (giá trị biên).");
        }

        SortOrder = sortOrder;
    }
}

/// <summary>
/// 📞 **MỘT LẦN CHĂM SÓC KHÁCH** (`CusCares`) — `WP-07` (`07 §WP-07`: *"`CustomerCare` (log 5 loại)"*).
///
/// 🔑 **VÌ SAO LÀ BẢNG APPEND-ONLY, KHÔNG PHẢI CỘT `LastContactedAt`:** câu hỏi thật của quản lý là
/// *"tháng này ai đã gọi khách nào, bao nhiêu lần"* — một cột "lần cuối" **xoá mất** lịch sử và không đếm được.
/// 📌 Bản ghi **không sửa, không xoá** (không kế thừa `FullAudited` có `IsDeleted`) — sửa một biên bản
/// chăm sóc là **làm lại lịch sử**; muốn đính chính thì thêm bản ghi mới.
/// </summary>
public class CustomerCare : CreationAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiNoiDungToiDa = 2048;

    public Guid? TenantId { get; protected set; }

    public Guid CustomerId { get; protected set; }

    public CustomerCareType Type { get; protected set; }

    /// <summary>Nội dung trao đổi — 📌 **có thể là PII** (lời khách nói). 🔒 Không ghi thẳng vào log.</summary>
    public string Content { get; protected set; } = default!;

    /// <summary>Nhân viên thực hiện — `null` = hệ thống ghi (ví dụ nhắc sinh nhật tự động).</summary>
    public Guid? StaffId { get; protected set; }

    /// <summary>Lúc chăm sóc (UTC) — 📌 **không** dùng `CreationTime`: nhập lại một cuộc gọi hôm qua phải
    /// giữ đúng ngày xảy ra, nếu không báo cáo "chăm sóc tuần này" sai.</summary>
    public DateTime OccurredAtUtc { get; protected set; }

    /// <summary>Hẹn theo dõi lần sau — `null` = không hẹn (📌 phân biệt *"không hẹn"* với *"hẹn hôm nay"*).</summary>
    public DateTime? NextFollowUpAtUtc { get; protected set; }

    protected CustomerCare() { }

    public CustomerCare(Guid id, Guid customerId, CustomerCareType type, string content, DateTime occurredAtUtc,
        Guid? staffId = null, DateTime? nextFollowUpAtUtc = null) : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(content))
                .WithData("lyDo", "Một lần chăm sóc phải có NỘI DUNG (không ghi lại được gì thì không phải biên bản).");
        }

        var sach = content.Trim();

        if (sach.Length > DoDaiNoiDungToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(content)).WithData("toiDa", DoDaiNoiDungToiDa);
        }

        CustomerId = customerId;
        Type = type;
        Content = sach;
        OccurredAtUtc = occurredAtUtc;
        StaffId = staffId;
        NextFollowUpAtUtc = nextFollowUpAtUtc;
    }
}
