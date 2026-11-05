import Link from "next/link";
import { ApiError, apiGet, apiGetSafe } from "@/lib/api";
import {
  PAYMENT_LABEL,
  SOURCE_OPTIONS,
  TYPE_OPTIONS,
  fulfillmentLabel,
  orderQueryString,
  toPage,
  type OrderListItemDto,
  type OrderSource,
  type OrderTabCountsDto,
  type OrderType,
  type Page,
} from "@/lib/order";
import { dateOnly, money, vnDateToUtcIso } from "@/lib/format";
import type { components } from "@ecommerce/api-client/schema";
import { OrderStatusBadge } from "@/components/order/OrderStatusBadge";

const PAGE_SIZE = 20;

type WarehouseDto = components["schemas"]["eCommerce.Application.Contracts.Inventory.WarehouseDto"];

type BoLoc = {
  tab: string;
  q: string;
  type: string;
  source: string;
  warehouseId: string;
  from: string;
  to: string;
};

function soNguyen(value: string | undefined): number | undefined {
  if (!value) return undefined;
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
}

/**
 * **Màn #10 — DS đơn hàng** (`/{business}/order/manage`).
 *
 * ✅ Nhanh pattern: **14 tab có badge số** (tab "Cần xử lý" + 13 nhóm trạng thái) · **bộ lọc server-side** ·
 *    **dòng nhóm theo ngày** · bảng dày.
 * ✅ **Badge và nội dung tab dùng CHUNG một định nghĩa** (`OrderTabs` ở BE): bộ đếm gọi `GET /orders/tabs`,
 *    danh sách gọi `GET /orders?tab=…` với **cùng** bộ lọc ⇒ hết ca *"tab đếm 7, bấm vào ra 5"*.
 * 📌 Lọc ở **server** — 20.000 đơn không thể tải hết rồi lọc ở client (`04 §5`).
 */
