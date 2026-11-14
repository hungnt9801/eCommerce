# Mặc định kỹ thuật — dùng khi quyết thay chủ dự án

> Mỗi dòng là **mặc định đã có tiền lệ** trong repo. Khi câu hỏi rơi vào một dòng ở đây, trả **`TIEN_LE`**
> (trích nguồn), không tạo D-xx mới. Nguồn: `docs/nhanh/decisions.md` (D-xx), `docs/coding-conventions.md` (§),
> `docs/structure-and-techstack.md`, `docs/nhanh/09-parallel-sessions.md`.
>
> Stack: .NET 10 · ABP 10.7 modular monolith · EF Core + PostgreSQL 17 · Redis 7 · OpenIddict ·
> Next.js App Router (`apps/admin-web`) + BFF · pnpm workspace · Vitest · Playwright · GitHub Actions.

## 1. Mô hình dữ liệu & migration

| Câu hỏi hay gặp | Mặc định | Nguồn |
|---|---|---|
| Thêm giá trị enum lưu DB? | **Chỉ thêm ở CUỐI dải**, không đổi số cũ; giá trị lấy theo **số Nhanh đo được** nếu có | D-17, D-20, D-28 |
| Thêm trạng thái mới hay suy ra? | Nếu suy ra được từ dữ liệu khác (vd "Hết hàng" từ tồn) ⇒ **suy ra**, không thêm enum | D-19 |
| Thêm cột vào bảng có dữ liệu? | **Nullable / có default**, expand-only; `NOT NULL` hoá là bước riêng (vùng cấm nếu có dữ liệu thật) | D-25/D-31, SKILL §3 |
| Kiểu tiền / số lượng? | Tiền `decimal` qua `Money` (`numeric(18,2)` ở chứng từ kho, `decimal(18,4)` ở domain lõi); số lượng `decimal(18,3)`; **không** `double`, **không** so `==` | §4, D-31, D-37, XC-001 |
| Unique index trên bảng `IMultiTenant`? | **Bắt buộc** `NULLS NOT DISTINCT`; test liệt kê **đích danh** tên index | §5, D-29 |
| Chống trùng khi client retry? | `IdempotencyKey` **do client sinh** + unique `(TenantId, IdempotencyKey)` có filter `IS NOT NULL` | D-29, D-04, D-42 |
| Khoá lạc quan cho aggregate? | Dùng `ConcurrencyStamp` của ABP; **không** thêm `RowVersion` thứ hai | D-26, D-32 |
| Xung đột đồng thời trả gì? | `409` là **hợp đồng** ("chồng request, thử lại"); client thử lại **có giới hạn** cùng khoá idempotency; **không** retry trong server | D-66 |
| Migration? | **EF sinh**, cấm viết tay; chạy DbMigrator bằng binary mới rồi kiểm DB thật; **không** 2 phiên cùng `migrations add` | §10, `09-parallel-sessions.md` §6 |
| Tồn kho đổi thế nào? | **Sổ cái append-only**; cấm `UPDATE` số lượng; ghi qua **một cửa** (`StockPostingService`, `IPosStockGateway`) | §5, D-43 |
| Tiền tính ở đâu? | **Hàm thuần** (`TinhTienHoaDon`, `TinhTienChungTu`, `TongHopDoanhThu`) — một công thức, có unit test | D-37, D-42, D-48 |
| Điểm / công nợ / tích luỹ của khách? | **Một cửa ghi** `Customer.GhiNhanMuaHang`/`DaoGhiNhanMuaHang`; vượt hạn mức ⇒ **NÉM** | D-45 |
| Đa doanh nghiệp? | `TenantId` trên **mọi bảng mới**, UI tenant chưa bật | D-01 |
| Số chứng từ? | Mẫu `<TIỀN TỐ>-yyyyMMdd-NNNN`, theo ngày kinh doanh; trùng số có mã lỗi riêng | D-44 |
| Thời gian? | Lưu **UTC**; "ngày kinh doanh" = **UTC+7** ở tầng báo cáo/hiển thị | §4, D-47 |

## 2. Module, sự kiện, quyền

| Câu hỏi | Mặc định | Nguồn |
|---|---|---|
| Module A cần dữ liệu module B? | Qua hợp đồng ở `*.Application.Contracts`; **cấm** `module → app`; không navigation/FK chéo | §2, D-43 |
| Aggregate mới hay tái dùng? | Vòng đời khác ⇒ **aggregate riêng** (vd `RetailBill` ≠ `Order`) | D-18, D-42 |
| Phát sự kiện? | Phân loại **mang dữ liệu** (fail-closed, không marker) vs **thông báo** (`INotificationEvent`); handler ghi dữ liệu mở UoW `requiresNew` | §6, §6.1, §6.2 |
| Webhook/IPN/job? | **Idempotent ở mọi biên vào**; sự kiện sàn là at-least-once | §5, README #6 |
| Tài nguyên dùng chung? | Khoá phân tán `<module>:<đối tượng>:<id>:<hành động>` qua `<Module>Consts.LockKeys`; test **2 instance + Redis thật** | §3, §5, §11 |
| Máy trạng thái gặp chuyển trạng thái không hợp lệ? | **NÉM LỖI**, không im lặng | §5, D-40 |
| App service mới? | `[Authorize]` **cấp class**, permission là hằng số đã định nghĩa, thêm vào ma trận 401/403/200 | §7 |
| Phạm vi dữ liệu theo người dùng? | Theo **vai trò**, mặc định `All`, gộp "rộng nhất", lọc ở **server** cho cả DS + chi tiết | D-46 |
| Worker gọi bên ngoài / sửa dữ liệu vận hành? | **Mặc định TẮT**, bật bằng cấu hình; `RunOnStart = true` | §9 |
| Mã lỗi mới? | `eCommerce:<Miền>:<4 số>` theo dải của nhóm chức năng | §3 |

