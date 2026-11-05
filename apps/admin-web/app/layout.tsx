import type { Metadata } from "next";
import { APP_NAME } from "@/lib/public-config";
import "./globals.css";

/**
 * 📌 `WP-10`: tên hiển thị đến từ `NEXT_PUBLIC_APP_NAME` (**nhúng lúc build**) thay vì hằng chuỗi rải rác
 * 3 tệp. Với giá trị mặc định, giao diện **không đổi một ký tự** — đây là điều kiện để thay đổi này
 * không phá 8 luồng E2E (`WP-09`) vốn khẳng định chữ trên màn hình.
 */
export const metadata: Metadata = {
  title: `${APP_NAME} Admin`,
  description: "Quản trị bán hàng đa kênh — UI theo Nhanh.vn",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi">
      <body>{children}</body>
    </html>
  );
}
