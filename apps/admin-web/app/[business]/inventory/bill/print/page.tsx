import Link from "next/link";
import { apiGet, apiGetSafe } from "@/lib/api";
import { amount, dateTime, qty } from "@/lib/format";
import {
  COUNTERPARTY_LABEL,
  DIRECTION_LABEL,
  TYPE_LABEL,
  modeLabel,
  sumQuantity,
  toBill,
  toLine,
  type Bill,
  type BillLine,
  type StockDocumentDto,
  type StockDocumentLineDto,
  type WarehouseDto,
} from "@/lib/stock";
import { PrintButton } from "@/components/inventory/PrintButton";

/** Trần số phiếu in một lần — chặn URL dài và 50 request liên tiếp từ một thao tác. */
const TOI_DA_PHIEU = 20;

type BanIn = { phieu: Bill; dong: BillLine[] };

/**
 * **Trang in phiếu kho** (`/{business}/inventory/bill/print?ids=a,b,c`).
 *
 * 📌 Nhanh có `In các phiếu XNK đã chọn` trong `Thao tác ▾`; `WP-04` chưa có mẫu in cấu hình được
 * (P0 #18 — thuộc WP sau) nên đây là **mẫu cố định** đủ để giao hàng/ký nội bộ: thông tin phiếu + dòng hàng
 * + ô ký. 🔒 In **dữ liệu đã ghi sổ hay còn Nháp đều được** — nhưng **ghi rõ trạng thái** trên giấy, nếu
 * không người nhận sẽ tưởng phiếu Nháp đã có hiệu lực.
 */
