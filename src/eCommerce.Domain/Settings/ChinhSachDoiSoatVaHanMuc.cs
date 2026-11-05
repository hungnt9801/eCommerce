using Volo.Abp.Settings;

namespace eCommerce.Settings;

public class ChinhSachSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // 📌 Dùng **ctor 2 tham số** `(name, defaultValue)`: ABP 10.7 đòi `LocalizableString` ở tham số 3
        // (build đã bắt được: `CS1503` — *"cannot convert from 'string' to 'LocalizableString'"*).
        // ⇒ Mô tả tiếng Việt để ở **chú thích** thay vì tham số ⇒ **không** phụ thuộc API bản địa hoá.
        context.Add(
            // Đơn vị ngưỡng lệch đối soát (Percent|Absolute)
            new SettingDefinition(TenChinhSach.NguongDoiSoat_DonVi, "Percent"),
            // Giá trị ngưỡng lệch đối soát
            new SettingDefinition(TenChinhSach.NguongDoiSoat_GiaTri, "0.5"),
            // Khi phát hiện lệch (Warn|RequireConfirmation)
            new SettingDefinition(TenChinhSach.NguongDoiSoat_HanhDong, "Warn"),
            // Số ngày lưu trữ dòng lệch ĐÃ XỬ LÝ (0 = tắt retention). Mặc định 180.
            new SettingDefinition(TenChinhSach.LuuTruDoiSoat_SoNgay, "180"),
            // Đơn vị hạn mức theo kênh (Percent|Absolute)
            new SettingDefinition(TenChinhSach.HanMuc_DonVi, "Percent"),
            // Khi vượt hạn mức kênh (Block|Warn)
            new SettingDefinition(TenChinhSach.HanMuc_HanhDong, "Block"),
            // Phạm vi hạn mức (Channel|ChannelWarehouse)
            new SettingDefinition(TenChinhSach.HanMuc_PhamVi, "Channel"),
            // 🆕 SAL-003 (24/10/2026) — 3 setting của state machine đơn
            new SettingDefinition(TenChinhSach.DonHang_SoLanGiaoToiDa, "3"),
            new SettingDefinition(TenChinhSach.DonHang_CuaSoDoiTra_Ngay, "7"),
            new SettingDefinition(TenChinhSach.DonHang_SoLanThanhToanToiDa, "3"),
            // Hạn mức mặc định cho kênh chưa cấu hình (0 = không áp hạn mức)
            new SettingDefinition(TenChinhSach.HanMuc_GiaTriMacDinh, "0"));
    }
}