import { qty } from "@/lib/format";
import { DIRECTION_LABEL, STATUS_LABEL } from "@/lib/stock";

/**
 * **Cột "Tồn" 3 số** (04 §6.1): Tồn · Tạm giữ · Có thể bán — số **âm in đỏ**.
 * 📌 Ba số này lấy **nguyên** từ `StockBalanceDto` (`onHand`/`reserved`/`available`): UI **không** tự tính
 * `Available` — hai nơi cùng tính một công thức là mầm lệch số (`D-27`).
 */
export function StockBadge({
  onHand,
  reserved,
  available,
}: {
  onHand: number;
  reserved: number;
  available: number;
}) {
  return (
    <div className="leading-tight tabular" title="Tồn / Tạm giữ / Có thể bán">
      <div
        className="text-[14px] font-semibold"
        style={{ color: onHand < 0 ? "var(--danger)" : undefined }}
      >
        {qty(onHand)}
      </div>
      <div className="text-[11px] flex items-center gap-2 mt-0.5">
        <span style={{ color: "var(--text-muted)" }}>Giữ {qty(reserved)}</span>
        <span style={{ color: available < 0 ? "var(--danger)" : "var(--success)" }}>Bán {qty(available)}</span>
      </div>
    </div>
  );
}

/** Nhãn trạng thái phiếu: Nháp (xám) · Đã ghi sổ (xanh) · Đã huỷ (đỏ nhạt). */
export function StatusBadge({ status }: { status: string }) {
  const kieu =
    status === "Posted"
      ? { background: "#ecfdf5", color: "var(--success)" }
      : status === "Cancelled"
        ? { background: "#fef2f2", color: "var(--danger)" }
        : { background: "var(--bg-subtle)", color: "var(--text-muted)" };

  return (
    <span className="text-[11px] px-2 py-0.5 rounded-full whitespace-nowrap" style={kieu}>
      {STATUS_LABEL[status] ?? status}
    </span>
  );
}

/** Chiều phiếu: Nhập (xanh) · Xuất (cam) · kiểm kê không có chiều (—). */
export function DirectionBadge({ direction }: { direction: string | null }) {
  if (!direction) {
    return (
      <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ background: "var(--bg-subtle)", color: "var(--text-muted)" }}>
        Không chiều
      </span>
    );
  }

  return (
    <span
      className="text-[11px] px-2 py-0.5 rounded-full whitespace-nowrap"
      style={
        direction === "In"
          ? { background: "#ecfdf5", color: "var(--success)" }
          : { background: "#fff7ed", color: "var(--warning)" }
      }
    >
      {DIRECTION_LABEL[direction] ?? direction}
    </span>
  );
}
