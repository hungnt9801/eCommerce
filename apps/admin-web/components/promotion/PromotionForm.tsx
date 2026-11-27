"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import { type CreatePromotionDto, moTaLoiPromotion } from "@/lib/promotion";

/**
 * **TẠO KHUYẾN MÃI** — `PRO-002` / `W24-3` (SRS §10.1 `CreateAsync`).
 *
 * 🚨 **Ẩn `Kind = FlashSale`** (`D-156`) và **`DiscountKind = FreeShipping`** (`D-158`): BE trả validation
 * `400` cho hai lựa chọn này ở v1 ⇒ form **không** cho chọn (vẫn để BE làm cửa chặn thật).
 *
 * 🚨 **Chống bấm lặp**: nút "Tạo khuyến mãi" `disabled` khi đang gửi (single-flight). Endpoint tạo **không**
 * nhận `IdempotencyKey` (khác lô gửi) ⇒ khoá nút + `disabled` là tầng chống nhân đôi.
 *
 * 📌 Lỗi hiện **theo mã** (câu tiếng Việt), **không** dán mã `eCommerce:Promotion:00xx` thô.
 */
export function PromotionForm({ business }: { business: string }) {
  const router = useRouter();
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);

  async function tao(form: FormData) {
    if (dangGui) return;

    const code = str(form, "code").trim();
    const name = str(form, "name").trim();
    const startsAtLocal = str(form, "startsAtLocal");
    const endsAtLocal = str(form, "endsAtLocal");

    if (!code || !name) {
      setLoi("Mã và tên khuyến mãi là bắt buộc.");
      return;
    }
    if (!startsAtLocal) {
      setLoi("Cần chọn thời điểm bắt đầu.");
      return;
    }
    if (endsAtLocal && new Date(endsAtLocal) <= new Date(startsAtLocal)) {
      setLoi("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
      return;
    }

    setDangGui(true);
    setLoi(null);

    const body: CreatePromotionDto = {
      code,
      name,
      // 0 Mã giảm giá · 1 Khuyến mãi tự động (KHÔNG có FlashSale — D-156).
      kind: Number(str(form, "kind") || "1") as CreatePromotionDto["kind"],
      isCumulative: form.get("isCumulative") != null,
      priority: Number(str(form, "priority") || "0"),
      channelScope: nullable(str(form, "channelScope")),
      startsAtUtc: new Date(startsAtLocal).toISOString(),
      endsAtUtc: endsAtLocal ? new Date(endsAtLocal).toISOString() : null,
      // 0 Phần trăm · 1 Số tiền cố định (KHÔNG có FreeShipping — D-158).
      discountKind: Number(str(form, "discountKind") || "1") as CreatePromotionDto["discountKind"],
      discountValue: Number(str(form, "discountValue") || "0"),
      maxDiscountAmount: numOrNull(str(form, "maxDiscountAmount")),
      minOrderAmount: numOrNull(str(form, "minOrderAmount")),
      currency: (str(form, "currency") || "VND").toUpperCase(),
      maxUsageTotal: intOrNull(str(form, "maxUsageTotal")),
      maxUsagePerCustomer: intOrNull(str(form, "maxUsagePerCustomer")),
    };

    try {
      const res = await fetch("/api/proxy/v1/promotion", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(moTaLoiPromotion(failure.code) ?? failure.message);
        return;
      }

      const created = (await res.json()) as { id?: string };
      if (!created?.id) {
        setLoi("API không trả id khuyến mãi vừa tạo — kiểm tra hợp đồng OpenAPI.");
        return;
      }

      router.push(`/${business}/promotion/${created.id}`);
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <form className="nh-card p-3 space-y-3 max-w-[860px]" action={(fd) => void tao(fd)}>
      <div className="text-[13px] font-semibold">Thông tin khuyến mãi</div>

      <div className="flex flex-wrap items-end gap-3">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Mã khuyến mãi *</div>
          <input
            className="nh-input mt-0.5 font-mono"
            name="code"
            required
            maxLength={64}
            placeholder="vd: SALE-T11"
            style={{ width: 200 }}
          />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Tên hiển thị *</div>
          <input
            className="nh-input mt-0.5"
            name="name"
            required
            maxLength={256}
            placeholder="vd: Giảm 10% đơn từ 500k"
            style={{ width: 320 }}
          />
        </label>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Loại</div>
          <select className="nh-input mt-0.5" name="kind" defaultValue="1" style={{ width: 200 }}>
            <option value="0">Mã giảm giá (Voucher)</option>
            <option value="1">Khuyến mãi tự động</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Kiểu giảm</div>
          <select className="nh-input mt-0.5" name="discountKind" defaultValue="1" style={{ width: 180 }}>
            <option value="0">Phần trăm</option>
            <option value="1">Số tiền cố định</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Giá trị giảm *</div>
          <input
            className="nh-input mt-0.5"
            name="discountValue"
            type="number"
            min={0}
            step="0.01"
            required
            defaultValue={0}
            style={{ width: 160 }}
          />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Tiền tệ</div>
          <input className="nh-input mt-0.5 font-mono" name="currency" defaultValue="VND" maxLength={3} style={{ width: 90 }} />
        </label>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Giảm tối đa (tuỳ chọn)</div>
          <input
            className="nh-input mt-0.5"
            name="maxDiscountAmount"
            type="number"
            min={0}
            step="0.01"
            placeholder="không giới hạn"
            style={{ width: 180 }}
          />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Đơn tối thiểu (tuỳ chọn)</div>
          <input
            className="nh-input mt-0.5"
            name="minOrderAmount"
            type="number"
            min={0}
            step="0.01"
            placeholder="không yêu cầu"
            style={{ width: 180 }}
          />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Thứ tự ưu tiên</div>
          <input className="nh-input mt-0.5" name="priority" type="number" step="1" defaultValue={0} style={{ width: 120 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Phạm vi kênh (tuỳ chọn)</div>
          <input
            className="nh-input mt-0.5"
            name="channelScope"
            maxLength={64}
            placeholder="vd: web"
            style={{ width: 160 }}
          />
        </label>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Bắt đầu *</div>
          <input className="nh-input mt-0.5" name="startsAtLocal" type="datetime-local" required style={{ width: 210 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Kết thúc (tuỳ chọn)</div>
          <input className="nh-input mt-0.5" name="endsAtLocal" type="datetime-local" style={{ width: 210 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Trần tổng lượt (tuỳ chọn)</div>
          <input
            className="nh-input mt-0.5"
            name="maxUsageTotal"
            type="number"
            min={1}
            step="1"
            placeholder="không giới hạn"
            style={{ width: 170 }}
          />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Trần lượt/khách (tuỳ chọn)</div>
          <input
            className="nh-input mt-0.5"
            name="maxUsagePerCustomer"
            type="number"
            min={1}
            step="1"
            placeholder="không giới hạn"
            style={{ width: 170 }}
          />
        </label>

        <label className="text-[12px] flex items-center gap-1.5 self-end pb-1.5">
          <input type="checkbox" name="isCumulative" />
          <span>Cộng dồn với khuyến mãi khác</span>
        </label>
      </div>

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        📌 Giai đoạn này <b>ẩn</b> loại <b>Flash Sale</b> (<code>D-156</code>) và kiểu <b>Miễn phí vận chuyển</b>{" "}
        (<code>D-158</code>): BE trả lỗi kiểm tra nếu chọn. Các trường giá trị/giới hạn chỉ đặt được lúc tạo
        (<code>D-150</code> — chưa có đường sửa ở v1).
      </div>

      {loi && (
        <div className="text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      <div className="flex items-center gap-2">
        <button className="nh-btn nh-btn-primary" type="submit" disabled={dangGui}>
          {dangGui ? "Đang tạo…" : "Tạo khuyến mãi"}
        </button>
        <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          Khuyến mãi tạo ra ở trạng thái <b>Nháp</b> — dựng cây điều kiện rồi mới duyệt được.
        </span>
      </div>
    </form>
  );
}

function str(form: FormData, key: string): string {
  const v = form.get(key);
  return typeof v === "string" ? v : "";
}

function nullable(s: string): string | null {
  const t = s.trim();
  return t === "" ? null : t;
}

function numOrNull(s: string): number | null {
  const t = s.trim();
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

function intOrNull(s: string): number | null {
  const t = s.trim();
  if (t === "") return null;
  const n = Number.parseInt(t, 10);
  return Number.isFinite(n) ? n : null;
}
