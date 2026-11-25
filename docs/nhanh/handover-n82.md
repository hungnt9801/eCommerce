# Handover N82 — WP-22 / W22-0 (cổng tài liệu `D-208`, `NOT-001`)

> Chuỗi `gd3-1` · hàng `W22-0` · nhánh `feat/gd3-wp22-thong-bao` · base `origin/tich-hop/gd3` (`58326ba`) · ngày 26/11/2026.
> Hàng trước: `W18-5` (`N81`, [handover-n81](handover-n81.md)). Hàng này mở WP-22 (module `Notification` mới, `D-197`).

## 1. Đã xong

**Baseline mở phiên** (WP thuần tài liệu — `doc-gate`; build/test bỏ qua có lý do "WP thuần tài liệu"):

| Lệnh | Exit | Số đo |
|---|---|---|
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49 pass** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 260 tệp · 951 link · 1596 đường dẫn |
| `npm --prefix tools run check:mermaid` | 0 | **213 khối · 0 lỗi** |

**Cổng tài liệu `D-208` (6) — đóng 2 P2 #24/#26 của `NOT-001` (v0.4 → v0.5):**

- `ba` sửa `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` lên **`v0.5 — 26/11/2026`**:
  - **#24** — thêm **hàng 15** `DangBanGiao → HetHan` (guard `NOT-002` lỗi tạm thời **và** `now > ExpiresAtUtc`, event `Expired`,
    `AttemptCount` không tăng); hàng 14 dẫn hàng 15; sơ đồ §3 + §12 + `TC-05` (14 → **15** hàng) nhất quán.
  - **#26** — nhánh đua `23505` ghi lần hai trong **UoW MỚI `requiresNew`** sau khi UoW lỗi rollback (cấm dùng lại transaction
    đã abort ⇒ `25P02`); áp ở §3 hàng 13 · §7.4 L5 · §12 · "Chiến lược transaction"; `TC-30` assert **đúng 1** `QueueEvent(Suppressed)`.
  - §15.4 thêm dòng `v0.5`.
- `docrev` **vòng 4 — kiểm nhanh** (`docs/reviews/NOT-001-docreview.md`): **VERDICT `DOC-OK` · 0 P1 · 0 P2**;
  #24/#26 **đã đóng**, P3 #25 **đóng kèm**; 1 P3 **mới** #29 (`ExpiresAtUtc IS NULL` ở hàng 14 — không chặn verdict).
- `decider` ghi **`D-253`** (TẠM CHỐT DEC): điều kiện `D-208` (6) **đã đạt**, `NOT-001` v0.5 tạm duyệt **có hiệu lực cho code
  WP-22 trên nhánh**; merge `main` **vẫn** cần biên bản người ký (`D-71`/§A.5). Thêm 1 dòng hàng đợi xem lại.
- ORCH cập nhật `docs/bien-ban-tam-duyet-gd3-c-2026-11-24.md`: dòng `NOT-001` → **`Tạm duyệt`** (`D-253`), DOCREV vòng 4 0 P2;
  tóm tắt §1 + xác nhận kiến trúc §2 + header/hàng-đợi-link đồng bộ.

**Đo lại cuối phiên:**

| Lệnh | Exit | Số đo |
|---|---|---|
| `check-feature-doc.ps1 -Path …/not-001-…md -WP14` | 0 | **16/16** heading · **25/25** BR có test · 12 câu §15 `Open` · 0 TODO |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/NOT-001-docreview.md` | 0 | `verdict=DOC-OK · P1=0 · P2=0` |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | 116 dòng; riêng `-Id D-253` exit 0 |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-22` | 0 | XANH — "mọi mã của WP-22 đều 'Tạm duyệt' trong biên bản tạm duyệt" |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 260 tệp · 951 link · **1602** đường dẫn |
| `npm --prefix tools run check:mermaid` | 0 | **213 khối · 0 lỗi** |

**Commit phiên:** `2bc9478` (content: NOT-001 v0.5 · docreview vòng 4 · `D-253` · hàng đợi · biên bản đợt C).

