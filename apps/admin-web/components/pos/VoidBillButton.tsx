"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";

/**
 * Nút **HUỶ hoá đơn** (đảo tồn + đảo tiền) — `WP-06`, quyền `eCommerce.Pos.Void`.
 *
 * 🚨 **KHÔNG ẩn nút khi ca đã kết rồi để server báo lỗi** — ngược với quy tắc "không hiện nút rồi báo lỗi"
 * (`04 §6.4`): hoá đơn của ca đã kết thì nút **không render** (`cashSessionClosed` do API trả).
 * 📌 Lý do thứ hai: huỷ là thao tác **đảo doanh thu** ⇒ luôn hỏi lý do (audit + đối chiếu két).
 */
export function VoidBillButton({
  billId,
  billNumber,
  daHuy,
  caDaKet,
}: {
  billId: string;
  billNumber: string;
  daHuy: boolean;
  caDaKet: boolean;
}) {
  const router = useRouter();
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);

  if (daHuy || caDaKet) {
    return (
      <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        {daHuy ? "đã huỷ" : "ca đã kết"}
      </span>
    );
  }

  async function huy() {
    const lyDo = window.prompt(`Lý do HUỶ hoá đơn ${billNumber} (bắt buộc):`, "");

    if (lyDo === null) return;

    if (lyDo.trim().length === 0) {
      setLoi("Huỷ hoá đơn phải nêu LÝ DO.");
      return;
    }

    setDangGui(true);
    setLoi(null);

    try {
      const res = await fetch(`/api/proxy/v1/pos/bills/${billId}/void`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason: lyDo.trim() }),
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
    <span className="inline-flex flex-col">
      <button type="button" className="underline" disabled={dangGui} onClick={() => void huy()}>
        {dangGui ? "Đang huỷ…" : "Huỷ"}
      </button>
      {loi && (
        <span className="text-[10px]" style={{ color: "var(--danger)" }}>
          {loi}
        </span>
      )}
    </span>
  );
}
