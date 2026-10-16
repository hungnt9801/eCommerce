using eCommerce.Messaging.DeadLetter;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Tracing;
using Volo.Abp.Uow;

namespace eCommerce.Marketplace;

/// <summary>
/// 🚨 **GHI VÀO HÀNG ĐỢI THƯ CHẾT** (`XC-002`) — điểm duy nhất biết cách đẩy một sự kiện vào DLQ.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **QUYẾT ĐỊNH QUAN TRỌNG NHẤT CỦA LỚP NÀY: HAI CHẾ ĐỘ UoW, VÀ VÌ SAO PHẢI CÓ CẢ HAI**
///
/// <list type="number">
///   <item><b><see cref="DayVaoDlqAsync"/> — DÙNG CHUNG UoW của nơi gọi.</b>
///     📌 Dùng khi ta **quyết định** đẩy DLQ (xung đột payload / hạ watermark) **và** vẫn muốn ghi vết khác
///     (nhật ký nhận đơn) **cùng một transaction**: hai thứ đó **phải** cùng sống hoặc cùng chết. Nếu DLQ ghi
///     được mà vết không, hoặc ngược lại, thì **không ai** đối chiếu được *"DLQ nói gì"* với *"nhật ký nói gì"*;</item>
///   <item><b><see cref="DayVaoDlqUoWRiengAsync"/> — MỞ UoW MỚI (`requiresNew`).</b>
///     🚨 Dùng khi **chính việc xử lý đã NÉM LỖI**. 📌 Lúc đó UoW của nơi gọi **sẽ bị rollback** ⇒ nếu ghi DLQ
///     vào **cùng** UoW thì **bản ghi DLQ CŨNG BỊ ROLLBACK** ⇒ DLQ **rỗng** đúng lúc cần nó nhất —
///     và ta lại quay về đúng tình trạng *"chỉ có log, rồi log bị xoay vòng"*.
///     🔑 Đây là **lỗi kinh điển** của mọi thiết kế DLQ: *"ghi DLQ trong cùng transaction với việc đã hỏng"*.
///     📌 Cùng họ với bài học `XC-002` đã ghi: `IDistributedEventBus` giao sự kiện **sau khi** UoW nơi phát
///     hoàn tất ⇒ handler **buộc** mở UoW riêng.</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** nếu **chính** `DayVaoDlqUoWRiengAsync` cũng hỏng (DB chết), thì sự kiện **mất**
/// và chỉ còn log. 📌 Không có cách nào tránh hoàn toàn khi **cùng** một DB vừa là nơi hỏng vừa là nơi ghi DLQ
/// — cách duy nhất chắc hơn là DLQ ở **hạ tầng khác** (hàng đợi thật, ví dụ RabbitMQ/Kafka). Ghi ở `sal-005` §18.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
///
/// 📌 **VÌ SAO `ITransientDependency` (không phải app service):** đây là **hạ tầng nội bộ**, không phải endpoint.
/// ⚠️ Nếu để thành app service thì ABP **tự sinh API controller** ⇒ mở ra Internet một đường ghi DLQ
/// **không ai review**.
/// </summary>
public class DeadLetterWriter : ITransientDependency
{
    private readonly IRepository<DeadLetterMessage, Guid> _dlqRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<DeadLetterWriter> _logger;

    public DeadLetterWriter(
        IRepository<DeadLetterMessage, Guid> dlqRepository,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<DeadLetterWriter> logger)
    {
        _dlqRepository = dlqRepository;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    /// <summary>Đẩy vào DLQ **trong UoW của nơi gọi** — xem chế độ (1) ở chú thích lớp.</summary>
    public Task DayVaoDlqAsync(
        string tenSuKien,
        string khoaNghiepVu,
        string payloadJson,
        string payloadHash,
        LyDoVaoDlq lyDo,
        string moTaLyDo,
        string? loi = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
        => GhiAsync(tenSuKien, khoaNghiepVu, payloadJson, payloadHash, lyDo, moTaLyDo, loi, tenantId, cancellationToken);

    /// <summary>
    /// 🚨 Đẩy vào DLQ **bằng UoW RIÊNG** — dùng khi việc xử lý **đã ném lỗi** (xem chế độ (2) ở chú thích lớp).
    /// 🔒 **KHÔNG ném** nếu ghi DLQ cũng hỏng: lỗi **gốc** mới là thứ nơi gọi cần biết, và một lỗi phụ
    /// sẽ **che** lỗi gốc. 📌 Thay vào đó ghi log `Error` — và đó là **toàn bộ** những gì còn lại trong ca xấu nhất.
    /// </summary>
    public async Task DayVaoDlqUoWRiengAsync(
        string tenSuKien,
        string khoaNghiepVu,
        string payloadJson,
        string payloadHash,
        LyDoVaoDlq lyDo,
        string moTaLyDo,
        string? loi = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

            await GhiAsync(tenSuKien, khoaNghiepVu, payloadJson, payloadHash, lyDo, moTaLyDo, loi, tenantId, cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // 🚨 Ca xấu nhất: KHÔNG ghi được DLQ. 📌 Ghi log ở mức `Critical` để nó **nổi** lên —
            // nhưng **KHÔNG** ném: lỗi gốc (đang được ném ở nơi gọi) mới là thứ cần điều tra trước.
            _logger.LogCritical(ex,
                "🚨 KHÔNG GHI ĐƯỢC DLQ (sự kiện SẼ MẤT): {TenSuKien} · khoá={Khoa} · lý do={LyDo}. [DLQ-GHI-HONG]",
                tenSuKien, khoaNghiepVu, lyDo);
        }
    }

    private async Task GhiAsync(
        string tenSuKien,
        string khoaNghiepVu,
        string payloadJson,
        string payloadHash,
        LyDoVaoDlq lyDo,
        string moTaLyDo,
        string? loi,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var ban = new DeadLetterMessage(
            _guidGenerator.Create(),
            tenSuKien,
            khoaNghiepVu,
            payloadJson,
            payloadHash,
            lyDo,
            moTaLyDo,
            DateTime.UtcNow,
            tenantId,
            loi)
        {
            CorrelationId = _correlationIdProvider.Get()
        };

        await _dlqRepository.InsertAsync(ban, autoSave: true, cancellationToken: cancellationToken);

        // 🔒 Neo ASCII để **tìm được trong log** (bài học `tools\README-probe.md`: console log làm hỏng dấu tiếng Việt).
        _logger.LogError(
            "DAY VAO DLQ: suKien={TenSuKien} · khoa={Khoa} · lyDo={LyDo}. [DLQ-DA-DAY]",
            tenSuKien, khoaNghiepVu, lyDo);
    }
}
