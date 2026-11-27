/**
 * **Skeleton khi tải chi tiết khuyến mãi** — trạng thái `loading` (F3/F6, `nextjs-admin-rules`).
 */
export default function Loading() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Đang tải chi tiết khuyến mãi">
      <div className="flex items-center gap-2">
        <div className="h-6 w-52 rounded" style={{ background: "var(--bg-subtle)" }} />
        <div className="ml-auto h-8 w-40 rounded" style={{ background: "var(--bg-subtle)" }} />
      </div>
      <div className="nh-card p-3 grid grid-cols-2 md:grid-cols-4 gap-3">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="h-5 rounded" style={{ background: "var(--bg-subtle)" }} />
        ))}
      </div>
      <div className="nh-card p-3 grid grid-cols-2 md:grid-cols-4 gap-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-14 rounded" style={{ background: "var(--bg-subtle)" }} />
        ))}
      </div>
      <div className="nh-card p-3 space-y-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-10 rounded" style={{ background: "var(--bg-subtle)" }} />
        ))}
      </div>
    </div>
  );
}
