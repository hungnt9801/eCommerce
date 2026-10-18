using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Address;

/// <summary>
/// 🚨 **HIỆN THỰC `IAddressDirectory`** — cổng nội bộ để module khác tạo/lấy/đọc **snapshot** địa chỉ
/// (`ADDR-004` §6.1).
///
/// 🔒 **MỘT chỗ duy nhất** biết cách *"tạo hoặc lấy theo nội dung"* (`ADR-04`). Nếu app service và
/// directory mỗi bên tự viết một bản thì sẽ có **hai** định nghĩa *"cùng nội dung"* ⇒ khử trùng lặp
/// hỏng **âm thầm** (đúng họ lỗi mà cả dự án đang chống).
///
/// 🔑 **VÌ SAO `ON CONFLICT DO NOTHING` (không chỉ `SELECT` rồi `INSERT`):** hai luồng cùng tạo một địa
/// chỉ ⇒ cả hai `SELECT` đều *"chưa có"* ⇒ bản thứ hai đụng unique index `(TenantId, ContentHash)`.
/// Trong PostgreSQL, **một lỗi làm hỏng CẢ transaction** ⇒ không thể *"bắt lỗi rồi tra lại"* trong cùng
/// UoW. `ON CONFLICT DO NOTHING` + `SELECT` lại là cách duy nhất **không** phá transaction
/// (`ADDR-004` §8.2 #1: *"khoá phân tán để che một ràng buộc DB còn thiếu là phản mẫu"*).
/// 📌 Chỉ dùng SQL thô cho **đúng một** lệnh INSERT này; mọi đường đọc vẫn đi qua EF.
/// </summary>
public class AddressDirectory : IAddressDirectory, ITransientDependency
{
    private readonly IRepository<Address, Guid> _diaChiRepository;
    private readonly IRepository<AdministrativeUnit, Guid> _donViRepository;
    private readonly ICurrentTenant _tenantHienTai;
    private readonly Volo.Abp.Users.ICurrentUser _nguoiDung;

    public AddressDirectory(
        IRepository<Address, Guid> diaChiRepository,
        IRepository<AdministrativeUnit, Guid> donViRepository,
        ICurrentTenant tenantHienTai,
        Volo.Abp.Users.ICurrentUser nguoiDung)
    {
        _diaChiRepository = diaChiRepository;
        _donViRepository = donViRepository;
        _tenantHienTai = tenantHienTai;
        _nguoiDung = nguoiDung;
    }

    public async Task<DiaChiSnapshotDto> TaoHoacLayAsync(TaoDiaChiInput input, CancellationToken ct = default)
        => (await TaoHoacLayVaBietAsync(input, ct)).DiaChi;

    /// <summary>
    /// Như <see cref="TaoHoacLayAsync"/> nhưng **cho biết có vừa TẠO hay không** — cần để phát
    /// `AddressChangedEto` **một lần** cho mỗi nội dung mới (không phát lại mỗi lần gọi).
    /// 📌 Nằm trên lớp hiện thực (không ở hợp đồng) vì đây là chi tiết của miền `Address`.
    /// </summary>
    public async Task<(DiaChiSnapshotDto DiaChi, bool VuaTao)> TaoHoacLayVaBietAsync(
        TaoDiaChiInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var daChuanHoa = !string.IsNullOrWhiteSpace(input.TinhCode) || !string.IsNullOrWhiteSpace(input.XaCode);

        Address? diaChi;
        var vuaTao = false;

        if (daChuanHoa)
        {
            // ── Đường CHUẨN HOÁ (fail-closed): phải tra được tỉnh + xã và xã phải thuộc tỉnh ──────────
            var daval = await LayDonViLienQuanAsync(input.TinhCode, input.XaCode, input.HuyenCode);

            var ketQua = input.SchemaVersion == 1
                ? AdministrativeUnitResolver.KiemTraBaCap(daval, 1, input.TinhCode!, input.HuyenCode!, input.XaCode!)
                : AdministrativeUnitResolver.KiemTraHaiCap(daval, input.SchemaVersion, input.TinhCode!, input.XaCode!);

            if (!ketQua.HopLe)
            {
                // 🔒 FAIL-CLOSED: **0 dòng** ghi. Mã lỗi đến từ resolver (0001/0003/0004/0005/0006).
                throw new Volo.Abp.BusinessException(ketQua.ErrorCode!)
                    .WithData("tinhCode", input.TinhCode)
                    .WithData("xaCode", input.XaCode)
                    .WithData("lyDo", ketQua.ThongDiep);
            }

            var hash = DiaChiCanonical.TinhHashDaChuanHoa(
                input.SchemaVersion, input.TinhCode!, input.HuyenCode, input.XaCode!, input.SoNhaDuong);

            diaChi = await TimTheoHashAsync(hash, ct);

            if (diaChi is null)
            {
                var phienBan = AdministrativeSchemaVersion.TheoSo(input.SchemaVersion);

                var moi = Address.TaoDaChuanHoa(
                    phienBan,
                    input.TinhCode!,
                    input.HuyenCode,
                    input.XaCode!,
                    input.SoNhaDuong,
                    DungDiaChiDayDu(input.SoNhaDuong, ketQua.Xa?.Name, ketQua.Huyen?.Name, ketQua.Tinh?.Name),
                    ChuyenNguonTao(input.NguonTao),
                    DateTime.UtcNow);

                (diaChi, vuaTao) = await ChenBoQuaTrungAsync(moi, hash, ct);
            }
        }
        else
        {
            // ── Đường địa chỉ THÔ: chỉ có chữ tự do (webhook sàn / khách nhập tay) ─────────────────────
            if (string.IsNullOrWhiteSpace(input.DiaChiDayDuTho))
            {
                throw new Volo.Abp.BusinessException(AddressErrorCodes.ThieuNoiDung)
                    .WithData("lyDo", "Cần `tinhCode`+`xaCode` (địa chỉ chuẩn hoá) hoặc `diaChiDayDuTho` (địa chỉ thô).");
            }

            var hashTho = DiaChiCanonical.TinhHashTho(input.DiaChiDayDuTho);

            diaChi = await TimTheoHashAsync(hashTho, ct);

            if (diaChi is null)
            {
                var moi = Address.TaoTuDiaChiTho(
                    input.DiaChiDayDuTho, ChuyenNguonTao(input.NguonTao), tenantId: _tenantHienTai.Id);

                (diaChi, vuaTao) = await ChenBoQuaTrungAsync(moi, hashTho, ct);
            }
        }

        return (await ChuyenSnapshotAsync(diaChi, ct), vuaTao);
    }

