import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";
import { getGrantedPermissions } from "@/lib/api";
import { MENU, MENU_PERMISSIONS } from "@/lib/menu";
import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";

// Mọi trang dưới `/{business}` phụ thuộc session + dữ liệu sống ⇒ không render tĩnh lúc build.
export const dynamic = "force-dynamic";

export default async function AdminLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ business: string }>;
}) {
  const { business } = await params;
  const session = await auth();

  // 🔒 Guard ở server — không dựa vào middleware/client.
  if (!session?.accessToken) {
    redirect(`/login?next=/${encodeURIComponent(business)}`);
  }

  const granted = await getGrantedPermissions(MENU_PERMISSIONS);

  // Ẩn mục không có quyền; ẩn nhóm rỗng (đúng quy tắc menu 2 tầng của Nhanh).
  const menu = MENU.map((group) => ({
    ...group,
    items: group.items.filter((item) => !item.permission || granted.has(item.permission)),
  })).filter((group) => group.items.length > 0);

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar business={business} menu={menu} />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar business={business} userName={session.user?.name} />
        <main className="flex-1 overflow-auto p-5">{children}</main>
      </div>
    </div>
  );
}
