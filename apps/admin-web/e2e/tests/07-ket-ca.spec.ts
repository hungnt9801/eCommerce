import { expect, test } from "@playwright/test";
import { caHienTai } from "../support/api";
import { BUSINESS } from "../support/env";
import { batBuoc } from "../support/state";
import { dangNhap, soVn } from "../support/ui";

/**
 * **Luồng ⑦ — Kết ca** (`07 §WP-09`, `04 §5` màn #12 · `D-42` ⑧).
 *
 * Đây là luồng **tiền mặt**: `TienMatThucThu = Σ tiền mặt − tiền thối` (KHÔNG phải Σ tiền mặt), nên
 * `ExpectedCash` = **tiền đầu ca + Σ thu tiền mặt** và `Chênh lệch` = `bàn giao − ExpectedCash`.
 *
 * 🚨 **VÌ SAO PHẢI ĐO `chênh lệch = 0`:** nếu công thức cộng **tiền khách đưa** thay vì **tiền thực thu**, mọi
 * ca đều báo **thiếu** đúng bằng tiền thối — và không ai biết đó là lỗi phần mềm hay thu ngân lấy tiền.
 * 📌 Test bàn giao **đúng** số server tính ⇒ chênh lệch 0; lệch khác 0 là **đỏ**, không phải "sai số nhỏ".
 */
test.describe("⑦ Kết ca", () => {
  test("kết ca: tiền cuối ca = đầu ca + thu tiền mặt, chênh lệch 0", async ({ page }) => {
    await dangNhap(page);

    const kho = batBuoc("kho", "③ nhập kho");
    const ca = batBuoc("ca", "④ bán lẻ");
    const hoaDon = batBuoc("hoaDon", "④ bán lẻ");

    // Tiền đầu ca 500.000 (luồng ④) + hoá đơn thu TIỀN MẶT đúng bằng tổng phải trả.
    const tienDauCa = 500000;
    const tienMat = tienDauCa + hoaDon.total;

    await page.goto(`/${BUSINESS}/pos/bill?warehouseId=${kho.id}`);
    await expect(page.getByText(new RegExp(`Ca ${ca.number}`))).toBeVisible();

    // Nút "Kết ca" trên header mở khối nhập tiền bàn giao.
    await page.getByRole("button", { name: "Kết ca", exact: true }).click();
    await expect(page.getByText(new RegExp(`Kết ca ${ca.number}`))).toBeVisible();

    await page.locator("label").filter({ hasText: "Tiền bàn giao" }).locator("input").fill(String(tienMat));
    await page.getByRole("button", { name: /Kết ca \(không quay ngược được\)/ }).click();

    const thongBao = page.getByText(new RegExp(`ĐÃ KẾT CA ${ca.number}`));
    await expect(thongBao).toBeVisible();
    await expect(thongBao).toContainText(`tiền cuối ca ${soVn(tienMat)} đ`);
    await expect(thongBao).toContainText("chênh lệch 0 đ");

    // Ca đã đóng ⇒ quầy **không còn ca mở** (API trả `204`). Đọc lại để không tin mỗi thông báo.
    expect(await caHienTai(kho.id), "ca vẫn còn mở sau khi bấm Kết ca").toBeNull();
  });
});
