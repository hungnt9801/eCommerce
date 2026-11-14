# Kế hoạch: dựng hệ sub-agent theo vai trò cho `eCommerce` trên Claude Code

> **Trạng thái:** Draft — chờ duyệt · **Ngày lập:** 14/11/2026
> **Nguồn tham chiếu:** dự án `D:\Projects\Tools\SelfLearningAI` (bộ `agents/roles/*/ROLE.md`, `agents/state/`,
> `scripts/wp.ps1`, `scripts/check-*.ps1`, `docs/09-process/*`) — dựng cho **DeepSeek Harness**.
> **Phạm vi:** chuyển mô hình "dây chuyền nhiều vai + cổng kiểm bằng máy" sang `eCommerce`, dùng các cơ chế
> **gốc** của Claude Code thay cho các cách mô phỏng mà DeepSeek Harness buộc phải dùng.
> **Chưa làm gì ngoài file này.** Không có file nào khác được tạo/sửa.

---

## 0. Tóm tắt trong 12 dòng

1. `SelfLearningAI` chạy theo **dây chuyền 9 vai** (ORCH · BA · ARCH · BE · FE · QA · REV · DOCREV · OPS), bàn giao
   bằng **file**, chuyển bước bằng **cổng G1–G6**, và chỉ tin **exit code** — không tin lời agent.
2. `eCommerce` **đã có** nửa sau của mô hình đó, và đã tốt hơn bản gốc ở chỗ này: cổng `kiem-tat-ca.ps1` (9/19 bước),
   CI `cong-15-buoc` + `docker-build` là required check, quy ước phiên/handover (`docs/nhanh/08-session-protocol.md`),
   nhật ký `D-xx` (`docs/nhanh/decisions.md`, đến `D-68`), mẫu thiết kế 16 mục, 55 tài liệu feature.
3. `eCommerce` **còn thiếu** nửa đầu: **không có vai**, mọi phiên là một agent làm từ thiết kế đến PR ⇒ người viết
   code tự review code của mình, không có lớp **kiểm tài liệu** độc lập, không có **negative control** bắt buộc.
4. Kế hoạch: **thêm lớp vai** vào quy trình hiện có, **không** thay quy trình phiên/handover/cổng đang chạy tốt.
5. Khác biệt lớn nhất so với DeepSeek: Claude Code có **subagent thật** (`.claude/agents/*.md`), **skill thật**
   (`.claude/skills/`), **hook** để **chặn** ghi ngoài phạm vi (thay vì kiểm sau), **chọn model theo vai**, và
   **worktree** cách ly. Playbook Markdown không còn là đường duy nhất.
6. Phiên chính (main session) là **Orchestrator** — vì subagent của Claude Code **không gọi được subagent khác**.
7. 9 vai giữ nguyên tên/ranh giới của bản gốc; nội dung luật được **viết lại cho ABP + Next.js + PostgreSQL**,
   danh sách đỏ được thay bằng rủi ro của bán hàng (tiền, hoá đơn điện tử, token sàn, PII khách).
8. Ràng buộc ghi theo vai được **thi hành bằng hook `PreToolUse`**, có lưới thứ hai là `git status --porcelain`.
9. Trạng thái dây chuyền (board + registry hạng mục) **gắn vào** `07-ke-hoach-trien-khai.md` và handover, không tạo
   hệ thứ hai cạnh tranh.
10. Lộ trình 6 bước, **pilot 1 feature ở mức L1** trước khi nhân rộng — đúng bài học pilot `LRN-002` của bản gốc.
11. Ước lượng dựng: **3–4 phiên** (khung + script + hook + pilot) trước khi chạy feature thứ hai.
12. Cần chốt **9 quyết định** ở §11 trước khi dựng — **Q9 đã chốt** (`D-69`: lộ trình chính là `05` + `07`), còn Q1–Q8.
13. Tài liệu cũ của `eCommerce` **không di chuyển, không xoá**: xếp loại + gắn nhãn "lịch sử", và **14 luật cũ tốt hơn
    bản gốc** được đưa vào vai/cổng — xem **§14**.

---

## 1. Review dự án nguồn `SelfLearningAI`

### 1.1. Cái gì làm nên giá trị — phải giữ

| # | Cơ chế | Ở đâu trong nguồn | Vì sao giữ |
|---|---|---|---|
| K1 | **Chỉ exit code là bằng chứng** | `00-orchestrator/ROLE.md` §1, `de-xuat-giai-phap.md` §6.2 | Trùng khớp với luật "ghi số đo, không ghi đã ổn" của `eCommerce` |
| K2 | **Mỗi vai một vùng ghi**, ghi ngoài vùng là revert | mục "Bạn được ghi / KHÔNG được ghi" trong mọi `ROLE.md` | Ngăn REV "tiện tay sửa", ngăn QA sửa code để test xanh |
| K3 | **Ngữ cảnh sạch cho vai đánh giá** (QA, REV không dùng fork) | ORCH §1.4 | Reviewer không bị "neo" theo lời giải thích của người viết |
| K4 | **Bàn giao bằng file**, mỗi vai kết thúc bằng **một khối JSON** | §8 của mỗi `ROLE.md` | Orchestrator kiểm bằng máy, không đọc cảm tính |
| K5 | **DOCREV** — vai kiểm tài liệu D1–D6 trước cổng người | `65-doc-reviewer/ROLE.md` | 6/6 lỗi nghiêm trọng của pilot nằm ở tài liệu, không cổng nào của chính nó bắt được |
| K6 | **Negative control bắt buộc** (mutation thật rồi khôi phục) | `50-qa/ROLE.md` §6, `negative-control.md` | Chống test rỗng; Q7 cấm stub "allow all" |
| K7 | **Trần làm lại 2 vòng**, quá trần ⇒ `blocked` + inbox | ORCH §4 | Chi phí có trần |
| K8 | **Thẩm quyền 3 tầng A/B/C** theo **tính đảo ngược**, tầng B phải ghi `decide.ps1` với `-Revert` + `-ReviewBefore` | ORCH §7, `decisions.md` | Giảm số lần chặn người dùng mà không trao quyền việc không lùi được |
| K9 | **Registry hạng mục + claim/heartbeat/worktree** cho nhiều phiên song song | `scripts/wp.ps1`, `session-new/merge.ps1` | `eCommerce` hiện chỉ có luật chữ (`09-parallel-sessions.md`), chưa có khoá máy |
| K10 | **"Bài học bắt buộc"** ghi ngay trong playbook của vai đã mắc lỗi | cuối `20-architect`, `30-backend`, `50-qa`, `60-reviewer` | Bài học đi theo vai, không nằm trong báo cáo không ai đọc lại |

### 1.2. Cái gì là **mô phỏng** vì giới hạn của DeepSeek Harness — thay bằng cơ chế gốc

| Giới hạn của DeepSeek Harness (đo trong `de-xuat-giai-phap.md` §1) | Cách nguồn xoay xở | Claude Code có gì |
|---|---|---|
| Chỉ **một model** (`deepseek-flash`) cho mọi vai | Tạo khác biệt bằng playbook + ngữ cảnh hẹp | Frontmatter `model:` cho từng subagent (opus / sonnet / haiku / inherit) |
| **Không có API skill** | Thư mục `playbooks/` + câu mở "đọc ROLE.md trước" | `.claude/skills/<tên>/SKILL.md` thật, nạp theo mô tả; subagent nạp sẵn qua frontmatter `skills:` |
| Vùng ghi chỉ kiểm **sau** khi agent chạy | ORCH chạy `git status` rồi `git checkout --` | Hook `PreToolUse` **chặn trước** lệnh `Edit`/`Write`; vẫn giữ `git status` làm lưới thứ hai (vì `Bash` ghi được file) |
| `subagent` vs `subagent_fork` | Luật ORCH §1.4 | Mặc định subagent **luôn** ngữ cảnh sạch; fork phải gọi tường minh `subagent_type: "fork"` ⇒ luật này **tự đúng** |
| Tool lọc theo vai: không có | Tin vào luật chữ | Frontmatter `tools:` — REV/DOCREV **không có** `Edit`/`Write` ngoài 1 file kết quả (xem §4.3) |
| Workflow script | `workflow({script…})` phụ lục B | Tool `Workflow` (script JS, `agent()`/`pipeline()`/`phase()`, có `schema`) — **chỉ dùng khi người dùng chủ động bật** |
| Không có worktree tích hợp | `session-new.ps1` tự `git worktree add` | `isolation: "worktree"` khi gọi Agent; `EnterWorktree` cho phiên |

