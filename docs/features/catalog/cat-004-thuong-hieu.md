# `CAT-004` — Thương hiệu (`Brand` / `CatBrands`)

> **Trạng thái:** ✅ Triển khai 27/10/2026 · **Quyết định:** chốt `CAT-002` **Q3** — Brand là **entity riêng**, không phải chuỗi trên `Product`.

## 1. Vì sao có tài liệu này

`CAT-001` để lại `Product.BrandId` (`Guid?`) nhưng **không có bảng `CatBrands`** ⇒ cột **mồ côi**, không FK,
không màn hình quản trị. `CAT-002` §15.1 **Q3** để ngỏ *"entity riêng hay chuỗi?"* ⇒ mọi lần bàn tới Brand đều
dừng ở câu hỏi đó. Ngày **27/10/2026** chốt: **entity riêng**.

## 2. Quyết định (ADR)

| Mã | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| **BRAND-01** | `Brand` là **aggregate riêng** (`CatBrands`) | Một thương hiệu có **một** slug/logo/mô tả dùng chung; chuỗi tự do khiến `Samsung`/`samsung`/`SS` không gộp được, sửa tên phải sửa từng sản phẩm | Thêm bảng + FK; đổi tên thương hiệu là một thao tác riêng |
| **BRAND-02** | `Code` **bất biến** sau khi tạo; `Name`/`Slug`/mô tả/logo đổi được | `Code` dùng cho import/báo cáo/đối soát — đổi mã làm lệch lịch sử; `Slug` là SEO nên phải đổi được (`CAT-003` ADR-05) | Người dùng muốn đổi mã phải tạo thương hiệu mới |
| **BRAND-03** | `Code` và `Slug` **duy nhất trong tenant**, `NULLS NOT DISTINCT` | Cùng bất biến NND của toàn dự án: `TenantId = NULL` không được làm index mất tác dụng | 2 unique index (NND 54 → **56**) |
| **BRAND-04** | Không có navigation `Products`; gán sản phẩm qua `Product.BrandId` | Mỗi sản phẩm là aggregate riêng; tránh nạp cả danh mục sản phẩm chỉ để đổi tên thương hiệu | Muốn đếm sản phẩm theo brand phải query riêng |
| **BRAND-05** | Xoá = **ngừng hoạt động**, không xoá cứng | Sản phẩm/lịch sử bán cũ vẫn phải đọc được thương hiệu | Bảng giữ bản ghi `IsActive = false` |
| **BRAND-06** | GĐ1 dùng quyền `Catalog.Products` cho CRUD brand | Tách `Brands.*` là thay đổi **hợp đồng phân quyền** (seed + UI + test) — làm lát cắt riêng | Người quản sản phẩm quản được brand (chấp nhận GĐ1) |

## 3. Mô hình dữ liệu

Bảng `CatBrands`:

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | `uuid` | PK |
| `TenantId` | `uuid?` | NND trên `(TenantId, Code)` và `(TenantId, Slug)` |
| `Code` | `varchar(64)` | **bất biến**, duy nhất/tenant |
| `Name` | `varchar(256)` | |
| `Slug` | `varchar(256)` | sinh từ `Name` nếu bỏ trống; **không** bịa `brand-{guid}` |
| `Description` | `varchar(2048)?` | |
| `LogoUrl` | `varchar(1024)?` | |
| `IsActive` | `bool` | mặc định `true` |
| `SortOrder` | `int` | |
| audit | | `FullAuditedAggregateRoot` |

FK: `CatProducts.BrandId → CatBrands.Id` **`Restrict`** (xoá cứng brand không kéo mất sản phẩm).

## 4. API

`IBrandAppService` — `/api/app/brand` (quyền `Catalog.Products`):

| Method | Route | Ghi chú |
|---|---|---|
| `GET` | `/api/app/brand` | lọc `Filter` (Code/Name/Slug), `IsActive`, phân trang |
| `GET` | `/api/app/brand/{id}` | |
| `POST` | `/api/app/brand` | trùng Code ⇒ `Catalog:0030`; trùng Slug ⇒ `Catalog:0031` |
| `PUT` | `/api/app/brand/{id}` | **không** đổi `Code` |
| `POST` | `/api/app/brand/{id}/deactivate` | |
| `POST` | `/api/app/brand/{id}/activate` | |

## 5. Bằng chứng

- Migration `20261027023328_Brand_CatBrands` — bảng + FK + 2 unique NND.
- `BrandTests` (7 ca): chuẩn hoá, sinh slug, mã lỗi `0011`/`0202`/`0032`, `Code` bất biến, ngừng/kích hoạt.
- `PostgreSqlSchemaTests.F1_du_56_...`: NND **56**, có `IX_CatBrands_TenantId_Code` + `IX_CatBrands_TenantId_Slug`.
- `ViPhamRangBuocDb`: 2 index ⇒ `Catalog:0030`/`0031` (không rơi về `Db:0001`).
