using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.EInvoice;

/// <summary>
/// **Hoá đơn điện tử** — aggregate gốc của module (theo `NĐ 254/2026/NĐ-CP` + `TT 91/2026/TT-BTC`).
///
/// 🔒 BẤT BIẾN:
///   1. **Hai trục trạng thái ĐỘC LẬP** — phát hành (<see cref="Status"/>) và gửi CQT
///      (<see cref="TaxAuthorityStatus"/>). Không gộp: hoá đơn có thể **đã phát hành** mà CQT
///      **từ chối**, và cả hai sự thật đó đều phải được lưu.
///   2. **State machine NÉM LỖI** khi chuyển sai (quy ước #11) — không im lặng bỏ qua.
///   3. **Không xoá hoá đơn**: điều chỉnh/thay thế/huỷ đều là **chuyển trạng thái + trỏ tới hoá đơn
///      liên quan**, giữ nguyên bản ghi gốc (yêu cầu của cơ quan thuế và của kiểm toán).
/// </summary>
public class EInvoice : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Ký hiệu mẫu số (ví dụ <c>1C26TAA</c>) — theo quy định về ký hiệu hoá đơn.</summary>
    public string KyHieuMauSo { get; protected set; } = default!;

    /// <summary>Số hoá đơn (do hệ thống cấp theo quy tắc liên tục).</summary>
    public string SoHoaDon { get; protected set; } = default!;

    public DateTime NgayLap { get; protected set; }

    public EInvoiceStatus Status { get; protected set; }

    public TaxAuthorityStatus TaxAuthorityStatus { get; protected set; }

    /// <summary>Mã giao dịch/mã cấp của cơ quan thuế khi đã nhận.</summary>
    public string? MaCQT { get; protected set; }

    /// <summary>Lý do điều chỉnh / thay thế / huỷ (BẮT BUỘC khi thực hiện các thao tác đó).</summary>
    public string? LyDo { get; protected set; }

    /// <summary>
    /// Id của **hoá đơn ĐIỀU CHỈNH** — đặt trên **HOÁ ĐƠN GỐC** khi gốc bị điều chỉnh.
    ///
    /// 🚨 TÊN TRƯỜNG ĐỌC NGƯỢC CHIỀU VỚI NỘI DUNG (đã đối chiếu và sửa tài liệu 14/10/2026):
    ///   • Tài liệu CŨ ghi *"hoá đơn bị điều chỉnh (nếu hoá đơn này là bản điều chỉnh)"* ⇒ **SAI**.
    ///   • Thực tế: <see cref="DieuChinh"/> được gọi trên **gốc** (yêu cầu gốc đang `DaPhatHanh`) và
    ///     đặt gốc thành `DaDieuChinh`; trường này lưu **Id của BẢN ĐIỀU CHỈNH**.
    ///   • Chiều đúng: **GỐC → BẢN ĐIỀU CHỈNH**. Muốn tìm bản điều chỉnh của gốc X thì lọc
    ///     `Id == X.Id` rồi đọc trường này — **KHÔNG** lọc `HoaDonBiDieuChinhId == X.Id` (đó là chiều ngược,
    ///     sẽ trả về **rỗng** và bị hiểu nhầm là "chưa điều chỉnh").
    /// </summary>
    public Guid? HoaDonBiDieuChinhId { get; protected set; }

    /// <summary>
    /// Id của **hoá đơn THAY THẾ** — đặt trên **HOÁ ĐƠN GỐC** khi gốc bị thay thế.
    /// 🚨 Cùng cảnh báo chiều như <see cref="HoaDonBiDieuChinhId"/>: chiều đúng là
    /// **GỐC → BẢN THAY THẾ**, và gốc chuyển sang `DaThayThe`.
    /// </summary>
    public Guid? HoaDonBiThayTheId { get; protected set; }

    /// <summary>Lỗi CQT trả về khi bị từ chối (chuỗi <c>ERR:</c> — xem `EINV-002`).</summary>
    public string? LoiCQT { get; protected set; }

    /// <summary>Số lần đã gửi cơ quan thuế — đầu vào cho <see cref="EInvoiceResubmissionPolicy"/> (ngưỡng 5).</summary>
    public int SoLanGuiCQT { get; protected set; }

    /// <summary>Thời điểm gửi gần nhất — để chính sách giãn cách tối thiểu giữa hai lần gửi.</summary>
    public DateTime? LanGuiCuoiUtc { get; protected set; }

    /// <summary>Ảnh chụp XML đã ký (để đối chiếu về sau và không phụ thuộc NCC còn giữ hay không).</summary>
    public string? XmlDaKy { get; protected set; }

    protected EInvoice()
    {
    }

    public static EInvoice TaoMoi(
        string kyHieuMauSo,
        string soHoaDon,
        DateTime ngayLap,
        Guid? tenantId = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(kyHieuMauSo) || string.IsNullOrWhiteSpace(soHoaDon))
        {
            throw new BusinessException(EInvoiceErrorCodes.ThieuThongTinBatBuoc)
                .WithData("kyHieuMauSo", kyHieuMauSo)
                .WithData("soHoaDon", soHoaDon);
        }

        return new EInvoice
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            KyHieuMauSo = kyHieuMauSo.Trim(),
            SoHoaDon = soHoaDon.Trim(),
            NgayLap = ngayLap,
            Status = EInvoiceStatus.Nhap,
            TaxAuthorityStatus = TaxAuthorityStatus.ChuaGui
        };
    }

    // ── Hàm kiểm tra dùng chung ────────────────────────────────────────────

    private void YeuCauTrangThai(EInvoiceStatus mongDoi, string hanhDong)
    {
        if (Status == EInvoiceStatus.DaHuy)
        {
            throw new BusinessException(EInvoiceErrorCodes.HoaDonDaKetThuc)
                .WithData("hanhDong", hanhDong)
                .WithData("trangThai", Status.ToString());
        }

        if (Status != mongDoi)
        {
            throw new BusinessException(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("hanhDong", hanhDong)
                .WithData("trangThaiHienTai", Status.ToString())
                .WithData("trangThaiYeuCau", mongDoi.ToString());
        }
    }

    private static string YeuCauLyDo(string? lyDo, string hanhDong)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            // 🚨 Lý do là thông tin BẮT BUỘC trên chứng từ điều chỉnh/thay thế/huỷ. Thiếu nó thì
            // chứng từ không hợp lệ — phải ném lỗi, không được để trống rồi gửi CQT.
            throw new BusinessException(EInvoiceErrorCodes.ThieuLyDo).WithData("hanhDong", hanhDong);
        }

        return lyDo.Trim();
    }

    // ── Trục 1: phát hành ──────────────────────────────────────────────────

    /// <summary>Bước ký số hoàn tất (bước 2 của quy trình ký thủ công — `SIGN-001`).</summary>
    public void Ky(string xmlDaKy)
    {
        if (string.IsNullOrWhiteSpace(xmlDaKy))
        {
            throw new BusinessException(EInvoiceErrorCodes.ThieuThongTinBatBuoc)
                .WithData("lyDo", "XML đã ký rỗng");
        }

        YeuCauTrangThai(EInvoiceStatus.Nhap, nameof(Ky));

        Status = EInvoiceStatus.DaKy;
        XmlDaKy = xmlDaKy;
    }

    /// <summary>
    /// Phát hành hoá đơn cho khách.
    /// 🚨 **BẮT BUỘC đã ký** — không có đường phát hành hoá đơn chưa ký.
    /// </summary>
    public void PhatHanh()
    {
        if (Status == EInvoiceStatus.Nhap)
        {
            throw new BusinessException(EInvoiceErrorCodes.ChuaKyKhongDuocPhatHanh)
                .WithData("soHoaDon", SoHoaDon);
        }

        YeuCauTrangThai(EInvoiceStatus.DaKy, nameof(PhatHanh));

        Status = EInvoiceStatus.DaPhatHanh;
    }

    /// <summary>
    /// Điều chỉnh hoá đơn đã phát hành (hoá đơn **này** = GỐC, chuyển sang <see cref="EInvoiceStatus.DaDieuChinh"/>;
    /// <paramref name="hoaDonDieuChinhId"/> = Id **bản điều chỉnh**).
    /// </summary>
    public void DieuChinh(string lyDo, Guid hoaDonDieuChinhId)
    {
        YeuCauTrangThai(EInvoiceStatus.DaPhatHanh, nameof(DieuChinh));

        LyDo = YeuCauLyDo(lyDo, nameof(DieuChinh));
        HoaDonBiDieuChinhId = YeuCauHoaDonLienKet(hoaDonDieuChinhId, nameof(DieuChinh));
        Status = EInvoiceStatus.DaDieuChinh;
    }

    /// <summary>
    /// Thay thế hoá đơn đã phát hành (hoá đơn **này** = GỐC, chuyển sang <see cref="EInvoiceStatus.DaThayThe"/>;
    /// <paramref name="hoaDonThayTheId"/> = Id **bản thay thế**).
    /// </summary>
    public void ThayThe(string lyDo, Guid hoaDonThayTheId)
    {
        YeuCauTrangThai(EInvoiceStatus.DaPhatHanh, nameof(ThayThe));

        LyDo = YeuCauLyDo(lyDo, nameof(ThayThe));
        HoaDonBiThayTheId = YeuCauHoaDonLienKet(hoaDonThayTheId, nameof(ThayThe));
        Status = EInvoiceStatus.DaThayThe;
    }

    /// <summary>
    /// Kiểm tra Id hoá đơn liên kết (thay thế / điều chỉnh).
    ///
    /// 🚨 VÌ SAO PHẢI KIỂM: trước 14/10/2026 hàm nhận **mọi** `Guid`. Hệ quả:
    ///   • `Guid.Empty` ⇒ hoá đơn gốc chuyển sang `DaThayThe` nhưng **trỏ tới hư không** — gốc coi như
    ///     "đã được xử lý" mà **không có chứng từ thay thế nào tồn tại**.
    ///   • `hoaDonThayTheId == Id` ⇒ hoá đơn **tự thay thế chính nó** — vòng lặp vô nghĩa.
    ///   Việc "hoá đơn đích có thật và đã phát hành hay không" được kiểm ở **tầng gọi** (cần đọc DB);
    ///   ở đây chặn những giá trị **chắc chắn vô nghĩa** mà không cần I/O.
    /// </summary>
    private Guid YeuCauHoaDonLienKet(Guid id, string hanhDong)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessException(EInvoiceErrorCodes.ThieuHoaDonLienKet)
                .WithData("hanhDong", hanhDong)
                .WithData("lyDo", "Id hoá đơn liên kết rỗng");
        }

        if (id == Id)
        {
            throw new BusinessException(EInvoiceErrorCodes.ThieuHoaDonLienKet)
                .WithData("hanhDong", hanhDong)
                .WithData("lyDo", "hoá đơn không thể liên kết tới chính nó");
        }

        return id;
    }

    /// <summary>
    /// Huỷ hoá đơn. Chỉ từ <see cref="EInvoiceStatus.DaKy"/> hoặc <see cref="EInvoiceStatus.DaPhatHanh"/>.
    /// 🚨 Không huỷ được hoá đơn **đã điều chỉnh / đã thay thế** — chúng đã được xử lý bằng chứng từ khác;
    /// huỷ thêm sẽ làm **hai chứng từ cùng "xoá" một nghĩa vụ** ⇒ sổ sách không đối chiếu được.
    /// </summary>
    public void Huy(string lyDo)
    {
        if (Status is not (EInvoiceStatus.DaKy or EInvoiceStatus.DaPhatHanh))
        {
            if (Status == EInvoiceStatus.DaHuy)
            {
                throw new BusinessException(EInvoiceErrorCodes.HoaDonDaKetThuc).WithData("hanhDong", nameof(Huy));
            }

            throw new BusinessException(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("hanhDong", nameof(Huy))
                .WithData("trangThaiHienTai", Status.ToString())
                .WithData("choPhep", "DaKy, DaPhatHanh");
        }

        LyDo = YeuCauLyDo(lyDo, nameof(Huy));
        Status = EInvoiceStatus.DaHuy;
    }

    // ── Trục 2: gửi cơ quan thuế (ĐỘC LẬP với trục 1) ──────────────────────

    /// <summary>Gửi hoá đơn sang cơ quan thuế. Chỉ khi **đã phát hành**.</summary>
    /// <param name="thoiDiemGuiUtc">
    /// Thời điểm gửi (UTC) — **truyền vào** thay vì đọc đồng hồ hệ thống để hàm **test được** và để
    /// chính sách giãn cách (`EInvoiceResubmissionPolicy`) có dữ liệu thật.
    /// </param>
    public void GuiCQT(DateTime thoiDiemGuiUtc)
    {
        if (Status != EInvoiceStatus.DaPhatHanh)
        {
            throw new BusinessException(EInvoiceErrorCodes.ChuaPhatHanhKhongDuocGuiCQT)
                .WithData("trangThai", Status.ToString());
        }

        if (TaxAuthorityStatus == TaxAuthorityStatus.DaGui)
        {
            throw new BusinessException(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("hanhDong", nameof(GuiCQT))
                .WithData("trangThaiCQT", TaxAuthorityStatus.ToString());
        }

        TaxAuthorityStatus = TaxAuthorityStatus.DaGui;
        LoiCQT = null;

        // Bộ đếm phục vụ chính sách gửi lại: đếm MỌI lần gửi (kể cả lần sau khi bị từ chối).
        SoLanGuiCQT++;
        LanGuiCuoiUtc = thoiDiemGuiUtc;
    }

    /// <summary>CQT đã nhận — lưu mã cấp để tra cứu/đối chiếu.</summary>
    public void NhanKetQuaCQT(string maCQT)
    {
        if (TaxAuthorityStatus != TaxAuthorityStatus.DaGui)
        {
            throw new BusinessException(EInvoiceErrorCodes.ChuaGuiKhongTheNhanKetQua)
                .WithData("trangThaiCQT", TaxAuthorityStatus.ToString());
        }

        if (string.IsNullOrWhiteSpace(maCQT))
        {
            throw new BusinessException(EInvoiceErrorCodes.ThieuThongTinBatBuoc).WithData("truong", "maCQT");
        }

        TaxAuthorityStatus = TaxAuthorityStatus.DaNhan;
        MaCQT = maCQT.Trim();
        LoiCQT = null;
    }

    /// <summary>CQT từ chối — lưu lỗi nhưng **KHÔNG** đổi trục 1 (hoá đơn vẫn đã phát hành cho khách).</summary>
    public void BiTuChoiCQT(string loiCQT)
    {
        if (TaxAuthorityStatus != TaxAuthorityStatus.DaGui)
        {
            throw new BusinessException(EInvoiceErrorCodes.ChuaGuiKhongTheNhanKetQua)
                .WithData("trangThaiCQT", TaxAuthorityStatus.ToString());
        }

        TaxAuthorityStatus = TaxAuthorityStatus.BiTuChoi;
        LoiCQT = loiCQT;
    }

    /// <summary>Cho phép gửi lại sau khi bị từ chối (đã sửa lỗi).</summary>
    public void ChoPhepGuiLaiCQT()
    {
        if (TaxAuthorityStatus != TaxAuthorityStatus.BiTuChoi)
        {
            throw new BusinessException(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("hanhDong", nameof(ChoPhepGuiLaiCQT))
                .WithData("trangThaiCQT", TaxAuthorityStatus.ToString());
        }

        TaxAuthorityStatus = TaxAuthorityStatus.ChuaGui;
    }

    public override string ToString()
        => $"{KyHieuMauSo}/{SoHoaDon} · {Status} · CQT={TaxAuthorityStatus}";
}
