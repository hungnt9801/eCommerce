# Handover N92 — WP-26 / hàng `W26-5` (F6 OPS + G6) XONG — **ĐÓNG WP-26**

> Phiên `N92` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W26-5` · WP-26 · mã `REP-001` · module `Reporting` · nhánh
> `feat/gd3-wp26-read-model` (base `origin/tich-hop/gd3`; HEAD trước phiên `2fe2e58`) · 27/11/2026.
> Phạm vi phiên: **F6 OPS** (tài liệu triển khai + đường lùi) → **G6** `kiem-tat-ca.ps1 -DayDu` **20/20 ĐẠT · 1448 s**
> ⇒ **fast-forward** nhánh WP vào `tich-hop/gd3`. `main` không đụng; **không** mở PR (`D-216` ③).

## 1. Đã xong

- **Baseline mở phiên** (`N92`, trước khi làm hàng) — build/test/link/hook tuần tự (nợ bộ nhớ `N69`), xem bảng dưới.
- **F6 OPS** — `ops` viết **mới** `docs/ops/REP-001-deploy.md` (**236 dòng**, 7 mục, khuôn `NOT-001-deploy.md`):
  - §1 Phạm vi: nhánh → `tich-hop/gd3` (G6 xanh mới nhận) → `main` là **vùng cấm** (biên bản người ký); module
    `Reporting` **4** project (có từ `WP-08`, WP-26 **không** thêm project/ref); **3** bảng `RepRunLogs` ·
    `RepConsistencyChecks` · `RepPeriodCloses`; migration `20261126181842_Wp26_VanHanh_BaoCao` = **4 index thường** +
    **4 unique `NULLS NOT DISTINCT`** partial + `btree_gist` + `EXCLUDE USING gist` (`D-171`); **10** op `/api/v1/reports/*`;
    **0** đổi `eCommerce.slnx`/`Directory.Packages.props`/`pnpm-lock.yaml`; **FE = N/A** (`D-276`).
  - §2 Cấu hình: **4 khoá thật** `Reporting:GioLechUtc=7` · `NhipJobPhut=15` · `NguongSapHet=5` ·
    `SoMatHangSapHetToiDa=20` (nơi đọc rõ); nhịp `ReportRebuildWorker` **hard-code 60 s**; ⚠️ **KHÔNG** có cờ
    `EnableRebuildWorker`/`EnablePeriodClose` trong code (arch §9.3(b) mới **đề xuất**) ⇒ **đường lùi phải revert code**.
  - §3 Thứ tự: `pg_dump -Fc` (K-01) → merge BE → build → `dotnet run --project src/eCommerce.DbMigrator` (**cấm**
    `dotnet ef database update`) → kiểm DB thật → deploy BE → FE N/A → kiểm sau; G6 để chỗ chờ handover N92.
  - §4 **Đường lùi** (bảng theo sự cố): rebuild sai/worker lỗi ⇒ bỏ đăng ký worker `ReportingApplicationModule.cs:39`
    hoặc revert; ghi kỳ đã chốt ⇒ cổng fail-closed `ChanNeuGiaoKyDaChotAsync` (`0011`, trước `DELETE`, 3 đường ghi);
    cấu hình sai ⇒ sửa `appsettings`; bỏ hẳn mã ⇒ `git revert` **6 SHA mới→cũ** `57cf660`→`4a964e6`→`608915d`→
    `7b82b53`→`0718f13`→`681c289`; **KHÔNG** `Down`/DROP (expand-only, giữ 3 bảng); `pg_restore` chỉ khi lùi toàn bộ;
    sau revert kiểm `GET /health-status` **200**.
  - §5 Cổng số đo thật + 10 route→quyền (4 quyền hẹp khai **2 nơi**) + §5.3 SQL kiểm DB (migration history · 3 bảng ·
    4 NND · `btree_gist`+`EXCLUDE`) + ca `EXCLUDE` chạy trong `BEGIN … ROLLBACK`.
  - §6 Khi hỏng ai gọi · §7 Nợ (11 dòng có chủ): R1 TOCTOU · R2 double-submit `23505`→500 · R3 `AlreadyRequested` ·
    BR-002 (`ba`) · BR-008/UoW harness · NFR chưa đo · **thiếu cờ tắt** · `NhipJobPhut` as-built lệch `D-172` (`D-273`) ·
    `D-184` (`REP-003`) · FE N/A (`D-276`) · merge `main` (chủ dự án).
- **`07` §13**: thêm **một dòng** WP-26/W26-5 (`N92`) với số đo G6 thật.
- **G6** — `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` chạy **tiến trình nền** (bài học `N90`: dự kiến > 10 phút):
  **exit 0 · 20/20 bước ĐẠT · 1448 s**, `stderr` rỗng, log có `TAT CA PHEP KIEM DAT`, **0** dòng `HONG`.

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error · 9 warning** (không warning mới) |
| Baseline `dotnet test test/eCommerce.Tests --no-build` | 0 | **2376/2376** · 0 skipped |
| Baseline `dotnet test test/eCommerce.IntegrationTests --no-build` | 0 | **139/139** |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 282 tệp · 965 link · 1700 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `ops` sau khi viết deploy doc: `kiem-link-tai-lieu.ps1` | 0 | **PASS** · 283 tệp · 965 link · 1707 backtick · 0 chết |
| **`pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (G6, nền)** | **0** | **20/20 ĐẠT · 1448 s** · stderr 0 B |
| ├ [1] dọn tiến trình + cổng | 0 | 2,1 s |
| ├ [2] build + mốc nhị phân | 0 | **0 error** · 126 tệp đổi mốc · 127,5 s |
| ├ [3] unit | 0 | **2376/2376** · 67,6 s |
| ├ [4] integration | 0 | **139/139** · 240,3 s |
| ├ [5] mermaid | 0 | **214 khối · 0 lỗi** · 82,6 s |
| ├ [6] cổng tài liệu WP-26 | 0 | **XANH** (`Tạm duyệt` đủ mã) · 1 s |
| ├ [7] FE typecheck + Vitest | 0 | **0 lỗi** · **45/45** · 23,5 s |
| ├ [8] FE `gen:api` | 0 | `schema.d.ts` **KHÔNG đổi** · 4,3 s |
| ├ [9] bất biến DB | 0 | **25/25 DAT · 0 HỎNG** · 12 s |
| ├ [10] test cổng | 0 | **60/60 DAT · 0 HỎNG** · 226,5 s |
| ├ [11] E2E đăng nhập | 0 | 130,7 s |
| ├ [12] Địa chỉ HTTP thật | 0 | 25,1 s |
| ├ [13] Khách hàng HTTP thật | 0 | 27 s |
| ├ [14] Thanh toán IPN | 0 | 23 s |
| ├ [15] Đơn sàn đường chạy thật | 0 | 49 s |
| ├ [16] Oversell 2 request đồng thời | 0 | **11 DAT / 0 HỎNG** · 36 s |
| ├ [17] Outbox process chết→phát lại | 0 | **9 DAT / 0 HỎNG** · 45,8 s |
| ├ [18] Outbox 2 instance | 0 | **9 DAT / 0 HỎNG** · 49,4 s |
| ├ [19] Giữ chỗ 2 instance | 0 | **18 DAT / 0 HỎNG** · 51,4 s |
| └ [20] Playwright 8 luồng | 0 | **8/8 passed** · 222,6 s |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W26-5`** — DoD của hàng đã đạt: F6 có `docs/ops/REP-001-deploy.md` kèm đường lùi · `07` §13 một dòng ·
G6 **exit 0 (20/20)** · push nhánh WP + fast-forward `tich-hop/gd3`. **WP-26 ĐÓNG.**

Các mục **chưa kiểm được** (giữ nguyên từ `N91`, **không** đánh dấu phủ — nay là nợ của WP kế/phiên dựng harness):
`BR-008`/`TC-09`/`TC-10`/`TC-05b`/`TC-22(runtime resume)` cần harness `DoWorkAsync` + UoW/tenant thật (`D-248`);
API 401/403 + 200 runtime mới kiểm metadata; NFR-001/002/003/006/009/010 chưa đo định lượng; UI/E2E/OpenAPI drift **N/A** (`D-276`).

## 3. Migration đã áp

- **Không có** migration mới trong phiên này (`W26-5` chỉ tài liệu + cổng). **0 `pg_dump`** (không chạm schema/dữ liệu).
- Migration của WP-26 đã áp **trước đó** ở `N89`: `20261126181842_Wp26_VanHanh_BaoCao` (3 bảng `Rep*`, 4 index thường,
  4 unique NND partial, `btree_gist` + `EXCLUDE`, expand-only) — `pg_dump` trước khi áp: `artifacts/pg-dump/N89-W26-2.dump`
  = **497.829 byte**. Cách lùi: xem `docs/ops/REP-001-deploy.md` §4 (revert code, **KHÔNG** Down/DROP, giữ 3 bảng).

## 4. Quyết định mới

- **Không có `D-xx` mới** (không gọi `decider` — F6/G6 không sinh câu hỏi mở).

## 5. Nợ / rủi ro

- 🟡 **P3 R1** — TOCTOU hẹp: cổng đọc và `DELETE` cùng UoW nhưng khoá khác nhau (rebuild vs chốt kỳ, SRS §8.2#7).
- 🟡 **P3 R2** — double-submit `StartRebuild` **đồng thời** vẫn có thể `23505` → 500 (chỉ lỗi HTTP, không hỏng dữ liệu).
- 🟡 **P3 R3** — `AlreadyRequested` gộp nghĩa "cùng yêu cầu" với "một rebuild **khác** đang chạy".
- 🟡 **P3 nợ `ba`** — BR-002 allow-list chưa nêu ngoại lệ "đối chiếu được đọc nguồn"; `TC02` chỉ quét `ReportAppService.cs`.
- 🟡 **BR-008/UoW chưa có test thật** — chủ: phiên dựng harness (khuôn `D-248`).
- 🟡 **Không có cờ tắt** rebuild/chốt kỳ bằng config (`arch` §9.3(b) mới đề xuất) ⇒ đường lùi phải **revert code**, không phải
  một dòng `.env` — đã ghi `REP-001-deploy.md` §2/§4/§7.
- 🟡 **`Reporting:NhipJobPhut = 15` as-built** lệch `D-172` (5 phút) + lookback `IsStale` — nợ có chủ `D-172`/`D-273`.
- 🟡 **`D-184`** tên permission `eCommerce.Reporting.Aggregation.Inspect` lệch quy ước — nợ `REP-003`.
- 🟡 **FE = N/A** (`D-276`): chưa có UI vận hành rebuild/chốt kỳ; dùng HTTP trực tiếp; nợ hàng contract `be` + màn `WEB-*`.
- **Cảnh báo hook**: `ops` bị báo "thiếu khối JSON" — false positive đã biết (báo cáo thực **có** khối JSON).
- **Chờ người (vùng cấm §6 hồ sơ uỷ quyền)**: (a) duyệt lô `D-270…D-276`; (b) kế toán R3-3 ký ngưỡng lý do `D-274`;
  (c) merge `main`. Ngoài ra người **xếp hàng** contract + WP màn hình `WEB-*` (`D-276` ⑤).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-26 → mở `W24-1` (WP-24 Promotions, F0–F2b).**

- Hàng kế của chuỗi `gd3-1` = **`W24-1`** (loại `code`, bước `F0-F2b`, module `Promotions` **mới**): đọc SRS theo mục, `arch` khi
  chạm ≥ 2 module / schema / contract; `G1/G2` theo M2; `Add_Promotion_Module` là nguồn schema duy nhất (`D-151`);
  `Q8` chỉ chọn nhánh điều kiện địa chỉ. Chỉ code khi `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-24` **exit 0**.
- Bằng chứng WP-26 sẵn có: G6 **20/20 · 1448 s**; unit **2376**; integration **139**; G4 **DAT**; G5 **PASS/P1=0**;
  deploy doc `docs/ops/REP-001-deploy.md`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp26-read-model`; base `origin/tich-hop/gd3`; HEAD trước phiên `2fe2e58`.
- Đã **push** nhánh WP (`git push origin feat/gd3-wp26-read-model`) rồi **fast-forward** lên **`tich-hop/gd3`**
  (`git push origin feat/gd3-wp26-read-model:tich-hop/gd3` — chỉ ff, không force/merge).
- **Không** mở PR (`D-216` ③); **không** đụng `main`.

---

**Hàng rào agent** (phiên `N92`): `ops` ×1 (F6). **0** lần chặn dừng; cảnh báo "thiếu khối JSON" của `ops` là false positive đã biết.
`git status` cuối phiên: sạch sau commit (`docs/**` + `agents/state/**`).
