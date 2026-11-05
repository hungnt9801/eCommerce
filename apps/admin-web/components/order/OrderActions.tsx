"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import {
  CANCEL_OPTIONS,
  FULFILLMENT_LABEL,
  REASON_OPTIONS,
  sortTransitions,
  type FulfillmentStatus,
  type OrderReasonCode,
  type OrderStatusDto,
} from "@/lib/order";

type KetQua = { ok: boolean; text: string; code?: string | null; canhBao?: string | null };

/**
 * **Đổi trạng thái + huỷ đơn** trên màn chi tiết (`04 §5` màn #11).
 *
 * 🚨 **BA ĐIỀU UI PHẢI TÔN TRỌNG (nếu không, người dùng mất niềm tin):**
 * 1. **Chỉ hiện bước ĐI TIẾP ĐƯỢC** — `allowedTransitions` do **server** trả (suy từ bảng cạnh state machine)
 *    ⇒ không có nút nào bấm vào rồi nhận `eCommerce:Order:0001`.
 * 2. **Gửi `expectedVersion`** — hai người cùng mở một đơn, người sau nhận **`0003`** và phải tải lại;
 *    UI hiện **mã** chứ không nói "lỗi 403" (xem `lib/api-error.ts`).
 * 3. **Huỷ là cửa RIÊNG** (quyền `Orders.Cancel`) và phải nêu **ai huỷ** + **lý do**; hàng đã rời kho thì
 *    API trả `0001` — UI nói rõ *"phải Trả hàng, không phải Huỷ"* thay vì để người dùng bấm lại.
 *
 * 📌 Kết quả trả về nói rõ **tác động kho** (`stockEffect`/`stockAffectedLines`): *"đã chốt 2 lượt giữ chỗ"* —
 * đây là câu trả lời cho *"tồn kho có đổi không"* mà không phải mở màn kho.
 */
