/**
 * **Báo cáo nền a11y — axe-core trên CẢ 27 route** (`UI-41` / `U41-3`, `D-331`, `D-333`).
 *
 * 🔑 Mục đích (không phải cổng chặn): đo **hiện trạng** a11y của giao diện hiện tại, đếm vi phạm theo
 * mức `critical|serious|moderate|minor`, ghi ra `e2e/visual/.run/axe-summary.json` để báo cáo
 * `UI-41-a11y.md` dùng lại (chỉ P1 do WP-41 gây ra mới chặn — xem `bang-phien-ui-1.md` `U41-3`).
 *
 * 🔒 KHÔNG cài gói mới: `axe-core` là devDependency sẵn có của `packages/ui`; `support/axe.ts`
 * `duongAxe()` giải đường dẫn và ta inject bằng `page.addScriptTag({ path })` — đúng bản đã khoá trong
 * `pnpm-lock.yaml`.
 *
 * 📌 Cùng 27 route với `01-tinh.spec.ts` (19) + `02-dong.spec.ts` (8). Route động dùng **fixture API thật**
 * (`support/fixtures.ts`); route nào không dựng được dữ liệu ⇒ `test.skip` + ghi lý do vào `boQua` — KHÔNG
 * quét một trang lỗi rồi báo số liệu giả.
 *
 * 📌 Vi phạm axe **không** làm đỏ ca này: đây là **bản đồ nợ a11y**, không phải hợp đồng "0 vi phạm". Ca vẫn
 * có thể đỏ khi axe **không chạy được** (inject hỏng, `page.evaluate` trả rỗng) — đó mới là lỗi harness.
 */
import fs from "node:fs";
import path from "node:path";
import { expect, test, type Page } from "@playwright/test";
import { dangNhap, duong } from "../../support/ui";
import { type Fixture, duongPos, taoFixture } from "../support/fixtures";
import { type MucAxe, duongAxe, tomTatAxe } from "../support/axe";

type ViPham = { id: string; impact: string | null; help: string; nodes: unknown[] };
type KqRoute = {
  slug: string;
  url: string;
  dem: Record<MucAxe, number>;
  viol: Array<{ id: string; impact: string | null; help: string; nodes: number }>;
};
type BoQua = { slug: string; url: string | null; lyDo: string };

const TEP_KQ = path.resolve(__dirname, "..", ".run", "axe-summary.json");

const ketQua: KqRoute[] = [];
const boQua: BoQua[] = [];

/** Gọi `axe.run` trong trang đã inject `axe.min.js`; trả vi phạm thô để `tomTatAxe` đếm. */
async function quetAxe(page: Page): Promise<ReturnType<typeof tomTatAxe>> {
  await page.addScriptTag({ path: duongAxe() });

  const tho = await page.evaluate(async (): Promise<ViPham[]> => {
    const axe = (window as unknown as { axe?: { run: (ctx: Document, o: unknown) => Promise<{ violations: ViPham[] }> } })
      .axe;

    if (!axe) {
      throw new Error("axe chưa được inject vào trang — addScriptTag thất bại.");
    }

    const r = await axe.run(document, {
      resultTypes: ["violations"],
      runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"] },
    });

    return r.violations.map((v) => ({
      id: v.id,
      impact: v.impact,
      help: v.help,
      nodes: v.nodes.map((n: unknown) => String((n as { target?: unknown }).target ?? "")),
    }));
  });

  // Harness tự kiểm: axe PHẢI trả một mảng (kể cả rỗng) — nếu `undefined` thì inject/`evaluate` hỏng.
  expect(Array.isArray(tho), "axe.run phải trả mảng violations").toBe(true);
  return tomTatAxe(tho);
}

/** Đi + quét một route đã có phiên (trừ `/login`). Ghi kết quả vào `ketQua`. */
async function quet(page: Page, slug: string, url: string, coPhien: boolean): Promise<void> {
  if (coPhien) {
    await dangNhap(page);
  }

  await page.goto(url);
  // Chờ mềm: POS là client component tự gọi API sau mount; polling/SSE không bao giờ "idle".
  await page.waitForLoadState("networkidle").catch(() => {});
  await page.locator("body").waitFor({ state: "visible" });

  const { dem, viol } = await quetAxe(page);
  ketQua.push({ slug, url, dem, viol });
}

