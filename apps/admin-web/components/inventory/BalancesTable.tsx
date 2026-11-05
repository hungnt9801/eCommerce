import { qty } from "@/lib/format";
import type { Balance } from "@/lib/stock";
import { StockBadge } from "./Badges";

/**
 * Bảng **tồn kho 9 chiều** (Nhanh: cột tồn của màn kho). Dùng ở màn `/{business}/product/inventory`
 * và ở phần "Tồn hiện tại" của chi tiết phiếu.
 *
 * 🔒 **Mọi số lấy từ API** — kể cả `InWarehouse`/`Available`; component này **không** tính lại công thức
 * `D-27`. Nếu UI tự tính, hai nơi có thể lệch nhau và không biết tin nơi nào.
 * 📌 4 chiều `Đang chuyển`/`Đặt trước`/`Chờ nhập`/`BH giữ` **chưa có nghiệp vụ sinh** (thuộc WP-05/POS) ⇒
 * luôn 0; màn hình ghi rõ điều đó thay vì để người dùng tin là "đã theo dõi".
 */
export function BalancesTable({
  rows,
  showWarehouse = true,
  emptyText = "Chưa có dòng tồn kho nào.",
}: {
  rows: Balance[];
  showWarehouse?: boolean;
  emptyText?: string;
}) {
  const soCot = showWarehouse ? 13 : 12;

  return (
    <div className="nh-card overflow-auto">
      <table className="nh-table">
        <thead>
          <tr>
            {showWarehouse && <th className="w-[160px]">Kho</th>}
            <th className="w-[180px]">SKU</th>
            <th className="w-[130px]">Tồn / Giữ / Bán</th>
            <th className="w-[90px] text-right" title="Tồn trong kho = Tồn − Đang chuyển">
              Trong kho
            </th>
            <th className="w-[90px] text-right" title="Đang chuyển kho (chưa có nghiệp vụ sinh — WP-05)">
              Đang chuyển
            </th>
            <th className="w-[90px] text-right" title="Đang giao (đã rời kho)">
              Đang giao
            </th>
            <th className="w-[90px] text-right" title="Tạm giữ (còn trong kho, không bán được)">
              Tạm giữ
            </th>
            <th className="w-[80px] text-right" title="Hàng lỗi">
              Lỗi
            </th>
            <th className="w-[80px] text-right" title="Đang bảo hành (đã rời kho)">
              Bảo hành
            </th>
            <th className="w-[90px] text-right" title="Tạm giữ linh kiện bảo hành (chưa có nghiệp vụ sinh)">
              BH giữ
            </th>
            <th className="w-[90px] text-right" title="Đặt trước (chưa có nghiệp vụ sinh)">
              Đặt trước
            </th>
            <th className="w-[90px] text-right" title="Chờ nhập (chưa có nghiệp vụ sinh)">
              Chờ nhập
            </th>
            <th className="w-[110px] text-right" title="Có thể bán = Tồn − Giữ − Chuyển − Tạm giữ − Lỗi">
              Có thể bán
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && (
            <tr>
              <td colSpan={soCot} className="text-center py-10" style={{ color: "var(--text-muted)" }}>
                {emptyText}
              </td>
            </tr>
          )}

          {rows.map((dong) => (
            <tr key={`${dong.warehouseId}-${dong.sku}`}>
              {showWarehouse && (
                <td style={{ color: "var(--text-muted)" }} className="text-[13px]">
                  <div className="font-medium" style={{ color: "var(--text)" }}>
                    {dong.warehouseCode || "—"}
                  </div>
                  <div className="text-[11px]">{dong.warehouseName}</div>
                </td>
              )}
              <td className="font-medium">{dong.sku}</td>
              <td>
                <StockBadge onHand={dong.onHand} reserved={dong.reserved} available={dong.available} />
              </td>
              <td className="text-right tabular">{qty(dong.inWarehouse)}</td>
              <td className="text-right tabular">{qty(dong.transferring)}</td>
              <td className="text-right tabular">{qty(dong.shipping)}</td>
              <td className="text-right tabular">{qty(dong.holding)}</td>
              <td className="text-right tabular">{qty(dong.damaged)}</td>
              <td className="text-right tabular">{qty(dong.warranty)}</td>
              <td className="text-right tabular">{qty(dong.warrantyHolding)}</td>
              <td className="text-right tabular">{qty(dong.preOrder)}</td>
              <td className="text-right tabular">{qty(dong.waitingInbound)}</td>
              <td
                className="text-right tabular font-semibold"
                style={{ color: dong.available < 0 ? "var(--danger)" : "var(--success)" }}
              >
                {qty(dong.available)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
