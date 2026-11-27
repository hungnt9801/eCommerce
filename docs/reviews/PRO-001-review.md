# Review code `PRO-001` — vòng 2 — 28/11/2026

- Nhánh: `feat/gd3-wp25-voucher` · base `origin/tich-hop/gd3` · head `6f618f5`
- Vòng 1: review `27538f8` — `VERDICT PASS · P1=0 · P2=4 · P3=4` (xem lịch sử trong tệp này).
- Delta vòng 2 theo yêu cầu: `git diff --stat 514c252..HEAD` (5 commit: `27538f8` trùng vòng 1 + `b3cb947`,
  `c16824e`, `696ce46`, `6f618f5`). **Phần sửa mới so với vòng 1** (`27538f8..HEAD`) = **5 tệp**:
  - `modules/Promotions/eCommerce.Promotions.Application/VoucherAdminAppService.cs` **+21** — **mã sản phẩm DUY NHẤT** (`D-309`);
  - `docs/features/promotions/PRO-001-arch.md` **+56/−17** (v0.7, `D-310` + đặc tả `D-309`);
  - `test/eCommerce.Tests/Promotions/VoucherMaxBatchSizeTests.cs` **+80** (4 ca `D309_*`);
  - `docs/tests/PRO-001-testplan.md` (+NC-3/NC-3b, số ca); `tools/probe-voucher-wp25.ps1` (delta vòng 1, không đổi nội dung logic).
- SRS `pro-001-voucher-coupon.md` v0.13 · ARCH `PRO-001-arch.md` **v0.7** · quyết định vá: `D-309`, `D-310`, `D-311`.

## Vòng 2 — kiểm bản vá (không hồi quy)

