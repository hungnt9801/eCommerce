/**
 * **27 ảnh chuẩn — phần ĐỘNG (8 route)** (`UI-41` / `U41-3`, `D-331`, `D-333`).
 *
 * 7 route `[id]` + POS gắn kho: dữ liệu dựng bằng **API thật** (`support/fixtures.ts`); đơn hàng dùng
 * ngoại lệ SQL duy nhất của `D-49`. Route nào không dựng được dữ liệu ⇒ `test.skip` + testplan ghi
 * "chưa chụp được" — **không chụp ảnh giả**.
 *
 * Cùng luật với `01-tinh.spec.ts`: mỗi ca khẳng định **nội dung thật** rồi mới so ảnh. Chuỗi mang `runId`
 * (số đơn/phiếu/mã KM/tên khách) được `maskChuoi` che, vì đó là giá trị của **lượt chạy**, không phải
 * bất biến giao diện.
 */
import { expect, test, type Locator, type Page } from "@playwright/test";
import { dangNhap, duong } from "../../support/ui";
import { type Fixture, duongPos, taoFixture } from "../support/fixtures";
import { chup, maskChung, maskChuoi } from "../support/manhinh";

let fixture: Fixture;

test.beforeAll(async () => {
  fixture = await taoFixture();
});

/** Ghép mask chung + mask riêng của ca. */
function mask(page: Page, them: Locator[]): Locator[] {
  return [...maskChung(page), ...them];
}

test("#12 pos/bill — quầy bán lẻ (kho fixture)", async ({ page }) => {
  await dangNhap(page);
  await page.goto(duongPos(fixture));

  await expect(page.getByRole("heading", { name: "Bán lẻ tại quầy" })).toBeVisible();
  // POS mở ở trạng thái CHƯA có tab hoá đơn (ô quét SP chỉ hiện sau khi bấm F1) — khẳng định đúng màn hiện tại.
  await expect(page.getByText("Chưa có tab hoá đơn")).toBeVisible();

  await chup(
    page,
    "12-pos-bill.png",
    mask(page, [
      // "Quầy: <kho fixture> · Ca <số ca> · mở <tiền> · N hoá đơn" — số ca/tên kho là dữ liệu lượt chạy.
      page.locator("main span", { hasText: "Quầy:" }),
      page.locator("main select"),
    ]),
  );
});

test("#7 order/manage/[id] — chi tiết đơn (seed D-49)", async ({ page }) => {
  const don = fixture.order;

  if (!don) {
    test.skip(true, fixture.loi["07-order-detail"] ?? "không dựng được đơn seed");
    return;
  }

  await dangNhap(page);
  await page.goto(duong(`/order/manage/${don.id}`));

  await expect(page.getByRole("heading", { name: new RegExp(don.number) })).toBeVisible();

  await chup(
    page,
    "07-order-detail.png",
    mask(page, [
      // h1 chứa số đơn + badge trạng thái; breadcrumb + dòng "tạo lúc" mang thời điểm tạo.
      page.locator("main h1"),
      page.locator("main p", { hasText: "tạo lúc" }),
      // `Kho xuất` hiện 8 ký tự đầu của GUID kho (ngẫu nhiên mỗi lượt).
      page.getByText(/^[0-9a-f]{8}$/),
      ...maskChuoi(page, [don.number]),
    ]),
  );
});

