namespace eCommerce.Kernel.Data;

/// <summary>
/// **HÀM THUẦN: ánh xạ VI PHẠM RÀNG BUỘC DB → LỖI NGHIỆP VỤ** (dùng cho exception filter).
///
/// 🚨 **VÌ SAO CẦN (nợ đã đo được, không phải phòng xa):** PostgreSQL trả `23505` (vi phạm duy nhất) /
/// `23514` (vi phạm `CHECK`) khi một **ràng buộc thật sự chặn** một hành vi **hợp lệ về nghiệp vụ**
/// (retry, double-click, job chạy lại). Nếu để nguyên, ABP trả **`500`** ⇒ client **KHÔNG phân biệt được**
/// *"đã làm rồi"* với *"hệ thống hỏng"* ⇒ **client sẽ retry** ⇒ tạo thêm tải và có thể thành **vòng lặp**.
/// ⇒ Đã đo **`500` ở 3 chỗ** trước khi làm phép ánh xạ này (sổ cái, giữ chỗ trùng nguồn, và ca hai request đồng thời).
///
/// 📌 **VÌ SAO ÁNH XẠ THEO *TÊN RÀNG BUỘC* (không theo bảng):** một bảng có thể có nhiều ràng buộc duy nhất
/// mang **ý nghĩa nghiệp vụ khác nhau** (`InvStockLedger` chặn *"ghi sổ trùng"*, `InvItems` chặn *"âm kho"*).
/// Ánh xạ theo bảng là **đoán**; theo **tên ràng buộc** là **biết chính xác**.
///
/// ⚠️ **BẢO TRÌ:** mỗi khi thêm unique index/`CHECK` mới **có ý nghĩa nghiệp vụ**, phải thêm một dòng vào đây —
/// nếu không, ca đó rơi về mã chung `eCommerce:Db:0001` (vẫn **tốt hơn `500`**, nhưng kém hữu ích cho CSKH).
/// </summary>
public static class ViPhamRangBuocDb
{
    /// <summary>Kết quả ánh xạ: mã nghiệp vụ + thông điệp cho người dùng.</summary>
    public sealed record KetQua(string Code, string Message, bool LaViPhamDuyNhat);

    /// <summary>Mã **chung** khi không nhận ra ràng buộc — vẫn là lỗi **nghiệp vụ/dữ liệu**, KHÔNG phải `500`.</summary>
    public const string MaChung = "eCommerce:Db:0001";

