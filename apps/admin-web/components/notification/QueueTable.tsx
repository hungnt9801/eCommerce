"use client";

import { Fragment, useState } from "react";
import { docLoiApi } from "@/lib/api-error";
import {
  type NotificationQueueEventDto,
  type NotificationQueueItemDto,
  dinhDangNgayGio,
  laLoiCauHinh,
  moTaLoiThongBao,
  nhanKenh,
  nhanLoaiSuKien,
  nhanTrangThaiHangDoi,
} from "@/lib/notification";

/**
 * **BẢNG HÀNG ĐỢI + TIMELINE MỞ RỘNG** — `NOT-001` / `WP-22` (SRS §10).
 *
 * 🚨 **CHE người nhận (`D-191`)**: chỉ hiển thị `recipientMasked` / `renderedBodyMasked` — **tuyệt đối**
 * không hiện bản đầy đủ (controller đã không trả trường thô; UI cũng không được ghép lại).
 *
 * 🚨 **`D-199`**: `renderedBodyMasked` và timeline render bằng `<pre>{text}</pre>` (React ghi `textContent`)
 * — **CẤM** `dangerouslySetInnerHTML`.
 *
 * 📌 API **không có** `GET /queue/{id}` ⇒ chi tiết mở **ngay trong bảng**: bấm dòng → gọi
 * `GET /queue/{id}/events` (sổ append-only, chỉ đọc) và hiện timeline theo `occurredAtUtc`.
 */
