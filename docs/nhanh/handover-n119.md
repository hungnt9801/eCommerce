# Handover N119 — WP-17 / hàng `W17-5` (F6 OPS + G6) XONG: đóng WP-17 "giá vốn", `-DayDu` 20/20 · 840,9 s, FF `tich-hop/gd3`

> Phiên `N119` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-5` (loại `dong-wp`, bước `F6-G6`, vai `ops → ORCH`) ·
> WP-17 "giá vốn" · mã `PRC-001` · nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: `ops` viết tài liệu triển khai `docs/ops/PRC-001-deploy.md` có đường lùi; ORCH chạy G6 `kiêm-tat-ca -DayDu`,
> cập nhật `07` §13, đóng hàng, push nhánh WP + **fast-forward** `feat/gd3-wp17-gia-von` → `tich-hop/gd3`.
> **0 `.cs`, 0 migration, 0 `pg_dump`, 0 tệp FE, 0 merge `main`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Chọn hàng + nhánh | ORCH | `PH next` ⇒ `W17-5`; đúng nhánh `feat/gd3-wp17-gia-von` (đã có ở máy), cây sạch; `PH check` **OK**; `PH start` ⇒ **`PHIEN=N119`** |
| **F6 OPS** tài liệu triển khai | `ops` | Tạo mới `docs/ops/PRC-001-deploy.md` (**194 dòng**, 6 mục): phạm vi 1 migration expand-only + 0 FE/0 env/0 CI; §2.1 **4 Setting `Pricing:*`** (engine `GiaVonDongCo` = **`false` — TẮT**); §3 thứ tự `DbMigrator` binary mới (**cấm** `dotnet ef database update`), không có script backfill riêng; **§4 đường lùi bắt buộc**; §5.2 kiểm sau triển khai; §6 ai gọi |
| Đối chiếu code (ops) | `ops` | Xác nhận: worker `AsyncPeriodicBackgroundWorkerBase` nhịp **30 phút hardcode** (`NhipMacDinhPhut`), `RunOnStart=true`, đăng ký `PricingApplicationModule.OnPostApplicationInitializationAsync:29`; khoá theo `pricing:cost-sku:{tenant|null}:{sku}`; mã lỗi mới **`0014`/`0015`**; `0012`/`0013` giữ chỗ `PRC-002`; `GiaVonTheoKho=true` **NÉM `0014`** kể cả khi engine tắt (`CostEngineService.cs:132-135` trước nhánh tắt `:144`); health Host là **`/health-status`** (không phải `/api/health` — BFF) |
| **G6 `-DayDu`** | ORCH | Chạy detached (`chay-g6-n119.ps1`), **exit 0 · 20/20 bước · 840,9 s** (log `artifacts/agents/tu-dong/g6-n119.log`) |
| `07` §13 | ORCH | Thêm **1 dòng** tiến độ ngày 28/11/2026, số đo thật |
| Đóng hàng + FF | ORCH | `PH done` bằng script; push nhánh WP; **fast-forward** → `tich-hop/gd3` (SHA ở §7) |

**Baseline mở phiên (`/mo-phien` §3, WP có code):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `docker compose ps` | 0 | `ecommerce-postgres` + `ecommerce-redis` running (5433/6380) |
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 Error · 9 Warning** (cũ) |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2691/2691** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS · 1053 link · 2022 backtick |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |

**Cổng đo lại cuối phiên (ORCH tự chạy, không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| **G6** | `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **exit 0 · 20/20 bước · 840,9 s** |
| G6 build | (bước 2) | **0 Error / 9 Warning** (0 warning mới) · mốc build 134 tệp |
| G6 unit | (bước 3) | **2691/2691** (giữ nguyên so baseline) |
| G6 integration | (bước 4) | **171/171** |
| G6 mermaid | (bước 5) | **214 khối · 0 lỗi** |
| G6 cổng tài liệu | (bước 6) | **XANH exit 0** — `PRC-001: duyệt GD1` (**nguồn NGƯỜI**) |
| G6 FE | (bước 7–8) | typecheck **0 lỗi** · Vitest **60/60** · `gen:api` **không đổi** `schema.d.ts` |
| G6 DB | (bước 9) | **25/25** bất biến DAT · 0 hỏng |
| G6 test cổng | (bước 10) | **60/60 DAT** · 0 hỏng |
| G6 E2E/probe | (bước 11–20) | E2E web **116,9 s** · ADDR **17/17** · CUS **26/26** · PAY **30/30** · SAL/DLQ · oversell **11/0** · outbox **9/0** · outbox 2 instance **9/0** · giữ chỗ 2 instance **18/0/0** · Playwright **8/8** |
| Link | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** · 334 tệp · 1059 link · 0 chết |
| Hook | `node --test .claude/hooks/test/hooks.test.mjs` | **49/49** |

