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
