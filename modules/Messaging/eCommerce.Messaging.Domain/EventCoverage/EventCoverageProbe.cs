using System.Reflection;
using Microsoft.Extensions.Logging;

namespace eCommerce.Messaging.EventCoverage;

/// <summary>
/// Bản thật của <see cref="IEventCoverageProbe"/>: quét assembly của hệ thống **một lần duy nhất**
/// (lazy, có cache, an toàn đa luồng) rồi trả lời theo kết quả đã cache.
///
/// 📌 Đăng ký **TƯỜNG MINH** ở <see cref="MessagingDomainModule"/> (không dựa vào đăng ký quy ước):
/// nếu ai xoá đăng ký đó thì ứng dụng **không khởi động được** (resolve thất bại) — **ồn ào**,
/// đúng ý đồ. Đó là lựa chọn có chủ ý: một cổng an toàn chống mất dữ liệu **không được phép**
/// biến mất trong im lặng.
/// </summary>
public sealed class EventCoverageProbe : IEventCoverageProbe
{
    private readonly ILogger<EventCoverageProbe> _logger;
    private readonly Lazy<KetQuaQuetPhuSongSuKien> _ketQua;
    private readonly Lazy<HashSet<string>> _tenSuKienCoNguoiNhan;

    public EventCoverageProbe(ILogger<EventCoverageProbe> logger)
    {
        _logger = logger;

        _ketQua = new Lazy<KetQuaQuetPhuSongSuKien>(Quet, LazyThreadSafetyMode.ExecutionAndPublication);

        _tenSuKienCoNguoiNhan = new Lazy<HashSet<string>>(
            () => new HashSet<string>(_ketQua.Value.PhuSong.SuKienCoNguoiNhan, StringComparer.Ordinal),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public KetQuaQuetPhuSongSuKien KetQua => _ketQua.Value;

    public bool CoNguoiNhan(Type kieuSuKien)
    {
        ArgumentNullException.ThrowIfNull(kieuSuKien);

        // ⚠️ So khớp theo TÊN KIỂU (không kèm namespace) — nhất quán với `EventCoverageChecker`, vốn so
        // khớp theo tên để lõi không phụ thuộc ABP. Hệ quả đã biết: hai sự kiện TRÙNG TÊN ở hai namespace
        // khác nhau sẽ được coi là một. Ghi rõ ở đây để không ai tưởng đây là so khớp chặt hơn thực tế.
        return _tenSuKienCoNguoiNhan.Value.Contains(kieuSuKien.Name);
    }

    private KetQuaQuetPhuSongSuKien Quet()
    {
        var assemblies = EventCoverageScan.LocAssemblyHeThong(AppDomain.CurrentDomain.GetAssemblies());
        var ketQua = EventCoverageScan.Quet(assemblies);

        foreach (var ten in ketQua.AssemblyKhongNapDu)
        {
            _logger.LogWarning(
                "Không nạp được toàn bộ kiểu từ assembly {Assembly} khi quét độ phủ sự kiện — " +
                "kết quả quét có thể THIẾU người nhận.",
                ten);
        }

        if (ketQua.PhuSong.CoSuKienBiBoQuen)
        {
            _logger.LogWarning(
                "Quét độ phủ sự kiện: {SoLuong}/{TongSo} hợp đồng sự kiện KHÔNG có người nhận " +
                "(đã quét {SoAssembly} assembly).",
                ketQua.PhuSong.SuKienKhongCoNguoiNhan.Count,
                ketQua.PhuSong.SuKienKhongCoNguoiNhan.Count + ketQua.PhuSong.SuKienCoNguoiNhan.Count,
                ketQua.SoAssemblyDaQuet);
        }

        return ketQua;
    }
}
