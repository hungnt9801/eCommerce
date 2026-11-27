import type { CSSProperties } from "react";
import Link from "next/link";
import { ApiError, apiGet, getGrantedPermissions } from "@/lib/api";
import {
  type PromotionListItemDto,
  QUYEN,
  nhanKieuGiam,
  nhanLoai,
  nhanTrangThai,
  taoDanhSachQueryString,
  toPage,
} from "@/lib/promotion";
import { dateOnly, int, money, qty } from "@/lib/format";

/**
 * **DANH SÁCH KHUYẾN MÃI** (`/{business}/promotion`) — `PRO-002` / `W24-3` (SRS §10.1).
 *
 * 📌 `GET /api/v1/promotion` (lọc `Status`/`Kind`, phân trang) — **server-side** filter + paging.
 * 🚨 Cột **"Đã dùng"** (`usedCount`) **luôn đứng cạnh** "Trần lượt" (`maxUsageTotal`) — thói quen ghi rõ
 * chênh lệch của repo (`docs/design-doc-guidelines.md:33`, `§10.1`).
 *
 * Ba trạng thái: **loading** (`loading.tsx` skeleton) · **empty** (hướng dẫn tạo, KHÔNG phải lỗi) ·
 * **error** (thông điệp + thử lại).
 */
const PAGE_SIZE = 50;

