using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Messaging.Idempotency;

/// <summary>
/// Bản ghi **chống trùng ở biên vào** (inbox) — trái tim của `XC-002` ở tầng nghiệp vụ.
///
/// 🔒 BẤT BIẾN:
///   1. **Một khoá chỉ có MỘT bản ghi hợp lệ** — ràng buộc unique `(TenantId, Scope, Value)` và
///      **PHẢI** dùng `NULLS NOT DISTINCT`, nếu không thì khi `TenantId` NULL ràng buộc **mất tác dụng**
///      (đúng lỗi C4/F1 đã gặp thật và đã kiểm chứng bằng PostgreSQL).
///   2. **PayloadHash theo phiên bản** (`XC-002` Q9): khi đổi thuật toán băm, bản ghi cũ **không** được
///      coi là xung đột giả ⇒ lưu kèm <see cref="PayloadHashVersion"/>.
///   3. State machine **NÉM LỖI** khi chuyển trạng thái sai (quy ước #11) — không im lặng bỏ qua.
/// </summary>
public class IdempotencyRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Phạm vi khoá (ví dụ <c>vnpay:ipn</c>).</summary>
    public string Scope { get; protected set; } = default!;

    /// <summary>Giá trị khoá (ví dụ mã giao dịch).</summary>
    public string KeyValue { get; protected set; } = default!;

    /// <summary>Hash của payload (đã chuẩn hoá) tại thời điểm nhận lần đầu.</summary>
    public string PayloadHash { get; protected set; } = default!;

    /// <summary>Phiên bản thuật toán băm — xem `XC-002` Q9.</summary>
    public int PayloadHashVersion { get; protected set; }

    public IdempotencyState State { get; protected set; }

    /// <summary>Kết quả đã lưu để **phát lại** cho lần gọi sau (thường là JSON).</summary>
    public string? Result { get; protected set; }

    public string? ErrorCode { get; protected set; }

    public string? ErrorMessage { get; protected set; }

    /// <summary>Số lần đã thử (ngưỡng thử = <b>5</b> theo `XC-002` Q3).</summary>
    public int Attempts { get; protected set; }

    public DateTime? CompletedAtUtc { get; protected set; }

    /// <summary>Hết hạn lưu vết — sau mốc này khoá có thể được dùng lại (dọn dẹp định kỳ).</summary>
    public DateTime ExpiresAtUtc { get; protected set; }

    protected IdempotencyRecord()
    {
    }

    private IdempotencyRecord(
        Guid id,
        IdempotencyKey key,
        string payloadHash,
        int payloadHashVersion,
        DateTime expiresAtUtc,
        Guid? tenantId)
    {
        Id = id;
        Scope = key.Scope;
        KeyValue = key.Value;
        PayloadHash = payloadHash;
        PayloadHashVersion = payloadHashVersion;
        State = IdempotencyState.DangXuLy;
        Attempts = 1;
        ExpiresAtUtc = expiresAtUtc;
        TenantId = tenantId;
    }

    /// <summary>Bắt đầu xử lý một khoá MỚI.</summary>
    public static IdempotencyRecord BatDau(
        IdempotencyKey key,
        string payloadHash,
        int payloadHashVersion,
        DateTime expiresAtUtc,
        Guid? tenantId = null,
        Guid? id = null)
        => new(id ?? Guid.NewGuid(), key, payloadHash, payloadHashVersion, expiresAtUtc, tenantId);

    public IdempotencyKey Khoa() => IdempotencyKey.Cua(Scope, KeyValue);

    /// <summary>
    /// Payload lần này có **trùng** với payload đã ghi nhận hay không.
    /// 🔒 So cả <see cref="PayloadHashVersion"/>: khác phiên bản băm ⇒ **không** kết luận là trùng
    /// (tránh "xung đột khoá GIẢ" sau khi nâng cấp thuật toán — `XC-002` Q9).
    /// </summary>
    public bool CungPayload(string payloadHash, int payloadHashVersion)
        => PayloadHashVersion == payloadHashVersion
           && string.Equals(PayloadHash, payloadHash, StringComparison.Ordinal);

    public bool ConHieuLuc(DateTime nowUtc) => ExpiresAtUtc > nowUtc;

    /// <summary>Đánh dấu xử lý xong và lưu kết quả để phát lại.</summary>
    public void HoanThanh(string? result, DateTime nowUtc)
    {
        if (State == IdempotencyState.HoanThanh)
        {
            throw new BusinessException(MessagingErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("tu", State.ToString())
                .WithData("den", nameof(IdempotencyState.HoanThanh));
        }

        State = IdempotencyState.HoanThanh;
        Result = result;
        ErrorCode = null;
        ErrorMessage = null;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>Ghi nhận thất bại và tăng số lần thử.</summary>
    public void ThatBai(string errorCode, string? errorMessage, DateTime nowUtc)
    {
        if (State == IdempotencyState.HoanThanh)
        {
            // 🔒 Không cho phép hạ một bản ghi ĐÃ THÀNH CÔNG xuống thất bại: nếu cho phép thì một
            // lần thử lại lỗi sẽ làm mất kết quả đã trả cho khách.
            throw new BusinessException(MessagingErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("tu", State.ToString())
                .WithData("den", nameof(IdempotencyState.ThatBai));
        }

        State = IdempotencyState.ThatBai;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Attempts++;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>Nhận lại để thử lại (chỉ khi đang thất bại hoặc đang xử lý nhưng đã hết hạn).</summary>
    public void ThuLai(DateTime nowUtc)
    {
        if (State == IdempotencyState.HoanThanh)
        {
            throw new BusinessException(MessagingErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("tu", State.ToString())
                .WithData("den", nameof(IdempotencyState.DangXuLy));
        }

        State = IdempotencyState.DangXuLy;
        Attempts++;
        CompletedAtUtc = null;
    }

    public void GiaHan(DateTime expiresAtUtc) => ExpiresAtUtc = expiresAtUtc;

    /// <summary>
    /// 🚨 BUG ĐÃ SỬA (test bắt được): dùng lại một bản ghi **đã hết hạn** cho **payload MỚI**.
    ///
    /// Trước đây nhánh này chỉ gọi <see cref="GiaHan"/> ⇒ bản ghi vẫn giữ `State = HoanThanh` **cũ** và
    /// `PayloadHash` **cũ** ⇒ khi xử lý xong, <see cref="HoanThanh"/> **NÉM LỖI** (vì đã ở trạng thái
    /// hoàn thành) — tức yêu cầu hợp lệ bị chặn bởi **dữ liệu cũ**, và thông báo lỗi chẳng liên quan.
    ///
    /// Nay khởi động lại **toàn bộ** trạng thái của bản ghi như một bản ghi mới.
    /// </summary>
    public void BatDauLai(string payloadHash, int payloadHashVersion, DateTime expiresAtUtc)
    {
        PayloadHash = payloadHash;
        PayloadHashVersion = payloadHashVersion;
        State = IdempotencyState.DangXuLy;
        Result = null;
        ErrorCode = null;
        ErrorMessage = null;
        Attempts = 1;
        CompletedAtUtc = null;
        ExpiresAtUtc = expiresAtUtc;
    }
}