export function OrderActions({
  orderId,
  fulfillmentVersion,
  closureVersion,
  allowedTransitions,
  canCancel,
  isCancelled,
}: {
  orderId: string;
  fulfillmentVersion: number;
  closureVersion: number;
  allowedTransitions: readonly FulfillmentStatus[] | null | undefined;
  canCancel: boolean;
  isCancelled: boolean;
}) {
  const router = useRouter();
  const [dangChay, setDangChay] = useState<string | null>(null);
  const [ketQua, setKetQua] = useState<KetQua | null>(null);
  const [hienHuy, setHienHuy] = useState(false);
  const [aiHuy, setAiHuy] = useState<FulfillmentStatus>(14);
  const [lyDo, setLyDo] = useState<OrderReasonCode | "">(1);
  const [ghiChu, setGhiChu] = useState("");

  const buoc = sortTransitions(allowedTransitions);

  async function goi(path: string, body: unknown, nhan: string) {
    setDangChay(nhan);
    setKetQua(null);

    try {
      const res = await fetch(`/api/proxy${path}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      const raw = await res.text();
      let du: unknown = null;
      try {
        du = raw.length > 0 ? JSON.parse(raw) : null;
      } catch {
        du = null;
      }

      if (!res.ok) {
        const loi = docLoiApi(du, res.status);
        setKetQua({ ok: false, text: loi.message, code: loi.code });
        return;
      }

      const kq = du as OrderStatusDto | null;
      const nhanMoi = kq ? (FULFILLMENT_LABEL[kq.fulfillmentStatus ?? -1] ?? "") : "";
      const kho =
        kq && kq.stockEffect === "ChotGiuCho"
          ? ` Đã chốt ${kq.stockAffectedLines ?? 0} lượt giữ chỗ (xuất kho).`
          : kq && kq.stockEffect === "NhapHangHoan"
            ? ` Đã nhập lại kho ${kq.stockAffectedLines ?? 0} dòng hàng hoàn.`
            : "";

      setKetQua({
        ok: true,
        text: `Đã chuyển sang "${nhanMoi || nhan}".${kho}`,
        canhBao: kq?.warning ?? null,
      });

      setHienHuy(false);
      router.refresh();
    } catch {
      setKetQua({ ok: false, text: "Không gọi được BFF (/api/proxy). Kiểm tra Next đang chạy." });
    } finally {
      setDangChay(null);
    }
  }

  if (isCancelled) {
    return (
      <div className="nh-card p-3 text-[12px]" style={{ color: "var(--text-muted)" }}>
        Đơn đã huỷ — trạng thái <b>hấp thụ</b>: không chuyển tiếp được. Muốn bán lại phải tạo đơn mới.
      </div>
    );
  }

  return (
    <div className="nh-card p-3 space-y-3">
      <div className="flex items-center justify-between">
        <div className="text-[13px] font-semibold">Xử lý đơn</div>
        <span className="text-[11px] tabular" style={{ color: "var(--text-muted)" }}>
          bản ghi thực hiện: {fulfillmentVersion}
        </span>
      </div>

      {buoc.length === 0 ? (
        <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          Không còn bước chuyển trạng thái nào từ trạng thái hiện tại (đơn đã kết thúc vòng đời).
        </p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {buoc.map((den) => {
            const nhan = FULFILLMENT_LABEL[den] ?? String(den);
            const laXuatKho = den === 1; // `Confirmed` ⇒ chốt giữ chỗ ⇒ xuất kho
            const laNhapKho = den === 8; // `Returned` ⇒ nhập hàng hoàn

            return (
              <button
                key={den}
                type="button"
                className={laXuatKho ? "nh-btn nh-btn-primary" : "nh-btn"}
                disabled={dangChay !== null}
                onClick={() =>
                  goi(
                    `/v1/orders/${orderId}/transitions`,
                    {
                      toStatus: den,
                      expectedVersion: fulfillmentVersion,
                      reasonCode: `Chuyen_${den}`,
                      reasonText:
                        laXuatKho
                          ? "Xác nhận đơn — chốt giữ chỗ và xuất kho."
                          : laNhapKho
                            ? "Nhận hàng hoàn — nhập lại kho."
                            : null,
                    },
                    nhan,
                  )
                }
              >
                {dangChay === nhan ? "Đang chuyển…" : `→ ${nhan}`}
              </button>
            );
          })}
        </div>
      )}

      {buoc.length > 0 && (
        <p className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          Danh sách nút lấy từ <b>bảng chuyển trạng thái của domain</b> ⇒ không có nút nào dẫn tới lỗi{" "}
          <code>eCommerce:Order:0001</code>. Bước <b>Đã xác nhận</b> chốt giữ chỗ (xuất kho); bước{" "}
          <b>Đã hoàn</b> nhập lại kho.
        </p>
      )}

      <div className="border-t pt-3" style={{ borderColor: "var(--border)" }}>
        {!canCancel ? (
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Hàng đã rời kho ⇒ <b>không huỷ được</b>: phải TRẢ HÀNG (chứng từ nhập), không xoá bút toán sổ cái.
          </p>
        ) : !hienHuy ? (
          <button type="button" className="nh-btn" onClick={() => setHienHuy(true)} disabled={dangChay !== null}>
            Huỷ đơn…
          </button>
        ) : (
          <div className="space-y-2">
            <div className="grid grid-cols-2 gap-2">
              <label className="block">
                <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                  Ai huỷ
                </span>
                <select
                  className="nh-input mt-0.5 w-full"
                  value={aiHuy}
                  onChange={(e) => setAiHuy(Number(e.target.value) as FulfillmentStatus)}
                >
                  {CANCEL_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="block">
                <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                  Lý do (24 lý do Nhanh)
                </span>
                <select
                  className="nh-input mt-0.5 w-full"
                  value={lyDo}
                  onChange={(e) => setLyDo(e.target.value === "" ? "" : (Number(e.target.value) as OrderReasonCode))}
                >
                  <option value="">— không nêu —</option>
                  {REASON_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <input
              className="nh-input w-full"
              placeholder="Ghi chú (tuỳ chọn)"
              value={ghiChu}
              onChange={(e) => setGhiChu(e.target.value)}
            />

            <div className="flex items-center gap-2">
              <button
                type="button"
                className="nh-btn"
                style={{ color: "var(--danger)" }}
                disabled={dangChay !== null}
                onClick={() =>
                  goi(
                    `/v1/orders/${orderId}/cancel`,
                    {
                      cancelledBy: aiHuy,
                      expectedVersion: closureVersion,
                      reasonCode: "HuyTheoTrangThai",
                      businessReason: lyDo === "" ? null : lyDo,
                      reasonText: ghiChu || null,
                    },
                    "Huỷ",
                  )
                }
              >
                {dangChay === "Huỷ" ? "Đang huỷ…" : "Xác nhận huỷ"}
              </button>
              <button type="button" className="nh-btn" onClick={() => setHienHuy(false)} disabled={dangChay !== null}>
                Thôi
              </button>
            </div>

            <p className="text-[11px]" style={{ color: "var(--text-muted)" }}>
              Huỷ sẽ <b>nhả giữ chỗ ngay</b> (hàng bán được cho người khác) và ghi <b>2 dòng lịch sử</b>:
              ai huỷ + đơn đóng.
            </p>
          </div>
        )}
      </div>

      {ketQua && (
        <div
          className="text-[12px] px-3 py-2 rounded space-y-1"
          style={
            ketQua.ok
              ? { background: "#ecfdf5", color: "var(--success)" }
              : { background: "#fef2f2", color: "var(--danger)" }
          }
        >
          <div>{ketQua.text}</div>
          {ketQua.canhBao && (
            <div className="text-[11px]" style={{ color: "#b45309" }}>
              ⚠ {ketQua.canhBao}
            </div>
          )}
          {ketQua.code && (
            <div className="tabular text-[11px]">
              mã: <code>{ketQua.code}</code>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default OrderActions;
