# Hướng dẫn viết tài liệu thiết kế feature

> Mẫu: [`templates/feature-design-template.md`](templates/feature-design-template.md)
> Kế hoạch & danh sách tài liệu cần viết: `implementation-plan.md` **Phụ lục A**.

---

## 1. Mục đích

Mỗi feature **phải có tài liệu thiết kế được duyệt TRƯỚC khi code** (Definition of Ready).
Tài liệu là **hợp đồng giữa người viết và người review**: nó buộc phải trả lời hết các câu hỏi khó
(trạng thái, đồng thời, idempotency, lỗi, phân quyền) **trước** khi những câu hỏi đó trở thành bug trong code.

**Vì sao bắt buộc với dự án này:** 6 lỗi tìm được ở Phase 0 (thiếu `DbSet`, thiếu `AbpDbContextOptions`,
thiếu `[Authorize]` cấp class, Redis làm sập khởi động, 2 bug ABP CLI) **đều là lỗi thiết kế**, không phải lỗi code.
Và bài học từ eShop cho thấy outbox thiếu relay, guard trạng thái im lặng, `IdentifiedCommand` không dùng trong saga —
**tất cả đều là thứ một tài liệu thiết kế tốt sẽ phát hiện ra trên giấy**.

---

## 2. Đề xuất sửa đổi so với tài liệu tham khảo của bạn

Tài liệu bạn đưa (`ChiTietHopDong_SRS.md`) là **mẫu rất tốt** — tôi **giữ nguyên tinh thần** và chỉ sửa phần
lệch với kiến trúc của ta. Bảng dưới đây nêu rõ từng thay đổi:

### 2.1 ✅ GIỮ NGUYÊN (những điểm mạnh nên theo)

| Điểm mạnh của tài liệu gốc | Vì sao giữ |
|---|---|
| **Use Case Specification dạng bảng** đầy đủ (Pre/Post-condition, Basic/Alternative/Exception Flow, Trigger, Actor, Priority) | Chuẩn, dễ review, không bỏ sót |
| **Mã hoá Business Rule (`BR-xxx`) và NFR (`NFR-xxx`)** | Cho phép **truy vết**: mỗi BR phải có test; mỗi NFR phải có ngưỡng. Đây là điểm giá trị nhất của tài liệu |
| **Bảng "Mapping UI ↔ Data source"** | Phát hiện sớm lệch số liệu giữa tổng hợp và hiển thị |
| **Ghi rõ điểm chênh lệch số liệu** (đếm khác số dòng hiển thị do bộ lọc) | Loại lỗi rất khó phát hiện khi test — thói quen tốt |
| **Phân biệt "không có dữ liệu" (không phải lỗi) vs "lỗi thật"** | Tránh việc trả 500 cho ca rỗng |
| **Test case liên kết ngược về BR** | Đảm bảo coverage có chủ đích |
| **Definition of Done dạng checklist** | Rõ ràng, kiểm được |
| **Sơ đồ Use Case + DFD + Sequence + ERD (Mermaid)** | Trực quan, version-control được |
| **Dùng `<details>` cho phần dài** (source PlantUML, ví dụ) | Giữ tài liệu đọc được |

### 2.2 🔧 SỬA (do khác kiến trúc)

| Mục trong tài liệu gốc | Vấn đề với ta | Sửa thành |
|---|---|---|
| §7 "Service / **Stored Procedure** signature" | ⚠️ **Ta KHÔNG dùng stored procedure** — ta dùng ABP application service + EF Core + PostgreSQL | **§6.1 Application Service** (interface, method, DTO vào/ra) — không còn khái niệm `@param`, `OUTPUT` |
| §8 "**Result Set** Schema (thứ tự result set, `DataTable`)" | Ta trả **DTO có cấu trúc**, không trả nhiều result set | **§5.2 Data Dictionary** (bảng + cột + index + **ràng buộc**) và **DTO ở §6** |
| §7 "REST endpoint: `POST /DataAccess` + `command` bắt đầu `ws_`/`rep_`" | Đặc thù Aladdin (một endpoint chung, dispatch theo tên SP) | **REST theo tài nguyên** do ABP auto API controller sinh: `/<verb> /api/app/<resource>` |
| Tham số inject từ cookie `s`, loại `SessionID` khỏi parameters | Đặc thù Aladdin | Ta dùng **Bearer token (OpenIddict)** + **`TenantId` lấy từ `ICurrentTenant`** (không truyền tay) |
| Transaction mô tả ở tầng controller | Ta dùng **Unit of Work của ABP** | Ghi rõ phạm vi UoW, và **outbox ghi cùng transaction** |
| §9 Error handling: `ex.ToDataSet(messageDev)` | Ta dùng `BusinessException` + mã lỗi + `AbpExceptionFilter` | Giữ bảng tình huống, đổi phần "phản hồi" sang mã lỗi/HTTP thực |

