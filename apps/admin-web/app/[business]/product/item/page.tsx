import Link from "next/link";
import { apiGet } from "@/lib/api";
import { ApiError } from "@/lib/api";

type ProductRow = {
  id: string;
  code: string;
  name: string;
  sku: string | null;
  barcode: string | null;
  price: number | null;
  status: string;
  variantCount: number;
  creationTime: string;
};

type ProductPage = { items: ProductRow[]; total: number; page: number; pageSize: number };

const STATUS_LABEL: Record<string, string> = {
  Draft: "Mới",
  Published: "Đang bán",
  Unpublished: "Ngừng bán",
  Archived: "Lưu trữ",
};

const TABS = [
  { key: "", label: "Tất cả" },
  { key: "Published", label: "Đang bán" },
  { key: "Draft", label: "Mới" },
  { key: "Unpublished", label: "Ngừng bán" },
];

function money(value: number | null): string {
  if (value === null || value === undefined) return "—";
  return new Intl.NumberFormat("vi-VN").format(value);
}

export default async function ProductListPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<{ q?: string; status?: string; page?: string }>;
}) {
  const { business } = await params;
  const sp = await searchParams;
  const q = sp.q ?? "";
  const status = sp.status ?? "";
  const page = Math.max(1, Number(sp.page ?? "1") || 1);
  const pageSize = 20;

  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (q) query.set("q", q);
  if (status) query.set("status", status);

  let data: ProductPage = { items: [], total: 0, page, pageSize };
  let error: string | null = null;

  try {
    data = await apiGet<ProductPage>(`/api/v1/products?${query.toString()}`);
  } catch (e) {
    error = e instanceof ApiError ? `${e.status}` : "không gọi được API";
  }

  const totalPages = Math.max(1, Math.ceil(data.total / pageSize));

  return (
    <div className="space-y-3">
      {/* Tabs có badge — pattern đặc trưng của Nhanh */}
      <div className="flex items-center gap-1 border-b" style={{ borderColor: "var(--border)" }}>
        {TABS.map((tab) => {
          const active = status === tab.key;
          const href = `/${business}/product/item?${new URLSearchParams({
            ...(q ? { q } : {}),
            ...(tab.key ? { status: tab.key } : {}),
          }).toString()}`;
          return (
            <Link
              key={tab.key || "all"}
              href={href}
              className="px-3 h-[38px] flex items-center text-[13px]"
              style={{
                color: active ? "var(--primary)" : "var(--text)",
                fontWeight: active ? 600 : 400,
                borderBottom: active ? "2px solid var(--primary)" : "2px solid transparent",
                marginBottom: -1,
              }}
            >
              {tab.label}
              {tab.key === "" && (
                <span className="ml-1.5 text-[11px] px-1.5 rounded-full text-white" style={{ background: "var(--primary)" }}>
                  {data.total}
                </span>
              )}
            </Link>
          );
        })}
      </div>

      {/* Bộ lọc */}
      <form className="nh-card p-3 flex items-center gap-2" action={`/${business}/product/item`}>
        {status && <input type="hidden" name="status" value={status} />}
        <input
          className="nh-input max-w-[320px]"
          name="q"
          placeholder="Tên, mã sản phẩm, SKU…"
          defaultValue={q}
        />
        <button type="submit" className="nh-btn nh-btn-primary">
          Lọc
        </button>
        <Link href={`/${business}/product/item`} className="nh-btn">
          Xoá lọc
        </Link>

        <div className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          {data.total === 0 ? "0" : `${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, data.total)}`} / {data.total}
        </div>
      </form>

      {error && (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Không tải được danh sách (mã {error}). Kiểm tra API host ở cổng 54275.
        </div>
      )}

      {/* Bảng dày 8/20 cột — các cột tồn/giá nhập sẽ thêm ở WP-04 */}
      <div className="nh-card overflow-auto">
        <table className="nh-table">
          <thead>
            <tr>
              <th className="w-[90px]">ID</th>
              <th className="w-[130px]">Mã</th>
              <th>Tên</th>
              <th className="w-[140px]">SKU</th>
              <th className="w-[130px] text-right">Giá bán</th>
              <th className="w-[120px] text-right">Biến thể</th>
              <th className="w-[130px]">Trạng thái</th>
              <th className="w-[140px]">Ngày tạo</th>
            </tr>
          </thead>
          <tbody>
            {data.items.length === 0 && !error && (
              <tr>
                <td colSpan={8} className="text-center py-10" style={{ color: "var(--text-muted)" }}>
                  Chưa có sản phẩm. (DB dev đang rỗng — WP-03 sẽ có import và form tạo.)
                </td>
              </tr>
            )}
            {data.items.map((row) => (
              <tr key={row.id}>
                <td className="tabular" style={{ color: "var(--text-muted)" }}>
                  {row.id.slice(0, 8)}
                </td>
                <td style={{ color: "var(--primary)" }}>{row.code}</td>
                <td className="font-medium">{row.name}</td>
                <td>{row.sku ?? "—"}</td>
                <td className="text-right tabular">{money(row.price)}</td>
                <td className="text-right tabular">{row.variantCount}</td>
                <td>
                  <span
                    className="text-[11px] px-2 py-0.5 rounded-full"
                    style={{
                      background: row.status === "Published" ? "#ecfdf5" : "var(--bg-subtle)",
                      color: row.status === "Published" ? "var(--success)" : "var(--text-muted)",
                    }}
                  >
                    {STATUS_LABEL[row.status] ?? row.status}
                  </span>
                </td>
                <td className="tabular" style={{ color: "var(--text-muted)" }}>
                  {new Date(row.creationTime).toLocaleDateString("vi-VN")}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 text-[13px]">
          <Link
            className="nh-btn"
            aria-disabled={page <= 1}
            href={`/${business}/product/item?${new URLSearchParams({ ...(q ? { q } : {}), ...(status ? { status } : {}), page: String(page - 1) })}`}
          >
            Trước
          </Link>
          <span className="tabular">
            {page} / {totalPages}
          </span>
          <Link
            className="nh-btn"
            aria-disabled={page >= totalPages}
            href={`/${business}/product/item?${new URLSearchParams({ ...(q ? { q } : {}), ...(status ? { status } : {}), page: String(page + 1) })}`}
          >
            Sau
          </Link>
        </div>
      )}
    </div>
  );
}
