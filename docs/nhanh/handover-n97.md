# Handover phiên N97 — `W24-4` (WP-24 · `PRO-002` engine điều kiện khuyến mãi) · chuỗi `gd3-1`

> Nhánh `feat/gd3-wp24-dieu-kien-km` · base `origin/tich-hop/gd3` @ `84157a1` · bước **F4 QA + F5 REV (vá REV vòng 1 → REV vòng 2)**.
> Kết quả: **W24-4 XONG** — `F4 QA` đạt (testplan DAT) · `F5 REV` **PASS · P1=0** · `G3 kiem-tat-ca` 10/10. **Chưa** push `tich-hop/gd3` (đó là `W24-5`).

## 1. Đã xong

**Mở lại `W24-4` sau REV vòng 1 CHANGES (N96) — vá theo `D-291` + `D-292`:**

| Bước | Vai | Kết quả |
|---|---|---|
| Phạm vi vá | `decider` | `D-291` (TẠM CHỐT): cho mở một vòng `be`→`qa`→`rev` vòng 2 trong cùng hàng; phạm vi bắt buộc P1 #1 + P2 #2/#3/#4 + P3 #6/#8; cột `vai` thêm `be` |
| Vá code | `be` (Opus) | P1 #1 guard `Promotion.Status == Active` + `VoucherCode.Status != Revoked` (fail-closed, đối xứng auto); P2 #2 `Subtotal`=`TongTruocGiam` + `CurrencyCode` từ pipeline giá (cờ additive `KhongGhiSoGiaiTrinh`, mặc định `false`; 0 ghi sổ hai lần); P2 #3 `VoucherOutcome` additive + `ValidateVoucherAsync` chỉ theo voucher; P2 #4 NÉM `0007`/`0119`/`0104`/`0101` (mã đã cấp `D-144`/`D-147`), cây `false` không ném; P3 #6 `?sort=` whitelist; P3 #8 `PromotionDiscountCalculator` default NÉM; P3 #7 truyền `CancellationToken` thật |
| Thứ tự | `decider` | `D-292` (TẠM CHỐT): giữ `D-161`/`D-162`, **sửa code** — `DanhGiaCandidateAsync` duyệt **cây trước, hết lượt (khách) sau** |
| SRS | `ba` | `PRO-002` **v0.18**: vẽ as-built (`VoucherOutcome`, cờ `KhongGhiSoGiaiTrinh`, mã `0101/0104/0119`, `?sort=`, calculator NÉM); hoàn nguyên thứ tự về `D-161`/`D-162`; thêm `TC-72…TC-76` + DoD 62 |
| Kiểm tài liệu | `docrev` | vòng 1 `DOC-CHANGES` (0 P1/3 P2) → **vòng 2 `DOC-OK`** (0 P1/0 P2/1 P3) |
| Test | `qa` | **+26 unit** (2432→2458) phủ `TC-72…TC-76` + `D-292`; **+3 test DI** (2458→**2461**); **7 negative control đỏ→xanh**; probe HTTP thật (401×3 · 200 rule-catalog **4 tên** · 200 approve · 400 `0001` · 409 `0006`); **403 chưa đo** (identity API 404) |
| Bug thật do probe | `be` | ① `[ExposeServices(typeof(IPromotionConditionRule))]` cho 4 rule (ABP DI theo tên ⇒ `rule-catalog` trả `[]`); ② `SaveTreeAsync` dùng `CurrentTenant.Id` (Guid?) thay `Guid.Empty` (host `TenantId = NULL` khớp filter) |
| REV vòng 2 | `rev` (Opus) | **`VERDICT: PASS` · P1=0 · P2=1 · P3=5** — P2 còn lại: `0101/0104/0119` chưa map Host (chủ `PRO-001`, không chạm tiền/trạng thái/quyền) |

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` (baseline) | 0 | 0 error · **9 warning** (55,1 s) |
| `dotnet test test/eCommerce.Tests --no-build` (baseline) | 0 | **2432/2432** |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (baseline) | 0 | PASS — 292 tệp · 977 link · 1752 backtick |
| `dotnet build` (sau) | 0 | 0 error · 9 warning |
| `dotnet test test/eCommerce.Tests --no-build` (sau) | 0 | **2461/2461** (+29) |
| `dotnet test test/eCommerce.IntegrationTests` | 0 | **156/156** |
| `pwsh -File tools/kiem-tat-ca.ps1` | 0 | **10/10 · 346,0 s** — unit 2461 · integration 156 · mermaid 214/0 · FE typecheck+Vitest 60/60 · `gen:api` không đổi · DB 25/25 · test cổng 60/60 |
| `check-agent-artifact -Kind testplan` | 0 | DAT — `BR trong SRS=20 · chua co trong testplan=0` |
| `check-agent-artifact -Kind review` | 0 | DAT — **verdict=PASS · P1=0** |
| `check-agent-artifact -Kind docreview` | 0 | DAT — **DOC-OK · P1=0 · P2=0** |
| `check-agent-artifact -Kind decision` | 0 | DAT |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau) | 0 | PASS — 292 tệp · 977 link · 1757 backtick |

**Commit đã push** `origin/feat/gd3-wp24-dieu-kien-km`: `48d9732` (vá P1/P2/P3 + SRS v0.18 + D-291/D-292 + docreview) → `3d52d51` (DI `rule-catalog` + tenant `save-tree` + test QA) → `e602e92` (test DI + probe + testplan) → `48eda1d` (REV vòng 2 PASS).

## 2. Chưa xong / cắt giữa

**Không** — `W24-4` đã đạt DoD (`F5 REV PASS · P1=0`) và đã đo lại. Các mục còn lại đều là **nợ có chủ** (xem §5), không phải việc dở của hàng.

## 3. Migration đã áp

**Không có** — **0 migration, 0 `pg_dump`**. Không đổi schema; `schema.sql` (test asset) không đổi trong N97.
Ghi chú: `qa` chạy `DbMigrator` để seed `ProPromotionRuleCosts` (4 dòng) phục vụ probe trên DB dev; không áp migration mới, dọn `ProPromotions`/`ProPromotionConditions` của probe (còn 0 dòng).

## 4. Quyết định mới

- **`D-291`** (TẠM CHỐT DEC) — mở lại `W24-4`, phạm vi vá P1 #1 + P2 #2/#3/#4 (+P3 #6/#8), probe HTTP thật bắt buộc thử, cột `vai` thêm `be`.
- **`D-292`** (TẠM CHỐT DEC) — giữ `D-161`/`D-162`, **sửa code** thứ tự `DanhGiaCandidateAsync` (cây trước, hết lượt sau).
- Cả hai nằm trong lô `D-277…D-292` **chờ người duyệt** (`docs/agents/hang-doi-xem-lai.md`).

## 5. Nợ / rủi ro

- 🟠 **`0101/0104/0119` chưa map Host** (`eCommerceHttpApiHostModule.cs` chỉ `0001–0010`): gọi `/api/app/promotion/evaluate|validate-voucher` với mã sai ⇒ **403** thay vì 404/409. Chủ **`PRO-001`** (khi mở đường voucher); đã ghi as-built SRS v0.18; không chạm tiền/trạng thái/quyền nên không chặn PASS.
- 🟠 **Probe 403 CHƯA ĐO** — Host trả 404 cho `/api/identity/**` ⇒ không tạo được user thiếu quyền. Nợ chủ **`qa`**, hạn **`W24-5`**; **không** hạ kỳ vọng 403.
- 🟡 **BR-10** (áp khuyến mãi qua một UoW `Redemption`+đơn+outbox) và **BR-19** (COD chưa đối soát ⇒ `0008`) **chưa phủ** — nợ có chủ `SAL-002` / `SHP-003` / `REC-003`; bổ sung test khi các WP đó land.
- 🟡 **P3 còn** (REV vòng 2): `CheckoutAppService.Lines.UnitPrice` vẫn `ListPrice` (dòng đơn — ngoài hợp đồng engine); thiếu test tự động cho tenant/`Map`; cờ `KhongGhiSoGiaiTrinh` trên DTO công khai; `PromotionCandidateData.Status` mặc định `Active` (fail-open tiềm ẩn — data source thật luôn điền); DTO `/evaluate` chưa phơi `VoucherOutcome`.
- 🟡 **`D-283`** (4/9 metric `ChuaCoNoiPhat`), **`D-284`** (chưa nối `IIdempotencyGuard`) — nợ từ `N94`, không đổi.
- 🟡 **33 ràng buộc `Pro*`** trong `RangBuocChuaAnhXa.txt` — cơ chế của test; `be` có thể chuyển sang `ViPhamRangBuocDb` nếu cần mã lỗi riêng.
- 🟡 **`pnpm build` EPERM** + **`pnpm lint` no-script** (`N70`) — môi trường/nợ cũ; `kiem-tat-ca` vẫn 0.
- **Chờ người:** (a) duyệt lô `D-277…D-292`; (b) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp; (c) kế toán duyệt lô M4 trước release; (d) merge `main`.

## 6. Việc đầu tiên của phiên sau

**XONG `W24-4` → mở `W24-5` (`dong-wp`: F6 OPS + G6 `-DayDu` ⇒ push fast-forward `tich-hop/gd3`).**

- `W24-5` chạy `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (**không** chạy song song build/test khác — N69), viết tài liệu triển khai `PRO-002` (thư mục `docs/ops`, tạo ở bước này) có đường lùi, một dòng `07` §13, rồi `git push origin feat/gd3-wp24-dieu-kien-km:tich-hop/gd3` (chỉ fast-forward; bị từ chối ⇒ `PH dodang`, không force/merge).
- Đọc trước: `docs/nhanh/handover-n97.md` (§5 nợ), `docs/reviews/PRO-002-review.md` (P2 còn lại), `docs/tests/PRO-002-testplan.md` §5.
- Sau `W24-5` mới mở `W25-0` (cổng tài liệu `PRO-001`, `D-169`).

```
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp24-dieu-kien-km`; base `origin/tich-hop/gd3` @ `84157a1`; HEAD `48eda1d`.
- Cả 4 commit phiên **đã push** `origin/<nhánh>`.
- **Không** push `tich-hop/gd3` (chỉ `W24-5` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N97`): `decider` ×2 (`D-291`, `D-292`) · `be` ×3 (vá → đảo thứ tự → DI/tenant) · `ba` ×3 (vẽ as-built → đóng DOCREV → hoàn nguyên order) · `docrev` ×2 (vòng 1 CHANGES → vòng 2 DOC-OK; lượt 1 chạm trần 60 turn, lượt 2 ghi xong) · `qa` ×2 (tests → DI test + probe; lượt 1 chạm trần 60 turn) · `rev` ×1 (chạm trần 30 turn, ghi xong sau khi được nhắc). **0 lần chặn dừng.** Cảnh báo hook "thiếu khối JSON" là **báo nhầm** (báo cáo đều có khối JSON; `artifacts/agents/canh-bao.jsonl` ghi `ngoaiVung=[]` cho mọi vai).
