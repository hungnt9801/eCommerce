"use client";

import { useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import { amount, int, qty, vnDateToUtcIso } from "@/lib/format";
import {
  BILL_KINDS,
  DIRECTION_LABEL,
  findKind,
  modeLabelByValue,
  sumQuantity,
  suggestNumber,
  thanhTienDong,
  tongTienPhieu,
  withPrefix,
  type CreateStockDocumentDto,
  type WarehouseDto,
} from "@/lib/stock";
import { ProductPicker, type PickedProduct } from "./ProductPicker";

type DongNhap = {
  key: number;
  sku: string;
  /** Chuỗi (không phải number) để người dùng gõ được `1,5` / `1.5` / `1,` mà không bị nuốt ký tự. */
  quantity: string;
  vatPercent: string;
  /** Đơn giá (chuỗi, cùng lý do như `quantity`) — rỗng = **chưa khai giá** (`null`). */
  unitPrice: string;
  imeiSerials: string;
  /** Tên sản phẩm đã chọn — chỉ để hiển thị, KHÔNG gửi lên API (API chỉ nhận `sku`). */
  tenSanPham?: string;
};

let demDong = 1;

function dongRong(): DongNhap {
  return { key: demDong++, sku: "", quantity: "1", vatPercent: "", unitPrice: "", imeiSerials: "" };
}

/**
 * Đọc số người dùng gõ: `1,5` (vi-VN) · `1.5` (bàn phím số) · `1.234,5` (có phân cách nghìn) ⇒ `1.5`/`1234.5`.
 * ✅ Có dấu phẩy ⇒ dấu phẩy là **thập phân**, dấu chấm là phân cách nghìn.
 * ✅ Không có dấu phẩy và chỉ **một** dấu chấm ⇒ dấu chấm là **thập phân**.
 */
function soTuChuoi(chuoi: string): number {
  const t = chuoi.trim();
  if (t.length === 0) return NaN;

  if (t.includes(",")) {
    return Number(t.replace(/\./g, "").replace(",", "."));
  }

  const soDauCham = (t.match(/\./g) ?? []).length;
  return soDauCham <= 1 ? Number(t) : Number(t.replace(/\./g, ""));
}

/**
 * **Form tạo phiếu kho** (04 §5 màn #8, `POST /api/v1/stock/bills`).
 *
 * 🚨 **"CHỌN KIỂU → FORM ĐỘNG"** không phải trang trí: `StockDocument.ChuanHoaMode` **NÉM** khi
 * `Transfer`/`Stocktake` đi với sai `Type`, và `GoodsIssue` **bắt buộc** có `Mode` (`eCommerce:Inventory:0036`).
 * ⇒ Form chỉ cho chọn **9 tổ hợp hợp lệ** (`BILL_KINDS`) nên người dùng không thể gửi tổ hợp sai.
 *
 * 🚨 **`IdempotencyKey` sinh Ở CLIENT, giữ nguyên khi retry** (D-29): bấm Lưu 2 lần (hoặc mạng chập chờn)
 * ⇒ lần hai nhận `eCommerce:Inventory:0035`, **không** tạo phiếu thứ hai.
 */
export function BillForm({
  business,
  warehouses,
  defaultNumber,
  defaultDate,
  initialKind,
}: {
  business: string;
  warehouses: WarehouseDto[];
  defaultNumber: string;
  defaultDate: string;
  /** Kiểu giao dịch chọn sẵn (từ `?kind=` — mỗi loại phiếu của Nhanh có route riêng). */
  initialKind?: string;
}) {
  const router = useRouter();

  const khoHoatDong = useMemo(() => warehouses.filter((k) => k.isActive), [warehouses]);
  const [kindKey, setKindKey] = useState(initialKind ?? BILL_KINDS[0].key);
  const kind = findKind(kindKey);

  const [number, setNumber] = useState(defaultNumber);
  const [numberDaSua, setNumberDaSua] = useState(false);
  const [ngay, setNgay] = useState(defaultDate);
  const [warehouseId, setWarehouseId] = useState(khoHoatDong[0]?.id ?? "");
  const [toWarehouseId, setToWarehouseId] = useState("");
  const [note, setNote] = useState("");
  const [chietKhau, setChietKhau] = useState("");
  const [daTra, setDaTra] = useState("");
  const [dong, setDong] = useState<DongNhap[]>([dongRong()]);
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<{ text: string; code: string | null } | null>(null);

  // 🔑 Khoá chống trùng: sinh **một lần** cho cả vòng đời form (retry dùng lại ⇒ API chặn tạo phiếu 2).
  const idempotencyKey = useRef<string | null>(null);
  function layKhoa(): string {
    idempotencyKey.current ??= crypto.randomUUID();
    return idempotencyKey.current;
  }

  const soDongHopLe = dong.filter((d) => d.sku.trim().length > 0).length;
  const tongSoLuong = sumQuantity(dong.map((d) => ({ quantity: soTuChuoi(d.quantity) || 0 })));

  // 💰 **Dự phóng** trước khi lưu — sau khi lưu, số chính thức là của server (`TinhTienChungTu`).
  const chietKhauSo = chietKhau.trim().length > 0 ? soTuChuoi(chietKhau) : 0;
  const tien = tongTienPhieu(
    dong
      .filter((d) => d.sku.trim().length > 0)
      .map((d) => ({
        quantity: soTuChuoi(d.quantity) || 0,
        unitPrice: d.unitPrice.trim().length > 0 ? soTuChuoi(d.unitPrice) : null,
        vatPercent: d.vatPercent.trim().length > 0 ? soTuChuoi(d.vatPercent) : null,
      })),
    Number.isFinite(chietKhauSo) ? chietKhauSo : 0,
  );

  function doiKieuGiaoDich(key: string) {
    const moi = findKind(key);
    setKindKey(key);
    setNumber((hienTai) => (numberDaSua ? withPrefix(hienTai, moi.prefix) : suggestNumber(moi.prefix)));
    if (!moi.needsTargetWarehouse) {
      setToWarehouseId("");
    }
  }

  function capNhatDong(key: number, thayDoi: Partial<DongNhap>) {
    setDong((ds) => ds.map((d) => (d.key === key ? { ...d, ...thayDoi } : d)));
  }

  /** Chọn từ ProductPicker: điền vào dòng **trống SKU** đầu tiên, hết thì thêm dòng mới. */
  function chonSanPham(product: PickedProduct) {
    setDong((ds) => {
      const trong = ds.find((d) => d.sku.trim().length === 0);
      if (trong) {
        return ds.map((d) => (d.key === trong.key ? { ...d, sku: product.sku, tenSanPham: product.name } : d));
      }
      return [...ds, { ...dongRong(), sku: product.sku, tenSanPham: product.name }];
    });
  }

  async function gui() {
    setLoi(null);

    if (!warehouseId) {
      setLoi({ text: "Chọn kho trước khi lưu phiếu.", code: null });
      return;
    }

    if (kind.needsTargetWarehouse) {
      if (!toWarehouseId) {
        setLoi({ text: "Chuyển kho phải chọn kho đích.", code: null });
        return;
      }
      if (toWarehouseId === warehouseId) {
        setLoi({ text: "Kho đích phải KHÁC kho nguồn (chuyển kho cùng kho bị chặn).", code: null });
        return;
      }
    }

    const lines = dong
      .filter((d) => d.sku.trim().length > 0)
      .map((d) => {
        const sl = soTuChuoi(d.quantity);
        const vat = d.vatPercent.trim().length > 0 ? soTuChuoi(d.vatPercent) : null;

        const gia = d.unitPrice.trim().length > 0 ? soTuChuoi(d.unitPrice) : null;

        return {
          sku: d.sku.trim(),
          quantity: sl,
          vatPercent: vat !== null && Number.isFinite(vat) ? vat : null,
          unitPrice: gia !== null && Number.isFinite(gia) ? gia : null,
          imeiSerials: d.imeiSerials.trim().length > 0 ? d.imeiSerials.trim() : null,
        };
      });

    if (lines.length === 0) {
      setLoi({ text: "Cần ít nhất một dòng có SKU.", code: null });
      return;
    }

    const dongLoi = lines.find((d) => !Number.isFinite(d.quantity) || d.quantity < 1);
    if (dongLoi) {
      setLoi({
        text: `Dòng “${dongLoi.sku}”: số lượng phải ≥ 1 (API nhận tối đa 3 số lẻ, ví dụ 1,5).`,
        code: null,
      });
      return;
    }

    const body: CreateStockDocumentDto = {
      number: number.trim(),
      type: kind.type,
      warehouseId,
      occurredAtUtc: vnDateToUtcIso(ngay) ?? undefined,
      idempotencyKey: layKhoa(),
      lines,
    };

    if (kind.mode !== null) body.mode = kind.mode;
    if (kind.needsTargetWarehouse) body.toWarehouseId = toWarehouseId;
    if (kind.counterpartyType !== null) body.counterpartyType = kind.counterpartyType;
    if (note.trim().length > 0) body.note = note.trim();
    if (Number.isFinite(chietKhauSo) && chietKhauSo > 0) body.discountAmount = chietKhauSo;
    if (daTra.trim().length > 0 && Number.isFinite(soTuChuoi(daTra))) body.paidAmount = soTuChuoi(daTra);

    setDangGui(true);

    try {
      const res = await fetch("/api/proxy/v1/stock/bills", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      const raw = await res.text();
      let ketQua: unknown = null;
      try {
        ketQua = raw.length > 0 ? JSON.parse(raw) : null;
      } catch {
        ketQua = null;
      }

      if (!res.ok) {
        const failure = docLoiApi(ketQua, res.status);
        setLoi({ text: failure.message, code: failure.code });
        return;
      }

      const id =
        ketQua && typeof ketQua === "object" && "id" in ketQua ? String((ketQua as { id?: string }).id ?? "") : "";

      router.push(`/${business}/inventory/bill/${id}`);
    } catch {
      setLoi({ text: "Không gửi được yêu cầu tới BFF (/api/proxy).", code: null });
    } finally {
      setDangGui(false);
    }
  }

  if (warehouses.length === 0) {
    return (
      <div className="nh-card p-5 text-[13px]">
        <div className="font-semibold mb-1">Chưa có kho nào</div>
        <p style={{ color: "var(--text-muted)" }}>
          Phiếu kho phải gắn với một kho đang hoạt động. Tạo kho trước (API <code>POST /api/app/warehouse</code>),
          rồi quay lại màn này.
        </p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-[minmax(0,1fr)_320px] gap-3 items-start">
      {/* ── Cột trái: thông tin phiếu + dòng hàng ─────────────────────────────────────────────── */}
      <div className="space-y-3">
        <div className="nh-card p-4 space-y-3">
          <div className="flex items-baseline justify-between">
            <h2 className="text-[15px] font-semibold">Thông tin phiếu</h2>
            <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
              Lưu ⇒ phiếu ở trạng thái <b>Nháp</b> (chưa đổi tồn kho)
            </span>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <label className="block col-span-2">
              <span className="text-[12px] font-medium">Kiểu giao dịch</span>
              <select
                className="nh-input mt-1"
                value={kindKey}
                onChange={(e) => doiKieuGiaoDich(e.target.value)}
              >
                {BILL_KINDS.map((k) => (
                  <option key={k.key} value={k.key}>
                    {k.label}
                    {k.mode !== null ? ` · ${modeLabelByValue(k.mode)}` : ""}
                  </option>
                ))}
              </select>
            </label>

            <label className="block">
              <span className="text-[12px] font-medium">Số phiếu</span>
              <input
                className="nh-input mt-1"
                value={number}
                onChange={(e) => {
                  setNumberDaSua(true);
                  setNumber(e.target.value);
                }}
                maxLength={64}
                required
              />
            </label>

            <label className="block">
              <span className="text-[12px] font-medium">Ngày phiếu</span>
              <input className="nh-input mt-1" type="date" value={ngay} onChange={(e) => setNgay(e.target.value)} />
            </label>

            <label className="block">
              <span className="text-[12px] font-medium">{kind.needsTargetWarehouse ? "Kho nguồn" : "Kho"}</span>
              <select className="nh-input mt-1" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
                <option value="">— chọn kho —</option>
                {warehouses.map((k) => (
                  <option key={k.id} value={k.id} disabled={!k.isActive}>
                    {k.code} — {k.name}
                    {k.isActive ? "" : " (ngừng dùng)"}
                  </option>
                ))}
              </select>
            </label>

            {kind.needsTargetWarehouse ? (
              <label className="block">
                <span className="text-[12px] font-medium">Kho đích</span>
                <select
                  className="nh-input mt-1"
                  value={toWarehouseId}
                  onChange={(e) => setToWarehouseId(e.target.value)}
                >
                  <option value="">— chọn kho đích —</option>
                  {warehouses.map((k) => (
                    <option key={k.id} value={k.id} disabled={!k.isActive}>
                      {k.code} — {k.name}
                      {k.isActive ? "" : " (ngừng dùng)"}
                    </option>
                  ))}
                </select>
              </label>
            ) : (
              <label className="block">
                <span className="text-[12px] font-medium">
                  Đối tượng{kind.counterpartyType === null ? " (không áp dụng)" : ""}
                </span>
                <input
                  className="nh-input mt-1"
                  value={
                    kind.counterpartyType === null
                      ? "—"
                      : kind.counterpartyType === 2
                        ? "Nhà cung cấp"
                        : kind.counterpartyType === 1
                          ? "Khách hàng"
                          : "Khác"
                  }
                  readOnly
                  disabled
                />
              </label>
            )}

            <label className="block col-span-2">
              <span className="text-[12px] font-medium">Ghi chú</span>
              <input
                className="nh-input mt-1"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                maxLength={2048}
                placeholder="VD: nhập lô hàng tháng 11…"
              />
            </label>
          </div>

          {kind.hint && (
            <p className="text-[11px] px-3 py-2 rounded" style={{ background: "var(--bg-subtle)", color: "var(--text-muted)" }}>
              {kind.hint}
            </p>
          )}
        </div>

        {/* ── Dòng hàng ─────────────────────────────────────────────────────────────────────── */}
        <div className="nh-card p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-[15px] font-semibold">Hàng hoá ({dong.length} dòng)</h2>
            <button type="button" className="nh-btn" onClick={() => setDong((ds) => [...ds, dongRong()])}>
              + Thêm dòng
            </button>
          </div>

          <ProductPicker onPick={chonSanPham} />

          <div className="overflow-auto">
            <table className="nh-table">
              <thead>
                <tr>
                  <th className="w-[150px]">SKU</th>
                  <th className="w-[110px] text-right">Số lượng</th>
                  <th className="w-[120px] text-right">Đơn giá</th>
                  <th className="w-[90px] text-right">VAT %</th>
                  <th className="w-[120px] text-right">Thành tiền</th>
                  <th className="w-[180px]">Serial / IMEI</th>
                  <th className="w-[44px]"></th>
                </tr>
              </thead>
              <tbody>
                {dong.map((d) => (
                  <tr key={d.key}>
                    <td>
                      <input
                        className="nh-input"
                        value={d.sku}
                        onChange={(e) => capNhatDong(d.key, { sku: e.target.value })}
                        placeholder="SKU"
                        maxLength={64}
                      />
                      {d.tenSanPham && (
                        <div className="text-[11px] mt-1" style={{ color: "var(--text-muted)" }}>
                          {d.tenSanPham}
                        </div>
                      )}
                    </td>
                    <td>
                      <input
                        className="nh-input text-right tabular"
                        value={d.quantity}
                        inputMode="decimal"
                        onChange={(e) => capNhatDong(d.key, { quantity: e.target.value })}
                        placeholder="1"
                      />
                    </td>
                    <td>
                      <input
                        className="nh-input text-right tabular"
                        value={d.unitPrice}
                        inputMode="decimal"
                        onChange={(e) => capNhatDong(d.key, { unitPrice: e.target.value })}
                        placeholder="—"
                      />
                    </td>
                    <td>
                      <input
                        className="nh-input text-right tabular"
                        value={d.vatPercent}
                        inputMode="decimal"
                        onChange={(e) => capNhatDong(d.key, { vatPercent: e.target.value })}
                        placeholder="—"
                      />
                    </td>
                    <td className="text-right tabular text-[13px]">
                      {/* 📌 Số **dự phóng**; sau khi lưu, số chính thức là `amount` do API trả. */}
                      {amount(
                        thanhTienDong(
                          soTuChuoi(d.quantity) || 0,
                          d.unitPrice.trim().length > 0 ? soTuChuoi(d.unitPrice) : null,
                        ),
                      )}
                    </td>
                    <td>
                      <input
                        className="nh-input"
                        value={d.imeiSerials}
                        onChange={(e) => capNhatDong(d.key, { imeiSerials: e.target.value })}
                        placeholder="phân tách bằng dấu phẩy"
                        maxLength={4000}
                      />
                    </td>
                    <td className="text-center">
                      <button
                        type="button"
                        className="text-[12px]"
                        style={{ color: "var(--danger)" }}
                        onClick={() => setDong((ds) => (ds.length === 1 ? [dongRong()] : ds.filter((x) => x.key !== d.key)))}
                        title="Xoá dòng"
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Số lượng nhận tới <b>3 số lẻ</b>; tiền làm tròn <b>2 số lẻ</b> theo <b>từng dòng</b> rồi mới cộng (D-37).
            Nếu khai Serial/IMEI thì <b>số serial phải bằng số lượng</b> (nếu không API trả{" "}
            <code>eCommerce:Inventory:0029</code>).
          </p>
        </div>
      </div>

      {/* ── Cột phải: tóm tắt + Lưu ───────────────────────────────────────────────────────────── */}
      <aside className="nh-card p-4 space-y-3 sticky top-0">
        <h2 className="text-[15px] font-semibold">Tóm tắt</h2>

        <dl className="text-[13px] space-y-2">
          <div className="flex justify-between gap-2">
            <dt style={{ color: "var(--text-muted)" }}>Kiểu</dt>
            <dd className="text-right font-medium">{kind.label}</dd>
          </div>
          <div className="flex justify-between gap-2">
            <dt style={{ color: "var(--text-muted)" }}>Chiều</dt>
            <dd className="text-right">
              {kind.direction ? DIRECTION_LABEL[kind.direction] : "Không chiều (kiểm kê)"}
            </dd>
          </div>
          <div className="flex justify-between gap-2">
            <dt style={{ color: "var(--text-muted)" }}>Dòng hợp lệ</dt>
            <dd className="text-right tabular">
              {int(soDongHopLe)} / {int(dong.length)}
            </dd>
          </div>
          <div className="flex justify-between gap-2">
            <dt style={{ color: "var(--text-muted)" }}>Tổng số lượng</dt>
            <dd className="text-right tabular font-semibold">{qty(tongSoLuong)}</dd>
          </div>
        </dl>

        <div className="border-t pt-3" style={{ borderColor: "var(--border)" }}>
          <label className="block">
            <span className="text-[12px] font-medium">Chiết khấu (đ)</span>
            <input
              className="nh-input mt-1 text-right tabular"
              value={chietKhau}
              inputMode="decimal"
              onChange={(e) => setChietKhau(e.target.value)}
              placeholder="0"
            />
          </label>

          <label className="block mt-2">
            <span className="text-[12px] font-medium">Đã trả (đ)</span>
            <input
              className="nh-input mt-1 text-right tabular"
              value={daTra}
              inputMode="decimal"
              onChange={(e) => setDaTra(e.target.value)}
              placeholder="—"
            />
          </label>
        </div>

        <dl className="text-[13px] space-y-2 border-t pt-3" style={{ borderColor: "var(--border)" }}>
          <div className="flex justify-between gap-2">
            <dt style={{ color: "var(--text-muted)" }}>Tổng tiền hàng</dt>
            <dd className="text-right tabular">{amount(tien.tienHang)}</dd>
          </div>
          <div className="flex justify-between gap-2">
            <dt style={{ color: "var(--text-muted)" }}>VAT</dt>
            <dd className="text-right tabular">{amount(tien.vat)}</dd>
          </div>
          <div className="flex justify-between gap-2">
            <dt style={{ color: "var(--text-muted)" }}>Thanh toán</dt>
            <dd className="text-right tabular font-semibold">{amount(tien.thanhToan)}</dd>
          </div>
        </dl>

        <p className="text-[11px]" style={{ color: "var(--text-muted)" }}>
          Số <b>dự phóng</b> trước khi lưu (server tính lại bằng cùng công thức). Dòng <b>chưa khai giá</b> ⇒
          tổng hiện <b>—</b> chứ không phải 0; VAT chỉ cộng khi dòng có %VAT và đơn giá (D-37).
        </p>

        {loi && (
          <div className="text-[12px] px-3 py-2 rounded" style={{ background: "#fef2f2", color: "var(--danger)" }}>
            <div>{loi.text}</div>
            {loi.code && (
              <div className="mt-0.5 tabular" style={{ fontSize: 11 }}>
                mã: <code>{loi.code}</code>
              </div>
            )}
          </div>
        )}

        <button type="button" className="nh-btn nh-btn-primary w-full justify-center" disabled={dangGui} onClick={gui}>
          {dangGui ? "Đang lưu…" : "Lưu phiếu (Nháp)"}
        </button>

        <Link href={`/${business}/inventory/bill`} className="nh-btn w-full justify-center">
          Huỷ
        </Link>
      </aside>
    </div>
  );
}
