import Link from "next/link";
import { ApiError, apiGet, apiGetSafe } from "@/lib/api";
import {
  DIRECTION_VALUE,
  MODE_OPTIONS,
  STATUS_LABEL,
  STATUS_VALUE,
  billQueryString,
  toBill,
  toPage,
  type Page,
  type StockDocumentDto,
  type StockDocumentMode,
  type WarehouseDto,
} from "@/lib/stock";
import { BillTable, type BillRow } from "@/components/inventory/BillTable";
import { SavedFilters } from "@/components/inventory/SavedFilters";

const PAGE_SIZE = 20;

type BoLoc = {
  direction: string;
  mode: string;
  status: string;
  warehouseId: string;
  from: string;
  to: string;
};

type TrangPhieu = { items?: StockDocumentDto[] | null; total?: number; page?: number; pageSize?: number };

function soNguyen(value: string | undefined): number | undefined {
  if (!value) return undefined;
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
}

/**
 * **Màn #7 — DS phiếu kho** (`/{business}/inventory/bill`).
 *
 * ✅ Nhanh pattern: **tab có badge số** (Nháp / Đã ghi sổ / Đã huỷ) + **bộ lọc 2 tầng** (tab là tầng 1,
 *    các ô lọc là tầng 2) + bảng dày.
 * ✅ Lọc **ở server** (`GET /api/v1/stock/bills?direction=&status=&mode=&warehouseId=&fromUtc=&toUtc=`) —
 *    bảng 20.000 phiếu không thể tải hết rồi lọc ở client (`04 §5`).
 */
