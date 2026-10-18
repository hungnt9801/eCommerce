using eCommerce.Address;
using eCommerce.Address.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Customers;

/// <summary>
/// **SỔ ĐỊA CHỈ CỦA KHÁCH** (`ADDR-004` phần B — `S2`) — thêm/đặt mặc định/gỡ/đọc.
///
/// 🔒 **BỐN LUẬT ĐÃ TRẢ GIÁ ĐƯỢC THI HÀNH Ở ĐÂY:**
/// <list type="number">
///   <item><b>`[Authorize]` cấp CLASS</b> + permission cụ thể từng method (thiếu ⇒ `403` cho mọi người).</item>
///   <item><b>Đặt mặc định là thao tác TRANH CHẤP</b> ⇒ khoá phân tán <see cref="CustomersConsts.LockKeys.TheoKhach"/>
///     **bao ngoài** UoW (`ADDR-004` §8.2 #3). 🔑 Nếu Redis chết, DB **vẫn** chặn nhờ partial unique index —
///     và mã lỗi khi đó là `0012` (không phải `500`).</item>
///   <item><b>Bỏ-trước-đặt-sau trong MỘT transaction</b> (`ADR-06`): PostgreSQL kiểm unique index **ngay từng
///     câu lệnh** ⇒ đặt-trước-bỏ-sau sẽ `23505` (đã chứng minh: `sal-002`/`TC-06b`).</item>
///   <item><b>Gỡ mặc định ⇒ chọn mục THAY THẾ trong cùng giao dịch</b> (`BR-009`/A4), nếu không khách mất
///     mặc định mà **không ai biết**.</item>
/// </list>
///
/// ⚠️ **GHI RÕ GIỚI HẠN (không nói quá):** kiểm *"chính chủ"* ở đây dựa trên **permission** (`ManageBook`).
/// Việc so `Customer.IdentityUserId == CurrentUser.Id` cho đường khách tự phục vụ thuộc `WEB-*`/`S8`
/// (khi đó khách **không** có `ManageBook`) — 📌 nếu làm nửa vời ở đây thì người vận hành (không có
/// `IdentityUserId` trùng) sẽ **bị chặn nhầm**.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Addresses.Default)]
public class CustomerAddressAppService : ApplicationService, ICustomerAddressAppService
{
    private readonly IRepository<CustomerAddressLink, Guid> _lienKetRepository;
    private readonly IRepository<Customer, Guid> _khachRepository;
    private readonly IAddressDirectory _diaChiDirectory;
    private readonly IAbpDistributedLock _khoa;
    private readonly IDistributedEventBus _eventBus;
    private readonly IConfiguration _cauHinh;

    public CustomerAddressAppService(
        IRepository<CustomerAddressLink, Guid> lienKetRepository,
        IRepository<Customer, Guid> khachRepository,
        IAddressDirectory diaChiDirectory,
        IAbpDistributedLock khoa,
        IDistributedEventBus eventBus,
        IConfiguration cauHinh)
    {
        _lienKetRepository = lienKetRepository;
        _khachRepository = khachRepository;
        _diaChiDirectory = diaChiDirectory;
        _khoa = khoa;
        _eventBus = eventBus;
        _cauHinh = cauHinh;
    }

    /// <summary>Đọc sổ địa chỉ (chỉ liên kết **còn hiệu lực**). Sổ rỗng **không phải lỗi** (`ADDR-004` §12).</summary>
    public async Task<SoDiaChiDto> GetSoDiaChiAsync(Guid customerId)
    {
        await KhachPhaiTonTaiAsync(customerId);

        var lienKet = await _lienKetRepository.GetListAsync(x => x.CustomerId == customerId);

        var snapshot = await _diaChiDirectory.TimNhieuTheoIdAsync(lienKet.Select(x => x.AddressId).ToHashSet());

        var theoId = snapshot.ToDictionary(x => x.AddressId);

        var dong = new List<DiaChiTrongSoDto>(lienKet.Count);

        foreach (var lk in lienKet.OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreationTime))
        {
            // ⚠️ Địa chỉ đã bị ẩn/không đọc được ⇒ vẫn hiện mục đó với `CoDungDuoc = false`, **không** tự xoá
            // liên kết (`ADDR-004` §10 ghi chú ②: *"lỗi im lặng khi ẩn đi"*).
            if (!theoId.TryGetValue(lk.AddressId, out var snap))
            {
                continue;
            }

            dong.Add(Chuyen(lk, snap));
        }

