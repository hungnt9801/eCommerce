using eCommerce.ChannelHub.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Tracing;
using Volo.Abp.Uow;

namespace eCommerce.Marketplace;

/// <summary>
/// 🚨 **GHI NHẬT KÝ SỰ KIỆN ĐƠN SÀN** (`SAL-005` D7) — điểm **duy nhất** biết cách ghi một dòng
/// `SalMarketplaceEventLogs`.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO DÙNG **UoW RIÊNG** (`requiresNew`) CHO MỌI LẦN GHI — không phải chi tiết kỹ thuật:**
///
/// Nhật ký sự kiện phải **sống sót** đúng trong hai ca mà transaction chính **chết**:
/// <list type="number">
///   <item>**nhánh BỎ QUA** (`Late`/`Duplicate`) — transaction chính có thể bị rollback vì lý do khác
///     (ví dụ bước sau ném lỗi) ⇒ nếu ghi chung thì **mất dấu vết của ca bình thường nhất**;
///   <item>**nhánh THẤT BẠI** (`Failed`) — transaction chính **chắc chắn** rollback ⇒ ghi chung nghĩa là
///     dòng `Failed` **cũng bị rollback** ⇒ nhật ký **rỗng đúng lúc cần nó nhất**. 📌 Đây đúng lỗi kinh điển
///     đã ghi ở `DeadLetterWriter` (`sal-005` §18.4).</item>
/// </list>
/// 🔒 Đổi lại: một dòng nhật ký có thể tồn tại trong khi thao tác nghiệp vụ **đã rollback**. 📌 Điều đó
/// **đúng** với ngữ nghĩa *"sự kiện ĐÃ ĐƯỢC NHẬN"* — nhật ký ghi **việc nhận**, không ghi **việc thành công**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔒 **IDEMPOTENT THEO KHOÁ TỰ NHIÊN** (không ném khi trùng):
/// hai luồng cùng ghi **cùng một sự kiện** (dual-path webhook+pull) ⇒ một dòng thắng, luồng kia **im lặng
/// bỏ qua** — vì *"đã có dòng cho sự kiện này"* **chính là** kết quả mong muốn.
/// 🚨 **Cách nhận biết trùng: đọc lại DB**, **KHÔNG** đoán theo chuỗi thông điệp lỗi
/// (`23505`/`duplicate key`) — chuỗi đó phụ thuộc provider/phiên bản, và **đoán sai** ở đây sẽ biến một
/// lỗi thật thành *"trùng"* (im lặng) hoặc biến trùng thành **lỗi** (ồn ào vô ích).
///
/// 📌 **VÌ SAO `ITransientDependency` (không phải app service):** hạ tầng **nội bộ**. ⚠️ Nếu là app service
/// thì ABP tự sinh API controller ⇒ mở ra Internet một đường ghi nhật ký **không ai review**.
/// </summary>
public class MarketplaceEventLogWriter : ITransientDependency
{
    private readonly IRepository<Orders.MarketplaceEventLog, Guid> _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<MarketplaceEventLogWriter> _logger;

    public MarketplaceEventLogWriter(
        IRepository<Orders.MarketplaceEventLog, Guid> repository,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<MarketplaceEventLogWriter> logger)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    /// <summary>
    /// Ghi **đúng một** dòng cho sự kiện vừa nhận. 🚨 **NÉM** nếu ghi thất bại (trừ ca trùng) —
    /// nhật ký thiếu dòng là vi phạm bất biến 5 của `SAL-005` (§8.1), nên nó **phải ồn ào**, không được nuốt.
    /// </summary>
    public async Task GhiAsync(
        ChannelOrderPulledEto suKien,
        Orders.MarketplaceIntakeOutcome ketCuc,
        string payloadHash,
        long? sourceVersion,
        string? failureCode = null,
        string? failureMessage = null,
        CancellationToken cancellationToken = default)
    {
        var ban = TaoBan(suKien, ketCuc, payloadHash, sourceVersion, failureCode, failureMessage);

        try
        {
            await ChenTrongUoWRiengAsync(ban, cancellationToken);
        }
        catch (Exception ex)
        {
            // 🔑 Phân biệt TRÙNG với LỖI **bằng bằng chứng** (đọc lại DB), không bằng chuỗi lỗi.
            if (await DaCoDongAsync(ban, cancellationToken))
            {
                _logger.LogDebug(
                    "Nhật ký sự kiện đã có dòng cho {ChannelCode}/{ExternalOrderId} v={PhienBan} " +
                    "(kết cục {KetCuc}) — luồng khác đã ghi trước. [MP-EVENTLOG-TRUNG]",
                    ban.ChannelCode, ban.ExternalOrderId, sourceVersion, ketCuc);
                return;
            }

            _logger.LogCritical(ex,
                "🚨 KHÔNG GHI ĐƯỢC NHẬT KÝ SỰ KIỆN cho {ChannelCode}/{ExternalOrderId} v={PhienBan} " +
                "(kết cục {KetCuc}) ⇒ sự kiện này KHÔNG có dấu vết. [MP-EVENTLOG-GHI-HONG]",
                ban.ChannelCode, ban.ExternalOrderId, sourceVersion, ketCuc);

            throw;
        }
    }

