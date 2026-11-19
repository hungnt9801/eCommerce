# Kế hoạch v2: dựng quy trình agent cho `eCommerce` trên Claude Code (gói Pro)

> **Trạng thái:** Draft — chờ duyệt · **Ngày lập:** 17/11/2026
> **Quan hệ với kế hoạch cũ:** [ke-hoach-he-thong-agent.md](ke-hoach-he-thong-agent.md) (14/11/2026) **vẫn là kế hoạch gốc** — phần
> review nguồn (§1), chín vai (§4), danh sách đỏ (§4.3), xếp loại tài liệu cũ (§14) **giữ nguyên**. Tệp này:
> ① khảo sát lại hai dự án ngày 17/11, ② **sửa 13 giả định** của bản 14/11 sau khi đối chiếu tài liệu chính thức của Claude Code,
> ③ thêm ràng buộc **gói Pro** (hạn mức 5 giờ + hạn mức tuần), ④ đổi thứ tự dựng cho khớp `D-71` (WP-14 thuần tài liệu),
> ⑤ cụ thể hoá đến mức **thi công được**: danh sách tệp, nội dung từng tệp, ca thử, DoD từng bước.
> **Chưa làm gì ngoài tệp này.** Không tệp nào khác bị tạo/sửa; không có `D-xx` mới.
> **Bổ sung 17/11:** [de-xuat-uy-quyen-va-chay-tu-dong.md](de-xuat-uy-quyen-va-chay-tu-dong.md) — phiên uỷ quyền một lần cho `decider`
> + bộ chạy tự động; nếu được duyệt sẽ **đảo C11** (hàng việc máy đọc cần có từ bước U4) và thay B3 thủ công bằng U4–U5.
> **Ghi chú tên nguồn:** yêu cầu ghi `D:\Projects\Tools\SelfSelfLearni` — thư mục này không có; thư mục khớp là
> `D:\Projects\Tools\SelfLearningAI` (đã khảo sát).

---

## 0. Tóm tắt trong 12 dòng

1. `SelfLearningAI` đã chạy xong **46/46 WP** bằng dây chuyền **9 vai** (ORCH · BA · ARCH · BE · FE · QA · REV · DOCREV · OPS),
   **104 quyết định** `D-001…D-103`, 36 ADR, bàn giao bằng **tệp** + khối JSON, cổng G1–G6, chỉ tin **exit code**.
2. Bên DeepSeek, "skill" chỉ là **`ROLE.md`** (9 tệp, 905 dòng). Thư mục `playbooks/` dự kiến trong đề xuất **không được dựng**;
   `agents/workflows/` **rỗng**. Workflow thật nằm trong `00-orchestrator/ROLE.md` + `scripts/wp.ps1`.
3. `eCommerce` đã có: `CLAUDE.md`, agent `decider` + skill `decision-policy`, xếp loại tài liệu cũ, `D-69…D-71`, và **WP-14 đã mở**
   (cổng tài liệu GĐ3, 18 tài liệu, **0 tệp `.cs`**). Chưa có: 8 vai còn lại, hook, `settings.json`, script `tools/agents/`.
4. Đối chiếu tài liệu Claude Code (17/11): **rủi ro lớn nhất của bản 14/11 (R1 — hook không biết subagent nào đang gọi) đã hết**:
   input hook có `agent_type` và `agent_id`. Hook còn khai được **trong frontmatter** của từng subagent, và `SubagentStop` **chặn được**.
5. Ba giả định của bản 14/11 sai hoặc đã lỗi thời (chi tiết §3): subagent **lồng được** tối đa 3 tầng; gói Pro tính Opus nặng hơn vào
   cùng hạn mức (gói có Opus 5.5 + Sonnet 5.5 — chủ dự án xác nhận 17/11); `Workflow` nay là "dynamic workflows" có chi phí riêng.
6. **Ràng buộc mới quyết định thiết kế: gói Pro** — một hạn mức 5 giờ + một hạn mức tuần, **dùng chung** với chat/Cowork; mọi
   subagent tính vào cùng hạn mức. Song song nhiều phiên **không** tăng ngân sách ⇒ hoãn `wp.ps1`/registry.
7. WP-14 có **2,17 MB** tài liệu (đợt A 1,18 MB, `SAL-006` một tệp 302 KB). Đọc nguyên văn hai lượt (BA + DOCREV) gần như chắc
   **vượt một cửa sổ 5 giờ** ⇒ đề xuất chia phiên nhỏ hơn và luật **đọc theo mục**.
8. **Đổi thứ tự dựng:** hàng rào (hook) trước → vai **tài liệu** (BA, DOCREV) → **Pilot-D trên WP-14** → vai **code** →
   **Pilot-C trên WP-15**. Bản 14/11 dựng cả 8 vai rồi mới pilot một feature có FE — không khớp `D-71`.
9. Hook viết bằng **Node `.mjs`** (khởi động nhanh, đọc JSON sẵn, repo đã dùng Node trong `tools/`); cổng kiểm vẫn là PowerShell.
10. **Model đã chọn** (gói Pro có Opus 5.5 + Sonnet 5.5): **Opus** cho 4 vai phán xét DOCREV, REV, ARCH, DEC; **Sonnet** cho ORCH, BA,
    BE, FE, QA, OPS; ghim ID đầy đủ; có luật nâng BE / hạ DOCREV theo số đo (§5.2).
11. Lộ trình **8 bước B0–B7**, mỗi bước kết thúc ở điểm xanh, ước **5–7 phiên** đến hết Pilot-C (§11).
12. Còn **10 câu** cần chốt (§13): 5 câu cũ còn mở + 5 câu mới; chỉ **3 câu** phải hỏi người (Q3, Q7, Q15), còn lại `decider` chốt tạm được.

---

## 1. Khảo sát `SelfLearningAI` (đo 17/11/2026)

### 1.1. Kho hiện vật

| Nhóm | Hiện vật | Số đo |
|---|---|---|
| Vai | `agents/roles/<nn>-<vai>/ROLE.md` | 9 tệp, 905 dòng; mỗi tệp: *Bạn là ai · Đọc trước · Được ghi · KHÔNG được ghi · Luật cứng · DoD máy kiểm · JSON trả về · Khi bị chặn · Bài học bắt buộc* |
| Trạng thái | `agents/state/board.json` (feature × stage × attempts × artifact) · `work-packages.json` (WP × dependsOn × writeScope × addsMigration × dod × stopIf × claim) | 23 KB · 129 KB, **46/46 WP `done`**, 18 WP có migration |
| Điều phối | `scripts/wp.ps1` (`status/check/start/done/release/heartbeat/blocked/sync-doc`, mã thoát 0/2/3/4) · `session-new.ps1` / `session-merge.ps1` (worktree + nhánh, trạng thái dùng chung ở worktree chính) | — |
| Kiểm | `check-all.ps1` (13 mục) · `check-artifact.ps1` (srs/arch/testplan/review) · `check-docs.ps1` · `negative-control.ps1` (mutate → test đỏ → khôi phục → ghi bằng chứng) · `verify.ps1` | — |
| Quyết định | `decide.ps1` (từ chối nếu thiếu `-Revert`/`-ReviewBefore` hoặc `-Tier C`) → `SelfLearningAI/docs/09-process/decisions.md` | 104 mục |
| Tài liệu theo feature | `04-features/<ID>-srs.md` (BA) · `<ID>-arch.md` (ARCH) · `05-tests/<ID>-testplan.md` (QA) · `06-review/<ID>-review.md` + `-docreview.md` (REV/DOCREV) · `07-ops/<ID>-deploy.md` (OPS) | 20 SRS, 29 testplan, 61 review |
| Tài liệu hệ thống | `01-product` (PRD, backlog) · `02-design` (SAD, db/, api/, ranh giới module) · `03-ai` · `08-adr` (36) · `10-inbox` (CR, blocked, brief phiên) · `00-templates` (mẫu SRS v2 12 mục) | — |

### 1.2. Quy trình (vòng đời một feature)

```text
board.json → chọn feature stage thấp nhất, không blocked
F1  BA      → check-artifact srs      → F1b DOCREV (DOC-OK) → G1 người
F2  ARCH    → check-artifact arch     → F2b DOCREV          → G2 người (tầng B nếu expand-only)
F3  BE ∥ FE → verify.ps1 (build+test) → G3 máy
F4  QA      → negative control bắt buộc → G4 máy
F5  REV     → VERDICT PASS / CHANGES (≤ 2 vòng) → G5
F6  OPS     → CI xanh → G6
DONE → commit → board.json
```

### 1.3. Điều làm nên giá trị (đã có bằng chứng trong nguồn)

- **Lỗi nằm ở tài liệu nhiều hơn ở code.** Pilot `LRN-002`/`PLT-001`: 6 lỗi nghiêm trọng của tài liệu, **không lỗi nào** bị bắt ở cổng
  của chính nó ⇒ sinh vai DOCREV (D1–D6). Pilot `LRN-002`: 3 P1 trước merge, **2 P1 chỉ lộ sau khi test đã xanh** (REV bắt).
- **Negative control có số thật**: mỗi ca ghi mutation, filter, output đỏ, output xanh (`SelfLearningAI/docs/09-process/negative-control.md`).
- **Bài học đi theo vai**: mục cuối của ARCH/BE/QA/REV ghi lỗi đã mắc kèm cơ chế (ví dụ "migration thành công mà không làm gì").
- **Thẩm quyền theo tính đảo ngược**, không theo chủ đề (A/B/C) — eCommerce đã kế thừa bằng `decider`.

### 1.4. Điểm yếu (không chép)

Vẫn đúng như [§1.3 bản 14/11](ke-hoach-he-thong-agent.md) (Y1–Y5). Bổ sung:

| # | Điểm yếu | Bằng chứng |
|---|---|---|
| Y6 | Không đo được token ("Môi trường không có instrumentation") ⇒ ước lượng chi phí cả dự án là đoán | `pilot-LRN-002-report.md` §3.2 |
| Y7 | "Skill" chỉ là luật chữ trong một tệp dài; không nạp theo nhu cầu ⇒ mỗi lần gọi vai trả toàn bộ ngữ cảnh | 9 `ROLE.md`, không có `playbooks/` |
| Y8 | Phiên dài làm chất lượng giảm: 4/5 lỗi tự gây ở **một phần ba cuối phiên** | `ke-hoach-tong-the.md` §3 |

Y8 cùng kết luận với luật dừng phiên "context > ~70%" của eCommerce — trên gói Pro còn thêm lý do **ngân sách**.

