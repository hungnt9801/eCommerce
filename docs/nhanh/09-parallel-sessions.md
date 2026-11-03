# 09 — Chạy song song nhiều session

> Câu hỏi: *"có thể chạy song song các session như thế nào?"*
> **Trả lời ngắn:** được, nhưng **chỉ khi mỗi session có thư mục làm việc, cổng, DB riêng** —
> nếu 2 session cùng sửa một thư mục thì chúng **ghi đè file của nhau** (kể cả `git index`).
> Tài liệu này là cách dựng + luật phối hợp.

---

## 1. Ba kiểu song song (chọn đúng kiểu)

| Kiểu | Cách làm | Dùng khi | **Không** dùng khi |
|---|---|---|---|
| **A. Worktree + session riêng** (khuyến nghị cho code) | `git worktree add` mỗi luồng một thư mục, mỗi session DSH mở ở thư mục đó | 2 luồng cùng code (BE/FE, WP khác nhau) | Hai luồng chạm cùng file domain |
| **B. Subagent trong 1 session** | công cụ `subagent` / `subagent_fork` | Việc **đọc/khảo sát**, sinh tài liệu, audit — không sửa file dùng chung | Sửa code (chung thư mục ⇒ tranh file) |
| **C. Workflow fan-out** | công cụ `workflow` chạy nhiều agent theo script | Audit nhiều file, sinh nhiều tài liệu độc lập | Việc cần tuần tự (migration → build → test) |

📌 **Subagent không có worktree riêng** — chúng dùng chung ổ đĩa với session cha. Vì vậy kiểu B/C chỉ an toàn cho việc **read-only** hoặc **file mới không trùng tên**.

---

## 2. Điều kiện tiên quyết (nếu thiếu thì song song = hỏng)

| # | Điều kiện | Kiểm bằng |
|---|---|---|
| 1 | **Working tree sạch** trước khi tạo worktree (không có thay đổi chưa commit) | `git status --short` |
| 2 | **Mỗi luồng một nhánh**, tên theo WP | `git branch -vv` |
| 3 | **Mỗi luồng một DB** (cùng container Postgres, khác database) | `psql -l` |
| 4 | **Mỗi luồng một cổng API + cổng Next** | bảng §3 |
| 5 | **Hợp đồng API có snapshot** để FE không phụ thuộc host của BE | `packages/api-client/openapi.v1.json` |
| 6 | **Một "chủ migration"** tại một thời điểm | §6 |

✅ Điều 5 **đã làm xong** trong phiên này: `pnpm spec:snapshot` xuất JSON, `pnpm gen:api` sinh type **offline từ snapshot** (không cần API chạy).

---

## 3. Bảng cấp phát tài nguyên

| Luồng | Worktree | Nhánh | API (http) | Next | Database | Redis db |
|---|---|---|---|---|---:|---:|
| **A — BE** (WP-03/04) | `D:\Projects\Tools\eCommerce` (gốc) | `feature/wp-03-catalog` | **54275** | 3000 | `ecommerce` | 0 |
| **B — FE** (màn hình) | `D:\Projects\Tools\eCommerce-fe` | `feature/fe-screens` | 54276 | **3001** | `ecommerce_fe` | 1 |
| **C — Docs/khảo sát** | `D:\Projects\Tools\eCommerce-docs` | `feature/docs-nhanh` | — | — | — | — |

> 🔑 Mỗi worktree có `bin/`, `obj/`, `node_modules/`, `.next/` **riêng** ⇒ build không giẫm nhau.
> `pnpm` dùng chung store nên `pnpm install` lần 2 rất nhanh; NuGet cache dùng chung.

---

## 4. Dựng một luồng mới (PowerShell, đã kiểm cú pháp)