    public async Task<DiaChiSnapshotDto?> TimTheoIdAsync(Guid addressId, CancellationToken ct = default)
    {
        var diaChi = await _diaChiRepository.FindAsync(addressId, cancellationToken: ct);

        return diaChi is null ? null : await ChuyenSnapshotAsync(diaChi, ct);
    }

    public async Task<IReadOnlyList<DiaChiSnapshotDto>> TimNhieuTheoIdAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        var danhSach = await _diaChiRepository.GetListAsync(x => ids.Contains(x.Id), cancellationToken: ct);

        var ketQua = new List<DiaChiSnapshotDto>(danhSach.Count);

        foreach (var diaChi in danhSach)
        {
            ketQua.Add(await ChuyenSnapshotAsync(diaChi, ct));
        }

        return ketQua;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // NỘI BỘ
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tìm theo `ContentHash`. 🔒 **KHÔNG** lọc `TenantId` bằng tay: `IMultiTenant` + bộ lọc của ABP đã
    /// giới hạn theo tenant hiện tại (lọc tay ở đây sẽ thành **nguồn sự thật thứ hai**).
    /// </summary>
    private Task<Address?> TimTheoHashAsync(string hash, CancellationToken ct)
        => _diaChiRepository.FirstOrDefaultAsync(x => x.ContentHash == hash, cancellationToken: ct);

