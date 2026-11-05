"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";

type BoLocDaLuu = { ten: string; search: string };

const STORAGE = "nh:bill-filters:v1";

/**
 * **Lưu bộ lọc** (Nhanh: nút `Lưu bộ lọc` trong FilterBar — `04 §6.1`).
 *
 * 📌 P0 dùng **localStorage** (đúng `04 §6.1`: *"`POST /saved-filters` (P1) hoặc localStorage (P0)"*) vì
 * chưa có bảng `SavedFilter` ở BE. Lưu **nguyên chuỗi truy vấn** (không lưu từng ô) ⇒ thêm/bớt tham số lọc
 * sau này **không** làm hỏng bộ lọc đã lưu.
 * ⚠️ Bộ lọc là **của máy/trình duyệt này** — không chia sẻ giữa nhân viên. Nếu người dùng tưởng nó dùng
 * chung, họ sẽ mất niềm tin khi mở máy khác; nhãn nói rõ *"chỉ trên máy này"*.
 */
export function SavedFilters() {
  const pathname = usePathname();
  const [ds, setDs] = useState<BoLocDaLuu[]>([]);
  const [mo, setMo] = useState(false);
  const [ten, setTen] = useState("");
  const [searchHienTai, setSearchHienTai] = useState("");

  useEffect(() => {
    setSearchHienTai(window.location.search ?? "");

    try {
      const raw = localStorage.getItem(STORAGE);
      const daLuu = raw ? (JSON.parse(raw) as BoLocDaLuu[]) : [];
      setDs(Array.isArray(daLuu) ? daLuu.filter((x) => typeof x?.ten === "string" && typeof x?.search === "string") : []);
    } catch {
      setDs([]);
    }
  }, []);

  function ghi(dsMoi: BoLocDaLuu[]) {
    setDs(dsMoi);
    try {
      localStorage.setItem(STORAGE, JSON.stringify(dsMoi));
    } catch {
      /* ignore */
    }
  }

  function luu() {
    const tenSach = ten.trim();
    if (tenSach.length === 0) return;

    // Trùng tên ⇒ ghi đè (người dùng sửa bộ lọc rồi lưu lại cùng tên là ý định rõ ràng).
    const dsMoi = [...ds.filter((x) => x.ten !== tenSach), { ten: tenSach, search: searchHienTai }];
    ghi(dsMoi);
    setTen("");
    setMo(false);
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      {ds.map((b) => (
        <span
          key={b.ten}
          className="inline-flex items-center gap-1 text-[12px] px-2 py-1 rounded-full"
          style={{ background: "var(--bg-subtle)", color: "var(--text)" }}
          title={`Bộ lọc đã lưu trên máy này: ${b.ten}`}
        >
          <Link href={`${pathname}${b.search}`}>{b.ten}</Link>
          <button
            type="button"
            aria-label={`Xoá bộ lọc ${b.ten}`}
            className="opacity-60 hover:opacity-100"
            onClick={() => ghi(ds.filter((x) => x.ten !== b.ten))}
          >
            ✕
          </button>
        </span>
      ))}

      {mo ? (
        <span className="inline-flex items-center gap-1">
          <input
            className="nh-input h-[30px] w-[160px]"
            placeholder="Tên bộ lọc"
            value={ten}
            autoFocus
            onChange={(e) => setTen(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                luu();
              }
              if (e.key === "Escape") setMo(false);
            }}
          />
          <button type="button" className="nh-btn h-[30px]" onClick={luu}>
            Lưu
          </button>
          <button type="button" className="text-[12px] underline" onClick={() => setMo(false)}>
            Thôi
          </button>
        </span>
      ) : (
        <button
          type="button"
          className="text-[12px] underline"
          style={{ color: "var(--primary)" }}
          onClick={() => {
            setSearchHienTai(window.location.search ?? "");
            setMo(true);
          }}
          title="Chỉ lưu trên trình duyệt này (P0 — chưa có API saved-filters)"
        >
          + Lưu bộ lọc
        </button>
      )}
    </div>
  );
}

export default SavedFilters;
