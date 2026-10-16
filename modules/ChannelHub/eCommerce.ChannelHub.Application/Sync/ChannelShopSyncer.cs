using eCommerce.ChannelHub.Channels;
using eCommerce.ChannelHub.Connectors;
using eCommerce.ChannelHub.Events;
using eCommerce.Messaging;
using eCommerce.Messaging.EventCoverage;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.ChannelHub.Sync;

/// <summary>
/// Đồng bộ MỘT shop: đảm bảo token hợp lệ → kéo đơn → phát sự kiện cho app xử lý.
///
/// 🎯 Nguyên tắc: ChannelHub KHÔNG tự tạo đơn hàng. Nó chỉ phát
/// <see cref="ChannelOrderPulledEto"/>; app subscribe và tạo Order (idempotent theo
/// TenantId + ChannelCode + ExternalOrderId). Nhờ vậy:
///   • module không phụ thuộc ngược vào app,
///   • tách microservice được về sau,
///   • và lỗi khi tạo đơn không làm hỏng con trỏ đồng bộ của kênh.
///
/// 🚨 **FAIL-CLOSED — LỖI #22 (mức nghiêm trọng nhất dự án, sửa 14/10/2026):**
/// Trước đây lớp này publish `ChannelOrderPulledEto` cho **từng đơn** rồi **tiến con trỏ đồng bộ**
/// *bất kể có ai nhận hay không*. Khi chưa có handler nào:
///   • đồng bộ báo **THÀNH CÔNG** (`OrdersPulled = N`, `LastError = null`, log Information),
///   • sàn **không bao giờ gửi lại** ⇒ **đơn mất VĨNH VIỄN**, **không có lỗi nào được ghi**.
///
/// Nay trước khi bàn giao, lớp này **tự hỏi** `IEventCoverageProbe` xem `ChannelOrderPulledEto` đã có
/// người nhận chưa, rồi để <see cref="QuyetDinhDongBoDonPolicy"/> quyết định. Nếu **có đơn mà chưa có
/// nơi nhận**: **KHÔNG publish** và **KHÔNG tiến con trỏ** ⇒ sàn gửi lại ⇒ **đơn không mất**.
///
/// 🔗 Vì sao dùng `IEventCoverageProbe` (chứ không tự quét): nó **dùng chung một nguồn sự thật** với
/// cảnh báo độ phủ sự kiện lúc khởi động ⇒ hai nơi **không thể lệch nhau** theo hướng nguy hiểm.
/// </summary>
public class ChannelShopSyncer : IChannelShopSyncService
{
    private readonly IRepository<ChannelShop, Guid> _channelShopRepository;
    private readonly IChannelConnectorFactory _connectorFactory;
    private readonly ChannelTokenManager _tokenManager;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<ChannelShopSyncer> _logger;

    /// <summary>
    /// 🔒 **Cờ "đã cảnh báo thiếu `update_time`"** — chỉ ghi log **MỘT LẦN** cho cả tiến trình.
    ///
    /// **VÌ SAO KHÔNG GHI MỖI ĐƠN:** một nhịp kéo có thể có hàng nghìn đơn thiếu `update_time` ⇒ hàng nghìn
    /// dòng log **giống hệt nhau** ⇒ **chôn mất** cảnh báo thật. 📌 Đúng bài học đã trả giá: *"dương tính giả
    /// làm người đọc bắt đầu bỏ qua cảnh báo"* — và đó là lúc cảnh báo **THẬT** bị bỏ qua.
    ///
    /// ⚠️ **Giới hạn ĐÃ BIẾT:** `Syncer` là transient ⇒ cờ này **chỉ** có tác dụng trong **một** lần resolve
    /// (một nhịp đồng bộ). Nhịp sau sẽ cảnh báo lại **một lần** nữa — **có chủ ý**: cảnh báo vẫn định kỳ
    /// (không bị mất hẳn) nhưng không nhân lên theo số đơn.
    /// </summary>
    private bool _daCanhBaoThieuUpdateTime;