---

## 2. Hiện trạng `eCommerce` (đo 17/11/2026)

| Mặt | Hiện trạng | Hệ quả |
|---|---|---|
| Claude Code | `CLAUDE.md` (bước 1b) · `.claude/agents/decider.md` · `.claude/skills/decision-policy/` (+2 tham chiếu). **Không** có `settings.json`, hook, 8 vai còn lại | Toàn bộ §4–§8 là việc mới |
| Lộ trình | `D-69` (lộ trình chính `05`+`07`) · `D-70` (phạm vi GĐ3) · `D-71` (**WP-14 = cổng tài liệu GĐ3**, 18 tài liệu, 3 đợt `N22…N24`, 0 `.cs`) | Việc đầu tiên dây chuyền phải làm là **tài liệu**, không phải code |
| Phiên | Handover mới nhất `docs/nhanh/handover-n21.md`; `N22` chưa chạy; câu mở phiên của `N21` §6 đã hết hiệu lực theo `D-71` ⑤ | Pilot-D = `N22` |
| Hàng đợi người | `docs/agents/hang-doi-xem-lai.md`: 0 dòng chờ (H1–H4 đã chốt `D-70`) | — |
| Build máy dev | `dotnet build` **đỏ `MSB3491`** (không ghi được `obj\`, thư mục thuộc `BUILTIN/Administrators`) — ghi ở `07` §WP-14 | Chặn **WP-15** và mọi vai code; **không** chặn WP-14 |
| Mẫu tài liệu | `docs/templates/feature-design-template.md`: 476 dòng, 16 mục `## 0.`…`## 15.` | Tài liệu thật dài **5–16 lần** mẫu (86–338 KB) ⇒ luật đọc theo mục |
| Cổng máy | `tools/kiem-tat-ca.ps1` (nhẹ 9/9 · `-DayDu` 19/19) · `kiem-link-tai-lieu.ps1` · `check:mermaid` · `test-cong-kiem.ps1` · CI 2 required check | Là G3/G6 — **không viết lại** |
| Cổng link | Quét mọi `*.md` (kể cả `.claude/`); kiểm link markdown + đường dẫn trong backtick bắt đầu bằng `docs/`, `tools/`… và đuôi `.md`; bỏ qua đường dẫn có `<`, `*`, `…` | Mọi tệp agent/skill phải qua cổng này; đường dẫn tương lai viết dạng `<MÃ>` |

---

## 3. Mười ba chỗ phải sửa trong kế hoạch 14/11

Đối chiếu với tài liệu chính thức của Claude Code ngày 17/11 (nguồn ở §15) và với `D-71`.

| # | Bản 14/11 nói | Thực tế 17/11 | Sửa thành |
|---|---|---|---|
| C1 | "Subagent **không gọi được** subagent khác" ⇒ ORCH phải là phiên chính (§0.6) | Subagent **lồng được tối đa 3 tầng** (biến `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH`; `1` = tắt lồng). Bỏ `Agent` khỏi `tools` để chặn từng vai | ORCH **vẫn** là phiên chính (người gác cổng ở đó), nhưng **mọi vai bỏ `Agent`** khỏi `tools` ⇒ không lồng, chi phí có trần. BA/ARCH **không** tự gọi `decider` — trả `openQuestions` để ORCH gom |
| C2 | R1 "hook có nhận được định danh subagent không" — rủi ro **Cao**, dự phòng bằng marker file | Input hook có `agent_type` (tên vai) và `agent_id` khi chạy trong subagent; phiên chính **không** có hai trường này | Bỏ phương án marker. `guard-write-scope` tra `agent_type` → `scopes.json`. Rủi ro R1 hạ xuống **Thấp**, vẫn có ca thử ở B1 |
| C3 | Hook chỉ khai ở `settings.json` | Hook khai được **trong frontmatter** subagent (chỉ chạy khi subagent đó chạy); hook frontmatter của **project** chỉ chạy sau khi tin thư mục (workspace trust), phiên `-p` không tính | Hook **trung tâm** ở `.claude/settings.json` (luôn chạy, một nguồn luật) — không dùng hook frontmatter cho hàng rào |
| C4 | `SubagentStop` chỉ để **báo** | `SubagentStop` **chặn được** việc dừng (exit 2 / `"decision": "block"`) và trả phản hồi cho chính subagent | Thiếu artifact/khối JSON ⇒ chặn dừng, bảo vai **tự bổ sung ngay** (rẻ hơn ORCH gọi lại). **Trần 1 lần chặn / `agent_id`** để không lặp vô hạn |
| C5 | `skills:` "cần xác nhận được nạp" | Đã xác nhận: nạp **toàn văn** lúc khởi động, tối đa 32 skill; skill có `disable-model-invocation: true` **không** nạp sẵn được | Skill của vai: không đặt cờ đó. Skill điều phối gọi tay (`/doc-gate`, `/feature`) **đặt** cờ đó |
| C6 | Tool `Workflow` cho L2 | Nay là "dynamic workflows"; mỗi agent của workflow có request riêng, tính vào cùng hạn mức | Giữ "không bật mặc định"; **không** dùng trên gói Pro trước khi có số đo |
| C7 | Opus cho ARCH/REV/DOCREV/DEC | Gói Pro: 5 giờ + tuần, dùng chung chat/Cowork; model nặng làm hết hạn mức nhanh hơn; trang hỗ trợ **không** nói rõ Pro có Opus trong Claude Code ✅ Đã xác nhận 17/11 (chủ dự án): Pro có **Opus 5.5** + **Sonnet 5.5** ⇒ chọn model theo vai ở §5.1, lý do + luật nâng/hạ ở §5.2 |
| C8 | Hook bằng `pwsh` | Hook chạy **mỗi** lần gọi `Edit`/`Write`/`Bash`; `pwsh` khởi động chậm trên Windows | Hook bằng **Node `.mjs`** (repo đã dùng Node cho `tools/*.mjs`; phiên bản kiểm ở B0); script cổng vẫn PowerShell |
| C9 | Pilot 1 feature **có FE** (Q3) | `D-71`: việc kế tiếp là WP-14 **thuần tài liệu**; WP code đầu tiên là WP-15, và build đang đỏ `MSB3491` | Hai pilot: **Pilot-D** (BA + DOCREV + DEC trên WP-14 đợt A) rồi **Pilot-C** (đủ F1→F6 trên WP-15) |
| C10 | Dựng đủ 8 vai rồi mới pilot | Vai code không có việc tới WP-15 | Dựng **theo nhu cầu**: vai tài liệu trước, vai code sau Pilot-D |
| C11 | `wp.ps1` + `registry.json` ở bước 2 | Trên Pro, phiên song song **dùng chung một hạn mức** ⇒ song song chỉ rút ngắn thời gian tường, không tăng lượng việc; eCommerce chạy tuần tự `N1…N21` | **Hoãn** sang B7, chỉ dựng khi thật sự chạy song song; trạng thái WP vẫn ở `07` + handover |
| C12 | `decide.ps1` ghi `D-xx` | `decider` đã tự ghi `D-xx` theo `decision-policy` §6 | Không port `decide.ps1`; thay bằng **kiểm** `D-xx` mới (có cách lùi + "xem lại trước") trong `check-agent-artifact.ps1` |
| C13 | `.claude/workflows/`, `artifacts/agents/<MÃ>.jsonl` do ORCH tự ghi | Hook `SubagentStop` có sẵn `agent_type`, `agent_id`, thông điệp cuối | Nhật ký lượt gọi do **hook** ghi (gitignore), ORCH không phải nhớ ghi |

---

## 4. Kiến trúc đích (bản thi công)

### 4.1. Cây tệp

```text
eCommerce/
├── CLAUDE.md                              ← GIỮ; thêm §9 "Vai & dây chuyền" (≤ 15 dòng) ở B2
├── .claude/
│   ├── settings.json                      ← hook + permissions dùng chung (commit)                     [B1]
│   ├── scopes.json                        ← NGUỒN DUY NHẤT vùng ghi + lệnh cho phép theo vai           [B1]
│   ├── hooks/
│   │   ├── guard-write-scope.mjs          ← PreToolUse Edit|Write|MultiEdit|NotebookEdit               [B1]
│   │   ├── guard-shell.mjs                ← PreToolUse Bash|PowerShell: vai chỉ-đọc + lệnh cấm chung    [B1]
│   │   ├── check-agent-stop.mjs           ← SubagentStop: khối JSON + artifact + git status trong vùng  [B1]
│   │   ├── session-context.mjs            ← SessionStart: báo handover mới nhất + WP đang mở           [B1]
│   │   └── test/*.test.mjs                ← `node --test`, không gọi model                             [B1]
│   ├── agents/
│   │   ├── decider.md                     ← CÓ; chỉ thêm `disallowedTools: Agent` + `maxTurns`          [B2]
│   │   ├── ba.md · docrev.md                                                                           [B2]
│   │   └── arch.md · be.md · fe.md · qa.md · rev.md · ops.md                                           [B5]
│   └── skills/
│       ├── decision-policy/               ← CÓ
│       ├── session-protocol/              ← /mo-phien, /dong-phien (08 §4, §5, §5.1)                    [B2]
│       ├── doc-gate/                      ← /doc-gate <đợt|MÃ…>: dây chuyền tài liệu (WP-14)            [B2]
│       ├── feature-doc-rules/             ← 16 mục, đọc theo mục, BR *Test:*, đối chiếu mô hình Nhanh   [B2]
│       ├── doc-review/                    ← D1–D7 + mẫu docreview                                      [B2]
│       ├── feature-pipeline/              ← /feature <MÃ>: F1→F6 cho WP code                            [B5]
│       ├── abp-module-rules/ · ef-migration-postgres/ · nextjs-admin-rules/                          [B5]
│       └── testing-negative-control/ · code-review/                                                  [B5]
├── tools/agents/
│   ├── check-feature-doc.ps1              ← 16 heading, BR có *Test:*, có mục "Đối chiếu mô hình Nhanh" [B2]
│   ├── check-agent-artifact.ps1           ← docreview/review/testplan/D-xx mới                         [B2]
│   └── negative-control.ps1               ← port nguồn; từ chối nếu tệp chưa commit                    [B5]
└── docs/
    ├── features/<module>/<mã>-<slug>.md   ← SRS (BA) — GIỮ chỗ cũ, không đổi tên
    ├── features/<module>/<mã>-arch.md     ← chỉ khi ≥ 2 module / đổi schema / đổi contract
    ├── reviews/<MÃ>-docreview.md · <MÃ>-review.md   ← DOCREV · REV (`D-71` đã dùng `docs/reviews/`)
    ├── tests/<MÃ>-testplan.md · ops/<MÃ>-deploy.md  ← QA · OPS                                     [B5]
    ├── inbox/<MÃ>-blocked.md                ← vai ghi khi bị chặn
    └── agents/                              ← tệp này, kế hoạch 14/11, báo cáo pilot
```

Không tạo: `agents/state/registry.json`, `tools/agents/wp.ps1`, `decide.ps1`, `.claude/workflows/` (lý do ở C11–C13).
Không thêm thư mục ADR: `D-xx` trong `docs/nhanh/decisions.md` đã đóng vai ADR.

### 4.2. Bảy luật tiết kiệm ngữ cảnh (bắt buộc trên gói Pro)

| # | Luật | Thi hành bằng |
|---|---|---|
| P1 | Tệp agent ≤ 120 dòng, `SKILL.md` ≤ 300 dòng; chi tiết để ở tệp phụ của skill, nạp khi cần | Ca kiểm trong `test-cong-kiem.ps1` (đếm dòng) |
| P2 | **Đọc theo mục**: `Grep "^## "` lấy số dòng → `Read` có `offset`/`limit`. Chỉ DOCREV đọc toàn văn, và chỉ tài liệu đang kiểm | Skill `feature-doc-rules` §1; DOCREV ghi "đã đọc dòng a–b" vào docreview |
| P3 | Vai **không có** `Agent` ⇒ không lồng | `disallowedTools: Agent` mọi vai |
| P4 | `maxTurns` theo vai (§5.1); hết lượt ⇒ kết quả dở, ORCH quyết | Frontmatter |
| P5 | Vai trả về **ngắn**: khối JSON + đường dẫn tệp; chi tiết nằm trong tệp | Hook `check-agent-stop` kiểm khối JSON |
| P6 | ORCH **không đọc lại** artifact để đánh giá — chạy script kiểm, chỉ đọc phần script báo đỏ | Skill `doc-gate` / `feature-pipeline` |
| P7 | Một phiên = một gói việc đo được; `/clear` giữa hai gói; đọc `/usage` (phân bổ theo subagent) trước khi đóng phiên, ghi vào handover | Skill `session-protocol` |

---

## 5. Vai

### 5.1. Bảng vai

`tools` liệt kê công cụ **có**; mọi vai đều `disallowedTools: Agent`. Vùng ghi chi tiết ở §7.2.

Model chọn ngày 17/11/2026 theo hai model có trong gói Pro: **Opus 5.5** (`claude-opus-5-5`) và **Sonnet 5.5** (`claude-sonnet-5-5`).
Lý do và luật nâng/hạ ở §5.2.

| Vai | Tệp | **Model** | **`effort`** | Tools | Skill nạp sẵn | Vùng ghi | `maxTurns` 🔶 | JSON cuối |
|---|---|---|---|---|---|---|---|---|
| **ORCH** | phiên chính | **Sonnet 5.5** (`/model` của phiên) | `high` | tất cả | gọi tay `/doc-gate`, `/feature`, `/mo-phien`, `/dong-phien` | `docs/agents/**`, handover, `docs/bien-ban-duyet-*` (bản nháp), `07` §13 tiến độ | — | handover 7 mục + khối A/B |
| **BA** | `ba.md` | **Sonnet 5.5** | `high` | Read, Grep, Glob, Edit, Write, Bash (chỉ đọc) | `feature-doc-rules` | `docs/features/**/*.md` (trừ `README.md`, `*-arch.md`), `docs/inbox/**` | 40 | `{mode, doc, sectionsPresent, nhanhDiffRows, brCount, brWithoutTest, openQuestions, readRanges}` |
| **DOCREV** | `docrev.md` | **Opus 5.5** | `high` | Read, Grep, Glob, Write, Bash (chỉ đọc; `psql` trong `BEGIN…ROLLBACK`) | `doc-review`, `feature-doc-rules` | **chỉ** `docs/reviews/*-docreview.md` | 40 | `{verdict, p1, p2, p3, sqlRun, readRanges}` |
| **DEC** | `decider.md` (có) | **Opus 5.5** (giữ như hiện tại) | `high` | như hiện tại | `decision-policy` | `docs/nhanh/decisions.md` (thêm dòng), `docs/agents/hang-doi-xem-lai.md` | 20 | như hiện tại |
| **ARCH** | `arch.md` | **Opus 5.5** | `high` | Read, Grep, Glob, Edit, Write, Bash (chỉ đọc + `psql` ROLLBACK) | `abp-module-rules`, `ef-migration-postgres` | `docs/features/**/*-arch.md`, `docs/inbox/**` | 30 | `{tablesWritten, contracts, schemaChanges, boundaryViolations, openQuestions}` |
| **BE** | `be.md` | **Sonnet 5.5** (ORCH nâng Opus theo §5.2 ②) | `medium` | tất cả trừ Web\*, Agent | `abp-module-rules`, `ef-migration-postgres` | `modules/**`, `src/**`, `shared/**` (khi ARCH cho), mục "Trạng thái triển khai" của SRS | 80 | `{buildExit, testExit, testsBefore, testsAfter, migrations, docDeviation}` |
| **FE** | `fe.md` | **Sonnet 5.5** | `medium` | tất cả trừ Web\*, Agent | `nextjs-admin-rules` | `apps/admin-web/**` (trừ `e2e/**`, `test/**`), `packages/api-client/**` | 60 | `{lintExit, typecheckExit, testExit, buildExit, genApiDiff, screens, statesCovered}` |
| **QA** | `qa.md` | **Sonnet 5.5** | `high` | tất cả trừ Web\*, Agent | `testing-negative-control` | `test/**`, `apps/admin-web/e2e/**`, `apps/admin-web/test/**`, `tools/probe-*.ps1`, `docs/tests/**` | 60 | `{brCovered, brTotal, uncovered, negativeControl[], testExit}` |
| **REV** | `rev.md` | **Opus 5.5** | `high` | Read, Grep, Glob, Write, Bash (chỉ đọc + chạy test) | `code-review`, `abp-module-rules` | **chỉ** `docs/reviews/*-review.md` | 30 | `{verdict, p1, p2, p3}` |
| **OPS** | `ops.md` | **Sonnet 5.5** | `medium` | tất cả trừ Web\*, Agent | `ef-migration-postgres` | `.github/**`, `docker-compose.yml`, `**/Dockerfile`, `tools/*.ps1` (trừ `probe-*`), `docs/ops/**` | 30 | `{filesChanged, envVarsAdded, migrations, rollback, gateExit}` |

**Tóm tắt:** 4 vai Opus (DOCREV, DEC, ARCH, REV) là vai **phán xét**, gọi **ít lần** hoặc đọc khối lượng **có trần**; 6 vai Sonnet
(ORCH, BA, BE, FE, QA, OPS) là vai **làm theo hợp đồng** và chiếm phần lớn khối lượng đọc/ghi.

BA có hai chế độ trong cùng một tệp vai: `viet-moi` (feature mới) và `doi-chieu` (WP-14 ①: thêm mục "Đối chiếu mô hình Nhanh",
sửa thân tài liệu cho hết lệch, mô tả code thật kèm `file:dòng`). Không tách thành vai thứ mười.

Nội dung luật của từng vai: lấy theo [§4.2 bản 14/11](ke-hoach-he-thong-agent.md) (đã viết lại cho ABP + Next.js) + các luật K-01…K-14
(§14.2 bản 14/11), cộng "Bài học bắt buộc" của nguồn. Mỗi tệp vai có đúng 9 mục cố định, mục 9 luôn là "Bài học bắt buộc"
(tránh lỗi đánh số lặp Y2).

### 5.2. Lý do chọn model và luật nâng/hạ

**Nguyên tắc:** Opus đặt ở chỗ **một lỗi lọt qua đắt nhất và khối lượng đọc nhỏ nhất**. Sonnet đặt ở chỗ khối lượng lớn và đầu ra
được máy kiểm lại (build, test, script cổng, rồi vai Opus phía sau).

| Vai | Model | Vì sao |
|---|---|---|
| DOCREV | Opus | Nguồn đo được: **6/6** lỗi tài liệu nghiêm trọng không bị cổng của chính nó bắt; lỗi D1 (tự mâu thuẫn cách 6 dòng) và D3 (BR không có cơ chế) cần suy luận sâu. WP-14 là việc đầu tiên của dây chuyền ⇒ chất lượng DOCREV quyết định Pilot-D. **Chi phí lớn nhất trong 4 vai Opus** vì đọc toàn văn — xem luật ③ |
| REV | Opus | Pilot `LRN-002` của nguồn: **2 P1 chỉ lộ sau khi test đã xanh**, đều do REV bắt. Khối lượng có trần: diff ≤ ~600 dòng (K-04) + mục SRS liên quan |
| ARCH | Opus | Chỉ gọi khi ≥ 2 module / đổi schema / đổi contract ⇒ **ít lần**; sai ranh giới module hoặc schema là lỗi đắt nhất để sửa sau |
| DEC | Opus | Mỗi lượt ngắn; ra quyết định thay người, có quyền ghi `D-xx` ⇒ giữ như tệp `decider.md` đang có |
| ORCH | Sonnet | Phiên **dài nhất**, ngữ cảnh lớn nhất ⇒ mọi token ở đây nhân lên nhiều lần. Việc của ORCH là chạy script và gọi vai theo skill — phán xét đã giao cho 4 vai Opus. Khi cần lập kế hoạch khó (chia lại WP, retro): `/model` sang Opus cho **một lượt**, rồi trả về |
| BA | Sonnet | Đọc nhiều nhất (đối chiếu 18 tài liệu với `D-xx` + code). Đầu ra bị `check-feature-doc.ps1` và DOCREV (Opus) kiểm lại. `effort: high` vì chế độ `doi-chieu` cần so kỹ |
| BE, FE | Sonnet | Khối lượng code lớn; bị build/test/lint/`openapi-drift` + QA + REV (Opus) kiểm lại |
| QA | Sonnet | Viết nhiều test; `effort: high` vì thiết kế negative control cần tư duy "phá". Negative control là cơ chế máy kiểm (đỏ → xanh), không dựa vào model |
| OPS | Sonnet | Việc theo khuôn (CI, compose, script); bị cổng `-DayDu` + CI kiểm lại |

**Luật nâng/hạ (ORCH áp dụng, ghi vào handover; đổi mặc định của vai = sửa frontmatter ⇒ tầng B, ghi `D-xx`):**

1. **Ghim model đầy đủ** trong frontmatter: `model: claude-opus-5-5` / `model: claude-sonnet-5-5` (không dùng bí danh `opus`/`sonnet`)
   ⇒ số đo giữa các pilot so được với nhau; đổi model là quyết định có ghi, không xảy ra ngầm khi bí danh trỏ sang bản mới.
2. **Nâng BE lên Opus cho một lượt gọi** (tham số `model` khi ORCH gọi `Agent`) **chỉ khi** feature thuộc danh sách đỏ §4.3 bản 14/11
   **và** lượt đó chạm lõi tính tiền / sổ cái tồn kho / máy trạng thái đơn. Phần còn lại của cùng feature vẫn Sonnet.
3. **Hạ DOCREV xuống Sonnet `effort: xhigh`** cho tài liệu **không** thuộc danh sách đỏ, **nếu** số đo Pilot-D cho thấy một tài liệu
   (BA + DOCREV) dùng > **40%** hạn mức 5 giờ 🔶 (ngưỡng tạm, chỉnh ở B4). Tài liệu danh sách đỏ (`SAL-006`, `REC-003`, `SHP-003`
   COD, `ACC-001`, `REP-002` lãi gộp) **luôn** Opus.
4. **Vai Opus chạm hạn mức giữa phiên** ⇒ không tự chuyển vai đó sang Sonnet để chạy tiếp; dừng ở điểm xanh, ghi handover (vai phán xét
   chạy model yếu hơn mà không ghi lại thì số đo pilot sai).
5. Không đặt biến môi trường `CLAUDE_CODE_EFFORT_LEVEL` khi chạy dây chuyền (biến này **ghi đè** `effort` trong frontmatter của mọi vai).
6. Không có Haiku trong gói ⇒ không dùng; Fable không nằm trong hạn mức Pro ⇒ không dùng.

---

## 6. Skill

| Skill | Gọi | Dùng cho | Nội dung chính | Nguồn port |
|---|---|---|---|---|
| `session-protocol` | `/mo-phien`, `/dong-phien` (gọi tay) | ORCH | Baseline 6 lệnh · handover 7 mục + **đúng một** khối A/B · bảng "D-xx DEC đã ghi trong phiên" · bảng `/usage` · 5 dấu hiệu dừng | `docs/nhanh/08-session-protocol.md` §4–§6 (dẫn, không chép) |
| `doc-gate` | `/doc-gate A` hoặc `/doc-gate ADDR-001 ADDR-002` (gọi tay, `disable-model-invocation: true`) | ORCH | Dây chuyền §9.1: BA `doi-chieu` → script → DOCREV → DEC cho câu hỏi mở → ≤ 2 vòng → nháp dòng biên bản duyệt | `07` §WP-14 ①–⑤, ORCH §2–§4 của nguồn |
| `feature-pipeline` | `/feature <MÃ>` (gọi tay) | ORCH | F1→F6, cổng G1–G6, trần 2 vòng, xử lý hỏng, người review theo loại (K-08), go/no-go (K-12) | ORCH `ROLE.md` §2–§6 |
| `feature-doc-rules` | nạp sẵn | BA, DOCREV | Đọc theo mục (P2) · 16 mục · `BR-` có `*Test:*` · NFR có ngưỡng số · danh sách `D-xx` phải đối chiếu (`D-07`, `D-08`, `D-18`/`D-42`, `D-27`, `D-31`, `D-45`, `D-47`/`D-48`) · "code buộc lệch ⇒ sửa tài liệu trước" | `docs/design-doc-guidelines.md`, `docs/templates/feature-design-template.md`, BA `ROLE.md` B1–B10 |
| `doc-review` | nạp sẵn | DOCREV | D1–D6 của nguồn + **D7** = 14 quy tắc `design-doc-guidelines` §5 · D4 chạy SQL trên PostgreSQL 17 của compose trong `BEGIN…ROLLBACK`, không có DB ⇒ ghi "chưa chạy" · mẫu docreview (tệp phụ `templates/docreview.md`) | `65-doc-reviewer/ROLE.md` |
| `abp-module-rules` | nạp sẵn | ARCH, BE, REV | Ranh giới module ABP, `*.Application.Contracts`, ETO + outbox `XC-002`, `[Authorize]` cấp class, tenant filter, `Money` | ARCH A1–A5, BE B1–B10, `docs/coding-conventions.md` |
| `ef-migration-postgres` | nạp sẵn | BE, ARCH, OPS | Expand → migrate → contract · EF sinh migration · `NULLS NOT DISTINCT` · một chủ migration · `pg_dump` trước khi chạm DB có dữ liệu | BE bài học 4–5, `docs/nhanh/09-parallel-sessions.md` §6 |
| `nextjs-admin-rules` | nạp sẵn | FE, REV | Chỉ endpoint trong `schema.d.ts` · `pnpm gen:api` không đổi schema · 3 trạng thái · empty ≠ error · chống bấm lặp · token UI Nhanh | FE F1–F8, `docs/nhanh/04-web-admin-nextjs.md` |
| `testing-negative-control` | nạp sẵn | QA | Q1–Q7 · số ca tối thiểu theo tầng (K-06) · cách chạy `negative-control.ps1` · probe HTTP + Playwright · cấm stub "allow all" | QA `ROLE.md`, `07` §9 |
| `code-review` | nạp sẵn | REV | Checklist A–D + phần riêng eCommerce (permission ABP, tenant, idempotency webhook/IPN, token sàn không lộ, tiền không `double`) · P1/P2/P3 · mẫu review | REV `ROLE.md` |

Mẫu artifact (docreview, review, testplan, deploy, blocked) để **trong thư mục skill** (tệp phụ), không thêm vào `docs/templates/`
— chỉ agent dùng, và giữ `docs/templates/` một mẫu duy nhất (tránh Y3).

---

## 7. Hàng rào: hook, vùng ghi, quyền

### 7.1. `.claude/settings.json` (phác thảo — kiểm ở B1)

```json
{
  "hooks": {
    "SessionStart": [
      { "hooks": [ { "type": "command", "command": "node \"$CLAUDE_PROJECT_DIR/.claude/hooks/session-context.mjs\"" } ] }
    ],
    "PreToolUse": [
      { "matcher": "Edit|Write|MultiEdit|NotebookEdit",
        "hooks": [ { "type": "command", "command": "node \"$CLAUDE_PROJECT_DIR/.claude/hooks/guard-write-scope.mjs\"" } ] },
      { "matcher": "Bash|PowerShell",
        "hooks": [ { "type": "command", "command": "node \"$CLAUDE_PROJECT_DIR/.claude/hooks/guard-shell.mjs\"" } ] }
    ],
    "SubagentStop": [
      { "hooks": [ { "type": "command", "command": "node \"$CLAUDE_PROJECT_DIR/.claude/hooks/check-agent-stop.mjs\"" } ] }
    ]
  },
  "permissions": {
    "deny": [
      "Bash(git push:*)", "Bash(git merge:*)", "Bash(gh pr merge:*)",
      "Bash(git config --global:*)", "Bash(dotnet ef database update:*)"
    ]
  }
}
```

🔶 Cần đo trên Windows ở B1: cách `$CLAUDE_PROJECT_DIR` được thay trong lệnh hook; cú pháp mẫu `permissions` của phiên bản đang cài.
`dotnet ef database update` bị cấm cho agent vì migration chạy qua `DbMigrator`/cổng kiểm; người tự chạy khi cần.

### 7.2. `.claude/scopes.json` — nguồn duy nhất

```json
{
  "version": 1,
  "roles": {
    "ba":      { "write": ["docs/features/**/*.md", "docs/inbox/**"], "deny": ["docs/features/README.md", "docs/features/**/*-arch.md"], "shell": "read" },
    "docrev":  { "write": ["docs/reviews/*-docreview.md"], "shell": "read+psql-rollback" },
    "decider": { "write": ["docs/nhanh/decisions.md", "docs/agents/hang-doi-xem-lai.md"], "shell": "read" },
    "arch":    { "write": ["docs/features/**/*-arch.md", "docs/inbox/**"], "shell": "read+psql-rollback" },
    "rev":     { "write": ["docs/reviews/*-review.md"], "shell": "read+test" },
    "be":      { "write": ["modules/**", "src/**", "shared/**", "docs/features/**/*.md"], "docSection": "Trạng thái triển khai", "shell": "dev" },
    "fe":      { "write": ["apps/admin-web/**", "packages/api-client/**"], "deny": ["apps/admin-web/e2e/**", "apps/admin-web/test/**"], "shell": "dev" },
    "qa":      { "write": ["test/**", "apps/admin-web/e2e/**", "apps/admin-web/test/**", "tools/probe-*.ps1", "docs/tests/**"], "shell": "dev" },
    "ops":     { "write": [".github/**", "docker-compose.yml", "**/Dockerfile", "tools/*.ps1", "docs/ops/**"], "deny": ["tools/probe-*.ps1"], "shell": "dev" }
  },
  "neverWrite": ["eCommerce.slnx", "Directory.Packages.props", "pnpm-lock.yaml", ".claude/**", "docs/bien-ban-duyet-*.md"],
  "neverWriteExceptRoles": { "eCommerce.slnx": ["be", "ops"], "Directory.Packages.props": ["be", "ops"], "pnpm-lock.yaml": ["fe", "ops"] }
}
```

Luật của `guard-write-scope.mjs`:
1. Không có `agent_type` (phiên chính) ⇒ **cho qua** (người có mặt), trừ khi skill `/doc-gate` hoặc `/feature` đang chạy:
   hai skill này khai `hooks` trong frontmatter (skill được đăng ký hook "từ lúc gọi tới hết phiên") ⇒ ORCH bị khoá vào vùng của ORCH
   — thi hành luật nguồn "ORCH không tự sửa code".
2. `agent_type` có trong `roles` ⇒ đường dẫn phải khớp `write`, không khớp `deny`/`neverWrite` ⇒ ngược lại **exit 2** + lý do
   ("vai rev chỉ được ghi docs/reviews/*-review.md; ghi phát hiện vào review thay vì sửa").
3. `agent_type` lạ (vai chưa khai, ví dụ `Explore`, `general-purpose`) ⇒ **chặn ghi** (fail-closed).
4. `docSection` (BE): hook cho ghi tệp; `check-agent-stop` so diff — dòng đổi phải nằm **dưới** heading "Trạng thái triển khai",
   ngoài đó ⇒ báo `docDeviation` cho ORCH (luật M1 bản 14/11: BA sửa SRS, không phải BE).

Luật của `guard-shell.mjs`:
- `shell: read` ⇒ chỉ cho `git diff|log|show|status|blame|grep`, `rg`, `grep`, `ls`/`Get-ChildItem`, `pwsh -File tools/kiem-link-tai-lieu.ps1`,
  `npm --prefix tools run check:mermaid`, `pwsh -File tools/agents/check-*.ps1`. Chặn mọi chuyển hướng ghi (`>`, `>>`, `Set-Content`,
  `Out-File`, `tee`), `rm`/`Remove-Item`, `mv`, `git add|commit|checkout|restore|reset|stash`.
- `read+psql-rollback` ⇒ thêm `psql` khi câu lệnh bắt đầu `BEGIN` và kết thúc `ROLLBACK`, và chuỗi kết nối trỏ DB dev của compose.
- `read+test` ⇒ thêm `dotnet test`, `pnpm test`, `pwsh -File tools/kiem-tat-ca.ps1`.
- `dev` ⇒ cho mọi lệnh trừ danh sách cấm chung (push/merge/`git config --global`/xoá volume/`psql` ngoài DB dev).

### 7.3. `check-agent-stop.mjs` (SubagentStop)

1. Ghi một dòng JSONL vào `artifacts/agents/<ngày>.jsonl` (đã gitignore theo `artifacts/`): `agent_type`, `agent_id`, giờ, khối JSON cuối.
2. Thiếu khối JSON hợp lệ, hoặc thiếu artifact bắt buộc của vai (docreview/review/testplan) ⇒ **chặn dừng một lần** với danh sách thiếu;
   lần thứ hai vẫn thiếu ⇒ cho dừng, đánh dấu `artifactMissing` để ORCH xử lý theo trần 2 vòng.
3. Chạy `git status --porcelain`, so với `scopes.json` của vai ⇒ tệp ngoài vùng (ghi qua shell) ghi vào log + `additionalContext`
   cho ORCH. **Hook không tự `git restore`** — ORCH khôi phục đúng tệp (giữ nguyên luật nguồn §4).

### 7.4. Ca thử hàng rào (B1, `node --test`, không gọi model)

| Ca | Input giả lập | Kỳ vọng |
|---|---|---|
| H1 | `agent_type=rev`, Edit `modules/Sales/x.cs` | exit 2, stderr có "chỉ được ghi docs/reviews" |
| H2 | `agent_type=rev`, Write `docs/reviews/<MÃ>-review.md` (ví dụ mã `SHP-001`) | exit 0 |
| H3 | `agent_type=docrev`, Bash `echo x > docs/features/a.md` | exit 2 |
| H4 | `agent_type=ba`, Edit `docs/features/README.md` | exit 2 |
| H5 | `agent_type=Explore`, Write bất kỳ | exit 2 (fail-closed) |
| H6 | không `agent_type`, Edit `modules/x.cs`, không skill điều phối | exit 0 |
| H7 | `agent_type=be`, Edit `pnpm-lock.yaml` | exit 2 |
| H8 | `agent_type=docrev`, Bash `psql -c "BEGIN; SELECT 1; ROLLBACK;"` | exit 0 |
| H9 | SubagentStop `agent_type=docrev`, không có khối JSON, lần 1 / lần 2 | block / cho dừng + `artifactMissing` |

Sau ca giả lập: **một ca thật** trong Claude Code — gọi `rev`, bảo nó sửa một tệp `modules/**` ⇒ phải bị chặn, `git status` sạch.

---

## 8. Script `tools/agents/`

| Script | Việc | Mã thoát | Ca trong `test-cong-kiem.ps1` |
|---|---|---|---|
| `check-feature-doc.ps1 -Path <tệp> [-WP14]` | đủ 16 heading `## 0.`…`## 15.` · mọi `BR-` có `*Test:*` · `-WP14`: có mục "Đối chiếu mô hình Nhanh" không rỗng · không còn `TODO` | 0 đạt / 1 thiếu (liệt kê) | ≥ 3 (đủ, thiếu heading, BR thiếu Test) |
| `check-agent-artifact.ps1 -Kind docreview\|review\|testplan\|decision -Path <tệp>` | docreview: `VERDICT: DOC-OK\|DOC-CHANGES\|DOC-BLOCKED`, DOC-OK ⇒ P1 = 0, có bảng "Đã kiểm" đủ D1–D7 · review: `VERDICT: PASS\|CHANGES`, PASS ⇒ P1 = 0 · testplan: mỗi `BR-` của SRS xuất hiện ≥ 1 lần + có negative control đỏ→xanh · decision: dòng `D-xx` mới có cách lùi + "xem lại trước" | 0 / 1 | ≥ 4 |
| `negative-control.ps1` | port nguồn: từ chối khi tệp đích có thay đổi chưa commit; áp mutation → test **phải đỏ** → khôi phục → test **phải xanh** → ghi bảng bằng chứng vào testplan | 0 / 2 / 3 như nguồn | ≥ 2 |

Cả ba viết ASCII + `pwsh` 7, đọc/ghi UTF-8 tường minh (luật Y4 bản 14/11).

---

## 9. Workflow

### 9.1. Dây chuyền tài liệu — `/doc-gate` (WP-14 và mọi lần duyệt tài liệu sau này)

```text
/mo-phien  → baseline 6 lệnh (08 §4)
/doc-gate <MÃ…>  (mỗi MÃ tuần tự; ORCH bị khoá vùng ghi)
  D1  BA (doi-chieu) → JSON {nhanhDiffRows, openQuestions}
  D1s check-feature-doc.ps1 -WP14 → đỏ ⇒ gọi lại BA đúng danh sách thiếu (≤ 2)
  D1q openQuestions ≠ ∅ ⇒ DEC (song song từng câu) → TIEN_LE/TAM_CHOT: BA áp dụng · CHUYEN_NGUOI: gom cuối phiên
  D2  DOCREV (ngữ cảnh sạch) → docs/reviews/<MÃ>-docreview.md
  D2s check-agent-artifact.ps1 -Kind docreview → DOC-CHANGES ⇒ BA sửa theo phát hiện → DOCREV vòng 2 (trần 2)
  D3  ORCH: nháp dòng biên bản duyệt (DOC-OK / chưa + lý do) — KHÔNG ghi "đã duyệt"
  D4  kiem-link-tai-lieu.ps1 PASS · check:mermaid 0 lỗi · git diff --stat chỉ docs/**
/dong-phien → handover 7 mục + khối A/B + bảng /usage + câu hỏi CHUYEN_NGUOI (hỏi một lần)
Người: duyệt theo đợt, ký biên bản `docs/bien-ban-duyet-gd3-<đợt>-<ngày>.md` (`D-71` ④)
```

### 9.2. Dây chuyền code — `/feature` (WP-15 trở đi)

Giữ máy trạng thái [§6.1 bản 14/11](ke-hoach-he-thong-agent.md) (F1 → F1b → G1 → F2 → F2b → G2 → F3 BE ∥ FE → G3 → F4 QA → G4 →
F5 REV → G5 → F6 OPS → G6 → người merge), với ba thay đổi:

1. **F1/F1b bỏ qua** khi SRS đã có trong biên bản duyệt (trường hợp của mọi feature GĐ3 sau WP-14) — chỉ chạy BA khi code buộc lệch.
2. **BE ∥ FE chỉ khi** API đã merge (contract-first `09` §7) **và** có bảng cấp phát DB/cổng; trên Pro mặc định **tuần tự** BE → FE.
3. Trần làm lại: 2 vòng mỗi vai **và** 5 dấu hiệu dừng phiên (K-01) — cái nào tới trước thì dừng, ghi handover.

### 9.3. Bảng tài liệu theo giai đoạn phát triển

| Giai đoạn | Tài liệu eCommerce | Vai tạo / kiểm | Tương đương bên `SelfLearningAI` |
|---|---|---|---|
| Yêu cầu, khảo sát | `docs/nhanh/01…06`, `docs/business-modules-proposal.md` | người (+ DEC tra cứu) | `01-product/prd.md`, `product-backlog.md` |
| Lộ trình, WP | `docs/nhanh/05-lo-trinh-rui-ro.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` | người; DEC chốt tạm thứ tự trong phạm vi | `ke-hoach-tong-the.md`, `work-packages.json` |
| Đặc tả feature | `docs/features/<module>/<mã>-<slug>.md` (16 mục) | BA / DOCREV | `<ID>-srs.md` (12 mục) |
| Thiết kế liên module | `<mã>-arch.md` (chỉ khi cần) | ARCH / DOCREV | `<ID>-arch.md` + ADR |
| Kiểm tài liệu | `docs/reviews/<MÃ>-docreview.md` | DOCREV | `06-review/<ID>-docreview.md` |
| Duyệt | biên bản duyệt (khuôn `docs/bien-ban-duyet-gd1-2026-10-29.md`) | **người** | `board.json` G1/G2 |
| Thi công | code + mục "Trạng thái triển khai" của SRS | BE / FE | `src/`, `web/` |
| Kiểm thử | `docs/tests/<MÃ>-testplan.md` + bảng negative control | QA | `05-tests/<ID>-testplan.md`, `negative-control.md` |
| Review code | `docs/reviews/<MÃ>-review.md` | REV | `06-review/<ID>-review.md` |
| Vận hành | `docs/ops/<MÃ>-deploy.md`, CI | OPS | `07-ops/<ID>-deploy.md` |
| Quyết định | `docs/nhanh/decisions.md` (`D-xx`), `docs/agents/hang-doi-xem-lai.md` | DEC / người | `decisions.md` + `decide.ps1`, ADR |
| Bị chặn, đổi yêu cầu | `docs/inbox/<MÃ>-blocked.md` | mọi vai | `10-inbox/` |
| Bàn giao phiên | `docs/nhanh/handover-n<số>.md` (7 mục + A/B) | ORCH | `SESSION-LOG` + `bang-hang-muc.md` |

---

## 10. Ngân sách gói Pro

### 10.1. Khối lượng WP-14 (đo từ kích thước tệp, 17/11)

| Đợt | Tài liệu | Tổng | Tệp lớn nhất |
|---|---|---|---|
| A | 9 (`ADDR-001…003`, `SHP-001…004`, `REC-003`, `SAL-006`) | 1.181 KB | `SAL-006` 302 KB |
| B | 3 (`PRO-001`, `PRO-002`, `LOY-001`) | 336 KB | `PRO-001` 143 KB |
| C | 6 (`REP-001…003`, `ACC-001`, `NOT-001`, `NOT-002`) | 656 KB | `NOT-001` 133 KB |
| **Tổng** | **18** | **2.173 KB** | |

🔶 **Chưa đo token.** Ước lượng thô (tiếng Việt có dấu, giả định ~3 byte/token): một lượt đọc toàn văn WP-14 ≈ **0,7 triệu token**;
BA + DOCREV mỗi vai ít nhất một lượt, cộng đọc `D-xx` và code để ghi `file:dòng` ⇒ **≥ 2 triệu token** cho cả WP-14.
Kế hoạch `D-71` "3 phiên" (`N22…N24`) giả định mỗi phiên làm 3–9 tài liệu — trên gói Pro rất có thể **không** vừa một cửa sổ 5 giờ.

### 10.2. Luật vận hành trên Pro

1. **Pilot-D đo trước, chia sau:** `N22` bắt đầu bằng **2 tài liệu nhỏ đã có code** (`ADDR-001` 84 KB, `ADDR-002` 97 KB); đọc `/usage`
   (phân bổ theo subagent) sau mỗi tài liệu ⇒ suy ra số tài liệu/cửa sổ cho phần còn lại. `SAL-006` luôn **một phiên riêng**.
2. **Không mở phiên song song** để "làm nhanh hơn" — chúng dùng chung hạn mức.
3. **Cắt phiên khi hạn mức 5 giờ còn ~20%**, ở điểm xanh (thêm vào 5 dấu hiệu dừng của `08` §6 dưới dạng dấu hiệu thứ 6, ở B7).
4. Hạn mức dùng chung với chat/Cowork: tránh làm việc nặng ở chat trong cửa sổ đang chạy dây chuyền.
5. Nếu số đo Pilot-D cho thấy một đợt cần > 3 cửa sổ: đó là số liệu cho quyết định giữ Pro / dùng usage credits / nâng Max — **việc của
   người** (tiền, vùng cấm của DEC), dây chuyền chỉ báo số.

---

## 11. Lộ trình thi công

| Bước | Việc | DoD (lệnh kiểm) | Ước lượng |
|---|---|---|---|
| **B0** — tiền đề (người, ~30 phút) | ① `git config --global --add safe.directory D:/Projects/Tools/eCommerce` (người tự chạy) · ② xác nhận phiên bản Claude Code (`claude --version`), `/model` nhận đúng hai ID `claude-opus-5-5`, `claude-sonnet-5-5` (chọn model đã chốt ở §5.1), thanh hạn mức trong `/usage` · ③ tin thư mục (workspace trust) cho repo · ④ `node --version` ≥ 20, `pwsh --version` ≥ 7 · ⑤ biến `CLAUDE_CODE_EFFORT_LEVEL` **không** được đặt · ⑥ nhánh `chore/agents-scaffold` | `git status` chạy được; ghi 4 số đo vào handover | — |
| **B1** — hàng rào | `.claude/settings.json`, `.claude/scopes.json`, 4 hook `.mjs` + `test/` | `node --test .claude/hooks/test` 9/9 (H1–H9) · `/hooks` liệt kê đủ · ca thật REV bị chặn · `kiem-link-tai-lieu.ps1` PASS | 1 phiên |
| **B2** — vai tài liệu | `ba.md`, `docrev.md`; sửa `decider.md` (thêm `disallowedTools: Agent`, `maxTurns`); skill `session-protocol`, `doc-gate`, `feature-doc-rules`, `doc-review`; `check-feature-doc.ps1`, `check-agent-artifact.ps1` + ca trong `test-cong-kiem.ps1`; `CLAUDE.md` §9 | `/agents` liệt kê ba, docrev, decider · `test-cong-kiem.ps1` xanh, số ca tăng · cổng link + mermaid PASS · chạy `check-feature-doc.ps1` trên 1 tài liệu đã duyệt (`ADDR-004`) cho kết quả đúng | 1 phiên |
| **B3** — Pilot-D (= `N22`) | `/doc-gate ADDR-001 ADDR-002` ở mức L1 (người xem từng bước), đủ D1→D4 | DoD mỗi tài liệu theo `07` §WP-14 · bảng đo §12 có số · handover `N22` có bảng `/usage` | 1 phiên |
| **B4** — retro-D | Sửa vai/skill theo số đo; mục "Bài học bắt buộc" cho BA/DOCREV; **đề xuất chia lại** phần còn lại của WP-14 theo số tài liệu/cửa sổ đo được (DEC chốt tạm hoặc người — Q11) | `D-xx` ghi cách chia; `07` §WP-14 cập nhật bảng đợt | 0,3 phiên (cuối B3 hoặc đầu phiên sau) |
| *(WP-14 tiếp tục)* | Các phiên `/doc-gate` còn lại — **đây là việc của dự án, không phải việc dựng hệ** | DoD WP-14 toàn bộ | theo số đo B4 |
| **B5** — vai code | Sửa `MSB3491` (người — quyền `obj\`) · `arch.md`, `be.md`, `fe.md`, `qa.md`, `rev.md`, `ops.md` · skill `feature-pipeline`, `abp-module-rules`, `ef-migration-postgres`, `nextjs-admin-rules`, `testing-negative-control`, `code-review` · `negative-control.ps1` | `dotnet build eCommerce.slnx --no-incremental` 0 error · `/agents` đủ 9 · `test-cong-kiem.ps1` xanh · ca thật: REV/QA không ghi được `modules/**` | 1–2 phiên |
| **B6** — Pilot-C (= WP-15) | `/feature` trên feature code đầu tiên của GĐ3 (do `§WP-15+` của WP-14 ⑤ chọn), mức L1; nhận luôn 2 nợ của `D-71` ④ (`C10`, `409 code = null`) nếu nằm trong WP-15 | `-DayDu` 19/19 · PR xanh 2 required check · bảng §12 có số | 1–2 phiên |
| **B7** — retro-C | Bài học bắt buộc cho vai code · quyết L2 (`D-xx`) · bổ sung `08-session-protocol.md` (§ vai trong phiên, dấu hiệu dừng thứ 6) · nếu cần song song mới dựng `wp.ps1` | báo cáo pilot trong `docs/agents/` | 0,5 phiên |

**Đường găng:** B0 → B1 → B2 → B3. B5 chờ sửa `MSB3491` và chờ WP-14 có `§WP-15+`. Không bước nào cần merge vào `main`
trong lúc dựng ngoài PR thông thường (người merge).

---

## 12. Đo thành công

| Chỉ số | Cách đo | Ngưỡng | Pilot |
|---|---|---|---|
| Ghi ngoài vùng | số lần hook chặn + số tệp lệch trong log `check-agent-stop` | 0 tệp lọt qua cả hai lưới | D, C |
| Lỗi tài liệu bắt ở đúng tầng | P1 của DOCREV so với P1 người duyệt tìm thêm | người tìm thêm ≤ 30% | D |
| Câu hỏi không phải chờ người | `TIEN_LE`+`TAM_CHOT` / tổng câu gửi DEC | ≥ 70%; tỉ lệ người đảo quyết định tạm < 20% (đo ở lần duyệt lô) | D, C |
| Vòng làm lại | đếm theo `agent_type` trong log | ≤ 2 mỗi vai | D, C |
| Test có thật | mutation làm đỏ / mutation | 100% với BR quan trọng nhất | C |
| Ngân sách | `/usage` sau mỗi tài liệu / mỗi vai; % hạn mức 5 giờ và tuần | ghi số — dùng chia phiên (§10.2) | D, C |

---

## 13. Câu cần chốt

| # | Câu hỏi | Đề xuất | Ai chốt được |
|---|---|---|---|
| Q1 | Giữ SRS tại `docs/features/<module>/`, thêm `docs/reviews`, `docs/tests`, `docs/ops`, `docs/inbox`? | Có (`docs/reviews` đã dùng trong `D-71`) | DEC (tiền lệ `D-71`) |
| Q2 | `-arch.md` chỉ khi ≥ 2 module / đổi schema / đổi contract? | Có | DEC |
| Q3 | Pilot | **Đổi:** Pilot-D = `N22` (`ADDR-001`, `ADDR-002`); Pilot-C = WP-15 | người (đổi so với Q3 cũ) |
| Q4 | Model theo vai | Đã chọn ở §5.1 (17/11) | ✅ |
| Q5 | Đóng `D-67` (`C10`) trước Pilot-C? | Có — đã giao cho WP-15 theo `D-71` ④ | đã chốt |
| Q6 | `Workflow` cho L2? | Không trên Pro trước khi có số đo | DEC |
| Q7 | Danh sách đỏ đủ chưa? | Giữ §4.3 bản 14/11 | người |
| Q8 | `CLAUDE.md` commit chung? | Có (đã commit ở bước 1b) | đã chốt |
| **Q10** | Gói Pro có model nào? | ✅ **Đã trả lời 17/11**: Opus 5.5 + Sonnet 5.5 ⇒ model theo vai ở §5.1; ghi `D-xx` khi bắt đầu B1 (đổi được ⇒ tầng B) | đã chốt |
| **Q11** | Chia lại WP-14 theo số đo Pilot-D (nhiều hơn 3 phiên)? | Có — một phiên = số tài liệu vừa một cửa sổ 5 giờ | DEC chốt tạm (chia phiên đảo ngược được, không đổi phạm vi) |
| **Q12** | Hoãn `wp.ps1` + registry tới khi chạy song song thật? | Có | DEC |
| **Q13** | Hook bằng Node `.mjs`? | Có | DEC (tiền lệ: `tools/*.mjs`) |
| **Q14** | Mẫu artifact để trong thư mục skill thay vì `docs/templates/`? | Có | DEC |
| **Q15** | Agent không chạy `dotnet ef database update` (cấm trong `permissions`)? | Có — migration chạy qua cổng/`DbMigrator`, người chạy khi cần | người (đụng vùng "dữ liệu thật") |

Theo `CLAUDE.md` §5: các câu "DEC" đưa qua `decider` khi bắt đầu B1; chỉ Q3, Q7, Q15 còn hỏi người — **một lần**, trước B1.

---

## 14. Đối chiếu nguồn → đích (cập nhật bảng §15 bản 14/11)

| Nguồn (`SelfLearningAI`) | Đích (`eCommerce`) | Bước | Trạng thái |
|---|---|---|---|
| `agents/roles/00-orchestrator/ROLE.md` | `CLAUDE.md` + skill `doc-gate`, `feature-pipeline`, `session-protocol` | B2, B5 | `CLAUDE.md` ✅ (+§9) · `doc-gate`, `mo-phien`, `dong-phien` ✅ (17/11) · `feature-pipeline` ⬜ (B5) |
| `agents/roles/10-business-analyst/ROLE.md` | `.claude/agents/ba.md` + skill `feature-doc-rules` | B2 | ✅ 17/11 |
| `agents/roles/65-doc-reviewer/ROLE.md` | `.claude/agents/docrev.md` + skill `doc-review` (D1–D7) | B2 | ✅ 17/11 |
| `agents/roles/20…70/ROLE.md` (6 vai còn lại) | `.claude/agents/{arch,be,fe,qa,rev,ops}.md` + 5 skill | B5 | ⬜ |
| "Thẩm quyền tự quyết" ORCH §7 + `decide.ps1` | `.claude/agents/decider.md` + skill `decision-policy` | — | ✅ (14/11) |
| Kiểm vùng ghi sau khi chạy (`git status` + `git checkout --`) | hook `guard-write-scope` + `guard-shell` (chặn **trước**) + `subagent-start` / `check-agent-stop` (lưới sau) + `bao-orch` (báo phiên chính) | B1 | ✅ 17/11 |
| `scripts/check-artifact.ps1`, `check-docs.ps1` | `tools/agents/check-agent-artifact.ps1`, `check-feature-doc.ps1` | B2 | ✅ 17/11 |
| `scripts/negative-control.ps1` | `tools/agents/negative-control.ps1` | B5 | ⬜ |
| `scripts/check-all.ps1`, `verify.ps1` | **không port** — `tools/kiem-tat-ca.ps1` | — | có sẵn |
| `scripts/wp.ps1`, `session-new/merge.ps1`, `board.json`, `work-packages.json` | **hoãn** (C11); phiên tuần tự theo `08`, song song theo `09` | B7 nếu cần | hoãn |
| `scripts/decide.ps1` | **không port** (C12) | — | — |
| `SelfLearningAI/docs/00-templates/feature-design-template-v2.md` | **không port** — giữ `docs/templates/feature-design-template.md` | — | có sẵn |
| `docs/10-inbox/` | `docs/inbox/` | B2 | ✅ 17/11 |
| `docs/08-adr/` | **không port** — `D-xx` đóng vai ADR | — | — |
| "Bài học bắt buộc" cuối ROLE | mục 9 cố định của mỗi tệp vai; nguồn bài học eCommerce: tệp nhãn **B** (§14.1 bản 14/11) | B2, B5 | ✅ `ba`, `docrev` · ⬜ vai code |

---

## 15. Nguồn đã đối chiếu (17/11/2026)

- Claude Code — Subagents (frontmatter `tools`/`disallowedTools`/`model`/`skills`/`hooks`/`maxTurns`/`effort`/`isolation`; lồng tối đa 3 tầng):
  <https://code.claude.com/docs/en/sub-agents>
- Claude Code — Hooks (`agent_id`, `agent_type`; exit 2; `permissionDecision`; hook trong frontmatter; `SubagentStop` chặn được):
  <https://code.claude.com/docs/en/hooks>
- Claude Code — Skills (frontmatter `disable-model-invocation`, `context: fork`, `hooks`, `$ARGUMENTS`; `SKILL.md` < 500 dòng):
  <https://code.claude.com/docs/en/skills>
- Claude Code — Manage costs (`/usage` phân bổ theo subagent; subagent tính vào hạn mức; chọn model; "dynamic workflows"):
  <https://code.claude.com/docs/en/costs>
- Claude Help Center — Use Claude Code with your Pro, Max, or Ultra plan (hạn mức 5 giờ + tuần; dùng chung):
  <https://support.claude.com/en/articles/11145838-use-claude-code-with-your-pro-max-or-ultra-plan>
- Trong repo: [ke-hoach-he-thong-agent.md](ke-hoach-he-thong-agent.md) · [anh-xa-lo-trinh-d69.md](anh-xa-lo-trinh-d69.md) ·
  [hang-doi-xem-lai.md](hang-doi-xem-lai.md) · [07-ke-hoach-trien-khai.md](../nhanh/07-ke-hoach-trien-khai.md) §WP-14 ·
  [08-session-protocol.md](../nhanh/08-session-protocol.md) · [09-parallel-sessions.md](../nhanh/09-parallel-sessions.md) ·
  [decisions.md](../nhanh/decisions.md) (`D-69`…`D-71`)
- Nguồn DeepSeek: `D:\Projects\Tools\SelfLearningAI` — `agents/roles/*/ROLE.md`, `agents/state/*.json`, `scripts/*.ps1`,
  `SelfLearningAI/docs/09-process/` (kế hoạch tổng thể, đề xuất giải pháp, pilot, negative control, decisions).

---

## 16. Kết quả bước B1–B2 (17/11/2026)

> Dựng theo yêu cầu chủ dự án ("xây dựng theo kế hoạch"). **Chưa commit** — mọi tệp nằm trong cây làm việc; commit theo CLAUDE.md §8 khi chủ dự án yêu cầu.
> Bước B0 (người) và B3 (Pilot-D chạy trong Claude Code trên máy) **chưa làm** — xem §16.4.
> ⚠️ **Phần `.claude/**` chưa nằm trong `.claude/`**: công cụ từ xa của phiên dựng **không được phép ghi** vào thư mục `.claude` trên máy
> ⇒ 19 tệp `.claude/**` đặt tạm ở `artifacts\cai-dat-agent\claude\` (gitignore). Chủ dự án chạy **một lần**
> `pwsh -File artifacts\cai-dat-agent\cai-dat.ps1` (tạo mới 18, ghi đè 1 = `decider.md` có sao lưu, không xoá gì, rồi tự chạy ca thử hook).
> Đã chạy thử script trên repo giả: tạo 18 · ghi đè 1 · `decision-policy` giữ nguyên · hook 26/26 · chạy lại lần 2 ⇒ 19 "giữ nguyên".

### 16.1. Tệp đã tạo / sửa

| Nhóm | Tệp | Ghi chú |
|---|---|---|
| Hàng rào (B1) | `.claude/settings.json` · `.claude/scopes.json` | hook dạng exec `node` + `args`; `permissions.deny`: `git config --global`, `git push --force`, `gh pr merge`, `dotnet ef database update` |
| | `.claude/hooks/lib.mjs` · `guard-write-scope.mjs` · `guard-shell.mjs` · `subagent-start.mjs` · `check-agent-stop.mjs` · `bao-orch.mjs` · `session-context.mjs` | chỉ dùng module có sẵn của Node, không `npm install` |
| | `.claude/hooks/test/hooks.test.mjs` | 26 ca (H1–H9 + E1–E15) |
| Vai (B2) | `.claude/agents/ba.md` (Sonnet 5.5) · `.claude/agents/docrev.md` (Opus 5.5) · `.claude/agents/decider.md` (chỉ sửa frontmatter: `claude-opus-5-5`, `effort: high`, `disallowedTools: Agent`, `maxTurns: 20`) | |
| Skill (B2) | `feature-doc-rules` · `doc-review` (+ `templates/docreview.md`) · `doc-gate` · `mo-phien` · `dong-phien` | |
| Script (B2) | `tools/agents/check-feature-doc.ps1` · `tools/agents/check-agent-artifact.ps1` · nhóm ca **NHOM 4** trong `tools/test-cong-kiem.ps1` (12 ca) | |
| Tài liệu | `CLAUDE.md` §9 · `docs/reviews/README.md` · `docs/inbox/README.md` · `docs/nhanh/decisions.md` (`D-72`…`D-76`, **ĐỀ XUẤT**) · `docs/agents/hang-doi-xem-lai.md` (5 dòng chờ) | |

### 16.2. Số đo (chạy trong môi trường dựng, Linux, Node 22, PowerShell 7.4)

| Lệnh | Kết quả |
|---|---|
| `node --test .claude/hooks/test/hooks.test.mjs` | **26/26 pass** |
| NHOM 4 của `tools/test-cong-kiem.ps1` (chạy riêng với khung `Describe`/`It` của tệp) | **12/12 đạt** |
| Negative control cho chính cổng: sửa `check-feature-doc.ps1` cho luôn `exit 0` | NHOM 4 **8/12** (4 ca đỏ đúng) → khôi phục **12/12** |
| Negative control: tắt luật "DOC-OK mà còn P1" của `check-agent-artifact.ps1` | ca B2 **đỏ** → khôi phục **12/12** |
| `[Parser]::ParseFile` cho `tools/test-cong-kiem.ps1` (bản đã chèn NHOM 4) + 2 script mới | **0 lỗi cú pháp** |
| `check-feature-doc.ps1 -WP14` trên 18 tài liệu WP-14 thật | 18/18 đủ 16 heading · **13/18 có BR chưa có test ở §14** (37 BR) · 18/18 chưa có mục "Đối chiếu mô hình Nhanh" (đúng — WP-14 chưa chạy) |
| `kiem-link-tai-lieu.ps1` (bản thật của repo) trên cây mô phỏng: mọi tệp mới/sửa + tệp đích có thật trên máy | **PASS — 0 tham chiếu chết** (33 tệp `.md`, 22 link, 55 đường dẫn backtick) |
| Khối Mermaid trong tệp mới | 0 (không cần `check:mermaid`) |
| Độ dài (luật P1: vai ≤ 120 dòng, skill ≤ 300) | vai 75–103 dòng · skill 42–100 dòng |

🔶 **Chưa đo trên máy Windows của chủ dự án**: toàn bộ `tools/test-cong-kiem.ps1` (cần Docker/DB cho nhóm 3), hook chạy thật trong Claude Code, `/hooks` liệt kê đúng.

### 16.3. Chỗ lệch so với kế hoạch (đã ghi `D-xx`)

| # | Kế hoạch ghi | Đã làm | Vì sao | D-xx |
|---|---|---|---|---|
| 1 | "mọi `BR-` có `*Test:*`" (§8, theo nguồn) | Độ phủ BR đo theo cột "Phủ" của bảng §14, nhận cả `BR-<MÃ>-NNN` và `**BR-NN**` | Mẫu 16 mục của eCommerce không có dòng `*Test:*`; đo thật trên 18 tài liệu | `D-74` |
| 2 | Một skill `session-protocol` cho `/mo-phien`, `/dong-phien` | Hai skill `mo-phien`, `dong-phien` | Một skill tạo đúng một lệnh `/` | `D-74` |
| 3 | 4 hook | 6 hook + thư viện chung | `SubagentStart` chụp trạng thái để lưới thứ hai chỉ tính tệp **chính vai đó** đổi; `bao-orch` đưa cảnh báo vào phiên chính (`SubagentStop` không nói được với phiên chính) | `D-73` |
| 4 | `decider` shell "chỉ đọc" | `read+psql-rollback+test` | Thân `decider.md` cho phép `psql` SELECT và chạy test để đo | `D-73` |
| 5 | BE sửa mục "Trạng thái triển khai" | Nhận cả "Tiến độ triển khai" (không phân biệt hoa thường) | Tài liệu thật dùng `## 16. 💻 Tiến độ triển khai` (`ADDR-004`) và `TRẠNG THÁI TRIỂN KHAI` | `D-73` |
| 6 | `/doc-gate` gửi decider mọi câu hỏi mở | Chỉ câu **chặn DoD** WP-14; 2 tài liệu/phiên | Hạn mức Pro; 159 câu `Open` | `D-76` |
| 7 | ORCH ghi "nháp dòng biên bản duyệt" | Nháp ở `docs/agents/nhap-bien-ban-gd3-<đợt>.md`, biên bản thật do chủ dự án lập | Tránh agent khác coi nháp là đã duyệt | `D-74` |

### 16.4. Việc tiếp theo

1. **B0 — chủ dự án (~30 phút):** `pwsh -File artifacts\cai-dat-agent\cai-dat.ps1` (chép `.claude/**`, xem đầu §16) · `git config --global --add safe.directory D:/Projects/Tools/eCommerce` · mở Claude Code tại repo, chấp nhận
   **workspace trust** (hook dự án chỉ chạy sau bước này) · `node --version` ≥ 20 · `/hooks` thấy 6 nhóm hook · `/agents` thấy `ba`, `docrev`, `decider` ·
   chạy `node --test .claude/hooks/test/hooks.test.mjs` và `pwsh -File tools\test-cong-kiem.ps1 -ChiKiemMoc` · **ca thật**: bảo phiên gọi
   `docrev` sửa một tệp `modules/**` ⇒ phải bị chặn, `git status` sạch.
2. Trả lời `D-75` (= Q15) và Q3/Q7 của §13; duyệt lô `D-72`…`D-76` trong `docs/agents/hang-doi-xem-lai.md`.
3. **B3 — Pilot-D (= phiên `N22`):** `/mo-phien WP-14 N22` → `/doc-gate ADDR-001 ADDR-002` → `/dong-phien`; ghi `/usage` sau mỗi tài liệu.

### 16.5. Kiểm tra sau Pilot-D (phiên `N22`, 18/11/2026)

Kiểm lại bằng chạy script trên tệp thật của máy (không dùng số vai tự báo):

| Hạng mục | Kết quả |
|---|---|
| Phạm vi đã làm | **2/18** tài liệu WP-14 (`ADDR-001`, `ADDR-002`) = 2/9 đợt A — đúng như `handover-n22.md` ghi "DỞ DANG WP-14"; 16 tài liệu còn lại **chưa** đổi |
| `check-feature-doc.ps1 -WP14` | `ADDR-001` DAT (16/16 mục · 14/14 BR có test · đối chiếu Nhanh dòng 1028 · 12 câu `Open`) · `ADDR-002` DAT (16/16 · 16/16 · dòng 941 · 8 `Open`) |
| `check-agent-artifact.ps1 -Kind docreview` | cả hai `DOC-OK` · P1 = 0 · P2 = 2 |
| `check-agent-artifact.ps1 -Kind decision` | 16 dòng `TẠM CHỐT (DEC)` (`D-77`…`D-92`) đều có cách lùi + hạn xem lại · `D-93`, `D-94` chủ dự án chốt |
| Hàng đợi xem lại | 21 dòng chờ; 3 dòng chặn release (`D-77`, `D-78`, `D-92`) |
| Hàng rào | 34 lượt vai · **0** lần ghi ngoài vùng · nhưng 31/34 báo "thiếu JSON" **nhầm** ⇒ mọi lượt bị chặn dừng 1 lần |
| Kích thước tài liệu | `ADDR-001` 84 → 172 KB (+105%) · `ADDR-002` 97 → 178 KB (+84%) |
| Nhịp | 2 tài liệu ≈ 34 lượt vai (17 lượt `decider`), chạm hạn mức 5 giờ 1 lần |

**Đã sửa (ghi tạm `D-95` lúc 19:00 — dòng đó và mục này mất trong sự cố commit `5aa3dce` của `N23`; ghi lại ở `D-102`, §16.6)** — hook đọc JSON từ transcript của vai, phân biệt JSON thiếu / không hợp lệ · `docrev` chạy được psql heredoc
`BEGIN…ROLLBACK` và các dạng `npm … check:mermaid` · `maxTurns` `ba` 60, `decider` 30 · `/doc-gate`: sau `DOC-OK` không sửa thêm ·
bài học #7 của `ba` (sửa tại chỗ). `hooks.test.mjs` **30/30** (thêm 4 ca `N22-*`; negative control: tắt đọc transcript ⇒ `N22-1` đỏ).
Cài bằng `pwsh -File artifacts/cai-dat-agent/cai-dat.ps1` (kèm bản sửa dấu `\` của `mo-phien`/`dong-phien` chưa cài từ 17/11).

**Đề xuất cho B4 (chia lại WP-14):** với nhịp đo được, 16 tài liệu còn lại ≈ 8 phiên nếu giữ 2 tài liệu/phiên. Đo lại ở `N23` sau khi hết
chặn nhầm (mỗi lượt vai bớt một lần chặn) rồi mới chốt số tài liệu/phiên.

### 16.6. Kiểm tra sau `N23` và vòng sửa thứ hai (18/11/2026 tối)

| Hạng mục | Kết quả |
|---|---|
| Phạm vi `N23` | `ADDR-003` tới D2 vòng 1 (`DOC-CHANGES`), còn áp `D-98…D-101` + DOCREV vòng 2 — đúng `handover-n23.md` §2. Đợt A: 2/9 xong, 1 dở |
| `decisions.md` | 100 dòng `D-xx`, **không trùng số**, đủ `D-77…D-94` (đã khôi phục sau sự cố `5aa3dce`); 23 dòng DEC đều có cách lùi + hạn xem lại (`check-agent-artifact.ps1 -Kind decision` DAT) |
| Hàng rào | 12 lượt vai · 0 ghi ngoài vùng · **12/12 vẫn báo "thiếu JSON" nhầm**: nhật ký mới cho thấy `last_assistant_message` **rỗng** (`len: 0`) cả 12 lượt và transcript không đọc được |
| Trần lượt | `ba` chạm 60 hai lần, `docrev` 40, `decider` 30; hạn mức 5 giờ chạm giữa `ba` |
| Song song | hai `decider` tranh cùng số `D-99`, `D-100` |

**Vòng sửa thứ hai (`D-102`, ĐỀ XUẤT)** — hook chỉ chặn dừng khi **thấy** nội dung vai mà không có JSON; không đọc được ⇒ ghi
`khongXacMinh` + tên các trường input (lần chạy sau sẽ cho biết trường nào mang khối JSON); mở rộng `~` trong `agent_transcript_path` ·
sửa lỗi hook ném ngoại lệ khi vừa không có JSON vừa không có nội dung (ca `N23-1` bắt được trong lúc sửa) · `maxTurns` `ba` 80, `docrev` 60,
`decider` 40 · `/doc-gate`: `decider` **tuần tự**; tài liệu > 120 KB hoặc ≥ 5 câu gửi decider ⇒ **một tài liệu/phiên**.
`hooks.test.mjs` **33/33** (ca `N23-1…3`; negative control: bỏ luật không-chặn ⇒ `N23-1` đỏ; bỏ mở rộng `~` ⇒ `N23-2` đỏ).

### 16.7. Bảng phiên chạy WP-14 + `/chay-phien` (18/11/2026 khuya)

Theo yêu cầu "mở phiên, dán prompt, không can thiệp" — khuôn §14 *Bảng phiên chạy* của `SelfLearningAI/docs/09-process/ke-hoach-xay-dung-cms.md`.

| Thành phần | Vai trò |
|---|---|
| `agents/state/phien-wp14.json` | Registry — 23 hàng (2 phiên uỷ quyền `U-01`/`U-02`, 14 `doc-gate`, 3 chuẩn bị hồ sơ duyệt, 3 duyệt của người, 1 đóng WP, 1 PR của người); nguồn sự thật |
| [bang-phien-wp14.md](bang-phien-wp14.md) | Bảng để người xem + dán prompt; phần giữa marker do script sinh, **không sửa tay** |
| `tools/agents/phien.ps1` | `status` · `next` · `start` · `done` (bắt buộc bằng chứng) · `dodang` · `blocked` · `release` · `sync` · `check` · `doisoat` (đối soát phiên chạy kiểu cũ) · `uyquyen` (chuyển hàng duyệt cho decider sau `U-02`) |
| `tools/agents/test-phien.ps1` | 37/37 ca (negative control: sửa tay bảng ⇒ `check` exit 1; sắp handover theo **số**, không theo chữ: `n10` > `n9`) |
| `.claude/skills/chay-phien/SKILL.md` | Đối soát → chọn hàng → mở phiên → `/doc-gate` → đóng phiên → cập nhật bảng → commit + push nhánh WP; không hỏi giữa chừng |
| [uy-quyen-chu-du-an.md](uy-quyen-chu-du-an.md) | Hồ sơ uỷ quyền (K1) — CHƯA HIỆU LỰC tới `U-02`; decider đọc trước mọi thứ (T0), `decision-policy` §3a, `decider.md` §4a |

Đối soát trên dữ liệu thật (`handover-n24.md`): `ADDR-001…002` ⇒ đề xuất `done`; `ADDR-003` ⇒ làm tiếp (§2 còn áp `D-105…D-107`).
Quyết định: dòng `D-xx` ĐỀ XUẤT do lần `/chay-phien` đầu tiên chèn (văn bản ở `artifacts/cai-dat-agent/D-bang-phien.md`).
Giới hạn: hạn mức 5 giờ vẫn có thể cắt ngang phiên — hàng ở lại 🟨, lần `/chay-phien` sau làm tiếp từ cây làm việc; hàng 👤 luôn là việc của người.
