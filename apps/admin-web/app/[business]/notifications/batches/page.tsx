import Link from "next/link";
import { ApiError, apiGet, apiGetSafe, getGrantedPermissions } from "@/lib/api";
import {
  type MeResult,
  type NotificationTemplateDto,
  QUYEN,
  toPage,
} from "@/lib/notification";
import { BatchForm } from "@/components/notification/BatchForm";
import { BatchApprove } from "@/components/notification/BatchApprove";

/**
 * **LÔ GỬI** (`/{business}/notifications/batches`) — `NOT-001` / `WP-22` (SRS §10).
 *
 * 🔐 Tạo lô cần `Notification.Batches.Create`; duyệt lô cần `Notification.Batches.Approve` (tách nhiệm vụ,
 * `D-198`). Server vẫn là cửa chặn; ẩn nút chỉ là trải nghiệm.
 *
 * 📌 `currentUserId` lấy từ `GET /api/v1/system/me` để biết **người tạo không tự duyệt** (client ẩn nút duyệt
 * của lô vừa tạo). Link chia sẻ `?batchId=<id>` cho người khác mở trang này và duyệt.
 */
const PAGE_SIZE = 200;

export default async function NotificationBatchesPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;
  const batchId = typeof sp.batchId === "string" ? sp.batchId : "";

  const [granted, me, templates] = await Promise.all([
    getGrantedPermissions([QUYEN.batchesCreate, QUYEN.batchesApprove]),
    apiGetSafe<MeResult>("/api/v1/system/me", {}),
    taiTemplates(),
  ]);

  const coQuyenTao = granted.has(QUYEN.batchesCreate);
  const coQuyenDuyet = granted.has(QUYEN.batchesApprove);
  const currentUserId = me.userId ?? null;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Lô gửi thông báo</h1>
        <Link className="nh-btn ml-auto" href={`/${business}/notifications/queue`}>
          Hàng đợi
        </Link>
      </div>

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        📌 Lô gửi chỉ dành cho tin <b>Marketing</b> (<code>D-198</code>). Người tạo lô <b>không</b> được tự
        duyệt — cần <b>người khác</b> duyệt để gửi ra người thật.
      </div>

      {templates.loi ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--danger)" }}>
          <div>Không tải được danh sách template{templates.ma ? ` (mã ${templates.ma})` : ""}.</div>
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {templates.thongDiep}
          </div>
          <Link className="nh-btn" href={`/${business}/notifications/batches`}>
            Thử lại
          </Link>
        </div>
      ) : coQuyenTao ? (
        <BatchForm
          business={business}
          templates={templates.items}
          total={templates.total}
          currentUserId={currentUserId}
        />
      ) : (
        <div className="nh-card p-3 text-[12px]" style={{ color: "var(--text-muted)" }}>
          Cần quyền <span className="font-mono">Notification.Batches.Create</span> để tạo lô gửi.
        </div>
      )}

      <BatchApprove business={business} coQuyenDuyet={coQuyenDuyet} initialBatchId={batchId} />
    </div>
  );
}

async function taiTemplates(): Promise<{
  items: NotificationTemplateDto[];
  total: number;
  loi: boolean;
  ma: string | null;
  thongDiep: string;
}> {
  try {
    const raw = await apiGet<{
      items?: NotificationTemplateDto[] | null;
      total?: number;
      page?: number;
      pageSize?: number;
    }>(`/api/v1/notifications/templates?page=1&pageSize=${PAGE_SIZE}`);

    const trang = toPage(raw);
    return { items: trang.items, total: trang.total, loi: false, ma: null, thongDiep: "" };
  } catch (e) {
    const ma = e instanceof ApiError ? e.code : null;
    const thongDiep =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API (mất mạng hoặc host chưa chạy?)";
    return { items: [], total: 0, loi: true, ma, thongDiep };
  }
}
