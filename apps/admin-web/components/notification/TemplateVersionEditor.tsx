"use client";

import { useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import {
  type NotificationTemplateDto,
  type PreviewResultDto,
  PLACEHOLDER_WHITELIST,
  kiemTraNoiDung,
  moTaLoiThongBao,
  nhanBodyFormat,
  nhanKenh,
  nhanMucDich,
  nhanTrangThaiTemplate,
  placeholdersSuDung,
  templateChuaPublish,
} from "@/lib/notification";

type Truong = "subject" | "body";

/**
 * **SOẠN & PUBLISH VERSION** — `NOT-001` / `WP-22` (SRS §10, `PublishVersionAsync` + `PreviewAsync`).
 *
 * 🚨 **Placeholder chèn từ whitelist** (chống `0015`): người dùng bấm nút để chèn `{{Ten}}`; gõ tay một tên
 * ngoài danh mục (hoặc `{{` chưa đóng) ⇒ **chặn publish** và báo lý do. Whitelist gửi kèm = **đúng** các
 * placeholder đã dùng (`placeholdersSuDung`) ⇒ body không bao giờ tham chiếu placeholder ngoài whitelist.
 *
 * 🚨 **`D-199`**: xem trước render bằng `<pre>{text}</pre>` (React ghi `textContent`) — **CẤM**
 * `dangerouslySetInnerHTML`. Nhãn rõ “Đây là bản xem trước — chưa gửi”.
 *
 * 🚨 **Bất biến sau publish**: hộp thoại xác nhận nêu rõ “sau khi publish, nội dung này không sửa được; muốn
 * đổi phải tạo version mới” (`PhienBanBatBien` = `0016`).
 *
 * ⚠️ **`docDeviation`:** API **không** có endpoint đọc version hiện tại ⇒ editor mở **trống** (không prefill).
 * `Preview` render **version đã publish gần nhất** (không phải bản nháp đang sửa) vì `PreviewAsync` cần
 * `templateVersionId`/`currentPublishedVersionId`. Template chưa publish ⇒ không xem trước được (báo rõ).
 */
export function TemplateVersionEditor({
  business,
  template,
  coQuyenPublish,
}: {
  business: string;
  template: NotificationTemplateDto;
  coQuyenPublish: boolean;
}) {
  const router = useRouter();
  const templateId = template.id ?? "";

  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [sampleData, setSampleData] = useState<Record<string, string>>({});

  const [truongDangChon, setTruongDangChon] = useState<Truong>("body");
  const subjectRef = useRef<HTMLInputElement>(null);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  const [dangPreview, setDangPreview] = useState(false);
  const [preview, setPreview] = useState<PreviewResultDto | null>(null);
  const [loiPreview, setLoiPreview] = useState<string | null>(null);

  const [hienXacNhan, setHienXacNhan] = useState(false);
  const [dangPublish, setDangPublish] = useState(false);
  const [loiPublish, setLoiPublish] = useState<string | null>(null);
  const [daPublish, setDaPublish] = useState<number | null>(null);

  const chuaPublish = templateChuaPublish(template);
  const kiemTra = useMemo(() => kiemTraNoiDung(subject, body), [subject, body]);
  const dungPlaceholder = useMemo(() => rutAll(subject, body), [subject, body]);

  const biChan = kiemTra.ngoaiWhitelist.length > 0 || kiemTra.ngoacNhonMo;
  const rong = subject.trim() === "" && body.trim() === "";

  function chenPlaceholder(ten: string) {
    const doan = `{{${ten}}}`;
    if (truongDangChon === "subject") {
      const el = subjectRef.current;
      const vt = el?.selectionStart ?? subject.length;
      setSubject((cu) => cu.slice(0, vt) + doan + cu.slice(vt));
      requestAnimationFrame(() => el?.focus());
    } else {
      const el = bodyRef.current;
      const vt = el?.selectionStart ?? body.length;
      setBody((cu) => cu.slice(0, vt) + doan + cu.slice(vt));
      requestAnimationFrame(() => el?.focus());
    }
  }

  async function xemTruoc() {
    if (dangPreview) return;
    setDangPreview(true);
    setLoiPreview(null);
    setPreview(null);

    try {
      const res = await fetch("/api/proxy/v1/notifications/templates/preview", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ templateId, sampleData }),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoiPreview(moTaLoiThongBao(failure.code) ?? failure.code ?? failure.message);
        return;
      }

      setPreview((await res.json()) as PreviewResultDto);
    } catch {
      setLoiPreview("Không gọi được API (mất mạng?).");
    } finally {
      setDangPreview(false);
    }
  }

  async function publish() {
    if (dangPublish || biChan) return;
    setDangPublish(true);
    setLoiPublish(null);

    const payload = {
      templateId,
      subject: subject.trim() === "" ? null : subject,
      bodyText: body,
      bodyFormat: 0,
      placeholders: placeholdersSuDung(subject, body),
    };

    try {
      const res = await fetch(
        `/api/proxy/v1/notifications/templates/${encodeURIComponent(templateId)}/versions`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        },
      );

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoiPublish(moTaLoiThongBao(failure.code) ?? (failure.code ? `${failure.code} — ${failure.message}` : failure.message));
        return;
      }

      const version = (await res.json()) as { version?: number };
      setDaPublish(version?.version ?? null);
      setHienXacNhan(false);
      // Cập nhật `currentPublishedVersionId` cho khối meta + mở khoá nút "Xem trước".
      router.refresh();
    } catch {
      setLoiPublish("Không gọi được API (mất mạng?).");
    } finally {
      setDangPublish(false);
    }
  }

  return (
    <div className="space-y-3">
      {/* Meta template */}
      <div className="nh-card p-3 text-[12px] flex flex-wrap gap-x-6 gap-y-1">
        <span>
          <span style={{ color: "var(--text-muted)" }}>Kênh:</span> {nhanKenh(template.channelKind)}
        </span>
        <span>
          <span style={{ color: "var(--text-muted)" }}>Ngôn ngữ:</span> {template.locale ?? "—"}
        </span>
        <span>
          <span style={{ color: "var(--text-muted)" }}>Mục đích:</span> {nhanMucDich(template.purpose)}
        </span>
        <span>
          <span style={{ color: "var(--text-muted)" }}>Trạng thái:</span> {nhanTrangThaiTemplate(template.status)}
        </span>
        <span>
          <span style={{ color: "var(--text-muted)" }}>Định dạng:</span> {nhanBodyFormat(0)}
        </span>
        <span>
          <span style={{ color: "var(--text-muted)" }}>Version đang dùng:</span>{" "}
          {template.currentPublishedVersionId ? (
            <span className="font-mono">{template.currentPublishedVersionId.slice(0, 8)}…</span>
          ) : (
            <b style={{ color: "var(--warning)" }}>— chưa publish</b>
          )}
        </span>
      </div>

      {chuaPublish && (
        <div
          className="nh-card p-3 text-[12px]"
          style={{ color: "var(--warning)", borderColor: "var(--warning)" }}
        >
          ⚠️ Mẫu này <b>chưa có version Published</b> — đây là <b>lỗi cấu hình</b> (tin sẽ bị từ chối bằng mã{" "}
          <span className="font-mono">0012</span>). Soạn nội dung và bấm <b>Publish version</b> để khắc phục.
          Chưa publish thì <b>không xem trước được</b> (renderer cần một version đã lưu).
        </div>
      )}

      {/* Soạn nội dung */}
      <div className="nh-card p-3 space-y-2">
        <div className="text-[13px] font-semibold">Nội dung version mới</div>

        <label className="text-[12px] block">
          <div style={{ color: "var(--text-muted)" }}>Tiêu đề (Subject)</div>
          <input
            ref={subjectRef}
            className="nh-input mt-0.5 w-full"
            value={subject}
            maxLength={512}
            onFocus={() => setTruongDangChon("subject")}
            onChange={(e) => setSubject(e.target.value)}
            placeholder="vd: Xác nhận đơn {{MaDon}}"
          />
        </label>

        <label className="text-[12px] block">
          <div style={{ color: "var(--text-muted)" }}>Nội dung (BodyText)</div>
          <textarea
            ref={bodyRef}
            className="nh-input mt-0.5 w-full"
            rows={8}
            value={body}
            onFocus={() => setTruongDangChon("body")}
            onChange={(e) => setBody(e.target.value)}
            placeholder="Gõ nội dung, chèn placeholder bằng nút bên dưới…"
            style={{ fontFamily: "inherit", resize: "vertical" }}
          />
        </label>

        {/* Bảng placeholder whitelist */}
        <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          Chèn placeholder vào <b>{truongDangChon === "subject" ? "tiêu đề" : "nội dung"}</b> (bấm vào ô trước
          để chọn nơi chèn):
        </div>
        <div className="flex flex-wrap gap-1.5">
          {PLACEHOLDER_WHITELIST.map((p) => (
            <button
              key={p.name}
              type="button"
              className="nh-btn text-[12px]"
              onClick={() => chenPlaceholder(p.name ?? "")}
              title={`Chèn {{${p.name}}}`}
            >
              <span className="font-mono">{`{{${p.name}}}`}</span>
              <span className="ml-1" style={{ color: "var(--text-muted)" }}>
                · {kieuLabel(p.kind)}
              </span>
            </button>
          ))}
        </div>

        {biChan && (
          <div className="text-[12px]" style={{ color: "var(--danger)" }}>
            ⛔ Không thể publish:{" "}
            {kiemTra.ngoaiWhitelist.length > 0 && (
              <>placeholder ngoài danh mục: {kiemTra.ngoaiWhitelist.map((x) => `{{${x}}}`).join(", ")}. </>
            )}
            {kiemTra.ngoacNhonMo && <>có dấu <span className="font-mono">{"{{"}</span> chưa đóng. </>}
            Hãy chèn placeholder từ danh sách thay vì gõ tay (chống mã <span className="font-mono">0015</span>).
          </div>
        )}
      </div>

      {/* Dữ liệu mẫu để xem trước */}
      <div className="nh-card p-3 space-y-2">
        <div className="text-[13px] font-semibold">Dữ liệu mẫu (chỉ để xem trước — KHÔNG gửi, KHÔNG ghi DB)</div>
        {dungPlaceholder.length === 0 ? (
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Chèn placeholder vào nội dung để có ô nhập dữ liệu mẫu.
          </div>
        ) : (
          <div className="flex flex-wrap gap-2">
            {dungPlaceholder.map((ten) => (
              <label key={ten} className="text-[12px]">
                <div className="font-mono" style={{ color: "var(--text-muted)" }}>{`{{${ten}}}`}</div>
                <input
                  className="nh-input mt-0.5"
                  value={sampleData[ten] ?? ""}
                  onChange={(e) => setSampleData((cu) => ({ ...cu, [ten]: e.target.value }))}
                  style={{ width: 160 }}
                />
              </label>
            ))}
          </div>
        )}

        <div className="flex items-center gap-2">
          <button
            type="button"
            className="nh-btn"
            onClick={() => void xemTruoc()}
            disabled={dangPreview || chuaPublish}
            title={chuaPublish ? "Chưa publish nên chưa xem trước được" : "Render version đã publish gần nhất"}
          >
            {dangPreview ? "Đang render…" : "Xem trước"}
          </button>
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Render <b>version đã publish gần nhất</b> (không phải bản nháp đang sửa).
          </span>
        </div>

        {loiPreview && (
          <div className="text-[12px]" style={{ color: "var(--danger)" }}>
            {loiPreview}
          </div>
        )}

        {preview && (
          <div className="space-y-1 border-t pt-2" style={{ borderColor: "var(--border)" }}>
            <div
              className="text-[11px] font-semibold px-2 py-1 rounded"
              style={{ background: "var(--bg-subtle)", color: "var(--text-muted)" }}
            >
              Đây là bản xem trước — chưa gửi
            </div>
            <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
              Tiêu đề:
            </div>
            <div className="text-[13px] font-medium">{preview.subject || "—"}</div>
            <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
              Nội dung:
            </div>
            {/* 🚨 textContent, KHÔNG dangerouslySetInnerHTML (D-199) */}
            <pre
              className="nh-card p-3 text-[13px]"
              style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", margin: 0 }}
            >
              {preview.body || "—"}
            </pre>
            {preview.bodyFingerprint && (
              <div className="text-[10px] font-mono" style={{ color: "var(--text-muted)" }}>
                fingerprint: {preview.bodyFingerprint.slice(0, 16)}…
              </div>
            )}
          </div>
        )}
      </div>

      {/* Publish */}
      <div className="nh-card p-3 space-y-2">
        <div className="text-[13px] font-semibold">Publish version</div>
        <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          Sau khi publish, nội dung này <b>không sửa được</b>; muốn đổi phải tạo version mới.
        </div>

        {daPublish != null && (
          <div className="text-[12px]" style={{ color: "var(--success)" }}>
            Đã publish version <b>{daPublish}</b>.
          </div>
        )}

        {loiPublish && (
          <div className="text-[12px]" style={{ color: "var(--danger)" }}>
            {loiPublish}
          </div>
        )}

        <div className="flex items-center gap-2">
          <button
            type="button"
            className="nh-btn nh-btn-primary"
            onClick={() => setHienXacNhan(true)}
            disabled={!coQuyenPublish || dangPublish || biChan || rong}
            title={
              !coQuyenPublish
                ? "Cần quyền Notification.Templates.Publish"
                : biChan
                  ? "Sửa lỗi placeholder trước khi publish"
                  : rong
                    ? "Nhập tiêu đề hoặc nội dung trước"
                    : "Publish version mới"
            }
          >
            Publish version
          </button>
          {!coQuyenPublish && (
            <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
              Cần quyền <span className="font-mono">Notification.Templates.Publish</span>.
            </span>
          )}
        </div>
      </div>

      {/* Hộp thoại xác nhận bất biến */}
      {hienXacNhan && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          style={{ background: "rgba(0,0,0,0.35)" }}
        >
          <div className="nh-card p-4 max-w-[520px] w-full space-y-3">
            <div className="text-[15px] font-semibold">Xác nhận publish version</div>
            <div className="text-[13px]">
              Sau khi publish, nội dung này <b>không sửa được</b>; muốn đổi phải tạo version mới.
            </div>
            <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
              Placeholder gửi kèm:{" "}
              {placeholdersSuDung(subject, body)
                .map((p) => `{{${p.name}}}`)
                .join(", ") || "không có"}
            </div>
            <div className="flex items-center justify-end gap-2">
              <button
                type="button"
                className="nh-btn"
                onClick={() => setHienXacNhan(false)}
                disabled={dangPublish}
              >
                Huỷ
              </button>
              <button
                type="button"
                className="nh-btn nh-btn-primary"
                onClick={() => void publish()}
                disabled={dangPublish || biChan}
              >
                {dangPublish ? "Đang publish…" : "Xác nhận publish"}
              </button>
            </div>
          </div>
        </div>
      )}

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        📌 Mẫu: <b>{template.code}</b>
      </div>
    </div>
  );
}

/** Rút tên placeholder trong subject+body (giữ thứ tự) — dùng cho ô dữ liệu mẫu. */
function rutAll(subject: string, body: string): string[] {
  const out: string[] = [];
  for (const text of [subject, body]) {
    let i = 0;
    while (i < text.length) {
      const mo = text.indexOf("{{", i);
      if (mo < 0) break;
      const dong = text.indexOf("}}", mo + 2);
      if (dong < 0) break;
      const ten = text.slice(mo + 2, dong).trim();
      if (ten && !out.includes(ten)) out.push(ten);
      i = dong + 2;
    }
  }
  return out;
}

function kieuLabel(kind: number | undefined): string {
  switch (kind) {
    case 1:
      return "Tiền";
    case 2:
      return "Ngày giờ";
    case 3:
      return "Số";
    case 4:
      return "Liên kết";
    default:
      return "Văn bản";
  }
}
