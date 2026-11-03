import Link from "next/link";
import { ApiError, apiGet, apiGetSafe } from "@/lib/api";
import { int } from "@/lib/format";
import {
  balanceQueryString,
  toBalance,
  toPage,
  type StockBalanceDto,
  type WarehouseDto,
} from "@/lib/stock";
import { BalancesTable } from "@/components/inventory/BalancesTable";

const PAGE_SIZE = 50;

/**
 * **Tồn kho 9 chiều** (`/{business}/product/inventory`) — "cột tồn 3 số" của `WP-04` (07 việc 7).
 *
 * 📌 Đọc `GET /api/v1/stock/balances` — API trả **12 số** (10 số lưu + `InWarehouse`/`Available` **suy ra**).
 * UI không tự tính lại (`D-27`), chỉ hiển thị.
 * 📌 Lọc theo **SKU** và **kho** ở server: bảng tồn có thể lớn hơn nhiều so với số sản phẩm
 * (mỗi cặp kho–SKU là một dòng).
 */
export default async function StockBalancePage({
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

  const sku = mot("sku").trim();
  const warehouseId = mot("warehouseId");
  const page = Math.max(1, Number(mot("page")) || 1);

  let dsRaw: { items?: StockBalanceDto[] | null; total?: number; page?: number; pageSize?: number } = {
    items: [],
    total: 0,
    page,
    pageSize: PAGE_SIZE,
  };
  let loi: string | null = null;

  try {
    dsRaw = await apiGet(`/api/v1/stock/balances?${balanceQueryString({ sku, warehouseId, page, pageSize: PAGE_SIZE })}`);
  } catch (e) {
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API";
  }

  const khoRaw = await apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []);

  const trang = toPage<StockBalanceDto>(dsRaw);
  const dong = trang.items.map(toBalance);
  const tongTrang = Math.max(1, Math.ceil(trang.total / PAGE_SIZE));

  const href = (pageMoi: number): string => {
    const q = new URLSearchParams();
    if (sku) q.set("sku", sku);
    if (warehouseId) q.set("warehouseId", warehouseId);
    if (pageMoi > 1) q.set("page", String(pageMoi));
    const chuoi = q.toString();
    return `/${business}/product/inventory${chuoi ? `?${chuoi}` : ""}`;
  };

  return (
    <div className="space-y-3">
      <div className="flex items-baseline justify-between">
        <div>
          <h1 className="text-[20px] font-semibold">Tồn kho</h1>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            9 chiều tồn theo cặp <b>kho – SKU</b>. Tồn chỉ đổi khi phiếu kho được <b>ghi sổ</b>.
          </p>
        </div>
        <Link href={`/${business}/inventory/bill/new`} className="nh-btn nh-btn-primary">
          + Tạo phiếu
        </Link>
      </div>

      <form className="nh-card p-3 flex flex-wrap items-end gap-2" action={`/${business}/product/inventory`}>
        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            SKU
          </span>
          <input
            className="nh-input mt-0.5"
            name="sku"
            defaultValue={sku}
            placeholder="VD: WP04-SKU-123456"
            style={{ width: 260 }}
          />
        </label>

        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Kho
          </span>
          <select className="nh-input mt-0.5" name="warehouseId" defaultValue={warehouseId} style={{ width: 220 }}>
            <option value="">Tất cả</option>
            {khoRaw.map((k) => (
              <option key={k.id} value={k.id}>
                {k.code} — {k.name}
              </option>
            ))}
          </select>
        </label>

        <button type="submit" className="nh-btn nh-btn-primary">
          Lọc
        </button>
        <Link href={`/${business}/product/inventory`} className="nh-btn">
          Xoá lọc
        </Link>

        <div className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          {int(trang.total)} dòng
        </div>
      </form>

      {loi && (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Không tải được tồn kho (mã {loi}). Kiểm tra API host ở cổng 54275 và quyền{" "}
          <code>eCommerce.Inventory</code>.
        </div>
      )}

      <BalancesTable
        rows={dong}
        emptyText={
          sku || warehouseId
            ? "Không có dòng tồn kho nào khớp bộ lọc."
            : "Chưa có tồn kho — tạo phiếu nhập và bấm Ghi sổ để bắt đầu."
        }
      />

      {tongTrang > 1 && (
        <div className="flex items-center justify-end gap-2 text-[13px]">
          <Link className="nh-btn" aria-disabled={page <= 1} href={href(page - 1)}>
            Trước
          </Link>
          <span className="tabular">
            {page} / {tongTrang}
          </span>
          <Link className="nh-btn" aria-disabled={page >= tongTrang} href={href(page + 1)}>
            Sau
          </Link>
        </div>
      )}

      <div className="nh-card p-4 text-[12px] space-y-1" style={{ color: "var(--text-muted)" }}>
        <div className="font-medium" style={{ color: "var(--text)" }}>
          Đọc đúng 9 chiều
        </div>
        <div>
          <b>Tồn</b> = tổng hàng thuộc kho. <b>Trong kho</b> = Tồn − Đang chuyển.{" "}
          <b>Có thể bán</b> = Tồn − Giữ − Đang chuyển − Tạm giữ − Lỗi.
        </div>
        <div>
          Chiều <b>đã rời kho</b> (Đang giao, Bảo hành) làm <b>Tồn giảm</b>; chiều <b>còn trong kho nhưng không bán
          được</b> (Tạm giữ, Lỗi, Đang chuyển) chỉ bị trừ khỏi <b>Có thể bán</b>.
        </div>
        <div>
          ⚠️ <b>Đang chuyển · Đặt trước · Chờ nhập · BH giữ</b> chưa có nghiệp vụ sinh (WP-05/POS) nên luôn 0 — cột
          có đó để báo cáo không phải sửa schema khi nối, <b>không</b> phải bằng chứng "đã theo dõi".
        </div>
      </div>
    </div>
  );
}