export function QueueTable({ items }: { items: NotificationQueueItemDto[] }) {
  const [moId, setMoId] = useState<string | null>(null);
  const [events, setEvents] = useState<Record<string, NotificationQueueEventDto[]>>({});
  const [dangTai, setDangTai] = useState<string | null>(null);
  const [loiTheoId, setLoiTheoId] = useState<Record<string, string>>({});

  async function moDong(item: NotificationQueueItemDto) {
    const id = item.id ?? "";
    if (!id) return;

    if (moId === id) {
      setMoId(null);
      return;
    }
    setMoId(id);

    if (events[id] || dangTai === id) return;

    setDangTai(id);
    setLoiTheoId((cu) => {
      const { [id]: _boQua, ...con } = cu;
      return con;
    });

    try {
      const res = await fetch(`/api/proxy/v1/notifications/queue/${encodeURIComponent(id)}/events`);
      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoiTheoId((cu) => ({ ...cu, [id]: moTaLoiThongBao(failure.code) ?? failure.code ?? failure.message }));
        return;
      }
      const ds = (await res.json()) as NotificationQueueEventDto[];
      const sapXep = [...(ds ?? [])].sort((a, b) =>
        (a.occurredAtUtc ?? "").localeCompare(b.occurredAtUtc ?? ""),
      );
      setEvents((cu) => ({ ...cu, [id]: sapXep }));
    } catch {
      setLoiTheoId((cu) => ({ ...cu, [id]: "Không gọi được API (mất mạng?)." }));
    } finally {
      setDangTai(null);
    }
  }

  return (
    <div className="nh-card overflow-auto">
      <table className="nh-table">
        <thead>
          <tr>
            <th className="w-[36px]"></th>
            <th className="w-[170px]">Người nhận (đã che)</th>
            <th className="w-[100px]">Kênh</th>
            <th className="w-[150px]">Mục / sự kiện</th>
            <th className="w-[140px]">Trạng thái</th>
            <th className="w-[150px]">Lên lịch (VN)</th>
            <th className="w-[80px] text-right">Lần thử</th>
            <th className="w-[200px]">Lỗi gần nhất</th>
          </tr>
        </thead>
        <tbody>
          {items.length === 0 && (
            <tr>
              <td colSpan={8} className="text-center py-10" style={{ color: "var(--text-muted)" }}>
                Chưa có tin nào trong hàng đợi. Tin mới (đơn hàng, cảnh báo vận hành…) sẽ xuất hiện ở đây.
              </td>
            </tr>
          )}

          {items.map((item) => {
            const id = item.id ?? "";
            const dangMo = moId === id;
            const ds = events[id];
            const loiMa = item.lastErrorCode ?? null;
            const coLoiCauHinh =
              (loiMa && laLoiCauHinh(loiMa)) ||
              (ds ?? []).some((e) => e.errorCode && laLoiCauHinh(e.errorCode));

            return (
              <Fragment key={id}>
                <tr
                  onClick={() => void moDong(item)}
                  style={{ cursor: "pointer", background: dangMo ? "var(--bg-subtle)" : undefined }}
                >
                  <td style={{ color: "var(--text-muted)" }}>{dangMo ? "▾" : "▸"}</td>
                  <td className="font-mono text-[12px]">{item.recipientMasked || "—"}</td>
                  <td>{nhanKenh(item.channelKind)}</td>
                  <td className="text-[12px]">
                    {item.sourceEventType || (
                      <span style={{ color: "var(--text-muted)" }}>
                        {(item.templateId ?? "").slice(0, 8)}…
                      </span>
                    )}
                  </td>
                  <td>
                    <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ background: "var(--bg-subtle)" }}>
                      {nhanTrangThaiHangDoi(item.status)}
                    </span>
                  </td>
                  <td className="tabular">{dinhDangNgayGio(item.scheduledAtUtc)}</td>
                  <td className="text-right tabular">{item.attemptCount ?? 0}</td>
                  <td className="text-[11px]" style={{ color: loiMa ? "var(--danger)" : "var(--text-muted)" }}>
                    {loiMa ? <span className="font-mono">{loiMa}</span> : "—"}
                  </td>
                </tr>

                {dangMo && (
                  <tr>
                    <td colSpan={8} style={{ background: "var(--bg-subtle)" }}>
                      <div className="p-3 space-y-3 text-[12px]">
                        {coLoiCauHinh && (
                          <div
                            className="nh-card p-2"
                            style={{ color: "var(--warning)", borderColor: "var(--warning)" }}
                          >
                            ⚠️ {moTaLoiThongBao("eCommerce:Notification:0012")}
                          </div>
                        )}

                        {/* Nội dung đã dựng — bản đã mask */}
                        <div className="space-y-1">
                          <div style={{ color: "var(--text-muted)" }}>Nội dung đã dựng (đã che PII):</div>
                          <div className="font-medium">{item.renderedSubject || "(không có tiêu đề)"}</div>
                          {/* 🚨 textContent, KHÔNG dangerouslySetInnerHTML (D-199) */}
                          <pre
                            className="nh-card p-2"
                            style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", margin: 0 }}
                          >
                            {item.renderedBodyMasked || "(không có nội dung)"}
                          </pre>
                        </div>

                        {/* Timeline */}
                        <div>
                          <div className="font-semibold mb-1">Dòng thời gian (chỉ đọc)</div>
                          {dangTai === id && (
                            <div style={{ color: "var(--text-muted)" }}>Đang tải sự kiện…</div>
                          )}
                          {loiTheoId[id] && (
                            <div style={{ color: "var(--danger)" }}>{loiTheoId[id]}</div>
                          )}
                          {ds && ds.length === 0 && (
                            <div style={{ color: "var(--text-muted)" }}>Chưa có sự kiện nào.</div>
                          )}
                          {ds && ds.length > 0 && (
                            <table className="w-full text-[12px]">
                              <thead>
                                <tr className="text-left" style={{ color: "var(--text-muted)" }}>
                                  <th className="p-1 w-[150px]">Thời điểm (VN)</th>
                                  <th className="p-1 w-[150px]">Sự kiện</th>
                                  <th className="p-1 w-[200px]">Trạng thái</th>
                                  <th className="p-1">Mã lỗi / tương quan</th>
                                </tr>
                              </thead>
                              <tbody>
                                {ds.map((e) => (
                                  <tr key={e.id} className="border-t" style={{ borderColor: "var(--border)" }}>
                                    <td className="p-1 tabular">{dinhDangNgayGio(e.occurredAtUtc)}</td>
                                    <td className="p-1">{nhanLoaiSuKien(e.eventType)}</td>
                                    <td className="p-1">
                                      {e.fromStatus != null || e.toStatus != null
                                        ? `${e.fromStatus != null ? nhanTrangThaiHangDoi(e.fromStatus) : "—"} → ${
                                            e.toStatus != null ? nhanTrangThaiHangDoi(e.toStatus) : "—"
                                          }`
                                        : "—"}
                                    </td>
                                    <td className="p-1">
                                      {e.errorCode && (
                                        <span className="font-mono" style={{ color: "var(--danger)" }}>
                                          {e.errorCode}
                                        </span>
                                      )}
                                      {e.reasonMasked && <span> · {e.reasonMasked}</span>}
                                      {e.correlationId && (
                                        <span className="font-mono" style={{ color: "var(--text-muted)" }}>
                                          {" "}
                                          · corr {e.correlationId.slice(0, 12)}…
                                        </span>
                                      )}
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          )}
                        </div>

                        <div className="font-mono text-[10px]" style={{ color: "var(--text-muted)" }}>
                          correlationId mục: {item.correlationId || "—"}
                        </div>
                      </div>
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
