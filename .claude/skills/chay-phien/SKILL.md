---
name: chay-phien
description: Chạy trọn một phiên WP-14 theo bảng phiên chạy docs/agents/bang-phien-wp14.md — đối soát phiên trước, tự chọn hàng kế tiếp (tools/agents/phien.ps1 next), mở phiên, chạy /doc-gate cho đúng hàng, đóng phiên (handover), cập nhật bảng bằng script, commit + push nhánh WP. Không hỏi người giữa chừng. Gọi tay — /chay-phien hoặc /chay-phien P-xx [trả lời câu chờ người].
argument-hint: "[P-xx] [trả lời câu chờ người / 'P-09 đã duyệt, biên bản ở …']"
disable-model-invocation: true
hooks:
  PreToolUse:
    - matcher: "Edit|Write|MultiEdit|NotebookEdit"
      hooks:
        - type: command
          command: node
          args: ["${CLAUDE_PROJECT_DIR}/.claude/hooks/guard-write-scope.mjs", "--orch"]
---

# /chay-phien — một lệnh cho một phiên

Tham số: `$ARGUMENTS`

Bạn là **orchestrator** (phiên chính). Chủ dự án đã dán `/chay-phien` rồi **rời máy**: làm trọn phiên từ đầu đến cuối, **không** hỏi
giữa chừng (không `AskUserQuestion`); mọi câu cần người gom vào **một** bảng ở câu trả lời cuối.
**Ngoại lệ duy nhất:** hàng loại `thao-luan` (phiên uỷ quyền `U-02`) — chủ dự án ngồi cùng, ở đó **phải** hỏi (§3.1).

**Hồ sơ uỷ quyền:** khi `docs/agents/uy-quyen-chu-du-an.md` ghi **Trạng thái: HIỆU LỰC**, decider quyết thay chủ dự án theo hồ sơ đó ⇒
bảng "Chờ người" cuối phiên chỉ còn câu thuộc §6 của hồ sơ (vùng cấm) và §5 (R3 — ai lo, khi nào). Đừng tự sửa hồ sơ ngoài `U-02`. Hook khoá bạn vào vùng ORCH
(`.claude/scopes.json` `orch.write`) — tài liệu feature do vai `ba` sửa. Luật nền: `CLAUDE.md` §3–§8, `docs/agents/bang-phien-wp14.md` §2.

Lệnh shell chạy qua **Git Bash** ⇒ đường dẫn dùng `/`. Viết tắt dưới đây: `PH` = `pwsh -File tools/agents/phien.ps1`.

**Uỷ quyền git của lệnh này:** người dùng gọi `/chay-phien` = yêu cầu **commit + push nhánh WP** (`CLAUDE.md` §8 "chỉ khi người dùng yêu cầu")
ở các điểm xanh của phiên. **Không bao giờ:** merge, mở/đóng PR, push `main`, `--force`, `git add -A`/`.`, `git config --global`, sửa lịch sử.

## 0. Chuẩn bị (≤ 5 lệnh)

0. **Kiểm vai đã nạp (trước mọi việc khác):** danh sách `subagent_type` của công cụ `Agent` phải có đủ `ba`, `docrev`, `decider`.
   Thiếu vai nào ⇒ **không** `start` hàng nào, **không** dùng `general-purpose` thay vai: chạy `node --test .claude/hooks/test/hooks.test.mjs`
   (ca `F1` chỉ đúng tệp + dòng frontmatter hỏng — Claude Code bỏ qua im lặng tệp vai có YAML sai), báo kết quả trong câu trả lời và dừng.
   Không viết handover cho phiên dừng ở bước này.
0b. **Một phiên một lúc:** có tệp `artifacts/agents/tu-dong/chay-tu-dong.lock` mà biến môi trường `CHAY_TU_DONG_PHIEN` **không** có
   (`echo "$CHAY_TU_DONG_PHIEN"` rỗng) ⇒ bộ chạy tự động đang chạy ⇒ dừng ngay, báo "đang có bộ chạy tự động — tạo
   `artifacts/agents/tu-dong/DUNG` rồi chờ nó dừng". Không đụng registry.
1. `git status --short` + `git branch --show-current`. Nhánh phải là trường `nhanh` của `agents/state/phien-wp14.json`
   (`docs/n22-wp14-dot-a`). Khác nhánh và cây sạch ⇒ `git switch <nhanh>`; cây bẩn ngoài `docs/**`, `agents/state/**` ⇒ **dừng**, báo người (không stash hộ).
