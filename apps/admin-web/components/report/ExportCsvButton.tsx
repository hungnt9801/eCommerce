"use client";

import { buildCsv } from "@/lib/report";

/**
 * **Nút xuất CSV** (`D-36`) — dựng CSV **ở client** từ đúng dữ liệu đang hiển thị.
 *
 * 🔑 **VÌ SAO CLIENT (không thêm API `/export`):** dữ liệu đã có trên trang; thêm endpoint xuất là thêm
 * **một định nghĩa cột thứ hai** (một ở bảng, một ở API) — sớm muộn CSV và bảng lệch nhau và người dùng
 * không biết bản nào đúng. Ở đây cột của bảng và cột của CSV là **cùng một biến** ở trang.
 * 📌 BOM UTF-8 + `;` để Excel (bản Việt) đọc đúng dấu — xem `buildCsv`.
 */
export function ExportCsvButton({
  fileName,
  headers,
  rows,
  label = "Xuất CSV",
}: {
  fileName: string;
  headers: string[];
  rows: string[][];
  label?: string;
}) {
  const xuat = (): void => {
    const blob = new Blob([buildCsv(headers, rows)], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();

    URL.revokeObjectURL(url);
  };

  return (
    <button type="button" className="nh-btn" onClick={xuat} disabled={rows.length === 0}>
      {label}
    </button>
  );
}
