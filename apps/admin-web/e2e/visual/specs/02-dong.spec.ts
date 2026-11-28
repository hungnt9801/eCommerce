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
  // 📌 Terminal tự mở **một tab hoá đơn nháp** sau khi có ca (fixture mở ca bằng ca thật của tenant) ⇒
  // màn hiện tab trống "Chưa có món nào…", KHÔNG phải trạng thái "Chưa có tab". Khẳng định nút mở tab +
  // dòng bảng trống — có mặt bất kể dữ liệu lượt chạy, chứng minh terminal đã render (ca không rỗng).
  await expect(page.getByRole("button", { name: /\+ HĐ mới/ })).toBeVisible();
  await expect(page.getByText("Chưa có món nào")).toBeVisible();

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
      // 🔴 Fixture là **entity MỚI mỗi lượt** (số phiếu, ngày tạo, SKU, kho, tồn) ⇒ nội dung thẻ + bảng
      // biến động. Che breadcrumb + dòng tiêu đề/trạng thái + thẻ thông tin + bảng + mục tồn hiện tại
      // (giữ chrome/nút ngoài các vùng này) — F-02 P2 chấp nhận.
      page.locator("main .flex.items-center.gap-2").first(),
      page.locator("main .flex.items-center.gap-3").first(),
      page.locator("main .nh-card"),
      page.locator("main table"),
      page.locator("main section", { hasText: "Tồn kho hiện tại" }),
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
      // Dòng "Số: <số> · Ngày <thời điểm> · <trạng thái>" — phần tử ghép, che cả khối tiêu đề giữa.
      page.locator("section .text-center > div").nth(1),
      // Ô "Kho: <mã> — <tên>" (kho fixture mỗi lượt) — con đầu của lưới thông tin.
      page.locator("section > div.grid > div").first(),
      // "<số> phiếu · mẫu in cố định …" trên thanh công cụ.
      page.getByText(/phiếu · mẫu in cố định/),
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
    mask(page, [
      // Khách là fixture mới mỗi lượt: tên/SĐT/ngày tạo/điểm… biến động. Che dòng tiêu đề + thẻ thông tin.
      page.locator("main .flex.flex-wrap.items-center.gap-2").first(),
      page.locator("main .nh-card"),
      page.locator("main h1"),
      ...maskChuoi(page, [khach.name, khach.phone]),
    ]),
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
    mask(page, [
      // Thẻ thông tin (mã/ngày bắt đầu–kết thúc/trần lượt) là dữ liệu fixture mỗi lượt.
      page.locator("main .nh-card").first(),
      ...maskChuoi(page, [km.code, km.name]),
    ]),
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
