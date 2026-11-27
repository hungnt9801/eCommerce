# Handover phiên N96 — `W24-4` (WP-24 · `PRO-002` engine điều kiện khuyến mãi) · chuỗi `gd3-1`

> Nhánh `feat/gd3-wp24-dieu-kien-km` · base `origin/tich-hop/gd3` @ `84157a1` · bước **F4 QA + F5 REV**.
> Kết quả: **F4 XONG · F5 REV = CHANGES (P1=1)** ⇒ **W24-4 CHƯA xong**, đánh `do-dang`. Không push `tich-hop/gd3`.

## 1. Đã xong

**F4 QA (`qa` ×2 vòng)** — `docs/tests/PRO-002-testplan.md` (mới):
- Phủ **18/20 BR** (`BR-01`…`BR-20`); `TC-70`/`TC-71` + `D-288 Limits` có bảng riêng; **4 negative control đỏ→xanh** (NC-N96-1 rule-catalog `D-289`, NC-N96-2 Limits `D-288`, NC-N96-3 quyền `BR-13`, NC-N96-4 tiền `BR-11`).
- **+13 test unit** (tệp mới `PromotionV1SurfaceTests` TC-70 · `PromotionRuleCatalogTests` TC-71 · `PromotionGetLimitsTests` `D-288`) + **+17 test integration** (tệp mới `PostgreSqlPromotionSchemaTests` phủ DB phần `TC-33/34/42/43`).
- Dựng lại `test/eCommerce.IntegrationTests/Assets/schema.sql` từ **chính migration** `20261126224627_Add_Promotion_Module` (mechan: `dotnet ef migrations script`): **+239 dòng thuần thêm**, **7 bảng `Pro*`**, **9 index `UNIQUE NULLS NOT DISTINCT`**; `PostgreSqlSchemaTests` đếm NND **105 → 114** + thêm 9 tên index vào `phaiCo` (không nới assertion nào khác); `RangBuocChuaAnhXa.txt` +33 dòng **có lý do** cho ràng buộc `Pro*` (hàng rào cuối, cơ chế của chính test).

