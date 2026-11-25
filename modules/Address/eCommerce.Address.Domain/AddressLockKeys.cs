namespace eCommerce.Address;

/// <summary>
/// **Tên khoá phân tán do module `Address` sở hữu** (`D-82`, khuôn `XC-003` D3).
///
/// ⚠️ **KHÔNG** thêm tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` của Host đã ghép
/// (`eCommerceHttpApiHostModule.cs`), thêm ở đây sẽ thành **tiền tố đôi** `ecommerce:ecommerce:…`.
///
/// 🔒 **MỘT khoá toàn danh mục** dùng chung cho **cả** job đồng bộ **và** `ImportDatasetAsync`, **không**
/// khoá theo `schemaVersion`/`sourceHash`: danh mục ĐVHC là dữ liệu quốc gia dùng chung, hai lượt nạp
/// (khác đường hoặc khác hash) chạy chồng sẽ nạp chồng + xoá cache xen kẽ + để lại lô một nửa.
/// </summary>
public static class AddressLockKeys
{
    /// <summary>
    /// Khoá nạp danh mục ĐVHC — giá trị **giữ nguyên** chuỗi cũ của worker
    /// (`address:sync-catalog`) để khi deploy cuốn chiếu hai instance vẫn loại trừ nhau.
    /// </summary>
    public const string CatalogImport = "address:sync-catalog";
}
