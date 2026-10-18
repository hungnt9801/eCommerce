using eCommerce.Orders;
using eCommerce.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Marketplace;

/// <summary>
/// **CỔNG VẬN HÀNH CỦA ĐƠN SÀN** (`SAL-005` **§6.1** · đóng nốt vế còn thiếu của `S6`):
/// đọc watermark + **hạ cờ `GapSuspected`** sau khi người vận hành đã đối soát **thật**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **VÌ SAO PHẢI CÓ (đo được ở `S6`):** job `D12` **cố ý không** tự hạ cờ (`SAL-005` §19.2) — hạ cờ là
/// **khẳng định** *"đã kiểm và dữ liệu khớp"*, mà job **không gọi được sàn**. Nhưng khi kiểm lại thì
/// `DanhDauDaDoiSoat` **không có NGƯỜI GỌI NÀO** (chỉ có trong chú thích + unit test) ⇒ cờ chỉ hạ được bằng
/// **SQL tay**. 🔑 Nghĩa là vế *"để cờ `GapSuspected` không tồn đọng"* của `S6` mới đúng **một nửa**:
/// job **phát hiện** được, nhưng **không ai xử lý được** bằng đường chính thức. Lớp này đóng nốt nửa kia.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔐 **HAI QUYỀN, HAI VIỆC** (`XC-004` D2 — permission phải **được định nghĩa** ở provider, nếu không thì
/// `403` cho **mọi** người kể cả admin):
/// <list type="bullet">
///   <item><c>…MarketplaceIntake.View</c> — đọc trạng thái watermark;</item>
///   <item><c>…MarketplaceIntake.Reprocess</c> — **hạ cờ** (thao tác **xoá một cảnh báo** ⇒ cần quyền riêng).</item>
/// </list>
/// 📌 `[Authorize]` ở **cấp CLASS** (quy ước #4): nếu chỉ gắn ở vài method thì các method còn lại thành API
/// **công khai** — lỗi Phase 0 đã xảy ra thật với `GetShopsAsync`.
///
/// 🔒 **TUYỆT ĐỐI KHÔNG có phương thức sửa watermark** (BR-008): watermark **chỉ** tiến qua luật 4 nhánh
/// (`ApDungWatermark`). 📌 Một API đặt watermark bằng tay là mở đường cho **ghi đè trạng thái mới hơn** —
/// đúng thứ `D6` cấm. Vì vậy lớp này **chỉ** hạ cờ, **không** đụng `AppliedSourceVersion`.
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** chưa có **danh sách** đơn bị đánh cờ (lọc/phân trang) và chưa có API đọc
/// **nhật ký sự kiện** (`SAL-005` §6.1 `GetIntakesAsync`/`GetEventLogAsync`) ⇒ thuộc `S7` (UI vận hành).
/// 📌 Đường dùng hiện tại: log của job `D12` in **khoá nghiệp vụ** của tối đa 20 đơn cũ nhất
/// (`[D12-GAP-TON-DONG]`) ⇒ người vận hành tra **đúng** đơn đó qua `GET`/`POST` dưới đây.
/// </summary>
[Authorize]
public class MarketplaceIntakeAppService : ApplicationService, IMarketplaceIntakeAppService
{
    private readonly IRepository<MarketplaceOrderIntake, Guid> _repository;
    private readonly ILogger<MarketplaceIntakeAppService> _logger;

