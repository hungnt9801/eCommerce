using System.Text.Json;
using eCommerce.ChannelHub.Events;
using eCommerce.Messaging;
using eCommerce.Messaging.DeadLetter;
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
    private readonly DeadLetterWriter _dlq;
    private readonly MarketplaceEventLogWriter _eventLog;

    public MarketplaceOrderIntakeHandler(
        IRepository<Orders.Order, Guid> orderRepository,
        IRepository<Orders.MarketplaceOrderIntake, Guid> intakeRepository,
        IIdempotencyGuard idempotencyGuard,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<MarketplaceOrderIntakeHandler> logger,
        DeadLetterWriter dlq,
        MarketplaceEventLogWriter eventLog)
    {
        _orderRepository = orderRepository;
        _intakeRepository = intakeRepository;
        _idempotencyGuard = idempotencyGuard;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
        _dlq = dlq;
        _eventLog = eventLog;
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

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **HAI PAYLOAD, HAI MỤC ĐÍCH KHÁC NHAU — đừng gộp (lỗi THẬT, sửa ở `S6`):**
        //
        //   • `payloadJsonDayDu` = `Serialize(eventData)` — **payload GỐC nguyên vẹn**, dùng cho **DLQ**
        //     (bất biến 1 của DLQ: cắt/chuẩn hoá lại ⇒ **không phát lại được**);
        //   • `payloadJsonNghiepVu` = **nội dung NGHIỆP VỤ** (không có `CorrelationId`/`TenantId`/
        //     `ChannelShopId`/`IsFromWebhook`) — dùng cho **khoá chống trùng** và **hash**.
        //
        // 🔑 **VÌ SAO PHẢI TÁCH (đo được):** trước `S6` cả hai dùng chung `payloadJson` ⇒ hash **có**
        // `CorrelationId` — mà trường đó **đổi mỗi lần kéo** ⇒ cùng một đơn, cùng nội dung, lần kéo sau vẫn
        // ra **hash khác** ⇒ tầng chống trùng kết luận **XUNG ĐỘT PAYLOAD** ⇒ **DLQ GIẢ ở MỌI lần kéo lại**
        // + `SkipReason` bị ghi đè. 📌 Sau vài lần DLQ giả thì người trực **bỏ qua cảnh báo** — và đó là lúc
        // xung đột **THẬT** bị bỏ qua (`PayloadCanonicalizer` đã cảnh báo đúng điều này).
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        var payloadJsonDayDu = JsonSerializer.Serialize(eventData);
        var payloadJsonNghiepVu = MarketplacePayloadHash.ChuoiChuanHoa(eventData);
        var payloadHash = MarketplacePayloadHash.Tinh(eventData);

        var khoa = IdempotencyKey.Cua($"{eventData.ChannelCode}:order", eventData.ExternalOrderId);

        // `IDistributedEventHandler<T>` (ABP 10.7) không truyền `CancellationToken` vào handler ⇒ dùng token
        // mặc định cho cả lần gọi guard và lần ghi vết, để hai chỗ **cùng một token**.
        var ct = CancellationToken.None;

        try
        {
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

            var ketQua = await _idempotencyGuard.ThucThiAsync(
                khoa,
                payloadJsonNghiepVu,
                token => NhanDonAsync(eventData, payloadHash, token),
                ct);

            switch (ketQua.Kind)
            {
                case IdempotencyOutcomeKind.DaThucThi:
                    // 📌 Dấu vết của nhánh này (`Applied`/`GapSuspected`) được ghi **bên trong**
                    // `NhanDonAsync` — nơi **biết** kết cục thật. Ghi thêm ở đây là **hai dòng cho một sự kiện**.
                    _logger.LogInformation(
                        "Đã nhận đơn sàn {ChannelCode}/{ExternalOrderId} ⇒ tạo đơn nội bộ {OrderId}.",
                        eventData.ChannelCode, eventData.ExternalOrderId, ketQua.Result);
                    break;

                case IdempotencyOutcomeKind.TraVeKetQuaCu:
                    // Sàn gửi lại / worker pull lại cùng đơn: KHÔNG tạo đơn thứ hai — đúng thiết kế.
                    _logger.LogInformation(
                        "Đơn sàn {ChannelCode}/{ExternalOrderId} đã xử lý trước đó (đơn nội bộ {OrderId}) — bỏ qua, không tạo trùng.",
                        eventData.ChannelCode, eventData.ExternalOrderId, ketQua.Result);

                    // 🆕 `D7` — ca BỎ QUA **cũng** phải có dấu vết (§8.1 bất biến 5). 📌 Nếu thiếu, câu hỏi
                    // *"sàn đã gửi lại bao nhiêu lần?"* **không trả lời được** — và đó chính là thông tin
                    // dùng để phát hiện dual-path (webhook + pull cùng đơn).
                    await _eventLog.GhiAsync(
                        eventData,
                        Orders.MarketplaceIntakeOutcome.Duplicate,
                        payloadHash,
                        eventData.AggregateVersion,
                        cancellationToken: ct);
                    break;

                case IdempotencyOutcomeKind.XungDotPayload:
                    // 🚨 Cùng khoá đơn nhưng nội dung KHÁC ⇒ **KHÔNG đoán bản nào đúng** (`SAL-005` D6):
                    //    KHÔNG ghi đè, GIỮ bản đã nhận đầu tiên (không mất dữ liệu).
                    //
                    // ⚠️ QUYẾT ĐỊNH CÓ CHỦ Ý — **KHÔNG ném lỗi** ở đây:
                    //    • Ném lỗi ⇒ `PublishAsync` ném ⇒ `ChannelShopSyncer` **KHÔNG tiến con trỏ** ⇒ sàn gửi lại
                    //      đúng đơn đó ở nhịp sau ⇒ **lặp vô hạn** và **CHẶN CẢ KÊNH**: mọi đơn phía sau cũng không
                    //      vào được. Một đơn nghi vấn làm **sập luồng bán hàng** — hỏng nặng hơn nhiều.
                    //      *(Đã đo được thật 15/10/2026: con trỏ kẹt ở NULL qua nhiều nhịp.)*
                    //    • Nay: GIỮ bản đầu (không ghi đè ⇒ **không mất dữ liệu**, và **tất định**: không instance
                    //      nào ghi đè, nên kết quả không phụ thuộc thứ tự đến), **GHI VẾT** vào nhật ký nhận đơn
                    //      (`SkipReason`) + **DLQ** + nhật ký sự kiện ⇒ **không im lặng**.
                    _logger.LogError(
                        "🚨 XUNG ĐỘT PAYLOAD cho đơn sàn {ChannelCode}/{ExternalOrderId}: cùng khoá nhưng nội dung khác. " +
                        "GIỮ NGUYÊN bản đã nhận đầu tiên (không ghi đè, không đoán) và GHI VẾT để xử lý thủ công. " +
                        "KHÔNG chặn kênh — nếu chặn thì một đơn nghi vấn sẽ làm kẹt toàn bộ đơn phía sau.",
                        eventData.ChannelCode, eventData.ExternalOrderId);

                    await GhiVetXungDotPayloadAsync(eventData, ct);

                    // 🚨 `XC-002` — **ĐẨY DLQ**.
                    // 🔑 Vì sao `LogError` **KHÔNG đủ**: log bị **xoay vòng** (retention vài ngày), **không** tra cứu
                    // được theo khoá nghiệp vụ, và **không ai** biết còn tồn bao nhiêu ⇒ **mất dữ liệu nhưng có vẻ
                    // như đã xử lý**.
                    // 📌 Dùng chế độ **chung UoW** (không phải UoW riêng): việc xử lý **không** hỏng — ta **quyết định**
                    // đẩy DLQ, và bản ghi DLQ **phải** cùng transaction với vết `SkipReason` ở trên.
                    await _dlq.DayVaoDlqAsync(
                        tenSuKien: nameof(ChannelOrderPulledEto),
                        khoaNghiepVu: $"{eventData.ChannelCode}:order:{eventData.ExternalOrderId}",
                        payloadJson: payloadJsonDayDu,
                        payloadHash: payloadHash,
                        lyDo: Messaging.DeadLetter.LyDoVaoDlq.XungDotPayload,
                        moTaLyDo:
                            "Cùng khoá đơn nhưng NỘI DUNG KHÁC bản đã nhận. GIỮ bản đầu (không đoán bản nào đúng) "
                            + "và đẩy DLQ để người xử lý quyết định.",
                        tenantId: eventData.TenantId,
                        cancellationToken: ct);

                    await _eventLog.GhiAsync(
                        eventData,
                        Orders.MarketplaceIntakeOutcome.Conflict,
                        payloadHash,
                        eventData.AggregateVersion,
                        cancellationToken: ct);
                    break;

                case IdempotencyOutcomeKind.DangXuLy:
                    _logger.LogWarning(
                        "Đơn sàn {ChannelCode}/{ExternalOrderId} ĐANG được xử lý ở nơi khác ⇒ bỏ lượt này; " +
                        "sàn/worker sẽ gửi lại (con trỏ đồng bộ chỉ tiến khi xử lý xong).",
                        eventData.ChannelCode, eventData.ExternalOrderId);

                    // 📌 Vẫn ghi dấu vết: đây **là** một lần nhận sự kiện, chỉ là lần nhận **không được xử lý**
                    // vì cùng sự kiện đang bay ở luồng khác ⇒ xếp vào `Duplicate` (cùng sự kiện, giao lại).
                    await _eventLog.GhiAsync(
                        eventData,
                        Orders.MarketplaceIntakeOutcome.Duplicate,
                        payloadHash,
                        eventData.AggregateVersion,
                        cancellationToken: ct);
                    break;
            }

            // Chốt transaction CỦA HANDLER (mọi nhánh đều ghi vết rồi mới tới đây ⇒ KHÔNG rollback oan).
            await uow.CompleteAsync();
        }
        catch (Exception ex)
        {
            // 🆕 `D7`/§8.1 bất biến 5 — **sự kiện hỏng VẪN phải có dòng**. 📌 Không có nó thì
            // *"không có dòng nào"* và *"chưa từng nhận sự kiện này"* **trông giống hệt nhau** khi điều tra.
            // 🔒 `GhiThatBaiAsync` tự mở UoW riêng ⇒ dòng `Failed` **không** bị rollback theo transaction vừa hỏng.
            await _eventLog.GhiThatBaiAsync(
                eventData,
                eventData.ChannelCode,
                eventData.ExternalOrderId,
                payloadHash,
                eventData.AggregateVersion,
                failureCode: (ex as BusinessException)?.Code ?? ex.GetType().Name,
                failureMessage: ex.Message,
                cancellationToken: ct);

            throw;
        }
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
            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 **CẬP NHẬT THEO WATERMARK** (`SAL-005` D5/D6) — **NỢ §17.8 #3 ĐÃ ĐÓNG Ở ĐÂY.**
            //
            // 📌 TRƯỚC LƯỢT NÀY: đơn đã nhận thì **lần sau KHÔNG sửa** — cập nhật của sàn (đổi địa chỉ,
            // huỷ, đổi trạng thái) **BỊ BỎ HOÀN TOÀN**. Chú thích cũ ghi rõ đó là đánh đổi **cố ý**
            // (*"an toàn trước, đầy đủ sau"*). 🔒 Nay làm phần **đầy đủ**.
            //
            // 🔑 **LUẬT 4 NHÁNH (`LuatWatermark.Tinh`) — KHÔNG tự viết lại ở đây:**
            //   • `ApDung`   (v_in > v_đã_áp) ⇒ **ghi** cập nhật + watermark;
            //   • `Trung`    (v_in == v_đã_áp, hash GIỐNG) ⇒ **không làm gì** (retry của sàn — bình thường);
            //   • `XungDot`  (v_in == v_đã_áp, hash KHÁC) ⇒ 🚨 **KHÔNG đoán bản nào đúng** ⇒ **đẩy DLQ**;
            //   • `DenMuon`  (v_in < v_đã_áp) ⇒ **KHÔNG ghi đè** (bản cũ hơn) — ⚠️ nhưng **không im lặng**:
            //      đẩy DLQ để có **bằng chứng** sàn gửi sai thứ tự (hoặc hai nguồn tranh nhau).
            //      📌 Đây **không phải** lỗi ⇒ `MoTaLyDo` nói rõ *"cần xem xét, không phải hỏng"*.
            //
            // ⚠️ **`AggregateVersion` có thể `null`** (connector chưa trả `update_time` — nợ §17.8).
            //    Xử lý: coi như **không có watermark** ⇒ giữ nguyên hành vi cũ (chỉ ghi vết `SkipReason`)
            //    và **KHÔNG** đẩy DLQ (vì **không thể** kết luận gì về thứ tự). 📌 Tuyệt đối **không** bịa
            //    một phiên bản cho nó: bịa ⇒ mọi cập nhật sau đó bị coi là *"đến muộn"* ⇒ **bỏ hết**.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            if (suKien.AggregateVersion is { } vVao)
            {
                var quyetDinh = LuatWatermark.Tinh(
                    vVao,
                    daNhan.AppliedSourceVersion,
                    payloadHash,
                    daNhan.AppliedPayloadHash);

                switch (quyetDinh.KetLuan)
                {
                    case KetLuanWatermark.ApDung:
                        // 🔒 `ApDungWatermark` **NÉM** nếu phiên bản không tăng (`Sales:0003`) — đó là hàng rào
                        // ở **tầng domain**, không phải ở đây. 📌 Ta đã hỏi luật trước, nên ở nhánh này nó
                        // **không** ném; nếu ném thì đó là **bất nhất quán** giữa luật và entity ⇒ **phải** nổi lên.
                        daNhan.ApDungWatermark(
                            vVao,
                            payloadHash,
                            suKien.SourceUpdatedAtUtc,
                            quyetDinh.NghiNgoThieuBanTrungGian);

                        await _intakeRepository.UpdateAsync(daNhan, autoSave: true, cancellationToken: cancellationToken);

                        // ⚠️ **CẬP NHẬT ĐƠN NỘI BỘ** — nợ đã ghi: hiện `Order` **chưa** có đường áp cập nhật từ sàn
                        // (đó là phần `SAL-006`/đồng bộ trạng thái). 📌 Ta **vẫn** áp watermark + phát hiện xung đột
                        // (giá trị thật của `D5`), nhưng **KHÔNG** giả vờ rằng đơn nội bộ đã được cập nhật.
                        // 🔑 Ghi log rõ để **không** ai đọc nhầm là đã đồng bộ xong.
                        _logger.LogInformation(
                            "ĐÃ ÁP WATERMARK cho đơn sàn {ChannelCode}/{ExternalOrderId}: phiên bản {PhienBan} " +
                            "(nghiNgoThieuBan={NghiNgo}). [WM-DA-AP]",
                            suKien.ChannelCode, suKien.ExternalOrderId, vVao, quyetDinh.NghiNgoThieuBanTrungGian);

                        // 🆕 `D7` — dấu vết cho nhánh **ÁP DỤNG**. 📌 Tách `GapSuspected` khỏi `Applied`:
                        // nếu gộp thì *"có bao nhiêu lần nghi mất bản"* chỉ còn đọc được qua cột cờ — mà cột cờ
                        // bị Ops hạ sau khi đối soát ⇒ **con số biến mất khỏi nhật ký**.
                        await _eventLog.GhiAsync(
                            suKien,
                            quyetDinh.NghiNgoThieuBanTrungGian
                                ? Orders.MarketplaceIntakeOutcome.GapSuspected
                                : Orders.MarketplaceIntakeOutcome.Applied,
                            payloadHash,
                            vVao,
                            cancellationToken: cancellationToken);
                        break;

                    case KetLuanWatermark.Trung:
                        // 📌 Sàn gửi lại **đúng** bản đã nhận ⇒ **không làm gì**. Đây là ca **bình thường nhất**
                        // (retry của sàn, worker pull lại) ⇒ **KHÔNG** log `Warning`, **KHÔNG** DLQ.
                        _logger.LogDebug(
                            "Đơn sàn {ChannelCode}/{ExternalOrderId} TRÙNG (cùng phiên bản + cùng hash) — bỏ qua. [WM-TRUNG]",
                            suKien.ChannelCode, suKien.ExternalOrderId);

                        await _eventLog.GhiAsync(
                            suKien,
                            Orders.MarketplaceIntakeOutcome.Duplicate,
                            payloadHash,
                            vVao,
                            cancellationToken: cancellationToken);
                        break;

                    case KetLuanWatermark.XungDot:
                        // 🚨 CÙNG phiên bản nhưng NỘI DUNG KHÁC ⇒ **KHÔNG đoán** (`D6`) ⇒ **đẩy DLQ**.
                        daNhan.DanhDauBoQua(
                            $"XUNG ĐỘT: cùng phiên bản {vVao} nhưng hash khác bản đã áp. Giữ bản đầu; đã đẩy DLQ.");
                        await _intakeRepository.UpdateAsync(daNhan, autoSave: true, cancellationToken: cancellationToken);

                        await _dlq.DayVaoDlqAsync(
                            tenSuKien: nameof(ChannelOrderPulledEto),
                            khoaNghiepVu: $"{suKien.ChannelCode}:order:{suKien.ExternalOrderId}",
                            payloadJson: JsonSerializer.Serialize(suKien),
                            payloadHash: payloadHash,
                            lyDo: Messaging.DeadLetter.LyDoVaoDlq.XungDotPayload,
                            moTaLyDo:
                                $"Cùng phiên bản {vVao} nhưng PayloadHash KHÁC bản đã áp "
                                + $"({daNhan.AppliedPayloadHash}). KHÔNG đoán bản nào đúng — giữ bản đầu.",
                            tenantId: suKien.TenantId,
                            cancellationToken: cancellationToken);

                        await _eventLog.GhiAsync(
                            suKien,
                            Orders.MarketplaceIntakeOutcome.Conflict,
                            payloadHash,
                            vVao,
                            cancellationToken: cancellationToken);
                        break;

                    case KetLuanWatermark.DenMuon:
                        // 🔒 **KHÔNG GHI ĐÈ** bản cũ hơn — đây là bất biến trung tâm của cả feature.
                        // ⚠️ Nhưng **KHÔNG im lặng**: đẩy DLQ để có **bằng chứng** (log bị xoay vòng, DLQ thì không).
                        daNhan.DanhDauBoQua(
                            $"ĐẾN MUỘN: phiên bản {vVao} < đã áp {daNhan.AppliedSourceVersion}. Không ghi đè; đã đẩy DLQ.");
                        await _intakeRepository.UpdateAsync(daNhan, autoSave: true, cancellationToken: cancellationToken);

                        await _dlq.DayVaoDlqAsync(
                            tenSuKien: nameof(ChannelOrderPulledEto),
                            khoaNghiepVu: $"{suKien.ChannelCode}:order:{suKien.ExternalOrderId}",
                            payloadJson: JsonSerializer.Serialize(suKien),
                            payloadHash: payloadHash,
                            lyDo: Messaging.DeadLetter.LyDoVaoDlq.HaWatermark,
                            moTaLyDo:
                                $"Bản đến MUỘN (phiên bản {vVao} < đã áp {daNhan.AppliedSourceVersion}). "
                                + "KHÔNG ghi đè — cần xem xét (sàn gửi sai thứ tự, hoặc hai nguồn tranh nhau). "
                                + "Đây KHÔNG phải lỗi hệ thống.",
                            tenantId: suKien.TenantId,
                            cancellationToken: cancellationToken);

                        await _eventLog.GhiAsync(
                            suKien,
                            Orders.MarketplaceIntakeOutcome.Late,
                            payloadHash,
                            vVao,
                            cancellationToken: cancellationToken);
                        break;
                }

                return daNhan.OrderId?.ToString();
            }

            // ── Không có `AggregateVersion` ⇒ giữ nguyên hành vi cũ (không thể kết luận về thứ tự) ──────────
            if (!string.Equals(daNhan.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                daNhan.DanhDauBoQua(
                    $"Nội dung khác bản đã nhận lúc {daNhan.ReceivedAtUtc:O} nhưng sự kiện KHÔNG có "
                    + "`AggregateVersion` (connector chưa trả `update_time`) ⇒ KHÔNG thể xét thứ tự ⇒ "
                    + "giữ nguyên bản cũ, cần xử lý thủ công.");
                await _intakeRepository.UpdateAsync(daNhan, autoSave: true, cancellationToken: cancellationToken);

                _logger.LogWarning(
                    "🚨 Đơn sàn {ChannelCode}/{ExternalOrderId}: nội dung KHÁC nhưng KHÔNG có `AggregateVersion` " +
                    "⇒ không thể áp watermark ⇒ giữ bản cũ + GHI VẾT. [WM-THIEU-PHIEN-BAN]",
                    suKien.ChannelCode, suKien.ExternalOrderId);

                // 🆕 `D7` — `ThieuWatermark` (KHÔNG phải `Late`): `Late` là **kết luận** *"bản này cũ hơn"*
                // dựa trên dữ liệu; ở đây ta **không có** dữ liệu để kết luận. 📌 Gộp hai thứ đó là đúng loại
                // "hai nguyên nhân, một mã" đã làm hỏng phép đo trước đây.
                await _eventLog.GhiAsync(
                    suKien,
                    Orders.MarketplaceIntakeOutcome.ThieuWatermark,
                    payloadHash,
                    sourceVersion: null,
                    cancellationToken: cancellationToken);
            }
            else
            {
                // Cùng hash ⇒ đúng là **cùng một sự kiện** được giao lại (at-least-once) — ca bình thường.
                await _eventLog.GhiAsync(
                    suKien,
                    Orders.MarketplaceIntakeOutcome.Duplicate,
                    payloadHash,
                    sourceVersion: null,
                    cancellationToken: cancellationToken);
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

        // 🆕 `D7`/§3.1 dòng 2 — **GHI WATERMARK NGAY LẦN ĐẦU** (`ChuaCoBanGhi → DaApDung`).
        //
        // 🚨 ĐÂY LÀ MỘT LỖ HỔNG THẬT CỦA LƯỢT TRƯỚC (đóng ở `S6`): trước đây dòng intake mới **không** được
        // ghi `AppliedSourceVersion` ⇒ nó ở `null` cho tới **sự kiện thứ hai**. Hệ quả: một bản **CŨ HƠN**
        // đến sau lại được coi là *"chưa có watermark"* ⇒ **áp dụng** ⇒ **ghi đè trạng thái mới hơn** — đúng
        // điều `D5`/`D6` cấm. 📌 Không lỗi biên dịch, không test nào đỏ (vì chưa có test hai sự kiện trên
        // một đơn ở tầng này). Ghi trước khi `INSERT` để giữ **một** lần ghi.
        if (suKien.AggregateVersion is { } vDauTien)
        {
            nhatKy.ApDungWatermark(vDauTien, payloadHash, suKien.SourceUpdatedAtUtc);
        }

        await _intakeRepository.InsertAsync(nhatKy, autoSave: true, cancellationToken: cancellationToken);

        // 🆕 `D7` — dấu vết cho **sự kiện ĐẦU TIÊN** của đơn sàn.
        await _eventLog.GhiAsync(
            suKien,
            Orders.MarketplaceIntakeOutcome.Applied,
            payloadHash,
            suKien.AggregateVersion,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Đã nhận và lưu đơn sàn {ChannelCode}/{ExternalOrderId}: đơn nội bộ {SoDon}, {SoDong} dòng, " +
            "tổng {GrandTotal} {Currency}.",
            suKien.ChannelCode, suKien.ExternalOrderId, order.Number, suKien.Lines.Count,
            order.GrandTotal, order.Currency);

        return order.Id.ToString();
    }
}