        return new SoDiaChiDto
        {
            CustomerId = customerId,
            CacDiaChi = dong,
            // 🔑 Đếm THEO LOẠI — 📌 không phải tổng số địa chỉ (xem chú thích `SoDiaChiDto`).
            SoMacDinhGiaoHang = lienKet.Count(x => x.IsDefault && x.AddressType == LoaiDiaChi.Shipping),
            SoMacDinhHoaDon = lienKet.Count(x => x.IsDefault && x.AddressType == LoaiDiaChi.Billing)
        };
    }

    /// <summary>Thêm địa chỉ — tạo/dùng lại `Address` theo nội dung rồi tạo liên kết (xem chú thích lớp).</summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.ManageBook)]
    public async Task<DiaChiTrongSoDto> ThemDiaChiAsync(Guid customerId, ThemDiaChiInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        await KhachPhaiTonTaiAsync(customerId);

        var loai = ParseLoai(input.AddressType);

        // ── Trần số địa chỉ: **CẤU HÌNH**, mặc định 20 (`ADDR-004` Q1) ────────────────────────────
        var tran = LayTranDiaChi();

        var dangCo = await _lienKetRepository.CountAsync(x => x.CustomerId == customerId);

        if (dangCo >= tran)
        {
            // ⚠️ `ADDR-004` §8.1 #12 ghi rõ: race có thể cho vượt **1** — chấp nhận CÓ Ý THỨC (không phải
            // bất biến tài chính; thêm khoá chỉ để giữ một con số mềm là đánh đổi sai).
            throw new BusinessException(CustomerErrorCodes.VuotTranDiaChi)
                .WithData("customerId", customerId)
                .WithData("toiDa", tran)
                .WithData("dangCo", dangCo);
        }

        // ── Tạo/dùng lại `Address` (idempotent theo NỘI DUNG — ADR-04) ─────────────────────────────
        var snapshot = await _diaChiDirectory.TaoHoacLayAsync(new TaoDiaChiInput
        {
            SchemaVersion = input.SchemaVersion,
            TinhCode = input.TinhCode,
            XaCode = input.XaCode,
            SoNhaDuong = input.SoNhaDuong,
            NguonTao = "Web"
        });

        if (!snapshot.CoDungDuoc)
        {
            // 🔑 Địa chỉ thô không dùng được cho vận đơn/hoá đơn ⇒ không cho vào sổ giao hàng.
            throw new BusinessException(CustomerErrorCodes.DiaChiKhongDungDuoc)
                .WithData("addressId", snapshot.AddressId)
                .WithData("trangThai", snapshot.TrangThai)
                .WithData("lyDo", "Địa chỉ chưa chuẩn hoá (thiếu mã tỉnh/xã) ⇒ hãy chọn tỉnh và xã.");
        }

        // ── Chống trùng: báo bằng MÃ NGHIỆP VỤ kèm `linkId` để UI tô sáng mục đã có (`§12`) ─────────
        var daCo = await _lienKetRepository.FirstOrDefaultAsync(
            x => x.CustomerId == customerId && x.AddressId == snapshot.AddressId && x.AddressType == loai);

        if (daCo is not null)
        {
            throw new BusinessException(CustomerErrorCodes.LienKetDiaChiDaTonTai)
                .WithData("customerId", customerId)
                .WithData("linkId", daCo.Id)
                .WithData("addressId", snapshot.AddressId)
                .WithData("addressType", loai.ToString());
        }

        IAbpDistributedLockHandle? giuCho = null;

        try
        {
            if (input.LaMacDinh)
            {
                giuCho = await GiuKhoaKhachAsync(customerId);
                await BoMacDinhHienTaiAsync(customerId, loai);
            }

            var lienKet = new CustomerAddressLink(
                GuidGenerator.Create(), customerId, snapshot.AddressId, loai, input.Label, input.LaMacDinh);

            await _lienKetRepository.InsertAsync(lienKet, autoSave: true);

            if (lienKet.IsDefault)
            {
                await PhatSuKienMacDinhAsync(lienKet);
            }

            return Chuyen(lienKet, snapshot);
        }
        catch (DbUpdateException ex) when (LaLoiTrungMacDinh(ex))
        {
            // Redis chết / hai luồng vượt khoá ⇒ DB là **chốt cuối**; trả mã nghiệp vụ, KHÔNG để `500`.
            throw new BusinessException(CustomerErrorCodes.MacDinhKhongHopLe)
                .WithData("customerId", customerId)
                .WithData("lyDo", "Mục mặc định vừa bị luồng khác đặt — thử lại.");
        }
        finally
        {
            if (giuCho is not null)
            {
                await giuCho.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Đặt mặc định — 🔒 khoá phân tán **bao ngoài** UoW + bỏ-trước-đặt-sau trong **một** transaction.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.ManageBook)]
    public async Task<DiaChiTrongSoDto> DatMacDinhAsync(Guid customerId, Guid linkId)
    {
        await KhachPhaiTonTaiAsync(customerId);

        await using var giuCho = await GiuKhoaKhachAsync(customerId);

        var lienKet = await _lienKetRepository.FirstOrDefaultAsync(x => x.Id == linkId && x.CustomerId == customerId)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayLienKet)
                .WithData("customerId", customerId)
                .WithData("linkId", linkId);

        // 📌 Idempotent: gọi lại khi **đã** mặc định ⇒ trả luôn (retry/double-click là vô hại).
        if (lienKet.IsDefault)
        {
            return Chuyen(lienKet, await SnapshotAsync(lienKet.AddressId));
        }

        try
        {
            // ① BỎ cờ mặc định hiện tại TRƯỚC (nếu đặt trước sẽ `23505` — PostgreSQL kiểm ngay từng câu lệnh).
            await BoMacDinhHienTaiAsync(customerId, lienKet.AddressType);

            // ② ĐẶT mục này làm mặc định.
            lienKet.DatMacDinh();
            await _lienKetRepository.UpdateAsync(lienKet, autoSave: true);
        }
        catch (DbUpdateException ex) when (LaLoiTrungMacDinh(ex))
        {
            throw new BusinessException(CustomerErrorCodes.MacDinhKhongHopLe)
                .WithData("customerId", customerId)
                .WithData("linkId", linkId)
                .WithData("lyDo", "Hai luồng cùng đặt mặc định ⇒ DB đã chặn luồng này. Thử lại.");
        }

        await PhatSuKienMacDinhAsync(lienKet);

        return Chuyen(lienKet, await SnapshotAsync(lienKet.AddressId));
    }

    /// <summary>Gỡ địa chỉ = **xoá mềm**; nếu đang là mặc định ⇒ chọn mục thay thế ngay (`BR-009`/A4).</summary>
    [Authorize(eCommerceConsts.Permissions.Addresses.ManageBook)]
    public async Task GoDiaChiAsync(Guid customerId, Guid linkId)
    {
        await KhachPhaiTonTaiAsync(customerId);

        await using var giuCho = await GiuKhoaKhachAsync(customerId);

        var lienKet = await _lienKetRepository.FirstOrDefaultAsync(x => x.Id == linkId && x.CustomerId == customerId)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayLienKet)
                .WithData("customerId", customerId)
                .WithData("linkId", linkId);

        var dangLaMacDinh = lienKet.IsDefault;

        // 🔒 XOÁ MỀM (append-only): `FullAuditedAggregateRoot` ⇒ `IsDeleted = true`; bản ghi vẫn còn cho lịch sử.
        await _lienKetRepository.DeleteAsync(lienKet, autoSave: false);

        // 🚨 **PHẢI GHI XOÁ MỀM XUỐNG DB TRƯỚC KHI ĐẶT MẶC ĐỊNH THAY THẾ** — đây là bug THẬT đo được
        // (18/10/2026, probe `go-dia-chi` ⇒ `23505` trên `IX_CusAddressLinks_…_MacDinh`):
        // PostgreSQL kiểm partial unique index **ngay từng câu lệnh**; nếu EF phát `UPDATE … IsDefault = true`
        // cho mục thay thế **trước** câu `UPDATE … IsDeleted = true` của mục đang gỡ thì tại thời điểm đó
        // **hai** dòng cùng `(khách, loại)` đều đang là mặc định ⇒ chặn. 📌 Cùng bản chất với
        // *"bỏ-trước-đặt-sau"* ở `DatMacDinhAsync`, chỉ khác là "bỏ" đến từ đường **xoá mềm**.
        if (dangLaMacDinh)
        {
            await CurrentUnitOfWork!.SaveChangesAsync();
        }

        if (dangLaMacDinh)
        {
            var conLai = await _lienKetRepository.GetListAsync(
                x => x.CustomerId == customerId && x.AddressType == lienKet.AddressType && x.Id != linkId);

            // 🔑 A4: ưu tiên mục **dùng gần nhất**; nếu chưa mục nào có `LastUsedAtUtc` thì lấy mục **cũ nhất**
            // (ổn định, dễ giải thích). Còn 0 mục ⇒ **hợp lệ**: sổ không còn mặc định (`ADDR-004` §12).
            var thayThe = conLai
                .OrderByDescending(x => x.LastUsedAtUtc ?? DateTime.MinValue)
                .ThenBy(x => x.CreationTime)
                .FirstOrDefault();

            if (thayThe is not null)
            {
                thayThe.DatMacDinh();
                await _lienKetRepository.UpdateAsync(thayThe, autoSave: false);
            }

            await CurrentUnitOfWork!.SaveChangesAsync();
        }

        await CurrentUnitOfWork!.SaveChangesAsync();

        if (dangLaMacDinh)
        {
            await PhatSuKienMacDinhAsync(lienKet, laMacDinh: false);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // NỘI BỘ
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    private async Task KhachPhaiTonTaiAsync(Guid customerId)
    {
        // 🔒 `404` (`0001`) cho **cả** *"không tồn tại"* và *"tenant khác"* — không tiết lộ sự tồn tại
        // (`XC-004` BR-008). Bộ lọc tenant của ABP đã tự giới hạn phạm vi.
        _ = await _khachRepository.FindAsync(customerId)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayKhach)
                .WithData("customerId", customerId);
    }

    private async Task<IAbpDistributedLockHandle> GiuKhoaKhachAsync(Guid customerId)
    {
        // 🚨 Khoá phân tán **bao ngoài** UoW (`XC-003` §12): nhả sau khi transaction kết thúc.
        // 🔒 Tên khoá từ `CustomersConsts` — **CẤM** viết literal (hai nguồn sự thật = khoá không bảo vệ gì).
        return await _khoa.TryAcquireAsync(
                   CustomersConsts.LockKeys.TheoKhach(customerId),
                   TimeSpan.FromSeconds(CustomersConsts.ChoKhoaToiDaGiay))
               ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                   .WithData("khoa", "customers:customer")
                   .WithData("choGiay", CustomersConsts.ChoKhoaToiDaGiay);
    }

    /// <summary>Bỏ cờ mặc định của **mọi** mục cùng loại (bước ① của bỏ-trước-đặt-sau).</summary>
    private async Task BoMacDinhHienTaiAsync(Guid customerId, LoaiDiaChi loai)
    {
        var dangMacDinh = await _lienKetRepository.GetListAsync(
            x => x.CustomerId == customerId && x.AddressType == loai && x.IsDefault);

        foreach (var cu in dangMacDinh)
        {
            cu.BoMacDinh();
            await _lienKetRepository.UpdateAsync(cu, autoSave: false);
        }

        if (dangMacDinh.Count > 0)
        {
            // 🔑 Bắt buộc phải SaveChanges **trước** khi đặt cờ mới: PostgreSQL kiểm unique index ngay từng
            // câu lệnh ⇒ gộp chung thì vẫn có thể `23505` tuỳ thứ tự EF phát sinh (xem `TC-06b`).
            await CurrentUnitOfWork!.SaveChangesAsync();
        }
    }

    private int LayTranDiaChi()
    {
        var tho = _cauHinh[AddressConsts.TenCauHinhTranDiaChiMotKhach];

        return int.TryParse(tho, out var so) && so > 0 ? so : AddressConsts.TranDiaChiMotKhachMacDinh;
    }

    private static LoaiDiaChi ParseLoai(string? giaTri) => giaTri?.Trim().ToLowerInvariant() switch
    {
        null or "" or "shipping" or "0" => LoaiDiaChi.Shipping,
        "billing" or "1" => LoaiDiaChi.Billing,
        _ => throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
            .WithData("truong", nameof(giaTri))
            .WithData("giaTri", giaTri)
            .WithData("lyDo", "`addressType` chỉ nhận `Shipping` hoặc `Billing`.")
    };

    private async Task<DiaChiSnapshotDto> SnapshotAsync(Guid addressId)
        => await _diaChiDirectory.TimTheoIdAsync(addressId)
           ?? throw new BusinessException(CustomerErrorCodes.DiaChiKhongDungDuoc)
               .WithData("addressId", addressId)
               .WithData("lyDo", "Địa chỉ của mục này không đọc được (đã ẩn hoặc khác tenant).");

    private static DiaChiTrongSoDto Chuyen(CustomerAddressLink lk, DiaChiSnapshotDto snap) => new()
    {
        LinkId = lk.Id,
        AddressId = lk.AddressId,
        AddressType = lk.AddressType.ToString(),
        Label = lk.Label,
        IsDefault = lk.IsDefault,
        LastUsedAtUtc = lk.LastUsedAtUtc,
        CoDungDuoc = snap.CoDungDuoc,
        DiaChi = snap
    };

    /// <summary>
    /// Phát `CustomerDefaultAddressChangedEto` — 🔒 **không** chứa chữ địa chỉ (`XC-002` **D11**).
    /// 📌 Phát trong **cùng UoW**; event bus hiện là **in-process** (chưa có broker — nợ `XC-002`/`CAT-001` F8).
    /// </summary>
    private async Task PhatSuKienMacDinhAsync(CustomerAddressLink lienKet, bool laMacDinh = true)
    {
        await _eventBus.PublishAsync(new CustomerDefaultAddressChangedEto
        {
            TenantId = CurrentTenant.Id,
            CustomerId = lienKet.CustomerId,
            AddressType = lienKet.AddressType.ToString(),
            AddressId = lienKet.AddressId,
            LinkId = lienKet.Id,
            AggregateId = lienKet.Id,
            AggregateVersion = lienKet.ConcurrencyStamp,
            LaMacDinh = laMacDinh,
            CorrelationId = System.Diagnostics.Activity.Current?.Id
        });
    }

    /// <summary>
    /// Nhận diện `23505` **của partial unique index "một mặc định"** — chỉ ca đó mới đổi thành `0012`.
    /// ⚠️ Cố ý kiểm **tên index** trong thông điệp (không phải mọi `23505`): một `23505` khác (ví dụ liên kết
    /// trùng) phải giữ nguyên đường xử lý của nó, nếu không ta **gán sai nguyên nhân**.
    /// </summary>
    private static bool LaLoiTrungMacDinh(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("IX_CusAddressLinks_TenantId_CustomerId_AddressType_MacDinh", StringComparison.Ordinal) == true
           || ex.Message.Contains("IX_CusAddressLinks_TenantId_CustomerId_AddressType_MacDinh", StringComparison.Ordinal);
}
