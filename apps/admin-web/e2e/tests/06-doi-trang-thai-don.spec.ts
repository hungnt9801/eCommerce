import { expect, test } from "@playwright/test";
import { BUSINESS } from "../support/env";
import { gieoDonHang } from "../support/don-hang";
import { batBuoc, ghiTrangThai } from "../support/state";
import { dangNhap } from "../support/ui";

/**
 * **Luồng ⑥ — Đổi trạng thái đơn** (`07 §WP-09`, `04 §5` màn #11).
 *
 * Đo **đúng** điều `WP-05` hứa: nút chuyển trạng thái lấy từ **bảng cạnh của domain** (không có nút nào
 * bấm vào rồi nhận `0001`), bấm xong **badge đổi** và **lịch sử** ghi một dòng.
 *
 * ⚠️ **Đơn được seed bằng SQL** (`support/don-hang.ts`) vì **chưa có API tạo đơn** dùng được — nợ đã ghi ở
 * `handover-n10` §2 (nhận chủ `WP-05` P1). Bước `Mới → Chờ xác nhận` **không** đụng sổ cái (`D-39`), nên đơn
 * seed **không** cần lượt giữ chỗ.
 */
test.describe("⑥ Đổi trạng thái đơn", () => {
  test("Mới → Chờ xác nhận (nút từ state machine, không phải chuỗi if của UI)", async ({ page }) => {
    await dangNhap(page);

    const sp = batBuoc("sanPham", "② tạo sản phẩm");
    const runId = batBuoc("runId", "global-setup");

    const don = gieoDonHang(sp.sku, runId);
    ghiTrangThai({ donHang: don });

    await page.goto(`/${BUSINESS}/order/manage/${don.id}`);

    await expect(page.getByRole("heading", { name: new RegExp(don.number) })).toBeVisible();
    await expect(page.getByText("Mới", { exact: true }).first()).toBeVisible();

    // Nút chỉ có vì `allowedTransitions` của server trả về (bảng cạnh `OrderStateMachine`).
    const nutChuyen = page.getByRole("button", { name: "→ Chờ xác nhận" });
    await expect(nutChuyen).toBeVisible();
    await nutChuyen.click();

    await expect(page.getByText(/Đã chuyển sang "Chờ xác nhận"/)).toBeVisible();
    await expect(page.getByText("Chờ/Đang xác nhận").first()).toBeVisible();

    // ── Khẳng định lại bằng API qua BFF (UI có thể vẽ lại nhãn mà DB không đổi) ─────────────────────
    const chiTiet = await page.request.get(`/api/proxy/v1/orders/${don.id}`);
    expect(chiTiet.ok(), `GET /orders/{id} → ${chiTiet.status()}`).toBeTruthy();
    const du = (await chiTiet.json()) as { fulfillmentStatus: number; statusLabel: string };
    expect(du.fulfillmentStatus).toBe(9); // PendingConfirm
    expect(du.statusLabel).toBe("Chờ/Đang xác nhận");

    // Và lịch sử phải có một dòng cho lần chuyển này (append-only — không sửa dòng cũ).
    const lichSu = await page.request.get(`/api/proxy/v1/orders/${don.id}/history?maxResultCount=10`);
    expect(lichSu.ok()).toBeTruthy();
    const ls = (await lichSu.json()) as { items: Array<{ toStatus: number }>; totalCount: number };
    expect(ls.totalCount).toBe(1);
    expect(ls.items[0].toStatus).toBe(9);
  });
});
