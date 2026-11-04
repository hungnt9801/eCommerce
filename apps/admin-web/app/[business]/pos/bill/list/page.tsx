import Link from "next/link";
import { ApiError, apiGetSafe } from "@/lib/api";
import { dateTime, money } from "@/lib/format";
import { BILL_STATUS_LABEL, paymentIcon, toPage, type RetailBillDto, type Page } from "@/lib/pos";
import type { WarehouseDto } from "@/lib/stock";
import { VoidBillButton } from "@/components/pos/VoidBillButton";

const PAGE_SIZE = 30;

type LocState = {
  q: string;
  warehouseId: string;
  status: string;
  from: string;
  to: string;
  page: number;
};

/**
 * **Màn #13 — DS hoá đơn bán lẻ** (`/{business}/pos/bill/list`) — `04 §5` màn #13.
 *
 * 📌 Cột theo Nhanh (`nhanh-pos-module-map.md` §5.1): ID · Sản phẩm · Chiết khấu · Thanh toán (**icon** —
 * "hiện đúng icon tiền mặt/chuyển khoản/quẹt thẻ/điểm" là **AC** của màn #13) · Ghi chú.
 * 🔑 **Lọc server-side** (`GET /api/v1/pos/bills?q=&warehouseId=&status=&fromUtc=&toUtc=`) — bảng hoá đơn
 * một quầy/năm có thể lên hàng chục nghìn dòng, tải hết rồi lọc ở client là **đứng máy**.
 */
