# Khung `docs/tests/<MÃ>-testplan.md` (QA — bước F4)

> Chép khung này, thay `<MÃ>`. Phải có **mọi** `BR-` của SRS và mục **negative control** với **≥ 2 dòng `exit N`**
> (đột biến ⇒ đỏ; khôi phục ⇒ xanh) để `check-agent-artifact.ps1 -Kind testplan -Srs <tệp>` đạt.

```markdown
# Testplan `<MÃ>` — <ngày>

- SRS: `docs/features/<module>/<MÃ>-<slug>.md`
- Nhánh: `<nhánh>`

## 1. Phủ BR
| BR | Test (tệp · tên) | Tầng | Trạng thái |
|---|---|---|---|
| BR-01 | `test/.../XTests.cs` · `TenTest` | Unit | ✅ |
| BR-02 | … | App | ✅ |

- Tổng BR: `<n>` · đã phủ: `<n>` · chưa phủ: `0`

## 2. Số ca theo tầng (K-06)
| Tầng | Trước | Sau | Ghi chú |
|---|---|---|---|
| Domain invariant | … | … | … |
| Application | … | … | … |
| API 200/401/403 | … | … | … |
| UI Vitest | … | … | … |

## 3. Negative control (bắt buộc)
| # | Đột biến | Tệp:dòng | Test | Khi đột biến | Sau khôi phục |
|---|---|---|---|---|---|
| 1 | … | `file:dòng` | `<tên>` | exit 1 (đỏ) | exit 0 (xanh) |
| 2 | … | `file:dòng` | `<tên>` | exit 1 (đỏ) | exit 0 (xanh) |

## 4. Cổng
```powershell
dotnet build eCommerce.slnx --no-incremental     # exit 0
dotnet test test/eCommerce.Tests --no-build      # exit 0 · <n> ca
pwsh -File tools/kiem-tat-ca.ps1                 # exit 0
```

## 5. Chưa phủ / nợ
- …
```
