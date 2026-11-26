"use client";

import { useState } from "react";
import { docLoiApi } from "@/lib/api-error";
import {
  type NotificationBatchDto,
  moTaLoiThongBao,
  nhanTrangThaiLo,
} from "@/lib/notification";

/**
 * **DUYỆT LÔ GỬI** — `NOT-001` / `WP-22` (SRS §10, `ApproveBatchAsync`).
 *
 * 🔐 Chỉ hiện nút khi có quyền `Notification.Batches.Approve` (`getGrantedPermissions` ở server page).
 * 🚨 Hộp thoại xác nhận nêu rõ **“sẽ gửi ra người thật”** — duyệt lô là thao tác phát tin hàng loạt.
 * 🚨 Người tạo **không** tự duyệt: BE ném `0011`; UI dịch đúng nghĩa. (Không có `GET /batches/{id}` nên
 * UI không tự kiểm người tạo trước được — server là cửa chặn.)
 */
export function BatchApprove({
  coQuyenDuyet,
  initialBatchId,
}: {
  business: string;
  coQuyenDuyet: boolean;
  initialBatchId: string;
}) {
  const [batchId, setBatchId] = useState(initialBatchId);
  const [hienXacNhan, setHienXacNhan] = useState(false);
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [ketQua, setKetQua] = useState<NotificationBatchDto | null>(null);

  if (!coQuyenDuyet) {
    return (
      <div className="nh-card p-3 text-[12px] space-y-1">
        <div className="text-[13px] font-semibold">Duyệt lô</div>
        <div style={{ color: "var(--text-muted)" }}>
          Cần quyền <span className="font-mono">Notification.Batches.Approve</span> để duyệt lô gửi.
        </div>
      </div>
    );
  }

  async function duyet() {
    if (dangGui) return;
    const id = batchId.trim();
    if (!id) {
      setLoi("Nhập hoặc dán mã lô (batchId) cần duyệt.");
      return;
    }

    setDangGui(true);
    setLoi(null);
    setKetQua(null);

    try {
      const res = await fetch(
        `/api/proxy/v1/notifications/batches/${encodeURIComponent(id)}/approve`,
        { method: "POST", headers: { "Content-Type": "application/json" } },
      );

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(moTaLoiThongBao(failure.code) ?? (failure.code ? `${failure.code} — ${failure.message}` : failure.message));
        return;
      }

      setKetQua((await res.json()) as NotificationBatchDto);
      setHienXacNhan(false);
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <div className="nh-card p-3 space-y-2">
      <div className="text-[13px] font-semibold">Duyệt lô</div>
      <div className="flex flex-wrap items-end gap-2">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Mã lô (batchId)</div>
          <input
            className="nh-input mt-0.5 font-mono"
            value={batchId}
            onChange={(e) => setBatchId(e.target.value)}
            placeholder="dán id lô do người tạo chia sẻ"
            style={{ width: 360 }}
          />
        </label>
        <button
          type="button"
          className="nh-btn nh-btn-primary"
          onClick={() => {
            setLoi(null);
            setKetQua(null);
            if (!batchId.trim()) {
              setLoi("Nhập hoặc dán mã lô (batchId) cần duyệt.");
              return;
            }
            setHienXacNhan(true);
          }}
          disabled={dangGui}
        >
          Duyệt lô
        </button>
      </div>

      {loi && (
        <div className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      {ketQua && (
        <div className="text-[12px]" style={{ color: "var(--success)" }}>
          Đã duyệt lô <b>{ketQua.code}</b> — trạng thái: <b>{nhanTrangThaiLo(ketQua.status)}</b>.
        </div>
      )}

      {hienXacNhan && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          style={{ background: "rgba(0,0,0,0.35)" }}
        >
          <div className="nh-card p-4 max-w-[520px] w-full space-y-3">
            <div className="text-[15px] font-semibold">Xác nhận duyệt lô</div>
            <div className="text-[13px]" style={{ color: "var(--danger)" }}>
              🚨 Duyệt lô <b>sẽ gửi tin ra người thật</b>. Mọi mục “Chờ duyệt” của lô sẽ chuyển sang “Chờ gửi”.
            </div>
            <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
              Người tạo lô không được tự duyệt (BE trả mã <span className="font-mono">0011</span>).
            </div>
            <div className="flex items-center justify-end gap-2">
              <button
                type="button"
                className="nh-btn"
                onClick={() => setHienXacNhan(false)}
                disabled={dangGui}
              >
                Huỷ
              </button>
              <button
                type="button"
                className="nh-btn nh-btn-primary"
                onClick={() => void duyet()}
                disabled={dangGui}
              >
                {dangGui ? "Đang duyệt…" : "Xác nhận gửi ra người thật"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
