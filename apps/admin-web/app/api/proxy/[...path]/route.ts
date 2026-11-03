import { auth } from "@/lib/auth";
import { API_BASE_URL } from "@/lib/config";

/**
 * BFF: trình duyệt gọi `/api/proxy/v1/products` → server gắn Bearer rồi chuyển tiếp.
 * 🔒 Access token **không** bao giờ rời khỏi server (D-12).
 */
async function forward(request: Request, context: { params: Promise<{ path: string[] }> }) {
  const session = await auth();

  if (!session?.accessToken) {
    return Response.json({ error: { code: "auth.unauthorized", message: "Chưa đăng nhập." } }, { status: 401 });
  }

  const { path } = await context.params;
  const search = new URL(request.url).search;
  const target = `${API_BASE_URL}/api/${path.join("/")}${search}`;

  const headers: Record<string, string> = { Authorization: `Bearer ${session.accessToken}` };
  const contentType = request.headers.get("content-type");
  if (contentType) {
    headers["Content-Type"] = contentType;
  }

  const hasBody = !["GET", "HEAD"].includes(request.method);

  const upstream = await fetch(target, {
    method: request.method,
    headers,
    body: hasBody ? await request.text() : undefined,
    cache: "no-store",
  });

  return new Response(await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json; charset=utf-8" },
  });
}

export { forward as GET, forward as POST, forward as PUT, forward as PATCH, forward as DELETE };
