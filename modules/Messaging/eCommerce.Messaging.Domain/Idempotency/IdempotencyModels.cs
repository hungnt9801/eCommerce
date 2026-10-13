using Volo.Abp;

namespace eCommerce.Messaging.Idempotency;

/// <summary>
/// **Khoá idempotency** của một biên vào — gồm <b>phạm vi</b> (scope) và <b>giá trị</b>.
///
/// 🎯 VÌ SAO PHẢI CÓ SCOPE: cùng một giá trị như `"12345"` có thể là mã đơn của Shopee, mã giao dịch
/// của VNPay, hay mã đơn nội bộ. Nếu chỉ dùng giá trị trần thì các miền sẽ **ghi đè lẫn nhau** ⇒
/// yêu cầu của hệ thống này bị coi là "đã xử lý" bởi hệ thống khác. Đây là **lỗi im lặng**.
///
/// 🔒 BẤT BIẾN: khoá được **chuẩn hoá** khi tạo (trim + max length) để `" ABC "` và `"abc"` **không**
/// tạo ra hai bản ghi khác nhau.
/// </summary>
public sealed record IdempotencyKey
{
    public const int DoDaiScopeToiDa = 64;
    public const int DoDaiGiaTriToiDa = 256;

    /// <summary>Phạm vi, ví dụ <c>shopee:order</c>, <c>vnpay:ipn</c>, <c>ghtk:webhook</c>.</summary>
    public string Scope { get; }

    /// <summary>Giá trị định danh trong phạm vi, ví dụ mã đơn / mã giao dịch.</summary>
    public string Value { get; }

    private IdempotencyKey(string scope, string value)
    {
        Scope = scope;
        Value = value;
    }

    public static IdempotencyKey Cua(string scope, string value)
    {
        var s = ChuanHoa(scope, DoDaiScopeToiDa, nameof(scope));
        var v = ChuanHoa(value, DoDaiGiaTriToiDa, nameof(value));
        return new IdempotencyKey(s, v);
    }

    private static string ChuanHoa(string? raw, int toiDa, string ten)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new BusinessException(MessagingErrorCodes.KhoaKhongHopLe)
                .WithData("truong", ten)
                .WithData("lyDo", "rỗng");
        }

        var s = raw.Trim();

        if (s.Length > toiDa)
        {
            throw new BusinessException(MessagingErrorCodes.KhoaKhongHopLe)
                .WithData("truong", ten)
                .WithData("doDai", s.Length)
                .WithData("toiDa", toiDa);
        }

        return s;
    }

    /// <summary>
    /// Tên khoá phân tán để **tuần tự hoá** các yêu cầu cùng khoá.
    /// 🔒 KHÔNG chứa tiền tố `ecommerce:` — tiền tố do `AbpDistributedLockOptions.KeyPrefix` cấp
    /// (quy ước `XC-003` §5.4; thêm vào đây sẽ thành **tiền tố đôi** — đúng lỗi C13 đã gặp).
    /// </summary>
    public string KhoaLock() => $"messaging:idempotency:{Scope}:{Value}";

    public override string ToString() => $"{Scope}:{Value}";
}

/// <summary>Trạng thái của một bản ghi idempotency.</summary>
public enum IdempotencyState
{
    /// <summary>Đang xử lý (đã nhận, chưa xong).</summary>
    DangXuLy = 0,

    /// <summary>Đã xử lý xong — lần gọi sau trả lại kết quả này.</summary>
    HoanThanh = 1,

    /// <summary>Xử lý thất bại — có thể thử lại nếu còn lượt.</summary>
    ThatBai = 2
}

/// <summary>Kết quả một lần gọi <c>IIdempotencyGuard.ThucThiAsync</c>.</summary>
public enum IdempotencyOutcomeKind
{
    /// <summary>Lần đầu gặp khoá này ⇒ đã **thực thi** xử lý.</summary>
    DaThucThi = 0,

    /// <summary>Khoá đã xử lý xong trước đó ⇒ **trả lại kết quả cũ**, KHÔNG thực thi lại.</summary>
    TraVeKetQuaCu = 1,

    /// <summary>Khoá đang được xử lý ở nơi khác ⇒ tầng HTTP trả <c>409</c>.</summary>
    DangXuLy = 2,

    /// <summary>Cùng khoá nhưng **payload khác** ⇒ lỗi, KHÔNG trả kết quả cũ.</summary>
    XungDotPayload = 3
}

/// <summary>
/// Kết quả trả về của <c>IIdempotencyGuard.ThucThiAsync</c>.
/// </summary>
/// <param name="Kind">Loại kết quả.</param>
/// <param name="Result">Kết quả đã lưu (chỉ có khi <see cref="IdempotencyOutcomeKind.DaThucThi"/> hoặc <see cref="IdempotencyOutcomeKind.TraVeKetQuaCu"/>).</param>
/// <param name="ErrorCode">Mã lỗi (chỉ có khi <see cref="IdempotencyOutcomeKind.XungDotPayload"/> hoặc <see cref="IdempotencyOutcomeKind.DangXuLy"/>).</param>
public sealed record IdempotencyOutcome(
    IdempotencyOutcomeKind Kind,
    string? Result = null,
    string? ErrorCode = null)
{
    /// <summary>Có phải lần gọi này **thực sự chạy** xử lý hay không.</summary>
    public bool DaChayXuLy => Kind == IdempotencyOutcomeKind.DaThucThi;

    /// <summary>Có phải trả lại kết quả của lần xử lý trước.</summary>
    public bool LaPhatLai => Kind == IdempotencyOutcomeKind.TraVeKetQuaCu;

    /// <summary>Có phải lỗi (xung đột payload hoặc đang xử lý) hay không.</summary>
    public bool LaLoi => Kind is IdempotencyOutcomeKind.XungDotPayload or IdempotencyOutcomeKind.DangXuLy;
}
