"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import { amount, dateTime } from "@/lib/format";
import { DIRECTION_LABEL, MODE_OPTIONS, STATUS_LABEL, TYPE_LABEL, type Bill } from "@/lib/stock";
import { DirectionBadge, StatusBadge } from "./Badges";

export type BillRow = Bill & { warehouseCode: string; warehouseName: string };

type ColumnKey =
  | "number"
  | "date"
  | "direction"
  | "mode"
  | "type"
  | "warehouse"
  | "lines"
  | "amount"
  | "status";

/** Định nghĩa cột **một chỗ** — bảng, hộp chọn cột và CSV đều đọc từ đây (3 nơi lệch nhau là mầm lỗi). */
const COLUMNS: ReadonlyArray<{ key: ColumnKey; label: string; width: string; align?: "right" }> = [
  { key: "number", label: "Số phiếu", width: "180px" },
  { key: "date", label: "Ngày", width: "150px" },
  { key: "direction", label: "Nhập/Xuất", width: "100px" },
  { key: "mode", label: "Kiểu", width: "150px" },
  { key: "type", label: "Chứng từ", width: "130px" },
  { key: "warehouse", label: "Kho hàng", width: "150px" },
  { key: "lines", label: "Dòng", width: "70px", align: "right" },
  { key: "amount", label: "Tổng tiền", width: "130px", align: "right" },
  { key: "status", label: "Trạng thái", width: "120px" },
];

const STORAGE_COLS = "nh:bill-cols:v1";

type KetQuaHang = { number: string; ok: boolean; text: string; code?: string | null; boQua?: boolean };

/**
 * **Bảng DS phiếu kho + thao tác hàng loạt** (`04 §5` màn #7, đối chiếu Nhanh 03/11/2026).
 *
 * 📌 **Nhanh có `Thao tác ▾`** trên DS phiếu (`In các phiếu XNK đã chọn`, `Xuất dữ liệu`, `Gắn/Gỡ nhãn`,
 * `Đổi ngày`, `Xoá các dòng đã chọn`) và **checkbox từng dòng**. Ở đây làm 4 việc tương ứng với API hiện có:
 * **Ghi sổ** · **Huỷ** · **Xuất CSV** · **In phiếu**.
 *
 * 🚨 **VÌ SAO BULK GỌI API TỪNG PHIẾU (không có endpoint hàng loạt):** `POST /stock/bills/{id}/post` là
 * **một cửa duy nhất** đi qua `StockPostingService` (khoá phân tán + idempotency + append-only). Thêm
 * endpoint bulk ở client là **bỏ qua** các hàng rào đó. ⇒ gọi tuần tự, **dừng ở phiếu lỗi? KHÔNG** — ghi
 * nhận từng kết quả và báo **mã lỗi** (`eCommerce:Inventory:0010/0013…`), để người dùng biết phiếu nào đã
 * ghi sổ thật (gọi lại **an toàn** nhờ `0010`, không sinh bút toán thứ hai — D-29).
 *
 * 📌 **Không có bulk cho phiếu đã `Posted`**: nút chỉ tác động lên phiếu `Draft` đang chọn; phiếu khác
 * được liệt kê là "bỏ qua" **trước khi** gọi API (không để người dùng nhận 403 hàng loạt rồi hoang mang).
 */
