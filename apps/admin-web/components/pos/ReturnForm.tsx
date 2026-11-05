"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { docLoiApi } from "@/lib/api-error";
import { money, qty } from "@/lib/format";
import {
  PAYMENT_OPTIONS,
  paymentLabel,
  type RetailBillDto,
  type RetailPaymentMethod,
  type RetailReturnDto,
} from "@/lib/pos";

type DongTra = { sku: string; quantity: number; toiDa: number; donGia: number };

/**
 * **Màn #14 — Trả hàng** (`04 §5` màn #14 · `nhanh-pos-module-map.md` §5.4).
 *
 * 🚨 **LUẬT TIỀN ĐƯỢC SERVER QUYẾT ĐỊNH** (`RetailReturn.TinhTien`): `hàng trả − phí` ⇒ **trừ công nợ trước**
 * ⇒ còn lại mới hoàn tiền. UI **hiện** ước tính để thu ngân nói trước với khách, nhưng số **lưu** là số
 * server trả về — hai bên lệch thì hiện số server (và nêu rõ ở dòng "Kết quả").
 *
 * 📌 **P0 chỉ trả TỪ HOÁ ĐƠN** (`D-42`): trả không hoá đơn không đảo được công nợ (không biết trả cho hoá đơn
 * nào) ⇒ ghi ở `handover-n7` §5.
 */
