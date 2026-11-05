import Link from "next/link";
import { apiGetSafe } from "@/lib/api";
import { dateTime, money, qty } from "@/lib/format";
import { paymentLabel, type RetailBillDto } from "@/lib/pos";
import type { WarehouseDto } from "@/lib/stock";
import { PrintButton } from "@/components/inventory/PrintButton";

/**
 * **In hoá đơn bán lẻ** (`/{business}/print/bill/{id}`) — `WP-06` · màn #12 "in (route `/print/bill/[id]`)".
 *
 * 📌 Trang **in** tách khỏi màn POS: mở ở **cửa sổ mới** (`F10` → `window.open`) với khổ hẹp để máy in
 * bill nhiệt không phải cắt lại. `PrintButton` gọi `window.print()` (`components/inventory/PrintButton.tsx`).
 * 🔒 **Mọi số tiền đọc từ DTO của server** — KHÔNG tính lại ở đây: hoá đơn đã in phải khớp đúng chứng từ.
 */
export default async function PrintBillPage({
  params,
}: {
  params: Promise<{ business: string; id: string }>;
}) {
  const { business, id } = await params;

  const hoaDon = await apiGetSafe<RetailBillDto | null>(`/api/v1/pos/bills/${id}`, null);
  const kho = await apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []);
  const tenKho = (warehouseId: string | null | undefined): string => {
    const k = kho.find((x) => x.id === warehouseId);
    return k ? `${k.code} — ${k.name}` : "—";
  };

  if (!hoaDon) {
    return (
      <div className="p-6 space-y-2">
        <div className="text-[15px] font-semibold">Không mở được hoá đơn</div>
        <p className="text-[13px]" style={{ color: "var(--danger)" }}>
          Hoá đơn không tồn tại hoặc bạn không có quyền <code>eCommerce.Pos</code>.
        </p>
        <Link className="nh-btn inline-flex" href={`/${business}/pos/bill/list`}>
          Về danh sách
        </Link>
      </div>
    );
  }

  return (
    <div className="p-4" style={{ maxWidth: 380, margin: "0 auto" }}>
      <div className="flex justify-end mb-2 print:hidden">
        <PrintButton />
      </div>

      <div className="text-center space-y-1">
        <div className="text-[16px] font-semibold">HOÁ ĐƠN BÁN LẺ</div>
        <div className="text-[12px] tabular">{hoaDon.number}</div>
        <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          {dateTime(hoaDon.creationTime)}
        </div>
        <div className="text-[11px]">{tenKho(hoaDon.warehouseId)}</div>
      </div>

      {hoaDon.status === "Voided" && (
        <div className="text-center text-[12px] font-semibold mt-2" style={{ color: "var(--danger)" }}>
          *** ĐÃ HUỶ ***
          {hoaDon.voidReason ? <div>{hoaDon.voidReason}</div> : null}
        </div>
      )}

      <table className="w-full text-[11px] mt-3">
        <thead>
          <tr style={{ borderBottom: "1px solid var(--border)" }}>
            <th className="text-left py-1">Tên</th>
            <th className="text-right py-1">SL</th>
            <th className="text-right py-1">Giá</th>
            <th className="text-right py-1">T.Tiền</th>
          </tr>
        </thead>
        <tbody>
          {(hoaDon.lines ?? []).map((d) => (
            <tr key={d.id}>
              <td className="py-0.5">
                {d.productName ?? d.sku}
                {Number(d.discountAmount ?? 0) > 0 ? (
                  <div className="text-[10px]" style={{ color: "var(--text-muted)" }}>
                    CK {money(d.discountAmount)} đ
                  </div>
                ) : null}
              </td>
              <td className="text-right tabular py-0.5">{qty(d.quantity)}</td>
              <td className="text-right tabular py-0.5">{money(d.unitPrice)}</td>
              <td className="text-right tabular py-0.5">{money(d.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="mt-2 pt-2 text-[11px] space-y-0.5" style={{ borderTop: "1px dashed var(--border)" }}>
        <Dong nhan="Tiền hàng" gt={money(hoaDon.subtotal)} />
        {Number(hoaDon.lineDiscountTotal ?? 0) > 0 && <Dong nhan="Chiết khấu dòng" gt={`−${money(hoaDon.lineDiscountTotal)}`} />}
        {Number(hoaDon.discountTotal ?? 0) > 0 && <Dong nhan="Chiết khấu HĐ" gt={`−${money(hoaDon.discountTotal)}`} />}
        {Number(hoaDon.vatTotal ?? 0) > 0 && <Dong nhan="VAT" gt={money(hoaDon.vatTotal)} />}
        {Number(hoaDon.pointsUsedAmount ?? 0) > 0 && (
          <Dong nhan={`Tiêu điểm (${qty(hoaDon.pointsUsed)})`} gt={`−${money(hoaDon.pointsUsedAmount)}`} />
        )}

        <div className="flex justify-between font-semibold text-[13px] pt-1">
          <span>PHẢI TRẢ</span>
          <span className="tabular">{money(hoaDon.totalPayment)} đ</span>
        </div>

        {(hoaDon.payments ?? []).map((p) => (
          <Dong key={p.id} nhan={paymentLabel(p.method)} gt={money(p.amount)} />
        ))}

        {Number(hoaDon.changeAmount ?? 0) > 0 && <Dong nhan="Tiền thối" gt={money(hoaDon.changeAmount)} />}
        {Number(hoaDon.debtAmount ?? 0) > 0 && (
          <Dong nhan="Còn nợ" gt={`${money(hoaDon.debtAmount)} (hạn ${dateTime(hoaDon.dueDateUtc)})`} />
        )}
      </div>

      {hoaDon.note && (
        <div className="mt-2 text-[11px]">
          <div style={{ color: "var(--text-muted)" }}>Ghi chú</div>
          <div className="whitespace-pre-wrap">{hoaDon.note}</div>
        </div>
      )}

      <div className="text-center text-[10px] mt-3" style={{ color: "var(--text-muted)" }}>
        Cảm ơn quý khách — hẹn gặp lại!
      </div>
    </div>
  );
}

function Dong({ nhan, gt }: { nhan: string; gt: string }) {
  return (
    <div className="flex justify-between">
      <span style={{ color: "var(--text-muted)" }}>{nhan}</span>
      <span className="tabular">{gt}</span>
    </div>
  );
}
