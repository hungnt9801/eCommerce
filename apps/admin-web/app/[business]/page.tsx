import Link from "next/link";
import { apiGetSafe } from "@/lib/api";

type ProductPage = { total: number };

export default async function DashboardPage({ params }: { params: Promise<{ business: string }> }) {
  const { business } = await params;
  const products = await apiGetSafe<ProductPage>("/api/v1/products?page=1&pageSize=1", { total: 0 });
  const ping = await apiGetSafe<{ serverTimeUtc: string; service: string }>("/api/v1/system/ping", {
    serverTimeUtc: "",
    service: "?",
  });

  const cards = [
    { label: "Sản phẩm", value: products.total, href: `/${business}/product/item`, hint: "đang quản lý" },
    { label: "Đơn hàng", value: "—", href: `/${business}/order/manage`, hint: "WP-05" },
    { label: "Tồn kho", value: "—", href: `/${business}/product/inventory`, hint: "WP-04" },
    { label: "Doanh thu hôm nay", value: "—", href: `/${business}/report/revenue`, hint: "WP-08" },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-baseline justify-between">
        <h1 className="text-[20px] font-semibold">Tổng quan</h1>
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {ping.service} · {ping.serverTimeUtc ? new Date(ping.serverTimeUtc).toLocaleString("vi-VN") : "mất kết nối"}
        </span>
      </div>

      <div className="grid grid-cols-4 gap-3">
        {cards.map((card) => (
          <Link key={card.label} href={card.href} className="nh-card p-4 hover:shadow-sm">
            <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
              {card.label}
            </div>
            <div className="text-[26px] font-semibold tabular mt-1">{card.value}</div>
            <div className="text-[11px] mt-1" style={{ color: "var(--text-muted)" }}>
              {card.hint}
            </div>
          </Link>
        ))}
      </div>

      <div className="nh-card p-5">
        <h2 className="text-[15px] font-semibold mb-2">Đường dọc đã chạy được</h2>
        <ul className="text-[13px] space-y-1 list-disc pl-5" style={{ color: "var(--text-muted)" }}>
          <li>Đăng nhập qua OpenIddict → token chỉ nằm ở server (cookie httpOnly).</li>
          <li>Menu lọc theo quyền **hỏi server** (`/api/v1/system/permissions`), không đọc token.</li>
          <li>Danh sách sản phẩm đọc `/api/v1/products` thật (chưa có dữ liệu ⇒ 0).</li>
          <li>Kho/đơn/POS/báo cáo: chưa làm — hiện mờ trong menu, không phải link chết.</li>
        </ul>
      </div>
    </div>
  );
}
