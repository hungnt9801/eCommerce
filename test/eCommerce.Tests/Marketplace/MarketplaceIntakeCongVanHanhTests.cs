using System.Reflection;
using eCommerce.Sales;
using Microsoft.AspNetCore.Authorization;

namespace eCommerce.Tests.Marketplace;

/// <summary>
/// 🚨 **CỔNG VẬN HÀNH CỦA ĐƠN SÀN** (`SAL-005` **§6.1** · `S6`) — bất biến khoá bằng **reflection**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO CÓ LỚP NÀY (đo được ở `S6`):** job `D12` **cố ý không** tự hạ cờ `GapSuspected`, nhưng
/// `DanhDauDaDoiSoat` từng **không có người gọi nào** ⇒ cờ chỉ hạ được bằng **SQL tay** ⇒ vế *"để cờ không
/// tồn đọng"* của `S6` mới đúng **một nửa**. Cổng này đóng nốt nửa kia — và ba bất biến dưới đây giữ cho nó
/// **không** biến thành một lỗ hổng mới:
/// <list type="number">
///   <item>🔐 **`[Authorize]` ở cấp CLASS** + **mỗi method có permission riêng** — thiếu ở class thì mọi method
///     không gắn gì thành API **công khai** (lỗi Phase 0 đã xảy ra thật với `GetShopsAsync`); permission khai
///     mà **không định nghĩa** ở provider thì `403` cho **mọi** người kể cả admin (`XC-004` D2);</item>
///   <item>🔒 **KHÔNG có đường đặt watermark** (BR-008): watermark **chỉ** tiến qua luật 4 nhánh. 📌 Một
///     `SetWatermarkAsync` là mở đường cho **ghi đè trạng thái mới hơn** — đúng thứ `D6` cấm, và nó **không**
///     làm đỏ bất kỳ test nào khác nếu chỉ kiểm hành vi;</item>
///   <item>📌 **Hai quyền phải KHÁC nhau**: `View` (đọc) và `Reprocess` (hạ cờ). Gộp lại là biến một thao tác
///     **xoá cảnh báo** thành quyền của người **chỉ-đọc**.</item>
/// </list>
///
/// ⚠️ **Giới hạn đã biết (nói thẳng):** reflection **không** chứng minh được API trả `200`/`403`/`404` đúng —
/// việc đó do **bước ⑫** của cổng đo trên **HTTP thật** (`C38`…`C46`). 📌 Hai tầng này **bổ sung**, không thay nhau.
/// </summary>
public class MarketplaceIntakeCongVanHanhTests
{
    private static readonly Type KieuAppService = typeof(eCommerce.Marketplace.MarketplaceIntakeAppService);
    private static readonly Type KieuHopDong = typeof(IMarketplaceIntakeAppService);

    [Fact]
    public void App_service_PHAI_co_Authorize_o_cap_CLASS()
    {
        var coAuthorize = KieuAppService.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();

        Assert.True(coAuthorize,
            "🚨 `MarketplaceIntakeAppService` THIẾU `[Authorize]` ở cấp CLASS ⇒ method nào không gắn gì sẽ trở " +
            "thành API CÔNG KHAI (lỗi Phase 0: `GetShopsAsync` từng công khai đúng như vậy).");
    }

    [Fact]
    public void Moi_method_PHAI_gan_permission_RIENG__va_hai_quyen_phai_KHAC_nhau()
    {
        var theoMethod = KieuAppService
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToDictionary(
                m => m.Name,
                m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    .Select(a => a.Policy)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList(),
                StringComparer.Ordinal);

        // 🔐 Mỗi method công khai phải có **policy** riêng (không dựa vào `[Authorize]` trần của class).
        var thieuPolicy = theoMethod.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();

        Assert.True(thieuPolicy.Count == 0,
            "🚨 Method KHÔNG gắn permission riêng (chỉ dựa vào class) ⇒ mọi người đã đăng nhập đều gọi được:\n  " +
            string.Join("\n  ", thieuPolicy));

        Assert.True(theoMethod.ContainsKey("GetIntakeAsync") && theoMethod.ContainsKey("MarkReconciledAsync"),
            "Thiếu method bắt buộc của hợp đồng (§6.1): GetIntakeAsync / MarkReconciledAsync");

        // 🔒 Đọc và hạ cờ phải là **hai quyền khác nhau**.
        var quyenDoc = theoMethod["GetIntakeAsync"].Single();
        var quyenHaCo = theoMethod["MarkReconciledAsync"].Single();

        Assert.Equal(eCommerceConsts.Permissions.MarketplaceIntake.View, quyenDoc);
        Assert.Equal(eCommerceConsts.Permissions.MarketplaceIntake.Reprocess, quyenHaCo);
        Assert.NotEqual(quyenDoc, quyenHaCo);
    }

    [Fact]
    public void KHONG_duoc_co_duong_DAT_WATERMARK_bang_tay()
    {
        // 🚨 BR-008 của `SAL-005`: watermark **chỉ** tiến qua luật 4 nhánh (`ApDungWatermark`).
        // 📌 Một API đặt watermark bằng tay = mở đường **ghi đè trạng thái mới hơn** (đúng thứ `D6` cấm),
        // và nó **không** làm đỏ test nào nếu chỉ kiểm hành vi — nên phải khoá bằng **sự tồn tại**.
        var nghiemTrong = new[] { "watermark", "setwater", "datnuoc", "datphienban", "resetwatermark" };

        var viPham = KieuHopDong
            .GetMethods()
            .Select(m => m.Name)
            .Where(ten => nghiemTrong.Any(t => ten.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(viPham.Count == 0,
            "🚨 Hợp đồng có phương thức sửa watermark — BR-008 CẤM (watermark chỉ tiến qua luật 4 nhánh):\n  " +
            string.Join("\n  ", viPham));

        // 📌 Khoá cả **số lượng**: hợp đồng chỉ được có **2** method. Thêm method mới phải là quyết định
        // có chủ ý (và phải cập nhật tài liệu §6.1), không phải lặng lẽ mở thêm bề mặt.
        Assert.Equal(2, KieuHopDong.GetMethods().Length);
    }

    [Fact]
    public void DTO_khong_duoc_lo_PayloadHash_cua_ban_DEN_GAN_NHAT()
    {
        // 📌 `sal-005` §17.3 #4: `PayloadHash` (bản **đến gần nhất**, có thể là bản bị bỏ qua) **KHÁC**
        // `AppliedPayloadHash` (bản **đang hiệu lực**). Trả cả hai mà không giải thích ⇒ người đọc tưởng có
        // **hai** mốc watermark. DTO chỉ được có bản **đã áp**.
        var ten = typeof(MarketplaceIntakeDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("AppliedPayloadHash", ten);
        Assert.DoesNotContain("PayloadHash", ten);
    }
}
