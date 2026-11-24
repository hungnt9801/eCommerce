---
name: ef-migration-postgres
description: Luật migration EF Core + PostgreSQL 17 của eCommerce — expand → migrate → contract, migration do EF sinh (cấm viết tay), một chủ migration, không sửa migration đã merge, chạy DbMigrator bằng binary mới rồi kiểm DB thật, unique index multi-tenant có NULLS NOT DISTINCT, pg_dump trước khi chạm DB có dữ liệu. Nạp sẵn cho arch, be, ops; dùng trước mọi `migrations add` và khi review diff schema.
user-invocable: false
---

# EF Core + PostgreSQL — eCommerce

Nguồn: `docs/coding-conventions.md` §10 · `docs/nhanh/09-parallel-sessions.md` §6 · `docs/nhanh/decisions.md`.
Máy: PostgreSQL **17** (`docker-compose.yml`), EF Core của ABP 10.7. Migration gốc ở `src/eCommerce.EntityFrameworkCore/Migrations/`.

## 1. Vòng đời thay đổi schema (expand → migrate → contract)

| Bước | Việc | Ai |
|---|---|---|
| 1. Expand | Thêm cột/bảng/index **additive**, nullable hoặc có default; chưa xoá/đổi kiểu | BE (một chủ migration) |
| 2. Migrate | Backfill bằng job/script có idempotency; kiểm số dòng; không khoá bảng lâu | BE + OPS |
| 3. Contract | Chỉ sau khi mọi nơi dùng bản mới; xoá cột/bảng cũ bằng migration **mới** | BE, cần người duyệt nếu phá huỷ |

Trong một WP: **expand-only** trừ khi DoD ghi rõ và có người duyệt (`07` §1 #8, K-05).

## 2. Luật bắt buộc

| # | Luật | Vì sao / dấu hiệu |
|---|---|---|
| M1 | **Để EF sinh migration — CẤM viết tay.** | Migration thiếu `.Designer.cs` bị **bỏ qua âm thầm** (C3) |
| M2 | **Một chủ migration.** Chỉ luồng BE chạy `migrations add`; FE/docs không đụng. | `ModelSnapshot` conflict ⇒ schema lệch |
| M3 | Buộc 2 nhánh: A merge trước → B `git rebase` → B `migrations remove` rồi `add` lại để snapshot nối tiếp. | Không resolve tay snapshot |
| M4 | **CẤM sửa migration đã merge**; thay đổi tiếp ⇒ migration mới. | Lịch sử DB không tái lập được |
| M5 | Chạy `DbMigrator` bằng **binary MỚI build**, rồi **kiểm trạng thái DB thật** (`__EFMigrationsHistory`, bảng/cột/index). | DbMigrator `exit=0` nhưng không áp vì binary cũ |
| M6 | Agent **CẤM** `dotnet ef database update` (deny trong `settings.json`); schema áp qua `DbMigrator`/cổng kiểm. | `D-75` |
| M7 | Unique index trên bảng `IMultiTenant` **PHẢI** `NULLS NOT DISTINCT`. | PostgreSQL coi `NULL` là khác nhau ⇒ ràng buộc mất tác dụng im lặng |
| M8 | Test bất biến index liệt kê **đích danh tên index**, không chỉ đếm tổng. | Thêm 1 index + hỏng 1 index cũ ⇒ tổng không đổi ⇒ test xanh vô nghĩa |
| M9 | Chạm DB **có dữ liệu** ⇒ `pg_dump` trước, ghi đường dẫn tệp dump vào handover. | Không có đường lùi = vùng cấm |
| M10 | Seed **idempotent**; cấm sửa tay DB. | Chạy lại không lỗi |
| M11 | Package ABP cùng version, sửa **một** `Directory.Packages.props`; project test ghim tường minh (bỏ CPM). | Lệch version ABP |
| M12 | SQL/DDL dùng `timestamptz` cho thời điểm; tiền `numeric(18,4)`, số lượng `numeric(18,3)`. | Lệch giờ/tiền |

## 3. Quy trình chuẩn (BE)

```powershell
# 1) Đảm bảo sạch, một chủ migration
git status --short

# 2) Sửa model/DbContext rồi để EF sinh
dotnet ef migrations add <TenTen> --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.HttpApi.Host

# 3) Đọc migration vừa sinh: chỉ expand? có default/backfill? index NULLS NOT DISTINCT?
git diff -- src/eCommerce.EntityFrameworkCore/Migrations

# 4) Build lại binary rồi áp bằng DbMigrator (KHÔNG dùng database update)
dotnet build eCommerce.slnx --no-incremental
dotnet run --project src/eCommerce.DbMigrator

# 5) Kiểm DB THẬT
#    SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1 DESC LIMIT 3;
#    \d "<Bảng>"   -- cột/index/default
```

## 4. Khi review diff schema

- Có `DROP`/đổi kiểu cột **có dữ liệu** ⇒ P1 (phá huỷ, cần người duyệt + `pg_dump`).
- Có `.Designer.cs`? TenTen khớp? Snapshot chỉ đổi đúng phần cần?
- Index tenant thiếu `NULLS NOT DISTINCT` ⇒ P1.
- Backfill trong migration dài/khoá bảng ⇒ P2 (tách job).
- Migration đã merge bị sửa ⇒ P1.
