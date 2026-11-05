using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **HIỆN THỰC `IDataScopeResolver`** — đọc phạm vi dữ liệu **hiệu lực** của người đang đăng nhập (`WP-07`).
///
/// **LUẬT GỘP (đã chốt ở `D-46`) — chỗ dễ sai nhất, nên phải là một dòng `Min`:**
/// <list type="number">
///   <item>Không có cấu hình nào cho các vai trò của người dùng ⇒ **`All`**. 🔑 Đây là **tương thích ngược**:
///     hệ thống đang chạy 1 tenant/1 admin, thêm bảng phạm vi mà **mặc định chặn** sẽ làm mọi màn hình
///     **rỗng** sau khi nâng cấp — và không có lỗi nào để lần.</item>
///   <item>Nhiều vai trò ⇒ lấy phạm vi **rộng nhất** (`Min` theo số enum: `All = 0`). 📌 Lý do: người có
///     vai trò `admin` (`All`) **không** được bị `sales` (`Own`) **làm hẹp** quyền — nếu không, gán thêm
///     một vai trò phụ là **âm thầm giảm** quyền của họ. Và ngược lại, phép `Min` **không** nới quyền cho
///     người chỉ có `Own` (họ không có role `All`).</item>
///   <item>`Own` ⇒ lọc `CreatorId == Id` người dùng; `Store` ⇒ lọc theo **kho của cửa hàng** mà nhân viên
///     thuộc. 📌 Nếu nhân viên **chưa thuộc cửa hàng**/cửa hàng **chưa gắn kho** ⇒ trả `Store` **kèm
///     `KhoCuaHangId = null`** để tầng gọi lọc ra **rỗng** — ⚠️ **KHÔNG** tự nới thành `All`: nới là biến
///     một cấu hình thiếu thành **lộ toàn bộ dữ liệu**.</item>
/// </list>
///
/// 🔒 **LOG Ở MỨC `Debug`** cho ca thường (rất nóng: mỗi lần mở DS), `Warning` cho ca **cấu hình thiếu**
/// (nhân viên chưa gắn cửa hàng) — để người vận hành tìm được lý do *"sao tôi không thấy gì"*.
/// </summary>
public class DataScopeResolver : IDataScopeResolver, ITransientDependency
{
    private readonly IRepository<RoleDataScope, Guid> _phamVi;
    private readonly IRepository<Staff, Guid> _nhanVien;
    private readonly IRepository<Store, Guid> _cuaHang;
    private readonly ICurrentUser _nguoiDung;
    private readonly ILogger<DataScopeResolver> _logger;

    public DataScopeResolver(
        IRepository<RoleDataScope, Guid> phamVi,
        IRepository<Staff, Guid> nhanVien,
        IRepository<Store, Guid> cuaHang,
        ICurrentUser nguoiDung,
        ILogger<DataScopeResolver> logger)
    {
        _phamVi = phamVi;
        _nhanVien = nhanVien;
        _cuaHang = cuaHang;
        _nguoiDung = nguoiDung;
        _logger = logger;
    }

    public async Task<PhamViHienTai> LayAsync()
    {
        if (_nguoiDung.Id is not { } nguoiDungId)
        {
            // 📌 Job nền / tiến trình không có principal: **KHÔNG** chặn (báo cáo/job phải thấy dữ liệu),
            // nhưng nói rõ lý do để log không mơ hồ.
            return new PhamViHienTai(PhamViDuLieu.All, null, null, null, "không có người dùng đăng nhập (job nền)");
        }

        var nhanVien = await _nhanVien.FirstOrDefaultAsync(x => x.IdentityUserId == nguoiDungId && x.IsActive);

        // 🔑 Vai trò lấy từ **hai** nguồn (và gộp): claim của phiên đăng nhập + `Staff.RoleName` (vai trò
        // chính do người vận hành khai). 📌 Chỉ dùng claim thì nhân viên vừa được gán vai trò **phải đăng
        // nhập lại** mới thấy phạm vi mới; chỉ dùng `Staff` thì tài khoản admin không có dòng `Staff` nào
        // sẽ rơi về `All` — sai.
        var vaiTro = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in _nguoiDung.Roles ?? [])
        {
            vaiTro.Add(r);
        }

        if (!string.IsNullOrWhiteSpace(nhanVien?.RoleName))
        {
            vaiTro.Add(nhanVien.RoleName);
        }

        if (vaiTro.Count == 0)
        {
            return new PhamViHienTai(PhamViDuLieu.All, nguoiDungId, nhanVien?.Id, null,
                "người dùng không có vai trò nào ⇒ mặc định All");
        }

        var ds = vaiTro.ToList();
        var cauHinh = await _phamVi.GetListAsync(x => ds.Contains(x.RoleName));

        if (cauHinh.Count == 0)
        {
            return new PhamViHienTai(PhamViDuLieu.All, nguoiDungId, nhanVien?.Id, null,
                "chưa cấu hình phạm vi cho vai trò nào ⇒ mặc định All");
        }

        // 🚨 Phép gộp: `All = 0` rộng nhất ⇒ `Min` = **rộng nhất** trong các vai trò được cấp.
        var phamVi = cauHinh.Min(x => x.Scope);

        if (phamVi == PhamViDuLieu.All)
        {
            return new PhamViHienTai(PhamViDuLieu.All, nguoiDungId, nhanVien?.Id, null,
                $"vai trò {string.Join(",", cauHinh.OrderBy(x => x.RoleName).Select(x => x.RoleName))} ⇒ All");
        }

        if (phamVi == PhamViDuLieu.Own)
        {
            return new PhamViHienTai(PhamViDuLieu.Own, nguoiDungId, nhanVien?.Id, null,
                $"vai trò {string.Join(",", cauHinh.OrderBy(x => x.RoleName).Select(x => x.RoleName))} ⇒ Own");
        }

        // ── STORE: cần biết kho của cửa hàng mà nhân viên thuộc ─────────────────────────────────
        Guid? kho = null;

        if (nhanVien?.StoreId is { } storeId)
        {
            var store = await _cuaHang.FindAsync(storeId);
            kho = store?.WarehouseId;
        }

        if (kho is null)
        {
            // ⚠️ `Warning` (không `Debug`): đây là **cấu hình thiếu** — người dùng sẽ thấy danh sách rỗng.
            _logger.LogWarning(
                "PHẠM VI DỮ LIỆU `Store` NHƯNG THIẾU KHO: người dùng {NguoiDungId} · nhân viên {NhanVienId} · " +
                "cửa hàng {CuaHangId} ⇒ danh sách sẽ RỖNG. [WP07-DATASCOPE-THIEU-KHO]",
                nguoiDungId, nhanVien?.Id, nhanVien?.StoreId);
        }

        return new PhamViHienTai(PhamViDuLieu.Store, nguoiDungId, nhanVien?.Id, kho,
            kho is null
                ? "vai trò Store nhưng nhân viên chưa gắn cửa hàng/kho ⇒ lọc ra RỖNG (không nới thành All)"
                : "vai trò Store ⇒ lọc theo kho của cửa hàng");
    }
}