### 1.3. Điểm yếu của nguồn — **không** chép sang

| # | Điểm yếu | Bằng chứng trong nguồn | Cách tránh ở `eCommerce` |
|---|---|---|---|
| Y1 | Luật mâu thuẫn trong chính `ROLE.md`: BA "được ghi `docs/04-features/<ID>.md`" nhưng "KHÔNG được ghi `docs/**`" | `10-business-analyst/ROLE.md` §3–§4 (lỗi thay thế chuỗi khi gom tài liệu vào `docs/`) | Vùng ghi khai báo **một nơi** (file JSON máy đọc), hook và `ROLE` đều sinh từ đó |
| Y2 | Đánh số mục lặp (`## 8` hai lần trong ARCH, QA) | `20-architect`, `50-qa` | Mục "Bài học" là mục cố định cuối, có số riêng |
| Y3 | Hai mẫu SRS (12 mục ở luật BA, mẫu v2) + bộ `docs/02-design` lớn ⇒ tài liệu phình | `ke-hoach-ap-dung-mau.md` | `eCommerce` **giữ mẫu 16 mục hiện có**, không thêm mẫu thứ hai |
| Y4 | Kiểm encoding PowerShell 5.1 tốn công (BOM, mã hoá hai lần) | ORCH §3b | `eCommerce` chạy `pwsh` 7 + script ASCII sẵn rồi; chỉ giữ luật "đọc/ghi UTF-8 tường minh" |
| Y5 | `board.json` và `work-packages.json` là **hai** nguồn trạng thái | `agents/state/` | Gộp một registry (xem §6) |

---

## 2. Hiện trạng `eCommerce` (đo 14/11/2026)

| Mặt | Hiện trạng | Hệ quả cho kế hoạch |
|---|---|---|
| Stack | .NET 10 + ABP 10.7 modular monolith · PostgreSQL 17 · Redis 7 · Next.js `apps/admin-web` · pnpm | BE/FE/OPS viết luật theo ABP + Next.js, không theo bản gốc |
| Module | 15 thư mục trong `modules/` + 9 project `src/` + `shared/eCommerce.Kernel` | Vùng ghi BE theo **module chủ** của feature |
| Tài liệu feature | **55** tệp `docs/features/<module>/<mã>-<slug>.md`, mẫu **16 mục** (§0–§15) — `docs/templates/feature-design-template.md` | BA viết vào đúng chỗ này; **không** đổi tên 55 tệp |
| Duyệt tài liệu | **27/55** Approved (theo `docs/features/README.md` §2) | DOCREV có việc ngay: 28 tệp Draft |
| Quy trình phiên | `N1…N21` xong, handover 7 mục, biến thể A/B (`docs/nhanh/08-session-protocol.md`) | **Giữ nguyên**; vai chạy **bên trong** một phiên |
| Quyết định | `D-01…D-68` trong `docs/nhanh/decisions.md` | Tầng B ghi tiếp `D-xx` ở đây, không mở nhật ký mới |
| Cổng máy | `tools/kiem-tat-ca.ps1` (nhẹ 9/9 · `-DayDu` 19/19), `kiem-db.ps1`, `kiem-link-tai-lieu.ps1`, `test-cong-kiem.ps1`, mermaid | Đây chính là G3/G6 — **không viết lại** |
| CI | `ci` (`cong-15-buoc`, `openapi-drift`, `e2e-playwright`) + `docker-build`; 2 required check trên `main` | G6 = PR xanh |
| Kế hoạch | `07-ke-hoach-trien-khai.md` **chưa có §WP-14** ⇒ phiên `N22` không có WP để mở | Bước đầu tiên sau khi dựng khung là **lập WP-14+** (việc của người + BA) |
| Claude Code | **Chưa có** `CLAUDE.md`, **chưa có** `.claude/` | Toàn bộ §4–§5 là việc mới |
| Git | `git` báo *dubious ownership* (thư mục `.git` do `BUILTIN/Administrators` sở hữu) | Phải sửa trước khi agent chạy `git status` (§10 bước 0) |
| Gốc repo | ~50 tệp `*.log` / `_probe-*` / `.tmp-*` (đã gitignore) | Không chặn, nhưng làm nhiễu `ls` của agent ⇒ dọn ở bước 0 |

### Điểm `eCommerce` **đã hơn** nguồn — mang ngược lại nếu muốn

- Cổng tự test chính nó (`test-cong-kiem.ps1`, 38 ca) — nguồn không có.
- Required check trên GitHub — nguồn còn nợ `C-07` (CI chưa chạy remote).
- Câu mở phiên kế chuẩn hoá 2 biến thể A/B — nguồn chỉ có prompt trong bảng.

---

## 3. Kiến trúc đích

```text
Lớp 4  Orchestrator        ← phiên chính Claude Code (CLAUDE.md + skill /feature)
Lớp 3  Workflow / cổng     ← skill `feature-pipeline` (thủ công qua Agent) | Workflow script (tuỳ chọn, L2)
Lớp 2  9 subagent          ← .claude/agents/<vai>.md  (model, tools, skills, vùng ghi)
Lớp 1  Hợp đồng bàn giao   ← file theo mẫu + khối JSON cuối mỗi lần chạy
Lớp 0  Nguồn sự thật       ← docs/features · docs/nhanh · code · cổng tools/*.ps1
       Hàng rào            ← hook PreToolUse (vùng ghi) · SubagentStop (kiểm artifact) · git status
```

### 3.1. Cây thư mục mới (đề xuất)

```text
eCommerce/
├── CLAUDE.md                          ← ngắn (≤ 120 dòng): điểm vào, luật bất biến, lệnh cổng, trỏ tới docs
├── .claude/
│   ├── settings.json                  ← hooks + permissions dùng chung (commit)
│   ├── agents/                        ← 9 subagent (mục 4)
│   │   ├── ba.md · arch.md · be.md · fe.md · qa.md
│   │   └── rev.md · docrev.md · ops.md        (orchestrator = phiên chính, không có file agent)
│   ├── skills/                        ← tri thức dùng lại, nạp vào agent qua `skills:`
│   │   ├── feature-pipeline/          ← quy trình F1→F6 cho orchestrator (/feature <MÃ>)
│   │   ├── abp-module-rules/          ← ranh giới module ABP, DbContext, permission, ETO/outbox
│   │   ├── ef-migration-postgres/     ← expand→migrate→contract, ModelSnapshot, chạy trên DB rỗng
│   │   ├── nextjs-admin-rules/        ← BFF, `pnpm gen:api`, 3 trạng thái, token UI Nhanh
│   │   ├── negative-control/          ← cách mutate–chạy–khôi phục–ghi bằng chứng
│   │   ├── doc-review-d1-d6/          ← 6 nhóm kiểm tài liệu
│   │   └── session-handover/          ← chuẩn hoá §5/§5.1 của 08-session-protocol
│   ├── hooks/
│   │   ├── guard-write-scope.ps1      ← PreToolUse: chặn Edit/Write ngoài vùng của vai
│   │   └── check-artifact.ps1         ← SubagentStop: kiểm artifact + khối JSON
│   └── workflows/ (tuỳ chọn)          ← feature-pipeline.js cho chế độ L2
├── agents/
│   ├── scopes.json                    ← NGUỒN DUY NHẤT của vùng ghi theo vai (hook + ROLE đọc chung)
│   └── state/registry.json            ← trạng thái feature/WP (gộp board + work-packages) — xem §6
├── docs/
│   ├── features/<module>/<mã>-<slug>.md       ← SRS (BA) — GIỮ NGUYÊN chỗ cũ
│   ├── features/<module>/<mã>-arch.md         ← ARCH (chỉ khi cần, xem §4.2)
│   ├── tests/<MÃ>-testplan.md                 ← QA (mới)
│   ├── reviews/<MÃ>-review.md · <MÃ>-docreview.md ← REV · DOCREV (mới)
│   ├── ops/<MÃ>-deploy.md                     ← OPS (mới)
│   ├── inbox/<MÃ>-blocked.md · <MÃ>-change-requests.md ← vai ghi khi bị chặn / muốn đổi tài liệu gốc
│   └── agents/                                ← tài liệu của chính hệ agent (file này, retro pilot)
└── tools/agents/                      ← script mới: wp.ps1, check-feature-doc.ps1, decide.ps1, negative-control.ps1
```

