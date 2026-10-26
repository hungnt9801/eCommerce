namespace eCommerce.Settings;

/// <summary>
/// 🚨 **TÍNH HẠN MỨC THEO KÊNH — HÀM THUẦN** (`INV-003`, phần **cưỡng chế**).
///
/// 📌 **VÌ SAO TÁCH RIÊNG KHỎI DỊCH VỤ:** đây là **số học nghiệp vụ** (phần trăm vs tuyệt đối, biên, làm tròn) —
/// chỗ **dễ sai** và **không cần DB** ⇒ tách ra để **kiểm được bằng unit test** trước khi tin nó trong đường đặt hàng.
/// 🔒 Hàm **không ném**, **không side effect**, chỉ trả **quyết định**.
///
/// ⚠️ **PHẠM VI v2 (ghi rõ, không giấu):**
///   • ✅ hạn mức lấy từ **bảng RIÊNG TỪNG KÊNH** (`InvChannelLimits`) — xem <see cref="ChonHanMuc"/>; chỉ khi
///     kênh **chưa** cấu hình mới rơi về giá trị **dùng chung** (`Inv.ChannelLimit.DefaultValue`);
///   • **`Percent`** = phần trăm của **tồn khả dụng CỦA TỪNG `(kho, SKU)`** tại thời điểm giữ chỗ — không phải tổng của cả kênh
///     (tồn kho chưa mô hình hoá "theo kênh"; đây là **giới hạn đã biết**, ghi ở `web-001` §94);
///   • ✅ **`PhamVi`** (`Channel` | `ChannelWarehouse`) **nay CÓ tác dụng**: quyết định dòng `(kênh, kho)` có được
///     xét hay không (xem <see cref="ChonHanMuc"/>) — không còn là "ô chọn không đổi gì".
/// </summary>
/// <summary>
/// 🔑 **NGUỒN TỐI THIỂU ĐỂ CHIẾU SANG <see cref="HanMucKenh.DongHanMuc"/>.**
///
/// 📌 **VÌ SAO LÀ GIAO DIỆN (không nhận thẳng entity `InvChannelLimit`):** `eCommerce.Tests`
/// **KHÔNG** tham chiếu `eCommerce.Domain` (layering — `Application.Contracts` không được tham chiếu
/// `Domain`, và test đi theo cùng khuôn). 🔒 Nhận **entity** ⇒ hàm thuần **không test được** — mà
/// **không test được** chính là lý do phép chiếu này từng nằm inline trong một hàm `async` chạm 4 repository.
/// ✅ Giao diện này chỉ có **đúng 5 thuộc tính** cần cho việc chọn hạn mức ⇒ test **cài đặt nó bằng một
/// `record` 5 dòng**, không cần DB, không cần mock.
/// ⚠️ **Đánh đổi đã biết:** thêm một giao diện cho một phép chiếu là hơi thừa về mặt kỹ thuật — 📌 nhưng nó
/// **mua được** khả năng test cho **đúng chỗ** mà `DonVi` có thể bị đánh rơi (xem chú thích `ChieuDongHanMuc`).
/// </summary>
public interface IDongHanMucNguon
{
    Guid Id { get; }
    Guid? WarehouseId { get; }
    decimal GiaTri { get; }
    HanhDongHanMuc HanhDong { get; }
    DonViHanMuc? DonVi { get; }
}

public static class HanMucKenh
{
    /// <summary>
    /// Tính **hạn mức số lượng** được giữ cho một `(kho, SKU)`.
    /// <list type="bullet">
    ///   <item><see cref="DonViHanMuc.Absolute"/>: hạn mức = `giaTri` (số lượng).</item>
    ///   <item><see cref="DonViHanMuc.Percent"/>: hạn mức = `floor(tồnKhảDụng × giaTri / 100)` — **làm tròn XUỐNG**
    ///   (⚠️ cố ý: làm tròn **lên** sẽ cho phép giữ **nhiều hơn** tỷ lệ đã chọn ⇒ phá chính chính sách).</item>
    /// </list>
    /// 🔒 `giaTri &lt;= 0` ⇒ **0** ⇒ nghĩa là **KHÔNG áp hạn mức** (quy ước: `DefaultValue = 0` là **tắt**).
    /// </summary>
    public static int TinhHanMuc(DonViHanMuc donVi, decimal giaTri, int tonKhaDung)
    {
        if (giaTri <= 0) { return 0; }

        if (donVi == DonViHanMuc.Absolute) { return (int)decimal.Truncate(giaTri); }

        if (tonKhaDung <= 0) { return 0; }

        var hanMuc = decimal.Truncate(tonKhaDung * giaTri / 100m);

        return (int)hanMuc;
    }

