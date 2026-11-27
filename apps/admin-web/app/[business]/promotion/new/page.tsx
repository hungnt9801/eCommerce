import Link from "next/link";
import { getGrantedPermissions } from "@/lib/api";
import { QUYEN } from "@/lib/promotion";
import { PromotionForm } from "@/components/promotion/PromotionForm";

/**
 * **TẠO KHUYẾN MÃI MỚI** (`/{business}/promotion/new`) — `PRO-002` / `W24-3`.
 * Ẩn form khi thiếu quyền `Promotions.Manage` — nhưng **server vẫn là cửa chặn** (`[Authorize]`).
 */
export default async function NewPromotionPage({ params }: { params: Promise<{ business: string }> }) {
  const { business } = await params;
  const granted = await getGrantedPermissions([QUYEN.manage]);
  const coQuyenTao = granted.has(QUYEN.manage);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Tạo khuyến mãi</h1>
        <Link className="nh-btn ml-auto" href={`/${business}/promotion`}>
          ← Danh sách khuyến mãi
        </Link>
      </div>

      {coQuyenTao ? (
        <PromotionForm business={business} />
      ) : (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Bạn không có quyền <span className="font-mono">Promotions.Manage</span> để tạo khuyến mãi.
        </div>
      )}
    </div>
  );
}
