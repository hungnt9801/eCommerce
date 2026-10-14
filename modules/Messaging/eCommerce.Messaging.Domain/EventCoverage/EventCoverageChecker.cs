namespace eCommerce.Messaging.EventCoverage;

/// <summary>Kết quả kiểm tra độ phủ: sự kiện nào có người nhận, sự kiện nào bị bỏ quên.</summary>
public sealed record KetQuaPhuSongSuKien(
    IReadOnlyList<string> SuKienKhongCoNguoiNhan,
    IReadOnlyList<string> SuKienCoNguoiNhan)
{
    /// <summary>Có sự kiện được publish nhưng KHÔNG ai nhận ⇒ dữ liệu sẽ mất.</summary>
    public bool CoSuKienBiBoQuen => SuKienKhongCoNguoiNhan.Count > 0;
}

/// <summary>
/// **LÕI THUẦN kiểm tra "sự kiện được publish có người nhận hay không"** — không phụ thuộc ABP,
/// không I/O ⇒ test được toàn bộ.
///
/// 🚨 VÌ SAO CẦN (lỗi thật phát hiện 14/10/2026 — mức nghiêm trọng nhất trong dự án):
///   `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho **từng đơn hàng kéo từ Shopee/Lazada**,
///   nhưng **KHÔNG có handler nào** đăng ký nhận sự kiện đó. Hệ quả:
///   • Đồng bộ báo **THÀNH CÔNG** (`OrdersPulled = N`, `LastError = null`, log mức Information),
///   • `ChannelShopSyncer` **TIẾN CON TRỎ ĐỒNG BỘ** (`shop.LastSyncedAtUtc = pull.NewSyncCursorUtc`)
///     *sau khi* publish ⇒ sàn **KHÔNG BAO GIỜ gửi lại** những đơn đó,
///   • ⇒ đơn hàng **MẤT VĨNH VIỄN**, và **không có lỗi nào được ghi**.
///
///   Đây là dạng nguy hiểm nhất: **báo thành công trong khi không làm gì**. Outbox KHÔNG cứu được ca này
///   — outbox bảo đảm *giao tin cậy*, nhưng giao **cho không ai** thì vẫn mất. Phải kiểm tra **độ phủ**
///   (có người nhận hay không) trước khi bàn tới chuyện giao tin cậy.
///
/// 🎯 CÁCH DÙNG: gọi lúc khởi động ⇒ biến lỗi **im lặng** thành lỗi **ồn ào ở mọi lần boot**.
/// </summary>
public static class EventCoverageChecker
{
    /// <summary>
    /// Tên các interface đánh dấu "người nhận sự kiện" (so khớp theo TIỀN TỐ vì tên interface generic
    /// có hậu tố `` `1 ``). So khớp theo tên để lõi này **không phụ thuộc ABP**.
    /// </summary>
    public static readonly string[] TenInterfaceNguoiNhan =
    [
        "IDistributedEventHandler",
        "ILocalEventHandler",
        "IEventHandler"
    ];

    /// <summary>Hậu tố quy ước của hợp đồng sự kiện.</summary>
    public const string HauToSuKien = "Eto";

    /// <summary>Hậu tố quy ước của **ETO CON** (dòng chi tiết trong một sự kiện) — không publish độc lập.</summary>
    public const string HauToSuKienCon = "LineEto";

    /// <summary>
    /// ETO con (ví dụ `ChannelOrderPulledLineEto` là một DÒNG của `ChannelOrderPulledEto`) — nó **không**
    /// phải sự kiện publish độc lập nên **không** được tính vào kiểm tra độ phủ. Nếu tính, báo cáo sẽ có
    /// **dương tính giả** và người đọc sẽ bắt đầu bỏ qua cảnh báo (đúng thứ cần tránh).
    /// </summary>
    public static bool LaSuKienCon(Type kieu)
        => kieu.Name.EndsWith(HauToSuKienCon, StringComparison.Ordinal);

    /// <summary>
    /// Đối chiếu **sự kiện được publish** với **người nhận đã đăng ký**.
    /// </summary>
    /// <param name="cacSuKien">Các kiểu sự kiện đang được publish (ví dụ mọi kiểu tên kết thúc bằng `Eto`).</param>
    /// <param name="cacNguoiNhan">Các kiểu handler đã đăng ký.</param>
    public static KetQuaPhuSongSuKien KiemTra(
        IEnumerable<Type> cacSuKien,
        IEnumerable<Type> cacNguoiNhan)
    {
        ArgumentNullException.ThrowIfNull(cacSuKien);
        ArgumentNullException.ThrowIfNull(cacNguoiNhan);

        var duocNhan = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nguoiNhan in cacNguoiNhan)
        {
            foreach (var giaoDien in nguoiNhan.GetInterfaces())
            {
                if (!giaoDien.IsGenericType)
                {
                    continue;
                }

                var tenGiaoDien = giaoDien.GetGenericTypeDefinition().Name;

                if (!TenInterfaceNguoiNhan.Any(t => tenGiaoDien.StartsWith(t, StringComparison.Ordinal)))
                {
                    continue;
                }

                var thamSo = giaoDien.GetGenericArguments();

                if (thamSo.Length == 1)
                {
                    duocNhan.Add(thamSo[0].Name);
                }
            }
        }

        // Sắp xếp + khử trùng để kết quả ỔN ĐỊNH (test và log không phụ thuộc thứ tự assembly nạp).
        var tatCa = cacSuKien
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => !LaSuKienCon(t))
            .Select(t => t.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var khongCo = tatCa.Where(x => !duocNhan.Contains(x)).ToList();
        var coNguoiNhan = tatCa.Where(duocNhan.Contains).ToList();

        return new KetQuaPhuSongSuKien(khongCo, coNguoiNhan);
    }
}