## 2. Chưa xong / cắt giữa

Không có việc dở của `W17-5`. Các mục dưới là **nợ có nhận chủ**, không chặn DoD:

- 🟠 **`Q4`/`PRC-001-AQ1`** (CostPrice duyệt 2 bước) vẫn `Open` — câu kế toán trưởng; **chặn release**. **`PRC-001-AQ2`** (nhập lùi đụng kỳ đã duyệt: từ chối vs revision) vẫn `Open`.
- 🔴 **M4 trước release**: kế toán duyệt lô `PRC-001` (gồm `Q4`/`AQ1`, đa tiền tệ) + xác nhận `D-325`/`D-326`.
- 🟡 **P2 #2 (REV)**: hai ca integration `K06-08b` "bền vững qua rollback"/"Attempt++" **mô phỏng SQL tay**, không gọi `CostEngineService` ⇒ không canh hồi quy thứ tự 3 pha UoW. Chủ: `qa` khi mở lại `PRC-001`/`K-06`.
- 🟡 **P3 #5/#6/#7** (SRS/tech): bỏ `hopLe` của `DocChinhSachLamTron`; `DateTime.UtcNow` thay `IClock`; lệch mã Transfer SRS §20.4 :1271-1272 vs `CostFeedLookupAdapter.cs:125-128` (tác động hiện tại = 0).
- 🟡 **`PriceList`/`PriceListItem`/`TierPriceSet`** vẫn chặn kỳ một ngày (`<=`), khác `CostPrice` — nợ WP chạm `PRC-002`/bảng giá (`D-326` ⑥).
- 🟠 **Engine mặc định TẮT** (`Pricing:GiaVonDongCo=false`) — bật trên dữ liệu thật là **việc người** (`D-318` C6), chưa backfill; không có script backfill riêng (worker/command replay khi bật).
- 🟡 **Run kẹt `DangChay`**: process chết giữa 3 pha UoW ⇒ `409` tới khi reset; **chưa có lệnh reset** — chủ `be`.
- 🟡 **Worker không lặp tenant** (chạy scope host) — đúng mô hình 1 tenant hiện nay; multi-tenant thật cần command theo tenant (D-xx mới).
- 🟡 **`WEB-001`**: nếu cần 2 endpoint giá vốn qua `/api/v1` thì phải thêm controller v1 (BE), tách WP.

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp | Trả lời bằng cách nào |
|---|---|---|---|---|
| 1 | `Q4`/`PRC-001-AQ1`: `CostPrice` bắt buộc luồng duyệt 2 bước? | Giữ **(a) 2 bước** cho GĐ3 (đã code + test) | Đã hiện thực (a); `Q4` giữ `Open` | Kế toán duyệt lô M4/R3-3 trước release |
| 2 | `PRC-001-AQ2`: nhập lùi đụng kỳ **đã duyệt** có cần revision? | Giữ **(a) từ chối** (`NÉM 0015`); (b) revision = đổi unique = phá huỷ schema | Đã hiện thực (a) | Người quyết nếu muốn (b) |
| 3 | Đa tiền tệ / tiền tệ cơ sở tenant cho giá vốn | **(a) v1 chỉ VND** (`D-324` ③) | v1 VND; `TienTeNhap ≠ VND ⇒ 0014` | Kế toán + kiến trúc khi mở ngoại tệ |
| 4 | `D-325`/`D-326` (TẠM CHỐT DEC) — xác nhận "replay toàn lịch sử" + "kỳ một ngày hợp lệ" | Chấp nhận (đã có test + NC) | Đã code + test + ARCH đồng bộ | Duyệt cùng lô M4/R3-3 |
| 5 | Bật động cơ giá vốn + backfill lịch sử trên dữ liệu thật | Sau khi kế toán duyệt lô `PRC-001` + `pg_dump` mới (M9) | Engine để **TẮT** mặc định; deploy doc §3–§4 chỉ rõ | Người (`D-318` C6 / M4) |
| 6 | Mở PR xếp chồng `tich-hop/gd3` → `main` và merge | Chuỗi `gd3-1` **hết hàng**; mở PR cuối chuỗi rồi người review | Nhánh tích hợp đã nhận FF WP-17 | `chay-tu-dong.ps1 -MoPRCuoi` + người merge (M3; `main` **chỉ người**) |