    /// <summary>
    /// Ghi dòng `Failed` **best-effort** — dùng ở nhánh **đã có lỗi khác**: 🔒 **KHÔNG ném** (một lỗi phụ
    /// sẽ **che** lỗi gốc — cùng quyết định với `DeadLetterWriter.DayVaoDlqUoWRiengAsync`).
    /// </summary>
    public async Task GhiThatBaiAsync(
        ChannelOrderPulledEto? suKien,
        string channelCode,
        string externalOrderId,
        string payloadHash,
        long? sourceVersion,
        string failureCode,
        string failureMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ban = suKien is null
                ? new Orders.MarketplaceEventLog(
                    _guidGenerator.Create(),
                    channelCode,
                    externalOrderId,
                    payloadHash,
                    Orders.MarketplaceIntakeOutcome.Failed,
                    DateTime.UtcNow,
                    sourceVersion,
                    failureCode: failureCode,
                    failureMessage: failureMessage)
                : TaoBan(suKien, Orders.MarketplaceIntakeOutcome.Failed, payloadHash, sourceVersion, failureCode, failureMessage);

            await ChenTrongUoWRiengAsync(ban, cancellationToken);
        }
        catch (Exception ex)
        {
            if (suKien is not null && await DaCoDongAsync(
                    TaoBan(suKien, Orders.MarketplaceIntakeOutcome.Failed, payloadHash, sourceVersion, failureCode, failureMessage),
                    cancellationToken))
            {
                return;
            }

            _logger.LogCritical(ex,
                "🚨 KHÔNG GHI ĐƯỢC NHẬT KÝ THẤT BẠI cho {ChannelCode}/{ExternalOrderId} ⇒ chỉ còn log. [MP-EVENTLOG-THATBAI-HONG]",
                channelCode, externalOrderId);
        }
    }

    private Orders.MarketplaceEventLog TaoBan(
        ChannelOrderPulledEto suKien,
        Orders.MarketplaceIntakeOutcome ketCuc,
        string payloadHash,
        long? sourceVersion,
        string? failureCode,
        string? failureMessage) => new(
            _guidGenerator.Create(),
            suKien.ChannelCode,
            suKien.ExternalOrderId,
            payloadHash,
            ketCuc,
            DateTime.UtcNow,
            sourceVersion,
            suKien.SourceUpdatedAtUtc,
            channelShopId: suKien.ChannelShopId,
            tenantId: suKien.TenantId,
            eventKind: suKien.EventKind,
            marketplaceStatusRaw: suKien.MarketplaceStatusRaw,
            isFromWebhook: suKien.IsFromWebhook,
            correlationId: suKien.CorrelationId ?? _correlationIdProvider.Get(),
            failureCode: failureCode,
            failureMessage: failureMessage);

    private async Task ChenTrongUoWRiengAsync(Orders.MarketplaceEventLog ban, CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        await _repository.InsertAsync(ban, autoSave: true, cancellationToken: cancellationToken);

        await uow.CompleteAsync(cancellationToken);

        // 🔒 Neo ASCII để **tìm được trong log** (console logger làm hỏng dấu tiếng Việt — `README-probe` I4).
        _logger.LogInformation(
            "NHAT KY SU KIEN: {ChannelCode}/{ExternalOrderId} v={PhienBan} ketCuc={KetCuc} nguon={Nguon}. [MP-EVENTLOG-DA-GHI]",
            ban.ChannelCode, ban.ExternalOrderId, ban.SourceVersion, ban.Outcome,
            ban.IsFromWebhook ? "webhook" : "pull");
    }

    /// <summary>Đọc lại theo **khoá tự nhiên** của bảng — xem giải thích ở chú thích lớp (đoán theo chuỗi lỗi là sai).
    /// 📌 Khoá **có cả `Outcome`** (không chỉ sự kiện): nhờ vậy *"lần giao lại đầu tiên bị bỏ qua"* ghi được
    /// một dòng `Duplicate`, trong khi *"cùng kết cục ghi hai lần"* vẫn bị chặn.</summary>
    private async Task<bool> DaCoDongAsync(Orders.MarketplaceEventLog ban, CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        try
        {
            var co = await _repository.AnyAsync(
                x => x.ChannelCode == ban.ChannelCode
                     && x.ExternalOrderId == ban.ExternalOrderId
                     && x.SourceVersion == ban.SourceVersion
                     && x.PayloadHash == ban.PayloadHash
                     && x.Outcome == ban.Outcome,
                cancellationToken);

            await uow.CompleteAsync(cancellationToken);

            return co;
        }
        catch (Exception ex)
        {
            // 📌 Không đọc lại được ⇒ **không** được kết luận "trùng" (kết luận đó sẽ nuốt mất lỗi thật).
            _logger.LogWarning(ex,
                "Không đọc lại được nhật ký sự kiện để phân biệt TRÙNG/LỖI cho {ChannelCode}/{ExternalOrderId}.",
                ban.ChannelCode, ban.ExternalOrderId);
            return false;
        }
    }
}
