using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Tax;

/// <summary>
/// Domain của module <b>Tax</b> — quản trị mức thuế suất GTGT theo thời gian và phân loại thuế theo SKU/danh mục.
///
/// 🚨 VÌ SAO MODULE NÀY TỒN TẠI (đây là yêu cầu PHÁP LÝ, không phải tính năng):
///   • NQ 204/2025/QH15 + NĐ 174/2025/NĐ-CP giảm thuế GTGT 10% → 8% cho một số nhóm hàng, TRỪ nhóm
///     thuộc Phụ lục I/II (viễn thông, tài chính–ngân hàng–chứng khoán–bảo hiểm, bất động sản,
///     sản phẩm kim loại, khai khoáng (trừ than), hàng chịu thuế TTĐB (trừ xăng)).
///   • Mức 8% chỉ có hiệu lực đến hết 31/12/2026.
///   • Doanh nghiệp phải kê khai hàng hoá được giảm theo Mẫu số 01 (Phụ lục III NĐ 174/2025) nộp kèm tờ khai GTGT.
///
/// 🎯 QUYẾT ĐỊNH THIẾT KẾ CỐT LÕI (xem docs/features/XC-006-*.md):
///   • Danh mục hàng hoá Phụ lục I/II là DỮ LIỆU do kế toán quản trị — KHÔNG hard-code trong code.
///   • Mức thuế suất là HÀM CỦA THỜI GIAN (khung hiệu lực) — KHÔNG hard-code.
///   • Thuế suất được CHỐT (snapshot) vào dòng đơn/hóa đơn; đổi cấu hình không làm đổi chứng từ cũ.
/// </summary>
[DependsOn(typeof(AbpDddDomainModule))]
public class TaxDomainModule : AbpModule
{
}
