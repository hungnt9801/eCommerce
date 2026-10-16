using eCommerce.Customers;
using eCommerce.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;

namespace eCommerce.Tests.Customers;

/// <summary>
/// 🚨 **BẤT BIẾN CỦA NHẬT KÝ KIỂM TOÁN XUẤT DỮ LIỆU CÁ NHÂN** (`CUS-001` §22).
///
/// 🔑 **VÌ SAO CÓ NHÓM TEST NÀY — VÀ VÌ SAO NÓ KHÔNG CHỈ LÀ "TEST CHO VUI":**
///
/// Đây là **nhật ký kiểm toán** — 🔒 thứ mà **luật** dựa vào để trả lời *"ai đã xem dữ liệu của tôi"*.
/// ⚠️ Một nhật ký **có ghi** nhưng **thiếu người xuất**, hoặc **trỏ tới khách rỗng**, thì **trông như có vết**
/// — 📌 và đó là kiểu hỏng **tệ nhất**: nó khiến ta **tin** rằng mình đã có bằng chứng.
///
/// ➕ **Và có MỘT bất biến chỉ ở tầng ENTITY mới kiểm được:** 🔑 `CustomerDataExportLog` **KHÔNG** được là
/// `ISoftDelete` — 📌 vì một nhật ký **xoá được** thì **không còn là** nhật ký (người có quyền xoá sẽ
/// **xoá vết của chính mình**). ⚠️ Điều đó **không** hiện ra ở test DB và **không** hiện ra ở migration —
/// nó chỉ đúng/sai ở **khai báo kiểu**. 🔑 Nên phải khoá nó **bằng phản chiếu (reflection)**.
/// </summary>
public class CustomerDataExportLogTests
{
    private static CustomerDataExportLog Tao(
        Guid? id = null,
        Guid? customerId = null,
        string nguoiXuat = "admin",
        DateTime? luc = null,
        int soDongHoSo = 10,
        int soDonHang = 1,
        bool biCat = false,
        string? lyDo = null)
        => new(
            id ?? Guid.NewGuid(),
            customerId ?? Guid.NewGuid(),
            nguoiXuat,
            luc ?? new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc),
            soDongHoSo,
            soDonHang,
            biCat,
            lyDo);

    // ── ① NHAT KY PHAI TRO TOI MOT KHACH CU THE ────────────────────────────────────────────────

    [Fact]
    public void TC01_Khach_rong_thi_NEM__khong_co_nhat_ky_xuat_chung_chung()
    {
        // 🔑 *"Xuat du lieu cua ai?"* la cau hoi DAU TIEN. 📌 Mot dong `CustomerId = Guid.Empty`
        // **trong nhu** mot vet nhung **khong tra loi duoc** cau hoi nao.
        var nem = Assert.Throws<BusinessException>(() => Tao(customerId: Guid.Empty));

        Assert.Equal(CustomerErrorCodes.KhongTimThayKhach, nem.Code);
    }

    // ── ② PHAI BIET **AI** XUAT ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TC02_Nguoi_xuat_rong_thi_NEM__mot_vet_rong_la_mot_vet_VO_DUNG(string? nguoiXuat)
    {
        // 🚨 **CA QUAN TRONG NHAT.** 🔑 Neu nguoi xuat de trong thi ta co mot dong **trong nhu bang chung**
        // nhung **khong quy duoc trach nhiem cho ai** — 📌 va do chinh la cau hoi luat dat ra.
        // ⚠️ Chuoi KHOANG TRANG cung phai bi chan (`"   "` **khong phai** la ten nguoi).
        var nem = Assert.Throws<BusinessException>(() => Tao(nguoiXuat: nguoiXuat!));

        Assert.Equal(CustomerErrorCodes.ThieuThongTinBatBuoc, nem.Code);
    }

    // ── ③ SO LIEU KHONG DUOC AM ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void TC03_So_dong_hoac_so_don_AM_thi_NEM(int soDong, int soDon)
    {
        // 🔑 So am **khong the** la su that ⇒ 📌 no la **dau hieu tinh toan sai** o noi goi.
        // 🔒 Ghi mot con so sai vao nhat ky con te hon **khong ghi** — vi no lam nhat ky **noi doi**.
        var nem = Assert.Throws<BusinessException>(() => Tao(soDongHoSo: soDong, soDonHang: soDon));

        Assert.Equal(CustomerErrorCodes.ThieuThongTinBatBuoc, nem.Code);
    }

    [Fact]
    public void TC04_So_0_la_HOP_LE__khach_khong_co_don_nao_la_binh_thuong()
    {
        // ⚠️ Nguoc lai voi TC03: `0` **khong phai** loi — 📌 mot khach chua mua gi la **binh thuong**.
        // 🔑 Neu chan `0` thi ta se **khong ghi duoc nhat ky** cho nhung lan xuat **dau tien** —
        // 📌 dung nhung lan quan trong nhat (khach vua dang ky, yeu cau du lieu ngay).
        var nk = Tao(soDongHoSo: 0, soDonHang: 0);

        Assert.Equal(0, nk.SoDongHoSo);
        Assert.Equal(0, nk.SoDonHang);
    }

    // ── ④ CAT BOT THEO DO DAI — **KHONG NEM** ──────────────────────────────────────────────────

    [Fact]
    public void TC05_Nguoi_xuat_qua_dai_thi_CAT__KHONG_nem()
    {
        // 🔒 **QUYET DINH CO Y:** day la **nhat ky kiem toan**; ⚠️ neu no **nem** vi mot ten qua dai thi
        // 🔑 **su kien xuat PII se KHONG duoc ghi** — tuc ta **mat vet** chi vi mot **cai ten**.
        // 📌 Mot vet **bi cat** van **tot hon** khong co vet nao.
        var dai = new string('x', CustomerDataExportLog.DoDaiNguoiXuatToiDa + 50);

        var nk = Tao(nguoiXuat: dai);

        Assert.Equal(CustomerDataExportLog.DoDaiNguoiXuatToiDa, nk.NguoiXuat.Length);
        Assert.StartsWith("xxx", nk.NguoiXuat, StringComparison.Ordinal);
    }

    [Fact]
    public void TC06_Ly_do_qua_dai_thi_CAT__va_ly_do_TRANG_thi_thanh_null()
    {
        var dai = new string('y', CustomerDataExportLog.DoDaiLyDoToiDa + 50);

        Assert.Equal(CustomerDataExportLog.DoDaiLyDoToiDa, Tao(lyDo: dai).LyDo!.Length);

        // ⚠️ `""` va `"   "` phai thanh `null` — 🔑 de **phan biet** *"khong co ly do"* voi
        // *"co ly do nhung rong"*; 📌 va tranh ghi mot chuoi **vo nghia** vao nhat ky.
        Assert.Null(Tao(lyDo: "").LyDo);
        Assert.Null(Tao(lyDo: "   ").LyDo);
    }

    [Fact]
    public void TC07_Ly_do_binh_thuong_duoc_giu_NGUYEN__khong_tu_y_cat_trim()
    {
        // 🔑 Chong "cat qua tay": gia tri **hop le** phai di qua **nguyen ven** (chi `Trim` hai dau).
        Assert.Equal("theo yeu cau cua khach qua dien thoai", Tao(lyDo: "  theo yeu cau cua khach qua dien thoai  ").LyDo);
    }

    // ── ⑤ NGUOI XUAT LAY TU THAM SO, KHONG DOAN ────────────────────────────────────────────────

    [Fact]
    public void TC08_Moi_truong_duoc_gan_DUNG_tham_so__khong_co_truong_nao_bi_hoan_doi()
    {
        // 🚨 🔑 **CA CHONG LOI "THAM SO HOAN DOI"** — 📌 mot trong nhung loi kho phat hien nhat:
        // `soDongHoSo` va `soDonHang` **cung kieu `int`**, va 🔒 neu chung bi hoan doi thi nhat ky
        // **van hop le** (du lieu **sai** nhung **khong co trieu chung nao**).
        // 🔑 Cach chong duy nhat: **dung hai gia tri KHAC NHAU** roi kiem **tung truong**.
        var id = Guid.NewGuid();
        var khach = Guid.NewGuid();
        var luc = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        var nk = Tao(id: id, customerId: khach, nguoiXuat: "nhanvien.a", luc: luc, soDongHoSo: 17, soDonHang: 3, biCat: true, lyDo: "ly do X");

        Assert.Equal(id, nk.Id);
        Assert.Equal(khach, nk.CustomerId);
        Assert.Equal("nhanvien.a", nk.NguoiXuat);
        Assert.Equal(luc, nk.XuatLucUtc);
        Assert.Equal(17, nk.SoDongHoSo);
        Assert.Equal(3, nk.SoDonHang);
        Assert.True(nk.DonHangBiCat);
        Assert.Equal("ly do X", nk.LyDo);
    }

    // ── ⑥ 🔒 BAT BIEN CHI O TANG KIEU MOI KIEM DUOC ────────────────────────────────────────────

    [Fact]
    public void TC09_NHAT_KY_KHONG_DUOC_XOA_DUOC__khong_phai_ISoftDelete()
    {
        // 🚨🚨 **CA QUAN TRONG NHAT CUA NHOM NAY.**
        //
        // 🔒 Mot nhat ky kiem toan **XOA DUOC** thi **khong con la** nhat ky: 📌 nguoi co quyen xoa se
        // **xoa vet cua chinh minh** — va 🔑 do la **chinh xac** kieu che giau ma nhat ky sinh ra de chan.
        //
        // ⚠️ **VA DIEU NAY KHONG THE KIEM O DAU KHAC:** no **khong** hien ra o test DB (cot `IsDeleted`
        // se **khong** duoc tao — su **vang mat** kho khan nhan ra hon su **hien dien**), va no **khong**
        // hien ra o migration. 🔑 No chi dung/sai o **khai bao kieu** ⇒ ✅ phai khoa bang **phan chieu**.
        //
        // 📌 **CACH KIEM:** thay vi import namespace cua ABP (⚠️ ten namespace **khong on dinh** giua cac
        // phien ban — da **sai HAI LAN** khi viet test nay), 🔑 ta kiem **DAU HIEU THAT** cua xoa mem:
        // su **HIEN DIEN** cua cac thuoc tinh `IsDeleted` / `DeletionTime` / `DeleterId`.
        // ✅ Cach nay **manh hon**: no khong quan tam ABP goi interface la gi, 📌 no hoi *"entity co kha nang
        // bi xoa mem khong"* — **dung cau hoi ta can**.
        foreach (var ten in new[] { "IsDeleted", "DeletionTime", "DeleterId" })
        {
            var thuocTinh = typeof(CustomerDataExportLog).GetProperty(ten);

            Assert.True(thuocTinh is null,
                $"🚨 `CustomerDataExportLog` co thuoc tinh `{ten}` ⇒ no XOA MEM duoc ⇒ **khong con la** nhat ky " +
                "kiem toan: nguoi co quyen xoa se XOA VET CUA CHINH MINH.");
        }

        // ➕ Va khong duoc ke thua tu mot kieu FullAudited nao cua ABP (🔑 kiem theo **TEN kieu** de khong
        // phu thuoc namespace — ⚠️ namespace ABP da lam test nay sai hai lan).
        var tenKieuCoSo = typeof(CustomerDataExportLog).BaseType?.Name ?? string.Empty;

        Assert.DoesNotContain("FullAudited", tenKieuCoSo, StringComparison.Ordinal);
    }

    [Fact]
    public void TC10_NHAT_KY_LA_APPEND_ONLY__khong_co_setter_cong_khai_cho_truong_nghiep_vu()
    {
        // 🔑 Cung tinh than TC09, o muc **thuoc tinh**: moi truong nghiep vu phai **chi doc tu ngoai**
        // ⇒ 📌 khong ai **sua** duoc mot dong nhat ky sau khi ghi (⚠️ sua duoc = che giau duoc).
        foreach (var ten in new[]
                 {
                     nameof(CustomerDataExportLog.CustomerId),
                     nameof(CustomerDataExportLog.NguoiXuat),
                     nameof(CustomerDataExportLog.XuatLucUtc),
                     nameof(CustomerDataExportLog.SoDongHoSo),
                     nameof(CustomerDataExportLog.SoDonHang),
                     nameof(CustomerDataExportLog.DonHangBiCat),
                     nameof(CustomerDataExportLog.LyDo)
                 })
        {
            var thuocTinh = typeof(CustomerDataExportLog).GetProperty(ten)!;

            Assert.False(thuocTinh.CanWrite && thuocTinh.SetMethod!.IsPublic,
                $"🚨 `{ten}` co setter CONG KHAI ⇒ nhat ky kiem toan SUA DUOC ⇒ khong con la bang chung.");
        }
    }

    // ── ⑦ 🔑 DBSET — LOI DA GAP THAT KHI LAM LAT CAT NAY ────────────────────────────────────────

    [Fact]
    public void TC11_PHAI_co_DbSet_va_map_vao_bang_CusDataExportLogs()
    {
        // 🚨 **LOI DA GAP THAT (`CUS-001` §22.5):** toi khai `builder.Entity<CustomerDataExportLog>(...)`
        // trong `OnModelCreating` va **QUEN** `DbSet<>` tren `eCommerceDbContext`.
        // 🔑 ABP dang ky `IRepository<T>` bang cach **QUET `DbSet<>`** (khong quet model) ⇒
        // `IRepository<CustomerDataExportLog>` **khong duoc dang ky** ⇒ **DI no luc chay** ⇒
        // 📌 `/KhachHang` tra **`500`** — ⚠️ va trieu chung **khong he** tro tới nhat ky
        // (do la `GET` **danh sach khach**).
        //
        // ✅ Test nay khoa **ca hai** ve: co `DbSet` **va** map dung bang ⇒ 🔑 loi chi-lo-khi-chay
        // tro thanh loi **lo-ngay-trong-CI**.
        var options = new DbContextOptionsBuilder<eCommerceDbContext>()
            .UseNpgsql("Host=khong-ket-noi;Database=khong-ket-noi")
            .Options;

        using var db = new eCommerceDbContext(options);

        var coDbSet = typeof(eCommerceDbContext)
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                        && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)
                        && p.PropertyType.GetGenericArguments()[0] == typeof(CustomerDataExportLog))
            .ToList();

        Assert.True(coDbSet.Count == 1,
            "🚨 `CustomerDataExportLog` THIEU `DbSet<>` tren `eCommerceDbContext` ⇒ ABP KHONG dang ky " +
            "`IRepository<CustomerDataExportLog>` ⇒ DI no (HTTP 500) khi goi API.");

        var entity = db.Model.FindEntityType(typeof(CustomerDataExportLog));
        Assert.NotNull(entity);
        Assert.Equal("CusDataExportLogs", entity!.GetTableName());
    }

    [Fact]
    public void TC12_Bang_nhat_ky_KHONG_co_unique_index__vi_su_LAP_LAI_moi_la_thu_can_PHAT_HIEN()
    {
        // 🔑 **QUYET DINH CO Y:** nhieu lan xuat cung mot khach trong cung mot giay la **hop le**, va
        // 📌 chinh su **lap lai** moi la thu can **phat hien**. ⚠️ Mot unique index se **chan** lan thu hai
        // ⇒ 🔒 ta **mat** dung dau hieu dang ngo nhat (co nguoi thu di thu lai de lay du lieu).
        // ➕ Va 🔑 he qua: bang nay **KHONG** lam tang so index can `NULLS NOT DISTINCT` ⇒ **NND van = 48**.
        var options = new DbContextOptionsBuilder<eCommerceDbContext>()
            .UseNpgsql("Host=khong-ket-noi;Database=khong-ket-noi")
            .Options;

        using var db = new eCommerceDbContext(options);

        var entity = db.Model.FindEntityType(typeof(CustomerDataExportLog))!;

        var unique = entity.GetIndexes().Where(i => i.IsUnique).ToList();

        Assert.True(unique.Count == 0,
            "🚨 Bang nhat ky xuat PII KHONG duoc co unique index — nhieu lan xuat la HOP LE, va chinh su " +
            "LAP LAI moi la thu can PHAT HIEN. Index unique: " +
            string.Join(", ", unique.Select(i => i.GetDatabaseName())));
    }
}
