"use client";

import { Fragment, useState } from "react";
import { docLoiApi } from "@/lib/api-error";
import type { PermissionMatrixDto } from "@/lib/customer";

/**
 * **MA TRẬN QUYỀN** (permission × vai trò) — `WP-07`, quyền `eCommerce.Settings.Manage`.
 *
 * 🚨 **DANH SÁCH QUYỀN ĐẾN TỪ SERVER** (`PermissionDefinitionProvider`), **KHÔNG** phải bảng viết tay trong
 * FE: một bảng chép tay sẽ **không** có quyền thêm sau này ⇒ người vận hành **không cấp được**, và
 * **không ai biết** là thiếu (màn hình trông vẫn đầy đủ). 📌 Cùng nguyên tắc `D-41`/`D-33`.
 *
 * 🔑 **Ô vuông = grant TRỰC TIẾP cho role**. ABP còn cho **thừa hưởng** từ vai trò cha — 📌 nên ô trống
 * **không** luôn có nghĩa "không có quyền"; dòng ghi chú dưới bảng nói rõ điều đó để người vận hành
 * không kết luận sai.
 */
export function PermissionMatrix({ matrix }: { matrix: PermissionMatrixDto }) {
  const [granted, setGranted] = useState<Record<string, Set<string>>>(() => {
    const m: Record<string, Set<string>> = {};
    for (const role of matrix.roles ?? []) {
      m[role] = new Set(matrix.grantedByRole?.[role] ?? []);
    }
    return m;
  });

  const [dangGui, setDangGui] = useState<string | null>(null);
  const [loi, setLoi] = useState<string | null>(null);

  async function doi(role: string, permission: string, bat: boolean) {
    const khoa = `${role}::${permission}`;
    setDangGui(khoa);
    setLoi(null);

    // 📌 Cập nhật lạc quan để bấm 10 ô liên tiếp không bị "giật"; nếu server từ chối thì **hoàn lại**.
    const truoc = new Set(granted[role] ?? []);
    setGranted((cu) => {
      const moi = { ...cu };
      const s = new Set(moi[role] ?? []);
      if (bat) s.add(permission);
      else s.delete(permission);
      moi[role] = s;
      return moi;
    });

    try {
      const res = await fetch("/api/proxy/v1/settings/permissions", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ roleName: role, permissionName: permission, isGranted: bat }),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(failure.code ? `${failure.code} — ${failure.message}` : failure.message);
        setGranted((cu) => ({ ...cu, [role]: truoc }));
        return;
      }

      // 🔑 Lấy **sự thật** server trả (không tin state vừa gửi) — store có thể chuẩn hoá khác.
      const ketQua = (await res.json()) as { isGranted?: boolean };
      const daLuu = ketQua.isGranted === true;

      setGranted((cu) => {
        const moi = { ...cu };
        const s = new Set(moi[role] ?? []);
        if (daLuu) s.add(permission);
        else s.delete(permission);
        moi[role] = s;
        return moi;
      });
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
      setGranted((cu) => ({ ...cu, [role]: truoc }));
    } finally {
      setDangGui(null);
    }
  }

  const roles = matrix.roles ?? [];

  return (
    <div className="space-y-2">
      {loi && (
        <div className="nh-card p-2 text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      <div className="nh-card overflow-x-auto">
        <table className="w-full text-[12px]">
          <thead>
            <tr className="text-left" style={{ color: "var(--text-muted)" }}>
              <th className="p-2 sticky left-0" style={{ background: "var(--surface)" }}>
                Quyền
              </th>
              {roles.map((r) => (
                <th key={r} className="p-2 text-center">
                  {r}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {(matrix.groups ?? []).map((g) => (
              <Fragment key={`g-${g.name}`}>
                <tr className="border-t" style={{ borderColor: "var(--border)" }}>
                  <td
                    className="p-2 font-semibold"
                    colSpan={1 + roles.length}
                    style={{ background: "var(--surface-alt, transparent)" }}
                  >
                    {g.name}
                  </td>
                </tr>
                {(g.permissions ?? []).map((p) => {
                  // 📌 Chuẩn hoá **một chỗ** (spec OpenAPI để mọi prop tham chiếu là nullable).
                  const tenQuyen = p.name ?? "";

                  return (
                    <tr key={tenQuyen} className="border-t" style={{ borderColor: "var(--border)" }}>
                      <td className="p-2" style={{ paddingLeft: p.parentName ? 24 : 8 }}>
                        <span title={tenQuyen}>{p.displayName}</span>
                        {p.laQuyenDoc && (
                          <span className="ml-1 text-[10px]" style={{ color: "var(--text-muted)" }}>
                            (đọc)
                          </span>
                        )}
                      </td>
                      {roles.map((r) => {
                        const dang = (granted[r] ?? new Set<string>()).has(tenQuyen);
                        const khoa = `${r}::${tenQuyen}`;

                        return (
                          <td key={khoa} className="p-2 text-center">
                            <input
                              type="checkbox"
                              checked={dang}
                              disabled={dangGui === khoa}
                              onChange={(e) => void doi(r, tenQuyen, e.target.checked)}
                            />
                          </td>
                        );
                      })}
                    </tr>
                  );
                })}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        Ô vuông = quyền được cấp <b>trực tiếp</b> cho vai trò. ABP còn cho <b>thừa hưởng</b> từ vai trò cha —
        ô trống không luôn có nghĩa &quot;không có quyền&quot;.
      </div>
    </div>
  );
}
