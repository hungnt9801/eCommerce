import Link from "next/link";
import { ApiError, apiGet, getGrantedPermissions } from "@/lib/api";
import {
  type PromotionDetailDto,
  type PromotionRuleCatalogItemDto,
  QUYEN,
  nhanKieuGiam,
  nhanLoai,
  nhanTrangThai,
} from "@/lib/promotion";
import { dateTime, int, money, qty } from "@/lib/format";
import { TreeBuilder } from "@/components/promotion/TreeBuilder";

/**
 * **CHI TIẾT + DỰNG CÂY KHUYẾN MÃI** (`/{business}/promotion/{id}`) — `PRO-002` / `W24-3` (SRS §10.1).
 *
 * 🚨 **Trần cây (`Limits`) là nguồn DUY NHẤT** cho đồng hồ giới hạn (`D-288`). Tải chi tiết lỗi — hoặc
 * API **thiếu** `Limits` — ⇒ màn ở trạng thái **error**, **KHÔNG** cho sửa cây, **KHÔNG** fallback hằng số.
 *
 * Ba trạng thái: **loading** (`loading.tsx`) · **empty** (cây rỗng — hướng dẫn thêm gốc) · **error** (thử lại).
 */
export default async function PromotionDetailPage({
  params,
}: {
  params: Promise<{ business: string; id: string }>;
}) {
  const { business, id } = await params;

  const [granted, chiTiet, catalog] = await Promise.all([
    getGrantedPermissions([QUYEN.manage, QUYEN.approve]),
    taiChiTiet(id),
    taiCatalog(),
  ]);

  const coQuyenManage = granted.has(QUYEN.manage);
  const coQuyenDuyet = granted.has(QUYEN.approve);

  const loiTai = chiTiet.loi || catalog.loi;
  const thongDiepLoi = chiTiet.loi ? chiTiet.thongDiep : catalog.thongDiep;
  const detail = chiTiet.detail;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Chi tiết khuyến mãi</h1>
        <Link className="nh-btn ml-auto" href={`/${business}/promotion`}>
          ← Danh sách khuyến mãi
        </Link>
      </div>

      {loiTai ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--danger)" }}>
          <div>Không tải được chi tiết khuyến mãi hoặc danh mục điều kiện.</div>
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {thongDiepLoi}
          </div>
          <Link className="nh-btn" href={`/${business}/promotion/${id}`}>
            Thử lại
          </Link>
        </div>
      ) : !detail ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--text-muted)" }}>
          <div>
            Không tìm thấy khuyến mãi <span className="font-mono">{id}</span> (có thể đã xoá hoặc id sai).
          </div>
          <Link className="nh-btn" href={`/${business}/promotion`}>
            Về danh sách
          </Link>
        </div>
      ) : !detail.limits ? (
        // 🚨 D-288: thiếu Limits ⇒ KHÔNG dựng đồng hồ, KHÔNG cho sửa cây, KHÔNG fallback hằng số.
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--danger)" }}>
          <div>API không trả trần cây (<span className="font-mono">Limits</span>).</div>
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Không thể dựng đồng hồ giới hạn an toàn (không có fallback hằng số — <code>D-288</code>). Báo BE
            kiểm <span className="font-mono">GetAsync</span>.
          </div>
          <Link className="nh-btn" href={`/${business}/promotion`}>
            Về danh sách
          </Link>
        </div>
      ) : (
        <>
          {/* Thông tin khuyến mãi */}
          <div className="nh-card p-3 text-[12px] grid grid-cols-2 md:grid-cols-4 gap-x-6 gap-y-1">
            <span>
              <span style={{ color: "var(--text-muted)" }}>Mã:</span>{" "}
              <b className="font-mono">{detail.code}</b>
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Loại:</span> {nhanLoai(detail.kind)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Kiểu giảm:</span> {moTaGiam(detail)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Trạng thái:</span> {nhanTrangThai(detail.status)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Đơn tối thiểu:</span>{" "}
              {detail.minOrderAmount == null ? "—" : money(detail.minOrderAmount)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Giảm tối đa:</span>{" "}
              {detail.maxDiscountAmount == null ? "—" : money(detail.maxDiscountAmount)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Đã dùng:</span> {int(detail.usedCount)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Trần lượt:</span>{" "}
              {detail.maxUsageTotal == null ? "Không giới hạn" : int(detail.maxUsageTotal)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Bắt đầu:</span> {dateTime(detail.startsAtUtc)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Kết thúc:</span> {dateTime(detail.endsAtUtc)}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Cộng dồn:</span>{" "}
              {detail.isCumulative ? "Có" : "Không"}
            </span>
            <span>
              <span style={{ color: "var(--text-muted)" }}>Tree version:</span> {int(detail.treeVersion)}
            </span>
          </div>

          <TreeBuilder
            business={business}
            promotion={detail}
            catalog={catalog.catalog ?? []}
            coQuyenManage={coQuyenManage}
            coQuyenDuyet={coQuyenDuyet}
          />
        </>
      )}
    </div>
  );
}

function moTaGiam(p: PromotionDetailDto): string {
  if (p.discountKind === 0) return `${nhanKieuGiam(p.discountKind)} ${qty(p.discountValue)}%`;
  if (p.discountKind === 1) return `${nhanKieuGiam(p.discountKind)} ${money(p.discountValue)}`;
  return nhanKieuGiam(p.discountKind);
}

async function taiChiTiet(id: string): Promise<{
  detail: PromotionDetailDto | null;
  loi: boolean;
  thongDiep: string;
}> {
  try {
    const detail = await apiGet<PromotionDetailDto>(`/api/v1/promotion/${encodeURIComponent(id)}`);
    return { detail, loi: false, thongDiep: "" };
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) {
      return { detail: null, loi: false, thongDiep: "" };
    }
    return { detail: null, loi: true, thongDiep: moTaTai(e) };
  }
}

async function taiCatalog(): Promise<{
  catalog: PromotionRuleCatalogItemDto[] | null;
  loi: boolean;
  thongDiep: string;
}> {
  try {
    const catalog = await apiGet<PromotionRuleCatalogItemDto[]>(`/api/v1/promotion/rule-catalog`);
    return { catalog, loi: false, thongDiep: "" };
  } catch (e) {
    return { catalog: null, loi: true, thongDiep: moTaTai(e) };
  }
}

function moTaTai(e: unknown): string {
  return e instanceof ApiError
    ? e.code
      ? `${e.status} · ${e.code}`
      : `${e.status} · ${e.message}`
    : "không gọi được API (mất mạng hoặc host chưa chạy?)";
}