export default async function PromotionListPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const mot = (k: string): string => (typeof sp[k] === "string" ? (sp[k] as string) : "");

  const status = mot("status");
  const kind = mot("kind");
  const page = Math.max(1, Number(mot("page") || 1) || 1);

  const [granted, ketQua] = await Promise.all([
    getGrantedPermissions([QUYEN.manage]),
    taiDanhSach({
      status: status === "" ? null : Number(status),
      kind: kind === "" ? null : Number(kind),
      page,
    }),
  ]);

  const coQuyenTao = granted.has(QUYEN.manage);
  const tongTrang = Math.max(1, Math.ceil(ketQua.trang.total / PAGE_SIZE));

  const link = (sua: Record<string, string | number>): string => {
    const q = new URLSearchParams();
    const gop = { status, kind, page: String(page), ...sua };
    if (gop.status) q.set("status", String(gop.status));
    if (gop.kind) q.set("kind", String(gop.kind));
    q.set("page", String(gop.page));
    return `/${business}/promotion?${q.toString()}`;
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Khuyến mãi</h1>
        {coQuyenTao ? (
          <Link className="nh-btn nh-btn-primary ml-auto" href={`/${business}/promotion/new`}>
            + Tạo khuyến mãi
          </Link>
        ) : (
          <span className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
            Cần quyền <span className="font-mono">Promotions.Manage</span> để tạo khuyến mãi.
          </span>
        )}
      </div>

      {/* Bộ lọc SERVER-SIDE */}
      <form className="nh-card p-3 flex flex-wrap items-end gap-2" action={`/${business}/promotion`} method="get">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Trạng thái</div>
          <select className="nh-input mt-0.5" name="status" defaultValue={status} style={{ width: 150 }}>
            <option value="">Tất cả</option>
            <option value="0">Nháp</option>
            <option value="1">Đã duyệt</option>
            <option value="2">Đang chạy</option>
            <option value="3">Tạm dừng</option>
            <option value="4">Hết hạn</option>
            <option value="5">Đã huỷ</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Loại</div>
          <select className="nh-input mt-0.5" name="kind" defaultValue={kind} style={{ width: 190 }}>
            <option value="">Tất cả</option>
            <option value="0">Mã giảm giá</option>
            <option value="1">Khuyến mãi tự động</option>
            <option value="2">Flash Sale</option>
          </select>
        </label>

        <button type="submit" className="nh-btn nh-btn-primary">
          Lọc
        </button>
        <Link className="nh-btn" href={`/${business}/promotion`}>
          Xoá lọc
        </Link>

        <div className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          Tổng: <b className="tabular">{int(ketQua.trang.total)}</b> khuyến mãi
        </div>
      </form>

      {ketQua.loi ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--danger)" }}>
          <div>Không tải được danh sách khuyến mãi.</div>
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {ketQua.thongDiep}
          </div>
          <Link className="nh-btn" href={`/${business}/promotion`}>
            Thử lại
          </Link>
        </div>
      ) : (
        <div className="nh-card overflow-auto">
          <table className="nh-table">
            <thead>
              <tr>
                <th className="w-[150px]">Mã</th>
                <th>Tên</th>
                <th className="w-[150px]">Loại</th>
                <th className="w-[140px]">Kiểu giảm</th>
                <th className="w-[120px]">Trạng thái</th>
                <th className="w-[110px] text-right">Đã dùng</th>
                <th className="w-[120px] text-right">Trần lượt</th>
                <th className="w-[150px]">Bắt đầu</th>
                <th className="w-[110px]"></th>
              </tr>
            </thead>
            <tbody>
              {ketQua.trang.items.length === 0 && (
                <tr>
                  <td colSpan={9} className="text-center py-10" style={{ color: "var(--text-muted)" }}>
                    Chưa có khuyến mãi nào khớp bộ lọc. Bấm “Tạo khuyến mãi” để bắt đầu.
                  </td>
                </tr>
              )}

              {ketQua.trang.items.map((p) => (
                <tr key={p.id}>
                  <td className="font-mono text-[12px]" style={{ color: "var(--primary)" }}>
                    {p.code}
                  </td>
                  <td className="font-medium">{p.name}</td>
                  <td>{nhanLoai(p.kind)}</td>
                  <td>{moTaGiam(p)}</td>
                  <td>
                    <span
                      className="text-[11px] px-2 py-0.5 rounded-full"
                      style={mauTrangThai(p.status)}
                    >
                      {nhanTrangThai(p.status)}
                    </span>
                  </td>
                  <td className="text-right tabular">{int(p.usedCount)}</td>
                  <td className="text-right tabular">
                    {p.maxUsageTotal == null ? (
                      <span style={{ color: "var(--text-muted)" }}>Không giới hạn</span>
                    ) : (
                      int(p.maxUsageTotal)
                    )}
                  </td>
                  <td>{dateOnly(p.startsAtUtc)}</td>
                  <td>
                    <Link className="nh-btn" href={`/${business}/promotion/${p.id}`}>
                      Chi tiết
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {tongTrang > 1 && (
        <div className="flex items-center justify-end gap-2 text-[13px]">
          <Link className="nh-btn" aria-disabled={page <= 1} href={link({ page: page - 1 })}>
            Trước
          </Link>
          <span className="tabular">
            {page} / {tongTrang}
          </span>
          <Link className="nh-btn" aria-disabled={page >= tongTrang} href={link({ page: page + 1 })}>
            Sau
          </Link>
        </div>
      )}
    </div>
  );
}

/** Mô tả giá trị giảm theo kiểu: % hoặc số tiền. */
function moTaGiam(p: PromotionListItemDto): string {
  if (p.discountKind === 0) {
    return `${nhanKieuGiam(p.discountKind)} ${qty(p.discountValue)}%`;
  }
  if (p.discountKind === 1) {
    return `${nhanKieuGiam(p.discountKind)} ${money(p.discountValue)}`;
  }
  return nhanKieuGiam(p.discountKind);
}

function mauTrangThai(status: number | undefined): CSSProperties {
  // 2 Active · 1 Approved ⇒ xanh; 0 Draft ⇒ cam; 3 Paused ⇒ vàng nhạt; 4/5 ⇒ xám.
  if (status === 1 || status === 2) {
    return { background: "#ecfdf5", color: "var(--success)" };
  }
  if (status === 0) {
    return { background: "#fff7ed", color: "var(--warning)" };
  }
  if (status === 3) {
    return { background: "#fefce8", color: "var(--warning)" };
  }
  return { background: "var(--bg-subtle)", color: "var(--text-muted)" };
}

async function taiDanhSach(loc: {
  status: number | null;
  kind: number | null;
  page: number;
}): Promise<{
  trang: { items: PromotionListItemDto[]; total: number; page: number; pageSize: number };
  loi: boolean;
  thongDiep: string;
}> {
  try {
    const raw = await apiGet<{
      items?: PromotionListItemDto[] | null;
      total?: number;
      page?: number;
      pageSize?: number;
    }>(
      `/api/v1/promotion?${taoDanhSachQueryString({
        status: loc.status,
        kind: loc.kind,
        page: loc.page,
        pageSize: PAGE_SIZE,
      })}`,
    );
    return { trang: toPage(raw), loi: false, thongDiep: "" };
  } catch (e) {
    const thongDiep =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API (mất mạng hoặc host chưa chạy?)";
    return {
      trang: { items: [], total: 0, page: loc.page, pageSize: PAGE_SIZE },
      loi: true,
      thongDiep,
    };
  }
}
