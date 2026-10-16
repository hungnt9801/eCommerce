using Volo.Abp.Settings;

namespace eCommerce.Settings;

/// <summary>
/// 🚨 **TÊN + GIÁ TRỊ HỢP LỆ của các CHÍNH SÁCH "CHO PHÉP CHỌN"** (`INV-003` · `REC-001`).
///
/// 📌 **VÌ SAO "CHO PHÉP CHỌN" thay vì hard-code** (yêu cầu của người quyết định, 16/10/2026):
/// nghiệp vụ **chưa chốt** một con số/chính sách duy nhất ⇒ mã hoá **cứng** một lựa chọn sẽ buộc phải **sửa mã + deploy**
/// mỗi lần đổi ý. Đưa thành **setting** ⇒ đổi **không cần deploy**, và **log ra chính sách đang chạy** (để phép kiểm chứng thấy được).
///
/// 🔒 **Hạ tầng có sẵn:** `AbpSettings` + `AbpSettingDefinitions` **đã tồn tại** trong DB ⇒ **KHÔNG cần migration**.
/// </summary>
public static class TenChinhSach
{
    // ── REC-001: ĐỐI SOÁT TỒN KHO ────────────────────────────────────────────────────────────────
    /// <summary>Ngưỡng lệch tính theo gì: `Percent` (phần trăm) hay `Absolute` (số tuyệt đối).</summary>
    public const string NguongDoiSoat_DonVi = "Recon.Threshold.Unit";

    /// <summary>Giá trị ngưỡng (diễn giải theo <see cref="NguongDoiSoat_DonVi"/>).</summary>
    public const string NguongDoiSoat_GiaTri = "Recon.Threshold.Value";

    /// <summary>Khi phát hiện lệch thì làm gì: `Warn` (chỉ cảnh báo) hay `RequireConfirmation` (chờ người xác nhận).</summary>
    public const string NguongDoiSoat_HanhDong = "Recon.Mismatch.Action";

    // ── INV-003: HẠN MỨC CỨNG THEO KÊNH ─────────────────────────────────────────────────────────
    /// <summary>Hạn mức theo kênh tính theo gì: `Percent` (phần trăm tồn khả dụng) hay `Absolute` (số tuyệt đối).</summary>
    public const string HanMuc_DonVi = "Inv.ChannelLimit.Unit";

    /// <summary>Vượt hạn mức thì làm gì: `Block` (chặn) hay `Warn` (cho qua + cảnh báo).</summary>
    public const string HanMuc_HanhDong = "Inv.ChannelLimit.Action";

    /// <summary>Phạm vi áp hạn mức: `Channel` (theo kênh) hay `ChannelWarehouse` (theo kênh **và** kho).</summary>
    public const string HanMuc_PhamVi = "Inv.ChannelLimit.Scope";

    /// <summary>Giá trị hạn mức **mặc định** cho kênh **chưa** cấu hình riêng (`0` = **không** áp hạn mức).</summary>
    public const string HanMuc_GiaTriMacDinh = "Inv.ChannelLimit.DefaultValue";

    public static readonly string[] DonViHopLe = { "Percent", "Absolute" };
    public static readonly string[] HanhDongLechHopLe = { "Warn", "RequireConfirmation" };
    public static readonly string[] HanhDongHanMucHopLe = { "Block", "Warn" };
    public static readonly string[] PhamViHanMucHopLe = { "Channel", "ChannelWarehouse" };
}

/// <summary>Đơn vị tính ngưỡng lệch (`REC-001`).</summary>
public enum DonViNguong
{
    /// <summary>Phần trăm trên tổng tồn (`OnHand`).</summary>
    Percent,

    /// <summary>Số lượng tuyệt đối.</summary>
    Absolute
}

/// <summary>Việc cần làm khi phát hiện lệch (`REC-001`).</summary>
public enum HanhDongLech
{
    /// <summary>Chỉ ghi cảnh báo + metric — **không** tự sửa dữ liệu.</summary>
    Warn,

