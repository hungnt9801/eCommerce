"use client";

import { useMemo, useRef, useState } from "react";
import { docLoiApi } from "@/lib/api-error";
import {
  type NotificationBatchDto,
  type NotificationTemplateDto,
  canhBaoThieuTrang,
  duocDuyetLo,
  hopLeTotalItems,
  locTemplateTheoMucDich,
  moTaLoiThongBao,
  nhanTrangThaiLo,
  templateChuaPublish,
  TOTAL_ITEMS_MAX,
  TOTAL_ITEMS_MIN,
} from "@/lib/notification";

/**
 * **TẠO LÔ GỬI** — `NOT-001` / `WP-22` (SRS §10, `CreateBatchAsync`).
 *
 * 🚨 **Chỉ template `Marketing`** (`D-198`, `D-268`): lọc **phía client** từ danh sách template tải sẵn
 * (`purpose === Marketing`); template khác **không** có trong ô chọn. Template chưa `currentPublishedVersionId`
 * ⇒ **chặn tạo lô** + hướng dẫn publish (lỗi cấu hình, không phải "rỗng").
 *
 * 🚨 **Chống trùng** (`D-33`): `IdempotencyKey` sinh **ở client** (`crypto.randomUUID`), **giữ nguyên** khi bấm
 * lại ⇒ bấm 2 lần không tạo 2 lô. Nút `disabled` khi đang gửi (single-flight).
 *
 * 🚨 **Người tạo không tự duyệt** (`D-198`): sau khi tạo, `duocDuyetLo(batch, currentUserId)` = `false` ⇒
 * **không** hiện nút duyệt, chỉ hiện **liên kết chia sẻ** để người khác duyệt.
 */