export default async function StockBillListPage({
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
    direction: mot("direction"),
    mode: mot("mode"),
    status: mot("status"),
    warehouseId: mot("warehouseId"),
    from: mot("from"),
    to: mot("to"),
  };

  const page = Math.max(1, soNguyen(mot("page")) ?? 1);

  const direction =
    boLoc.direction === "In" || boLoc.direction === "Out" ? DIRECTION_VALUE[boLoc.direction] : undefined;
  const mode = soNguyen(boLoc.mode) as StockDocumentMode | undefined;
  const status =
    boLoc.status === "Draft" || boLoc.status === "Posted" || boLoc.status === "Cancelled"
      ? STATUS_VALUE[boLoc.status]
      : undefined;

  // Ngày người dùng chọn là **giờ Việt Nam** ⇒ đổi sang UTC trước khi gửi (nếu không, phiếu 00:30 bị lọt).
  const fromUtc = boLoc.from ? new Date(`${boLoc.from}T00:00:00.000+07:00`).toISOString() : null;
  const toUtc = boLoc.to ? new Date(`${boLoc.to}T23:59:59.999+07:00`).toISOString() : null;

  const loc = {
    direction,
    mode,
    warehouseId: boLoc.warehouseId || undefined,
    fromUtc,
    toUtc,
  };

  let dsRaw: TrangPhieu = { items: [], total: 0, page, pageSize: PAGE_SIZE };
  let loi: string | null = null;

  try {
    dsRaw = await apiGet<TrangPhieu>(`/api/v1/stock/bills?${billQueryString({ ...loc, status, page, pageSize: PAGE_SIZE })}`);
  } catch (e) {
    // 📌 Hiện **mã lỗi** (`eCommerce:Inventory:…`) chứ không chỉ "403" — xem `lib/api-error.ts`.
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API";
  }

  // Đếm theo trạng thái cho badge của từng tab (song song, `pageSize=1` ⇒ chỉ lấy `total`).
  const [khoRaw, demDraft, demPosted, demCancelled] = await Promise.all([
    apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []),
    apiGetSafe<{ total?: number }>(`/api/v1/stock/bills?${billQueryString({ ...loc, status: 0, page: 1, pageSize: 1 })}`, {
      total: 0,
    }),
    apiGetSafe<{ total?: number }>(`/api/v1/stock/bills?${billQueryString({ ...loc, status: 1, page: 1, pageSize: 1 })}`, {
      total: 0,
    }),
    apiGetSafe<{ total?: number }>(`/api/v1/stock/bills?${billQueryString({ ...loc, status: 2, page: 1, pageSize: 1 })}`, {
      total: 0,
    }),
  ]);

  const trang = toPage<StockDocumentDto>(dsRaw);
  const ds: Page<ReturnType<typeof toBill>> = { ...trang, items: trang.items.map(toBill) };

  const khoTheoId = new Map(khoRaw.map((k) => [k.id, k]));

  // Mã + tên kho do **server** giải (bảng là client component: không đẩy cả danh sách kho xuống bundle).
  const rows: BillRow[] = ds.items.map((phieu) => {
    const kho = khoTheoId.get(phieu.warehouseId);
    return {
      ...phieu,
      warehouseCode: kho?.code ?? phieu.warehouseId.slice(0, 8),
      warehouseName: kho?.name ?? "",
    };
  });

  // 🔗 Giữ nguyên bộ lọc khi đổi tab / trang.
  const href = (thayDoi: Partial<BoLoc & { page: number }>): string => {
    const q = new URLSearchParams();
    const gop = { ...boLoc, ...thayDoi };

    if (gop.direction) q.set("direction", gop.direction);
    if (gop.mode) q.set("mode", gop.mode);
    if (gop.status) q.set("status", gop.status);
    if (gop.warehouseId) q.set("warehouseId", gop.warehouseId);
    if (gop.from) q.set("from", gop.from);
    if (gop.to) q.set("to", gop.to);
    if (thayDoi.page && thayDoi.page > 1) q.set("page", String(thayDoi.page));

    const chuoi = q.toString();
    return `/${business}/inventory/bill${chuoi ? `?${chuoi}` : ""}`;
  };

  const tongTrang = Math.max(1, Math.ceil(ds.total / PAGE_SIZE));

  const tabs = [
    { key: "", label: "Tất cả", value: ds.total },
    { key: "Draft", label: STATUS_LABEL.Draft, value: demDraft.total ?? 0 },
    { key: "Posted", label: STATUS_LABEL.Posted, value: demPosted.total ?? 0 },
    { key: "Cancelled", label: STATUS_LABEL.Cancelled, value: demCancelled.total ?? 0 },
  ];

  return (
    <div className="space-y-3">
      <div className="flex items-baseline justify-between">
        <div>
          <h1 className="text-[20px] font-semibold">Phiếu kho</h1>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Nhập / xuất / chuyển kho / kiểm kho — phiếu chỉ đổi tồn kho <b>sau khi ghi sổ</b>.
          </p>
        </div>
        <Link href={`/${business}/inventory/bill/new`} className="nh-btn nh-btn-primary">
          + Tạo phiếu
        </Link>
      </div>

      {/* Tab có badge — tầng lọc thứ nhất */}
      <div className="flex items-center gap-1 border-b" style={{ borderColor: "var(--border)" }}>
        {tabs.map((tab) => {
          const active = boLoc.status === tab.key;
          return (
            <Link
              key={tab.key || "all"}
              href={href({ status: tab.key, page: 1 })}
              className="px-3 h-[38px] flex items-center text-[13px] gap-1.5"
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
                {tab.value}
              </span>
            </Link>
          );
        })}
      </div>

      {/* Bộ lọc — tầng lọc thứ hai (server-side) */}
      <form className="nh-card p-3 flex flex-wrap items-end gap-2" action={`/${business}/inventory/bill`}>
        {boLoc.status && <input type="hidden" name="status" value={boLoc.status} />}

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Loại
          </span>
          <select className="nh-input mt-0.5" name="direction" defaultValue={boLoc.direction} style={{ width: 120 }}>
            <option value="">Tất cả</option>
            <option value="In">Nhập</option>
            <option value="Out">Xuất</option>
          </select>
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Kiểu
          </span>
          <select className="nh-input mt-0.5" name="mode" defaultValue={boLoc.mode} style={{ width: 190 }}>
            <option value="">Tất cả</option>
            {MODE_OPTIONS.map((m) => (
              <option key={m.value} value={String(m.value)}>
                {m.label}
              </option>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Kho hàng
          </span>
          <select className="nh-input mt-0.5" name="warehouseId" defaultValue={boLoc.warehouseId} style={{ width: 200 }}>
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
        <Link href={`/${business}/inventory/bill`} className="nh-btn">
          Xoá lọc
        </Link>

        <div className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          {ds.total === 0 ? "0" : `${(page - 1) * PAGE_SIZE + 1}–${Math.min(page * PAGE_SIZE, ds.total)}`} / {ds.total}
        </div>
      </form>

      {/* Bộ lọc đã lưu (P0: localStorage) — Nhanh có nút `Lưu bộ lọc` cùng vị trí này */}
      <SavedFilters />

      {loi && (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Không tải được danh sách phiếu (mã {loi}). Kiểm tra API host ở cổng 54275 và quyền{" "}
          <code>eCommerce.Inventory</code>.
        </div>
      )}

      {!loi && ds.items.length === 0 && (
        <div className="nh-card p-8 text-center space-y-2">
          <div className="text-[14px] font-medium">Chưa có phiếu kho nào khớp bộ lọc</div>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Tạo phiếu nhập đầu tiên để bắt đầu theo dõi tồn kho — phiếu ở trạng thái <b>Nháp</b>, tồn kho chỉ đổi
            khi bấm <b>Ghi sổ</b>.
          </p>
          <Link href={`/${business}/inventory/bill/new`} className="nh-btn nh-btn-primary inline-flex">
            + Tạo phiếu
          </Link>
        </div>
      )}

      {ds.items.length > 0 && <BillTable rows={rows} business={business} />}
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
