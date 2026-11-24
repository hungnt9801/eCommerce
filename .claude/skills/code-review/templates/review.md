# Khung `docs/reviews/<MÃ>-review.md` (REV — bước F5)

> Chép khung này, thay `<MÃ>`/`<ngày>`/`<sha>`. Giữ đúng dòng `VERDICT:` và cột **Mức** (P1/P2/P3) để
> `check-agent-artifact.ps1 -Kind review` đọc được. Không sửa code trong review.

```markdown
# Review code `<MÃ>` — <ngày>

- Nhánh: `<nhánh>` · base `main` · head `<sha>`
- Diff: `<n>` dòng (trừ migration `<m>` dòng)
- SRS: `docs/features/<module>/<MÃ>-<slug>.md` (mục đã đọc: …)

## Đã kiểm
| Nhóm | Kết quả |
|---|---|
| A. Hợp đồng & đúng đắn | … |
| B. Tiền, tồn kho, dữ liệu | … |
| C. Bảo mật & multi-tenant | … |
| D. Test & vận hành | … |

## Phát hiện
| # | Mức | Vị trí | Phát hiện | Bằng chứng | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | P1/P2/P3 | `file:dòng` | … | … | … |

## Kết luận
VERDICT: PASS | CHANGES
```

> `PASS` chỉ khi **P1 = 0**. Có P1 ⇒ `CHANGES`; ORCH đưa BE/QA sửa rồi gọi REV vòng 2 (trần 2 vòng).