export function BatchForm({
  business,
  templates,
  total,
  currentUserId,
}: {
  business: string;
  templates: NotificationTemplateDto[];
  total: number;
  currentUserId: string | null;
}) {
  // D-268: chỉ Marketing; cảnh báo khi còn trang chưa tải.
  const marketing = useMemo(() => locTemplateTheoMucDich(templates, 1), [templates]);
  const thieuTrang = canhBaoThieuTrang(total, templates.length);

  const [templateId, setTemplateId] = useState("");
  const [code, setCode] = useState("");
  const [totalItems, setTotalItems] = useState("1");
  const [correlationId, setCorrelationId] = useState("");
  const idemKeyRef = useRef<string>("");

  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [ketQua, setKetQua] = useState<NotificationBatchDto | null>(null);
  const [shareUrl, setShareUrl] = useState<string | null>(null);
  const [daSaoChep, setDaSaoChep] = useState(false);

  const daChon = marketing.find((t) => t.id === templateId) ?? null;
  const chuaPublish = daChon ? templateChuaPublish(daChon) : false;
  const soLuong = Number(totalItems);
  const soLuongHopLe = hopLeTotalItems(soLuong);

  async function tao() {
    if (dangGui) return;
    setLoi(null);
    setKetQua(null);
    setShareUrl(null);
    setDaSaoChep(false);

    if (!daChon || !daChon.id) {
      setLoi("Chọn một template Marketing đã publish.");
      return;
    }
    if (chuaPublish) {
      setLoi("Template chưa publish — hãy publish một version trước khi tạo lô (lỗi cấu hình 0012).");
      return;
    }
    if (!soLuongHopLe) {
      setLoi(`Số người nhận phải là số nguyên trong khoảng ${TOTAL_ITEMS_MIN}..${TOTAL_ITEMS_MAX}.`);
      return;
    }
    if (!code.trim()) {
      setLoi("Mã lô là bắt buộc.");
      return;
    }

    // IdempotencyKey: sinh 1 lần, GIỮ NGUYÊN khi bấm lại (retry) — không tạo lô trùng.
    if (!idemKeyRef.current) {
      idemKeyRef.current = crypto.randomUUID();
    }
    // correlationId: nếu người dùng chưa nhập, sinh và LƯU lại để lần bấm sau payload y hệt.
    let corr = correlationId.trim();
    if (!corr) {
      corr = crypto.randomUUID();
      setCorrelationId(corr);
    }

    setDangGui(true);
    try {
      const res = await fetch("/api/proxy/v1/notifications/batches", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          code: code.trim(),
          templateId: daChon.id,
          templateVersionId: daChon.currentPublishedVersionId,
          idempotencyKey: idemKeyRef.current,
          totalItems: soLuong,
          correlationId: corr,
        }),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(moTaLoiThongBao(failure.code) ?? (failure.code ? `${failure.code} — ${failure.message}` : failure.message));
        return;
      }

      const batch = (await res.json()) as NotificationBatchDto;
      setKetQua(batch);
      if (batch?.id) {
        setShareUrl(`${window.location.origin}/${business}/notifications/batches?batchId=${batch.id}`);
      }
      // Lô mới tạo xong ⇒ đổi khoá cho lần tạo kế tiếp (lần này đã "tiêu" khoá).
      idemKeyRef.current = "";
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangGui(false);
    }
  }

  const taoLaNguoiTao = ketQua ? !duocDuyetLo(ketQua, currentUserId) : false;

  return (
    <div className="nh-card p-3 space-y-3">
      <div className="text-[13px] font-semibold">Tạo lô gửi (chỉ Marketing)</div>

      {thieuTrang && (
        <div className="text-[12px]" style={{ color: "var(--warning)" }}>
          ⚠️ Đang có {templates.length}/{total} template — còn {total - templates.length} chưa tải. Ô chọn chỉ
          gồm template đã tải.
        </div>
      )}

      {marketing.length === 0 ? (
        <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          Chưa có template <b>Marketing</b> nào. Vào màn <b>Mẫu thông báo</b> tạo/publish một template mục đích
          Marketing trước — template khác mục đích không dùng được cho lô gửi (D-198).
        </div>
      ) : (
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-[12px]">
            <div style={{ color: "var(--text-muted)" }}>Mã lô *</div>
            <input
              className="nh-input mt-0.5"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              maxLength={64}
              placeholder="vd: promo-2026-11"
              style={{ width: 200 }}
            />
          </label>

          <label className="text-[12px]">
            <div style={{ color: "var(--text-muted)" }}>Template (Marketing) *</div>
            <select
              className="nh-input mt-0.5"
              value={templateId}
              onChange={(e) => setTemplateId(e.target.value)}
              style={{ width: 260 }}
            >
              <option value="">— chọn template —</option>
              {marketing.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.code} — {t.name}
                  {templateChuaPublish(t) ? " (chưa publish)" : ""}
                </option>
              ))}
            </select>
          </label>

          <label className="text-[12px]">
            <div style={{ color: "var(--text-muted)" }}>Số người nhận *</div>
            <input
              className="nh-input mt-0.5"
              type="number"
              min={TOTAL_ITEMS_MIN}
              max={TOTAL_ITEMS_MAX}
              value={totalItems}
              onChange={(e) => setTotalItems(e.target.value)}
              style={{ width: 140 }}
            />
          </label>

          <label className="text-[12px]">
            <div style={{ color: "var(--text-muted)" }}>CorrelationId (tuỳ chọn)</div>
            <input
              className="nh-input mt-0.5 font-mono"
              value={correlationId}
              onChange={(e) => setCorrelationId(e.target.value)}
              placeholder="tự sinh nếu bỏ trống"
              style={{ width: 260 }}
            />
          </label>
        </div>
      )}

      {daChon && chuaPublish && (
        <div className="text-[12px]" style={{ color: "var(--warning)" }}>
          ⚠️ Template <b>{daChon.code}</b> chưa có version publish. Số tin sẽ bị từ chối với mã{" "}
          <span className="font-mono">0012</span> — vào màn <b>Mẫu thông báo</b> publish trước.
        </div>
      )}

      {loi && (
        <div className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      {marketing.length > 0 && (
        <div className="flex items-center gap-2">
          <button
            type="button"
            className="nh-btn nh-btn-primary"
            onClick={() => void tao()}
            disabled={dangGui || !daChon || chuaPublish || !soLuongHopLe || !code.trim()}
          >
            {dangGui ? "Đang tạo…" : "Tạo lô"}
          </button>
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Lô tạo ra ở trạng thái <b>Chờ duyệt</b>; phải do <b>người khác</b> duyệt mới gửi.
          </span>
        </div>
      )}

      {ketQua && (
        <div className="border-t pt-2 space-y-1" style={{ borderColor: "var(--border)" }}>
          <div className="text-[12px]" style={{ color: "var(--success)" }}>
            Đã tạo lô <b>{ketQua.code}</b> — trạng thái: <b>{nhanTrangThaiLo(ketQua.status)}</b> · số người nhận:{" "}
            {ketQua.totalItems ?? 0}
          </div>

          {taoLaNguoiTao ? (
            <div className="text-[12px]" style={{ color: "var(--warning)" }}>
              🚨 Bạn là <b>người tạo lô</b> — bạn <b>không</b> được tự duyệt. Hãy gửi liên kết dưới cho người khác
              duyệt.
            </div>
          ) : (
            <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
              Dùng thẻ “Duyệt lô” bên dưới để duyệt.
            </div>
          )}

          {shareUrl && (
            <div className="space-y-1">
              <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>Liên kết chia sẻ (cho người duyệt):</div>
              <div className="flex items-center gap-2">
                <input className="nh-input font-mono text-[12px]" value={shareUrl} readOnly style={{ width: 520 }} />
                <button
                  type="button"
                  className="nh-btn"
                  onClick={async () => {
                    try {
                      await navigator.clipboard.writeText(shareUrl);
                      setDaSaoChep(true);
                    } catch {
                      setDaSaoChep(false);
                    }
                  }}
                >
                  {daSaoChep ? "Đã chép" : "Sao chép"}
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
