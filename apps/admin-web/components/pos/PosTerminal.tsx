"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { docLoiApi } from "@/lib/api-error";
import { money } from "@/lib/format";
import {
  PAYMENT_OPTIONS,
  TY_LE_DIEM,
  type CashSessionDto,
  type CreateRetailBillResultDto,
  type DongDraft,
  type OutboxItem,
  type RetailPaymentMethod,
  type TabHoaDon,
  type ThanhToanDraft,
  dayVaoOutbox,
  docOutbox,
  docTabs,
  luuTab,
  tabMoi,
  thanhPayload,
  tinhTam,
  xoaOutbox,
  xoaTab,
} from "@/lib/pos";

type SanPham = {
  sku: string;
  name: string;
  price: number | null;
  barcode?: string | null;
};

type Khach = { id: string; name: string };

/**
 * 🧾 **MÀN POS BÁN LẺ** (`WP-06`, `04 §5` màn #12 · `04 §6.3`).
 *
 * 🚨 **BA YÊU CẦU KHÔNG ĐƯỢC BỎ (đo được ở DoD `WP-06`):**
 *   ① **Đa tab** lưu **IndexedDB** ⇒ `F5` **KHÔNG** mất hoá đơn đang soạn;
 *   ② **Phím tắt** F1/F3/F4/F6/F8/F9/F10 đúng Nhanh — thu ngân làm 100 hoá đơn/ngày **không** dùng chuột;
 *   ③ **Rớt mạng vẫn lưu**: hoá đơn vào **outbox** IndexedDB và đồng bộ lại sau (idempotent theo
 *      `clientBillId`) ⇒ mất mạng **không** làm mất doanh thu, và gửi lại **không** tạo hoá đơn thứ hai.
 *
 * 📌 **Tiền hiển thị do `tinhTam` tính (cùng công thức server)** — nhưng khi lưu, **server** là nguồn sự thật:
 * nếu lệch, UI hiện số server trả về và không tự sửa.
 */
