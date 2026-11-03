import { redirect } from "next/navigation";
import Link from "next/link";
import { signIn, auth } from "@/lib/auth";
import { DEFAULT_BUSINESS } from "@/lib/config";

function isRedirectError(error: unknown): boolean {
  const digest = (error as { digest?: unknown } | null)?.digest;
  return typeof digest === "string" && digest.startsWith("NEXT_REDIRECT");
}

// Trang đăng nhập đọc session hiện tại ⇒ render động.
export const dynamic = "force-dynamic";

async function login(formData: FormData) {
  "use server";

  const username = String(formData.get("username") ?? "").trim();
  const password = String(formData.get("password") ?? "");
  const next = String(formData.get("next") ?? `/${DEFAULT_BUSINESS}`);

  if (!username || !password) {
    redirect(`/login?error=missing`);
  }

  try {
    await signIn("credentials", { username, password, redirectTo: next });
  } catch (error) {
    if (isRedirectError(error)) {
      throw error;
    }
    redirect(`/login?error=invalid&next=${encodeURIComponent(next)}`);
  }
}

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ error?: string; next?: string }>;
}) {
  const session = await auth();
  const sp = await searchParams;

  if (session?.accessToken) {
    redirect(`/${DEFAULT_BUSINESS}`);
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-6">
      <div className="w-full max-w-[380px]">
        <div className="mb-6 text-center">
          <div className="text-[22px] font-semibold">eCommerce Admin</div>
          <div className="text-[13px] mt-1" style={{ color: "var(--text-muted)" }}>
            Quản trị bán hàng đa kênh
          </div>
        </div>

        <form action={login} className="nh-card p-6 space-y-3">
          <input type="hidden" name="next" value={sp.next ?? `/${DEFAULT_BUSINESS}`} />

          <label className="block">
            <span className="text-[12px] font-medium">Email</span>
            <input className="nh-input mt-1" name="username" type="email" autoComplete="username" required defaultValue="admin@abp.io" />
          </label>

          <label className="block">
            <span className="text-[12px] font-medium">Mật khẩu</span>
            <input className="nh-input mt-1" name="password" type="password" autoComplete="current-password" required />
          </label>

          {sp.error && (
            <div className="text-[12px] px-3 py-2 rounded" style={{ background: "#fef2f2", color: "var(--danger)" }}>
              {sp.error === "missing" ? "Nhập đủ email và mật khẩu." : "Sai email hoặc mật khẩu (hoặc tài khoản buộc đổi mật khẩu lần đầu)."}
            </div>
          )}

          <button type="submit" className="nh-btn nh-btn-primary w-full justify-center">
            Đăng nhập
          </button>

          <p className="text-[11px] text-center" style={{ color: "var(--text-muted)" }}>
            Dev: <code>admin@abp.io</code> / <code>1q2w3E*</code>
          </p>
        </form>

        <p className="text-center text-[11px] mt-4" style={{ color: "var(--text-muted)" }}>
          <Link href={`/${DEFAULT_BUSINESS}`} className="underline">
            Vào thẳng doanh nghiệp {DEFAULT_BUSINESS}
          </Link>
        </p>
      </div>
    </div>
  );
}