export default async function PrintBillsPage({
  params,
  searchParams,
}: {
  params: Promise<{ business: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { business } = await params;
  const sp = await searchParams;

  const raw = typeof sp.ids === "string" ? sp.ids : "";
  const ids = raw
    .split(",")
    .map((x) => x.trim())
    .filter((x) => x.length > 0)
    .slice(0, TOI_DA_PHIEU);

  const khoRaw = await apiGetSafe<WarehouseDto[]>("/api/app/warehouse", []);
  const khoTheoId = new Map(khoRaw.map((k) => [k.id, k]));

  const banIn: BanIn[] = [];

  for (const id of ids) {
    try {
      const phieu = toBill(await apiGet<StockDocumentDto>(`/api/v1/stock/bills/${id}`));
      const dong = (await apiGetSafe<StockDocumentLineDto[]>(`/api/v1/stock/bills/${id}/lines`, [])).map(toLine);
      banIn.push({ phieu, dong });
    } catch {
      // Một phiếu hỏng/không quyền ⇒ in các phiếu còn lại, có ghi chú thiếu — không làm sập cả bản in.
    }
  }

  const tenKho = (id: string | null): string => {
    if (!id) return "—";
    const kho = khoTheoId.get(id);
    return kho ? `${kho.code} — ${kho.name}` : `${id.slice(0, 8)}…`;
  };

  if (banIn.length === 0) {
    return (
      <div className="nh-card p-6 space-y-2 max-w-[640px]">
        <div className="text-[15px] font-semibold">Không có phiếu nào để in</div>
        <p className="text-[13px]" style={{ color: "var(--text-muted)" }}>
          Chọn ít nhất một phiếu ở DS phiếu rồi bấm <b>In phiếu</b>, hoặc kiểm tra lại quyền{" "}
          <code>eCommerce.Inventory</code>.
        </p>
        <Link href={`/${business}/inventory/bill`} className="nh-btn inline-flex">
          Về danh sách
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 print:hidden">
        <Link href={`/${business}/inventory/bill`} className="nh-btn">
          ← Danh sách
        </Link>
        <span className="text-[12px]" style={{ color: "var(--text-muted)" }}>
          {banIn.length} phiếu · mẫu in cố định (chưa có mẫu cấu hình — P0 #18)
        </span>
        <div className="ml-auto">
          <PrintButton />
        </div>
      </div>

      {banIn.map(({ phieu, dong }) => (
        <section
          key={phieu.id}
          className="nh-card p-6 space-y-3"
          style={{ breakAfter: "page", background: "#fff" }}
        >
          <div className="text-center">
            <div className="text-[18px] font-semibold">PHIẾU {TYPE_LABEL[phieu.type]?.toUpperCase() ?? "KHO"}</div>
            <div className="text-[12px]" style={{ color: "var(--text-muted)" }}>
              Số: <b>{phieu.number}</b> · Ngày {dateTime(phieu.occurredAtUtc)} ·{" "}
              {phieu.status === "Draft" ? "NHÁP (chưa ghi sổ)" : phieu.status === "Posted" ? "Đã ghi sổ" : "Đã huỷ"}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-[13px]">
            <div>
              <span style={{ color: "var(--text-muted)" }}>Kho: </span>
              {tenKho(phieu.warehouseId)}
            </div>
            <div>
              <span style={{ color: "var(--text-muted)" }}>Chiều: </span>
              {phieu.direction ? (DIRECTION_LABEL[phieu.direction] ?? phieu.direction) : "Không chiều (kiểm kê)"}
            </div>
            <div>
              <span style={{ color: "var(--text-muted)" }}>Kho đích: </span>
              {phieu.toWarehouseId ? tenKho(phieu.toWarehouseId) : "—"}
            </div>
            <div>
              <span style={{ color: "var(--text-muted)" }}>Kiểu: </span>
              {modeLabel(phieu.mode)}
            </div>
            <div>
              <span style={{ color: "var(--text-muted)" }}>Đối tượng: </span>
              {phieu.counterpartyType
                ? (COUNTERPARTY_LABEL[phieu.counterpartyType] ?? phieu.counterpartyType)
                : "—"}
            </div>
            <div>
              <span style={{ color: "var(--text-muted)" }}>Ghi chú: </span>
              {phieu.note ?? "—"}
            </div>
          </div>

          <table className="nh-table" style={{ borderTop: "1px solid var(--border)" }}>
            <thead>
              <tr>
                <th className="w-[40px] text-right">#</th>
                <th>SKU</th>
                <th className="w-[100px] text-right">Số lượng</th>
                <th className="w-[110px] text-right">Đơn giá</th>
                <th className="w-[70px] text-right">VAT %</th>
                <th className="w-[110px] text-right">Thành tiền</th>
                <th className="w-[170px]">Serial / IMEI</th>
              </tr>
            </thead>
            <tbody>
              {dong.map((d, i) => (
                <tr key={d.id}>
                  <td className="text-right tabular" style={{ color: "var(--text-muted)" }}>
                    {i + 1}
                  </td>
                  <td>{d.sku}</td>
                  <td className="text-right tabular">{qty(d.quantity)}</td>
                  <td className="text-right tabular">{amount(d.unitPrice)}</td>
                  <td className="text-right tabular">{d.vatPercent === null ? "—" : qty(d.vatPercent)}</td>
                  <td className="text-right tabular">{amount(d.amount)}</td>
                  <td className="text-[12px]" style={{ color: "var(--text-muted)" }}>
                    {d.imeiSerials ?? "—"}
                  </td>
                </tr>
              ))}
              <tr>
                <td></td>
                <td className="font-semibold">Tổng</td>
                <td className="text-right tabular font-semibold">{qty(sumQuantity(dong))}</td>
                <td></td>
                <td></td>
                <td className="text-right tabular font-semibold">{amount(phieu.totalAmount)}</td>
                <td></td>
              </tr>
              <tr>
                <td colSpan={4} className="text-right" style={{ color: "var(--text-muted)" }}>
                  VAT
                </td>
                <td className="text-right tabular">{amount(phieu.totalVat)}</td>
                <td colSpan={2}></td>
              </tr>
              <tr>
                <td colSpan={4} className="text-right font-semibold">
                  Thanh toán
                </td>
                <td className="text-right tabular font-semibold">{amount(phieu.totalPayment)}</td>
                <td colSpan={2}></td>
              </tr>
            </tbody>
          </table>

          <div className="grid grid-cols-3 gap-4 text-center text-[12px] pt-4">
            {["Người lập phiếu", "Người giao hàng", "Thủ kho"].map((vaiTro) => (
              <div key={vaiTro}>
                <div className="font-medium">{vaiTro}</div>
                <div className="h-[64px]" />
                <div style={{ color: "var(--text-muted)" }}>(Ký, họ tên)</div>
              </div>
            ))}
          </div>

          <p className="text-[11px]" style={{ color: "var(--text-muted)" }}>
            Tiền làm tròn 2 số lẻ theo từng dòng rồi mới cộng (D-37); dòng <b>chưa khai giá</b> ⇒ tổng hiện
            <b> —</b>. Đơn giá là giá <b>ghi tại thời điểm lập phiếu</b>. Mẫu in cấu hình được thuộc P0 #18 (WP sau).
          </p>
        </section>
      ))}
    </div>
  );
}
