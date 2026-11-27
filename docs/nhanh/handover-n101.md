# Handover N101 — WP-25 / hàng `W25-2` (F3 BE) XONG — **mở F3 FE (`W25-3`)**

> Phiên `N101` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W25-2` · WP-25 · mã `PRO-001` · module `Promotions` + `Sales` ·
> nhánh `feat/gd3-wp25-voucher` (base `origin/tich-hop/gd3` @ `e7aa32c`) · 27–28/11/2026.
> Phạm vi phiên: **F3 BE** — hợp đồng `Promotions.Application.Contracts` + 2 service voucher + domain 2 máy trạng thái NÉM +
> `VoucherController` 7 route + map `01xx→HTTP` + nối `Sales` (`D-298`/`D-305`) + job `voucher-expiry` (`D-306`) +
> migration `Add_Voucher_Status_Check` (EF sinh) + **116 test đơn vị**.

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Tiền đề tài liệu | `ba` | SRS `pro-001-voucher-coupon.md` **v0.10 → v0.12**: §6.1 `ReserveVoucherDto` +`Subtotal`/`Currency`/`TtlSeconds`/`CheckoutSessionId`; `CommitVoucherDto` +`CheckoutSessionId`/`OrderDiscountTotal` (5 trường, thứ tự giữ nguyên); BR-06/E6/§6.2 `0111` = Σ sau UPDATE; §7.1 ETO hoãn có chủ; §7.2/§8.2/§13 job `voucher-expiry`; TC-42 sửa |
| ARCH | `arch` | `PRO-001-arch.md` **v0.3 → v0.6**: §4.2 `Guid→string` `CheckoutSessionId` (`D-303`), trường thứ 5 `OrderDiscountTotal` + `[Range("0.00",…)]`, validator `Subtotal`/`Currency`, §6.3 bước 4 cơ chế Σ + replay, §6.4/§6.5 job TTL, §8 metric, §13 nợ `PRO-002`; P3 "14 dòng map" → **17** |
| DOCREV | `docrev` | vòng 3 delta **`DOC-OK` P1=0 P2=2 P3=3** → sửa hết P2/P3 → vòng 4 delta D-305/D-306 **`DOC-OK` P1=0 P2=0 P3=5** (P3 còn lại ở §5) |
| Quyết định | `decider` | **`D-303`…`D-306`** (xem §4); `check-agent-artifact -Kind decision` **DAT** (exit 0) |
| F3 BE | `be` (Opus) | Contracts `IVoucherAdminAppService` (7 method) + `IVoucherRedemptionService` (4 method); domain 2 máy trạng thái **NÉM** (`0006/0105/0116`); `UsageSequence` đúng `D-295` (SAVEPOINT, `ON CONFLICT` có target, **5 lần thử**, `23505` index khác NÉM, hết trần `0104`, không `25P02`); guard tường minh `AbpValidationException` (400) vì service **không** kế thừa `IApplicationService`; `MaxBatchSize` kẹp 1…100000; `VoucherAdminAppService` 7 method + 3 quyền `.Voucher.*` + audit `Revoke/OverrideLimit`; `VoucherController` `/api/v1/voucher/**` **7 route** + giữ auto `/api/app/voucher-admin`; Host map **17 dòng** `01xx→HTTP` (trừ `0109`/`0113`) + `Sales 0005`; **`D-305`** Σ `DiscountAmount` vs `OrderDiscountTotal` ⇒ NÉM `0111` (cả replay) + `DonHangAppService` truyền `don.DiscountTotal` sau `Recalculate` trước tripwire; **`D-306`** `VoucherExpiryWorker` (lô 500, UoW `requiresNew`, khoá `promotion:job:voucher-expiry:{tenant}` TTL 300 s, guard `Status=0`, `40P01` thử lại trần 3, hoàn `ReservedCount` mã-trước-promotion-sau, 2 counter, gỡ `reservation_expired` khỏi `ChuaCoNoiPhat`) |
| Migration | `be` (EF sinh) | `20261127153549_Add_Voucher_Status_Check` — 2 `CHECK` miền `Status` (`CK_ProVoucherCodes_Status`, `CK_ProPromotionRedemptions_Status`), expand-only, **CHƯA ÁP DB** (xem §3) |
| Test đơn vị | `qa` | **+116 ca** (2477 → **2593**): guard DTO, D-305 commit/replay, UsageSequence, `MaxBatchSize`, máy trạng thái, worker TTL, allowlist 2 namespace. **2 negative control đỏ→xanh** (`NC-1` guard `<0`→`<=0`; `NC-2` đảo chiều so Σ replay), `git diff` sản phẩm sau NC = rỗng |

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-25` (mở) | 0 | **XANH** — mọi mã `Tạm duyệt` |
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở) | 0 | **0 error · 9 warning** (Address/Sales có sẵn) · 81,14 s |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở) | 0 | **2477/2477** · 0 skipped |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở) | 0 | **PASS** · 996 link · 1816 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` (mở) | 0 | **49/49** pass |
| **Đo lại** `dotnet build … --no-incremental` (đóng) | 0 | **0 error · 9 warning** (0 warning mới) · Build succeeded |
| **Đo lại** `dotnet test test/eCommerce.Tests --no-build` (đóng) | 0 | **2593/2593** · 0 skipped · 12 s (+116) |
| **Đo lại** `pwsh -File tools/kiem-tat-ca.ps1` (ORCH chạy, đóng) | 0 | **10/10 bước ĐẠT** (build 107 s · unit 24,8 s · integration 35,2 s · mermaid 11,5 s · FE typecheck+Vitest 12,4 s · `gen:api` drift 0 · DB 11,6 s · test cổng 186 s) |
| **Đo lại** `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng) | 0 | **PASS** · 1816 backtick · 0 chết |
| **Đo lại** `npm --prefix tools run check:mermaid` (đóng) | 0 | **214 khối · 0 lỗi** |
| **Đo lại** `node --test .claude/hooks/test/hooks.test.mjs` (đóng) | 0 | **49/49** pass |
| `check-agent-artifact.ps1 -Kind docreview` (vòng 3 và 4) | 0 | **DAT** · `DOC-OK` · **P1=0** (vòng 3 P2=2/P3=3 đã sửa; vòng 4 P2=0/P3=5) |
| `check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** · 117 dòng cần kiểm |
| `check-feature-doc.ps1 -WP14 -Path docs/features/promotions/pro-001-voucher-coupon.md` | 0 | **DAT** — 16/16 heading · 36/36 BR có test |

## 2. Chưa xong / cắt giữa

**Không có việc code dở của `W25-2`.** Hai việc còn lại **không làm được trong auto mode** hoặc **thuộc hàng sau**:

1. **Áp migration `Add_Voucher_Status_Check` lên DB dev — BỊ CHẶN NGOÀI PHIÊN.** ORCH đã `pg_dump` (§3) nhưng lệnh
   `dotnet run --project src/eCommerce.DbMigrator` bị **auto-mode classifier từ chối** (lý do: áp migration phải do người duyệt ngoài auto mode;
   không lách). Migration đã nằm trong commit, **chưa** có trong `__EFMigrationsHistory`. Người chạy 1 lệnh ở §6.
2. **P3 còn lại của docrev vòng 4** (`docs/reviews/PRO-001-arch-docreview.md` "Vòng 4") — 5 mục nhỏ, **không chặn**: (1) arch header trỏ SRS
   `v0.11` trong khi SRS đã `v0.12`; (2) bảng lịch sử arch thiếu dòng `v0.6`; (3) số dòng `PromotionExpiryWorker` lệch mốc; (4) câu "so decimal, cấm `==`"
   mâu thuẫn nội tại (as-built dùng `decimal.Compare`); (5) `40P01` "thử lại có trần" chưa nêu số lần (code chọn **3**).
3. **Phủ integration còn nợ `W25-4`** (đã ghi trong test QA): Σ sau UPDATE trên PostgreSQL thật + rollback; `UsageSequence` hết trần/`25P02`;
   worker `UPDATE 0 dòng`/deadlock trên DB thật; đề bài probe HTTP 200/401/403 cho 7 route.
4. **`W25-3` (F3 FE)** — hàng kế, chưa mở.

## 3. Migration đã áp

**Có file migration, CHƯA áp DB.**

- `src/eCommerce.EntityFrameworkCore/Migrations/20261127153549_Add_Voucher_Status_Check.cs` — 2 `AddCheckConstraint`
  (`CK_ProVoucherCodes_Status`, `CK_ProPromotionRedemptions_Status`), `Down` = 2 `DropCheckConstraint`; **0 cột/index/dữ liệu** (expand-only),
  do EF sinh, chủ `PRO-002` (`D-300`).
- **`pg_dump` trước khi áp (đã làm):** `artifacts/pg-dump/N101-W25-2.dump` — **576.910 byte** (PostgreSQL 17 cổng 5433, `pg_dump -Fc`).
- **Chưa áp** vì auto mode từ chối `dotnet run --project src/eCommerce.DbMigrator` (không `dotnet ef database update` — bị cấm).
  Người chạy ngoài auto mode: `dotnet run --project src/eCommerce.DbMigrator`, rồi kiểm `__EFMigrationsHistory` có
  `20261127153549_Add_Voucher_Status_Check`.

## 4. Quyết định mới

`decider` ghi **4 dòng** `TẠM CHỐT (DEC, uỷ quyền)`, `check-agent-artifact -Kind decision` **DAT**:

| D-xx | Nội dung (ngắn) | Phạm vi ảnh hưởng |
|---|---|---|
| `D-303` | `CheckoutSessionId` ở **cả hai** record là **`string`** ≤ 128 (= `CheckoutInput.IdempotencyKey`, `CheckoutDtos.cs:67-69`), **không** `Guid`; lùi = 2 dòng doc | Hợp đồng `Promotions.Application.Contracts` ↔ `Sales` |
| `D-304` | Hoãn 2 ca test (`TtlSeconds` ngoài `60…86400`; `Subtotal` thiếu/`=0`) về **`qa` ở `W25-4`**; cấm đóng `W25-4` khi thiếu, cấm bỏ ca/hạ `Range` | SRS §14.1 + testplan `W25-4` |
| `D-305` | **BR-06/`0111`**: thêm trường **thứ 5** `CommitVoucherDto.OrderDiscountTotal`; `Promotions` tự so Σ `DiscountAmount` các lượt `Applied` cùng `OrderId` ⇒ lệch NÉM `0111` (500), **replay vẫn so**; `DonHangAppService` truyền `don.DiscountTotal` sau `Recalculate` trước tripwire, cùng UoW; không vào payload khoá | `Promotions` + `Sales` + SRS/arch |
| `D-306` | Job **`voucher-expiry`** làm **ngay `W25-2`**: khoá riêng TTL 300 s, lô 500, UoW `requiresNew`, guard `Status=0`, `40P01` thử lại trần 3, 2 counter (`reservation_expired` + `released{TTL_EXPIRED}`), `reservation_expired` rời `ChuaCoNoiPhat`; ETO ra **hoãn có chủ** (`REP-001`, nhánh huỷ `SAL-004`), `PromotionAppliedEto` giữ nợ `SAL-001` | `Promotions` worker + metric + SRS/arch |

Hàng đợi xem lại: `docs/agents/hang-doi-xem-lai.md` thêm `D-303`…`D-306`.

## 5. Nợ / rủi ro

- 🔴 **Migration chưa áp** — chủ **người** (ngoài auto mode). File + `pg_dump` đã sẵn; 1 lệnh ở §6.
- 🟡 **`F-1` (QA phát hiện, chưa sửa):** `VoucherExpiryWorker` chỉ phát metric khi `daNha > 0`, còn arch §6.4 ghi "0 dòng ⇒ bỏ qua + log Debug + metric".
  Chủ `rev/ba` `W25-4` chốt câu chữ hoặc code.
- 🟡 **P3 docrev vòng 4** — 5 mục nhỏ ở §2.2; chủ `arch`/`ba`, sửa cùng `W25-3`/`W25-4` (không chặn).
- 🟡 **Phủ test tầng integration** — Σ sau UPDATE/rollback, `UsageSequence` hết trần/`25P02`, worker `0 dòng`/deadlock, probe 200/401/403:
  chủ `qa` `W25-4` (đã ghi trong testplan dự kiến).
- 🟡 **Nợ `PRO-002`** — `PromotionExpiryWorker` (job cũ) **chưa** đọc cờ `Promotions:Enabled` dù arch nói cả 2 job đọc; chủ `PRO-002`, `qa/rev` xác nhận `W25-4`.
- 🟡 **`promotion_applied_total`** vẫn trong `ChuaCoNoiPhat` (nợ `SAL-001`).
- 🟡 **Nợ từ N98/N99**: probe 403 chưa đo · P3 `PRO-002` · `D-283`/`D-284` · `pnpm build` EPERM + `pnpm lint` no-script (nợ môi trường) ·
  33 ràng buộc `Pro*` trong `RangBuocChuaAnhXa.txt`.
- **Chờ người** (gom một lần ở câu trả lời cuối): (a) **áp migration** bằng DbMigrator ngoài auto mode; (b) duyệt lô `D-277…D-306`;
  (c) kế toán duyệt lô M4 (`D-143`/`D-148`/`D-149`/`D-152`/`D-158`) trước release; (d) `Q16` mở WP đối chiếu as-built `PRO-001`;
  (e) merge `main` + PR nháp `tich-hop/gd3 → main` khi chuỗi xong; (f) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W25-2` → mở `W25-3`** (F3 FE: màn hình voucher/coupon trên `/api/v1/voucher/**`).

- Hàng kế = **`W25-3`** (loại `code`, bước `F3 FE`), vai **`fe`**. Cần `schema.d.ts` có 7 route voucher — kiểm `pnpm gen:api` (hiện `gen:api` drift **0**; nếu path voucher chưa có trong `schema.d.ts` thì dùng đúng quy trình `fe` + bảng cấp phát).
- **Việc của người trước/trong phiên sau (1 lệnh):** `dotnet run --project src/eCommerce.DbMigrator` (ngoài auto mode) — `pg_dump` đã có
  `artifacts/pg-dump/N101-W25-2.dump` (576.910 byte); kiểm `__EFMigrationsHistory`.
- Đọc trước: `PRO-001-arch.md` **v0.6** (§10.1 bề mặt UI, §10.4 ca đỏ), SRS **v0.12** (§10 màn hình, §11 quyền), handover này §2/§5.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-25
và handover gần nhất `docs/nhanh/handover-n101.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng W25-3** (phiên N102). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp25-voucher`; base `origin/tich-hop/gd3` @ `e7aa32c`; **HEAD `6f9766d`**.
- Commit phiên đã push `origin/feat/gd3-wp25-voucher`: `f7d157f` (docs), `d55cf56` (BE F3 + migration), `3dbdbfa` (docs D-305/306),
  `8dd40e1` (BE D-305/306), `6f9766d` (test +116).
- **Không** đẩy `tich-hop/gd3` (hàng `code`; việc đó ở `W25-5` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N101`): `ba` ×3 · `docrev` ×2 · `decider` ×2 · `arch` ×3 (2 lượt chạm trần 30 tool-use — đã hoàn tất tệp) ·
`be` ×1 (chạm trần 80 lượt rồi `SendMessage` tiếp) · `qa` ×1 (94 tool-use) · **0 lần chặn dừng**. Hook `canh-bao.jsonl` ghi "thiếu khối JSON cuối"
ở vài lượt — **không phải thiếu thật** (giới hạn đã biết, `chanDungKhiThieuJson=false`); **0 tệp ngoài vùng ghi** lọt
(`ngoaiVung = []` mọi lượt; hook chặn `be` ghi `test/**` đúng thiết kế ⇒ `qa` viết test).
