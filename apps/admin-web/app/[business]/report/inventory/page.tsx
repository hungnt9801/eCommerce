import Link from "next/link";
import { ApiError, apiGet } from "@/lib/api";
import { amount, int, qty, todayVn } from "@/lib/format";
import { csvFileName, type TongQuanTonKhoDto } from "@/lib/report";
import { ExportCsvButton } from "@/components/report/ExportCsvButton";

/** Cột của bảng VÀ của CSV là **cùng một biến** — CSV không thể lệch bảng. */
const COT_KHO = ["Kho", "Số SKU", "Tồn", "Khả dụng", "Tạm giữ"] as const;
const COT_SAP_HET = ["SKU", "Kho", "Tồn", "Khả dụng"] as const;

/**
 * **Báo cáo tồn kho** (`/{business}/report/inventory`) — `WP-08`.
 *
 * 🚨 **ĐÂY LÀ ẢNH CHỤP MỚI NHẤT, KHÔNG PHẢI "TỒN NGÀY X":** nguồn (`InvItems`) chỉ giữ trạng thái hiện tại;
 * muốn có lịch sử thật thì job phải chụp **mỗi ngày một lần** (nhịp ngày). 📌 Vì vậy trang ghi rõ **ngày chụp**
 * thay vì để người dùng tưởng đây là số của ngày họ chọn.
 * 📌 `daCoDuLieu = false` ⇒ *"chưa chụp lần nào"* (chưa chạy job) — hiện `—`, **không** hiện `0`.
 */
