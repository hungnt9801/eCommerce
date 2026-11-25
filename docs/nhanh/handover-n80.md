# Handover N80 — WP-18 / hàng `W18-4` (F4 QA + F5 REV) XONG: 14/14 + 16/16 BR, 11 negative control, P1 = 0

> Phiên `N80` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W18-4` · WP-18 · mã `ADDR-001`, `ADDR-002` · nhánh
> `feat/gd3-wp18-dia-chi` (base `origin/tich-hop/gd3`) · 26/11/2026.
> Phạm vi phiên: **F4 QA** (testplan + test + negative control + integration/HTTP/probe + NFR) → **F5 REV**
> (`VERDICT: PASS`, P1 = 0). **Không** code FE/màn `WEB-001` (`D-247` chờ người), **không** mở `/api/v1` (`D-245`),
> **không** adapter GHN/GHTK (`R3-1`), **không** PII/`ADDR-003`.

## 1. Đã xong

- **F4 QA — 2 testplan mới, phủ đủ BR:**
  - `docs/tests/ADDR-001-testplan.md` — **14/14 BR**, +47 ca unit + 9 ca integration, mục NC + số đo NFR.
  - `docs/tests/ADDR-002-testplan.md` — **16/16 BR**, +22 ca unit + 1 ca integration, ca runtime HTTP.
  - `check-agent-artifact.ps1 -Kind testplan` **DAT** cho cả hai (exit 0, `chua co trong testplan = 0`).
- **Hạ tầng schema (nợ từ `N78`):** regenerate `test/eCommerce.IntegrationTests/Assets/schema.sql` từ **chính migration**
  (`dotnet ef migrations script …`) — **+92 dòng, 0 dòng xoá**, thêm 3 bảng `AddrCatalogSyncRuns`/`AddrCarrierSyncRuns`/
  `AddrCarrierSyncRunUnmappedCodes`; `PostgreSqlSchemaTests` **93 → 94** + tên đích danh
  `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code`; allowlist 5 ràng buộc mới vào `test/eCommerce.Tests/Data/RangBuocChuaAnhXa.txt`.
- **Test hành vi mới:** app service `GetUnmappedCodesAsync` (9 ca), app service `ImportDatasetAsync` (11 ca), cache/TTL `D-242` (20 ca),
  harness `CarrierAddressSyncWorker` (**7 ca**, dựng `PeriodicBackgroundWorkerContext` thật — ctor public ABP 10.7; UoW fake có vùng chờ/rollback);
  integration PostgreSQL `PostgreSqlAddressSyncTests` (**9 ca**); NFR-001 p95 (`Nfr001AddressP95Tests`).
- **Negative control: 11 ca đỏ→xanh có bằng chứng** (script tự chèn vào testplan):
  - `ADDR-001` **8** (NC-1…NC-7 + NC-8 `AQ6`); `ADDR-002` **3** (NC-1 hàng rào 3, NC-3 `TraNguoc` 2 `DaXacMinh`, NC-5 `Trim` mã hãng).
  - `git status --porcelain -- modules/ src/ shared/` **rỗng** sau mỗi ca.
- **Runtime HTTP:** `tools/probe-dia-chi.ps1` **exit 0, 10/10** — gồm **P2 `D-233`**: `POST tra-ma-hang` mã hãng lạ ⇒
  **404** + `error.code = eCommerce:Address:0501`; hãng rỗng ⇒ **400** + `0505`; `ChuaXacMinh` ⇒ **409** + `0502`;
  đơn vị đóng ⇒ **409** + `0504`; 2 `DaXacMinh` cùng đích ⇒ **409** + `0510`; `GET unmapped-codes` **401/200**;
  `POST {id}/xac-minh` **401**. Ca **403** (thiếu permission) **chưa đo được** — xem §2.
- **NFR-001 p95** (đo 26/11/2026, PG17 Testcontainers): **nguội 1,353 ms** (max 1,855 ms; ngưỡng < 100 ms) ·
  **nóng < 0,01 ms** (fake in-memory × 500; ngưỡng < 10 ms; là **cận dưới**, bỏ RTT Redis). Bằng chứng đo, **không** phải cổng CI.
- **Đóng 28 P3 tài liệu** (`D-243` điều kiện (2)): `arch` sửa **6** P3 `ADDR-001-arch.md` + **16** P3 `ADDR-002-arch.md`, khớp code as-built
  (`ba` đã đồng bộ SRS ở `N78` — P3 #9 "đã khớp", không chuyển `ba`).
- **Vá `ADDR-001-AQ6`** (`D-251`): `be` thêm `Enum.IsDefined` **trước** `DoDaiMa` ⇒ `Level` ngoài dải ra **`0605`** (`BiChan`) thay vì
  `0002` (`Loi`); `qa` +1 ca `BR009_hang_rao_0b_cap_ngoai_AdministrativeLevel_thi_NEM_0605_khong_phai_0002` + NC-8; `arch` đóng AQ6 trong `-arch`.
- **F5 REV — P1 = 0:** `docs/reviews/ADDR-001-review.md` (`VERDICT: PASS`, P1=0, P2=3, P3=4) ·
  `docs/reviews/ADDR-002-review.md` (`VERDICT: PASS`, P1=0, P2=0, P3=5); `check-agent-artifact -Kind review` **DAT** cả hai.
- **`D-250` phần của `qa`:** đổi 4 call site test sang `ApDung(kế hoạch, nguồn)`; overload `modules/**` chờ `be` (xem §5).
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2089/2089** |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | PASS — 252 tệp · 949 link · 1524 backtick |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-18` (điều kiện vào) | 0 | **XANH** |
| `dotnet build eCommerce.slnx --no-incremental` (cuối phiên) | 0 | **0 error / 9 warning** (0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2166/2166** (+77) |
| `dotnet test test/eCommerce.IntegrationTests --no-build` | 0 | **106/106** (+10) |
| `pwsh -File tools/kiem-tat-ca.ps1` (G3) | 0 | **10/10 bước · 305,2 s** (build · unit · integration · mermaid · cổng tài liệu · FE typecheck+Vitest · `gen:api` không đổi · DB 25/25 · test cổng 60/60) |
| `check-agent-artifact.ps1 -Kind testplan` ×2 | 0 | **DAT** — 14/14 và 16/16 BR |
| `check-agent-artifact.ps1 -Kind review` ×2 | 0 | **DAT** — `verdict=PASS · P1=0` |
| `check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** (116 dòng) |
| `tools/agents/negative-control.ps1` ×11 | 0 | 11/11 đỏ→xanh |
| `pwsh -File tools/probe-dia-chi.ps1` | 0 | **10/10 DAT** (+1 ca 403 chưa đo được) |
| `npm --prefix tools run check:mermaid` | 0 | **213 khối · 0 lỗi** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau handover) | 0 | PASS — xem lần chạy cuối trong log phiên |

## 2. Chưa xong / cắt giữa

**DoD `W18-4` đạt:** G3 xanh · G4 DAT (2 testplan) · G5 DAT (PASS, P1=0). Không có việc dở giữa chừng.

Các mục **cắt có nhận chủ**:

- **403 runtime (thiếu `Addresses.CarrierMapping.Verify`) chưa đo được** — Host **không** tham chiếu `Volo.Abp.Identity.HttpApi`
  (`POST /api/identity/users` ⇒ **404**, `route-check -Match identity` = 0 route) ⇒ probe không tạo được user thường.
  Khối 4 bước đã viết trong probe, tự kích hoạt khi Host có Identity HTTP API. **Chủ `qa`, hạn `W18-5`** (`D-249`).
  Quyền vẫn phủ bằng reflection (`AddressCarrierContractGuardTests`).
- **REV P2 #2 (`ADDR-001`):** `ImportDatasetAsync`/`CatalogSyncRun.Ghi` **không** giới hạn độ dài `SourceName` (cột `varchar(512)`);
  input > 512 ⇒ lỗi lúc commit **ngoài** try ⇒ **500** và **không có dòng lô** (mất dấu vết BR-009). Đề nghị validate trước khi giữ khoá.
  **Chủ `be`, hạn trước PR `tich-hop/gd3 → main`.**
- **Overload `AdministrativeUnitSynchronizer.ApDung(kế hoạch)`** (P2 #1 REV, `D-250`): 0 call site production nhưng tự bơm `legalBasis` giả;
  `qa` đã đổi 4 call site test, **`be` xoá overload** (`modules/Address/eCommerce.Address.Domain/Sync/AdministrativeUnitSynchronizer.cs:307-315`).
  **Chủ `be`, hạn trước PR `tich-hop/gd3 → main`.**
- **P2 #3 REV:** phạm vi code phi-migration **3.416 dòng / 37 tệp** > K-04 ~600 — **chấp nhận theo chuỗi WP-18** (`D-216`), phải mô tả rõ trong PR.
- **9 P3 REV** (ghi trong 2 tệp review): doc-comment bị cắt (`AddressEvents.cs`), `<summary>` mở thiếu (`eCommerceMetrics.cs:38`),
  `AddressCacheInvalidatedEto` chưa `INotificationEvent`, testplan lệch số đo (105↔106), `TienToKhoa` inline, SRS `ADDR-002` BR-013/014 còn câu cũ,
  log sau commit trong worker nằm trong `try`. Chủ: `be`/`ba` — gom ở phiên dọn nợ, không chặn.
- **Màn vận hành `WEB-001`** (2 màn + nạp danh mục) → `D-247` **CHUYỂN NGƯỜI** (thêm WP ngoài `07` §WP-15+).
- **`ADDR-002-arch` §6 + `ADDR-001-arch` ghi `/api/app/…`** → `D-245` ⑤: BA + ARCH sửa **trước** hàng F2 của WP màn hình `WEB-001`.

## 3. Migration đã áp

**Không có migration mới.** Phiên này **0** `.cs` migration, **0** `pg_dump` (không chạm DB schema). DB dev giữ nguyên trạng thái `N78`
(`Add_Addr001_CatalogSyncRuns`, `Add_Addr002_CarrierSyncRuns` đã áp). Chỉ **sinh lại** `Assets/schema.sql` từ chính 2 migration đó
(+3 bảng, **0** dòng xoá) — dùng cho integration test, không áp lên DB.

## 4. Quyết định mới

- **`D-248`** (TẠM CHỐT, DEC) — dựng **harness `CarrierAddressSyncWorker`** ngay trong `W18-4` (test-only) để chạy TC-07/TC-29 + NC-2/NC-4;
  không hạ chuẩn sang quét mã nguồn.
- **`D-249`** (TẠM CHỐT, DEC) — đo **403 runtime** ngay bằng probe (user thường); không tạo được ⇒ ghi "chưa đo được", **không** đánh dấu ✅.
- **`D-250`** (TẠM CHỐT, DEC) — overload `ApDung`: `qa` đổi 4 call site **ngay**; `be` xoá overload ở phiên BE kế; hạn cứng **trước PR `tich-hop/gd3 → main`**.
- **`D-251`** (TẠM CHỐT, DEC) — vá `ADDR-001-AQ6` (`Enum.IsDefined` trước `DoDaiMa` ⇒ `0605`/`BiChan`), `qa` phủ ca + NC, `arch` đóng AQ6.
- `check-agent-artifact.ps1 -Kind decision` **DAT** (116 dòng, tới `D-251`).

## 5. Nợ / rủi ro

- **Hàng đợi xem lại:** +4 dòng (`D-248`…`D-251`) — tổng **149** quyết định tạm chờ người duyệt (ước tính 145 của `N79` + 4).
- **Hook cảnh báo "thiếu khối JSON cuối"** cho 8 lượt vai (harness không cấp `last_assistant_message` đúng lúc) — khối JSON **có** trong báo cáo
  hand-back; **0** artifact bị ảnh hưởng, **0 lần chặn dừng**. Khuôn đã gặp ở `N78`/`N79`.
- **Trần lượt vai:** `qa` **3 lần** chạm 60 (pass 1, pass 3, + tiếp), `arch` **2 lần** chạm 30, `rev` **1 lần** chạm 30 — đều gửi tiếp và hoàn tất;
  **0** vòng lỗi lặp 3 lần.
- **`CarrierAddressSyncWorker` log sau `CompleteAsync` nằm trong `try`** (P3 REV): nếu log ném (hiếm) sẽ rơi vào `catch` và ghi **thêm** dòng `Loi`
  cho **cùng** lượt — cần chuyển log ra ngoài `try`. Chủ `be`, không chặn.
- **NFR-001 nóng là cận dưới** (fake in-memory, bỏ RTT Redis) — đo lại trên Redis thật khi có môi trường.
- **41 hit `api/proxy`** + hardening BFF → `D-246`, nợ chủ `fe`, chậm nhất trước PR `tich-hop/gd3 → main`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-18/W18-4 → mở `W18-5` (F6 OPS + G6).**

- `W18-5`: `ops` viết tài liệu triển khai trong `docs/ops/` (tên `<MÃ>-deploy.md`; biến môi trường `Address:Cache:*`/`Address:CarrierSync:*`, migration,
  **đường lùi** = đặt cờ `Address:Cache:Enabled=false`/`Address:CarrierSync:Enabled=false` + `git revert` commit WP-18; 0 migration mới);
  chạy `pwsh -File tools/kiem-tat-ca.ps1 -DayDu`; xanh ⇒ **push fast-forward** `feat/gd3-wp18-dia-chi` → `tich-hop/gd3`; thêm một dòng `07` §13.
- **Trước `-DayDu`/PR:** đóng 2 nợ `be` (`D-250` overload + P2 #2 `SourceName`) và quyết nợ 403 (`D-249` — Host thiếu `Volo.Abp.Identity.HttpApi`:
  thêm package là việc `be`/`ops`, hoặc chấp nhận reflection + ghi nợ tới WP màn hình).
- **Không** code FE/màn `WEB-001` (`D-247` chờ người), **không** mở `/api/v1` (`D-245`), **không** adapter GHN/GHTK (`R3-1`), **không** PII/`ADDR-003`.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-18
và handover gần nhất `docs/nhanh/handover-n80.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng W18-5** (phiên N81). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp18-dia-chi`, base `origin/tich-hop/gd3` (merge-base `7d15f84`); mở phiên `HEAD` `ebf2a28`.
- Commit phiên: `9a81daf` (vá AQ6) · `e75fe17` (testplan + test + schema + probe) · `5cf642f` (28 P3 arch + `D-248…251` + registry) ·
  `540ccd2` (ca AQ6 + NC-8) · **commit đóng phiên** (handover này + review + `07` §13 + registry).
- **Đã push** `origin/feat/gd3-wp18-dia-chi` (sau commit test/docs); **chưa** đẩy `tich-hop/gd3` (chỉ ở hàng `dong-wp` `W18-5` khi G6 xanh).
- Không mở PR (`D-216`③).

---

**Hàng rào agent** (phiên `N80`): **11 lượt vai** — `qa` ×4 (pass 1 ADDR-001 · pass 2 ADDR-002 · pass 3 harness/403 · ca AQ6),
`decider` ×2 (`D-248…250` · `D-251`), `arch` ×3 (28 P3 · đóng AQ6 · 2 câu header/§9), `be` ×1 (vá AQ6), `rev` ×1 (2 review).
**0 lần chặn dừng**; **0 tệp ngoài vùng** (`git status` trước mỗi commit chỉ có `test/**`, `docs/tests/**`, `tools/probe-dia-chi.ps1`,
`docs/features/address/*-arch.md`, `docs/nhanh/**`, `docs/agents/**`, `agents/state/**`, `modules/Address/**` đúng vai).
