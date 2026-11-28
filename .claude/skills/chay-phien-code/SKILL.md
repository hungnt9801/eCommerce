---
name: chay-phien-code
description: Chạy trọn một phiên của chuỗi WP code GĐ3 theo bảng docs/agents/bang-phien-<chuỗi>.md — tự chọn hàng (tools/agents/phien.ps1 -Chuoi), chuyển/cắt nhánh WP xếp chồng trên nhánh tích hợp, làm đúng bước của hàng (doc-gate / code qua feature-pipeline / dong-wp), đóng phiên, cập nhật bảng, commit + push nhánh WP; hàng dong-wp đẩy fast-forward vào nhánh tích hợp (M3, D-114). Không hỏi người giữa chừng, không mở PR mỗi phiên. Gọi tay — /chay-phien-code chuoi=gd3-1 [P-xx] [trả lời câu chờ người].
argument-hint: "chuoi=<tên> [W16-1] [trả lời câu chờ người / 'W17-2 đã duyệt, biên bản ở …']"
disable-model-invocation: true
hooks:
  PreToolUse:
    - matcher: "Edit|Write|MultiEdit|NotebookEdit"
      hooks:
        - type: command
          command: node
          args: ["${CLAUDE_PROJECT_DIR}/.claude/hooks/guard-write-scope.mjs", "--orch"]
    - matcher: "mcp__gitnexus__.*|mcp__codegraph__.*"
      hooks:
        - type: command
          command: node
          args: ["${CLAUDE_PROJECT_DIR}/.claude/hooks/guard-orch-mcp.mjs"]
---

# /chay-phien-code — một lệnh cho một phiên của chuỗi WP code

Tham số: `$ARGUMENTS` · Thiết kế: `docs/agents/chay-tu-dong-code.md` (phương án **B** — nhánh xếp chồng, `D-xx` ghi ở §0 tệp đó).

Bạn là **orchestrator**. Chủ dự án (hoặc `tools/agents/chay-tu-dong.ps1 -Chuoi <tên>`) đã gọi lệnh rồi **rời máy**: làm trọn phiên,
**không** `AskUserQuestion` (ngoại lệ duy nhất: hàng `thao-luan`, §3.1); mọi câu cần người gom vào **một** bảng ở câu trả lời cuối. Hồ sơ uỷ quyền `docs/agents/uy-quyen-chu-du-an.md`
(HIỆU LỰC) cho phép: **M2** decider tạm duyệt tài liệu `DOC-OK` **ngoài danh sách đỏ** (§2.1) để code trên nhánh; **M3** agent đưa code vào
nhánh tích hợp khi cổng máy xanh — **`main` chỉ người**.

Viết tắt: `PH` = `pwsh -File tools/agents/phien.ps1 -Chuoi <tên>` (tên lấy từ `chuoi=` trong `$ARGUMENTS`; thiếu thì lấy biến môi trường
`CHAY_TU_DONG_CHUOI`; cả hai rỗng ⇒ dừng, báo "thiếu chuoi="). Lệnh shell qua **Git Bash** ⇒ đường dẫn `/`.

Luật kế thừa từ `.claude/skills/chay-phien/SKILL.md` (đọc mục đầu tệp đó một lần): gọi vai **tiền cảnh** (`run_in_background: false`), đọc tệp
bằng `Read`/`Grep` (không chuỗi shell ghép), phiên chính **không** gọi `mcp__gitnexus__*`/`mcp__codegraph__*`, giao việc cần đối chiếu code thì
dặn vai dùng đồ thị trước `Grep`.

**Uỷ quyền git của lệnh này:** gọi `/chay-phien-code` = yêu cầu commit + push **nhánh WP của hàng**, và — chỉ ở hàng `dong-wp` khi G6 xanh —
push **fast-forward** nhánh WP lên nhánh tích hợp (`nhanhTichHop` của registry). **Không bao giờ:** merge/push `main`, mở/đóng/merge PR,
`--force`/`--force-with-lease`, `git add -A`/`.`, `git config --global`, sửa lịch sử (`rebase`, `reset --hard`, `commit --amend` sau push).

