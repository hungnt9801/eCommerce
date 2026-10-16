using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;

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

    public CustomerAppService(IRepository<Customer, Guid> khachRepository, IAbpDistributedLock khoa)
    {
        _khachRepository = khachRepository;
        _khoa = khoa;
    }

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
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public async Task<XuatDuLieuCaNhanDto> XuatDuLieuCaNhanAsync(Guid customerId)
    {
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

        // 🔒 **LOG CHI QUA `MoTaAnToan`** (PII da che) — xem quyet dinh (2) o tren.
        // 📌 `Warning` (khong `Information`): xuat PII la **su kien can vet**, khong phai thao tac thuong ngay.
        Logger.LogWarning(
            "XUAT DU LIEU CA NHAN: khach={CustomerId} · so dong={SoDong} · nguoi xuat={NguoiXuat} · {MoTa}. " +
            "[CUS-XUAT-DU-LIEU-CA-NHAN]",
            khach.Id, dto.CacDong.Count, nguoiXuat, MoTaAnToan(khach));

        return dto;
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