> ⚠️ **Cổng link sẽ quét cả `.claude/**/*.md`** (`kiem-link-tai-lieu.ps1` quét mọi `*.md` trừ `node_modules|bin|obj|.git|artifacts`).
> Mọi đường dẫn `docs/…md` viết trong backtick ở file agent/skill **phải có thật**, hoặc dùng `<MÃ>` để cổng bỏ qua.

---

## 4. Chín vai — chuyển đổi cụ thể

### 4.1. Bảng tổng

| Vai | File agent | Model đề xuất | Tools | Được ghi | Kết thúc bằng |
|---|---|---|---|---|---|
| **ORCH** | *(phiên chính)* + `CLAUDE.md` + skill `feature-pipeline` | phiên hiện tại | tất cả | `agents/state/**`, `docs/nhanh/decisions.md`, `docs/nhanh/handover-<Nx>`, commit/PR | báo cáo cuối phiên + khối A/B |
| **BA** | `ba.md` | sonnet | Read, Grep, Glob, Edit, Write | `docs/features/<module>/<mã>-<slug>.md`, `docs/inbox/**` | JSON `{sectionsPresent:16, brCount, brWithoutTest, openQuestions}` |
| **ARCH** | `arch.md` | **opus** | Read, Grep, Glob, Edit, Write, Bash (chỉ đọc + `psql` trong `BEGIN…ROLLBACK`) | `docs/features/<module>/<mã>-arch.md`, `docs/inbox/**` | JSON `{tablesWritten, contracts, schemaChanges, boundaryViolations}` |
| **BE** | `be.md` | sonnet (opus cho feature tiền/tồn kho) | tất cả trừ Web | `modules/<Module chủ>/**`, `src/eCommerce.*` (composition root, migration), `shared/**` chỉ khi ARCH cho phép, **mục "trạng thái triển khai" ở cuối tài liệu feature** (§14.3 M1) | JSON `{buildExit, testExit, testsAdded, migrations, docDeviation}` |
| **FE** | `fe.md` | sonnet | tất cả trừ Web | `apps/admin-web/**` (trừ `e2e/**`), `packages/api-client/**`, mục "trạng thái triển khai" của tài liệu `web-*` | JSON `{typecheckExit, buildExit, vitestExit, lintExit, screens, statesCovered, docDeviation}` |
| **QA** | `qa.md` | sonnet | tất cả trừ Web | `test/**`, `apps/admin-web/e2e/**`, `apps/admin-web/test/**`, `tools/probe-*.ps1`, `docs/tests/**` | JSON `{brCovered, brTotal, uncovered, negativeControl, testExit}` |
| **REV** | `rev.md` | **opus** | Read, Grep, Glob, Bash (git diff/log, dotnet test chỉ chạy), Write | **chỉ** `docs/reviews/<MÃ>-review.md` | JSON `{verdict, p1, p2, p3}` |
| **DOCREV** | `docrev.md` | **opus** | Read, Grep, Glob, Bash (`psql` chạy thử SQL trong tài liệu), Write | **chỉ** `docs/reviews/<MÃ>-docreview.md` | JSON `{verdict: DOC-OK/DOC-CHANGES/DOC-BLOCKED, p1, p2}` |
| **OPS** | `ops.md` | sonnet | tất cả trừ Web | `.github/**`, `docker-compose.yml`, `**/Dockerfile`, `tools/*.ps1` (trừ probe nghiệp vụ), `docs/ops/**` | JSON `{filesChanged, envVarsAdded, migrations, rollback, gateExit}` |
| **DEC** 🆕 | `decider.md` ✅ **đã tạo** | **opus** | Read, Grep, Glob, Bash (chỉ đọc), Edit, Write, WebSearch, WebFetch | **chỉ** thêm dòng vào `docs/nhanh/decisions.md` + `docs/agents/hang-doi-xem-lai.md` | JSON `{outcome: TIEN_LE/TAM_CHOT/CHUYEN_NGUOI, choice, sources, confidence, reverseCost, revert, reviewBefore}` — xem §13 |

> Model là **đề xuất ban đầu**; pilot phải đo (thời gian, số vòng làm lại, số P1 bị lọt) rồi chỉnh. Nguyên tắc:
> vai **phán xét** (ARCH, REV, DOCREV) dùng model mạnh nhất; vai **làm theo hợp đồng** dùng sonnet. Không dùng haiku
> cho vai có quyền phán xét.

### 4.2. Điều chỉnh nội dung so với nguồn

| Vai | Giữ từ nguồn | Đổi cho `eCommerce` |
|---|---|---|
| BA | B1–B10 (BR có `*Test:*`, NFR có ngưỡng số, ghi module chủ, không bịa yêu cầu → "Câu hỏi mở") | 12 mục ⇒ **16 mục §0–§15** của mẫu hiện có; nguồn yêu cầu là `docs/nhanh/` (khảo sát Nhanh) + `business-modules-proposal.md`, không phải PRD |
| ARCH | A1–A5 (không bảng 2 chủ, không FK/navigation xuyên module, không transaction xuyên module, lời gọi chéo qua contract, vi phạm ⇒ CR) + 6 bài học | Luật R1–R3 thay bằng ranh giới **ABP module** (`*.Application.Contracts`, ETO, `IDistributedEventBus` + outbox `XC-002`); **chỉ gọi khi** feature chạm ≥ 2 module / đổi schema / đổi contract — **mẫu 16 mục đã có §5–§8**, nên với feature 1 module ARCH **không** viết `-arch.md` riêng mà DOCREV kiểm §5–§8 |
| BE | B1–B10 + 5 bài học (không nới catch, timestamp từ DB, migration "thành công mà không làm gì") | Cấu trúc ABP (Domain / Application / EntityFrameworkCore / HttpApi), `decimal(18,4)` tiền, `decimal(18,3)` số lượng (`D-31`), migration theo `09-parallel-sessions.md` §6 (**không** 2 phiên cùng `migrations add`) |
| FE | F1–F8 (3 trạng thái, empty ≠ error, không bịa endpoint, chống bấm lặp) | Chỉ gọi endpoint có trong `schema.d.ts` sinh từ OpenAPI (`pnpm gen:api` không đổi ⇒ cổng `openapi-drift`); UI theo token Nhanh (`docs/nhanh/04-web-admin-nextjs.md`) |
| QA | Q1–Q7 + negative control + 4 bài học | Thêm hai tầng nguồn chưa có: **probe HTTP thật** (`tools/probe-*.ps1`, mẫu `nen-tang.ps1`) và **Playwright**; Q7 ⇒ fixture ABP thật trên PostgreSQL (`eCommerce.IntegrationTests`), cấm `AddAlwaysAllowAuthorization` |
| REV | Checklist A–D, P1/P2/P3, không sửa code, 4 bài học | Thêm: permission ABP (`[Authorize]` cấp class — lỗi thật ở Phase 0), multi-tenant filter, idempotency webhook sàn/IPN, token sàn không lộ qua DTO/log, tiền không `double` |
| DOCREV | D1–D6, DOC-OK/CHANGES/BLOCKED | D4 chạy SQL trên PostgreSQL 17 của `docker compose` trong `BEGIN … ROLLBACK`; D5 thêm "tên probe/ca được nhắc có thật"; dùng luôn `kiem-link-tai-lieu.ps1` + `check:mermaid` làm phần máy |
| OPS | O1–O6 | G6 = `kiem-tat-ca.ps1 -DayDu` + PR xanh 2 required check; không động vào branch protection (tầng C) |
| ORCH | §1–§7 (exit code, không tự sửa code, 1 bước 1 vai, trần 2 vòng, log, thẩm quyền A/B/C) | Thêm luật của `eCommerce`: mở phiên bằng baseline 6 lệnh, đóng bằng handover 7 mục + khối A/B; mỗi feature một nhánh + PR |

