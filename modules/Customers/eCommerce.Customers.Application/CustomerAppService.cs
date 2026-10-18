using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using eCommerce.Orders;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **APP SERVICE KHÁCH HÀNG** (`CUS-001`) — và **cũng là** hiện thực của <see cref="ICustomerDirectory"/>
/// (hợp đồng đọc cho module khác — `ADR-12`).
///
/// 📌 **VÌ SAO MỘT LỚP LÀM CẢ HAI VIỆC:** `ICustomerDirectory` là **cùng** dữ liệu, **cùng** bất biến
/// (che PII, khách bị chặn thì không giao dịch được). Tách thành hai lớp ⇒ **hai đường đọc** cho cùng một
/// đại lượng ⇒ mầm của *"hai nguồn sự thật"* (`XC-003` **F1/F2**). 🔒 Một lớp thì bất biến **chỉ có một chỗ**.
///
/// 🔐 **`[Authorize]` ở CẤP CLASS** (quy ước bắt buộc #4): thiếu nó ở class thì các method không gắn gì
/// trở thành **API công khai** — đã xảy ra thật ở Phase 0 với `GetShopsAsync`.
/// 🛡️ `BatBienPhanQuyenTests.TC-01` khoá bất biến này trong CI.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Customers.Default)]
public class CustomerAppService : ApplicationService
{
    private readonly IRepository<Customer, Guid> _khachRepository;
    private readonly IAbpDistributedLock _khoa;

    /// <summary>`CUS-001` §20.7 #3 — **ban xuat du lieu ca nhan PHAI gom don hang** (📌 do la **phan LON**
    /// du lieu ca nhan that). 🔒 Nhung doc **THANG** repository (khong qua app service cua app chinh):
    /// ⚠️ goi `IOrderAppService` se tao **vong** vi `eCommerce.Application` **da** tham chieu module nay.</summary>
    private readonly IRepository<Orders.Order, Guid> _donHangRepository;
    private readonly IRepository<Orders.OrderLine, Guid> _dongHangRepository;

    /// <summary>🔑 **NHAT KY KIEM TOAN** (`CUS-001` §22) — tra loi *"AI / KHI NAO / BAO NHIEU LAN"*.</summary>
    private readonly IRepository<CustomerDataExportLog, Guid> _nhatKyXuatRepository;

    /// <summary>🔒 De mo **UoW RIENG** cho nhat ky — xem `XuatDuLieuCaNhanAsync`.</summary>
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 🔑 `ADDR-004` **B10** — **sổ địa chỉ là dữ liệu CÁ NHÂN** (`Luật 91/2025/QH15`): *"tôi đang được giao
    /// tới đâu"* là câu hỏi **chính** của chủ thể dữ liệu. 📌 Bản xuất thiếu nó là **thiếu hụt pháp lý**,
    /// không phải thiếu tính năng.
    /// </summary>
    private readonly IRepository<CustomerAddressLink, Guid> _lienKetDiaChiRepository;

    /// <summary>Hợp đồng đọc snapshot địa chỉ (chỉ **Contracts** — không phụ thuộc tầng Application của `Address`).</summary>
    private readonly eCommerce.Address.IAddressDirectory _diaChiDirectory;

    public CustomerAppService(
        IRepository<Customer, Guid> khachRepository,
        IAbpDistributedLock khoa,
        IRepository<Orders.Order, Guid> donHangRepository,
        IRepository<Orders.OrderLine, Guid> dongHangRepository,
        IRepository<CustomerDataExportLog, Guid> nhatKyXuatRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IRepository<CustomerAddressLink, Guid> lienKetDiaChiRepository,
        eCommerce.Address.IAddressDirectory diaChiDirectory)
    {
        _khachRepository = khachRepository;
        _khoa = khoa;
        _donHangRepository = donHangRepository;
        _dongHangRepository = dongHangRepository;
        _nhatKyXuatRepository = nhatKyXuatRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _lienKetDiaChiRepository = lienKetDiaChiRepository;
        _diaChiDirectory = diaChiDirectory;
    }

    /// <summary>
    /// 🚨 **TRAN SO DON MOT LAN XUAT** — 🔑 co tran vi **hai** ly do:
    /// <list type="number">
    ///   <item>**Hieu nang:** mot khach mua hang nam co the co **hang nghin** don ⇒ 📌 keo het vao mot ban xuat
    ///     se **rat cham** va co the **het bo nho**;</item>
    ///   <item>**It nguoi doc:** 🔒 mot tep JSON 5000 don thi **khong ai doc** — 📌 va mot ban xuat **khong ai doc**
    ///     thi **khong phuc vu** quyen truy cap.</item>
    /// </list>
    /// ⚠️ **VA KHI CHAM TRAN THI PHAI NOI RO** (`DonHangBiCat`) — 🔑 mot ban xuat **im lang cat bot** se khien
    /// nguoi doc tin rang *"khach chi co bay nhieu don"*, 📌 va do la mot **loi khang dinh sai ve du lieu ca nhan**.
    /// </summary>
    public const int TranDonHangMotLanXuat = 200;

    // ── ĐỌC ─────────────────────────────────────────────────────────────────────────────────────
    // 🚨 Ba method ĐỌC của hợp đồng `ICustomerDirectory` **KHÔNG** còn ở đây — chúng đã chuyển sang lớp
    // `CustomerDirectory` (xem chú thích ở đó: để ở đây thì swagger sinh `POST` cho method chỉ-đọc).

    /// <summary>Danh sách khách (đọc cho UI vận hành). ⚠️ Có **trần** — chưa phân trang (nợ đã ghi).</summary>
    public async Task<List<CustomerDto>> GetDanhSachAsync(string? keyword = null, int toiDa = 100)
    {
        var soLuong = Math.Clamp(toiDa, 1, 500);

        var truyVan = await _khachRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var tu = keyword.Trim();
            // 🔒 Tìm theo **tên** và **liên hệ** — nhưng KHÔNG log từ khoá (nó **chính là** PII nếu là email/SĐT).
            truyVan = truyVan.Where(x => x.Name.Contains(tu) || (x.Email != null && x.Email.Contains(tu))
                || (x.Phone != null && x.Phone.Contains(tu)));
        }