## 0. Chuẩn bị

0. **Vai đã nạp:** danh sách `subagent_type` của `Agent` phải có `ba`, `docrev`, `decider`, `arch`, `be`, `fe`, `qa`, `rev`, `ops`. Thiếu ⇒
   `node --test .claude/hooks/test/hooks.test.mjs` (ca `F1`), báo và dừng (không handover).
0b. **Một bộ chạy một lúc:** có `artifacts/agents/tu-dong/chay-tu-dong.lock` mà `CHAY_TU_DONG_PHIEN` rỗng ⇒ dừng, báo "đang có bộ chạy tự động".
1. `git fetch origin` · `git status --short` · `git branch --show-current`. Cây bẩn ngoài `docs/**`, `agents/state/**` ⇒ **dừng**, báo người
   (không stash hộ). `PH check` đỏ vì bảng lệch ⇒ `PH sync`.
2. Hạ tầng dev: `docker compose ps` có `ecommerce-postgres` + `ecommerce-redis` đang chạy (cổng ngoài theo `.env` — máy dev dùng 5433/6380,
   ứng dụng đọc `ConnectionStrings__Default`/`Redis__Configuration` từ biến môi trường). Không chạy ⇒ `docker compose up -d` **một lần**; vẫn hỏng ⇒ dừng.

## 1. Trả lời của người + đối soát

- `$ARGUMENTS` có "W.. đã duyệt, biên bản ở <tệp>" (hàng 👤) ⇒ chuyển sang nhánh của hàng (§2.2) ⇒ kiểm tệp tồn tại ⇒
  `PH done -Id <hàng> -Evidence "<tệp>: duyệt"`; không có tệp ⇒ không đánh dấu, ghi bảng chờ người.
- Trả lời câu chờ người ⇒ `decider` ghi `D-xx` **người chốt** đúng lời người; vai liên quan áp ở §3.
- Hàng 🟨 mà handover lớn nhất **không** mới hơn cột Phiên ⇒ phiên trước bị cắt ngang: việc dở nằm trên nhánh của hàng (commit cuối +
  cây làm việc). Làm tiếp, không làm lại bước đã có commit/tệp kết quả.

## 2. Chọn hàng + nhánh + mở phiên

1. `$ARGUMENTS` có mã hàng ⇒ hàng đó; không ⇒ `PH next`. exit 3/5 ⇒ không mở phiên, in bảng chờ người (§6), dừng.
2. `PH show -Id <hàng>` ⇒ đọc `hang.nhanh`, `hang.buoc`, `hang.loai`, `hang.ma`, `chuoi.nhanhTichHop`.
   - **Nhánh của hàng** (phương án B): có ở máy ⇒ `git switch <nhanh>`; chỉ có ở `origin` ⇒ `git switch -c <nhanh> --track origin/<nhanh>`;
     chưa có ⇒ `git switch -c <nhanh> origin/<nhanhTichHop>` (WP mới **cắt từ đỉnh nhánh tích hợp** = đã chứa mọi WP trước của chuỗi).
   - `origin/<nhanhTichHop>` không tồn tại ⇒ dừng: "người tạo nhánh tích hợp trước (M3)".
   - Sau khi chuyển nhánh: `PH check` (registry trên nhánh này là bản đang dùng) — hàng phải vẫn là ⬜/🔁/🟨 như ở bước 1, lệch ⇒ dừng, báo.
3. `PH start -Id <hàng>` (không `-Session`) ⇒ dùng đúng `PHIEN=N<x>`. exit 4 ⇒ có phiên khác: dừng, không `release`/`-Force`.
4. **Mở phiên:** đọc `.claude/skills/mo-phien/SKILL.md`, làm đúng các bước với `<WP> N<x>` (WP = `hang.dot`). Giữ bảng baseline cho handover.

