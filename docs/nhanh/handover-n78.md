# Handover N78 — WP-18 / hàng `W18-2` (F3 BE) XONG: code ADDR-001 + ADDR-002, 2 migration expand-only đã áp, +68 test đơn vị

> Phiên `N78` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W18-2` · WP-18 · mã `ADDR-001`, `ADDR-002` · nhánh
> `feat/gd3-wp18-dia-chi` (base `origin/tich-hop/gd3`) · 26/11/2026.
> Phạm vi phiên: **F3 BE** — domain + app service + endpoint + migration (expand-only, `pg_dump` trước khi áp) + test đơn vị.
> Thiết kế đã tạm duyệt ở `N77` (`D-243`, M2). **Không** adapter NCC (R3-1), **không** nguồn file production (`D-93`), **không** PII/`ADDR-003`.

## 1. Đã xong

- **ADDR-001 (commit `31c7ead`)** — domain + app service + endpoint + schema:
  - Domain: `AddressLockKeys.CatalogImport`; guard khởi động `AddressSyncGuard` (BR-010, `D-78`); hàng rào 5 `ParentCode ⇒ 0008` (`D-81`); `DoiTen` bắt buộc `LegalBasis` (`0009`, `D-239`); entity append-only `CatalogSyncRun` + enum `CatalogSyncTrigger`/`CatalogSyncOutcome`; `AddressSourceHash` null-safe; Setting `Address:Cache:TtlSeconds`.
  - Application: 3 endpoint danh sách tỉnh/huyện/con; `ImportDatasetAsync` trả `CatalogSyncRunDto` (`D-241`); cache TTL 86400 s + cờ `Address:Cache:Enabled` + `AddressCacheInvalidatedEto` (`D-242`); permission `Addresses.Catalog.Import` (`D-238`); map HTTP 8 mã (`D-240`); metric NFR-008/009.
  - Worker: UoW transactional, ghi dòng lô cho **mọi** lượt (kể cả 0 thay đổi), phân loại `BiChan`/`Loi`, bỏ lượt khi bận khoá.
- **ADDR-002 (commit `86bde2b`)** — hợp đồng + tra ngược + schema:
  - Contracts hẹp `ICarrierAddressResolver` + DTO (`D-232`); lớp riêng `CarrierAddressResolver`; app service cùng lớp đúng **6** method public (`D-235`).
  - `ChuanHoaVaKiemMa` một vòng quét (`Trim`/`Ordinal`, mã rỗng/`null`/phần tử `null`/trùng ⇒ đúng **một** `0509`); hàng rào 3 chỉ đếm chuyển **mới** sang `NghiNgo` (`D-87`); `TraNguoc` fail-closed `0501/0502/0504/0505/0510` (`D-89`); `GetUnmappedCodesAsync` chỉ lượt `DaApDung` gần nhất, phân trang ≤ 1000 (`D-88`); `SourceHash` (§5.3).
  - Worker một UoW `requiresNew` bao đọc + `UpdateManyAsync(autoSave:false)` + dòng lô + dòng con (`D-90`); 2 bảng `AddrCarrierSyncRuns` + `AddrCarrierSyncRunUnmappedCodes` (`MauCanNguoiAnhXa text[]`, `D-234`); permission `Addresses.CarrierMapping.Verify` (`D-85`); map HTTP 6 mã reachable (`D-233`).
- **Test đơn vị (commit `372a33e`)** — `qa` thêm **6 tệp / 68 ca** trong `test/eCommerce.Tests/AddressSync/`, phủ 11 BR theo ánh xạ arch §6/§7: guard + hàng rào 5 + `0009` + hash (ADDR-001); quét mã/`0509` + hàng rào 3 + `TraNguoc` 7 nhánh + reflection append-only/permission/mã (ADDR-002).
- **SRS đồng bộ as-built (commit `0b2ea6f`)** — `ba` sửa 12 điểm lệch (7 ADDR-001, 5 ADDR-002): §6.1 `CatalogSyncRunDto`/`ICarrierAddressResolver`, TC-13 = 6 `DbSet`, permission, mã `0009`/`0509`/`0510`, map HTTP 8/6 mã, TTL cache, `MauCanNguoiAnhXa text[]`, enum ở Domain. Không thêm BR mới ⇒ BR có test **14/14** và **16/16**.
- **Migration EF sinh, expand-only, đã áp DB dev**:
  - `20261125191757_Add_Addr001_CatalogSyncRuns` — CREATE TABLE + 2 index + 3 CHECK (không ALTER/DROP bảng cũ).
  - `20261125193330_Add_Addr002_CarrierSyncRuns` — 2 CREATE TABLE + 4 index + FK `Restrict` + CHECK + SQL thô unique `NULLS NOT DISTINCT`.
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2021/2021** |
| `dotnet build eCommerce.slnx --no-incremental` (sau code + test) | 0 | **0 error / 9 warning** (0 warning mới) |
| `dotnet test test/eCommerce.Tests --no-build` (sau test) | 0 | **2089/2089** (+68) |
| `pwsh -File tools/kiem-tat-ca.ps1` | 0 | **10/10 bước · 304,3 s** (build · unit · integration · mermaid · cổng tài liệu · FE typecheck+Vitest · `gen:api` không đổi `schema.d.ts` · DB · test cổng) |
| `pwsh -File tools/agents/negative-control.ps1` NC-N78-1 (bỏ guard BR-010) | 0 | đột biến **exit 1 (đỏ)** → khôi phục **exit 0 (xanh)** |
| `pwsh -File tools/agents/negative-control.ps1` NC-N78-2 (bỏ hàng rào mã rỗng `0509`) | 0 | đột biến **exit 1 (đỏ)** → khôi phục **exit 0 (xanh)** |
| `pwsh -File tools/agents/check-feature-doc.ps1 -WP14` ×2 | 0 | **DAT** (16/16 heading · 14/14 và 16/16 BR có test) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 250 tệp · 947 link · 1505 backtick · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 213 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-18` | 0 | **XANH** |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** (115 dòng) |
| DB thật (psql) | 0 | 3 bảng mới + unique `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code`; migration history có `Add_Addr001`/`Add_Addr002` |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W18-2`.** DoD đạt: code 2 mã + 2 migration áp + test đơn vị + negative control + SRS đồng bộ + `kiem-tat-ca` xanh.

Các mục **cắt có nhận chủ** (không thuộc `W18-2`):

- **Testplan đầy đủ 21 BR + integration/HTTP/probe + negative control từng BR** → **`W18-4`** (F4 QA). Gồm 1 P2 runtime probe `D-233` (mã lạ ⇒ `404` + `0501`) đã ghi trong `D-243`.
- **`PostgreSqlSchemaTests`**: cập nhật số đếm index `NULLS NOT DISTINCT` **93 → 94** + thêm tên đích danh `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code` → **`W18-4`**.
- **4 call site test cũ** còn gọi overload tương thích `ApDung(kế hoạch)` — đổi sang `ApDung(kế hoạch, danhMuc.Nguon)` rồi xoá overload (xoá method thuộc `modules/**` ⇒ cần `be`) → **`W18-4`** hoặc phiên BE kế.
- **28 P3** (22 `ADDR-002`, 6 `ADDR-001`) + **`XC-004` §5.1** (số hằng/định nghĩa permission tăng 2) → **`W18-4`**/BA.
- **NFR-001 p95 tra cứu cache** chưa đo → **`W18-4`**.
- **`ADDR-002` route mới** dự kiến ABP conventional `/api/app/carrier-address-mapping/...`; `gen:api` trong `kiem-tat-ca` **không đổi `schema.d.ts`** (không drift) nhưng chưa probe HTTP sống → `W18-4`.
- **FE**: WP-18 có FE ở hàng `W18-3`; `W18-2` **0 đổi FE**, không chạy `pnpm build` riêng (cổng `kiem-tat-ca` đã chạy typecheck + Vitest + `gen:api`).

