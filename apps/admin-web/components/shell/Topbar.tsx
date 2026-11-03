import { signOut } from "@/lib/auth";

export function Topbar({ business, userName }: { business: string; userName?: string | null }) {
  return (
    <header
      className="h-[52px] flex items-center justify-between px-4 shrink-0"
      style={{ background: "var(--topbar-bg)", color: "var(--topbar-text)" }}
    >
      <div className="flex items-center gap-3">
        <span className="text-[13px] opacity-70">Doanh nghiệp</span>
        <span className="text-[14px] font-semibold">{business}</span>
      </div>

      <div className="flex items-center gap-4">
        <span className="text-[13px] opacity-80">{userName}</span>
        <form
          action={async () => {
            "use server";
            await signOut({ redirectTo: "/login" });
          }}
        >
          <button type="submit" className="text-[12px] px-2 py-1 rounded border border-white/30 hover:bg-white/10">
            Đăng xuất
          </button>
        </form>
      </div>
    </header>
  );
}