export function ReturnForm({ business }: { business: string }) {
  const router = useRouter();

  const [tuKhoa, setTuKhoa] = useState("");
  const [dsHoaDon, setDsHoaDon] = useState<RetailBillDto[]>([]);
  const [hoaDon, setHoaDon] = useState<RetailBillDto | null>(null);
  const [dongTra, setDongTra] = useState<DongTra[]>([]);
  const [phi, setPhi] = useState(0);
  const [phuongThuc, setPhuongThuc] = useState<RetailPaymentMethod>(1);
  const [ghiChu, setGhiChu] = useState("");
  const [dangGui, setDangGui] = useState(false);
  const [loi, setLoi] = useState<string | null>(null);
  const [ketQua, setKetQua] = useState<RetailReturnDto | null>(null);

  async function timHoaDon() {
    setLoi(null);
    setKetQua(null);

    try {
      const res = await fetch(`/api/proxy/v1/pos/bills?q=${encodeURIComponent(tuKhoa.trim())}&maxResultCount=20`);

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(`Tìm hoá đơn lỗi ${failure.code ?? res.status}: ${failure.message}`);
        return;
      }

      const body = (await res.json()) as { items?: RetailBillDto[] | null };
      setDsHoaDon(body.items ?? []);
      setHoaDon(null);
      setDongTra([]);

      if ((body.items ?? []).length === 0) {
        setLoi("Không thấy hoá đơn nào khớp số hoá đơn.");
      }
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    }
  }

  async function chonHoaDon(id: string) {
    setLoi(null);
    setKetQua(null);

    try {
      const res = await fetch(`/api/proxy/v1/pos/bills/${id}`);

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(`Mở hoá đơn lỗi ${failure.code ?? res.status}: ${failure.message}`);
        return;
      }

      const bill = (await res.json()) as RetailBillDto;
      setHoaDon(bill);

      // 📌 Mỗi dòng chỉ trả được `quantity − returnedQuantity` (server có CHECK y hệt ở DB).
      setDongTra(
        (bill.lines ?? []).map((d) => ({
          sku: d.sku ?? "",
          quantity: 0,
          toiDa: Number(d.quantity ?? 0) - Number(d.returnedQuantity ?? 0),
          donGia: Number(d.unitPrice ?? 0),
        })),
      );
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    }
  }

  const tienHangTra = dongTra.reduce((tong, d) => tong + d.quantity * d.donGia, 0);
  const conNo = Number(hoaDon?.debtAmount ?? 0);
  const sauPhi = Math.max(0, tienHangTra - phi);
  const giamNo = Math.min(sauPhi, conNo);
  const hoanTien = sauPhi - giamNo;

  async function gui() {
    if (!hoaDon) return;

    const lines = dongTra.filter((d) => d.quantity > 0).map((d) => ({ sku: d.sku, quantity: d.quantity }));

    if (lines.length === 0) {
      setLoi("Chưa chọn dòng nào để trả (SL > 0).");
      return;
    }

    setDangGui(true);
    setLoi(null);

    try {
      const res = await fetch("/api/proxy/v1/pos/returns", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          retailBillId: hoaDon.id,
          returnFeeAmount: phi,
          refundMethod: phuongThuc,
          note: ghiChu || null,
          lines,
        }),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoi(`Tạo phiếu trả lỗi ${failure.code ?? res.status}: ${failure.message}`);
        return;
      }

      const dt = (await res.json()) as RetailReturnDto;
      setKetQua(dt);
      setDongTra((cu) => cu.map((d) => ({ ...d, quantity: 0 })));
      router.refresh();
    } catch {
      setLoi("Không gọi được API (mất mạng?).");
    } finally {
      setDangGui(false);
    }
  }

  return (
    <div className="space-y-3">
      <div className="nh-card p-3 flex flex-wrap items-end gap-2">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Tìm hoá đơn gốc (số hoá đơn)</div>
          <input
            className="nh-input mt-0.5"
            value={tuKhoa}
            onChange={(e) => setTuKhoa(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                void timHoaDon();
              }
            }}
            placeholder="POS-20261104-0001"
            style={{ width: 260 }}
          />
        </label>

        <button type="button" className="nh-btn" onClick={() => void timHoaDon()}>
          Tìm
        </button>

        {dsHoaDon.length > 0 && (
          <label className="text-[12px]">
            <div style={{ color: "var(--text-muted)" }}>Chọn hoá đơn ({dsHoaDon.length})</div>
            <select
              className="nh-input mt-0.5"
              defaultValue=""
              onChange={(e) => {
                if (e.target.value) void chonHoaDon(e.target.value);
              }}
              style={{ width: 300 }}
            >
              <option value="">— chọn —</option>
              {dsHoaDon.map((h) => (
                <option key={h.id} value={h.id}>
                  {h.number} · {money(h.totalPayment)} đ · {h.status === "Voided" ? "ĐÃ HUỶ" : "đã bán"}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      {loi && (
        <div className="nh-card p-3 text-[12px]" style={{ color: "var(--danger)" }}>
          {loi}
        </div>
      )}

      {ketQua && (
        <div className="nh-card p-3 text-[12px]" style={{ background: "#ecfdf5", color: "#065f46" }}>
          Đã tạo phiếu trả <b>{ketQua.number}</b> · hàng trả {money(ketQua.goodsAmount)} đ · giảm nợ{" "}
          {money(ketQua.debtReductionAmount)} đ · hoàn tiền {money(ketQua.refundAmount)} đ (
          {paymentLabel(ketQua.refundMethod)}).
        </div>
      )}

      {hoaDon && (
        <>
          <div className="nh-card p-4 text-[12px] grid grid-cols-2 lg:grid-cols-4 gap-3">
            <O nhan="Hoá đơn">{hoaDon.number}</O>
            <O nhan="Phải trả">{money(hoaDon.totalPayment)} đ</O>
            <O nhan="Đã thu">{money(hoaDon.paidAmount)} đ</O>
            <O nhan="Còn nợ">
              <span style={{ color: conNo > 0 ? "var(--danger)" : "inherit" }}>{money(conNo)} đ</span>
            </O>
          </div>

          <div className="nh-card overflow-x-auto">
            <table className="w-full text-[12px]">
              <thead>
                <tr style={{ color: "var(--text-muted)" }}>
                  <th className="text-left px-3 py-2 font-medium">Sản phẩm</th>
                  <th className="text-right px-2 py-2 font-medium">Đã mua</th>
                  <th className="text-right px-2 py-2 font-medium">Đã trả</th>
                  <th className="text-right px-2 py-2 font-medium">Trả được</th>
                  <th className="text-right px-2 py-2 font-medium">Đơn giá</th>
                  <th className="text-right px-2 py-2 font-medium">SL trả</th>
                  <th className="text-right px-2 py-2 font-medium">Tiền trả</th>
                </tr>
              </thead>
              <tbody>
                {(hoaDon.lines ?? []).map((d) => {
                  const dong = dongTra.find((x) => x.sku === d.sku);

                  return (
                    <tr key={d.id} className="border-t" style={{ borderColor: "var(--border)" }}>
                      <td className="px-3 py-2">
                        <div className="font-medium">{d.productName ?? d.sku}</div>
                        <code className="text-[10px]">{d.sku}</code>
                      </td>
                      <td className="px-2 py-2 text-right tabular">{qty(d.quantity)}</td>
                      <td className="px-2 py-2 text-right tabular">{qty(d.returnedQuantity)}</td>
                      <td className="px-2 py-2 text-right tabular">{qty(dong?.toiDa ?? 0)}</td>
                      <td className="px-2 py-2 text-right tabular">{money(d.unitPrice)}</td>
                      <td className="px-2 py-2 text-right">
                        <input
                          className="nh-input tabular text-right"
                          type="number"
                          min={0}
                          max={dong?.toiDa ?? 0}
                          step="0.001"
                          value={dong?.quantity ?? 0}
                          disabled={(dong?.toiDa ?? 0) <= 0}
                          onChange={(e) => {
                            const gt = Math.min(Number(e.target.value), dong?.toiDa ?? 0);
                            setDongTra((cu) =>
                              cu.map((x) => (x.sku === d.sku ? { ...x, quantity: Math.max(0, gt) } : x)),
                            );
                          }}
                          style={{ width: 90 }}
                        />
                      </td>
                      <td className="px-2 py-2 text-right tabular">
                        {money((dong?.quantity ?? 0) * Number(d.unitPrice ?? 0))}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-[1fr_340px] gap-3">
            <div className="nh-card p-3 space-y-2 text-[12px]">
              <label className="block">
                <div style={{ color: "var(--text-muted)" }}>Phí trả hàng</div>
                <input
                  className="nh-input tabular"
                  type="number"
                  min={0}
                  value={phi}
                  onChange={(e) => setPhi(Number(e.target.value))}
                  style={{ width: 160 }}
                />
              </label>

              <label className="block">
                <div style={{ color: "var(--text-muted)" }}>Phương thức hoàn tiền</div>
                <select
                  className="nh-input"
                  value={phuongThuc}
                  onChange={(e) => setPhuongThuc(Number(e.target.value) as RetailPaymentMethod)}
                  style={{ width: 220 }}
                >
                  {PAYMENT_OPTIONS.filter((o) => o.value !== 4).map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.icon} {o.label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="block">
                <div style={{ color: "var(--text-muted)" }}>Ghi chú</div>
                <input
                  className="nh-input w-full"
                  value={ghiChu}
                  onChange={(e) => setGhiChu(e.target.value)}
                  placeholder="Lý do trả / tình trạng hàng…"
                />
              </label>
            </div>

            <div className="nh-card p-3 space-y-2 text-[12px]">
              <Row nhan="Tiền hàng trả" gt={`${money(tienHangTra)} đ`} />
              <Row nhan="Phí trả hàng" gt={`−${money(phi)} đ`} />
              <Row nhan="Trừ công nợ TRƯỚC" gt={`${money(giamNo)} đ`} manh />
              <div className="border-t pt-2" style={{ borderColor: "var(--border)" }} />
              <Row nhan="Hoàn tiền khách" gt={`${money(hoanTien)} đ`} manh />

              <button type="button" className="nh-btn w-full" disabled={dangGui} onClick={() => void gui()}>
                {dangGui ? "Đang lưu…" : "Lưu phiếu trả"}
              </button>

              <div className="text-[10px]" style={{ color: "var(--text-muted)" }}>
                Số tiền LƯU do server tính (hàng trả − phí ⇒ trừ nợ ⇒ hoàn tiền). Nếu lệch với ước tính trên,
                kết quả server là số đúng.
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function Row({ nhan, gt, manh }: { nhan: string; gt: string; manh?: boolean }) {
  return (
    <div className="flex justify-between">
      <span style={{ color: "var(--text-muted)" }}>{nhan}</span>
      <span className="tabular" style={{ fontWeight: manh ? 600 : 400 }}>
        {gt}
      </span>
    </div>
  );
}

function O({ nhan, children }: { nhan: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        {nhan}
      </div>
      <div className="tabular">{children}</div>
    </div>
  );
}
