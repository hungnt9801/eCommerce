# Handover N72 — WP-16 / hàng `W16-2` (F3 BE, 4 hạng mục) XONG

> Phiên `N72` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W16-2` · WP-16 · mã `PRC-002`
> · nhánh `feat/gd3-wp16-bang-gia` (base `origin/tich-hop/gd3` @ `e481094`) · 25/11/2026.
> Phạm vi phiên: **F3 BE** (4 hạng mục `D-218`) + BA sửa SRS cùng PR + DOCREV + test đơn vị/kiến trúc. **0 migration.**

## 1. Đã xong

- **F1/F1b — đồng bộ SRS `prc-002` với thiết kế** (`D-225`…`D-228`): §6.1 (hoãn `IRoundingPolicySettingAppService` + permission), §9 (đúng 4 khoá Setting của `D-218`), §12 (mã `0010`/`0011`/`0013`; chồng khung = `0013`), §20.3 (`Reject` mặc định NÉM / `Clamp`), §1 E6/E7 + thêm E12/E13, §3 dòng 159, §6.2 bảng mã lỗi, §14 TC-14/TC-19 (mã tiền tệ theo code `Money:0001/0003` — `D-228`), §1 A7, §4.2 B1, §6.2 route, §10 UI. **DOCREV vòng 3 + vòng 4 = `DOC-OK` (P1 = 0)**.
- **F3 BE — 4 hạng mục `D-218` (0 migration):**
  1. **Setting + hàm đọc thuần:** `PricingChinhSach.cs` (`PRC002SettingTenKhoa`, enum `ChinhSachTongDonAm {Reject=0, Clamp=1}`, 4 hàm đọc có `hopLe`) + `PricingSettingDefinitionProvider.cs` (4 Setting, phạm vi `Application | Tenant`, loại `User`).
  2. **Guard tổng âm `D-219`:** overload 5 tham số `TinhTienDon.Tinh`; `Reject` (mặc định, chữ ký cũ uỷ quyền) NÉM `eCommerce:Pricing:0010`; `Clamp` kẹp giảm cấp đơn ≤ thành tiền TRƯỚC phân bổ + `BiKep`/`SoKep` (`D-223`); tổng = 0 hợp lệ.
  3. **Ngưỡng số bậc `D-224`:** `TierPriceSetAppService.AddTierAsync` NÉM `eCommerce:Pricing:0011` khi vượt `Pricing:MaxTiersPerSet` (0/âm/sai ⇒ 20).
  4. **Tên 4 index NND:** thêm đích danh vào `phaiCo` (`PostgreSqlSchemaTests.cs`), đóng 2 chú thích nợ; tổng NND giữ **93**.
  - `PriceCalculationAppService`: `ISettingProvider` + `TaoChinhSachSnapshotAsync()` đọc 3 khoá **một lần** ở B1 + dòng `[CHINH-SACH-DA-DOC]` + `LogWarning` khi `BiKep`. Không đổi DTO/route/`schema.d.ts`.
- **Test (vai `qa` — chủ `test/**`):** thay tripwire tổng âm bằng **3a/3b/3c**; thêm `PricingChinhSachTests` (1a/1b), `PricingKienTrucTests` (3d), `PricingErrorCodesTests` (dải `0001…0011`). Số ca **1966 → 1999** (tăng 33).
- **ARCH đồng bộ 2 lệch kỹ thuật:** ABP 10.7 bỏ `SettingScopes` ⇒ `.WithProviders(DefaultValue|Configuration|Global|Tenant)`; namespace thật `eCommerce.Settings.ChinhSach`.

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 error · 9 warning** (= baseline, 0 mới) |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **1999/1999** (baseline 1966; +33) |
| `pwsh -File tools/kiem-tat-ca.ps1` | **0** | **10/10 bước · 331,8 s** (integration test chạy, 29,2 s; FE typecheck+Vitest; `gen:api` **không** đổi `schema.d.ts`) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 237 tệp `.md` · 931 link · 1455 backtick · 0 chết |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** · 111 dòng |
| `... -Kind docreview -Path docs/reviews/PRC-002-docreview.md` | 0 | **DAT** · `DOC-OK` · P1=0 · P2=1 |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` | 0 | XANH (đầu phiên) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** (baseline) |
| `PostgreSqlSchemaTests` (integration) | 0 | pass (52 ca; tổng NND = **93**) |

## 2. Chưa xong / cắt giữa

**Không có việc dở trong hàng `W16-2`.** WP-16 còn 3 hàng kế (không phải cắt giữa):

1. **`W16-3` (F3 FE):** đóng với bằng chứng **"WP không có FE"** — `apps/admin-web` không có route Pricing/tier-price; không đổi DTO ⇒ `schema.d.ts` không đổi (`gen:api` xác nhận trong cổng).
2. **`W16-4` (F4–F5):** negative control đỏ→xanh cho các ca mới; **3 ca tầng app service hoãn từ W16-2** (`1c` đọc Setting→`RoundingPolicyCode`; `2a/2b` guard số bậc qua `TierPriceSetAppService`) — repo test **chưa có** ABP test base/`UseSqlite`/fake `IRepository`; phép đo audit `D-222`; testplan; REV `PASS`.
3. **`W16-5` (F6–G6):** `-DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`.

## 3. Migration đã áp

- **Không có** — `D-218`② T6 chốt **0 migration**; `PRC-002-arch.md` §4 xác nhận. **Không** chạm DB → **không** cần `pg_dump`.
- Nếu W16-3/4 buộc sinh migration (ngoài dự kiến) ⇒ **dừng, hỏi decider** (`D-220`).

## 4. Quyết định mới (TẠM CHỐT DEC, uỷ quyền `D-114`)

| `D-xx` | Nội dung |
|---|---|
| `D-225` | Q11 — đồng bộ nghĩa mã `0010`/`0011`/`0013` ở §1/§3/§6.2/§14 SRS cùng PR; giá trị `RoundingPolicyCode` lạ ⇒ mặc định, không ném (`D-221`) |
| `D-226` | Q12 — dọn bề mặt chính sách làm tròn còn sót: bỏ route `GET/PUT /rounding-policy` + `RoundingPolicyDto` khỏi hợp đồng (HOÃN `D-222`); `MaxTiersPerSet` không vào snapshot |
| `D-227` | Q13 — hoãn sửa §14 (4 BR chưa có TC) + mục "Đối chiếu mô hình Nhanh"; `check-feature-doc -WP14` **không** phải cổng vào/DoD WP-16 — nợ BA |
| `D-228` | TC-14 dùng mã tiền tệ theo code (`0001`/`0003`), bỏ `0002`/`0005` + bỏ kết luận CHECK-DB cho bảng PRC-002; `XC-001` §6.2 lệch `MoneyErrorCodes.cs` ⇒ **nợ NGƯỜI** (danh sách đỏ §2.1) |

## 5. Nợ / rủi ro

- **Nợ NGƯỜI (danh sách đỏ §2.1) — `D-228`:** `XC-001` §6.2 gán `0002` = mã tiền tệ lạ, `0003` = tổng trọng số 0… **lệch** `MoneyErrorCodes.cs` (`0001` CurrencyMismatch · `0002` InvalidDivisor · `0003` UnsupportedCurrency · `0005` InvalidAmount). Chủ dự án sửa tài liệu theo code + thêm mục DOCREV; **hạn trước PR `tich-hop/gd3 → main` của WP-16**.
- **QA `D-217` ④ (chặn trước `W17-*` đầu tiên và trước PR `tich-hop/gd3 → main`):** sửa `probe-cong-tai-lieu.ps1` đọc biên bản GĐ1 + ca ngược; **cùng commit xoá** `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25`.
- **BA `D-227`:** thêm 4 dòng TC §14 cho `BR-PRC-002-010/011/013/014` + mục "Đối chiếu mô hình Nhanh" (khi có bằng chứng) — trước `doc-gate`/`duyet-dec` kế tiếp của `PRC-002`.
- **W16-4 — 3 ca tầng app service (1c/2a/2b):** repo test thiếu ABP test base/fake repo; phải dựng hạ tầng test (hoặc decider chọn cách khác) trước khi W16-4 đóng.
- **DOCREV vòng 4 — P2 không chặn BE:** `prc-002` §5.2/§8.1 còn khẳng định CÓ CHECK `^[A-Z]{3}$` trên `TierPriceSets`/`TierPrices`/`PriceCalculationRecords`, mâu thuẫn code (chỉ `PrcCostPrices` có CHECK). BA đánh dấu "kế hoạch — chưa triển khai" hoặc sửa tên cột. **P3:** §8.1 còn `Money.Of` (thật là `Money.Cua`); `-arch` §7 R7 còn nhắc `SettingScopes.Tenant`.
- **R8 còn treo:** xác nhận provider trong `Pricing.Domain` được ABP quét + đọc được 4 Setting ở **host + tenant** — cần **khởi động Host thật** (M6), không chứng minh được bằng build/test; W16-4 chạy.
- **Release chặn tới khi kế toán duyệt lô M4** cho `D-219`/`D-221`…`D-224`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG hàng `W16-2` (WP-16 F3 BE) → mở hàng `W16-3` (F3 FE).**

- `/chay-phien-code chuoi=gd3-1` (bộ chạy tự chọn `W16-3`).
- `W16-3` đóng với bằng chứng **"WP không có FE"**: `apps/admin-web` không có route Pricing/tier-price; `pnpm gen:api` **không** đổi `schema.d.ts`.
- Nhắc: **không** xoá tệp gương trước khi QA sửa probe (`D-217`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp16-bang-gia`, base `origin/tich-hop/gd3` @ `e481094`.
- Commit chính: **`100db0a`** — 19 tệp (7 `.cs` sản phẩm + 5 tệp test + SRS/arch/docreview + decisions/queue). Đã push `origin/feat/gd3-wp16-bang-gia`.
- Không mở PR (`D-216`③: PR xếp chồng mở cuối chuỗi). Commit đóng phiên (handover này + `07` §13 + registry) đi **sau** trên cùng nhánh.

---

**Hàng rào agent** (phiên `N72`): **ba ×3 · decider ×2 · docrev ×2 · be ×1 · qa ×1 · arch ×1** = **10 lượt**. **0** lần chặn dừng. **0** tệp ghi ngoài vùng thật (1 cảnh báo `docSection` của `be` là **dương tính giả**: dòng §1 A7 do `ba` sửa hợp lệ trước đó, `be` chỉ append §22). **3** cảnh báo thiếu khối JSON cuối (`docrev` ×2 cũ/mới, `qa` ×1) — đều đã trả JSON qua hand-back. `decider` lần 1 chạm trần 40 lượt **sau khi đã ghi** `D-225`…`D-227` (không mất việc). `/usage`: chưa đo.
