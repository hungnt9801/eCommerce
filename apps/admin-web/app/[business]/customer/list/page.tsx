import Link from "next/link";
import { ApiError, apiGetSafe } from "@/lib/api";
import { amount, dateOnly, money } from "@/lib/format";
import {
  customerTypeLabel,
  toPage,
  type CustomerDto,
  type CustomerLevelDto,
  type CustomerTabCountsDto,
  type Page,
} from "@/lib/customer";

const PAGE_SIZE = 30;

type LocState = {
  q: string;
  tab: string;
  type: string;
  levelId: string;
  conNo: string;
  page: number;
};

/**
 * **Màn DS KHÁCH — 5 TAB PHÂN KHÚC** (`/{business}/customer/list`) — `WP-07`.
 *
 * 🔑 Vì sao **tab là dữ liệu**: `GET /api/v1/customers/tabs` và `GET /api/v1/customers?tab=` dùng **cùng**
 * một định nghĩa ở BE (`CustomerTabs`) — badge và nội dung **không thể** lệch nhau (`D-41`).
 * 📌 Lọc **server-side**: 20.000 khách không thể tải hết rồi lọc ở client (và "lọc ở client" là **không lọc**:
 * DevTools vẫn thấy dữ liệu).
 */
export default async function CustomerListPage({
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
    tab: mot("tab") || "all",
    type: mot("type"),
    levelId: mot("levelId"),
    conNo: mot("conNo"),
    page: Math.max(1, Number(mot("page") || 1)),
  };

  const qs = new URLSearchParams();
  if (boLoc.q) qs.set("q", boLoc.q);
  if (boLoc.type) qs.set("type", boLoc.type);
  if (boLoc.levelId) qs.set("levelId", boLoc.levelId);
  if (boLoc.conNo === "1") qs.set("conNo", "true");
  qs.set("skipCount", String((boLoc.page - 1) * PAGE_SIZE));
  qs.set("maxResultCount", String(PAGE_SIZE));

  const qsTab = new URLSearchParams(qs);
  qsTab.set("tab", boLoc.tab);

  let loi: string | null = null;
  let trang: Page<CustomerDto> = { items: [], total: 0 };

  try {
    const raw = await apiGetSafe<Parameters<typeof toPage<CustomerDto>>[0]>(
      `/api/v1/customers?${qsTab.toString()}`,
      null,
    );
    trang = toPage<CustomerDto>(raw);
  } catch (e) {
    loi = e instanceof ApiError ? `${e.status} · ${e.code ?? e.message}` : "không gọi được API";
  }

  const dem = await apiGetSafe<CustomerTabCountsDto>("/api/v1/customers/tabs", {
    total: 0,
    tabs: [],
  });  const caps = await apiGetSafe<CustomerLevelDto[]>("/api/v1/customer-levels", []);
  const capTheoId = new Map(caps.map((c) => [c.id, c]));

  const tongTrang = Math.max(1, Math.ceil(trang.total / PAGE_SIZE));

  const link = (sua: Partial<LocState>): string => {
    const gop = { ...boLoc, ...sua };
    const q = new URLSearchParams();
    if (gop.q) q.set("q", gop.q);
    if (gop.tab && gop.tab !== "all") q.set("tab", gop.tab);
    if (gop.type) q.set("type", gop.type);
    if (gop.levelId) q.set("levelId", gop.levelId);
    if (gop.conNo === "1") q.set("conNo", "1");
    q.set("page", String(gop.page));
    return `/${business}/customer/list?${q.toString()}`;
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Khách hàng</h1>
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {money(trang.total)} hồ sơ
        </span>

        <div className="ml-auto flex gap-2">
          <Link className="nh-btn" href={`/${business}/settings/staff`}>
            Nhân viên &amp; quyền
          </Link>
        </div>
      </div>

      {/* 5 tab phân khúc — badge đếm từ server, CÙNG định nghĩa với bộ lọc */}
      <div className="flex flex-wrap gap-1">
        {(dem.tabs ?? []).map((t) => {
          // 📌 `key`/`label` từ OpenAPI có thể `null` (mọi prop tham chiếu đều nullable trong spec) —
          // server **luôn** trả, nhưng ta chuẩn hoá **một chỗ** thay vì rải `!` khắp JSX.
          const khoaTab = t.key ?? "";

          return (
            <Link
              key={khoaTab}
              href={link({ tab: khoaTab, page: 1 })}
              className="nh-btn"
              style={
                boLoc.tab === khoaTab
                  ? { background: "var(--brand)", color: "#fff", borderColor: "var(--brand)" }
                  : undefined
              }
            >
              {t.label} <span className="opacity-80">({t.count ?? 0})</span>
            </Link>
          );
        })}
      </div>

      <form className="nh-card p-3 flex flex-wrap items-end gap-2" action={`/${business}/customer/list`}>
        <input type="hidden" name="tab" value={boLoc.tab} />

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Tìm tên / SĐT / email / MST</div>
          <input className="nh-input mt-0.5" name="q" defaultValue={boLoc.q} style={{ width: 240 }} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Loại</div>
          <select className="nh-input mt-0.5" name="type" defaultValue={boLoc.type} style={{ width: 150 }}>
            <option value="">Tất cả</option>
            <option value="Retail">Khách lẻ</option>
            <option value="Wholesale">Khách buôn</option>
            <option value="Agency">Đại lý</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Cấp khách</div>
          <select className="nh-input mt-0.5" name="levelId" defaultValue={boLoc.levelId} style={{ width: 170 }}>
            <option value="">Tất cả</option>
            {caps.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>

        <label className="text-[12px] flex items-center gap-1">
          <input type="checkbox" name="conNo" value="1" defaultChecked={boLoc.conNo === "1"} />
          Chỉ khách còn nợ
        </label>

        <button className="nh-btn" type="submit">
          Lọc
        </button>
        <Link className="nh-btn" href={`/${business}/customer/list`}>
          Xoá lọc
        </Link>
      </form>

      {loi && (
        <div className="nh-card p-3 text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      <div className="nh-card overflow-x-auto">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="text-left" style={{ color: "var(--text-muted)" }}>
              <th className="p-2">Mã</th>
              <th className="p-2">Khách</th>
              <th className="p-2">Liên hệ</th>
              <th className="p-2">Loại</th>
              <th className="p-2">Cấp</th>
              <th className="p-2 text-right">Điểm</th>
              <th className="p-2 text-right">Tiền tích luỹ</th>
              <th className="p-2 text-right">Dư nợ</th>
              <th className="p-2 text-right">Hạn mức nợ</th>
              <th className="p-2">Tạo</th>
            </tr>
          </thead>
          <tbody>
            {trang.items.length === 0 && (
              <tr>
                <td className="p-3" colSpan={10} style={{ color: "var(--text-muted)" }}>
                  Không có khách nào khớp bộ lọc.
                </td>
              </tr>
            )}

            {trang.items.map((k) => {
              const cap = k.levelId ? capTheoId.get(k.levelId) : undefined;

              return (
                <tr key={k.id} className="border-t" style={{ borderColor: "var(--border)" }}>
                  <td className="p-2 font-mono text-[11px]">{k.id.slice(0, 8)}</td>
                  <td className="p-2">
                    <Link className="underline" href={`/${business}/customer/${k.id}`}>
                      {k.name}
                    </Link>
                    {k.vuotHanMucNo && (
                      <span className="ml-1 text-[10px]" style={{ color: "var(--danger)" }}>
                        vượt hạn mức
                      </span>
                    )}
                  </td>
                  <td className="p-2">
                    {k.phone ?? "—"}
                    {k.email && <div className="text-[11px] opacity-70">{k.email}</div>}
                  </td>
                  <td className="p-2">{customerTypeLabel(k.type)}</td>
                  <td className="p-2">{cap?.name ?? "—"}</td>
                  <td className="p-2 text-right">{amount(k.points)}</td>
                  <td className="p-2 text-right">{money(k.accumulatedAmount)}</td>
                  <td
                    className="p-2 text-right"
                    style={{ color: k.debtBalance > 0 ? "var(--danger)" : undefined }}
                  >
                    {money(k.debtBalance)}
                  </td>
                  <td className="p-2 text-right">{k.debtLimit > 0 ? money(k.debtLimit) : "không đặt"}</td>
                  <td className="p-2">{dateOnly(k.creationTime)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="flex items-center gap-2 text-[12px]">
        <Link className="nh-btn" href={link({ page: Math.max(1, boLoc.page - 1) })}>
          ← Trước
        </Link>
        <span>
          Trang {boLoc.page} / {tongTrang}
        </span>
        <Link className="nh-btn" href={link({ page: Math.min(tongTrang, boLoc.page + 1) })}>
          Sau →
        </Link>
      </div>
    </div>
  );
}
