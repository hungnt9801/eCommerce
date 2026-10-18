using System.Security.Cryptography;
using System.Text;

namespace eCommerce.IntegrationTests;

/// <summary>
/// Băm chuỗi canonical thành **hex 64 ký tự** cho test schema của `ADDR-004`.
///
/// 🎯 CỐ Ý KHÔNG dùng `eCommerce.Address.DiaChiCanonical`:
///   ① test `PostgreSqlSchemaTests` kiểm **HÀNG RÀO DB** (index `NULLS NOT DISTINCT`), **không** kiểm
///      hàm băm — hàm băm đã có test riêng ở `test/eCommerce.Tests/Address/AddressTests.cs`;
///   ② nhờ vậy project test tích hợp **không** phải tham chiếu `eCommerce.Address.Domain`;
///   ③ nếu thuật toán băm thật đổi, test này **vẫn** kiểm đúng điều nó định kiểm (miễn hex 64 hợp lệ —
///      đúng như CHECK `CK_AddrAddresses_ContentHashHex` ở DB).
///
/// 📌 Giá trị băm ở đây **không** phải `ContentHash` thật của bất kỳ địa chỉ nào; nó chỉ cần **hợp lệ**
///    và **phân biệt được** giữa các ca test.
/// </summary>
internal static class DiaChiHashThu
{
    public static string Bam(string input)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
