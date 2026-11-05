import { NextResponse, type NextRequest } from "next/server";
import { DEFAULT_BUSINESS } from "@/lib/config";

/**
 * Tương thích link kiểu Nhanh: `/product/item/index?businessId=230222`
 * → `/{business}/product/item` (D-11). Giữ 308 để link cũ vẫn dùng được.
 */
export function middleware(request: NextRequest) {
  const url = request.nextUrl;
  const businessId = url.searchParams.get("businessId");

  if (!businessId) {
    return NextResponse.next();
  }

  const segments = url.pathname.split("/").filter(Boolean);

  // Bỏ hậu tố kiểu Nhanh (`/index`) và map sang route Next.
  const cleaned = segments
    .filter((s) => s !== "index")
    .map((s) => s.replace(/^add2?$/, "new"))
    .join("/");

  const target = new URL(`/${businessId || DEFAULT_BUSINESS}/${cleaned}`, url.origin);
  target.search = url.search;
  target.searchParams.delete("businessId");

  return NextResponse.redirect(target, 308);
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico).*)"],
};