## 3. Làm hàng — theo `loai`

| `loai` | Làm gì | DoD (ORCH tự chạy lệnh, không tin số vai báo) |
|---|---|---|
| `doc-gate` | Cổng tài liệu của WP (`07` §WP-15+ cột "Cổng tài liệu", nội dung cụ thể ở `hang.buoc`): đọc `.claude/skills/doc-gate/SKILL.md`, chạy §1–§3 cho **đúng** mã `hang.ma` — BA sửa đúng các P2/P3 được nêu, DOCREV kiểm nhanh. Mã ngoài danh sách đỏ đạt `DOC-OK` ⇒ `decider` (M2) cập nhật dòng của mã trong `docs/bien-ban-tam-duyet-gd3-*.md` thành `Tạm duyệt` + `D-xx`. Mã **đỏ** ⇒ không tạm duyệt: hàng `nguoi` kế tiếp sẽ chờ người ký. | DOCREV `DOC-OK` + `check-agent-artifact.ps1 -Kind docreview` exit 0 · `kiem-link-tai-lieu.ps1` PASS · (mã không đỏ) `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP <WP>` exit 0 |
| `code` | **Điều kiện vào:** `probe-cong-tai-lieu.ps1 -CheDo Code -WP <WP>` exit 0 (khác 0 ⇒ `PH blocked`, không code). Đọc `.claude/skills/feature-pipeline/SKILL.md` và chạy **chỉ** các bước trong `hang.buoc` (vd `F0–F2b`, `F3 BE`, `F3 FE`, `F4–F5`) với `hang.ma` (WP hạ tầng không mã ⇒ lý do ở `hang.noiDung`). G1/G2 "người duyệt" ⇒ theo M2: `decider` tạm duyệt khi mã không đỏ và DOCREV `DOC-OK`; mã đỏ ⇒ `PH blocked -Reason "G1/G2 cần người (danh sách đỏ)"`. Không có phần FE ⇒ hàng `F3 FE` `done` với bằng chứng "WP không có phần FE: <lý do từ SRS>". | Cổng G3 của `feature-pipeline` §3 (build 0 error + 0 warning mới · số ca test không giảm · FE nếu có · `kiem-tat-ca.ps1` exit 0) · bước `F4` ⇒ G4 · `F5` ⇒ G5 · test mới có `negative-control.ps1` đỏ→xanh (sau commit) |
| `dong-wp` | F6 `ops` + G6: `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (**không** chạy song song build/test khác — máy dev từng hết bộ nhớ, N69) · `docs/ops/<MÃ>-deploy.md` có đường lùi · `07` §13 một dòng. Xanh ⇒ commit + push nhánh WP rồi **đưa vào nhánh tích hợp**: `git push origin <nhanh>:<nhanhTichHop>` (chỉ fast-forward; bị từ chối ⇒ **không** force, không merge, `PH dodang -Reason "nhánh tích hợp đã đi tiếp — cần người xem"`). | G6 exit 0 (số bước + thời gian) · G4/G5 của WP còn xanh · push lên `nhanhTichHop` thành công (SHA) |
| `thao-luan` | **Phiên uỷ quyền có người ngồi cùng** (ngoại lệ duy nhất được hỏi — §3.1). Bộ chạy tự động **dừng** trước hàng này. | `D-xx` **ĐÃ CHỐT (người, phiên uỷ quyền <ngày>)** · hồ sơ uỷ quyền §2.1 có dòng ngoại lệ · `PH uyquyen` xong các hàng 👤 người đồng ý giao · người xác nhận tóm tắt |
| `duyet-dec` | Hàng 👤 đã giao cho decider (`PH uyquyen`, theo `D-xx` của `thao-luan`): gọi `decider` **một lần** với đúng tiêu chí của `D-xx` + bằng chứng của cổng (G1: docreview `DOC-OK` của các mã vừa sửa; G2: `docs/features/**/<MÃ>-arch.md` + docreview của nó). Đạt mọi tiêu chí ⇒ thêm dòng vào `docs/bien-ban-tam-duyet-gd3-wp17-<ngày>.md` (khuôn bảng **y hệt** `docs/bien-ban-tam-duyet-gd3-a-*.md`: cột `Mã`, `Kết luận DEC` bắt đầu bằng `Tạm duyệt`; tiêu đề **"TẠM DUYỆT (DEC, uỷ quyền D-xx) — chưa phải biên bản người; chặn release tới khi kế toán duyệt lô M4; không merge `main`"**) + dòng hàng đợi `docs/agents/hang-doi-xem-lai.md`. Thiếu tiêu chí nào / decider `CHUYEN_NGUOI` ⇒ **không** tạm duyệt: `PH blocked -Reason "<tiêu chí thiếu>"`, ghi bảng chờ người. | biên bản tạm duyệt có dòng · `probe-cong-tai-lieu.ps1 -CheDo Code -WP <WP>` exit 0 · `kiem-link-tai-lieu.ps1` PASS |
| `nguoi` | Không bao giờ do bạn làm hay đánh dấu xong (trừ §1 khi người trả lời kèm tệp). | — |

### 3.1. Phiên uỷ quyền (`thao-luan`) — chủ dự án ngồi cùng

Khuôn `U-02` của `.claude/skills/chay-phien/SKILL.md` §3.1, thu hẹp cho **một** WP:
1. Mở đầu một câu: phiên hỏi gì, bao lâu (~30–60 phút), sau phiên decider được duyệt thay **những hàng 👤 nào**, và cái gì **vẫn** của người
   (release: kế toán duyệt lô M4; merge `main`: biên bản người ký — `D-115` ⑤; vùng cấm `CLAUDE.md` §5 không đổi).
2. `decider` (chế độ chuẩn bị, **không** ghi `decisions.md`): đọc hồ sơ uỷ quyền §2.1, các tài liệu trong `hang.ma`, các `D-xx` liên quan
   (`07` cột "Cổng tài liệu" của WP) ⇒ trả **≤ 8 câu**, mỗi câu: tiêu chí duyệt thay đề xuất + khuyến nghị + rủi ro + chi phí lùi. Bộ câu tối thiểu:
   giao G1 không · giao G2 không · tiêu chí bắt buộc (DOCREV `DOC-OK` 0 P1/P2 · không đổi `XC-001`/làm tròn · công thức theo đúng SRS ·
   mọi tiền qua `Money` · ca test domain invariant K-06) · điều gì luôn chuyển người (lệch SRS về tiền, phá huỷ schema, đổi contract liên module).
3. Hỏi bằng `AskUserQuestion` (≤ 4 câu/lượt, khuyến nghị **đứng đầu** ghi "(khuyến nghị)", mỗi câu tự đứng được).
4. Ghi ngay (bạn ghi, người đang ngồi cùng): `docs/nhanh/decisions.md` một dòng `D-xx` (đọc lại để lấy số kế tiếp) — tiêu chí + phạm vi +
   `reviewBefore = release`, trạng thái `ĐÃ CHỐT (người, phiên uỷ quyền <ngày>)`; `docs/agents/uy-quyen-chu-du-an.md` §2.1 thêm **một dòng
   ngoại lệ** trỏ `D-xx` (không sửa dòng cũ). Người đồng ý giao hàng nào ⇒ `PH uyquyen -Id <hàng> -Evidence D-xx` cho đúng hàng đó.
5. In tóm tắt ≤ ½ trang, hỏi xác nhận một lần ⇒ `PH done -Id <hàng thao-luan> -Evidence "D-xx; uyquyen <hàng…>"`. Người không giao ⇒ hàng 👤
   giữ nguyên; người dừng giữa chừng ⇒ ghi phần đã chốt, `PH dodang`.

**Migration:** áp bằng `dotnet run --project src/eCommerce.DbMigrator` (`dotnet ef database update` bị **cấm** trong `.claude/settings.json`). Trước **mọi** lần áp trên DB dev có dữ liệu ⇒
`mkdir -p artifacts/pg-dump && docker exec ecommerce-postgres pg_dump -U ecommerce -Fc ecommerce > artifacts/pg-dump/N<x>-<hàng>.dump`
và ghi kích thước tệp vào handover §3. Không có bản dump ⇒ không migrate (K-01). Migration do EF sinh, expand-only, **một chủ** (chuỗi chạy tuần tự).

**Điểm xanh trong hàng:** sau mỗi vai qua cổng máy của bước đó ⇒ commit + push nhánh WP (§5) để phiên bị cắt ngang không mất việc.
**Dấu hiệu dừng** (`feature-pipeline` §4, `08` §6): 3 vòng cùng lỗi · context > ~70 % · > 5 tệp ngoài phạm vi · test đỏ ngoài dự kiến
(**không** sửa test cho xanh) · quá trần 2 vòng/vai ⇒ dừng ở điểm xanh gần nhất, sang §4. Cây đỏ mà không về được điểm xanh ⇒ **không commit**
phần đỏ: `git stash push -m "N<x> do - <hàng>" -- <từng tệp>` (không xoá), ghi tên stash vào handover §2 và `PH dodang`.

## 4. Đóng phiên

1. Đọc `.claude/skills/dong-phien/SKILL.md`, làm §1–§4 (đo lại, handover `docs/nhanh/handover-n<x>.md` 7 mục, kiểm link). Khối câu mở phiên
   ở §6: **một dòng** `/chay-phien-code chuoi=<tên>`.
2. Bảng — **chỉ bằng script**: đạt DoD ⇒ `PH done -Id <hàng> -Evidence "<lệnh + số đo + verdict + D-xx + SHA>" -Handover docs/nhanh/handover-n<x>.md`;
   chưa ⇒ `PH dodang -Reason "<bước dở, việc còn lại>" -Handover …`; chặn ngoài ⇒ `PH blocked -Reason "…"`.
3. `PH check` exit 0 · `pwsh -File tools/kiem-link-tai-lieu.ps1` PASS.

## 5. Commit + push

1. `git status --porcelain`. Mỗi tệp đổi phải thuộc vùng ghi của vai đã làm nó (`.claude/scopes.json`) hoặc vùng ORCH; tệp ngoài vùng ⇒
   `git restore -- <tệp>` (khuôn `feature-pipeline` §5.2) và ghi cảnh báo. **Không** add: `artifacts/`, `.env*`, tệp bí mật,
   `.github/workflows/ci.yml` (tệp bảo vệ — đổi CI là việc người).
2. `git add -- <từng đường dẫn>`; `git commit -m "<loại>(<phạm vi>): N<x> - <WP> <hàng> <tóm tắt ASCII>"` (loại: `feat`/`fix`/`test`/`docs`/`chore`;
   **thuần ASCII**).
3. `git push origin <nhanh>` (lần đầu: `-u`). Lỗi push ⇒ không thử cách khác, ghi vào câu trả lời cuối.
4. **Không** mở PR — PR xếp chồng mở một lần cuối chuỗi (`chay-tu-dong.ps1 -MoPRCuoi`). CI chạy nhờ **PR nháp** `nhanhTichHop` → `main` do người mở.

## 6. Câu trả lời cuối

1. Một dòng: hàng, trạng thái mới (✅/🔁/⛔), handover, nhánh + SHA ngắn, đã push nhánh WP / nhánh tích hợp chưa.
2. Bảng **Chờ người** (gom một lần): `# · câu · khuyến nghị (đứng đầu) · việc đã làm tiếp · trả lời bằng cách nào` — mặc định
   "dán sau `/chay-phien-code chuoi=<tên>` ở phiên kế".
3. `PH next` sau khi đóng ⇒ hàng kế, hoặc "chỉ còn việc của người: <hàng 👤>".
