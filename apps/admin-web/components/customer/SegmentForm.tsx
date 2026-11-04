"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import type { CustomerDetailDto, CustomerLevelDto } from "@/lib/customer";

/**
 * **PHÂN KHÁCH** (loại · cấp · hạn mức nợ · MST · ngày sinh) — `WP-07`, quyền `eCommerce.Customers.Manage`.
 *
 * 🔑 **Hai điều form này cố ý KHÔNG cho sửa:**
 * ① **điểm / tiền tích luỹ / dư nợ** — đó là **số dư của một sổ**, chỉ đổi qua chứng từ (bán/trả/huỷ).
 *    Cho sửa tay ở đây là mở đường *"khách khiếu nại → sửa số"* và **mất dấu chứng từ** đã tạo ra nó.
 * ② **cấp khách khi để trống ô chọn** = *"để hệ thống tự xếp theo ngưỡng"* — không phải "bỏ cấp".
 */
export function SegmentForm({
  khach,
  caps,
}: {
  khach: CustomerDetailDto;
  caps: CustomerLevelDto[];
}) {
  const router = useRouter();
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [thanhCong, setThanhCong] = useState(false);

  async function luu(form: FormData) {
    setDangGui(true);
    setLoi(null);
    setThanhCong(false);

    const so = (ten: string): number => {
      const raw = String(form.get(ten) ?? "").replace(/\./g, "").replace(/,/g, ".").trim();
      const n = Number(raw);
      return Number.isFinite(n) ? n : 0;
    };

    const body = {
      type: Number(form.get("type") ?? 0),
      levelId: (form.get("levelId") as string) || null,
      debtLimit: so("debtLimit"),
      saleStaffId: null,
      referrerId: null,
      taxCode: ((form.get("taxCode") as string) || "").trim() || null,
      birthday: ((form.get("birthday") as string) || "") || null,
    };

    try {
      const res = await fetch(`/api/proxy/v1/customers/${khach.id}/segment`, {
        method: "PUT",
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
    <form
      className="nh-card p-3 space-y-2"
      action={(fd) => void luu(fd)}
    >
      <div className="text-[13px] font-semibold">Phân khách</div>

      <div className="flex flex-wrap items-end gap-2">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Loại khách</div>
          <select className="nh-input mt-0.5" name="type" defaultValue={String(customerTypeNumber(khach.type))}>
            <option value="0">Khách lẻ</option>
            <option value="1">Khách buôn</option>
            <option value="2">Đại lý</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Cấp (trống = tự xếp theo ngưỡng)</div>
          <select className="nh-input mt-0.5" name="levelId" defaultValue={khach.levelId ?? ""} style={{ width: 200 }}>
            <option value="">— tự động —</option>
            {caps.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} (≥ {c.minAccumulatedAmount})
              </option>
            ))}
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Hạn mức nợ (0 = không đặt)</div>
          <input className="nh-input mt-0.5" name="debtLimit" defaultValue={khach.debtLimit} style={{ width: 150 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Mã số thuế</div>
          <input className="nh-input mt-0.5" name="taxCode" defaultValue={khach.taxCode ?? ""} style={{ width: 160 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Ngày sinh</div>
          <input
            className="nh-input mt-0.5"
            type="date"
            name="birthday"
            defaultValue={khach.birthday ? khach.birthday.slice(0, 10) : ""}
          />
        </label>

        <button className="nh-btn" type="submit" disabled={dangGui}>
          {dangGui ? "Đang lưu…" : "Lưu phân khách"}
        </button>
      </div>

      {loi && (
        <div className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      {thanhCong && (
        <div className="text-[12px]" style={{ color: "var(--brand)" }}>
          Đã lưu.
        </div>
      )}

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        Điểm · tiền tích luỹ · dư nợ <b>không sửa được ở đây</b>: chúng là số dư của sổ bán/trả/huỷ — sửa tay
        là mất dấu chứng từ.
      </div>
    </form>
  );
}

/** `"Retail"` → `0` (giá trị số của enum) — 📌 FE không tự đoán thứ tự khác. */
export function customerTypeNumber(type: string | null | undefined): number {
  switch (type) {
    case "Wholesale":
      return 1;
    case "Agency":
      return 2;
    default:
      return 0;
  }
}