### 4.3. Danh sách đỏ (tầng C) cho `eCommerce` — thay danh sách "dữ liệu trẻ em" của nguồn

1. **Tiền**: luật tính tiền/làm tròn (`XC-001`), xác nhận thanh toán (IPN/webhook), hoàn tiền, đối soát, COD.
2. **Thuế & hoá đơn điện tử**: phân loại thuế GTGT, phát hành/điều chỉnh/huỷ hoá đơn, ký số (`EINV-*`, `SIGN-*`).
3. **Bí mật & tích hợp ngoài**: token Shopee/Lazada/TikTok, khoá VNPay/GHN/GHTK, `IStringEncryptionService`, secret CI.
4. **Không đảo ngược**: `DROP`/đổi kiểu cột có dữ liệu, backfill thật (không phải dry-run), xoá dữ liệu khách.
5. **PII khách hàng**: thời hạn lưu, xuất dữ liệu, chia sẻ cho bên thứ ba.
6. **Hạ tầng dùng chung**: branch protection, required check, merge vào `main`, phát hành.
7. **Phạm vi**: thêm/bỏ WP trong `07`, đổi thứ tự giai đoạn, cam kết thời hạn.
8. **G2 cho thay đổi phá huỷ** contract liên module hoặc schema.

Tầng B (tự quyết, bắt buộc ghi `D-xx` có *cách lùi* + *xem lại trước khi*): chi tiết thiết kế, giá trị mặc định,
migration **expand-only**, duyệt CR chỉnh tài liệu theo code đã chạy.

> 🆕 **Từ 14/11/2026, tầng B do agent `decider` quyết** (§13), và một phần tầng C được tách đôi theo nguyên tắc
> *"quyết cho phát triển ≠ quyết cho production"*: DEC được **chốt tạm thiết kế** cho mảng tiền/thuế/pháp lý để code
> chạy tiếp trên sandbox/giả lập, nhưng quyết định đó **chặn release** tới khi người duyệt. Vùng cấm thật sự (không
> đảo ngược được) nằm ở `.claude/skills/decision-policy/SKILL.md` §3.

---

## 5. Cơ chế Claude Code dùng cho từng nguyên tắc

| Nguyên tắc (nguồn) | Cơ chế Claude Code | Ghi chú cần đo ở pilot |
|---|---|---|
| Vùng ghi theo vai (K2) | `agents/scopes.json` + hook `PreToolUse` matcher `Edit\|Write\|MultiEdit\|NotebookEdit` ⇒ `guard-write-scope.ps1` đọc JSON từ stdin, lấy loại agent đang chạy + `file_path`, so với glob của vai, **exit 2** để chặn kèm lý do | 🔶 Phải **đo** hook nhận được định danh subagent trong input; nếu không ⇒ phương án dự phòng: biến môi trường/marker file do orchestrator đặt trước khi gọi, cộng lưới `git status --porcelain` sau mỗi lần gọi |
| Lưới thứ hai (Bash ghi được file) | Orchestrator chạy `git status --porcelain` sau mỗi subagent, so với `scopes.json`; lệch ⇒ `git restore` đúng file + gọi lại với nhắc phạm vi | Giữ y nguồn |
| Ngữ cảnh sạch (K3) | Gọi `Agent` với `subagent_type: "qa" / "rev" / "docrev"` — **không** dùng `fork` | Tự đúng theo mặc định |
| Tool theo vai | Frontmatter `tools:`; REV/DOCREV không có `Edit`, chỉ `Write` + hook cho phép đúng 1 đường dẫn | |
| Playbook/skill (K10) | Luật cốt lõi trong thân `.claude/agents/<vai>.md` (≤ 150 dòng); checklist dài trong skill nạp qua `skills:` | Giữ mỗi file agent ngắn ⇒ ít tốn ngữ cảnh mỗi lần gọi |
| Kiểm artifact sau mỗi vai | Hook `SubagentStop` ⇒ `check-artifact.ps1` (port từ `SelfLearningAI/scripts/check-artifact.ps1`): đủ mục, có khối JSON, `BR-` có `*Test:*`, `VERDICT:` hợp lệ | Hook **báo**, orchestrator **quyết** gọi lại hay dừng |
| Chỉ exit code (K1) | Orchestrator tự chạy `dotnet build`/`dotnet test`/`pnpm …`/`kiem-tat-ca.ps1`, **không** dùng số agent báo | |
| Song song an toàn (K9) | `tools/agents/wp.ps1` (port `wp.ps1`: `status/check/start/heartbeat/done/release/blocked/sync-doc`) + worktree; BE ∥ FE trong cùng feature dùng `isolation: "worktree"` khi chạm file rời nhau | Luật "không 2 phiên `migrations add`" thành `migrationsExclusive: true` trong registry |
| Thẩm quyền A/B/C (K8) | `tools/agents/decide.ps1` ghi `D-xx` vào `docs/nhanh/decisions.md`, từ chối nếu thiếu `-Revert`/`-ReviewBefore` hoặc `-Tier C` | Theo đúng định dạng `D-xx` đang có |
| Dây chuyền tự động (L2) | Skill `feature-pipeline` (mặc định, orchestrator gọi `Agent` tuần tự) — hoặc `Workflow` script khi người dùng **chủ động** yêu cầu | Workflow tốn nhiều token, không bật mặc định |
| Bộ nhớ qua phiên | Handover + decisions (đã có) — không thêm cơ chế mới | |

---

## 6. Workflow và cổng

### 6.1. Máy trạng thái của một feature (lồng trong một phiên `Nx`)

```text
READY ─► F1 BA ─► F1b DOCREV ─► G1 người duyệt SRS (ghi vào BIÊN BẢN DUYỆT, không sửa header — §14.3 M2)
      ─► F2 ARCH (nếu cần) ─► F2b DOCREV ─► G2 người duyệt (tầng B nếu expand-only)
      ─► F3 BE ∥ FE ─► G3 máy: build + test + `kiem-tat-ca.ps1` 9/9 + git status trong vùng
      ─► F4 QA ─► G4 máy: mọi BR có test + negative control có bằng chứng đỏ→xanh
      ─► F5 REV ─► G5 VERDICT PASS (CHANGES ⇒ quay F3, tối đa 2 vòng)
      ─► F6 OPS ─► G6 máy: `-DayDu` 19/19 + PR xanh 2 required check
      ─► người merge ─► DONE (registry + README features + handover)
```

### 6.2. Bảng cổng

| Cổng | Ai gác | Tiêu chí (máy kiểm được) |
|---|---|---|
| G1 | **Người** — người review **theo loại feature** (§14.2 K-08) | DOCREV `DOC-OK`; 16 mục; 0 BR thiếu `*Test:*`; không câu hỏi mở chặn; ghi vào biên bản duyệt |
| G2 | **Người** (tầng B nếu expand-only) | DOCREV `DOC-OK` cho `-arch`; không vi phạm ranh giới module; có diff schema |
| G3 | Máy | `dotnet build eCommerce.slnx --no-incremental` **0 error, 0 warning mới** · `dotnet test` **số ca ≥ baseline** (giảm phải có lý do + người duyệt) · `pnpm lint/typecheck/build/test` · `kiem-tat-ca.ps1` exit 0 · diff ≤ ~600 dòng trừ migration (§14.2) |
| G4 | Máy | `check-testplan`: mỗi `BR-` xuất hiện ≥ 1 lần; mục negative control có output đỏ + xanh |
| G5 | Agent REV | `VERDICT: PASS`, `P1 = 0` |
| G6 | Máy + GitHub | `kiem-tat-ca.ps1 -DayDu` exit 0; `cong-15-buoc` + `docker-build` xanh trên PR |
| Merge/Release | **Người** | — |

### 6.3. Registry trạng thái (gộp 2 file của nguồn)

