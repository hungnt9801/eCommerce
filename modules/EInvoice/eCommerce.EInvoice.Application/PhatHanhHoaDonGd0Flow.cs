using System.Text.Json;
using eCommerce.Address;
using eCommerce.Signing;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.EInvoice.Application;

// ═══════════════════════════════════════════════════════════════════════════════
//  LUỒNG NGHIỆP VỤ GĐ0 — NỐI 3 MODULE ĐANG LÀ "ĐẢO": Address → EInvoice → Signing
//  Xác minh địa chỉ → cấp số hoá đơn → tạo hoá đơn nháp → tạo yêu cầu ký (bước 1)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Đầu vào luồng phát hành hoá đơn GĐ0.</summary>
public sealed record PhatHanhHoaDonGd0Input(
    int SchemaVersion,
    string TinhCode,
    string XaCode,
    string KyHieuMauSo,
    string SoHoaDon,
    DateTime NgayLap,
    Guid? TenantId = null);

/// <summary>Kết quả luồng: hoá đơn nháp + yêu cầu ký **đã gắn hash của tài liệu (có địa chỉ)**.</summary>
public sealed record KeHoachPhatHanh(
    EInvoice HoaDon,
    SigningRequest YeuCauKy,
    string HashTaiLieu,
    string DiaChiDaXacMinh);

/// <summary>
/// **LÕI THUẦN của luồng GĐ0** — không DI, không DB, không đọc đồng hồ hệ thống.
///
/// 🎯 VÌ SAO TÁCH LÕI THUẦN: luồng này là **chuỗi phụ thuộc pháp lý** — địa chỉ sai thì hoá đơn sai,
/// hoá đơn sai thì chữ ký vô hiệu. Nếu nhồi toàn bộ vào app service (có repository, có transaction)
/// thì **không test được** các nhánh fail-closed. Tách lõi ⇒ test được **mọi** nhánh.
///
/// 🔒 BẤT BIẾN QUAN TRỌNG NHẤT — **ĐỊA CHỈ NẰM TRONG HASH ĐƯỢC KÝ**:
///   Hash tài liệu được tính trên (ký hiệu, số, ngày lập, **phiên bản schema, mã+tên tỉnh, mã+tên xã**).
///   Nhờ vậy **đổi địa chỉ ⇒ đổi hash** ⇒ chữ ký cũ **không còn khớp** (bị `SigningRequest` từ chối).
///   Nếu KHÔNG đưa địa chỉ vào hash thì kẻ xấu (hoặc lỗi) có thể **sửa địa chỉ trên hoá đơn sau khi ký**
///   mà chữ ký **vẫn "hợp lệ"** — hoá đơn sai địa chỉ nhưng không có cảnh báo nào.
/// </summary>
public static class PhatHanhHoaDonGd0Planner
{
    public static KeHoachPhatHanh Lap(IEnumerable<AdministrativeUnit> donVi, PhatHanhHoaDonGd0Input dauVao)
    {
        ArgumentNullException.ThrowIfNull(donVi);
        ArgumentNullException.ThrowIfNull(dauVao);

        // ① XÁC MINH ĐỊA CHỈ — FAIL-CLOSED. Sai ⇒ NÉM LỖI ngay, KHÔNG tạo hoá đơn.
        var kq = AdministrativeUnitResolver.KiemTraHaiCap(
            donVi, dauVao.SchemaVersion, dauVao.TinhCode, dauVao.XaCode);

        if (!kq.HopLe)
        {
            throw new BusinessException(kq.ErrorCode ?? AddressErrorCodes.KhongTimThayDonVi)
                .WithData("thongDiep", kq.ThongDiep)
                .WithData("tinhCode", dauVao.TinhCode)
                .WithData("xaCode", dauVao.XaCode);
        }

        var tinh = kq.Tinh!;
        var xa = kq.Xa!;

        // ② HOÁ ĐƠN NHÁP (chưa ký, chưa phát hành). Số do nơi gọi cấp qua khoá phân tán.
        var hoaDon = EInvoice.TaoMoi(
            dauVao.KyHieuMauSo, dauVao.SoHoaDon, dauVao.NgayLap, dauVao.TenantId);

        // ③ HASH TÀI LIỆU — **BAO GỒM ĐỊA CHỈ ĐÃ XÁC MINH** (xem ghi chú đầu class).
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kyHieuMauSo"] = hoaDon.KyHieuMauSo,
            ["soHoaDon"] = hoaDon.SoHoaDon,
            ["ngayLap"] = hoaDon.NgayLap.ToString("O"),
            ["schemaVersion"] = dauVao.SchemaVersion,
            ["tinhCode"] = tinh.Code,
            ["tinhTen"] = tinh.Name,
            ["xaCode"] = xa.Code,
            ["xaTen"] = xa.Name
        });

        var hash = Messaging.Idempotency.CanonicalJsonHasher.Bam(payload);

        // ④ YÊU CẦU KÝ (BƯỚC 1 của quy trình ký 2 bước) — ghi lại hash để đối chiếu ở bước 2.
        var yeuCauKy = SigningRequest.TaoYeuCau(
            $"HD:{hoaDon.KyHieuMauSo}/{hoaDon.SoHoaDon}", hash, "SHA256", dauVao.TenantId);

        return new KeHoachPhatHanh(
            hoaDon, yeuCauKy, hash, $"{xa.Name}, {tinh.Name}");
    }
}

