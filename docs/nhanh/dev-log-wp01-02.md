# Dev log — WP-01 (nền Web Admin) + WP-02 (hợp đồng API)
> 🗄️ **LỊCH SỬ — KHÔNG dùng làm điểm vào** (gắn nhãn 14/11/2026 theo `D-69`). Tệp ghi lại một thời kỳ/việc **đã qua**; **số đo, trạng thái và 'việc tiếp theo' trong tệp đã cũ** — kể cả câu *'đọc tệp này trước'* bên dưới. Điểm vào hiện tại: `CLAUDE.md` → `docs/nhanh/README.md` → handover mới nhất `docs/nhanh/handover-n*.md` → `docs/nhanh/07-ke-hoach-trien-khai.md`. Xếp loại tài liệu: `docs/agents/ke-hoach-he-thong-agent.md` §14.1.


> Ngày 02/11/2026 · nhánh `feature/wp-01-02-foundation` · người thực hiện: phiên thi công
> Kế hoạch: [07](07-ke-hoach-trien-khai.md) · Quyết định: [decisions.md](decisions.md)

---

## 1. Đã làm & **đo được** (không phải "đã viết xong")

| Hạng mục | Kết quả đo | Cách kiểm |
|---|---|---|
| Baseline trước khi sửa | build **0 error / 9 warning**; **1634/1634** unit test | [baseline-2026-11-02.md](baseline-2026-11-02.md) |
| Project `src/eCommerce.HttpApi` | build xanh, Host nạp module mới | `dotnet build eCommerce.slnx` |
| `GET /api/v1/system/ping` | **200** (anonymous) `{service,apiVersion,serverTimeUtc,authenticated}` | `Invoke-RestMethod` |
| `GET /api/v1/system/me` | **200** sau khi có token (`userId=3a24454e…`, tenantId null) | token password grant |
| `GET /api/v1/system/permissions` | **200** `{granted:[eCommerce.Products,eCommerce.Orders],count:2}` — **loại** quyền không có & tên sai | hỏi 5 quyền, chỉ 3 tồn tại/có |
| `GET /api/v1/products` | **200** `{items:[],total:0,page:1,pageSize:5}` (DB dev rỗng) | |
| `GET /api/v1/products/lookup?q=KHONGCO` | **404** `{code:"catalog.product.not_found"}` | |
| `GET /api/v1/products` không token | **401** | |
| FE `next build` | **✓** 7 route; First Load JS **102–106 kB**; middleware 34 kB | `pnpm --filter @ecommerce/admin-web build` |
| FE `/login` | **200** (8.5 KB HTML) | |
| FE `/{business}` không session | **307** → `/login` | |
| FE proxy không session | **401** JSON | |
| **Đăng nhập thật + tải trang có dữ liệu** | POST `/api/auth/callback/credentials` → 302 + cookie `authjs.session-token`; `GET /230222/product/item` → **200, 20.197 bytes**, có "Sản phẩm" + empty-state | `WebRequestSession` |
| BFF proxy có session | **200** `{items:[],total:0}`; permissions **200** | `/api/proxy/v1/...` |
| OpenAPI → TS | sinh **6/204 path** (`/api/v1`), 110 KB (đã lọc, không kéo 1,1 MB ABP) | `pnpm gen:api` |

## 2. Cấu trúc đã thêm

```
pnpm-workspace.yaml · package.json (root)
apps/admin-web/                     Next.js 15.5.28 (App Router, TS strict, Tailwind 4)
  app/{layout,page,login}/…         login + redirect
  app/[business]/layout|page        shell (sidebar 2 cấp + topbar 52px) + dashboard
  app/[business]/product/item       danh sách sản phẩm (bảng dày, tab, lọc, phân trang)
  app/api/auth/[...nextauth]        Auth.js v5
  app/api/proxy/[...path]           BFF gắn Bearer (token không rời server)
  components/shell/{Sidebar,Topbar}
  lib/{auth,api,menu,config}.ts
  app/globals.css                   design tokens Nhanh (#0C83FF/#059669/#252B36/#E2E6ED)
packages/api-client/                openapi-typescript + script lọc /api/v1
src/eCommerce.HttpApi/              controller v1 tường minh (D-14)
```

## 3. Cách chạy (3 lệnh)

```powershell
# 0) hạ tầng + DB
docker compose up -d
cd src\eCommerce.DbMigrator; dotnet run          # 50 migration + seed (idempotent)

# 1) API host ở HTTP 54275 (profile mới, Authority khớp cổng)
cd ..\eCommerce.HttpApi.Host
dotnet run --launch-profile "eCommerce.HttpApi.Host (http 54275 - cho Web Admin Next.js)"

# 2) Web Admin
cd ..\..\apps\admin-web
pnpm dev            # http://localhost:3000 → admin@abp.io / 1q2w3E*
```

## 4. Ba điểm **lệch kế hoạch** (cần bạn biết, không giấu)

| # | Lệch | Vì sao | Xử lý |
|---|---|---|---|
| 1 | Đăng nhập dùng grant **`password`** thay vì Authorization Code + PKCE | Client seed `eCommerce_App` **chưa bật** `authorization_code` + redirect URI; bật cần sửa seeder + chạy lại migrator (rủi ro giữa sprint) | Giữ nguyên hành vi token-server-side (D-12 vẫn đúng); đổi sang OIDC ở cuối WP-01 — **không đổi API** |
| 2 | Next **15.5.28** thay vì 16 | Bản cài được từ registry tại thời điểm này; API dùng (App Router/router handler) ổn định | Nâng lên 16 ở WP-09 (có E2E chặn hồi quy) |
| 3 | `AuthServer:Authority` đang là `http://localhost:5000` trong `appsettings.Development.json` | Cổng thật là 54275 ⇒ token hợp lệ vẫn bị **401** (đã gặp và sửa bằng env) | Thêm **launch profile** đặt `AuthServer__Authority=http://localhost:54275`; không sửa file chung vì `eCommerce.Web` còn dùng |

## 5. Việc tiếp theo (theo 07 §7)

1. **Ngày 6**: nút "Thêm mới" + form sản phẩm (type Thường) → `POST /api/v1/products` (cần bổ sung endpoint ghi ở WP-03).
2. **WP-03**: `ProductUnit`/`Type`/`ParentId` + migration `51` (additive) + backfill `--dry-run`.
3. **WP-09**: Playwright 8 luồng (bắt đầu bằng login → danh sách → tạo SP).
4. Dọn 9 warning `CS8604` khi chạm 2 module Address/Sales.

## 6. Lệnh kiểm chứng lại toàn bộ

```powershell
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pnpm --filter @ecommerce/admin-web build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
npm --prefix tools run check:mermaid
```
