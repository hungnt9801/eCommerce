using System.Globalization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;

namespace eCommerce.EInvoice;

/// <summary>
/// **Cổng đọc số hoá đơn lớn nhất đã cấp** cho một ký hiệu mẫu số.
/// Tách cổng để thuật toán cấp số **test được không cần DB**.
/// </summary>
public interface IEInvoiceNumberStore
{
    /// <summary>Số lớn nhất đã cấp cho ký hiệu mẫu số (0 nếu chưa cấp số nào).</summary>
    Task<long> SoLonNhatDaCapAsync(string kyHieuMauSo, CancellationToken cancellationToken = default);
}

/// <summary>
/// **Cấp số hoá đơn** — quy tắc: **liên tục, không trùng, không tái sử dụng**.
///
/// 🔒 VÌ SAO PHẢI GIỮ KHOÁ PHÂN TÁN QUANH VIỆC CẤP SỐ:
///   "đọc số lớn nhất rồi cộng 1" là thao tác **read-modify-write**. Hai yêu cầu đồng thời cùng đọc
///   được số lớn nhất cũ ⇒ **cả hai nhận cùng một số** ⇒ **trùng số hoá đơn**. Đây là lỗi pháp lý
///   (hoá đơn phải có số duy nhất, tăng liên tục), và nó **không** gây lỗi chương trình.
///   ⇒ Không giành được khoá thì **NÉM LỖI**, tuyệt đối không cấp số "tạm".
///
/// 🔒 VÌ SAO **KHÔNG** TÁI SỬ DỤNG SỐ CỦA HOÁ ĐƠN ĐÃ HUỶ:
///   Hoá đơn huỷ **vẫn giữ số** của nó (đã thông báo với cơ quan thuế). Vì vậy số tiếp theo luôn lấy
///   theo **số lớn nhất đã cấp**, không theo "số hoá đơn còn hiệu lực" ⇒ huỷ hoá đơn **không** giải
///   phóng số. Cách cài đặt dưới đây tự nhiên thoả mãn điều đó.
/// </summary>
public class EInvoiceNumberIssuer : ITransientDependency
{
    /// <summary>Độ dài số hoá đơn (8 chữ số, có số 0 đứng đầu) — theo thông lệ ký hiệu/số hoá đơn VN.</summary>
    public const int DoDaiSo = 8;

    /// <summary>Chờ giành khoá tối đa khi cấp số.</summary>
    public static readonly TimeSpan ChoKhoaToiDa = TimeSpan.FromSeconds(5);

    private readonly IEInvoiceNumberStore _store;
    private readonly IAbpDistributedLock _khoaPhanTan;

    public EInvoiceNumberIssuer(IEInvoiceNumberStore store, IAbpDistributedLock khoaPhanTan)
    {
        _store = store;
        _khoaPhanTan = khoaPhanTan;
    }

    /// <summary>
    /// Tên khoá phân tán để **tuần tự hoá** việc cấp số theo ký hiệu mẫu số.
    /// 🔒 KHÔNG chứa tiền tố `ecommerce:` (tiền tố do `AbpDistributedLockOptions.KeyPrefix` cấp —
    /// thêm vào đây sẽ thành **tiền tố đôi**, đúng lỗi C13 đã sửa).
    /// </summary>
    public static string KhoaCapSo(string kyHieuMauSo) => $"einvoice:number:{kyHieuMauSo}";

    /// <summary>Cấp số hoá đơn kế tiếp cho một ký hiệu mẫu số.</summary>
    public async Task<string> CapSoMoiAsync(string kyHieuMauSo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kyHieuMauSo))
        {
            throw new BusinessException(EInvoiceNumberingErrorCodes.ThieuKyHieuMauSo);
        }

        var kyHieu = kyHieuMauSo.Trim();

        await using var handle = await _khoaPhanTan.TryAcquireAsync(
            KhoaCapSo(kyHieu), ChoKhoaToiDa, cancellationToken);

        if (handle is null)
        {
            // 🚨 KHÔNG được cấp số khi không giữ khoá (xem ghi chú đầu class).
            throw new BusinessException(EInvoiceNumberingErrorCodes.KhongGianhDuocKhoaCapSo)
                .WithData("kyHieuMauSo", kyHieu);
        }

        var soLonNhat = await _store.SoLonNhatDaCapAsync(kyHieu, cancellationToken);

        return DinhDang(soLonNhat + 1);
    }

    /// <summary>Định dạng số hoá đơn: 8 chữ số, thêm 0 ở đầu.</summary>
    public static string DinhDang(long so)
    {
        if (so <= 0)
        {
            throw new BusinessException(EInvoiceNumberingErrorCodes.SoHoaDonKhongHopLe).WithData("so", so);
        }

        return so.ToString(CultureInfo.InvariantCulture).PadLeft(DoDaiSo, '0');
    }

    /// <summary>Đọc số từ chuỗi số hoá đơn (để so sánh/tìm số lớn nhất).</summary>
    public static long DocSo(string soHoaDon)
    {
        if (string.IsNullOrWhiteSpace(soHoaDon) || !long.TryParse(soHoaDon.Trim(), out var so) || so <= 0)
        {
            throw new BusinessException(EInvoiceNumberingErrorCodes.SoHoaDonKhongHopLe)
                .WithData("soHoaDon", soHoaDon);
        }

        return so;
    }

    /// <summary>
    /// Kiểm tra **tính liên tục** của dãy số hoá đơn — dùng cho báo cáo/đối chiếu với cơ quan thuế.
    /// Trả về danh sách các khoảng số bị thiếu (rỗng = liên tục).
    /// </summary>
    public static IReadOnlyList<(long Tu, long Den)> TimKhoangThieu(IEnumerable<string> soHoaDon)
    {
        ArgumentNullException.ThrowIfNull(soHoaDon);

        var so = soHoaDon.Select(DocSo).Distinct().OrderBy(x => x).ToList();

        var thieu = new List<(long Tu, long Den)>();

        for (var i = 1; i < so.Count; i++)
        {
            if (so[i] - so[i - 1] > 1)
            {
                thieu.Add((so[i - 1] + 1, so[i] - 1));
            }
        }

        return thieu;
    }
}