// ─────────────────────────── DTO cho API ───────────────────────────

public sealed record PhatHanhHoaDonGd0Request(
    int SchemaVersion,
    string TinhCode,
    string XaCode,
    string KyHieuMauSo,
    DateTime NgayLap);

public sealed record PhatHanhHoaDonGd0Result(
    Guid HoaDonId,
    string SoHoaDon,
    string KyHieuMauSo,
    EInvoiceStatus TrangThai,
    string DiaChiDaXacMinh,
    string HashTaiLieu,
    Guid YeuCauKyId,
    SigningRequestStatus TrangThaiKy);

// ─────────────────────────── App service (vỏ MỎNG) ───────────────────────────

/// <summary>
/// **API luồng phát hành hoá đơn GĐ0** — nối `Address` → `EInvoice` → `Signing`.
///
/// 📌 Vỏ này **mỏng có chủ ý**: nạp dữ liệu, cấp số qua khoá phân tán, gọi lõi thuần, lưu.
/// Mọi luật nghiệp vụ nằm ở `PhatHanhHoaDonGd0Planner` (test được) và ở các tầng Domain đã kiểm chứng.
/// </summary>
[Authorize]
public class PhatHanhHoaDonGd0Service : ApplicationService
{
    private readonly IRepository<AdministrativeUnit, Guid> _donVi;
    private readonly IRepository<EInvoice, Guid> _hoaDon;
    private readonly IRepository<SigningRequest, Guid> _yeuCauKy;
    private readonly EInvoiceNumberIssuer _capSo;

    public PhatHanhHoaDonGd0Service(
        IRepository<AdministrativeUnit, Guid> donVi,
        IRepository<EInvoice, Guid> hoaDon,
        IRepository<SigningRequest, Guid> yeuCauKy,
        EInvoiceNumberIssuer capSo)
    {
        _donVi = donVi;
        _hoaDon = hoaDon;
        _yeuCauKy = yeuCauKy;
        _capSo = capSo;
    }

    public async Task<PhatHanhHoaDonGd0Result> PhatHanhAsync(PhatHanhHoaDonGd0Request yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var tatCaDonVi = await _donVi.GetListAsync();

        // Cấp số TRƯỚC (qua khoá phân tán — đã kiểm chứng): ném lỗi nếu không giành được khoá,
        // và như vậy KHÔNG tạo hoá đơn nào khi chưa chắc chắn có số hợp lệ.
        var soHoaDon = await _capSo.CapSoMoiAsync(yeuCau.KyHieuMauSo);

        var keHoach = PhatHanhHoaDonGd0Planner.Lap(tatCaDonVi, new PhatHanhHoaDonGd0Input(
            yeuCau.SchemaVersion, yeuCau.TinhCode, yeuCau.XaCode,
            yeuCau.KyHieuMauSo, soHoaDon, yeuCau.NgayLap, CurrentTenant.Id));

        await _hoaDon.InsertAsync(keHoach.HoaDon, autoSave: true);
        await _yeuCauKy.InsertAsync(keHoach.YeuCauKy, autoSave: true);

        return new PhatHanhHoaDonGd0Result(
            keHoach.HoaDon.Id,
            keHoach.HoaDon.SoHoaDon,
            keHoach.HoaDon.KyHieuMauSo,
            keHoach.HoaDon.Status,
            keHoach.DiaChiDaXacMinh,
            keHoach.HashTaiLieu,
            keHoach.YeuCauKy.Id,
            keHoach.YeuCauKy.TrangThai);
    }
}