**F5 REV (`rev` ×2 lượt — lượt 1 chạm trần 30 turn, lượt 2 ghi xong)** — `docs/reviews/PRO-002-review.md`:
- **`VERDICT: CHANGES` · P1=1 · P2=4 · P3=4.** Không sửa code (đúng vai).

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` (baseline) | 0 | 0 error · **9 warning** (55,12 s) |
| `dotnet test test/eCommerce.Tests --no-build` (baseline) | 0 | **2419/2419** |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (baseline) | 0 | PASS — 289 tệp · 976 link · 1740 backtick |
| `dotnet build` (sau) | 0 | 0 error · 9 warning |
| `dotnet test test/eCommerce.Tests --no-build` (sau) | 0 | **2432/2432** (+13) |
| `dotnet test test/eCommerce.IntegrationTests` | 0 | **156/156** (+17) |
| `pwsh -File tools/kiem-tat-ca.ps1` | 0 | **10/10 · 307,7 s** — unit 2432 · integration 156 · mermaid 214/0 · FE typecheck+Vitest 60/60 · `gen:api` không đổi · DB 25/25 · test cổng 60/60 |
| `check-agent-artifact -Kind testplan` | 0 | DAT — `BR trong SRS=20 · chua co trong testplan=0` |
| `check-agent-artifact -Kind review` | 0 | DAT (định dạng) — nhưng **verdict=CHANGES · P1=1** ⇒ **G5 KHÔNG đạt** |
| `check-agent-artifact -Kind decision` | 0 | DAT — 117 dòng |

**Commit đã push** `origin/feat/gd3-wp24-dieu-kien-km`: `33559e5` (F4 tests + schema) → `5d7bab6` (testplan) → `c17d9fe` (review).

## 2. Chưa xong / cắt giữa

**W24-4 chưa đạt DoD** (`F5 REV: VERDICT PASS, P1 = 0` chưa đạt). Không đóng hàng, không push nhánh tích hợp.

Các phát hiện **chặn merge** (chi tiết + `file:dòng` + `graph:` trong `docs/reviews/PRO-002-review.md`):
- 🔴 **P1 #1** — `EfPromotionDataSource.FindVoucherCandidateAsync` (`:32-48`) nạp voucher theo `Code` **bất kể** `Promotion.Status` / `VoucherCode.Status`; `PromotionCandidateData` không có `Status`; engine chỉ kiểm khung giờ + kênh. Trái SRS **§4.2 bước 2(b)**. Đường tới lỗi: dòng `ProVoucherCodes` trỏ promotion `Paused/Cancelled/Draft/Expired` còn trong khung giờ ⇒ `evaluate`/`validate-voucher` trả `Applied=true` + tiền giảm cho khuyến mãi đã tạm dừng/đã huỷ. **Đề nghị:** ánh xạ `Promotion.Status` (+ `VoucherCode.Status`) hoặc lọc fail-closed ngay trong `FindVoucherCandidateAsync` (`Status == Active`, `voucher.Status != Revoked`); thêm test.
- 🟠 **P2 #2** — `modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs:137,144` truyền `Subtotal = Σ ListPrice×Qty` (giá niêm yết) thay vì `tongTruocGiam` của pipeline giá; `Currency = "VND"` hard-code.
- 🟠 **P2 #3** — `PromotionAppService.ValidateVoucherAsync` (`:302-327`): `IsValid = kq.Applied` ⇒ mã rác "hợp lệ" nhờ một khuyến mãi tự động áp; lấy nhầm `PromotionId` từ breakdown.
- 🟠 **P2 #4** — voucher bị từ chối trả **im lặng** thay vì NÉM `0007`/`0119`/`0104` (hết lượt, cây `false`, mã không tồn tại); `lyDoTong` null khi auto áp.
- 🟠 **P2 #5** — thiếu ca HTTP thật `200/401/403` cho 7 route (chỉ reflection — testplan tự nhận).
- 🟡 **P3**: `?sort=` bị bỏ qua; paging in-memory + `CancellationToken.None`; `PromotionDiscountCalculator` default trả `Money.Zero` im lặng; phạm vi > K-04 (module mới, hợp lệ `D-216`).
- **`BR-10`** (áp qua một UoW: `Redemption`+đơn+outbox — nợ `SAL-002`) và **`BR-19`** (COD chưa đối soát ⇒ `0008` — nợ `SHP-003`/`REC-003`) **chưa phủ** — đúng là nợ có chủ, **không phải P1**.

## 3. Migration đã áp

**Không có** — **0 migration, 0 `pg_dump`**. `schema.sql` (test asset) chỉ **sinh lại** từ migration `Add_Promotion_Module` đã áp ở `N94`; không chạm DB thật.

## 4. Quyết định mới

**Không có `D-xx` mới** trong phiên (không gọi `decider`; 4 negative control + testplan đều nằm trong `D-285`…`D-290`). `check-agent-artifact -Kind decision` DAT (117 dòng). Lô `D-277`…`D-290` vẫn **chờ người duyệt**.

## 5. Nợ / rủi ro

- 🔴 **P1 #1 voucher status guard** — chặn merge, chủ sửa **`be`** (Promotions). Xem §2.
- 🟠 **P2 #2/#3/#4** chạm **tiền** ⇒ theo luật `rev` vẫn buộc **CHANGES** kể cả khi P1 đã sửa; chủ sửa `be` (Promotions + Sales).
- 🟠 **P2 #5** thiếu probe HTTP thật — chủ `qa`/ORCH (khi Host sống).
- 🟡 **`D-283`** (4/9 metric `ChuaCoNoiPhat`), **`D-284`** (chưa nối `IIdempotencyGuard` cho `SaveTree`/`Import`) — nợ từ `N94`, không đổi.
- 🟡 33 ràng buộc `Pro*` trong `RangBuocChuaAnhXa.txt` — cơ chế của test; `be` có thể chuyển sang `ViPhamRangBuocDb` nếu cần mã lỗi riêng.
- 🟡 **`pnpm build` EPERM** + **`pnpm lint` no-script** (`N70`) — môi trường/nợ cũ; `kiem-tat-ca` vẫn 0.
- **Chờ người:** (a) duyệt lô `D-277`…`D-290`; (b) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp; (c) kế toán duyệt lô M4 trước release; (d) merge `main`; **(e) MỚI — chấp thuận một lượt sửa lỗi `be`+`qa`+`rev` cho `PRO-002` trước `W24-5`** (xem §6; thêm việc ngoài lộ trình nên cần người).

## 6. Việc đầu tiên của phiên sau

**DỞ DANG `W24-4` → mở lại `W24-4`: sửa P1 + P2 rồi REV vòng 2 (chưa chạy `W24-5` dong-wp).**

- **Cần chấp thuận phạm vi:** sửa P1 + 3 P2 chạm **> 5 tệp sản phẩm** (`Promotions` + `Sales`) ngoài vai `qa → rev` của hàng ⇒ phải thêm một lượt `be` (và `qa` bổ sung test) vào hàng/lộ trình. Theo `CLAUDE.md` §5 đây là "thêm việc ngoài lộ trình" ⇒ **người/decider chốt** trước khi làm.
- Thứ tự đề nghị: `be` sửa **P1 #1** (guard `Status` ở đường voucher) + **P2 #4** (NÉM thay im lặng) + **P3 #3** (`ValidateVoucherAsync`), `be` sửa **P2 #2** (Subtotal/Currency ở `Sales`); `qa` thêm test (voucher của promotion `Paused/Cancelled` ⇒ không áp; `ValidateVoucherAsync` mã rác + auto ⇒ `IsValid=false`); `rev` **REV vòng 2** phải ra **PASS · P1=0**.
- Đọc trước: `docs/reviews/PRO-002-review.md` (§Phát hiện #1…#5), SRS §4.2 bước 2(b)/§6.1 quy tắc 8–9, `docs/tests/PRO-002-testplan.md` §5.
- Sau khi PASS mới mở `W24-5` (`-DayDu` + push `tich-hop/gd3`).

```
/chay-phien-code chuoi=gd3-1
```

> ⚠️ Lệnh trên sẽ `PH next` = `W24-4` (do `do-dang`); cần bổ sung **lượt `be` sửa lỗi** trước khi chạy lại F5 — hàng hiện khai `vai: qa → rev` nên chưa phủ bước sửa code.

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp24-dieu-kien-km`; base `origin/tich-hop/gd3` @ `84157a1`.
- Commit phiên: **`33559e5`** (F4 tests + schema) → **`5d7bab6`** (testplan) → **`c17d9fe`** (REV), cả ba **đã push** `origin/<nhánh>`.
- **Không** push `tich-hop/gd3` (chỉ `dong-wp` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N96`): `qa` ×2 (vòng 1 tests+schema, vòng 2 testplan+4 NC) · `rev` ×2 (**lượt 1 chạm trần 30 turn** trước khi ghi; lượt 2 ghi `PRO-002-review.md`). **0 lần chặn dừng.**
Cảnh báo hook: **"thiếu khối JSON"** ghi cho `qa` ×2 + `rev` ×1 — **báo nhầm** (cả ba báo cáo đều có khối JSON; đường `SendMessage`/`SubagentHandBack` làm hook không thấy). `artifacts/agents/canh-bao.jsonl` ghi `ngoaiVung=[]` cho mọi vai ⇒ **0 tệp lọt vùng**.