`agents/state/registry.json` — một mục cho mỗi **WP** của `07` (WP-14 trở đi) và mỗi **feature** trong WP:
`id`, `wp`, `module`, `stage` (READY/F1…/DONE), `attempts` theo vai, `gates`, `claimedBy/At`, `heartbeatAt`,
`writeScope`, `addsMigration`, `blockedReason`, `evidence`. `wp.ps1 sync-doc` sinh bảng trạng thái vào
`docs/agents/` (có marker), `check` của cổng so bảng với registry — đúng cách nguồn đã sửa lỗi "bảng lệch".

### 6.4. Ghép với quy trình phiên hiện có

- **Mở phiên**: baseline 6 lệnh (§4 của `08-session-protocol.md`) **+** `wp.ps1 start <WP>` (claim + nhánh).
- **Trong phiên**: orchestrator chạy dây chuyền §6.1 cho feature của WP.
- **Đóng phiên**: handover 7 mục **+** mục "Quyết định tự làm (tầng B)" và "Chờ người (tầng C)" của nguồn;
  khối A/B **không đổi**.
- Mỗi lần gọi subagent ghi 1 dòng vào `artifacts/agents/<MÃ>.jsonl` (gitignore) — tóm tắt đưa vào handover.

---

## 7. Mức tự động hoá

| Mức | Dùng cho | Người gác |
|---|---|---|
| L1 | **Pilot** + mọi feature thuộc danh sách đỏ (PAY, EINV, SIGN, REC, SAL-002/004/006) | Mọi cổng |
| L2 | Mặc định sau pilot | G1, G2, merge |
| L3 | CRUD 1 module, đã có feature tương tự đi qua L2 sạch | Chỉ merge |

---

## 8. Rủi ro và cách chặn

| # | Rủi ro | Mức | Chặn bằng |
|---|---|---|---|
| R1 | Hook không nhận diện được subagent ⇒ không chặn được vùng ghi | Cao | Đo ở bước 3; dự phòng marker + `git status` |
| R2 | Agent ghi qua `Bash` (`Set-Content`, `>`), lách hook `Edit/Write` | Trung bình | Lưới `git status` sau mỗi vai; REV/DOCREV không có `Bash` ghi (permission `deny` cho lệnh ghi) |
| R3 | Chi phí: 9 vai × mỗi feature, opus cho 3 vai | Trung bình | Pilot đo token thật; ARCH bỏ qua khi 1 module; L3 cho CRUD |
| R4 | Hai hệ quy trình chồng nhau (phiên `Nx` + dây chuyền) | Trung bình | Dây chuyền **lồng trong** phiên; không có tài liệu quy trình thứ hai — chỉ bổ sung `08` |
| R5 | Cổng `-DayDu` dài (~460 s) chạy lại sau mỗi vòng REV | Thấp | G3 dùng bản nhẹ 9/9; `-DayDu` chỉ ở G6 |
| R6 | Flaky có sẵn (`D-67`, `C10`) làm đỏ G6 vô cớ | Trung bình | Đóng `D-67` **trước** pilot hoặc ghi rõ ngoại lệ tầng B |
| R7 | 28 tài liệu Draft ⇒ BA/ARCH dựa vào tài liệu chưa duyệt | Trung bình | Pilot chỉ chọn feature có tài liệu **Approved**; DOCREV chạy đợt duyệt riêng |
| R8 | Agent tự mở WP khi `07` chưa có `§WP-14` | Thấp | Luật ORCH: không có WP ⇒ dừng, hỏi người (tầng C mục 7) |

---

## 9. Đo thành công của pilot

| Chỉ số | Cách đo | Ngưỡng chấp nhận |
|---|---|---|
| Lỗi tài liệu bắt ở đúng tầng | đếm P1 của DOCREV vs P1 lộ ở BE/QA/REV/OPS | ≥ 70% bắt ở F1b/F2b |
| Ghi ngoài vùng | số lần hook chặn + số lần `git status` lệch | 0 lần lọt qua cả hai lưới |
| Test có thật | negative control: số mutation làm đỏ / số mutation | 100% với BR quan trọng nhất |
| P1 REV mà QA bỏ sót | đếm | ghi nhận (nguồn: 2 P1) — dùng để chỉnh playbook QA |
| Vòng làm lại | `attempts` trong registry | ≤ 2 mỗi vai |
| Token / thời gian | `/cost` hoặc usage cuối phiên | ghi lại để ước lượng các WP sau |

---

## 10. Lộ trình dựng (6 bước, mỗi bước kết thúc bằng điểm xanh)

| Bước | Việc | DoD (lệnh kiểm) | Ước lượng |
|---|---|---|---|
| **0** | Tiền đề: sửa *dubious ownership* (`git config --global --add safe.directory D:/Projects/Tools/eCommerce`, người dùng tự chạy) · dọn ~50 log ở gốc vào `artifacts/` · nhánh `chore/agents-scaffold` | `git status` chạy được; gốc repo không còn `*.log` | 0,2 phiên |
| **1** | `CLAUDE.md` + `agents/scopes.json` + 8 file `.claude/agents/*.md` (port luật §4) | `kiem-link-tai-lieu.ps1` PASS; `/agents` liệt kê đủ 8 | 1 phiên |
| **2** | 7 skill §3.1 + `tools/agents/` (`wp.ps1`, `decide.ps1`, `check-feature-doc.ps1`, `check-artifact.ps1`, `negative-control.ps1`) — port từ nguồn, viết lại cho mẫu 16 mục + `D-xx` | mỗi script có ca test trong `test-cong-kiem.ps1` (giữ truyền thống "cổng tự test") | 1 phiên |
| **3** | Hook `guard-write-scope` + `SubagentStop` trong `.claude/settings.json`; **đo R1** bằng ca thử: gọi REV, bảo nó sửa `modules/**` ⇒ phải bị chặn | ca thử chặn đúng; `git status` sạch sau ca | 0,5 phiên |
| **4** | **Pilot L1** trên 1 feature (chọn ở §11 Q3), chạy đủ F1→F6 | registry `DONE`; PR xanh; bảng §9 có số | 1–2 phiên |
| **1b** ✅ *14/11/2026 — xem §14.4* | **Chuyển đổi tài liệu cũ** theo §14.1: banner "LỊCH SỬ" cho nhóm L, mục "Không đọc làm điểm vào" trong `CLAUDE.md`, ánh xạ việc còn lại của `implementation-plan.md` vào `07` (theo `D-69`), đưa 14 luật §14.2 vào agent/skill | `kiem-link-tai-lieu.ps1` PASS; `grep` thấy banner ở đủ tệp nhóm L; mỗi luật K-xx có nơi nhận | 0,5 phiên |
| **5** | Retro: sửa agent/skill theo số đo, thêm mục "Bài học bắt buộc" vào vai đã mắc lỗi; cập nhật `08-session-protocol.md` (§ vai trong phiên) | `docs/agents/` có báo cáo pilot; quyết định chuyển L2 ghi `D-xx` | 0,5 phiên |

**Đường găng:** bước 0 → 1 → 3 (hook phải đo được trước pilot) → 4. Bước 2 song song với 3 được nếu tách file.

---