| Tệp | Thay đổi | Kết luận |
|---|---|---|
| `VoucherAdminAppService.cs:133-152` | Thêm pre-check dung lượng `codespace = 36^(Length−len(Prefix))` (vòng nhân **có trần**, dừng khi `codespace ≥ Quantity`) trước `while`; thiếu chỗ ⇒ `0107` `VoucherCodeInvalid` + `WithData(length, prefix, codespace, quantity)`. | **ĐÚNG** — xem §1 dưới. |
| `PRO-001-arch.md` v0.7 `§6.1b`/`§6.4`/`§1.1`/`§8` | §6.4 bỏ "+ metric" ở nhánh 0 dòng (khớp as-built, `D-310`); §1.1/§8 sửa nợ metric cũ; thêm §6.1b đặc tả thứ tự guard `GenerateAsync` + pre-check `D-309`. | **ĐÚNG** — line tham chiếu khớp mã as-built (`:82-174`, `:117`, `:126-131`, `:138-152`, `:155-158`, `:165`). |
| `VoucherMaxBatchSizeTests.cs` | 4 ca `D309_*` (thiếu chỗ ⇒ `0107` <1s; đủ chỗ 10000; biên `space==Quantity`; thứ tự `0108` trước `0107`). | **ĐÚNG** — xem §2. |
| `PRO-001-testplan.md` | Thêm dòng `D309_*`, số ca unit **2619** (+26), NC-3 (surrogate) + NC-3b (biên `<`→`<=`), và **nợ "chống treo chỉ ở mức test"** (§5 #13). | **ĐÚNG** (NC-3b đỏ→xanh ghi thật). |

### §1. Xác nhận `D-309` đúng (đối chiếu checklist)

- **Thứ tự**: `0108` (`:117-122`) → guard độ dài/prefix `0107` (`:126-131`) → **pre-check dung lượng** (`:138-152`) →
  `while` (`:155-158`) → `InsertAsync` (`:165`). Pre-check đứng **sau** `0108` và guard hình dạng, **trước** mọi ghi — đúng `D-309` ①(4).
- **Không tràn**: vòng lặp `for (; i < soKyTuNga && codespace < Quantity; )` dừng ngay khi `codespace ≥ Quantity`
  (`Quantity ≤ MaxBatchSize ≤ 100000`) ⇒ `codespace` trước mỗi phép nhân `< 100000`, `×36 < 3.6e6` — **không** chạm `long`.
  `soKyTuNga = doDai − prefix.Length ≥ 1` được bảo đảm bởi guard `prefix.Length >= doDai` ở `:126`.
- **Biên**: điều kiện ném là `codespace < Quantity` (không `<=`) ⇒ `codespace == Quantity` **KHÔNG** ném (đúng, và có ca `D309_codespace_bang_dung_Quantity__KHONG_nem` chứng minh).
- **Không đổi hợp đồng**: chỉ **thêm** 21 dòng; không đổi `TaoMa`/chữ ký `GenerateAsync`/route; **mã lỗi duy nhất** dùng là `0107` `VoucherCodeInvalid` (đã cấp + đã map 400 ở Host) — **không** thêm số, **không** đổi số cũ, **không** dùng `0108` sai nguyên nhân.

### §2. Xác nhận test `D309_*` thật sự chứng minh

- `D309_codespace_thieu__nem_0107_khong_treo`: `{Length:3, Prefix:"AB", Quantity:100}` ⇒ `ex.Code == 0107`, `codespace=36`,
  `quantity=100`, `SoLanChen == 0`, đo `< 1 s`. **Chứng minh**: có guard ⇒ ném nhanh, không vào `while`.
- `D309_codespace_du__sinh_du_so_dong`: `{Length:12, Quantity:10000}` ⇒ sinh đủ 10000 (không chặn oan).
- `D309_codespace_bang_dung_Quantity__KHONG_nem`: `{Length:4, Prefix:"AB", Quantity:1296}` = `36^2` ⇒ **không** ném (khoá đúng biên `<`).
- `D309_thu_tu__quantity_10001_van_0108_truoc_0107`: `Quantity` vượt trần ⇒ `0108` **trước** `0107`.
- **Tự đo lại (vai `rev`, lệnh scoped)**: `dotnet test test/eCommerce.Tests --filter FullyQualifiedName~VoucherMaxBatchSizeTests`
  → **28/28 pass, 0 fail, 110 ms** (gồm 4 ca `D309`). Không tự chạy lại toàn bộ cổng (theo ràng buộc).
- **Giới hạn (đã ghi đúng ở testplan §5 #13, KHÔNG giấu)**: ca "chống treo" chỉ chặn **ở mức test** bằng
  `Task.WaitAsync(5 s)` — nếu **gỡ hẳn** pre-check, vòng `while` đồng bộ sẽ treo tiến trình; một lượt NC "gỡ hẳn"
  trước đó đã treo > 600 s (harness kill, `git restore`, không mất gì). Vì vậy NC-3b đột biến **biên** `<`→`<=` (không gỡ
  guard) để chứng minh đỏ→xanh an toàn; "chống treo ở mức tiến trình" là **nợ có chủ `qa`/`be`** (process riêng hoặc `CancellationToken`/trần vòng lặp), mốc trước `WEB-001` — **không** nới kỳ vọng. Bản thân bản vá (guard có mặt) là đúng; đây là giới hạn của *cách chứng minh*, ghi thành P3 #9.

### §3. Hồi quy

- `git status --porcelain -- modules src shared test` = **rỗng** (mã sản phẩm/test sạch; 4 tệp `docs/**` đang dirty là của
  vai khác trong cùng lượt vá — `decisions.md`, `hang-doi-xem-lai.md`, `bang-phien`, `agents/state` — **không** do `rev` sửa).
- Build nhánh: **0 error**; warning còn lại là `CS8604` **có sẵn** ở `Sales/OrderStatusAppService`/`MarketplaceIntakeAppService`
  (**không** thuộc delta này, không phát sinh từ `VoucherAdminAppService`).
- Số ca: unit **2619/2619** (QA; tôi xác minh scoped 28/28 lớp bị chạm) · integration **162/162** (QA, không chạy lại) · probe 6/6 (QA).

## Trạng thái phát hiện vòng 1

| # | Mức | Vị trí | Phát hiện | Trạng thái vòng 2 |
|---|---|---|---|---|
| 1 | P2 | `modules/Promotions/eCommerce.Promotions.Application/VoucherAdminAppService.cs:133-152` | Vòng sinh mã không trần thử ⇒ treo vô hạn với input hợp lệ (36 mã < Quantity). | **ĐÃ VÁ** (`D-309`, commit `b3cb947`): pre-check `36^(Length−len(Prefix))`, thiếu ⇒ `0107` trước mọi ghi; 4 ca `D309_*`; NC-3b đỏ→xanh. Đóng. |
| 2 | P2 | `modules/Promotions/eCommerce.Promotions.Application/VoucherRedemptionService.cs:439-528` | Nhánh chốt **ĐẦU** `ThucHienCommitAsync` chưa test nào thực thi (`GuardGia` không gọi `xuLy`). | **GIỮ — nợ có chủ** `D-311` (`qa` +`be` nếu cần seam), mốc **trước `WEB-001`**. Không chặn `W25-4`. |
| 3 | P2 | `modules/Sales/eCommerce.Sales.Application/DonHangAppService.cs:118-132` | Guard `BR-06` một chiều: bỏ `VoucherRedemptionId` ⇒ đơn nhận giảm mà lượt vẫn `Reserved` ⇒ dùng lại mã. | **GIỮ — nợ có chủ** `D-311` (`be`/`Sales`, fail-closed trước `WEB-001`). Không chặn `W25-4`. |
| 4 | P2 | `modules/Promotions/eCommerce.Promotions.Application/PromotionAppService.cs:174,234,239,242,276,330,476` · `…Domain/Seed/PromotionRuleCostSeedContributor.cs:81,88,98` | **PRE-EXISTING** `PRO-002`/W24: 7+3 `autoSave: true` trái `BR-13`/`NFR-09`. | **GIỮ — nợ cũ** chủ `PRO-002`/`be`; QA khoá allowlist. Ngoài phạm vi `PRO-001`; **không** chặn `W25-4`. |
| 5 | P3 | `modules/Promotions/eCommerce.Promotions.Application/Workers/VoucherExpiryWorker.cs:180-186,232-237` | `F-1`: metric chỉ phát khi `daNha>0`, nhánh 0 dòng chỉ `LogDebug`; arch §6.4 hứa metric. | **ĐÓNG bằng `D-310`** (doc-only, giữ code): arch v0.7 §6.4/§8 sửa câu chữ khớp as-built; cấm counter `kind="skipped"`. Code + test `D306_…` giữ nguyên. |
| 6 | P3 | `modules/Promotions/eCommerce.Promotions.Application/eCommerce.Promotions.Application.csproj:13-16` | Comment nói "KHÔNG dùng Messaging/**Idempotency**" nhưng `VoucherRedemptionService` dùng `IIdempotencyGuard`. | **GIỮ** — comment lỗi thời (không hồi quy ở vòng 2); dọn khi chạm csproj. |
| 7 | P3 | `modules/Promotions/eCommerce.Promotions.Application/Workers/VoucherExpiryWorker.cs:73-77,129-135` | Nạp toàn bộ `Reserved` mọi tenant vào RAM để `Distinct`/`Take(500)`. | **GIỮ** — tối ưu khi có tải thật. |
| 8 | P3 | `src/eCommerce.EntityFrameworkCore/Migrations/20261127153549_Add_Voucher_Status_Check.cs` · `test/eCommerce.IntegrationTests/Assets/schema.sql` | Migration 2 `CHECK` chưa áp DB dev; `schema.sql` thiếu 2 `CHECK` ⇒ `BR-09` DB-level (`23514`) chưa phủ. | **GIỮ** — áp migration + tái sinh schema.sql là việc của người (`handover-n102`). |
| 9 | P3 | `test/eCommerce.Tests/Promotions/VoucherMaxBatchSizeTests.cs:204-217` | Ca "chống treo" chỉ chặn **ở mức test** (`Task.WaitAsync(5s)`); gỡ hẳn guard ⇒ treo tiến trình (NC gỡ-guard trước đó treo >600s). | **GIỮ — nợ có chủ `qa`/`be`** (testplan §5 #13), mốc trước `WEB-001`: chạy ca trong process riêng hoặc thêm `CancellationToken`/trần vòng lặp. |

## Bằng chứng đồ thị (kết luận về code)

- `graph: codegraph_explore "VoucherAdminAppService GenerateAsync TaoMa guards"` → xác nhận thứ tự `0108 → 0107(hình dạng) → pre-check(D-309) → while → InsertAsync`; pre-check chỉ ném `0107`; không đổi chữ ký/route.
- `graph: codegraph_explore "VoucherRedemptionService CommitAsync ThucHienCommitAsync"` → `UPDATE … WHERE Status=0 AND ExpiresAtUtc>now` 0 dòng ⇒ `0105`; `Σ DiscountAmount` so `OrderDiscountTotal` (`decimal.Compare`) sau `UPDATE`; counter guard `ReservedCount>0`; SAVEPOINT `sp_seq`/`sp_commit` (không đổi ở vòng 2).
- `graph: codegraph_explore "IIdempotencyGuard ThucThiAsync"` → guard cùng DbContext/UoW; replay `TraVeKetQuaCu`, khác payload `XungDotPayload` (không đổi).
- `graph: codegraph_explore "VoucherExpiryWorker NhaMotLoAsync daNha metric"` → khẳng định `D-310`: metric chỉ khi `daNha>0` (`:180-186`), nhánh 0 dòng `LogDebug` (`:235`).
- `graph: codegraph_explore "DonHangAppService TaoTuCheckoutAsync VoucherRedemptionId"` → xác nhận guard `BR-06` vẫn một chiều (vì `D-311` hoãn, không sửa ở vòng 2).

## Chưa xác nhận

- Không chạy lại **toàn bộ** unit/integration/probe (theo ràng buộc): chỉ tự đo scoped `VoucherMaxBatchSizeTests` 28/28; các số 2619/2619, 162/162, 6/6 lấy từ testplan QA.
- Migration vẫn **chưa áp** DB dev/integration (P3 #8) — không đổi ở vòng 2.
- Đường happy-path end-to-end checkout→tạo đơn→chốt lượt vẫn là điểm phủ mỏng (P2 #2, nợ `D-311`).

## Kết luận

Bản vá `D-309` (P2 #1 vòng 1) **đúng và đã đóng**: thứ tự guard chuẩn, tính dung lượng không tràn, giữ biên `== Quantity`,
chỉ dùng `0107`, có 4 ca test + NC-3b đỏ→xanh. `P3 #5` (`F-1`) **đóng bằng `D-310`** (doc-only). Không hồi quy (mã sản phẩm
delta = 1 tệp +21 dòng, `git status` mã sản phẩm sạch, build 0 error, 28/28 ca lớp bị chạm xanh). Các P2 #2/#3 (nợ tiền/lượt) và
P2 #4 (`autoSave` pre-existing) cùng P3 #6/#7/#8/#9 **giữ** làm nợ có chủ + mốc (`D-311`, trước `WEB-001`; `#4` thuộc `PRO-002`).

**P1 = 0.**

VERDICT: PASS