    public MarketplaceIntakeAppService(
        IRepository<MarketplaceOrderIntake, Guid> repository,
        ILogger<MarketplaceIntakeAppService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.MarketplaceIntake.View)]
    public async Task<MarketplaceIntakeDto> GetIntakeAsync(GetIntakeInput input)
    {
        KiemKhoa(input?.ChannelCode, input?.ExternalOrderId);

        var ban = await TimAsync(input!.ChannelCode, input.ExternalOrderId);

        if (ban is null)
        {
            // 📌 `404` (không phải `200` + rỗng): "đơn sàn này **chưa từng** được nhận" là một **câu trả lời
            // khác** với "đã nhận và đang chờ đối soát". Gộp hai thứ lại là đúng loại nhoè thông tin mà
            // `XC-004` BR-008 cấm (và làm người vận hành tưởng đơn đã mất).
            throw new EntityNotFoundException(
                typeof(MarketplaceOrderIntake), $"{input.ChannelCode}/{input.ExternalOrderId}");
        }

        return Chuyen(ban);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.MarketplaceIntake.Reprocess)]
    public async Task<MarketplaceIntakeDto> MarkReconciledAsync(MarkReconciledInput input)
    {
        KiemKhoa(input?.ChannelCode, input?.ExternalOrderId);

        var ban = await TimAsync(input!.ChannelCode, input.ExternalOrderId);

        if (ban is null)
        {
            throw new EntityNotFoundException(
                typeof(MarketplaceOrderIntake), $"{input.ChannelCode}/{input.ExternalOrderId}");
        }

        // 🔒 Lý do **BẮT BUỘC** — kiểm Ở ĐÂY để trả **mã nghiệp vụ** (`0005`) thay vì `ArgumentException`
        // thô (entity cũng ném, nhưng message của nó là thông điệp **lập trình viên**, không phải cho API).
        // 📌 Đặt SAU bước tìm: đơn không tồn tại phải là `404` — *"thiếu lý do"* và *"đơn không có"* là **hai**
        // lỗi khác nhau, gộp chúng lại sẽ khiến người gọi sửa **sai chỗ**.
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new Volo.Abp.BusinessException(MarketplaceErrorCodes.ThieuLyDoDoiSoat)
                .WithData("channelCode", input.ChannelCode)
                .WithData("externalOrderId", input.ExternalOrderId)
                .WithData("lyDo", "Hạ cờ `GapSuspected` là KHẲNG ĐỊNH đã đối soát khớp ⇒ BẮT BUỘC có lý do (audit).");
        }

        // 🔒 Bất biến nằm ở **entity** (không phải ở đây): `DanhDauDaDoiSoat` ghi `LastReconciledAtUtc` +
        // hạ cờ + ghi vết `SkipReason`, và **không** đụng `AppliedSourceVersion` (BR-008).
        ban.DanhDauDaDoiSoat(DateTime.UtcNow, input.Reason);

        await _repository.UpdateAsync(ban, autoSave: true);

        // 🔒 Neo ASCII để tìm được trong log (console logger làm hỏng dấu tiếng Việt — `README-probe` I4).
        _logger.LogInformation(
            "DA DOI SOAT don san {ChannelCode}/{ExternalOrderId}: ha co GapSuspected (watermark GIU NGUYEN = {PhienBan}). [MP-DA-DOI-SOAT]",
            ban.ChannelCode, ban.ExternalOrderId, ban.AppliedSourceVersion);

        return Chuyen(ban);
    }

    private async Task<MarketplaceOrderIntake?> TimAsync(string channelCode, string externalOrderId)
        => await _repository.FirstOrDefaultAsync(
            x => x.ChannelCode == channelCode && x.ExternalOrderId == externalOrderId);

    private static void KiemKhoa(string? channelCode, string? externalOrderId)
    {
        if (string.IsNullOrWhiteSpace(channelCode) || string.IsNullOrWhiteSpace(externalOrderId))
        {
            throw new Volo.Abp.BusinessException(MarketplaceErrorCodes.ThieuThongTinBatBuoc)
                .WithData("channelCode", channelCode)
                .WithData("externalOrderId", externalOrderId)
                .WithData("lyDo", "Thiếu khoá nghiệp vụ (kênh + mã đơn sàn) ⇒ không tra được đơn nào.");
        }
    }

    /// <summary>
    /// Chuyển entity → DTO. 📌 Cố ý **không** trả `PayloadHash` (bản **đến gần nhất**) — chỉ trả
    /// `AppliedPayloadHash` (bản **đang hiệu lực**): trả cả hai mà không giải thích sẽ khiến người đọc
    /// tưởng có **hai** mốc watermark (đúng bẫy `sal-005` §17.3 #4).
    /// </summary>
    private static MarketplaceIntakeDto Chuyen(MarketplaceOrderIntake ban) => new()
    {
        ChannelCode = ban.ChannelCode,
        ExternalOrderId = ban.ExternalOrderId,
        OrderId = ban.OrderId,
        AppliedSourceVersion = ban.AppliedSourceVersion,
        AppliedSourceUpdatedAtUtc = ban.AppliedSourceUpdatedAtUtc,
        AppliedPayloadHash = ban.AppliedPayloadHash,
        GapSuspected = ban.GapSuspected,
        LastReceivedAtUtc = ban.ReceivedAtUtc,
        LastReconciledAtUtc = ban.LastReconciledAtUtc,
        CorrelationId = ban.CorrelationId,
        SkipReason = ban.SkipReason
    };
}