    /// <summary>
    /// Quyết định **có vượt hạn mức** không. 🔒 `hanMuc &lt;= 0` ⇒ **KHÔNG** áp hạn mức ⇒ **không** bao giờ vượt
    /// (đây là đường "tắt chính sách" **duy nhất**, để không ai tình cờ chặn hết hàng).
    /// </summary>
    public static bool VuotHanMuc(int soLuongYeuCau, int hanMuc)
    {
        if (hanMuc <= 0) { return false; }

        return soLuongYeuCau > hanMuc;
    }

    /// <summary>
    /// **Có nên CHẶN không** — kết hợp *vượt hạn mức* **và** *hành động đã chọn*.
    /// 🔒 `Warn` ⇒ **KHÔNG** chặn (cho qua + ghi cảnh báo) — theo đúng lựa chọn `Inv.ChannelLimit.Action`.
    /// </summary>
    public static bool NenChan(HanhDongHanMuc hanhDong, int soLuongYeuCau, int hanMuc)
        => hanhDong == HanhDongHanMuc.Block && VuotHanMuc(soLuongYeuCau, hanMuc);

    /// <summary>
    /// 🚨 **CẢNH BÁO "HẠN MỨC VÔ NGHĨA"** — cấu hình **CÓ Ý ĐỊNH** áp hạn mức (`giaTri &gt; 0`) nhưng hạn mức tính ra **`0`**
    /// (vì `Percent` **làm tròn xuống** trên tồn khả dụng nhỏ) ⇒ theo quy ước `0` = **TẮT**, chính sách sẽ chạy **như không cấu hình**.
    ///
    /// 📌 **VÌ SAO PHẢI PHÁT HIỆN:** đây đúng loại **"tắt im lặng"** mà dự án đã trả giá nhiều lần (``CHN-002`` · ``inv-001`` §27):
    /// người vận hành đặt ``50%``, thấy **không có gì bị chặn**, rồi **tin** rằng hạn mức đang hoạt động.
    /// 🔒 Hàm này **không** đổi hành vi (vẫn `0` = tắt) — nó chỉ **nói ra** để chỗ gọi **ghi cảnh báo**;
    /// quyết định *"chặn hay không"* vẫn thuộc `Action`, **không** bị hàm này lén đổi.
    /// </summary>
    public static bool HanMucVoNghia(DonViHanMuc donVi, decimal giaTri, int tonKhaDung)
        => giaTri > 0 && TinhHanMuc(donVi, giaTri, tonKhaDung) <= 0;

    // ── `INV-003` v2 — CHỌN HẠN MỨC TỪ BẢNG RIÊNG TỪNG KÊNH ────────────────────────────────────────