    private static readonly Dictionary<string, KetQua> TheoRangBuoc = new(StringComparer.Ordinal)
    {
        // ── KHO: chống âm tồn / giữ chỗ vượt tồn (`CHECK` — `INV-001` ADR-03) ─────────────────────
        ["CK_InvItems_OnHand_KhongAm"] =
            new(MaChung, "Tồn kho sẽ bị ÂM ⇒ thao tác bị từ chối.", false),
        ["CK_InvItems_Reserved_KhongAm"] =
            new(MaChung, "Số lượng giữ chỗ sẽ bị ÂM ⇒ thao tác bị từ chối.", false),
        ["CK_InvItems_Reserved_KhongVuotOnHand"] =
            new("eCommerce:Inventory:0001", "Giữ chỗ VƯỢT tồn thực ⇒ không đủ hàng.", false),
        ["CK_InvStockLedger_Balance_KhongAm"] =
            new(MaChung, "Số dư sổ cái kho sẽ bị ÂM ⇒ thao tác bị từ chối.", false),
        ["CK_InvStockLedger_Delta_KhacKhong"] =
            new(MaChung, "Bút toán kho có độ lệch BẰNG 0 ⇒ vô nghĩa, bị từ chối.", false),

        // ── KHO: chống ghi trùng (`INV-001` ADR-05) ──────────────────────────────────────────────
        ["IX_InvStockLedger_TenantId_ReferenceLineId"] =
            new("eCommerce:Inventory:0010", "Chứng từ này ĐÃ được ghi sổ (dòng này đã có bút toán).", true),

        // ── KHO: danh mục + chứng từ ─────────────────────────────────────────────────────────────
        ["IX_InvWarehouses_TenantId_Code"] =
            new("eCommerce:Inventory:0003", "Mã kho đã tồn tại.", true),
        ["IX_InvStockDocuments_TenantId_Number"] =
            new("eCommerce:Inventory:0014", "Số chứng từ đã tồn tại.", true),
        ["IX_InvStockDocumentLines_TenantId_Doc_Sku"] =
            new("eCommerce:Inventory:0015", "SKU này đã có dòng trong chứng từ (gộp số lượng vào MỘT dòng).", true),

        // ── ĐƠN HÀNG (`SAL-001`) ─────────────────────────────────────────────────────────────────
        // 📌 Dòng này được thêm vì CHÍNH lượt kiểm chứng đã phát hiện thiếu: tạo đơn lần hai từ cùng checkout
        // trả **mã chung** `eCommerce:Db:0001` thay vì mã nói rõ *"đã có đơn rồi"*. Đúng quy tắc bảo trì ở đầu
        // lớp này: **thêm ràng buộc mới ⇒ PHẢI thêm dòng ánh xạ**.
        ["IX_OrdOrders_TenantId_CheckoutIdempotencyKey"] =
            new("eCommerce:Orders:0002",
                "Đã có ĐƠN cho lần checkout này rồi ⇒ không tạo đơn thứ hai.", true),

        // ── KHO: giữ chỗ (`INV-002`) ─────────────────────────────────────────────────────────────
        ["IX_InvStockReservations_TenantId_Ref_Sku_Kho"] =
            new("eCommerce:Inventory:0032",
                "Nguồn này (giỏ/đơn) ĐÃ giữ chỗ cho SKU này rồi ⇒ không giữ lần nữa.", true),

        // ── KHO: hạn mức phân bổ RIÊNG TỪNG KÊNH (`INV-003`, bảng `InvChannelLimits`) ────────────
        // 📌 Ràng buộc này CHẶN MỘT HÀNH VI HỢP LỆ VỀ NGHIỆP VỤ: người vận hành (hoặc màn hình admin bấm hai lần)
        // khai hạn mức cho cùng một `(kênh, kho)` LẦN HAI ⇒ trước đây là **HTTP 500**, client **không phân biệt**
        // được *"đã có hạn mức rồi"* với *"hệ thống hỏng"* ⇒ **bấm lại** ⇒ vòng lặp (đúng lý do Phụ lục B tồn tại).
        // ⚠️ Đây là ca NẶNG của lỗi C4/F1: cột phân biệt *"cả kênh"* với *"một kho"* chính là `WarehouseId`
        // (NULLABLE) ⇒ thiếu `NULLS NOT DISTINCT` là index **mất tác dụng im lặng** (xem chú thích migration).
        ["IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId"] =
            new("eCommerce:Inventory:0045",
                "Kênh này ĐÃ có hạn mức cho phạm vi đó (cả kênh, hoặc đúng kho này) ⇒ sửa dòng đang có thay vì thêm dòng mới.", true),

        // ── KHO: `CHECK` của `INV-003` + `REC-001` (16/10/2026) ─────────────────────────────────
        // 📌 **QUYẾT ĐỊNH CÓ Ý THỨC — dùng mã CHUNG `eCommerce:Db:0001`, KHÔNG bịa mã nghiệp vụ:**
        // các `CHECK` dưới đây là **hàng rào phòng vệ chiều sâu** trên những đường ghi **ĐANG CHẠY THẬT**
        // (`InvChannelLimits` do người vận hành cấu hình, `ReconRuns` do job 15 phút ghi). Tầng domain **đã**
        // kiểm trước và **NÉM lỗi nghiệp vụ có mã riêng** (`HanMucKenhGiaTriAm` = `0044`, `ArgumentException`
        // cho mốc thời gian/số lệch) ⇒ để DB nổ nghĩa là **có đường ghi TẮT qua domain**, tức là **bug**, chứ
        // không phải một hành vi nghiệp vụ bình thường cần mã riêng.
        // 🔒 Vì sao **KHÔNG** để chúng ở danh sách trắng: danh sách trắng nghĩa *"chưa có đường ghi nào chạm tới"* —
        // điều đó **SAI** với 4 ràng buộc này, và một lời khai sai trong danh sách trắng làm cơ chế mất giá trị.
        // 📌 Chúng **vẫn** phải được ánh xạ (đây là lý do khối này tồn tại): có ánh xạ thì ca đó trả **403 kèm
        // SQLSTATE + tên ràng buộc** (tra được ngay), thay vì `500` mù.
        ["CK_InvChannelLimits_GiaTri_KhongAm"] =
            new(MaChung, "Hạn mức theo kênh KHÔNG được âm (quy ước `0` = TẮT). Giá trị âm chỉ đến được DB nếu có đường ghi bỏ qua tầng domain.", false),
        ["CK_ReconRuns_MocThoiGian_HopLe"] =
            new(MaChung, "Lượt đối soát có mốc KẾT THÚC trước mốc BẮT ĐẦU ⇒ dữ liệu thời gian không hợp lệ.", false),
        ["CK_ReconRuns_SoCapDaDo_KhongAm"] =
            new(MaChung, "Lượt đối soát có số cặp đã đo ÂM ⇒ dữ liệu không hợp lệ.", false),
        ["CK_ReconRuns_SoLechVuot_KhongVuotTong"] =
            new(MaChung, "Lượt đối soát có số lệch VƯỢT NGƯỠNG lớn hơn TỔNG số lệch ⇒ dữ liệu không hợp lệ.", false),

        // ── CATALOG: SLUG là khoá URL công khai (`CAT-003` ADR-03, 16/10/2026) ───────────────────
        // 📌 Ràng buộc này CHẶN MỘT HÀNH VI HỢP LỆ VỀ NGHIỆP VỤ: hai người (hoặc hai request đồng thời) cùng
        // chọn một slug ⇒ trước đây là **HTTP 500** ⇒ client **không phân biệt** *"slug đã dùng"* với
        // *"hệ thống hỏng"* ⇒ **gửi lại** ⇒ vòng lặp (đúng lý do Phụ lục B tồn tại).
        // 🔑 Mã riêng `SlugDaTonTai` (`CAT-003` §6.3 = `0201`; KHÔNG mượn `0025` = *"mã danh mục không hợp lệ"*):
        // ca này cần chỉ dẫn *"chọn slug KHÁC"*, không phải *"sửa mã sản phẩm"*.
        // ⚠️ Đây cũng là ca NẶNG của lỗi C4/F1: `TenantId` có thể `NULL` ⇒ thiếu `NULLS NOT DISTINCT` là
        // index **mất tác dụng im lặng** (xem chú thích migration `Cat003_ProductSlug_Unique`).
        ["IX_CatProducts_TenantId_Slug"] =
            new(eCommerceConsts.ErrorCodes.Catalog.SlugDaTonTai,
                "Slug này ĐÃ được sản phẩm khác dùng ⇒ URL công khai sẽ trùng. Chọn slug khác.", true),

        // ── KHÁCH HÀNG (`CUS-001`, 16/10/2026) ──────────────────────────────────────────────────
        // 📌 Cả ba ràng buộc dưới đây CHẶN MỘT HÀNH VI HỢP LỆ VỀ NGHIỆP VỤ (retry webhook / bấm hai lần /
        // hai request đồng thời) ⇒ client **cần mã** để phân biệt *"đã có rồi"* với *"hệ thống hỏng"*.
        // Trước đây là `23505` ⇒ **HTTP 500** ⇒ consumer **retry vô hạn** (đúng lý do Phụ lục B tồn tại).
        // ⚠️ Cả ba đều là ca NẶNG của lỗi C4/F1: `TenantId` có thể `NULL` ⇒ thiếu `NULLS NOT DISTINCT`
        // là index **mất tác dụng im lặng** (xem chú thích migration `Cus001_Customers_CustomerGroups`).
        ["IX_CusCustomers_TenantId_IdempotencyKey"] =
            new("eCommerce:Customers:0003",
                "Lần gọi này ĐÃ tạo khách rồi (khoá idempotency trùng) ⇒ dùng lại hồ sơ cũ, KHÔNG tạo mới.", true),
        ["IX_CusCustomers_TenantId_IdentityUserId"] =
            new("eCommerce:Customers:0004",
                "Tài khoản này ĐÃ gắn với một hồ sơ khách khác ⇒ một tài khoản chỉ được một hồ sơ (ADR-07).", true),
        // ── MEDIA SẢN PHẨM (`CAT-003` ADR-12, 16/10/2026) ─────────────────────────────────────────
        // 📌 Ràng buộc này CHẶN MỘT HÀNH VI HỢP LỆ VỀ NGHIỆP VỤ: đặt ảnh chính là thao tác
        // *"đọc ảnh chính hiện tại → bỏ đánh dấu → đánh dấu ảnh mới"*; hai request ĐỒNG THỜI
        // (double-click, hai tab, hai nhân viên) **cùng** thấy *"chưa có ảnh chính"* ⇒ **cả hai** đánh dấu.
        // 🔑 Mã riêng `DaCoAnhChinh` (`CAT-003` §6.3 = `0204`; KHÔNG mượn `0011` = *"thiếu thông tin bắt buộc"*):
        // ca này cần chỉ dẫn *"bỏ đánh dấu ảnh chính hiện tại trước"* — khác hẳn *"điền thêm trường"*.
        // ⚠️ Ca NẶNG của lỗi C4/F1: `TenantId` có thể `NULL` ⇒ thiếu `NULLS NOT DISTINCT` là index
        // **mất tác dụng im lặng** (xem chú thích migration `Cat003_ProductMedia`).
        ["IX_CatProductMedia_MotAnhChinh"] =
            new(eCommerceConsts.ErrorCodes.Catalog.DaCoAnhChinh,
                "Sản phẩm này ĐÃ có ảnh chính ⇒ bỏ đánh dấu ảnh chính hiện tại TRƯỚC khi đặt ảnh mới.", true),
        // ── HIỂN THỊ THEO KÊNH (`CAT-003` ADR-08, 16/10/2026) ────────────────────────────────────
        // 📌 Ràng buộc này CHẶN MỘT HÀNH VI HỢP LỆ VỀ NGHIỆP VỤ: đặt trạng thái là thao tác
        // *"đọc bản ghi hiện tại → quyết định → ghi"*; hai request ĐỒNG THỜI (double-click, hai tab)
        // **cùng** đọc thấy *"chưa có bản ghi"* ⇒ **cả hai** `INSERT`.
        // 🔑 Mã `eCommerce:Catalog:0205` (KHÔNG mượn `0011` = *"thiếu thông tin bắt buộc"*): ca này cần chỉ dẫn
        // *"cặp `(sản phẩm, kênh)` này ĐÃ có cấu hình hiển thị ⇒ SỬA bản ghi đó, đừng tạo mới"*.
        // ⚠️ Ca NẶNG của lỗi C4/F1: `TenantId` có thể `NULL` ⇒ thiếu `NULLS NOT DISTINCT` là index
        // **mất tác dụng im lặng** (xem chú thích migration `Cat003_ProductChannelVisibility`).
        ["IX_CatProductChannelVisibilities_TenantId_SanPham_Kenh"] =
            new(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe,
                "Cặp (sản phẩm, kênh) này ĐÃ có cấu hình hiển thị ⇒ SỬA bản ghi hiện có, KHÔNG tạo bản ghi thứ hai.", true),
        ["IX_CusGroups_TenantId_Code"] =
            new("eCommerce:Customers:0008",
                "Mã nhóm khách này ĐÃ tồn tại trong tenant ⇒ chọn mã khác (KHÔNG tạo hai nhóm cùng mã).", true),

        // ── ĐỐI SOÁT (`REC-001`) ────────────────────────────────────────────────────────────────
        // 📌 Ràng buộc này CHẶN MỘT HÀNH VI HỢP LỆ VỀ NGHIỆP VỤ: job chạy lại / hai instance cùng quét
        // (khoá phân tán hết hạn) / đường ghi tay đều có thể chốt **cùng một cặp `(kho, SKU)`** trong một lượt.
        // Trước đây là **500** ⇒ client **không phân biệt** *"đã ghi rồi"* với *"hệ thống hỏng"* ⇒ **chạy lại**.
        // 🔑 Mã `eCommerce:Recon:0001` (KHÔNG mượn mã miền kho — bài học `0040`/`0042`).
        ["IX_ReconRunMismatches_TenantId_Run_Kho_Sku"] =
            new("eCommerce:Recon:0001",
                "Lượt đối soát này ĐÃ ghi dòng lệch cho đúng cặp (kho, SKU) đó ⇒ không ghi lần nữa.", true),

        // ── PRICING: idempotency + sổ giải trình (ADR-13) ────────────────────────────────────────
        ["IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey"] =
            new("eCommerce:Pricing:0006",
                "Khoá idempotency này ĐÃ được dùng ⇒ gọi lại sẽ trả kết quả đã lưu.", true),
        ["IX_PrcPriceLists_TenantId_Code_EffectiveFrom"] =
            new("eCommerce:Pricing:0002", "Bảng giá cùng mã + ngày hiệu lực đã tồn tại.", true),
        ["IX_PrcTierPriceSets_TenantId_Code_EffectiveFrom"] =
            new("eCommerce:Pricing:0002", "Bộ bậc thang cùng mã + ngày hiệu lực đã tồn tại.", true),
        ["IX_PrcTierPrices_TenantId_SetId_Sku_MinQty"] =
            new("eCommerce:Pricing:0008", "Bậc thang TRÙNG `MinQuantity` cho SKU này.", true),

        // ── CATALOG ──────────────────────────────────────────────────────────────────────────────
        ["IX_CatProducts_TenantId_Sku"] =
            new("eCommerce:Catalog:0004", "SKU đã tồn tại.", true),
        ["IX_CatCategories_TenantId_ParentId_Slug"] =
            new("eCommerce:Catalog:0020", "Danh mục trùng `Slug` trong cùng danh mục cha.", true)
    };

