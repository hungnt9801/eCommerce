# Handover phiên N99 — `W25-0` (WP-25 · `PRO-001`) · chuỗi `gd3-1`

> Nhánh `feat/gd3-wp25-voucher` · base `origin/tich-hop/gd3` @ `e7aa32c` · bước **cổng tài liệu `D-169`**.
> Kết quả: **W25-0 XONG** — `PRO-001` lên **v0.10**, DOCREV **`DOC-OK` vòng 5 · 0 P1**, **P2 #20 đã đóng**;
> `probe-cong-tai-lieu -CheDo Code -WP WP-25` **XANH** ⇒ cổng code của WP-25 mở.

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| D1 BA đối chiếu | `ba` | `PRO-001` v0.9: **P2 #20** (bước 7 / `BR-04` / §5.2 `UsageSequence` / skeleton `TC08` + **`TC-68`**): `MaxUsagePerCustomer = NULL` ⇒ **không trần theo khách**, vẫn cấp `UsageSequence` = số nhỏ nhất chưa bị lượt còn hiệu lực giữ trong `[1, ∞)`, `23505` do đua ⇒ thử lại có trần, **CẤM** gán `1`. P3 **#12, #21–#25** đóng |
| D1q decider | `decider` | **`D-295`** trần thử cấp số = **5 lần THỬ** (1 ghi + 4 thử lại, `PromotionOptions.MaxUsageSequenceAttempts = 5`); hết 5 ⇒ `0104` + `LogWarning` + metric `usage_sequence_exhausted` |
| D1 BA áp D-295 | `ba` | Ghi số 5 vào bước 7 / `BR-04` / `E4` / `TC-68` |
| D2 DOCREV kiểm nhanh | `docrev` | Vòng 4: **`DOC-CHANGES`** · 1 P1 (#26 `0109` trái as-built) · 1 P2 (#27 trỏ nhầm dòng §6.1) · 1 P3 (#28 heading) |
| D1q decider | `decider` | **`D-296`** chọn (a) doc-only: mô tả as-built — `CreateAsync`/`Promotion.TaoMoi` ném `AbpValidationException` (400, không `Code`), giữ số `0109` "v1 chưa có đường ném"; thêm `TC-21b` (`DiscountValue = 0` lọt ở v1); **không** sửa code `PRO-002` |
| D1 BA áp D-296 | `ba` | P1 #26 (E10/BR-19/CR-1/§6.2/§12/TC-20/TC-21 + `TC-21b`), P2 #27 (trỏ `IPromotionAppService.cs:12-30` 10 method), P3 #28 (heading), nhất quán §8.2 #3; 1 dòng nợ `pro-002` (`v0.20`) |
| D2 DOCREV kiểm nhanh | `docrev` | **Vòng 5: `DOC-OK` · 0 P1 · 1 P2 (#29) · 1 P3 (#30)** |
| D3 nháp biên bản | ORCH | `docs/agents/nhap-bien-ban-gd3-b.md` cập nhật dòng `PRO-001` (v0.10, `DOC-OK` vòng 5, P2 #20 xong) |
| Dòng biên bản tạm duyệt | ORCH | `docs/bien-ban-tam-duyet-gd3-b-2026-11-24.md` dòng 1: **`Tạm duyệt`** — điều kiện (a)(b) `D-169` đã đạt (P2 #20 đóng + `DOC-OK` vòng 5 0 P1) |
| Cổng code WP-25 | ORCH | `probe-cong-tai-lieu -CheDo Code -WP WP-25` → **XANH, exit 0** (mọi mã WP-25 "Tạm duyệt" trong biên bản) |

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (baseline) | 0 | PASS — 295 tệp · 989 link · 1781 backtick · 0 chết |
| `npm --prefix tools run check:mermaid` (baseline) | 0 | 214 khối · **0 lỗi** |
| `node --test .claude/hooks/test/hooks.test.mjs` (baseline) | 0 | **49/49** |
| `check-feature-doc.ps1 -Path pro-001 -WP14` (nhiều lần) | 0 | DAT — 16/16 heading · 36/36 BR có test · Đối chiếu dòng 1171 · 7 `Open` · 0 TODO |
| `check-agent-artifact.ps1 -Kind docreview` | 0 | DAT — verdict=DOC-OK · P1=0 · P2=1 |
| `check-agent-artifact.ps1 -Kind decision` | 0 | DAT — 117 dòng |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-25` | 0 | **XANH** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau) | 0 | PASS — 989 link · **1791** backtick · 0 chết |
| `npm --prefix tools run check:mermaid` (sau) | 0 | 214 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` (sau) | 0 | **49/49** |

> `dotnet build` / `dotnet test` **bỏ qua có lý do**: WP-25 hàng `W25-0` là **thuần tài liệu** (0 tệp `.cs`, 0 migration) — chưa chạm mã.

## 2. Chưa xong / cắt giữa

**Không** — `W25-0` đạt DoD (DOCREV `DOC-OK` 0 P1 · `check-agent-artifact` exit 0 · kiem-link PASS · probe `-CheDo Code -WP WP-25` exit 0).

`DOC-OK` cho phép **< 3 P2**: còn **P2 #29** (`BR-19` dòng 141 phát biểu `0 < DiscountValue` trong khi as-built chấp nhận `0` — nên thêm ngoặc ngoại lệ v1) và **P3 #30** (§1 Post-Condition còn `UsageSequence ∈ [1, Max]` thiếu nhánh `NULL`) — **để người duyệt**, theo N22 không gọi BA sửa thêm sau `DOC-OK`.

## 3. Migration đã áp

**Không có — 0 migration, 0 `pg_dump`.** Không đổi schema, không chạm DB.

## 4. Quyết định mới

- **`D-295`** (TẠM CHỐT DEC) — trần thử cấp `UsageSequence` khi `23505` = **5 lần thử** (1 ghi + 4 thử lại), hằng `PromotionOptions.MaxUsageSequenceAttempts = 5`; hết trần ⇒ `0104` (409) + `LogWarning` + metric `usage_sequence_exhausted`.
- **`D-296`** (TẠM CHỐT DEC) — mô tả `PRO-001` theo **as-built** của `PRO-002`: `CreateAsync`/`Promotion.TaoMoi` ném `AbpValidationException` (400, không `Code`); **giữ số `0109`** "v1 chưa có đường ném"; `TC-21b` ghi `DiscountValue = 0` lọt ở v1; **không** sửa code `PRO-002` (nợ ghi ở lịch sử `pro-002` `v0.20`).
- Cả hai vào lô **`D-277…D-296` chờ người duyệt** (`docs/agents/hang-doi-xem-lai.md`); `reviewBefore` = W25-2 · W25-4 · trước PR `tich-hop/gd3 → main` · release.

## 5. Nợ / rủi ro

- 🟠 **P2 #29** — `BR-19` (dòng 141) còn câu `0 < DiscountValue ≤ 100` coi `DiscountValue = 0` là vi phạm, trong khi `E10`/`TC-21b`/as-built cho phép `0`; đề nghị người duyệt thêm ngoặc "v1 as-built 0 lọt — nợ `PRO-002`".
- 🟡 **P3 #30** — §1 Post-Condition (dòng 137) còn `UsageSequence ∈ [1, MaxUsagePerCustomer]` thiếu nhánh `NULL`.
- 🟡 **`PRO-001-Q16`** — `modules/Promotions/**` đã có (W24-5) nhưng §14.5 + cột "Code thật" của Đối chiếu còn ghi "0 code `Promotions`" ⇒ cần **đợt đối chiếu as-built riêng** (BA khuyến nghị mở WP riêng; không chặn `W25-0`).
- 🟡 **Nợ `PRO-002`** (`D-296`) — `CreateAsync`/`TaoMoi` chưa ném `0109`; `Percentage` với `DiscountValue = 0` lọt guard; chủ `PRO-002` thêm `discountValue <= 0` + `0109` khi cần.
- 🟡 Nợ **từ N98**: probe 403 chưa đo (staging có Identity HTTP API) · `0101/0104/0119` chưa map Host (`eCommerceHttpApiHostModule.cs` chỉ `0001–0010`) — chủ `PRO-001` (đợt code) · P3 còn của `PRO-002` · `D-283`/`D-284` · `pnpm build` EPERM + `pnpm lint` no-script (nợ môi trường cũ) · 33 ràng buộc `Pro*` trong `RangBuocChuaAnhXa.txt`.
- **Chờ người:** (a) duyệt lô `D-277…D-296` (tăng `D-295`/`D-296`); (b) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp; (c) kế toán duyệt lô M4 trước release; (d) merge `main`; (e) tạo **PR nháp** `tich-hop/gd3 → main` khi chuỗi xong; (f) quyết `Q16` (mở WP đối chiếu as-built `PRO-001` hay giữ nhãn lịch sử).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W25-0` → mở `W25-1`** (F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2; M4 trước release).

- Cổng code **đã mở** (`probe -CheDo Code -WP WP-25` XANH) ⇒ `W25-1` không bị `PH blocked`.
- Đọc trước: `docs/nhanh/handover-n99.md` §5 (nợ), `docs/reviews/PRO-001-docreview.md` vòng 5 (P2 #29/P3 #30 + Q16), `docs/features/promotions/pro-001-voucher-coupon.md` v0.10 (§1 bước 7 · `BR-04` · `D-295`/`D-296`).
- Nền engine WP-24 đã đóng (`modules/Promotions/**`); `W25-1` thiết kế trên engine đó.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp25-voucher`; base `origin/tich-hop/gd3` @ `e7aa32c`; HEAD sau phiên: xem `git log -1`.
- Commit phiên đã push `origin/feat/gd3-wp25-voucher` (hàng `doc-gate` ⇒ **không** đẩy `tich-hop/gd3`; việc đó ở hàng `dong-wp` `W25-5` khi G6 xanh).
- **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N99`): `ba` ×3 (D1 initial → D-295 → D-296) · `decider` ×2 (`D-295`, `D-296`) · `docrev` ×2 (vòng 4 `DOC-CHANGES` → vòng 5 `DOC-OK`). **0 lần chặn dừng.**
⚠️ Hook `canh-bao.jsonl` ghi cảnh báo "thiếu khối JSON cuối" ở lượt `ba`/`decider` — **không phải thiếu thật**: handback có khối JSON, giới hạn đã biết (`chanDungKhiThieuJson=false`, `scopes.json` dòng 5). `ngoaiVung = []` cho mọi lượt của phiên này (đối chiếu `git diff --stat`: `ba` chỉ chạm `pro-001` + `pro-002` (nợ `D-296`), `decider` chỉ chạm `decisions.md` + `hang-doi-xem-lai.md`).
