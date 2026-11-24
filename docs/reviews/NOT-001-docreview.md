# Doc review — «NOT-001» Template và hàng đợi thông báo (chống gửi trùng)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` (`v0.4 — 24/11/2026`) |
| Ngày · vòng | 24/11/2026 · **vòng 3 — kiểm nhanh** (chỉ các mục BA đã đổi) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | toàn văn các **hunk đổi** (`git diff -U3`) của: header · §0 · §1 · §3 · §5.1–§5.6 · §6.1 · §6.2 · §7.1 · §7.4 · §12 · §14 · §15.4 · "Đối chiếu mô hình Nhanh" + đọc trực tiếp dòng **244–298, 723–782, 895–1010, 1132–1250**. Không đọc lại các mục không đổi giữa các hunk (phạm vi kiểm nhanh) |
| Máy | `check-feature-doc.ps1 -Path … -WP14` → exit **0** (ĐẠT) · 16/16 heading · **25/25** BR có test · có mục đối chiếu Nhanh (dòng 1233) · §15 còn **12** câu `Open` |
| Phạm vi | 5 P2 của vòng 2 (#13–#17) + tương tác trực tiếp của chúng; **không** mở lại toàn văn |

## Đã kiểm (vòng 3 — kiểm nhanh)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc vùng đổi, rồi grep các ràng buộc **cấm** mới (`0018` một nghĩa, `0017` cho lô không tồn tại, `Moi → ChoDuyet` **chỉ** trong `CreateBatchAsync`, mục bị khử trùng bucket `NULL`, mọi chuyển có **một** event) và đối chiếu chéo §1/§3/§5.4/§5.5/§6.2/§7.4/§12/§14 | 2 phát hiện (#24, #25) |
| D2 chéo tài liệu / code / D-xx | đọc gốc `D-197`/`D-198`/`D-199` (`docs/nhanh/decisions.md:211–213`); grep `NotificationChannelKind` trong `not-002…`; `codegraph_explore` + `Read` cho `eCommerceHttpApiHostModule.cs` | 2 phát hiện (#26; `D-197`/`D-198`/`D-199` áp **đúng** trong vùng đổi) |
| D3 cơ chế thực thi | 5 cơ chế mới → cái gì **đỏ**: pre-check + partial UNIQUE (`TC-30` **Integration**, có nhánh song song) · `QueuedForApproval` (`TC-05`) · `0017` (FK + guard) · enum (`TC-44` + §14.3 e) · hàng 14 (`TC-05`) · lô marketing (`TC-45`) | 1 phát hiện (#26 — nhánh `23505` không có test khẳng định mục `DaHuy`/event) |
| D4 SQL chạy được | quét ```` ```sql ```` / ```` ```powershell ```` / ```` ```bash ```` trên tài liệu | 0 phát hiện — **chưa chạy: không có khối lệnh** |
| D5 test/probe/tệp có thật | đối chiếu tên test/đường dẫn trong vùng đổi: `TC-30`/`TC-44`/`TC-45` có thật (§14.1); `not-002…:440,461,503` khớp `NotificationChannelKind`; đọc `eCommerceHttpApiHostModule.cs:218–255` + grep | 0 phát hiện **mới**; P3 #8 (vòng 2) **còn** trong vùng đổi |
| D6 nhãn tin cậy | `v0.4` + §15.4 khớp nội dung 5 sửa; rà nhãn ✅ trong mục "Đối chiếu mô hình Nhanh" | 0 phát hiện **mới**; P3 #20 (vòng 2) **còn** (dòng 1237) |
| D7 14 quy tắc + đối chiếu Nhanh | #1 BR kiểm chứng được · #5 index idempotency · #6 tên khoá · #9 đếm ≠ hiển thị · #11 Mermaid; mục Nhanh có thật + khớp `D-xx` | 3 phát hiện (#24, #27, #28); mục Nhanh **khớp** `D-197`/`D-198`/`D-199` + 2 dòng vòng 2 |

### Đối chiếu bắt buộc của vòng 3 — 5 P2 #13–#17

- **#13 lô không tồn tại ⇒ `0017`** · **đã đóng**: §3 hàng 2 (**dòng 218**) trả `0017` (`404`), ghi rõ `D-198` ①(3) **cấm** mở rộng nghĩa `0018`; §6.2 (**dòng 660**) `0018` còn **một** nghĩa ("lô **tồn tại** nhưng chưa duyệt; lô không tồn tại ⇒ `0017`"). Grep `0018` toàn tài liệu: chỉ còn nghĩa này (§1 E8, §12 dòng 908, TC-26 dòng 987). Khớp `decisions.md:212` ("không cần mã lỗi mới, không mở rộng nghĩa `0012`/`0018`").
- **#14 event cho `Moi → ChoDuyet`** · **đã đóng**: §3 hàng 2 side effect "`QueueEvent(QueuedForApproval)`" (dòng 218); `EventType` §5.5 (**dòng 479**) thêm `QueuedForApproval` + `RetryScheduled` **ở cuối** dải (`D-17`); §1 Post-Condition + TC-05 (dòng 966, "**14** hàng §3") nhất quán; đối chiếu 14 hàng ⇄ 13 `EventType`: **mọi** hàng có event, không hàng nào thiếu.
- **#15 khử trùng cửa sổ** · **đã đóng ở mức cơ chế**: §3 hàng 13 (**dòng 229**) chốt **pre-check** → mục `DaHuy` bucket `NULL` (ngoài partial index) + `Suppressed`; index chỉ là **backstop**; đua `23505` ⇒ `0019`. §5.4 (**dòng 440**, dòng 458), §7.4 L5 (**dòng 731**) và §12 (**dòng 909**) khớp; TC-30 (**dòng 991**) nay là **Integration**, có **cả** tuần tự **và** 2 luồng song song ⇒ đóng đúng yêu cầu "TC-30 Unit không bắt được xung đột DB". Hai chi tiết còn mờ: **#25** (bên thua có ghi mục + event không) và **#26** (UoW sau `23505`).
- **#16 một tên enum** · **đã đóng ở mức tài liệu**: §6.1 (**dòng 525–526, 574, 602**) chỉ dùng `ChannelKind`, có comment cấm enum thứ hai; ADR-14, NFR-018 (dòng 952), TC-44 (**dòng 1005**: "**1** enum `ChannelKind`, **0** enum `NotificationChannelKind`") và §14.3(e) (**dòng 1154**) đều nêu nợ đổi tên `not-002` **trong cùng PR** (grep: `not-002…:440,461,503` **vẫn** khai `NotificationChannelKind` — đúng như nợ đã ghi).
- **#17 `DangBanGiao → ChoGui` khi `NOT-002` lỗi tạm thời** · **đã đóng**: §3 **hàng 14 (dòng 230)** có guard + side effect `QueueEvent(RetryScheduled)` + `AttemptCount++` + `LastErrorCode`; phân biệt hàng 7; §12 (**dòng 915**), §5.4 (`AttemptCount` dòng 443), TC-05 (dòng 966) khớp. Nhánh **quá `ExpiresAtUtc`** của hàng 14 mở ra lỗi mới **#24**.

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 24 | P2 | §3 hàng 14 dòng 230 · §12 dòng 915 | Hàng 14 (mới) ghi "quá `ExpiresAtUtc` ⇒ đi hàng 9 (`HetHan`) thay vì thử lại", nhưng **hàng 9 là `ChoGui → HetHan`** — bảng **không** có cạnh `DangBanGiao → HetHan`. Làm theo (gọi hàm hết hạn khi mục đang `DangBanGiao`) ⇒ guard sai ⇒ **NÉM `0011`** (BR-005); nếu không gọi thì mục chỉ rời `DangBanGiao` khi lease hết hạn (hàng 7) ⇒ hành vi đã ghi không thực hiện được ngay. | Thêm hàng 15 `DangBanGiao → HetHan` (guard `now > ExpiresAtUtc`, event `Expired`) **hoặc** sửa câu thành "**không** thử lại; mục chờ hàng 7 đưa về `ChoGui` rồi hàng 9 chuyển `HetHan`" — và cập nhật TC-05. |
| 25 | P3 | §3 hàng 13 dòng 229 vs §12 dòng 909 | Nhánh **đua** khử trùng: hàng 13 nói bên thua "ghi mục `DaHuy` bucket `NULL` + `QueueEvent(Suppressed)` + `0019`"; §12 chỉ nói "trả `0019` + metric (**không** ghi mục gửi thứ hai)" ⇒ không rõ bên thua có ghi mục + event hay không; TC-30 (dòng 991) chỉ khẳng định `0019` + **1** mục gửi. | Chốt **một** hành vi ở cả hai chỗ (khuyến nghị: vẫn ghi `DaHuy` + `Suppressed` để có dấu vết) và thêm vế đó vào Expected của TC-30. |
| 26 | P2 | §3 hàng 13 dòng 229 · §7.4 L5 dòng 731 · §12 dòng 909 · TC-30 dòng 991 | Cơ chế "bắt `23505` rồi ghi mục `DaHuy` + event" **không nói** phải rollback transaction đã lỗi và mở **UoW mới**. Theo ngữ nghĩa PostgreSQL 17, sau lỗi trong transaction, lệnh ghi kế tiếp nhận `25P02` (*current transaction is aborted*) ⇒ mất mục/event `Suppressed` hoặc trả `500` thay vì `0019`; TC-30 chỉ kiểm `0019` + "1 mục gửi" nên **không** bắt được. | Ghi rõ: lần ghi thứ hai chạy trong UoW **mới** (`requiresNew`) sau khi UoW lỗi rollback; thêm assert "đúng **1** `QueueEvent(Suppressed)` trên mục `DaHuy`" vào TC-30. |
| 27 | P3 | §3 hàng 14 dòng 230 · §12 dòng 915 · §5.4 dòng 443–444 | Hàng 14 **giữ nguyên** `ScheduledAtUtc` (đã ≤ now) ⇒ nhịp 10 s lại bàn giao lại, **không** backoff/trần `AttemptCount`; với NCC cạn quota và `ExpiresAtUtc` dài (marketing đề xuất 72 h — Q6) ⇒ thử lại mỗi nhịp tới hạn; `AttemptCount` hiện chỉ dùng cho metric. | Nêu backoff tối thiểu (ví dụ `ScheduledAtUtc = now + min(2^AttemptCount × 10 s, cap)`) hoặc trần `AttemptCount` trước `HetHan`; hoặc ghi rõ "thử lại mỗi nhịp là **chủ ý**" + cảnh báo theo `AttemptCount`. |
| 28 | P3 | §4.1 sơ đồ dòng 285–291 | Sơ đồ tuần tự chỉ có nhánh "NOT-002 nhận" và "lỗi **vĩnh viễn**"; nhánh **lỗi tạm thời** (hàng 14 mới) không xuất hiện ⇒ người đọc sơ đồ (nguồn chính cho luồng) không thấy đường `RetryScheduled`. | Thêm nhánh `else «lỗi tạm thời»` → `ChoGui + QueueEvent(RetryScheduled) + AttemptCount++`, hoặc ghi chú trỏ §3 hàng 14. |

> 12 phát hiện P3 của vòng 2 **chưa sửa** (không ảnh hưởng verdict) được giữ trạng thái ở bảng "Vòng trước"; theo phạm vi **kiểm nhanh** của vòng 3 chúng **không** chép lại vào bảng trên. `#8`, `#10`, `#12`, `#18`, `#19`, `#20`, `#22` đã xác nhận **còn** bằng dòng cụ thể; `#7`, `#9`, `#11`, `#21`, `#23` **chưa kiểm lại** (ngoài vùng đổi).

## Không kiểm được

- **D4:** tài liệu **không có** khối ```` ```sql ````/```` ```powershell ````/```` ```bash ```` nào ⇒ **chưa chạy: không có khối lệnh**.
- **`25P02` (#26):** môi trường vai không có `psql` (exit 127) và không có `docker` (exit 127) ⇒ **chưa đo**; phát biểu dựa trên ngữ nghĩa PostgreSQL 17 + mô hình UoW của ABP (`TransactionBehavior.Auto`). ORCH chạy `psql` (dạng `BEGIN…ROLLBACK`) nếu muốn đo.
- **D7 mermaid:** `npm` bị hook của vai chặn ⇒ **chưa chạy** `check:mermaid`. Đã đọc tay 2 sơ đồ: §2 flowchart và §3 `stateDiagram-v2` (không `;` trong text, không placeholder `<...>`, dùng `«»`; cạnh `Loi → ChoGui` **đã bỏ**, nhãn `DangBanGiao --> ChoGui` nay gộp "lease hết hạn **hoặc** NOT-002 lỗi tạm thời" — khớp hàng 7 + hàng 14).
- Module `Notification` **chưa có trong code** ⇒ §5/§6 vẫn chỉ đối chiếu được với **tiền lệ** + `NOT-002`, không với entity/DTO thật.

## Bằng chứng đồ thị

- `graph: codegraph_explore "Notification module NotificationErrorCodes NotificationQueueItems NotificationBodyFormat ChannelKind eCommerceDbContext OutgoingEvents IncomingEvents"` ⇒ `NotificationErrorCodes` / `NotificationQueueItems` / `NotificationBodyFormat` / `ChannelKind` ***not in the index***; `eCommerceDbContext.OnModelCreating` chỉ gọi `ConfigureChannelHub/ConfigurePaymentHub/ConfigureTax/ConfigureMessaging/…/ConfigureEventOutbox/ConfigureEventInbox`, **không** có `ConfigureNotification` ⇒ câu "module chưa tồn tại, kế hoạch GĐ3" của tài liệu **đúng** (không có gì để mâu thuẫn ở #14/#16).
- `Read src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:218–255` + `grep TransactionBehavior|AbortOnConnectFail` ⇒ `KeyPrefix` dòng **222/237**, `AbortOnConnectFail` dòng **251**, `TransactionBehavior` dòng **94**. ADR-10 (vùng đổi) **đã sửa đúng**; §0 dòng 49/56, ADR-03 dòng 99, §7.4 dòng 735, §8.2 dòng 781, §12 dòng 912/922 vẫn dẫn số cũ ⇒ P3 #8 vòng 2 **còn**.
- `grep NotificationChannelKind docs/features/notifications/not-002…` ⇒ dòng **440, 461, 503** (đúng như nợ ghi ở §14.3 e) ⇒ #16 đóng ở mức tài liệu `NOT-001`, chuyển thành **việc của PR code**.
- Đọc gốc `docs/nhanh/decisions.md:211–213`: `D-198` ①(3) "không mở rộng nghĩa `0012`/`0018`" (khớp #13); `D-199` ①(4) "mã `0020` mở rộng dải `NOT-001` từ `0010–0019` (`D-197`) thành `0010–0020`" (khớp ADR-14/§6.2).

## Vòng trước (kết quả vòng 2 — 0 P1 · 5 P2 · 12 P3)

Cột 2 là **trạng thái** (không phải "Mức") để script không đếm trùng. Phát hiện **mới** của vòng 3 không nằm ở bảng này.

| # vòng 2 | Trạng thái | Mức lúc đó | Nội dung vòng 2 | Kiểm vòng 3 (tệp:dòng) |
|---|---|---|---|---|
| 13 | **đã đóng** | P2 | Lô không tồn tại lại trả `0018` | §3 hàng 2 dòng **218** (`0017`); §6.2 dòng **660** (`0018` một nghĩa); grep `0018` sạch |
| 14 | **đã đóng** | P2 | `Moi → ChoDuyet` không có event | §3 hàng 2 dòng **218** + §5.5 dòng **479** (`QueuedForApproval`); TC-05 dòng **966** |
| 15 | **đã đóng** | P2 | Khử trùng cửa sổ: hai cơ chế chồng nhau | §3 hàng 13 dòng **229**; §5.4 **440/458**; §7.4 L5 **731**; §12 **909**; TC-30 **991** (Integration + song song). Còn chi tiết: **#25**, **#26** |
| 16 | **đã đóng** | P2 | Hai tên enum `ChannelKind` / `NotificationChannelKind` | §6.1 **525–526/574/602**; TC-44 **1005**; §14.3(e) **1154**; `not-002` vẫn `NotificationChannelKind` (nợ đã ghi) |
| 17 | **đã đóng** | P2 | Không có cạnh cho "NOT-002 lỗi tạm thời" | §3 hàng 14 **230**; §12 **915**; §5.4 **443**; TC-05 **966**. Nhánh quá hạn: **#24** |
| 7 | còn (chưa kiểm lại — ngoài vùng đổi) | P3 | §11 dẫn `eCommerceMetrics.cs:75–78` cho `Allowed` | chưa đọc lại dòng 884 |
| 8 | **còn** (đã xác nhận) | P3 | Dẫn số dòng `eCommerceHttpApiHostModule.cs` sai | dòng **49** (`115–128`), **56** (`60–64`), **99**, **735** (`137–145`), **781** (`115–128`), **912** (`137–145`), **922** (`60–64`); code thật **222/237/251/94** |
| 9 | còn (chưa kiểm lại — ngoài vùng đổi) | P3 | `StockLedger.cs` dẫn đường dẫn cũ | chưa đọc lại |
| 10 | **còn** | P3 | Q11 `TenantId = NULL` còn `Open`; Basic Flow không nêu cách vượt query-filter | Q11 dòng **1189** vẫn `Open`; §1 Basic Flow bước 3 **không đổi** |
| 11 | còn (chưa kiểm lại — ngoài vùng đổi) | P3 | `0010` gộp hai nghĩa | chưa đọc lại dòng 650 |
| 12 | **còn** | P3 | Hàng 8 guard sai lại NÉM mã `NOT-002` (`0002`/`0007`) | §3 hàng 8 dòng **224** vẫn vậy |
| 18 | **còn** | P3 | Cùng test dẫn hai kiểu (`69–105` vs `86–105`) | §0 dòng **49** vẫn `69–105`; ADR-08 dòng **104** + §14.4 **1162** đã `86–105` ⇒ chưa thống nhất |
| 19 | **còn** | P3 | F9 dẫn "§6.3 tài liệu này" (không có §6.3) | F9 dòng **1205** vẫn "§6.3" |
| 20 | **còn** | P3 | Mục Nhanh ghi `NOT-001` có `DOC-OK` (sai — vòng 1/2 đều `DOC-CHANGES`) | "Đối chiếu mô hình Nhanh" dòng **1237** vẫn `DOC-OK` |
| 21 | còn (chưa kiểm lại — ngoài vùng đổi) | P3 | `ContentFingerprint` tính ở renderer thuần nhưng công thức cần `RecipientHash`/`TemplateVersionId` | chưa đối chiếu lại §5.4/§6.1 |
| 22 | **còn** | P3 | `FromStatus` NOT NULL nhưng event `Enqueued` không có trạng thái nguồn | §5.5 dòng **480** (đã sửa sang `Replayed`/`Suppressed` nhưng vẫn **không** nói `Enqueued`) |
| 23 | còn (chưa kiểm lại — ngoài vùng đổi) | P3 | `NOT-002` ghi `NOT-001` tiêu thụ `NotificationSentEto`; §7.2 không có | chưa đối chiếu lại §7.2 |

## Vòng 2 — báo cáo gốc (lịch sử, 24/11/2026)

**VERDICT (vòng 2): DOC-CHANGES** (0 P1 · **5 P2** · 12 P3)

| | |
|---|---|
| Tài liệu | `v0.3 — 24/11/2026` |
| Đã đọc | dòng 1–1245 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 · 16/16 heading · 25/25 BR · mục Nhanh có thật · 12 câu `Open` |

### Đã kiểm (vòng 2)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep ràng buộc cấm rồi đối chiếu §1/§3/§5.4/§5.5/§12/§14 | 4 phát hiện (#14, #15, #21, #22) |
| D2 chéo tài liệu / code / D-xx | đối chiếu §5↔§6↔§7, `not-002…`, `D-197`/`D-198`/`D-199` | 3 phát hiện (#12, #13, #16) |
| D3 cơ chế thực thi | 25 BR "phải/không được" → cái gì đỏ | 2 phát hiện (#15, #17) |
| D4 SQL chạy được | quét khối `sql`/`powershell`/`bash` | 0 phát hiện — không có khối lệnh |
| D5 test/probe/tệp có thật | đối chiếu mọi tệp/test/số dòng | 4 phát hiện (#7, #8, #9, #18) |
| D6 nhãn tin cậy | ✅/🔶/⬜ có số đo đi kèm? | 2 phát hiện (#7, #20) |
| D7 14 quy tắc + đối chiếu Nhanh | #1–#14 + mục Nhanh + đọc tay Mermaid | 4 phát hiện (#10, #11, #19, #23); **chưa chạy** `check:mermaid` |

### Bằng chứng đồ thị (vòng 2)

- `graph: codegraph_explore "ChannelEvents ChannelOrderPulledEto ChannelShopTokenRefreshedEto CorrelationId"` ⇒ `ChannelOrderPulledEto.CorrelationId` dòng **72**; `ChannelShopTokenRefreshedEto` dòng **116–125** **không** có ⇒ P2 vòng 1 #1 đóng đúng.
- `graph: codegraph_explore "TaxLockKeys LockKeyPrefixTests eCommerceMetrics eCommerceMetricTags Allowed Forbidden"` ⇒ `TaxLockKeys` trả `tax:…`; `Allowed` **212–216**; test `TaxLockKeys_khong_chua_tien_to` có thật.
- `graph: codegraph_explore "PostgreSqlSchemaTests NullsNotDistinct 93 phaiCo unique index"` ⇒ `Assert.Equal(93L, …)` dòng **208**; `string[] phaiCo` dòng **212**.
- `graph: codegraph_explore "MetricsTests MOI_hang_so_metric_PHAI_co_trong_All ChuaCoNoiPhat instrument"` ⇒ `All` dòng **81–107** (16 tên), `ChuaCoNoiPhat` **rỗng** (169–180).
- `graph: codegraph_explore "Notification module NotificationErrorCodes NotificationQueueItems NotificationTemplate NotificationChannelKind"` ⇒ tất cả **not in the index** ⇒ miền thông báo chưa có code.
- Kiểm bằng đĩa: `test/eCommerce.Tests` = **136** tệp; 6 tệp khuôn F11 tồn tại.

## Vòng 1 — báo cáo gốc (lịch sử, 24/11/2026)

**VERDICT (vòng 1): DOC-CHANGES**

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` |
| Đã đọc | dòng 1–1193 (toàn văn) |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (ĐẠT; 16/16 heading · 22/22 BR · có mục Nhanh; 13 câu `Open`) |

### Đã kiểm (vòng 1)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep "cấm"/"mọi … phải"/"không bao giờ" rồi đối chiếu §1/§3/§7.1/§8.1 | 3 phát hiện (#1, #2, #3) |
| D2 chéo tài liệu / code / D-xx | §5↔code, §6↔enum, `D-191`/`D-193` | 2 phát hiện (#4, #6); `D-193`/`D-191` áp đúng |
| D3 cơ chế thực thi | 22 BR → cái gì đỏ | 2 phát hiện (#2, #10) |
| D4 SQL chạy được | quét khối `sql` | 0 phát hiện — không có khối `sql` |
| D5 test/probe/tệp có thật | đối chiếu tên tệp/test | 4 phát hiện (#5, #7, #8, #9) |
| D6 nhãn tin cậy | ✅ có số đo đi kèm? | 2 phát hiện (#4, #5); **93 ✅**, **16 ✅** |
| D7 14 quy tắc + đối chiếu Nhanh | #1–#14 + mục Nhanh; chạy `check:mermaid` | 2 phát hiện (#3, #6); mermaid 0 lỗi |

### Đối chiếu bắt buộc (vòng 1)

- **`D-193`:** áp đúng — §0, BR-022, §5.4, §5.6, TC-40: chi phí kỳ, sổ đúng **8 cột**, cấm `OrderId`/JOIN bảng đơn.
- **`D-191`:** áp đúng nguyên tắc — template `cấm` PII thật; người nhận chỉ `RecipientEncrypted`+`RecipientHash`; ETO/log/metric 0 PII.
- **F8/F9:** không còn treo chặn code — `NotificationEnqueuedEto` + dải `0010–0019`.
- **Số index `93`/`100`** và **metric `16`**: khớp code.

### Bằng chứng đồ thị (vòng 1)

- `graph: codegraph_explore "eCommerceMetricNames eCommerceMetricTags eCommerceMetrics All Forbidden Allowed"` ⇒ `All` **16** tên (81–107); `ChuaCoNoiPhat` **rỗng**; `Allowed` **212–216**.
- `graph: codegraph_explore "PostgreSqlSchemaTests NullableTenantId unique index NullsNotDistinct 93 100"` ⇒ `Assert.Equal(93L, …)` dòng **208**.
- `graph: codegraph_explore "ChannelEvents ChannelOrderPulledEto ChannelShopTokenRefreshedEto CorrelationId"` ⇒ xem vòng 2.
- `graph: codegraph_explore "OutgoingEventRecord IncomingEventRecord OutboxInboxBatBuocTests MessagingDbContextModelCreatingExtensions outbox inbox"` ⇒ migration `AbpEventBusOutboxInbox` ⇒ F6 "✅ đã có" **đúng**.
