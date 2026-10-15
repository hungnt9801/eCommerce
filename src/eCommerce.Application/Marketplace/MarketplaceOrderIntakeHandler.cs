using System.Text.Json;
using eCommerce.ChannelHub.Events;
using eCommerce.Messaging.Idempotency;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Tracing;
using Volo.Abp.Uow;

namespace eCommerce.Marketplace;

/// <summary>
/// **NƠI NHẬN ĐƠN TỪ SÀN** (`SAL-005`) — handler cho `ChannelOrderPulledEto`.
///
/// 🚨🚨 ĐÂY LÀ MẢNH CÒN THIẾU CỦA **LỖI #22** (lỗi nghiêm trọng nhất dự án).
/// Trước đó `ChannelShopSyncer` phát `ChannelOrderPulledEto` cho từng đơn mà **không ai nhận**, rồi vẫn
/// **tiến con trỏ đồng bộ** ⇒ sàn không gửi lại ⇒ **đơn mất vĩnh viễn**, và **không có lỗi nào được ghi**.
/// Lượt trước đã **chặn** sự mất mát (fail-closed: chưa có nơi nhận ⇒ không tiến con trỏ).
/// **Lượt này xây nơi nhận thật** ⇒ cổng fail-closed **tự mở**, không cần bật cờ nào.
///
/// 🎯 NGUYÊN TẮC (theo `SAL-005` D1): `ChannelHub` **không** tự tạo đơn — nó chỉ phát sự kiện.
/// Tầng **app** (ở đây) mới là nơi sở hữu `Order`. Nhờ vậy ChannelHub tách thành microservice được về sau.
///
/// 🔒 CHỐNG TRÙNG (D4): dùng **khoá idempotency ở mức ĐƠN** `"{channelCode}:order" / "{externalOrderId}"`.
/// Đường **pull** hôm nay và đường **webhook** (GĐ4) sẽ dùng **cùng** khoá này ⇒ cùng một đơn đến hai
/// đường vẫn chỉ tạo **một** đơn. Ngoài ra còn **hai hàng rào độc lập**:
///   1. unique index `(TenantId, ChannelCode, ExternalOrderId)` trên `OrdOrders` (`NULLS NOT DISTINCT`),
///   2. unique index `(TenantId, ChannelCode, ExternalOrderId)` trên bảng nhật ký nhận đơn.
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT CỦA LƯỢT NÀY (ghi rõ, không giả vờ):** xử lý **cập nhật** đơn sàn (so watermark
/// theo `update_time` của sàn — D5/D6) **CHƯA** làm. Hiện theo hướng **tạo-một-lần**: đơn đã nhận thì
/// lần sau **không** sửa. Hệ quả: **không mất đơn**, nhưng đơn có thể **cũ** so với sàn cho tới khi làm
/// phần watermark. Đây là đánh đổi **cố ý** (an toàn trước, đầy đủ sau) và **được ghi lại**, không im lặng.
///
/// 📌 Hệ quả lên cổng fail-closed: khi handler này được đăng ký, `EventCoverageProbe` thấy
/// `ChannelOrderPulledEto` **có người nhận** ⇒ `ChannelShopSyncer` **tiến con trỏ** trở lại.
/// ⚠️ Điều đó chỉ **an toàn** vì handler này **ghi đơn xuống DB** trước khi trả về thành công.
/// </summary>
public class MarketplaceOrderIntakeHandler : IDistributedEventHandler<ChannelOrderPulledEto>, Volo.Abp.DependencyInjection.ITransientDependency
{
    private readonly IRepository<Orders.Order, Guid> _orderRepository;
    private readonly IRepository<Orders.MarketplaceOrderIntake, Guid> _intakeRepository;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<MarketplaceOrderIntakeHandler> _logger;

    public MarketplaceOrderIntakeHandler(
        IRepository<Orders.Order, Guid> orderRepository,
        IRepository<Orders.MarketplaceOrderIntake, Guid> intakeRepository,
        IIdempotencyGuard idempotencyGuard,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<MarketplaceOrderIntakeHandler> logger)
    {
        _orderRepository = orderRepository;
        _intakeRepository = intakeRepository;
        _idempotencyGuard = idempotencyGuard;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public async Task HandleEventAsync(ChannelOrderPulledEto eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (string.IsNullOrWhiteSpace(eventData.ChannelCode) || string.IsNullOrWhiteSpace(eventData.ExternalOrderId))
        {
            // Không có khoá nghiệp vụ ⇒ KHÔNG được đoán. Ném lỗi để nó ồn ào (im lặng ở đây = mất đơn).
            throw new BusinessException(MarketplaceErrorCodes.ThieuThongTinBatBuoc)
                .WithData("channelCode", eventData.ChannelCode)
                .WithData("externalOrderId", eventData.ExternalOrderId);
        }

        // Payload chuẩn hoá để (a) băm chống trùng, (b) phát hiện "cùng đơn nhưng nội dung khác".
        var payloadJson = JsonSerializer.Serialize(eventData);
        var payloadHash = CanonicalJsonHasher.Bam(payloadJson);

        var khoa = IdempotencyKey.Cua($"{eventData.ChannelCode}:order", eventData.ExternalOrderId);

        // 🚨🚨 BẮT BUỘC MỞ **UoW RIÊNG** — BÀI HỌC ĐO ĐƯỢC 15/10/2026 (không phải suy luận).
        //
        // ABP giao sự kiện của `IDistributedEventBus` (transport **in-process**, `XC-002` Q1) **SAU KHI
        // UoW của NƠI PHÁT hoàn tất** — để người nhận không đọc phải dữ liệu chưa commit. Hệ quả:
        // `eCommerceDbContext` của UoW đó **ĐÃ BỊ DISPOSE**. Nếu handler dùng repository ngay thì:
        //
        //     System.ObjectDisposedException: Cannot access a disposed context instance.
        //     Object name: 'eCommerceDbContext'.
        //        at eCommerce.Messaging.EntityFrameworkCore.EfCoreIdempotencyStore.TimAsync(...)
        //        at eCommerce.Messaging.Idempotency.IdempotencyGuard.ThucThiAsync(...)
        //        at eCommerce.Marketplace.MarketplaceOrderIntakeHandler.HandleEventAsync(...)
        //
        // ⇒ Đo được thật khi chạy Host (không lỗi biên dịch, **không** lỗi khi build/test).
        // Cách sửa ĐÚNG: mở UoW **mới** (`requiresNew: true`) cho toàn bộ phần ghi của handler.
        // ⚠️ **KHÔNG** dùng `[UnitOfWork]`/`IUnitOfWorkEnabled` trần: interceptor của ABP mặc định
        //    `requiresNew = false` ⇒ nó **tái dùng** UoW đã dispose ⇒ vẫn lỗi y nguyên.
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        // `IDistributedEventHandler<T>` (ABP 10.7) không truyền `CancellationToken` vào handler ⇒ dùng token
        // mặc định cho cả lần gọi guard và lần ghi vết, để hai chỗ **cùng một token**.
        var ct = CancellationToken.None;

        var ketQua = await _idempotencyGuard.ThucThiAsync(
            khoa,
            payloadJson,
            token => NhanDonAsync(eventData, payloadHash, token),
            ct);

        switch (ketQua.Kind)
        {
            case IdempotencyOutcomeKind.DaThucThi:
                _logger.LogInformation(
                    "Đã nhận đơn sàn {ChannelCode}/{ExternalOrderId} ⇒ tạo đơn nội bộ {OrderId}.",
                    eventData.ChannelCode, eventData.ExternalOrderId, ketQua.Result);
                break;

            case IdempotencyOutcomeKind.TraVeKetQuaCu:
                // Sàn gửi lại / worker pull lại cùng đơn: KHÔNG tạo đơn thứ hai — đúng thiết kế.
                _logger.LogInformation(
                    "Đơn sàn {ChannelCode}/{ExternalOrderId} đã xử lý trước đó (đơn nội bộ {OrderId}) — bỏ qua, không tạo trùng.",
                    eventData.ChannelCode, eventData.ExternalOrderId, ketQua.Result);
                break;

            case IdempotencyOutcomeKind.XungDotPayload:
                // 🚨 Cùng khoá đơn nhưng nội dung KHÁC ⇒ **KHÔNG đoán bản nào đúng** (`SAL-005` D6):
                //    KHÔNG ghi đè, GIỮ bản đã nhận đầu tiên (không mất dữ liệu).
                //
                // ⚠️ QUYẾT ĐỊNH CÓ CHỦ Ý — **KHÔNG ném lỗi** ở đây (khác D6 nguyên bản "đẩy DLQ"):
                //    • Ném lỗi ⇒ `PublishAsync` ném ⇒ `ChannelShopSyncer` **KHÔNG tiến con trỏ** ⇒ sàn gửi lại
                //      đúng đơn đó ở nhịp sau ⇒ **lặp vô hạn** và **CHẶN CẢ KÊNH**: mọi đơn phía sau cũng không
                //      vào được. Một đơn nghi vấn làm **sập luồng bán hàng** — hỏng nặng hơn nhiều.
                //      *(Đã đo được thật 15/10/2026: con trỏ kẹt ở NULL qua nhiều nhịp.)*
                //    • Nay: GIỮ bản đầu (không ghi đè ⇒ **không mất dữ liệu**, và **tất định**: không instance
                //      nào ghi đè, nên kết quả không phụ thuộc thứ tự đến), **GHI VẾT** vào nhật ký nhận đơn
                //      (`SkipReason`) + log **Error** ⇒ **không im lặng**.
                //    • 📌 Khi `XC-002` có **DLQ** (đã cố ý hoãn), nhánh này PHẢI chuyển sang **đẩy DLQ**.
                _logger.LogError(
                    "🚨 XUNG ĐỘT PAYLOAD cho đơn sàn {ChannelCode}/{ExternalOrderId}: cùng khoá nhưng nội dung khác. " +
                    "GIỮ NGUYÊN bản đã nhận đầu tiên (không ghi đè, không đoán) và GHI VẾT để xử lý thủ công. " +
                    "KHÔNG chặn kênh — nếu chặn thì một đơn nghi vấn sẽ làm kẹt toàn bộ đơn phía sau.",
                    eventData.ChannelCode, eventData.ExternalOrderId);

                await GhiVetXungDotPayloadAsync(eventData, ct);
                break;

            case IdempotencyOutcomeKind.DangXuLy:
                _logger.LogWarning(
                    "Đơn sàn {ChannelCode}/{ExternalOrderId} ĐANG được xử lý ở nơi khác ⇒ bỏ lượt này; " +
                    "sàn/worker sẽ gửi lại (con trỏ đồng bộ chỉ tiến khi xử lý xong).",
                    eventData.ChannelCode, eventData.ExternalOrderId);
                break;
        }

        // Chốt transaction CỦA HANDLER (mọi nhánh đều ghi vết rồi mới tới đây ⇒ KHÔNG rollback oan).
        await uow.CompleteAsync();
    }

    /// <summary>
    /// Ghi vết xung đột payload vào **nhật ký nhận đơn** — để ca "cùng đơn, khác nội dung" **không bao giờ
    /// im lặng**. Không ném lỗi (lý do ở nhánh gọi).
    /// </summary>
    private async Task GhiVetXungDotPayloadAsync(ChannelOrderPulledEto suKien, CancellationToken cancellationToken)
    {
        var daNhan = await _intakeRepository.FirstOrDefaultAsync(
            x => x.ChannelCode == suKien.ChannelCode && x.ExternalOrderId == suKien.ExternalOrderId,
            cancellationToken: cancellationToken);

        if (daNhan is null)
        {
            // Bất thường: có bản ghi idempotency mà không có nhật ký nhận đơn. Ghi log, KHÔNG nuốt.
            _logger.LogWarning(
                "Xung đột payload cho {ChannelCode}/{ExternalOrderId} nhưng KHÔNG tìm thấy nhật ký nhận đơn để ghi vết.",
                suKien.ChannelCode, suKien.ExternalOrderId);
            return;
        }

        daNhan.DanhDauBoQua(
            "XUNG ĐỘT PAYLOAD: cùng khoá đơn nhưng nội dung khác bản đã nhận. Giữ bản đầu (không đoán bản nào đúng). " +
            "Cần xử lý thủ công; khi có DLQ (XC-002) thì chuyển sang đẩy DLQ.");

        await _intakeRepository.UpdateAsync(daNhan, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>Phần xử lý thật — chạy **bên trong** khoá idempotency (đã được tuần tự hoá).</summary>
    private async Task<string?> NhanDonAsync(
        ChannelOrderPulledEto suKien,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        // Hàng rào thứ ba: đã có nhật ký nhận cho đơn này chưa (kể cả khi bản ghi idempotency đã hết hạn).
        var daNhan = await _intakeRepository.FirstOrDefaultAsync(
            x => x.ChannelCode == suKien.ChannelCode && x.ExternalOrderId == suKien.ExternalOrderId,
            cancellationToken: cancellationToken);

        if (daNhan is not null)
        {
            if (!string.Equals(daNhan.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                // Cùng đơn, khác nội dung, mà không có watermark ⇒ KHÔNG đoán (D6). Ghi vết rồi thoát.
                daNhan.DanhDauBoQua(
                    $"Nội dung khác bản đã nhận lúc {daNhan.ReceivedAtUtc:O} nhưng CHƯA hỗ trợ cập nhật theo watermark " +
                    "(SAL-005 D5/D6) ⇒ giữ nguyên bản cũ, cần xử lý thủ công.");
                await _intakeRepository.UpdateAsync(daNhan, autoSave: true, cancellationToken: cancellationToken);

                _logger.LogWarning(
                    "🚨 Đơn sàn {ChannelCode}/{ExternalOrderId} đã nhận trước đó với NỘI DUNG KHÁC và CHƯA có xử lý " +
                    "cập nhật ⇒ giữ nguyên bản cũ (không mất dữ liệu) và GHI VẾT để xử lý thủ công.",
                    suKien.ChannelCode, suKien.ExternalOrderId);
            }

            return daNhan.OrderId?.ToString();
        }

        var order = MarketplaceOrderFactory.TaoOrder(suKien, _guidGenerator.Create);

        await _orderRepository.InsertAsync(order, autoSave: true, cancellationToken: cancellationToken);

        var nhatKy = new Orders.MarketplaceOrderIntake(
            _guidGenerator.Create(),
            suKien.ChannelCode,
            suKien.ExternalShopId,
            suKien.ExternalOrderId,
            payloadHash,
            DateTime.UtcNow,
            suKien.PlacedAtUtc,
            suKien.TenantId)
        {
            CorrelationId = _correlationIdProvider.Get()
        };

        nhatKy.GanOrder(order.Id);

        await _intakeRepository.InsertAsync(nhatKy, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Đã nhận và lưu đơn sàn {ChannelCode}/{ExternalOrderId}: đơn nội bộ {SoDon}, {SoDong} dòng, " +
            "tổng {GrandTotal} {Currency}.",
            suKien.ChannelCode, suKien.ExternalOrderId, order.Number, suKien.Lines.Count,
            order.GrandTotal, order.Currency);

        return order.Id.ToString();
    }
}
