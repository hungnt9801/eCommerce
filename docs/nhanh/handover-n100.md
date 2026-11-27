# Handover N100 — WP-25 / hàng `W25-1` (F0–F2b) XONG — **mở F3 BE (`W25-2`)**

> Phiên `N100` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W25-1` · WP-25 · mã `PRO-001` · module `Promotions` (đã có từ W24-2) ·
> nhánh `feat/gd3-wp25-voucher` (base `origin/tich-hop/gd3` @ `e7aa32c`) · 27/11/2026.
> Phạm vi phiên: **F0** (đọc SRS theo mục + danh sách người review K-08) → **F2** `arch` viết `PRO-001-arch.md` v0.1→v0.3 →
> **F2b** `docrev` vòng 1 `DOC-CHANGES` → vòng 2 **`DOC-OK` 0 P1/0 P2** → **G1** (SRS đã `Tạm duyệt` từ `W25-0`) + **G2 theo M2** (`D-297`).
> **0 dòng code**, **0 migration**, **0 `pg_dump`**.

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| F0 | ORCH | Đọc SRS `PRO-001` v0.10 theo mục (§0 ADR-01…15 + ranh giới `PRO-002`, §1, §3, §5.1–§5.3 CR-1…CR-11, §6.1–§6.2, §7, §8, §9, §11, §12, §13, §14.2–§14.4, §15). Xác định **ARCH bắt buộc** (chạm `Promotions`+`Sales`+`Pricing`, schema, contract). **Người review K-08:** state machine ⇒ **Kiến trúc trưởng + BA**; tiền/VAT ⇒ **+ kế toán** (trước release); chạm `XC-001..005` ⇒ **nhóm XC** |
| Cổng vào WP-25 | ORCH | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-25` = **exit 0 (XANH)** — mọi mã WP-25 `Tạm duyệt` |
| F2 ARCH | `arch` | `docs/features/promotions/PRO-001-arch.md` **mới, 15 mục**, v0.1 → **v0.3** (khảo sát as-built qua `codegraph_explore`): **schema delta = 0 cột/index** — CR-1…CR-7 + CR-11 đã nằm trong `Add_Promotion_Module` (W24-2); chỉ **một** migration `Add_Voucher_Status_Check` (2 `CHECK` miền `Status`, **`PRO-002` sở hữu**, expand-only); 2 máy trạng thái **NÉM**; 2 service voucher; cấp `UsageSequence` đúng **`D-295`** (SAVEPOINT, 5 lần thử, 0 metric mới); 3 metric gỡ `ChuaCoNoiPhat`; 3 quyền `.Voucher.*`; mở rộng allowlist **đúng 2 namespace** (`Messaging.Idempotency`) + test; 13 `docDeviation` (D1–D13) |
| F2b DOCREV | `docrev` | `docs/reviews/PRO-001-arch-docreview.md` **2 vòng**: vòng 1 `DOC-CHANGES` **P1=0/P2=5/P3=6** (ON CONFLICT thiếu target `D-295`; test ranh giới sai tên; thiếu bảng `01xx→HTTP`; `TenantId` rỗng vs `NULL`; thiếu `D13`) → vòng 2 **`DOC-OK` · P1=0 · P2=0 · P3=1** (đã đo `ON CONFLICT` thật trên PostgreSQL 17, cổng 5433, `BEGIN…ROLLBACK`) |
| G1 (SRS) | — | Đã đạt từ `W25-0`: biên bản tạm duyệt `gd3-b` §1 dòng 1 (`PRO-001` Tạm duyệt, `DOC-OK` vòng 5, 0 P1) |
| G2 (ARCH) | `decider` (M2) | **`D-297`** tạm duyệt `PRO-001-arch.md` cho **F3 trên nhánh WP**, điều kiện `docrev` `DOC-OK` 0 P1 trên bản chốt — **đã đạt** (v0.3). ORCH ghi **§4** vào `docs/bien-ban-tam-duyet-gd3-b-2026-11-24.md` |
| Câu hỏi mở | `decider` | **`D-297`…`D-302`** đóng **11/11** câu (xem §4) |

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-25` (mở + sau) | 0 | **XANH** cả hai lần |
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở) | 0 | **0 error · 9 warning** (Address/Sales, có sẵn) |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở) | 0 | **2477/2477** · 0 skipped |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở) | 0 | **PASS** · 296 tệp · 990 link · 1798 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` (mở) | 0 | **49/49** pass |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/PRO-001-arch-docreview.md` | 0 | **DAT** · `verdict=DOC-OK` · **P1=0 · P2=0** |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** · 117 dòng cần kiểm |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau tài liệu) | 0 | **PASS** · 298 tệp · 992 link · 1803 backtick · 0 chết |
| `npm --prefix tools run check:mermaid` (sau) | 0 | **214 khối · 0 lỗi** |
| **Đo lại lúc đóng** `dotnet build … --no-incremental` | 0 | **0 error · 9 warning** · 90,78 s |
| **Đo lại** `dotnet test test/eCommerce.Tests --no-build` | 0 | **2477/2477** · 14 s |
| **Đo lại** `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |

