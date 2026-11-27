# Handover N103 — WP-25 / hàng `W25-4` (F4 QA → F5 REV) XONG — **mở `W25-5`**

> Phiên `N103` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W25-4` · WP-25 · mã `PRO-001` · module `Promotions` + `Sales` ·
> nhánh `feat/gd3-wp25-voucher` (base `origin/tich-hop/gd3` @ `e7aa32c`) · 28/11/2026.
> Phạm vi: **F4 QA** (testplan 26/26 BR, negative control đỏ→xanh, probe HTTP thật) → **F5 REV** (`VERDICT: PASS`, P1=0) ·
> kèm `ba` sửa SRS §6.2 (`D-307` ①(5)(b)), `be` vá P2 #1 (`D-309`), `arch` v0.7 (`D-310`).

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Sửa SRS §6.2 theo as-built | `ba` | `pro-001-voucher-coupon.md` **v0.13**: bảng REST endpoint → `/api/v1/voucher/**` **đúng 7** route (thêm `GET /{id}`), permission theo `D-299` ①(1); thêm dòng giữ auto `/api/app/voucher-admin/**`; sửa dòng 📌 "ABP tự sinh"; §10 thêm 1 dòng màn voucher thuộc `WEB-001` + hợp đồng đã tái sinh `D-308`. |
| F4 QA — surface | `qa` | `test/eCommerce.Tests/Promotions/VoucherV1SurfaceTests.cs` (8 Fact): `[Route] api/v1/voucher`, `[Authorize]` class `Promotions.Default`, **đúng 7** route, map method→permission, 0 `[AllowAnonymous]`, 0 route `reserve`/`commit`, allowlist 7 method `IVoucherAdminAppService`. |
| F4 QA — hợp đồng OpenAPI | `qa` | `VoucherOpenApiContractTests.cs` (4 Fact): đọc `packages/api-client/openapi.v1.json` — **đúng 7** path voucher, khớp reflection `VoucherController`, không có `reserve`/`commit`. |
| F4 QA — BR bổ sung | `qa` | `VoucherDomainRulesTests.cs` (8 Fact): `BR-09` enum suy ra, `BR-11` append-only (reflection + quét 0 `DELETE`), `BR-13` quét `autoSave: true` (allowlist nợ cũ), `BR-22` quét log PII/seed. |
| F4 QA — integration | `qa` | `test/eCommerce.IntegrationTests/PostgreSqlVoucherWp25Tests.cs` (6 Fact): `BR-06` rollback cùng UoW (lượt vẫn `Reserved`, bộ đếm không đổi), `UsageSequence` `23505` + `SAVEPOINT` chống `25P02`, guard `Status=0` 0 dòng. |
| F4 QA — probe HTTP | `qa` | Tệp mới `tools/probe-voucher-wp25.ps1`: **6/6 PASS** — T01–T03 ẩn danh ⇒ **401**, T04a `View` ⇒ **200**, T04b `View` + generate ⇒ **403**, T05 admin ⇒ **200**; user/role probe dọn sạch. |
| F4 QA — testplan + NC | `qa` | `docs/tests/PRO-001-testplan.md`: **26/26 BR** có test; **NC-1** (hạ quyền) `exit 1 → 0`, **NC-2** (đảo guard Σ) `exit 1 → 0`, **NC-3b** (biên `<`→`<=` trên pre-check `D-309`) `exit 1 → 0`; NC-3 = surrogate ghi rõ. |
| F5 REV | `rev` | `docs/reviews/PRO-001-review.md` **vòng 2: `VERDICT: PASS`, P1=0, P2=3, P3=4**. Vòng 1 P2 #1 (treo vô hạn) ⇒ vá `D-309`; P3 #5 (`F-1`) ⇒ đóng bằng `D-310`. |
| Vá P2 #1 | `be` | `VoucherAdminAppService.cs:133-152`: pre-check `codespace = 36^(Length−len(Prefix))` (nhân có trần, không tràn `long`), thiếu ⇒ `0107` + `WithData(length/prefix/codespace/quantity)`, **trước** `InsertAsync`; `codespace == Quantity` không ném; không đổi mã/chữ ký/route. |
| Sửa arch theo `D-310` | `arch` | `PRO-001-arch.md` **v0.7**: §6.4 metric **chỉ** khi `daNha > 0` (0 dòng = `LogDebug`, không metric); §1.1/§8 as-built (3 metric voucher đã có điểm phát, chỉ còn nợ `promotion_applied_total`/`SAL-001`); §6.1b đặc tả thứ tự guard + pre-check `D-309`; §13/§15. |

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở) | 0 | **0 error · 9 warning** · 83,1 s |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở) | 0 | **2593/2593** |
| Baseline `kiem-link-tai-lieu.ps1` (mở) | 0 | **PASS** · 998 link · 1835 backtick |
| Baseline `check:mermaid` (mở) | 0 | **214 khối · 0 lỗi** |
| Baseline `hooks.test.mjs` (mở) | 0 | **49/49** |
| Baseline FE `typecheck` + `test` (mở) | 0 | 0 lỗi · **60/60** |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-25` (mở) | 0 | **XANH** |
| `dotnet build` (giữa phiên, sau vá) | 0 | **0 error · 9 warning** |
| `dotnet test test/eCommerce.Tests --no-build` (giữa phiên) | 0 | **2619/2619** (+26 so nền) |
| `dotnet test test/eCommerce.IntegrationTests` | 0 | **162/162** (+6) |
| `check-agent-artifact -Kind testplan` | 0 | **DAT** · BR chưa có trong testplan = 0 |
| `check-agent-artifact -Kind review` | 0 | **DAT** · verdict=PASS · P1=0 |
| `check-agent-artifact -Kind decision` | 0 | **DAT** · 117 dòng |
| `pwsh -File tools/probe-voucher-wp25.ps1` (QA chạy) | 0 | **6/6 PASS** (401×3 · 200 · 403 · 200) |
| **Đo lại** `pwsh -File tools/kiem-tat-ca.ps1` (ORCH, đóng) | 0 | **10/10 ĐẠT · 297,1 s** — build 0 err/9 warn · unit **2619/2619** · integration **162/162** · mermaid 214/0 · FE typecheck+Vitest 0 lỗi/**60/60** · `gen:api` drift **0** (82/82) · DB **25/25** · test cổng **60/60** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng) | 0 | **PASS** · 998 link · 1841 backtick |

## 2. Chưa xong / cắt giữa

**Không có việc code dở của `W25-4`.** Các mục còn lại là **nợ có chủ** hoặc **bị chặn ngoài phiên**:

1. **P2 #2 (`D-311`)** — `VoucherRedemptionService.ThucHienCommitAsync:439-528`: nhánh chốt **đầu tiên** (UPDATE `Status=1`, so Σ, `Reserved→Used`, `23505→0115`) **không có test nào thực thi** (unit `GuardGia` không gọi `xuLy`; integration chỉ chép SQL; NC-2 chỉ phủ replay). Chủ **`qa`** (+ `be` nếu cần seam test), mốc **trước `WEB-001`**.
2. **P2 #3 (`D-311`)** — `DonHangAppService.cs:118-132`: guard BR-06 **một chiều** — đơn có giảm giá mà thiếu `VoucherRedemptionId` không bị chặn ⇒ lượt vẫn `Reserved`, mã dùng lại được. Chưa lộ vì FE voucher (`WEB-001`) chưa có. Chủ **`be` (Sales)**, mốc **trước `WEB-001`**.
3. **P2 #4 (pre-existing)** — `PromotionAppService.cs` **7** chỗ + `PromotionRuleCostSeedContributor.cs` **3** chỗ `autoSave: true` (trái `BR-13`/NFR-09), code `PRO-002`/`W24`. QA khoá bằng allowlist (lỗi **mới** vẫn đỏ). Chủ **`be`/`PRO-002`**.
4. **P3 #9** — ca chống treo của `D-309` chỉ chặn ở **mức test** (`Task.WaitAsync`); gỡ hẳn guard vẫn treo tiến trình (đã quan sát >600 s khi NC chạy sai đột biến). Cần chạy ca trong process riêng hoặc `be` thêm trần lặp/`CancellationToken`. Chủ **`qa`/`be`**, trước `WEB-001`.
5. **P3 #6/#7/#8** — comment `csproj` lỗi thời (Messaging/Idempotency); worker nạp toàn bộ `Reserved` vào RAM; migration **chưa áp** nên `Assets/schema.sql` thiếu 2 `CHECK` ⇒ `BR-09` DB-level chưa phủ. Không chặn.
6. **Màn voucher** — chủ WP `WEB-001` (`D-247`/`D-307`), **chưa có lịch**; hợp đồng đã sẵn (`D-308`).
7. **Arch §1.1 còn vài dòng as-built N100 cũ** (`:65-69`, `:72`: service/state machine/mã lỗi/permission/job "❌ chưa có") — `arch` ghi nhận ngoài phạm vi phiên; chủ `arch`, không chặn (P3).

## 3. Migration đã áp

**Không có migration mới trong phiên này; không áp migration nào.**

- `20261127153549_Add_Voucher_Status_Check` (EF sinh ở `W25-2`) vẫn **CHƯA ÁP** DB dev (`__EFMigrationsHistory` chưa có).
  `pg_dump` trước đó: `artifacts/pg-dump/N101-W25-2.dump` — **576.910 byte**.
- Người chạy ngoài auto mode: `dotnet run --project src/eCommerce.DbMigrator`, rồi kiểm `__EFMigrationsHistory`.
- Lùi: 2 `DropCheckConstraint` (`CK_ProVoucherCodes_Status`, `CK_ProPromotionRedemptions_Status`); 0 cột/index/dữ liệu.

## 4. Quyết định mới

`decider` ghi **3 dòng** `TẠM CHỐT (DEC, uỷ quyền)`, `check-agent-artifact -Kind decision` **DAT**:

| D-xx | Nội dung (ngắn) | Phạm vi ảnh hưởng |
|---|---|---|
| `D-309` | P2 #1 sửa **ngay trong `W25-4`**: pre-check `36^(Length−len(Prefix)) < Quantity` ⇒ NÉM `0107` (giữ số) + `WithData`; không thêm mã mới | `VoucherAdminAppService` + testplan + arch §6.1b |
| `D-310` | `F-1`: sửa **câu chữ** arch §6.4 (giữ code) — metric **chỉ** khi `daNha > 0`; **không** thêm counter `skipped` | `PRO-001-arch.md` v0.7 |
| `D-311` | P2 #2/#3 giao **nợ có chủ** + mốc **trước `WEB-001`**; thêm dòng hàng đợi xem lại | testplan §5 + `hang-doi-xem-lai.md` + `WEB-001` |

Hàng đợi xem lại: `docs/agents/hang-doi-xem-lai.md` thêm `D-309`, `D-310`, `D-311`.

## 5. Nợ / rủi ro

- 🔴 **Migration chưa áp** — chủ **người**, như `N101`/`N102`.
- 🟠 **P2 #2/#3 (`D-311`)** — nhánh chốt đầu tiên **chưa có test** và guard BR-06 **một chiều**; **cổng cứng trước `WEB-001`**. Đây là hai chỗ chạm tiền/lượt nên phải đóng trước khi có FE voucher.
- 🟡 **P3 #9** — cơ chế chống treo `D-309` chỉ chứng minh được ở mức test; nếu guard bị gỡ, tiến trình test treo tới khi bị kill (đã xảy ra một lần trong phiên: NC chạy đột biến gỡ guard ⇒ `exit 143`, tệp nguồn phải `git restore`).
- 🟡 **P2 #4 `autoSave: true`** (10 chỗ, `PRO-002`/W24) — pre-existing, trái `BR-13`; chủ `be`.
- 🟡 **Nợ `PRO-002`**: `PromotionExpiryWorker` cũ chưa đọc cờ `Promotions:Enabled`; `promotion_applied_total` còn trong `ChuaCoNoiPhat` (`SAL-001`).
- 🟡 **Môi trường**: `pnpm build` EPERM symlink standalone (`K-05`/`N95`); `pnpm lint` không có script (`N70`).
- 🟡 **Arch §1.1** còn dòng as-built N100 cũ (P3, chủ `arch`).
- **Chờ người** (gom một lần ở câu trả lời cuối): (a) **áp migration** bằng DbMigrator ngoài auto mode; (b) duyệt lô `D-277…D-311`; (c) kế toán duyệt lô M4 (`D-143`/`D-148`/`D-149`/`D-152`/`D-158`) trước release; (d) xếp lịch WP `WEB-001` (màn voucher) — `D-247`/`D-307`; (e) merge `main` + PR nháp `tich-hop/gd3 → main` khi chuỗi xong; (f) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W25-4` → mở `W25-5`.**

- Hàng kế = **`W25-5`** (loại `dong-wp`, bước **F6 OPS + G6**), vai **`ops` · ORCH**.
- G6 = `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (**19 bước**; **không** chạy song song build/test khác) → xanh thì
  tài liệu triển khai `PRO-001` trong `docs/ops/` (có đường lùi) + `07` §13 một dòng, rồi **push fast-forward** nhánh WP lên `tich-hop/gd3`
  (`git push origin feat/gd3-wp25-voucher:tich-hop/gd3`; bị từ chối ⇒ **không** force, `PH dodang`).
- Đọc trước: `D-309`/`D-310`/`D-311`, `docs/tests/PRO-001-testplan.md` §5, `docs/reviews/PRO-001-review.md` (vòng 2), handover này.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-25
và handover gần nhất `docs/nhanh/handover-n103.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng W25-5** (phiên N104). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

Một dòng cho chuỗi: `/chay-phien-code chuoi=gd3-1`

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp25-voucher`; base `origin/tich-hop/gd3` @ `e7aa32c`.
- Commit phiên: `514c252` (SRS §6.2) · `27538f8` (F4 QA) · `b3cb947` (vá `D-309`) · `c16824e` (arch v0.7) ·
  `696ce46` (test `D-309`) · `6f618f5` (NC-3b) · `e18d341` (review vòng 2) · `docs(nhanh)` (handover + registry) — xem `git log -1`.
- **Đã push nhánh WP**; **chưa** đẩy `tich-hop/gd3` (việc của `W25-5` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N103`): `ba` ×1 · `qa` ×5 lượt (2 lượt chạm trần 60 turn, tiếp bằng `SendMessage`) · `rev` ×2 · `decider` ×1 · `be` ×1 · `arch` ×2.
`canh-bao.jsonl` ghi "thiếu khối JSON cuối" ở nhiều lượt — **không phải thiếu thật** (giới hạn hook đã biết, `chanDungKhiThieuJson=false`).
**Sự cố 1 lần:** NC-3b chạy đột biến gỡ guard ⇒ treo 600 s, bị kill, tệp nguồn còn dòng đột biến — ORCH `git restore` ngay,
cây sạch xác nhận; chạy lại bằng đột biến biên `<`→`<=` ⇒ đỏ→xanh. **`ngoaiVung`**: `qa` bị hook chặn `git restore` (ORCH làm thay);
mọi tệp còn lại đúng vùng ghi.