```powershell
cd D:\Projects\Tools\eCommerce

# 0) Bắt buộc: commit phần đang làm trước (worktree mới KHÔNG thấy thay đổi chưa commit)
git status --short
git add -A ; git commit -m "feat(admin): WP-01/02 - API v1 + Next.js shell/login/product list"

# 1) Tạo worktree + nhánh
git worktree add ..\eCommerce-fe -b feature/fe-screens
cd ..\eCommerce-fe

# 2) Cài dependency riêng (store dùng chung)
pnpm install

# 3) DB riêng (cùng container Postgres)
docker exec ecommerce-postgres psql -U ecommerce -d postgres -c "CREATE DATABASE ecommerce_fe;"
cd src\eCommerce.DbMigrator
$env:ConnectionStrings__Default='Host=localhost;Port=5432;Database=ecommerce_fe;User ID=ecommerce;Password=ecommerce_dev_only;'
dotnet run

# 4) API riêng ở 54276 (Authority PHẢI khớp cổng)
cd ..\eCommerce.HttpApi.Host
$env:ConnectionStrings__Default='Host=localhost;Port=5432;Database=ecommerce_fe;User ID=ecommerce;Password=ecommerce_dev_only;'
$env:Redis__Configuration='127.0.0.1:6379,defaultDatabase=1'
$env:AuthServer__Authority='http://localhost:54276'
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --no-launch-profile --urls http://localhost:54276

# 5) Next riêng ở 3001 (terminal khác)
cd ..\..\apps\admin-web
$env:API_BASE_URL='http://localhost:54276'
pnpm exec next dev -p 3001
```

Kiểm nhanh sau khi dựng:

```powershell
Invoke-RestMethod http://localhost:54276/api/v1/system/ping     # 200
Invoke-WebRequest http://localhost:3001/login -UseBasicParsing  # 200
```

Dọn luồng:

```powershell
git worktree remove ..\eCommerce-fe      # chỉ khi đã commit/hết việc
docker exec ecommerce-postgres psql -U ecommerce -d postgres -c "DROP DATABASE ecommerce_fe;"
```

---

## 5. Luật phối hợp (quan trọng hơn hạ tầng)

1. **Chủ sở hữu file**: mỗi luồng khai vùng file của mình. Ví dụ A sở hữu `src/`, `modules/`; B sở hữu `apps/admin-web/`; C sở hữu `docs/`.
   Không luồng nào sửa file của luồng khác — cần thì mở issue/PR.
2. **Rebase hằng ngày** lên `main` (hoặc nhánh tích hợp), không để 2 tuần.
3. **Merge theo thứ tự**: BE (API + migration) trước → FE rebase → FE merge.
4. **Không chạy `dotnet ef migrations add` ở 2 luồng cùng lúc** — xem §6.
5. **Không sửa** `eCommerce.slnx`, `Directory.Packages.props`, `pnpm-lock.yaml` ở 2 luồng cùng lúc. Ai cần thì báo và merge ngay.
6. **Port/DB ghi vào** bảng §3 (tạo tệp `parallel-board.md` trong thư mục này khi có ≥ 2 luồng) để không trùng.

---

## 6. Migration & `ModelSnapshot` — điểm chết người

`dotnet ef migrations add` sửa **`eCommerceDbContextModelSnapshot.cs`**. Hai nhánh cùng thêm migration sẽ **conflict file này**, và resolve sai = **schema lệch**.

| Luật | Cách làm |
|---|---|
| **Một chủ migration** | Chỉ luồng BE được `migrations add`. FE/docs không đụng. |
| **Serialize khi buộc phải 2** | A merge migration trước → B `git rebase main` → B **xoá migration vừa tạo** và tạo lại (`dotnet ef migrations remove` rồi `add`) để snapshot nối tiếp |
| **Không sửa migration đã merge** | Tạo migration mới |
| **DB riêng** | Mỗi luồng migrate DB của mình; **không** trỏ 2 luồng vào cùng DB |

---

## 7. Contract-first (đã bật) — cách FE chạy trước BE

```
BE: sửa DTO/controller → chạy host → pnpm spec:snapshot → commit openapi.v1.json
FE: git pull → pnpm gen:api (đọc snapshot, KHÔNG cần host) → code theo type
```

- `packages/api-client/openapi.v1.json` là **hợp đồng đã commit** (6 path `/api/v1` hiện tại).
- CI (WP-09) sẽ chạy `pnpm gen:api` + `git diff --exit-code` ⇒ API đổi mà quên snapshot là **đỏ**.

⚠️ Đổi API theo hướng **phá vỡ** (xoá field, đổi kiểu) phải báo luồng FE **trước** khi merge.

---

## 8. Ma trận: việc nào song song được

| Song song được | Không song song |
|---|---|
| BE module **Catalog** + FE dựng **shell/design system** | 2 luồng cùng sửa `Catalog` (Product/Unit) |
| BE module **Inventory** + FE màn **Đơn hàng** (API đã có) | 2 luồng cùng `StockDocument`/`StockLedger` |
| Docs/khảo sát + code | 2 luồng cùng `docs/nhanh/README.md` |
| Viết test E2E cho màn đã xong + làm màn mới | 2 luồng cùng `apps/admin-web/app/[business]/layout.tsx` |
| Sinh tài liệu từ template + sửa code | 2 luồng cùng `pnpm-lock.yaml` / `slnx` |

