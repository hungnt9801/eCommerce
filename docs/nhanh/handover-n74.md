# Handover N74 — WP-16 / hàng `W16-4` (F4–F5) XONG

> Phiên `N74` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W16-4` · WP-16 · mã `PRC-002`
> · nhánh `feat/gd3-wp16-bang-gia` (base `origin/tich-hop/gd3` @ `e481094`) · 26/11/2026.
> Phạm vi phiên: **F4 QA** (testplan + 3 ca app service + negative control) → **F5 REV** (`PASS`, P1 = 0).
> **0 `.cs` sản phẩm, 0 migration, 0 package** (W16-4 chỉ thêm test + testplan + review).

## 1. Đã xong

- **F4 QA (`D-229`)**:
  - `docs/tests/PRC-002-testplan.md` — phủ **21/21 BR** `BR-PRC-002-001..021`; `check-agent-artifact -Kind testplan` **DAT**.
  - Hạ tầng test tầng app service `D-229` (A): `test/eCommerce.Tests/Pricing/HaTang/{RepoGia,SettingGia,HaTangAbp}.cs`; thêm **1 dòng** `ProjectReference eCommerce.Pricing.Application` vào `test/eCommerce.Tests/eCommerce.Tests.csproj`.
  - **3 ca hoãn từ W16-2 đã xong**: `1c` (`PriceCalculationAppServiceTests`) — mỗi khoá policy đọc đúng **1 lần**/lần tính, `DungGiaNiemYet`/`NemLoi`, idempotency, sổ append-only; `2a`/`2b` (`TierPriceSetAppServiceTests`) — bậc 20 OK/21 NÉM `0011`, **`SoLanChen = 0`**, `"3"` ⇒ bậc 4 bị chặn, `"abc"`/`""` ⇒ 20.
  - **Negative control 4/4 đỏ→xanh** (`tools/agents/negative-control.ps1`, `TestCommand` có build lại): NC-1 `PriceCalculationAppService.cs:280` · NC-2 `TierPriceSetAppService.cs:121` · NC-3 `TinhTienDon.cs:195` · NC-4 `PostgreSqlSchemaTests.cs:399` — mỗi ca **exit 1 (đỏ) → khôi phục → exit 0 (xanh)**; `git status --porcelain -- modules/ src/` rỗng.
  - Test **+22** (1999 → **2021/2021**), không xoá/skip/nới.
- **F5 REV**: `docs/reviews/PRC-002-review.md` — **`VERDICT: PASS`, P1 = 0** (1 P2, 5 P3); `check-agent-artifact -Kind review` **DAT**.
  - P2 #1: `PricingChinhSach.DocSoBacToiDa` (`:113`) ép `decimal → int` không chặn trần ⇒ `MaxTiersPerSet > 2.147.483.647` (đường lùi "số rất lớn") ném `OverflowException` → **500**; không chạm tiền/tồn/quyền/idempotency ⇒ **không chặn merge**, ghi nợ.
  - P3 #2–#6: XML doc `TinhTienDon.cs:84-85` ghi ngược; allowlist khai nhưng reader dùng literal; `TierPriceSetAppService` bỏ `out _` (mất cảnh báo); log đường nóng; `PricingSettingDefinitionProvider` chưa có ca phạm vi (nợ kế thừa R8/`D-217`④).
- **Quyết định**: `D-229` (TẠM CHỐT, sửa footnote ABP 10.7: `FirstOrDefaultAsync`/`CountAsync` đi qua `GetQueryableAsync` + `AsyncExecuter`) · `D-230` (TẠM CHỐT: **không** sửa SRS trong W16-4; 4 `docDeviation` thành nợ có chủ BA/ARCH).
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-tat-ca.ps1` | 0 | **10/10 bước · 287,4 s** |
| └ Build | 0 | **0 error · 9 warning** (= baseline, 0 mới) |
| └ Unit test | 0 | **2021/2021** (+22) |
| └ Integration test | 0 | **96/96** |
| └ Mermaid | 0 | **213 khối · 0 lỗi** |
| └ Cổng tài liệu WP-16 | 0 | **XANH** |
| └ FE typecheck + Vitest | 0 | **0 lỗi · 25/25** (4 tệp) |
| └ `gen:api` | 0 | **`schema.d.ts` không đổi** |
| └ Bất biến DB | 0 | **25/25** |
| └ Test cổng kiểm | 0 | **60/60** |
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 error · 9 warning** |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2021/2021** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 241 tệp · 933 link · 1471 backtick · **0 chết** |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` | 0 | **XANH** (đầu phiên) |
| `check-agent-artifact -Kind testplan` | 0 | **DAT** (BR=21, thiếu=0) |
| `check-agent-artifact -Kind review` | 0 | **DAT** (`PASS`, P1=0) |
| `check-agent-artifact -Kind decision` | 0 | **DAT** · 114 dòng |

## 2. Chưa xong / cắt giữa

**Không có việc dở trong hàng `W16-4`.** WP-16 còn **1 hàng kế** (không phải cắt giữa):

1. **`W16-5` (F6–G6):** `ops` chạy `pwsh -File tools/kiem-tat-ca.ps1 -DayDu`, viết tài liệu deploy `PRC-002` trong `docs/ops/` có đường lùi, rồi **push fast-forward** `feat/gd3-wp16-bang-gia` → `tich-hop/gd3`.
2. **P2/P3 của REV** (mục 5) — ghi nợ, không chặn `W16-5`.

📌 Ghi nhận vận hành: QA và REV mỗi vai **chạm trần lượt** một lần (QA 60, REV 30) và được `SendMessage` cho chạy tiếp; **không** phải hook chặn, **không** mất artifact.

## 3. Migration đã áp

- **Không có** — `D-218`② giữ **0 migration**; phiên này **0 `.cs` sản phẩm**. Không chạm DB → **không** cần `pg_dump`.

## 4. Quyết định mới

- **`D-229`** (TẠM CHỐT — DEC; sửa footnote trong phiên): hạ tầng test tầng app service chọn **(A)** fake in-memory + `ISettingProvider` giả + `LazyServiceProvider` gán tay; **không** Sqlite/host ABP. Footnote: ABP 10.7 dựng `FirstOrDefaultAsync`/`CountAsync` trên `GetQueryableAsync` + `AsyncExecuter`.
- **`D-230`** (TẠM CHỐT — DEC): **không** gọi BA sửa SRS trong `W16-4`; 4 `docDeviation` (Kind/PercentOff/ExternalRef + CHECK; `Money.ToMinorUnits`; 6 method §6.1 + TC-25/26/27; nhãn "CHƯA có trong code" + `Money.Of`) thành **nợ có chủ BA/ARCH**, hạn trước `W17-1` / doc-gate kế tiếp / trước PR `tich-hop/gd3 → main`.
- `check-agent-artifact -Kind decision` **DAT** · 114 dòng.

## 5. Nợ / rủi ro

Kế thừa `N73`, còn nguyên (trừ các mục đã đóng trong phiên):

- **P2 REV #1** (`D-229` phạm vi review): `DocSoBacToiDa` tràn `int` khi `MaxTiersPerSet > 2.147.483.647` ⇒ 500. Chủ: BE (`W16-5` hoặc WP kế); không chặn merge.
- **P3 REV #2–#6**: XML doc ngược (`TinhTienDon.cs:84-85`), allowlist không được reader dùng, mất `LogWarning` khi setting sai ở `AddTierAsync`, log đường nóng, thiếu ca `PricingSettingDefinitionProvider` (R8).
- **`D-230`** — 4 `docDeviation` SRS↔code: chủ **BA** (+ **ARCH** cho `-arch`), hạn trước `W17-1`/doc-gate kế tiếp/trước PR `tich-hop/gd3 → main`.
- **`D-217`④** (QA): sửa `probe-cong-tai-lieu.ps1` đọc biên bản GĐ1 + ca ngược; **cùng commit xoá** tệp gương — chặn trước `W17-*` đầu tiên và trước PR `tich-hop/gd3 → main`.
- **`D-228`** (NGƯỜI — danh sách đỏ §2.1): `XC-001` §6.2 lệch `MoneyErrorCodes.cs`.
- **`D-227`** (BA): thêm 4 dòng TC §14 + mục "Đối chiếu mô hình Nhanh" cho `BR-PRC-002-010/011/013/014` — `check-feature-doc -WP14` **vẫn đỏ**, không phải cổng WP-16.
- **R8 còn treo:** provider trong `Pricing.Domain` + đọc 4 Setting ở **host + tenant** cần **khởi động Host thật** (M6).
- **M4 trước release:** kế toán duyệt lô cho `D-219`/`D-221`…`D-224`.
- **127 quyết định tạm** của decider đang chờ người duyệt (`docs/agents/hang-doi-xem-lai.md`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG hàng `W16-4` (F4–F5) → mở hàng `W16-5` (F6–G6).**

- `/chay-phien-code chuoi=gd3-1` (bộ chạy tự chọn `W16-5`).
- `W16-5`: `ops` chạy `kiem-tat-ca.ps1 -DayDu`; tài liệu deploy `PRC-002` trong `docs/ops/` có đường lùi; `07` §13 một dòng; **push fast-forward** nhánh WP → `tich-hop/gd3` (không `--force`, không merge `main`).
- Nhắc: **không** xoá tệp gương trước khi QA sửa probe (`D-217`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp16-bang-gia`, base `origin/tich-hop/gd3` @ `e481094`.
- HEAD trước phiên: **`99888c6`**. Các commit phiên `N74`: `70641e4` (F4 pass 1) · `685b659` (F4 pass 2 + D-229/D-230) · `798cca9` (review PASS). Commit đóng phiên (handover này + `07` §13 + registry) trên **cùng nhánh**; SHA ghi ở câu trả lời cuối.
- Đã push `origin/feat/gd3-wp16-bang-gia`. **Chưa** push `tich-hop/gd3` (chỉ ở `W16-5`). Không mở PR (`D-216`③).

---

**Hàng rào agent** (phiên `N74`): **3 vai** — `decider` (2 lượt: `D-229`, rồi `D-229` sửa + `D-230`), `qa` (2 pass, 4/4 NC), `rev` (1 review, `PASS`); **0** lần hook chặn dừng; **0** tệp ghi ngoài vùng (`qa` → `test/**` + `docs/tests/**`; `rev` → `docs/reviews/*-review.md`; `decider` → `decisions.md` + `hang-doi-xem-lai.md`). QA/REV chạm trần lượt và được chạy tiếp, không mất việc.
