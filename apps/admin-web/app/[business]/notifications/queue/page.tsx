import Link from "next/link";
import { ApiError, apiGet } from "@/lib/api";
import { vnDateToUtcIso } from "@/lib/format";
import {
  type NotificationQueueItemDto,
  taoQueueQueryString,
  toPage,
} from "@/lib/notification";
import { QueueTable } from "@/components/notification/QueueTable";

/**
 * **HÀNG ĐỢI THÔNG BÁO** (`/{business}/notifications/queue`) — `NOT-001` / `WP-22` (SRS §10).
 *
 * 🔑 **Lọc ở SERVER** qua `GET /api/v1/notifications/queue` (`Status`, `ChannelKind`, `TuNgayUtc`,
 * `DenNgayUtc`) — bảng hàng đợi một tenant có thể lên hàng chục nghìn dòng, tải hết rồi lọc ở client là
 * **đứng máy** (và "lọc ở client" là **không lọc**: DevTools vẫn thấy dữ liệu).
 *
 * 🚨 **CHE người nhận (`D-191`)**: chỉ hiển thị `recipientMasked`.
 *
 * Ba trạng thái: **loading** (`loading.tsx`) · **empty** (hướng dẫn, KHÔNG phải lỗi) · **error** (thử lại).
 * `lastErrorCode = 0012` ⇒ khối **hướng dẫn publish** (lỗi cấu hình) trong panel chi tiết, không phải "rỗng".
 */
const PAGE_SIZE = 50;

export default async function NotificationQueuePage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const mot = (k: string): string => (typeof sp[k] === "string" ? (sp[k] as string) : "");

  const status = mot("status");
  const channel = mot("channel");
  const from = mot("from");
  const to = mot("to");
  const page = Math.max(1, Number(mot("page") || 1) || 1);

  const loc = {
    status: status === "" ? null : Number(status),
    channelKind: channel === "" ? null : Number(channel),
    // Ngày người dùng chọn là **giờ VN** ⇒ đổi sang UTC trước khi gửi (nếu không, mốc 00:30 bị lọt).
    tuNgayUtc: from ? vnDateToUtcIso(from) : null,
    denNgayUtc: to ? vnDateToUtcIso(to, true) : null,
    page,
    pageSize: PAGE_SIZE,
  };

  let trang = { items: [] as NotificationQueueItemDto[], total: 0, page, pageSize: PAGE_SIZE };
  let loi: string | null = null;

  try {
    const raw = await apiGet<{
      items?: NotificationQueueItemDto[] | null;
      total?: number;
      page?: number;
      pageSize?: number;
    }>(`/api/v1/notifications/queue?${taoQueueQueryString(loc)}`);
    trang = toPage(raw);
  } catch (e) {
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API (mất mạng hoặc host chưa chạy?)";
  }

  const tongTrang = Math.max(1, Math.ceil(trang.total / PAGE_SIZE));

  const link = (sua: Record<string, string | number>): string => {
    const q = new URLSearchParams();
    const gop = { status, channel, from, to, page: String(page), ...sua };
    if (gop.status) q.set("status", String(gop.status));
    if (gop.channel) q.set("channel", String(gop.channel));
    if (gop.from) q.set("from", String(gop.from));
    if (gop.to) q.set("to", String(gop.to));
    q.set("page", String(gop.page));
    return `/${business}/notifications/queue?${q.toString()}`;
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-[20px] font-semibold">Hàng đợi thông báo</h1>
        <div className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          Tổng: <b className="tabular">{trang.total}</b> tin
        </div>
      </div>

      {/* Bộ lọc SERVER-SIDE */}
      <form className="nh-card p-3 flex flex-wrap items-end gap-2" action={`/${business}/notifications/queue`} method="get">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Trạng thái</div>
          <select className="nh-input mt-0.5" name="status" defaultValue={status} style={{ width: 150 }}>
            <option value="">Tất cả</option>
            <option value="0">Mới</option>
            <option value="1">Chờ duyệt</option>
            <option value="2">Chờ gửi</option>
            <option value="3">Đang bàn giao</option>
            <option value="4">Đã bàn giao</option>
            <option value="5">Đã huỷ</option>
            <option value="6">Hết hạn</option>
            <option value="7">Lỗi</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Kênh</div>
          <select className="nh-input mt-0.5" name="channel" defaultValue={channel} style={{ width: 130 }}>
            <option value="">Tất cả</option>
            <option value="0">Email</option>
            <option value="1">SMS</option>
            <option value="2">Zalo OA</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Từ ngày</div>
          <input type="date" className="nh-input mt-0.5" name="from" defaultValue={from} />
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Đến ngày</div>
          <input type="date" className="nh-input mt-0.5" name="to" defaultValue={to} />
        </label>

        <button type="submit" className="nh-btn nh-btn-primary">
          Lọc
        </button>
        <Link className="nh-btn" href={`/${business}/notifications/queue`}>
          Xoá lọc
        </Link>
      </form>

      {loi ? (
        <div className="nh-card p-4 text-[13px] space-y-2" style={{ color: "var(--danger)" }}>
          <div>Không tải được hàng đợi thông báo.</div>
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            {loi}
          </div>
          <Link className="nh-btn" href={`/${business}/notifications/queue`}>
            Thử lại
          </Link>
        </div>
      ) : (
        <QueueTable items={trang.items} />
      )}

      {tongTrang > 1 && (
        <div className="flex items-center justify-end gap-2 text-[13px]">
          <Link className="nh-btn" aria-disabled={page <= 1} href={link({ page: page - 1 })}>
            Trước
          </Link>
          <span className="tabular">
            {page} / {tongTrang}
          </span>
          <Link className="nh-btn" aria-disabled={page >= tongTrang} href={link({ page: page + 1 })}>
            Sau
          </Link>
        </div>
      )}

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        📌 Bấm vào một dòng để xem <b>nội dung đã dựng (đã che PII)</b> + <b>dòng thời gian</b> sự kiện.
        Người nhận luôn hiển thị ở dạng <b>đã che</b> (<code>D-191</code>).
      </div>
    </div>
  );
}
