using eCommerce.Tax;
using eCommerce.Tax.RateSchedules;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace eCommerce.DbMigrator;

/// <summary>
/// Seed **bảng mức thuế suất GTGT** (XC-006 §11).
///
/// 🚨 VÌ SAO SEED NÀY LÀ BẮT BUỘC (không phải tiện ích):
///   <c>TaxRateResolver</c> KHÔNG hard-code thuế suất — nó tra bảng theo ngày. Nếu bảng rỗng thì
///   mọi lần tính thuế sẽ ném <c>eCommerce:Tax:0002</c> (không có mức hiệu lực).
///
/// 🎯 DÒNG QUAN TRỌNG NHẤT LÀ <c>VAT8</c>: nó có <c>EffectiveTo = 31/12/2026</c> theo
///   NQ 204/2025/QH15. Nhờ vậy **00:00 ngày 01/01/2027 hệ thống tự áp lại 10%** mà KHÔNG cần
///   sửa code, KHÔNG cần release (BR-XC-006-001). Nếu luật **gia hạn** mức 8%, chỉ cần sửa
///   <c>EffectiveTo</c> của dòng này qua màn hình quản trị (kèm duyệt + audit — BR-011).
///
/// ⚠️ KHÔNG seed danh mục hàng hoá Phụ lục I/II: đó là **dữ liệu do kế toán nhập** (quyết định D1).
/// </summary>
public class TaxRateScheduleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>Ngày bắt đầu hiệu lực của các mức phổ thông (trước mọi dữ liệu của hệ thống).</summary>
    private static readonly DateOnly MocPhoThong = new(2020, 1, 1);

    /// <summary>Mức 8% bắt đầu từ 01/7/2025 theo NQ 204/2025/QH15 + NĐ 174/2025/NĐ-CP.</summary>
    private static readonly DateOnly BatDauGiam = new(2025, 7, 1);

    /// <summary>
    /// 🔴 Ngày hết hiệu lực của mức 8% — **chưa tìm thấy văn bản gia hạn** (tra ngày 12/10/2026).
    /// Khi có văn bản gia hạn, đổi giá trị này qua màn hình quản trị (BR-011: có duyệt + audit).
    /// </summary>
    private static readonly DateOnly HetHieuLucGiam = new(2026, 12, 31);

    private readonly IRepository<TaxRateSchedule, Guid> _scheduleRepository;
    private readonly IGuidGenerator _guidGenerator;

    public TaxRateScheduleDataSeedContributor(
        IRepository<TaxRateSchedule, Guid> scheduleRepository,
        IGuidGenerator guidGenerator)
    {
        _scheduleRepository = scheduleRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Idempotent: chạy lại DbMigrator nhiều lần không được nhân bản mức thuế.
        if (await _scheduleRepository.GetCountAsync() > 0)
        {
            return;
        }

        await _scheduleRepository.InsertManyAsync(
        [
            new TaxRateSchedule(
                _guidGenerator.Create(), "VAT0", "Thuế suất 0% (hàng hoá, dịch vụ xuất khẩu)",
                0m, TaxRateKind.Standard, MocPhoThong,
                legalBasis: "Luật 48/2024/QH15 Điều 9 khoản 1"),

            new TaxRateSchedule(
                _guidGenerator.Create(), "VAT5", "Thuế suất 5%",
                5m, TaxRateKind.Standard, MocPhoThong,
                legalBasis: "Luật 48/2024/QH15 Điều 9 khoản 2"),

            new TaxRateSchedule(
                _guidGenerator.Create(), "VAT10", "Thuế suất 10% (mức phổ thông)",
                10m, TaxRateKind.Standard, MocPhoThong,
                legalBasis: "Luật 48/2024/QH15 Điều 9 khoản 3"),

            // ⚠️ DÒNG DUY NHẤT CÓ NGÀY KẾT THÚC — xem ghi chú ở đầu class.
            new TaxRateSchedule(
                _guidGenerator.Create(), "VAT8", "Thuế suất 8% (giảm 2% theo NQ 204/2025/QH15)",
                8m, TaxRateKind.Reduced, BatDauGiam, HetHieuLucGiam,
                reducedFromCode: "VAT10",
                legalBasis: "NQ 204/2025/QH15 + NĐ 174/2025/NĐ-CP")
        ]);
    }
}