### 2.3 ➕ THÊM (tài liệu gốc chưa có, nhưng dự án này BẮT BUỘC phải có)

| Mục mới | Vì sao bắt buộc |
|---|---|
| **§3 Sơ đồ trạng thái (state machine) + bảng guard** | Ta đã học: guard im lặng của eShop làm event rơi **không để lại dấu vết**; đơn hàng có **4 trục trạng thái**. Đây là nơi phát sinh lỗi nghiệp vụ nhiều nhất |
| **§7 Sự kiện, tích hợp ngoài & idempotency** (ETO phát ra/nhận vào, webhook, retry, DLQ, **idempotency key ổn định**) | Ta có webhook sàn, IPN cổng TT, event bus, outbox. eShop chứng minh: outbox **thiếu relay** ⇒ dual-write vẫn còn; consume **luôn ACK** ⇒ handler lỗi là mất event |
| **§8 Bất biến & xử lý đồng thời** (lock key, unique index, optimistic concurrency) | Phase 0 + khảo sát: 2 luồng cùng trừ một SKU; Shopee **rotate refresh token** ⇒ phải lock |
| **§9 Phân quyền (permission)** | ⚠️ **ABP không tự bảo vệ app service** — đã gặp lỗi API đọc dữ liệu bị công khai ở Phase 0 |
| **§11 Migration & seed** (tên migration, bảng mới, backfill, rollback) | Ta dùng EF migration thật; tài liệu gốc làm việc trên DB có sẵn |
| **§13 NFR mở rộng**: bảo mật, audit, observability (`CorrelationId`), **cách ly đa tenant/kênh** | Hệ thống đa tenant + đa kênh; tài liệu gốc là đơn tenant, một cơ sở |
| **§14.4 Definition of Ready** | Tài liệu gốc chỉ có DoD (sau khi làm); ta cần điều kiện **trước khi bắt đầu** |
| **§15 Câu hỏi mở & giả định** | Buộc ghi rõ điều chưa biết thay vì im lặng đoán — đặc biệt các câu hỏi phải hỏi NCC |
| **§0 Tóm tắt + ADR ngắn** (quyết định + phương án bị loại) | Để 6 tháng sau không ai hỏi lại "sao không làm cách kia" |
| **Ghi rõ `TenantId`/`ChannelCode` trong Pre-condition** | Hệ thống đa tenant và đa kênh |

### 2.4 ❌ BỎ / KHÔNG ÁP DỤNG

| Mục | Lý do |
|---|---|
| Chú giải `@param`, `OUTPUT`, `DataTable`, `result set thứ tự` | Không dùng stored procedure |
| Collapsible "source PlantUML gốc" | Ta dùng Mermaid trực tiếp — **không cần** lưu source PlantUML song song |
| Mã `FacID`, `HopDongID`, `SessionID` | Đặc thù hệ thống vaccine; ta thay bằng `TenantId`/`ChannelCode`/`ShopId`… |
| **NHƯNG GIỮ:** nguyên tắc "mỗi khẳng định phải có nguồn" | Chuyển thành: mỗi khẳng định về NCC ngoài **phải kèm link tài liệu** họ cung cấp |

---

## 3. Quy ước mã & vị trí file

| Mục | Quy ước |
|---|---|
| **Mã feature** | `<MODULE>-<3 chữ số>` — ví dụ `SAL-003`, `INV-002`, `EINV-001` |
| **Tiền tố module** | `XC` (cross-cutting) · `CAT` Catalog · `PRC` Pricing · `INV` Inventory · `SAL` Sales · `CUS` Customers · `PRO` Promotions · `ADDR` AddressHub · `EINV` EInvoiceHub · `SIGN` DigitalSigningHub · `SHP` ShippingHub · `PAY` PaymentHub · `REC` Reconciliation · `ACC` AccountingHub · `REP` Reporting · `NOT` Notifications · `CHN` ChannelHub · `LOY` Loyalty · `REV` Reviews · `WEB` Web |
| **Đường dẫn** | 🚨 **MỖI MODULE MỘT THƯ MỤC RIÊNG**: `docs/features/<module>/<mã>-<slug>.md` — ví dụ `docs/features/sales/sal-003-state-machine-4-truc.md`. **Tên thư mục = tên module, viết thường, tiếng Anh**, khớp với `modules/<Module>/` khi module đã tách (xem bảng dưới) |
| **Ngôn ngữ** | Tiếng Việt; thuật ngữ kỹ thuật giữ tiếng Anh (entity, aggregate, outbox…) |
| **Sơ đồ** | Mermaid trực tiếp trong file (không ảnh, không PlantUML song song) |
| **Độ dài** | 2–8 trang. Feature nhỏ thì ngắn, nhưng **không được bỏ mục nào** — mục không áp dụng ghi "Không áp dụng + lý do" |