> Không chạy `eCommerce.IntegrationTests` và `kiem-tat-ca.ps1 -DayDu` phiên này: hàng `W25-1` **0 tệp `.cs`/`.sql`/schema/route** ⇒
> không có bề mặt để integration/e2e kiểm thêm; G6 thuộc `W25-5`. Build/test **không đổi** so baseline (0 code).

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W25-1`** — DoD của hàng đã đạt (F0 + F2 + F2b `DOC-OK` 0 P1 + G1/G2 theo M2). WP-25 còn 4 hàng:
`W25-2` (F3 BE), `W25-3` (F3 FE), `W25-4` (F4 QA + F5 REV), `W25-5` (F6 OPS + G6).

**Việc `W25-2` BẮT BUỘC làm theo `D-297`…`D-302` (đừng để rơi):**
1. **`D13` / `D-298` ①(4) / `D-302`** — `ba` cập nhật **SRS `pro-001` §6.1** (dòng ~461–462, cùng nhánh cùng PR): `ReserveVoucherDto` thêm `Subtotal`/`Currency`/`TtlSeconds`; `CommitVoucherDto` thêm `CheckoutSessionId`.
2. **`D-298`** — `Sales` mang mã/lượt bằng **DTO additive** (`CheckoutInput.VoucherCode?`, `TaoDonTuCheckoutInput.VoucherRedemptionId?/VoucherCode?`, `KetQuaCheckoutDto.VoucherRedemptionId/VoucherDiscountTotal/VoucherExpiresAtUtc/VoucherReasonCode`); `CommitAsync` gọi **sau `Recalculate()` :107, trước tripwire :110**, cùng UoW; server kiểm `(TenantId, RedemptionId)` + `IdempotencyKey` phiên ⇒ lệch `0105`.
3. **`D-299`** — controller tường minh `VoucherController` `/api/v1/voucher/**` **7 route** + giữ auto `/api/app/voucher-admin/**`; audit `Revoke/OverrideLimit` bằng ABP audit log + `LogInformation` (`reason` ≥ 10 ký tự ⇒ 400), **0 cột**. `AbpAuditLogActions.Parameters` **chưa đo** ⇒ `W25-4` test integration (rỗng thì mở lại `D-299`).
4. **`D-300`** — migration `Add_Voucher_Status_Check` do **`PRO-002` sở hữu** (chủ migration duy nhất); `PRO-001` chỉ gửi CR (`D-151`); `pg_dump` trước khi áp.
5. **`D-295`** — cấp `UsageSequence` = **5 lần THỬ** (1 ghi + 4 thử lại); `ON CONFLICT` **có target** `(TenantId, PromotionId, CustomerId, UsageSequence) WHERE CustomerId IS NOT NULL AND Status <> 2` (0 dòng = trùng số, thử tiếp; `23505` index khác = **NÉM** trong `SAVEPOINT`, không thử tiếp); hết trần ⇒ `0104` (409) + `LogWarning` + nhãn `result=usage_sequence_exhausted`; **cấm** chạy tiếp `25P02`.
6. **`D-301`/`D-302`** — TTL **900 s** (`TtlSeconds` bắt buộc, Sales `(TtlPhut ?? 15) × 60`, 60…86400); `MaxBatchSize = 10000` (Setting + hàm thuần kẹp 1…100000, vượt ⇒ `0108` trước khi ghi); `GetRedemptionsAsync(status=Reserved)`; không dry-run; trả **cả hai** `ExpiresAtUtc`.
7. **P3 còn lại của ARCH** — §1.2/§7.6 nói "14 dòng map" ⇒ sửa thành "map đủ `0101…0119` trừ `0109`/`0113`" (đếm 16–17 dòng `options.Map`).
8. **P2 #29 / P3 #30 của SRS** (từ `W25-0`) vẫn **để người duyệt** — không tự sửa sau `DOC-OK` (N22).

## 3. Migration đã áp

**Không có — 0 migration, 0 `pg_dump`.** Hàng thiết kế thuần (F0–F2b). `W25-2` sẽ tạo **một** migration `Add_Voucher_Status_Check`
(2 `CHECK` miền `Status`, do `PRO-002` sở hữu); **trước khi áp** phải `pg_dump` (`artifacts/pg-dump/N101-W25-2.dump`) — DB dev **đang có dữ liệu**.

## 4. Quyết định mới

`decider` ghi **6 dòng** `TẠM CHỐT (DEC, uỷ quyền)`, `check-agent-artifact -Kind decision` **DAT**:

| D-xx | Trạng thái | Nội dung (ngắn) |
|---|---|---|
| `D-297` | TẠM CHỐT (DEC) | **G2**: tạm duyệt `PRO-001-arch.md` cho F3 trên nhánh WP, **có điều kiện** `docrev` `DOC-OK` 0 P1 (đã đạt v0.3); `DOC-CHANGES` ⇒ mở lại; không biên bản người / không merge `main` |
| `D-298` | TẠM CHỐT (DEC) | **AQ1+AQ5**: `Sales` mang voucher bằng DTO additive (`SAL-002`/`SAL-001`), server kiểm tenant+phiên ⇒ `0105`; `ReserveVoucherDto` +`Subtotal`/`Currency`, `CommitVoucherDto` +`CheckoutSessionId`; v1 giữ `CustomerGroupId`/`IsFirstOrder = null` ⇒ 2 lá luôn `false` (công bố Marketing; nợ nối `CustomerGroupId`) |
| `D-299` | TẠM CHỐT (DEC) | **AQ2+AQ3**: controller tường minh `/api/v1/voucher/**` (7 route) + giữ auto `/api/app/voucher-admin/**`; audit `Revoke/OverrideLimit` = ABP audit log + `LogInformation` có `CorrelationId`, **0 cột/0 migration**, `reason` ≥ 10 ⇒ 400 |
| `D-300` | TẠM CHỐT (DEC) | **AQ4**: thêm ngay 2 `CHECK` miền `Status` qua **một** migration `Add_Voucher_Status_Check` do **`PRO-002` sở hữu**; lùi = `DROP CONSTRAINT` |
| `D-301` | TẠM CHỐT (DEC) | **AQ6**: trả **cả hai** `ExpiresAtUtc` (voucher + checkout), không đổi nghĩa cũ; FE hiện **min**; Sales ghép, không sửa `Promotions` |
| `D-302` | TẠM CHỐT (DEC) | **Q2+Q6+Q7+Q9+Q11**: TTL 900 s không gia hạn; `MaxBatchSize` 10000, không rate limit, vượt ⇒ `0108`; `GetRedemptionsAsync(status=Reserved)`, không route mới; không dry-run; test chuỗi khoá 0 tiền tố + `ops` `MONITOR` khi có Redis |

Hàng đợi xem lại: `docs/agents/hang-doi-xem-lai.md` thêm 6 dòng (lô `D-277…D-302` chờ người duyệt).

## 5. Nợ / rủi ro

- 🟡 **P3 ARCH còn 1** — "14 dòng map" (§1.2/§7.6) lệch số (thực cần 16–17); sửa cùng `W25-2` (không chặn).
- 🟡 **`D13` SRS chưa sửa** — `pro-001` §6.1 vẫn ghi chữ ký cũ; chủ **`ba`** ở `W25-2` (cùng PR).
- 🟡 **`D-299` audit chưa đo** — `AbpAuditLogActions.Parameters` của ABP 10.7 chưa kiểm; chủ **`qa`** `W25-4` (rỗng ⇒ mở lại `D-299`).
- 🟡 **`D-300` chủ migration** — `Add_Voucher_Status_Check` do `PRO-002` sở hữu (chuỗi chạy tuần tự, một chủ `migrations add`).
- 🟡 **Nợ `PRO-002`** (`D-296`) — `0109` chưa có đường ném (`AbpValidationException` 400); `DiscountValue = 0` lọt guard; chủ `PRO-002` khi mở CR.
- 🟡 **Nợ từ N98/N99** — probe 403 chưa đo (staging có Identity HTTP API) · `0101/0104/0119` **chưa map Host** (chủ `PRO-001`, chính `W25-2` — `D-297` §7.6 đã có bảng) · P3 của `PRO-002` · `D-283`/`D-284` · `pnpm build` EPERM + `pnpm lint` no-script (nợ môi trường) · 33 ràng buộc `Pro*` trong `RangBuocChuaAnhXa.txt`.
- **Chờ người** (gom một lần ở câu trả lời cuối): (a) duyệt lô `D-277…D-302`; (b) kế toán duyệt lô M4 (`D-143`/`D-148`/`D-149`/`D-152`/`D-158`) **trước release**; (c) `Q16` mở WP đối chiếu as-built `PRO-001` hay giữ nhãn lịch sử; (d) merge `main` + PR nháp `tich-hop/gd3 → main` khi chuỗi xong; (e) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W25-1` → mở `W25-2`** (F3 BE: domain + app service + endpoint + migration `Add_Voucher_Status_Check` do `PRO-002` sở hữu + test đơn vị; `pg_dump` trước khi áp).

- Hàng kế = **`W25-2`** (loại `code`, bước `F3 BE`), vai **`be`**. Điều kiện: `probe -CheDo Code -WP WP-25` **exit 0** (đã đo) **và** `D-297` hiệu lực (đã đạt).
- Đọc trước: `docs/features/promotions/PRO-001-arch.md` **v0.3** (nhất là §2 ranh giới, §4 contracts, §5 EF/migration, §6 luồng `UsageSequence`, §7.6 bảng `01xx→HTTP`, §12 D1–D13) + `docs/reviews/PRO-001-arch-docreview.md` (vòng 2) + §2 trên.
- **Nhớ**: 8 việc bắt buộc ở §2; `ba` sửa SRS §6.1 **trước** khi `be` chốt DTO; ghim ordinal enum; `NULLS NOT DISTINCT`; `0104→409`/`0119→409` map Host.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-25
và handover gần nhất `docs/nhanh/handover-n100.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng W25-2** (phiên N101). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp25-voucher`; base `origin/tich-hop/gd3` @ `e7aa32c`; HEAD sau phiên: xem `git log -1`.
- Commit phiên đã push `origin/feat/gd3-wp25-voucher` (hàng `code`/thiết kế ⇒ **không** đẩy `tich-hop/gd3`; việc đó ở hàng `dong-wp` `W25-5` khi G6 xanh).
- **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N100`): `arch` ×1 agent (4 lượt sửa: v0.1 → v0.2 → v0.3; **chạm trần 30 tool-use 2 lần** nhưng đã hoàn tất + ghi tệp) · `decider` ×1 (chạm trần 40 lượt rồi `SendMessage` tiếp; ghi `D-297`…`D-302` + 6 dòng hàng đợi) · `docrev` ×1 (2 vòng: `DOC-CHANGES` → `DOC-OK`). **0 lần chặn dừng.**
⚠️ Hook `canh-bao.jsonl` ghi "thiếu khối JSON cuối" ở lượt `arch`/`decider` — **không phải thiếu thật**: cả hai có khối JSON trong handback (giới hạn đã biết, `chanDungKhiThieuJson=false`). `ngoaiVung = []` cho mọi lượt (`arch` chỉ chạm `PRO-001-arch.md`; `decider` chỉ chạm `decisions.md` + `hang-doi-xem-lai.md`; `docrev` chỉ chạm `PRO-001-arch-docreview.md`).
