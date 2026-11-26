/**
 * **Skeleton khi tải hàng đợi thông báo** — trạng thái `loading` (F3/F6).
 */
export default function Loading() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Đang tải hàng đợi thông báo">
      <div className="flex items-center gap-2">
        <div className="h-6 w-48 rounded" style={{ background: "var(--bg-subtle)" }} />
        <div className="ml-auto h-6 w-24 rounded" style={{ background: "var(--bg-subtle)" }} />
      </div>
      <div className="nh-card p-3 flex flex-wrap gap-2">
        {[150, 130, 140, 140].map((w) => (
          <div key={w} className="h-9 rounded" style={{ width: w, background: "var(--bg-subtle)" }} />
        ))}
      </div>
      <div className="nh-card p-3 space-y-2">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="h-8 rounded" style={{ background: "var(--bg-subtle)" }} />
        ))}
      </div>
    </div>
  );
}