## 11. Quyết định cần chốt trước khi dựng

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q1 | Giữ SRS tại `docs/features/<module>/` (không đổi tên 55 tệp) và thêm `docs/tests`, `docs/reviews`, `docs/ops`, `docs/inbox`? | **Có** — tránh đổi 55 tệp + hàng trăm tham chiếu |
| Q2 | ARCH viết `-arch.md` riêng hay điền §5–§8 của SRS? | `-arch.md` **chỉ** khi ≥ 2 module / đổi schema / đổi contract; còn lại DOCREV kiểm §5–§8 của SRS |
| Q3 | Feature pilot | Một feature **đã Approved**, chạm **1–2 module**, **không** thuộc danh sách đỏ, có FE (để vai FE chạy thật) — ví dụ một màn P1 trong `docs/nhanh/04-web-admin-nextjs.md`. Người chọn và đưa vào `07` thành `§WP-14` |
| Q4 | Model theo vai như §4.1? | Có, đo lại sau pilot |
| Q5 | Đóng `D-67` (flaky `C10`) trước pilot? | **Có** — nếu không, G6 đỏ vô cớ sẽ làm sai số đo pilot |
| Q6 | Dùng tool `Workflow` cho L2 hay chỉ skill `feature-pipeline`? | Skill trước; Workflow chỉ khi chạy theo lô ở L3 |
| Q7 | Danh sách đỏ §4.3 đủ chưa? | Người bổ sung |
| Q8 | `CLAUDE.md` commit chung hay để `CLAUDE.local.md`? | Commit chung — là luật của repo |
| Q9 | **Lộ trình nào là chính?** Hai bản đang song song: `docs/implementation-plan.md` (GĐ0–GĐ4, website trước, pháp lý P0 — chạy phiên `S0…S19`) và `docs/nhanh/05-lo-trinh-rui-ro.md` + `docs/nhanh/07-ke-hoach-trien-khai.md` (Nhanh-hoá — chạy phiên `N1…N21`) | **`05` + `07` là chính** (mới hơn, D-01…D-21 đã chốt theo nó); việc **chỉ** có trong `implementation-plan.md` (GĐ2 cũ: `PRO-*`, `SHP-*`, `REC-002/003`) được **ánh xạ** vào giai đoạn tương ứng của `05` ở bước 1b, việc không ánh xạ được ⇒ hỏi người. ✅ **ĐÃ CHỐT 14/11/2026 — `D-69`** (chủ dự án đồng ý đúng đề xuất) |

---

## 12. (dành cho báo cáo pilot)

## 13. Agent quyết định thay chủ dự án (`DEC`) — đã dựng 14/11/2026

### 13.1. Vấn đề

Dây chuyền dừng nhiều nhất ở **câu hỏi mở** (BA ghi "Câu hỏi mở", ARCH gặp hai phương án, BE/QA gặp chỗ SRS mơ hồ,
handover không có WP kế tiếp). Đọc `D-22…D-68` thấy chủ dự án gần như luôn chọn theo **cùng một bộ nguyên tắc**:
*số đo thắng kế hoạch* (D-20, D-38) · *chỉ append, không đổi số* (D-17) · *một cửa ghi* (D-43, D-45) · *hàm thuần tính
tiền* (D-42) · *không nới bất biến để xanh* (D-66, D-68) · *hoãn có lý do là phương án hợp lệ* (D-34). Viết bộ nguyên
tắc đó ra thành chính sách thì agent áp được thay người.

### 13.2. Đã tạo

| Tệp | Vai trò |
|---|---|
| `.claude/agents/decider.md` | Subagent `decider` (opus): vùng ghi, vùng cấm, định dạng JSON trả về |
| `.claude/skills/decision-policy/SKILL.md` | Ma trận quyền (tin cậy × chi phí lùi), **vùng cấm 7 mục**, quy trình 7 bước, thứ tự tiêu chí T1–T8, cách ghi D-xx |
| `.claude/skills/decision-policy/references/tech-defaults.md` | ~40 mặc định kỹ thuật **có tiền lệ** (ABP, EF/PostgreSQL, sự kiện, quyền, API v1, FE, kiểm chứng) — trả `TIEN_LE` không tạo D-xx mới |
| `.claude/skills/decision-policy/references/vn-commerce-domain.md` | Pháp lý đã tra trong repo + 11 mảng nghiệp vụ (hàng hoá, kho, đơn, POS, khách, KM, thanh toán, vận chuyển, sàn, kế toán, UI) theo Nhanh.vn → KiotViet/Sapo/Haravan/MISA → sàn/hãng; mỗi dòng có nhãn ✅/🔶/⬜ |
| `docs/agents/hang-doi-xem-lai.md` | Hàng đợi để chủ dự án duyệt **theo lô** các quyết định tạm; cổng release chặn khi còn dòng `release` |

### 13.3. Cách dây chuyền dùng DEC (thay cho "dừng hỏi người")

```text
Agent X trả openQuestions / blocked     ┐
Orchestrator định hỏi người dùng        ├─► gọi `decider` (ngữ cảnh sạch) với: câu hỏi + MÃ feature + tệp liên quan
Handover không có WP kế tiếp            ┘
      │
      ├─ TIEN_LE      ⇒ trả D-xx cho agent X, chạy tiếp
      ├─ TAM_CHOT     ⇒ D-xx mới (TẠM CHỐT (DEC)) + hàng đợi; agent X chạy tiếp theo `choice`
      └─ CHUYEN_NGUOI ⇒ D-xx (ĐỀ XUẤT (DEC)); dây chuyền làm `continueWhileWaiting`;
                        orchestrator GOM mọi câu chuyển người, hỏi MỘT lần ở cuối phiên
```

Luật cho orchestrator (sẽ đưa vào skill `feature-pipeline` + `CLAUDE.md` ở bước 1–2):
1. **Không hỏi người dùng giữa phiên** cho câu hỏi chưa qua DEC.
2. Mỗi câu hỏi một lần gọi DEC; nhiều câu độc lập ⇒ gọi song song.
3. DEC không sửa gì ngoài 2 tệp nhật ký ⇒ chạy song song an toàn với BE/FE.
4. Báo cáo cuối phiên + handover §4 liệt kê các `D-xx` DEC đã ghi trong phiên.

### 13.4. Giới hạn — nói thẳng

- DEC **không thay** người ở: merge, dữ liệu thật, tiền thật, secret, hợp đồng/NCC, phạm vi ngoài lộ trình đã duyệt,
  kết luận tuân thủ pháp lý (SKILL §3). Với những câu này nó chỉ **giảm thời gian chờ**: đưa khuyến nghị và việc làm tiếp được.
- Quyết định tạm sai **vẫn tốn công làm lại** — trần chi phí là `reverseCost`; ma trận §2 của SKILL chặn trường hợp
  "tin cậy thấp × lùi đắt".
- Các dòng 🔶 trong tham chiếu domain là thông lệ **chưa xác minh trong repo**; DEC phải tra lại (URL + ngày) nếu muốn ghi `confidence = CAO`.
- Phải **đo** ở pilot: tỉ lệ quyết định tạm bị người đảo (mục tiêu < 20%), số lần dây chuyền còn dừng chờ người.
- Frontmatter `skills:` cần xác nhận được Claude Code nạp; thân agent đã bắt đọc `SKILL.md` tường minh để không phụ thuộc.

---

## 14. Chuyển đổi tài liệu cũ & kế thừa điểm tốt của quy trình cũ (bổ sung 14/11/2026)

> Bản đầu của kế hoạch chỉ đối chiếu **SelfLearningAI → eCommerce** (§15). Mục này đối chiếu chiều còn lại:
> **tài liệu và quy trình CŨ của chính eCommerce → hệ agent mới**, vì nhiều luật cũ **đã trả giá mới có** và tốt hơn
> bản gốc.

### 14.1. Xếp loại tài liệu cũ

**Nguyên tắc chuyển đổi:** **không di chuyển, không xoá, không đổi tên** — các tệp này có hàng trăm tham chiếu, và
cổng `kiem-link-tai-lieu.ps1` sẽ đỏ; lịch sử còn giá trị truy vết. Thay vào đó mỗi tệp được gắn **một trong 4 nhãn**:

| Nhãn | Nghĩa với agent |
|---|---|
| **N** — Nguồn sự thật | Agent **phải đọc** khi làm việc thuộc phạm vi đó; mâu thuẫn với tệp khác ⇒ tệp N thắng |
| **Q** — Quy trình đang hiệu lực | Luật làm việc; được **dẫn vào** agent/skill, không chép lại |
| **B** — Bài học | Không đọc khi làm việc; đã/ sẽ được **rút** thành mục "Bài học bắt buộc" của vai liên quan |
| **L** — Lịch sử | **Không dùng làm điểm vào.** Gắn banner đầu tệp *"LỊCH SỬ — điểm vào hiện tại là …"* ở bước 1b; `CLAUDE.md` liệt kê để agent không đọc nhầm |