## 3. Migration đã áp

**Không có migration mới trong phiên.** Migration `20261128052137_Added_Pricing_CostEngine` của `W17-2` đã áp ở `N116`
(sau `pg_dump` **634 674 bytes** → `artifacts/pg-dump/N116-W17-2.dump`, gitignored). Phiên này **0 `pg_dump`, 0 thay đổi schema**;
`DbMigrator` **không** chạy lại. Deploy doc §3 ghi rõ: môi trường mới chạy `dotnet run --project src/eCommerce.DbMigrator`
bằng binary mới rồi xác nhận `__EFMigrationsHistory` có tên migration; **cấm** `dotnet ef database update`.

## 4. Quyết định mới

**Không có `D-xx` mới trong `W17-5`.** Hàng dùng các quyết định đã có: `D-318` (uỷ quyền WP-17, C4/C6/C9), `D-320` (G2 ARCH),
`D-321…D-324` (ánh xạ chuyển động/VND, thiếu giá ⇒ `null`, job+command, mã lỗi giữ chỗ), **`D-325`** (replay toàn lịch sử),
**`D-326`** (kỳ một ngày hợp lệ). `check-agent-artifact -Kind decision` vẫn DAT.

## 5. Nợ / rủi ro

- 🔴 **Trước release (M4/R3-3)**: kế toán duyệt lô `PRC-001` (`Q4`/`AQ1`, đa tiền tệ) + xác nhận `D-325`/`D-326`.
- 🟠 **Engine mặc định TẮT**, chưa backfill — bật là việc người (`D-318` C6); `pg_dump` mới **trước** khi bật (M9).
- 🟠 **R6 chưa chứng minh trên Host thật**: provider Setting nằm trong assembly module `Pricing.Domain`; build/test không chứng minh ABP quét lúc chạy. Kiểm sau triển khai §5.2 deploy doc.
- 🟡 P2 #2 + 3 P3 + `PriceList`/`TierPriceSet` như §2.
- 🟡 Run kẹt `DangChay` chưa có lệnh reset; worker không lặp tenant.
- ⚠️ Mọi thay đổi hành vi của WP-17 **đi kèm engine TẮT** ⇒ triển khai binary + migration **không** làm đổi giá vốn/đơn giá của hệ thống cho tới khi người bật cờ.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-5` → ĐÓNG WP-17; chuỗi `gd3-1` HẾT HÀNG (`PH next` exit 5).**

- Không còn hàng agent nào trong `agents/state/phien-gd3-1.json`.
- Việc còn lại **đều của người**: M4 kế toán duyệt lô `PRC-001` trước release; mở PR xếp chồng `tich-hop/gd3` → `main` (`chay-tu-dong.ps1 -MoPRCuoi`) và merge `main` (chỉ người).
- Nếu mở phiên mới cho WP khác (chuỗi mới / `WEB-001`): dán lệnh dưới với đúng tên chuỗi.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ `5c902ed`.
- Commit sản phẩm/phiên trước (đã push ở `N118`): `fd139f4` (handover n118) cùng chuỗi `W17-2…W17-4` (`54ab64b`, `f2e3337`, `84d4aa9`, `ac4af17`, `e56dee2`).
- Commit phiên này: `docs(nhanh): N119 - W17-5 F6 OPS deploy doc PRC-001 and G6 dong WP-17` (handover + deploy doc + `07` §13 + registry).
- **Push** `origin/feat/gd3-wp17-gia-von`; **fast-forward** → `origin/tich-hop/gd3` (SHA đích ghi ở câu trả lời cuối). **Không** force/merge, **không** mở PR, **không** đụng `main`.

---

**Hàng rào agent** (phiên `N119`): `ops` ×1. ORCH tự chạy toàn bộ cổng G6. `git status` khớp vùng (`docs/ops/**` của `ops`; `docs/nhanh/**` + `agents/state/**` của ORCH). 1 cảnh báo hook là **dương tính giả** đã biết: `ops` thiếu khối JSON có rào ba dấu (khối JSON vẫn được gửi về), `chanDungKhiThieuJson=false` ⇒ không chặn.
