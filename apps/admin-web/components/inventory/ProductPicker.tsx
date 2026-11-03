"use client";

import { useEffect, useRef, useState } from "react";
import type { components } from "@ecommerce/api-client/schema";
import { money } from "@/lib/format";

type ProductItem = components["schemas"]["eCommerce.HttpApi.Dtos.ProductListItemDto"];

export type PickedProduct = {
  id: string;
  code: string;
  name: string;
  sku: string;
  /** Giá bán (để hiển thị trong danh sách chọn) — **không** gửi lên API phiếu kho (dòng phiếu không có giá). */
  price: number | null;
};

/**
 * **ProductPicker** cho dòng phiếu kho (04 §5 màn #8) — gõ tên/mã/SKU, chọn 1 sản phẩm ⇒ điền **SKU** vào dòng.
 * 📌 Gọi qua **BFF** `/api/proxy/v1/products` (D-12: access token không rời server).
 * 📌 `q` < 2 ký tự thì **không** gọi API: gõ từng ký tự mà mỗi ký tự một request là tự làm nghẽn.
 * ⚠️ Sản phẩm **chưa có SKU** (chưa tạo ĐVT gốc) thì không chọn được — đó là dữ liệu chưa đủ, không phải lỗi UI.
 */
export function ProductPicker({
  onPick,
  placeholder = "Tìm sản phẩm (tên / mã / SKU)…",
}: {
  onPick: (product: PickedProduct) => void;
  placeholder?: string;
}) {
  const [q, setQ] = useState("");
  const [items, setItems] = useState<PickedProduct[]>([]);
  const [dangTai, setDangTai] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [mo, setMo] = useState(false);
  const hopRef = useRef<HTMLDivElement | null>(null);

  // Đóng danh sách khi bấm ra ngoài (không dùng `onBlur`: bấm vào kết quả sẽ mất trước khi kịp chọn).
  useEffect(() => {
    function xuLy(event: MouseEvent) {
      if (hopRef.current && !hopRef.current.contains(event.target as Node)) {
        setMo(false);
      }
    }

    document.addEventListener("mousedown", xuLy);
    return () => document.removeEventListener("mousedown", xuLy);
  }, []);

  useEffect(() => {
    const tuKhoa = q.trim();

    if (tuKhoa.length < 2) {
      setItems([]);
      return;
    }

    const hen = setTimeout(async () => {
      setDangTai(true);
      setLoi(null);

      try {
        const res = await fetch(`/api/proxy/v1/products?q=${encodeURIComponent(tuKhoa)}&pageSize=8`);
        if (!res.ok) {
          setLoi(`HTTP ${res.status}`);
          setItems([]);
          return;
        }

        const body = (await res.json()) as { items?: ProductItem[] | null };
        setItems(
          (body.items ?? [])
            .filter((p): p is ProductItem & { sku: string } => typeof p.sku === "string" && p.sku.length > 0)
            .map((p) => ({ id: p.id ?? "", code: p.code ?? "", name: p.name ?? "", sku: p.sku, price: p.price ?? null })),
        );
        setMo(true);
      } catch {
        setLoi("Không gọi được API");
        setItems([]);
      } finally {
        setDangTai(false);
      }
    }, 300);

    return () => clearTimeout(hen);
  }, [q]);

  function chon(product: PickedProduct) {
    onPick(product);
    setQ("");
    setItems([]);
    setMo(false);
  }

  return (
    <div ref={hopRef} className="relative">
      <input
        className="nh-input"
        value={q}
        placeholder={placeholder}
        onChange={(e) => setQ(e.target.value)}
        onFocus={() => setMo(true)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && items.length > 0) {
            e.preventDefault();
            chon(items[0]);
          }
          if (e.key === "Escape") setMo(false);
        }}
      />

      {mo && q.trim().length >= 2 && (
        <div
          className="absolute z-20 left-0 right-0 mt-1 nh-card shadow-lg max-h-[280px] overflow-auto"
          style={{ background: "var(--bg-surface)" }}
        >
          {dangTai && (
            <div className="px-3 py-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
              Đang tìm…
            </div>
          )}

          {!dangTai && loi && (
            <div className="px-3 py-2 text-[12px]" style={{ color: "var(--danger)" }}>
              {loi}
            </div>
          )}

          {!dangTai && !loi && items.length === 0 && (
            <div className="px-3 py-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
              Không thấy sản phẩm có SKU khớp “{q.trim()}”.
            </div>
          )}

          {!dangTai &&
            items.map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => chon(p)}
                className="w-full text-left px-3 py-2 hover:bg-[#f8fafc] border-b last:border-b-0"
                style={{ borderColor: "var(--border)" }}
              >
                <div className="flex items-center justify-between gap-3">
                  <span className="text-[13px] font-medium">{p.name}</span>
                  <span className="text-[12px] tabular" style={{ color: "var(--text-muted)" }}>
                    {p.sku}
                  </span>
                </div>
                <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                  Mã {p.code}
                  {typeof p.price === "number" ? ` · ${money(p.price)} đ` : ""}
                </div>
              </button>
            ))}
        </div>
      )}
    </div>
  );
}
