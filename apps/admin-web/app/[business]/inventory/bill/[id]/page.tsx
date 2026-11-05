import Link from "next/link";
import { ApiError, apiGet, apiGetSafe } from "@/lib/api";
import { amount, dateTime, qty } from "@/lib/format";
import {
  COUNTERPARTY_LABEL,
  TYPE_LABEL,
  balanceQueryString,
  modeLabel,
  toBalance,
  toBill,
  toLine,
  toPage,
  type Balance,
  type Bill,
  type StockBalanceDto,
  type StockDocumentDto,
  type StockDocumentLineDto,
  type WarehouseDto,
} from "@/lib/stock";
import { DirectionBadge, StatusBadge, StockBadge } from "@/components/inventory/Badges";
import { BalancesTable } from "@/components/inventory/BalancesTable";
import { BillActions } from "@/components/inventory/BillActions";

/** Số phiếu có thể có rất nhiều dòng — chặn trần truy vấn tồn để trang không bắn hàng trăm request. */
const TOI_DA_SKU_TON = 20;

export default async function StockBillDetailPage({
  params,
}: {
  params: Promise<{ business: string; id: string }>;
}) {
  const { business, id } = await params;

  let loi: string | null = null;
  let phieu: Bill | null = null;

  try {
    phieu = toBill(await apiGet<StockDocumentDto>(`/api/v1/stock/bills/${id}`));
  } catch (e) {
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API";
  }

  if (!phieu) {
    return (
      <div className="space-y-3">
        <div className="flex items-center gap-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
          <Link href={`/${business}/inventory/bill`} className="underline">
            Phiếu kho
          </Link>
          <span>/</span>
          <span>Chi tiết</span>
        </div>

        <div className="nh-card p-6 space-y-2">
          <div className="text-[15px] font-semibold">Không mở được phiếu</div>
          <p className="text-[13px]" style={{ color: "var(--danger)" }}>
            {loi ?? "không xác định"} — kiểm tra phiếu còn tồn tại và quyền <code>eCommerce.Inventory</code>.
          </p>
          <Link href={`/${business}/inventory/bill`} className="nh-btn inline-flex">
            Về danh sách
          </Link>
        </div>
      </div>
    );
  }

  const [dsDongRaw, khoRaw] = await Promise.all([
    apiGetSafe<StockDocumentLineDto[]>(`/api/v1/stock/bills/${id}/lines`, []),
    apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []),
  ]);

  const dong = dsDongRaw.map(toLine);
  const khoTheoId = new Map(khoRaw.map((k) => [k.id, k]));
  const tenKho = (warehouseId: string | null): string => {
    if (!warehouseId) return "—";
    const kho = khoTheoId.get(warehouseId);
    return kho ? `${kho.code} — ${kho.name}` : `${warehouseId.slice(0, 8)}…`;
  };

  // Tồn hiện tại của từng SKU trong phiếu — **một request / SKU** (API lọc theo `sku`), có trần.
  const skus = Array.from(new Set(dong.map((d) => d.sku))).slice(0, TOI_DA_SKU_TON);
  const tonTheoDong = await Promise.all(
    skus.map((sku) =>
      apiGetSafe<{ items?: StockBalanceDto[] | null }>(`/api/v1/stock/balances?${balanceQueryString({ sku, pageSize: 50 })}`, {
        items: [],
      }),
    ),
  );

  const tonTatCa: Balance[] = tonTheoDong.flatMap((trang) => toPage<StockBalanceDto>(trang).items.map(toBalance));
  const tonTheoKhoVaSku = new Map(tonTatCa.map((t) => [`${t.warehouseId}|${t.sku}`, t]));

  const hanhDong =
    phieu.status === "Draft"
      ? "Phiếu Nháp — chưa có bút toán nào. Bấm Ghi sổ để đổi tồn kho."
      : phieu.status === "Posted"
        ? "Đã ghi sổ — tồn kho bên dưới là số SAU khi ghi sổ."
        : "Đã huỷ — không ảnh hưởng tồn kho.";

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
        <Link href={`/${business}/inventory/bill`} className="underline">
          Phiếu kho
        </Link>
        <span>/</span>
        <span>{phieu.number}</span>
      </div>

      <div className="flex items-center gap-3">
        <h1 className="text-[20px] font-semibold">{phieu.number}</h1>
        <StatusBadge status={phieu.status} />
        <DirectionBadge direction={phieu.direction} />
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {hanhDong}
        </span>
      </div>

      <div className="grid grid-cols-[minmax(0,1fr)_320px] gap-3 items-start">
        <div className="nh-card p-4">
          <h2 className="text-[15px] font-semibold mb-3">Thông tin phiếu</h2>

          <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-[13px]">
            <Dong nhan="Loại phiếu" giaTri={TYPE_LABEL[phieu.type] ?? phieu.type} />
            <Dong nhan="Kiểu phiếu (mode)" giaTri={modeLabel(phieu.mode)} />
            <Dong nhan="Kho" giaTri={tenKho(phieu.warehouseId)} />
            <Dong nhan="Kho đích" giaTri={phieu.toWarehouseId ? tenKho(phieu.toWarehouseId) : "—"} />
            <Dong nhan="Ngày phiếu" giaTri={dateTime(phieu.occurredAtUtc)} />
            <Dong
              nhan="Đối tượng"
              giaTri={phieu.counterpartyType ? (COUNTERPARTY_LABEL[phieu.counterpartyType] ?? phieu.counterpartyType) : "—"}
            />
            <Dong nhan="Nguồn (refType/refId)" giaTri={phieu.refType ? `${phieu.refType} · ${phieu.refId ?? ""}` : "—"} />
            <Dong nhan="Khoá chống trùng" giaTri={phieu.idempotencyKey ?? "—"} mono />
            <Dong nhan="Chiết khấu" giaTri={phieu.discountAmount === null ? "—" : `${qty(phieu.discountAmount)} đ`} />
            <Dong nhan="Đã trả" giaTri={phieu.paidAmount === null ? "—" : `${qty(phieu.paidAmount)} đ`} />
          </dl>

          {/* 💰 Tiền của phiếu — số do **server** tính (`TinhTienChungTu`), UI chỉ hiển thị. */}
          <div
            className="mt-3 grid grid-cols-3 gap-3 border-t pt-3 text-[13px]"
            style={{ borderColor: "var(--border)" }}
          >
            <div>
              <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                Tổng tiền hàng
              </div>
              <div className="tabular font-semibold">{amount(phieu.totalAmount)}</div>
            </div>
            <div>
              <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                VAT
              </div>
              <div className="tabular">{amount(phieu.totalVat)}</div>
            </div>
            <div>
              <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                Thanh toán
              </div>
              <div className="tabular font-semibold">{amount(phieu.totalPayment)}</div>
            </div>
          </div>

          {phieu.totalAmount === null && (
            <p className="mt-2 text-[11px]" style={{ color: "var(--text-muted)" }}>
              Phiếu có dòng <b>chưa khai đơn giá</b> ⇒ tổng tiền = <b>—</b> (không phải 0). Xem D-37.
            </p>
          )}

          {phieu.note && (
            <div className="mt-3 text-[13px] px-3 py-2 rounded" style={{ background: "var(--bg-subtle)" }}>
              <span style={{ color: "var(--text-muted)" }}>Ghi chú: </span>
              {phieu.note}
            </div>
          )}
        </div>

        <aside className="nh-card p-4 space-y-2 sticky top-0">
          <h2 className="text-[15px] font-semibold">Thao tác</h2>
          <BillActions billId={phieu.id} status={phieu.status} onDoneHref={`/${business}/inventory/bill`} />
          <p className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Phiếu đã ghi sổ không sửa/xoá được (sổ cái append-only). Sai thì tạo phiếu ĐẢO.
          </p>
        </aside>
      </div>

      {/* ── Dòng phiếu ─────────────────────────────────────────────────────────────────────────── */}
      <div className="nh-card overflow-auto">
        <table className="nh-table">
          <thead>
            <tr>
              <th>SKU</th>
              <th className="w-[110px] text-right">Số lượng</th>
              <th className="w-[120px] text-right">Đơn giá</th>
              <th className="w-[90px] text-right">VAT %</th>
              <th className="w-[120px] text-right">Thành tiền</th>
              <th className="w-[200px]">Serial / IMEI</th>
              <th className="w-[140px]">Tồn tại kho phiếu</th>
            </tr>
          </thead>
          <tbody>
            {dong.length === 0 && (
              <tr>
                <td colSpan={7} className="text-center py-8" style={{ color: "var(--text-muted)" }}>
                  Phiếu không có dòng nào.
                </td>
              </tr>
            )}

            {dong.map((d) => {
              const ton = tonTheoKhoVaSku.get(`${phieu.warehouseId}|${d.sku}`);
              return (
                <tr key={d.id}>
                  <td className="font-medium">{d.sku}</td>
                  <td className="text-right tabular">{qty(d.quantity)}</td>
                  <td className="text-right tabular">{amount(d.unitPrice)}</td>
                  <td className="text-right tabular">{d.vatPercent === null ? "—" : qty(d.vatPercent)}</td>
                  <td className="text-right tabular font-medium">{amount(d.amount)}</td>
                  <td className="text-[12px]" style={{ color: "var(--text-muted)" }}>
                    {d.imeiSerials ?? "—"}
                  </td>
                  <td>
                    {ton ? (
                      <StockBadge onHand={ton.onHand} reserved={ton.reserved} available={ton.available} />
                    ) : (
                      <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
                        chưa có dòng tồn
                      </span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="space-y-2">
        <div className="flex items-baseline justify-between">
          <h2 className="text-[15px] font-semibold">Tồn kho hiện tại của các SKU trong phiếu</h2>
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Nguồn: <code>GET /api/v1/stock/balances</code> — 4 chiều Đang chuyển / Đặt trước / Chờ nhập / BH giữ chưa
            có nghiệp vụ sinh (WP-05/POS) nên luôn 0.
          </span>
        </div>

        <BalancesTable rows={tonTatCa} emptyText="Chưa có dòng tồn kho nào cho các SKU này (phiếu chưa ghi sổ?)." />
      </div>
    </div>
  );
}

function Dong({ nhan, giaTri, mono = false }: { nhan: string; giaTri: string; mono?: boolean }) {
  return (
    <div className="flex justify-between gap-3">
      <dt style={{ color: "var(--text-muted)" }}>{nhan}</dt>
      <dd className={`text-right ${mono ? "tabular text-[12px]" : ""}`} style={mono ? { color: "var(--text-muted)" } : undefined}>
        {giaTri}
      </dd>
    </div>
  );
}
