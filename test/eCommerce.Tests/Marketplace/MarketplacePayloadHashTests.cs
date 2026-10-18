using eCommerce.ChannelHub.Connectors;
using eCommerce.ChannelHub.Events;
using eCommerce.Marketplace;
using eCommerce.Messaging;
using Volo.Abp;

namespace eCommerce.Tests.Marketplace;

/// <summary>
/// 🚨 **HASH "NỘI DUNG NGHIỆP VỤ" CỦA SỰ KIỆN ĐƠN SÀN** — khoá lại một **lỗi THẬT đã sửa ở `S6`**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **LỖI ĐÃ ĐO ĐƯỢC (không phải giả thuyết):** handler từng băm **cả ETO**
/// (`JsonSerializer.Serialize(eventData)`) — tức băm cả `CorrelationId`, mà trường đó **đổi mỗi lần kéo**
/// (`ChannelShopSyncer` sinh `Guid.NewGuid()` cho **mỗi nhịp**). ⇒ cùng một đơn, cùng nội dung, lần kéo sau
/// vẫn ra **hash khác** ⇒ tầng chống trùng báo **XUNG ĐỘT PAYLOAD** ⇒ **DLQ giả ở MỌI lần kéo lại**
/// + `SkipReason` bị ghi đè.
///
/// 📌 Vì sao lỗi này **không** bị bắt bởi 1358 test cũ: mọi test đều xây **một** ETO rồi xử lý **một** lần.
/// 🔒 Lỗi chỉ hiện khi **cùng một đơn được gửi HAI lần với `CorrelationId` khác nhau** — đúng ca
/// *"sàn gửi lại"* mà `SAL-005` tồn tại để phục vụ. ⇒ Test dưới đây khoá đúng ca đó.
///
/// 🚨 **Nếu ai thêm một trường NGỮ CẢNH VẬN CHUYỂN vào <see cref="MarketplacePayloadHash.NoiDung"/>**
/// (`TenantId`, `ChannelShopId`, `CorrelationId`, `IsFromWebhook`, `PayloadHash`) thì test ①/② **đỏ** —
/// và nếu không có chúng thì lỗi sẽ **quay lại trong im lặng**.
/// </summary>
public class MarketplacePayloadHashTests
{
    private static ChannelOrderPulledEto TaoEto(
        string correlationId = "corr-1",
        Guid? tenantId = null,
        Guid? channelShopId = null,
        bool isFromWebhook = false,
        decimal donGia = 100_000m) => new()
    {
        TenantId = tenantId,
        ChannelShopId = channelShopId ?? Guid.Parse("0f4c1f9e-6f1e-4a3c-9a1f-2b6a4c7d8e90"),
        ChannelCode = "shopee",
        ExternalShopId = "SHOP-1",
        ExternalOrderId = "SN-231012ABCDEFGH",
        PlacedAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
        Currency = "VND",
        ShippingFee = 30_000m,
        ChannelDiscount = 5_000m,
        BuyerNote = "giao gio hanh chinh",
        AggregateVersion = 1768003260,
        SourceUpdatedAtUtc = new DateTime(2026, 1, 10, 0, 1, 0, DateTimeKind.Utc),
        PayloadHash = new string('a', 64),
        CorrelationId = correlationId,
        IsFromWebhook = isFromWebhook,
        MarketplaceStatusRaw = "READY_TO_SHIP",
        EventKind = "Created",
        Lines =
        [
            new ChannelOrderPulledLineEto
            {
                ExternalLineId = "L1",
                Sku = "SKU-A",
                Quantity = 2,
                UnitPrice = donGia,
                DiscountAmount = 1_000m
            }
        ]
    };

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ① TRƯỜNG VẬN CHUYỂN KHÔNG ĐƯỢC ẢNH HƯỞNG HASH — đây là ca chống tái phát của lỗi
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Doi_CorrelationId__hash_KHONG_doi()
    {
        // 🚨 ĐÂY LÀ CA QUAN TRỌNG NHẤT: `CorrelationId` đổi **mỗi lần kéo** ⇒ nếu nó vào hash thì mọi lần
        // kéo lại cùng một đơn đều thành "xung đột payload" ⇒ DLQ giả + cảnh báo mất tác dụng.
        var hash1 = MarketplacePayloadHash.Tinh(TaoEto(correlationId: "corr-lan-1"));
        var hash2 = MarketplacePayloadHash.Tinh(TaoEto(correlationId: "corr-lan-2-khac-hoan-toan"));

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Doi_TenantId_ChannelShopId_duong_vao_PayloadHash__hash_KHONG_doi()
    {
        // 📌 Cùng lý do với ①: đây là **ngữ cảnh vận chuyển**, không phải nội dung đơn.
        // 🔑 `IsFromWebhook` đổi khi cùng một đơn đến **webhook** thay vì **pull** — mà `D4` nói hai đường
        // PHẢI dùng **cùng** khoá chống trùng ⇒ hash cũng phải giống nhau, nếu không dual-path thành xung đột.
        var goc = MarketplacePayloadHash.Tinh(TaoEto());

        var khacNguCanh = TaoEto();
        khacNguCanh.TenantId = Guid.NewGuid();
        khacNguCanh.ChannelShopId = Guid.NewGuid();
        khacNguCanh.IsFromWebhook = true;
        khacNguCanh.PayloadHash = new string('b', 64);

        Assert.Equal(goc, MarketplacePayloadHash.Tinh(khacNguCanh));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ② NỘI DUNG ĐỔI THÌ HASH PHẢI ĐỔI — chống chiều ngược lại ("hash hằng số" thì vô dụng)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Doi_noi_dung_nghiep_vu__hash_PHAI_doi()
    {
        var goc = MarketplacePayloadHash.Tinh(TaoEto());

        Assert.NotEqual(goc, MarketplacePayloadHash.Tinh(TaoEto(donGia: 100_001m)));

        var doiTrangThai = TaoEto();
        doiTrangThai.MarketplaceStatusRaw = "CANCELLED";
        Assert.NotEqual(goc, MarketplacePayloadHash.Tinh(doiTrangThai));

        var doiSoLuong = TaoEto();
        doiSoLuong.Lines[0].Quantity = 3;
        Assert.NotEqual(goc, MarketplacePayloadHash.Tinh(doiSoLuong));

        var doiMocCapNhat = TaoEto();
        doiMocCapNhat.SourceUpdatedAtUtc = doiMocCapNhat.SourceUpdatedAtUtc!.Value.AddMinutes(5);
        Assert.NotEqual(goc, MarketplacePayloadHash.Tinh(doiMocCapNhat));
    }

    [Fact]
    public void Chuoi_chuan_hoa_on_dinh_va_KHONG_chua_truong_van_chuyen()
    {
        var a = MarketplacePayloadHash.ChuoiChuanHoa(TaoEto(correlationId: "corr-a"));
        var b = MarketplacePayloadHash.ChuoiChuanHoa(TaoEto(correlationId: "corr-b"));

        Assert.Equal(a, b);   // ổn định + transport-free

        // 🔒 Khẳng định thêm bằng **nội dung**: chuỗi này KHÔNG được nhắc tới các trường vận chuyển.
        // 📌 So khớp KHÔNG phân biệt HOA/thường để không bỏ sót nếu ai đổi tên thành `CorrelationID`.
        foreach (var truong in new[] { "correlation", "tenantid", "channelshopid", "isfromwebhook", "payloadhash" })
        {
            Assert.DoesNotContain(truong, a.ToLowerInvariant(), StringComparison.Ordinal);
        }

        Assert.Contains("externalorderid", a.ToLowerInvariant(), StringComparison.Ordinal);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ③ PRODUCER ↔ CONSUMER — hai bên phải băm CÙNG một thứ
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Producer_bam_model_va_consumer_bam_ETO__cho_CUNG_hash()
    {
        // 🚨 VÌ SAO TEST NÀY: `ChannelShopSyncer` băm **model của connector** (`ChannelOrderModel`) để đặt vào
        // `ETO.PayloadHash`; handler băm **nội dung nghiệp vụ** của ETO. Nếu hai hình dạng lệch nhau (đổi tên
        // trường, thêm/bớt trường) thì **hai hash khác nhau cho cùng một đơn** ⇒ không ai so được chúng,
        // và mọi kết luận "trùng/xung đột" dựa trên **hai định nghĩa khác nhau** — đúng họ lỗi `XC-003` F2.
        var model = new ChannelOrderModel
        {
            ExternalOrderId = "SN-231012ABCDEFGH",
            PlacedAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            BuyerNote = "giao gio hanh chinh",
            Currency = "VND",
            ShippingFee = 30_000m,
            ChannelDiscount = 5_000m,
            UpdatedAtUtc = new DateTime(2026, 1, 10, 0, 1, 0, DateTimeKind.Utc),
            StatusRaw = "READY_TO_SHIP",
            EventKind = "Created",
            Lines =
            [
                new ChannelOrderLineModel
                {
                    ExternalLineId = "L1",
                    Sku = "SKU-A",
                    Quantity = 2,
                    UnitPrice = 100_000m,
                    DiscountAmount = 1_000m
                }
            ]
        };

        // ① phía PHÁT — đúng như `ChannelShopSyncer.BanGiaoLoDonAsync`
        var hashModel = PayloadCanonicalizer.Bam(model);

        // ② phía NHẬN — handler (ETO ⇐ model ở trên)
        var hashEto = MarketplacePayloadHash.Tinh(TaoEto());

        Assert.Equal(hashModel, hashEto);
    }
}
