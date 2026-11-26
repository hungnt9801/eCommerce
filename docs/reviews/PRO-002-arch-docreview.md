# Doc review — PRO-002 «Kiến trúc module Promotions: cây điều kiện AND/OR & engine khuyến mãi»

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/PRO-002-arch.md` (v0.4, 15 mục) |
| Ngày · vòng | 27/11/2026 · **vòng 4** (N94 — kiểm nhanh P3 vòng 3) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc vòng 4 | dòng 1–26 (header) · 72–128 (§2.1–§2.3) · 474–540 (§10–§12) · 596–603 (§15); đối chiếu `IEventCoverageProbe.cs:3,17`, `Sales.Application.csproj` (tiền lệ, vòng 2–3) |
| Máy | `check-agent-artifact.ps1 -Kind docreview` → exit 0; `check-feature-doc.ps1 -WP14` **không áp** cho tệp `-arch` (đã ghi đúng ở §14) |

**P1 = 0 · P2 = 0 · P3 = 1**

Tóm tắt vòng 4: **P3 vòng 3 đã đóng** — allowlist thu hẹp còn **đúng một** namespace `eCommerce.Messaging.EventCoverage` (không wildcard) ở §2.1 (:101) + B2 (:113), và §10.2 (:492) nêu tên test `RanhGioiPromotionsTests`; hàng `v0.4` có ở §15 (:603). Không phát sinh P1/P2.

## Vòng 4 (N94 — kiểm nhanh P3 vòng 3)

### Đã kiểm (vòng 4)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep `Messaging.*`/`EventCoverage`/`RanhGioiPromotionsTests`: §2.1 sơ đồ (:83), bảng cho phép (:96), ngoại lệ (:101), B2 (:113), §10.2 (:492), §13 (:564) nói **cùng một** ngoại lệ; wildcard `eCommerce.Messaging.*` chỉ còn xuất hiện trong câu **cấm** ("không wildcard"). | 0 phát hiện |
| D2 chéo SRS / code | `IEventCoverageProbe.cs:3` = `namespace eCommerce.Messaging.EventCoverage;` đúng như allowlist; `Sales.Application.csproj` tham chiếu `Messaging.Domain` (tiền lệ vòng 2); SRS v0.10 §7.1 nay khớp D3/D4/D5 của §12.1. | 0 phát hiện |
| D3 cơ chế thực thi | Cổng cho ngoại lệ B2 = test quét `using` **đích danh** `RanhGioiPromotionsTests` (đỏ nếu mở rộng) — §10.2 :492; tên + phạm vi + "không wildcard" đã rõ. | 0 phát hiện |
| D4 SQL | Không có khối ` ```sql ` trong các mục vừa sửa; §5.5 (:342-363) không đổi. | 0 phát hiện (không có khối sql) |
| D5 tệp/symbol có thật | `IEventCoverageProbe.cs:17` có thật (đồ thị); `grep -rln "RanhGioiPromotionsTests" test modules` → **0** tệp — đúng là **kế hoạch** W24-2 (`§10.2` "Harness cần"), tài liệu **không** nói "đã có". | 0 phát hiện |
| D6 nhãn | §15 :603 có hàng `v0.4` mô tả đúng thay đổi. Header :5 và :18–20 vẫn chụp trạng thái cũ (SRS `v0.9`, "còn 2 P3"). | 1 phát hiện (#1) |
| D7 15 mục + mermaid | Đủ 15 mục; tệp không có Mermaid; F1 không thêm `;`/placeholder. | 0 phát hiện |

### Phát hiện (vòng 4)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | Header dòng 5, 12, 18–20; §12.3 dòng 539 | Header còn trích SRS `v0.9 — 23/11/2026` (SRS nay `v0.10`), "SRS v0.3–v0.9", và ghi "DOCREV … còn 2 P3 (số test case 54→55; trích số dòng cũ trong delta `pro-001` §6.1) … BA sửa ở F3" — cả hai P3 đó **đã đóng** (DoD **55**; hai dòng delta chính đã đổi sang tên mục; chỉ còn `pro-001` :477/:492). | Cập nhật header sang SRS `v0.10` + trạng thái DOCREV vòng 5, hoặc ghi rõ đây là ảnh chụp lúc `v0.3`; sửa §12.3 "đã đóng" cho khớp. |

### Không kiểm được (vòng 4)

- **D4:** không có khối ` ```sql `; các `CHECK`/partial index là mô tả, chưa chạy trên PostgreSQL 17 (module `Promotions` chưa có migration).
- **Test ranh giới `RanhGioiPromotionsTests`:** chưa có tệp vì module `Promotions` chưa tồn tại — việc của W24-2; đây là **kế hoạch**, không phải tuyên bố "đã có".

### Bằng chứng đồ thị (vòng 4)

- `graph: codegraph_explore "IEventCoverageProbe INotificationEvent AddressEvents PromotionAppliedEto namespace eCommerce.Messaging.EventCoverage"` → `IEventCoverageProbe` :17 trong namespace `eCommerce.Messaging.EventCoverage`, impl `EventCoverageProbe` :15, caller `ChannelShopSyncer` (calls `CoNguoiNhan`) ⇒ tên allowlist ở §2.1/§10.2 **đúng namespace thật**, không phải suy đoán.
- `Grep "namespace" IEventCoverageProbe.cs` → dòng 3 `namespace eCommerce.Messaging.EventCoverage;` — xác nhận lại bằng tệp (đồ thị có thể trễ).

## Vòng trước

### Vòng 3 (P1=0, P2=0, P3=1)

| # vòng 3 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 allowlist chỉ nói "`eCommerce.Messaging.*`" (wildcard) + thiếu tên test | **đã đóng** — §2.1 :101 + B2 :113 ghi đúng `eCommerce.Messaging.EventCoverage` (không wildcard); §10.2 :492 nêu `RanhGioiPromotionsTests`; §15 :603 hàng `v0.4` | P3 |

### Vòng 2 (P2=1, P3=4)

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 ngoại lệ `IEventCoverageProbe` chưa có trong bảng ranh giới | đã đóng — §2.1 :83/:96/:101 + B2 :113 + §6.2 :386 + §13 :563; tiền lệ `Sales.Application.csproj:16` xác nhận | P2 |
| 2 §1.1 bằng chứng khoá phân tán sai tệp | đã đóng — `NotificationDispatchWorker.cs:79` đúng `TryAcquireAsync` | P3 |
| 3 §5.5 "2 index có filter" | đã đóng — :357 "cả 4 đều có `WHERE`" | P3 |
| 4 §5.4 thiếu 4 cột timestamp | đã đóng — :338 khai đủ | P3 |
| 5 §14 dòng tự kiểm chung chung | đã đóng — :591 "quét từ khoá bỏ-ngỏ trên tệp = 0" | P3 |

### Vòng 1 (P1=1, P2=6, P3=6)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 §5.5 thiếu `UNIQUE` | đã đóng (vòng 2) | P1 |
| 2 §5.4 thiếu cột so SRS §5.2 | đã đóng (vòng 2) | P2 |
| 3 gộp `CR-1…CR-11` | đã đóng (vòng 2) | P2 |
| 4 `§7.4` không tồn tại | đã đóng (vòng 2) | P2 |
| 5 trích nhầm `:52-54` cho `HasConversion` | đã đóng (vòng 2) | P2 |
| 6 `PromotionAppliedEto` xếp fail-open | đã đóng (vòng 2) | P2 |
| 7 thiếu guard `OrderDiscount ≠ 0` | đã đóng (vòng 2) | P2 |
| 8 trích worker/khoá phân tán | đã đóng (vòng 3) | P3 |
| 9 thiếu cơ chế đỏ cho cấm song song / cờ tắt | đã đóng (vòng 2) | P3 |
| 10 dòng tự kiểm §14 | đã đóng (vòng 3) | P3 |
| 11 dải `TC-01…TC-69` | đã đóng (vòng 2) | P3 |
| 12 đếm `11 mã lỗi`/`10 quyền` | đã đóng (vòng 2) | P3 |
| 13 thứ tự xoá cây vs FK Restrict | đã đóng (vòng 2) | P3 |
