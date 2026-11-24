---
name: code-review
description: Checklist review code của eCommerce cho vai rev — hiểu đúng hợp đồng SRS, tiền/tồn kho/máy trạng thái, ranh giới module ABP, bảo mật + multi-tenant, idempotency webhook/IPN, test có negative control, migration expand-only, phạm vi PR; phân loại P1/P2/P3, verdict PASS/CHANGES, không sửa code. Nạp sẵn cho rev; dùng khi review một diff trước khi merge.
user-invocable: false
---

# Review code — eCommerce

Nguồn: `docs/coding-conventions.md` · `docs/agents/ke-hoach-he-thong-agent.md` §4.2 (REV) + §14.2 · `docs/features/<module>/<MÃ>-*.md` (hợp đồng).
Đầu ra **chỉ** `docs/reviews/<MÃ>-review.md`. Không sửa code, không ghi tệp khác.

## 1. Trước khi đọc diff

- Đọc SRS của `<MÃ>` theo mục (BR, §5–§8, §11 schema, §14 test) + mục "Trạng thái triển khai".
- `git diff main...HEAD --stat` và `git diff main...HEAD` — diff ≤ ~600 dòng trừ migration; lớn hơn ⇒ P2 (phạm vi) hoặc dừng.
- Xác định danh sách đỏ (§4.3): tiền/thuế/HĐĐT/bí mật/PII/migration phá huỷ ⇒ soi kỹ hơn.

## 2. Checklist

### A. Hợp đồng & đúng đắn
| # | Kiểm |
|---|---|
| A1 | Hành vi khớp **từng** BR của SRS; sai lệch phải có `docDeviation` + SRS đã sửa **trong cùng PR** (K-09). |
| A2 | Mã lỗi đúng dải/nhóm (`eCommerce:<Miền>:<4 số>`), không mã mới ngoài tài liệu; thông điệp tiếng Việt nêu hệ quả. |
| A3 | Máy trạng thái **ném lỗi** ở nhánh sai; enum DB chỉ thêm ở cuối. |
| A4 | Idempotency ở mọi biên vào; khoá phân tán tên đúng quy ước. |
| A5 | Ranh giới module: không `module → app`, không FK/navigation/transaction xuyên module, giao tiếp qua Contracts/ETO. |

### B. Tiền, tồn kho, dữ liệu
| # | Kiểm |
|---|---|
| B1 | Tiền `decimal` qua `Money`; **không** `double`/`float`; **không** so `==`; làm tròn đúng `XC-001`. |
| B2 | Số lượng `decimal(18,3)`; tiền `decimal(18,4)`; thời gian UTC. |
| B3 | Tồn kho append-only, một cửa ghi; không `UPDATE` số lượng. |
| B4 | Unique index tenant có `NULLS NOT DISTINCT`; test bất biến liệt kê **tên** index. |
| B5 | Handler `IDistributedEventBus` ghi dữ liệu mở `Begin(requiresNew: true, isTransactional: true)`. |
| B6 | Migration expand-only; không sửa migration đã merge; có đường lùi (K-05). |

### C. Bảo mật & multi-tenant
| # | Kiểm |
|---|---|
| C1 | `[Authorize]` cấp class; permission là hằng số **đã định nghĩa**; không `AddAlwaysAllowAuthorization`. |
| C2 | Không đổi `MapInboundClaims`/`NameClaimType`; `[AllowAnonymous]` chỉ trong allowlist có lý do. |
| C3 | Tenant filter không bị `IgnoreQueryFilters()` không lý do; dữ liệu trả về không lộ tenant khác. |
| C4 | Token sàn/VNPay/GHN/GHTK **không** lộ qua DTO/log/URL; secret không hard-code. |
| C5 | Webhook/IPN có chống trùng + xác thực chữ ký theo tài liệu; fail-closed khi thiếu. |

### D. Test & vận hành
| # | Kiểm |
|---|---|
| D1 | Có test **tái hiện** cho bug/luật mới; số ca không giảm; có negative control đỏ→xanh. |
| D2 | Mỗi BR có test; endpoint mới có ca 200 + 401/403; app service ≥ 3 ca. |
| D3 | Cổng xanh: build `--no-incremental` 0 warning mới · test · `kiem-tat-ca.ps1` · FE lint/typecheck/test/build · `gen:api` không diff. |
| D4 | Không sửa tệp ngoài phạm vi (`eCommerce.slnx`, `Directory.Packages.props`, `pnpm-lock.yaml` trừ khi có lý do + chủ tệp). |
| D5 | Tài liệu cập nhật: mục "Trạng thái triển khai" + link/mermaid PASS; `docs/ops` có rollback nếu chạm hạ tầng. |

## 3. Phân loại

| Mức | Nghĩa | Ví dụ |
|---|---|---|
| **P1** | Sai tiền/tồn/quyền/bảo mật, mất dữ liệu, phá hợp đồng, sai pháp lý; **chặn merge** | so tiền `==`; thiếu `[Authorize]`; index tenant thiếu `NULLS NOT DISTINCT`; token trong log |
| **P2** | Đúng nhưng dễ sinh lỗi, thiếu test quan trọng, phạm vi rộng, hiệu năng rõ rệt | thiếu ca 401; migration backfill dài; N+1 ở danh sách |
| **P3** | Đặt tên, trùng lặp nhỏ, chú thích, format | biến không dùng; comment lỗi thời |

`VERDICT: CHANGES` nếu có **bất kỳ P1**; `PASS` chỉ khi **P1 = 0**. P2/P3 ghi kèm khuyến nghị, không tự sửa.

## 4. Mẫu tệp phụ

`templates/review.md` — khung `docs/reviews/<MÃ>-review.md` (bảng phát hiện có `file:dòng`, mức, bằng chứng).

## 5. Lệnh kiểm (chạy được, chỉ đọc)

```powershell
git diff main...HEAD --stat
git diff main...HEAD
dotnet build eCommerce.slnx --no-incremental
dotnet test test/eCommerce.Tests --no-build
pwsh -File tools/kiem-tat-ca.ps1
```
