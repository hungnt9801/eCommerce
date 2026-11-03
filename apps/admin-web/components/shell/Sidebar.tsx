"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { CaretDoubleLeft, CaretDoubleRight } from "@phosphor-icons/react";
import type { MenuGroup } from "@/lib/menu";

export function Sidebar({ business, menu }: { business: string; menu: MenuGroup[] }) {
  const [collapsed, setCollapsed] = useState(false);
  const pathname = usePathname();

  return (
    <aside
      className="flex flex-col border-r shrink-0 transition-[width] duration-150"
      style={{
        width: collapsed ? 64 : 256,
        background: "var(--sidebar-bg)",
        borderColor: "var(--border)",
      }}
    >
      <div className="h-[52px] flex items-center justify-between px-4 border-b" style={{ borderColor: "var(--border)" }}>
        {!collapsed && (
          <span className="font-semibold tracking-tight" style={{ color: "var(--sidebar-text)" }}>
            eCommerce
          </span>
        )}
        <button
          type="button"
          onClick={() => setCollapsed((v) => !v)}
          className="p-1 rounded hover:bg-black/5"
          aria-label={collapsed ? "Mở rộng menu" : "Thu gọn menu"}
        >
          {collapsed ? <CaretDoubleRight size={16} /> : <CaretDoubleLeft size={16} />}
        </button>
      </div>

      <nav className="flex-1 overflow-y-auto py-2">
        {menu.map((group) => (
          <div key={group.label} className="mb-3">
            {!collapsed && (
              <div className="px-4 py-1 text-[11px] font-semibold uppercase tracking-wide" style={{ color: "var(--text-muted)" }}>
                {group.label}
              </div>
            )}
            <ul>
              {group.items.map((item) => {
                const href = `/${business}/${item.href}`.replace(/\/+$/, "");
                const active = pathname === href || (item.href && pathname.startsWith(`${href}/`));

                if (!item.enabled) {
                  return (
                    <li key={item.label}>
                      <span
                        className="flex items-center justify-between mx-2 px-2 py-1.5 rounded text-[13px] cursor-not-allowed"
                        style={{ color: "var(--text-muted)" }}
                        title={item.note}
                      >
                        <span>{collapsed ? item.label.slice(0, 2) : item.label}</span>
                        {!collapsed && item.note && (
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-black/5">{item.note}</span>
                        )}
                      </span>
                    </li>
                  );
                }

                return (
                  <li key={item.label}>
                    <Link
                      href={href}
                      className="flex items-center mx-2 px-2 py-1.5 rounded text-[13px]"
                      style={{
                        background: active ? "#e8f1ff" : "transparent",
                        color: active ? "var(--primary)" : "var(--sidebar-text)",
                        fontWeight: active ? 600 : 400,
                        borderLeft: active ? "3px solid var(--primary)" : "3px solid transparent",
                      }}
                      title={collapsed ? item.label : undefined}
                    >
                      {collapsed ? item.label.slice(0, 2) : item.label}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>

      {!collapsed && (
        <div className="px-4 py-3 text-[11px]" style={{ color: "var(--text-muted)" }}>
          UI theo Nhanh.vn · v0.1
        </div>
      )}
    </aside>
  );
}
