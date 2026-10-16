using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **NHAT KY KIEM TOAN: MOI LAN XUAT DU LIEU CA NHAN** (`CUS-001`, Luat **91/2025/QH15**).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VI SAO CAN BANG NAY — VA VI SAO LOG KHONG DU:**
///
/// O §20 va §21, moi lan xuat PII deu ghi **mot dong log** (`Logger.LogWarning` voi neo
/// `[CUS-XUAT-DU-LIEU-CA-NHAN]`). ⚠️ Nhung cau hoi luat dat ra la:
///
///   • *"**AI** da xuat du lieu cua toi?"*   • *"**KHI NAO**?"*   • *"**BAO NHIEU LAN**?"*
///
/// 🔒 **Log KHONG tra loi duoc ba cau hoi do** vi:
/// <list type="number">
///   <item><b>Log bi xoay vong/het han</b> — 📌 sau 30 ngay (hoac sau khi xoay file) **khong con** gi de tra.
///     ⚠️ Va cau hoi *"nam ngoai ai da xuat du lieu cua toi"* la cau hoi **co that**;</item>
///   <item><b>Log khong TRA CUU duoc</b> — 🔑 muon tra *"khach X bi xuat may lan"* phai **grep** mot tep van ban,
///     📌 va grep phu thuoc **dinh dang dong log** (doi mot chu la hong);</item>
///   <item><b>Log nam ngoai giao dich</b> — ⚠️ neu ban xuat **that bai giua duong** thi dong log co the **khong**
///     duoc ghi, trong khi 🔒 mot **lan thu xuat** cung la su kien **can vet** (co the la **lam dung**).</item>
/// </list>
///
/// ✅ **Bang nay tra loi ca ba bang MOT cau truy van** — 📌 va do la khac biet giua **ghi lai** va **tra loi duoc**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔒 **VI SAO LA `CreationAuditedEntity` (KHONG phai `FullAudited`/`AggregateRoot`):**
///
/// <list type="bullet">
///   <item>**KHONG `ISoftDelete`** — 🚨 mot nhat ky kiem toan **khong duoc xoa** (ke ca xoa mem). 📌 Neu no
///     xoa duoc thi no **khong con la** nhat ky: 🔑 nguoi co quyen xoa se **xoa vet cua chinh minh**.
///     ⚠️ Va mot dong `IsDeleted = true` **van chiem** unique index — 📌 nhung bang nay **khong** co unique
///     index nao (xem duoi), nen van de do **khong** ap dung;</item>
///   <item>**KHONG `AggregateRoot`** — 📌 day la ban ghi **ghi-mot-lan, doc-nhieu**, khong co bat bien nghiep vu
///     nao can bao ve ⇒ 🔑 them lop aggregate chi lam **phuc tap** ma **khong** them an toan;</item>
///   <item>**KHONG co `ConcurrencyStamp`/`ExtraProperties`** — ⚠️ chung ton tai de ho tro **sua doi co kiem
///     soat**; 🔒 ma mot dong **khong bao gio sua** thi hai cot do la **trong rong**.</item>
/// </list>
///
/// ⚠️ **VA NO **KHONG** CO UNIQUE INDEX** — 🔑 co y: **nhieu** lan xuat cung mot khach trong cung mot giay la
/// **hop le** (va 📌 chinh su viec **lap lai** moi la thu can **phat hien**, nen 🔒 mot unique index se **che**
/// mat dau hieu do).
/// </summary>
public class CustomerDataExportLog : CreationAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>Do dai toi da cua nguoi xuat — 📌 du cho ten hien thi hoac ten dang nhap.</summary>
    public const int DoDaiNguoiXuatToiDa = 256;

    /// <summary>Do dai toi da cua ly do (neu co) — ⚠️ **khong** chua PII, chi la ghi chu van hanh.</summary>
    public const int DoDaiLyDoToiDa = 512;

    public Guid? TenantId { get; protected set; }

    /// <summary>Khach **bi xuat** du lieu. ⚠️ **KHONG** dat FK cung: 🔑 nhat ky phai **song sot** ke ca khi
    /// ho so khach bi xoa (📌 neu FK cung thi viec xoa khach se **keo theo** hoac **chan** viec xoa nhat ky).</summary>
    public Guid CustomerId { get; protected set; }

    /// <summary>🔑 **AI** da xuat — 📌 ten hien thi, hoac ten dang nhap, hoac mot chuoi **noi ro**.</summary>
    public string NguoiXuat { get; protected set; } = default!;

    /// <summary>🔑 **KHI NAO** — thoi diem xuat (UTC).</summary>
    public DateTime XuatLucUtc { get; protected set; }

    /// <summary>So dong ho so trong ban xuat — 🔑 de biet **quy mo** du lieu da ra.</summary>
    public int SoDongHoSo { get; protected set; }

    /// <summary>So don hang trong ban xuat.</summary>
    public int SoDonHang { get; protected set; }

    /// <summary>⚠️ Ban xuat co **bi cat bot** khong (nhieu don hon tran) — 📌 de phan biet *"da lay het"*
    /// voi *"da lay mot phan"*.</summary>
    public bool DonHangBiCat { get; protected set; }

    /// <summary>Ly do/ghi chu van hanh (⚠️ **KHONG** chua PII). 📌 Vi du *"theo yeu cau cua khach qua dien thoai"*.</summary>
    public string? LyDo { get; protected set; }

    protected CustomerDataExportLog() { }

    public CustomerDataExportLog(
        Guid id,
        Guid customerId,
        string nguoiXuat,
        DateTime xuatLucUtc,
        int soDongHoSo,
        int soDonHang,
        bool donHangBiCat,
        string? lyDo = null) : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new BusinessException(CustomerErrorCodes.KhongTimThayKhach)
                .WithData("lyDo", "Nhat ky xuat PII PHAI tro toi mot khach cu the — khong co 'xuat chung chung'.");
        }

        if (string.IsNullOrWhiteSpace(nguoiXuat))
        {
            // 🚨 **KHONG DUOC DE TRONG:** 🔑 mot nhat ky kiem toan ma khong biet **AI** lam thi **vo dung**
            // — 📌 va day chinh la cau hoi dau tien cua luat.
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(nguoiXuat))
                .WithData("lyDo", "Nhat ky xuat PII PHAI co NGUOI XUAT — mot vet rong la mot vet VO DUNG.");
        }

        if (soDongHoSo < 0 || soDonHang < 0)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("soDongHoSo", soDongHoSo)
                .WithData("soDonHang", soDonHang)
                .WithData("lyDo", "So dong/don KHONG duoc am — so am la dau hieu tinh toan sai.");
        }

        CustomerId = customerId;
        NguoiXuat = Cat(nguoiXuat, DoDaiNguoiXuatToiDa);
        XuatLucUtc = xuatLucUtc;
        SoDongHoSo = soDongHoSo;
        SoDonHang = soDonHang;
        DonHangBiCat = donHangBiCat;
        LyDo = string.IsNullOrWhiteSpace(lyDo) ? null : Cat(lyDo.Trim(), DoDaiLyDoToiDa);
    }

    /// <summary>
    /// 🔒 **Cat bot theo do dai — KHONG nem.** 📌 Ly do: day la **nhat ky kiem toan**; ⚠️ neu no **nem** vi mot
    /// ly do qua dai thi 🔑 **su kien xuat PII se khong duoc ghi** — tuc ta **mat vet** chi vi mot **ghi chu**.
    /// 📌 Doi lai: mot vet **bi cat** van **tot hon** khong co vet nao.
    /// </summary>
    private static string Cat(string giaTri, int toiDa)
        => giaTri.Length <= toiDa ? giaTri : giaTri[..toiDa];
}
