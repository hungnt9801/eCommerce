# Doc review — «NOT-001-arch» Kiến trúc liên module: template, hàng đợi, chống gửi trùng (WP-22, W22-1)

**VERDICT: DOC-OK** (vòng 2 — 0 P1 · 0 P2 · 1 P3; vòng 1 là `DOC-CHANGES` 0 P1 · 2 P2 · 5 P3)

| | |
|---|---|
| Tài liệu | `docs/features/notifications/NOT-001-arch.md` (686 dòng sau sửa — thiết kế kiến trúc MỚI cho module `Notification` chưa có code), nhánh `feat/gd3-wp22-thong-bao`, hàng `W22-1` |
| Ngày · vòng | 26/11/2026 · **vòng 2 — kiểm nhanh** (chỉ vùng đã đổi theo 2 P2 + 5 P3 của vòng 1) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N83) |
| Đã đọc | vòng 1: toàn văn 1–671. Vòng 2: tệp **chưa commit** (`git diff` rỗng) ⇒ đọc trực tiếp các vùng đổi: §1.1 dòng **30** · §1.4 dòng **82** · §3.1 dòng **133-143** · §3.4 hàng 3/5 dòng **167-169** · §4.4 dòng **284-303** · §7.2 dòng **443-472** · §14 #27 dòng **623** · §15.2 dòng **645-660** · mục tự kiểm dòng **686**. Đối chiếu SRS §3 hàng 5 (dòng 222), SRS §14.1 `TC-01` (dòng **965**), `NOT-002` §6.1 (dòng 511-541). |
| Máy | `check-feature-doc.ps1 -WP14` → exit **1** (3 `LOI`: thiếu `## 0.`/BR/"Nhanh") — **đúng chủng loại `-arch`** (header **16-18** đã nêu). `check-agent-artifact.ps1 -Kind docreview` → exit **0** (`verdict=DOC-OK · P1=0 · P2=0`). |
| Phạm vi | 7 mục vòng 1 (#1–#2 P2, #3–#7 P3) + tương tác trực tiếp của bản sửa; **không** mở lại toàn văn ngoài vùng đổi. |

## Vòng 2 — kiểm nhanh

### Đã kiểm (vòng 2)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc §7.2 **(C1)/(C2)** mới + §3.4 hàng 3/5; đối chiếu sự kiện `DanhDauSanSang`/`ReadyToDispatch`/`Dispatching`; đối chiếu record §4.4 mới | **1 phát hiện P3** (#1 vòng 2 = `TC-01` "3 event" vs hàng 5 `Dispatching` — kế thừa SRS); **không** còn mục `Moi` bị bỏ sót |
| D2 chéo tài liệu / code / D-xx | đối chiếu §4.4 canonical ↔ `NOT-002` §6.1 (dòng 511-541); `docDeviation #11`; `D-193` (đọc gốc dòng 207 — không có retry); `D-197` ①(2) | 0 phát hiện (P2 #2 đóng đúng hướng) |
| D3 cơ chế thực thi | (C1) race: 2 luồng cùng `Moi→ChoGui` ⇒ `WHERE Status=Moi` 0 dòng ⇒ bỏ qua (không NÉM); tương tác hàng 13 (`DaHuy` sau `23505` ⇒ `WHERE Status=Moi` = 0, không hồi sinh); kill giữa (C1)/(C2) ⇒ `ChoGui` ⇒ nhịp sau (C2) bàn giao | 0 phát hiện — bất biến "không mất việc, không gửi trùng" được nêu tường minh (dòng 471) |
| D4 SQL chạy được | quét fence vùng đổi: **0** khối `sql`/`powershell`/`bash` | **chưa chạy: không có khối lệnh** |
| D5 tên test/tệp/số dòng | `TC-01`/`TC-05` thật ở SRS §14.1; `NotationSendRequest` bỏ `TemplateCode`/`Data`; `NOT-002` §6.1 `SendRequest` **có** `Recipient` plaintext + `TemplateCode` + `Data` + `HandleCallbackAsync` — khớp mô tả `docDeviation #11` | 0 phát hiện |
| D6 nhãn tin cậy | đo lại: "10 hàm `ConfigureXxx` module" (đồ thị = 10 trực tiếp) đúng; `NotificationTemplateStatus` đủ 3 giá trị | 0 phát hiện |
| D7 14 quy tắc + Nhanh | #5 index idempotency không đổi; #6 tên khoá không đổi; `-arch` ngoài mẫu 16 mục (header 16-18) | 0 phát hiện **mới** |

### Đối chiếu bắt buộc — 7 mục vòng 1

| # vòng 1 | Trạng thái | Bằng chứng đã sửa (tệp:dòng) |
|---|---|---|
| #1 (P2) | **đã đóng** | §7.2 dòng **454-459** tách **(C1)** `Moi→ChoGui`: `UPDATE … SET Status=ChoGui … WHERE Id=@id AND Status=Moi → 1 dòng ⇒ insert QueueEvent(ReadyToDispatch) TRONG CÙNG UoW` · **(C2)** dòng **460-466** `UPDATE … WHERE Status=ChoGui` rồi gửi. Lý do (C1) dùng UPDATE có điều kiện thay hàm entity + xử lý đua + hàng 13 + kill giữa C1/C2: dòng **471**. §3.4 hàng 3 dòng **167** trỏ §7.2 (C1). **Hết** cảnh mục `Moi` bị `WHERE Status=ChoGui` bỏ qua. |
| #2 (P2) | **đã đóng** | §4.4 dòng **290-294**: `NotificationSendRequest(TenantId, QueueItemId, IdempotencyKey, ChannelKind, RecipientEncrypted, Subject, Body, Priority)` — **bỏ** `TemplateCode`/`Data`; dòng **300-302** chốt **canonical** + buộc `NOT-002` §6.1 đổi (nhận `RecipientEncrypted`+`Subject`+`Body` đã render; tách `HandleCallbackAsync`). `docDeviation #11` dòng **658**; §1.4 dòng **82** trỏ đúng. |
| #3 (P3) | **đã đóng** | §3.1 dòng **137** thêm `NotificationTemplateStatus` = `Draft=0 · Published=1 · Archived=2` (nguồn SRS §5.1, chỉ thêm cuối `D-17`). |
| #4 (P3) | **đã đóng** | §1.1 dòng **30**: "gọi **10** hàm `ConfigureXxx` của module (+ `ConfigureCommerce` 6; ABP tự cấu hình 9)" — khớp đồ thị (10 lời gọi module trực tiếp ở `:353-391`). |
| #5 (P3) | **đã đóng** | §14 #27 dòng **623**: "`NOT-002` sở hữu — **ADR-01 tách lớp · SRS §0**, `D-193` chỉ nói *chi phí kỳ*, **không** nói retry". |
| #6 (P3) | **đã đóng** | §4.4 dòng **292** đổi `BodyMasked` → **`Body`**; dòng **302** nêu rõ payload gửi = bản **đã lưu đã qua masker một cửa** (no-op với nội dung hợp lệ; secret lọt ⇒ cắt = fail-closed); cột `…Masked` chỉ để audit, **cấm** thêm trường `…Masked` vào DTO gửi. |
| #7 (P3) | **đã đóng** | §15.2 dòng **645-660** bỏ hàng "`Messaging`" khỏi bảng deviation; note dòng **660** nói rõ "**Không lệch — tuân thủ** `D-197` ①(2) (điều kiện 'khi cần' không kích hoạt)"; thêm hàng **#11** cho `NOT-002` §6.1. |

## Phát hiện (vòng 2)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §7.2 dòng **471** · §13.1 dòng **587** ↔ §3.4 hàng 5/6 dòng **169-170** · SRS §14.1 `TC-01` dòng **965** | Bản sửa #1 ghi "`TC-01` (**3** event: `Enqueued`/`ReadyToDispatch`/`HandedOff`)" nhưng §3.4 **hàng 5** (`ChoGui→DangBanGiao`) phát `Dispatching` và **hàng 6** (`DangBanGiao→DaBanGiao`) phát `HandedOff` ⇒ happy path (C1→C2) **thực tế 4** event (`+Dispatching`) và có trạng thái trung gian `DangBanGiao` mà `TC-01` ("Moi → ChoGui → DaBanGiao; 3 QueueEvent") **bỏ qua**. **Kế thừa SRS `TC-01`** (SRS đã DOC-OK) nên không phải lỗi của bản sửa #1, nhưng dòng 471 vừa liệt kê đúng 3 tên event nên nên chốt. Rủi ro: BE viết `TC-01` đếm 3 nhưng code phát `Dispatching` ⇒ test đỏ; hoặc bỏ `Dispatching` ⇒ thiếu vết audit (BR-006). | Ghi rõ ở §7.2 (C2)/§3.4 hàng 5: `Dispatching` **có** phát ở đường nóng hay **không**; nếu có ⇒ `TC-01` phải là **4** event; nếu không ⇒ thêm ghi chú ở hàng 5 như hàng 3 ("đường nóng không phát `Dispatching`, chỉ `HandedOff`"). Sửa `TC-01` (SRS) tương ứng cùng PR. |

> **0 P1/P2 mới.** 2 P2 (#1, #2) đóng đúng **bản chất** (không chỉ đổi câu chữ): #1 thêm bước tường minh + xử lý đua/hàng 13/kill; #2 chốt canonical + `docDeviation #11` buộc `NOT-002` đổi. 5 P3 (#3–#7) đóng đủ. Duy nhất **1 P3 kế thừa SRS** (`TC-01` event count) — không chặn.

## Vòng trước (vòng 1 — 0 P1 · 2 P2 · 5 P3)

Cột 2 là **trạng thái** (không phải "Mức") để script không đếm trùng. Nội dung vòng 1 giữ nguyên; kiểm vòng 2 ở bảng "Đối chiếu bắt buộc" phía trên.

| # vòng 1 | Trạng thái | Vị trí | Nội dung vòng 1 (vấn đề → đề nghị) |
|---|---|---|---|
| 1 | **đã đóng** | §7.2 dòng 449-451 ↔ §3.4 hàng 3 dòng 166 | Pha (C) chọn `(Moi\|ChoGui)` nhưng chỉ `UPDATE … WHERE Status=ChoGui`; `DanhDauSanSang` (Moi→ChoGui, `ReadyToDispatch`) không được gọi ⇒ mục `Moi` kẹt vĩnh viễn → thêm bước Moi→ChoGui tường minh. |
| 2 | **đã đóng** | §4.4 dòng 279-298, §2.2 B2, §1.4 ↔ `NOT-002` §6.1 | `INotificationSender` định nghĩa hai nơi khác nhau (ARCH: nội dung đã render, 1 method; NOT-002: `TemplateCode`+`Data`+`Recipient` plaintext + `HandleCallbackAsync`) → chốt canonical + docDeviation buộc NOT-002 đổi. |
| 3 | **đã đóng** | §3.1 dòng 133-141 | Thiếu enum `NotificationTemplateStatus` (Draft/Published/Archived) → thêm vào bảng enum. |
| 4 | **đã đóng** | §1.1 dòng 30 | "12 hàm `ConfigureXxx`" sai (thực 10 module trực tiếp) → sửa số. |
| 5 | **đã đóng** | §14 #27 dòng 611 | Dẫn sai `D-193` (chi phí kỳ) cho retry ownership → dẫn ADR-01/SRS §0. |
| 6 | **đã đóng** | §4.4 dòng 289-291 | `Subject` (không che) + `BodyMasked` (đã che) bất đối xứng trong DTO gửi → đổi `Body` + nêu rõ payload vs cột audit. |
| 7 | **đã đóng** | §15.2 #10 dòng 646 | "0 tham chiếu Messaging" xếp nhầm vào bảng "buộc khác" → chuyển ra note "tuân thủ D-197 ①(2)". |

## Bằng chứng đồ thị

- **Vòng 1** (giữ nguyên hiệu lực, mọi `file:dòng` vẫn khớp): `codegraph_explore "eCommerceDbContext OnModelCreating …"` → `OnModelCreating :353-391`, outbox/inbox `:47/:50`, `ConfigureEventOutbox/Inbox :389-390`; `codegraph_explore "eCommerceMetrics …"` → `All :94-124` (19 tên), `Allowed :238-242` (15), `Forbidden :245-248`; `codegraph_explore "eCommercePermissionDefinitionProvider …"` → `Define :23-25`; `PostgreSqlSchemaTests` `Assert.Equal(94L) :215-216`, foreach `:421-423`.
- **Vòng 2:** `graph: mcp__codegraph__codegraph_explore "eCommerceDbContext OutgoingEvents IncomingEvents OnModelCreating ConfigureEventOutbox ConfigureEventInbox"` → xác nhận lại `OutgoingEvents :47`, `IncomingEvents :50`, `ConfigureEventOutbox :389`, `ConfigureEventInbox :390` và **10** lời gọi `ConfigureXxx` module trực tiếp trong `OnModelCreating :353-391` ⇒ note §15.2 dòng 660 (outbox là hạ tầng ABP) và con số "10 hàm" của #4 **khớp code**.
- `grep` `NOT-002` §6.1 → `INotificationSender` `:511-525`, `SendRequest` `:527-535` (Recipient plaintext + TemplateCode + Data), `HandleCallbackAsync` `:520-524` ⇒ mô tả `docDeviation #11` **đúng**; `SRS §14.1 TC-01 :965` = "Moi → ChoGui → DaBanGiao; 3 QueueEvent" ⇒ nền của P3 #1 vòng 2.

## Không kiểm được

- **D4:** vùng đổi **không có** khối ```` ```sql ````/```` ```powershell ````/```` ```bash ```` ⇒ **chưa chạy: không có khối lệnh**. Migration `Added_Notification_Template_Queue` vẫn **chưa sinh** (0 migration ở W22-1), `NULLS NOT DISTINCT`/`UPDATE … WHERE Status=Moi` chỉ là kế hoạch.
- **Test thực thi:** `TC-*`/`NC-*` §13 vẫn là kế hoạch; module `Notification` chưa có code ⇒ chưa chạy `negative-control.ps1`.
- **`check:mermaid`:** ARCH không có sơ đồ Mermaid ⇒ N/A.
- **`git diff`:** tệp `NOT-001-arch.md` **chưa được commit** (`git status` = `??`) ⇒ không lấy được hunk; vòng 2 đọc trực tiếp vùng đổi (liệt kê ở header).

**VERDICT: DOC-OK** (vòng 2, 26/11/2026 — 0 P1 · 0 P2 · 1 P3 kế thừa SRS)
