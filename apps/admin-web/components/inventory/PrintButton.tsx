"use client";

import { useEffect } from "react";

/**
 * Tự bật hộp thoại in sau khi trang in render xong (Nhanh: `Lưu và in (F10)` / `In các phiếu XNK đã chọn`).
 * 📌 Chờ 400 ms để font + bảng kịp layout — in ngay lập tức thường ra trang **trắng** ở Chrome.
 * ⚠️ Chỉ gọi **một lần** (`[]`): gọi lại sau mỗi lần re-render sẽ bật hộp thoại in lặp.
 */
export function PrintButton() {
  useEffect(() => {
    const hen = setTimeout(() => window.print(), 400);
    return () => clearTimeout(hen);
  }, []);

  return (
    <button type="button" className="nh-btn nh-btn-primary print:hidden" onClick={() => window.print()}>
      In
    </button>
  );
}

export default PrintButton;
