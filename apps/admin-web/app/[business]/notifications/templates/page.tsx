import Link from "next/link";
import { ApiError, apiGet, getGrantedPermissions } from "@/lib/api";
import {
  type NotificationTemplateDto,
  QUYEN,
  toPage,
} from "@/lib/notification";
import { TemplateTable } from "@/components/notification/TemplateTable";

/**
 * **DANH SÁCH MẪU THÔNG BÁO** (`/{business}/notifications/templates`) — `NOT-001` / `WP-22` (SRS §10).
 *
 * Server Component gọi `/api/v1/notifications/templates` (kèm cookie qua BFF server-side), render HTML;
 * phần lọc theo kênh/mục đích/trạng thái giao cho client (`TemplateTable`) vì dữ liệu một tenant nhỏ và
 * SRS **cấm** ẩn bản ghi `Archived`.
 *
 * Ba trạng thái: **loading** (`loading.tsx` skeleton) · **empty** (hướng dẫn tạo, KHÔNG phải lỗi) ·
 * **error** (thông điệp + thử lại). Template chưa publish ⇒ **cảnh báo cấu hình** (không phải rỗng).
 */
const PAGE_SIZE = 200; // trần `NotificationApiPaging.PageSizeToiDa`; vượt ⇒ cảnh báo thiếu trang ở client.

export default async function NotificationTemplatesPage({
  params,
}: {
  params: Promise<{ business: string }>;
}) {
  const { business } = await params;

  const [granted, ketQua] = await Promise.all([
    getGrantedPermissions([QUYEN.templatesManage, QUYEN.templatesPublish]),
    taiDanhSach(),
  ]);

  const coQuyenTao = granted.has(QUYEN.templatesManage);
  const coQuyenPublish = granted.has(QUYEN.templatesPublish);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Mẫu thông báo</h1>
        {coQuyenTao ? (
          <Link className="nh-btn nh-btn-primary ml-auto" href={`/${business}/notifications/templates/new`}>
            + Tạo mẫu
          </Link>
        ) : (
          <span className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
            Cần quyền <span className="font-mono">Notification.Templates.Manage</span> để tạo mẫu.
          </span>
        )}
      </div>

      {ketQua.loi ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--danger)" }}>
          <div>Không tải được danh sách mẫu thông báo{ketQua.ma ? ` (mã ${ketQua.ma})` : ""}.</div>
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {ketQua.thongDiep}
          </div>
          <Link className="nh-btn" href={`/${business}/notifications/templates`}>
            Thử lại
          </Link>
        </div>
      ) : (
        <TemplateTable
          business={business}
          items={ketQua.trang.items}
          total={ketQua.trang.total}
          coQuyenPublish={coQuyenPublish}
        />
      )}
    </div>
  );
}

async function taiDanhSach(): Promise<{
  trang: { items: NotificationTemplateDto[]; total: number; page: number; pageSize: number };
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

    return { trang: toPage(raw), loi: false, ma: null, thongDiep: "" };
  } catch (e) {
    // 📌 Hiện **mã lỗi** (`eCommerce:Notification:…`), không chỉ "403" — xem `lib/api-error.ts`.
    const ma = e instanceof ApiError ? e.code : null;
    const thongDiep =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API (mất mạng hoặc host chưa chạy?)";

    return { trang: { items: [], total: 0, page: 1, pageSize: PAGE_SIZE }, loi: true, ma, thongDiep };
  }
}