export function PosTerminal({
  business,
  warehouseId,
  warehouseName,
  sessionBanDau,
}: {
  business: string;
  warehouseId: string;
  warehouseName: string;
  sessionBanDau: CashSessionDto | null;
}) {
  const [tabs, setTabs] = useState<TabHoaDon[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [session, setSession] = useState<CashSessionDto | null>(sessionBanDau);
  const [q, setQ] = useState("");
  const [ketQuaTim, setKetQuaTim] = useState<SanPham[]>([]);
  const [dangTim, setDangTim] = useState(false);
  const [chuKhach, setChuKhach] = useState("");
  const [khachTim, setKhachTim] = useState<Khach[]>([]);
  const [outbox, setOutbox] = useState<OutboxItem[]>([]);
  const [thongBao, setThongBao] = useState<string | null>(null);
  const [loiHeThong, setLoiHeThong] = useState<string | null>(null);
  const [moKetCa, setMoKetCa] = useState(false);
  const [tienBanGiao, setTienBanGiao] = useState(0);
  const [chiPhiKhac, setChiPhiKhac] = useState(0);
  const [tienDauCa, setTienDauCa] = useState(0);
  const [dangMoCa, setDangMoCa] = useState(false);

  const oTim = useRef<HTMLInputElement | null>(null);
  const oKhach = useRef<HTMLInputElement | null>(null);
  const oChietKhau = useRef<HTMLInputElement | null>(null);
  const oTienMat = useRef<HTMLInputElement | null>(null);
  const hopTim = useRef<HTMLDivElement | null>(null);

  const tab = tabs.find((t) => t.clientBillId === activeId) ?? null;
  const tong = tab ? tinhTam(tab) : null;

  // ── Khôi phục tab + outbox từ IndexedDB (F5 không mất tab) ─────────────────────────────────────
  useEffect(() => {
    let huy = false;

    (async () => {
      try {
        const daLuu = await docTabs();
        const cho = await docOutbox();

        if (huy) return;

        if (daLuu.length === 0) {
          const moi = tabMoi();
          setTabs([moi]);
          setActiveId(moi.clientBillId);
          await luuTab(moi);
        } else {
          // 🔒 Giữ **THỨ TỰ tab** như lúc thu ngân đang làm: `getAll()` của IndexedDB trả theo khoá
          // (`clientBillId` = UUID ngẫu nhiên) ⇒ không sắp lại thì sau F5 tab **đổi chỗ** (đã đo được).
          const daSap = [...daLuu].sort((a, b) => (a.taoLuc ?? "").localeCompare(b.taoLuc ?? ""));

          setTabs(daSap);
          setActiveId(daSap[0].clientBillId);
        }

        setOutbox(cho);
      } catch (e) {
        setLoiHeThong(
          e instanceof Error ? e.message : "Không đọc được IndexedDB ⇒ tab hoá đơn KHÔNG được lưu trên máy.",
        );
      }
    })();

    return () => {
      huy = true;
    };
  }, []);

  const capNhat = useCallback((id: string, sua: (t: TabHoaDon) => TabHoaDon) => {
    setTabs((cu) => {
      const moi = cu.map((t) => (t.clientBillId === id ? sua(t) : t));
      const daSua = moi.find((t) => t.clientBillId === id);

      if (daSua) {
        // 📌 Lưu bất đồng bộ: lỗi ghi IndexedDB KHÔNG được làm sập màn bán hàng (đang có khách chờ).
        void luuTab(daSua).catch(() => setLoiHeThong("Không ghi được IndexedDB ⇒ tab có thể mất khi F5."));
      }

      return moi;
    });
  }, []);

  const themTab = useCallback(() => {
    const moi = tabMoi();
    setTabs((cu) => [...cu, moi]);
    setActiveId(moi.clientBillId);
    void luuTab(moi);
    oTim.current?.focus();
  }, []);

  const dongTab = useCallback(
    (id: string) => {
      setTabs((cu) => {
        const moi = cu.filter((t) => t.clientBillId !== id);

        if (moi.length === 0) {
          const thay = tabMoi();
          void luuTab(thay);
          setActiveId(thay.clientBillId);
          return [thay];
        }

        if (activeId === id) {
          setActiveId(moi[0].clientBillId);
        }

        return moi;
      });

      void xoaTab(id);
    },
    [activeId],
  );

  // ── Tìm sản phẩm / quét mã vạch ────────────────────────────────────────────────────────────────
  useEffect(() => {
    const tu = q.trim();

    if (tu.length < 2) {
      setKetQuaTim([]);
      return;
    }

    const hen = setTimeout(async () => {
      setDangTim(true);
      try {
        // 📌 Ưu tiên **tra mã vạch** khi chuỗi trông như mã (không dấu cách, ≥ 6 ký tự): barcode là
        // thứ máy quét gửi vào; tra theo `q` trước sẽ ra danh sách rồi bắt thu ngân bấm chọn — chậm hơn.
        if (/^[0-9A-Za-z._-]{6,}$/.test(tu)) {
          const tra = await fetch(`/api/proxy/v1/products/lookup?barcode=${encodeURIComponent(tu)}`);

          if (tra.ok) {
            const sp = (await tra.json()) as {
              id?: string;
              name?: string;
              sku?: string | null;
              barcode?: string | null;
              price?: number | null;
            };

            if (sp.sku) {
              setKetQuaTim([{ sku: sp.sku, name: sp.name ?? sp.sku, price: sp.price ?? null, barcode: sp.barcode }]);
              return;
            }
          }
        }

        const res = await fetch(`/api/proxy/v1/products?q=${encodeURIComponent(tu)}&pageSize=8`);

        if (!res.ok) {
          setKetQuaTim([]);
          return;
        }

        const body = (await res.json()) as { items?: Array<Record<string, unknown>> | null };
        const ds = (body.items ?? [])
          .map((x) => ({
            sku: typeof x.sku === "string" ? x.sku : "",
            name: typeof x.name === "string" ? x.name : "",
            price: typeof x.price === "number" ? x.price : null,
            barcode: typeof x.barcode === "string" ? x.barcode : null,
          }))
          .filter((x) => x.sku.length > 0);

        setKetQuaTim(ds);
      } finally {
        setDangTim(false);
      }
    }, 250);

    return () => clearTimeout(hen);
  }, [q]);

  const themDong = useCallback(
    (sp: SanPham) => {
      if (!tab) return;

      capNhat(tab.clientBillId, (t) => {
        const cu = t.lines.findIndex((d) => d.sku === sp.sku);

        if (cu >= 0) {
          // 📌 "Quét 2 lần = SL 2" — đúng Nhanh; thu ngân không phải sửa cột SL cho món thường.
          const lines = t.lines.map((d, i) => (i === cu ? { ...d, quantity: d.quantity + 1 } : d));
          return { ...t, lines };
        }

        const dong: DongDraft = {
          sku: sp.sku,
          productName: sp.name,
          quantity: 1,
          unitPrice: sp.price ?? 0,
          discountAmount: 0,
          vatPercent: null,
        };

        return { ...t, lines: [...t.lines, dong] };
      });

      setQ("");
      setKetQuaTim([]);
      oTim.current?.focus();
    },
    [capNhat, tab],
  );

  const suaDong = useCallback(
    (sku: string, sua: (d: DongDraft) => DongDraft) => {
      if (!tab) return;

      capNhat(tab.clientBillId, (t) => ({
        ...t,
        lines: t.lines.map((d) => (d.sku === sku ? sua(d) : d)),
      }));
    },
    [capNhat, tab],
  );

  const xoaDong = useCallback(
    (sku: string) => {
      if (!tab) return;

      capNhat(tab.clientBillId, (t) => ({ ...t, lines: t.lines.filter((d) => d.sku !== sku) }));
    },
    [capNhat, tab],
  );

  // ── Khách hàng (bắt buộc khi ghi nợ) ───────────────────────────────────────────────────────────
  useEffect(() => {
    const tu = chuKhach.trim();

    if (tu.length < 2) {
      setKhachTim([]);
      return;
    }

    const hen = setTimeout(async () => {
      try {
        const res = await fetch(
          `/api/proxy/app/customer/danh-sach-phan-trang?keyword=${encodeURIComponent(tu)}&maxResultCount=6`,
        );

        if (!res.ok) {
          setKhachTim([]);
          return;
        }

        const body = (await res.json()) as { items?: Array<{ id?: string; name?: string }> | null };

        setKhachTim(
          (body.items ?? [])
            .filter((x): x is { id: string; name?: string } => typeof x.id === "string")
            .map((x) => ({ id: x.id, name: x.name ?? x.id.slice(0, 8) })),
        );
      } catch {
        setKhachTim([]);
      }
    }, 300);

    return () => clearTimeout(hen);
  }, [chuKhach]);

  // ── Thanh toán ────────────────────────────────────────────────────────────────────────────────
  const themThanhToan = useCallback(
    (method: RetailPaymentMethod, amount: number) => {
      if (!tab) return;

      capNhat(tab.clientBillId, (t) => ({
        ...t,
        payments: [...t.payments, { method, amount }],
      }));
    },
    [capNhat, tab],
  );

  const suaThanhToan = useCallback(
    (chiSo: number, sua: (p: ThanhToanDraft) => ThanhToanDraft) => {
      if (!tab) return;

      capNhat(tab.clientBillId, (t) => ({
        ...t,
        payments: t.payments.map((p, i) => (i === chiSo ? sua(p) : p)),
      }));
    },
    [capNhat, tab],
  );

  const xoaThanhToan = useCallback(
    (chiSo: number) => {
      if (!tab) return;

      capNhat(tab.clientBillId, (t) => ({ ...t, payments: t.payments.filter((_, i) => i !== chiSo) }));
    },
    [capNhat, tab],
  );

  // ── LƯU (F9) / LƯU + IN (F10) ────────────────────────────────────────────────────────────────
  const luu = useCallback(
    async (inAn: boolean) => {
      if (!tab) return;

      if (tab.lines.length === 0) {
        setThongBao("Hoá đơn CHƯA có dòng hàng — quét mã hoặc tìm sản phẩm trước (F3).");
        return;
      }

      const payload = thanhPayload({ ...tab, trangThai: "saving" }, warehouseId, session?.id ?? null);

      capNhat(tab.clientBillId, (t) => ({ ...t, trangThai: "saving", loi: null }));

      try {
        const res = await fetch("/api/proxy/v1/pos/bills", {
          method: "POST",
          headers: { "Content-Type": "application/json", "Idempotency-Key": tab.clientBillId },
          body: JSON.stringify(payload),
        });

        if (res.status === 401 || res.status === 403) {
          const body = await res.json().catch(() => null);
          const failure = docLoiApi(body, res.status);

          capNhat(tab.clientBillId, (t) => ({
            ...t,
            trangThai: "error",
            loi: failure.code ? `${failure.code} — ${failure.message}` : failure.message,
          }));

          return;
        }

        if (!res.ok) {
          const body = await res.json().catch(() => null);
          const failure = docLoiApi(body, res.status);

          // 📌 5xx = server/sự cố ⇒ **đưa vào outbox** thay vì bắt thu ngân nhập lại (đúng tinh thần offline).
          if (res.status >= 500) {
            await luuOffline(payload, tab.clientBillId);
            return;
          }

          capNhat(tab.clientBillId, (t) => ({
            ...t,
            trangThai: "error",
            loi: failure.code ? `${failure.code} — ${failure.message}` : failure.message,
          }));

          return;
        }

        const ketQua = (await res.json()) as CreateRetailBillResultDto;
        const soHoaDon = ketQua.bill?.number ?? "(không có số)";

        setThongBao(
          ketQua.alreadyExisted
            ? `Hoá đơn ${soHoaDon} ĐÃ được lưu trước đó (gửi lại) — không tạo hoá đơn thứ hai.`
            : `Đã lưu hoá đơn ${soHoaDon} · phải trả ${money(ketQua.bill?.totalPayment ?? 0)} đ` +
                (Number(ketQua.bill?.debtAmount ?? 0) > 0 ? ` · còn nợ ${money(ketQua.bill?.debtAmount)} đ` : ""),
        );

        dongTab(tab.clientBillId);

        if (inAn && ketQua.bill?.id) {
          window.open(`/${business}/print/bill/${ketQua.bill.id}`, "_blank", "width=420,height=640");
        }
      } catch {
        // 🚨 MẤT MẠNG: `fetch` ném `TypeError` ⇒ lưu máy, KHÔNG báo "lỗi hệ thống" (thu ngân sẽ tưởng mất bill).
        await luuOffline(payload, tab.clientBillId);
      }
    },
    [business, capNhat, dongTab, session?.id, tab, warehouseId],
  );

  const luuOffline = useCallback(
    async (payload: ReturnType<typeof thanhPayload>, clientBillId: string) => {
      const item: OutboxItem = { clientBillId, payload, luc: new Date().toISOString() };

      try {
        await dayVaoOutbox(item);
        setOutbox((cu) => [...cu.filter((x) => x.clientBillId !== clientBillId), item]);
        setThongBao(
          "MẤT MẠNG — hoá đơn đã lưu trên MÁY và xếp hàng đồng bộ. Bấm “Đồng bộ” khi có mạng (không tạo trùng).",
        );
        dongTab(clientBillId);
      } catch {
        capNhat(clientBillId, (t) => ({
          ...t,
          trangThai: "error",
          loi: "KHÔNG lưu được vào IndexedDB ⇒ hoá đơn CHƯA được ghi ở đâu cả. Báo quản lý NGAY.",
        }));
      }
    },
    [capNhat, dongTab],
  );

  const dongBo = useCallback(async () => {
    if (outbox.length === 0) {
      setThongBao("Không có hoá đơn nào đang chờ đồng bộ.");
      return;
    }

    let thanhCong = 0;
    let thatBai = 0;

    for (const item of outbox) {
      try {
        const res = await fetch("/api/proxy/v1/pos/bills", {
          method: "POST",
          headers: { "Content-Type": "application/json", "Idempotency-Key": item.clientBillId },
          body: JSON.stringify(item.payload),
        });

        // 📌 200 **và** lỗi nghiệp vụ 403: gửi lại hoá đơn đã tồn tại là **thành công** với outbox
        //    (hoá đơn đã ở server, chỉ là mình chưa biết) — trừ khi mã là trùng-khoá-khác-nội-dung.
        if (res.ok) {
          thanhCong++;
          await xoaOutbox(item.clientBillId);
          continue;
        }

        const body = await res.json().catch(() => null);
        const failure = docLoiApi(body, res.status);

        if (failure.code === "eCommerce:Pos:0026") {
          thanhCong++;
          await xoaOutbox(item.clientBillId);
          continue;
        }

        thatBai++;
        setLoiHeThong(`Hoá đơn ${item.clientBillId.slice(0, 8)}… lỗi ${failure.code ?? res.status}: ${failure.message}`);
      } catch {
        thatBai++;
      }
    }

    setOutbox(await docOutbox().catch(() => []));
    setThongBao(`Đồng bộ xong: ${thanhCong} thành công · ${thatBai} còn chờ (mất mạng lại).`);
  }, [outbox]);

  // ── Ca thu ngân ───────────────────────────────────────────────────────────────────────────────
  const moCa = useCallback(async () => {
    setDangMoCa(true);
    try {
      const res = await fetch("/api/proxy/v1/pos/sessions/open", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ warehouseId, openingCash: tienDauCa }),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoiHeThong(`Mở ca lỗi ${failure.code ?? res.status}: ${failure.message}`);
        return;
      }

      setSession((await res.json()) as CashSessionDto);
      setThongBao(`Đã MỞ CA với tiền đầu ca ${money(tienDauCa)} đ.`);
    } catch {
      setLoiHeThong("Không gọi được API mở ca (mất mạng?).");
    } finally {
      setDangMoCa(false);
    }
  }, [tienDauCa, warehouseId]);

  const ketCa = useCallback(async () => {
    if (!session?.id) return;

    try {
      const res = await fetch(`/api/proxy/v1/pos/sessions/${session.id}/close`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ handoverCash: tienBanGiao, otherExpense: chiPhiKhac }),
      });

      if (!res.ok) {
        const failure = docLoiApi(await res.json().catch(() => null), res.status);
        setLoiHeThong(`Kết ca lỗi ${failure.code ?? res.status}: ${failure.message}`);
        return;
      }

      const da = (await res.json()) as CashSessionDto;
      setSession(da);
      setMoKetCa(false);
      setThongBao(
        `ĐÃ KẾT CA ${da.number} · tiền cuối ca ${money(da.expectedCash)} đ · chênh lệch ${money(da.variance)} đ.`,
      );
    } catch {
      setLoiHeThong("Không gọi được API kết ca (mất mạng?).");
    }
  }, [chiPhiKhac, session?.id, tienBanGiao]);

  // ── PHÍM TẮT (F1…F10) ─────────────────────────────────────────────────────────────────────────
  useEffect(() => {
    function xuLy(e: KeyboardEvent) {
      switch (e.key) {
        case "F1":
          e.preventDefault();
          themTab();
          break;
        case "F3":
          e.preventDefault();
          oTim.current?.focus();
          break;
        case "F4":
          e.preventDefault();
          oKhach.current?.focus();
          break;
        case "F6":
          e.preventDefault();
          oChietKhau.current?.focus();
          break;
        case "F8":
          e.preventDefault();
          oTienMat.current?.focus();
          break;
        case "F9":
          e.preventDefault();
          void luu(false);
          break;
        case "F10":
          e.preventDefault();
          void luu(true);
          break;
        default:
          break;
      }
    }

    window.addEventListener("keydown", xuLy);
    return () => window.removeEventListener("keydown", xuLy);
  }, [luu, themTab]);

  // Bấm ra ngoài thì đóng danh sách gợi ý.
  useEffect(() => {
    function xuLy(e: MouseEvent) {
      if (hopTim.current && !hopTim.current.contains(e.target as Node)) {
        setKetQuaTim([]);
        setKhachTim([]);
      }
    }

    document.addEventListener("mousedown", xuLy);
    return () => document.removeEventListener("mousedown", xuLy);
  }, []);

  const conNo = (tong?.conNo ?? 0) > 0;

  return (
    <div className="space-y-3">
      {/* ── ĐẦU TRANG: quầy + ca + đồng bộ ─────────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-[20px] font-semibold">Bán lẻ tại quầy</h1>

        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          Quầy: <b>{warehouseName}</b>
          {session ? (
            <>
              {" · "}Ca <b>{session.number}</b> · mở {money(session.openingCash)} đ · {session.billCount ?? 0} hoá đơn
            </>
          ) : (
            " · CHƯA MỞ CA"
          )}
        </span>

        <div className="ml-auto flex items-center gap-2">
          {outbox.length > 0 && (
            <button type="button" className="nh-btn" onClick={() => void dongBo()} data-pos-dong-bo="1">
              Đồng bộ {outbox.length} hoá đơn chờ
            </button>
          )}

          <button type="button" className="nh-btn" onClick={() => setMoKetCa((x) => !x)}>
            {session ? "Kết ca" : "Mở ca"}
          </button>

          <Link className="nh-btn" href={`/${business}/pos/bill/list`}>
            DS hoá đơn
          </Link>
          <Link className="nh-btn" href={`/${business}/pos/return`}>
            Trả hàng
          </Link>
        </div>
      </div>

      {thongBao && (
        <div className="nh-card p-2 text-[12px]" style={{ background: "#eff6ff", color: "#1e40af" }}>
          {thongBao}
        </div>
      )}

      {loiHeThong && (
        <div className="nh-card p-2 text-[12px]" style={{ background: "#fef2f2", color: "var(--danger)" }}>
          {loiHeThong}
        </div>
      )}

      {/* ── MỞ CA / KẾT CA ─────────────────────────────────────────────────────────────────────── */}
      {(moKetCa || !session) && (
        <div className="nh-card p-3 flex flex-wrap items-end gap-3">
          {!session ? (
            <>
              <div className="text-[13px] font-medium">
                Chưa mở ca ⇒ <b>không bán được</b> (tiền mặt phải có ca để đối chiếu khi kết ca).
              </div>
              <label className="text-[12px]">
                <div style={{ color: "var(--text-muted)" }}>Tiền đầu ca</div>
                <input
                  className="nh-input tabular"
                  type="number"
                  min={0}
                  value={tienDauCa}
                  onChange={(e) => setTienDauCa(Number(e.target.value))}
                  style={{ width: 160 }}
                />
              </label>
              <button type="button" className="nh-btn" disabled={dangMoCa} onClick={() => void moCa()}>
                {dangMoCa ? "Đang mở…" : "Mở ca"}
              </button>
            </>
          ) : (
            <>
              <div className="text-[13px] font-medium">
                Kết ca {session.number} — nhập <b>tiền bàn giao thực tế</b> (đếm két).
              </div>
              <label className="text-[12px]">
                <div style={{ color: "var(--text-muted)" }}>Tiền bàn giao</div>
                <input
                  className="nh-input tabular"
                  type="number"
                  min={0}
                  value={tienBanGiao}
                  onChange={(e) => setTienBanGiao(Number(e.target.value))}
                  style={{ width: 160 }}
                />
              </label>
              <label className="text-[12px]">
                <div style={{ color: "var(--text-muted)" }}>Chi phí khác (tiền mặt)</div>
                <input
                  className="nh-input tabular"
                  type="number"
                  min={0}
                  value={chiPhiKhac}
                  onChange={(e) => setChiPhiKhac(Number(e.target.value))}
                  style={{ width: 150 }}
                />
              </label>
              <button type="button" className="nh-btn" onClick={() => void ketCa()}>
                Kết ca (không quay ngược được)
              </button>
              <button type="button" className="nh-btn" onClick={() => setMoKetCa(false)}>
                Đóng
              </button>
            </>
          )}
        </div>
      )}

      {/* ── TAB HOÁ ĐƠN ────────────────────────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-1 flex-wrap border-b" style={{ borderColor: "var(--border)" }}>
        {tabs.map((t, i) => {
          const dangChon = t.clientBillId === activeId;

          return (
            <div
              key={t.clientBillId}
              data-pos-tab={i}
              data-pos-tab-active={dangChon ? "1" : "0"}
              className="flex items-center gap-1 px-2 h-[34px] text-[12px] cursor-pointer"
              style={{
                background: dangChon ? "var(--bg-surface)" : "transparent",
                borderBottom: dangChon ? "2px solid var(--primary)" : "2px solid transparent",
                marginBottom: -1,
              }}
              onClick={() => setActiveId(t.clientBillId)}
            >
              <span style={{ fontWeight: dangChon ? 600 : 400 }}>
                HĐ {i + 1}
                {t.trangThai === "offline" ? " ⏳" : ""}
                {t.trangThai === "error" ? " ⚠" : ""}
              </span>
              <button
                type="button"
                aria-label="Đóng tab"
                onClick={(e) => {
                  e.stopPropagation();
                  dongTab(t.clientBillId);
                }}
                style={{ color: "var(--text-muted)" }}
              >
                ×
              </button>
            </div>
          );
        })}

        <button type="button" className="px-2 h-[34px] text-[12px]" onClick={themTab} title="F1" data-pos-tab-moi="1">
          + HĐ mới (F1)
        </button>
      </div>

      {!tab || !tong ? (
        <div className="nh-card p-6 text-[13px]">Chưa có tab hoá đơn — bấm <b>F1</b> để mở.</div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_340px] gap-3">
          {/* ── CỘT TRÁI: tìm sản phẩm + dòng hàng ─────────────────────────────────────────────── */}
          <div className="space-y-3">
            <div ref={hopTim} className="relative">
              <input
                ref={oTim}
                className="nh-input w-full"
                placeholder="Quét mã vạch hoặc gõ tên/mã/SKU (F3)…"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && ketQuaTim.length > 0) {
                    e.preventDefault();
                    themDong(ketQuaTim[0]);
                  }
                }}
              />

              {q.trim().length >= 2 && (
                <div
                  className="absolute z-20 left-0 right-0 mt-1 nh-card shadow-lg max-h-[280px] overflow-auto"
                  style={{ background: "var(--bg-surface)" }}
                >
                  {dangTim && (
                    <div className="px-3 py-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
                      Đang tìm…
                    </div>
                  )}

                  {!dangTim && ketQuaTim.length === 0 && (
                    <div className="px-3 py-2 text-[12px]" style={{ color: "var(--text-muted)" }}>
                      Không thấy sản phẩm có SKU khớp “{q.trim()}”.
                    </div>
                  )}

                  {ketQuaTim.map((sp) => (
                    <button
                      key={sp.sku}
                      type="button"
                      onClick={() => themDong(sp)}
                      className="w-full text-left px-3 py-2 hover:bg-[#f8fafc] border-b last:border-b-0"
                      style={{ borderColor: "var(--border)" }}
                    >
                      <div className="flex items-center justify-between gap-3">
                        <span className="text-[13px] font-medium">{sp.name}</span>
                        <span className="text-[12px] tabular" style={{ color: "var(--text-muted)" }}>
                          {sp.sku}
                        </span>
                      </div>
                      <div className="text-[11px] tabular" style={{ color: "var(--text-muted)" }}>
                        {sp.barcode ? `Barcode ${sp.barcode} · ` : ""}
                        {sp.price === null ? "CHƯA có giá" : `${money(sp.price)} đ`}
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div className="nh-card overflow-x-auto">
              <table className="w-full text-[12px]">
                <thead>
                  <tr style={{ color: "var(--text-muted)" }}>
                    <th className="text-left px-3 py-2 font-medium">Sản phẩm</th>
                    <th className="text-right px-2 py-2 font-medium" style={{ width: 90 }}>
                      SL
                    </th>
                    <th className="text-right px-2 py-2 font-medium">Đơn giá</th>
                    <th className="text-right px-2 py-2 font-medium">CK dòng</th>
                    <th className="text-right px-2 py-2 font-medium">Thành tiền</th>
                    <th className="px-2 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {tab.lines.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-3 py-6 text-center" style={{ color: "var(--text-muted)" }}>
                        Chưa có món nào — quét mã vạch hoặc gõ tên sản phẩm.
                      </td>
                    </tr>
                  )}

                  {tab.lines.map((d) => (
                    <tr
                      key={d.sku}
                      data-pos-dong={d.sku}
                      className="border-t"
                      style={{ borderColor: "var(--border)" }}
                    >
                      <td className="px-3 py-2">
                        <div className="font-medium">{d.productName ?? d.sku}</div>
                        <div className="text-[10px]" style={{ color: "var(--text-muted)" }}>
                          <code>{d.sku}</code>
                        </div>
                      </td>
                      <td className="px-2 py-2 text-right">
                        <input
                          className="nh-input tabular text-right"
                          type="number"
                          min={0.001}
                          step="0.001"
                          value={d.quantity}
                          onChange={(e) => suaDong(d.sku, (x) => ({ ...x, quantity: Number(e.target.value) }))}
                          style={{ width: 80 }}
                        />
                      </td>
                      <td className="px-2 py-2 text-right">
                        <input
                          className="nh-input tabular text-right"
                          type="number"
                          min={0}
                          value={d.unitPrice}
                          onChange={(e) => suaDong(d.sku, (x) => ({ ...x, unitPrice: Number(e.target.value) }))}
                          style={{ width: 110 }}
                        />
                      </td>
                      <td className="px-2 py-2 text-right">
                        <input
                          className="nh-input tabular text-right"
                          type="number"
                          min={0}
                          value={d.discountAmount}
                          onChange={(e) =>
                            suaDong(d.sku, (x) => ({ ...x, discountAmount: Number(e.target.value) }))
                          }
                          style={{ width: 90 }}
                        />
                      </td>
                      <td className="px-2 py-2 text-right tabular font-medium">
                        {money(d.quantity * d.unitPrice - d.discountAmount)}
                      </td>
                      <td className="px-2 py-2 text-right">
                        <button
                          type="button"
                          aria-label={`Xoá ${d.sku}`}
                          onClick={() => xoaDong(d.sku)}
                          style={{ color: "var(--danger)" }}
                        >
                          ✕
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="nh-card p-3 text-[11px]" style={{ color: "var(--text-muted)" }}>
              <b>F1</b> hoá đơn mới · <b>F3</b> tìm SP · <b>F4</b> khách hàng · <b>F6</b> chiết khấu tổng ·{' '}
              <b>F8</b> tiền mặt · <b>F9</b> lưu · <b>F10</b> lưu + in. Quét 2 lần = SL 2.
            </div>
          </div>

          {/* ── CỘT PHẢI: khách hàng + tiền + thanh toán ────────────────────────────────────────── */}
          <div className="space-y-3">
            <div className="nh-card p-3 space-y-2">
              <div className="text-[12px] font-medium">Khách hàng (F4)</div>
              <input
                ref={oKhach}
                className="nh-input w-full"
                placeholder="Tên / SĐT / email…"
                value={tab.customerName ?? chuKhach}
                onChange={(e) => {
                  setChuKhach(e.target.value);
                  capNhat(tab.clientBillId, (t) => ({ ...t, customerId: null, customerName: null }));
                }}
              />

              {khachTim.length > 0 && (
                <div className="nh-card max-h-[160px] overflow-auto">
                  {khachTim.map((k) => (
                    <button
                      key={k.id}
                      type="button"
                      className="w-full text-left px-2 py-1.5 text-[12px] hover:bg-[#f8fafc] border-b last:border-b-0"
                      style={{ borderColor: "var(--border)" }}
                      onClick={() => {
                        capNhat(tab.clientBillId, (t) => ({ ...t, customerId: k.id, customerName: k.name }));
                        setKhachTim([]);
                        setChuKhach("");
                      }}
                    >
                      {k.name}
                    </button>
                  ))}
                </div>
              )}

              <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                {tab.customerId ? (
                  <>
                    Đã chọn: <b>{tab.customerName}</b>{" "}
                    <button
                      type="button"
                      className="underline"
                      onClick={() =>
                        capNhat(tab.clientBillId, (t) => ({ ...t, customerId: null, customerName: null }))
                      }
                    >
                      bỏ
                    </button>
                  </>
                ) : (
                  "Khách vãng lai (bắt buộc chọn khách nếu ghi nợ)."
                )}
              </div>
            </div>

            <div className="nh-card p-3 space-y-2 text-[12px]">
              <Row nhan="Tiền hàng" giaTri={`${money(tong.tienHang)} đ`} />
              <Row nhan="VAT" giaTri={`${money(tong.tienVat)} đ`} />

              <div className="flex items-center justify-between gap-2">
                <span style={{ color: "var(--text-muted)" }}>Chiết khấu tổng (F6)</span>
                <input
                  ref={oChietKhau}
                  className="nh-input tabular text-right"
                  type="number"
                  min={0}
                  value={tab.discountAmount}
                  onChange={(e) =>
                    capNhat(tab.clientBillId, (t) => ({ ...t, discountAmount: Number(e.target.value) }))
                  }
                  style={{ width: 120 }}
                />
              </div>

              <div className="flex items-center justify-between gap-2">
                <span style={{ color: "var(--text-muted)" }}>Điểm tiêu ({TY_LE_DIEM}đ/điểm)</span>
                <input
                  className="nh-input tabular text-right"
                  type="number"
                  min={0}
                  value={tab.pointsUsed}
                  onChange={(e) => capNhat(tab.clientBillId, (t) => ({ ...t, pointsUsed: Number(e.target.value) }))}
                  style={{ width: 120 }}
                />
              </div>

              <div className="border-t pt-2 flex items-center justify-between" style={{ borderColor: "var(--border)" }}>
                <b>Phải trả</b>
                <b className="tabular text-[18px]">{money(tong.phaiTra)} đ</b>
              </div>

              <div className="flex items-center justify-between gap-2">
                <span style={{ color: "var(--text-muted)" }}>Tiền khách đưa</span>
                <input
                  className="nh-input tabular text-right"
                  type="number"
                  min={0}
                  value={tab.customerGivenAmount}
                  onChange={(e) =>
                    capNhat(tab.clientBillId, (t) => ({ ...t, customerGivenAmount: Number(e.target.value) }))
                  }
                  style={{ width: 120 }}
                />
              </div>

              <Row nhan="Đã thu" giaTri={`${money(tong.daTra)} đ`} />
              {tong.tienThua > 0 && <Row nhan="Tiền thối" giaTri={`${money(tong.tienThua)} đ`} manh />}
              {conNo && <Row nhan="Còn nợ" giaTri={`${money(tong.conNo)} đ`} manh />}

              {conNo && (
                <label className="block">
                  <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
                    Hạn thanh toán (bắt buộc khi ghi nợ)
                  </div>
                  <input
                    className="nh-input w-full"
                    type="date"
                    value={tab.dueDate ?? ""}
                    onChange={(e) => capNhat(tab.clientBillId, (t) => ({ ...t, dueDate: e.target.value || null }))}
                  />
                </label>
              )}
            </div>

            <div className="nh-card p-3 space-y-2">
              <div className="text-[12px] font-medium">Thanh toán ({tab.payments.length} phương thức)</div>

              {tab.payments.map((p, i) => (
                <div key={i} className="flex items-center gap-1">
                  <select
                    className="nh-input"
                    value={p.method}
                    onChange={(e) =>
                      suaThanhToan(i, (x) => ({ ...x, method: Number(e.target.value) as RetailPaymentMethod }))
                    }
                    style={{ width: 130 }}
                  >
                    {PAYMENT_OPTIONS.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.icon} {o.label}
                      </option>
                    ))}
                  </select>

                  <input
                    className="nh-input tabular text-right"
                    type="number"
                    min={0}
                    value={p.amount}
                    onChange={(e) => suaThanhToan(i, (x) => ({ ...x, amount: Number(e.target.value) }))}
                    style={{ width: 110 }}
                  />

                  <button type="button" onClick={() => xoaThanhToan(i)} style={{ color: "var(--danger)" }}>
                    ✕
                  </button>
                </div>
              ))}

              <div className="flex flex-wrap gap-1">
                {PAYMENT_OPTIONS.filter((o) => o.value !== 4).map((o) => (
                  <button
                    key={o.value}
                    type="button"
                    className="nh-btn"
                    onClick={() => themThanhToan(o.value, Math.max(0, tong.phaiTra - tong.daTra))}
                    title={`Thêm ${o.label} với số còn lại`}
                  >
                    {o.icon} {o.label}
                  </button>
                ))}
              </div>

              <input
                ref={oTienMat}
                className="nh-input w-full"
                placeholder="F8: số tiền mặt khách đưa (Enter = thêm)"
                type="number"
                min={0}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    themThanhToan(1, Number((e.target as HTMLInputElement).value));
                    (e.target as HTMLInputElement).value = "";
                  }
                }}
              />

              {tab.loi && (
                <div className="text-[11px]" style={{ color: "var(--danger)" }}>
                  {tab.loi}
                </div>
              )}

              <div className="flex gap-2 pt-1">
                <button type="button" className="nh-btn flex-1" onClick={() => void luu(false)} title="F9">
                  Lưu (F9)
                </button>
                <button type="button" className="nh-btn flex-1" onClick={() => void luu(true)} title="F10">
                  Lưu + in (F10)
                </button>
              </div>

              <div className="text-[10px]" style={{ color: "var(--text-muted)" }}>
                Khoá chống trùng: <code>{tab.clientBillId.slice(0, 8)}…</code>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function Row({ nhan, giaTri, manh }: { nhan: string; giaTri: string; manh?: boolean }) {
  return (
    <div className="flex items-center justify-between">
      <span style={{ color: "var(--text-muted)" }}>{nhan}</span>
      <span className="tabular" style={{ fontWeight: manh ? 600 : 400 }}>
        {giaTri}
      </span>
    </div>
  );
}
