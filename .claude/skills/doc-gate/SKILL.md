---
name: doc-gate
description: Dây chuyền tài liệu cho orchestrator (phiên chính) — với từng mã tài liệu feature, gọi BA đối chiếu/sửa, chạy script kiểm, gửi câu hỏi chặn cho decider, gọi DOCREV kiểm độc lập (tối đa 2 vòng), soạn nháp dòng biên bản duyệt. Dùng cho WP-14 (cổng tài liệu GĐ3) và mọi lần trình duyệt tài liệu. Gọi tay — /doc-gate A | B | C | <MÃ> [MÃ…].
argument-hint: "A | B | C | <MÃ> [MÃ…]"
disable-model-invocation: true
hooks:
  PreToolUse:
    - matcher: "Edit|Write|MultiEdit|NotebookEdit"
      hooks:
        - type: command
          command: node
          args: ["${CLAUDE_PROJECT_DIR}/.claude/hooks/guard-write-scope.mjs", "--orch"]
---

# /doc-gate — dây chuyền tài liệu

Tham số: `$ARGUMENTS`

Bạn là **orchestrator** (phiên chính). Bạn **điều phối và kiểm bằng máy**, không tự sửa tài liệu feature — từ lúc gọi skill này,
hook khoá phiên chính vào vùng ORCH của `.claude/scopes.json` (`docs/agents/**`, handover, `07`, `docs/features/README.md`…) tới hết phiên.

Tiền đề: đã chạy `/mo-phien` (baseline). Luật nền: `CLAUDE.md` §3–§7, `docs/nhanh/08-session-protocol.md` §6 (dấu hiệu dừng).

## 0. Xác định danh sách mã

- `A` / `B` / `C` ⇒ đúng các mã của đợt đó trong bảng §WP-14 của `docs/nhanh/07-ke-hoach-trien-khai.md`
  (A: `ADDR-001…003`, `SHP-001…004`, `REC-003`, `SAL-006` · B: `PRO-001`, `PRO-002`, `LOY-001` · C: `REP-001…003`, `ACC-001`, `NOT-001`, `NOT-002`).
- Mã lẻ ⇒ dùng đúng các mã đó. Tìm tệp: `Glob docs/features/**/<mã viết thường>-*.md` (vd `shp-003-*.md`).
- **Gói Pro:** mỗi phiên chỉ nhận số tài liệu vừa một cửa sổ 5 giờ — mặc định **2 tài liệu**, `SAL-006` luôn **một mình**
  (`docs/agents/ke-hoach-v2-claude-code-pro.md` §10). Danh sách dài hơn ⇒ làm 2 mã đầu, ghi phần còn lại vào handover §6.

## 1. Vòng cho mỗi mã (tuần tự — không chạy hai mã song song)

**D1 — BA đối chiếu.** Gọi `Agent` với `subagent_type: "ba"`:

```text
Chế độ doi-chieu. MÃ=<MÃ>. Tệp=<đường dẫn>. Ngày=<dd/mm/yyyy>. WP-14 (D-71).
Làm đúng .claude/agents/ba.md §5. Kết thúc bằng đúng một khối JSON theo §8.
```

**D1s — kiểm máy.** Tự chạy `pwsh -File tools/agents/check-feature-doc.ps1 -Path <tệp> -WP14` (không tin `checkExit` BA báo).
Đỏ ⇒ gọi lại BA **một lần** với đúng các dòng `LOI`. Vẫn đỏ ⇒ mục §3 (bị chặn).

**D1q — câu hỏi mở.** Từ `openQuestions` của BA, **chỉ** gửi decider những câu có `blocks` khác rỗng **và** `needs = DEC`
(câu không chặn DoD của WP-14 giữ `Open` ở §15 — không tiêu lượt Opus cho chúng). Mỗi câu một lần gọi `Agent` `subagent_type: "decider"`,
các câu độc lập gọi **song song**:

```text
Câu hỏi: <question>. Phương án: <options>. BA khuyến nghị: <recommend> (<why>).
Ngữ cảnh: MÃ=<MÃ>, tệp=<đường dẫn> §<mục bị chặn>, phiên <Nx>, WP-14.
```

