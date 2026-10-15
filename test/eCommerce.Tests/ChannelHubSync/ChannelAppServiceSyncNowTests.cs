using eCommerce.ChannelHub.Sync;
using eCommerce.Channels;

namespace eCommerce.Tests.ChannelHubSync;

/// <summary>
/// Test cho **đường "Đồng bộ ngay" trên admin** — biến thể thứ hai của lỗi #22.
///
/// 🚨 VÌ SAO CẦN TEST NÀY:
/// Lỗi #22 nguy hiểm không chỉ vì **mất đơn**, mà vì **màn hình báo THÀNH CÔNG**. Nếu tầng API vẫn
/// trả về "thành công, N đơn" khi lô đơn bị **CHẶN** thì người vận hành **không có cách nào biết** —
/// và đúng cái nút họ sẽ bấm khi nghi ngờ đồng bộ là nút này. Vì vậy trạng thái bị chặn **PHẢI đi tới DTO**.
///
/// 🎯 Cách test: bơm một cổng đồng bộ GIẢ (không cần HTTP, không cần DB, không cần token) rồi khẳng định
/// **ánh xạ** `ChannelSyncResult` → `ChannelSyncResultDto` giữ nguyên mọi tín hiệu chẩn đoán.
/// Các phụ thuộc không dùng tới được truyền `null!` — **có chủ ý và ghi rõ**, theo đúng lối đã dùng ở
/// `ChannelTokenManagerMetricsTests`.
/// </summary>
public class ChannelAppServiceSyncNowTests
{
    private sealed class CongDongBoGia(ChannelSyncResult ketQua) : IChannelShopSyncService
    {
        public Guid? IdDaNhan { get; private set; }

        public Task<ChannelSyncResult> SyncAsync(Guid channelShopId, CancellationToken cancellationToken = default)
        {
            IdDaNhan = channelShopId;
            return Task.FromResult(ketQua);
        }
    }

    private static ChannelAppService TaoAppService(IChannelShopSyncService cong)
        => new(
            null!,   // IRepository<ChannelShop, Guid>  — SyncNowAsync KHÔNG dùng (đi qua cổng chung)
            null!,   // IChannelConnectorFactory       — SyncNowAsync KHÔNG dùng nữa (đã bỏ kéo đơn trực tiếp)
            null!,   // IStringEncryptionService       — chỉ ConnectAsync dùng
            null!,   // IGuidGenerator                 — chỉ ConnectAsync dùng
            cong);

    [Fact]
    public async Task SyncNowAsync_PHAI_tra_ve_trang_thai_BI_CHAN_de_admin_nhin_thay()
    {
        var shopId = Guid.NewGuid();
        var batDau = new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

        var cong = new CongDongBoGia(new ChannelSyncResult
        {
            ChannelShopId = shopId,
            ChannelCode = "shopee",
            OrdersPulled = 3,
            Errors = 1,
            TokenRefreshed = true,
            BiChanDoChuaCoNoiNhanDon = true,
            LyDoChan = LyDoChanDongBoDon.ChuaCoNoiNhanDon.ToString(),
            Message = "Kéo được 3 đơn nhưng chưa có nơi nhận ⇒ không tiến con trỏ.",
            StartedAtUtc = batDau,
            FinishedAtUtc = batDau.AddSeconds(2)
        });

        var dto = await TaoAppService(cong).SyncNowAsync(shopId);

        Assert.Equal(shopId, cong.IdDaNhan);            // đi ĐÚNG qua cổng chung
        Assert.Equal(shopId, dto.ChannelShopId);
        Assert.Equal("shopee", dto.ChannelCode);

        // 🚨 3 tín hiệu chẩn đoán KHÔNG được rơi rụng trên đường tới màn hình admin.
        Assert.True(dto.BiChanDoChuaCoNoiNhanDon);
        Assert.Equal("ChuaCoNoiNhanDon", dto.LyDoChan);
        Assert.Equal(1, dto.Errors);

        Assert.Equal(3, dto.OrdersPulled);
        Assert.True(dto.TokenRefreshed);
        Assert.Equal(batDau, dto.StartedAtUtc);
        Assert.Equal(batDau.AddSeconds(2), dto.FinishedAtUtc);
    }

    [Fact]
    public async Task SyncNowAsync_lo_don_binh_thuong_thi_KHONG_bao_bi_chan()
    {
        var shopId = Guid.NewGuid();

        var cong = new CongDongBoGia(new ChannelSyncResult
        {
            ChannelShopId = shopId,
            ChannelCode = "shopee",
            OrdersPulled = 5
        });

        var dto = await TaoAppService(cong).SyncNowAsync(shopId);

        Assert.False(dto.BiChanDoChuaCoNoiNhanDon);
        Assert.Null(dto.LyDoChan);
        Assert.Equal(0, dto.Errors);
        Assert.Equal(5, dto.OrdersPulled);
    }
}
