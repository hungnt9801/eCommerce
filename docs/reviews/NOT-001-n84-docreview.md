# Doc review — «NOT-001» Template và hàng đợi thông báo (chống gửi trùng)

**VERDICT: DOC-OK**

**P1 = 0 · P2 = 0 · P3 = 5** — vòng 2 N84: 2 P2 của vòng 1 **đã đóng** (xác nhận ở mục "Vòng 2 (N84) — xác nhận"); 5 P3 còn lại **giữ nguyên** và **chấp nhận** (không ảnh hưởng hành vi, ghi ở bảng "Phát hiện").

> Ghi chú đường dẫn: ORCH yêu cầu một tên tệp có hậu tố `-n84`, nhưng hook vùng ghi của vai `docrev` chỉ cho `docs/reviews/*-docreview.md` ⇒ tệp này ghi tại `docs/reviews/NOT-001-n84-docreview.md` (tệp **mới**, **không** đè `docs/reviews/NOT-001-docreview.md` của vòng 4).

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` (`v0.6 — 26/11/2026`) |
| Ngày · vòng | 26/11/2026 · **N84 — kiểm nhanh W22-2** (chỉ các mục BA đã đổi: header · §0 · §3 hàng 5/8/14 · §4.1 · §5.1 · §5.5 · §6.1 · §6.2 · §7.1 · §8 · §11 · §12 · §14 (TC-01) · §15 · "Đối chiếu mô hình Nhanh") |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | `git diff` toàn tệp (mọi hunk BA đổi) + đọc trực tiếp: **1–135** (§header/§0/§1) · **188–307** (§3/§4.1) · **321–505** (§5.1–§5.5) · **528–814** (§6/§7/§8.1–§8.3) · **815–934** (§8.3/§9/§10/§11/§12) · **976–1056** (§14.1/đầu §14.2) · **1193–1272** (§15/đối chiếu Nhanh). Không đọc lại §2, phần còn lại §14.2–§14.3, §13 (ngoài phạm vi kiểm nhanh; mọi dòng đổi trong các mục đó đã thấy trong `git diff`) |
| Máy | `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/notifications/not-001-template-hang-doi-thong-bao.md -WP14` → **KET QUA: DAT** · 16/16 heading `## 0.`…`## 15.` · 25/25 BR ở §1 có test ở §14 · mục đối chiếu Nhanh dòng **1255** · §15 còn 6 dòng câu hỏi `Open` · 0 chữ TODO · `npm --prefix tools run check:mermaid` → **213 khối / 0 lỗi** |
| Đối chiếu | ARCH `docs/features/notifications/NOT-001-arch.md` (bản `DOC-OK`, tạm duyệt `D-266`) + `D-254`…`D-266` (`docs/nhanh/decisions.md:268–280`) |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc §7.1 (bảng ETO **và** ghi chú ngay dưới), §9 ⇄ §6.2, §5.1 ⇄ §15.1 Q5; grep `job bàn giao`, `Reports.View`, `NULL` = không hạn trên vùng đổi | **1 P3 (#4)**; P2 #1 vòng 1 **đã đóng** (xem "Vòng 2") |
| D2 chéo tài liệu / code / D-xx | đọc gốc `D-254`…`D-266` (`decisions.md:268–280`); đối chiếu ARCH §3.4/§3.5/§4.4/§4.5/§5.1/§7.2/§8/§15.2; đối chiếu code thật (metric, index, host module) | **2 P3 (#5, #6)**; P2 #2 vòng 1 **đã đóng** (xem "Vòng 2") |
| D3 cơ chế thực thi | với mỗi yêu cầu mới hỏi "cái gì đỏ": hàng 14 `ExpiresAtUtc IS NULL` (guard `HoanLaiChoGui` + `TC-05`); `Dispatching` (CHECK §5.5 + `TC-01`); `D-256` CẤM `IgnoreQueryFilters` (chỉ "grep phải = 0", **không** có ca §14) | **1 P3 (#7)** |
| D4 SQL chạy được | quét toàn bộ fence: 3 `mermaid` (dòng 136, 192, 248) + 2 `csharp` (528, 1030); **0** khối `sql`/`powershell`/`bash` | 0 phát hiện — **chưa chạy: không có khối lệnh** (không có gì để chạy trong `BEGIN…ROLLBACK`) |
| D5 test/probe/tệp có thật | mở đúng dòng các dẫn mới: `PostgreSqlSchemaTests.cs:215` (`Assert.Equal(94L, …)`), `:219` (`string[] phaiCo`); `MetricsTests.cs:44-45,55,72-73,100-103` khớp; `MetricEmitterTests.cs` có thật; `eCommerceHttpApiHostModule.cs:40,94,231,246,260`; `StockLedger.cs:240,243` | **1 P3 (#3)** — dẫn `:208`/"dòng 212+" lệch 7 dòng |
| D6 nhãn tin cậy | ✅ "**19** metric" đo được (`All` có **19** tên, `ChuaCoNoiPhat` rỗng); ✅ "test chốt **94** index" đo được (assert dòng 215); header vẫn `Draft` (không tự thành Approved) | 0 phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | #1 BR kiểm chứng được (`TC-01/05/30`) · #2 NFR có ngưỡng · #4 guard sai ghi rõ · #5 index idempotency · #6 tên khoá `notification:…` · #7 tiền `decimal` · #8 không endpoint NCC · #9 đếm ≠ hiển thị · #10 mục không áp dụng có lý do · #11–13 Mermaid không `;`/`<...>` | 0 phát hiện **mới** |

### Đối chiếu bắt buộc của vòng N84 — 3 câu hỏi của ORCH

- **TC-01 đúng 4 event + `DangBanGiao`? — ĐÚNG.** §14.1 dòng **982**: `Moi` → `ChoGui` → `DangBanGiao` → `DaBanGiao`; **4** `QueueEvent` (`Enqueued`, `ReadyToDispatch`, `Dispatching`, `HandedOff`); **1** lần gọi sender; phủ `D-265`. Khớp `D-265` ① ("happy path = 4 event + trạng thái trung gian `DangBanGiao`"). `Dispatching` phát ở (C2) trong **cùng** UoW với `UPDATE … WHERE Status = ChoGui` — đúng §3 hàng 5 dòng **222** + §4.1 dòng **283**.
- **§5.5 `FromStatus/ToStatus` có `int?` + CHECK? — ĐÚNG.** Dòng **492**: `int?` · `✓` (nullable), lý do "`Enqueued` không có trạng thái nguồn"; CHECK `EventType = Enqueued ⇒ FromStatus IS NULL`, event khác ⇒ NOT NULL (kể cả `Replayed` `FromStatus = ToStatus`). Dòng **504** lặp lại CHECK ở khối index. Khớp ARCH §3.5 ("`int?` — xem #4") + `D-261` ①.
- **Hàng 14 có vế `ExpiresAtUtc IS NULL`? — CÓ.** Dòng **231**: guard "**và** (`ExpiresAtUtc IS NULL` **hoặc** `now <= ExpiresAtUtc`)". Khớp ARCH §3.4 hàng 14 + `D-261` ①. Bù trừ với hàng 15 (`ExpiresAtUtc` **không** null) và hàng 9 — không còn lỗ #29 của vòng 4.

## Vòng 2 (N84) — xác nhận

BA đã sửa **chính SRS** (không phải tệp review này); DOCREV kiểm lại đúng 2 chỗ:

- **P2 #1 đã đóng — §7.1 dòng 697:** cột "Ai tiêu thụ" của `NotificationBatchApprovedEto` nay là `` `REP-*` (**chỉ**) `` (không còn "job bàn giao"), khớp ghi chú dòng 699 (job **KHÔNG** tiêu thụ ETO nào) + ARCH §4.5 + `D-263`. Cả 4 hàng ETO ở §7.1 nay chỉ rõ bên nhận; không còn hàng nào để job tiêu thụ.
- **P2 #2 đã đóng — §9:** đã **xoá** hàng permission `eCommerce.Notification.Reports.View` → `GetSummaryAsync` và bỏ `.Reports.View` khỏi danh sách permission ở §9; grep `Reports.View` toàn SRS chỉ còn **một** chỗ ở §6.2 dòng **657** — đúng câu "**chưa** tạo permission `Reports.View` khi chưa có endpoint" (khớp `D-255`, điều kiện (i) của `D-266`).

**Kết luận vòng 2:** `VERDICT: DOC-OK` · **P1 = 0 · P2 = 0** · **P3 = 5** (giữ nguyên #3–#7; **chấp nhận** vì đều là dẫn dòng/nhãn/câu chữ không đổi hành vi, và #5 là tàn dư phía ARCH còn #6 bị `D-264` ① loại trừ việc sửa SRS `NOT-001`).

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 3 | P3 | §11 dòng **903** · §0 dòng **59** · §8.1 dòng **785** · §14.4 dòng **1183** · §15.2 F4 dòng **1220** | Các dẫn `PostgreSqlSchemaTests.cs:208` / "dòng 208" / "dòng 212+" đều **lệch 7 dòng**: assert là `Assert.Equal(94L, …)` ở dòng **215** (SQL dòng 216), danh sách `string[] phaiCo` ở dòng **219**. Số **94** đúng; chỉ vị trí sai ⇒ người đi sửa `94 → 101` mở dòng 208 sẽ thấy dòng **comment**, không thấy assert. | `208–209` → `215–216`; "dòng 208" → "dòng 215"; "dòng 212+" → "dòng 219+". |
| 4 | P3 | §15.1 Q5 dòng **1203** (đối chiếu §5.1 dòng **345**) | Câu trả lời Q5 (cửa sổ **khử trùng**) ghi "`NULL` = không hạn", nhưng `CoalesceWindowSeconds` là `int` **NOT NULL** (§5.1) + CHECK `Purpose = 1 OR CoalesceWindowSeconds = 0`; `D-257` chỉ cho `DefaultExpiresAfterSeconds = NULL` = không hạn (thuộc **Q6**, dòng 1204 đã ghi đúng). Đọc Q5 có thể tưởng cột cho phép `NULL`. | Bỏ mệnh đề "`NULL` = không hạn" ở Q5 (giữ ở Q6), hoặc ghi rõ "cửa sổ khử trùng **không** có giá trị `NULL`". |
| 5 | P3 | §14.1 TC-01 dòng **982** ⇄ ARCH §7.2 dòng **471** + §13 dòng **587** | SRS đã đúng `D-265` (**4** event, có `Dispatching`), nhưng ARCH còn 2 câu cũ "`TC-01` … **3 event**". SRS **không** cần sửa; đây là tàn dư phía ARCH. Nếu QA đọc ARCH sẽ viết thiếu `Dispatching` ⇒ lệch SRS/`D-265`. | **ARCH** sửa 2 câu thành 4 event (SRS giữ nguyên); ghi nhận để BA/ARCH đồng bộ cùng PR `W22-2`. |
| 6 | P3 | §4.1 dòng **287** + §4.2 dòng **313** | Sequence/DFD vẫn ghi `SendAsync(…, Recipient)` / `SendRequest`, không khớp canonical `NotificationSendRequest(RecipientEncrypted, Subject, Body, …)` (ARCH §4.4, `D-264`). `D-264` ① nói rõ "**không** sửa SRS `NOT-001` ở hàng này" ⇒ **không bắt buộc** sửa; nhưng chữ "Recipient" (không phải `RecipientEncrypted`) dễ bị đọc là plaintext ở biên gửi. | Không bắt buộc theo `D-264`; nếu muốn chống hiểu nhầm, thêm chú thích "viết tắt — kiểu canonical ở ARCH §4.4 (`RecipientEncrypted`)". |
| 7 | P3 | §5.1 dòng **362** (`D-256`) | Yêu cầu **CẤM** `IgnoreQueryFilters()` nêu cơ chế "grep phải = 0" nhưng **không** có ca nào trong §14.1 (TC-27 là cách ly tenant 2 tenant; TC-44 quét "một nguồn"); hiện **không có gì đỏ tự động** nếu ai thêm `IgnoreQueryFilters`. | Thêm ca quét vào `TC-44` (hoặc TC mới) — P3 vì hôm nay 1 tenant và "grep" đã là cơ chế tĩnh được nêu. |

## Vòng 1 (N84) — 2 P2 đã đóng

Cột 2 là **trạng thái** (không phải "Mức") để script không đếm trùng; nội dung giữ để thấy cái gì đã đóng.

| # vòng 1 | Trạng thái | Mức lúc đó | Nội dung vòng 1 (vấn đề → đề nghị) | Kiểm vòng 2 |
|---|---|---|---|---|
| 1 | đã đóng | P2 | §7.1 dòng 697: cột "Ai tiêu thụ" của `NotificationBatchApprovedEto` còn "job bàn giao" — trái ghi chú dòng 699 / ARCH §4.5 / `D-263` → đổi "`REP-*` (**chỉ**)" | SRS dòng 697 nay "`REP-*` (**chỉ**)"; cả 4 hàng ETO sạch |
| 2 | đã đóng | P2 | §9 dòng 845 + 864: còn permission `Reports.View`/`GetSummaryAsync` — trái §6.2 dòng 657 + `D-255` → xoá permission | §9 không còn hàng permission + không còn `.Reports.View`; grep toàn SRS chỉ còn §6.2 dòng 657 (đúng nghĩa "chưa tạo") |

## Bằng chứng đồ thị (bắt buộc cho kết luận về code)

- `graph: codegraph_explore "eCommerceMetrics eCommerceMetricNames eCommerceMetricTags Allowed Forbidden ChuaCoNoiPhat MetricsTests"` ⇒ `eCommerceMetricNames.All` **:94–123** đếm **19** tên; `ChuaCoNoiPhat` **:189–200** rỗng; `eCommerceMetricTags.Allowed` **:238–242**; `Forbidden` **:245–248** ⇒ SRS "**19** metric / thêm 7 ⇒ **26**" và các dẫn `:245–248`, `:238–242` **đúng**.
- `graph: codegraph_explore "SendRequest NotificationSendRequest INotificationSender NotificationQueuedEto NotificationBatchApprovedEto"` ⇒ **not in the index** (module `Notification` **0 dòng code**) ⇒ không có DTO/entity thật để mâu thuẫn; các câu "kế hoạch GĐ3" là đúng hiện trạng.
- Đọc tệp xác nhận (Read): `test/eCommerce.IntegrationTests/PostgreSqlSchemaTests.cs` **:215** `Assert.Equal(94L, await DemAsync(`, **:216** SQL `pg_indexes`, **:219** `string[] phaiCo`; `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs` **:40** `[DependsOn(`, **:94** `TransactionBehavior`, **:231,246** `KeyPrefix = "ecommerce:"`, **:260** `AbortOnConnectFail = false`; `modules/Inventory/eCommerce.Inventory.Domain/StockLedger.cs` **:240,243** (APPEND-ONLY).
- `grep` + Read xác nhận có thật: `test/eCommerce.Tests/Observability/MetricsTests.cs:44-45,55,72-73,100-103` (so đúng tập `All` + chiều ngược hằng số), `MetricEmitterTests.cs` (khoá hai chiều `ChuaCoNoiPhat`), `LockKeyPrefixTests.cs:33` `TaxLockKeys_khong_chua_tien_to`.
- Đọc gốc quyết định: `docs/nhanh/decisions.md` dòng **268–280** (`D-254`…`D-266`) — `D-255` ① (bỏ permission `Reports.View`), `D-263` ① (job quét DB, cột "Ai tiêu thụ" = `REP-*`), `D-265` ① (**4** event), `D-261` ① (`int?` + CHECK + `ExpiresAtUtc IS NULL`).

## Không kiểm được

- **D4:** tài liệu **không có** khối ```` ```sql ````/```` ```powershell ````/```` ```bash ```` nào (chỉ 3 `mermaid` + 2 `csharp`) ⇒ **chưa chạy: không có khối lệnh**; Mermaid đã chạy qua cổng chung = **213 / 0 lỗi**.
- Module `Notification` **chưa tồn tại** trong code (graph: not in the index) ⇒ không đối chiếu được cột/DTO/entity thật; chỉ đối chiếu được với ARCH + `NOT-002` + `D-xx`.
- **#5 là tàn dư phía ARCH** (SRS đúng theo `D-265`); vai DOCREV **không** sửa ARCH — chỉ báo.
- **P3 #6** không phải lỗi bắt buộc: `D-264` ① nói rõ **không** sửa SRS `NOT-001` ở hàng đó; ghi nhận để tránh hiểu "Recipient" là plaintext.