## 3. Migration đã áp

- **DB dev** `ecommerce` (PostgreSQL 17, cổng ngoài `5433`), áp bằng `dotnet run --project src/eCommerce.DbMigrator` (binary mới build), exit 0.
- **Backup trước khi áp (K-01)**: `artifacts/pg-dump/N78-W18-2.dump` — **197.121 byte** (`pg_dump -Fc`, 26/11/2026).
- Migration: `20261125191757_Add_Addr001_CatalogSyncRuns` + `20261125193330_Add_Addr002_CarrierSyncRuns` — **expand-only**: chỉ `CREATE TABLE`/index/CHECK/FK mới, **0** `ALTER`/`DROP` trên bảng có dữ liệu.
- **Đường lùi**: 3 bảng mới không được bảng cũ tham chiếu ⇒ `DROP TABLE "AddrCarrierSyncRunUnmappedCodes"` → `"AddrCarrierSyncRuns"` → `"AddrCatalogSyncRuns"`; hoặc `pg_restore` từ dump trên. Không cần lùi dữ liệu nghiệp vụ.

## 4. Quyết định mới

- **Không có `D-xx` mới** trong phiên này (không gọi `decider`; thi công đúng `D-232`…`D-243` của `N77`).
- `check-agent-artifact.ps1 -Kind decision` **DAT** (115 dòng, tới `D-243`).

