using eCommerce.Reconciliation;
using eCommerce.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **APP SERVICE ĐỐI SOÁT TỒN KHO — ĐƯỜNG ĐỌC CHO NGƯỜI VẬN HÀNH** (`REC-001`).
///
/// **VÌ SAO PHẢI CÓ (nợ đã ghi ở `rec-001` §16.8, và đây là lý do nó nặng):**
/// entity `ReconRun`/`ReconRunMismatch` + `InventoryReconciliationService` + `InventoryReconciliationWorker`
/// **đã có, đã chạy, và đã ghi dữ liệu** — nhưng **không có một endpoint nào** để đọc.
/// ⇒ Lệch tồn kho **được phát hiện rồi nằm im trong DB**. 📌 Cùng họ với `SAL-006`: *"đã cài nhưng
/// không có đường gọi tới"* — và cùng họ với bài học `XC-002` §0: **báo thành công trong khi không ai nhận**.
///
/// 🔒 **Ba quyết định thiết kế (mỗi cái chặn một kiểu hỏng):**
/// <list type="number">
///   <item><b>Trả `SoLechChuaXuLy` bên cạnh `SoLech`</b> — con số **hành động được**. Nếu chỉ trả tổng thì
///     danh sách **mãi** hiện như cũ và **không ai đọc nữa** (số phận của mọi báo cáo không có trạng thái);</item>
///   <item><b>Xử lý lệch CHỈ ghi trạng thái, KHÔNG sửa số đo</b> — `OnHand`/`TongSoCai`/`ChenhLech` là
///     **ảnh chụp tại thời điểm quét**; sửa chúng là **phá bằng chứng** ⇒ lịch sử đối soát vô nghĩa;</item>
///   <item><b>`DenUtc` bao gồm cả ngày đó</b> (`< DenUtc.AddDays(1)`) — xem chú thích ở
///     <see cref="LocLuotDoiSoatInput"/>: dùng `<=` trần sẽ **bỏ sót** lượt của `23:59:59.xxx`.</item>
/// </list>
///
/// 🔐 **`[Authorize]` ở CẤP CLASS** (quy ước #4). Dùng quyền `eCommerce.Inventory` (**đọc**) cho cả ba
/// đường đọc, và **cùng quyền đó** cho thao tác xử lý — 📌 lý do: *"đánh dấu đã xử lý"* là **ghi chú vận hành**
/// trên một dòng **đã có**, **không** đổi tồn kho và **không** đổi số liệu đo. Nó **khác hẳn**
/// `Inventory.Adjust` (điều chỉnh tồn — thao tác **đổi tài sản thật**). Dùng `Adjust` ở đây sẽ
/// **chặn oan** người trực kho (người phát hiện lệch lại không được ghi chú là đã xem).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class DoiSoatAppService : ApplicationService, IDoiSoatAppService
{
    /// <summary>Trần số dòng một trang — chặn client xin `MaxResultCount` khổng lồ.</summary>
    private const int TranDongMotTrang = 200;

    private readonly IRepository<ReconRun, Guid> _luotDoiSoat;
    private readonly IRepository<ReconRunMismatch, Guid> _dongLech;
    private readonly IRepository<Warehouse, Guid> _kho;
    private readonly DoiSoatRetentionService _luuTru;
    private readonly ISettingProvider _settingProvider;
    private readonly IRepository<IdentityUser, Guid> _nguoiDung;

    public DoiSoatAppService(
        IRepository<ReconRun, Guid> luotDoiSoat,
        IRepository<ReconRunMismatch, Guid> dongLech,
        IRepository<Warehouse, Guid> kho,
        DoiSoatRetentionService luuTru,
        ISettingProvider settingProvider,
        IRepository<IdentityUser, Guid> nguoiDung)    {
        _luotDoiSoat = luotDoiSoat;
        _dongLech = dongLech;
        _kho = kho;
        _luuTru = luuTru;
        _settingProvider = settingProvider;
        _nguoiDung = nguoiDung;
    }

    /// <summary>
    /// 🆕 Tra **tên** người xác nhận cho một tập DTO — **một** truy vấn cho cả trang (không N+1).
    ///
    /// 🔒 **VÌ SAO PHẢI TRA TÊN Ở ĐÂY (không để UI tự tra):** UI Web chạy in-process và **không** nạp module
    /// Identity của ABP ⇒ nếu để view tự tra thì **nó không có** repository người dùng. 📌 Và nếu mỗi
    /// dòng tự gọi API thì thành N+1 trên đúng cái màn hình liệt kê lịch sử.
    ///
    /// ⚠️ **KHÔNG tra được tên KHÔNG phải lỗi:** `ConfirmedByName` để `null` ⇒ UI rơi về **GUID rút gọn** và
    /// nói rõ *"không tra được tên"*. 📌 Ghi đè bằng *"(không rõ)"* sẽ làm người kiểm toán **không phân biệt**
    /// được *"tài khoản đã bị xoá cứng"* với *"lỗi tra cứu"*.
    ///
    /// 📌 **Vì sao `IRepository<IdentityUser, Guid>` chứ không `IIdentityUserRepository`:** bản thứ hai
    /// **không** phơi `GetQueryableAsync` (đã đo: `CS1061`) — nó là repository **tuỳ biến** của module
    /// Identity. ✅ `IRepository<…>` là hợp đồng chuẩn của ABP nên **lọc được ngay trong SQL**.
    /// </summary>
    private async Task GanTenNguoiXacNhanAsync(List<LuotDoiSoatDto> cacLuot)
    {
        var idCanTra = cacLuot
            .Where(x => x.ConfirmedByUserId is not null)
            .Select(x => x.ConfirmedByUserId!.Value)
            .Distinct()
            .ToList();

        if (idCanTra.Count == 0)
        {
            return;
        }

        var nguoiDung = await AsyncExecuter.ToListAsync(
            (await _nguoiDung.GetQueryableAsync()).Where(x => idCanTra.Contains(x.Id)));

        var tenTheoId = nguoiDung.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Name)
                ? x.UserName
                : (string.IsNullOrWhiteSpace(x.Surname) ? $"{x.Name}" : $"{x.Name} {x.Surname}"));

        foreach (var luot in cacLuot)
        {
            if (luot.ConfirmedByUserId is { } id && tenTheoId.TryGetValue(id, out var ten))
            {
                luot.ConfirmedByName = ten;
            }
        }
    }

    // ── LƯỢT ĐỐI SOÁT ───────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PagedResultDto<LuotDoiSoatDto>> GetDanhSachLuotAsync(LocLuotDoiSoatInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var truyVan = await _luotDoiSoat.GetQueryableAsync();

        if (input.TuUtc is { } tu)
        {
            truyVan = truyVan.Where(x => x.BatDauUtc >= tu);
        }

        if (input.DenUtc is { } den)
        {
            // 🔒 **BAO GỒM cả ngày `den`**: `< den.AddDays(1)` chứ KHÔNG `<= den`.
            // 📌 Người dùng chọn "đến 16/10" và mong thấy lượt lúc 23:59 ngày 16/10. Dùng `<= den`
            // (nửa đêm) sẽ **bỏ sót** đúng những lượt đó ⇒ *"thiếu lượt"* mà không ai hiểu vì sao.
            // ⚠️ `AddDays(1)` trên mốc đã có `Kind` ⇒ giữ nguyên `Kind`, an toàn với cột `timestamptz`.
            var mocSau = den.AddDays(1);
            truyVan = truyVan.Where(x => x.BatDauUtc < mocSau);
        }

        if (input.ChiVuotNguong == true)
        {
            truyVan = truyVan.Where(x => x.CoVuotNguong);
        }

        // 🆕 `S7` — *"hộp việc"* của người có quyền `eCommerce.Reconciliation.Confirm`.
        // 📌 Điều kiện viết **y hệt** `ReconRun.ChoXacNhan` và `DoiSoatRetention` (giữ bằng chứng khi chờ
        // xác nhận) ⇒ một định nghĩa *"đang chờ"* cho cả ba chỗ; lệch một chỗ là màn hình / dọn dữ liệu
        // **nói khác nhau** về cùng một lượt.
        if (input.ChiChoXacNhan == true)
        {
            truyVan = truyVan.Where(x => x.KetLuan == KetLuanDoiSoat.VuotCanXacNhan && x.ConfirmedAtUtc == null);
        }

        // ⚠️ Lọc theo *"còn việc chưa xử lý"* phải hỏi bảng **dòng lệch** ⇒ cần truy vấn con.
        // 📌 Làm bằng `Contains` trên id (dịch được sang `IN (...)`), KHÔNG nạp cả bảng lệch vào bộ nhớ.
        if (input.ChiConViecChuaXuLy == true)
        {
            var luotConViec = (await _dongLech.GetQueryableAsync())
                .Where(x => !x.DaXuLy)
                .Select(x => x.ReconRunId)
                .Distinct();

            truyVan = truyVan.Where(x => luotConViec.Contains(x.Id));
        }

        var tong = await AsyncExecuter.CountAsync(truyVan);

        var soLuong = Math.Clamp(input.MaxResultCount, 1, TranDongMotTrang);
        var boQua = Math.Max(0, input.SkipCount);

        var ds = await AsyncExecuter.ToListAsync(
            truyVan.OrderByDescending(x => x.BatDauUtc).Skip(boQua).Take(soLuong));

        // 🔒 Đếm số lệch CHƯA xử lý cho **trang hiện tại** bằng MỘT truy vấn gộp (không N+1).
        var idTrang = ds.Select(x => x.Id).ToList();
        var demTheoLuot = idTrang.Count == 0
            ? new Dictionary<Guid, int>()
            : (await AsyncExecuter.ToListAsync(
                (await _dongLech.GetQueryableAsync())
                    .Where(x => idTrang.Contains(x.ReconRunId) && !x.DaXuLy)
                    .GroupBy(x => x.ReconRunId)
                    .Select(g => new { LuotId = g.Key, So = g.Count() })))
              .ToDictionary(x => x.LuotId, x => x.So);

        var ketQua = ds.Select(x => ChuyenDto(x, demTheoLuot.GetValueOrDefault(x.Id))).ToList();

        // 🆕 Tra **tên** người xác nhận cho cả trang bằng MỘT truy vấn (không N+1) — 📌 màn hình không
        // phải hiện GUID. Tên chỉ để **đọc**; `ConfirmedByUserId` vẫn là nguồn sự thật (audit).
        await GanTenNguoiXacNhanAsync(ketQua);

        return new PagedResultDto<LuotDoiSoatDto>(tong, ketQua);
    }

    /// <inheritdoc />
    public async Task<LuotDoiSoatDto> GetLuotAsync(Guid reconRunId)
    {
        var luot = await _luotDoiSoat.FindAsync(reconRunId)
            ?? throw new BusinessException(ReconErrorCodes.KhongTimThayBanGhiDoiSoat)
                .WithData("reconRunId", reconRunId)
                .WithData("loai", "luot-doi-soat");

        var conLai = await AsyncExecuter.CountAsync(
            (await _dongLech.GetQueryableAsync()).Where(x => x.ReconRunId == reconRunId && !x.DaXuLy));

        var dto = ChuyenDto(luot, conLai);
        await GanTenNguoiXacNhanAsync([dto]);

        return dto;
    }

    // ── DÒNG LỆCH ───────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PagedResultDto<DongLechDto>> GetDanhSachDongLechAsync(LocDongLechInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.ReconRunId == Guid.Empty)
        {
            throw new BusinessException(ReconErrorCodes.KhongTimThayBanGhiDoiSoat)
                .WithData("lyDo", "Thiếu `ReconRunId` — danh sách lệch luôn thuộc MỘT lượt.");
        }

        var truyVan = (await _dongLech.GetQueryableAsync()).Where(x => x.ReconRunId == input.ReconRunId);

        if (input.ChiVuotNguong == true)
        {
            truyVan = truyVan.Where(x => x.VuotNguong);
        }

        if (input.ChiChuaXuLy == true)
        {
            truyVan = truyVan.Where(x => !x.DaXuLy);
        }

        if (input.WarehouseId is { } khoId && khoId != Guid.Empty)
        {
            truyVan = truyVan.Where(x => x.WarehouseId == khoId);
        }

        if (!string.IsNullOrWhiteSpace(input.Sku))
        {
            var sku = input.Sku.Trim();
            truyVan = truyVan.Where(x => x.Sku.Contains(sku));
        }

        var tong = await AsyncExecuter.CountAsync(truyVan);

        var soLuong = Math.Clamp(input.MaxResultCount, 1, TranDongMotTrang);
        var boQua = Math.Max(0, input.SkipCount);

        var ds = await AsyncExecuter.ToListAsync(
            truyVan
                // 🔒 **Thứ tự có chủ ý: vượt ngưỡng TRƯỚC, rồi lệch LỚN trước.** Người trực cần thấy
                // việc **nặng nhất** ở đầu; sắp theo `Id` sẽ để một lệch `1` đơn vị lên trước một lệch
                // `500` đơn vị chỉ vì nó được ghi sớm hơn.
                .OrderByDescending(x => x.VuotNguong)
                .ThenByDescending(x => x.ChenhLech < 0 ? -x.ChenhLech : x.ChenhLech)
                .Skip(boQua).Take(soLuong));

        var tenKho = await LayTenKhoAsync(ds.Select(x => x.WarehouseId).Distinct().ToList());

        var ketQua = ds.Select(x => ChuyenDto(x, tenKho.GetValueOrDefault(x.WarehouseId))).ToList();

        return new PagedResultDto<DongLechDto>(tong, ketQua);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Inventory.Default)]
    public async Task<DongLechDto> PostXuLyLechAsync(Guid dongLechId, XuLyLechInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var dong = await _dongLech.FindAsync(dongLechId)
            ?? throw new BusinessException(ReconErrorCodes.KhongTimThayBanGhiDoiSoat)
                .WithData("dongLechId", dongLechId)
                .WithData("loai", "dong-lech");

        var daXuLyTruocDo = dong.DaXuLy;

        // 🚨 Domain **NÉM** nếu thiếu lý do/người xử lý, và **no-op** nếu đã xử lý (không ghi đè bằng chứng).
        // 📌 App service **KHÔNG** bọc `try/catch` để "cho qua": bọc lại là biến guard **ném lỗi** thành
        // guard **im lặng** (quy ước #11 — đúng lỗi đã trả giá nhiều lần).
        dong.DanhDauDaXuLy(Clock.Now, input.NguoiXuLy, input.LyDo);

        if (!daXuLyTruocDo)
        {
            await _dongLech.UpdateAsync(dong, autoSave: true);

            Logger.LogWarning(
                "ĐÃ XỬ LÝ LỆCH ĐỐI SOÁT: lượt={ReconRunId} · kho={WarehouseId} · SKU={Sku} · " +
                "chênh lệch={ChenhLech} · người xử lý={NguoiXuLy} · lý do={LyDo}. [REC-DA-XU-LY]",
                dong.ReconRunId, dong.WarehouseId, dong.Sku, dong.ChenhLech, input.NguoiXuLy, input.LyDo);
        }
        else
        {
            // 🔔 Gọi lại (retry/double-click) ⇒ **vô hại**, nhưng ghi log để **không** im lặng hoàn toàn:
            // nếu ai đó cố **đổi** lý do/lý do cũ, họ cần biết là **không** đổi được.
            Logger.LogInformation(
                "LỆCH ĐÃ XỬ LÝ TRƯỚC ĐÓ ⇒ không ghi đè bằng chứng (no-op): lượt={ReconRunId} · SKU={Sku} · " +
                "người xử lý đầu={NguoiXuLyDau}. [REC-DA-XU-LY-TRUOC-DO]",
                dong.ReconRunId, dong.Sku, dong.NguoiXuLy);
        }

        var tenKho = await LayTenKhoAsync([dong.WarehouseId]);

        return ChuyenDto(dong, tenKho.GetValueOrDefault(dong.WarehouseId));
    }

    // ── 🆕 `S7`: XÁC NHẬN LƯỢT + RETENTION ──────────────────────────────────────────────────────

    /// <summary>
    /// 🚨 **XÁC NHẬN một lượt đang chờ xác nhận** — quyết định `Q5` = **A**: ghi `ai / khi nào / vì sao`
    /// rồi **đóng cảnh báo**. 🔒 **KHÔNG** sửa tồn kho, **KHÔNG** sinh bút toán, **KHÔNG** đụng các dòng lệch.
    ///
    /// 🔐 Quyền riêng `eCommerce.Reconciliation.Confirm` — 📌 khác `Inventory.Default` (xem/đánh dấu dòng lệch)
    /// vì xác nhận làm cảnh báo **biến mất** khỏi danh sách cần xem.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Reconciliation.Confirm)]
    public async Task<LuotDoiSoatDto> PostXacNhanLuotAsync(Guid reconRunId, XacNhanLuotInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var luot = await _luotDoiSoat.FindAsync(reconRunId)
            ?? throw new BusinessException(ReconErrorCodes.KhongTimThayBanGhiDoiSoat)
                .WithData("reconRunId", reconRunId)
                .WithData("loai", "luot-doi-soat");

        // 🔒 **Ai xác nhận lấy từ PRINCIPAL, không từ input:** người gõ tay tên không đối chiếu được với
        // tài khoản nào ⇒ không phải audit (`Q5` yêu cầu `ConfirmedByUserId`).
        // 🚨 **Ném `AbpAuthorizationException`, KHÔNG ném một mã nghiệp vụ** (bản đầu ném `0005` = *"thiếu ghi
        // chú"* — 📌 **sai nghĩa**: người nhận sẽ đi điền ghi chú trong khi vấn đề là **không có chủ thể**).
        // Trạng thái này **không tới được** qua UI/API (đã có `[Authorize]` cấp class + quyền) — đây là hàng
        // rào cho ca principal méo mó, và `403` là mã đúng nghĩa cho nó.
        var nguoiXacNhan = CurrentUser.Id
            ?? throw new Volo.Abp.Authorization.AbpAuthorizationException(
                "Không xác định được người dùng hiện tại ⇒ không ghi được audit cho việc xác nhận lệch.");

        var daXacNhanTruocDo = luot.ConfirmedAtUtc is not null;

        // 🚨 Domain **NÉM** `0004`/`0005` và **no-op** nếu đã xác nhận — app service KHÔNG bọc `try/catch`
        // để "cho qua" (quy ước #11: bọc lại là biến guard ném lỗi thành guard im lặng).
        luot.XacNhan(nguoiXacNhan, Clock.Now, input.GhiChu);

        if (!daXacNhanTruocDo)
        {
            await _luotDoiSoat.UpdateAsync(luot, autoSave: true);

            Logger.LogWarning(
                "ĐÃ XÁC NHẬN LỆCH ĐỐI SOÁT: lượt={ReconRunId} · người xác nhận={NguoiDung} · " +
                "số lệch vượt ngưỡng={SoVuot} · ghi chú={GhiChu}. KHÔNG sửa tồn/sổ cái (Q5 = A). [REC-DA-XAC-NHAN]",
                luot.Id, nguoiXacNhan, luot.SoLechVuotNguong, luot.ConfirmNote);
        }
        else
        {
            // 🔔 Gọi lại (double-click/retry) ⇒ **vô hại**, nhưng ghi log để không im lặng hoàn toàn: người
            // muốn **đổi** ghi chú cần biết là **không đổi được** (bằng chứng của lần đầu được giữ).
            Logger.LogInformation(
                "LƯỢT ĐÃ XÁC NHẬN TRƯỚC ĐÓ ⇒ không ghi đè bằng chứng (no-op): lượt={ReconRunId} · " +
                "người xác nhận đầu={NguoiDung}. [REC-DA-XAC-NHAN-TRUOC-DO]",
                luot.Id, luot.ConfirmedByUserId);
        }

        var conLai = await AsyncExecuter.CountAsync(
            (await _dongLech.GetQueryableAsync()).Where(x => x.ReconRunId == reconRunId && !x.DaXuLy));

        var ketQua = ChuyenDto(luot, conLai);
        await GanTenNguoiXacNhanAsync([ketQua]);

        return ketQua;
    }

    /// <summary>
    /// 🆕 **DỌN LỊCH SỬ THEO RETENTION** (`REC-001` §17.8 #1) — gọi `DoiSoatRetentionService` (**cùng luật**
    /// với job định kỳ, không có bản thứ hai).
    ///
    /// 🔒 `ChiXemTruoc` mặc định **`true`** ở DTO ⇒ một lời gọi thiếu tham số **không** xoá gì.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Reconciliation.Confirm)]
    public async Task<KetQuaDonDepDoiSoatDto> PostDonDepAsync(DonDepDoiSoatInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var soNgay = input.SoNgay
            ?? ChinhSach.DocSoNgayLuuTru(await _settingProvider.GetOrNullAsync(TenChinhSach.LuuTruDoiSoat_SoNgay), out _);

        // 🚨 `soNgay <= 0` ⇒ domain **NÉM** `Recon:0006` (xem `DoiSoatRetention`: `0` = TẮT, không phải "xoá hết").
        var ketQua = await _luuTru.DonDepAsync(Clock.Now, soNgay, input.ChiXemTruoc);

        Logger.LogWarning(
            "DỌN LỊCH SỬ ĐỐI SOÁT: {CheDo} · số ngày={SoNgay} · mốc={Moc:O} · đủ điều kiện={DuDieuKien} · " +
            "đã xoá={DaXoa} · giữ lại (chưa xử lý)={ChuaXuLy} · giữ lại (chờ xác nhận)={ChoXacNhan}. [REC-DON-DEP]",
            input.ChiXemTruoc ? "XEM TRƯỚC" : "XOÁ THẬT",
            ketQua.SoNgay, ketQua.MocUtc, ketQua.SoDongDuDieuKien, ketQua.SoDongDaXoa,
            ketQua.SoDongGiuLaiChuaXuLy, ketQua.SoDongGiuLaiChoXacNhan);

        return new KetQuaDonDepDoiSoatDto
        {
            SoNgay = ketQua.SoNgay,
            MocUtc = ketQua.MocUtc,
            ChiXemTruoc = ketQua.ChiXemTruoc,
            SoDongDuDieuKien = ketQua.SoDongDuDieuKien,
            SoDongDaXoa = ketQua.SoDongDaXoa,
            SoDongGiuLaiChuaXuLy = ketQua.SoDongGiuLaiChuaXuLy,
            SoDongGiuLaiChoXacNhan = ketQua.SoDongGiuLaiChoXacNhan
        };
    }

    // ── CHUYỂN ĐỔI ──────────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, string>> LayTenKhoAsync(List<Guid> idKho)
    {
        if (idKho.Count == 0)
        {
            return [];
        }

        var kho = await AsyncExecuter.ToListAsync(
            (await _kho.GetQueryableAsync()).Where(x => idKho.Contains(x.Id)));

        return kho
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Code);
    }

    private static LuotDoiSoatDto ChuyenDto(ReconRun x, int soLechChuaXuLy) => new()
    {
        Id = x.Id,
        BatDauUtc = x.BatDauUtc,
        KetThucUtc = x.KetThucUtc,
        SoGiayQuet = Math.Round((x.KetThucUtc - x.BatDauUtc).TotalSeconds, 3),
        SoCapDaDo = x.SoCapDaDo,
        SoLech = x.SoLech,
        SoLechVuotNguong = x.SoLechVuotNguong,
        CoVuotNguong = x.CoVuotNguong,
        DonViNguong = x.DonViNguong.ToString(),
        GiaTriNguong = x.GiaTriNguong,
        HanhDong = x.HanhDong.ToString(),
        NguongHopLe = x.NguongHopLe,
        KetLuan = x.KetLuan.ToString(),
        SoLechChuaXuLy = soLechChuaXuLy,
        ChoXacNhan = x.ChoXacNhan,
        DaXacNhan = x.ConfirmedAtUtc is not null,
        ConfirmedByUserId = x.ConfirmedByUserId,
        ConfirmedAtUtc = x.ConfirmedAtUtc,
        ConfirmNote = x.ConfirmNote
    };

    private static DongLechDto ChuyenDto(ReconRunMismatch x, string? maKho) => new()
    {
        Id = x.Id,
        ReconRunId = x.ReconRunId,
        WarehouseId = x.WarehouseId,
        WarehouseCode = maKho,
        Sku = x.Sku,
        OnHand = x.OnHand,
        TongSoCai = x.TongSoCai,
        ChenhLech = x.ChenhLech,
        VuotNguong = x.VuotNguong,
        DaXuLy = x.DaXuLy,
        XuLyLucUtc = x.XuLyLucUtc,
        NguoiXuLy = x.NguoiXuLy,
        LyDoXuLy = x.LyDoXuLy
    };
}