---

## 9. Đề xuất cho hoàn cảnh của bạn (1 người + agent)

**Chạy 2 luồng là tối đa** — nhiều hơn thì chi phí chuyển ngữ cảnh + merge lớn hơn lợi ích.

| Tuần | Luồng A (BE) | Luồng B (FE) |
|---|---|---|
| 1 | WP-03 `ProductUnit` + migration 51 | Design system: `DataTable`, `FilterBar`, `PageTabs`, `MoneyInput` (dùng API hiện có) |
| 2 | WP-03 backfill + API ghi sản phẩm | Form sản phẩm (type Thường) trên contract snapshot |
| 3 | WP-04 tồn 9 cột + posting | Màn kho (list) + màn chi tiết SP |
| 4 | WP-04 phiếu kho | Màn tạo phiếu + cột tồn |
| 5–6 | WP-05 đơn hàng | POS UI (mock state trước, nối API sau) |

**Nhịp:** sáng rebase + chạy baseline; chiều merge luồng xong; cuối tuần một bản build xanh trên `main`.

---

## 10. Chi phí & rủi ro

| Rủi ro | Dấu hiệu | Xử lý |
|---|---|---|
| **Hai session sửa cùng file** | conflict liên tục, mất code | Kiểm `git status` 2 bên trước khi sửa; worktree là bắt buộc |
| **Port/DB trùng** | API 500 "connection refused"/dữ liệu lạ | Bảng §3 + đổi `AuthServer__Authority` đúng cổng |
| **Redis chung** | cache quyền/setting "nhảy" giữa luồng | `defaultDatabase` khác nhau (0/1/2) |
| **Migration conflict** | snapshot lệch, test DB đỏ | §6: một chủ migration |
| **Ổ đĩa** | mỗi worktree ~1–2 GB (`bin/obj/node_modules/.next`) | Dọn `obj/bin` khi xong; `git worktree remove` |
| **Context switching** | 2 luồng đều dở dang | Mỗi luồng phải kết thúc ngày ở **điểm xanh** + handover ([08](08-session-protocol.md)) |

### 10.1 Server dev sống lâu hơn session

Background job của session **bị thu hồi khi job/session kết thúc** (đã đo 02/11/2026: host chạy ~1 giờ rồi
dừng với `exit 1`, **không có exception** trong log — chỉ là bị thu hồi). Vì vậy, khi cần server chạy qua
nhiều session:

```powershell
# Chạy DLL trực tiếp (không qua `dotnet run` để không có tiến trình con), detached, có log
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:AuthServer__Authority='http://localhost:54275'
Start-Process dotnet `
  -ArgumentList 'bin\Debug\net10.0\eCommerce.HttpApi.Host.dll','--urls','http://localhost:54275' `
  -WorkingDirectory 'D:\Projects\Tools\eCommerce\src\eCommerce.HttpApi.Host' `
  -RedirectStandardOutput 'D:\Projects\Tools\eCommerce\artifacts\logs\api-54275.log' `
  -RedirectStandardError  'D:\Projects\Tools\eCommerce\artifacts\logs\api-54275.err.log' `
  -WindowStyle Hidden -PassThru
```

- Ghi **PID** vào bảng §3 để lần sau tắt đúng tiến trình (`Stop-Process -Id <pid>`).
- Log nằm trong `artifacts/logs/` (đã gitignore).
- Mỗi luồng một cổng + một file log riêng; **đừng** dùng chung một job nền cho 2 luồng.

---

## 11. Checklist mở/đóng mỗi luồng

**Mở:**
- [ ] `git status` sạch ở luồng này
- [ ] Worktree + nhánh đúng tên
- [ ] DB + API port + Next port **khác** luồng kia (ghi vào bảng)
- [ ] `pnpm install` (nếu là worktree mới)
- [ ] `pnpm gen:api` từ snapshot — type khớp hợp đồng đã commit
- [ ] Baseline: build + test + 2 cổng tài liệu

**Đóng:**
- [ ] Build/test xanh, số đo ghi vào handover
- [ ] Commit + push nhánh (hoặc nói rõ chưa push)
- [ ] Cập nhật bảng port/DB nếu có thay đổi
- [ ] Nếu đổi API: `pnpm spec:snapshot` + commit JSON
- [ ] Ghi việc đầu tiên của phiên sau