## 5. Nợ / rủi ro

- **Hàng đợi xem lại**: 142 dòng (`N77`) — phiên này **không thêm** dòng `D-xx` mới.
- **Hook cảnh báo 3 lượt** (`artifacts/agents/canh-bao.jsonl`): `be` ×2 + `qa` ×1 báo "thiếu khối JSON cuối" — khối JSON **có** trong báo cáo hand-back (harness không cấp `last_assistant_message`); **0** artifact bị ảnh hưởng, **0 lần chặn dừng**. `be` lượt 1 và `ba` chạm trần 80 lượt rồi được gửi tiếp, hoàn tất.
- **Overload tương thích `ApDung(kế hoạch)`** còn trong `AdministrativeUnitSynchronizer` (4 call site test cũ dùng) — không sai hành vi nhưng là nợ dọn dẹp; chủ `W18-4`.
- **28 P3 + 1 P2 runtime probe** theo `D-243` — chưa đóng; điều kiện tạm duyệt vẫn còn.
- **Vi phạm ranh giới có sẵn** `EInvoice → Address.Domain` (ngoài WP-18, `D-77`) — không chạm.
- **`DbMigrator` cảnh báo môi trường Production + mật khẩu admin mặc định** — điều kiện dev có sẵn, không do phiên này tạo.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-18/W18-2 → mở `W18-3` (F3 FE).**

- `W18-3`: xác định WP-18 có phần FE không; nếu có, làm màn vận hành theo **hợp đồng FE tối thiểu** của 2 `-arch.md` (`ADDR-002` §6, `ADDR-001`) qua `schema.d.ts` contract-first; nếu không có ⇒ đánh `done` kèm lý do từ SRS.
- Trước khi code FE: chạy `pnpm gen:api` xác nhận route 2 app service mới có trong `schema.d.ts` (nếu đổi, ghi nhận hợp đồng).
- **Không** code adapter GHN/GHTK (`R3-1`), **không** nguồn file production (`D-93`), **không** PII/`ADDR-003`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp18-dia-chi`, base `origin/tich-hop/gd3`; mở phiên `HEAD` `1f07e13`.
- Commit phiên: `31c7ead` (ADDR-001) · `86bde2b` (ADDR-002) · `372a33e` (test +68) · `0b2ea6f` (SRS) · **commit đóng phiên** (handover này + `07` §13 + registry `agents/state/phien-gd3-1.json`).
- **Đã push** `origin/feat/gd3-wp18-dia-chi`; **chưa** đẩy `tich-hop/gd3` (chỉ ở hàng `dong-wp` `W18-5` khi G6 xanh).
- Không mở PR (`D-216`③).

---

**Hàng rào agent** (phiên `N78`): **4 vai** — `be` ×2 (ADDR-001, ADDR-002; lượt 1 chạm trần 80 rồi gửi tiếp) · `qa` ×1 (68 test đơn vị) · `ba` ×1 (SRS; chạm trần 80 rồi gửi tiếp).
**0 lần chặn dừng**; **0 tệp ngoài vùng** (`git status` trước commit chỉ có `modules/Address/**`, `src/**`, `shared/**`, `test/eCommerce.Tests/AddressSync/**`, `docs/features/address/addr-00*.md`, `docs/nhanh/**`, `agents/state/**`).
