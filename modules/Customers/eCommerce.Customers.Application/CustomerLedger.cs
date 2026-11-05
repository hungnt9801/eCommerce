using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Customers;

/// <summary>
/// 💰 **HIỆN THỰC `ICustomerLedger`** — nơi **DUY NHẤT** ghi điểm/tiền tích luỹ/công nợ vào khách (`WP-07`).
///
/// 🚨 **ĐÂY LÀ CHỖ NỐI CỦA `handover-n7` §5 #6:** POS (`WP-06`) đã ghi `PointsEarned/PointsUsed/DebtAmount`
/// **trên hoá đơn** nhưng **chưa** đụng `Customer`. Nay mỗi lần bán/trả/huỷ gọi cổng này **trong cùng UoW**
/// ⇒ hoặc cả hai cùng ghi, hoặc cả hai cùng rollback.
///
/// 📌 **KHÔNG idempotent ở đây** (cố ý): hoá đơn đã idempotent theo `ClientBillId` (`D-42`) — thêm khoá
/// thứ hai ở tầng sổ khách là **hai cơ chế cho một bất biến** (cùng lý do `D-26`/`D-32` loại `RowVersion`).
/// ⚠️ **Hệ quả cần biết:** nếu ai đó gọi cổng này **hai lần** cho cùng một hoá đơn (viết code sai, không
/// phải retry mạng) thì số dư bị cộng hai lần. Vì vậy cổng chỉ được gọi **ngay sau** khi chứng từ nguồn
/// được tạo trong cùng một hàm — và đường retry đã dừng ở `alreadyExisted` **trước** khi tới đây.
/// </summary>
public class CustomerLedger : ICustomerLedger, ITransientDependency
{
    private readonly IRepository<Customer, Guid> _khach;
    private readonly IRepository<CustomerLevel, Guid> _cap;
    private readonly ILogger<CustomerLedger> _logger;

    public CustomerLedger(
        IRepository<Customer, Guid> khach,
        IRepository<CustomerLevel, Guid> cap,
        ILogger<CustomerLedger> logger)
    {
        _khach = khach;
        _cap = cap;
        _logger = logger;
    }

    public async Task<bool> GhiNhanBanHangAsync(Guid? customerId, decimal tienHang, decimal diemTich,
        decimal diemSuDung, decimal noTang, string nguon)
    {
        if (customerId is not { } id)
        {
            // Khách vãng lai là **hạng nhất** (`ADR-03`): đơn không khách vẫn hợp lệ, chỉ là không có sổ để ghi.
            return false;
        }

        var khach = await LayKhachAsync(id);

        var truoc = khach.GhiNhanMuaHang(tienHang, diemTich, diemSuDung, noTang);

        await XepLaiCapAsync(khach);
        await _khach.UpdateAsync(khach, autoSave: true);

        _logger.LogInformation(
            "SỔ KHÁCH: BÁN {Nguon} · khách {KhachId} · điểm {DiemTruoc}→{DiemSau} · tích luỹ {TienTruoc}→{TienSau} · " +
            "nợ {NoTruoc}→{NoSau} · cấp {Cap}. [WP07-SO-KHACH-BAN]",
            nguon, khach.Id, truoc.DiemTruoc, khach.Points, truoc.TienTichLuyTruoc, khach.AccumulatedAmount,
            truoc.NoTruoc, khach.DebtBalance, khach.LevelId);

        return true;
    }

    public async Task<bool> DaoBanHangAsync(Guid? customerId, decimal tienHang, decimal diemTich,
        decimal diemSuDung, decimal noGiam, string nguon)
    {
        if (customerId is not { } id)
        {
            return false;
        }

        var khach = await LayKhachAsync(id);

        var ketQua = khach.DaoGhiNhanMuaHang(tienHang, diemTich, diemSuDung, noGiam);

        await XepLaiCapAsync(khach);
        await _khach.UpdateAsync(khach, autoSave: true);

        _logger.LogWarning(
            "SỔ KHÁCH: ĐẢO {Nguon} · khách {KhachId} · đảo {Diem} điểm / {Tien} tiền / {No} nợ · " +
            "còn lại điểm {DiemSau} · tích luỹ {TienSau} · nợ {NoSau}. [WP07-SO-KHACH-DAO]",
            nguon, khach.Id, ketQua.DiemDaDao, ketQua.TienDaDao, ketQua.NoDaGiam,
            khach.Points, khach.AccumulatedAmount, khach.DebtBalance);

        return true;
    }

    public async Task<bool> GhiNhanTraHangAsync(Guid? customerId, decimal tienHangTra, decimal diemThuHoi,
        decimal noGiam, string nguon)
    {
        if (customerId is not { } id)
        {
            return false;
        }

        var khach = await LayKhachAsync(id);

        // 📌 Trả hàng = **đảo một phần** lần bán: `diemTich` âm theo nghĩa "thu hồi điểm đã tích" nhưng
        // `DaoGhiNhanMuaHang` nhận số DƯƠNG + chiều cố định, nên ta truyền `diemTich = diemThuHoi`.
        var ketQua = khach.DaoGhiNhanMuaHang(tienHangTra, diemThuHoi, 0m, noGiam);

        await XepLaiCapAsync(khach);
        await _khach.UpdateAsync(khach, autoSave: true);

        _logger.LogInformation(
            "SỔ KHÁCH: TRẢ HÀNG {Nguon} · khách {KhachId} · trừ {Tien} tiền · thu hồi {Diem} điểm · " +
            "giảm {No} nợ · còn lại điểm {DiemSau} · tích luỹ {TienSau} · nợ {NoSau}. [WP07-SO-KHACH-TRA]",
            nguon, khach.Id, ketQua.TienDaDao, ketQua.DiemDaDao, ketQua.NoDaGiam,
            khach.Points, khach.AccumulatedAmount, khach.DebtBalance);

        return true;
    }

    private async Task<Customer> LayKhachAsync(Guid id)
        => await _khach.FindAsync(id)
           ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayKhach)
               .WithData("customerId", id)
               .WithData("lyDo", "Hoá đơn trỏ tới khách KHÔNG tồn tại ⇒ không ghi được sổ điểm/công nợ.");

    /// <summary>
    /// Xếp lại **cấp khách** theo tiền tích luỹ **sau** giao dịch.
    /// 📌 Đọc **mọi cấp đang bật** của tenant (bảng cấu hình nhỏ) rồi chọn bằng hàm thuần —
    /// không viết một câu SQL `ORDER BY … LIMIT 1` riêng (hai định nghĩa của "cấp hiện tại").
    /// </summary>
    private async Task XepLaiCapAsync(Customer khach)
    {
        var cacCap = await _cap.GetListAsync(x => x.IsActive);

        var nguong = cacCap
            .Select(x => new NghiepVuKhach.NguongCapKhach(x.Id, x.MinAccumulatedAmount, x.SortOrder))
            .ToList();

        khach.XepCap(NghiepVuKhach.ChonCapKhach(nguong, khach.AccumulatedAmount));
    }
}
