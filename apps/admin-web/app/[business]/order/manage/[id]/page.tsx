import Link from "next/link";
import { ApiError, apiGet, apiGetSafe } from "@/lib/api";
import { dateTime, money } from "@/lib/format";
import {
  PAYMENT_LABEL,
  RETURN_LABEL,
  SOURCE_OPTIONS,
  TYPE_OPTIONS,
  fulfillmentLabel,
  reasonLabel,
  type OrderDetailDto,
  type OrderStatusHistoryDto,
} from "@/lib/order";
import { OrderActions } from "@/components/order/OrderActions";
import { OrderStatusBadge } from "@/components/order/OrderStatusBadge";

type TabKey = "info" | "lines" | "history";

/**
 * **Màn #11 — Chi tiết đơn** (`/{business}/order/manage/{id}`) — tab **động** theo giai đoạn đơn
 * (`nhanh-pos-module-map.md` §6.3).
 *
 * 📌 **Tab "Lịch trình"** chỉ hiện với đơn **đã gửi HVC** (`Chờ thu gom` trở đi) — đúng Nhanh: đơn còn trong
 * kho thì chưa có lịch trình vận chuyển nào để xem; hiện tab rỗng chỉ làm người dùng bấm vào rồi thất vọng.
 * 📌 **Tab "Xác nhận"** (đổi trạng thái) chỉ hiện khi **còn bước đi tiếp**; đơn đã kết thúc thì không.
 */