    /// <summary>
    /// 🔒 **BẮT BUỘC** (không phải tham số tuỳ chọn — cố ý khác `_metrics`): đây là **cổng an toàn chống
    /// mất đơn**. Nếu để tuỳ chọn thì một lần quên truyền là hệ thống **âm thầm** quay lại hành vi làm
    /// mất đơn vĩnh viễn. Bắt buộc ⇒ quên là **lỗi biên dịch / lỗi resolve DI** (ồn ào).
    /// </summary>
    private readonly IEventCoverageProbe _eventCoverageProbe;

    /// <summary>📊 Metric — tham số tuỳ chọn (DI inject bản thật, test cũ truyền null vẫn chạy).</summary>
    private readonly eCommerce.Kernel.Observability.eCommerceMetrics? _metrics;

    public ChannelShopSyncer(
        IRepository<ChannelShop, Guid> channelShopRepository,
        IChannelConnectorFactory connectorFactory,
        ChannelTokenManager tokenManager,
        IDistributedEventBus distributedEventBus,
        ILogger<ChannelShopSyncer> logger,
        IEventCoverageProbe eventCoverageProbe,
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null)
    {
        _channelShopRepository = channelShopRepository;
        _connectorFactory = connectorFactory;
        _tokenManager = tokenManager;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
        _eventCoverageProbe = eventCoverageProbe;
        _metrics = metrics;
    }

