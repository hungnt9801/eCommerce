# `docs/reviews/` — báo cáo kiểm của vai agent

| Tệp | Vai ghi | Mẫu | Kiểm bằng máy |
|---|---|---|---|
| `<MÃ>-docreview.md` | `docrev` — kiểm tài liệu D1–D7, verdict `DOC-OK` / `DOC-CHANGES` / `DOC-BLOCKED` | `.claude/skills/doc-review/templates/docreview.md` | `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path <tệp>` |
| `<MÃ>-review.md` | `rev` — review code, verdict `PASS` / `CHANGES` (dựng ở bước B5) | (B5) | `... -Kind review -Path <tệp>` |

- Mỗi vai **chỉ** ghi đúng loại tệp của mình — vùng ghi ở `.claude/scopes.json`, hook chặn trước khi ghi.
- Báo cáo **không** phải biên bản duyệt: duyệt tài liệu ghi ở biên bản duyệt do chủ dự án lập (CLAUDE.md §6).
- Kế hoạch: [ke-hoach-v2-claude-code-pro.md](../agents/ke-hoach-v2-claude-code-pro.md) §6, §9.1.
