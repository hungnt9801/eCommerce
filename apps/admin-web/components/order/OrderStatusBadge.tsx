import { fulfillmentLabel, toneOf, type BadgeTone } from "@/lib/order";

const TONE_STYLE: Record<BadgeTone, { background: string; color: string }> = {
  success: { background: "#ecfdf5", color: "var(--success)" },
  danger: { background: "#fef2f2", color: "var(--danger)" },
  warn: { background: "#fffbeb", color: "#b45309" },
  info: { background: "#eff6ff", color: "#1d4ed8" },
  muted: { background: "var(--bg-subtle)", color: "var(--text-muted)" },
};

/**
 * **Một nhãn trạng thái** (đúng Nhanh): `statusLabel` do **server** suy từ 4 trục (`03 §3.5`) — UI **không**
 * tự ghép nhãn từ enum (hai bản ghép là mầm lệch: badge nói "Thành công" trong khi tab "Đang hoàn" đếm nó).
 * 📌 Màu suy từ **số trạng thái** (không từ chuỗi nhãn) — đổi câu chữ không làm badge đổi màu.
 */
export function OrderStatusBadge({
  label,
  status,
  isCancelled,
}: {
  label: string;
  status: number | null | undefined;
  isCancelled: boolean;
}) {
  const kieu = TONE_STYLE[toneOf(status, isCancelled)];

  return (
    <span className="text-[11px] px-2 py-0.5 rounded-full whitespace-nowrap" style={kieu}>
      {label || fulfillmentLabel(status)}
    </span>
  );
}

export default OrderStatusBadge;
