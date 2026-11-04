"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import { SCOPE_OPTIONS } from "@/lib/customer";

/**
 * **CHỌN PHẠM VI DỮ LIỆU cho một VAI TRÒ** — `WP-07`, quyền `eCommerce.Settings.Manage`.
 *
 * 🚨 Đây là thao tác **phân quyền**, không phải sửa hồ sơ: đổi xong, lần gọi API **kế tiếp** của người
 * thuộc vai trò đó đã bị lọc theo phạm vi mới (BE đọc phạm vi từ DB, **không** từ token — xem `RoleDataScope`).
 * 📌 Nhãn chọn nói **hệ quả** (*"Chỉ dữ liệu của tôi"*) chứ không phải tên enum.
 */
export function ScopeSelect({
  roles,
  macDinh,
}: {
  /** Các vai trò có trong hệ thống (đọc từ ma trận quyền — nguồn duy nhất). */
  roles: string[];
  /** Vai trò chọn sẵn (thường là `admin`). */
  macDinh?: string;
}) {
  const router = useRouter();
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);

  const [vaiTro, setVaiTro] = useState(macDinh ?? roles[0] ?? "");
  const [phamVi, setPhamVi] = useState<number>(0);

  async function luu() {
    if (!vaiTro) {
      setLoi("Chọn vai trò trước.");
      return;
    }

    setDangGui(true);
    setLoi(null);

    try {
      const res = await fetch("/api/proxy/v1/settings/data-scopes", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ roleName: vaiTro, scope: phamVi }),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(failure.code ? `${failure.code} — ${failure.message}` : failure.message);
        return;
      }

      router.refresh();
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <div className="flex flex-wrap items-end gap-2">
      <label className="text-[12px]">
        <div style={{ color: "var(--text-muted)" }}>Vai trò</div>
        <select
          className="nh-input mt-0.5"
          value={vaiTro}
          onChange={(e) => setVaiTro(e.target.value)}
          disabled={dangGui}
          style={{ width: 180 }}
        >
          {roles.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
      </label>

      <label className="text-[12px]">
        <div style={{ color: "var(--text-muted)" }}>Phạm vi dữ liệu</div>
        <select
          className="nh-input mt-0.5"
          value={phamVi}
          onChange={(e) => setPhamVi(Number(e.target.value))}
          disabled={dangGui}
          style={{ width: 260 }}
        >
          {SCOPE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label} — {o.note}
            </option>
          ))}
        </select>
      </label>

      <button className="nh-btn" type="button" disabled={dangGui} onClick={() => void luu()}>
        {dangGui ? "Đang lưu…" : "Lưu phạm vi"}
      </button>

      {loi && (
        <span className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </span>
      )}
    </div>
  );
}