    public async Task<ChannelSyncResult> SyncAsync(Guid channelShopId, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var result = new ChannelSyncResult { ChannelShopId = channelShopId, StartedAtUtc = startedAt };

        var shop = await _channelShopRepository.GetAsync(channelShopId, cancellationToken: cancellationToken);
        result.ChannelCode = shop.ChannelCode;

        try
        {
            var (shopContext, tokenRefreshed) = await _tokenManager.GetValidContextAsync(shop, cancellationToken);
            result.TokenRefreshed = tokenRefreshed;

            var connector = _connectorFactory.Get(shop.ChannelCode);
            var pull = await connector.PullOrdersAsync(shopContext, cancellationToken);

            if (pull.RateLimited)
            {
                // Sàn báo hết quota: KHÔNG retry dồn, chỉ ghi nhận để supervisor giãn nhịp.
                result.RateLimited = true;
                result.Message = pull.Message ?? "Sàn báo rate limit (HTTP 429).";
                _logger.LogWarning("Shop {ShopId} trên {ChannelCode} bị rate limit.", shop.ExternalShopId, shop.ChannelCode);
            }
            else
            {
                await BanGiaoLoDonAsync(shop, pull, result, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            result.Errors++;
            result.Message = ex.Message;

            // 📊 Phát metric thất bại đồng bộ (`XC-005` §5.1). Nhãn HỮU HẠN: kênh + loại lỗi.
            // KHÔNG đưa `shopId`/`orderId` vào nhãn (cardinality cao — xem `eCommerceMetricTags.Forbidden`).
            _metrics?.ChannelSyncFailed.Add(1,
                new KeyValuePair<string, object?>(
                    eCommerce.Kernel.Observability.eCommerceMetricTags.Channel, shop.ChannelCode),
                new KeyValuePair<string, object?>(
                    eCommerce.Kernel.Observability.eCommerceMetricTags.Reason, ex.GetType().Name));

            // Ghi lỗi để hiển thị trên admin, nhưng KHÔNG nuốt exception ở tầng worker
            // (supervisor cần biết để log/cảnh báo).
            _logger.LogError(ex, "Đồng bộ shop {ShopId} trên {ChannelCode} thất bại.", shop.ExternalShopId, shop.ChannelCode);

            shop.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await _channelShopRepository.UpdateAsync(shop, autoSave: true, cancellationToken: cancellationToken);
        }

        result.FinishedAtUtc = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Bàn giao lô đơn kéo được: hoặc **phát sự kiện + tiến con trỏ**, hoặc **chặn cả hai** (fail-closed).
    ///
    /// Toàn bộ quy tắc nằm ở <see cref="QuyetDinhDongBoDonPolicy"/> (lõi thuần, test được mọi nhánh);
    /// ở đây chỉ là phần I/O.
    /// </summary>
    private async Task BanGiaoLoDonAsync(
        ChannelShop shop,
        PullOrdersResult pull,
        ChannelSyncResult result,
        CancellationToken cancellationToken)
    {
        var soDon = pull.Orders.Count;
        var coNoiNhan = _eventCoverageProbe.CoNguoiNhan(typeof(ChannelOrderPulledEto));
        var quyetDinh = QuyetDinhDongBoDonPolicy.Tinh(soDon, coNoiNhan);

        if (quyetDinh.ChoPhepPhatSuKien)
        {
            // 🔑 `CorrelationId` **một giá trị cho MỘT LẦN KÉO** (cả lô đơn của nhịp này) để khi điều tra
            // nối được *"nhịp đồng bộ nào sinh ra đơn này"*. 📌 Khác `EventId` (mỗi sự kiện một mã) —
            // và **khác** `SyncCursor` (con trỏ là mốc của **lần sau**, không phải mã của lần này).
            var correlationId = Guid.NewGuid().ToString("N");

            foreach (var order in pull.Orders)
            {
                // 🚨 `SAL-005` §7.1 — **ETO PHẢI MANG WATERMARK**.
                //
                // 📌 `PayloadHash` được tính trên **CHÍNH `order`** (model của connector), **KHÔNG** phải trên
                // ETO đã dựng: băm ETO thì `TenantId`/`ChannelShopId` (ngữ cảnh **VẬN CHUYỂN**) lọt vào hash
                // ⇒ **cùng một nội dung đơn** đến từ hai shop/kênh khác nhau sẽ ra **hash khác nhau**.
                // Hash phải là hash của **NỘI DUNG NGHIỆP VỤ**.
                var payloadHash = PayloadCanonicalizer.Bam(order);

                // 🔒 Thiếu `UpdatedAtUtc` ⇒ **KHÔNG** suy diễn từ `PlacedAtUtc` (xem chú thích ở
                // `ChannelOrderModel.UpdatedAtUtc`): để `null` ⇒ consumer **NÉM** `eCommerce:Sales:0001`
                // + DLQ `PoisonMessage` — *thà không xử lý còn hơn xử lý dựa trên suy đoán*.
                // 📌 Đây là điểm **cố ý** để lỗi trở nên **ồn ào**: nếu connector trả `update_time` rỗng thì
                // phải sửa **connector**, không phải để tầng dưới đoán.
                var phienBan = order.UpdatedAtUtc is { } mocCapNhat
                    ? PayloadCanonicalizer.ThanhPhienBan(mocCapNhat)
                    : (long?)null;

                if (phienBan is null && !_daCanhBaoThieuUpdateTime)
                {
                    // 📌 Ghi **MỘT LẦN** cho cả tiến trình (không phải mỗi đơn): hàng nghìn dòng log giống nhau
                    // sẽ **chôn mất** cảnh báo thật — đúng bài học "dương tính giả làm người đọc bỏ qua cảnh báo".
                    _daCanhBaoThieuUpdateTime = true;
                    _logger.LogWarning(
                        "KÊNH {ChannelCode}: sàn KHÔNG trả `update_time` cho đơn ⇒ ETO KHÔNG có watermark " +
                        "và consumer sẽ NÉM (eCommerce:Sales:0001) + DLQ. Cần sửa CONNECTOR để trả `update_time`. " +
                        "[MP-THIEU-UPDATE-TIME] (chỉ ghi 1 lần cho cả tiến trình)",
                        shop.ChannelCode);
                }

                await _distributedEventBus.PublishAsync(new ChannelOrderPulledEto
                {
                    TenantId = shop.TenantId,
                    ChannelShopId = shop.Id,
                    ChannelCode = shop.ChannelCode,
                    ExternalShopId = shop.ExternalShopId,
                    ExternalOrderId = order.ExternalOrderId,
                    PlacedAtUtc = order.PlacedAtUtc,
                    Currency = order.Currency,
                    ShippingFee = order.ShippingFee,
                    ChannelDiscount = order.ChannelDiscount,
                    BuyerNote = order.BuyerNote,

                    // ── Watermark + truy vết (`SAL-005` §7.1) ────────────────────────────────────
                    AggregateVersion = phienBan,
                    SourceUpdatedAtUtc = order.UpdatedAtUtc,
                    PayloadHash = payloadHash,
                    CorrelationId = correlationId,
                    IsFromWebhook = false,
                    MarketplaceStatusRaw = order.StatusRaw,
                    EventKind = order.EventKind,

                    Lines = order.Lines.Select(l => new ChannelOrderPulledLineEto
                    {
                        ExternalLineId = l.ExternalLineId,
                        Sku = l.Sku,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        DiscountAmount = l.DiscountAmount
                    }).ToList()
                });
            }
        }

        result.OrdersPulled = soDon;

        if (quyetDinh.ChoPhepTienConTro)
        {
            // Chỉ tiến con trỏ khi đã bàn giao xong (hoặc lô rỗng — không có gì để mất).
            shop.LastSyncedAtUtc = pull.NewSyncCursorUtc ?? DateTime.UtcNow;
            shop.LastError = null;
            await _channelShopRepository.UpdateAsync(shop, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        // 🚨 FAIL-CLOSED (lỗi #22): giữ nguyên `LastSyncedAtUtc` để sàn GỬI LẠI lô đơn này.
        var thongDiep =
            $"[{ChannelHubErrorCodes.ChuaCoNoiNhanDon}] Kéo được {soDon} đơn nhưng CHƯA CÓ NƠI NHẬN " +
            $"'{nameof(ChannelOrderPulledEto)}' ⇒ KHÔNG bàn giao và KHÔNG tiến con trỏ đồng bộ " +
            "(đơn sẽ được sàn gửi lại). Cần xây Order intake cho GĐ1 rồi mới đồng bộ được.";

        result.Errors++;
        result.BiChanDoChuaCoNoiNhanDon = true;
        result.LyDoChan = LyDoChanDongBoDon.ChuaCoNoiNhanDon.ToString();
        result.Message = thongDiep;

        // 📊 Cùng instrument với lỗi đồng bộ — nhưng nhãn `reason` PHÂN BIỆT ĐƯỢC ca "chưa có nơi nhận"
        // với lỗi kỹ thuật. Nhãn vẫn HỮU HẠN (kênh + lý do), không có `shopId`.
        _metrics?.ChannelSyncFailed.Add(1,
            new KeyValuePair<string, object?>(
                eCommerce.Kernel.Observability.eCommerceMetricTags.Channel, shop.ChannelCode),
            new KeyValuePair<string, object?>(
                eCommerce.Kernel.Observability.eCommerceMetricTags.Reason,
                LyDoChanDongBoDon.ChuaCoNoiNhanDon.ToString()));

        _logger.LogError(
            "🚨 CHẶN BÀN GIAO ĐƠN — shop {ShopId} trên {ChannelCode}: kéo được {SoDon} đơn nhưng sự kiện " +
            "{TenSuKien} KHÔNG có người nhận. Đã GIỮ NGUYÊN con trỏ đồng bộ để sàn gửi lại (đơn KHÔNG mất). " +
            "Đã quét {SoAssembly} assembly; sự kiện chưa có người nhận: {DanhSach}. " +
            "Đây là trạng thái có chủ ý cho tới khi Order intake (GĐ1) được xây.",
            shop.ExternalShopId,
            shop.ChannelCode,
            soDon,
            nameof(ChannelOrderPulledEto),
            _eventCoverageProbe.KetQua.SoAssemblyDaQuet,
            string.Join(", ", _eventCoverageProbe.KetQua.PhuSong.SuKienKhongCoNguoiNhan));

        shop.LastError = thongDiep.Length > 2000 ? thongDiep[..2000] : thongDiep;
        await _channelShopRepository.UpdateAsync(shop, autoSave: true, cancellationToken: cancellationToken);
    }
}
