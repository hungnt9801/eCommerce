using eCommerce.Signing;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.EInvoice.Application;

/// <summary>Mã lỗi của **luồng GĐ0** — dải `eCommerce:EInvoice:04xx`.</summary>
public static class Gd0FlowErrorCodes
{
    /// <summary>
    /// 🚨 Yêu cầu ký **không thuộc** hoá đơn đang xử lý.
    ///
    /// Đây là chốt chặn ở tầng LUỒNG cho lỗi "ký tài liệu A rồi gắn vào hoá đơn B". Tầng Domain đã chặn
    /// việc **hash không khớp**, nhưng nếu ai đó truyền **đúng hash** của một yêu cầu ký **khác** thì
    /// Domain không thể biết — chỉ luồng mới biết yêu cầu ký đó thuộc hoá đơn nào.
    /// </summary>
    public const string YeuCauKyKhongThuocHoaDon = "eCommerce:EInvoice:0401";
}

/// <summary>
/// **LÕI THUẦN của các bước còn lại** trong luồng GĐ0: ký → phát hành → gửi CQT → nhận kết quả.
///
/// 🎯 VÌ SAO CẦN LỚP NÀY (chứ không gọi thẳng Domain ở app service): các bước này **có THỨ TỰ bắt buộc**:
///   • phải **KÝ** trước khi **PHÁT HÀNH** (`eCommerce:EInvoice:0102`),
///   • phải **PHÁT HÀNH** trước khi **GỬI CQT** (`0105`),
///   • phải **GỬI** rồi mới **NHẬN KẾT QUẢ** (`0106`).
///   Tầng Domain đã cưỡng chế từng bước, nhưng **thứ tự giữa các bước** là việc của luồng. Gom vào một
///   lớp thuần ⇒ test được cả **chuỗi** lẫn **từng bước đi sai thứ tự**.
///
/// 📌 GHI CHÚ GIAO DỊCH: các hàm dưới đây **thay đổi entity** theo nhiều bước. Nơi gọi **PHẢI** chạy
/// trong **một Unit of Work** (app service của ABP làm việc này) để nếu bước sau ném lỗi thì bước trước
/// **cũng bị hoàn tác** — không để lại trạng thái nửa vời (ví dụ yêu cầu ký đã `DaKy` mà hoá đơn vẫn `Nhap`).
/// </summary>
public static class HoanTatLuongGd0Planner
{
    /// <summary>
    /// **BƯỚC 2 của ký** + **ký hoá đơn**.
    /// </summary>
    /// <param name="hashDaKy">Hash mà người dùng **thực sự đã ký** — Domain sẽ từ chối nếu không khớp.</param>
    public static void Ky(
        EInvoice hoaDon,
        SigningRequest yeuCauKy,
        SigningCertificate chungThu,
        string hashDaKy,
        string chuKy,
        string nguoiKy,
        string xmlDaKy,
        DateTime thoiDiemKy)
    {
        ArgumentNullException.ThrowIfNull(hoaDon);
        ArgumentNullException.ThrowIfNull(yeuCauKy);
        ArgumentNullException.ThrowIfNull(chungThu);

        // 🚨 CHỐT CHẶN Ở TẦNG LUỒNG: yêu cầu ký phải THUỘC hoá đơn này.
        var maMongDoi = $"HD:{hoaDon.KyHieuMauSo}/{hoaDon.SoHoaDon}";

        if (!string.Equals(yeuCauKy.MaTaiLieu, maMongDoi, StringComparison.Ordinal))
        {
            throw new BusinessException(Gd0FlowErrorCodes.YeuCauKyKhongThuocHoaDon)
                .WithData("maTaiLieuCuaYeuCauKy", yeuCauKy.MaTaiLieu)
                .WithData("maTaiLieuMongDoi", maMongDoi);
        }

        // Domain cưỡng chế: hash phải khớp + chứng thư còn hiệu lực TẠI THỜI ĐIỂM KÝ.
        yeuCauKy.HoanTatKy(hashDaKy, chungThu, chuKy, nguoiKy, thoiDiemKy);

        // Chỉ sau khi chữ ký hợp lệ mới đánh dấu hoá đơn đã ký.
        hoaDon.Ky(xmlDaKy);
    }

    /// <summary>**Phát hành** hoá đơn (Domain bắt buộc đã ký) rồi **gửi cơ quan thuế**.</summary>
    public static void PhatHanhVaGuiCqt(EInvoice hoaDon, DateTime thoiDiemGuiUtc)
    {
        ArgumentNullException.ThrowIfNull(hoaDon);

        hoaDon.PhatHanh();               // 0102 nếu chưa ký
        hoaDon.GuiCQT(thoiDiemGuiUtc);   // 0105 nếu chưa phát hành
    }

