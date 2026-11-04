import { expect, test } from "@playwright/test";
import { caHienTai, timHoaDon, tonKho } from "../support/api";
import { BUSINESS } from "../support/env";
import { batBuoc, ghiTrangThai } from "../support/state";
import { dangNhap, soVn } from "../support/ui";

/**
 * **Luồng ④ — Bán lẻ tại quầy (POS)** (`07 §WP-09`, `04 §5` màn #12).
 *
 * Đo trọn đường thu ngân: **mở ca** → chọn SP (gõ SKU) → **thu tiền mặt** → **Lưu (F9)** → nhận **số hoá đơn**.
 * 📌 Số hoá đơn do **server** sinh; test **không** tự đặt số — đọc số từ thông báo rồi tra lại bằng API để lấy
 * `id` (luồng ⑤ in đúng hoá đơn đó).
 *
 * 🚨 Mở ca là **bắt buộc** (`D-42`: một quầy một ca — tiền mặt phải có ca để đối chiếu két). Bỏ bước này thì
 * nút Lưu vẫn xanh nhưng `cashSessionId = null` và luồng ⑦ không có gì để kết.
 */
test.describe("④ Bán lẻ", () => {
  test("mở ca · bán 1 SP · thu tiền mặt · lưu hoá đơn", async ({ page }) => {
    await dangNhap(page);

    const sp = batBuoc("sanPham", "② tạo sản phẩm");
    const kho = batBuoc("kho", "③ nhập kho");
    const soLuong = 2;
    const phaiTra = sp.gia * soLuong;

    // 🔎 Tiền điều kiện ĐỌC ĐƯỢC: chưa có tồn thì POS trả `eCommerce:Inventory:0001` (không đủ tồn) —
    // thông điệp đó đúng nhưng dễ bị đọc thành "POS hỏng". Kiểm ở đây để biết ngay lỗi thuộc luồng ③.
    const ton = await tonKho(sp.sku);
    expect(ton?.available ?? 0, "luồng ③ chưa nhập kho cho SKU này ⇒ không thể bán").toBeGreaterThanOrEqual(soLuong);

    await page.goto(`/${BUSINESS}/pos/bill?warehouseId=${kho.id}`);
    await expect(page.getByRole("heading", { name: "Bán lẻ tại quầy" })).toBeVisible();

    // ── MỞ CA (tiền đầu ca 500.000) ──────────────────────────────────────────────────────────────────
    const tienDauCa = 500000;
    const oTienDauCa = page.locator("label").filter({ hasText: "Tiền đầu ca" }).locator("input");
    await expect(oTienDauCa).toBeVisible();
    await oTienDauCa.fill(String(tienDauCa));

    await page
      .locator("div.nh-card")
      .filter({ hasText: "Chưa mở ca" })
      .getByRole("button", { name: "Mở ca" })
      .click();

    // Header POS đổi sang "Ca POS-CA-…" khi ca đã mở (server trả ca, không phải state cục bộ).
    await expect(page.getByText(/Ca POS-CA-\d{8}-\d+/)).toBeVisible();

    const ca = await caHienTai(kho.id);
    expect(ca, "API không thấy ca vừa mở — nút Mở ca có thể đã báo lỗi").not.toBeNull();
    ghiTrangThai({ ca: { id: ca!.id, number: ca!.number } });

    // ── CHỌN SẢN PHẨM (gõ SKU, chọn ở dropdown — đúng thao tác thu ngân) ─────────────────────────────
    const oTim = page.getByPlaceholder(/Quét mã vạch hoặc gõ tên\/mã\/SKU/);
    await oTim.fill(sp.sku);
    await page.getByRole("button", { name: new RegExp(sp.name) }).first().click();

    const dongHang = page.locator(`tr[data-pos-dong="${sp.sku}"]`);
    await expect(dongHang).toBeVisible();
    await dongHang.locator('input[type="number"]').first().fill(String(soLuong)); // 0 = SL

    // ── THANH TOÁN TIỀN MẶT (bấm nút, không gõ tay số còn lại) ──────────────────────────────────────
    await page.getByRole("button", { name: /Tiền mặt/ }).click();
    await expect(page.getByText(new RegExp(`Đã thu\\s*${soVn(phaiTra)} đ`))).toBeVisible();

    // ── LƯU (F9) ─────────────────────────────────────────────────────────────────────────────────────
    await page.getByRole("button", { name: "Lưu (F9)" }).click();

    const thongBao = page.getByText(new RegExp(`Đã lưu hoá đơn POS-\\d{8}-\\d+`)).first();
    await expect(thongBao).toBeVisible({ timeout: 30_000 });
    const soHoaDon = (await thongBao.textContent())?.match(/POS-\d{8}-\d+/)?.[0] ?? "";
    expect(soHoaDon, "không đọc được số hoá đơn từ thông báo").not.toBe("");

    // ── ĐỐI CHIẾU BẰNG API (không tin mỗi thông báo trên UI) ────────────────────────────────────────
    const hoaDon = await timHoaDon(soHoaDon);
    expect(hoaDon, `API không có hoá đơn ${soHoaDon}`).not.toBeNull();
    expect(hoaDon?.totalPayment).toBe(phaiTra);
    expect(hoaDon?.lines?.[0]?.sku).toBe(sp.sku);

    ghiTrangThai({
      hoaDon: { id: hoaDon!.id, number: hoaDon!.number, total: hoaDon!.totalPayment },
      ca: { id: ca!.id, number: ca!.number },
    });
  });
});
