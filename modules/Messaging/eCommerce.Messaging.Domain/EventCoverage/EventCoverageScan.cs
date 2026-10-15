using System.Reflection;

namespace eCommerce.Messaging.EventCoverage;

/// <summary>
/// Kết quả MỘT lần quét assembly để tìm người nhận sự kiện.
///
/// ⚠️ <see cref="SoAssemblyDaQuet"/> và <see cref="AssemblyKhongNapDu"/> tồn tại là để **phạm vi quét
/// NHÌN THẤY ĐƯỢC**. Bài học C16: quét theo "assembly đã nạp" rất dễ thiếu, mà thiếu thì bộ kiểm tra
/// cho ra kết quả **"sạch" giả tạo** — đúng loại lỗi im lặng cần tránh.
/// </summary>
public sealed record KetQuaQuetPhuSongSuKien(
    KetQuaPhuSongSuKien PhuSong,
    int SoAssemblyDaQuet,
    IReadOnlyList<string> AssemblyKhongNapDu);

/// <summary>
/// **LÕI THUẦN**: quét một tập assembly để tìm (a) mọi hợp đồng sự kiện `*Eto` và (b) mọi kiểu
/// đóng vai "người nhận sự kiện", rồi đối chiếu bằng <see cref="EventCoverageChecker"/>.
///
/// 🚨 VÌ SAO PHẢI CÓ **MỘT** NGUỒN SỰ THẬT:
/// Câu hỏi *"sự kiện này có người nhận chưa?"* được hỏi ở **hai nơi**:
///   1. lúc **khởi động** — để biến lỗi im lặng thành lỗi ồn ào (cảnh báo `EventCoverageChecker`);
///   2. lúc **đồng bộ sàn** — để **fail-closed**, KHÔNG tiến con trỏ đồng bộ khi chưa có nơi nhận
///      (lỗi #22: publish cho không ai ⇒ đơn mất vĩnh viễn mà không có lỗi nào được ghi).
/// Nếu hai nơi tự quét riêng thì chúng **có thể lệch nhau** — và lệch theo hướng nguy hiểm nghĩa là
/// log nói "có người nhận" trong khi thực tế đơn vẫn mất. Vì vậy cả hai dùng **chung lớp này**.
///
/// Không phụ thuộc ABP, không I/O ⇒ test được toàn bộ (xem `EventCoverageScanTests`).
/// </summary>
public static class EventCoverageScan
{
    /// <summary>Tiền tố tên assembly thuộc hệ thống này (bỏ qua mọi assembly của framework/thư viện).</summary>
    public const string TienToAssembly = "eCommerce";

    /// <summary>
    /// Quét danh sách assembly. Assembly **động** (`IsDynamic`) bị bỏ qua vì không có kiểu thật để xét.
    /// </summary>
    public static KetQuaQuetPhuSongSuKien Quet(IEnumerable<Assembly> cacAssembly)
    {
        ArgumentNullException.ThrowIfNull(cacAssembly);

        // `Distinct` (theo tham chiếu) để `SoAssemblyDaQuet` là **số assembly PHÂN BIỆT** — con số phạm vi
        // phải có nghĩa kể cả khi nguồn đầu vào bị nối trùng.
        var danhSach = cacAssembly
            .Where(a => a is not null && !a.IsDynamic)
            .Distinct()
            .ToList();

        var kieu = new List<Type>();
        var khongNapDu = new List<string>();

        foreach (var asm in danhSach)
        {
            try
            {
                kieu.AddRange(asm.GetTypes());
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Nạp được một phần còn hơn bỏ qua toàn bộ — nhưng PHẢI ghi lại tên assembly,
                // để không ai đọc kết quả mà tưởng là đã quét đủ.
                kieu.AddRange(ex.Types.Where(t => t is not null)!);
                khongNapDu.Add(asm.GetName().Name ?? "(không tên)");
            }
        }

        var suKien = kieu
            .Where(t => t.Name.EndsWith(EventCoverageChecker.HauToSuKien, StringComparison.Ordinal))
            .ToList();

        return new KetQuaQuetPhuSongSuKien(
            EventCoverageChecker.KiemTra(suKien, kieu),
            danhSach.Count,
            khongNapDu);
    }

    /// <summary>
    /// Tiện ích: lọc assembly của chính hệ thống theo <see cref="TienToAssembly"/>. Dùng
    /// <c>AppDomain.CurrentDomain.GetAssemblies()</c> làm nguồn — xem cảnh báo về phạm vi quét ở trên.
    /// </summary>
    public static IReadOnlyList<Assembly> LocAssemblyHeThong(IEnumerable<Assembly> cacAssembly)
        => cacAssembly
            .Where(a => a is not null && !a.IsDynamic)
            .Where(a => a.GetName().Name?.StartsWith(TienToAssembly, StringComparison.Ordinal) == true)
            .ToList();
}