export function BillTable({ rows, business }: { rows: BillRow[]; business: string }) {
  const router = useRouter();
  const [chon, setChon] = useState<Set<string>>(new Set());
  const [cotHien, setCotHien] = useState<Set<ColumnKey>>(() => new Set(COLUMNS.map((c) => c.key)));
  const [moChonCot, setMoChonCot] = useState(false);
  const [dangChay, setDangChay] = useState<"post" | "cancel" | null>(null);
  const [ketQua, setKetQua] = useState<KetQuaHang[] | null>(null);

  // Nhớ cột đã chọn giữa các lần tải (localStorage — P0, chưa cần API `saved-columns`).
  useEffect(() => {
    try {
      const raw = localStorage.getItem(STORAGE_COLS);
      if (!raw) return;
      const ds = JSON.parse(raw) as ColumnKey[];
      const hopLe = ds.filter((k) => COLUMNS.some((c) => c.key === k));
      if (hopLe.length > 0) setCotHien(new Set(hopLe));
    } catch {
      /* localStorage hỏng ⇒ dùng mặc định, không làm sập bảng */
    }
  }, []);

  function doiCot(key: ColumnKey) {
    setCotHien((truoc) => {
      const sau = new Set(truoc);
      if (sau.has(key)) {
        // ⚠️ Không cho ẩn HẾT: bảng rỗng trông như "mất dữ liệu".
        if (sau.size === 1) return truoc;
        sau.delete(key);
      } else {
        sau.add(key);
      }
      try {
        localStorage.setItem(STORAGE_COLS, JSON.stringify([...sau]));
      } catch {
        /* ignore */
      }
      return sau;
    });
  }

  const dongDangChon = useMemo(() => rows.filter((r) => chon.has(r.id)), [rows, chon]);
  const soDraft = dongDangChon.filter((r) => r.status === "Draft").length;
  const tatCaDangChon = rows.length > 0 && chon.size === rows.length;

  function doiChon(id: string) {
    setChon((truoc) => {
      const sau = new Set(truoc);
      sau.has(id) ? sau.delete(id) : sau.add(id);
      return sau;
    });
  }

  async function chayBulk(action: "post" | "cancel") {
    const mucTieu = dongDangChon.filter((r) => r.status === "Draft");

    if (mucTieu.length === 0) {
      // 🚨 **"Không có gì để làm" KHÔNG phải "lỗi"**: báo đỏ ở đây sẽ dạy người dùng bỏ qua màu đỏ.
      setKetQua([
        {
          number: "—",
          ok: true,
          boQua: true,
          text: `bỏ qua ${chon.size} phiếu không ở trạng thái Nháp — không có gì để ${action === "post" ? "ghi sổ" : "huỷ"}.`,
        },
      ]);
      return;
    }

    setDangChay(action);
    setKetQua(null);

    const kq: KetQuaHang[] = [];
    const boQua = dongDangChon.length - mucTieu.length;

    for (const phieu of mucTieu) {
      try {
        const res = await fetch(`/api/proxy/v1/stock/bills/${phieu.id}/${action}`, { method: "POST" });
        const raw = await res.text();
        let body: unknown = null;
        try {
          body = raw.length > 0 ? JSON.parse(raw) : null;
        } catch {
          body = null;
        }

        if (!res.ok) {
          const loi = docLoiApi(body, res.status);
          kq.push({ number: phieu.number, ok: false, text: loi.message, code: loi.code });
          continue;
        }

        const butToan =
          body && typeof body === "object" && "postedEntries" in body
            ? Number((body as { postedEntries?: number }).postedEntries ?? 0)
            : null;

        kq.push({
          number: phieu.number,
          ok: true,
          text: action === "post" ? `đã ghi sổ (${butToan ?? 0} bút toán)` : "đã huỷ",
        });
      } catch {
        kq.push({ number: phieu.number, ok: false, text: "không gọi được BFF (/api/proxy)" });
      }
    }

    if (boQua > 0) {
      kq.push({ number: "—", ok: true, boQua: true, text: `bỏ qua ${boQua} phiếu không ở trạng thái Nháp` });
    }

    setKetQua(kq);
    setDangChay(null);
    setChon(new Set());
    router.refresh();
  }

  function xuatCsv() {
    const dong = dongDangChon.length > 0 ? dongDangChon : rows;
    const cot = COLUMNS.filter((c) => cotHien.has(c.key));

    const giaTri = (r: BillRow, key: ColumnKey): string => {
      switch (key) {
        case "number":
          return r.number;
        case "date":
          return dateTime(r.occurredAtUtc);
        case "direction":
          return r.direction ? (DIRECTION_LABEL[r.direction] ?? r.direction) : "Không chiều";
        case "mode":
          return MODE_OPTIONS.find((m) => m.name === r.mode)?.label ?? (r.mode ?? "");
        case "type":
          return TYPE_LABEL[r.type] ?? r.type;
        case "warehouse":
          return r.warehouseCode;
        case "lines":
          return String(r.lineCount);
        case "amount":
          // `null` = có dòng chưa khai giá ⇒ để rỗng trong CSV, KHÔNG ghi 0 (D-37).
          return r.totalAmount === null ? "" : String(r.totalAmount);
        case "status":
          return STATUS_LABEL[r.status] ?? r.status;
      }
    };

    const boc = (v: string) => `"${v.replaceAll('"', '""')}"`;
    const noiDung = [
      cot.map((c) => boc(c.label)).join(";"),
      ...dong.map((r) => cot.map((c) => boc(giaTri(r, c.key))).join(";")),
    ].join("\r\n");

    // `\uFEFF` (BOM) để Excel đọc đúng tiếng Việt — thiếu nó là "Tiếng Việt không dấu".
    const blob = new Blob([`\uFEFF${noiDung}`], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `phieu-kho-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  function inPhieu() {
    const ids = (dongDangChon.length > 0 ? dongDangChon : rows).map((r) => r.id);
    if (ids.length === 0) return;
    window.open(`/${business}/inventory/bill/print?ids=${ids.join(",")}`, "_blank");
  }

  const soCot = COLUMNS.filter((c) => cotHien.has(c.key)).length + 1;

  // 📌 Ba con số **tách bạch**: "bỏ qua" (không ở trạng thái Nháp) không phải "lỗi" — gộp lại thì
  // người dùng tưởng thao tác hàng loạt hỏng và sẽ bấm lại vô ích.
  const soThanhCong = ketQua?.filter((x) => x.ok && !x.boQua).length ?? 0;
  const soLoi = ketQua?.filter((x) => !x.ok).length ?? 0;
  const soBoQua = ketQua?.filter((x) => x.boQua).length ?? 0;

  return (
    <div className="space-y-2">
      {/* ── Thanh thao tác hàng loạt (Nhanh: `Thao tác ▾`) ─────────────────────────────────── */}
      <div className="nh-card px-3 py-2 flex flex-wrap items-center gap-2">
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {chon.size === 0 ? "Chọn phiếu để thao tác hàng loạt" : `Đã chọn ${chon.size} phiếu`}
          {chon.size > 0 && soDraft !== chon.size ? ` · ${soDraft} Nháp` : ""}
        </span>

        <button
          type="button"
          className="nh-btn nh-btn-primary h-[32px]"
          disabled={chon.size === 0 || dangChay !== null}
          onClick={() => chayBulk("post")}
        >
          {dangChay === "post" ? "Đang ghi sổ…" : "Ghi sổ phiếu đã chọn"}
        </button>
        <button
          type="button"
          className="nh-btn h-[32px]"
          disabled={chon.size === 0 || dangChay !== null}
          onClick={() => chayBulk("cancel")}
        >
          {dangChay === "cancel" ? "Đang huỷ…" : "Huỷ phiếu đã chọn"}
        </button>
        <button type="button" className="nh-btn h-[32px]" disabled={rows.length === 0} onClick={xuatCsv}>
          Xuất CSV
        </button>
        <button type="button" className="nh-btn h-[32px]" disabled={rows.length === 0} onClick={inPhieu}>
          In phiếu
        </button>
        {chon.size > 0 && (
          <button type="button" className="text-[12px] underline" onClick={() => setChon(new Set())}>
            Bỏ chọn
          </button>
        )}

        <div className="ml-auto relative">
          <button type="button" className="nh-btn h-[32px]" onClick={() => setMoChonCot((v) => !v)}>
            ⚙ Cột ({soCot - 1}/{COLUMNS.length})
          </button>
          {moChonCot && (
            <div
              className="absolute right-0 mt-1 z-20 nh-card shadow-lg p-2 w-[200px]"
              style={{ background: "var(--bg-surface)" }}
            >
              {COLUMNS.map((c) => (
                <label key={c.key} className="flex items-center gap-2 px-1 py-1 text-[13px] cursor-pointer">
                  <input type="checkbox" checked={cotHien.has(c.key)} onChange={() => doiCot(c.key)} />
                  {c.label}
                </label>
              ))}
            </div>
          )}
        </div>
      </div>

      {ketQua && (
        <div className="nh-card p-3 text-[12px] space-y-1">
          <div className="font-medium">
            Kết quả thao tác hàng loạt ({soThanhCong} thành công
            {soLoi > 0 ? ` / ${soLoi} lỗi` : ""}
            {soBoQua > 0 ? ` / ${soBoQua} bỏ qua` : ""})
          </div>
          {ketQua.map((x, i) => (
            <div
              key={`${x.number}-${i}`}
              style={{ color: x.boQua ? "var(--text-muted)" : x.ok ? "var(--success)" : "var(--danger)" }}
            >
              {x.boQua ? "•" : x.ok ? "✓" : "✗"} {x.number}: {x.text}
              {x.code ? (
                <span className="tabular">
                  {" "}
                  — mã <code>{x.code}</code>
                </span>
              ) : null}
            </div>
          ))}
          <button type="button" className="text-[11px] underline" onClick={() => setKetQua(null)}>
            Đóng
          </button>
        </div>
      )}

      <div className="nh-card overflow-auto">
        <table className="nh-table">
          <thead>
            <tr>
              <th className="w-[40px]">
                <input
                  type="checkbox"
                  aria-label="Chọn tất cả phiếu trên trang"
                  checked={tatCaDangChon}
                  onChange={() => setChon(tatCaDangChon ? new Set() : new Set(rows.map((r) => r.id)))}
                />
              </th>
              {COLUMNS.filter((c) => cotHien.has(c.key)).map((c) => (
                <th key={c.key} className={c.align === "right" ? "text-right" : undefined} style={{ width: c.width }}>
                  {c.label}
                </th>
              ))}
              <th className="w-[60px]"></th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={soCot + 1} className="text-center py-8" style={{ color: "var(--text-muted)" }}>
                  Không có phiếu nào trên trang này.
                </td>
              </tr>
            )}

            {rows.map((phieu) => {
              const daChon = chon.has(phieu.id);
              return (
                <tr key={phieu.id} style={daChon ? { background: "#f8fafc" } : undefined}>
                  <td>
                    <input
                      type="checkbox"
                      aria-label={`Chọn phiếu ${phieu.number}`}
                      checked={daChon}
                      onChange={() => doiChon(phieu.id)}
                    />
                  </td>
                  {cotHien.has("number") && (
                    <td>
                      <Link
                        href={`/${business}/inventory/bill/${phieu.id}`}
                        className="font-medium"
                        style={{ color: "var(--primary)" }}
                      >
                        {phieu.number}
                      </Link>
                      {phieu.note && (
                        <div className="text-[11px] truncate max-w-[170px]" style={{ color: "var(--text-muted)" }}>
                          {phieu.note}
                        </div>
                      )}
                    </td>
                  )}
                  {cotHien.has("date") && (
                    <td className="tabular" style={{ color: "var(--text-muted)" }}>
                      {dateTime(phieu.occurredAtUtc)}
                    </td>
                  )}
                  {cotHien.has("direction") && (
                    <td>
                      <DirectionBadge direction={phieu.direction} />
                    </td>
                  )}
                  {cotHien.has("mode") && (
                    <td>{MODE_OPTIONS.find((m) => m.name === phieu.mode)?.label ?? (phieu.mode ?? "—")}</td>
                  )}
                  {cotHien.has("type") && (
                    <td style={{ color: "var(--text-muted)" }}>{TYPE_LABEL[phieu.type] ?? phieu.type}</td>
                  )}
                  {cotHien.has("warehouse") && (
                    <td className="text-[13px]" title={phieu.warehouseName}>
                      {phieu.warehouseCode}
                    </td>
                  )}
                  {cotHien.has("lines") && <td className="text-right tabular">{phieu.lineCount}</td>}
                  {cotHien.has("amount") && (
                    <td className="text-right tabular">{amount(phieu.totalAmount)}</td>
                  )}
                  {cotHien.has("status") && (
                    <td>
                      <StatusBadge status={phieu.status} />
                    </td>
                  )}
                  <td className="text-right">
                    <Link
                      href={`/${business}/inventory/bill/${phieu.id}`}
                      className="text-[12px]"
                      style={{ color: "var(--primary)" }}
                    >
                      Xem
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
