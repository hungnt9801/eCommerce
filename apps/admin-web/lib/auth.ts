import NextAuth from "next-auth";
import Credentials from "next-auth/providers/credentials";
import { TOKEN_ENDPOINT, OAUTH_CLIENT_ID, OAUTH_CLIENT_SECRET, OAUTH_SCOPE } from "./config";

type TokenResponse = {
  access_token: string;
  refresh_token?: string;
  expires_in: number;
  token_type: string;
};

/**
 * Đổi username/password lấy token tại OpenIddict của Host.
 *
 * 🔒 Token **chỉ sống trong cookie session mã hoá của Auth.js** (D-12) — trình duyệt không bao giờ
 * đọc được access token. Mọi lời gọi API đi qua server (RSC hoặc `/api/proxy`).
 *
 * ⚠️ `D-12`/WP-01: hiện dùng grant `password` vì client seed `eCommerce_App` **chưa bật**
 * `authorization_code`. Khi bật (thêm grant + redirect URI) thì đổi provider này sang OIDC
 * (Authorization Code + PKCE) — không đổi gì phía API.
 */
async function requestToken(body: Record<string, string>): Promise<TokenResponse | null> {
  const res = await fetch(TOKEN_ENDPOINT, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams(body),
    cache: "no-store",
  });

  if (!res.ok) {
    return null;
  }

  return (await res.json()) as TokenResponse;
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: true,
  secret: process.env.AUTH_SECRET ?? "dev-only-auth-secret-doi-o-production",
  session: { strategy: "jwt", maxAge: 8 * 60 * 60 },
  pages: { signIn: "/login" },
  providers: [
    Credentials({
      name: "eCommerce",
      credentials: {
        username: { label: "Email", type: "email" },
        password: { label: "Mật khẩu", type: "password" },
      },
      async authorize(credentials) {
        const username = typeof credentials?.username === "string" ? credentials.username.trim() : "";
        const password = typeof credentials?.password === "string" ? credentials.password : "";

        if (!username || !password) {
          return null;
        }

        const token = await requestToken({
          grant_type: "password",
          username,
          password,
          client_id: OAUTH_CLIENT_ID,
          client_secret: OAUTH_CLIENT_SECRET,
          scope: OAUTH_SCOPE,
        });

        if (!token) {
          return null;
        }

        return {
          id: username,
          name: username,
          accessToken: token.access_token,
          refreshToken: token.refresh_token,
          expiresAt: Date.now() + token.expires_in * 1000,
        };
      },
    }),
  ],
  callbacks: {
    async jwt({ token, user }) {
      if (user) {
        token.accessToken = user.accessToken;
        token.refreshToken = user.refreshToken;
        token.expiresAt = user.expiresAt;
        token.error = undefined;
        return token;
      }

      const expiresAt = typeof token.expiresAt === "number" ? token.expiresAt : 0;
      const refreshToken = typeof token.refreshToken === "string" ? token.refreshToken : undefined;

      // Còn hạn (chừa 60s) ⇒ dùng tiếp
      if (expiresAt > 0 && Date.now() < expiresAt - 60_000) {
        return token;
      }

      if (!refreshToken) {
        token.error = "RefreshTokenMissing";
        return token;
      }

      const refreshed = await requestToken({
        grant_type: "refresh_token",
        refresh_token: refreshToken,
        client_id: OAUTH_CLIENT_ID,
        client_secret: OAUTH_CLIENT_SECRET,
      });

      if (!refreshed) {
        token.error = "RefreshFailed";
        return token;
      }

      token.accessToken = refreshed.access_token;
      token.refreshToken = refreshed.refresh_token ?? token.refreshToken;
      token.expiresAt = Date.now() + refreshed.expires_in * 1000;
      token.error = undefined;
      return token;
    },
    async session({ session, token }) {
      session.accessToken = typeof token.accessToken === "string" ? token.accessToken : undefined;
      session.expiresAt = typeof token.expiresAt === "number" ? token.expiresAt : undefined;
      session.error = typeof token.error === "string" ? token.error : undefined;
      return session;
    },
  },
});
