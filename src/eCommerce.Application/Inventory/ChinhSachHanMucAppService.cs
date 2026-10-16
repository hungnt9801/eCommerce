using eCommerce.Application.Contracts.Inventory;
using eCommerce.ChannelHub.Channels;
using eCommerce.Inventory;
using eCommerce.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace eCommerce.Application.Inventory;

/// <summary>
/// 🚨 **ĐƯỜNG GHI CHUẨN CHO ADMIN** — đổi **chính sách "cho phép chọn"** và quản lý **hạn mức riêng từng kênh**
/// (`INV-003` · `REC-001`).
///
/// **VÌ SAO PHẢI CÓ LỚP NÀY (khoảng trống thật, mục 5 việc 4 của gói bàn giao):**
/// trước lớp này, muốn đổi hạn mức phải **sửa thẳng `AbpSettings` bằng SQL rồi xoá cache bằng tay** — và đó
/// chính là cái bẫy đã làm **4 phép đo cho kết quả SAI mà không có lỗi nào** (`inv-003` §16.6):
/// <list type="bullet">
///   <item>mẫu key cache hay bị **sai** (`'*TenSetting*'` khớp **0 key**);</item>
///   <item>cache có **2 tầng**: Redis **và** bộ nhớ tiến trình ⇒ xoá Redis mà **không** khởi động lại Host thì
///     giá trị **CŨ** vẫn có hiệu lực.</item>
/// </list>
/// 🔒 `ISettingManager` xử lý **cả hai** tầng ⇒ đây là đường ghi **duy nhất** nên dùng.
///
/// 📌 **VÌ SAO KHÔNG CHỈ NẠP `Volo.Abp.SettingManagement.HttpApi`:** đã nạp (16/10/2026) nhưng ABP 10.7 **chỉ**
/// phơi `setting-management/emailing` + `setting-management/timezone` (**đo bằng swagger thật**) — **không** có
/// route cho setting nghiệp vụ ⇒ **không thể** thay thế lớp này.
/// </summary>
[Authorize(eCommerceConsts.Permissions.ChannelLimits.Default)]
public class ChinhSachHanMucAppService : ApplicationService, IChinhSachHanMucAppService
{
    private readonly IRepository<InvChannelLimit, Guid> _hanMucRepository;
    private readonly IRepository<Warehouse, Guid> _khoRepository;
    private readonly IRepository<Channel, Guid> _kenhRepository;
    private readonly IRepository<ReconRun, Guid> _luotDoiSoatRepository;
    private readonly ISettingManager _settingManager;
    private readonly ISettingProvider _settingProvider;

