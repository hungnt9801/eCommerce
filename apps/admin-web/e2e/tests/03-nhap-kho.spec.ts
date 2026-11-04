import { expect, test } from "@playwright/test";
import { taoKho, tonKho } from "../support/api";
import { BUSINESS } from "../support/env";
import { batBuoc, docTrangThai, ghiTrangThai } from "../support/state";
import { dangNhap, soLuongVn } from "../support/ui";

/**
 * **Luồng ③ — Nhập kho qua UI** (`07 §WP-09`, `04 §5` màn #7/#8).
 *
 * Chuỗi được đo **trọn trên giao diện**: chọn kho → tìm SP → khai số lượng + đơn giá → **Lưu phiếu (Nháp)**
 * → trang chi tiết → **Ghi sổ phiếu** → tồn kho **đổi thật**.
 *
 * 🚨 **VÌ SAO PHẢI BẤM "GHI SỔ" (không dừng ở Lưu):** phiếu `Draft` **không** đổi tồn kho (đúng thiết kế).
 * Một bộ test chỉ kiểm "lưu được phiếu" sẽ **xanh** dù sổ cái không hề ghi — đúng lỗi mà `N4` đã phải dựng
 * probe riêng để bắt.
 */
test.describe("③ Nhập kho", () => {
  test("tạo phiếu nhập trên UI ⇒ ghi sổ ⇒ tồn 10", async ({ page }) => {
    await dangNhap(page);

    const sp = batBuoc("sanPham", "② tạo sản phẩm");
    const runId = batBuoc("runId", "global-setup");
    const soLuong = 10;
    const donGia = 10000;

    // ── Setup bằng API: quầy/kho (hạ tầng, không phải đường đo) ───────────────────────────────────────
    const kho = await taoKho(`E2E-${runId}-Q`, `Quầy E2E ${runId}`);
    ghiTrangThai({ kho });

    // ── UI: tạo phiếu nhập ───────────────────────────────────────────────────────────────────────────
    await page.goto(`/${BUSINESS}/inventory/bill/new`);
    await expect(page.getByRole("heading", { name: "Tạo phiếu kho" })).toBeVisible();

    // 📌 `getByLabel("Kho")` KHÔNG dùng được ở đây: `<label>` bọc cả `<select>` ⇒ text của label gồm **toàn bộ
    // text các `<option>`**, và `exact: true` không bao giờ khớp. Địa chỉ đúng là **ARIA name** của combobox.
    await page.getByRole("combobox", { name: "Kho", exact: true }).selectOption(kho.id);

    await page.getByPlaceholder("Tìm sản phẩm (tên / mã / SKU)…").fill(sp.sku);
    await page.getByRole("button", { name: new RegExp(sp.name) }).first().click();

    const dongHang = page.locator("tbody tr").filter({ hasText: sp.name }).first();
    const oNhap = dongHang.locator("input");
    await oNhap.nth(1).fill(String(soLuong)); // 0 = SKU · 1 = Số lượng
    await oNhap.nth(2).fill(String(donGia)); // 2 = Đơn giá

    await expect(dongHang).toContainText(soLuongVn(soLuong * donGia)); // Thành tiền dự phóng

    await page.getByRole("button", { name: "Lưu phiếu (Nháp)" }).click();

    // ── Trang chi tiết phiếu (server điều hướng sau khi lưu) ─────────────────────────────────────────
    await expect(page).toHaveURL(new RegExp(`/${BUSINESS}/inventory/bill/[0-9a-f-]{36}`));
    await expect(page.getByRole("button", { name: "Ghi sổ phiếu" })).toBeVisible();

    // Số phiếu do **server** sinh (`suggestNumber`: `PN-yyyyMMdd-NNNN`) — đọc từ trang, không tự đặt.
    const maPhieu = (await page.locator("body").innerText()).match(/PN-\d{8}-\d+/)?.[0] ?? "";
    const idPhieu = page.url().split("/").pop() ?? "";
    expect(maPhieu, "không đọc được số phiếu trên trang chi tiết").not.toBe("");
    ghiTrangThai({ phieuNhap: { id: idPhieu, number: maPhieu } });

    await page.getByRole("button", { name: "Ghi sổ phiếu" }).click();
    await expect(page.getByText(/Đã ghi sổ — \d+ bút toán sổ cái/)).toBeVisible();

    // ── Tồn kho: ĐỌC CẢ HAI PHÍA (API là nguồn, UI là thứ người dùng thấy) ────────────────────────────
    const ton = await tonKho(sp.sku);
    expect(ton, `không thấy tồn của SKU ${sp.sku} sau khi ghi sổ`).not.toBeNull();
    expect(ton?.warehouseId).toBe(kho.id);
    expect(ton?.onHand).toBe(soLuong);

    await page.goto(`/${BUSINESS}/product/inventory?sku=${encodeURIComponent(sp.sku)}`);
    const dongTon = page.getByRole("row").filter({ hasText: sp.sku });
    await expect(dongTon).toHaveCount(1);
    await expect(dongTon).toContainText(soLuongVn(soLuong)); // cột "Tồn" = 10

    // 📌 Trạng thái chia sẻ cho luồng ④ (POS bán đúng kho vừa nhập).
    expect(docTrangThai().phieuNhap?.id).toBe(idPhieu);
  });
});
