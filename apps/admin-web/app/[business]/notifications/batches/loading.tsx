/**
 * **Skeleton khi tải lô gửi** — trạng thái `loading` (F3/F6).
 */
export default function Loading() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Đang tải lô gửi thông báo">
      <div className="h-6 w-48 rounded" style={{ background: "var(--bg-subtle)" }} />
      <div className="nh-card p-3 space-y-2">
        <div className="h-9 rounded" style={{ background: "var(--bg-subtle)" }} />
        <div className="h-9 rounded" style={{ background: "var(--bg-subtle)" }} />
      </div>
      <div className="nh-card p-3 h-16 rounded" style={{ background: "var(--bg-subtle)" }} />
    </div>
  );
}
