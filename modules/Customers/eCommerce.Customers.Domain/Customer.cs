using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **HỒ SƠ KHÁCH HÀNG** (`CusCustomers`) — `CUS-001`.
///
/// **BA BẤT BIẾN ĐỊNH HÌNH TOÀN BỘ THIẾT KẾ (tài liệu §0):**
/// <list type="number">
///   <item>🚨 **`Address` KHÔNG thuộc module này** (`ADR-01`): entity dưới đây **KHÔNG có** cột
///     `Street`/`Ward`/`Province` nào. Mô hình *"hai nguồn sự thật về địa chỉ"* (kiểu nopCommerce) là
///     **anti-pattern đã bị ghi nhận** của dự án. 🛡️ Có test reflection chặn tái phát (`TC-14`).
///     ⏳ Sổ địa chỉ (`CustomerAddressLink`) **bị chặn có thật** vì `Address` **chưa tồn tại**.</item>
///   <item>**Khách vãng lai là hạng nhất** (`ADR-03`): `Order.CustomerId` là `Guid?` ⇒ đơn **không** có khách
///     **vẫn hợp lệ**. Hệ thống **KHÔNG** được bắt buộc tạo `Customer` để bán được. Khách vãng lai được
///     **nâng cấp (claim)** bằng đường **idempotent** (`ADR-05`/`ADR-07`) — xem <see cref="NangCapVangLai"/>.</item>
///   <item>**Dữ liệu khách là dữ liệu CÁ NHÂN ⇒ có nghĩa vụ pháp lý**: Luật BV dữ liệu cá nhân
///     **91/2025/QH15** (hiệu lực 01/01/2026) cho chủ thể dữ liệu quyền *"xem, chỉnh sửa, **yêu cầu xoá**,
///     hạn chế xử lý"*. ⇒ Xoá khách là **XOÁ MỀM** (`FullAudited` ⇒ `IsDeleted`), **không** xoá cứng —
///     vừa thoả nghĩa vụ vừa **giữ được bất biến chứng từ** (hoá đơn/đơn đã phát hành).
///     🔒 **PII chỉ được vào log qua <see cref="PiiMasker"/>** (`ADR-11`).</item>
/// </list>
///
/// 🔑 **`IdempotencyKey`** (`ADR-05`): sinh từ **dữ liệu nghiệp vụ** (ổn định), **KHÔNG** `Guid.NewGuid()` —
/// nếu không thì **mỗi lần retry là một khách mới** (đúng lỗi `XC-002` **D7**).
/// </summary>
public class Customer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của `IdempotencyKey` — khớp `HasMaxLength(128)` ở mapping.</summary>
    public const int DoDaiKhoaIdempotencyToiDa = 128;

    public const int DoDaiTenToiDa = 256;
    public const int DoDaiEmailToiDa = 256;
    public const int DoDaiDienThoaiToiDa = 32;

    public Guid? TenantId { get; protected set; }

    public string Name { get; set; } = default!;

    /// <summary>Email liên hệ — **PII**. 🔒 Không log trực tiếp (dùng <see cref="PiiMasker"/>).</summary>
    public string? Email { get; protected set; }

    /// <summary>Số điện thoại — **PII**. 🔒 Không log trực tiếp.</summary>
    public string? Phone { get; protected set; }

    /// <summary>
    /// Tài khoản đăng nhập gắn với khách (ABP Identity) — **`null` = KHÁCH VÃNG LAI** (`ADR-03`).
    /// 🚨 **Claim (`ADR-07`) CHỈ khi cột này `null`** — gán đè lên khách đã có chủ là **đổi chủ sở hữu
    /// của người khác** (xem <see cref="NangCapVangLai"/>).
    /// </summary>
    public Guid? IdentityUserId { get; protected set; }

    public CustomerStatus Status { get; protected set; } = CustomerStatus.Inactive;

    /// <summary>Nhóm khách (`ADR-09`) — `null` = khách lẻ mặc định. **Không FK cứng** theo quy ước module
    /// (giống `Sku` của Pricing làm việc theo chuỗi): nhóm là **dữ liệu tham chiếu**, không phải quan hệ vòng đời.</summary>
    public Guid? CustomerGroupId { get; set; }

    /// <summary>Giá trị vòng đời (LTV) — `ADR-10`: **cột rời**, KHÔNG dùng `Money` complex type
    /// (nullable complex type vướng bug EF #37559, `XC-001` đã chốt dùng cột rời trên EF 10).
    /// ⚠️ **Cấm** `double`/`float`; **cấm** so tiền bằng `==` (`XC-001` **BR-003**).</summary>
    public decimal? LifetimeValueAmount { get; set; }

    public string? LifetimeValueCurrency { get; set; }

    /// <summary>Khoá idempotency **ỔN ĐỊNH** (`ADR-05`) — có unique index `(TenantId, IdempotencyKey)`.</summary>
    public string? IdempotencyKey { get; protected set; }

    /// <summary>Ghi chú nội bộ (ví dụ **lý do chặn** — `ADR-04` yêu cầu `Unblock` có lý do).</summary>
    public string? Note { get; set; }

    protected Customer() { }

    public Customer(Guid id, string name, string? email = null, string? phone = null, string? idempotencyKey = null) : base(id)
    {
        DatTen(name);
        DatLienHe(email, phone);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if (idempotencyKey.Trim().Length > DoDaiKhoaIdempotencyToiDa)
            {
                throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                    .WithData("truong", nameof(idempotencyKey))
                    .WithData("toiDa", DoDaiKhoaIdempotencyToiDa);
            }

            IdempotencyKey = idempotencyKey.Trim();
        }
    }

    // ── Đặt dữ liệu (có kiểm) ────────────────────────────────────────────────────────────────────

    public void DatTen(string ten)
    {
        if (string.IsNullOrWhiteSpace(ten))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(ten));
        }

        if (ten.Trim().Length > DoDaiTenToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(ten)).WithData("toiDa", DoDaiTenToiDa);
        }

        Name = ten.Trim();
    }

    /// <summary>
    /// Đặt liên hệ (email + SĐT). 🔒 **Chuẩn hoá + kiểm định dạng NGAY Ở ĐÂY** (không chỉ ở app service):
    /// email/SĐT sai định dạng mà lọt vào DB thì **mọi** đường gửi thông báo/hoá đơn sau này hỏng,
    /// và lỗi nổi lên ở **nơi không liên quan** (khó truy).
    /// ⚠️ Kiểm **định dạng ở mức tối thiểu** (không phải xác thực thật) — xác thực thật là việc của
    /// bước gửi OTP/email; ở đây chỉ chặn **rác rõ ràng** (thiếu `@`, quá ngắn).
    /// </summary>
    public void DatLienHe(string? email, string? phone)
    {
        var emailChuan = email?.Trim();
        var phoneChuan = phone?.Trim();

        if (!string.IsNullOrEmpty(emailChuan))
        {
            if (emailChuan.Length > DoDaiEmailToiDa || !KhuonEmail.IsMatch(emailChuan))
            {
                throw new BusinessException(CustomerErrorCodes.LienHeKhongHopLe)
                    .WithData("truong", nameof(email))
                    .WithData("lyDo", "Email không đúng định dạng (cần dạng ten@mien.tld).");
            }
        }

        if (!string.IsNullOrEmpty(phoneChuan))
        {
            if (phoneChuan.Length > DoDaiDienThoaiToiDa || !KhuonDienThoai.IsMatch(phoneChuan))
            {
                throw new BusinessException(CustomerErrorCodes.LienHeKhongHopLe)
                    .WithData("truong", nameof(phone))
                    .WithData("lyDo", "Số điện thoại chỉ gồm chữ số, có thể bắt đầu bằng `+` (tối thiểu 8 chữ số).");
            }
        }

        Email = string.IsNullOrEmpty(emailChuan) ? null : emailChuan.ToLowerInvariant();
        Phone = string.IsNullOrEmpty(phoneChuan) ? null : phoneChuan;
    }

    // 🔒 Khuôn **tối thiểu** — cố ý KHÔNG dùng regex email "đầy đủ" (những regex đó vừa phức tạp vừa
    // từ chối nhầm địa chỉ hợp lệ). Mục tiêu: chặn rác rõ ràng, không phải xác thực.
    private static readonly Regex KhuonEmail =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static readonly Regex KhuonDienThoai =
        new(@"^\+?\d{8,}$", RegexOptions.Compiled);

    // ── MÁY TRẠNG THÁI (ADR-04: MỌI chuyển sai ⇒ NÉM, KHÔNG im lặng) ────────────────────────────

    /// <summary>
    /// Kích hoạt khách. **NÉM** nếu khách đang bị **CHẶN** (`ADR-04`: `Blocked` là trạng thái hấp thụ
    /// **có điều kiện** — chỉ ra được bằng <see cref="MoChan"/>, cần quyền + lý do).
    /// 🔒 **Idempotent khi đã `Active`** (trả về, không ném): đây **không** phải "chuyển sai" mà là
    /// "đã ở đúng trạng thái" — cùng quy ước với `Product.Publish`.
    /// </summary>
    public void KichHoat()
    {
        if (Status == CustomerStatus.Blocked)
        {
            throw new BusinessException(CustomerErrorCodes.KhachDangBiChan)
                .WithData("tu", Status.ToString())
                .WithData("den", CustomerStatus.Active.ToString())
                .WithData("lyDo", "Khách đang BỊ CHẶN ⇒ phải gỡ chặn (kèm lý do) trước khi kích hoạt.");
        }

        if (Status == CustomerStatus.Active)
        {
            return;
        }

        Status = CustomerStatus.Active;
    }

    /// <summary>
    /// 🚫 **CHẶN khách** (`ADR-04`) — cần **LÝ DO** (`lyDo` bắt buộc).
    /// **VÌ SAO BẮT BUỘC LÝ DO:** chặn khách là **hành động ảnh hưởng quyền mua hàng** — chặn mà không ghi lý do
    /// thì 6 tháng sau **không ai giải thích được** vì sao khách này không đặt được hàng, và **không ai dám gỡ**.
    /// 📌 Idempotent khi đã `Blocked` (chặn lần hai là vô hại — retry/double-click).
    /// </summary>
    public void Chan(string lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(lyDo))
                .WithData("lyDo", "CHẶN khách BẮT BUỘC có lý do (để người sau giải thích/gỡ được).");
        }

        if (Status == CustomerStatus.Blocked)
        {
            return;
        }

        Status = CustomerStatus.Blocked;
        Note = lyDo.Trim();
    }

    /// <summary>
    /// 🔓 **GỠ CHẶN** — **NÉM** nếu khách **KHÔNG** đang bị chặn.
    /// 📌 **VÌ SAO NÉM (không im lặng cho qua):** `Unblock` một khách bình thường là dấu hiệu **người vận hành
    /// đang thao tác nhầm bản ghi** (hoặc UI gửi sai `id`) ⇒ nếu im lặng thì họ **tin** đã gỡ chặn cho người
    /// đang bị chặn, trong khi thực tế **chưa đụng tới ai** (đúng họ lỗi "im lặng" quy ước #11).
    /// 🔒 Bắt buộc `lyDo` cùng lý do như <see cref="Chan"/>.
    /// </summary>
    public void MoChan(string lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(lyDo))
                .WithData("lyDo", "GỠ CHẶN BẮT BUỘC có lý do.");
        }

        if (Status != CustomerStatus.Blocked)
        {
            throw new BusinessException(CustomerErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("tu", Status.ToString())
                .WithData("den", CustomerStatus.Active.ToString())
                .WithData("lyDo", "Khách KHÔNG ở trạng thái `Blocked` ⇒ không có gì để gỡ. Kiểm lại bản ghi/`id`.");
        }

        Status = CustomerStatus.Active;
        Note = lyDo.Trim();
    }

    /// <summary>Khách có đang **dùng được** không (để chặn bán cho khách bị chặn — `ADR-04`).</summary>
    public bool DungDuoc => Status != CustomerStatus.Blocked;

    // ── NÂNG CẤP KHÁCH VÃNG LAI (ADR-07) ────────────────────────────────────────────────────────

    /// <summary>
    /// 🚨 **NÂNG CẤP KHÁCH VÃNG LAI thành khách có TÀI KHOẢN** (`CUS-001` **ADR-07**) — thao tác
    /// **ĐỔI CHỦ SỞ HỮU** trên **cùng** bản ghi, **KHÔNG** tạo khách mới.
    ///
    /// **VÌ SAO KHÔNG TẠO KHÁCH MỚI (đã cân nhắc và loại):** tạo mới rồi trỏ đơn sang ⇒ **HAI khách cho MỘT
    /// con người** ⇒ lịch sử mua, điểm thưởng (`LOY-001`), đánh giá (`REV-001`) **phân mảnh vĩnh viễn**
    /// và **không gộp lại được** (không có khoá nào nói hai bản ghi là một người).
    ///
    /// 🔒 **HAI GUARD (mỗi guard có mã RIÊNG vì hành động của người vận hành khác nhau):**
    /// <list type="bullet">
    ///   <item><b>Đã có chủ</b> (<see cref="IdentityUserId"/> khác `null`) ⇒ **NÉM `0004`**:
    ///     gán đè nghĩa là **lấy tài khoản của người khác** (hoặc ghi đè chủ cũ ⇒ mất lịch sử của họ).</item>
    ///   <item><b>Đang bị CHẶN</b> ⇒ **NÉM `0005`**: khách chây nợ/gian lận **không** được "hợp thức hoá"
    ///     bằng cách tạo tài khoản mới (đó là đường vòng qua lệnh chặn).</item>
    /// </list>
    ///
    /// 📌 **Idempotent khi ĐÃ LÀ CHÍNH tài khoản đó** (`ADR-05`): gọi lại (retry/queue lặp) ⇒ **không ném**,
    /// vì đó là *"đã ở đúng trạng thái"*, không phải chuyển sai. Nếu ném thì **consumer retry vô hạn**.
    /// </summary>
    public void NangCapVangLai(Guid identityUserId)
    {
        if (identityUserId == Guid.Empty)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(identityUserId));
        }

        if (Status == CustomerStatus.Blocked)
        {
            throw new BusinessException(CustomerErrorCodes.KhachDangBiChan)
                .WithData("customerId", Id)
                .WithData("lyDo", "Khách đang BỊ CHẶN ⇒ không được nâng cấp thành tài khoản (đường vòng qua lệnh chặn).");
        }

        if (IdentityUserId is { } daCo && daCo == identityUserId)
        {
            return;   // đã là chính tài khoản này ⇒ idempotent, KHÔNG ném
        }

        if (IdentityUserId is not null)
        {
            throw new BusinessException(CustomerErrorCodes.KhachDaCoChu)
                .WithData("customerId", Id)
                .WithData("identityUserIdHienTai", IdentityUserId)
                .WithData("identityUserIdMoi", identityUserId)
                .WithData("lyDo", "Khách ĐÃ có tài khoản ⇒ nâng cấp nữa là ĐỔI CHỦ SỞ HỮU (mất lịch sử của chủ cũ).");
        }

        IdentityUserId = identityUserId;

        // 📌 Nâng cấp xong ⇒ khách **dùng được**: để `Inactive` thì mọi đường kiểm `DungDuoc`
        // sẽ chặn bán cho một khách **vừa được hợp thức hoá** — trái ý định của thao tác này.
        if (Status == CustomerStatus.Inactive)
        {
            Status = CustomerStatus.Active;
        }
    }

    /// <summary>
    /// Gắn **khoá idempotency** nếu chưa có (`ADR-05`) — dùng ở biên vào (webhook/queue).
    /// 🔒 **KHÔNG ghi đè khoá đã có**: khoá cũ là **bằng chứng** của lần tạo đầu; ghi đè ⇒ mất khả năng
    /// chống trùng cho các retry **đang bay**.
    /// </summary>
    public void DatKhoaIdempotency(string khoa)
    {
        if (string.IsNullOrWhiteSpace(khoa))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(khoa));
        }

        if (IdempotencyKey is not null)
        {
            return;
        }

        if (khoa.Trim().Length > DoDaiKhoaIdempotencyToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(khoa)).WithData("toiDa", DoDaiKhoaIdempotencyToiDa);
        }

        IdempotencyKey = khoa.Trim();
    }
}
