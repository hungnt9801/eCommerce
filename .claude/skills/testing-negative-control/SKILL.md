---
name: testing-negative-control
description: Luật kiểm thử của eCommerce cho vai qa — số ca tối thiểu theo tầng (domain invariant, app service, endpoint 200/401/403, UI, OpenAPI drift), mỗi BR có test, negative control bắt buộc (đột biến làm test ĐỎ rồi khôi phục XANH, có bằng chứng), probe HTTP thật và Playwright, cấm stub "allow all"/nới test để xanh. Nạp sẵn cho qa; dùng khi viết testplan, test hoặc chạy cổng test.
user-invocable: false
---

# Kiểm thử & negative control — eCommerce

Nguồn: `docs/nhanh/07-ke-hoach-trien-khai.md` §9 · `docs/coding-conventions.md` §11 · `docs/agents/ke-hoach-he-thong-agent.md`
§14.2 K-06 · `tools/README-probe.md` (danh mục kiểm probe). Skill **dẫn**; mâu thuẫn ⇒ tệp nguồn thắng.

## 1. Số ca tối thiểu theo tầng (K-06)

| Tầng | Bắt buộc |
|---|---|
| Domain invariant | xUnit cho **mọi** luật tồn/tiền/trạng thái mới (không có ngoại lệ) |
| Application | mỗi app service mới **≥ 3** ca: happy · dữ liệu sai · **sai quyền** |
| API `/api/v1` | mỗi endpoint **≥ 1** ca `200` **và ≥ 1** ca `401/403` (đo trên endpoint **gắn quyền**) |
| UI lõi | Vitest + RTL: `MoneyInput`, `DataTable`, `OrderStatusBadge` |
| E2E | Playwright 6–8 luồng (đăng nhập · tạo SP · nhập kho · bán lẻ · đổi trạng thái đơn · xuất báo cáo) |
| Tài liệu | mỗi PR sửa `.md`: `kiem-link-tai-lieu.ps1` + `check:mermaid` |
| OpenAPI | mỗi PR sửa DTO/controller: `pnpm gen:api` không diff |

Mỗi `BR-` trong SRS phải có **ít nhất một** test; ghi mã BR vào tên test hoặc comment. Testplan phải phủ **đủ** BR.

## 2. Negative control (bắt buộc — chống test rỗng)

Với **mỗi** nhóm luật quan trọng (tiền/tồn/trạng thái/quyền/idempotency):

1. Chọn một đột biến **thật** trong mã (đảo điều kiện, bỏ guard, đổi dấu so sánh, bỏ `NULLS NOT DISTINCT`…).
2. Chạy test ⇒ **PHẢI ĐỎ**. Không đỏ ⇒ test vô giá trị, sửa test trước.
3. Khôi phục mã ⇒ chạy lại ⇒ **PHẢI XANH**; `git diff` xác nhận đã khôi phục đúng (**B4** của `README-probe.md`).
4. Ghi bảng bằng chứng vào testplan: đột biến · tệp:dòng · output đỏ · output xanh.

Công cụ: `tools/agents/negative-control.ps1` (từ chối khi tệp đích có thay đổi chưa commit; áp mutation → chạy test → khôi phục → chạy lại; exit `0/2/3`).
Không có bằng chứng đỏ→xanh ⇒ G4 **đỏ**.

## 3. Cấm

- ❌ Xoá/skip test, nới timeout, hạ ngưỡng, tắt cổng để xanh.
- ❌ Stub `AddAlwaysAllowAuthorization`/"allow all" trong fixture; dùng fixture ABP thật trên PostgreSQL (`eCommerce.IntegrationTests`).
- ❌ Test chỉ chạy một lần rồi xanh mãi (không có negative control).
- ❌ Test đa instance bằng lock in-process; phải **2 process + Redis thật**.
- ❌ Test đồng thời/oversell không có PostgreSQL thật (`Testcontainers.PostgreSql`).

## 4. Probe HTTP thật (danh mục kiểm trước khi viết)

Theo `tools/README-probe.md`:
- **A1–A9**: kiểm cú pháp bằng parser trước · dùng công cụ `edit` cho mã · mỗi việc một dòng · chờ `/health-status` bằng `tools/wait-host.ps1` · kiểm **điểm đo có tồn tại** · in route thật bằng `tools/route-check.ps1` · **không lọc `stderr`** · đo điều kiện tiên quyết · không phẫu thuật probe cũ (viết probe mới).
- **B1–B4**: in `HTTP STATUS` trước body · `Invoke-WebRequest` tự theo redirect (dùng `-MaximumRedirection 0`) · không kiểm văn bản thô trên HTML · sau khôi phục luôn `git diff`.
- **D1–D3**: SQL ghi ra tệp `.sql` rồi chạy · `SELECT` xác nhận sau khi dọn · kiểm tiến trình `dotnet` treo.

## 5. Định nghĩa "xanh"

```powershell
dotnet build eCommerce.slnx --no-incremental     # 0 error, 0 warning mới
dotnet test test/eCommerce.Tests --no-build      # số ca KHÔNG giảm
dotnet test test/eCommerce.IntegrationTests      # khi có Docker
pnpm --prefix apps/admin-web lint && pnpm --prefix apps/admin-web typecheck
pwsh -File tools/kiem-tat-ca.ps1
pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/<MÃ>-testplan.md
```

Số ca chỉ **tăng**; giảm phải có lý do + người duyệt, ghi handover.

## 6. Mẫu tệp phụ

`templates/testplan.md` — khung `docs/tests/<MÃ>-testplan.md`: bảng BR → test → tầng, bảng negative control, số đo trước/sau.