    /// <summary>
    /// **Nhận kết quả từ cơ quan thuế** và ghi vào hoá đơn.
    /// 🔒 Việc phán đoán `OK:`/`ERR:` do `EInvoiceResponseParser` quyết định (**fail-closed**) — lớp này
    /// **không** tự đoán thành công.
    /// </summary>
    /// <summary>
    /// **GỬI LẠI** hoá đơn đã phát hành (đường của job gửi lại — mục 4).
    ///
    /// 🚨 VÌ SAO PHẢI CÓ HÀM RIÊNG (test end-to-end đã bắt được lỗi này): PhatHanhVaGuiCqt gọi
    /// PhatHanh() trước — nhưng hoá đơn bị CQT từ chối thì ĐÃ phát hành rồi ⇒ gọi lại sẽ ném
    /// ChuyenTrangThaiKhongHopLe. Tức hàm gộp KHÔNG dùng lại được cho đường gửi lại. Đúng loại lỗi mà
    /// test CẢ CHUỖI sinh ra để tìm: từng bước riêng lẻ đều đúng nhưng THỨ TỰ của đường gửi lại thì khác.
    /// </summary>
    public static void GuiLaiCqt(EInvoice hoaDon, DateTime thoiDiemGuiUtc)
    {
        ArgumentNullException.ThrowIfNull(hoaDon);

        // KHÔNG gọi PhatHanh() ở đây: hoá đơn đã phát hành. Domain vẫn cưỡng chế "phải đã phát hành"
        // (ném 0105 nếu chưa) và "không gửi hai lần liên tiếp" (ném 0101 nếu đang DaGui).
        hoaDon.GuiCQT(thoiDiemGuiUtc);
    }
    public static KetQuaGuiCQT NhanKetQuaCqt(EInvoice hoaDon, string phanHoiCqt)
    {
        ArgumentNullException.ThrowIfNull(hoaDon);

        var ketQua = EInvoiceResponseParser.PhanTich(phanHoiCqt);

        if (ketQua.ThanhCong)
        {
            hoaDon.NhanKetQuaCQT(ketQua.MaCQT!);
        }
        else
        {
            hoaDon.BiTuChoiCQT(ketQua.MaLoi ?? "khong-ro");
        }

        return ketQua;
    }
}

// ─────────────────────────── DTO + app service ───────────────────────────

public sealed record KyHoanTatGd0Request(
    string HashDaKy,
    Guid ChungThuSoId,
    string ChuKy,
    string NguoiKy,
    string XmlDaKy,
    DateTime ThoiDiemKy);

public sealed record KetQuaGuiCqtGd0Result(
    Guid HoaDonId,
    string SoHoaDon,
    EInvoiceStatus TrangThai,
    TaxAuthorityStatus TrangThaiCqt,
    string? MaCQT,
    string? LoiCQT,
    bool ThanhCong);

/// <summary>
/// **API hoàn tất luồng GĐ0**: ký → gửi CQT → nhận kết quả.
/// Vỏ **mỏng**: nạp entity, gọi lõi thuần, lưu. `[Authorize]` ở CẤP CLASS (quy ước #6).
/// </summary>
[Authorize]
public class HoanTatLuongGd0Service : ApplicationService
{
    private readonly IRepository<EInvoice, Guid> _hoaDon;
    private readonly IRepository<SigningRequest, Guid> _yeuCauKy;
    private readonly IRepository<SigningCertificate, Guid> _chungThu;

    public HoanTatLuongGd0Service(
        IRepository<EInvoice, Guid> hoaDon,
        IRepository<SigningRequest, Guid> yeuCauKy,
        IRepository<SigningCertificate, Guid> chungThu)
    {
        _hoaDon = hoaDon;
        _yeuCauKy = yeuCauKy;
        _chungThu = chungThu;
    }

    /// <summary>**BƯỚC 2** — nhận kết quả ký cho hoá đơn. Cả hai entity được lưu trong **cùng một UoW**.</summary>
    public async Task<EInvoiceDto> KyAsync(Guid hoaDonId, Guid yeuCauKyId, KyHoanTatGd0Request yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var hoaDon = await _hoaDon.GetAsync(hoaDonId);
        var yeuCauKy = await _yeuCauKy.GetAsync(yeuCauKyId);
        var chungThu = await _chungThu.GetAsync(yeuCau.ChungThuSoId);

        HoanTatLuongGd0Planner.Ky(
            hoaDon, yeuCauKy, chungThu,
            yeuCau.HashDaKy, yeuCau.ChuKy, yeuCau.NguoiKy, yeuCau.XmlDaKy, yeuCau.ThoiDiemKy);

        // ⚠️ Lưu cả hai TRONG CÙNG một lần gọi app service ⇒ cùng một UoW ⇒ hoặc cả hai, hoặc không cái nào.
        await _yeuCauKy.UpdateAsync(yeuCauKy, autoSave: true);
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    /// <summary>**Phát hành + gửi CQT** trong một bước.</summary>
    public async Task<EInvoiceDto> PhatHanhVaGuiCqtAsync(Guid hoaDonId)
    {
        var hoaDon = await _hoaDon.GetAsync(hoaDonId);

        HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, Clock.Now);

        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return EInvoiceDto.Tu(hoaDon);
    }

    /// <summary>**Nhận kết quả CQT** (job hoặc người dùng dán phản hồi vào).</summary>
    public async Task<KetQuaGuiCqtGd0Result> NhanKetQuaCqtAsync(Guid hoaDonId, string phanHoiCqt)
    {
        var hoaDon = await _hoaDon.GetAsync(hoaDonId);

        var ketQua = HoanTatLuongGd0Planner.NhanKetQuaCqt(hoaDon, phanHoiCqt);

        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        return new KetQuaGuiCqtGd0Result(
            hoaDon.Id, hoaDon.SoHoaDon, hoaDon.Status, hoaDon.TaxAuthorityStatus,
            hoaDon.MaCQT, hoaDon.LoiCQT, ketQua.ThanhCong);
    }
}
