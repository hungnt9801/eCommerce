namespace eCommerce.Sales;

/// <summary>
/// Đầu vào tra **một** đơn sàn trong nhật ký nhận đơn — khoá nghiệp vụ `(channelCode, externalOrderId)`.
/// 📌 Khoá **không** phải `Guid`: người vận hành chỉ có **mã đơn của sàn** (in ra từ log của job `D12`),
/// không có `Id` nội bộ. Ép họ đi tìm `Guid` là biến một việc 1 phút thành việc phải mở DB.
/// </summary>
public class GetIntakeInput
{
    /// <summary>Mã kênh (`shopee`, `lazada`…).</summary>
    public string ChannelCode { get; set; } = default!;

    /// <summary>Mã đơn của sàn (`order_sn`).</summary>
    public string ExternalOrderId { get; set; } = default!;
}

/// <summary>
/// Đầu vào **xác nhận đã đối soát** (hạ cờ `GapSuspected`) — `SAL-005` **§6.1** `MarkReconciledAsync`.
/// </summary>
public class MarkReconciledInput
{
    public string ChannelCode { get; set; } = default!;

    public string ExternalOrderId { get; set; } = default!;

    /// <summary>
    /// 🔒 **BẮT BUỘC** — lý do đối soát (audit). Hạ cờ là **KHẲNG ĐỊNH** *"đã kiểm và dữ liệu khớp"*;
    /// không có lý do thì 6 tháng sau **không ai** biết vì sao đơn này từng bị nghi ngờ thiếu bản trung gian.
    /// 📌 Cùng khuôn `Customer.Chan` / `ReconRunMismatch.DanhDauDaXuLy`.
    /// </summary>
    public string Reason { get; set; } = default!;
}

/// <summary>
/// Trạng thái **watermark** của một đơn sàn — đọc cho vận hành (`SAL-005` §6.1/§6.2).
///
/// 🚨 **ĐỌC KỸ HAI MỐC THỜI GIAN KHÁC NHAU** (§10 ghi chú 3 của `SAL-005`):
/// <list type="bullet">
///   <item><see cref="LastReceivedAtUtc"/> = *"hệ thống **nhận** sự kiện gần nhất lúc nào"* — **kể cả** sự kiện
///     bị bỏ qua (trùng/đến muộn);</item>
///   <item><see cref="AppliedSourceUpdatedAtUtc"/> = *"bản **đang có hiệu lực** là bản nào của sàn"*.</item>
/// </list>
/// 📌 Một đơn có thể **nhận** sự kiện gần đây mà **không áp dụng** gì (nhánh B/C) ⇒ UI **phải** hiển thị **cả hai**,
/// nếu không người vận hành sẽ tưởng đơn **đã** cập nhật.
/// </summary>
public class MarketplaceIntakeDto
{
    public string ChannelCode { get; set; } = default!;

    public string ExternalOrderId { get; set; } = default!;

    /// <summary>Đơn nội bộ đã tạo (soft reference — `null` nếu chưa tạo được).</summary>
    public Guid? OrderId { get; set; }

    /// <summary>⭐ Watermark đã áp = epoch **giây** của `update_time` sàn. `null` = chưa từng áp bản có watermark.</summary>
    public long? AppliedSourceVersion { get; set; }

    /// <summary>Mốc `update_time` của bản **đã áp** (người đọc hiểu được).</summary>
    public DateTime? AppliedSourceUpdatedAtUtc { get; set; }

    /// <summary>`PayloadHash` của bản **đã áp** (phân biệt "trùng" với "xung đột").</summary>
    public string? AppliedPayloadHash { get; set; }

    /// <summary>🚩 Cờ **nghi ngờ thiếu bản trung gian** — `true` ⇒ đơn đang chờ người đối soát.</summary>
    public bool GapSuspected { get; set; }

    /// <summary>Thời điểm **nhận** sự kiện gần nhất (kể cả bị bỏ qua).</summary>
    public DateTime LastReceivedAtUtc { get; set; }

    /// <summary>Lần cuối được xác nhận đã đối soát (`null` = chưa).</summary>
    public DateTime? LastReconciledAtUtc { get; set; }

    public string? CorrelationId { get; set; }

    /// <summary>Ghi chú bỏ qua / ghi chú đối soát gần nhất (audit ngắn).</summary>
    public string? SkipReason { get; set; }
}

/// <summary>
/// **Cổng vận hành của đơn sàn** (`SAL-005` §6.1) — đọc watermark và **hạ cờ** `GapSuspected`.
///
/// 🚨 **VÌ SAO PHẢI CÓ CỔNG NÀY (đo được ở `S6`):** job `D12` **cố ý KHÔNG** tự hạ cờ (`SAL-005` §19.2) —
/// hạ cờ là **khẳng định** *"đã kiểm và dữ liệu khớp"*, mà job **không gọi được sàn**. Nhưng trước lát cắt
/// này `DanhDauDaDoiSoat` **không có người gọi nào** ⇒ cờ `GapSuspected` chỉ hạ được bằng **SQL tay**.
/// 🔑 Nghĩa là vế *"để cờ không tồn đọng"* của `S6` mới chỉ đúng **một nửa**: job **phát hiện** được,
/// nhưng **không ai xử lý được** bằng đường chính thức. Cổng này đóng nốt nửa còn lại.
///
/// 🔒 **KHÔNG có** phương thức sửa watermark (BR-008): watermark **chỉ** tiến qua luật 4 nhánh. 📌 Một API
/// cho phép đặt watermark bằng tay là mở đường cho **ghi đè trạng thái mới hơn** — đúng thứ `D6` cấm.
/// </summary>
public interface IMarketplaceIntakeAppService
{
    /// <summary>Đọc trạng thái watermark + cờ của **một** đơn sàn. Quyền: `…MarketplaceIntake.View`.</summary>
    Task<MarketplaceIntakeDto> GetIntakeAsync(GetIntakeInput input);

    /// <summary>
    /// Xác nhận **đã đối soát** ⇒ hạ cờ `GapSuspected` (giữ nguyên watermark). Quyền: `…MarketplaceIntake.Reprocess`.
    /// 🔒 **Bắt buộc lý do** (audit). `404` nếu chưa có đơn sàn này trong nhật ký.
    /// </summary>
    Task<MarketplaceIntakeDto> MarkReconciledAsync(MarkReconciledInput input);
}
