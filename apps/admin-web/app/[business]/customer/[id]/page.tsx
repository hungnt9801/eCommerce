import Link from "next/link";
import { ApiError, apiGetSafe } from "@/lib/api";
import { amount, dateOnly, dateTime, money } from "@/lib/format";
import {
  CUSTOMER_DETAIL_TABS,
  careLabel,
  customerTypeLabel,
  detailTab,
  toPage,
  type CustomerCareDto,
  type CustomerDetailDto,
  type CustomerLevelDto,
  type DoiChieuSoDuDto,
  type Page,
} from "@/lib/customer";
import type { OrderListItemDto } from "@/lib/order";
import type { RetailBillDto } from "@/lib/pos";
import { SegmentForm } from "@/components/customer/SegmentForm";
import { CareForm } from "@/components/customer/CareForm";

const PAGE_SIZE = 20;

/**
 * **CHI TIẾT KHÁCH — 5 TAB** (`/{business}/customer/[id]`) — `WP-07`.
 *
 * 5 tab: **Tổng quan · Đơn hàng · Hoá đơn bán lẻ · Công nợ · Chăm sóc**.
 *
 * 🚨 **Số liệu lấy từ SERVER (không cộng ở FE):** `accumulatedAmount` / `debtBalance` là **cùng một**
 * đại lượng với tổng hoá đơn − tổng trả hàng. Nếu FE tự cộng từ danh sách thì có **hai định nghĩa** và
 * chúng lệch ngay khi có phiếu trả/huỷ (`XC-003` F2). 📌 Tab *"Công nợ"* còn hiện **cờ đối chiếu**
 * (`TienTichLuyKhop`/`CongNoKhop`) để người vận hành thấy ngay khi dữ liệu lệch.
 */
export default async function CustomerDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string; id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business, id } = await params;
  const sp = await searchParams;

  const tab = detailTab(typeof sp.tab === "string" ? sp.tab : null);

  let loi: string | null = null;
  let khach: CustomerDetailDto | null = null;

  try {
    khach = await apiGetSafe<CustomerDetailDto | null>(`/api/v1/customers/${id}`, null);

    if (khach === null) {
      loi = "Không tải được hồ sơ khách (404 hoặc API lỗi).";
    }
  } catch (e) {
    loi = e instanceof ApiError ? `${e.status} · ${e.code ?? e.message}` : "không gọi được API";
  }

  if (!khach) {
    return (
      <div className="space-y-3">
        <Link className="nh-btn" href={`/${business}/customer/list`}>
          ← DS khách
        </Link>
        <div className="nh-card p-3 text-[13px]" style={{ color: "var(--danger)" }}>
          {loi ?? "Không có dữ liệu."}
        </div>
      </div>
    );
  }

  const caps = await apiGetSafe<CustomerLevelDto[]>("/api/v1/customer-levels", []);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <Link className="nh-btn" href={`/${business}/customer/list`}>
          ← DS khách
        </Link>
        <h1 className="text-[20px] font-semibold">{khach.name}</h1>
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {customerTypeLabel(khach.type)}
          {khach.levelName ? ` · cấp ${khach.levelName}` : " · chưa đạt cấp"}
          {khach.identityUserId ? "" : " · khách vãng lai"}
        </span>

        <span className="ml-auto text-[12px]">
          Điểm <b>{amount(khach.points)}</b> · Tích luỹ <b>{money(khach.accumulatedAmount)}</b> · Dư nợ{" "}
          <b style={{ color: khach.debtBalance > 0 ? "var(--danger)" : undefined }}>{money(khach.debtBalance)}</b>
          {khach.debtLimit > 0 ? ` / hạn mức ${money(khach.debtLimit)}` : " (không đặt hạn mức)"}
        </span>
      </div>

      {khach.vuotHanMucNo && (
        <div className="nh-card p-2 text-[12px]" style={{ color: "var(--danger)" }}>
          ⚠️ Khách đang <b>vượt hạn mức nợ</b> — lần bán tiếp theo có ghi nợ sẽ bị server từ chối
          (`Customers:0019`).
        </div>
      )}

      <div className="flex flex-wrap gap-1">
        {CUSTOMER_DETAIL_TABS.map((t) => (
          <Link
            key={t.key}
            className="nh-btn"
            href={`/${business}/customer/${khach.id}?tab=${t.key}`}
            style={
              tab === t.key
                ? { background: "var(--brand)", color: "#fff", borderColor: "var(--brand)" }
                : undefined
            }
          >
            {t.label}
          </Link>
        ))}
      </div>

      {tab === "tong-quan" && (
        <div className="space-y-3">
          <div className="nh-card p-3 grid grid-cols-2 md:grid-cols-4 gap-3 text-[13px]">
            <O nhan="Điện thoại" gt={khach.phone} />
            <O nhan="Email" gt={khach.email} />
            <O nhan="Mã số thuế" gt={khach.taxCode} />
            <O nhan="Ngày sinh" gt={khach.birthday ? dateOnly(khach.birthday) : null} />
            <O nhan="Ngày tạo" gt={dateTime(khach.creationTime)} />
            <O nhan="Số đơn online" gt={String(khach.soDonHang)} />
            <O nhan="Số hoá đơn bán lẻ" gt={String(khach.soHoaDonBanLe)} />
            <O nhan="Tổng bán lẻ" gt={money(khach.tongBanLe)} />
          </div>

          <SegmentForm khach={khach} caps={caps} />
        </div>
      )}

      {tab === "don-hang" && <TabDonHang business={business} customerId={khach.id} />}

      {tab === "hoa-don" && <TabHoaDon business={business} customerId={khach.id} />}

      {tab === "cong-no" && <TabCongNo khach={khach} />}

      {tab === "cham-soc" && (
        <div className="space-y-3">
          <CareForm customerId={khach.id} />
          <TabChamSoc customerId={khach.id} />
        </div>
      )}
    </div>
  );
}