## 3. API & Frontend

| Câu hỏi | Mặc định | Nguồn |
|---|---|---|
| Endpoint cho Web Admin? | `/api/v1/**` controller **tường minh** trong `src/eCommerce.HttpApi`; `/api/app/**` giữ nguyên | D-13, D-14 |
| Đặt tên? | API/DTO **tiếng Anh**; method domain tiếng Việt được phép; **không** rename hàng loạt | D-09 |
| Thao tác hàng loạt? | FE gọi API **từng bản ghi tuần tự** qua cửa đơn lẻ; **không** thêm endpoint bulk | D-35 |
| Tính năng thuần hiển thị (in, ẩn cột, lưu lọc)? | Làm ở **FE**, không thêm API ở P0; lưu lọc ở `localStorage` | D-36 |
| Type DTO ở FE? | Lấy từ `packages/api-client/src/schema.d.ts` (sinh từ OpenAPI); **không** chép tay; `gen:api` không đổi schema là cổng | D-33, D-50 |
| Danh sách có tab? | Tab là **dữ liệu** (một bảng định nghĩa), đếm và lọc dùng **chung** một biểu thức | D-41 |
| Token đăng nhập? | Chỉ server-side (BFF + cookie httpOnly) | D-12 |
| Route? | `/{business}/module/entity/action` (giữ route Nhanh), redirect 308 từ `?businessId=` | D-11 |
| Định dạng số/ngày/điện thoại? | Theo `docs/nhanh/04-web-admin-nextjs.md` §9 (tiền dấu phẩy như Nhanh, số lượng 3 lẻ, `dd/MM/yyyy HH:mm`, SĐT bắt đầu `0`) | 04 §9 |
| Trạng thái màn có dữ liệu? | loading / empty / error; empty ≠ error | luật FE |

## 4. Kiểm chứng & quy trình

| Câu hỏi | Mặc định | Nguồn |
|---|---|---|
| Test/probe chập chờn? | **Chẩn đoán bằng chứng trước**, không nới hạn chờ, không nới bất biến; sửa có giới hạn khi đã biết nguyên nhân | D-64, D-66, D-67, D-68 |
| Kế hoạch ghi số khác số đo được? | **Theo số đo**, ghi chênh lệch | D-38, D-20 |
| Chưa làm được vì thiếu dữ liệu/cột? | **Hoãn có lý do** + nợ có nhận chủ (WP/D-xx) — là phương án hợp lệ | D-34 |
| Khác biệt Windows/Linux trong script? | Đi qua **một chỗ** trong `tools/nen-tang.ps1` | D-54 |
| Khi nào bật required check / merge? | Chỉ sau khi **đo** `ci` xanh trên đúng cây; merge là **vùng cấm** của DEC | D-59, D-62, D-65 |
| Bug sửa xong? | Có test tái hiện, test được kiểm **ngược** (vi phạm ⇒ đỏ) | §11 |
| Phụ thuộc input bên ngoài (XML thuế, endpoint NCC)? | **Không** hoàn thiện bằng suy đoán — adapter + giả lập + ghi "chưa xác minh" | §13 |
| Build để đo? | `dotnet build eCommerce.slnx --no-incremental` (build tăng dần ẩn warning); 0 warning mới | `coding-conventions.md` §1 |
| Số test giảm sau thay đổi? | **Không chấp nhận** mặc định; giảm phải có lý do + người duyệt | `07` §1 #3, §10 #7 |
| Test tối thiểu cho mã mới? | Luật tồn/tiền/trạng thái ⇒ test domain invariant · app service ≥ 3 ca (đúng, sai dữ liệu, sai quyền) · endpoint `/api/v1` ≥ 1 ca 200 + 1 ca 401/403 | `07` §9 |
| PR lớn? | ≤ ~600 dòng diff trừ migration; một WP một nhánh; tách nếu vượt | `07` §1 #5 |
| Hai luồng song song? | DB riêng + cổng riêng mỗi luồng; một chủ migration; merge BE trước FE; không sửa `eCommerce.slnx` / `Directory.Packages.props` / `pnpm-lock.yaml` cùng lúc | `09-parallel-sessions.md` §3–§6 |
| Code buộc lệch tài liệu feature? | **Sửa tài liệu trước, cùng PR** (BA sửa SRS trên cùng nhánh) | `design-doc-guidelines.md` §4, `coding-conventions.md` §12 |
| Commit / PR? | Commit **thuần ASCII**, dạng `<loại>(<phạm vi>): <mã phiên> - <nội dung>`; PR mở đầu `Refs: D-xx`; `git add` theo đường dẫn cụ thể | `git log`, `decisions.md` §Cách dùng |
| Lấy mã từ dự án khác? | **Không** copy từ nguồn copyleft (nopCommerce, Smartstore, GrandNode); nguồn MIT đã liệt kê mới được port | §13, `docs/business-modules-proposal.md` §7.3 |