    public ChinhSachHanMucAppService(
        IRepository<InvChannelLimit, Guid> hanMucRepository,
        IRepository<Warehouse, Guid> khoRepository,
        IRepository<Channel, Guid> kenhRepository,
        IRepository<ReconRun, Guid> luotDoiSoatRepository,
        ISettingManager settingManager,
        ISettingProvider settingProvider)
    {
        _hanMucRepository = hanMucRepository;
        _khoRepository = khoRepository;
        _kenhRepository = kenhRepository;
        _luotDoiSoatRepository = luotDoiSoatRepository;
        _settingManager = settingManager;
        _settingProvider = settingProvider;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ① CHÍNH SÁCH
    // ══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<ChinhSachHanMucDto> GetChinhSachAsync()
    {
        var donVi = ChinhSach.DocDonViHanMuc(await _settingProvider.GetOrNullAsync(TenChinhSach.HanMuc_DonVi), out var donViOk);
        var hanhDong = ChinhSach.DocHanhDongHanMuc(await _settingProvider.GetOrNullAsync(TenChinhSach.HanMuc_HanhDong), out var hanhDongOk);
        var phamVi = ChinhSach.DocPhamViHanMuc(await _settingProvider.GetOrNullAsync(TenChinhSach.HanMuc_PhamVi), out var phamViOk);
        var giaTriMacDinh = ChinhSach.DocSo(await _settingProvider.GetOrNullAsync(TenChinhSach.HanMuc_GiaTriMacDinh), 0m, out var giaTriOk);

        var ngDonVi = ChinhSach.DocDonViNguong(await _settingProvider.GetOrNullAsync(TenChinhSach.NguongDoiSoat_DonVi), out var ngDonViOk);
        var ngGiaTri = ChinhSach.DocSo(await _settingProvider.GetOrNullAsync(TenChinhSach.NguongDoiSoat_GiaTri), 0.5m, out var ngGiaTriOk);
        var ngHanhDong = ChinhSach.DocHanhDongLech(await _settingProvider.GetOrNullAsync(TenChinhSach.NguongDoiSoat_HanhDong), out var ngHanhDongOk);

        return new ChinhSachHanMucDto
        {
            DonVi = donVi.ToString(),
            HanhDong = hanhDong.ToString(),
            PhamVi = phamVi.ToString(),
            GiaTriMacDinh = giaTriMacDinh,
            NguongDoiSoatDonVi = ngDonVi.ToString(),
            NguongDoiSoatGiaTri = ngGiaTri,
            NguongDoiSoatHanhDong = ngHanhDong.ToString(),
            // 🚨 Trả ra **tên setting nào đang bị giá trị lạ** để màn hình admin nói thẳng, thay vì để người
            // vận hành **tin** cấu hình có hiệu lực (hàm đọc im lặng dùng mặc định an toàn khi gặp giá trị lạ).
            SettingKhongHopLe =
            [
                .. new[]
                {
                    donViOk ? null : TenChinhSach.HanMuc_DonVi,
                    hanhDongOk ? null : TenChinhSach.HanMuc_HanhDong,
                    phamViOk ? null : TenChinhSach.HanMuc_PhamVi,
                    giaTriOk ? null : TenChinhSach.HanMuc_GiaTriMacDinh,
                    ngDonViOk ? null : TenChinhSach.NguongDoiSoat_DonVi,
                    ngGiaTriOk ? null : TenChinhSach.NguongDoiSoat_GiaTri,
                    ngHanhDongOk ? null : TenChinhSach.NguongDoiSoat_HanhDong
                }
                .Where(x => x is not null)
                .Select(x => x!)
            ],
            GhiChu = GhichuNo()
        };
    }

    /// <summary>
    /// 🚨 **GHI QUA `ISettingManager`** — đây là điểm mấu chốt: nó **tự xoá cache cả 2 tầng**.
    /// 🔒 **Validate TRƯỚC khi ghi**: giá trị lạ mà lọt vào DB thì hàm đọc sẽ **im lặng dùng mặc định**
    /// (đúng thiết kế "dữ liệu cấu hình cũng là dữ liệu vào") ⇒ người vận hành **tin** cấu hình có hiệu lực.
    /// Vì vậy ở **đường GHI** ta **NÉM** (`eCommerce:Settings:0001`) thay vì chấp nhận rồi cảnh báo.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.ChannelLimits.Manage)]
    public async Task<ChinhSachHanMucDto> DoiChinhSachAsync(DoiChinhSachInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var thayDoi = new List<string>();

        if (!string.IsNullOrWhiteSpace(input.DonViHanMuc))
        {
            ChinhSach.DocDonViHanMuc(input.DonViHanMuc, out var ok);
            Kiem(input.DonViHanMuc, ok, TenChinhSach.HanMuc_DonVi, TenChinhSach.DonViHopLe);
            await _settingManager.SetGlobalAsync(TenChinhSach.HanMuc_DonVi, input.DonViHanMuc.Trim());
            thayDoi.Add($"{TenChinhSach.HanMuc_DonVi}={input.DonViHanMuc.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(input.HanhDongHanMuc))
        {
            ChinhSach.DocHanhDongHanMuc(input.HanhDongHanMuc, out var ok);
            Kiem(input.HanhDongHanMuc, ok, TenChinhSach.HanMuc_HanhDong, TenChinhSach.HanhDongHanMucHopLe);
            await _settingManager.SetGlobalAsync(TenChinhSach.HanMuc_HanhDong, input.HanhDongHanMuc.Trim());
            thayDoi.Add($"{TenChinhSach.HanMuc_HanhDong}={input.HanhDongHanMuc.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(input.PhamViHanMuc))
        {
            ChinhSach.DocPhamViHanMuc(input.PhamViHanMuc, out var ok);
            Kiem(input.PhamViHanMuc, ok, TenChinhSach.HanMuc_PhamVi, TenChinhSach.PhamViHanMucHopLe);
            await _settingManager.SetGlobalAsync(TenChinhSach.HanMuc_PhamVi, input.PhamViHanMuc.Trim());
            thayDoi.Add($"{TenChinhSach.HanMuc_PhamVi}={input.PhamViHanMuc.Trim()}");
        }

        if (input.GiaTriMacDinh is { } giaTriMacDinh)
        {
            // 🔒 Ghi bằng **InvariantCulture** — nếu ghi theo culture máy (`vi-VN` dùng dấu phẩy thập phân)
            // thì hàm đọc (chỉ nhận dấu CHẤM) sẽ coi là **giá trị lạ** ⇒ âm thầm dùng mặc định (bài học
            // `ChinhSach.DocSo`: `"0,5"` từng bị đọc thành **5** — sai 10 lần mà không có lỗi nào).
            await _settingManager.SetGlobalAsync(
                TenChinhSach.HanMuc_GiaTriMacDinh,
                giaTriMacDinh.ToString(System.Globalization.CultureInfo.InvariantCulture));
            thayDoi.Add($"{TenChinhSach.HanMuc_GiaTriMacDinh}={giaTriMacDinh}");
        }

        if (!string.IsNullOrWhiteSpace(input.NguongDoiSoatDonVi))
        {
            ChinhSach.DocDonViNguong(input.NguongDoiSoatDonVi, out var ok);
            Kiem(input.NguongDoiSoatDonVi, ok, TenChinhSach.NguongDoiSoat_DonVi, TenChinhSach.DonViHopLe);
            await _settingManager.SetGlobalAsync(TenChinhSach.NguongDoiSoat_DonVi, input.NguongDoiSoatDonVi.Trim());
            thayDoi.Add($"{TenChinhSach.NguongDoiSoat_DonVi}={input.NguongDoiSoatDonVi.Trim()}");
        }

        if (input.NguongDoiSoatGiaTri is { } ngGiaTri)
        {
            await _settingManager.SetGlobalAsync(
                TenChinhSach.NguongDoiSoat_GiaTri,
                ngGiaTri.ToString(System.Globalization.CultureInfo.InvariantCulture));
            thayDoi.Add($"{TenChinhSach.NguongDoiSoat_GiaTri}={ngGiaTri}");
        }

        if (!string.IsNullOrWhiteSpace(input.NguongDoiSoatHanhDong))
        {
            ChinhSach.DocHanhDongLech(input.NguongDoiSoatHanhDong, out var ok);
            Kiem(input.NguongDoiSoatHanhDong, ok, TenChinhSach.NguongDoiSoat_HanhDong, TenChinhSach.HanhDongLechHopLe);
            await _settingManager.SetGlobalAsync(TenChinhSach.NguongDoiSoat_HanhDong, input.NguongDoiSoatHanhDong.Trim());
            thayDoi.Add($"{TenChinhSach.NguongDoiSoat_HanhDong}={input.NguongDoiSoatHanhDong.Trim()}");
        }

        if (thayDoi.Count == 0)
        {
            // 📌 Không phải lỗi: *"gọi đổi mà không gửi trường nào"* là ca hợp lệ (màn hình chỉ gửi ô đã sửa).
            // Nhưng **phải ghi log** để không ai tưởng "đã đổi thành công" khi thực tế **không có gì đổi**.
            Logger.LogWarning("ĐỔI CHÍNH SÁCH: KHÔNG có trường nào được gửi ⇒ KHÔNG thay đổi gì (không phải lỗi). [SETTING-KHONG-DOI-GI]");
        }
        else
        {
            Logger.LogInformation("ĐỔI CHÍNH SÁCH (qua `ISettingManager` ⇒ cache 2 tầng đã tự xoá): {ThayDoi}. [SETTING-DA-DOI]",
                string.Join(" · ", thayDoi));
        }

        return await GetChinhSachAsync();
    }

    /// <summary>
    /// 🔒 Chặn giá trị **lạ** ngay ở đường GHI (xem chú thích ở <see cref="DoiChinhSachAsync"/>).
    /// `BusinessException` ⇒ ABP trả **`403`** cùng quy ước mã lỗi toàn hệ thống (không phải `500`).
    /// </summary>
    private static void Kiem(string giaTri, bool hopLe, string tenSetting, string[] giaTriHopLe)
    {
        if (hopLe)
        {
            return;
        }

        throw new BusinessException(eCommerceConsts.ErrorCodes.GiaTriSettingKhongHopLe)
            .WithData("setting", tenSetting)
            .WithData("giaTriNhan", giaTri)
            .WithData("giaTriHopLe", string.Join(" | ", giaTriHopLe))
            .WithData("lyDo", $"`{tenSetting}` chỉ nhận: {string.Join(" | ", giaTriHopLe)}.");
    }

    /// <summary>
    /// 📌 **NỢ ĐANG MỞ, trả ra cho UI** — để màn hình admin **không hứa quá** những gì hệ thống làm được.
    /// 🚨 Đây là phần dễ bị bỏ nhất khi làm API: API trả về *"đã lưu"* trong khi nghiệp vụ còn giới hạn
    /// ⇒ người vận hành **tin** một điều không đúng (đúng loại lỗi `inv-003` §16.8).
    /// </summary>
    private static List<string> GhichuNo() =>
    [
        "`Inv.ChannelLimit.Unit` (Percent/Absolute) là setting DÙNG CHUNG — chưa đặt được riêng từng kênh.",
        "`Percent` đang tính trên tồn khả dụng của TỪNG `(kho, SKU)`, không phải tổng của cả kênh.",
        "Hạn mức là TRẦN đọc lúc chạy, KHÔNG trừ dần — không phải \"ngân sách đã cấp\"."
    ];

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ② HẠN MỨC RIÊNG TỪNG KÊNH
    // ══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<HanMucKenhDto>> GetDanhSachAsync(string? channelCode = null)
    {
        var truyVan = await _hanMucRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(channelCode))
        {
            // 🔑 Chuẩn hoá **cùng một hàm** với lúc ghi và lúc giữ chỗ (một chỗ duy nhất — `InvChannelLimit`).
            var kenh = InvChannelLimit.ChuẩnHoáKenh(channelCode);
            truyVan = truyVan.Where(x => x.ChannelCode == kenh);
        }

        var cacDong = await AsyncExecuter.ToListAsync(truyVan.OrderBy(x => x.ChannelCode).ThenBy(x => x.WarehouseId));

        // 📌 Nạp tên kho theo **một** truy vấn (không N+1) để màn hình không hiện GUID "trần".
        var cacKhoId = cacDong.Where(x => x.WarehouseId is not null).Select(x => x.WarehouseId!.Value).Distinct().ToList();
        var tenKho = cacKhoId.Count == 0
            ? []
            : (await _khoRepository.GetListAsync(x => cacKhoId.Contains(x.Id))).ToDictionary(x => x.Id, x => (x.Code, x.Name));

        return cacDong.Select(x =>
        {
            (string Code, string Name)? kho = null;
            if (x.WarehouseId is { } id && tenKho.TryGetValue(id, out var t))
            {
                kho = t;
            }

            return new HanMucKenhDto
            {
                Id = x.Id,
                ChannelCode = x.ChannelCode,
                WarehouseId = x.WarehouseId,
                WarehouseCode = kho?.Code,
                WarehouseName = kho?.Name,
                GiaTri = x.GiaTri,
                HanhDong = x.HanhDong.ToString(),
                // 🔑 `null` = KE THUA setting chung. DTO tra `string?` (khong phai enum) vi
                // `HanMucKenhDto` nam o `Application.Contracts` — noi KHONG tham chieu module `Domain`.
                DonVi = x.DonVi?.ToString(),
                CreationTime = x.CreationTime,
                LastModificationTime = x.LastModificationTime
            };
        }).ToList();
    }

    public async Task<List<KenhKhaDungDto>> GetKenhKhaDungAsync()
    {
        var cacKenh = await _kenhRepository.GetListAsync();
        var hanMuc = await _hanMucRepository.GetListAsync();

        var demTheoKenh = hanMuc
            .GroupBy(x => x.ChannelCode)
            .ToDictionary(g => g.Key, g => g.Count());

        return cacKenh
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => new KenhKhaDungDto
            {
                Code = x.Code,
                Name = x.Name,
                HoldsInventory = x.HoldsInventory,
                IsActive = x.IsActive,
                SoDongHanMuc = demTheoKenh.TryGetValue(InvChannelLimit.ChuẩnHoáKenh(x.Code), out var n) ? n : 0
            })
            .ToList();
    }

    /// <summary>
    /// **UPSERT** theo khoá nghiệp vụ `(kênh, kho)` — 📌 cố ý **không** để người dùng tự chọn `Id`:
    /// nếu cho thêm dòng mới trùng khoá thì DB sẽ chặn (`0045`) và client nhận lỗi **đáng lẽ không cần**.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.ChannelLimits.Manage)]
    public async Task<HanMucKenhDto> DatHanMucAsync(DatHanMucKenhInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var kenh = InvChannelLimit.ChuẩnHoáKenh(input.ChannelCode);

        // 🚨 CHỐNG "TẮT IM LẶNG": hạn mức gắn mã kênh **không có thật** sẽ không bao giờ khớp lúc giữ chỗ.
        var kenhThat = await _kenhRepository.FirstOrDefaultAsync(x => x.Code == kenh);

        if (kenhThat is null)
        {
            var coThat = (await _kenhRepository.GetListAsync()).Select(x => x.Code).OrderBy(x => x, StringComparer.Ordinal);
            throw new BusinessException(InventoryErrorCodes.HanMucKenhKhongTonTai)
                .WithData("channelCode", kenh)
                .WithData("kenhCoThat", string.Join(" | ", coThat))
                .WithData("lyDo", $"Kênh '{kenh}' KHÔNG có trong danh mục kênh ⇒ hạn mức sẽ không bao giờ được áp. Thêm kênh trước.");
        }

        if (input.WarehouseId is { } khoId)
        {
            var kho = await _khoRepository.FindAsync(khoId)
                ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                    .WithData("warehouseId", khoId)
                    .WithData("lyDo", "Kho KHÔNG tồn tại ⇒ hạn mức theo kho sẽ không bao giờ khớp.");
            _ = kho;
        }

        var hanhDong = HanhDongHanMuc.Block;   // 🔒 mặc định FAIL-CLOSED (không bán quá hạn mức)
        if (!string.IsNullOrWhiteSpace(input.HanhDong))
        {
            hanhDong = ChinhSach.DocHanhDongHanMuc(input.HanhDong, out var ok);
            if (!ok)
            {
                Kiem(input.HanhDong, ok, nameof(input.HanhDong), TenChinhSach.HanhDongHanMucHopLe);
            }
        }

        // 🚨 `INV-003` muc 5 viec 2 — **DON VI CUA RIENG DONG**. `null`/rong = **khong override**
        // (dung setting chung). ⚠️ Chuoi SAI => NEM (khong im lang bo qua): neu im lang thi nguoi dung
        // tuong da doi don vi, trong khi he thong van dung don vi cu => HAN MUC SAI IM LANG.
        DonViHanMuc? donVi = null;

        if (!string.IsNullOrWhiteSpace(input.DonVi))
        {
            donVi = ChinhSach.DocDonViHanMuc(input.DonVi, out var donViOk);

            if (!donViOk)
            {
                Kiem(input.DonVi, donViOk, nameof(input.DonVi), TenChinhSach.DonViHopLe);
            }
        }

        var hienCo = await _hanMucRepository.FirstOrDefaultAsync(x => x.ChannelCode == kenh && x.WarehouseId == input.WarehouseId);

        if (hienCo is null)
        {
            hienCo = new InvChannelLimit(GuidGenerator.Create(), kenh, input.GiaTri, input.WarehouseId, hanhDong, donVi);
            await _hanMucRepository.InsertAsync(hienCo, autoSave: true);
            Logger.LogInformation("HẠN MỨC KÊNH: THÊM kênh {Kenh} kho {Kho} = {GiaTri} ({HanhDong}). [HANMUC-THEM]",
                kenh, input.WarehouseId?.ToString() ?? "(cả kênh)", input.GiaTri, hanhDong);
        }
        else
        {
            hienCo.DatHanMuc(input.GiaTri, hanhDong, donVi);
            await _hanMucRepository.UpdateAsync(hienCo, autoSave: true);
            Logger.LogInformation("HẠN MỨC KÊNH: SỬA kênh {Kenh} kho {Kho} = {GiaTri} ({HanhDong}). [HANMUC-SUA]",
                kenh, input.WarehouseId?.ToString() ?? "(cả kênh)", input.GiaTri, hanhDong);
        }

        // 🚨 **CẢNH BÁO NGAY TẠI CHỖ GHI** (không đợi tới lúc giữ chỗ): nếu `PhamVi = Channel` mà người dùng
        // vừa lưu một dòng **theo kho**, thì dòng đó **sẽ bị bỏ qua** lúc chạy (xem `HanMucKenh.ChonHanMuc`).
        // 📌 Ghi ở đây để người vận hành biết **trong cùng một lần bấm**, thay vì tin rồi mới phát hiện.
        var phamVi = ChinhSach.DocPhamViHanMuc(await _settingProvider.GetOrNullAsync(TenChinhSach.HanMuc_PhamVi), out _);
        if (input.WarehouseId is not null && phamVi == PhamViHanMuc.Channel)
        {
            Logger.LogWarning(
                "HẠN MỨC KÊNH: vừa lưu dòng THEO KHO cho kênh {Kenh} nhưng `{Setting}` = {PhamVi} ⇒ " +
                "dòng này SẼ BỊ BỎ QUA lúc giữ chỗ. Đổi setting sang `ChannelWarehouse` nếu muốn dùng.",
                kenh, TenChinhSach.HanMuc_PhamVi, phamVi);
        }

        var ds = await GetDanhSachAsync(kenh);
        return ds.First(x => x.Id == hienCo.Id);
    }

    [Authorize(eCommerceConsts.Permissions.ChannelLimits.Manage)]
    public async Task XoaHanMucAsync(Guid id)
    {
        var dong = await _hanMucRepository.FindAsync(id)
            ?? throw new BusinessException(InventoryErrorCodes.HanMucKenhKhongTonTai)
                .WithData("id", id)
                .WithData("lyDo", "Không tìm thấy dòng hạn mức cần xoá.");

        // 🔒 Xoá **MỀM** (`ISoftDelete`): lịch sử chặn đơn (`0042`) phải còn đối chiếu được
        // ⇒ người vận hành trả lời được *"vì sao tháng trước đơn này bị chặn"*.
        await _hanMucRepository.DeleteAsync(dong, autoSave: true);
        Logger.LogWarning("HẠN MỨC KÊNH: XOÁ (mềm) kênh {Kenh} kho {Kho} — lịch sử chặn đơn cũ vẫn tra được. [HANMUC-XOA-MEM]",
            dong.ChannelCode, dong.WarehouseId?.ToString() ?? "(cả kênh)");
    }
}