2. `PH check` — đỏ vì bảng lệch ⇒ `PH sync` (không sửa tay bảng).
3. Một lần duy nhất: nếu có `artifacts/cai-dat-agent/D-bang-phien.md` ⇒ làm đúng hướng dẫn trong tệp (chèn dòng `D-xx`), rồi đổi tên tệp thành `D-bang-phien.da-chen.md`.

## 1. Đối soát phiên trước + trả lời của người

1. **Trả lời trong `$ARGUMENTS`:**
   - "P-xx đã duyệt, biên bản ở <tệp>" (hàng 👤) ⇒ kiểm tệp tồn tại ⇒ `PH done -Id P-xx -Evidence "<tệp>: <n>/<n> duyệt"`.
     Không có tệp ⇒ **không** đánh dấu, ghi vào bảng chờ người.
   - Trả lời cho câu chờ người ⇒ gọi `decider` (tuần tự) để ghi `D-xx` **người chốt** theo đúng lời người; rồi BA áp ở bước §3.
2. **Hàng 🟨 / 🔁** (`PH status`): lấy handover số lớn nhất `docs/nhanh/handover-n<x>.md`.
   - `x` lớn hơn mọi số trong cột Phiên của hàng ⇒ phiên trước chạy kiểu cũ hoặc đã đóng mà chưa cập nhật bảng ⇒ `PH doisoat -Id <hàng>`:
     exit 0 ⇒ `PH done` với đúng `-Evidence` script đề xuất + `-Handover docs/nhanh/handover-n<x>.md`; exit 1 ⇒ hàng còn việc (§2 của handover đó là việc còn lại).
     Script chỉ đếm "§2 còn nhắc mã": đọc đúng các dòng §2 có mã đó để chắc nó là việc **chưa xong** (không phải lời nhắc qua).
   - Hàng ⬜ loại `doc-gate` mà đã có `docs/reviews/<MÃ>-docreview.md` của mã trong hàng (phiên kiểu cũ làm luôn mã kế, vd `N25` làm `SHP-001`)
     ⇒ cũng `PH doisoat`; exit 0 ⇒ `PH start -Id <hàng> -Session N<x>` (đúng số phiên **cũ** đã làm — chỗ duy nhất được truyền `-Session`) rồi `PH done` như trên; exit 1 ⇒ để nguyên,
     ghi nhận để §2 chọn nó (làm tiếp từ bước đã có, không làm lại).
   - Hàng 🟨 mà **không** có handover mới hơn ⇒ phiên trước **bị cắt ngang** (hết hạn mức / đóng cửa sổ): việc dở nằm trong cây làm việc +
     `docs/reviews/<MÃ>-docreview.md` + `docs/agents/doc-gate-*.md`. Ghi nhận, làm tiếp ở §3 (không làm lại bước đã có tệp kết quả).

## 2. Chọn hàng + mở phiên

1. `$ARGUMENTS` có `P-xx` ⇒ hàng đó; không ⇒ `PH next`.
   - exit 3 (chỉ còn việc của người / bị chặn) hoặc exit 5 (hết việc) ⇒ **không mở phiên mới, không viết handover**: in bảng chờ người (§6) và dừng.
2. `PH start -Id <hàng>` (**không** truyền `-Session`): script tự cấp số phiên = max(handover, cột Phiên) + 1 và in `PHIEN=N<x>` —
   dùng đúng số đó cho handover; không tự tính (N30 bị hai phiên cùng lấy).
3. exit 4 (đã có hàng khác 🟨) ⇒ **có phiên khác đang chạy**: dừng ngay, **không** `release`, **không** `-Force` hàng của phiên khác,
   không viết handover; báo "đang có phiên khác chạy <hàng> — không chạy song song". exit 2/6 ⇒ làm theo thông báo của script.
4. **Mở phiên:** đọc `.claude/skills/mo-phien/SKILL.md` và làm đúng các bước của nó với `WP-14 N<x>` (skill đó gọi tay được ⇒ bạn **đọc tệp rồi làm**,
   không gọi Skill). Bảng baseline giữ lại cho handover §1.

## 3. Làm hàng — theo cột `loai` trong registry