## 2. Chưa xong / cắt giữa

Không có phần dở. Mọi mục cắt **có nhận chủ**:

- **P3 #29** (mới, `ExpiresAtUtc IS NULL` ở hàng 14) + **#27/#28** + **12 P3 vòng 1/2** → sửa **cùng WP-22 code row** (`W22-1…W22-5`);
  không xoá test/không TC rỗng.
- **Merge `main`** vẫn cần biên bản **người ký** (`D-71`, `implementation-plan.md` §A.5, `D-115` ⑤ — cổng `MergeMain` còn ĐỎ).
- **Hook cảnh báo:** `docrev` bị báo "thiếu khối JSON cuối" do harness không cấp `last_assistant_message` đúng lúc — **khối JSON có**
  trong báo cáo hand-back; **0** artifact ảnh hưởng, **0** lần chặn dừng.

## 3. Migration đã áp

**Không có.** WP thuần tài liệu: 0 migration, 0 tệp `.cs`, 0 `pg_dump` (không chạm DB).

## 4. Quyết định mới

- **`D-253`** — `TẠM CHỐT (DEC, uỷ quyền `D-114`/`D-115`, khuôn `D-208` (6))`: điều kiện `D-208` (6) **đã đạt** ⇒ `NOT-001` v0.5
  tạm duyệt **có hiệu lực cho code WP-22 trên nhánh** `feat/gd3-wp22-thong-bao`; **merge `main` vẫn cần người ký**. ④ Lùi + ⑤ Xem lại
  trước không rỗng (máy: `check-agent-artifact -Kind decision -Id D-253` exit 0).
- **Câu chờ người:** **0** câu mới (`decider` trả `TAM_CHOT`, không có `CHUYEN_NGUOI`).

## 5. Nợ / rủi ro

- **P3 #29 + #27/#28 + P3 vòng 1/2** (chủ `be`/`ba`) — không chặn; xử lý trong WP-22 code row.
- **Merge `main` ĐỎ** tới khi có `docs/bien-ban-duyet-gd3-c-<ngày>.md` có `Đã duyệt` + `Người ký` (P-21, việc của người).
- **Hàng đợi xem lại:** **151** quyết định tạm chờ người duyệt (N81: 150; +`D-253`).
- **Không** có nợ chặn mở `W22-1`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W22-0` → mở `W22-1` (WP-22, F0–F2b).**

- `W22-1`: `ORCH · arch · docrev · decider` — F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2
  (module `Notification` mới, `D-197`; 15 P3 cùng WP). Hàng kế trong `docs/agents/bang-phien-gd3-1.md`.
- Trước `migrations add` module `Notification`: **bắt buộc** `pg_dump` nếu DB dev có dữ liệu (K-01, `D-75`); xem lại `D-253` ⑤.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp22-thong-bao`, base `origin/tich-hop/gd3` (`58326ba`) — **cắt từ đỉnh nhánh tích hợp** (phương án B, `D-216`).
- Commit nội dung: **`2bc9478`** (`docs(notification): N82 - WP-22 W22-0 doc-gate D-208 close P2 24/26 + D-253`).
- **Commit đóng phiên** (handover này + `07` §13 + registry `PH done`) — SHA ghi ở câu trả lời cuối.
- **Đã push** `origin/feat/gd3-wp22-thong-bao` (chưa đẩy `tich-hop/gd3` — chỉ đẩy ở hàng `dong-wp`).
- Không mở PR (`D-216` ③); PR xếp chồng mở một lần cuối chuỗi.

---

**Hàng rào agent** (phiên `N82`): **3 lượt vai** — `ba` ×1 (NOT-001 v0.5, 2 P2), `docrev` ×1 (vòng 4 kiểm nhanh), `decider` ×1
(`D-253`). **0 lần chặn dừng**; **0** ghi ngoài vùng; 1 cảnh báo "thiếu JSON" của harness cho `docrev` (khối JSON thật có).
