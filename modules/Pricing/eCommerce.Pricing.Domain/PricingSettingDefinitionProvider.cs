using Volo.Abp.Settings;

namespace eCommerce.Pricing;

/// <summary>
/// Khai báo **4 Setting của module Pricing** (`PRC-002` WP-16). ABP **tự đăng ký** mọi
/// `SettingDefinitionProvider` theo quy ước (tiền lệ: `ChinhSachSettingDefinitionProvider` ở
/// `src/eCommerce.Domain/Settings`) ⇒ chỉ cần có lớp này là setting **tồn tại** với giá trị mặc định
/// (không cần migration: bảng `AbpSettings` đã có).
///
/// 📌 Phạm vi **`Application | Tenant`** (`D-218`): vừa cho phép giá trị host-level (`SetGlobalAsync`) vừa theo
/// tenant. ABP 10.7 **không còn enum `SettingScopes`** — phạm vi được khai qua `WithProviders(...)`: chỉ định
/// provider **mặc định (D)**, **cấu hình (C)**, **global (G)** và **tenant (T)**, **LOẠI trừ user (U)** để chính
/// sách **không** bị ghi đè theo từng người dùng.
/// </summary>
public class PricingSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        var phamVi = new[]
        {
            DefaultValueSettingValueProvider.ProviderName,
            ConfigurationSettingValueProvider.ProviderName,
            GlobalSettingValueProvider.ProviderName,
            TenantSettingValueProvider.ProviderName
        };

        context.Add(
            // Mã chính sách làm tròn (D-221: chỉ half-away-from-zero)
            new SettingDefinition(PRC002SettingTenKhoa.ChinhSachLamTron, "half-away-from-zero").WithProviders(phamVi),
            // Chính sách khi thiếu giá (NemLoi | DungGiaNiemYet)
            new SettingDefinition(PRC002SettingTenKhoa.ThieuGia, "NemLoi").WithProviders(phamVi),
            // Số bậc tối đa mỗi bộ bậc thang (D-224)
            new SettingDefinition(PRC002SettingTenKhoa.SoBacToiDa, "20").WithProviders(phamVi),
            // Chính sách khi tổng đơn âm (Reject | Clamp) — D-219
            new SettingDefinition(PRC002SettingTenKhoa.TongDonAm, "Reject").WithProviders(phamVi),

            // ── 🆕 `W17-2` — 4 khoá SETTING ĐỘNG CƠ GIÁ VỐN (`PRC-001-arch` §5.2 · `D-323`) ─────────────
            // 🚨 THIẾU định nghĩa ⇒ `ISettingProvider` trả `null` ⇒ động cơ fail-closed KHÔNG chạy như mô tả
            // (R6). Mặc định an toàn: chỉ BQGG di động · không theo kho · không nhập lùi · động cơ TẮT.
            new SettingDefinition(
                PRC001SettingTenKhoa.PhuongPhapGiaVon,
                PricingChinhSach.PhuongPhapBinhQuanGiaQuyenDiDong).WithProviders(phamVi),
            new SettingDefinition(PRC001SettingTenKhoa.GiaVonTheoKho, "false").WithProviders(phamVi),
            new SettingDefinition(PRC001SettingTenKhoa.ChoNhapLuiNgay, "false").WithProviders(phamVi),
            // 📌 Mặc định `false` = **TẮT** — đường lùi rẻ nhất khi động cơ gây sự cố (`PRC-001-arch` §4.3).
            new SettingDefinition(PRC001SettingTenKhoa.GiaVonDongCo, "false").WithProviders(phamVi));
    }
}
