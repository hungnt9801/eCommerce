# Doc review — PRO-002 Cây điều kiện AND/OR & engine khuyến mãi

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md` (v0.10, 1263 dòng) |
| Ngày · vòng | 27/11/2026 · **vòng 5** (F1b `N94` — kiểm nhanh các mục F1) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–20 (header) · 367–506 (§5.2–§6.1) · 504–599 (§6.1–§6.2) · 595–636 (§6.2–§6.4) · 667–711 (§7.1–§7.4) · 850–871 (§11) · 1012–1016 (`TC-51`, `TC-66`…`TC-69`) · 1116–1139 (§14.3) · 1219–1236 (§15.4 + đối chiếu Nhanh) · `pro-001-voucher-coupon.md` 452–500 (§6.1) · `PRO-002-arch.md` 1–26, 72–128, 474–603 |
| Máy | `check-feature-doc.ps1 -Path … -WP14` → exit 0 (16/16 heading, 20/20 BR có test, mục đối chiếu Nhanh dòng 1236, 14 câu `Open`) · `check-agent-artifact.ps1 -Kind docreview` → exit 0 |

**P1 = 0 · P2 = 0 · P3 = 4** (3 trong vế F1; #4 là citation cũ ngoài vế, ghi để không mất dấu).

Tóm tắt vòng 5 (kiểm nhanh, **không** đọc lại toàn văn): 5 `docDeviation` D1–D5 của `PRO-002-arch.md` §12.1 đã vào **đúng thân** SRS — §5.2 ghi chú enum chuỗi vs as-built int + `Currency char(3)`/`CHECK` + as-built `varchar(8)`; §6.1 ghi namespace `.Contracts` có chủ đích; §7.1 ETO tự khai trường + marker `INotificationEvent`/fail-closed; §7.2 #4 `OrderCancelledEto` chờ `SAL-004`; §11 có cờ tắt; §15.4 có hàng `v0.10` và DoD **55** khớp `grep -c '^| \*\*TC-'` = **55**. Mọi `file:dòng` as-built mới thêm đều **mở đúng dòng**. Không có P1/P2 mới.

## Vòng 5 (F1b N94 — kiểm nhanh)

### Đã kiểm (vòng 5)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep `IntegrationEventBase`/`INotificationEvent`/`IEventCoverageProbe`: §7.1 (:671-679) là chỗ **duy nhất** nói ETO; không còn câu "phải kế thừa" cũ. Grep `eCommerce.Promotions.Contracts`: §6.1 code (:507) + ghi chú (:573-575) cùng một namespace. §5.2 ghi chú "giữ chuỗi" (:401-405) nhất quán với `Kind`/`Status`/`DiscountKind` `varchar` (:382-393); `ProPromotionRedemptions.Status` vẫn `int` (:453) nhưng đó là enum **khác** (đã ghim ordinal, không mâu thuẫn). | 0 phát hiện |
| D2 chéo tài liệu / code / D-xx | As-built mở đúng dòng: `PricingDbContextModelCreatingExtensions.cs:113` = `CHECK "CurrencyCode" ~ '^[A-Z]{3}$'`; `:33-34` = `Scope`/`Status` `HasConversion<int>()`; `SalesDbContextModelCreatingExtensions.cs:21` = `Currency` `HasMaxLength(8)`; `NotificationDbContextModelCreatingExtensions.cs:41-43` = 3 `HasConversion<int>()`; `AddressEvents.cs:13-35` = `[Serializable] class AddressChangedEto` tự khai `EventId|OccurredOn|TenantId|AggregateId|AggregateVersion`, **không** base class; `INotificationEvent.cs:31` (`eCommerce.Kernel.Events`); `IEventCoverageProbe.cs:17` (`eCommerce.Messaging.EventCoverage`); `NotificationDtos.cs:4` = `namespace eCommerce.Notification`. `D-277` (a) tạm duyệt **giữ** D1–D5 ⇒ SRS không trái quyết định. | 1 phát hiện (#3 — tên setting) |
| D3 cơ chế thực thi | (a) D1/D2 là khác biệt kiểu cột ⇒ hàng rào = `CHECK` §5.3 (:472-479); (b) namespace `.Contracts` = chữ ký đã duyệt, không cần cổng; (c) ETO fail-closed: `IEventCoverageProbe.CoNguoiNhan(Type)` (:17) — tiền lệ thật `ChannelShopSyncer` gọi `CoNguoiNhan` rồi fail-closed (đồ thị dưới); (d) `OrderCancelledEto` hoãn consumer, SRS ghi thẳng **"chưa kiểm được"** (đúng — không giả lập ETO); (e) cờ tắt: "test cờ tắt ⇒ engine không được gọi" (§11 :865) + cơ chế đỏ ở ARCH §10.2 :491. | 0 phát hiện |
| D4 SQL chạy được | `grep -c '```sql'` trên tệp = **0**; các mục F1 không có khối `bash`/`powershell`. Biểu thức `CHECK`/`~`/unique partial là mô tả trong bảng, **chưa chạy** (module chưa có migration). | 0 phát hiện (không có khối sql) |
| D5 test/probe/tệp có thật | Đếm `grep -c '^| \*\*TC-'` = **55** hàng (§14.1); §14.3 :1118 cũng **55**. `OrderCancelledEto` **không** có trong `modules/`/`shared/` (khớp "chưa tồn tại"); `SAL-004` có định nghĩa (:84, :155). `IEventCoverageProbe`/`INotificationEvent`/`AddressEvents` có thật (đồ thị + grep). | 2 phát hiện (#2 `pro-001` còn số dòng; #4 `rep-001:442`) |
| D6 nhãn tin cậy | §15.4 (:1232) có hàng `v0.10`; hàng `v0.9` nay ghi **55** (đóng P3 #1 vòng 4); DoD :1118 = 55 khớp §14.1. Còn dòng **header** (:15) vẫn `v0.9`. | 1 phát hiện (#1) |
| D7 14 quy tắc + đối chiếu Nhanh | `check-feature-doc.ps1 -WP14` exit 0 (16 mục, 20/20 BR có test, "Đối chiếu mô hình Nhanh" dòng 1236). F1 **không** đụng Mermaid/BR/TC (`git diff` chỉ thêm blockquote + 2 ô bảng + 1 hàng §11 + hàng `v0.10`; hàng `v0.10` tự ghi "Không đổi BR/TC/hợp đồng" — khớp diff). | 0 phát hiện |

### Phát hiện (vòng 5)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | Header dòng 15 (và §15.4 dòng 1232; kéo theo `PRO-002-arch.md` dòng 5) | Ô "Phiên bản / ngày" vẫn `v0.9 — 23/11/2026` trong khi §15.4 đã có hàng `v0.10 — 27/11/2026`; ARCH vẫn trích SRS `v0.9`. Người duyệt đọc header sẽ tưởng bản cũ. | Cập nhật ô header thành `v0.10 — 27/11/2026` kèm một dòng tóm tắt F1; nhờ ARCH sửa chỗ trích `v0.9` ở đầu `PRO-002-arch.md`. |
| 2 | P3 | `pro-001-voucher-coupon.md` §6.1 dòng 477 (và dòng 492) | Hai dòng delta chính **đã** đổi đúng sang tên mục (`pro-002` §3.2, §6.2, §3.3, §6.4, §1 — đã đối chiếu heading hiện tại, ánh xạ đúng). Nhưng dòng 477 còn `pro-002:500`, dòng 492 còn `pro-002:530`/`pro-002:515` — vẫn là số dòng cũ, sẽ vỡ ở lần sửa sau. | Dòng 477 → `PRO-002 §6.1 quy tắc 1`; dòng 492 → `PRO-002 §6.3` (route `validate-voucher`) và `PRO-002 §6.1` (record `PromotionEvaluationResult`). |
| 3 | P3 | §11 dòng 865 | Cờ `eCommerce.Promotions.Enabled` đặt tên theo kiểu **permission** (`eCommerce.Promotions.*` — §9 :796-804; `PricingPermissions.cs:22-23`), không theo quy ước **setting** as-built `<Module>:…` (`PricingChinhSach.cs:15` `Pricing:RoundingPolicyCode`; `NotificationSettingKeys.cs:6` `Notification:EnableDispatch`; `AddressSettingDefinitionProvider.cs:9` `Address:Cache:TtlSeconds`) — dù chính ô này trích `PricingSettingDefinitionProvider.cs:16-37` làm khuôn. Tên literal được ghi rõ nên không sinh lỗi ngay, nhưng dễ bị cấu hình sai khoá lúc vận hành. | Đổi thành `Promotions:Enabled` cho khớp khuôn (và sửa ARCH §9.3 :468 / §10.2 :491), **hoặc** ghi rõ `eCommerce.Promotions.Enabled` là cố ý và nêu lý do khác quy ước setting. |
| 4 | P3 | §7.1 bảng ETO dòng 683 (ngoài vế F1 — ghi nhận) | Trích `REP-001 DiscountAmount — docs/features/reporting/rep-001-read-model-job-tong-hop.md:442`; hiện `rep-001` dùng `LineDiscount` (:434) / `DocumentDiscountTotal` (:351), còn dòng 442 là header bảng "Index & ràng buộc" — tên trường và số dòng đều không khớp. | Đổi thành `REP-001 §5.2.1 LineDiscount` (bỏ số dòng). |

### Không kiểm được (vòng 5)

- **D4:** không có khối fenced `sql`/`bash`/`powershell` trong các mục F1 (`grep -c '```sql'` = 0); biểu thức `CHECK`/`~`/unique partial §5.3 là mô tả, **chưa chạy** (module `Promotions` chưa có migration).
- **Hành vi phát ETO:** `PromotionAppliedEto`/`PromotionUsageLimitReachedEto`/`PromotionExpiredEto` **chưa có trong code** (đồ thị: không có symbol) ⇒ mọi khẳng định "mang dữ liệu ⇒ fail-closed", "2 ETO còn lại là thông báo" là **thiết kế**, chưa đo. SRS **không** có `TC` riêng cho đường "probe ném khi chưa có người nhận"; `TC-30` chỉ phủ outbox cùng UoW — QA nên bổ sung một ca khi có code (không tính P vì cơ chế đỏ "NÉM" đã nêu tường minh).
- `XC-002` §7.1/BR-020/TC-26 (:741-765, :1009) **vẫn** buộc `IntegrationEventBase`; SRS ghi đây là `docDeviation` D3 theo `D-277`. Không phải mâu thuẫn mới của SRS, nhưng `XC-002` cần một CR cập nhật — ngoài phạm vi vòng này.
- Vòng 5 là **kiểm nhanh**: không đọc lại toàn văn; các mục ngoài danh sách F1 giữ kết luận vòng 3/4.

### Bằng chứng đồ thị (vòng 5)

- `graph: codegraph_explore "IEventCoverageProbe INotificationEvent AddressEvents PromotionAppliedEto namespace eCommerce.Messaging.EventCoverage"` → `IEventCoverageProbe` :17 (`eCommerce.Messaging.EventCoverage`), impl `EventCoverageProbe` :15, **2 caller** `ChannelShopSyncer` (calls `CoNguoiNhan`); `INotificationEvent` :31 (`eCommerce.Kernel.Events`); `AddressChangedEto` tự khai trường. `PromotionAppliedEto` **không có trong index** ⇒ đúng "module `Promotions` chưa có code" — mọi phát biểu về nó là kế hoạch.
- `Grep namespace` trên 4 tệp được trích: `IEventCoverageProbe.cs:3` = `eCommerce.Messaging.EventCoverage` · `INotificationEvent.cs:1` = `eCommerce.Kernel.Events` · `AddressEvents.cs:1` = `eCommerce.Address.Events` · `NotificationDtos.cs:4` = `eCommerce.Notification` — khớp từng câu trong §7.1/§6.1.
- `sed` mở đúng dòng: `NotificationDbContextModelCreatingExtensions.cs:41-43` (3 `HasConversion<int>()`), `PricingDbContextModelCreatingExtensions.cs:33-34` (`Scope`/`Status` int) và `:113` (`CHECK … ~ '^[A-Z]{3}$'`), `SalesDbContextModelCreatingExtensions.cs:21` (`Currency` `HasMaxLength(8)`).
- `grep -rln "RanhGioiPromotionsTests" test modules` → **0** tệp: test ranh giới của ARCH §10.2 là **kế hoạch** W24-2, không phải "đã có" — khớp cách ARCH viết ("Harness cần").

## Vòng trước (vòng 4 — kiểm nhanh, 2 P3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 4 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — hàng `v0.9` §15.4 nay ghi DoD **55**; §14.3 :1118 = **55**; đếm `grep -c '^| \*\*TC-'` = **55** | P3 |
| 2 | còn một phần — hai dòng delta chính (vòng 4 :467/:469) đã đổi sang **tên mục** đúng; còn `pro-001` :477/:492 trích số dòng → đã chép lên bảng "Phát hiện" **#2** | P3 |

## Vòng trước (vòng 3 — 4 phát hiện bắt buộc + 5 P3)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Mức lúc đó để ở cột 3. -->

| # (v3) | Trạng thái | Mức lúc đó | Bằng chứng đóng (vòng 4) |
|---|---|---|---|
| 1 | đã đóng | P1 | §4.2 bước 2 (:244) — tự động = `Kind = 'Discount'`, `Voucher` chỉ qua `VoucherCode`, khử trùng theo `PromotionId`; A2 (1) (:103); §6.1 quy tắc 8 (:580); §5.2 `Kind` (:382); `TC-67` (:996) |
| 2 | đã đóng | P2 | §7.2 #5 (:682), §8.2 #5 (:743), bảng khoá (:771), `TC-50` (viii) (:993) — bỏ khoá idempotency **theo kỳ giờ**, job vẫn kích hoạt `StartsAtUtc` giữa hai lần chạy |
| 3 | đã đóng | P2 | §6.1 quy tắc 8 (:580) + §3.3 `LoiCauHinh` (:191) — `0001/0002/0004/0010` **NÉM ở mọi nguồn** (`D-154` (5)); §12 (:862) hàng rào trước deploy; `TC-68` (:997); `Q28` (:1175) mở, không chặn |
| 4 | đã đóng | P2 | `PromotionEvaluationRequest.CustomerGroupId`/`IsFirstOrder` có nguồn (:519-520); `PromotionRuleContext` định nghĩa (:558-559, "Định nghĩa context" :567); quy tắc 10/11 (:582-583); `Q27` `Closed — D-163` (:1174); `TC-69` (:998) |
| 5 | đã đóng | P3 | §14.3 nay ghi **55** test case (:1100) + giải thích dải `TC-52`…`TC-65` thuộc `PRO-001` |
| 6 | đã đóng | P3 | Basic Flow 1 (:102) + §4.2 bước 1 (:243) không còn "`Now` từ `Sales`"; engine lấy `IClock` một lần (§6.1 quy tắc 2 :574) |
| 7 | đã đóng | P3 | §0 :61 viết lại: phạm vi quầy `RetailBill` rõ ràng; khuyến mãi tự động cho `web`/`Sales` **trong** phạm vi |
| 8 | đã đóng | P3 | §6.1 quy tắc 4 (:576) kiểm `Mode` **hai chiều** (`VoucherCode ≠ null ⇔ Mode = CustomerRequested`) |
| 9 | còn (ngoài phạm vi) | P3 | delta `pro-001` :467,:469 vẫn trích số dòng cũ của `pro-002` — đã chép lên bảng "Phát hiện" **#2** |

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — `D-160` đã áp: A2 (2a)/(5), BR-11, §4.2 bước 7, enum `= 10`, `Subtotal` ở request (:517) + quy tắc 9, §12 :865, §13 "12 giá trị `0…11`", Q22/Q23 `Closed`, DoR :1123, `TC-48`/`TC-49`, hàng đối chiếu :1220 | P2 |
| 2 | đã đóng — candidate tự động hết lượt ⇒ bỏ qua + `UsageLimitHit`, không ném: Basic Flow 6, §3.3 :188, §4.1 :229, §4.2 :248, §12 :868, `TC-51` | P2 |
| 3 | đã đóng — quy tắc 8 (:575) chế độ theo nguồn candidate. BR-07, §7.4 #4, §12 :857/:872 viết theo đó. Có `TC-07` (c) | P2 |
| 4 | đã đóng — `UsageSequence` của dòng tự động ở Basic Flow 7, `TC-43` (iv)–(vi); vãng lai theo `D-161` | P2 |
| 5 | đã đóng — `promotion-expiry` kiêm kích hoạt: §7.2 #5, §8.2 #5, UC-06, §3.2 :171, `TC-50` (khoá theo kỳ giờ sinh vấn đề mới ⇒ #2 vòng này) | P2 |
| 6 | đã đóng — khoá lưu cây có `TreeVersion` (:673, :762), `TC-28` (f) | P2 |
| 7 | đã đóng — route `POST /api/app/promotion` (:608) | P3 |
| 8 | đã đóng — route theo quy ước ABP + 🔶 chưa đo (:609-617) | P3 |
| 9 | đã đóng — `TC-13` (a)–(d) là mã khách nhập, (a) đổi `Percentage` 100 % | P3 |
| 10 | đã đóng — §10.2 :813 lấy từ `EvaluateAsync` | P3 |
| 11 | đã đóng — dòng trống trước `**Idempotency key**` (:754) | P3 |
| 12 | đã đóng — record `PromotionBreakdownItem` (:525-530) | P3 |