    /// <summary>Mã SQLSTATE của PostgreSQL mà ta xử lý.</summary>
    public static bool LaViPhamDuyNhat(string? sqlState) => sqlState == "23505";

    /// <summary>Mã SQLSTATE vi phạm `CHECK`.</summary>
    public static bool LaViPhamCheck(string? sqlState) => sqlState == "23514";

    /// <summary>Mã SQLSTATE vi phạm khoá ngoại.</summary>
    public static bool LaViPhamKhoaNgoai(string? sqlState) => sqlState == "23503";

    /// <summary>
    /// Ánh xạ. `sqlState` **bắt buộc** phải là một trong ba loại trên, nếu không ⇒ trả `null`
    /// (để lỗi thật **nổi lên như `500`** — **KHÔNG** nuốt lỗi hệ thống thành lỗi nghiệp vụ).
    /// 🚨 Đây là ranh giới quan trọng: chỉ ánh xạ **vi phạm ràng buộc dữ liệu**, không ánh xạ lỗi hạ tầng.
    /// </summary>
    public static KetQua? AnhXa(string? sqlState, string? tenRangBuoc)
    {
        if (!LaViPhamDuyNhat(sqlState) && !LaViPhamCheck(sqlState) && !LaViPhamKhoaNgoai(sqlState))
        {
            return null;
        }

        var laDuyNhat = LaViPhamDuyNhat(sqlState);

        if (tenRangBuoc is not null && TheoRangBuoc.TryGetValue(tenRangBuoc, out var kq))
        {
            return kq with { LaViPhamDuyNhat = laDuyNhat };
        }

        // Không nhận ra tên ràng buộc ⇒ mã CHUNG (vẫn là lỗi dữ liệu, KHÔNG phải `500`).
        return new KetQua(
            MaChung,
            LaViPhamKhoaNgoai(sqlState)
                ? "Dữ liệu tham chiếu KHÔNG tồn tại (khoá ngoại) ⇒ thao tác bị từ chối."
                : "Dữ liệu vi phạm ràng buộc toàn vẹn của hệ thống ⇒ thao tác bị từ chối.",
            laDuyNhat);
    }

    /// <summary>Tên các ràng buộc **đã có ánh xạ** — dùng cho test bảo trì (phát hiện thiếu sót).</summary>
    public static IReadOnlyCollection<string> CacRangBuocDaAnhXa => TheoRangBuoc.Keys;
}
