# Handover N79 — WP-18 / hàng `W18-3` (F3 FE) XONG: **không có FE contract-first**, kết luận `done kèm lý do`; 0 dòng code FE

> Phiên `N79` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W18-3` · WP-18 · mã `ADDR-001`, `ADDR-002` · nhánh
> `feat/gd3-wp18-dia-chi` (base `origin/tich-hop/gd3`) · 26/11/2026.
> Phạm vi phiên: **F3 FE** — xác định WP-18 có phần FE contract-first để thi công hay không; nếu không ⇒ `done` kèm lý do.
> **Không** code adapter NCC (`R3-1`), **không** nguồn file production (`D-93`), **không** PII/`ADDR-003`, **không** mở hợp đồng `/api/v1`.

## 1. Đã xong

- **Kết luận F3 FE: `KHONG_CO_FE_CONTRACT` — WP-18 không có việc FE contract-first.** `fe` kiểm bằng máy, ORCH đo lại:
  - `pnpm gen:api` (root) ⇒ **51/51 path `/api/v1`**; `git diff --exit-code packages/api-client/src/schema.d.ts packages/api-client/openapi.v1.json` ⇒ **exit 0** (hợp đồng **không đổi**).
  - Snapshot `openapi.v1.json`: **0** path `address|carrier|administrative|unmapped`; **0** path ngoài `/api/v1`. `schema.d.ts` có type `eCommerce.Address.*` trong `components.schemas` nhưng **0 operation** — bẫy đọc "thấy type tưởng có endpoint".
  - `src/eCommerce.HttpApi/Controllers/`: **8** controller (Customers/Orders/Pos/Products/Reports/Settings/Stock/System), **0** controller `Address`; **0** controller tham chiếu `AdministrativeUnitAppService`/`CarrierAddressMappingAppService`.
  - `apps/admin-web/app/[business]`: **22** thư mục route, **0** route `address`/`dia-chi`; grep `address|carrier|administrative|unmapped` trong `app/`+`lib/`+`components/` = **1** hit duy nhất (hiển thị `carrierCode` của đơn ở `order/manage/[id]/page.tsx:168`), không phải màn Address.
  - BFF `app/api/proxy/[...path]/route.ts` chỉ dựng `${API_BASE_URL}/api/${path}`; **28/28** call site dùng `/api/proxy/v1/...` ⇒ FE chỉ chạm `/api/v1`, không chạm `/api/app`.
  - `git show --name-only 31c7ead 86bde2b 372a33e 0b2ea6f`: **0** tệp `src/eCommerce.HttpApi/Controllers/**`, **0** tệp `packages/api-client/**`, **0** tệp `apps/admin-web/**`.
- **Lý do từ SRS/arch (dùng cho bằng chứng `PH done`):** `ADDR-002` §10 ghi màn *"Mã địa chỉ theo hãng"* **thuộc `WEB-001`** (dữ liệu do ADDR-002 cung cấp); `ADDR-001`/`ADDR-002` §6.2 ghi endpoint là **ABP auto-API `/api/app/**`** (`ADDR-001` §6.2: "không có lớp HTTP v1", `D-79`); `ADDR-002-arch` §6 là "hợp đồng FE tối thiểu" (không thiết kế màn chi tiết), §7.1 dòng `UI` = **"Không có ca UI trong WP-18"**. Hợp đồng FE thật (`packages/api-client/scripts/snapshot.mjs:17,19`) **chỉ lọc `/api/v1`**.
- **`decider` ghi 4 dòng** (`D-244`…`D-247`) chốt: done-kèm-lý-do (`D-244`); giữ `/api/app` trong `gd3-1`, v1 là hàng BE/contract của WP màn hình (`D-245`); hoãn hardening BFF, ghi nợ chủ `fe` (`D-246`); **chuyển người** lộ trình màn `WEB-001` (`D-247`).
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error / 9 warning** |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở phiên) | 0 | **2089/2089** |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | PASS — 948 link · 1506 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-18` (điều kiện vào) | 0 | **XANH** — mọi mã WP-18 `Tạm duyệt` |
| `pnpm gen:api` (root) | 0 | **51/51 path `/api/v1`** · `schema.d.ts` **không đổi** |
| `pwsh -File tools/kiem-tat-ca.ps1` (G3) | 0 | **10/10 bước · 279,6 s** (build · unit · integration · mermaid · cổng tài liệu · FE typecheck+Vitest · `gen:api` không đổi · DB 25/25 · test cổng 60/60) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** — 116 dòng `TẠM CHỐT/ĐỀ XUẤT (DEC)` |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (sau `D-244…247`) | 0 | PASS — 948 link · **1516** backtick · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | **213 khối · 0 lỗi** |

- **0 dòng code sản phẩm, 0 test mới, 0 migration.** Diff phiên chỉ gồm `docs/nhanh/decisions.md`, `docs/agents/hang-doi-xem-lai.md`, `docs/nhanh/07-ke-hoach-trien-khai.md`, `docs/nhanh/handover-n79.md`, `agents/state/phien-gd3-1.json`, `docs/agents/bang-phien-gd3-1.md`.

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W18-3`.** DoD đạt: probe xanh + kết luận FE có bằng chứng máy + G3 xanh + `decider` ghi quyết định + cổng tài liệu xanh.

Các mục **cắt có nhận chủ** (không thuộc `W18-3`):

- **Màn vận hành `WEB-001`** (ADDR-002 §10: "Mã địa chỉ theo hãng"; ADDR-001 §10: nạp danh mục ĐVHC) → **`D-247` CHUYỂN NGƯỜI** (thêm WP ngoài `07` §WP-15+ là việc chủ dự án).
- **Bề mặt `/api/v1` cho module `Address`** (controller tường minh + `spec:snapshot` + `gen:api`) → **`D-245`**, là hàng BE/contract của WP màn hình, **không** phải `fe`; danh sách operation allowlist đã chốt trong `D-245`.
- **Hardening BFF** (allowlist tiền tố `v1` + chặn `.`/`..`/rỗng + 2 ca Vitest) → **`D-246`**, nợ chủ `fe`, không sửa trong W18-3.
- **`ADDR-002-arch` §6 + `ADDR-001-arch` :98 đang ghi `/api/app/…`** → BA + ARCH phải sửa sang `/api/v1/…` **trước** hàng F2 của WP màn hình (`D-245` ⑤).
- **Toàn bộ nợ của W18-2** (testplan 21 BR + integration/HTTP/probe, `PostgreSqlSchemaTests` 93→94, 4 call site overload `ApDung`, 28 P3, NFR-001 p95, runtime probe `D-233`) → **`W18-4`** (F4 QA + F5 REV), không thay đổi.

## 3. Migration đã áp

**Không có.** Phiên này **0** `.cs`, **0** migration, **0** `pg_dump` (không chạm DB schema). DB dev giữ nguyên trạng thái của `N78` (`Add_Addr001`/`Add_Addr002`).

## 4. Quyết định mới

- **`D-244`** (TẠM CHỐT, DEC) — `W18-3` kết luận **`KHONG_CO_FE_DONE_LY_DO`**; **0** thay đổi `apps/admin-web/**` + `packages/api-client/**`; 3 màn ghi nợ có chủ `WEB-001`.
- **`D-245`** (TẠM CHỐT, DEC) — bề mặt HTTP module `Address` **giữ `/api/app`** trong `gd3-1`; mở `/api/v1` là hàng BE/contract của WP màn hình; allowlist **3** operation `ADDR-002` + **4** operation `ADDR-001`; **cấm** đưa `ResolveCarrierAddressCodeAsync`/`PhatHienSaiLechAsync` ra v1.
- **`D-246`** (TẠM CHỐT, DEC) — BFF không kiểm tiền tố `v1`: **hoãn**, ghi nợ chủ `fe` (allowlist + chặn `.`/`..`/rỗng + 2 ca Vitest).
- **`D-247`** (ĐỀ XUẤT, DEC — **CHUYỂN NGƯỜI**) — đề xuất 1 WP `WEB-001` đợt 1 (2 màn vận hành + 1 màn nạp danh mục, có hàng BE/contract v1 đi trước); **không** thêm vào `gd3-1`.
- `check-agent-artifact.ps1 -Kind decision` **DAT** (116 dòng, tới `D-247`).

## 5. Nợ / rủi ro

- **Chờ người (một câu):** có mở WP `WEB-001` đợt 1 theo `D-247` không, và xếp sau WP nào — xem bảng "Chờ người" cuối phiên.
- **Hàng đợi xem lại:** +3 dòng (`D-244`, `D-245`, `D-246`) — tổng **145** quyết định tạm chờ người duyệt (ước tính từ 142 của `N77` + 3); `D-247` là `ĐỀ XUẤT`.
- **`decider` chạm trần 40 lượt** trước khi phát khối JSON cuối; **artifact đã ghi đủ** `D-244…D-247` (`check-agent-artifact` DAT) và nằm trong vùng ghi `docs/nhanh/decisions.md` + `docs/agents/hang-doi-xem-lai.md`; **0** tệp ngoài vùng.
- **Hook cảnh báo `fe`:** `khongXacMinh:true`/`json:false` cho lượt `fe` dù khối JSON **có** trong báo cáo hand-back (harness không cấp `last_assistant_message`/transcript đúng lúc) — khuôn đã gặp ở `N78`; **0** artifact bị ảnh hưởng, **0** lần chặn dừng.
- **Bẫy hợp đồng:** `schema.d.ts` có **type** `eCommerce.Address.*` (7 chỗ) nhưng **0** operation — DTO mới của W18-2 (`UnmappedCodesResultDto`, `CatalogSyncRunDto`) **không** có trong hợp đồng. Ai đọc type mà tưởng có endpoint sẽ sai; ghi ở `D-244`.
- **41 hit `api/proxy`**, **0** call site lệch `v1`; một phiên đã đăng nhập vẫn gọi được `/api/proxy/app/…` (không leo thang quyền, host cố định) → `D-246`.
- **Nợ W18-2 không đổi:** 28 P3 + 1 P2 runtime probe `D-233` + `PostgreSqlSchemaTests` + overload `ApDung` → `W18-4`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-18/W18-3 → mở `W18-4` (F4 QA + F5 REV).**

- `W18-4`: `qa` viết testplan cho `ADDR-001` + `ADDR-002` (tạo mới trong `W18-4`, chưa có ở phiên này) phủ **mọi** BR (14/14 · 16/16) + negative control đỏ→xanh từng BR + integration/HTTP/probe (gồm P2 runtime probe `D-233`: mã lạ ⇒ `404` + `0501`); sau đó `rev` review ngữ cảnh sạch, `VERDICT: PASS`, P1 = 0.
- Nhớ: cập nhật `PostgreSqlSchemaTests` **93 → 94** + tên `UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code`; đo NFR-001 p95; đóng 28 P3.
- **Không** code FE/màn `WEB-001` (`D-247` chờ người), **không** mở `/api/v1` (`D-245`), **không** code adapter GHN/GHTK (`R3-1`), **không** PII/`ADDR-003`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp18-dia-chi`, base `origin/tich-hop/gd3`; mở phiên `HEAD` `dc5efae`.
- Commit phiên: **commit đóng phiên** (handover này + `07` §13 + registry `agents/state/phien-gd3-1.json` + `D-244…247`). **0** commit code.
- **Đã push** `origin/feat/gd3-wp18-dia-chi`; **chưa** đẩy `tich-hop/gd3` (chỉ ở hàng `dong-wp` `W18-5` khi G6 xanh).
- Không mở PR (`D-216`③).

---

**Hàng rào agent** (phiên `N79`): **2 vai** — `fe` ×1 (xác định không có FE contract-first; trả JSON qua hand-back) · `decider` ×1 (ghi `D-244…D-247`; chạm trần 40 lượt trước khối JSON, artifact DAT). **0 lần chặn dừng**; **0 tệp ngoài vùng** (`git status` trước commit chỉ có `docs/nhanh/**`, `docs/agents/**`, `agents/state/**` — đúng vùng `decider` + ORCH).
