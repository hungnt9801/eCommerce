using System.Net.Http.Json;
using eCommerce.EInvoice;
using Microsoft.Extensions.Logging;

namespace eCommerce.TaxAuthority;

/// <summary>
/// Client gọi **cơ quan thuế** (`EINV-002`) — mục (2) của đợt này.
///
/// 🎯 VÌ SAO CẦN LỚP NÀY VÀ VÌ SAO NÓ MỎNG:
///   Trước đợt này repo **không có `IHttpClientFactory`** (phát hiện F3 của `EINV-002`). Lớp này thêm
///   hạ tầng HTTP cho CQT nhưng **KHÔNG** chứa logic nghiệp vụ: việc phân tích `OK:`/`ERR:` đã nằm ở
///   `EInvoiceResponseParser` (đã có 13 test). Ở đây chỉ có: gọi HTTP, đọc chuỗi phản hồi, chuyển cho
///   parser, và **ghi log** kèm `CorrelationId` (đã wire ở `XC-005`).
///
/// 🔒 BẤT BIẾN: lớp này **KHÔNG BAO GIỜ** tự kết luận thành công. Mọi phán đoán đều do parser quyết định
/// (fail-closed). Nếu HTTP lỗi/timeout ⇒ **ném lỗi**, không trả "thành công".
///
/// ⚠️ **CHƯA KIỂM CHỨNG ĐƯỢC**: endpoint, định dạng SOAP/XML thật và **chứng thư sandbox** của CQT chưa có
/// ⇒ lớp này **chưa gọi được CQT thật**. Payload ở đây là **JSON tối thiểu** để chứng minh đường đi
/// (wiring + parser), sẽ thay bằng SOAP/XML khi có tài liệu chính thức.
/// </summary>
public class TaxAuthorityClient
{
    /// <summary>Tên client đã đăng ký trong DI (xem `eCommerceHttpApiHostModule`).</summary>
    public const string TenClientHttp = "TaxAuthority";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TaxAuthorityClient> _logger;

    public TaxAuthorityClient(IHttpClientFactory httpClientFactory, ILogger<TaxAuthorityClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gửi một hoá đơn (đã ký) tới CQT và phân tích phản hồi.
    /// </summary>
    /// <returns>Kết quả **do parser quyết định** — không tự suy diễn thành công.</returns>
    public async Task<KetQuaGuiCQT> GuiHoaDonAsync(
        string maTaiLieu,
        string xmlDaKy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(maTaiLieu) || string.IsNullOrWhiteSpace(xmlDaKy))
        {
            throw new ArgumentException("Thiếu mã tài liệu hoặc XML đã ký.", nameof(maTaiLieu));
        }

        var http = _httpClientFactory.CreateClient(TenClientHttp);

        _logger.LogInformation(
            "Gửi hoá đơn {MaTaiLieu} sang cơ quan thuế (endpoint {BaseAddress}).",
            maTaiLieu, http.BaseAddress);

        using var phanHoi = await http.PostAsJsonAsync(
            "submit",
            new { maTaiLieu, xmlDaKy },
            cancellationToken);

        // ⚠️ HTTP 500/404 cũng có thể chứa phản hồi CQT — đọc nội dung TRƯỚC, rồi mới xét mã trạng thái.
        var noiDung = await phanHoi.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(noiDung))
        {
            // 🚨 KHÔNG được coi là thành công khi không có nội dung phản hồi.
            _logger.LogWarning(
                "CQT trả về rỗng cho hoá đơn {MaTaiLieu} (HTTP {Status}).",
                maTaiLieu, (int)phanHoi.StatusCode);

            throw new InvalidOperationException(
                $"Cơ quan thuế trả về nội dung rỗng (HTTP {(int)phanHoi.StatusCode}) cho hoá đơn '{maTaiLieu}'.");
        }

        // Mọi phán đoán nằm ở parser (fail-closed).
        var ketQua = EInvoiceResponseParser.PhanTich(noiDung);

        if (ketQua.ThanhCong)
        {
            _logger.LogInformation(
                "CQT ĐÃ NHẬN hoá đơn {MaTaiLieu}; mã cơ quan thuế = {MaCQT}.", maTaiLieu, ketQua.MaCQT);
        }
        else
        {
            _logger.LogWarning(
                "CQT TỪ CHỐI hoá đơn {MaTaiLieu}; mã lỗi = {MaLoi}; mô tả = {MoTa}.",
                maTaiLieu, ketQua.MaLoi, ketQua.MoTa);
        }

        return ketQua;
    }
}
