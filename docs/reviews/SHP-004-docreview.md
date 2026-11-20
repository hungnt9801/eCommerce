# Doc review — SHP-004 Adapter GHTK (xác minh địa chỉ 2 cấp trước)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/shipping/shp-004-adapter-ghtk.md` (v0.5 theo header :22 và §15.4 :655, 675 dòng) |
| Ngày · vòng | 21/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–675 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (0 lỗi: 16/16 heading, 18/18 BR có test, mục đối chiếu ở dòng 659; 5 dòng §15 `Open`, 0 TODO) · `npm --prefix tools run check:mermaid` → 211 khối, 0 lỗi (2 khối SHP-004 PASS: :102, :172) · `kiem-link-tai-lieu.ps1` → PASS, 0 tham chiếu chết (166 tệp, 768 link, 943 đường dẫn backtick) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Với từng ràng buộc cấm/chỉ của v0.5 (không NÉM sau `Submitting`, thứ tự cấu hình → địa chỉ → giới hạn → mô phỏng, `AddressModel` một nguồn, không khoá `Enabled`, không thêm metric/nhãn, assembly riêng `Carriers.Ghtk`): grep `NÉM\|ném` (51 chỗ — đọc từng chỗ: adapter chỉ NÉM ở `GetTrackingAsync` + lỗi lập trình; mọi chỗ khác là `SHP-001` NÉM trước khi gọi adapter), `0101\|0102\|0401` đối chiếu §1 bước 2–5 ↔ BR-013 ↔ §3 #1–#6 ↔ §4.1 ↔ §6.1 :259 ↔ §12 ↔ TC-03/08/15/19/29; `AddressModel`/`Enabled`/`IsEnabled` ↔ ADR-03, §5.2, §6.3 :320, TC-28; `operation` chỉ còn ở câu "không thêm" (:490, TC-18); đếm TC-03 = 7×3×3×2 = 126, trong đó ca thật sự thiếu cấp = (7+6+6)×3×2 = 114 ≥ 100 (NFR-002 đạt) | 4 P3 (#5, #7, #8, #9) |
| D2 chéo tài liệu / code / D-xx | Dòng gốc `D-128`, `D-129`, `D-130`, `D-131` (`decisions.md:142–145`); `shp-001` §6.1 :443–549 (grep `CarrierFeeResult` = chỉ chữ ký :456), §6.2 :579–586, §6.3 :635–649, §6.4 :655–684, §7.3 :750, §12 :930, §13 :981, `AddressModel` (:314, :453, :673), `FromAddress` (:507, :637, :674); `shp-002` :56, §6.1 :491–519, §6.2 :620/:625, §6.4 :645–669, §7.2 :720–721, §12 :897–901, §13 :945; `git status` (nhánh chỉ sửa `shp-004`, không sửa `shp-001`); code `eCommerceMetrics.cs:212–222`, `PaymentTransaction.cs:313–337` | 2 P2 (#1, #2) + 3 P3 (#3, #4, #11) |
| D3 cơ chế thực thi | 18 BR + 10 NFR + §8.1 11 bất biến → TC "Đỏ" ở cột Phủ; BR-017 → TC-25/26/27 (hash sai/`null`, IP ngoài allowlist, thiếu cấu hình); BR-018 → TC-29 (`Record.ExceptionAsync = null`) + TC-22; BR-010 → TC-12 (payload có SĐT/địa chỉ/tên, giá trị trong chuỗi tự do); BR-006 khi GHTK không có API tra | 1 P3 mới (#6) + 3 P3 còn từ vòng 1 (#14, #15, #17) |
| D4 SQL chạy được | `Grep` khối mã: `mermaid` (2), `csharp` (2), `jsonc` (1) — 0 khối `sql`/`powershell`/`bash`; `SHP-004` không có bảng (§5) | không có khối sql; 0 phát hiện |
| D5 test/probe/tệp có thật | `git log -- modules shared src test` ⇒ commit code mới nhất là N20 (trước vòng 1) ⇒ các `file:dòng` code đã mở ở vòng 1 giữ nguyên; mở lại `eCommerceMetrics.cs:212–222` (Allowed có `Provider`, `Reason`; Forbidden :219), `PaymentTransaction.cs:313,321–334` (khớp BR-010); `ls modules/` = 15 thư mục, không `ShippingHub`; grep `*.cs` `GhtkCarrier\|IShippingCarrier\|CarrierFeeResult\|WebhookHashToken\|WebhookAllowedIps` = 0; grep `Shipping` trong `src/eCommerce.Application.Contracts/Permissions` = 0; dòng dẫn tài liệu: `implementation-plan.md` (:202, :293, :324, :341, :440), `business-modules-proposal.md` (:511, :515, :548, :572, :660), `design-doc-guidelines.md` (:63, :138, :154, :170) | 1 P3 mới (#10) + 3 P3 còn từ vòng 1 (#12, #13, #16) |
| D6 nhãn tin cậy | Đi mọi ✅: §14.4 :598–604 (đo lại: không module, không permission Shipping — khớp; Q2/4/5/6/8 = `D-114`, Q9 = `D-128` — khớp); §0 ✅ là dấu phạm vi; §7.2 cột "Thuộc" đã bỏ ✅ (đóng vòng 1 #21); §15.2 F1 câu "chỉ có chuỗi `ghtk` trong test `Address`" vẫn sai (vòng 1 #18) | 0 phát hiện mới (#16 đếm ở D5) |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14: BR kiểm chứng được ✔ (18/18 có TC); NFR có ngưỡng ✔ (10/10); guard sai làm gì ✔ (§3 bảng #1–#6, có thứ tự); chống trùng cửa vào ✔ (§7.2: webhook do `SHP-002`, gọi nội bộ theo `ClientOrderCode`); index idempotency ✔ (UNIQUE của `SHP-001`/`SHP-002`); tên khoá ✔ (không khoá riêng, §8.2); tiền `decimal`/`Money` ✔; endpoint NCC ở cấu hình ✔; đếm ≠ dòng: không áp dụng (không UI); mục không áp dụng có lý do ✔ (§3, §5.1, §6.2); Mermaid ✔ (cổng 0 lỗi, không `;` trong text, placeholder `«»`). Mục "Đối chiếu mô hình Nhanh" :659–675: 13 hàng, có `D-08`, `D-69/70`, hàng "Không lệch" có lý do cho `D-03/06/07/17/18/42/27/31/45/47/48`; thêm `D-129`/`D-130`/`D-131` và thân đã sửa theo (Q10–Q12 `Closed`, E1/E7/ADR-13/§4.1/§6.1) | 0 phát hiện mới |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §6.1 :262; §15.1 Q10 :623; đối chiếu hàng `D-129` :669 | :262 ghi `CarrierFeeResult(Success, Fee, CurrencyCode, FailureCode, FailureKind, FailureMessage)` "khai ở SHP-001 §6.1 + bất biến theo D-129", nhưng `shp-001` §6.1 chỉ có chữ ký `Task<CarrierFeeResult> GetFeeAsync(...)` (:456) — grep `CarrierFeeResult` trong `shp-001` = 1 dòng, 0 record — và `git status` nhánh này không sửa `shp-001`. `D-129` ⑥ đòi BA `SHP-001` sửa §6.1/§7.3/§14 "cùng nhánh, cùng PR với SHP-004". Đường lỗi: PR này merge một mình ⇒ người code `SHP-001` (WP code đầu tiên, `D-129` ⑤) đọc `shp-001` :750 ("trả `IsEstimate` + `FailureReason`") và khai record theo đó ⇒ nhánh lỗi mềm của GHTK (E4, `TC-22`, BR-018) không có trường để trả `Transient`/`Configuration` | ORCH gọi BA `SHP-001` làm việc kèm `D-129` trong cùng PR (khai record + bất biến ở §6.1, nguồn `FailureReason` ở §7.3 :750, 4 ca §14); tới lúc đó BA `SHP-004` đổi :262 thành "⬜ chờ `SHP-001` §6.1 khai (việc kèm `D-129`)" |
| 2 | P2 | BR-017 :94; §6.1 :268; §6.3 :330–331; TC-25/26/27 :524–526 | Tên + nguồn cấu hình webhook GHTK lệch tài liệu chủ luồng webhook: `SHP-004` dùng `ShippingHub:Providers:ghtk:WebhookHashToken` + `WebhookAllowedIps` (appsettings); `SHP-002` §6.4 khai **cùng mục** `ShippingHub:Providers:ghtk` với `HashToken` + `IpAllowlist` (:657–658), ghi `IpAllowlist` là "ABP Setting theo hãng" (:669), và hợp đồng `WebhookVerificationContext` mang `HashToken` (:508); grep `HashToken\|IpAllowlist` trong `shp-002` = 25 chỗ. Đường lỗi: vận hành cấu hình theo `SHP-002` ⇒ `GhtkWebhookVerifier` đọc `WebhookAllowedIps` rỗng ⇒ BR-017 fail-closed `not_configured` cho **mọi** webhook GHTK (chỉ còn đối soát chủ động); hoặc cấu hình theo `SHP-004` ⇒ lớp kiểm chung của `SHP-002` thấy `IpAllowlist` rỗng ⇒ `0207` | Dùng đúng tên và nguồn của `SHP-002` §6.4 (`HashToken` secret ngoài repo, `IpAllowlist` ABP Setting theo hãng) trong BR-017, §6.1, §6.3, TC-25–27, BR-010 (danh sách che); hoặc đề nghị BA `SHP-002` đổi tên — một tên duy nhất cho cả hai tài liệu |
| 3 | P3 | §6.3 :320; Pre-Condition :89; TC-28 (a) :527 | `SHP-004` ghi "KHÔNG có khoá `Enabled`" cho `ShippingHub:Providers:ghtk`, nhưng `SHP-002` §6.4 :656 khai `"Enabled": true` trong cùng mục. TC-28 chỉ quét `GhtkOptions` nên không đỏ, người vận hành thấy hai câu trái nhau | Ghi rõ `Enabled` của `SHP-002` (nếu giữ) không phải cờ bật hãng (cờ duy nhất là `ShpCarriers.IsEnabled`), hoặc đề nghị BA `SHP-002` bỏ khoá này |
| 4 | P3 | §7.3 :363; §6.3 :316 | `GetTrackingAsync` dùng `BaseUrl` + timeout/retry chung của `SHP-001` §6.4, trong khi `SHP-002` §6.4 khai riêng cho `ghtk` `StatusApiBaseUrl`, `TimeoutSeconds`, `MaxRetries` (:660–662) — hai bộ khoá cho cùng lời gọi tra trạng thái | Chốt một bộ khoá cho đường tra trạng thái GHTK (theo `SHP-002` hoặc theo `SHP-004`) và ghi ở cả hai tài liệu |
| 5 | P3 | BR-017 :94; §6.3 :330 | BR-017 nói "`hash` khớp `WebhookHashToken` **của tenant theo path**", nhưng §6.3 chỉ có **một** `WebhookHashToken` cho cả provider (không theo tenant); `SHP-002` cũng tách "tenant theo path" thành một lớp riêng | Ghi rõ một token toàn hệ (tenant chỉ là định danh, `SHP-002` §15.3 #6) hoặc nêu nơi lưu token theo tenant |
| 6 | P3 | BR-017 :94; §6.1 :268–269; TC-25/26/27 :524–526 | `WebhookVerificationResult` có `FailureCode` (`shp-002` :514) và `SHP-002` đã cấp `0201` (IP), `0202` (hash), `0207` (chưa cấu hình) (:620, :625, :897–901); `SHP-004` chỉ quy định `FailureReason`, không nói `GhtkWebhookVerifier` điền `FailureCode` nào | Thêm vào BR-017 + TC-25–27 cặp (`FailureReason`, `FailureCode`): `ip_not_allowed`/`0201`, `bad_token`/`0202`, `not_configured`/`0207` |
| 7 | P3 | E7 :93; §6.2 :302–307; TC-29 :528 | E7 (hãng 4xx nghiệp vụ / thành công mà không có mã) chỉ nói "`Business` ⇒ `SHP-001` NÉM `0103`", không nói adapter đặt `FailureCode` gì. Theo `D-130` (2) `FailureCode` rỗng ⇒ `SHP-001` ghi `0103` **kèm log `Error` "lỗi lập trình của adapter"** ⇒ mỗi lần GHTK từ chối thành một lỗi lập trình giả; TC-29 đòi "`FailureCode` đúng bảng §6.2" nhưng bảng không có dòng cho ca này | Ghi ở E7 + §6.2 (dòng kế thừa) rằng adapter tự đặt `FailureCode = eCommerce:Shipping:0103`; thêm ca E7 vào TC-29 |
| 8 | P3 | §2 :134 (UC2 «include» UC1); §3 :154–163; BR-002 :94; TC-22 :521 | Sơ đồ nói tính phí bao gồm kiểm mã địa chỉ, nhưng không mục nào nói `GetFeeAsync` làm gì khi `CarrierFeeRequest` thiếu cấp bắt buộc (vd `ThreeLevel`, `ToDistrictCode = null`): bảng §3 chỉ cho tạo vận đơn, BR-002 chỉ nói `ToAddress`, TC-22 chỉ thử lỗi mạng/401. `D-129` đòi `Success = false` ⇒ `FailureCode` khác rỗng | Ghi nhánh thiếu cấp của `GetFeeAsync` (trả `Business` + `0102`, 0 HTTP, không NÉM) vào BR-002 và thêm ca vào TC-03 hoặc TC-22 |
| 9 | P3 | §7.3 :363; BR-016 :94; TC-18 :517 | Không nói **ai** tăng `shipping_carrier_call_failed_total`: §7.3 đặt metric ở cột "Nếu thất bại" của adapter và TC-18 đo bằng `MeterListener` trên adapter, trong khi `D-130` (3) và `D-131` (3) cho `SHP-001` tăng cùng metric cho cùng lỗi (và `shp-001` §12 :930 cho `Transient`) ⇒ có thể đếm đôi | Ghi một chủ phát metric (đề nghị: `SHP-001` sau khi nhận kết quả; adapter không phát) và sửa TC-18 theo đó |
| 10 | P3 | Header :17; §11 :441; §6.1 :241 | §6.1 đặt adapter ở **assembly riêng** `eCommerce.ShippingHub.Carriers.Ghtk` (`D-128` ⑤), nhưng header :17 chỉ liệt kê 4 project của `SHP-001` và §11 không có dòng cho project mới (csproj, thêm vào `eCommerce.slnx` — tệp một chủ ở luồng song song, CLAUDE.md §4 — tham chiếu từ Host) | Thêm vào §11 một dòng "Project mới `eCommerce.ShippingHub.Carriers.Ghtk` + `eCommerce.slnx` + tham chiếu Host — ⬜ kế hoạch WP code `SHP-004`"; sửa header :17 |
| 11 | P3 | ADR-03 :63; §5.2 :224; §6.1 :254 | `SHP-004` dựa vào "factory cấp `SupportedAddressModel` từ cột `ShpCarriers.AddressModel`", nhưng hợp đồng factory của `SHP-001` chỉ có `Get(string providerCode)` / `IsSupported` (`shp-001` :545–548), không nói đọc DB; `SHP-001` §6.4 còn đặt `AddressModel` trong options của `ghn` (:673). Đóng vòng 1 #7 phụ thuộc một hành vi `SHP-001` chưa ghi | Thêm F-item ở §15.2: "`SHP-001` factory cấp `SupportedAddressModel` từ `ShpCarriers.AddressModel` — BA `SHP-001` ghi vào §6.1" |
| 12 | P3 | Header :5–7, :18–19; §15.3 #3 :643 | (vòng 1 #11 — còn) Số dòng `docs/implementation-plan.md` vẫn trôi: `:427` → nay `:440`, `:199` → `:202`, `:290` → `:293`, `:338` → `:341`; `:200` (bảng kê thủ công) → `:203` | Cập nhật số dòng |
| 13 | P3 | Header :8–9; §0 :31; ADR-02 :62; ADR-03 :63; ADR-05 :65; BR-005/BR-017 :94; §6.3 :341; §7.2 :355; §10 :432; §12 :456; §14.3 :594; Header :21 | (vòng 1 #12 — còn) `business-modules-proposal.md` `:546` → `:548`, `:547` → `:549`, `:570` → `:572`, `:509` → `:511`, `:513` → `:515`, `:658` → `:660`; `design-doc-guidelines.md` `:134` → `:170`, `:118` → `:154`, `:102` → `:138` | Cập nhật số dòng |
| 14 | P3 | BR-009 :94; NFR-005 :95; TC-11 :510; fixture :567–575 | (vòng 1 #14 — còn) "Cấm `==` trên `decimal` tiền" không có máy kiểm: TC-11 quét `double`/`float`, fixture chỉ thử `Money.Equals` của kernel (không gọi adapter); so phí nội bộ là việc của `SHP-001` | Viết lại TC-11 theo việc adapter làm (COD `long` ra payload, phí hãng → `Money.Vnd`); chuyển "lệch phí ⇒ metric" về `SHP-001`; nêu cách chặn `==` nếu giữ |
| 15 | P3 | TC-21 :520; TC-02 :501 | (vòng 1 #16 — còn) TC-21 (huỷ bị từ chối) vẫn Phủ `BR-008` (không ghi DB/khoá/ETO) — không đo BR-008; TC-02 đo hành vi của `SHP-001` (NÉM `0102` từ cửa mã), adapter không tham gia | TC-21 phủ E6/A3/BR-018; TC-02 ghi là ca liên tài liệu hoặc trỏ `SHP-001` TC-34/46–48 |
| 16 | P3 | §15.2 F1 :631 | (vòng 1 #18 — còn) "(chỉ có chuỗi `ghtk` trong test `Address`)" vẫn sai: chuỗi `ghtk` có ở 8 tệp `*.cs` gồm `CarrierAddressMapping.cs`, `CarrierAddressMappingSynchronizer.cs`, `CarrierAddressSyncWorker.cs`, `Order.cs`, `IdempotencyModels.cs` | Sửa thành "không có class adapter; `ghtk` chỉ ở đồng bộ mã địa chỉ (`Address`) và chú thích" |
| 17 | P3 | BR-006 :94; §7.3 :365; §15.3 #3 :643 | (vòng 1 #22 — còn) Giả định 3 vẫn chỉ nói mất đối soát nếu GHTK không có API tra; không nói `CreateShipmentAsync` làm gì với bước "tra trước" (bước 6) và chưa khai cờ khả năng tra mà `SHP-001` A2 đòi | Thêm hệ quả cho BR-006 vào §15.3 #3 và khai cờ khả năng tra của adapter GHTK (mặc định "không tra được" tới Q1) |

## Không kiểm được

- D4: không có khối `sql`/`powershell`/`bash` trong tài liệu; `SHP-004` không có bảng ⇒ không có gì để chạy.
- Đúng/sai của payload, endpoint, giới hạn độ dài mã, quy tắc địa chỉ 2 cấp, cách GHTK cấp `hash` và dải IP webhook: chỉ hãng trả lời được (Q1, Q3, Q7 — R3-1); tài liệu đã ghi "chưa xác minh" và fail-closed.
- Việc kèm của `D-130`/`D-131` cho BA `SHP-001` (§3.2 #4, §4.1, §4.2, §12, §14) được `D-xx` cho phép làm sau ("orchestrator ghi vào handover") — không tính là phát hiện của `SHP-004`; `SHP-004` §4.1 :208 đã ghi rõ. Riêng `D-129` đòi "cùng PR" ⇒ #1.
- Quan sát của `D-130` (job thử lại `SubmitFailed` với lỗi tất định `0102`/`0401` tới trần, chặn `Generation + 1` khoảng `MaxSubmitAttempts` × `SubmitRetryPeriodSeconds`) là câu mở của `SHP-001`, không quyết ở đây; E1/E3 của `SHP-004` đúng với hợp đồng hiện tại.
- Không đọc câu trả lời của BA (đúng vai); #1, #2, #3, #4, #11 cần BA `SHP-001`/`SHP-002` cùng sửa — ORCH quyết người sửa.

## Vòng trước (chỉ vòng 2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — ADR-13, BR-018, E1/E3/E9 "trả", §3 #1–#3, §4.1, §12, TC-29; `D-130` chốt Pha 3 `SubmitFailed` + `Rejected` | P1 |
| 2 | đã đóng — §1 bước 2→5, BR-013 thứ tự cố định, §3, TC-15 (a)(c); `0` không bao giờ làm ngưỡng (ADR-06, BR-006) | P2 |
| 3 | đã đóng — `VerifyAsync` thuộc `SHP-004` ở §0/§6.1/§6.2/§7.2, BR-017, TC-25/26/27 (tên khoá lệch `SHP-002` ⇒ phát hiện mới #2) | P2 |
| 4 | đã đóng — `{provider,reason}` ở BR-016, §7.3, §13, TC-18 | P2 |
| 5 | đã đóng — `GetFeeAsync` lỗi mềm ở E4, §6.2, §12, TC-22; `D-129` (record ở `SHP-001` chưa khai ⇒ phát hiện mới #1) | P2 |
| 6 | đã đóng — assembly riêng `eCommerce.ShippingHub.Carriers.Ghtk` ở BR-001, §6.1, TC-01, TC-10, NFR-004 | P2 |
| 7 | đã đóng — `AddressModel` một nguồn (ADR-03, §6.3 :320, TC-28) (phụ thuộc factory `SHP-001` ⇒ phát hiện mới #11) | P2 |
| 8 | đã đóng — `FromAddress` ở §6.3, BR-002, BR-013, §3 #1, TC-03, TC-15 (d) | P2 |
| 9 | đã đóng — BR-010 hàm che riêng (tên trường + giá trị đã biết), NFR-007, TC-12 có SĐT/địa chỉ/tên | P2 |
| 10 | đã đóng — ADR-12 :72 "Đã chốt — `D-128`" | P3 |
| 11 | còn (đã chép lên bảng Phát hiện #12) | P3 |
| 12 | còn (đã chép lên bảng Phát hiện #13) | P3 |
| 13 | đã đóng — §6.2 :295 dùng route của `SHP-001` §6.2 :581–582 | P3 |
| 14 | còn (đã chép lên bảng Phát hiện #14) | P3 |
| 15 | đã đóng — TC-03 theory 126 ca (114 ca thiếu cấp thật ≥ 100) | P3 |
| 16 | còn (đã chép lên bảng Phát hiện #15) | P3 |
| 17 | đã đóng — hàng `D-109` :665 "⬜ kế hoạch (`ShipmentCodes.TaoClientOrderCode`, `SHP-001`)" | P3 |
| 18 | còn (đã chép lên bảng Phát hiện #16) | P3 |
| 19 | đã đóng — BR-002 có luật cho `Both` | P3 |
| 20 | đã đóng — `IsEnabled` là cờ duy nhất (Pre-Condition :89, §5.2, §6.3 :320) (lệch `SHP-002` ⇒ phát hiện mới #3) | P3 |
| 21 | đã đóng — §7.2 cột "Thuộc" không còn ✅ | P3 |
| 22 | còn (đã chép lên bảng Phát hiện #17) | P3 |
