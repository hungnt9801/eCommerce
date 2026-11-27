# Doc review — NOT-002 Kênh gửi: email, SMS, Zalo OA

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md` (1074 dòng, v0.4) |
| Ngày · vòng | 24/11/2026 · **vòng 3** (kiểm nhanh — chỉ các mục BA đã đổi ở vòng 2) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | §0 (16–115) · §3 (173–287) · §4.2 (275–287) · §5/§5.1 (288–457) · §8 (654–713) · §9 (714–738) · §11–§12 (763–812) · §14.1–§14.3 (835–1004) · §15 (1004–1074). Không đọc lại §2/§6/§7/§10 (không đổi, không bị vòng 2 đánh dấu) |
| Máy | `check-feature-doc.ps1 -WP14` (vòng 2) → exit 0 · `check-agent-artifact.ps1 -Kind docreview` (vòng 3) → exit 0 |

> **Cập nhật 28/11/2026 — vòng 4 (kiểm nhanh sau khi BA sửa 2 phát hiện vòng 3):** xem mục "Vòng kiểm nhanh 28/11/2026" ở cuối tệp.

## Đã kiểm

| Nhóm | Cách kiểm (vòng 3 — trọng tâm mục đã đổi) | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | `grep` toàn tài liệu tìm dấu hiệu cũ: `5 bảng` / `ChoTai` / `Status IN (0,3)` = **0** khớp. Đọc lại các cặp câu dễ mâu thuẫn: số bảng §4.2 (:281) ↔ §5 (:297) ↔ §11 (:767) ↔ §14.3 (:981); NND §5.1 (:326/:327) ↔ §8.1 #1 (:660); chế độ suy giảm §0.2 (:75) ↔ §1 (:106) ↔ §15.3 (:1038) | 1 phát hiện (#2, P3) |
| D2 chéo tài liệu / code / D-xx | `graph: codegraph_explore "SalesDbContextModelCreatingExtensions IdempotencyKey PaymentTransaction ChannelCode ExternalOrderId"` → Sales index UNIQUE `(TenantId, ChannelCode, ExternalOrderId)` filter **:44–47 đúng**; `PaymentTransaction.IdempotencyKey` ở **:84** (tài liệu dẫn `:76` — **sai**). `graph: codegraph_explore "eCommerceDbContext OnModelCreating ConfigureChannelHub DbSet"` → `builder.ConfigureChannelHub()` gọi tại `eCommerceDbContext.cs:347` **đúng**. `ls modules/` = **15** module, **không** có `Notification` (đúng §0.1 :32). `Forbidden` ở `eCommerceMetrics.cs:218–223` **đúng**. Migration `20261024045237_AbpEventBusOutboxInbox.cs` có thật (:68–72 là `DropTable AbpEventInbox/AbpEventOutbox`) | 1 phát hiện (#1, P2) |
| D3 cơ chế thực thi | BR-002/ADR-03 "hàng rào cuối": §5.1 :326 UNIQUE `(TenantId, IdempotencyKey)` **NULLS NOT DISTINCT** + :327 unique filter `(TenantId, ProviderCode, ProviderMessageId)` **NND** + §8.1 #1 NND; đối chiếu `TenantId` nullable (§5.1 :304) ⇒ lỗ hổng "tin hạ tầng `TenantId=null` lọt trùng" vòng 2 **đã đóng**; BR-019 `UX_NotificationInbox_Dedup` NND (:374), BR-020 opt-out UNIQUE NND (:450) | 0 phát hiện |
| D4 SQL chạy được | `Grep '^```(sql\|powershell\|bash\|csharp\|mermaid)'` → vẫn **0** khối `sql`/`powershell`/`bash` (chỉ `mermaid` + `csharp` minh hoạ kế hoạch) ⇒ không có gì phải chạy trên PostgreSQL | 0 phát hiện |
| D5 test/probe/tệp có thật | Test vòng 2 thêm vào §14.1 có thật: **TC-46** (:886, phủ NFR-007) · **TC-47** (:887, phủ NFR-012); đếm `TC-01…TC-47` = **47** khớp §14.3 :974; §11 :767 liệt kê **6** bảng khớp §5 | 0 phát hiện |
| D6 nhãn tin cậy | Toạ độ as-built vòng 2 đã sửa đúng: `KeyPrefix` `:222,237` · `AbortOnConnectFail` :251 · health `:506–509` · UoW `:91–95`; migration outbox có thật; còn **1** toạ độ sai (ADR-03 `PaymentTransaction.cs:76`) | 1 phát hiện (#1, cùng D2) |
| D7 14 quy tắc + đối chiếu Nhanh | §14.3 DoD (:974) nay khớp TC-46/TC-47 (`NFR-007`/`NFR-012` được phủ); rà BR-001…024 + NFR-001…014 trong cột "Phủ" §14.1 = **đủ**; mục "Đối chiếu mô hình Nhanh (24/11/2026)" (:1057) có thật, khớp `D-193`/`D-197`/`D-199`/`D-200`…`D-207`; NFR có ngưỡng số; mermaid vòng 2 đã xanh | 0 phát hiện |

## Phát hiện (vòng 3 — cả 2 đã đóng ở vòng 4)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §0.2 ADR-03, dòng 83 | **Toạ độ as-built còn sót sai: `PaymentTransaction.cs:76` không phải `IdempotencyKey`.** Bằng chứng: `graph: codegraph_explore "…PaymentTransaction IdempotencyKey…"` → `IdempotencyKey` ở **:84**; dòng `:76` là `public string? FailureReason`. Đây là phần còn lại của P2 vòng 2 (#5 yêu cầu "cập nhật toạ độ … ADR-03"): các toạ độ khác đã sửa đúng (`KeyPrefix` :222,237 · `AbortOnConnectFail` :251 · UoW :91–95 · `ConfigureChannelHub` :347 · Sales index :44–47) nhưng chỗ này sót | Đổi `PaymentTransaction.cs:76` → `PaymentTransaction.cs:84` (hoặc dẫn theo tên symbol `PaymentTransaction.IdempotencyKey`, bỏ số dòng) |
| 2 | P3 | §1 Pre-Condition, dòng 106 (và §14.4 dòng 999) | **Dẫn tới nội dung nói ngược lại:** dòng 106 "`XC-002` … sẵn sàng — **nếu chưa, module chạy chế độ suy giảm (§15.3 giả định 1)**", trong khi §15.3 giả định 1 (dòng 1038) nay ghi "**ĐÃ TRIỂN KHAI** — `NOT-002` dùng trực tiếp, **không** cần chế độ suy giảm"; §0.2 dòng 75 cũng đã sửa thành "suy giảm chỉ còn là phương án dự phòng nếu có hồi quy". Câu "nếu chưa" thực tế không bao giờ kích hoạt (XC-002 đã có) | Sửa dòng 106/999 thành "**`XC-002` đã triển khai** — dùng trực tiếp; nếu hồi quy thì job quét DB vẫn giữ tin (§15.3 giả định 1)" cho khớp §15.3 |

## Không kiểm được

- **D4**: tài liệu không có khối `sql`/`powershell`/`bash` để chạy trên PostgreSQL (chỉ `mermaid` + `csharp` minh hoạ — thiết kế GĐ3).
- **Không chạy `dotnet test`**: 47 TC ở §14 là **kế hoạch** (module `Notification` chưa tồn tại — khớp F1).
- **Vòng 3 chỉ kiểm nhanh** các mục BA đã đổi ở vòng 2 + lân cận; không đọc lại §2/§6/§7/§10 (không đổi, không bị vòng 2 đánh dấu). Không phải kết luận toàn văn lần nữa.

## Vòng trước

### Vòng 1 → vòng 2 (giữ nguyên từ báo cáo vòng 2)

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — F2 (§8.2/F2) nay ghi `TaxLockKeys` đã bỏ tiền tố, mốc `0f2f286`; `graph: codegraph_explore "TaxErrorCodes TaxLockKeys…"` xác nhận :45–50 trả `tax:…` | P2 |
| 2 | đã đóng — F4 (§7.1/F4) nay ghi `ChannelOrderPulledEto` **đã có** `CorrelationId` (:72), chỉ `ChannelShopTokenRefreshedEto` (:117–125) còn thiếu; graph xác nhận | P2 |
| 3 | đã đóng — `Grep ChoTai` = 0; §3 hàng 1 = `[*] → ChoGui`, hàng 13 = `ChoGui → Huy`, khớp sơ đồ | P2 |
| 4 | đã đóng — 4 chỗ nay dẫn `eCommerceMetrics.cs:218–223`; graph xác nhận `Forbidden` :218–223 | P3 |
| 5 | đã đóng — §0.1 nay dẫn `PaymentTransaction.cs:313–337` (`otp` :323); `Grep` xác nhận :313/:323/:339 | P3 |
| 6 | đã đóng — §8.1 #7 nay dẫn `MetricsTests.cs:107+`; TC-07 ở dòng 108 | P3 |
| 7 | đã đóng — heading nay `### 0.1` (dòng 28) + `### 0.2` (dòng 77), không còn trùng số | P3 |
| 8 | đã đóng — §1 (dòng 112) nay đủ `NFR-NOT-002-001…014` | P3 |
| 9 | đã đóng — §14.2 comment nay `TC-25` + ghi chú "mã minh hoạ" cho thành viên ngoài `SendResult` (dòng 934–935) | P3 |

