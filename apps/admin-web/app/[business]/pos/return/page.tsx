import Link from "next/link";
import { ReturnForm } from "@/components/pos/ReturnForm";

/**
 * **Màn #14 — Trả hàng** (`/{business}/pos/return`) — `04 §5` màn #14.
 * 📌 P0 **chỉ trả từ hoá đơn** (`D-42`): trả không hoá đơn không đảo được công nợ ⇒ ghi ở `handover-n7` §5.
 */
export default async function PosReturnPage({ params }: { params: Promise<{ business: string }> }) {
  const { business } = await params;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
        <Link href={`/${business}/pos/bill`} className="underline">
          Bán hàng
        </Link>
        <span>/</span>
        <span>Trả hàng</span>
      </div>

      <h1 className="text-[20px] font-semibold">Trả hàng</h1>

      <ReturnForm business={business} />
    </div>
  );
}
