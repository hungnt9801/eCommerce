---
name: feature-pipeline
description: Dây chuyền code cho orchestrator (phiên chính) — với một mã feature đã có SRS (hoặc WP hạ tầng không có SRS), chạy F1→F6 qua các vai arch/be/fe/qa/rev/ops, gác bằng cổng máy G1–G6, trần 2 vòng mỗi vai, dừng ở điểm xanh, không merge. Gọi tay — /feature <MÃ> (ví dụ /feature PRC-002). Bước B5 của docs/agents/ke-hoach-v2-claude-code-pro.md.
argument-hint: "<MÃ> [ghi chú ngắn]"
disable-model-invocation: true
hooks:
  PreToolUse:
    - matcher: "Edit|Write|MultiEdit|NotebookEdit"
      hooks:
        - type: command
          command: node
          args: ["${CLAUDE_PROJECT_DIR}/.claude/hooks/guard-write-scope.mjs", "--orch"]
          timeout: 15
    - matcher: "mcp__gitnexus__.*|mcp__codegraph__.*"
      hooks:
        - type: command
          command: node
          args: ["${CLAUDE_PROJECT_DIR}/.claude/hooks/guard-orch-mcp.mjs"]
          timeout: 15
---

# /feature — dây chuyền code (ORCH)

Trạng thái đích: `docs/agents/ke-hoach-he-thong-agent.md` §6.1–§6.2 · điều chỉnh cho gói Pro:
`docs/agents/ke-hoach-v2-claude-code-pro.md` §9.2. Skill này **dẫn**, không chép lại; mâu thuẫn ⇒ hai tệp nguồn thắng.

## 1. Điều kiện vào (thiếu ⇒ **dừng**, không tự mở)

| # | Điều kiện | Kiểm bằng |
|---|---|---|
| V1 | Feature có `docs/features/<module>/<MÃ>-*.md` **có trong biên bản duyệt** (hoặc WP hạ tầng không có SRS như WP-15, ghi rõ lý do) | `docs/features/README.md` §2 · biên bản `docs/bien-ban-duyet-*` |
| V2 | Đã tạo nhánh của WP; `git status --short` sạch hoặc chỉ `docs/**` | `git status --short` |
| V3 | Build/test đang xanh (baseline có số) | `dotnet build eCommerce.slnx --no-incremental` · `dotnet test test/eCommerce.Tests --no-build` |
| V4 | Nếu sửa schema: **chưa luồng nào khác chạy `migrations add`** (`09-parallel-sessions.md` §6) | bảng luồng / hỏi người |
| V5 | Cổng tài liệu của WP đã đạt (`07` §WP-15+ cột "Cổng tài liệu") | `07` |

Không đạt V1 với feature có SRS ⇒ **không code** (`implementation-plan.md` §A.5, `D-71`). Câu mới ⇒ `decider`.

## 2. Dây chuyền (tuần tự trên Pro)

```text
F0 chuẩn bị → (F1 BA chỉ khi code buộc lệch) → F1b DOCREV → G1 người
  → F2 ARCH (chỉ khi ≥ 2 module / đổi schema / đổi contract) → F2b DOCREV → G2 người
  → F3 BE → FE (tuần tự; ∥ chỉ khi API đã merge + có bảng cấp phát DB/cổng)
  → G3 máy → F4 QA → G4 máy → F5 REV → G5 → F6 OPS → G6 máy → người merge
```

| Bước | Vai | Việc | Artifact |
|---|---|---|---|
| F0 | ORCH | tạo nhánh, đọc SRS theo mục, in danh sách người review theo loại feature (K-08) | — |
| F1 | `ba` | **chỉ** khi code buộc lệch SRS (`docDeviation`) hoặc SRS thiếu phần cần dùng | tài liệu feature sửa tại chỗ |
| F1b | `docrev` | kiểm nhanh phần BA vừa sửa | `docs/reviews/<MÃ>-docreview.md` |
| F2 | `arch` | **chỉ** khi chạm ≥ 2 module / đổi schema / đổi contract | `docs/features/**/<MÃ>-arch.md` |
| F3 | `be` rồi `fe` | code + test đơn vị; BE cập nhật **mục "Trạng thái triển khai"** ở cuối SRS | code + migration (expand-only) |
| F4 | `qa` | phủ mọi `BR-` bằng test; negative control có bằng chứng đỏ→xanh | `docs/tests/<MÃ>-testplan.md` |
| F5 | `rev` | review **ngữ cảnh sạch** trên `git diff` (≤ ~600 dòng trừ migration) | `docs/reviews/<MÃ>-review.md` |
| F6 | `ops` | CI/compose/deploy doc; chạy cổng đầy đủ | `docs/ops/<MÃ>-deploy.md` |

