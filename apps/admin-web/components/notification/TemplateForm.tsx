"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import {
  type CreateTemplateDto,
  type NotificationTemplateDto,
  moTaLoiThongBao,
} from "@/lib/notification";

/**
 * **SOẠN MẪU THÔNG BÁO MỚI** — `NOT-001` / `WP-22` (SRS §10, `CreateAsync`).
 *
 * 🚨 **Định dạng thân tin chỉ `Text`**: GĐ3 BE **NÉM `0020`** nếu `BodyFormat = Html` (`D-199`) ⇒ form
 * không cho chọn HTML (hiện khoá + ghi lý do). `BodyFormat` gửi số `0` (Text) — số là hợp đồng dữ liệu.
 *
 * 🚨 **Chống bấm lặp**: nút "Tạo mẫu" `disabled` khi đang gửi (single-flight). Endpoint tạo template **không**
 * nhận `IdempotencyKey` (khác endpoint lô) nên tầng bảo vệ là khoá nút + `disabled`, đủ để không tạo 2 mẫu
 * trùng do bấm đúp; nếu cần chống trùng mạnh hơn phải bổ sung hợp đồng BE (ghi `docDeviation`).
 */
export function TemplateForm({ business }: { business: string }) {
  const router = useRouter();
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);

  async function tao(form: FormData) {
    if (dangGui) return;
    setDangGui(true);
    setLoi(null);

    const body: CreateTemplateDto = {
      code: ((form.get("code") as string) || "").trim(),
      name: ((form.get("name") as string) || "").trim(),
      channelKind: Number(form.get("channelKind") ?? 0) as CreateTemplateDto["channelKind"],
      locale: ((form.get("locale") as string) || "vi").trim() || "vi",
      purpose: Number(form.get("purpose") ?? 0) as CreateTemplateDto["purpose"],
      // GĐ3 chỉ Text (0). Không cho chọn HTML — BE ném 0020.
      bodyFormat: 0,
    };

    if (!body.code || !body.name) {
      setLoi("Mã và tên mẫu là bắt buộc.");
      setDangGui(false);
      return;
    }

    try {
      const res = await fetch("/api/proxy/v1/notifications/templates", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        const moTa = moTaLoiThongBao(failure.code);
        setLoi(moTa ?? (failure.code ? `${failure.code} — ${failure.message}` : failure.message));
        return;
      }

      const created = (await res.json()) as NotificationTemplateDto;
      if (!created?.id) {
        setLoi("API không trả id mẫu vừa tạo — kiểm tra hợp đồng OpenAPI.");
        return;
      }

      // Sang màn soạn version để publish — mẫu mới ở trạng thái Nháp, CHƯA publish.
      router.push(`/${business}/notifications/templates/${created.id}`);
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <form className="nh-card p-3 space-y-3 max-w-[720px]" action={(fd) => void tao(fd)}>
      <div className="text-[13px] font-semibold">Thông tin mẫu</div>

      <div className="flex flex-wrap items-end gap-3">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Mã mẫu *</div>
          <input
            className="nh-input mt-0.5"
            name="code"
            required
            maxLength={64}
            placeholder="vd: order-confirmation"
            style={{ width: 220 }}
          />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Tên hiển thị *</div>
          <input
            className="nh-input mt-0.5"
            name="name"
            required
            maxLength={256}
            placeholder="vd: Xác nhận đơn hàng"
            style={{ width: 260 }}
          />
        </label>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Kênh</div>
          <select className="nh-input mt-0.5" name="channelKind" defaultValue="0" style={{ width: 150 }}>
            <option value="0">Email</option>
            <option value="1">SMS</option>
            <option value="2">Zalo OA</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Ngôn ngữ</div>
          <input className="nh-input mt-0.5" name="locale" defaultValue="vi" maxLength={8} style={{ width: 90 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Mục đích</div>
          <select className="nh-input mt-0.5" name="purpose" defaultValue="0" style={{ width: 190 }}>
            <option value="0">Giao dịch</option>
            <option value="1">Marketing</option>
            <option value="2">Cảnh báo vận hành</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Định dạng thân tin</div>
          <input className="nh-input mt-0.5" value="Văn bản (Text)" readOnly disabled style={{ width: 160 }} />
        </label>
      </div>

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        📌 GĐ3 chỉ hỗ trợ <b>Văn bản (Text)</b>; HTML bị BE từ chối bằng mã{" "}
        <span className="font-mono">0020</span> (<code>D-199</code>).
      </div>

      {loi && (
        <div className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      <div className="flex items-center gap-2">
        <button className="nh-btn nh-btn-primary" type="submit" disabled={dangGui}>
          {dangGui ? "Đang tạo…" : "Tạo mẫu"}
        </button>
        <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          Mẫu tạo ra ở trạng thái <b>Nháp</b> — cần publish một version trước khi gửi.
        </span>
      </div>
    </form>
  );
}
