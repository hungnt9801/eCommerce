import Link from "next/link";
import { apiGetSafe } from "@/lib/api";
import { todayVn } from "@/lib/format";
import { BILL_KINDS, findKind, suggestNumber, type WarehouseDto } from "@/lib/stock";
import { BillForm } from "@/components/inventory/BillForm";

/**
 * **Màn #8 — Tạo phiếu kho** (`/{business}/inventory/bill/new`, `POST /api/v1/stock/bills`).
 *
 * 📌 Kho nạp **ở server** (`/api/app/warehouse` — bề mặt ABP quy ước, D-13) rồi truyền xuống form;
 * form là client component vì dòng hàng **động** (thêm/xoá/chọn sản phẩm).
 * 📌 `?kind=transfer|stocktake|issue-retail…` chọn sẵn kiểu giao dịch (Nhanh: mỗi loại phiếu có **route riêng**
 * `/inventory/bill/import`, `/inventory/bill/export`, `/inventory/transfer/add2`) — cùng một form, khác kiểu.
 * 📌 Số phiếu + ngày mặc định sinh **ở server** để HTML đầu tiên trùng khớp khi hydrate
 * (sinh ở client trong lúc render ⇒ lệch số giữa SSR và trình duyệt).
 */
export default async function NewStockBillPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const kindParam = typeof sp.kind === "string" ? sp.kind : "";
  const kind = findKind(kindParam); // không khớp ⇒ mặc định "Nhập từ nhà cung cấp"
  const warehouses = await apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
        <Link href={`/${business}/inventory/bill`} className="underline">
          Phiếu kho
        </Link>
        <span>/</span>
        <span>Tạo phiếu</span>
      </div>

      <div className="flex items-center gap-3">
        <h1 className="text-[20px] font-semibold">Tạo phiếu kho</h1>
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {kind.label}
        </span>
      </div>

      <BillForm
        business={business}
        warehouses={warehouses}
        defaultNumber={suggestNumber(kind.prefix)}
        defaultDate={todayVn()}
        initialKind={BILL_KINDS.some((k) => k.key === kindParam) ? kindParam : kind.key}
      />
    </div>
  );
}