## 3. Cổng máy (ORCH tự chạy — **không** tin số vai báo)

| Cổng | Tiêu chí (exit code + số đo) |
|---|---|
| G1/G2 | Người duyệt: DOCREV `DOC-OK`, 0 P1, 0 câu hỏi mở chặn; ghi vào biên bản — **ORCH chỉ nháp**, không ghi "đã duyệt" |
| G3 | `dotnet build eCommerce.slnx --no-incremental`: 0 error, **0 warning mới** · `dotnet test test/eCommerce.Tests --no-build`: số ca **không giảm** · FE: `pnpm lint` · `pnpm typecheck` · `pnpm test` · `pnpm build` · `pnpm gen:api` không đổi `schema.d.ts` · `pwsh -File tools/kiem-tat-ca.ps1` exit 0 · `git diff --stat` ≤ ~600 dòng trừ migration |
| G4 | `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path <testplan>` exit 0: mọi `BR-` có mặt + có bảng negative control đỏ→xanh |
| G5 | `check-agent-artifact.ps1 -Kind review` exit 0: `VERDICT: PASS`, `P1 = 0` |
| G6 | `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` exit 0; PR xanh 2 required check; `docs/ops/<MÃ>-deploy.md` có đường lùi |
| Merge | **Người** — TP **cấm** merge `main` |

## 4. Trần và dấu hiệu dừng (K-01, K-02)

- **2 vòng** cho mỗi vai (làm lại cùng một lỗi lần 3 ⇒ dừng).
- Dừng phiên ngay khi: 3 vòng liên tiếp cùng lỗi · context > ~70% · sửa > 5 tệp ngoài phạm vi WP ·
  test đỏ do thay đổi ngoài dự kiến (**không** sửa test cho xanh) · migration chạm DB có dữ liệu mà chưa `pg_dump`.
- Chỉ cắt phiên ở **điểm xanh**; không cắt giữa migration/backfill. Ghi handover dù WP chưa xong.

## 5. Luật điều phối

1. **Một bước, một vai.** ORCH không tự sửa code thay vai khi pipeline đang chạy (hook khoá vùng ghi).
2. Sau **mỗi** vai: `git status --porcelain`, so `scopes.json`; tệp ngoài vùng ⇒ `git restore` đúng tệp + gọi lại kèm nhắc phạm vi.
3. Mọi câu hỏi mở của vai ⇒ **`decider`** trước; `CHUYEN_NGUOI` ⇒ làm `continueWhileWaiting`, gom hỏi người **một lần** cuối phiên.
4. BE/FE **không** tự sửa hợp đồng: trả `docDeviation` ⇒ ORCH gọi `ba` sửa SRS **trên cùng nhánh** ⇒ `docrev` kiểm nhanh ⇒ làm tiếp.
5. Danh sách đỏ §4.3 (`ke-hoach-he-thong-agent.md`): tiền/thuế/HĐĐT/bí mật/PII/migration phá huỷ ⇒ **L1**, mọi cổng có người; nâng BE lên Opus cho lượt chạm lõi tính tiền/sổ cái tồn kho/máy trạng thái.
6. Không đổi `eCommerce.slnx`, `Directory.Packages.props`, `pnpm-lock.yaml` khi có luồng khác; `migrations add` chỉ một chủ.

## 6. Mẫu tệp phụ

- `templates/deploy.md` — khung `docs/ops/<MÃ>-deploy.md` (biến môi trường, migration, rollback, cổng).
- `templates/blocked.md` — khung `docs/inbox/<MÃ>-blocked.md` khi bị chặn.

## 7. Lệnh kiểm nhanh (dán vào handover kèm số)

```powershell
dotnet build eCommerce.slnx --no-incremental
dotnet test test/eCommerce.Tests --no-build
pwsh -File tools/kiem-tat-ca.ps1
pwsh -File tools/kiem-tat-ca.ps1 -DayDu      # trước push
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/<MÃ>-testplan.md
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind review   -Path docs/reviews/<MÃ>-review.md
```