    /// <summary>
    /// 🚨 **INSERT + `ON CONFLICT DO NOTHING`** rồi `SELECT` lại (xem chú thích ở đầu lớp).
    /// 🔑 Trả về **bản ghi ĐANG có trong DB** — có thể là bản của luồng khác vừa thắng, đó chính là ý định
    /// của `ADR-04` (*"một nội dung ⇒ một bản ghi"*), **không** phải lỗi.
    /// </summary>
    private async Task<(Address DiaChi, bool VuaTao)> ChenBoQuaTrungAsync(Address moi, string hash, CancellationToken ct)
    {
        var db = await _diaChiRepository.GetDbContextAsync();

        // 📌 `ExtraProperties` của ABP là JSON — truyền như **tham số** (không nhúng `{}` vào chuỗi SQL raw:
        //    trong raw string có `$` thì ngoặc nhọn là cú pháp, và một lần nữa lại là bẫy "đoán chuỗi").
        var extraProperties = "{}";
        var concurrencyStamp = Guid.NewGuid().ToString("N");
        var taoLucUtc = DateTime.UtcNow;
        var nguoiTao = _nguoiDung.Id;

        // 📌 Tham số hoá bằng `ExecuteSqlInterpolatedAsync` (KHÔNG nối chuỗi) ⇒ SQL injection không có cửa.
        // 📌 Cột nào cũng tường minh: bảng do migration tạo, và `NOT NULL` là hợp đồng.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "AddrAddresses"
                ("Id", "TenantId", "TrangThai", "NguonTao", "SchemaVersion", "TinhCode", "HuyenCode", "XaCode",
                 "SoNhaDuong", "DiaChiDayDu", "ContentHash", "NormalizedAtUtc", "XacMinhLucUtc",
                 "ExtraProperties", "ConcurrencyStamp", "CreationTime", "CreatorId", "IsDeleted")
            VALUES
                ({moi.Id}, {_tenantHienTai.Id}, {(int)moi.TrangThai}, {(int)moi.NguonTao}, {moi.SchemaVersion},
                 {moi.TinhCode}, {moi.HuyenCode}, {moi.XaCode},
                 {moi.SoNhaDuong}, {moi.DiaChiDayDu}, {hash}, {moi.NormalizedAtUtc}, NULL,
                 {extraProperties}, {concurrencyStamp}, {taoLucUtc}, {nguoiTao}, FALSE)
            ON CONFLICT DO NOTHING;
            """,
            ct);

        var daCo = await TimTheoHashAsync(hash, ct);

        // ⚠️ Không thể `null` ở đây: hoặc INSERT thành công, hoặc đã có bản ghi cùng hash. Nếu `null` thì
        // nghĩa là bộ lọc tenant che mất dòng vừa ghi ⇒ lỗi **thật** (không phải ca nghiệp vụ) ⇒ ném rõ ràng.
        var ketQua = daCo ?? throw new Volo.Abp.BusinessException(AddressErrorCodes.KhongXacDinhDuocDonVi)
            .WithData("lyDo", "Vừa ghi địa chỉ nhưng không đọc lại được (kiểm bộ lọc tenant).")
            .WithData("contentHash8", hash[..8]);

        // 🔑 `VuaTao` = bản ghi đọc lại **chính là** bản ta vừa dựng ⇒ INSERT đã thắng (không phải luồng khác).
        return (ketQua, ketQua.Id == moi.Id);
    }

    private async Task<List<AdministrativeUnit>> LayDonViLienQuanAsync(string? tinh, string? xa, string? huyen)
    {
        var ma = new[] { tinh, xa, huyen }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ma.Count == 0)
        {
            return [];
        }

        return await _donViRepository.GetListAsync(x => ma.Contains(x.Code));
    }

    private async Task<DiaChiSnapshotDto> ChuyenSnapshotAsync(Address diaChi, CancellationToken ct)
    {
        var ma = new[] { diaChi.TinhCode, diaChi.HuyenCode, diaChi.XaCode }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var donVi = ma.Count == 0
            ? []
            : await _donViRepository.GetListAsync(
                x => ma.Contains(x.Code) && x.SchemaVersion == diaChi.SchemaVersion, cancellationToken: ct);

        string? Ten(string? code) => code is null
            ? null
            : donVi.FirstOrDefault(x => x.Code == code)?.Name;

        return new DiaChiSnapshotDto(
            AddressId: diaChi.Id,
            TrangThai: diaChi.TrangThai.ToString(),
            SchemaVersion: diaChi.SchemaVersion,
            TinhCode: diaChi.TinhCode,
            TenTinh: Ten(diaChi.TinhCode),
            HuyenCode: diaChi.HuyenCode,
            TenHuyen: Ten(diaChi.HuyenCode),
            XaCode: diaChi.XaCode,
            TenXa: Ten(diaChi.XaCode),
            SoNhaDuong: diaChi.SoNhaDuong,
            DiaChiDayDu: diaChi.DiaChiDayDu,
            NormalizedAtUtc: diaChi.NormalizedAtUtc,
            // 🔒 `false` ⇔ địa chỉ THÔ ⇒ KHÔNG dùng được cho vận đơn/hoá đơn (`ADDR-004` §12).
            CoDungDuoc: diaChi.TrangThai != TrangThaiDiaChi.ChuaChuanHoa);
    }

    /// <summary>
    /// Dựng **snapshot chữ** theo đúng thứ tự `SoNhaDuong, Xa, Huyen, Tinh` (`ADDR-004` §5.2).
    /// 🔒 Phần rỗng **bị bỏ** (không để *", , "*) vì đây là chuỗi **in ra chứng từ**.
    /// </summary>
    internal static string DungDiaChiDayDu(string? soNha, string? tenXa, string? tenHuyen, string? tenTinh)
    {
        var phan = new[] { soNha?.Trim(), tenXa, tenHuyen, tenTinh }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim());

        var noi = string.Join(", ", phan);

        if (string.IsNullOrWhiteSpace(noi))
        {
            throw new Volo.Abp.BusinessException(AddressErrorCodes.ThieuNoiDung)
                .WithData("lyDo", "Không dựng được địa chỉ đầy đủ: thiếu cả số nhà và tên đơn vị hành chính.");
        }

        return noi;
    }

    internal static NguonTaoDiaChi ChuyenNguonTao(string? nguon)
    {
        if (string.IsNullOrWhiteSpace(nguon))
        {
            return NguonTaoDiaChi.Web;
        }

        if (!Enum.TryParse<NguonTaoDiaChi>(nguon.Trim(), ignoreCase: true, out var ketQua))
        {
            throw new Volo.Abp.BusinessException(AddressErrorCodes.ThieuNoiDung)
                .WithData("truong", nameof(nguon))
                .WithData("giaTri", nguon)
                .WithData("lyDo", "Nguồn tạo phải là: Web · San · NhanVien · Import.");
        }

        return ketQua;
    }
}
