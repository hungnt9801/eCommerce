"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import { CARE_TYPE_OPTIONS } from "@/lib/customer";

/**
 * **GHI MỘT LẦN CHĂM SÓC** — `WP-07` (tab "Chăm sóc"), quyền `eCommerce.Customers.Manage`.
 *
 * 📌 Biên bản là **append-only**: không có nút sửa/xoá (sửa biên bản = làm lại lịch sử). Đính chính thì
 * ghi thêm một dòng mới — lịch sử vẫn đọc được **cả hai**.
 * 🔑 `OccurredAtUtc` cho phép nhập **lùi ngày** (nhập lại cuộc gọi hôm qua) — nếu server lấy `now` thì mọi
 * lần nhập bù đều rơi vào hôm nay và báo cáo *"chăm sóc tuần này"* sai.
 */
export function CareForm({ customerId }: { customerId: string }) {
  const router = useRouter();
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [thanhCong, setThanhCong] = useState(false);

  async function luu(form: FormData) {
    setDangGui(true);
    setLoi(null);
    setThanhCong(false);

    const occurredAt = (form.get("occurredAt") as string) || "";
    const nextFollowUp = (form.get("nextFollowUp") as string) || "";

    const body = {
      type: Number(form.get("type") ?? 0),
      content: ((form.get("content") as string) || "").trim(),
      occurredAtUtc: occurredAt ? new Date(`${occurredAt}T12:00:00+07:00`).toISOString() : null,
      staffId: null,
      nextFollowUpAtUtc: nextFollowUp ? new Date(`${nextFollowUp}T09:00:00+07:00`).toISOString() : null,
    };

    if (body.content.length === 0) {
      setLoi("Một lần chăm sóc phải có NỘI DUNG.");
      setDangGui(false);
      return;
    }

    try {
      const res = await fetch(`/api/proxy/v1/customers/${customerId}/care`, {
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
      <div className="text-[13px] font-semibold">Ghi lần chăm sóc</div>

      <div className="flex flex-wrap items-end gap-2">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Hình thức</div>
          <select className="nh-input mt-0.5" name="type" defaultValue="0">
            {CARE_TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Ngày (trống = hôm nay)</div>
          <input className="nh-input mt-0.5" type="date" name="occurredAt" />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Hẹn theo dõi</div>
          <input className="nh-input mt-0.5" type="date" name="nextFollowUp" />
        </label>
      </div>

      <label className="text-[12px] block">
        <div style={{ color: "var(--text-muted)" }}>Nội dung (bắt buộc)</div>
        <textarea className="nh-input mt-0.5" name="content" rows={2} style={{ width: "100%" }} />
      </label>

      <button className="nh-btn" type="submit" disabled={dangGui}>
        {dangGui ? "Đang lưu…" : "Ghi chăm sóc"}
      </button>

      {loi && (
        <div className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      {thanhCong && (
        <div className="text-[12px]" style={{ color: "var(--brand)" }}>
          Đã ghi.
        </div>
      )}
    </form>
  );
}