- `TIEN_LE` / `TAM_CHOT` ⇒ gom các `D-xx` + `choice`, gọi BA **một lần**: "Áp dụng các quyết định sau vào tài liệu: …".
- `CHUYEN_NGUOI`, và mọi câu `needs = NCC | ke-toan | nguoi` ⇒ **không dừng**: ghi vào danh sách "Chờ người" của phiên (hỏi **một lần**
  ở `/dong-phien`); BA đã thiết kế phương án trung lập theo `continueWhileWaiting`.
- Chạy lại `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` — dòng DEC mới phải có cách lùi + hạn xem lại.

**D2 — DOCREV kiểm độc lập.** Gọi `Agent` `subagent_type: "docrev"` (ngữ cảnh sạch — **không** chuyển lời giải thích của BA):

```text
MÃ=<MÃ>. Tệp=<đường dẫn>. Vòng 1. WP-14 (có mục "Đối chiếu mô hình Nhanh").
Làm đúng .claude/agents/docrev.md §5. Kết thúc bằng đúng một khối JSON theo §8.
```

Tự chạy `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/<MÃ>-docreview.md`.

- `DOC-OK` + script exit 0 ⇒ D3.
- `DOC-CHANGES` ⇒ gọi BA: "Sửa theo `docs/reviews/<MÃ>-docreview.md`, mọi P1 và P2; không sửa ngoài các mục được nêu" ⇒ D1s ⇒ DOCREV **vòng 2**
  (kèm đường dẫn docreview vòng 1 + `changedSections` của BA). Vẫn `DOC-CHANGES` ⇒ mục §3.
- `DOC-BLOCKED` ⇒ mục §3.
- **Sau `DOC-OK` không gọi BA sửa thêm** (N22: 4 chỗ sửa P2 sau DOCREV vòng 2 ⇒ không ai kiểm lại). `DOC-OK` cho phép < 3 P2 — để nguyên
  trong docreview cho người duyệt. Buộc phải sửa ⇒ gọi DOCREV **vòng kiểm nhanh**: chỉ các mục BA đổi (`changedSections`), cùng tệp docreview.

**D3 — nháp biên bản.** Thêm một dòng vào `docs/agents/nhap-bien-ban-gd3-<đợt>.md` (tạo nếu chưa có; tiêu đề ghi rõ
**"NHÁP — chưa có hiệu lực duyệt"**): `MÃ · tệp · DOCREV verdict + P1/P2 · số câu §15 còn Open · số câu chờ người · người review bắt buộc theo loại
(design-doc-guidelines §6) · đề xuất: trình duyệt / chưa`. Biên bản thật (`docs/bien-ban-duyet-gd3-<đợt>-<ngày>.md`) do **chủ dự án** lập khi duyệt.

**D4 — điểm xanh của mã.** `pwsh -File tools/kiem-link-tai-lieu.ps1` → PASS · `npm --prefix tools run check:mermaid` → 0 lỗi ·
`git status --porcelain` chỉ có `docs/**` (WP-14: 0 tệp `.cs`, 0 migration). Đây là điểm được phép cắt phiên.

## 2. Sau mỗi lượt gọi vai

- Đọc phần `[hang rao agent]` (hook `bao-orch.mjs` tự đưa vào nếu có): vai ghi **ngoài vùng** ⇒ `git restore -- <tệp>` (tệp đã theo dõi) hoặc
  xoá tệp mới đúng tên, rồi gọi lại vai đó **một lần** với nhắc phạm vi. Không bỏ qua cảnh báo.
- Đếm lượt: mỗi vai tối đa **2 lần làm lại** cho một mã. Kèm 5 dấu hiệu dừng của `08` §6 — cái nào tới trước thì dừng ở điểm xanh gần nhất.
- Không đọc lại toàn văn tài liệu để "tự kiểm" — tin script + verdict DOCREV; chỉ mở đúng dòng script báo đỏ.

## 3. Bị chặn / quá trần

Ghi `docs/agents/doc-gate-<ngày>.md` (một mục cho mỗi mã bị chặn: bước, lý do, số đo, việc cần) — **chuyển sang mã kế tiếp**, không dừng cả dây chuyền.

## 4. Kết thúc

In bảng tổng (mỗi mã: BA vòng · script exit · DOCREV verdict P1/P2 · D-xx mới · chờ người) rồi chạy `/dong-phien`.
Không tự commit trừ khi người dùng đã yêu cầu (CLAUDE.md §8).