export default async function InventoryReportPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const mot = (key: string): string => {
    const giaTri = sp[key];
    return typeof giaTri === "string" ? giaTri : "";
  };

  const ngay = mot("ngay");
  const homNay = todayVn();

  let bc: TongQuanTonKhoDto | null = null;
  let loi: string | null = null;

  try {
    bc = await apiGet<TongQuanTonKhoDto>(
      `/api/v1/reports/inventory/summary${ngay ? `?ngay=${encodeURIComponent(ngay)}` : ""}`,
    );
  } catch (e) {
    loi =
      e instanceof ApiError
        ? e.code
          ? `${e.status} · ${e.code}`
          : `${e.status} · ${e.message}`
        : "không gọi được API";
  }

  const theoKho = bc?.theoKho ?? [];
  const sapHet = bc?.sapHet ?? [];
  const daCoDuLieu = bc?.daCoDuLieu === true;

  const csvKho = theoKho.map((k) => [
    k.kho ?? k.warehouseId ?? "",
    String(k.soSku ?? 0),
    String(k.onHand ?? 0),
    String(k.khaDung ?? 0),
    String(k.tamGiu ?? 0),
  ]);

  const csvSapHet = sapHet.map((m) => [
    m.sku ?? "",
    m.kho ?? m.warehouseId ?? "",
    String(m.onHand ?? 0),
    String(m.khaDung ?? 0),
  ]);

  return (
    <div className="space-y-4">
      <div className="flex items-baseline justify-between">
        <div>
          <h1 className="text-[20px] font-semibold">Tồn kho</h1>
          <p className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Ảnh chụp <b>mới nhất</b> do job tổng hợp ghi — <b>không</b> phải tồn của ngày trong quá khứ.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <ExportCsvButton fileName={csvFileName("ton-kho", { tu: bc?.ngayChup ?? homNay })} headers={[...COT_KHO]} rows={csvKho} />
          <Link href={`/${business}/report/revenue`} className="nh-btn">
            Báo cáo doanh thu →
          </Link>
        </div>
      </div>

      <form className="nh-card p-3 flex flex-wrap items-end gap-3" action={`/${business}/report/inventory`}>
        <label className="block">
          <span className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Ngày chụp (để trống = mới nhất)
          </span>
          <input className="nh-input mt-0.5" type="date" name="ngay" defaultValue={ngay} />
        </label>

        <button type="submit" className="nh-btn nh-btn-primary">
          Xem
        </button>
        <Link href={`/${business}/report/inventory`} className="nh-btn">
          Mới nhất
        </Link>

        <span className="ml-auto text-[12px]" style={{ color: "var(--text-muted)" }}>
          Ngày chụp: <b>{bc?.ngayChup ?? "chưa có"}</b>
        </span>
      </form>

      {loi && (
        <div className="nh-card p-4 text-[13px]" style={{ color: "var(--danger)" }}>
          Không tải được báo cáo tồn kho (mã {loi}). Kiểm tra API host ở cổng 54275 và quyền{" "}
          <code>eCommerce.Reports</code>.
        </div>
      )}

      {!loi && !daCoDuLieu && (
        <div className="nh-card p-4 text-[13px]">
          <b>Chưa chụp ảnh tồn kho lần nào</b> — job tổng hợp sẽ chụp ở lượt chạy kế tiếp (mỗi 15 phút), hoặc chạy
          tay: <code>POST /api/v1/reports/aggregate</code> với quyền <code>eCommerce.Reports.Aggregate</code>.
          <div className="text-[12px] mt-1" style={{ color: "var(--text-muted)" }}>
            Đây <b>không phải</b> &quot;tồn bằng 0&quot;: chưa có ảnh thì chưa biết tồn.
          </div>
        </div>
      )}

      <div className="grid grid-cols-4 gap-3">
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Tồn (OnHand)
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">{daCoDuLieu ? qty(bc?.tongOnHand) : "—"}</div>
        </div>
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Khả dụng
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">{daCoDuLieu ? qty(bc?.tongKhaDung) : "—"}</div>
        </div>
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Số kho · số SKU
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">
            {daCoDuLieu ? `${int(bc?.soKho)} · ${int(bc?.soSku)}` : "—"}
          </div>
        </div>
        <div className="nh-card p-4">
          <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
            Mặt hàng sắp hết
          </div>
          <div className="text-[24px] font-semibold tabular mt-1">{daCoDuLieu ? int(sapHet.length) : "—"}</div>
        </div>
      </div>

      <div className="nh-card overflow-hidden">
        <table className="nh-table w-full">
          <thead>
            <tr>
              {COT_KHO.map((c) => (
                <th key={c}>{c}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {theoKho.length === 0 && (
              <tr>
                <td colSpan={COT_KHO.length} className="text-center py-6" style={{ color: "var(--text-muted)" }}>
                  Chưa có ảnh tồn kho.
                </td>
              </tr>
            )}
            {theoKho.map((k) => (
              <tr key={k.warehouseId}>
                <td>{k.kho ?? k.warehouseId}</td>
                <td className="tabular text-right">{int(k.soSku)}</td>
                <td className="tabular text-right">{qty(k.onHand)}</td>
                <td className="tabular text-right font-medium">{qty(k.khaDung)}</td>
                <td className="tabular text-right">{qty(k.tamGiu)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="nh-card p-2">
        <div className="flex items-center justify-between px-2 py-1">
          <h2 className="text-[15px] font-semibold">Mặt hàng sắp hết</h2>
          <ExportCsvButton
            fileName={csvFileName("sap-het", { tu: bc?.ngayChup ?? homNay })}
            headers={[...COT_SAP_HET]}
            rows={csvSapHet}
          />
        </div>

        <table className="nh-table w-full">
          <thead>
            <tr>
              {COT_SAP_HET.map((c) => (
                <th key={c}>{c}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sapHet.length === 0 && (
              <tr>
                <td colSpan={COT_SAP_HET.length} className="text-center py-6" style={{ color: "var(--text-muted)" }}>
                  Không có mặt hàng nào dưới ngưỡng.
                </td>
              </tr>
            )}
            {sapHet.map((m) => (
              <tr key={`${m.warehouseId}-${m.sku}`}>
                <td>{m.sku}</td>
                <td>{m.kho ?? m.warehouseId}</td>
                <td className="tabular text-right">{qty(m.onHand)}</td>
                <td className="tabular text-right" style={{ color: "var(--danger)" }}>
                  {qty(m.khaDung)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="nh-card p-4 text-[12px] space-y-1" style={{ color: "var(--text-muted)" }}>
        <div className="font-medium" style={{ color: "var(--text)" }}>
          Cách đọc
        </div>
        <div>
          <b>Tồn</b> = tổng hàng thuộc kho. <b>Khả dụng</b> = Tồn − Tạm giữ − Đang chuyển − Giữ chỗ − Lỗi (công
          thức `D-27`, tính ở server — UI không tự trừ).
        </div>
        <div>
          Ngưỡng &quot;sắp hết&quot; lấy từ cấu hình <code>Reporting:NguongSapHet</code> (mặc định 5) và chỉ hiện{" "}
          <b>20</b> mặt hàng thấp nhất — danh sách đầy đủ có trong CSV hoặc màn <b>Tồn kho</b>.
        </div>
        <div>{amount(bc?.tongTamGiu ?? 0)} đang tạm giữ trong tổng số trên.</div>
      </div>
    </div>
  );
}