    /// <summary>Dòng hạn mức **đã đọc từ DB** (dạng rút gọn) để <see cref="ChonHanMuc"/> là **hàm THUẦN**.</summary>
    /// <param name="DonVi">Đơn vị **của RIÊNG dòng này**; `null` = **kế thừa** setting dùng chung.
    /// 📌 Đặt ở record này (chứ không tra DB lúc chạy) để `ChonHanMuc` vẫn là **hàm THUẦN** — cùng lý do
    /// đã ghi ở chú thích `DongHanMuc` gốc.</param>
    public sealed record DongHanMuc(Guid Id, Guid? WarehouseId, decimal GiaTri, HanhDongHanMuc HanhDong, DonViHanMuc? DonVi = null);
    /// <summary>
    /// 🔑 **CHIẾU ENTITY `InvChannelLimit` → `DongHanMuc`** — hàm **THUẦN**, và **tồn tại để test được**.
    ///
    /// ══════════════════════════════════════════════════════════════════════════════════════════════════
    /// 🚨 **VÌ SAO TÁCH RA (đây KHÔNG phải chi tiết kỹ thuật — đây là chỗ BUG ẨN NẤP):**
    ///
    /// Phép chiếu này là **chỗ DUY NHẤT** mà `DonVi` đi từ **entity** sang **luật chọn**. 🔒 Nếu nó đánh rơi
    /// `DonVi` (ví dụ ai đó rút gọn còn 4 tham số), thì **mọi** dòng đều thành *"kế thừa setting"* —
    /// 🚨 và hậu quả **không có lỗi nào**: hạn mức `200 chiếc` bị đọc thành `200%` ⇒ **không chặn gì cả**.
    ///
    /// 📌 Trước khi tách, phép chiếu nằm **inline trong `StockReservationService.GiuChoAsync`** — một hàm
    /// `async` chạm **4 repository + khoá phân tán + `ISettingProvider`** ⇒ **không** test được nếu không có
    /// **mock/DB**. 🔑 Luật đã trả giá nhiều lần trong dự án: **tách lõi quyết định ra hàm thuần ⇒ có test
    /// cho MỌI nhánh** (`CAT-003` `SinhSlug` · `SAL-005` `LuatWatermark` · `INV-003` `ChonHanMuc`).
    /// ══════════════════════════════════════════════════════════════════════════════════════════════════
    /// </summary>
    /// <param name="cacDong">Các entity hạn mức **của đúng kênh** đã đọc từ `InvChannelLimits`.</param>
    public static List<DongHanMuc> ChieuDongHanMuc(IEnumerable<IDongHanMucNguon> cacDong)
    {
        ArgumentNullException.ThrowIfNull(cacDong);

        return cacDong
            .Select(x => new DongHanMuc(x.Id, x.WarehouseId, x.GiaTri, x.HanhDong, x.DonVi))
            .ToList();
    }

    /// <summary>Nguồn của hạn mức đang áp — 🔑 **phải ghi được vào log/DB**, nếu không thì *"vì sao đơn bị chặn"* không trả lời được.</summary>
    public enum NguonHanMuc
    {
        /// <summary>Dòng `(kênh, kho)` trong `InvChannelLimits` — chặt nhất, ưu tiên cao nhất.</summary>
        BangTheoKenhVaKho,

        /// <summary>Dòng `(kênh, mọi kho)` trong `InvChannelLimits`.</summary>
        BangTheoKenh,

        /// <summary>Không có dòng nào cho kênh ⇒ dùng `Inv.ChannelLimit.DefaultValue`.</summary>
        MacDinhChung
    }

    /// <summary>Kết quả chọn hạn mức: **đã trộn** mọi nguồn thành **MỘT** con số + **MỘT** hành động + **nguồn**.</summary>
    public sealed record KetQuaHanMuc(
        PhamViHanMuc PhamVi,
        NguonHanMuc Nguon,
        Guid? DongId,
        decimal GiaTri,
        DonViHanMuc DonVi,
        HanhDongHanMuc HanhDong);