### 3.1 Bảng thư mục module (nguồn sự thật cho đường dẫn)

| Thư mục | Tiền tố mã | Module | Tách thành `modules/<X>`? |
|---|---|---|---|
| `cross-cutting/` | `XC` | Nền tảng (Money, outbox, lock, auth, log/metric, CI) | — (dùng chung `shared/eCommerce.Kernel`) |
| `catalog/` | `CAT` | Catalog | ✖ còn trong `src/` |
| `pricing/` | `PRC` | Pricing | ✅ `modules/Pricing` |
| `inventory/` | `INV` | Inventory | ✖ còn trong `src/` |
| `sales/` | `SAL` | Sales | ✖ còn trong `src/` |
| `customers/` | `CUS` | Customers | ✅ `modules/Customers` |
| `promotions/` | `PRO` | Promotions | ✖ chưa có code |
| `address/` | `ADDR` | AddressHub | ✅ `modules/Address` |
| `einvoice/` | `EINV` | EInvoiceHub | ✅ `modules/EInvoice` |
| `signing/` | `SIGN` | DigitalSigningHub | ✅ `modules/Signing` |
| `shipping/` | `SHP` | ShippingHub | ✖ chưa có code |
| `payment/` | `PAY` | PaymentHub | ✅ `modules/PaymentHub` |
| `reconciliation/` | `REC` | Reconciliation | ✖ nằm trong app |
| `accounting/` | `ACC` | AccountingHub | ✖ chưa có code |
| `reporting/` | `REP` | Reporting | ✖ chưa có code |
| `notifications/` | `NOT` | Notifications | ✅ `modules/Messaging` |
| `channel/` | `CHN` | ChannelHub | ✅ `modules/ChannelHub` |
| `loyalty/` | `LOY` | Loyalty | ✖ chưa có code |
| `reviews/` | `REV` | Reviews | ✖ chưa có code |
| `web/` | `WEB` | Web (storefront + admin) | — (`src/eCommerce.Web`) |

📌 **Nhánh `Tax`** không có tài liệu riêng — nó thuộc `cross-cutting/XC-006` (phân loại thuế GTGT).

### 3.2 Cổng kiểm cho tài liệu

| Cổng | Lệnh | Kiểm gì |
|---|---|---|
| Sơ đồ | `npm --prefix tools run check:mermaid` | Mọi khối Mermaid phải **parse được** bằng parser thật của Mermaid |
| Tham chiếu | `pwsh -File tools\kiem-link-tai-lieu.ps1` | Mọi link `[…]` và mọi đường dẫn `docs/…`, `src/…` trong backtick **phải trỏ tới tệp CÓ THẬT**; thoát `1` nếu có tham chiếu chết |

> 🔑 **Vì sao cần cổng tham chiếu:** khi tài liệu được tổ chức lại theo module (`docs/features/<module>/`), mọi đường dẫn tương đối **sâu thêm một cấp** — và một tham chiếu chết **không làm đỏ `dotnet build`**, đúng cùng loại lỗi hỏng-âm-thầm như Mermaid (§7).

---

## 4. Vòng đời một tài liệu

```
1. Draft        → người viết tự điền theo mẫu (kể cả §15 câu hỏi mở)
2. Self-check   → đối chiếu checklist §14.4 (Definition of Ready)
3. Review       → ít nhất 1 người khác đọc, tập trung vào: state machine, đồng thời, idempotency, lỗi
4. Approved     → đổi trạng thái trong header thành Approved  ← ĐIỀU KIỆN ĐỂ BẮT ĐẦU CODE
5. Code         → code theo tài liệu; KHÔNG tự ý lệch
6. Update       → nếu buộc phải lệch: SỬA TÀI LIỆU TRƯỚC, rồi mới code
7. Done         → tick hết §14.3 + §14.4, ghi lịch sử thay đổi
```

**Quy tắc vàng:** *"Nếu code phải lệch tài liệu, sửa tài liệu trước — trong cùng pull request."*
Không có tài liệu nào lỗi thời âm thầm; đó là cách duy nhất để tài liệu còn giá trị sau 6 tháng.

---

## 5. Quy tắc chất lượng (để review nhanh và khách quan)

