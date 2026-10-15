using System.Reflection;
using System.Reflection.Emit;
using eCommerce.Messaging.EventCoverage;

namespace eCommerce.Tests.EventCoverage;

// ── Kiểu CỤC BỘ cho test quét assembly ───────────────────────────────────────
// Dùng lại interface giả ở `EventCoverageCheckerTests` (cùng namespace): lõi so khớp theo TÊN interface
// nên test kiểm chứng đúng LUẬT SO KHỚP mà không phụ thuộc gói ABP.

public class SuKienQuetCoNguoiNhanEto { }

public class SuKienQuetKhongCoNguoiNhanEto { }

public class SuKienQuetConLineEto { }

public class NguoiNhanChoSuKienQuet : IDistributedEventHandler<SuKienQuetCoNguoiNhanEto> { }

/// <summary>
/// Test cho <see cref="EventCoverageScan"/> — **lõi quét assembly** dùng CHUNG cho cả cảnh báo lúc khởi
/// động và quyết định **fail-closed** chống mất đơn (lỗi #22).
///
/// 🎯 Vì sao phải test lớp này (chứ không chỉ `EventCoverageChecker`):
/// `EventCoverageChecker` chỉ đối chiếu **hai danh sách kiểu đã có sẵn**. Thứ dễ sai nằm ở bước **QUÉT
/// assembly**: quét thiếu assembly ⇒ kết quả **"sạch" giả tạo** ⇒ tưởng sự kiện đã có người nhận trong
/// khi thực tế không có (bài học C16: quét theo "assembly đã nạp" rất dễ thiếu).
/// </summary>
public class EventCoverageScanTests
{
    private static readonly Assembly AssemblyTest = typeof(EventCoverageScanTests).Assembly;

    [Fact]
    public void Quet_thay_duoc_ca_su_kien_CO_va_KHONG_co_nguoi_nhan()
    {
        var kq = EventCoverageScan.Quet([AssemblyTest]);

        Assert.Equal(1, kq.SoAssemblyDaQuet);
        Assert.Empty(kq.AssemblyKhongNapDu);

        Assert.Contains("SuKienQuetCoNguoiNhanEto", kq.PhuSong.SuKienCoNguoiNhan);
        Assert.Contains("SuKienQuetKhongCoNguoiNhanEto", kq.PhuSong.SuKienKhongCoNguoiNhan);
    }

    [Fact]
    public void ETO_CON_LineEto_khong_bi_coi_la_su_kien_thieu_nguoi_nhan()
    {
        // Nếu tính, báo cáo có DƯƠNG TÍNH GIẢ ⇒ người đọc bắt đầu bỏ qua cảnh báo (đúng thứ cần tránh).
        var kq = EventCoverageScan.Quet([AssemblyTest]);

        Assert.DoesNotContain("SuKienQuetConLineEto", kq.PhuSong.SuKienKhongCoNguoiNhan);
        Assert.DoesNotContain("SuKienQuetConLineEto", kq.PhuSong.SuKienCoNguoiNhan);
    }

    [Fact]
    public void Assembly_DONG_bi_bo_qua_va_KHONG_duoc_tinh_la_da_quet()
    {
        // Assembly động không có kiểu thật để xét ⇒ phải bị loại khỏi PHẠM VI, không được tính vào
        // `SoAssemblyDaQuet` (nếu tính, con số "đã quét N assembly" trở nên vô nghĩa).
        var dong = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("eCommerce.DongGia-" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);

        var kq = EventCoverageScan.Quet([dong]);

        Assert.Equal(0, kq.SoAssemblyDaQuet);
        Assert.False(kq.PhuSong.CoSuKienBiBoQuen);
    }

    [Fact]
    public void Assembly_rong_thi_khong_bao_dong_va_pham_vi_bang_khong()
    {
        var kq = EventCoverageScan.Quet([]);

        Assert.Equal(0, kq.SoAssemblyDaQuet);
        Assert.Empty(kq.AssemblyKhongNapDu);
        Assert.False(kq.PhuSong.CoSuKienBiBoQuen);
    }

    [Fact]
    public void LocAssemblyHeThong_chi_giu_assembly_cua_he_thong()
    {
        var kq = EventCoverageScan.LocAssemblyHeThong(
            [typeof(string).Assembly, AssemblyTest, typeof(EventCoverageScanTests).Assembly]);

        // `typeof(string).Assembly` = System.Private.CoreLib → bị loại.
        Assert.All(kq, a => Assert.StartsWith(EventCoverageScan.TienToAssembly, a.GetName().Name!));
        Assert.Contains(AssemblyTest, kq);
        Assert.DoesNotContain(typeof(string).Assembly, kq);
    }

    [Fact]
    public void SoAssemblyDaQuet_dem_theo_assembly_PHAN_BIET()
    {
        // 🚨 Bất biến chống "kết quả sạch giả tạo": con số phạm vi phải có nghĩa — cùng một assembly
        // truyền vào hai lần vẫn là **một** assembly đã quét. Nếu đếm trùng, con số "đã quét N assembly"
        // phóng đại phạm vi thật ⇒ người đọc tin nhầm là đã quét đủ.
        var kq = EventCoverageScan.Quet([AssemblyTest, AssemblyTest]);

        Assert.Equal(1, kq.SoAssemblyDaQuet);
    }
}