// ─────────────────────────── 19 route TĨNH ───────────────────────────
// Cùng danh sách `01-tinh.spec.ts` (bỏ #1 `/` vì chỉ redirect).
const TINH: Array<{ slug: string; url: string; coPhien: boolean }> = [
  { slug: "02-login", url: "/login", coPhien: false },
  { slug: "03-dashboard", url: duong(""), coPhien: true },
  { slug: "04-product-item", url: duong("/product/item"), coPhien: true },
  { slug: "05-product-inventory", url: duong("/product/inventory"), coPhien: true },
  { slug: "06-order-manage", url: duong("/order/manage"), coPhien: true },
  { slug: "08-inventory-bill", url: duong("/inventory/bill"), coPhien: true },
  { slug: "09-inventory-bill-new", url: duong("/inventory/bill/new"), coPhien: true },
  { slug: "13-pos-bill-list", url: duong("/pos/bill/list"), coPhien: true },
  { slug: "14-pos-return", url: duong("/pos/return"), coPhien: true },
  { slug: "16-customer-list", url: duong("/customer/list"), coPhien: true },
  { slug: "18-settings-staff", url: duong("/settings/staff"), coPhien: true },
  { slug: "19-report-revenue", url: duong("/report/revenue"), coPhien: true },
  { slug: "20-report-inventory", url: duong("/report/inventory"), coPhien: true },
  { slug: "21-promotion", url: duong("/promotion"), coPhien: true },
  { slug: "22-promotion-new", url: duong("/promotion/new"), coPhien: true },
  { slug: "24-templates", url: duong("/notifications/templates"), coPhien: true },
  { slug: "25-templates-new", url: duong("/notifications/templates/new"), coPhien: true },
  { slug: "27-notifications-queue", url: duong("/notifications/queue"), coPhien: true },
  { slug: "28-notifications-batches", url: duong("/notifications/batches"), coPhien: true },
];

for (const r of TINH) {
  test(`axe · ${r.slug}`, async ({ page }) => {
    await quet(page, r.slug, r.url, r.coPhien);
  });
}

// ─────────────────────────── 8 route ĐỘNG ───────────────────────────
// Fixture API thật — cùng nguồn `02-dong.spec.ts`. Đơn hàng dùng ngoại lệ SQL duy nhất `D-49`.
let fixture: Fixture;

test.beforeAll(async () => {
  fixture = await taoFixture();
});

/** Một route động: dựng URL từ fixture; thiếu dữ liệu ⇒ skip + ghi `boQua` (không quét trang lỗi). */
function dong(slug: string, dungUrl: (f: Fixture) => string | null, lyDoThieu: string): void {
  test(`axe · ${slug}`, async ({ page }) => {
    const url = fixture ? dungUrl(fixture) : null;

    if (!url) {
      const lyDo = fixture?.loi[slug] ?? lyDoThieu;
      boQua.push({ slug, url: null, lyDo });
      test.skip(true, lyDo);
      return;
    }

    await quet(page, slug, url, true);
  });
}

dong("12-pos-bill", (f) => duongPos(f), "không dựng được kho/ca cho POS");
dong(
  "07-order-detail",
  (f) => (f.order ? duong(`/order/manage/${f.order.id}`) : null),
  "không dựng được đơn seed (D-49)",
);
dong(
  "10-inventory-bill-detail",
  (f) => (f.stockBill ? duong(`/inventory/bill/${f.stockBill.id}`) : null),
  "không dựng được phiếu kho",
);
dong(
  "11-inventory-bill-print",
  (f) => (f.stockBill ? duong(`/inventory/bill/print?ids=${f.stockBill.id}`) : null),
  "không dựng được phiếu kho",
);
dong(
  "15-print-bill",
  (f) => (f.retailBill ? duong(`/print/bill/${f.retailBill.id}`) : null),
  "không dựng được hoá đơn bán lẻ",
);
dong(
  "17-customer-detail",
  (f) => (f.customer ? duong(`/customer/${f.customer.id}`) : null),
  "không dựng được khách hàng",
);
dong(
  "23-promotion-detail",
  (f) => (f.promotion ? duong(`/promotion/${f.promotion.id}`) : null),
  "không dựng được khuyến mãi",
);
dong(
  "26-template-detail",
  (f) => (f.template ? duong(`/notifications/templates/${f.template.id}`) : null),
  "không dựng được mẫu thông báo",
);

// ─────────────────────────── Ghi báo cáo ───────────────────────────
test.afterAll(() => {
  const tong: Record<MucAxe, number> = { critical: 0, serious: 0, moderate: 0, minor: 0 };
  let soViPham = 0;

  for (const r of ketQua) {
    for (const muc of Object.keys(tong) as MucAxe[]) {
      tong[muc] += r.dem[muc];
    }
    soViPham += r.viol.length;
  }

  const baoCao = {
    taoLuc: new Date().toISOString(),
    phamVi: "27 route (19 tĩnh + 8 động)",
    routes: ketQua,
    boQua,
    tong: { dem: tong, soViPham, soRouteQuet: ketQua.length, soRouteBoQua: boQua.length },
  };

  fs.mkdirSync(path.dirname(TEP_KQ), { recursive: true });
  fs.writeFileSync(TEP_KQ, `${JSON.stringify(baoCao, null, 2)}\n`, "utf8");
  console.log(`[axe] đã quét ${ketQua.length} route · bỏ qua ${boQua.length} · ghi ${TEP_KQ}`);
  console.log(`[axe] TỔNG: ${JSON.stringify(tong)} · vi phạm=${soViPham}`);
});