function O({ nhan, gt }: { nhan: string; gt: string | null | undefined }) {
  return (
    <div>
      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        {nhan}
      </div>
      <div>{gt ?? "—"}</div>
    </div>
  );
}

async function TabDonHang({ business, customerId }: { business: string; customerId: string }) {
  const qs = new URLSearchParams({ customerId, pageSize: String(PAGE_SIZE), page: "1" });

  const raw = await apiGetSafe<{ items?: OrderListItemDto[] | null; totalCount?: number } | null>(
    `/api/v1/orders?${qs.toString()}`,
    null,
  );
  const trang = toPage<OrderListItemDto>(raw);

  return (
    <div className="nh-card overflow-x-auto">
      <table className="w-full text-[13px]">
        <thead>
          <tr className="text-left" style={{ color: "var(--text-muted)" }}>
            <th className="p-2">Số đơn</th>
            <th className="p-2">Kênh</th>
            <th className="p-2">Nhãn trạng thái</th>
            <th className="p-2 text-right">Tổng tiền</th>
            <th className="p-2">Tạo</th>
            <th className="p-2" />
          </tr>
        </thead>
        <tbody>
          {trang.items.length === 0 && (
            <tr>
              <td className="p-3" colSpan={6} style={{ color: "var(--text-muted)" }}>
                Khách chưa có đơn online.
              </td>
            </tr>
          )}
          {trang.items.map((d) => (
            <tr key={d.id} className="border-t" style={{ borderColor: "var(--border)" }}>
              <td className="p-2 font-mono text-[11px]">{d.number}</td>
              <td className="p-2">{d.channelCode}</td>
              <td className="p-2">{d.statusLabel}</td>
              <td className="p-2 text-right">{money(d.grandTotal)}</td>
              <td className="p-2">{dateOnly(d.creationTime)}</td>
              <td className="p-2">
                <Link className="underline" href={`/${business}/order/manage/${d.id}`}>
                  Mở
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="p-2 text-[11px]" style={{ color: "var(--text-muted)" }}>
        Tổng {money(trang.total)} đơn — tab này dùng **cùng** bộ lọc/phân trang với DS đơn nên số khớp.
      </div>
    </div>
  );
}

async function TabHoaDon({ business, customerId }: { business: string; customerId: string }) {
  const qs = new URLSearchParams({ customerId, maxResultCount: String(PAGE_SIZE), skipCount: "0" });

  const raw = await apiGetSafe<{ items?: RetailBillDto[] | null; totalCount?: number } | null>(
    `/api/v1/pos/bills?${qs.toString()}`,
    null,
  );
  const trang = toPage<RetailBillDto>(raw);

  return (
    <div className="nh-card overflow-x-auto">
      <table className="w-full text-[13px]">
        <thead>
          <tr className="text-left" style={{ color: "var(--text-muted)" }}>
            <th className="p-2">Số hoá đơn</th>
            <th className="p-2">Trạng thái</th>
            <th className="p-2 text-right">Phải trả</th>
            <th className="p-2 text-right">Đã trả</th>
            <th className="p-2 text-right">Còn nợ</th>
            <th className="p-2 text-right">Điểm</th>
            <th className="p-2">Tạo</th>
            <th className="p-2" />
          </tr>
        </thead>
        <tbody>
          {trang.items.length === 0 && (
            <tr>
              <td className="p-3" colSpan={8} style={{ color: "var(--text-muted)" }}>
                Khách chưa có hoá đơn bán lẻ.
              </td>
            </tr>
          )}
          {trang.items.map((h) => (
            <tr key={h.id} className="border-t" style={{ borderColor: "var(--border)" }}>
              <td className="p-2 font-mono text-[11px]">{h.number}</td>
              <td className="p-2">{h.status === "Voided" ? "Đã huỷ" : "Đã bán"}</td>
              <td className="p-2 text-right">{money(h.totalPayment)}</td>
              <td className="p-2 text-right">{money(h.paidAmount)}</td>
              <td className="p-2 text-right">{money(h.debtAmount)}</td>
              <td className="p-2 text-right">
                +{amount(h.pointsEarned)} / −{amount(h.pointsUsed)}
              </td>
              <td className="p-2">{dateOnly(h.creationTime)}</td>
              <td className="p-2">
                <Link className="underline" href={`/${business}/print/bill/${h.id}`}>
                  In
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="p-2 text-[11px]" style={{ color: "var(--text-muted)" }}>
        Tổng {money(trang.total)} hoá đơn.
      </div>
    </div>
  );
}

async function TabCongNo({ khach }: { khach: CustomerDetailDto }) {
  const doiChieu = await apiGetSafe<DoiChieuSoDuDto | null>(`/api/v1/customers/${khach.id}/balance`, null);
  const congNo = khach.congNo ?? [];

  return (
    <div className="space-y-3">
      <div className="nh-card p-3 text-[13px] grid grid-cols-2 md:grid-cols-4 gap-3">
        <O nhan="Dư nợ (sổ khách)" gt={money(khach.debtBalance)} />
        <O nhan="Hạn mức nợ" gt={khach.debtLimit > 0 ? money(khach.debtLimit) : "không đặt"} />
        <O nhan="Tổng nợ trên hoá đơn" gt={doiChieu ? money(doiChieu.tongNoHoaDon) : null} />
        <O nhan="Nợ quá hạn" gt={khach.coNoQuaHan ? "CÓ" : "không"} />
      </div>

      {doiChieu && (
        <div
          className="nh-card p-2 text-[12px]"
          style={{ color: doiChieu.congNoKhop && doiChieu.tienTichLuyKhop ? "var(--brand)" : "var(--danger)" }}
        >
          Đối chiếu sổ: {doiChieu.congNoKhop ? "công nợ KHỚP" : "công nợ LỆCH"} ·{" "}
          {doiChieu.tienTichLuyKhop ? "tiền tích luỹ KHỚP" : "tiền tích luỹ LỆCH"} · Σ hoá đơn{" "}
          {money(doiChieu.tongHoaDon)} − Σ trả hàng {money(doiChieu.tongTraHang)}.
        </div>
      )}

      <div className="nh-card overflow-x-auto">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="text-left" style={{ color: "var(--text-muted)" }}>
              <th className="p-2">Số hoá đơn</th>
              <th className="p-2 text-right">Phải trả</th>
              <th className="p-2 text-right">Đã trả</th>
              <th className="p-2 text-right">Còn nợ</th>
              <th className="p-2">Hạn thanh toán</th>
              <th className="p-2">Quá hạn</th>
              <th className="p-2">Tạo</th>
            </tr>
          </thead>
          <tbody>
            {congNo.length === 0 && (
              <tr>
                <td className="p-3" colSpan={7} style={{ color: "var(--text-muted)" }}>
                  Không còn khoản nợ nào.
                </td>
              </tr>
            )}
            {congNo.map((n) => (
              <tr key={n.billId} className="border-t" style={{ borderColor: "var(--border)" }}>
                <td className="p-2 font-mono text-[11px]">{n.number}</td>
                <td className="p-2 text-right">{money(n.totalPayment)}</td>
                <td className="p-2 text-right">{money(n.paidAmount)}</td>
                <td className="p-2 text-right" style={{ color: "var(--danger)" }}>
                  {money(n.debtAmount)}
                </td>
                <td className="p-2">{n.dueDateUtc ? dateOnly(n.dueDateUtc) : "—"}</td>
                <td className="p-2">{n.quaHan ? "CÓ" : "—"}</td>
                <td className="p-2">{dateOnly(n.creationTime)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

async function TabChamSoc({ customerId }: { customerId: string }) {
  const ds = await apiGetSafe<CustomerCareDto[]>(`/api/v1/customers/${customerId}/care`, []);

  return (
    <div className="nh-card p-3 text-[13px]">
      <div className="font-semibold mb-2">Lịch sử chăm sóc ({ds.length})</div>

      {ds.length === 0 && (
        <div style={{ color: "var(--text-muted)" }}>Chưa có lần chăm sóc nào.</div>
      )}

      <ul className="space-y-2">
        {ds.map((c) => (
          <li key={c.id} className="border-t pt-2" style={{ borderColor: "var(--border)" }}>
            <div className="flex flex-wrap gap-2">
              <b>{careLabel(c.type)}</b>
              <span style={{ color: "var(--text-muted)" }}>{dateTime(c.occurredAtUtc)}</span>
              {c.nextFollowUpAtUtc && (
                <span className="text-[11px]" style={{ color: "var(--brand)" }}>
                  hẹn lại {dateOnly(c.nextFollowUpAtUtc)}
                </span>
              )}
            </div>
            <div>{c.content}</div>
          </li>
        ))}
      </ul>
    </div>
  );
}
