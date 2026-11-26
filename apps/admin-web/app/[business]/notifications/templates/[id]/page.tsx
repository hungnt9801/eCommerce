import Link from "next/link";
import { ApiError, apiGet, getGrantedPermissions } from "@/lib/api";
import {
  type NotificationTemplateDto,
  QUYEN,
  nhanKenh,
  toPage,
} from "@/lib/notification";
import { TemplateVersionEditor } from "@/components/notification/TemplateVersionEditor";

/**
 * **SOẠN VERSION CHO MỘT MẪU** (`/{business}/notifications/templates/{id}`) — `NOT-001` / `WP-22`.
 *
 * ⚠️ **`docDeviation`:** chưa có endpoint `GET /templates/{id}` (hợp đồng `/api/v1` chỉ có list + create +
 * publish + archive + preview). Trang lấy mẫu bằng cách tải danh sách (pageSize = trần 200) rồi **tìm theo id**.
 * Nếu BE thêm endpoint đọc một mẫu, đổi sang gọi trực tiếp.
 */
const PAGE_SIZE = 200;

export default async function NotificationTemplateEditorPage({
  params,
}: {
  params: Promise<{ business: string; id: string }>;
}) {
  const { business, id } = await params;

  const [granted, ketQua] = await Promise.all([
    getGrantedPermissions([QUYEN.templatesPublish]),
    taiMot(id),
  ]);

  const coQuyenPublish = granted.has(QUYEN.templatesPublish);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Soạn version mẫu</h1>
        <Link className="nh-btn ml-auto" href={`/${business}/notifications/templates`}>
          ← Danh sách mẫu
        </Link>
      </div>

      {ketQua.loi ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--danger)" }}>
          <div>Không tải được mẫu thông báo{ketQua.ma ? ` (mã ${ketQua.ma})` : ""}.</div>
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {ketQua.thongDiep}
          </div>
          <Link className="nh-btn" href={`/${business}/notifications/templates`}>
            Về danh sách
          </Link>
        </div>
      ) : !ketQua.template ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--text-muted)" }}>
          <div>
            Không tìm thấy mẫu <span className="font-mono">{id}</span> (có thể đã lưu trữ hoặc id sai).
          </div>
          <Link className="nh-btn" href={`/${business}/notifications/templates`}>
            Về danh sách
          </Link>
        </div>
      ) : (
        <>
          <div className="text-[13px]">
            <b>{ketQua.template.code}</b> — {ketQua.template.name}{" "}
            <span style={{ color: "var(--text-muted)" }}>({nhanKenh(ketQua.template.channelKind)})</span>
          </div>
          <TemplateVersionEditor
            business={business}
            template={ketQua.template}
            coQuyenPublish={coQuyenPublish}
          />
        </>
      )}
    </div>
  );
}

async function taiMot(id: string): Promise<{
  template: NotificationTemplateDto | null;
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
    const template = trang.items.find((t) => t.id === id) ?? null;
    return { template, loi: false, ma: null, thongDiep: "" };
  } catch (e) {
    const ma = e instanceof ApiError ? e.code : null;
    const thongDiep =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API (mất mạng hoặc host chưa chạy?)";
    return { template: null, loi: true, ma, thongDiep };
  }
}
