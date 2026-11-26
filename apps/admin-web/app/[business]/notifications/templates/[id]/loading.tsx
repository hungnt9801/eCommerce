/**
 * **Skeleton khi tải mẫu để soạn version** — trạng thái `loading` (F3/F6).
 */
export default function Loading() {
  return (
    <div className="space-y-3" aria-busy="true" aria-label="Đang tải mẫu thông báo">
      <div className="h-6 w-48 rounded" style={{ background: "var(--bg-subtle)" }} />
      <div className="nh-card p-3 h-12 rounded" style={{ background: "var(--bg-subtle)" }} />
      <div className="nh-card p-3 space-y-2">
        <div className="h-8 rounded" style={{ background: "var(--bg-subtle)" }} />
        <div className="h-28 rounded" style={{ background: "var(--bg-subtle)" }} />
      </div>
    </div>
  );
}
