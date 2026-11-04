import { expect, test } from "@playwright/test";
import { BUSINESS } from "../support/env";
import { batBuoc, docTrangThai, ghiTrangThai } from "../support/state";
import { dangNhap, soVn } from "../support/ui";

/**
 * **Luồng ② — Tạo sản phẩm** (`07 §WP-09`, `04 §5` màn #4/#5).
 *
 * 🚨 **ĐIỂM CẮT CÓ LÝ DO:** **chưa có FORM sản phẩm trên FE** (việc FE 3 ngày của `WP-03` chưa làm —
 * `handover-n3` ghi rõ). Vì vậy luồng này đo **đường dọc thật** tới nơi nó dừng: `POST /api/v1/products` qua
 * **BFF** (cookie + token server-side, `D-12`) → DB → **DS sản phẩm do RSC render**. 📌 Nhận chủ: `WP-03` (FE).
 *
 * 📌 **Vì sao có thêm bước tạo "biến thể" (SKU):** mô hình Nhanh (`D-07`) giữ giá/tồn ở **ĐVT**, còn cột
 * `Sku` của DTO danh sách vẫn đọc từ **biến thể cũ** (`ProductApiMapper.ToListItem` → `Sku = first?.Sku`).
 * ⇒ Một sản phẩm **chỉ có ĐVT** sẽ hiện SKU rỗng và **không bán được ở POS** (POS lọc bỏ dòng không có SKU).
 * Đây là **nợ đã ghi** ở `handover-n10` §5 (không phải lỗi của bộ test).
 */
test.describe("② Tạo sản phẩm", () => {
  test("tạo SP qua BFF thật ⇒ hiện trên DS sản phẩm (kèm SKU bán được)", async ({ page }) => {
    await dangNhap(page);

    const runId = batBuoc("runId", "global-setup");
    const ma = `E2E-${runId}-SP`;
    const ten = `Bánh E2E ${runId}`;
    const gia = 15000;

    // ── Tạo sản phẩm: ĐÚNG đường của UI (BFF giữ token, `D-12`) ──────────────────────────────────────
    const tao = await page.request.post("/api/proxy/v1/products", {
      data: {
        code: ma,
        name: ten,
        type: 1, // Standard
        units: [
          { name: "Cái", conversionFactor: 1, isBase: true, salePrice: gia, barcode: `E2E${runId}`, importPrice: 10000 },
        ],
      },
    });

    expect(tao.status(), `tạo sản phẩm: ${await tao.text()}`).toBe(201);
    const sp = (await tao.json()) as { id: string; code: string };

    const taoSku = await page.request.post(`/api/proxy/app/product/variant/${sp.id}`, {
      data: { sku: ma, price: gia, barcode: `E2E${runId}` },
    });
    expect(taoSku.ok(), `tạo SKU: ${await taoSku.text()}`).toBeTruthy();

    // ── Khẳng định trên **UI** (RSC đọc API thật, không mock) ────────────────────────────────────────
    await page.goto(`/${BUSINESS}/product/item?q=${encodeURIComponent(ma)}`);

    const dong = page.locator("tbody tr").filter({ hasText: ma });
    await expect(dong).toHaveCount(1);
    await expect(dong).toContainText(ten);
    await expect(dong).toContainText(ma); // cột SKU
    await expect(dong).toContainText(soVn(gia)); // cột Giá bán (ĐVT gốc)
    await expect(dong).toContainText("Mới"); // `ProductStatus.Draft` (`D-19`)

    ghiTrangThai({ sanPham: { id: sp.id, code: ma, name: ten, sku: ma, gia } });

    // 📌 Đọc lại từ trạng thái: nếu `ghiTrangThai` hỏng thì luồng ③ sẽ báo "thiếu sanPham" thay vì đỏ mờ.
    expect(docTrangThai().sanPham?.sku).toBe(ma);
  });
});