| `loai` | Làm gì | DoD của hàng |
|---|---|---|
| `doc-gate` | Đọc `.claude/skills/doc-gate/SKILL.md` và chạy dây chuyền §1–§3 của nó cho **đúng** các mã trong trường `ma` của hàng (không thêm mã). Hàng 🔁 ⇒ bắt đầu từ việc còn lại ở §2 của handover/bằng chứng §1.2, không làm lại bước đã xong. | mỗi mã: `check-feature-doc.ps1 -WP14` exit 0 · DOCREV `DOC-OK` + `check-agent-artifact.ps1 -Kind docreview` exit 0 · quyết định của người trong phạm vi đã áp · D3 có dòng nháp biên bản · D4 xanh |
| `tong-hop` | Hoàn thiện `docs/agents/nhap-bien-ban-gd3-<đợt>.md`: đủ một dòng cho mọi mã của đợt (lấy số từ docreview + `check-feature-doc`), cột người review bắt buộc theo loại (`docs/design-doc-guidelines.md` §6), bảng mọi câu còn chờ người của đợt (từ `docs/agents/hang-doi-xem-lai.md` + §15 các tài liệu). Không lập biên bản thật. | tệp đủ dòng · `kiem-link-tai-lieu.ps1` PASS |
| `dong-wp` | `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-14 bước ⑤: cập nhật `docs/features/README.md` §2 theo các biên bản `docs/bien-ban-duyet-gd3-*.md` đã có; đề xuất thứ tự `§WP-15+` qua `decider` (trong phạm vi `D-70`); §13 một dòng. | biên bản A/B/C đều có · README §2 khớp · cổng link PASS |
| `chuan-bi-uy-quyen` | Làm §3.1 bước U2 — điền `docs/agents/phien-uy-quyen-cau-hoi.md`. | tệp đủ 6 mục + số câu mỗi rổ · cổng link PASS |
| `thao-luan` | Làm §3.1 bước U3 — chủ dự án ngồi cùng. | hồ sơ **HIỆU LỰC** + `D-xx` `ĐÃ CHỐT (người, phiên uỷ quyền)` · người xác nhận tóm tắt |
| `duyet-dec` | Hàng duyệt đợt đã được chuyển cho decider (`phien.ps1 uyquyen`, M2 của hồ sơ): gọi `decider` **một lần** với nháp biên bản của đợt + hồ sơ §2 M2; theo kết quả, lập `docs/bien-ban-tam-duyet-gd3-<đợt>-<ngày>.md` (tiêu đề **"TẠM DUYỆT (DEC, uỷ quyền D-xx) — chưa phải biên bản duyệt của người; không merge `main`"**), một dòng cho mỗi tài liệu: tạm duyệt / chưa + lý do; tài liệu danh sách đỏ hoặc cần kế toán mà M4 không uỷ quyền ⇒ "chưa", ghi vào hàng đợi. | biên bản tạm duyệt đủ dòng · hàng đợi có dòng xem lại · cổng link PASS |
| `nguoi` | Không bao giờ do bạn làm hay đánh dấu xong. | — |

### 3.1. Phiên uỷ quyền (`U-01` chuẩn bị, `U-02` thảo luận) — theo `docs/agents/de-xuat-uy-quyen-va-chay-tu-dong.md` §4

**U-01 (agent, không cần người):**
1. Gom câu: §15 của 18 tài liệu WP-14 (chỉ đọc §15: `grep -n "^## 15" ` rồi đọc tới hết tệp — không đọc toàn văn) · dòng `TẠM CHỐT (DEC)` / `ĐỀ XUẤT`
   trong bảng "Đang chờ" của `docs/agents/hang-doi-xem-lai.md` · M1–M6 (`de-xuat…` §11) · bộ câu chính sách mẫu (`de-xuat…` §4.3).
2. Câu đã có khuyến nghị của BA ở §15 ⇒ dùng lại, **không** gọi decider từng câu. Gọi `decider` **theo nhóm** (tiền/kế toán · NCC/hãng · phạm vi/lộ trình ·
   duyệt tài liệu/git · còn lại — tối đa 5 lượt, tuần tự), mỗi lượt: "Chế độ chuẩn bị phiên uỷ quyền: xếp các câu sau vào rổ R0–R3 theo `de-xuat…` §4.1,
   mỗi câu một khuyến nghị + tin cậy + chi phí lùi; **không** ghi `decisions.md`". Lượt này **không** tạo `D-xx`.
3. Viết `docs/agents/phien-uy-quyen-cau-hoi.md` theo khung có sẵn (đổi "Trạng thái" thành **ĐÃ CHUẨN BỊ** + ngày + số câu mỗi rổ). R1 gom thành ≤ 16 câu chính sách.

**U-02 (chủ dự án ngồi cùng — ~1–2 giờ):**
1. Mở đầu một câu: phiên này hỏi gì, bao lâu, sau phiên decider quyết thay những gì. Hỏi bằng `AskUserQuestion` (≤ 4 câu/lượt, khuyến nghị
   **đứng đầu** và ghi "(khuyến nghị)", mỗi câu tự đứng được): M1–M6 trước → R1 → R3 (ai lo, khi nào).
2. R2 và hàng đợi `D-xx`: in bảng trong chat, hỏi "đồng ý tất cả, trừ dòng…" — người gõ số dòng ngoại lệ + lựa chọn của họ.
3. Ghi ngay trong phiên (bạn là người ghi, người đang ngồi cùng):
   - `docs/agents/uy-quyen-chu-du-an.md`: điền §1–§5, đổi "Trạng thái" thành **HIỆU LỰC** từ <ngày> · `D-xx` · lịch xem lại.
   - `docs/nhanh/decisions.md` (đọc lại để lấy số kế tiếp): một dòng `D-xx` cho hồ sơ (M1–M6 + R1) và một dòng cho mỗi tài liệu có ngoại lệ R2,
     trạng thái `ĐÃ CHỐT (người, phiên uỷ quyền <ngày>)`. Dòng hàng đợi người đồng ý ⇒ đổi trạng thái `D-xx` đó thành `ĐÃ CHỐT` và chuyển dòng
     ở `hang-doi-xem-lai.md` sang "Đã xử lý" (đúng cách duyệt ở đầu tệp đó).
   - M2 = có ⇒ `PH uyquyen -Id P-09 -Evidence D-xx` (và `P-13`; `P-19` chỉ khi M4 uỷ quyền câu kế toán). `P-21` (merge) script luôn từ chối.
4. In **tóm tắt hồ sơ ≤ 1 trang**, hỏi xác nhận một lần; sửa theo lời người rồi mới `PH done -Id U-02`.
   Người dừng giữa chừng ⇒ ghi phần đã chốt (hồ sơ vẫn **CHƯA HIỆU LỰC**), `PH dodang -Id U-02 -Reason "đã xong M…, còn R…"`.

Điểm xanh trong hàng (sau **mỗi** mã qua D4): commit + push theo §5 — để phiên bị cắt ngang không mất việc.
Gặp dấu hiệu dừng (`08` §6, `/doc-gate` §2: 3 vòng cùng lỗi, context > ~70 %, quá trần làm lại) ⇒ dừng ở điểm xanh gần nhất, sang §4.

## 4. Đóng phiên

1. Đọc `.claude/skills/dong-phien/SKILL.md`, làm §1–§4 của nó (đo lại, thu thập, handover `docs/nhanh/handover-n<x>.md` 7 mục, kiểm link).
   Khối câu mở phiên A/B ở §6 của handover: **chỉ một dòng** `/chay-phien` (kèm "trả lời câu chờ người, nếu có, sau lệnh").
2. Cập nhật bảng — **chỉ bằng script**:
   - đạt DoD ⇒ `PH done -Id <hàng> -Evidence "<tên lệnh + số đo + verdict + D-xx>" -Handover docs/nhanh/handover-n<x>.md`;
   - chưa đạt ⇒ `PH dodang -Id <hàng> -Reason "<bước đang dở, việc còn lại>" -Handover docs/nhanh/handover-n<x>.md`;
   - bị chặn bên ngoài (hãng, kế toán, input thiếu) mà không làm tiếp được ⇒ `PH blocked -Id <hàng> -Reason "..."`.
3. `PH check` → exit 0; `pwsh -File tools/kiem-link-tai-lieu.ps1` → PASS.

## 5. Commit + push (điểm xanh)

1. `git status --porcelain` ⇒ chỉ nhận đường dẫn dưới `docs/` và `agents/state/`. Thấy tệp khác (`.cs`, `tools/`, `.claude/`, `CLAUDE.md`…) ⇒
   **không** add, ghi vào bảng chờ người.
2. `git add -- <từng đường dẫn>` (liệt kê cụ thể). Không add `artifacts/` (gitignore).
3. `git commit -m "docs(nhanh): N<x> - WP-14 <P-xx> <mã> <tóm tắt ASCII>"` — message **thuần ASCII** (bỏ dấu tiếng Việt).
4. `git push origin <nhanh>` (không `--force`). Lỗi push ⇒ không thử cách khác, ghi vào câu trả lời cuối (commit vẫn ở máy).

## 6. Câu trả lời cuối (chủ dự án đọc khi quay lại)

1. Một dòng: hàng nào, trạng thái mới (✅/🔁/⛔), handover, commit SHA ngắn + đã push chưa.
2. Bảng **Chờ người** (gom một lần): `# · câu · khuyến nghị (đứng đầu) · việc đã làm tiếp · trả lời bằng cách nào` — mặc định
   "dán sau `/chay-phien` ở phiên kế".
3. `PH next` sau khi đóng ⇒ "Phiên kế: mở cửa sổ mới, dán `/chay-phien`" (hoặc: "chỉ còn việc của người: <danh sách hàng 👤>").