export default async function PosBillListPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const mot = (ten: string): string => (typeof sp[ten] === "string" ? (sp[ten] as string) : "");

  const boLoc: LocState = {
    q: mot("q"),
    warehouseId: mot("warehouseId"),
    status: mot("status"),
    from: mot("from"),
    to: mot("to"),
    page: Math.max(1, Number(mot("page") || 1)),
  };

  const qs = new URLSearchParams();
  if (boLoc.q) qs.set("q", boLoc.q);
  if (boLoc.warehouseId) qs.set("warehouseId", boLoc.warehouseId);
  if (boLoc.status) qs.set("status", boLoc.status);
  qs.set("skipCount", String((boLoc.page - 1) * PAGE_SIZE));
  qs.set("maxResultCount", String(PAGE_SIZE));

  let loi: string | null = null;
  let trang: Page<RetailBillDto> = { items: [], total: 0, page: boLoc.page, pageSize: PAGE_SIZE };

  try {
    const raw = await apiGetSafe<Parameters<typeof toPage<RetailBillDto>>[0]>(
      `/api/v1/pos/bills?${qs.toString()}`,
      null,
    );
    trang = toPage<RetailBillDto>(raw);
  } catch (e) {
    loi = e instanceof ApiError ? `${e.status} · ${e.code ?? e.message}` : "không gọi được API";
  }

  const kho = await apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []);
  const khoTheoId = new Map(kho.map((k) => [k.id, k]));

  const tongTrang = Math.max(1, Math.ceil(trang.total / PAGE_SIZE));

  const link = (sua: Partial<LocState>): string => {
    const gop = { ...boLoc, ...sua };
    const q = new URLSearchParams();
    if (gop.q) q.set("q", gop.q);
    if (gop.warehouseId) q.set("warehouseId", gop.warehouseId);
    if (gop.status) q.set("status", gop.status);
    if (gop.from) q.set("from", gop.from);
    if (gop.to) q.set("to", gop.to);
    q.set("page", String(gop.page));

    return `/${business}/pos/bill/list?${q.toString()}`;
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Hoá đơn bán lẻ</h1>
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {money(trang.total)} hoá đơn
        </span>

        <div className="ml-auto flex gap-2">
          <Link className="nh-btn" href={`/${business}/pos/bill`}>
            Bán hàng (POS)
          </Link>
          <Link className="nh-btn" href={`/${business}/pos/return`}>
            Trả hàng
          </Link>
        </div>
      </div>

      {/* Lọc server-side — giữ trên URL để F5 / chia sẻ link không mất bộ lọc */}
      <form className="nh-card p-3 flex flex-wrap items-end gap-2" action={`/${business}/pos/bill/list`}>
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Tìm số hoá đơn</div>
          <input className="nh-input mt-0.5" name="q" defaultValue={boLoc.q} style={{ width: 200 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Kho</div>
          <select className="nh-input mt-0.5" name="warehouseId" defaultValue={boLoc.warehouseId} style={{ width: 200 }}>
            <option value="">Tất cả</option>
            {kho.map((k) => (
              <option key={k.id} value={k.id}>
                {k.code} — {k.name}
              </option>
            ))}
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Trạng thái</div>
          <select className="nh-input mt-0.5" name="status" defaultValue={boLoc.status} style={{ width: 150 }}>
            <option value="">Tất cả</option>
            <option value="Completed">Đã bán</option>
            <option value="Voided">Đã huỷ</option>
          </select>
        </label>

        <button className="nh-btn" type="submit">
          Lọc
        </button>
      </form>

      {loi && (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          {loi} — kiểm tra quyền <code>eCommerce.Pos</code>.
        </div>
      )}

      <div className="nh-card overflow-x-auto">
        <table className="w-full text-[12px]">
          <thead>
            <tr style={{ color: "var(--text-muted)" }}>
              <th className="text-left px-3 py-2 font-medium">Số HĐ</th>
              <th className="text-left px-2 py-2 font-medium">Thời gian</th>
              <th className="text-left px-2 py-2 font-medium">Kho</th>
              <th className="text-left px-2 py-2 font-medium">Sản phẩm</th>
              <th className="text-right px-2 py-2 font-medium">Tiền hàng</th>
              <th className="text-right px-2 py-2 font-medium">Chiết khấu</th>
              <th className="text-right px-2 py-2 font-medium">Phải trả</th>
              <th className="text-right px-2 py-2 font-medium">Đã thu</th>
              <th className="text-right px-2 py-2 font-medium">Còn nợ</th>
              <th className="px-2 py-2 font-medium">Thanh toán</th>
              <th className="px-2 py-2 font-medium">Trạng thái</th>
              <th className="px-2 py-2 font-medium">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {trang.items.length === 0 && (
              <tr>
                <td colSpan={12} className="px-3 py-6 text-center" style={{ color: "var(--text-muted)" }}>
                  Chưa có hoá đơn nào khớp bộ lọc.
                </td>
              </tr>
            )}

            {trang.items.map((hoaDon) => {
              const k = khoTheoId.get(hoaDon.warehouseId ?? "");

              return (
                <tr key={hoaDon.id} className="border-t" style={{ borderColor: "var(--border)" }}>
                  <td className="px-3 py-2">
                    <code>{hoaDon.number}</code>
                  </td>
                  <td className="px-2 py-2 tabular">{dateTime(hoaDon.creationTime)}</td>
                  <td className="px-2 py-2">{k ? k.code : (hoaDon.warehouseId ?? "").slice(0, 8)}</td>
                  <td className="px-2 py-2">
                    {(hoaDon.lines ?? []).length === 0
                      ? "—"
                      : (hoaDon.lines ?? [])
                          .slice(0, 2)
                          .map((d) => `${d.productName ?? d.sku} ×${d.quantity}`)
                          .join(" · ") + ((hoaDon.lines ?? []).length > 2 ? "…" : "")}
                  </td>
                  <td className="px-2 py-2 text-right tabular">{money(hoaDon.subtotal)}</td>
                  <td className="px-2 py-2 text-right tabular" style={{ color: "var(--danger)" }}>
                    {money((hoaDon.lineDiscountTotal ?? 0) + (hoaDon.discountTotal ?? 0))}
                  </td>
                  <td className="px-2 py-2 text-right tabular font-medium">{money(hoaDon.totalPayment)}</td>
                  <td className="px-2 py-2 text-right tabular">{money(hoaDon.paidAmount)}</td>
                  <td
                    className="px-2 py-2 text-right tabular"
                    style={{ color: Number(hoaDon.debtAmount ?? 0) > 0 ? "var(--danger)" : "inherit" }}
                  >
                    {money(hoaDon.debtAmount)}
                  </td>
                  <td className="px-2 py-2">
                    {(hoaDon.paymentMethods ?? []).map((m) => (
                      <span key={m} title={m}>
                        {paymentIcon(m)}{" "}
                      </span>
                    ))}
                  </td>
                  <td className="px-2 py-2">
                    {BILL_STATUS_LABEL[hoaDon.status ?? ""] ?? hoaDon.status}
                    {hoaDon.voidReason ? (
                      <div className="text-[10px]" style={{ color: "var(--text-muted)" }}>
                        {hoaDon.voidReason}
                      </div>
                    ) : null}
                  </td>
                  <td className="px-2 py-2">
                    <div className="flex items-center gap-2">
                      <Link className="underline" href={`/${business}/print/bill/${hoaDon.id}`} target="_blank">
                        In
                      </Link>
                      <VoidBillButton
                        billId={hoaDon.id ?? ""}
                        billNumber={hoaDon.number ?? ""}
                        daHuy={hoaDon.status === "Voided"}
                        caDaKet={hoaDon.cashSessionClosed ?? false}
                      />
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="flex items-center gap-2 text-[12px]">
        <Link
          className="nh-btn"
          aria-disabled={boLoc.page <= 1}
          href={link({ page: Math.max(1, boLoc.page - 1) })}
          style={{ pointerEvents: boLoc.page <= 1 ? "none" : "auto", opacity: boLoc.page <= 1 ? 0.5 : 1 }}
        >
          ← Trước
        </Link>
        <span className="tabular" style={{ color: "var(--text-muted)" }}>
          Trang {boLoc.page} / {tongTrang}
        </span>
        <Link
          className="nh-btn"
          aria-disabled={boLoc.page >= tongTrang}
          href={link({ page: Math.min(tongTrang, boLoc.page + 1) })}
          style={{
            pointerEvents: boLoc.page >= tongTrang ? "none" : "auto",
            opacity: boLoc.page >= tongTrang ? 0.5 : 1,
          }}
        >
          Sau →
        </Link>
      </div>
    </div>
  );
}
