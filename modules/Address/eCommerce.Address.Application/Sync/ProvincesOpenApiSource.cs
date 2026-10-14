using eCommerce.Address.Sync;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace eCommerce.Address;

/// <summary>
/// **Nguồn danh mục ĐVHC từ `provinces.open-api.vn`** — vỏ MỎNG: gọi HTTP rồi đưa JSON cho
/// <see cref="ProvincesOpenApiParser"/> (**lõi thuần, đã có test**).
///
/// 📌 VÌ SAO PHẢI GỌI **TỪNG TỈNH** (không phải một lần):
///   `GET /api/v2/p/` trả **34 tỉnh nhưng `wards` RỖNG** (đã kiểm tra trực tiếp 14/10/2026).
///   Muốn có cấp xã phải gọi `GET /api/v2/p/{mã}?depth=2` cho **từng** tỉnh ⇒ **35 lần gọi** cho v2.
///   Ai chỉ gọi endpoint danh sách sẽ nhận danh mục **thiếu toàn bộ cấp xã**.
///
/// 🔒 FAIL-CLOSED: nếu **bất kỳ** tỉnh nào gọi lỗi ⇒ **NÉM LỖI**, KHÔNG trả về danh mục thiếu.
///   Danh mục thiếu sẽ khiến lần đồng bộ sau **ĐÓNG** những xã bị thiếu ⇒ mọi vận đơn mới fail-closed.
///   Thà **không đồng bộ** còn hơn đồng bộ bằng dữ liệu khuyết.
/// </summary>
public class ProvincesOpenApiSource : IKhungDonViHanhChinhSource, ITransientDependency
{
    /// <summary>Tên client HTTP đăng ký ở tầng Host.</summary>
    public const string TenClientHttp = "ProvincesOpenApi";

    private readonly HttpClient _http;
    private readonly IClock _dongHo;

    public ProvincesOpenApiSource(HttpClient http, IClock dongHo)
    {
        _http = http;
        _dongHo = dongHo;
    }

    public async Task<DanhMucNguon> TaiDanhMucAsync(int schemaVersion, CancellationToken cancellationToken = default)
    {
        var (tienTo, doSau) = schemaVersion switch
        {
            2 => ("v2", 2),
            1 => ("v1", 3),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion,
                "Chỉ hỗ trợ phiên bản schema 1 (3 cấp) và 2 (2 cấp).")
        };

        // 1) Danh sách tỉnh (chỉ để lấy MÃ — `wards` ở đây rỗng).
        var jsonDanhSach = await _http.GetStringAsync($"api/{tienTo}/p/", cancellationToken);
        var maTinhs = ProvincesOpenApiParser.LayMaTinh(jsonDanhSach);

        // 2) Từng tỉnh, lấy đủ cấp dưới.
        var theoTinh = new List<IReadOnlyList<DonViNguon>>(maTinhs.Count);

        foreach (var maTinh in maTinhs)
        {
            // Không bọc try/catch: lỗi một tỉnh ⇒ ném ra ngoài ⇒ worker KHÔNG áp dụng gì.
            var jsonTinh = await _http.GetStringAsync($"api/{tienTo}/p/{maTinh}?depth={doSau}", cancellationToken);
            theoTinh.Add(ProvincesOpenApiParser.PhanTichTinh(jsonTinh, schemaVersion));
        }

        return ProvincesOpenApiParser.Ghep(theoTinh, _dongHo.Now);
    }
}
