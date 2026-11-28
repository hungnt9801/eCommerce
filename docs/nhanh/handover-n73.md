# Handover N73 — WP-16 / hàng `W16-3` (F3 FE) XONG — **WP không có FE**

> Phiên `N73` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W16-3` · WP-16 · mã `PRC-002`
> · nhánh `feat/gd3-wp16-bang-gia` (base `origin/tich-hop/gd3` @ `e481094`) · 25/11/2026.
> Phạm vi phiên: **F3 FE** — xác nhận WP không có phần FE theo SRS; **0 `.cs`, 0 `.tsx`, 0 migration.**

## 1. Đã xong

- **F3 FE — kết luận "WP không có phần FE", bằng chứng:**
  - SRS `PRC-002` §10 ghi rõ: *"**Có UI** (thuộc `WEB-001` thực hiện; `PRC-002` chốt **mapping dữ liệu + quy tắc hiển thị**)"* ⇒ màn Pricing/tier-price **không** thuộc WP-16; WP-16 chỉ chốt hợp đồng mapping.
  - `apps/admin-web`: **23** route (`find app -name page.tsx -o -name route.ts`), chỉ product/order/pos/inventory/customer/report/settings + auth/health/proxy — **không** có route Pricing/tier-price.
  - `grep -rniE "pricing|tierprice|tier-price|gia-bac-thang|PRC-002|bang-gia" apps/admin-web/app apps/admin-web/components apps/admin-web/lib` ⇒ **0** hit trong mã FE (chỉ có `packages/api-client` schema — hợp đồng BE).
  - W16-2 **không** đổi DTO/route ⇒ `schema.d.ts` không đổi; `pnpm gen:api` xác nhận trong cổng (bước 8).
- **Đo lại (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-tat-ca.ps1` | 0 | **10/10 bước · 290,5 s** |
| └ Build | 0 | **0 error · 9 warning** (= baseline, 0 mới) |
| └ Unit test | 0 | **1999/1999** (không đổi) |
| └ Integration test | 0 | **96/96** |
| └ Mermaid | 0 | **213 khối · 0 lỗi** |
| └ Cổng tài liệu WP-16 | 0 | **XANH** |
| └ FE typecheck (`tsc --noEmit`) | 0 | **0 lỗi** |
| └ FE Vitest | 0 | **25/25** (4 tệp) |
| └ `gen:api` | 0 | **`schema.d.ts` không đổi** |
| └ Bất biến DB | 0 | **25/25** |
| └ Test cổng kiểm | 0 | **60/60** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 239 tệp · 933 link · 1459 backtick · **0 chết** |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` | 0 | **XANH** (đầu phiên) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** · 112 dòng |

## 2. Chưa xong / cắt giữa

**Không có việc dở trong hàng `W16-3`.** WP-16 còn 2 hàng kế (không phải cắt giữa):

1. **`W16-4` (F4–F5):** negative control đỏ→xanh cho các ca mới; **3 ca tầng app service hoãn từ W16-2** (`1c` đọc Setting→`RoundingPolicyCode`; `2a/2b` guard số bậc qua `TierPriceSetAppService`) — repo test **chưa có** ABP test base/`UseSqlite`/fake `IRepository`; phép đo audit `D-222`; testplan; REV `PASS`.
2. **`W16-5` (F6–G6):** `-DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`.

## 3. Migration đã áp

- **Không có** — `D-218`② T6 chốt **0 migration**; phiên này **0 `.cs`**. Không chạm DB → **không** cần `pg_dump`.

## 4. Quyết định mới

- **Không có `D-xx` mới.** `check-agent-artifact.ps1 -Kind decision` **DAT** · 112 dòng.

## 5. Nợ / rủi ro

Kế thừa `N72`, còn nguyên:

- **Nợ NGƯỜI (danh sách đỏ §2.1) — `D-228`:** `XC-001` §6.2 gán `0002` = mã tiền tệ lạ, `0003` = tổng trọng số 0… **lệch** `MoneyErrorCodes.cs` (`0001` CurrencyMismatch · `0002` InvalidDivisor · `0003` UnsupportedCurrency · `0005` InvalidAmount). Chủ dự án sửa tài liệu theo code + thêm mục DOCREV; **hạn trước PR `tich-hop/gd3 → main` của WP-16**.
- **QA `D-217` ④ (chặn trước `W17-*` đầu tiên và trước PR `tich-hop/gd3 → main`):** sửa `probe-cong-tai-lieu.ps1` đọc biên bản GĐ1 + ca ngược; **cùng commit xoá** `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25`.
- **BA `D-227`:** thêm 4 dòng TC §14 cho `BR-PRC-002-010/011/013/014` + mục "Đối chiếu mô hình Nhanh" trước `doc-gate`/`duyet-dec` kế tiếp của `PRC-002`.
- **`W16-4` — 3 ca tầng app service (`1c`/`2a`/`2b`):** repo test thiếu ABP test base/fake repo; phải dựng hạ tầng test (hoặc decider chọn cách khác) trước khi `W16-4` đóng.
- **DOCREV vòng 4 — P2 không chặn BE:** `prc-002` §5.2/§8.1 còn khẳng định CÓ CHECK `^[A-Z]{3}$` trên `TierPriceSets`/`TierPrices`/`PriceCalculationRecords`, mâu thuẫn code (chỉ `PrcCostPrices` có CHECK). **P3:** §8.1 còn `Money.Of` (thật là `Money.Cua`); `-arch` §7 R7 còn nhắc `SettingScopes.Tenant`.
- **R8 còn treo:** xác nhận provider trong `Pricing.Domain` được ABP quét + đọc được 4 Setting ở **host + tenant** — cần **khởi động Host thật** (M6); `W16-4` chạy.
- **Release chặn tới khi kế toán duyệt lô M4** cho `D-219`/`D-221`…`D-224`.
- **UI màn Pricing theo SRS §10 thuộc `WEB-001`** — đây là **lý do** `W16-3` đóng "không có FE"; chuỗi `gd3-1` **chưa có hàng UI** cho màn này. Ghi nhận để không hiểu nhầm là nợ của WP-16; **không chặn** `W16-4`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG hàng `W16-3` (WP-16 F3 FE) → mở hàng `W16-4` (F4–F5).**

- `/chay-phien-code chuoi=gd3-1` (bộ chạy tự chọn `W16-4`).
- `W16-4`: QA phủ mọi BR + negative control đỏ→xanh; REV `PASS`; dựng hạ tầng test app-service (hoặc decider chọn cách khác) cho 3 ca `1c/2a/2b`.
- Nhắc: **không** xoá tệp gương trước khi QA sửa probe (`D-217`).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp16-bang-gia`, base `origin/tich-hop/gd3` @ `e481094`.
- HEAD trước phiên: **`ce5fe41`**. Commit đóng phiên `N73` (handover này + `07` §13 + registry) trên **cùng nhánh**; SHA ghi ở câu trả lời cuối.
- **Chưa** push `tich-hop/gd3` (chỉ ở `W16-5`). Không mở PR (`D-216`③: PR xếp chồng mở cuối chuỗi).

---

**Hàng rào agent** (phiên `N73`): **0 lượt vai** — hàng `W16-3` đóng bằng xác nhận "WP không có FE", ORCH tự chạy cổng G3; **0** lần chặn dừng; **0** tệp ghi ngoài vùng.
