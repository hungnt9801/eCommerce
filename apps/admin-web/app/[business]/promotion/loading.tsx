/**
 * **Skeleton khi tải danh sách khuyến mãi** — trạng thái `loading` (F3/F6, `nextjs-admin-rules`).
 * Không phải empty, không phải error: chỉ là "đang tải".
 */
export default function Loading() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Đang tải danh sách khuyến mãi">
      <div className="flex items-center gap-2">
        <div className="h-6 w-40 rounded" style={{ background: "var(--bg-subtle)" }} />
        <div className="ml-auto h-8 w-32 rounded" style={{ background: "var(--bg-subtle)" }} />
      </div>
      <div className="nh-card p-3 flex gap-2">
        {[150, 190, 80, 80].map((w) => (
          <div key={w} className="h-8 rounded" style={{ width: w, background: "var(--bg-subtle)" }} />
        ))}
      </div>
      <div className="nh-card p-3 space-y-2">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-8 rounded" style={{ background: "var(--bg-subtle)" }} />
        ))}
      </div>
    </div>
  );
}
