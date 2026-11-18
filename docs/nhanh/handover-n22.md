# Handover `N22` — WP-14 đợt A, phần 1 (`ADDR-001`, `ADDR-002`)

> Phiên `N22` · 17–18/11/2026 · nhánh `docs/n22-wp14-dot-a` (tách từ `chore/agents-b1-b2`) · `main` = `6052d14`.
> Lần chạy thật đầu tiên của dây chuyền agent (`/mo-phien` → `/doc-gate` → `/dong-phien`, `CLAUDE.md` §9).

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

**Baseline lúc mở phiên (`/mo-phien WP-14 N22`):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 141 tệp, 723 link, 730 đường dẫn, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 213 khối, 0 lỗi (sau khi `npm --prefix tools ci` — `tools/node_modules` chưa cài, thiếu `jsdom`) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 26/26 pass |
| `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | 1 | **43/50 đạt, 7 hỏng** — cả 7 (`A, A2, B, C, D, F, Y4`) cần thư mục `bin` của bản build; máy dev **chưa có bản build** (`MSB3491`, nhận chủ `WP-15`). Không do phiên này. |
| `dotnet build` / `dotnet test` | — | bỏ qua: WP thuần tài liệu (`D-71`) |

**Việc đã làm:**

1. Hệ agent B1–B2 (đang chưa commit trên `main`) **tách commit riêng** `e132f07` trên `chore/agents-b1-b2` (chưa push) — chủ dự án chọn; nhánh WP-14 tách từ đó để PR WP-14 chỉ chứa tài liệu.
2. `/doc-gate ADDR-001 ADDR-002` — tuần tự từng mã, D1 → D1s → D1q → D2 → D3 → D4:

| MÃ | BA | `check-feature-doc.ps1 -WP14` | DOCREV | D-xx | Chờ người |
|---|---|---|---|---|---|
| `ADDR-001` | đối chiếu + 2 lần áp quyết định + 1 vòng sửa P1/P2 + 1 lượt sửa nhỏ (v0.5) | exit 0 — 16/16 mục, 14/14 BR có test, 12 câu `Open` | V1 `DOC-CHANGES` 1 P1/12 P2/15 P3 → **V2 `DOC-OK` 0 P1/2 P2/22 P3** | `D-77…D-82` + Q4 = tiền lệ `XC-003` §5.4 + `D-93` | 0 (Q2 → `D-93`) |
| `ADDR-002` | đối chiếu + 2 lần áp quyết định + 1 vòng sửa P1/P2 + 1 lượt sửa nhỏ (v0.5) | exit 0 — 16/16 mục, 16/16 BR có test, 8 câu `Open` | V1 `DOC-CHANGES` 4 P1/9 P2/11 P3 → **V2 `DOC-OK` 0 P1/2 P2/14 P3** | `D-83…D-92` + `D-94` | 0 (Q12 + câu NCC `D-92` → `D-94`) |

3. Nháp biên bản `docs/agents/nhap-bien-ban-gd3-a.md` (**NHÁP — chưa có hiệu lực duyệt**): cả hai mã đề xuất **trình duyệt**.
4. Báo cáo kiểm: `docs/reviews/ADDR-001-docreview.md`, `docs/reviews/ADDR-002-docreview.md` (vòng 2).

**Đo lại lúc đóng phiên (18/11/2026):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 144 tệp, 727 link, 755 đường dẫn, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi (−1: `addr-002` bỏ sơ đồ máy trạng thái `SyncRun` 7 bước theo `D-83`) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 26/26 pass |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT — 16 dòng DEC |
| `check-agent-artifact.ps1 -Kind docreview` ×2 | 0 | `DOC-OK` · P1=0 · P2=2 (mỗi tệp) |
| `check-feature-doc.ps1 -WP14` ×2 | 0 | DAT (số ở bảng trên) |
| `git status --porcelain` | — | chỉ `docs/**` · **0** `.cs` · **0** migration |

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào sửa dở.** Cả hai mã tới điểm xanh D4.

1. ⏭ **Còn 7 tài liệu đợt A**: `ADDR-003`, `SHP-001…004`, `REC-003`, `SAL-006` (`07` §WP-14; `D-76`: 2 tài liệu/phiên, `SAL-006` một mình). Nhận chủ: phiên `N23`.
2. ⚠️ **4 chỗ sửa P2 vòng 2** (2 mỗi tài liệu, v0.5) làm **sau** DOCREV vòng 2 ⇒ **chưa** được DOCREV kiểm lại — đã ghi trong nháp biên bản; chủ dự án quyết có cần DOCREV vòng 3 trước khi ký.
3. ⏭ P3 còn lại (ADDR-001 ~20, ADDR-002 ~12) **không** chặn DoD (`DOC-OK` + 0 P1) — để vòng sửa sau hoặc khi code `WP-15`.
4. ⏭ **Duyệt** đợt A (biên bản thật `docs/bien-ban-duyet-gd3-a-<ngày>.md` + `docs/features/README.md` §2) — **chủ dự án**, sau khi đủ 9 tài liệu đợt A.

## 3. Migration đã áp

**Không có** (WP thuần tài liệu, `D-71`).

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

- **TẠM CHỐT (DEC)** — agent `decider`, đều có cách lùi + hạn xem lại, đều trong [`hang-doi-xem-lai.md`](../agents/hang-doi-xem-lai.md):
  - `ADDR-001`: `D-77` (đổi tên ĐVHC tại chỗ, bất biến ở chứng từ) · `D-78` (job đồng bộ chỉ dev/test, guard khởi động, bản ghi lô + `SourceHash`) · `D-79` (giữ `AdministrativeUnitAppService` + giá trị mã lỗi) · `D-80` (bỏ `AddrLevelPolicies`) · `D-81` (đổi `ParentCode` ⇒ từ chối cả lô `0008`) · `D-82` (khoá chung `address:sync-catalog`).
  - `ADDR-002`: `D-83` (1 bảng ánh xạ + bảng lô `AddrCarrierSyncRuns`) · `D-84` (mã hãng chỉ `Trim`, hàng rào trùng mã) · `D-85` (permission `CarrierMapping.Verify`) · `D-86` (đổi tên ⇒ hạ `NghiNgo`) · `D-87` (hàng rào 3 chỉ đếm chuyển mới) · `D-88` (bảng con mã chưa ánh xạ + `GetUnmappedCodesAsync`) · `D-89` (tra ngược fail-closed cho SHP) · `D-90` (lỗi adapter ⇒ dòng `Loi`, cấm lưu thông điệp) · `D-91` (mã rỗng ở cửa đồng bộ ⇒ từ chối cả lô) · `D-92` (dùng chung ở host, `TenantId = NULL`).
  - Hạn xem lại: **release** = `D-77`, `D-78`, `D-92`; **WP-15** = 13 dòng còn lại.
- **TIỀN LỆ** (không D-xx): `ADDR-001` Q4 — tiền tố khoá theo `XC-003` §5.4 hệ quả #2 (đã duyệt 29/10/2026).
- **Chủ dự án chốt** (18/11/2026, câu gom cuối phiên): `D-93` (nguồn ĐVHC production = file công bố của cơ quan nhà nước) · `D-94` (2 câu NCC "không có" ⇒ giữ `D-84`, `D-92`).

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Hook `check-agent-stop.mjs` báo động giả "thiếu khối JSON"** — 31 cảnh báo / 34 lượt vai ghi nhận (`json: true` chỉ 3/34), trong khi **mọi** báo cáo hand-back đều có khối JSON; `ba` bị **chặn dừng 8 lần** vì lỗi này. Nghi hook đọc tin cuối của vai, còn báo cáo đi qua kênh hand-back. Nhận chủ: bước **B3** của `docs/agents/ke-hoach-v2-claude-code-pro.md` (vùng `.claude/**`, không sửa từ trong vai). ⚠️ Sửa hook cũng phải thêm ca vào `hooks.test.mjs`.
2. ⚠️ **Hook chặn `npm`/`docker` cho `docrev`** ⇒ vai không tự chạy được `check:mermaid` / SQL `BEGIN…ROLLBACK`; ORCH chạy thay. Nhận chủ: B3.
3. ⚠️ **Trần lượt vai thấp**: `ba` chạm trần 40 lượt **2 lần**, `decider` chạm trần 20 lượt **1 lần** (đều làm tiếp bằng `SendMessage`). Một lượt `docrev` bị ngắt vì **hạn mức 5 giờ** (làm tiếp sau khi mở lại). Nhận chủ: B3/B4 (`D-72`, `D-76`).
4. 🚨 **EInvoice (trước release, `D-77`)**: `PhatHanhHoaDonGd0Flow.cs:73-92` băm `tinh.Name`/`xa.Name` lấy từ danh mục nhưng không lưu chuỗi ⇒ sau một lần `DoiTen` không dựng lại được hash. Nhận chủ: WP code đầu tiên chạm EInvoice.
5. ⚠️ **Tài liệu lệch ngoài phạm vi** (ghi nợ, không sửa): `XC-003` tự mâu thuẫn về tiền tố khoá (D4 dòng 47, TC-07/TC-09, NFR-XC-003-006 so với §5.4) · `addr-003` ADR-06 "không có `TenantId`" lệch `D-92` · `addr-002`/`implementation-plan.md:128` còn nhắc `AddressLevelPolicy` (một phần đã sửa ở `addr-002`) · `SHP-004` có bảng `ShpCarrierAddressMappings` = nguồn sự thật thứ hai (`D-89` N1). Nhận chủ: BA khi rà `ADDR-003` / `SHP-004` (phiên `N23`+); `XC-003` — phiên riêng.
6. ⚠️ **`D-93`: cơ quan / định dạng / đường dẫn file ĐVHC chính thức CHƯA XÁC ĐỊNH** — không được suy đoán (`CLAUDE.md` §6); `ImportDatasetAsync` không code được tới khi có input thật. Nhận chủ: chủ dự án, trước WP code ADDR.
7. 📌 **Toàn bộ phần code của `D-77…D-92`** (guard, bảng lô, bảng con, mã lỗi mới, permission, ~40 ca test giao QA) thuộc **WP-15+** (`D-71` ④). Nợ cũ giữ nguyên: `D-67` `C10` · `409 code = null` · build đỏ `MSB3491` (đều `WP-15`).
8. 📌 `test-cong-kiem.ps1 -ChiKiemMoc` = **43/50** trên máy dev cho tới khi build được (`MSB3491`).
9. 📌 `decisions.md`: bảng `D-xx` nằm **sát** bảng nhật ký (`| 04/11/2026 | WP-13…`) — có từ trước phiên (đã so `main`), không phải lỗi mới.

**Chờ người:** không còn câu nào — 3 câu gom cuối phiên đã được trả lời (`D-93`, `D-94`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (đợt A còn 7/9 tài liệu; DoD đợt A chưa đạt).

1. `/mo-phien WP-14 N23` — đọc §2 của handover này + `D-77…D-94`; baseline (`kiem-link-tai-lieu.ps1`, `check:mermaid`, `hooks.test.mjs`).
2. `/doc-gate ADDR-003 SHP-001` — `ADDR-003` trước (cùng module, phải đối chiếu `D-77…D-92`; sửa ADR-06 theo `D-92`). `SAL-006` để một phiên riêng.
3. Nếu chủ dự án đã quyết sửa hook (nợ §5 #1) trước phiên — chạy lại `node --test .claude/hooks/test/hooks.test.mjs`.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-14
và handover `docs/nhanh/handover-n22.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên N22 **dừng giữa** WP-14: làm tiếp **đúng phần còn lại** ghi ở handover §2 — **không** làm lại phần đã
xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```

## 7. Trạng thái nhánh

- `chore/agents-b1-b2` @ `e132f07` — hệ agent B1–B2 (`D-72…D-76`), **chưa push**, chưa PR.
- `docs/n22-wp14-dot-a` (tách từ `e132f07`) — thay đổi phiên này **chưa commit**, chưa push, chưa PR. Thứ tự merge đề xuất: PR `chore/agents-b1-b2` trước, rồi PR WP-14 (mở đầu `Refs: D-71`).
- `main` = `6052d14` (không đổi).

---

**Hàng rào agent** (`artifacts/agents/2026-11-17.jsonl` + `2026-11-18.jsonl`, `canh-bao.jsonl`)

| Vai | Lượt dừng ghi nhận | Nhận diện JSON | Lần chặn dừng | Ghi ngoài vùng |
|---|---|---|---|---|
| `ba` | 13 | 3 | 8 | 0 |
| `decider` | 17 | 0 | 0 | 0 |
| `docrev` | 4 | 0 | 0 | 0 |
| **Tổng** | 34 | 3 | 8 | **0** |

Cảnh báo: 31 dòng — **toàn bộ** là "thiếu khối JSON" (báo động giả, §5 #1); **0** lần ghi ngoài vùng. Hạn mức gói Pro: **chưa đo** (chạm hạn mức 5 giờ một lần giữa `docrev` ADDR-002 vòng 2).
