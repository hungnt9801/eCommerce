"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import type { DepartmentDto, StoreDto } from "@/lib/customer";

/**
 * **THÊM NHÂN VIÊN** — `WP-07`, quyền `eCommerce.Settings.Manage`.
 *
 * 🚨 **HAI ĐỊNH DANH, HAI MỤC ĐÍCH** (xem `Staff` ở BE): `code` là **mã trên chứng từ**, tài khoản đăng nhập
 * là **khoá của phạm vi dữ liệu**. 📌 Form này **không** tạo tài khoản ABP (việc của màn Identity) — nhân
 * viên chưa có tài khoản vẫn lưu được (`identityUserId = null`), và khi đó phạm vi dữ liệu áp cho họ
 * **chưa có tác dụng** (không đăng nhập được).
 */
export function StaffForm({
  stores,
  departments,
  roles,
}: {
  stores: StoreDto[];
  departments: DepartmentDto[];
  roles: string[];
}) {
  const router = useRouter();
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [thanhCong, setThanhCong] = useState(false);

  async function luu(form: FormData) {
    setDangGui(true);
    setLoi(null);
    setThanhCong(false);

    const body = {
      code: ((form.get("code") as string) || "").trim(),
      name: ((form.get("name") as string) || "").trim(),
      identityUserId: null,
      storeId: (form.get("storeId") as string) || null,
      departmentId: (form.get("departmentId") as string) || null,
      roleName: (form.get("roleName") as string) || null,
      phone: null,
      note: null,
      isActive: true,
    };

    try {
      const res = await fetch("/api/proxy/v1/settings/staff", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(failure.code ? `${failure.code} — ${failure.message}` : failure.message);
        return;
      }

      setThanhCong(true);
      router.refresh();
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <form className="nh-card p-3 space-y-2" action={(fd) => void luu(fd)}>
      <div className="text-[13px] font-semibold">Thêm nhân viên</div>

      <div className="flex flex-wrap items-end gap-2">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Mã NV</div>
          <input className="nh-input mt-0.5" name="code" style={{ width: 120 }} required />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Tên</div>
          <input className="nh-input mt-0.5" name="name" style={{ width: 200 }} required />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Vai trò (khoá phạm vi)</div>
          <select className="nh-input mt-0.5" name="roleName" defaultValue="" style={{ width: 160 }}>
            <option value="">— chưa gán —</option>
            {roles.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Cửa hàng</div>
          <select className="nh-input mt-0.5" name="storeId" defaultValue="" style={{ width: 180 }}>
            <option value="">—</option>
            {stores.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} — {s.name}
              </option>
            ))}
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Phòng ban</div>
          <select className="nh-input mt-0.5" name="departmentId" defaultValue="" style={{ width: 180 }}>
            <option value="">—</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.code} — {d.name}
              </option>
            ))}
          </select>
        </label>

        <button className="nh-btn" type="submit" disabled={dangGui}>
          {dangGui ? "Đang lưu…" : "Thêm"}
        </button>
      </div>

      {loi && (
        <div className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}
      {thanhCong && (
        <div className="text-[12px]" style={{ color: "var(--brand)" }}>
          Đã thêm nhân viên.
        </div>
      )}
    </form>
  );
}