### Vòng 2 → vòng 3 (5 P2 + 3 P3 BA đã sửa)

| # vòng 2 | Trạng thái | Mức vòng 2 |
|---|---|---|
| 1 | **đã đóng** — §0.2 dòng 75 nay "`XC-002` (**đã triển khai** …) … chế độ suy giảm chỉ còn là phương án dự phòng nếu có hồi quy", khớp §0.1 :38 / F5 :1029 / §15.3 :1036 | P2 |
| 2 | **đã đóng** — §5.1 :326 UNIQUE `(TenantId, IdempotencyKey)` **NULLS NOT DISTINCT** + :327 unique filter `(TenantId, ProviderCode, ProviderMessageId)` **NND**; §8.1 #1 :660 NND | P2 |
| 3 | **đã đóng** — §4.2 :281 nay "**6 bảng**" + bổ sung `NotificationOptOuts`, khớp §5 :297 / §11 :767 / §14.3 :981 | P2 |
| 4 | **đã đóng** — §14.3 :974 ghi "**phủ hết 24 BR và 14 NFR** (`NFR-007` = `TC-46`, `NFR-012` = `TC-47`)"; TC-46 (:886) và TC-47 (:887) có thật | P2 |
| 5 | **còn — đã chép lên bảng Phát hiện (#1)** — phần lớn toạ độ đã sửa đúng (KeyPrefix :222,237 · `AbortOnConnectFail` :251 · health :506–509 · UoW :91–95 · `ConfigureChannelHub` :347 · Sales index :44–47) nhưng ADR-03 còn `PaymentTransaction.cs:76` sai (IdempotencyKey ở :84) | P2 |
| 6 | **đã đóng** — §1 Basic Flow bước 5 (:108) nay dẫn "(khoá ở §8.2)", bỏ "(ADR-08/BR-010)" | P3 |
| 7 | **đã đóng** — §1 Exception Flow E6 (:110) nay `ProcessStatus=Rejected` (không còn `Status=Rejected`) | P3 |
| 8 | **đã đóng** — §5.1 :328 partial index nay "`WHERE Status IN (ChoGui, ChoThuLai)` — dùng **hằng enum**, **không** chép số `0,3`" | P3 |

---

## Vòng kiểm nhanh 28/11/2026 (vòng 4)

**VERDICT: DOC-OK** — 2 phát hiện vòng 3 **đã đóng**; 0 P1; 1 P2 mới **ngoài** 2 fix (as-built lệch code sau v0.4).

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md` (v0.4, 1074 dòng) |
| Ngày · vòng | 28/11/2026 · **vòng 4** (kiểm nhanh — chỉ 3 dòng BA đã sửa: §0.2 ADR-03 :83 · §1 Pre-Condition :106 · §14.4 :999) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | 25–38 · 60–124 · 972–1044; `git diff` xác nhận đúng 3 dòng đổi (3 insertion / 3 deletion), không đọc lại toàn văn |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (`DAT`; 16/16 heading · 24/24 BR · mục đối chiếu Nhanh :1057) · `check-agent-artifact.ps1 -Kind docreview` → exit 0 (P2 máy đếm = 2 gồm 1 dòng lịch sử vòng 3 đã đóng) |

### Đã kiểm (vòng 4)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | `grep` toàn tài liệu `nếu chưa|chế độ suy giảm` — các chỗ liên quan `:75`, `:106`, `:999`, `:1029`, `:1038` đều nói **cùng một ý**: `XC-002` đã triển khai, dùng trực tiếp; hồi quy ⇒ job quét DB giữ tin. Không còn câu "nếu chưa, chạy chế độ suy giảm"; khớp còn lại duy nhất là tiêu đề bảng §15.1 "Ảnh hưởng nếu chưa trả lời" (vô hại) | 0 phát hiện |
| D2 chéo code / `D-xx` | `graph: codegraph_explore "PaymentTransaction IdempotencyKey FailureReason"` → `public string? IdempotencyKey { get; set; }` tại `modules/PaymentHub/eCommerce.PaymentHub.Domain/Entities/PaymentTransaction.cs:84` (**đúng** dẫn mới; dòng 76 **không** phải `IdempotencyKey`). Đối chiếu `D-197` (một module `Notification`) + `ls modules/` + `git log -- modules/Notification` | 1 phát hiện (#1, P2 — as-built cũ, **ngoài** 3 dòng sửa) |
| D3 cơ chế thực thi | 3 dòng sửa chỉ là điều kiện/văn bản, không đụng BR/unique index; UNIQUE `(TenantId, IdempotencyKey)` NND §5.1 :326–327 giữ nguyên | 0 phát hiện |
| D4 SQL chạy được | Tài liệu vẫn **0** khối `sql`/`powershell`/`bash` (chỉ `mermaid` + `csharp` minh hoạ) ⇒ không có gì phải chạy trên PostgreSQL | 0 phát hiện |
| D5 test/probe/tệp có thật | Toạ độ mới `PaymentTransaction.cs:84` xác nhận bằng graph; 3 dòng sửa không thêm tên test/probe/tệp mới | 0 phát hiện |
| D6 nhãn tin cậy | `XC-002` mang ✅ ở §0.1 :38 có bằng chứng (migration `20261024045237_AbpEventBusOutboxInbox.cs:68–72`, `DeadLetterAppService.cs:40`, `IdempotencyGuard.cs:50`) — nhãn khớp; 3 dòng sửa không đổi nhãn | 0 phát hiện (trừ #1) |
| D7 14 quy tắc + đối chiếu Nhanh | 3 dòng sửa không đụng NFR/BR/index/Mermaid; mục "Đối chiếu mô hình Nhanh (24/11/2026)" :1057 vẫn có và máy nhận | 0 phát hiện |

### Phát hiện (vòng 4)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §0.1 :32–34 · §14.4 :994 (cùng gốc §15.2 F1 :1025) | **As-built đã lệch code sau v0.4:** tài liệu ghi `modules/` có **15** module, **không** có `Notification`, "0 dòng code miền thông báo, 0 project", và DoR :994 đánh `[x] ✅ "module chưa tồn tại"`. Thực tế `modules/Notification/` có **4 project** (Domain/EF/Application/Contracts), **28 tệp tracked**, commit `7f23726` (**N84, 26/11/2026** — sau v0.4 24/11) với `NotificationDomainModule`/`NotificationErrorCodes`/`NotificationConsts`; `ls modules/` nay **17** module (thêm `Notification`, `Promotions`). Người làm WP-23 đọc §0.1/§14.4 dễ tưởng phải dựng module mới / tự đặt mã lỗi thay vì mở rộng module có sẵn (trái `D-197` "một module") | Cập nhật as-built: "`modules/Notification` **đã có** (N84/N86, 26/11/2026), NOT-001 đã code; NOT-002 còn là kế hoạch"; `15` → `17`; sửa/tick dòng DoR :994 cho khớp |

### Đã đóng (2 phát hiện vòng 3)

| # vòng 3 | Trạng thái | Bằng chứng vòng 4 |
|---|---|---|
| 1 (P2) | **đã đóng** | §0.2 ADR-03 :83 nay dẫn `PaymentTransaction.IdempotencyKey` (`PaymentTransaction.cs:84`); `graph: codegraph_explore …` xác nhận `IdempotencyKey` ở **:84**, không còn `:76` |
| 2 (P3) | **đã đóng** | §1 :106 và §14.4 :999 nay "**`XC-002` (outbox/inbox) đã triển khai** — dùng trực tiếp; nếu hồi quy thì job quét DB vẫn giữ tin (§15.3 giả định 1)", khớp §15.3 :1038 và §0.2 :75; `grep "nếu chưa"` không còn câu nói ngược |

### Không kiểm được (vòng 4)

- **D4**: không có khối `sql`/`powershell`/`bash` để chạy PostgreSQL.
- **Không chạy `dotnet test`**: 47 TC §14 là **kế hoạch**; vòng kiểm nhanh không chạy test.
- **Ngoài phạm vi**: không đọc lại §2/§6/§7/§10/§11–§13 (không đổi); phát hiện #1 là as-built lệch code **sau** v0.4, không do 3 dòng sửa.

```json
{
  "agent": "DOCREV",
  "doc": "docs/features/notifications/not-002-kenh-gui-email-sms-zalo-oa.md",
  "artifact": "docs/reviews/NOT-002-docreview.md",
  "round": 4,
  "verdict": "DOC-OK",
  "p1": 0,
  "p2": 1,
  "p3": 0,
  "sqlRun": "không có khối sql",
  "readRanges": ["25-38", "60-124", "972-1044"],
  "mustFix": ["#1 §0.1 :32–34 / §14.4 :994 — cập nhật as-built: modules/Notification đã có (N84/N86, 26/11/2026); NOT-002 còn kế hoạch"]
}
```