| Tệp | Nhãn | Ghi chú chuyển đổi |
|---|---|---|
| `docs/nhanh/05-lo-trinh-rui-ro.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` | **N** | Lộ trình chính (`D-69`); registry WP sinh từ `07` |
| `docs/nhanh/decisions.md` | **N** | Nhật ký duy nhất; DEC ghi tiếp ở đây |
| `docs/nhanh/README.md`, `docs/nhanh/01…04`, `nhanh-pos-*.md`, `docs/nhanh/evidence/` | **N** | Domain Nhanh (đã dẫn vào `vn-commerce-domain.md`) |
| `docs/nhanh/handover-n*.md` (bản mới nhất) | **N** | Điểm vào của phiên; các bản cũ hơn = **L** |
| `docs/nhanh/08-session-protocol.md`, `docs/nhanh/09-parallel-sessions.md` | **Q** | Dẫn vào ORCH (`CLAUDE.md` + skill `feature-pipeline`) |
| `docs/coding-conventions.md` | **Q** + **N** | Dẫn vào BE/FE/REV + `tech-defaults.md` |
| `docs/design-doc-guidelines.md`, `docs/templates/feature-design-template.md` | **Q** | Dẫn vào BA/DOCREV (K-07, K-08) |
| `docs/features/README.md`, `docs/features/**` | **N** | SRS; trạng thái duyệt tra theo §14.3 M2 |
| `docs/bien-ban-duyet-gd1-2026-10-29.md` | **N** | **Nơi ghi trạng thái duyệt** của 26 tài liệu (`Q5`) — header vẫn `Draft` là **có chủ ý** |
| `docs/business-modules-proposal.md` | **N** (§6.7 pháp lý, §7.3 danh sách port) · phần còn lại **L** | |
| `docs/nhanh/03-kien-truc-dich.md` | **N** | Kiến trúc đích |
| `docs/architecture.md`, `docs/structure-and-techstack.md` | **N** (luật ranh giới module, khuôn module) · lộ trình "website trước" trong đó = **L** | Bước 1b: ghi chú đầu tệp chỉ phần nào còn hiệu lực |
| `docs/implementation-plan.md` | **L** (lộ trình) · **Q** (§3 *15 nguyên tắc*, §A.5 *điều kiện bắt đầu giai đoạn*) | Việc dang dở GĐ2 cũ ánh xạ vào `07` theo Q9 |
| `docs/SESSION-KICKOFF.md`, `docs/tiep-tuc-phien-moi.md`, `docs/handover-gd1.md`, `docs/ban-giao-gd1-con-lai.md`, `docs/ban-giao-s19-va-con-lai-2026-10-29.md`, `docs/handover/BAN-GIAO-GD1-2026-10-17.md`, `docs/handover/BAN-GIAO-GD1-GD2-2026-10-21.md`, `docs/gd1-coding-sessions.md` | **L** | Cửa vào của thời `S0…S19`; **nguy cơ cao nhất**: agent đọc `SESSION-KICKOFF.md` sẽ tin "phiên tiếp theo là GĐ2" và số đo cũ |
| `docs/audit-gd1.md`, `docs/phase-0-execution-report.md`, `docs/runtime-verification-report.md`, `docs/gd0-closure-report.md` | **B** | Rút lỗi đã gặp vào "Bài học bắt buộc" của BE/QA/OPS (ví dụ: 6 lỗi chỉ lộ khi chạy thật, lỗi #22 fail-closed, `F-403`) |
| `docs/abp-modules-plan.md`, `docs/ke-hoach-tach-module.md`, `docs/nhanh/dev-log-wp01-02.md`, `docs/nhanh/baseline-2026-11-02.md` | **L** | Việc đã xong |

### 14.2. Mười bốn luật cũ tốt hơn kế hoạch mới — đưa vào đâu

| # | Luật cũ | Nguồn | Kế hoạch mới trước đó | Đưa vào |
|---|---|---|---|---|
| K-01 | **5 dấu hiệu phải dừng phiên**: 3 vòng liên tiếp cùng lỗi · context > ~70% · sửa > 5 tệp ngoài phạm vi · test đỏ ngoài dự kiến (không sửa test cho xanh) · migration chạm DB có dữ liệu chưa `pg_dump` | `08` §6 | Chỉ "trần 2 vòng làm lại" | ORCH (`CLAUDE.md`, skill `feature-pipeline`) — dùng **cùng lúc** với trần 2 vòng |
| K-02 | **Chỉ cắt phiên ở điểm xanh**; không cắt giữa migration/backfill | `08` §2 | Không có | ORCH |
| K-03 | **Số ca test chỉ tăng**; giảm phải có lý do + người duyệt | `07` §1 #3, §10 #7 | "≥ baseline" | G3 |
| K-04 | **PR nhỏ** ≤ ~600 dòng diff trừ migration; một WP một nhánh | `07` §1 #5 | Không có | G3 + OPS |
| K-05 | **Mỗi WP có đường lùi** (cờ tính năng / tắt đọc bảng mới / revert migration) ghi trong DoD | `07` §1 #8 | Chỉ OPS ghi rollback | DoD của registry WP |
| K-06 | **Số ca tối thiểu theo tầng**: domain invariant cho mọi luật tồn/tiền/trạng thái · app service ≥ 3 ca (đúng, sai dữ liệu, sai quyền) · endpoint `/api/v1` ≥ 1 ca 200 + 1 ca 401/403 · UI component lõi có Vitest · OpenAPI drift mỗi PR sửa DTO | `07` §9 | QA chỉ "mỗi BR ≥ 1 test" | QA (bổ sung Q1) + G4 |
| K-07 | **14 quy tắc chất lượng tài liệu** (BR kiểm chứng được, NFR có ngưỡng số, guard sai thì làm gì, chống trùng ở mọi cửa vào, ghi tên khoá phân tán, không hard-code endpoint NCC, mục không áp dụng ghi lý do, Mermaid cấm `;` và placeholder dạng thẻ…) | `design-doc-guidelines.md` §5 | DOCREV chỉ có D1–D6 | DOCREV: thêm nhóm **D7** = 14 quy tắc này |
| K-08 | **Người review theo loại feature**: state machine ⇒ + kiến trúc; tích hợp NCC ⇒ + người đã liên hệ NCC; pháp lý ⇒ + **kế toán** (+ luật sư nếu cần); `XC-*` ⇒ cả nhóm | `design-doc-guidelines.md` §6 | G1 là "người" chung chung | G1: orchestrator in **danh sách người cần duyệt** theo loại; ARCH bắt buộc với feature có state machine |
| K-09 | **Quy tắc vàng: code buộc lệch tài liệu ⇒ sửa tài liệu trước, trong cùng PR** | `design-doc-guidelines.md` §4, `coding-conventions.md` §12 | Mâu thuẫn — xem M1 | §14.3 M1 |
| K-10 | **Song song cần DB riêng + cổng riêng + chủ tệp**; merge BE trước FE; **một chủ migration**; không sửa `eCommerce.slnx` / `Directory.Packages.props` / `pnpm-lock.yaml` ở 2 luồng | `09` §3–§6 | Chỉ "BE ∥ FE dùng worktree" ⇒ **sẽ đụng DB và cổng 54275/3000** | `registry.json` `sharedPaths` + skill `feature-pipeline`: BE ∥ FE chỉ khi có bảng cấp phát DB/cổng; FE chạy trên **API đã merge** (contract-first `09` §7) |
| K-11 | **Build `--no-incremental`** (build tăng dần ẩn warning) và kiểm **mốc nhị phân** (binary cũ) | `coding-conventions.md` §1, `kiem-tat-ca.ps1` bước 1 | Thiếu cờ | G3 — xem M4 |
| K-12 | **Go/no-go theo mốc + luật cắt phạm vi** khi trượt > 1 tuần; "scope trôi ⇒ không nhận, ghi sang giai đoạn sau" | `07` §10–§11 | Không có | ORCH + DEC (vùng cấm #5 đã chặn thêm phạm vi) |
| K-13 | **Điều kiện bắt đầu một giai đoạn**: toàn bộ tài liệu của giai đoạn đã duyệt (hoặc thoả thuận song song bằng văn bản) | `implementation-plan.md` §A.5 | Chỉ "pilot chọn feature đã Approved" | Registry: WP có `requiresApprovedDocs`; orchestrator không mở F3 khi SRS chưa có trong biên bản duyệt |
| K-14 | **Commit thuần ASCII**, tiền tố theo loại + mã phiên (`docs(nhanh): N21 - …`, `fix(probe): N20 - …`), `Refs: D-xx` ở đầu PR; `git add` theo đường dẫn cụ thể | `git log`, `decisions.md` §Cách dùng | Không có | OPS/ORCH + `CLAUDE.md` |

> Luật cũ **trùng** với bản gốc (exit code là bằng chứng, enum append, không nới test, handover có số đo) giữ nguyên
> cách diễn đạt của **eCommerce**, không dùng câu chữ của SelfLearningAI.

### 14.3. Bốn chỗ kế hoạch mới mâu thuẫn quy ước cũ — đã sửa trong tệp này

| # | Mâu thuẫn | Quyết định | Đã sửa ở |
|---|---|---|---|
| **M1** | Bản gốc: "BE/FE **không** được ghi tài liệu, ghi change request". eCommerce: "sửa tài liệu **trước**, trong cùng PR" (K-09) | **Giữ luật eCommerce, thực hiện qua vai**: BE/FE chỉ được sửa **mục trạng thái triển khai** cuối tài liệu feature. Khi code buộc lệch hợp đồng: BE trả `docDeviation` ⇒ orchestrator gọi **BA** sửa SRS **trên cùng nhánh** ⇒ DOCREV kiểm nhanh phần đổi ⇒ BE làm tiếp. Lệch là quyết định ⇒ qua DEC. `docs/inbox/` chỉ dùng khi lệch phải chờ người | §4.1 (BE, FE) |
| **M2** | Kế hoạch ghi G1 = "đổi header `Draft → Approved`". Thực tế: trạng thái duyệt nằm ở **biên bản trung tâm** (`docs/bien-ban-duyet-gd1-2026-10-29.md`), header 26 tệp **cố ý** để `Draft` | Agent tra trạng thái duyệt theo thứ tự: **biên bản duyệt** → `docs/features/README.md` §2 → header. Duyệt mới ⇒ biên bản mới cùng khuôn (`docs/bien-ban-duyet-<phạm vi>-<ngày>.md`), **không** sửa header | §6.1, §6.2 |
| **M3** | Hai lộ trình song song; skill `decision-policy` coi `05`/`07` là "đã duyệt" khi chưa được xác nhận | Thêm **Q9** ⇒ ✅ **đã chốt `D-69`**: `05` + `07` là lộ trình chính | §11, `decision-policy` §3, `docs/nhanh/decisions.md` |
| **M4** | G3 dùng `dotnet build` không `--no-incremental` | Thêm cờ + "0 warning mới" | §6.2 |

---

### 14.4. Kết quả bước 1b (14/11/2026)

| Việc | Kết quả | Kiểm bằng |
|---|---|---|
| Nhãn đầu tệp cho tài liệu cũ | **20 tệp**: 12 nhãn **LỊCH SỬ**, 4 nhãn **BÀI HỌC**, 4 nhãn **PHẠM VI HIỆU LỰC** (`implementation-plan`, `architecture`, `structure-and-techstack`, `business-modules-proposal`). Chèn **sau dòng tiêu đề**, giữ nguyên byte + kiểu xuống dòng của phần còn lại; **không** di chuyển/đổi tên tệp nào | `git diff --stat`: +2 dòng mỗi tệp |
| Handover cũ `docs/nhanh/handover-n*.md` | **Không** gắn nhãn từng tệp (mỗi phiên lại cũ thêm một tệp) — thay bằng luật "chỉ bản mới nhất là điểm vào" trong `CLAUDE.md` §2 | |
| `CLAUDE.md` | Tạo mới: điểm vào theo thứ tự · danh sách KHÔNG dùng làm điểm vào · lệnh bằng chứng · quy trình phiên · gọi `decider` thay vì dừng hỏi · tài liệu là hợp đồng · bất biến · git | cổng link |
| Ánh xạ lộ trình | `docs/agents/anh-xa-lo-trinh-d69.md`: 19 dòng — **15 R** (rõ ràng), **4 H** (hỏi người, đã vào `docs/agents/hang-doi-xem-lai.md`). Phát hiện: **mọi việc GĐ3 dựa trên tài liệu chưa duyệt** ⇒ `WP-14` hợp lý là *duyệt tài liệu GĐ3*, không phải code | |

**Nơi nhận của 14 luật K-xx — trạng thái thật:**

| Luật | Đã có nơi nhận | Chờ bước sau |
|---|---|---|
| K-01 dừng phiên · K-02 cắt ở điểm xanh · K-04 PR nhỏ · K-10 song song · K-14 commit | `CLAUDE.md` §4, §8 + `tech-defaults.md` | skill `feature-pipeline` (bước 2) |
| K-03 số test chỉ tăng · K-11 `--no-incremental` | `CLAUDE.md` §3 + `tech-defaults.md` | script G3 (bước 2) |
| K-09 sửa tài liệu trước · K-13 không code khi tài liệu chưa duyệt | `CLAUDE.md` §6 + `decision-policy` | agent BA/BE (bước 1) |
| K-05 đường lùi trong DoD WP | — | `registry.json` (bước 2) |
| K-06 số ca tối thiểu theo tầng | `tech-defaults.md` (để `decider` dùng) | agent QA (bước 1) + cổng G4 (bước 2) |
| K-07 14 quy tắc chất lượng tài liệu | — | agent DOCREV nhóm D7 (bước 1) |
| K-08 người review theo loại feature | — | skill `feature-pipeline` (bước 2) |
| K-12 go/no-go + cắt phạm vi | vùng cấm #5 của `decider` (chặn thêm phạm vi) | skill `feature-pipeline` (bước 2) |

⇒ **9/14** luật đã có ít nhất một nơi nhận đang chạy; **5** chờ các agent/skill của bước 1–2 (chưa tồn tại thì chưa nhận được).

---

## 15. Phụ lục — đối chiếu file nguồn → file đích

| Nguồn (`SelfLearningAI`) | Đích (`eCommerce`) | Cách chuyển |
|---|---|---|
| `agents/roles/00-orchestrator/ROLE.md` | `CLAUDE.md` + `.claude/skills/feature-pipeline/` | Tách: luật bất biến ⇒ CLAUDE.md; vòng đời + xử lý hỏng ⇒ skill |
| `agents/roles/10…70/ROLE.md` | `.claude/agents/{ba,arch,be,fe,qa,rev,ops}.md` | Frontmatter `name/description/tools/model/skills` + thân ≤ 150 dòng |
| `agents/roles/65-doc-reviewer/ROLE.md` | `.claude/agents/docrev.md` + skill `doc-review-d1-d6` | |
| `agents/state/board.json` + `work-packages.template.json` | `agents/state/registry.json` | Gộp (Y5) |
| `scripts/wp.ps1`, `session-new/merge.ps1` | `tools/agents/wp.ps1` | Port; dùng worktree của Claude Code khi có |
| `scripts/decide.ps1` | `tools/agents/decide.ps1` | Ghi `D-xx` vào `docs/nhanh/decisions.md` |
| `scripts/check-artifact.ps1`, `check-docs.ps1` | `tools/agents/check-artifact.ps1`, `check-feature-doc.ps1` | 12 mục ⇒ 16 mục |
| `scripts/negative-control.ps1` | `tools/agents/negative-control.ps1` + skill | |
| `scripts/check-all.ps1` | **không port** — đã có `tools/kiem-tat-ca.ps1` | |
| `SelfLearningAI/docs/00-templates/feature-design-template-v2.md` | **không port** — giữ `docs/templates/feature-design-template.md` | Chỉ bổ sung luật BR có `*Test:*` nếu mẫu chưa bắt buộc |
| `SelfLearningAI/docs/09-process/decisions.md` | **không port** — dùng `docs/nhanh/decisions.md` | Thêm cột tầng A/B/C |
| `docs/10-inbox/` | `docs/inbox/` | |
