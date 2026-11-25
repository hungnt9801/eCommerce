# Handover N81 — WP-18 / hàng `W18-5` (F6 OPS + G6) XONG: `-DayDu` 20/20, deploy docs + 2 P2 đóng, push `tich-hop/gd3`

> Phiên `N81` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W18-5` · WP-18 · mã `ADDR-001`, `ADDR-002` · nhánh
> `feat/gd3-wp18-dia-chi` (base `origin/tich-hop/gd3` = `7d15f84`) · 26/11/2026.
> Phạm vi phiên: **F6 OPS** (2 tài liệu triển khai + đường lùi) → **G6** (`kiem-tat-ca.ps1 -DayDu`) →
> **push fast-forward** `feat/gd3-wp18-dia-chi` → `tich-hop/gd3`. Trong cửa sổ BE được `D-250` ⑤ cho phép: đóng
> **2 P2** của `docs/reviews/ADDR-001-review.md`. **Không** code FE/màn `WEB-001` (`D-247`), **không** mở `/api/v1`
> (`D-245`), **không** adapter GHN/GHTK (`R3-1`), **không** PII/`ADDR-003`.

## 1. Đã xong

- **F6 OPS — 2 tài liệu triển khai** (khuôn 6 mục `docs/ops/WP-15-deploy.md`):
  - `docs/ops/ADDR-001-deploy.md` (125 dòng) — `Address:Cache:Enabled`/`Address:Cache:TtlSeconds` (ABP Setting, mặc định
    **86400**), `Address:Sync:Enabled` (**false**)/`Address:Sync:SchemaVersion`/`Address:Sync:PeriodSeconds`; migration
    `Add_Addr001_CatalogSyncRuns` (expand-only); đường lùi = tắt cờ + `git revert` commit WP-18.
  - `docs/ops/ADDR-002-deploy.md` (141 dòng) — `Address:CarrierSync:Enabled` (**false**)/`PeriodSeconds`/`Carriers`;
    migration `Add_Addr002_CarrierSyncRuns` (2 bảng, unique `NULLS NOT DISTINCT`); đường lùi tương tự; **mục
    "Kiểm trước go-live (403 — `D-252`, nợ `D-249`)"**: user thiếu `Addresses.CarrierMapping.Verify` ⇒ **403**; hôm nay
    **chưa đo được** (`/api/identity/**` = 404), probe in `CHUA DO`, không ghi "403 OK".
- **Đóng 2 P2 REV `ADDR-001` (commit `cea05f7`):**
  - P2 #1 / `D-250`: **xoá** overload `AdministrativeUnitSynchronizer.ApDung(keHoach, tenantId)` 1 tham số
    (`AdministrativeUnitSynchronizer.cs:307-315` cũ). Kiểm: `grep "ApDung("` toàn repo — **0** lời gọi 1 tham số.
  - P2 #2: `SourceName` > 512 (`varchar(512)`) — `CatalogSyncRun.Ghi` dùng `CatChuoi(sourceName, DoDaiSourceNameToiDa=512, …)`
    **ném** `ArgumentException` (không cắt im lặng) cho nhánh worker; `ImportDatasetAsync` validate `Trim().Length > 512`
    **trước khoá** ⇒ `BusinessException(AddressErrorCodes.MaKhongHopLe)` (`0002`/`400`), **0** dòng lô rác. Vá **trước**
    commit UoW ⇒ hết **500**.
- **QA (commit `d73b552`):** **+4** ca unit (**2166 → 2170**): 513 ký tự ⇒ `0002` + 0 dòng lô + chưa chiếm khoá; biên
  **512** vẫn nạp được; `CatalogSyncRun.Ghi` 513 ⇒ `ArgumentException`; `D-252` reflection `[Authorize]` **cấp method**
  của `CarrierAddressMappingAppService.XacMinhAsync` = `Addresses.CarrierMapping.Verify` + định nghĩa side `Host`.
  **4 negative control đỏ→xanh** (NC-9/NC-10/NC-11/NC-6), `git status --porcelain -- modules/ src/ shared/` **rỗng** sau mỗi lượt.
- **Đồng bộ tài liệu feature (cùng PR):** `ba` sửa SRS `ADDR-001` §6.2/§6.3/§16 (commit `c5c9466`); `arch` sửa
  `ADDR-001-arch.md` §3.6/§5.5/§5.6/§6/§9 + header (commit `b71e1c6`).
- **`decider` `D-252`** (commit `c0074d2`): không thêm `Volo.Abp.Identity.HttpApi`; giữ phủ reflection + ca mới;
  nợ 403 dời hạn `W18-5` → **release**; `ops` ghi mục kiểm 403 ở deploy doc (commit `4fbdc96`).
- **G6 — `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` exit 0 — 20/20 bước · 708,8 s** (log `artifacts/dayDu-n81.txt`).

**Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2166/2166** |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | PASS — 257 tệp · 950 link · 1562 backtick |
| Baseline `npm --prefix tools run check:mermaid` | 0 | **213 khối · 0 lỗi** |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49 pass / 0 fail** |
| `dotnet build eCommerce.slnx --no-incremental` (sau `be`) | 0 | **0 error / 9 warning** (0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` (sau `qa`) | 0 | **2170/2170** (+4) |
| `dotnet test test/eCommerce.IntegrationTests --no-build` (trong `-DayDu`) | 0 | **106/106** |
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | **0** | **20/20 bước · 708,8 s** (build 70,2 s · unit 17,9 s · integration 28 s · mermaid 213/0 · cổng tài liệu XANH · FE typecheck+Vitest · `gen:api` không đổi · DB 25/25 · test cổng 60/60 · ADDR/CUS/PAY/SAL · oversell · outbox ×2 · Playwright 8/8) |
| `check-agent-artifact.ps1 -Kind testplan -Path … -Srs …` ×2 | 0 | **DAT** — 14/14 và 16/16 BR, `chua co trong testplan=0` |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** (116 dòng theo mẫu `(DEC)`; `D-252` dùng `(DEC, uỷ quyền)` không thuộc mẫu đếm) |
| `check-feature-doc.ps1 -Path …addr-001… -WP14` | 0 | **DAT** — 16/16 heading · 14/14 BR · đối chiếu Nhanh |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau handover) | 0 | PASS — 260 tệp · 951 link · 1596 backtick · 0 chết |
| `node --test .claude/hooks/test/hooks.test.mjs` (cuối phiên) | 0 | **49 pass / 0 fail** |

## 2. Chưa xong / cắt giữa

**DoD `W18-5` đạt:** deploy docs có đường lùi · G6 xanh 20/20 · push fast-forward (ghi §7). Không có việc dở giữa chừng.

Các mục **cắt có nhận chủ**:

- **403 runtime vẫn chưa đo được** — `D-252` thay hạn `D-249`: chủ `qa`, hạn **release** (cổng release đỏ tới khi người
  duyệt); gỡ khi Host có `/api/identity/**` hoặc ở hàng BE/contract `WEB-001` (`D-245`/`D-247`). Phủ hiện tại: reflection
  `D252_XacMinhAsync_co_Authorize_dung_permission_Verify`; probe giữ `CHUA DO`, **không** đánh dấu ✅.
- **Màn vận hành `WEB-001`** (2 màn + nạp danh mục) → `D-247` **CHUYỂN NGƯỜI**.
- **`ADDR-002-arch` §6 + `ADDR-001-arch` ghi `/api/app/…`** → `D-245` ⑤: sửa trước hàng F2 `WEB-001`.
- **P2 #3 REV**: phạm vi code phi-migration 3.416 dòng/37 tệp > K-04 ~600 — **chấp nhận theo chuỗi WP-18** (`D-216`),
  mô tả rõ khi mở PR.
- **9 P3 REV** (2 tệp review): doc-comment bị cắt, `<summary>` mở thiếu (`eCommerceMetrics.cs:38`), `AddressCacheInvalidatedEto`
  chưa `INotificationEvent`, testplan lệch số đo (đã thành 106), `TienToKhoa` inline, SRS `ADDR-002` BR-013/014 còn câu cũ,
  log sau commit trong worker nằm trong `try`. Chủ `be`/`ba`, không chặn.
- **Hardening BFF `D-246`** (41 hit `api/proxy`) — chủ `fe`, chậm nhất trước PR `tich-hop/gd3 → main`.
- **NFR-001 nóng là cận dưới** (fake in-memory, bỏ RTT Redis) — đo lại trên Redis thật khi có môi trường.
- **Cảnh báo hook trong phiên:** 3 vai (`decider`, `ba`, `arch`) bị báo "thiếu khối JSON cuối" do harness không cấp
  `last_assistant_message` đúng lúc — **khối JSON có** trong báo cáo hand-back; **0** artifact ảnh hưởng, **0** lần chặn dừng.
- **Bài học vận hành (mới):** lần đầu gọi **3 vai song song** ⇒ hook so `git status` của vai này thấy tệp của vai khác,
  sinh cảnh báo "ghi ngoài vùng" **giả**. Đã xác minh tay: chỉ `be` thật sự vượt vùng (sửa `ADDR-001-arch.md` và mục "Mã lỗi"
  SRS) ⇒ `git restore` 2 tệp đó rồi `ba`/`arch` làm lại đúng vai. **Quy ước phiên sau: gọi vai TUẦN TỰ, commit sau mỗi vai.**

## 3. Migration đã áp

**Không có migration mới.** Phiên này **0** `.cs` migration, **0** `pg_dump` (không chạm schema). DB dev giữ trạng thái
`N78` (`Add_Addr001_CatalogSyncRuns`, `Add_Addr002_CarrierSyncRuns` đã áp sau `pg_dump` 197.121 B). Cách lùi: không cần —
không có thay đổi schema/dữ liệu trong phiên.

## 4. Quyết định mới

- **`D-252`** (TẠM CHỐT, DEC) — **KHÔNG** thêm `Volo.Abp.Identity.HttpApi` vào Host; **chấp nhận** phủ 403 bằng reflection
  + thêm **1 ca reflection** khoá `[Authorize]` cấp method của `XacMinhAsync` (`qa`); giữ probe `CHUA DO`, cấm ghi "403 OK";
  nợ `D-249` dời hạn **release** (chủ `qa`); `ops` thêm mục "Kiểm trước go-live (403)" ở `ADDR-002-deploy.md`.
  Ghi ở `docs/nhanh/decisions.md` (dòng 266) + `docs/agents/hang-doi-xem-lai.md` (dòng 20).
- `check-agent-artifact.ps1 -Kind decision` **DAT**.

## 5. Nợ / rủi ro

- **Cổng release sẽ ĐỎ** cho tới khi đo được 403 thật (`D-252` → hàng đợi `Xem lại trước = release`). Không phải lỗi sản phẩm;
  là lỗ hổng **lưới test** — nay đã có ca reflection chặn việc xoá `[Authorize]` cấp method.
- **`D-250`/P2 #2 đã đóng**, không còn nợ `be` cho WP-18. Không có nợ chặn merge `tich-hop/gd3`.
- **Push fast-forward** phải **chỉ** FF; nếu `tich-hop/gd3` đã đi tiếp ⇒ **không** force, báo người (`PH dodang`).
- **Hàng đợi xem lại:** +1 dòng (`D-252`) — tổng **150** quyết định tạm chờ người duyệt.
- **Hook concurrency** ở trên: phiên sau không nên chạy nhiều vai cùng lúc.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-18 → mở `W22-0` (WP-22, cổng tài liệu `D-208`).**

- `W22-0`: `ba` sửa 2 P2 #24/#26 của `PRC-002` → DOCREV kiểm nhanh **trước** `migrations add`/state machine; 15 P3 làm cùng WP;
  `decider` tạm duyệt khi mã không đỏ. Hàng tiếp theo trong `docs/agents/bang-phien-gd3-1.md`.
- **Trước PR `tich-hop/gd3 → main`** (người mở cuối chuỗi): mô tả PR theo `W18-1`…`W18-5` (P2 #3), đóng `D-246` (BFF) và ghi
  nợ 403 `D-252`; **không** để `D-252` trôi qua release mà không ghi.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp18-dia-chi`, base `origin/tich-hop/gd3` (`7d15f84`); mở phiên `HEAD` `f574512`.
- Commit phiên: `cea05f7` (vá 2 P2) · `c0074d2` (`D-252`) · `2f200fa` (deploy docs) · `e8f9f53` (registry PH start) ·
  `d73b552` (test + testplan) · `4fbdc96` (403 go-live) · `c5c9466` (SRS) · `b71e1c6` (arch) · **commit đóng phiên**
  (handover này + `07` §13 + registry).
- **Đã push** `origin/feat/gd3-wp18-dia-chi`; **đã push fast-forward** `feat/gd3-wp18-dia-chi` → `tich-hop/gd3`
  (G6 xanh — SHA ghi ở câu trả lời cuối).
- Không mở PR (`D-216` ③); PR xếp chồng mở một lần cuối chuỗi.

---

**Hàng rào agent** (phiên `N81`): **7 lượt vai** — `decider` ×1 (`D-252`), `be` ×1 (2 P2), `ops` ×2 (2 deploy docs + mục 403),
`qa` ×1 (4 ca + 4 NC + 2 testplan), `ba` ×1 (SRS), `arch` ×1 (arch). **0 lần chặn dừng**; 1 lần `be` ghi ngoài vùng (2 tài liệu)
đã `git restore` + làm lại đúng vai; 3 cảnh báo "thiếu JSON" của harness (khối JSON thật có).
