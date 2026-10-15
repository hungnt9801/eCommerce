using System.Reflection;
using eCommerce.ChannelHub.Sync;
using eCommerce.Channels;
using eCommerce.Messaging.EventCoverage;

namespace eCommerce.Tests.ChannelHubSync;

/// <summary>
/// **Test bất biến bằng reflection** — chặn lỗi #22 tái phát. Cùng họ với `XC-004` TC-01/TC-02
/// (test reflection đã cứu dự án khỏi 2 lỗi thật ở Phase 0).
///
/// 🎯 Vì sao phải là test REFLECTION (không phải test hành vi):
/// Cách sửa lỗi #22 dựa vào **cấu trúc phụ thuộc** (syncer **bắt buộc** có cổng dò người nhận;
/// app service **bắt buộc** đi qua cổng đồng bộ chung). Nếu ai đó *gỡ* cấu trúc đó ra — biến tham số
/// thành tuỳ chọn, hoặc tự kéo đơn trong app service — thì **không có test hành vi nào đỏ**, và lỗi
/// quay lại đúng dạng nguy hiểm nhất: **im lặng**.
/// </summary>
public class BatBienChongLoi22Tests
{
    private static readonly Type KieuProbe = typeof(IEventCoverageProbe);
    private static readonly Type KieuCongDongBo = typeof(IChannelShopSyncService);

    [Fact]
    public void ChannelShopSyncer_PHAI_nhan_IEventCoverageProbe_va_la_tham_so_BAT_BUOC()
    {
        var ctor = Assert.Single(typeof(ChannelShopSyncer).GetConstructors());

        var thamSo = ctor.GetParameters().SingleOrDefault(p => p.ParameterType == KieuProbe);

        Assert.True(
            thamSo is not null,
            "🚨 `ChannelShopSyncer` KHÔNG còn nhận `IEventCoverageProbe` ⇒ không còn cách biết sự kiện " +
            "`ChannelOrderPulledEto` có người nhận hay chưa ⇒ quay lại hành vi publish cho không ai rồi " +
            "TIẾN CON TRỎ ĐỒNG BỘ ⇒ MẤT ĐƠN VĨNH VIỄN (lỗi #22).");

        // 🚨 BẤT BIẾN QUAN TRỌNG NHẤT: KHÔNG được là tham số tuỳ chọn.
        // Nếu để `= null` thì một lần quên truyền là hệ thống **âm thầm** quay lại hành vi làm mất đơn.
        Assert.False(
            thamSo!.IsOptional,
            "🚨 `IEventCoverageProbe` bị đổi thành tham số TUỲ CHỌN ⇒ quên truyền sẽ âm thầm mất đơn.");
        Assert.False(
            thamSo.HasDefaultValue,
            "🚨 `IEventCoverageProbe` có giá trị mặc định ⇒ quên truyền sẽ âm thầm mất đơn.");
    }

    [Fact]
    public void ChannelShopSyncer_phai_hien_thuc_IChannelShopSyncService()
    {
        // Cổng này là thứ buộc tầng app dùng CHUNG một đường đồng bộ.
        Assert.True(
            KieuCongDongBo.IsAssignableFrom(typeof(ChannelShopSyncer)),
            "🚨 `ChannelShopSyncer` không còn hiện thực `IChannelShopSyncService` ⇒ tầng app không còn " +
            "cách nào đi qua đường đồng bộ chung ⇒ hai đường đồng bộ song song quay lại (biến thể #22).");
    }

    [Fact]
    public void ChannelAppService_PHAI_di_qua_cong_dong_bo_chung()
    {
        // 🚨 Biến thể thứ hai của lỗi #22 nằm ở nút "Đồng bộ ngay": nó từng TỰ kéo đơn rồi TỰ tiến con trỏ
        // mà KHÔNG phát sự kiện cho ai. Bất biến: app service phải phụ thuộc cổng đồng bộ chung.
        var ctor = Assert.Single(typeof(ChannelAppService).GetConstructors());

        Assert.True(
            ctor.GetParameters().Any(p => p.ParameterType == KieuCongDongBo),
            "🚨 `ChannelAppService` không còn dùng `IChannelShopSyncService` ⇒ nút \"Đồng bộ ngay\" có thể " +
            "lại tự kéo đơn và tự tiến con trỏ mà không bàn giao cho ai ⇒ MẤT ĐƠN (biến thể #22).");
    }

    [Fact]
    public void Chi_co_MOT_kieu_hien_thuc_cong_dong_bo()
    {
        // Nếu có nhiều bản cài đặt, hành vi fail-closed có thể bị vô hiệu ở một nhánh nào đó.
        // Phạm vi quét CỐ Ý hẹp và tường minh (2 assembly có thể chứa bản cài đặt) để test **ổn định** —
        // quét theo `GetReferencedAssemblies()` sẽ phụ thuộc thứ tự nạp và dễ hỏng vì lý do không liên quan.
        var cacBan = new[] { typeof(ChannelShopSyncer).Assembly, typeof(ChannelAppService).Assembly }
            .Distinct()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t is not null).Select(t => t!);
                }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(KieuCongDongBo.IsAssignableFrom)
            .OrderBy(t => t.FullName ?? t.Name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal([typeof(ChannelShopSyncer)], cacBan);
    }
}
