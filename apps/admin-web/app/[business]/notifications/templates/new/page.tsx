import Link from "next/link";
import { getGrantedPermissions } from "@/lib/api";
import { QUYEN } from "@/lib/notification";
import { TemplateForm } from "@/components/notification/TemplateForm";

/**
 * **TẠO MẪU THÔNG BÁO MỚI** (`/{business}/notifications/templates/new`) — `NOT-001` / `WP-22`.
 * Ẩn form khi thiếu quyền `Notification.Templates.Manage` — nhưng **server vẫn là cửa chặn** (`[Authorize]`).
 */
export default async function NewNotificationTemplatePage({
  params,
}: {
  params: Promise<{ business: string }>;
}) {
  const { business } = await params;
  const granted = await getGrantedPermissions([QUYEN.templatesManage]);
  const coQuyenTao = granted.has(QUYEN.templatesManage);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Tạo mẫu thông báo</h1>
        <Link className="nh-btn ml-auto" href={`/${business}/notifications/templates`}>
          ← Danh sách mẫu
        </Link>
      </div>

      {coQuyenTao ? (
        <TemplateForm business={business} />
      ) : (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Bạn không có quyền <span className="font-mono">Notification.Templates.Manage</span> để tạo mẫu
          thông báo.
        </div>
      )}
    </div>
  );
}
