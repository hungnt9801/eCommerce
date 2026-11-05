"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";

type KetQua = { ok: boolean; text: string; code?: string | null };

/**
 * **Nút ghi sổ / huỷ phiếu** trên màn chi tiết phiếu (màn #8).
 *
 * 🚨 Ba bất biến của `WP-04` mà UI **phải** tôn trọng (nếu không, người dùng mất niềm tin):
 * 1. **Ghi sổ mới đổi tồn** — phiếu `Draft` không ảnh hưởng kho ⇒ chữ dưới nút nói rõ.
 * 2. **Ghi sổ lần hai bị chặn** (`eCommerce:Inventory:0010`) — nút biến mất khi `Posted`;
 *    nếu tab khác đã ghi sổ trước, lời gọi này trả **403 + mã** và UI hiện **mã**, không hiện "lỗi 403".
 * 3. **Phiếu đã ghi sổ không huỷ được** (`0013`) — chỉ tạo phiếu đảo.
 */
export function BillActions({
  billId,
  status,
  onDoneHref,
}: {
  billId: string;
  status: string;
  /** Đích quay về sau khi huỷ; bỏ trống ⇒ ở lại trang chi tiết và làm mới. */
  onDoneHref?: string;
}) {
  const router = useRouter();
  const [dangChay, setDangChay] = useState<"post" | "cancel" | null>(null);
  const [ketQua, setKetQua] = useState<KetQua | null>(null);

  async function goi(action: "post" | "cancel") {
    setDangChay(action);
    setKetQua(null);

    try {
      const res = await fetch(`/api/proxy/v1/stock/bills/${billId}/${action}`, { method: "POST" });

      const raw = await res.text();
      let body: unknown = null;
      try {
        body = raw.length > 0 ? JSON.parse(raw) : null;
      } catch {
        body = null;
      }

      if (!res.ok) {
        const loi = docLoiApi(body, res.status);
        setKetQua({ ok: false, text: loi.message, code: loi.code });
        return;
      }

      if (action === "post") {
        const soButToan =
          body && typeof body === "object" && "postedEntries" in body
            ? Number((body as { postedEntries?: number }).postedEntries ?? 0)
            : 0;

        setKetQua({
          ok: true,
          text: `Đã ghi sổ — ${soButToan} bút toán sổ cái. Tồn kho bên dưới vừa cập nhật.`,
        });
        router.refresh();
        return;
      }

      setKetQua({ ok: true, text: "Đã huỷ phiếu (phiếu Nháp không ảnh hưởng tồn kho)." });

      if (onDoneHref) {
        router.push(onDoneHref);
      } else {
        router.refresh();
      }
    } catch {
      setKetQua({ ok: false, text: "Không gọi được BFF (/api/proxy). Kiểm tra Next đang chạy." });
    } finally {
      setDangChay(null);
    }
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        {status === "Draft" ? (
          <>
            <button
              type="button"
              className="nh-btn nh-btn-primary"
              disabled={dangChay !== null}
              onClick={() => goi("post")}
            >
              {dangChay === "post" ? "Đang ghi sổ…" : "Ghi sổ phiếu"}
            </button>
            <button
              type="button"
              className="nh-btn"
              disabled={dangChay !== null}
              onClick={() => goi("cancel")}
            >
              {dangChay === "cancel" ? "Đang huỷ…" : "Huỷ phiếu"}
            </button>
          </>
        ) : (
          <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {status === "Posted"
              ? "Phiếu đã ghi sổ — sổ cái append-only, muốn đảo phải tạo phiếu ĐẢO."
              : "Phiếu đã huỷ — không ghi sổ được nữa."}
          </span>
        )}
      </div>

      {status === "Draft" && (
        <p className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          Ghi sổ lần hai bị chặn ở API (<code>eCommerce:Inventory:0010</code>) và tồn kho không đổi.
        </p>
      )}

      {ketQua && (
        <div
          className="text-[12px] px-3 py-2 rounded"
          style={
            ketQua.ok
              ? { background: "#ecfdf5", color: "var(--success)" }
              : { background: "#fef2f2", color: "var(--danger)" }
          }
        >
          <div>{ketQua.text}</div>
          {ketQua.code && (
            <div className="mt-0.5 tabular" style={{ fontSize: 11 }}>
              mã: <code>{ketQua.code}</code>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default BillActions;
