"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import {
  type NotificationTemplateDto,
  canhBaoThieuTrang,
  nhanKenh,
  nhanMucDich,
  nhanTrangThaiTemplate,
  templateChuaPublish,
} from "@/lib/notification";

/**
 * **BẢNG MẪU THÔNG BÁO + LỌC PHÍA CLIENT** — `NOT-001` / `WP-22`.
 *
 * Vì sao lọc ở client: `GetTemplatesAsync` trả **toàn bộ theo trang** và SRS §10 **CẤM** ẩn bản ghi
 * `Archived`; số template của một tenant nhỏ (chục–trăm), nên lọc tại chỗ nhanh hơn round-trip. Khi vượt
 * một trang (tải tối đa 200) ta **cảnh báo** phần chưa tải thay vì lặng lẽ đếm thiếu.
 *
 * 🚨 `Archived` **luôn** có mặt ở lựa chọn "Tất cả" — bộ lọc trạng thái là tuỳ chọn của người dùng, không
 * phải mặc định hệ thống ẩn đi (tin cũ còn ghim version đã publish).
 */
export function TemplateTable({
  business,
  items,
  total,
  coQuyenPublish,
}: {
  business: string;
  items: NotificationTemplateDto[];
  total: number;
  coQuyenPublish: boolean;
}) {
  const [kenh, setKenh] = useState("");
  const [mucDich, setMucDich] = useState("");
  const [trangThai, setTrangThai] = useState("");

  const daLoc = useMemo(
    () =>
      items.filter((t) => {
        if (kenh !== "" && String(t.channelKind ?? "") !== kenh) return false;
        if (mucDich !== "" && String(t.purpose ?? "") !== mucDich) return false;
        if (trangThai !== "" && String(t.status ?? "") !== trangThai) return false;
        return true;
      }),
    [items, kenh, mucDich, trangThai],
  );

  const thieuTrang = canhBaoThieuTrang(total, items.length);
  const coLoc = kenh !== "" || mucDich !== "" || trangThai !== "";

  return (
    <div className="space-y-3">
      {/* Bộ lọc client */}
      <div className="nh-card p-3 flex flex-wrap items-center gap-2">
        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Kênh</div>
          <select
            className="nh-input mt-0.5"
            value={kenh}
            onChange={(e) => setKenh(e.target.value)}
            style={{ width: 130 }}
          >
            <option value="">Tất cả</option>
            <option value="0">Email</option>
            <option value="1">SMS</option>
            <option value="2">Zalo OA</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Mục đích</div>
          <select
            className="nh-input mt-0.5"
            value={mucDich}
            onChange={(e) => setMucDich(e.target.value)}
            style={{ width: 170 }}
          >
            <option value="">Tất cả</option>
            <option value="0">Giao dịch</option>
            <option value="1">Marketing</option>
            <option value="2">Cảnh báo vận hành</option>
          </select>
        </label>

        <label className="text-[12px]">
          <div style={{ color: "var(--text-muted)" }}>Trạng thái</div>
          <select
            className="nh-input mt-0.5"
            value={trangThai}
            onChange={(e) => setTrangThai(e.target.value)}
            style={{ width: 140 }}
          >
            <option value="">Tất cả</option>
            <option value="0">Nháp</option>
            <option value="1">Đã publish</option>
            <option value="2">Lưu trữ</option>
          </select>
        </label>

        {coLoc && (
          <button
            type="button"
            className="nh-btn self-end"
            onClick={() => {
              setKenh("");
              setMucDich("");
              setTrangThai("");
            }}
          >
            Xoá lọc
          </button>
        )}

        <div className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          Hiển thị {daLoc.length} / {items.length} đã tải
        </div>
      </div>

      {thieuTrang && (
        <div
          className="nh-card p-3 text-[12px]"
          style={{ color: "var(--warning)", borderColor: "var(--warning)" }}
        >
          ⚠️ Đang hiển thị {items.length}/{total} mẫu — còn {total - items.length} mẫu chưa tải. Bộ lọc và số
          đếm ở trên chỉ tính phần đã tải.
        </div>
      )}

      <div className="nh-card overflow-auto">
        <table className="nh-table">
          <thead>
            <tr>
              <th className="w-[150px]">Mã</th>
              <th>Tên</th>
              <th className="w-[110px]">Kênh</th>
              <th className="w-[90px]">Ngôn ngữ</th>
              <th className="w-[150px]">Mục đích</th>
              <th className="w-[140px]">Trạng thái</th>
              <th className="w-[150px]">Version đang dùng</th>
              <th className="w-[130px]"></th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 && (
              <tr>
                <td colSpan={8} className="text-center py-10" style={{ color: "var(--text-muted)" }}>
                  Chưa có mẫu thông báo nào. Tạo mẫu đầu tiên ở nút “Tạo mẫu” phía trên.
                </td>
              </tr>
            )}

            {items.length > 0 && daLoc.length === 0 && (
              <tr>
                <td colSpan={8} className="text-center py-10" style={{ color: "var(--text-muted)" }}>
                  Không có mẫu nào khớp bộ lọc hiện tại.
                </td>
              </tr>
            )}

            {daLoc.map((t) => {
              const chuaPublish = templateChuaPublish(t);
              const archived = t.status === 2;

              return (
                <tr key={t.id}>
                  <td className="font-mono text-[12px]" style={{ color: "var(--primary)" }}>
                    {t.code}
                  </td>
                  <td className="font-medium">{t.name}</td>
                  <td>{nhanKenh(t.channelKind)}</td>
                  <td>{t.locale ?? "—"}</td>
                  <td>{nhanMucDich(t.purpose)}</td>
                  <td>
                    <span
                      className="text-[11px] px-2 py-0.5 rounded-full"
                      style={{
                        background:
                          t.status === 1
                            ? "#ecfdf5"
                            : t.status === 0
                              ? "#fff7ed"
                              : "var(--bg-subtle)",
                        color:
                          t.status === 1
                            ? "var(--success)"
                            : t.status === 0
                              ? "var(--warning)"
                              : "var(--text-muted)",
                      }}
                    >
                      {nhanTrangThaiTemplate(t.status)}
                    </span>
                  </td>
                  <td>
                    {t.currentPublishedVersionId ? (
                      <span className="font-mono text-[11px]" style={{ color: "var(--text-muted)" }}>
                        {t.currentPublishedVersionId.slice(0, 8)}…
                      </span>
                    ) : (
                      <span className="text-[11px]" style={{ color: "var(--warning)" }}>
                        — chưa publish
                      </span>
                    )}
                  </td>
                  <td>
                    <Link
                      className="nh-btn"
                      href={`/${business}/notifications/templates/${t.id}`}
                      title={
                        coQuyenPublish
                          ? "Soạn và publish version mới"
                          : "Chỉ xem — cần quyền Templates.Publish để publish"
                      }
                    >
                      {archived ? "Xem" : coQuyenPublish ? "Soạn version" : "Xem"}
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="text-[11px]" style={{ color: "var(--text-muted)" }}>
        📌 Mẫu <b>chưa publish</b> (cột Version đang dùng = “—” ) là <b>lỗi cấu hình</b>, không phải “chưa có dữ
        liệu”: tin sẽ bị BE từ chối bằng mã <span className="font-mono">0012</span> cho tới khi publish một
        version. “Lưu trữ” vẫn hiển thị vì tin cũ còn ghim version đã publish.
      </div>
    </div>
  );
}