test("#10 inventory/bill/[id] — chi tiết phiếu kho (đã ghi sổ)", async ({ page }) => {
  const phieu = fixture.stockBill;

  if (!phieu) {
    test.skip(true, fixture.loi["10-inventory-bill-detail"] ?? "không dựng được phiếu kho");
    return;
  }

  await dangNhap(page);
  await page.goto(duong(`/inventory/bill/${phieu.id}`));

  await expect(page.getByRole("heading", { name: new RegExp(phieu.number) })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Thông tin phiếu" })).toBeVisible();

  await chup(
    page,
    "10-inventory-bill-detail.png",
    mask(page, [
      page.locator("main h1"),
      ...maskChuoi(page, [phieu.number, `PN-${fixture.runId}`, `${fixture.warehouse.code} — ${fixture.warehouse.name}`]),
    ]),
  );
});

test("#11 inventory/bill/print?ids= — mẫu in phiếu kho", async ({ page }) => {
  const phieu = fixture.stockBill;

  if (!phieu) {
    test.skip(true, fixture.loi["11-inventory-bill-print"] ?? "không dựng được phiếu kho");
    return;
  }

  await dangNhap(page);
  await page.goto(duong(`/inventory/bill/print?ids=${phieu.id}`));

  await expect(page.getByText(/^PHIẾU NHẬP KHO$/)).toBeVisible();

  await chup(
    page,
    "11-inventory-bill-print.png",
    mask(page, [
      // "Số: <số phiếu> · Ngày <thời điểm> · <trạng thái>" — một dòng ghép, không tách phần tử.
      page.getByText(/^Số: .* · Ngày .* · /),
      // "<số> phiếu · mẫu in cố định …" trên thanh công cụ.
      page.getByText(/phiếu · mẫu in cố định/),
      page.getByText(/^Kho: /),
      ...maskChuoi(page, [phieu.number, `${fixture.warehouse.code} — ${fixture.warehouse.name}`]),
    ]),
  );
});

test("#15 print/bill/[id] — in hoá đơn bán lẻ (khổ 380px)", async ({ page }) => {
  const hoaDon = fixture.retailBill;

  if (!hoaDon) {
    test.skip(true, fixture.loi["15-print-bill"] ?? "không dựng được hoá đơn bán lẻ");
    return;
  }

  await dangNhap(page);
  await page.goto(duong(`/print/bill/${hoaDon.id}`));

  await expect(page.getByText("HOÁ ĐƠN BÁN LẺ", { exact: true })).toBeVisible();

  await chup(
    page,
    "15-print-bill.png",
    mask(page, [
      // `tenKho()` in ra "<mã kho> — <tên kho>" (mang runId) dưới số hoá đơn.
      ...maskChuoi(page, [`${fixture.warehouse.code} — ${fixture.warehouse.name}`]),
    ]),
  );
});

test("#17 customer/[id] — chi tiết khách (5 tab)", async ({ page }) => {
  const khach = fixture.customer;

  if (!khach) {
    test.skip(true, fixture.loi["17-customer-detail"] ?? "không dựng được khách hàng");
    return;
  }

  await dangNhap(page);
  await page.goto(duong(`/customer/${khach.id}`));

  await expect(page.getByRole("heading", { name: khach.name })).toBeVisible();

  await chup(
    page,
    "17-customer-detail.png",
    mask(page, [page.locator("main h1"), ...maskChuoi(page, [khach.name])]),
  );
});

test("#23 promotion/[id] — chi tiết khuyến mãi", async ({ page }) => {
  const km = fixture.promotion;

  if (!km) {
    test.skip(true, fixture.loi["23-promotion-detail"] ?? "không dựng được khuyến mãi");
    return;
  }

  await dangNhap(page);
  await page.goto(duong(`/promotion/${km.id}`));

  await expect(page.getByRole("heading", { name: "Chi tiết khuyến mãi" })).toBeVisible();

  await chup(
    page,
    "23-promotion-detail.png",
    mask(page, maskChuoi(page, [km.code, km.name])),
  );
});

test("#26 notifications/templates/[id] — soạn version mẫu", async ({ page }) => {
  const mau = fixture.template;

  if (!mau) {
    test.skip(true, fixture.loi["26-template-detail"] ?? "không dựng được mẫu thông báo");
    return;
  }

  await dangNhap(page);
  await page.goto(duong(`/notifications/templates/${mau.id}`));

  await expect(page.getByRole("heading", { name: "Soạn version mẫu" })).toBeVisible();

  await chup(page, "26-template-detail.png", mask(page, maskChuoi(page, [mau.code, mau.name])));
});