export default async function OrderListPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const mot = (key: string): string => {
    const giaTri = sp[key];
    return typeof giaTri === "string" ? giaTri : "";
  };

  const boLoc: BoLoc = {
    tab: mot("tab"),
    q: mot("q"),
    type: mot("type"),
    source: mot("source"),
    warehouseId: mot("warehouseId"),
    from: mot("from"),
    to: mot("to"),
  };

  const page = Math.max(1, soNguyen(mot("page")) ?? 1);

  const type = soNguyen(boLoc.type) as OrderType | undefined;
  const source = soNguyen(boLoc.source) as OrderSource | undefined;

  // Ngày người dùng chọn là **giờ Việt Nam** ⇒ đổi sang UTC trước khi gửi (nếu không, đơn 00:30 bị lọt).
  const fromUtc = boLoc.from ? vnDateToUtcIso(boLoc.from) : null;
  const toUtc = boLoc.to ? vnDateToUtcIso(boLoc.to, true) : null;

  const loc = {
    tab: boLoc.tab || undefined,
    q: boLoc.q || undefined,
    type,
    source,
    warehouseId: boLoc.warehouseId || undefined,
    fromUtc,
    toUtc,
  };

  // ⚠️ `PagedResultDto` của ABP trả `totalCount` (KHÁC `total` của `ApiPage` bên kho) — `toPage` nhận cả hai.
  let dsRaw: { items?: OrderListItemDto[] | null; totalCount?: number } = { items: [], totalCount: 0 };
  let loi: string | null = null;

  try {
    dsRaw = await apiGet<{ items?: OrderListItemDto[] | null; totalCount?: number }>(
      `/api/v1/orders?${orderQueryString({ ...loc, page, pageSize: PAGE_SIZE })}`,
    );
  } catch (e) {
    // 📌 Hiện **mã lỗi** (`eCommerce:Order:…`) chứ không chỉ "403" — xem `lib/api-error.ts`.
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API";
  }

  // Bộ đếm 14 tab — **cùng** bộ lọc, một lời gọi (server đếm cả 14; UI không N+1).
  const [tabRaw, khoRaw] = await Promise.all([
    apiGetSafe<OrderTabCountsDto>(`/api/v1/orders/tabs?${orderQueryString({ ...loc, page: 1, pageSize: 1 })}`, {
      total: 0,
      tabs: [],
    }),
    apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []),
  ]);

  const trang: Page<OrderListItemDto> = toPage<OrderListItemDto>(dsRaw);
  const khoTheoId = new Map(khoRaw.map((k) => [k.id, k]));

  // 🔗 Giữ nguyên bộ lọc khi đổi tab / trang.
  const href = (thayDoi: Partial<BoLoc & { page: number }>): string => {
    const q = new URLSearchParams();
    const gop = { ...boLoc, ...thayDoi };

    if (gop.tab) q.set("tab", gop.tab);
    if (gop.q) q.set("q", gop.q);
    if (gop.type) q.set("type", gop.type);
    if (gop.source) q.set("source", gop.source);
    if (gop.warehouseId) q.set("warehouseId", gop.warehouseId);
    if (gop.from) q.set("from", gop.from);
    if (gop.to) q.set("to", gop.to);
    if (thayDoi.page && thayDoi.page > 1) q.set("page", String(thayDoi.page));

    const chuoi = q.toString();
    return `/${business}/order/manage${chuoi ? `?${chuoi}` : ""}`;
  };

  const tongTrang = Math.max(1, Math.ceil(trang.total / PAGE_SIZE));

  // Nhóm theo **ngày tạo** (pattern "grouped rows" của Nhanh): một dòng tiêu đề ngày, rồi các đơn trong ngày.
  const theoNgay = new Map<string, OrderListItemDto[]>();
  for (const don of trang.items) {
    const ngay = dateOnly(don.creationTime ?? null);
    const ds = theoNgay.get(ngay) ?? [];
    ds.push(don);
    theoNgay.set(ngay, ds);
  }

  return (
    <div className="space-y-3">
      <div className="flex items-baseline justify-between">
        <div>
          <h1 className="text-[20px] font-semibold">Đơn hàng</h1>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            18 trạng thái kiểu Nhanh · 4 trục trạng thái · tab <b>Cần xử lý</b> là hàng đợi việc thật của nhân viên.
          </p>
        </div>
        <div className="text-[12px] tabular" style={{ color: "var(--text-muted)" }}>
          {trang.total === 0 ? "0" : `${(page - 1) * PAGE_SIZE + 1}–${Math.min(page * PAGE_SIZE, trang.total)}`} /{" "}
          {trang.total}
        </div>
      </div>

      {/* 14 tab có badge — tầng lọc thứ nhất */}
      <div className="flex items-center gap-1 border-b overflow-x-auto" style={{ borderColor: "var(--border)" }}>
        {(tabRaw.tabs ?? []).map((tab) => {
          const active = (boLoc.tab || "all") === tab.key;
          return (
            <Link
              key={tab.key}
              href={href({ tab: tab.key === "all" ? "" : (tab.key ?? ""), page: 1 })}
              className="px-3 h-[38px] flex items-center text-[13px] gap-1.5 whitespace-nowrap"
              style={{
                color: active ? "var(--primary)" : "var(--text)",
                fontWeight: active ? 600 : 400,
                borderBottom: active ? "2px solid var(--primary)" : "2px solid transparent",
                marginBottom: -1,
              }}
            >
              {tab.label}
              <span
                className="text-[11px] px-1.5 rounded-full tabular"
                style={{
                  background: active ? "var(--primary)" : "var(--bg-subtle)",
                  color: active ? "#fff" : "var(--text-muted)",
                }}
              >
                {tab.count ?? 0}
              </span>
            </Link>
          );
        })}
      </div>

      {/* Bộ lọc — tầng lọc thứ hai (server-side) */}
      <form className="nh-card p-3 flex flex-wrap items-end gap-2" action={`/${business}/order/manage`}>
        {boLoc.tab && <input type="hidden" name="tab" value={boLoc.tab} />}

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Tìm
          </span>
          <input
            className="nh-input mt-0.5"
            name="q"
            defaultValue={boLoc.q}
            placeholder="Số đơn / mã vận đơn"
            style={{ width: 200 }}
          />
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Loại đơn
          </span>
          <select className="nh-input mt-0.5" name="type" defaultValue={boLoc.type} style={{ width: 180 }}>
            <option value="">Tất cả</option>
            {TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={String(o.value)}>
                {o.label}
              </option>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Nguồn
          </span>
          <select className="nh-input mt-0.5" name="source" defaultValue={boLoc.source} style={{ width: 160 }}>
            <option value="">Tất cả</option>
            {SOURCE_OPTIONS.map((o) => (
              <option key={o.value} value={String(o.value)}>
                {o.label}
              </option>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Kho
          </span>
          <select className="nh-input mt-0.5" name="warehouseId" defaultValue={boLoc.warehouseId} style={{ width: 190 }}>
            <option value="">Tất cả</option>
            {khoRaw.map((k) => (
              <option key={k.id} value={k.id}>
                {k.code} — {k.name}
              </option>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Từ ngày
          </span>
          <input className="nh-input mt-0.5" type="date" name="from" defaultValue={boLoc.from} style={{ width: 150 }} />
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Đến ngày
          </span>
          <input className="nh-input mt-0.5" type="date" name="to" defaultValue={boLoc.to} style={{ width: 150 }} />
        </label>

        <button type="submit" className="nh-btn nh-btn-primary">
          Lọc
        </button>
        <Link href={`/${business}/order/manage`} className="nh-btn">
          Xoá lọc
        </Link>
      </form>

      {loi && (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Không tải được danh sách đơn (mã {loi}). Kiểm tra API host ở cổng 54275 và quyền{" "}
          <code>eCommerce.Orders</code>.
        </div>
      )}

      {!loi && trang.items.length === 0 && (
        <div className="nh-card p-8 text-center space-y-2">
          <div className="text-[14px] font-medium">Chưa có đơn nào khớp bộ lọc</div>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Đơn phát sinh từ website/sàn hoặc tạo tay sẽ xuất hiện ở đây. Đơn mới ở trạng thái <b>Mới</b> và
            nằm trong tab <b>Cần xử lý</b>.
          </p>
        </div>
      )}

      {trang.items.length > 0 && (
        <div className="nh-card overflow-x-auto">
          <table className="w-full text-[12px]">
            <thead>
              <tr style={{ color: "var(--text-muted)" }}>
                <th className="text-left px-3 py-2 font-medium">Số đơn</th>
                <th className="text-left px-3 py-2 font-medium">Loại · Nguồn</th>
                <th className="text-left px-3 py-2 font-medium">Trạng thái</th>
                <th className="text-left px-3 py-2 font-medium">Thanh toán</th>
                <th className="text-right px-3 py-2 font-medium">Tiền hàng</th>
                <th className="text-right px-3 py-2 font-medium">COD</th>
                <th className="text-left px-3 py-2 font-medium">Vận đơn</th>
                <th className="text-right px-3 py-2 font-medium">Dòng</th>
              </tr>
            </thead>

            {[...theoNgay.entries()].map(([ngay, dsNgay]) => (
              <tbody key={ngay}>
                {/* Dòng NHÓM theo ngày (pattern Nhanh) */}
                <tr style={{ background: "var(--bg-subtle)" }}>
                  <td colSpan={8} className="px-3 py-1.5 text-[11px]" style={{ color: "var(--text-muted)" }}>
                    {ngay} · <b>{dsNgay.length}</b> đơn
                  </td>
                </tr>

                {dsNgay.map((don) => {
                  const kho = don.warehouseId ? khoTheoId.get(don.warehouseId) : undefined;

                  return (
                    <tr key={don.id} className="border-t" style={{ borderColor: "var(--border)" }}>
                      <td className="px-3 py-2">
                        <Link href={`/${business}/order/manage/${don.id}`} style={{ color: "var(--primary)" }}>
                          <b>{don.number}</b>
                        </Link>
                        <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                          {kho ? `${kho.code}` : "chưa gán kho"} · {don.channelCode}
                        </div>
                      </td>

                      <td className="px-3 py-2">
                        <div>{TYPE_OPTIONS.find((x) => x.value === don.type)?.label ?? "—"}</div>
                        <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                          {SOURCE_OPTIONS.find((x) => x.value === don.source)?.label ?? "—"}
                        </div>
                      </td>

                      <td className="px-3 py-2">
                        <OrderStatusBadge
                          label={don.statusLabel ?? ""}
                          status={don.fulfillmentStatus}
                          isCancelled={don.isCancelled ?? false}
                        />
                        <div className="text-[11px] mt-0.5" style={{ color: "var(--text-muted)" }}>
                          {fulfillmentLabel(don.fulfillmentStatus)}
                        </div>
                      </td>

                      <td className="px-3 py-2" style={{ color: "var(--text-muted)" }}>
                        {PAYMENT_LABEL[don.paymentStatus ?? ""] ?? don.paymentStatus ?? "—"}
                      </td>

                      <td className="px-3 py-2 text-right tabular">{money(don.grandTotal)}</td>
                      <td className="px-3 py-2 text-right tabular">{money(don.codAmount)}</td>

                      <td className="px-3 py-2">
                        {don.trackingNumber ? (
                          <code className="text-[11px]">{don.trackingNumber}</code>
                        ) : (
                          <span style={{ color: "var(--text-muted)" }}>—</span>
                        )}
                      </td>

                      <td className="px-3 py-2 text-right tabular">{don.lineCount ?? 0}</td>
                    </tr>
                  );
                })}
              </tbody>
            ))}
          </table>
        </div>
      )}

      {tongTrang > 1 && (
        <div className="flex items-center justify-end gap-2 text-[13px]">
          <Link className="nh-btn" aria-disabled={page <= 1} href={href({ page: page - 1 })}>
            Trước
          </Link>
          <span className="tabular">
            {page} / {tongTrang}
          </span>
          <Link className="nh-btn" aria-disabled={page >= tongTrang} href={href({ page: page + 1 })}>
            Sau
          </Link>
        </div>
      )}
    </div>
  );
}
