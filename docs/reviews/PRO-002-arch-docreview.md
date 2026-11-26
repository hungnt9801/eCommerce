# Doc review — PRO-002 «Kiến trúc module Promotions: cây điều kiện AND/OR & engine khuyến mãi»

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/PRO-002-arch.md` (v0.3, 15 mục) |
| Ngày · vòng | 27/11/2026 · **vòng 3** (kiểm nhanh mục vừa đổi) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc vòng 3 | §1.1, §1.2, §2.1, §2.2, §5.4, §5.5, §6.2, §13, §14; `Sales.Application.csproj`; `NotificationDispatchWorker.cs:66–97` |
| Máy | `check-agent-artifact.ps1 -Kind docreview` → exit 0 ("KET QUA: DAT"); `check-feature-doc.ps1 -WP14` **không áp** cho tệp `-arch` (đã ghi đúng ở §14 :589) |

**P1 = 0 · P2 = 0 · P3 = 1**

## Đã kiểm (vòng 3)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc lại §2.1/§2.2/§6.2/§13: bảng cho phép, sơ đồ, ngoại lệ B2 và câu "hiện không cần" cũ | 0 — câu cũ đã bị thay; 4 chỗ nói cùng một ngoại lệ, không lệch |
| D2 chéo SRS / code | `Sales.Application.csproj` có thật tham chiếu `Messaging.Domain`; `NotificationDispatchWorker.cs:79` là `TryAcquireAsync`; `IEventCoverageProbe` :17 (đã xác nhận vòng 2) | 0 |
| D3 cơ chế thực thi | ngoại lệ B2 có cổng "test quét `using`" — nhưng chưa đích danh tên test/namespace | 1 phát hiện (#1) |
| D4 SQL | không có khối ` ```sql `; §5.5 vẫn đúng cú pháp partial + NND | 0 |
| D5 tệp/symbol có thật | `NotificationDispatchWorker.cs:79`, `Sales.Application.csproj:16` | 0 |
| D6 nhãn | §14 :591 nay là phép đo ("quét từ khoá = 0"), không còn câu chung chung | 0 |
| D7 15 mục + mermaid | đủ 15 mục; không có Mermaid | 0 |

## Phát hiện (vòng 3)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §2.1 dòng 101 · §2.2 B2 dòng 113 · §10.2 | Cổng cho ngoại lệ `Promotions.Application → Messaging.Domain` mới chỉ nói "test quét `using` phải allowlist đúng một namespace `eCommerce.Messaging.*`": vừa là wildcard vừa là "một namespace", và test này **không** có tên/tệp trong §10.1–§10.2. Nếu allowlist đúng `eCommerce.Messaging.*` thì ngoại lệ mở luôn `eCommerce.Messaging.Idempotency`/`DeadLetter` — rộng hơn "**CHỈ** `IEventCoverageProbe`" | Ghi chính xác namespace `eCommerce.Messaging.EventCoverage` (không wildcard) và thêm một dòng §10.2 nêu tên test (vd `RanhGioiPromotionsTests`) khẳng định chỉ namespace đó được `using` |

## Bằng chứng đồ thị (vòng 3)

- `Read modules/Sales/eCommerce.Sales.Application/eCommerce.Sales.Application.csproj` :16 → `<ProjectReference Include="..\..\Messaging\eCommerce.Messaging.Domain\eCommerce.Messaging.Domain.csproj" />` ⇒ **tiền lệ "Sales.Application tham chiếu Messaging.Domain" là THẬT**, đúng như §2.1 :101.
- `Read modules/Notification/eCommerce.Notification.Application/Dispatch/NotificationDispatchWorker.cs` :79–80 → `await using var handle = await lockService.TryAcquireAsync(NotificationLockKeys.QueueDispatch(tenantId), TimeSpan.FromSeconds(5));` ⇒ bằng chứng khoá phân tán dùng thật đúng dòng, §1.1 :36 đóng.
- §2.1 :83 (sơ đồ), :96 (bảng cho phép, cấm `Messaging.Application`), :101 (ngoại lệ có chủ), §2.2 B2 :113, §6.2 :386, §13 :563 (`XC-002` E3) — **nhất quán**; không còn câu "hiện không cần" mâu thuẫn.
- §5.4 :338 khai đủ `ExpiresAtUtc/AppliedAtUtc/ReleasedAtUtc/ReversedAtUtc`; §5.5 :357 ghi "cả 4 đều có `WHERE`" — 2 P3 vòng 2 đóng.
- (Vòng 2) `codegraph_explore "IEventCoverageProbe EventCoverageProbe EventCoverage"` → interface :17, impl :15, caller `ChannelShopSyncer` — cơ chế có thật.

## Không kiểm được

- **D4:** tài liệu không có khối ` ```sql `; các `CHECK`/partial index là mô tả, chưa chạy trên PostgreSQL 17 (chưa có migration).
- **Test ranh giới `using`** (#1): chưa có tệp vì module `Promotions` chưa tồn tại — đây là việc của W24-2, ghi để BA/QA không quên.

## Vòng trước

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
