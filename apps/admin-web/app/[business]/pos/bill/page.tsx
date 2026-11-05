import Link from "next/link";
import { apiGetSafe } from "@/lib/api";
import type { CashSessionDto } from "@/lib/pos";
import type { WarehouseDto } from "@/lib/stock";
import { PosTerminal } from "@/components/pos/PosTerminal";

/**
 * **Màn #12 — POS bán lẻ** (`/{business}/pos/bill`) — `04 §5` màn #12, đặc tả `04 §6.3`.
 *
 * 📌 **Kho lấy ở server** (`/api/app/warehouse`, bề mặt ABP quy ước — `D-13`) rồi truyền xuống terminal:
 * terminal là client component, gọi API ở client cho **mỗi** lần chọn kho là thêm một vòng mạng trong lúc
 * khách đang chờ. 📌 Ca **đang mở** cũng đọc ở server (`/api/v1/pos/sessions/current`) — terminal mở ca ngay
 * trên màn này nếu chưa có (`WP-06`: một quầy một ca).
 *
 * 🚨 **Quyền:** trang này cần `eCommerce.Pos` (đọc) — nút **Lưu** cần `eCommerce.Pos.Sell`, **Kết ca** cần
 * `eCommerce.Pos.Session.Manage`; server trả `403` kèm mã (`lib/api-error.ts`) nên UI hiện đúng lý do.
 */
export default async function PosPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const khoChon = typeof sp.warehouseId === "string" ? sp.warehouseId : null;

  const warehouses = await apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []);
  const khoHoatDong = warehouses.filter((k) => k.isActive);

  const kho = khoHoatDong.find((k) => k.id === khoChon) ?? khoHoatDong[0] ?? null;

  if (!kho) {
    return (
      <div className="space-y-3">
        <h1 className="text-[20px] font-semibold">Bán lẻ tại quầy</h1>
        <div className="nh-card p-6 space-y-2 text-[13px]">
          <div className="font-medium">Chưa có KHO đang hoạt động</div>
          <p style={{ color: "var(--text-muted)" }}>
            POS phải gắn một kho/quầy (trừ tồn kho + đối chiếu tiền mặt theo ca). Tạo kho trước rồi quay lại.
          </p>
          <Link className="nh-btn inline-flex" href={`/${business}/product/inventory`}>
            Xem tồn kho
          </Link>
        </div>
      </div>
    );
  }

  // 🔒 `204 No Content` = chưa mở ca — trạng thái BÌNH THƯỜNG (không phải lỗi): `apiGetSafe` trả `null`.
  const session = await apiGetSafe<CashSessionDto | null>(
    `/api/v1/pos/sessions/current?warehouseId=${kho.id}`,
    null,
  );

  return (
    <PosTerminal
      business={business}
      warehouseId={kho.id ?? ""}
      warehouseName={`${kho.code} — ${kho.name}`}
      sessionBanDau={session ?? null}
    />
  );
}