        var ds = await AsyncExecuter.ToListAsync(truyVan.OrderByDescending(x => x.CreationTime).Take(soLuong));

        return ds.Select(ToDto).ToList();
    }

    // ── GHI ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **TẠO KHÁCH (IDEMPOTENT)** — `CUS-001` **ADR-05**.
    ///
    /// 🔑 **HAI LỚP CHỐNG TRÙNG (cả hai đều cần):**
    /// <list type="number">
    ///   <item><b>Khoá phân tán theo LIÊN HỆ + theo `IdempotencyKey`</b> — tuần tự hoá *"kiểm rồi ghi"* cho
    ///     ca **thường** (hai request cùng lúc) ⇒ người dùng nhận **khách đã có**, không phải lỗi ràng buộc.</item>
    ///   <item><b>Unique index ở DB</b> — hàng rào **cuối** cho ca **đồng thời thật** (khoá hết hạn, Redis chết).
    ///     `XC-003` **D2**: *"mọi bất biến phải có ràng buộc DB"*.</item>
    /// </list>
    ///
    /// 🔒 **Nếu `IdempotencyKey` đã tồn tại ⇒ trả khách CŨ** (không tạo mới, **không** ném): retry của webhook
    /// **phải** nhận cùng kết quả — ném `23505` ở đây là **bắt consumer retry vô hạn**.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<CustomerDto> TaoKhachAsync(TaoKhachInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // ① Khoá theo idempotency TRƯỚC (rẻ nhất, và là ca retry phổ biến nhất).
        if (!string.IsNullOrWhiteSpace(input.IdempotencyKey))
        {
            var khoaIdem = input.IdempotencyKey.Trim();

            await using var giuChoIdem = await _khoa.TryAcquireAsync(
                $"customers:idem:{khoaIdem}", TimeSpan.FromSeconds(CustomersConsts.ChoKhoaToiDaGiay))
                ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                    .WithData("khoa", "customers:idem").WithData("choGiay", CustomersConsts.ChoKhoaToiDaGiay);

            var daCo = await _khachRepository.FirstOrDefaultAsync(x => x.IdempotencyKey == khoaIdem);

            if (daCo is not null)
            {
                Logger.LogInformation("TẠO KHÁCH: khoá idempotency đã tồn tại ⇒ trả khách CŨ. [CUS-IDEM-TRUNG]");
                return ToDto(daCo);
            }

            return await TaoMoiAsync(input, khoaIdem);
        }

        return await TaoMoiAsync(input, null);
    }

    private async Task<CustomerDto> TaoMoiAsync(TaoKhachInput input, string? khoaIdem)
    {
        // ② Khoá theo LIÊN HỆ — chống hai hồ sơ cho cùng một người (email/SĐT trùng).
        await using var giuChoLienHe = await _khoa.TryAcquireAsync(
            CustomersConsts.LockKeys.TheoLienHe(input.Email, input.Phone),
            TimeSpan.FromSeconds(CustomersConsts.ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "customers:contact").WithData("choGiay", CustomersConsts.ChoKhoaToiDaGiay);

        var khach = new Customer(GuidGenerator.Create(), input.Name, input.Email, input.Phone, khoaIdem);

        // 📌 Kiểm trùng liên hệ để trả **mã nghiệp vụ** (`0003`) thay vì để unique index nổ `23505`:
        // client cần biết *"tra khách cũ"* chứ không phải *"hệ thống hỏng"* (đúng lý do Phụ lục B tồn tại).
        if (khach.Email is not null || khach.Phone is not null)
        {
            var trung = await _khachRepository.FirstOrDefaultAsync(x =>
                (khach.Email != null && x.Email == khach.Email) ||
                (khach.Phone != null && x.Phone == khach.Phone));

            if (trung is not null)
            {
                Logger.LogWarning(
                    "TẠO KHÁCH: liên hệ đã thuộc khách khác (id={KhachCu}) ⇒ KHÔNG tạo mới. [CUS-LIENHE-TRUNG]",
                    trung.Id);

                throw new BusinessException(CustomerErrorCodes.LienHeDaTonTai)
                    .WithData("khachDaCo", trung.Id)
                    .WithData("lyDo", "Email/SĐT này đã thuộc một khách khác ⇒ tra khách cũ thay vì tạo mới.");
            }
        }

        await _khachRepository.InsertAsync(khach, autoSave: true);

        // 🔒 Log **KHÔNG** chứa giá trị PII thô — chỉ bản ĐÃ CHE (`ADR-11`).
        Logger.LogInformation(
            "ĐÃ TẠO KHÁCH: id={KhachId} · liên hệ={LienHe} · trạng thái={TrangThai}. [CUS-DA-TAO]",
            khach.Id, MoTaAnToan(khach), khach.Status);

        return ToDto(khach);
    }

    /// <summary>
    /// **NÂNG CẤP KHÁCH VÃNG LAI** thành khách có tài khoản (`ADR-07`) — idempotent, có khoá phân tán.
    ///
    /// 🔒 **HAI KHOÁ (khách + tài khoản):** khoá theo **khách** ngăn hai yêu cầu cùng nâng cấp **một** khách;
    /// khoá theo **tài khoản** ngăn **hai hồ sơ khác nhau** cùng nhận **một** tài khoản (ca này unique index
    /// `(TenantId, IdentityUserId)` sẽ chặn, nhưng để DB nổ là **trả lỗi khó hiểu** cho người dùng).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<CustomerDto> NangCapKhachVangLaiAsync(Guid customerId, NangCapKhachInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var giuChoTaiKhoan = await _khoa.TryAcquireAsync(
            CustomersConsts.LockKeys.TheoTaiKhoan(input.IdentityUserId),
            TimeSpan.FromSeconds(CustomersConsts.ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "customers:identity").WithData("choGiay", CustomersConsts.ChoKhoaToiDaGiay);

        await using var giuChoKhach = await _khoa.TryAcquireAsync(
            CustomersConsts.LockKeys.TheoKhach(customerId),
            TimeSpan.FromSeconds(CustomersConsts.ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "customers:customer").WithData("choGiay", CustomersConsts.ChoKhoaToiDaGiay);

        var khach = await _khachRepository.FindAsync(customerId)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayKhach).WithData("customerId", customerId);

        // 🔒 Kiểm "tài khoản này đã thuộc khách KHÁC chưa" **trước** — nếu không, unique index nổ và
        // thông điệp không nói được **khách nào** đang giữ tài khoản (người vận hành phải tự đi tra).
        var chuCu = await _khachRepository.FirstOrDefaultAsync(x => x.IdentityUserId == input.IdentityUserId);

        if (chuCu is not null && chuCu.Id != customerId)
        {
            throw new BusinessException(CustomerErrorCodes.KhachDaCoChu)
                .WithData("taiKhoanDaThuocKhach", chuCu.Id)
                .WithData("customerId", customerId)
                .WithData("lyDo", "Tài khoản này ĐÃ gắn với một hồ sơ khách khác ⇒ không gán cho hai người.");
        }

        khach.NangCapVangLai(input.IdentityUserId);

        await _khachRepository.UpdateAsync(khach, autoSave: true);

        Logger.LogInformation("ĐÃ NÂNG CẤP KHÁCH VÃNG LAI: id={KhachId} ⇒ có tài khoản. [CUS-DA-NANG-CAP]", khach.Id);

        return ToDto(khach);
    }

    /// <summary>🚫 **CHẶN khách** — cần **lý do** (`ADR-04`).</summary>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<CustomerDto> ChanKhachAsync(Guid customerId, string lyDo)
        => await DoiTrangThaiAsync(customerId, khach => khach.Chan(lyDo), "CHẶN");

    /// <summary>🔓 **GỠ CHẶN** — **NÉM** nếu khách không đang bị chặn (xem `Customer.MoChan`).</summary>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<CustomerDto> MoChanKhachAsync(Guid customerId, string lyDo)
        => await DoiTrangThaiAsync(customerId, khach => khach.MoChan(lyDo), "GỠ CHẶN");

    private async Task<CustomerDto> DoiTrangThaiAsync(Guid customerId, Action<Customer> doi, string tenThaoTac)
    {
        await using var giuCho = await _khoa.TryAcquireAsync(
            CustomersConsts.LockKeys.TheoKhach(customerId),
            TimeSpan.FromSeconds(CustomersConsts.ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "customers:customer").WithData("choGiay", CustomersConsts.ChoKhoaToiDaGiay);

        var khach = await _khachRepository.FindAsync(customerId)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayKhach).WithData("customerId", customerId);

        // 🚨 Domain NÉM nếu chuyển sai (`ADR-04`) — app service **không** bọc `try/catch` để "cho qua":
        // bọc lại là biến guard **ném lỗi** thành guard **im lặng** (quy ước #11 — đúng lỗi đã trả giá).
        doi(khach);

        await _khachRepository.UpdateAsync(khach, autoSave: true);

        Logger.LogWarning("ĐÃ {ThaoTac} KHÁCH: id={KhachId} · trạng thái mới={TrangThai}. [CUS-DOI-TRANG-THAI]",
            tenThaoTac, khach.Id, khach.Status);

        return ToDto(khach);
    }

    // ── XUAT DU LIEU CA NHAN (quyen truy cap cua chinh chu) ─────────────────────────────────────

    /// <summary>
    /// 🚨 **XUAT TOAN BO DU LIEU CA NHAN CUA MOT KHACH** — quyen **TRUY CAP** cua chu the
    /// (Luat **91/2025/QH15**). Xem `XuatDuLieuCaNhanDto` de biet vi sao **khong** chi tra `CustomerDto`.
    ///
    /// **HAI QUYET DINH THIET KE, VA CA HAI DEU CO LY DO:**
    ///
    /// **① QUYEN = `Customers.Manage`, KHONG phai `Customers.Default`.**
    /// 🔑 `Default` la quyen **XEM HO SO** (danh sach, chi tiet). Con day la **XUAT TOAN BO PII CUA MOT NGUOI
    /// RA MOT TEP** — 📌 do la **muc do tiep can khac han**: mot nguoi co the duoc phep *"xem khach trong man
    /// hinh"* ma **khong** nen duoc phep *"tai ca ho so ve may minh"*. 🔒 Tach quyen la **nguyen tac toi thieu
    /// dac quyen** — va no **cung** la dieu luat doi hoi (*"chi nguoi co trach nhiem"*).
    /// ⚠️ Neu dung `Default` thi moi nhan vien doc duoc danh sach khach cung **xuat duoc** — 📌 va **khong ai
    /// biet**, vi ca hai deu la *"xem khach"*.
    ///
    /// **② MOI DONG CO `Nguon` VA `LyDo`.** 🔑 Mot ban xuat **khong co nguon** thi **khong kiem chung duoc**,
    /// va nguoi van hanh **khong tra loi duoc** cau hoi *"sao cac anh co cai nay?"* — 📌 ma do chinh la cau hoi
    /// luat bat phai tra loi (muc dich xu ly).
    ///
    /// 🔒 **VA KHONG GHI PII VAO LOG:** `MoTaAnToan` chi tra **email/sdt da che** (`ADR-11`). 📌 Ghi PII tho
    /// vao log nghia la **ban sao thu hai** cua du lieu ca nhan nam o mot noi **khong ai kiem soat** — dung cai
    /// ma ban xuat nay sinh ra de tranh.
    /// </summary>
    /// <param name="customerId">Khach can xuat.</param>
    /// <param name="lyDo">
    /// 🚨 **LY DO XUAT — BAT BUOC** (`CUS-001` §26). 🔑 Tra loi cau hoi luat: *"**vi sao** du lieu cua toi bi xuat"*.
    ///
    /// **⚠️ VI SAO BAT BUOC (khong phai "nen co"):** 📌 ly do la **mot phan cua ban ghi kiem toan**, khong phai
    /// ghi chu tien loi. 🔒 Mot lan xuat **khong co ly do** thi khi khach hoi *"sao cac anh xuat du lieu cua
    /// toi?"*, he thong **khong co cau tra loi** — ⚠️ va do **chinh xac** la cau hoi ma Luat **91/2025/QH15**
    /// cho khach quyen hoi.
    /// ➕ Va 🔑 **bat buoc ngay tu dau** (thay vi de sau) la **quan trong**: 📌 khi da co du lieu that thi
    /// **khong the** them rang buoc ma khong **pha vo** lich su (⚠️ hoac phai **bo qua** no — 🔒 va mot rang buoc
    /// bi bo qua thi **khong con la** rang buoc).
    ///
    /// 🔒 **KIEM O DAY (app service), KHONG CHI O UI:** ⚠️ rang buoc o UI la **tien nghi**, khong phai **guard**
    /// — 📌 API, script, va job deu goi duoc ham nay. ✅ Day moi la lop bao ve **that**.
    ///
    /// 📌 **Do dai** da bi chan o **entity** (`CustomerDataExportLog.DoDaiLyDoToiDa = 512`) ⇒ 🔑 tang nay chi
    /// can kiem **khong rong**.
    /// </param>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<XuatDuLieuCaNhanDto> XuatDuLieuCaNhanAsync(Guid customerId, string? lyDo = null)
    {
        // 🚨 **GUARD THAT** — xem `<param name="lyDo">`. ⚠️ `IsNullOrWhiteSpace` (khong chi `IsNullOrEmpty`):
        // 🔑 chuoi **KHOANG TRANG** khong phai la ly do (📌 cung bai hoc da ap dung o `CustomerDataExportLog`).
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(lyDo))
                .WithData("lyDo", "Xuat du lieu ca nhan PHAI co LY DO — Luat 91/2025/QH15 cho khach quyen biet " +
                    "VI SAO du lieu cua ho bi xuat, va mot ban ghi kiem toan khong co ly do thi KHONG tra loi duoc.");
        }

        var lyDoSach = lyDo.Trim();

        var khach = await _khachRepository.FindAsync(customerId)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayKhach)
                .WithData("customerId", customerId)
                .WithData("lyDo", "Khach KHONG ton tai ⇒ khong co du lieu ca nhan nao de xuat.");

        // 🔑 **NGUOI XUAT:** uu tien TEN HIEN THI, roi TEN DANG NHAP, cuoi cung la mot chuoi **noi ro**
        // (⚠️ khong de trong: mot vet kiem toan rong la mot vet **vo dung**).
        var nguoiXuat = CurrentUser.Name ?? CurrentUser.UserName ?? "(khong xac dinh duoc nguoi xuat)";

        var dto = new XuatDuLieuCaNhanDto
        {
            CustomerId = khach.Id,
            TenKhach = khach.Name,
            XuatLucUtc = DateTime.UtcNow,
            NguoiXuat = nguoiXuat,

            // ⛔ **CANH BAO BAT BUOC** — 🔒 tep nay la **tai lieu mat**.
            CanhBao = "BAN XUAT CHUA DU LIEU CA NHAN (Luat 91/2025/QH15). " +
                      "Phai duoc xu ly nhu TAI LIEU MAT: khong gui qua kenh khong ma hoa, " +
                      "khong luu o noi khong kiem soat, va phai xoa khi da giao cho chu the.",

            CacDong =
            [
                // 🔑 **MOI DONG CO NGUON + LY DO.** 📌 Thu tu: dinh danh → lien he → nghiep vu.
                new()
                {
                    Truong = "Ma khach",
                    GiaTri = khach.Id.ToString(),
                    Nguon = "CusCustomers.Id",
                    LyDo = "Dinh danh khach trong he thong (moi ho so, don hang, giao dich deu tro ve ma nay).",
                    LaDuLieuCaNhan = false
                },
                new()
                {
                    Truong = "Ho ten",
                    GiaTri = khach.Name,
                    Nguon = "CusCustomers.Name",
                    LyDo = "Goi ten khach khi lien he va tren hoa don/chung tu.",
                    LaDuLieuCaNhan = true
                },
                new()
                {
                    Truong = "Email",
                    GiaTri = khach.Email,
                    Nguon = "CusCustomers.Email",
                    LyDo = "Gui xac nhan don hang, hoa don dien tu, va ho tro khach.",
                    LaDuLieuCaNhan = true
                },
                new()
                {
                    Truong = "So dien thoai",
                    GiaTri = khach.Phone,
                    Nguon = "CusCustomers.Phone",
                    LyDo = "Lien he giao hang va ho tro khach.",
                    LaDuLieuCaNhan = true
                },
                new()
                {
                    Truong = "Tai khoan dang nhap",
                    GiaTri = khach.IdentityUserId?.ToString(),
                    Nguon = "CusCustomers.IdentityUserId",
                    LyDo = "Lien ket ho so khach voi tai khoan dang nhap (neu co) de khach tu xem don cua minh.",
                    LaDuLieuCaNhan = false
                },
                new()
                {
                    Truong = "Trang thai ho so",
                    GiaTri = khach.Status.ToString(),
                    Nguon = "CusCustomers.Status",
                    LyDo = "Cho biet ho so dang hoat dong hay bi chan (⚠️ bi chan thi khong giao dich duoc).",
                    LaDuLieuCaNhan = false
                },
                new()
                {
                    Truong = "Nhom khach",
                    GiaTri = khach.CustomerGroupId?.ToString(),
                    Nguon = "CusCustomers.CustomerGroupId",
                    LyDo = "Phan nhom khach de ap chinh sach gia/khuyen mai.",
                    LaDuLieuCaNhan = false
                },
                new()
                {
                    Truong = "Gia tri vong doi",
                    GiaTri = khach.LifetimeValueAmount is null
                        ? null
                        : $"{khach.LifetimeValueAmount} {khach.LifetimeValueCurrency}",
                    Nguon = "CusCustomers.LifetimeValueAmount + LifetimeValueCurrency",
                    LyDo = "Tong gia tri mua hang tich luy — dung de xep hang khach, khong dung de ra quyet dinh tu dong.",
                    LaDuLieuCaNhan = false
                },
                new()
                {
                    Truong = "Ghi chu noi bo",
                    GiaTri = khach.Note,
                    // ⚠️ **GHI RO LA GHI CHU NOI BO** — 🔑 neu khong noi ro thi khach doc mot cau do nhan vien
                    // viet cho nhan vien va **hieu sai**; va 📌 do cung la loai du lieu de bi loi dung nhat.
                    Nguon = "CusCustomers.Note",
                    LyDo = "GHI CHU NOI BO cua nhan vien (vi du: yeu cau giao hang dac biet). " +
                           "⚠️ Day KHONG phai thong tin khach tu cung cap.",
                    LaDuLieuCaNhan = true
                },
                new()
                {
                    Truong = "Ngay tao ho so",
                    GiaTri = khach.CreationTime.ToString("o"),
                    Nguon = "CusCustomers.CreationTime",
                    LyDo = "Cho biet he thong bat dau luu du lieu cua khach tu khi nao.",
                    LaDuLieuCaNhan = false
                }
            ]
        };

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **DON HANG (§20.7 #3) — PHAN LON DU LIEU CA NHAN THAT.**
        // 📌 Mot ban xuat chi co *"ten, email, sdt"* la **thieu hut nghiem trong**: 🔑 chu the du lieu muon
        // biet *"toi da mua gi, khi nao, bao nhieu tien"* — 📌 va do moi la **ly do** ho hoi.
        // ══════════════════════════════════════════════════════════════════════════════════════════

        var truyVanDon = await _donHangRepository.GetQueryableAsync();

        // ⚠️ **SAP XEP TAT DINH** (`CreationTime` roi `Id`): 🔑 neu chi sap xep theo thoi gian thi nhieu don
        // cung moc se cho **thu tu khac nhau moi lan** ⇒ 📌 ban xuat **khong tai lap duoc** ⇒ kho so sanh.
        var tatCaDon = truyVanDon
            .Where(x => x.CustomerId == khach.Id)
            .OrderBy(x => x.CreationTime)
            .ThenBy(x => x.Id)
            .Select(x => new DonHangTomTat
            {
                Id = x.Id,
                Number = x.Number,
                ChannelCode = x.ChannelCode,
                Currency = x.Currency,
                GrandTotal = x.GrandTotal,
                Status = x.Status,
                PaymentStatus = x.PaymentStatus,
                CreationTime = x.CreationTime
            });

        // 📌 Dem **TONG** truoc khi cat: 🔑 de noi duoc *"co bao nhieu, hien bao nhieu"*.
        var tongSoDon = await AsyncExecuter.CountAsync(tatCaDon);

        var donHienThi = await AsyncExecuter.ToListAsync(tatCaDon.Take(TranDonHangMotLanXuat));

        dto.TongSoDon = tongSoDon;
        dto.DonHangBiCat = tongSoDon > TranDonHangMotLanXuat;

        if (donHienThi.Count > 0)
        {
            // 📌 Nap **MOT LAN** moi dong hang cua cac don nay (tranh N+1: 🔑 mot lenh SQL cho moi don se
            // thanh **200 lenh** — 📌 cham va de gay tai).
            var cacDonId = donHienThi.Select(x => x.Id).ToList();
            var truyVanDong = await _dongHangRepository.GetQueryableAsync();

            var tatCaDong = await AsyncExecuter.ToListAsync(
                truyVanDong.Where(x => cacDonId.Contains(x.OrderId))
                    .OrderBy(x => x.CreationTime)
                    .ThenBy(x => x.Id));

            var dongTheoDon = tatCaDong
                .GroupBy(x => x.OrderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var d in donHienThi)
            {
                var dongCuaDon = dongTheoDon.TryGetValue(d.Id, out var ds) ? ds : [];

                dto.DonHang.Add(new DonHangCaNhanDto
                {
                    OrderId = d.Id,
                    SoDon = d.Number,
                    MaKenh = d.ChannelCode,
                    TienTe = d.Currency,
                    TongTien = d.GrandTotal,
                    TrangThai = d.Status.ToString(),
                    TrangThaiThanhToan = d.PaymentStatus.ToString(),
                    NgayTaoUtc = d.CreationTime,
                    CacDong = dongCuaDon.Select(l => new DongHangCaNhanDto
                    {
                        MaSku = l.Sku,
                        SoLuong = l.Quantity,
                        DonGia = l.UnitPrice,
                        Nguon = "OrdOrderLines.Sku + Quantity + UnitPrice (theo OrderId)"
                    }).ToList()
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 `ADDR-004` **B10** — **SỔ ĐỊA CHỈ** trong bản xuất dữ liệu cá nhân.
        //
        // 🔑 VÌ SAO PHẢI GOM CẢ MỤC **ĐÃ GỠ**: *"dữ liệu của tôi"* **không** chỉ là dữ liệu **đang dùng**.
        // 📌 Ẩn mục đã gỡ đi = nói dối chủ thể dữ liệu về việc hệ thống **vẫn còn** lưu địa chỉ cũ của họ
        // (Luật 91/2025/QH15 cho họ quyền **xem** dữ liệu được lưu). Vì vậy dùng `IgnoreQueryFilters()` và
        // đánh cờ `DaGo` cho từng dòng.
        // ══════════════════════════════════════════════════════════════════════════════════════════

        var truyVanLienKet = await _lienKetDiaChiRepository.GetQueryableAsync();

        var tatCaLienKet = await AsyncExecuter.ToListAsync(
            truyVanLienKet
                .IgnoreQueryFilters()
                .Where(x => x.CustomerId == khach.Id)
                // ⚠️ Sắp xếp TẤT ĐỊNH (như khối đơn hàng): bản xuất phải **tái lập được**.
                .OrderBy(x => x.CreationTime)
                .ThenBy(x => x.Id));

        if (tatCaLienKet.Count > 0)
        {
            var snapshotDiaChi = await _diaChiDirectory.TimNhieuTheoIdAsync(
                tatCaLienKet.Select(x => x.AddressId).ToHashSet());

            var theoId = snapshotDiaChi.ToDictionary(x => x.AddressId);

            foreach (var lk in tatCaLienKet)
            {
                var snap = theoId.GetValueOrDefault(lk.AddressId);

                dto.SoDiaChi.Add(new DiaChiCaNhanDto
                {
                    LinkId = lk.Id,
                    AddressId = lk.AddressId,
                    AddressType = lk.AddressType.ToString(),
                    Label = lk.Label,
                    IsDefault = lk.IsDefault,
                    DaGo = lk.IsDeleted,
                    LastUsedAtUtc = lk.LastUsedAtUtc,
                    DiaChiDayDu = snap?.DiaChiDayDu,
                    TrangThai = snap?.TrangThai,
                    CoDungDuoc = snap?.CoDungDuoc ?? false,
                    Nguon = "CusAddressLinks + AddrAddresses (DiaChiDayDu là SNAPSHOT chữ lúc tạo — ADR-10)"
                });
            }
        }

        // 📌 Dòng tóm tắt — 🔑 cùng lý do với dòng *"số đơn hàng"*: người đọc thấy **quy mô** mà không phải
        // đếm từng mục, và thấy rõ **có bao nhiêu mục đã gỡ** (thông tin họ có quyền biết).
        dto.CacDong.Add(new DongDuLieuCaNhanDto
        {
            Truong = "So dia chi trong so",
            GiaTri = $"{dto.SoDiaChi.Count(x => !x.DaGo)} dang dung · {dto.SoDiaChi.Count(x => x.DaGo)} da go",
            Nguon = "CusAddressLinks.CustomerId",
            LyDo = "Cho biet khach da luu bao nhieu dia chi, va bao nhieu dia chi da go (van con luu de doi soat).",
            LaDuLieuCaNhan = false
        });

        // 📌 **THEM DONG TOM TAT vao `CacDong`** — 🔑 de nguoi doc ban xuat thay **ngay** quy mo mua hang,
        // ngay ca khi ho **khong** doc het danh sach don.
        dto.CacDong.Add(new DongDuLieuCaNhanDto
        {
            Truong = "So don hang",
            GiaTri = dto.DonHangBiCat
                ? $"{tongSoDon} (⚠️ ban xuat CHI liet ke {TranDonHangMotLanXuat} don CU NHAT — xem `donHangBiCat`)"
                : tongSoDon.ToString(),
            Nguon = "OrdOrders.CustomerId",
            LyDo = "Cho biet khach da mua bao nhieu don — 🔑 va neu bi cat thi NOI RO la da cat.",
            LaDuLieuCaNhan = false
        });

        // 🔒 **LOG CHI QUA `MoTaAnToan`** (PII da che) — xem quyet dinh (2) o tren.
        // 📌 `Warning` (khong `Information`): xuat PII la **su kien can vet**, khong phai thao tac thuong ngay.
        Logger.LogWarning(
            "XUAT DU LIEU CA NHAN: khach={CustomerId} · so dong ho so={SoDong} · so don={SoDon} · bi cat={BiCat} · " +
            "nguoi xuat={NguoiXuat} · {MoTa}. [CUS-XUAT-DU-LIEU-CA-NHAN]",
            khach.Id, dto.CacDong.Count, dto.TongSoDon, dto.DonHangBiCat, nguoiXuat, MoTaAnToan(khach));

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **NHAT KY KIEM TOAN** (`CUS-001` §22) — 🔑 tra loi **AI / KHI NAO / BAO NHIEU LAN**.
        //
        // 🔒 **VI SAO GHI TRONG `requiresNew: true` (UoW RIENG):**
        //
        // 📌 `Logger.LogWarning` o tren **KHONG** du: log bi **xoay vong**, **khong tra cuu duoc**, va — quan
        // trong nhat — ⚠️ **nam ngoai giao dich**. 🔑 Nhung mot **lan THU xuat PII** cung la su kien **can vet**
        // (co the la **lam dung**: nguoi ta thu di thu lai de lay du lieu).
        //
        // ✅ UoW **rieng** nghia la: neu nhat ky ghi **xong** roi phan sau **nem**, 🔒 **vet VAN CON**.
        // ⚠️ Nguoc lai (ghi chung UoW), mot loi o buoc sau se **cuon theo** ca nhat ky ⇒ 📌 ta **mat vet**
        // dung o nhung lan **dang ngo** nhat.
        // ⚠️ Va ta **KHONG** de loi ghi nhat ky lam **hong** ban xuat cua nguoi dung: 🔑 nguoi dung dang lam
        // viec **hop phap**; 📌 neu nhat ky loi thi **ghi log loi** (co the sua), khong **chan** nghiep vu.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

            await _nhatKyXuatRepository.InsertAsync(new CustomerDataExportLog(
                Guid.NewGuid(),
                khach.Id,
                nguoiXuat,
                dto.XuatLucUtc,
                soDongHoSo: dto.CacDong.Count,
                soDonHang: dto.TongSoDon,
                donHangBiCat: dto.DonHangBiCat,
                // 🔑 **LY DO di thang vao nhat ky** — 📌 khong chi tra ve cho nguoi goi: 🔒 mot ly do
                // **khong duoc luu** thi **khong tra loi duoc** cau hoi luat ve sau.
                lyDo: lyDoSach), autoSave: false);

            // 📌 `CompleteAsync` **tu luu** moi thay doi trong UoW nay ⇒ 🔑 khong can `autoSave: true`
            // (va `autoSave: true` se **luu hai lan** — 📌 thua mot vong ghi DB).
            await uow.CompleteAsync();
        }
        catch (Exception ex)
        {
            // ⚠️ **KHONG** nem tiep — xem ly do o tren. 🔑 Nhung phai **noi that** la vet **KHONG** duoc ghi,
            // 📌 vi mot vet **im lang mat** con te hon mot vet **khong co** (ta tuong la co).
            Logger.LogError(ex,
                "🚨 KHONG GHI DUOC NHAT KY XUAT PII (khach={CustomerId}). ⚠️ VET BI MAT — can xu ly ngay. " +
                "[CUS-NHAT-KY-XUAT-LOI]",
                khach.Id);
        }

        return dto;
    }

    // ── NHAT KY KIEM TOAN (doc) ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🚨 **TRA CUU NHAT KY XUAT DU LIEU CA NHAN** (`CUS-001` §23) — tra loi *"**AI** da xuat du lieu cua toi,
    /// **KHI NAO**, **BAO NHIEU LAN**"*.
    ///
    /// 🔑 **VI SAO CAN DUONG DOC NAY:** o §22 toi da xay bang `CusDataExportLogs` — 🔒 nhung mot bang chung
    /// **khong ai doc duoc** thi **khong phuc vu** cau hoi nao. 📌 Nay nguoi kiem toan **tu xem duoc**, khong
    /// phai **truy van SQL** (do la **no da ghi** o §22.6 #1).
    ///
    /// 🔐 **QUYEN = `Customers.Manage`** — 🔑 **nhat quan voi quyen XUAT** (`XuatDuLieuCaNhanAsync`):
    /// neu mot nguoi duoc phep **xuat** PII thi ho **cung** phai duoc phep **xem vet** cua viec xuat.
    /// 📌 Nguoc lai thi ta co tinh huong vo ly: nguoi **lam** duoc viec nhung **khong tu kiem** duoc minh.
    /// ⚠️ Va **khong** dung `Customers.Default` (quyen **xem ho so**): 🔒 nhat ky nay noi **ai da lam gi** voi
    /// du lieu ca nhan ⇒ 📌 no la **thong tin ve nhan vien**, khong phai **thong tin ve khach**.
    ///
    /// **⏳ SAP XEP:** moi nhat truoc, ⚠️ **tat dinh** (`XuatLucUtc` roi `Id`) — 🔑 neu chi sap theo thoi gian
    /// thi nhieu dong cung moc se cho **thu tu khac nhau moi lan** ⇒ 📌 ket qua **khong tai lap duoc**.
    /// </summary>
    /// <param name="customerId">⚠️ **Tuy chon** — loc theo mot khach (📌 cau hoi cua **chu the du lieu**).</param>
    /// <param name="tuNgayUtc">⚠️ **Tuy chon** — **kem** (bao gom).</param>
    /// <param name="denNgayUtc">
    /// ⚠️ **Tuy chon** — 🔒 **KHONG bao gom** (nua mo). 📌 **VI SAO NUA MO:** neu lay `<= denNgay` voi moc
    /// `00:00` thi **moi** lan xuat **trong ngay cuoi cung** bi **mat** — ⚠️ va do la loai loi **im lang**
    /// nguy hiem nhat voi mot man hinh **kiem toan** (🔑 nguoi kiem toan ket luan *"khong ai xuat"* khi
    /// thuc te **co**). ✅ Nua mo (`< den + 1 ngay`) lam moc ngay **tron ven**.
    /// </param>
    /// <param name="nguoiXuat">
    /// ⚠️ **Tuy chon** — loc theo **NGUOI XUAT**. 🔑 Cau hoi kiem toan that: *"nhan vien X da xuat nhung gi"*.
    ///
    /// 🔒 **KHOP CHINH XAC (`==`), KHONG `Contains`** — 📌 **VI SAO LA MOT QUYET DINH, KHONG PHAI SU LUA:**
    /// <list type="bullet">
    ///   <item>`Contains` lam **"an"** (nhan vien **An** khop voi **Anh**, **Hoang An**, …) ⇒ ⚠️ ket qua
    ///     **thua** dong ⇒ 📌 voi mot man hinh **kiem toan**, *"thua"* nghia la **gan sai** cho nguoi khac
    ///     — 🔑 va do la loi **nghiem trong hon** *"thieu"*;</item>
    ///   <item>`Contains` **KHONG dung duoc index** ⇒ 🔑 quet **ca bang** — ⚠️ bang nhat ky **chi tang**, nen
    ///     day la loai cham **ngay cang te**;</item>
    ///   <item>Gia tri den tu **dropdown** (danh sach nguoi xuat **that**), ⚠️ **khong** phai o nhap tu do
    ///     ⇒ 📌 khong can *"tim gan dung"*.</item>
    /// </list>
    /// </param>
    /// <param name="toiDa">Tran so dong (⚠️ **co cat** — xem `KetQuaNhatKyXuatDuLieuDto.BiCat`).</param>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<KetQuaNhatKyXuatDuLieuDto> GetNhatKyXuatDuLieuAsync(
        Guid? customerId = null,
        DateTime? tuNgayUtc = null,
        DateTime? denNgayUtc = null,
        string? nguoiXuat = null,
        int toiDa = 100)
    {
        var soLuong = Math.Clamp(toiDa, 1, 500);

        var truyVan = await _nhatKyXuatRepository.GetQueryableAsync();

        if (customerId is not null)
        {
            truyVan = truyVan.Where(x => x.CustomerId == customerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(nguoiXuat))
        {
            // 🔒 **KHOP CHINH XAC** — xem chu thich o `<param name="nguoiXuat">`.
            // 📌 `Trim()` o day (khong o UI) ⇒ 🔑 de **moi duong goi** (UI, API, script) deu duoc chuan hoa.
            var ten = nguoiXuat.Trim();
            truyVan = truyVan.Where(x => x.NguoiXuat == ten);
        }

        if (tuNgayUtc is not null)
        {
            truyVan = truyVan.Where(x => x.XuatLucUtc >= tuNgayUtc.Value);
        }

        if (denNgayUtc is not null)
        {
            // 🔒 **NUA MO** — xem chu thich o `<param name="denNgayUtc">`.
            var mocSau = denNgayUtc.Value.Date.AddDays(1);
            truyVan = truyVan.Where(x => x.XuatLucUtc < mocSau);
        }

        // 📌 Dem **TONG** truoc khi cat — 🔑 de man hinh noi duoc *"co bao nhieu, hien bao nhieu"*.
        var tongSo = await AsyncExecuter.CountAsync(truyVan);

        var nhatKy = await AsyncExecuter.ToListAsync(
            truyVan.OrderByDescending(x => x.XuatLucUtc).ThenByDescending(x => x.Id).Take(soLuong));

        // 🔑 **TRA TEN KHACH** — ⚠️ de nguoi kiem toan doc duoc *"xuat du lieu cua AI"*, chu khong phai mot GUID.
        // 📌 Gom **MOT LAN** (tranh N+1) va 🔒 **KHONG** nem neu khach da bi xoa: nhat ky phai **song sot**.
        var cacKhachId = nhatKy.Select(x => x.CustomerId).Distinct().ToList();
        var truyVanKhach = await _khachRepository.GetQueryableAsync();

        var tenTheoKhach = (await AsyncExecuter.ToListAsync(
                truyVanKhach.Where(x => cacKhachId.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name })))
            .ToDictionary(x => x.Id, x => x.Name);

        return new KetQuaNhatKyXuatDuLieuDto
        {
            TongSo = tongSo,
            BiCat = tongSo > soLuong,
            CacDong = nhatKy.Select(x => new NhatKyXuatDuLieuDto
            {
                Id = x.Id,
                CustomerId = x.CustomerId,
                // ⚠️ `GetValueOrDefault` ⇒ `null` khi ho so da bi xoa ⇒ 📌 man hinh phai **noi ro**, khong de trong.
                TenKhach = tenTheoKhach.GetValueOrDefault(x.CustomerId),
                NguoiXuat = x.NguoiXuat,
                XuatLucUtc = x.XuatLucUtc,
                SoDongHoSo = x.SoDongHoSo,
                SoDonHang = x.SoDonHang,
                DonHangBiCat = x.DonHangBiCat,
                LyDo = x.LyDo
            }).ToList()
        };
    }

    /// <summary>
    /// 🔑 **DANH SACH NGUOI DA XUAT** (cho **dropdown** cua man hinh nhat ky) — 📌 du lieu **THAT**, khong phai
    /// mot danh sach viet cung trong view.
    ///
    /// ⚠️ **VI SAO KHONG DUNG O NHAP TU DO:** 🔒 nguoi kiem toan **go sai mot ky tu** se nhan **0 dong**, va
    /// 📌 voi mot man hinh **kiem toan** thi *"0 dong"* bi doc thanh *"khong ai xuat"* — 🔑 mot **ket luan sai
    /// ve bang chung** sinh ra tu mot **loi go tay**. ✅ Dropdown lam dieu do **khong the** xay ra.
    ///
    /// ➕ Va day cung la ly do `GetNhatKyXuatDuLieuAsync` loc theo **khop CHINH XAC**: 🔑 gia tri den tu
    /// **du lieu that** ⇒ 📌 khong can *"tim gan dung"*, va **an** khong bi lan voi **Anh**.
    /// </summary>
    /// <param name="customerId">⚠️ Tuy chon — thu hep danh sach theo mot khach.</param>
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<List<string>> GetDanhSachNguoiXuatNhatKyAsync(Guid? customerId = null)
    {
        var truyVan = await _nhatKyXuatRepository.GetQueryableAsync();

        if (customerId is not null)
        {
            truyVan = truyVan.Where(x => x.CustomerId == customerId.Value);
        }

        // 📌 Sap xep **tat dinh** (`OrderBy`) ⇒ 🔑 dropdown khong doi thu tu moi lan tai trang
        // (⚠️ neu khong, nguoi dung se **tuong** danh sach da doi ⇒ 📌 mat long tin vao man hinh kiem toan).
        return await AsyncExecuter.ToListAsync(
            truyVan.Select(x => x.NguoiXuat).Distinct().OrderBy(x => x));
    }

    // ── CHUYỂN ĐỔI ──────────────────────────────────────────────────────────────────────────────

    private static CustomerDto ToDto(Customer k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        Email = k.Email,
        Phone = k.Phone,
        IdentityUserId = k.IdentityUserId,
        Status = k.Status.ToString(),
        DungDuoc = k.DungDuoc,
        LaKhachVangLai = k.IdentityUserId is null,
        CustomerGroupId = k.CustomerGroupId,
        LifetimeValueAmount = k.LifetimeValueAmount,
        LifetimeValueCurrency = k.LifetimeValueCurrency,
        CreationTime = k.CreationTime
    };

    /// <summary>Mô tả khách an toàn để ghi log — **CHỈ** qua <see cref="PiiMasker"/> (`ADR-11`).</summary>
    private static string MoTaAnToan(Customer k)
        => $"email={PiiMasker.CheEmail(k.Email)} · sđt={PiiMasker.CheDienThoai(k.Phone)}";
}

/// <summary>
/// 🔑 **KIEU TOM TAT DON HANG cho phep chieu cua EF** — ⚠️ **KHONG** dung anonymous type o day.
///
/// 📌 **VI SAO:** `AsyncExecuter.CountAsync(...)` tren mot `IQueryable` co `Select` sang **anonymous type**
/// co the khong dich duoc (EF phai dich bieu thuc tren **kieu co ten**). 🔒 Va mot loi dich bieu thuc o day
/// se **nem luc chay** (khong phai luc build) ⇒ 📌 ta chi biet khi **goi that** — dung loai loi kho lan ra.
/// ✅ Kieu **co ten** lam phep chieu **tuong minh** va **dich duoc**.
/// </summary>
internal sealed class DonHangTomTat
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public string ChannelCode { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public decimal GrandTotal { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime CreationTime { get; set; }
}
