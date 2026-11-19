# Handover `N27` — WP-14: hàng `U-01` chuẩn bị phiên uỷ quyền xong (166 câu §15 + 37 dòng `D-xx` đã xếp rổ R0–R3)

> Phiên `N27` · 19/11/2026 · nhánh `docs/n22-wp14-dot-a` · `main` = `6052d14`. Hàng `U-01` của `docs/agents/bang-phien-wp14.md`, chạy bằng `/chay-phien U-01`.
> WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `kiem-link-tai-lieu.ps1` (mở + đóng phiên) | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 34/34 pass |
| `phien.ps1 check` | 0 | bảng khớp registry |
| `.cs` / migration đổi | — | **0** / **0** |

Việc làm:

1. **Cài `D-bang-phien`** (một lần): thêm `D-112` (bảng phiên + `/chay-phien`) và `D-113` (hồ sơ uỷ quyền) vào `docs/nhanh/decisions.md` (trạng thái `ĐỀ XUẤT`), hai dòng hàng đợi vào `docs/agents/hang-doi-xem-lai.md`; tệp nguồn đổi tên `D-bang-phien.da-chen.md`.
2. **`U-01`:** gom `206` dòng §15.1 của 18 tài liệu ⇒ `166` câu `Open` (41 dòng đã đóng bỏ qua); `decider` xếp rổ **5 lượt tuần tự** (tiền/kế toán 43 · NCC/hãng 37 · phạm vi/kiến trúc 50 · còn lại 36 · hàng đợi `D-xx` 37 + M1–M6 + câu chính sách mẫu), không tạo `D-xx`.
3. **Kết quả:** `docs/agents/phien-uy-quyen-cau-hoi.md` — R0 = 50 câu + 24 dòng `D-xx` · R1 = 58 câu gom **16** câu chính sách + 9 câu luật chơi (M1–M6, V1–V3) · R2 = 30 câu + 11 dòng `D-xx` (9 dòng ⚠) · R3 = 28 câu + 2 dòng `D-xx`, rút thành 8 việc "ai lo, khi nào"; kèm danh sách đỏ đề xuất cho M2.

Phát hiện ngoài kế hoạch (đã ghi vào tệp câu hỏi): 3 câu §15 đã cũ vì code đã trả lời (`ADDR-001-Q7`, `REP-001-Q2`, `REP-002-Q5`) + 4 câu khác dựa trên dữ kiện cũ; mẫu `de-xuat…` §4.3 ghi "rẻ nhất để lùi = T8" nhưng trong `decision-policy` đó là **T6**.

## 2. Chưa xong / cắt giữa

- `U-02` (người ngồi cùng ~1–1,5 giờ) — chưa làm; việc đầu tiên của người.
- `P-03` (`SHP-001`): docreview `DOC-OK` (0 P1, 0 P2), `check-feature-doc` exit 0 từ `N25`, nhưng `phien.ps1 start` bị chặn "phụ thuộc `U-02`" ⇒ hàng **chưa** được đóng trong bảng. Khi `U-02` xong: `phien.ps1 start -Id P-03 -Session N25` rồi `done` (`-Evidence` = số đo ở `handover-n25.md` §1, `-Handover docs/nhanh/handover-n25.md`). Không làm lại.
- Các hàng `SHP-002…SAL-006` (`P-04…P-07`) còn ⬜, đều phụ thuộc `U-02`.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

`D-112` và `D-113` — `ĐỀ XUẤT` (agent dựng hệ ngày 18/11; ORCH chỉ chèn theo tệp cài). **Không có `D-xx` mới từ decider** trong `U-01` (chế độ chuẩn bị). `D-113` phải do người chốt cùng `M1` ở `U-02`.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | Cây làm việc còn thay đổi ngoài `docs/**`: `.claude/agents/decider.md`, `.claude/scopes.json`, `.claude/settings.json`, `.claude/skills/{decision-policy,doc-gate,chay-phien}/`, `tools/agents/phien.ps1`, `test-phien.ps1` — **không** commit trong phiên này (`/chay-phien` §5 chỉ nhận `docs/` + `agents/state/`) | chủ dự án (commit tay, nhánh `chore/agents-b1-b2` hoặc nhánh WP) |
| 2 | License `ClosedXML` / `MiniExcel` / `EPPlus` (`REC-003-Q5`) là kiến thức sẵn có, chưa tra web | người kiểm lại trước khi thêm gói |
| 3 | `SHP-001-Q7` bị cắt 300 ký tự trong tệp gom — phần "`IShipmentAppService` dùng chung một class" chưa xét | BA khi mở `SHP-001`/`SHP-002` |
| 4 | Ca `Extension_btree_gist_ton_tai` (`PostgreSqlSchemaTests.cs:92`) chưa chạy lại phiên này | OPS khi có Docker |
| 5 | Hook báo 1 cảnh báo "vai `docrev` thiếu khối JSON cuối" (từ trước phiên này; `canh-bao.jsonl` 51 dòng) — không do `U-01` | ORCH phiên sau xem lại |
| 6 | `artifacts/u01/*` (bản xếp rổ thô, gitignore) chỉ nằm trên máy này — nội dung đã gộp vào tệp câu hỏi | — |

**Chờ người (một bảng):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp | Trả lời bằng |
|---|---|---|---|---|
| 1 | Chạy `U-02` (phiên ngồi cùng ~1–1,5 giờ) theo `docs/agents/phien-uy-quyen-cau-hoi.md` | làm sớm — chặn mọi hàng ⬜ | `U-01` xong; phần R0 dùng được ngay | mở cửa sổ mới, dán `/chay-phien` (`phien.ps1 next` sẽ trả `U-02`) |
| 2 | Commit tay phần `.claude/**`, `tools/agents/**` (nợ #1) | commit trước `U-02` | không đụng | người tự `git add` theo đường dẫn |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`U-01` ✅; `U-02` chờ người ngồi cùng; đợt A còn `SHP-001`(đóng bảng), `SHP-002`, `SHP-003`, `SHP-004`, `REC-003`, `SAL-006`).

1. Chủ dự án chạy `U-02` (hàng 👤 ngồi cùng) rồi dán `/chay-phien`.
2. Sau `U-02`: đóng `P-03` (nợ §2), rồi `P-04` (`/doc-gate SHP-002 SHP-004`).

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a` @ `ed39274` + commit của phiên này (SHA ngắn lấy từ `git log -1`; đã push nếu push thành công) — thay đổi ngoài `docs/**` (§5 #1) chưa commit. `main` = `6052d14`. Chưa PR.

---

**Hàng rào agent** (chỉ lượt `N27`)

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| `decider` | 5 (tuần tự, chế độ chuẩn bị) | 0 `D-xx`; không ghi tệp (đúng chỉ dẫn) |
| `ba` / `docrev` | 0 | — |
