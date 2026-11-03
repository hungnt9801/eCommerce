import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "eCommerce Admin",
  description: "Quản trị bán hàng đa kênh — UI theo Nhanh.vn",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi">
      <body>{children}</body>
    </html>
  );
}
