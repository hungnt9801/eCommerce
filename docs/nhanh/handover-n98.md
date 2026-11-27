# Handover phiên N98 — `W24-5` (WP-24 · `PRO-002`) · chuỗi `gd3-1`

> Nhánh `feat/gd3-wp24-dieu-kien-km` · base `origin/tich-hop/gd3` @ `84157a1` · bước **F6 OPS + G6 `-DayDu`**.
> Kết quả: **W24-5 XONG** — G6 `-DayDu` **exit 0 · 20/20**, E2E **167/167** + Playwright **8/8**, đẩy fast-forward `tich-hop/gd3`.
> Giữa phiên G6 **đỏ** (E2E `GET /DatHang` = 500) ⇒ `D-293`/`D-294` mở lại chính hàng, vá xong mới đẩy.

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| F6 tài liệu triển khai | `ops` | **`docs/ops/PRO-002-deploy.md` mới** (268 dòng, 7 mục): phạm vi/migration `20261126224627_Add_Promotion_Module` (7 bảng `Pro*`, 20 index — 9 unique `NULLS NOT DISTINCT`) · 8 Setting (`Promotions:Enabled` mặc định `true`, 0 env mới) · thứ tự triển khai (`pg_dump` trước, **cấm** `dotnet ef database update`) · **đường lùi** 6 hàng · cổng · đứt gãy ai gọi |
| G6 lần 1 | ORCH | **ĐỎ** ở bước E2E: `S1 GET /DatHang = 200` → **HTTP 500** (5 ca trượt: S1/S2/S3/S8-P1/S8-P2) |
| Chẩn đoán | `be` | `Autofac ... Cannot resolve parameter 'IPromotionEngine khuyenMai' of CheckoutAppService` — `src/eCommerce.Web` **không** nạp `PromotionsApplicationModule` (thiếu `ProjectReference` + `DependsOn`), trong khi `CheckoutAppService` nhận `IPromotionEngine` từ `0ebc110` (N94) ⇒ hồi quy **do WP-24** |
| Quyết định | `decider` | **`D-293`**: mở lại `W24-5` (không tạo hàng mới), `vai` += `be · qa · ba`, thứ tự cố định; **`D-294`**: chọn **(A)** `Replace(IAbpDistributedLock, LocalAbpDistributedLock)` ở Web (module kéo `AbpDistributedLockingModule` ⇒ Medallion cần `IDistributedLockProvider`); đồng thời sửa lỗi scope của `D-293` — ARCH do `arch` ghi, không phải `ba` |
| Vá code | `be` | `eCommerce.Web.csproj` +`ProjectReference` Promotions.Application · `eCommerceWebModule.cs` +`typeof(PromotionsApplicationModule)` trong `[DependsOn]` +`Replace(... LocalAbpDistributedLock)`; **0** sửa `CheckoutAppService` |
| Test cấu trúc | `qa` | `test/eCommerce.Tests/Promotions/PromotionsModuleKienTrucTests.cs` — **16 ca** (12 theory 6 module × Host/Web, quét generic `ProjectReference`→`DependsOn` với khoá sàn, `Replace(LocalAbpDistributedLock)`, worker/job tắt); **NC-N98-1/2 đỏ→xanh** (1→0); testplan §1.4/§2.1/§4.1 |
| Tài liệu as-built | `arch` · `ba` · `ops` | ARCH §2.1 sơ đồ + §B6 + §5.2 "Host **và** Web" + đoạn `Replace` + `v0.9` · SRS bullet "Trạng thái triển khai" + `v0.19` · deploy doc §1.1/§3/§4/§5.2 |
| G6 lần 2 | ORCH | **exit 0 · 20/20** — E2E Razor **167/167**, Playwright **8/8** |

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` (baseline) | 0 | 0 error · **9 warning** (117,8 s) |
| `dotnet test test/eCommerce.Tests --no-build` (baseline) | 0 | **2461/2461** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (baseline) | 0 | PASS — 293 tệp · 978 link · 1761 backtick |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (lần 1) | **1** | 10 bước xanh rồi **E2E đỏ** (`E2E: 145/150`) |
| `dotnet build` (sau vá) | 0 | 0 error · 9 warning |
| `dotnet test test/eCommerce.Tests --no-build` (sau vá) | 0 | **2477/2477** (+16) |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (lần 2) | **0** | **20/20 bước · ≈840,6 s** — build 0/9 · unit **2477** · integration **156** · mermaid **214/0** · cổng tài liệu XANH · FE typecheck 0 · `gen:api` không đổi · DB **25/25** · test cổng **60/60** · E2E Razor **167/167** · Playwright **8/8** |
| `check-agent-artifact -Kind decision` | 0 | DAT (117 dòng, gồm `D-293`/`D-294`) |
| `check-agent-artifact -Kind testplan` | 0 | DAT |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau) | 0 | PASS — 294 tệp · 988 link · 1774 backtick |

## 2. Chưa xong / cắt giữa

**Không.** `W24-5` đạt DoD (G6 `-DayDu` exit 0 + deploy doc có đường lùi + `07` §13) và đã đẩy `tich-hop/gd3`.

## 3. Migration đã áp

**Không có — 0 migration, 0 `pg_dump`** trong phiên này. Không đổi schema, không chạm DB có dữ liệu.
(`e2e.log` của G6 có gieo/dọn dữ liệu probe và tự khôi phục cờ `AbpUsers.ShouldChangePasswordOnNextLogin` — harness của dự án, không phải migration.)

## 4. Quyết định mới

- **`D-293`** (TẠM CHỐT DEC) — mở lại `W24-5` tại chỗ, `vai` += `be · qa · ba`; vá wiring Web; test cấu trúc **bắt buộc**; chạy lại **toàn bộ** `-DayDu` trước khi push.
- **`D-294`** (TẠM CHỐT DEC) — Web dùng `LocalAbpDistributedLock` (không Redis/package mới); ARCH do `arch` ghi (sửa scope `D-293`); `qa` phủ mốc `Replace`, **không** dựng test boot riêng.
- Cả hai vào lô `D-277…D-294` **chờ người duyệt** (`docs/agents/hang-doi-xem-lai.md`).

## 5. Nợ / rủi ro

- 🟠 **Probe 403 vẫn CHƯA ĐO** — nợ `qa` **đã quá hạn `W24-5`**: Host dev không có `/api/identity/**` (404) ⇒ không tạo được user thiếu quyền. **Không** hạ kỳ vọng 403; phải đo ở staging có Identity HTTP API.
- 🟠 **`0101/0104/0119` chưa map Host** (`eCommerceHttpApiHostModule.cs` chỉ `0001–0010`): `/api/app/promotion/evaluate|validate-voucher` mã sai ⇒ **403** thay vì 404/409. Chủ **`PRO-001`**.
- 🟡 **BR-10** (một UoW `Redemption`+đơn+outbox) · **BR-19** (COD chưa đối soát ⇒ `0008`) chưa phủ — chủ `SAL-002`/`SHP-003`/`REC-003`.
- 🟡 **P3 còn** (REV vòng 2): `CheckoutAppService.Lines.UnitPrice` = `ListPrice` · thiếu test tự động tenant/`Map` · cờ `KhongGhiSoGiaiTrinh` trên DTO công khai · `PromotionCandidateData.Status` mặc định `Active` · DTO `/evaluate` chưa phơi `VoucherOutcome`.
- 🟡 **`D-283`** (4/9 metric `ChuaCoNoiPhat`) · **`D-284`** (chưa nối `IIdempotencyGuard`) — nợ từ `N94`.
- 🟡 **`eCommerce.slnx` +6 dòng** (4 project `Promotions`) — additive bắt buộc của WP tạo module, đã ghi trong deploy doc §1.1; `Directory.Packages.props`/`pnpm-lock.yaml` không đổi.
- 🟡 **33 ràng buộc `Pro*`** trong `RangBuocChuaAnhXa.txt` · **`pnpm build` EPERM** + **`pnpm lint` no-script** (`N70`) — nợ môi trường cũ, `kiem-tat-ca` vẫn 0.
- **Chờ người:** (a) duyệt lô `D-277…D-294` (tăng 2 dòng); (b) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp; (c) kế toán duyệt lô M4 trước release; (d) merge `main`; (e) tạo **PR nháp** `tich-hop/gd3 → main` khi chuỗi xong.

## 6. Việc đầu tiên của phiên sau

**XONG `W24-5` (WP-24 đóng) → mở hàng kế của chuỗi** (`PH next`: `W25-0` — cổng tài liệu `PRO-001`, `D-169`).

- `W25-0` là `doc-gate` cho `PRO-001` voucher/coupon: đọc `07` §WP-25 + `D-169` dòng `PRO-001` (sửa **P2 #20** — **cấm** gán `UsageSequence = 1`), nhớ **M4 kế toán duyệt lô trước release**.
- Đọc trước: `docs/nhanh/handover-n98.md` §5 (nợ), `docs/ops/PRO-002-deploy.md` §7, `docs/reviews/PRO-002-review.md` (P2/P3 còn lại).
- Lưu ý vận hành: `Promotions:Enabled=false` là đường lùi rẻ nhất; deploy **cả Host và Web** (chỉ Host ⇒ `/DatHang` 500).

```
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp24-dieu-kien-km`; base `origin/tich-hop/gd3` @ `84157a1`; HEAD sau phiên: xem `git log -1`.
- Commit phiên đã push `origin/<nhánh>` và **fast-forward** lên `origin/tich-hop/gd3`.
- **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N98`): `ops` ×2 (deploy doc → cập nhật `D-294`) · `be` ×3 (chẩn đoán → vá 2 dòng → `Replace` Local lock) · `decider` ×2 (`D-293`, `D-294`) · `qa` ×1 (16 ca + 2 NC) · `arch` ×1 · `ba` ×1. **0 lần chặn dừng.**
⚠️ Hook `canh-bao.jsonl` ghi 4 cảnh báo "ghi ngoài vùng" ở lượt `arch`/`ba`/`ops` — **báo nhầm do chạy 3 vai song song**: mỗi vai so `git status` sau lượt và thấy tệp của hai vai kia. Đã đối chiếu `git diff --stat` từng tệp khớp đúng mô tả của từng vai (`arch` +11/−4 ARCH · `ba` +3/−1 SRS · `ops` deploy doc), `ngoaiVung=[]` thật.