    /// <summary>
    /// 🎯 **CHỌN HẠN MỨC ĐANG ÁP** cho một `(kênh, kho)` — hàm **THUẦN** (không DB, không side effect).
    ///
    /// **LUẬT CHỌN (thứ tự ưu tiên từ chặt tới lỏng):**
    /// <list type="number">
    ///   <item><b>`ChannelWarehouse`</b> (phạm vi chặt): dòng có **đúng** `WarehouseId` thắng → nếu không có thì dòng `WarehouseId = null` → nếu vẫn không có thì mặc định chung.</item>
    ///   <item><b>`Channel`</b> (phạm vi lỏng): **CHỈ** xét dòng `WarehouseId = null`; dòng theo kho bị **bỏ qua có chủ ý**
    ///     (trả về trong <c>boQua</c> để chỗ gọi **ghi cảnh báo** — nếu bỏ qua **im lặng** thì người vận hành
    ///     đã cấu hình hạn mức theo kho mà **không hiểu vì sao nó không có tác dụng**).</item>
    /// </list>
    /// 🔒 **Cấm "nhiều dòng cùng khớp" là BẤT KHẢ THI ở DB** (unique index `NULLS NOT DISTINCT` trên
    /// `(TenantId, ChannelCode, WarehouseId)`) ⇒ hàm này **không** phải chỗ chống trùng.
    /// 📌 Nhưng vì dữ liệu có thể đến từ **cache/ảnh chụp cũ** hoặc từ test, hàm **vẫn** chọn **tất định**:
    /// nhiều dòng cùng khớp ⇒ lấy **giá trị NHỎ NHẤT** (chặt nhất), rồi tới `Id` nhỏ nhất — **không** phụ thuộc
    /// thứ tự đầu vào (nếu lấy "dòng đầu tiên" thì kết quả phụ thuộc thứ tự DB trả về ⇒ cùng dữ liệu, hai kết quả).
    /// </summary>
    /// <param name="phamVi">Phạm vi đang cấu hình (`Inv.ChannelLimit.Scope`).</param>
    /// <param name="cacDong">Các dòng hạn mức **của đúng kênh này** đã đọc từ `InvChannelLimits`.</param>
    /// <param name="warehouseId">Kho đang giữ chỗ.</param>
    /// <param name="donVi">Đơn vị **dùng chung** (`Inv.ChannelLimit.Unit`) — không phải thuộc tính từng dòng (ghi rõ ở `InvChannelLimit`).</param>
    /// <param name="giaTriMacDinh">Giá trị mặc định dùng chung (`Inv.ChannelLimit.DefaultValue`).</param>
    /// <param name="hanhDongMacDinh">Hành động mặc định dùng chung (`Inv.ChannelLimit.Action`).</param>
    /// <param name="boQua">Các dòng bị **bỏ qua do phạm vi** ⇒ chỗ gọi dùng để ghi cảnh báo (KHÔNG im lặng).</param>
    public static KetQuaHanMuc ChonHanMuc(
        PhamViHanMuc phamVi,
        IEnumerable<DongHanMuc> cacDong,
        Guid warehouseId,
        DonViHanMuc donVi,
        decimal giaTriMacDinh,
        HanhDongHanMuc hanhDongMacDinh,
        out IReadOnlyList<DongHanMuc> boQua)
    {
        ArgumentNullException.ThrowIfNull(cacDong);

        var tatCa = cacDong as IReadOnlyList<DongHanMuc> ?? cacDong.ToList();
        var biBoQua = new List<DongHanMuc>();

        var ungVien = new List<DongHanMuc>();

        if (phamVi == PhamViHanMuc.ChannelWarehouse)
        {
            // ① Đúng (kênh, kho) — chặt nhất.
            ungVien.AddRange(tatCa.Where(x => x.WarehouseId == warehouseId));

            // ② Không có dòng theo kho ⇒ rơi về dòng cả kênh.
            if (ungVien.Count == 0)
            {
                ungVien.AddRange(tatCa.Where(x => x.WarehouseId is null));
            }
        }
        else
        {
            ungVien.AddRange(tatCa.Where(x => x.WarehouseId is null));

            // 🚨 Dòng theo KHO bị bỏ qua ⇒ phải TRẢ RA để chỗ gọi ghi cảnh báo (không bỏ qua im lặng).
            biBoQua.AddRange(tatCa.Where(x => x.WarehouseId is not null));
        }

        boQua = biBoQua;

        if (ungVien.Count == 0)
        {
            return new KetQuaHanMuc(phamVi, NguonHanMuc.MacDinhChung, null, giaTriMacDinh, donVi, hanhDongMacDinh);
        }

        // 🔒 Chọn TẤT ĐỊNH khi có nhiều dòng cùng khớp (xem chú thích): giá trị nhỏ nhất (chặt nhất) rồi `Id` nhỏ nhất.
        var chon = ungVien
            .OrderBy(x => x.GiaTri)
            .ThenBy(x => x.Id)
            .First();

        var nguon = chon.WarehouseId is null ? NguonHanMuc.BangTheoKenh : NguonHanMuc.BangTheoKenhVaKho;

        // 🔑 **ĐƠN VỊ HIỆU LỰC CỦA DÒNG = `DongVi` của dòng ĐÓ, rơi về setting chung khi `null`.**
        // 📌 Đặt ngay cạnh việc chọn dòng (không ở chỗ gọi) để **không** có đường nào lấy `GiaTri` của dòng
        // mà lại lấy `DonVi` của setting — 🚨 cặp sai đó đọc `100` (chiếc) thành `100%` và **không có lỗi nào**.
        return new KetQuaHanMuc(phamVi, nguon, chon.Id, chon.GiaTri, chon.DonVi ?? donVi, chon.HanhDong);
    }
}