    /// <summary>Đánh dấu **cần người xác nhận** trước khi điều chỉnh (không tự sửa).</summary>
    RequireConfirmation
}

/// <summary>Đơn vị tính hạn mức kênh (`INV-003`).</summary>
public enum DonViHanMuc
{
    /// <summary>Phần trăm trên **tồn khả dụng** (`OnHand - Reserved`) của kênh.</summary>
    Percent,

    /// <summary>Số lượng tuyệt đối.</summary>
    Absolute
}

/// <summary>Việc cần làm khi vượt hạn mức (`INV-003`).</summary>
public enum HanhDongHanMuc
{
    /// <summary>**Chặn** thao tác giữ chỗ (fail-closed).</summary>
    Block,

    /// <summary>Cho qua nhưng **ghi cảnh báo** (fail-open có tiếng nói).</summary>
    Warn
}

/// <summary>Phạm vi áp hạn mức (`INV-003`).</summary>
public enum PhamViHanMuc
{
    /// <summary>Hạn mức theo **kênh** (mọi kho cộng lại).</summary>
    Channel,

    /// <summary>Hạn mức theo **kênh + kho** (chặt hơn).</summary>
    ChannelWarehouse
}

/// <summary>
/// 🚨 **ĐỌC CHÍNH SÁCH — HÀM THUẦN** (không DB, không side effect) ⇒ **kiểm được bằng unit test**.
///
/// 🔒 **Nguyên tắc "dữ liệu cấu hình cũng là dữ liệu vào"** (bài học §88.3 · §90.2): setting có thể bị sửa thành **giá trị lạ**
/// ⇒ hàm đọc **KHÔNG ném**, mà trả **giá trị mặc định an toàn** (đồng thời trả cờ `hopLe = false` để chỗ gọi **ghi cảnh báo**).
/// 📌 Nếu không làm vậy, một giá trị gõ sai trong cấu hình sẽ làm **hỏng job/đường đặt hàng** thay vì chỉ **dùng mặc định**.
/// </summary>
public static class ChinhSach
{
    /// <summary>Đọc ngưỡng lệch (đơn vị) — mặc định **`Percent`**; giá trị lạ ⇒ mặc định + `hopLe = false`.</summary>
    public static DonViNguong DocDonViNguong(string? giaTriTho, out bool hopLe)
    {
        var giaTri = (giaTriTho ?? string.Empty).Trim();

        if (string.Equals(giaTri, "Absolute", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return DonViNguong.Absolute; }
        if (string.Equals(giaTri, "Percent", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return DonViNguong.Percent; }

        hopLe = false;
        return DonViNguong.Percent;
    }

    /// <summary>Đọc việc-cần-làm khi lệch — mặc định **`Warn`** (an toàn: **không** tự sửa dữ liệu).</summary>
    public static HanhDongLech DocHanhDongLech(string? giaTriTho, out bool hopLe)
    {
        var giaTri = (giaTriTho ?? string.Empty).Trim();

        if (string.Equals(giaTri, "RequireConfirmation", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return HanhDongLech.RequireConfirmation; }
        if (string.Equals(giaTri, "Warn", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return HanhDongLech.Warn; }

        hopLe = false;
        return HanhDongLech.Warn;
    }

    /// <summary>Đọc đơn vị hạn mức kênh — mặc định **`Percent`**.</summary>
    public static DonViHanMuc DocDonViHanMuc(string? giaTriTho, out bool hopLe)
    {
        var giaTri = (giaTriTho ?? string.Empty).Trim();

        if (string.Equals(giaTri, "Absolute", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return DonViHanMuc.Absolute; }
        if (string.Equals(giaTri, "Percent", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return DonViHanMuc.Percent; }

        hopLe = false;
        return DonViHanMuc.Percent;
    }

    /// <summary>Đọc việc-cần-làm khi vượt hạn mức — mặc định **`Block`** (**fail-closed**: không bán quá hạn mức).</summary>
    public static HanhDongHanMuc DocHanhDongHanMuc(string? giaTriTho, out bool hopLe)
    {
        var giaTri = (giaTriTho ?? string.Empty).Trim();

        if (string.Equals(giaTri, "Warn", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return HanhDongHanMuc.Warn; }
        if (string.Equals(giaTri, "Block", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return HanhDongHanMuc.Block; }

        hopLe = false;
        return HanhDongHanMuc.Block;
    }

    /// <summary>Đọc phạm vi hạn mức — mặc định **`Channel`**.</summary>
    public static PhamViHanMuc DocPhamViHanMuc(string? giaTriTho, out bool hopLe)
    {
        var giaTri = (giaTriTho ?? string.Empty).Trim();

        if (string.Equals(giaTri, "ChannelWarehouse", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return PhamViHanMuc.ChannelWarehouse; }
        if (string.Equals(giaTri, "Channel", StringComparison.OrdinalIgnoreCase)) { hopLe = true; return PhamViHanMuc.Channel; }

        hopLe = false;
        return PhamViHanMuc.Channel;
    }

    /// <summary>Đọc một con số cấu hình — **âm / không phải số / rỗng** ⇒ dùng `macDinh` (không ném).
    ///
    /// 🚨 **CHỈ cho dấu CHẤM thập phân và khoảng trắng — KHÔNG cho dấu phẩy.** Lý do là một lỗi **thật** đã bị test bắt:
    /// ``NumberStyles.Number`` coi ``,`` là **dấu phân nhóm NGHÌN** ⇒ ``"0,5"`` (người Việt gõ, ý là **0,5 %**) bị đọc thành **5**
    /// — tức ngưỡng cảnh báo **sai 10 lần** mà **không** có lỗi nào nổi lên. 📌 Chọn **từ chối** (dùng mặc định + cờ `hopLe = false`)
    /// thay vì **đoán** ý người gõ: đoán sai ở đây **im lặng**, còn từ chối thì chỗ gọi **ghi cảnh báo**.</summary>
    public static decimal DocSo(string? giaTriTho, decimal macDinh, out bool hopLe)
    {
        const System.Globalization.NumberStyles KieuChoPhep =
            System.Globalization.NumberStyles.AllowDecimalPoint
            | System.Globalization.NumberStyles.AllowLeadingWhite
            | System.Globalization.NumberStyles.AllowTrailingWhite;

        if (decimal.TryParse((giaTriTho ?? string.Empty).Trim(), KieuChoPhep, System.Globalization.CultureInfo.InvariantCulture, out var so) && so >= 0)
        {
            hopLe = true;
            return so;
        }

        hopLe = false;
        return macDinh;
    }
}

/// <summary>
/// 🚨 **KHAI BÁO CÁC CHÍNH SÁCH** — ABP **tự đăng ký** mọi `SettingDefinitionProvider` (đăng ký quy ước)
/// ⇒ chỉ cần có lớp này là setting **tồn tại** với giá trị mặc định (không cần migration: `AbpSettings` đã có).
/// 📌 Giá trị `DefaultValue` ở đây là **mặc định an toàn** — chọn theo nguyên tắc *"mặc định phải là lựa chọn ÍT RỦI RO NHẤT"*:
/// `Warn` (không tự sửa dữ liệu) và `Block` (không bán quá hạn mức).
/// </summary>
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
            // Đơn vị hạn mức theo kênh (Percent|Absolute)
            new SettingDefinition(TenChinhSach.HanMuc_DonVi, "Percent"),
            // Khi vượt hạn mức kênh (Block|Warn)
            new SettingDefinition(TenChinhSach.HanMuc_HanhDong, "Block"),
            // Phạm vi hạn mức (Channel|ChannelWarehouse)
            new SettingDefinition(TenChinhSach.HanMuc_PhamVi, "Channel"),
            // Hạn mức mặc định cho kênh chưa cấu hình (0 = không áp hạn mức)
            new SettingDefinition(TenChinhSach.HanMuc_GiaTriMacDinh, "0"));
    }
}