| # | Quy tắc | Cách kiểm |
|---|---|---|
| 1 | Mỗi **BR** phải **kiểm chứng được** | Nếu không viết được test ⇒ chưa phải BR, hãy viết lại |
| 2 | Mỗi **NFR** phải có **ngưỡng số** | "Nhanh", "ổn định" ⇒ không hợp lệ |
| 3 | Mỗi nhánh **state machine** phải ghi rõ **guard sai thì làm gì** | Nếu ghi "bỏ qua" ⇒ phải giải thích vì sao không phải lỗi |
| 4 | Mọi **cửa vào từ bên ngoài** phải có **chống trùng** | Webhook/IPN/job retry |
| 5 | Mọi **index phục vụ idempotency** phải nêu rõ | Đây là hàng rào cuối khi tầng ứng dụng sai |
| 6 | Mọi **khoá phân tán** phải ghi tên chính xác | Tránh mỗi nơi đặt một kiểu |
| 7 | Tiền phải là `decimal`; **cấm so tiền bằng `==`** | Kiểm trong phần Data Dictionary + test |
| 8 | Không hard-code endpoint NCC | Ghi endpoint vào cấu hình; tài liệu ghi rõ **nguồn** để xác minh |
| 9 | Nếu số liệu **đếm ≠ số dòng hiển thị** ⇒ phải ghi rõ | Kế thừa thói quen tốt của tài liệu gốc |
| 10 | Mục không áp dụng ⇒ ghi **"Không áp dụng + lý do"**, không xoá mục | Giữ tính so sánh giữa các tài liệu |
| 11 | 🚫 **KHÔNG dùng dấu `;` bên trong text của Mermaid** | Mermaid coi `;` là **dấu phân tách câu lệnh** ⇒ sơ đồ lỗi `Parse error`. Đã gặp thật: `Note over DOM: Kiểm tra bất biến; guard...` làm hỏng cả sơ đồ |
| 12 | 🚫 **KHÔNG dùng `<...>` làm placeholder trong Mermaid** | Mermaid hiểu `<...>` là **thẻ HTML** ⇒ chữ biến mất khi render. **Dùng `«...»`** |
| 13 | ✅ `<br/>`, `<b>`, `</b>` **được phép** (HTML hợp lệ, Mermaid render đúng) | Chỉ cấm `<` mở đầu một *tên thẻ không hợp lệ* |
| 14 | ✅ Trước khi commit, chạy `npm --prefix tools run check:mermaid` | Biến lỗi sơ đồ thành **lỗi CI** thay vì lỗi hiển thị khi có người mở file |

---

## 6. Ai viết — ai review

| Loại feature | Người viết | Người review bắt buộc |
|---|---|---|
| Module nghiệp vụ thường | Dev phụ trách | 1 dev khác + (nếu có) BA |
| Có **state machine** | Dev phụ trách | **Kiến trúc trưởng** + BA |
| Có **tích hợp NCC ngoài** | Dev phụ trách | Kiến trúc trưởng + người **đã liên hệ NCC** (để xác nhận thông số) |
| Có **pháp lý** (hoá đơn điện tử, lưu trữ) | Dev + **kế toán** | Kiến trúc trưởng + kế toán + (nếu cần) luật sư |
| Cross-cutting (XC-*) | Kiến trúc trưởng | Toàn nhóm |

---

## 7. Kiểm tra sơ đồ Mermaid tự động

**Vì sao cần:** lỗi cú pháp Mermaid **không** làm hỏng `dotnet build` — nó chỉ hiện
`Mermaid render error: Parse error on line N` khi ai đó mở file bằng công cụ render.
⇒ Rất dễ ship tài liệu lỗi mà không ai biết (đã xảy ra một lần với khối Sequence Diagram).

**Cách dùng:**

```powershell
npm --prefix tools install          # lần đầu: cài mermaid + jsdom
npm --prefix tools run check:mermaid
```

Kết quả: `PASS`/`FAIL` cho **từng khối mermaid** trong mọi file `.md`; exit code ≠ 0 nếu có lỗi ⇒
dùng được trong CI.

**Script:** `tools/check-mermaid.mjs` — dùng **parser thật của Mermaid** (không phải regex), chạy trong Node
với shim DOM (`jsdom`).

**Quy tắc viết Mermaid an toàn** (xem §5 quy tắc 11–14):

| ❌ Sai | ✅ Đúng | Vì sao |
|---|---|---|
| `Note over DOM: Kiểm tra bất biến; guard trạng thái` | `Note over DOM: Kiểm tra bất biến · guard trạng thái` | `;` là dấu phân tách câu lệnh |
| `U->>FE: <thao tác>` | `U->>FE: «thao tác»` | `<...>` bị hiểu là thẻ HTML ⇒ mất chữ |
| `U -- "Yêu cầu (<tham số>)" --> P1` | `U -- "Yêu cầu («tham số»)" --> P1` | như trên |
| `classDef x fill:#fff;` | giữ nguyên | `;` **cuối câu lệnh** là hợp lệ |