export default async function OrderDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string; id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business, id } = await params;
  const sp = await searchParams;
  const tab = (typeof sp.tab === "string" ? sp.tab : "info") as TabKey;

  let loi: string | null = null;
  let don: OrderDetailDto | null = null;

  try {
    don = await apiGet<OrderDetailDto>(`/api/v1/orders/${id}`);
  } catch (e) {
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API";
  }

  if (!don) {
    return (
      <div className="space-y-3">
        <div className="flex items-center gap-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
          <Link href={`/${business}/order/manage`} className="underline">
            Đơn hàng
          </Link>
          <span>/</span>
          <span>Chi tiết</span>
        </div>

        <div className="nh-card p-6 space-y-2">
          <div className="text-[15px] font-semibold">Không mở được đơn</div>
          <p className="text-[13px]" style={{ color: "var(--danger)" }}>
            {loi ?? "không xác định"} — kiểm tra đơn còn tồn tại và quyền <code>eCommerce.Orders</code>.
          </p>
          <Link href={`/${business}/order/manage`} className="nh-btn inline-flex">
            Về danh sách
          </Link>
        </div>
      </div>
    );
  }

  const lichSuRaw = await apiGetSafe<{ items?: OrderStatusHistoryDto[] | null }>(
    `/api/v1/orders/${id}/history?maxResultCount=50`,
    { items: [] },
  );

  const lichSu = lichSuRaw.items ?? [];

  // Đơn đã gửi HVC = trạng thái thực hiện từ `Chờ thu gom` (3) trở đi, trừ các trạng thái "còn ở shop".
  const daGuiHvc = [3, 4, 5, 6, 7, 8, 16, 17].includes(Number(don.fulfillmentStatus ?? -1));
  const conBuoc = (don.allowedTransitions ?? []).length > 0;

  const tabs: Array<{ key: TabKey; label: string; enabled: boolean }> = [
    { key: "info", label: "Thông tin", enabled: true },
    { key: "lines", label: `Sản phẩm (${(don.lines ?? []).length})`, enabled: true },
    { key: "history", label: "Lịch trình", enabled: daGuiHvc || lichSu.length > 0 },
  ];

  const tabHref = (key: TabKey) => `/${business}/order/manage/${id}?tab=${key}`;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
        <Link href={`/${business}/order/manage`} className="underline">
          Đơn hàng
        </Link>
        <span>/</span>
        <span>{don.number}</span>
      </div>

      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-[20px] font-semibold flex items-center gap-2">
            {don.number}
            <OrderStatusBadge
              label={don.statusLabel ?? ""}
              status={don.fulfillmentStatus}
              isCancelled={don.isCancelled ?? false}
            />
          </h1>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {TYPE_OPTIONS.find((x) => x.value === don.type)?.label ?? "—"} ·{" "}
            {SOURCE_OPTIONS.find((x) => x.value === don.source)?.label ?? "—"} · tạo lúc{" "}
            {dateTime(don.creationTime ?? null)}
          </p>
        </div>

        <div className="text-right">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Tổng tiền hàng
          </div>
          <div className="text-[20px] font-semibold tabular">{money(don.grandTotal)}</div>
          <div className="text-[11px] tabular" style={{ color: "var(--text-muted)" }}>
            COD {money(don.codAmount)}
          </div>
        </div>
      </div>

      {/* Tab động theo giai đoạn đơn */}
      <div className="flex items-center gap-1 border-b" style={{ borderColor: "var(--border)" }}>
        {tabs
          .filter((t) => t.enabled)
          .map((t) => {
            const active = tab === t.key;
            return (
              <Link
                key={t.key}
                href={tabHref(t.key)}
                className="px-3 h-[36px] flex items-center text-[13px]"
                style={{
                  color: active ? "var(--primary)" : "var(--text)",
                  fontWeight: active ? 600 : 400,
                  borderBottom: active ? "2px solid var(--primary)" : "2px solid transparent",
                  marginBottom: -1,
                }}
              >
                {t.label}
              </Link>
            );
          })}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-3">
        <div className="space-y-3">
          {tab === "info" && (
            <div className="nh-card p-4 space-y-3 text-[13px]">
              <div className="grid grid-cols-2 gap-3">
                <O nhan="Trạng thái thực hiện">{fulfillmentLabel(don.fulfillmentStatus)}</O>
                <O nhan="Nhãn hiển thị">{don.statusLabel ?? "—"}</O>
                <O nhan="Thanh toán">{PAYMENT_LABEL[don.paymentStatus ?? ""] ?? don.paymentStatus ?? "—"}</O>
                <O nhan="Trả hàng">{RETURN_LABEL[don.returnStatus ?? ""] ?? don.returnStatus ?? "—"}</O>
                <O nhan="Kênh">{don.channelCode ?? "—"}</O>
                <O nhan="Kho xuất">{don.warehouseId ? don.warehouseId.slice(0, 8) : "chưa gán kho"}</O>
                <O nhan="Hãng vận chuyển">{don.carrierCode ?? "—"}</O>
                <O nhan="Mã vận đơn">{don.trackingNumber ?? "—"}</O>
                <O nhan="Khối lượng">{don.weightGram ? `${don.weightGram} g` : "—"}</O>
                <O nhan="Lý do">{reasonLabel(don.reasonCode)}</O>
              </div>

              <div className="border-t pt-3" style={{ borderColor: "var(--border)" }}>
                <div className="grid grid-cols-2 gap-3">
                  <O nhan="Tiền hàng">{money(don.subtotal)}</O>
                  <O nhan="Chiết khấu">{money(don.discountTotal)}</O>
                  <O nhan="VAT">{money(don.taxTotal)}</O>
                  <O nhan="Phí ship">{money(don.shippingTotal)}</O>
                  <O nhan="Ship báo khách">{money(don.shippingFeeCustomer)}</O>
                  <O nhan="Ship thực tế">{money(don.shippingFeeActual)}</O>
                </div>
              </div>

              {don.note && (
                <div className="border-t pt-3" style={{ borderColor: "var(--border)" }}>
                  <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                    Ghi chú
                  </div>
                  <div className="whitespace-pre-wrap">{don.note}</div>
                </div>
              )}

              <div className="border-t pt-3 text-[11px] tabular" style={{ color: "var(--text-muted)" }}>
                bản ghi: thực hiện {don.fulfillmentVersion} · thanh toán {don.paymentVersion} · đối soát{" "}
                {don.reconciliationVersion} · kết thúc {don.closureVersion} · dòng {don.rowVersion}
                {don.externalOrderId ? ` · mã sàn ${don.externalOrderId}` : ""}
              </div>
            </div>
          )}

          {tab === "lines" && (
            <div className="nh-card overflow-x-auto">
              <table className="w-full text-[12px]">
                <thead>
                  <tr style={{ color: "var(--text-muted)" }}>
                    <th className="text-left px-3 py-2 font-medium">SKU</th>
                    <th className="text-right px-3 py-2 font-medium">SL</th>
                    <th className="text-right px-3 py-2 font-medium">Đã trả</th>
                    <th className="text-right px-3 py-2 font-medium">Đơn giá</th>
                    <th className="text-right px-3 py-2 font-medium">Giảm</th>
                    <th className="text-right px-3 py-2 font-medium">VAT</th>
                    <th className="text-right px-3 py-2 font-medium">Thành tiền</th>
                  </tr>
                </thead>
                <tbody>
                  {(don.lines ?? []).map((dong) => (
                    <tr key={dong.id} className="border-t" style={{ borderColor: "var(--border)" }}>
                      <td className="px-3 py-2">
                        <code>{dong.sku}</code>
                        {dong.productId && (
                          <div className="text-[10px]" style={{ color: "var(--text-muted)" }}>
                            SP {dong.productId.slice(0, 8)}
                          </div>
                        )}
                      </td>
                      <td className="px-3 py-2 text-right tabular">{dong.quantity ?? 0}</td>
                      <td className="px-3 py-2 text-right tabular">{dong.returnedQuantity ?? 0}</td>
                      <td className="px-3 py-2 text-right tabular">{money(dong.unitPrice)}</td>
                      <td className="px-3 py-2 text-right tabular">{money(dong.discountAmount)}</td>
                      <td className="px-3 py-2 text-right tabular">{money(dong.taxAmount)}</td>
                      <td className="px-3 py-2 text-right tabular font-medium">{money(dong.lineTotal)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {tab === "history" && (
            <div className="nh-card p-4 space-y-2">
              {lichSu.length === 0 ? (
                <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
                  Chưa có lần chuyển trạng thái nào — đơn mới tạo thì chưa có lịch trình.
                </p>
              ) : (
                <ol className="space-y-2">
                  {lichSu.map((h) => (
                    <li key={h.id} className="flex items-start gap-3 text-[12px]">
                      <span className="tabular w-[120px] shrink-0" style={{ color: "var(--text-muted)" }}>
                        {dateTime(h.occurredAtUtc ?? null)}
                      </span>
                      <span className="px-1.5 py-0.5 rounded text-[11px]" style={{ background: "var(--bg-subtle)" }}>
                        {h.axis}
                      </span>
                      <span>
                        {h.fromStatus} → <b>{h.toStatus}</b> · {h.actorType} · <code>{h.reasonCode}</code>
                        {h.reasonText ? ` · ${h.reasonText}` : ""}
                        <span className="tabular" style={{ color: "var(--text-muted)" }}>
                          {" "}
                          (v{h.versionAfter})
                        </span>
                      </span>
                    </li>
                  ))}
                </ol>
              )}
            </div>
          )}
        </div>

        <div className="space-y-3">
          <OrderActions
            orderId={don.id ?? id}
            fulfillmentVersion={don.fulfillmentVersion ?? 0}
            closureVersion={don.closureVersion ?? 0}
            allowedTransitions={don.allowedTransitions}
            canCancel={don.canCancel ?? false}
            isCancelled={don.isCancelled ?? false}
          />

          {!conBuoc && !don.isCancelled && (
            <div className="nh-card p-3 text-[11px]" style={{ color: "var(--text-muted)" }}>
              Đơn đã ở bước cuối — chỉ còn đường TRẢ HÀNG (nếu khách trả) hoặc đóng hồ sơ.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/** Một dòng nhãn–giá trị (giữ nhãn xám, giá trị đậm — bảng dày nhưng đọc được). */
function O({ nhan, children }: { nhan: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        {nhan}
      </div>
      <div className="tabular">{children}</div>
    </div>
  );
}
