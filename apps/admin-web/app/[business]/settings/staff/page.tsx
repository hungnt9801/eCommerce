import Link from "next/link";
import { apiGetSafe } from "@/lib/api";
import {
  type DepartmentDto,
  type PermissionMatrixDto,
  type PhamViCuaToiDto,
  type RoleDataScopeDto,
  type StaffDto,
  type StoreDto,
} from "@/lib/customer";
import { StaffForm } from "@/components/settings/StaffForm";
import { ScopeSelect } from "@/components/settings/ScopeSelect";
import { PermissionMatrix } from "@/components/settings/PermissionMatrix";

/**
 * **NHÂN VIÊN & MA TRẬN QUYỀN** (`/{business}/settings/staff`) — `WP-07`.
 *
 * Ba khối, đúng ba câu hỏi người quản lý hỏi:
 * ① **Ai đang làm việc** (nhân viên · cửa hàng · phòng ban · vai trò);
 * ② **Mỗi vai trò thấy dữ liệu nào** (`All`/`Store`/`Own`) — 🔑 đây là thứ quyết định *"nhân viên bật
 *    'chỉ xem dữ liệu của mình' chỉ thấy đơn mình tạo"* (DoD của `WP-07`);
 * ③ **Vai trò nào có quyền gì** (ma trận đọc từ định nghĩa permission thật).
 *
 * 🚨 **Cả ba khối đều đọc từ server** — không có danh sách viết tay trong page: thêm permission/vai trò mới
 * là màn hình tự có (nếu chép tay thì người vận hành **không cấp được**, và không ai biết là thiếu).
 */
export default async function StaffSettingsPage({ params }: { params: Promise<{ business: string }> }) {
  const { business } = await params;

  const [nhanVien, cuaHang, phongBan, phamVi, maTran, cuaToi] = await Promise.all([
    apiGetSafe<StaffDto[]>("/api/v1/settings/staff", []),
    apiGetSafe<StoreDto[]>("/api/v1/settings/stores", []),
    apiGetSafe<DepartmentDto[]>("/api/v1/settings/departments", []),
    apiGetSafe<RoleDataScopeDto[]>("/api/v1/settings/data-scopes", []),
    apiGetSafe<PermissionMatrixDto>("/api/v1/settings/permissions", { groups: [], roles: [], grantedByRole: {} }),
    apiGetSafe<PhamViCuaToiDto | null>("/api/v1/settings/data-scopes/me", null),
  ]);

  const roles = maTran.roles ?? [];
  const phamViTheoVaiTro = new Map(phamVi.map((x) => [x.roleName, x]));

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Nhân viên &amp; quyền</h1>
        <Link className="nh-btn ml-auto" href={`/${business}/customer/list`}>
          Khách hàng
        </Link>
      </div>

      {cuaToi && (
        <div className="nh-card p-2 text-[12px]">
          Phạm vi dữ liệu <b>hiệu lực của bạn</b>: <b>{scopeLabel(cuaToi.phamVi)}</b>
          {cuaToi.lyDo ? ` — ${cuaToi.lyDo}` : ""}
        </div>
      )}

      <StaffForm stores={cuaHang} departments={phongBan} roles={roles} />

      <div className="nh-card overflow-x-auto">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="text-left" style={{ color: "var(--text-muted)" }}>
              <th className="p-2">Mã</th>
              <th className="p-2">Tên</th>
              <th className="p-2">Tài khoản</th>
              <th className="p-2">Cửa hàng</th>
              <th className="p-2">Phòng ban</th>
              <th className="p-2">Vai trò</th>
              <th className="p-2">Phạm vi dữ liệu</th>
              <th className="p-2">Hoạt động</th>
            </tr>
          </thead>
          <tbody>
            {nhanVien.length === 0 && (
              <tr>
                <td className="p-3" colSpan={8} style={{ color: "var(--text-muted)" }}>
                  Chưa có nhân viên nào — thêm ở form phía trên (mã NV + tên + vai trò).
                </td>
              </tr>
            )}

            {nhanVien.map((n) => (
              <tr key={n.id} className="border-t" style={{ borderColor: "var(--border)" }}>
                <td className="p-2 font-mono text-[11px]">{n.code}</td>
                <td className="p-2">{n.name}</td>
                <td className="p-2">
                  {n.identityUserId ? (
                    <span className="font-mono text-[11px]">{n.identityUserId.slice(0, 8)}…</span>
                  ) : (
                    <span style={{ color: "var(--text-muted)" }}>chưa cấp</span>
                  )}
                </td>
                <td className="p-2">{n.storeName ?? "—"}</td>
                <td className="p-2">{n.departmentName ?? "—"}</td>
                <td className="p-2">{n.roleName ?? "—"}</td>
                <td className="p-2">{scopeLabel(n.phamViHieuLuc)}</td>
                <td className="p-2">{n.isActive ? "có" : "nghỉ"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="nh-card p-3 space-y-2">
        <div className="text-[13px] font-semibold">Phạm vi dữ liệu theo vai trò</div>

        <ScopeSelect roles={roles} macDinh={roles.find((r) => r.toLowerCase() === "admin") ?? roles[0]} />

        <table className="w-full text-[12px]">
          <thead>
            <tr className="text-left" style={{ color: "var(--text-muted)" }}>
              <th className="p-2">Vai trò</th>
              <th className="p-2">Phạm vi</th>
              <th className="p-2">Nghĩa</th>
            </tr>
          </thead>
          <tbody>
            {roles.map((r) => {
              const dong = phamViTheoVaiTro.get(r);

              return (
                <tr key={r} className="border-t" style={{ borderColor: "var(--border)" }}>
                  <td className="p-2 font-mono text-[11px]">{r}</td>
                  <td className="p-2">{dong ? scopeLabel(dong.scope) : "Tất cả (chưa cấu hình)"}</td>
                  <td className="p-2" style={{ color: "var(--text-muted)" }}>
                    {dong ? dong.scopeLabel : "Mặc định của hệ thống"}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>

        <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          📌 Đổi phạm vi có hiệu lực ở **lần gọi API kế tiếp** (BE đọc từ DB, không từ token). Phạm vi `Store`
          cần nhân viên thuộc một cửa hàng **đã gắn kho** — thiếu kho thì danh sách sẽ **rỗng** (cố ý, không
          nới thành `Tất cả`).
        </div>
      </div>

      <div className="space-y-2">
        <div className="text-[13px] font-semibold">Ma trận quyền</div>
        <PermissionMatrix matrix={maTran} />
      </div>
    </div>
  );
}

function scopeLabel(scope: string | null | undefined): string {
  switch (scope) {
    case "Store":
      return "Của cửa hàng";
    case "Own":
      return "Chỉ của tôi";
    case "All":
      return "Tất cả";
    default:
      return scope ?? "—";
  }
}
