# Handover N77 — WP-18 / hàng `W18-1` (F0–F2b) XONG: 2 thiết kế ARCH + DOCREV `DOC-OK` + G2 tạm duyệt

> Phiên `N77` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W18-1` · WP-18 · mã `ADDR-001`, `ADDR-002` · nhánh
> `feat/gd3-wp18-dia-chi` (base `origin/tich-hop/gd3` @ `7d15f84`, `HEAD` mở phiên `9a0e305`) · 26/11/2026.
> Phạm vi phiên: **F0–F2b** — đọc SRS theo mục, thiết kế kiến trúc 2 mã, DOCREV 2 vòng, G2 theo M2.
> **W18-1: 0 `.cs` sản phẩm, 0 migration, 0 package, 0 đổi FE/CI/compose.**

## 1. Đã xong

- **F0 — chuẩn bị**: cổng tài liệu `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-18` **exit 0** (XANH); đọc SRS
  `addr-001` (§0, §0.2, §5, §6, §7, §8, §9, §11, §12, §13, §14, §15, §16) và `addr-002` (§0–§18) theo mục;
  danh sách người cần duyệt theo **K-08**: `ADDR-001` (danh mục pháp lý) ⇒ **kế toán/pháp lý**; `ADDR-002`
  (tích hợp NCC + máy trạng thái `CarrierMappingStatus`) ⇒ **kiến trúc trưởng + người đã liên hệ NCC (GHN/GHTK)**.
- **F2 — ARCH `ADDR-002`**: `docs/features/address/ADDR-002-arch.md` (**482 dòng**) — 2 bảng mới
  (`AddrCarrierSyncRuns` append-only + `AddrCarrierSyncRunUnmappedCodes`), sửa lõi đồng bộ theo `D-84`/`D-86`/`D-87`/`D-90`/`D-91`,
  2 mã lỗi mới (`0509`, `0510`), tra ngược `ResolveCarrierAddressCodeAsync`/`TraNguoc`, `GetUnmappedCodesAsync`,
  permission `Addresses.CarrierMapping.Verify`, hợp đồng hẹp `ICarrierAddressResolver` (`D-232`), migration expand-only + đường lùi,
  FE contract tối thiểu, test/QA plan + 5 negative control.
- **F2 — ARCH `ADDR-001`**: `docs/features/address/ADDR-001-arch.md` (**475 dòng**) — bảng lô `AddrCatalogSyncRuns` +
  `SourceHash`, hằng `AddressLockKeys.CatalogImport`, guard khởi động BR-010 (`D-78`), hàng rào 5 `ParentCode ⇒ 0008` (`D-81`),
  `DoiTen` bắt buộc `LegalBasis` (`D-239` mã `0009`), cache BR-014 (`D-242`: TTL 86400 s + xoá qua ETO), endpoint danh sách
  tỉnh/huyện/con + `ImportDatasetAsync` (`D-241`), permission `Addresses.Catalog.Import` (`D-238`), map 8 mã HTTP (`D-240`),
  metric/ETO, migration expand-only + đường lùi.
- **F2b — DOCREV 2 vòng (ngữ cảnh sạch)**:
  - Vòng 1 **DOC-CHANGES**: `ADDR-002` **1 P1 + 4 P2** (P1: băm `SourceHash` trước khi quét `0509` ⇒ NRE khi danh mục có `null`);
    `ADDR-001` **0 P1 + 4 P2** (đo NFR bằng counter, UoW #1 `requiresNew`, thiếu `TC-12`, thiếu 1 nơi gọi test).
  - ARCH sửa + áp `D-232`…`D-242`; vòng 2: **`ADDR-002` DOC-OK 0 P1 / 1 P2 / 16 P3** (P2 còn lại = ca runtime probe `D-233`
    trong testplan — **QA phủ ở `W18-4`**, ghi trong `D-243`); **`ADDR-001` DOC-OK 0 P1 / 0 P2 / 6 P3**.
- **DEC (M2)**: **`D-232`…`D-242`** — 11 quyết định thiết kế (hợp đồng resolver + DTO, map HTTP 6/8 mã, `text[]`, chỉ lượt gần nhất,
  chấp nhận 0 dòng, không lưu tên cũ, permission nạp danh mục, mã `0009`, chỉ import trả DTO, TTL cache 86400 s);
  **`D-243`** — **G2 tạm duyệt thiết kế** 2 tài liệu để `W18-2` code trên nhánh, kèm danh sách **KHÔNG** tạm duyệt
  (merge `main`, migration chưa `pg_dump`, adapter NCC, nguồn file production `D-93`, PII/`ADDR-003`, giá trị Setting thật, danh sách đỏ).
- **Phát hiện đo được trong code (đưa vào thiết kế, không sửa code phiên này)**:
  - **0 mã `05xx` nào được map HTTP ở Host** ⇒ mọi `BusinessException` của ADDR-002 đang trả **403**; `D-233` map 6 mã reachable.
  - Outbox/inbox **đã bật** trong `eCommerceEntityFrameworkCoreModule.cs:84-88,99` (mạnh hơn ghi chú F6 của SRS — ETO cache dùng được).
  - **1 vi phạm ranh giới có sẵn, ngoài WP-18**: `EInvoice.Application` tham chiếu `Address.Domain` + `IRepository<AdministrativeUnit>`
    (`PhatHanhHoaDonGd0Flow.cs:126,132`, `csproj:12`) — nợ gắn `D-77`, ghi nhận không sửa.
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** (baseline cũ) |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2021/2021** |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | PASS · 245 tệp · 941 link · 1489 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-18` (mở + đóng phiên) | 0 / 0 | **XANH** |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/ADDR-001-arch-docreview.md` | 0 | **DAT** (verdict=DOC-OK · P1=0 · P2=0) |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/ADDR-002-arch-docreview.md` | 0 | **DAT** (verdict=DOC-OK · P1=0 · P2=1) |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** (115 dòng kiểm) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng phiên) | 0 | PASS · 249 tệp · 945 link · 1501 backtick · 0 chết |
| `npm --prefix tools run check:mermaid` (đóng phiên) | 0 | **213 khối · 0 lỗi** |
| `node --test .claude/hooks/test/hooks.test.mjs` (đóng phiên) | 0 | **49/49** |
| `dotnet build` / `dotnet test` (đóng phiên) | — | **bỏ qua có lý do**: hàng F0–F2b, **0 `.cs`** thay đổi (`git status` chỉ `docs/**` + `agents/state/**`) |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W18-1`.** DoD đạt: 2 `-arch.md` + 2 DOCREV `DOC-OK` + G2 `D-243` + cổng tài liệu XANH.

Chuyển sang hàng kế **`W18-2`** (F3 BE). Việc đã biết phải làm ở các hàng sau (không chặn `W18-2`):

- **1 P2 của `ADDR-002`**: thêm ca **runtime** `GET TraMaHangAsync` mã lạ ⇒ `404` + `error.code = eCommerce:Address:0501`
  (`0502/0503/0504/0510 ⇒ 409`; `0505 ⇒ 400`) vào testplan — **`qa` phủ ở `W18-4`**, thiếu thì `W18-4` chưa xong (`D-243`).
- **28 P3** (22 của `ADDR-002`, 6 của `ADDR-001`) — sửa **cùng nhánh/cùng PR** (`D-243`).
- **`docDeviation` SRS↔ARCH** (danh sách trong `ADDR-001-arch.md` §"docDeviation"): `ImportDatasetAsync`/`CatalogSyncRunDto`
  khác SRS §6.1; permission `Addresses.CatalogImport`; mã `0009`; map HTTP `D-240`; TTL `D-242`; DTO/interface `D-232`.
  **BA cập nhật SRS cùng PR `W18-2`** (không sửa SRS trong phiên này).
- **Không có** câu hỏi mở chặn: cả 2 `-arch.md` §9 = 0 câu mở; SRS còn `Q5` (`AllowAnonymous`) là cố ý, **không** mở route ẩn danh.

## 3. Migration đã áp

- **Không có.** Hàng F0–F2b, 0 `.cs`, 0 migration ⇒ không chạm DB, không cần `pg_dump` (K-01 không kích hoạt).
- Migration **thiết kế** (chưa sinh, `W18-2` sẽ làm): `AddrCarrierSyncRuns` + `AddrCarrierSyncRunUnmappedCodes` (ADDR-002);
  `AddrCatalogSyncRuns` (ADDR-001). **Expand-only**, một chủ migration.

## 4. Quyết định mới

- **`D-232`…`D-237`** (ADDR-002): hợp đồng hẹp `ICarrierAddressResolver` + DTO `Mapping`/`Unit` + lớp riêng;
  map HTTP 6 mã reachable; `MauCanNguoiAnhXa text[]`; chỉ lượt gần nhất; chấp nhận 0 dòng; không lưu tên cũ.
- **`D-238`…`D-242`** (ADDR-001): permission `eCommerce.Addresses.Catalog.Import`; mã `eCommerce:Address:0009 ThieuCanCuDoiTen`;
  map HTTP 8 mã; chỉ `ImportDatasetAsync` trả `CatalogSyncRunDto`; cache TTL `86400` s + `Enabled`.
- **`D-243`** — **G2 tạm duyệt thiết kế** 2 `-arch.md` (M2, khuôn `D-220`) + danh sách **KHÔNG** tạm duyệt.
- Không có `docDeviation`/`openQuestions` nào bị bỏ rơi: cả 12 dòng đều `TẠM CHỐT (DEC, uỷ quyền)`, **0** `CHUYEN_NGUOI`.

## 5. Nợ / rủi ro

- **`D-243` điều kiện**: (1) P2 runtime probe `D-233` do QA phủ ở `W18-4`; (2) 28 P3 sửa cùng nhánh; (3) cổng `MergeMain` còn **ĐỎ**
  tới khi **người** ký biên bản.
- **`D-217`④ vẫn nguyên** (chưa quá hạn): QA sửa probe đọc biên bản **người** GĐ1 + ca ngược + cùng commit xoá tệp gương.
  Hạn: trước hàng `W17-*` đầu tiên và trước PR `tich-hop/gd3 → main`.
- **`docDeviation` SRS↔ARCH (6 mục)** — chủ **BA**, đóng cùng PR `W18-2` (xem §2).
- **Vi phạm ranh giới có sẵn** `EInvoice → Address.Domain` — **ngoài WP-18**, chủ `EInvoice`, gắn nợ `D-77`; không sửa phiên này.
- **Hàng đợi xem lại**: 130 dòng (N76) **+ 12** (`D-232`…`D-243`) = **142** dòng chờ người.
- **Cảnh báo hook** (`artifacts/agents/canh-bao.jsonl`, 5 dòng cho 4 vai): `arch` ×1 (thiếu khối JSON cuối — nội dung JSON vẫn có),
  `decider` ×2 (thiếu khoá `agent`/`outcome`/`choice` trong lần trả mảng — nội dung đúng), `docrev` ×2 (hand-back không nằm ở
  `last_assistant_message`). **ORCH kiểm lại mọi số đo bằng máy** trước khi dùng; không có cảnh báo nào làm sai artifact.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-18/W18-1 (F0–F2b + G2) → mở `W18-2` (F3 BE WP-18).**

- `/chay-phien-code chuoi=gd3-1` — bộ chạy chọn hàng kế (`W18-2`).
- `W18-2`: code theo **2 tài liệu `-arch.md` đã tạm duyệt** (`D-243`), gồm domain + app service + endpoint + migration
  expand-only (**pg_dump trước khi áp** — K-01) + test đơn vị.
- **Không** code adapter GHN/GHTK (R3-1), **không** làm nguồn file production (`D-93`), **không** đụng PII/`ADDR-003`.
- BA cập nhật SRS cho 6 `docDeviation` **cùng PR**; QA ghi ca runtime probe `D-233` vào testplan ở `W18-4`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp18-dia-chi`, base `origin/tich-hop/gd3` @ `7d15f84`; mở phiên `HEAD` `9a0e305`.
- Commit phiên: **commit đóng phiên** (2 `-arch.md` + 2 `-docreview.md` + `decisions.md` `D-232…D-243` +
  `hang-doi-xem-lai.md` + handover này + `07` §13 + registry `agents/state/phien-gd3-1.json`).
- **Đã push** `origin/feat/gd3-wp18-dia-chi`; **chưa** đẩy `tich-hop/gd3` (chỉ ở hàng `dong-wp` `W18-5` khi G6 xanh).
- Không mở PR (`D-216`③).

---

**Hàng rào agent** (phiên `N77`): **4 vai** — `arch` (3 lượt: ADDR-002, ADDR-001, áp D-242) · `docrev` (2 vai × 2 vòng) ·
`decider` (3 lượt: 10 câu mở, TTL, G2). **0 lần chặn dừng**; **0 tệp ngoài vùng** (`git status` trước commit chỉ có
`docs/features/address/**`, `docs/reviews/**`, `docs/nhanh/decisions.md`, `docs/agents/**`, `agents/state/**`).
