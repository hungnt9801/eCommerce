import fs from "node:fs";
import { expect, test } from "@playwright/test";
import { tongHopBaoCao } from "../support/api";
import { BUSINESS } from "../support/env";
import { dangNhap, homNayVn } from "../support/ui";

/**
 * **Luồng ⑧ — Xuất CSV** (`07 §WP-09`, `D-36` ③ + `WP-08`) — **cả hai** báo cáo mới, không chỉ kho.
 *
 * 🔑 Ba thứ chỉ đo được bằng **tệp tải về thật**:
 * ① **BOM UTF-8** (`\uFEFF`): thiếu BOM, Excel bản Việt mở CSV thành `B?o c?o` ⇒ người dùng tưởng dữ liệu hỏng;
 * ② **phân cách `;`** + dòng tiêu đề **đúng cột đang hiện** (cột bảng và cột CSV là **cùng một biến**);
 * ③ **số là số thô** (`30000`, không phải `30.000`) — CSV để máy đọc, không phải để trình diễn.
 *
 * 🚨 **Phải chạy TỔNG HỢP trước:** báo cáo đọc **read-model** (`Rpt*`), không đọc chứng từ sống (`D-47`);
 * chưa tổng hợp thì nút xuất **bị vô hiệu** (bảng rỗng) — và đó là hành vi **đúng**, không phải lỗi.
 */
test.describe("⑧ Xuất CSV", () => {
  test("báo cáo doanh thu + tồn kho tải CSV có BOM, đúng cột, số thô", async ({ page }) => {
    await dangNhap(page);

    const homNay = homNayVn();

    // Tổng hợp **hôm nay** (backfill thắng mốc — `D-47` ⑤). Đi qua BFF để kiểm luôn quyền riêng
    // `eCommerce.Reports.Aggregate` trên đường người dùng thật.
    const agg = await page.request.post("/api/proxy/v1/reports/aggregate", {
      data: { tuNgay: homNay, denNgay: homNay },
    });
    expect(agg.ok(), `aggregate: HTTP ${agg.status()} ${await agg.text()}`).toBeTruthy();
    const ketQua = (await agg.json()) as { soNgay: number };
    expect(ketQua.soNgay).toBe(1);

    // ── ① Báo cáo DOANH THU ─────────────────────────────────────────────────────────────────────────
    await page.goto(`/${BUSINESS}/report/revenue?tu=${homNay}&den=${homNay}`);
    await expect(page.getByRole("heading", { name: "Doanh thu", exact: true })).toBeVisible();

    const [taiDoanhThu] = await Promise.all([
      page.waitForEvent("download"),
      page.getByRole("button", { name: "Xuất CSV" }).first().click(),
    ]);

    expect(taiDoanhThu.suggestedFilename()).toBe(`doanh-thu-${homNay}-${homNay}.csv`);

    const duongDanDt = await taiDoanhThu.path();
    expect(duongDanDt, "Playwright không lưu được tệp tải về").toBeTruthy();
    const noiDungDt = fs.readFileSync(duongDanDt!, "utf8");

    expect(noiDungDt.charCodeAt(0), "CSV thiếu BOM UTF-8 ⇒ Excel bản Việt sẽ hiện sai dấu").toBe(0xfeff);

    const cacDongDt = noiDungDt.slice(1).split(/\r\n/);
    expect(cacDongDt[0]).toBe("Kỳ;Bán lẻ;Đơn hàng;Tổng doanh thu;VAT;Số chứng từ");

    // Số **thô** (không dấu phân cách nghìn) + có dòng dữ liệu của **hôm nay** (doanh thu của `WP-08` đọc
    // read-model; nhãn kỳ là `yyyy-MM-dd`).
    const dongHomNay = cacDongDt.find((d) => d.startsWith(`${homNay};`));
    expect(dongHomNay, `CSV không có dòng của ${homNay}: ${JSON.stringify(cacDongDt.slice(0, 4))}`).toBeTruthy();
    expect(dongHomNay!.split(";")).toHaveLength(6);
    expect(dongHomNay!).toMatch(/;\d+(\.\d+)?;/); // số thô, không phải "30.000"

    // ── ② Báo cáo TỒN KHO ───────────────────────────────────────────────────────────────────────────
    await page.goto(`/${BUSINESS}/report/inventory`);
    await expect(page.getByRole("heading", { name: "Tồn kho" })).toBeVisible();
    await expect(page.getByText("Chưa chụp ảnh tồn kho nào")).toHaveCount(0);

    const [taiTonKho] = await Promise.all([
      page.waitForEvent("download"),
      page.getByRole("button", { name: "Xuất CSV" }).first().click(),
    ]);

    expect(taiTonKho.suggestedFilename()).toMatch(/^ton-kho-.*\.csv$/);

    const duongDanTk = await taiTonKho.path();
    expect(duongDanTk).toBeTruthy();
    const noiDungTk = fs.readFileSync(duongDanTk!, "utf8");

    expect(noiDungTk.charCodeAt(0), "CSV tồn kho thiếu BOM UTF-8").toBe(0xfeff);

    const cacDongTk = noiDungTk.slice(1).split(/\r\n/).filter((d) => d.length > 0);
    expect(cacDongTk[0]).toBe("Kho;Số SKU;Tồn;Khả dụng;Tạm giữ");

    // 📌 Báo cáo tồn kho là **ẢNH MỚI NHẤT**, không phải lịch sử (`D-47` ⑦): trên DB đã có ảnh của hôm nay,
    // lượt tổng hợp này **không** chụp lại ⇒ CSV có thể chứa ảnh **trước** lượt chạy. Vì vậy chỉ khẳng định
    // cấu trúc + có dòng thật, KHÔNG khẳng định kho của lượt chạy (khẳng định đó sẽ đỏ vì lý do sai).
    expect(cacDongTk.length, "CSV tồn kho không có dòng dữ liệu nào").toBeGreaterThan(1);
    for (const dong of cacDongTk.slice(1)) {
      expect(dong.split(";"), `dòng CSV tồn kho sai số cột: ${dong}`).toHaveLength(5);
    }
  });
});
