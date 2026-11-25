# Doc review — «NOT-001» Template và hàng đợi thông báo (chống gửi trùng)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/notifications/not-001-template-hang-doi-thong-bao.md` (`v0.5 — 26/11/2026`) |
| Ngày · vòng | 26/11/2026 · **vòng 4 — kiểm nhanh** (chỉ các mục BA đã đổi) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | `git diff -U2` toàn tệp để lấy hunk; đọc trực tiếp: header **1–13** · §3 sơ đồ + bảng + cảnh báo **188–240** · §5.4 (cột `CoalesceBucketStartUtc`/`AttemptCount`/`ExpiresAtUtc`/`DispatchLeaseExpiresAtUtc`/index/CHECK) **424–472** · §5.5 `EventType` **474–494** · §7.4 cả bảng L1–L5 + bảng idempotency key **725–749** · §12 toàn bộ **897–931** · §14.1 TC-05…TC-30 **961–1010** · §15.4 **1225–1235**. Không đọc lại các mục không đổi giữa các hunk (phạm vi kiểm nhanh) |
| Máy | `check-feature-doc.ps1 -Path docs/features/notifications/not-001-template-hang-doi-thong-bao.md -WP14` → exit **0** (ĐẠT) · **16/16** heading `## 0.`…`## 15.` · **25/25** BR ở §1 có test ở §14 · mục đối chiếu Nhanh dòng **1237** · §15 còn **12** dòng câu hỏi `Open` · 0 chữ TODO |
| Phạm vi | 2 P2 của vòng 3 (**#24**, **#26**) theo `D-208` mục (6) (`docs/nhanh/decisions.md:222`) + tương tác trực tiếp của 2 sửa; **không** mở lại toàn văn; P3 cũ ngoài vùng đổi giữ trạng thái |

## Đã kiểm (vòng 4 — kiểm nhanh)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc vùng đổi rồi grep toàn tài liệu các từ khoá `hàng 9` · `hàng 15` · `ExpiresAtUtc` · `không hạn` · `requiresNew` · `25P02` · `23505`; so cặp guard hàng 14/15 và đối chiếu hàng 7/9/12 + §5.4 + §12 + §14 | **1 phát hiện** (#29, P3) — không còn câu "quá `ExpiresAtUtc` ⇒ đi hàng 9" cho mục `DangBanGiao`; không có cạnh trạng thái không tồn tại; `Expired` có trong `EventType` §5.5 dòng **481** |
| D2 chéo tài liệu / code / D-xx | đọc gốc `D-208` mục (6) (`decisions.md:222`); đối chiếu `not-002…:108` (việc thử lại do `NOT-002` sở hữu); đối chiếu cặp hàng 13/§7.4 L5/§12/TC-30 | 0 phát hiện (2 P2 #24/#26 **đóng đúng hướng `D-208`**; module `Notification` chưa có code ⇒ không có gì mâu thuẫn) |
| D3 cơ chế thực thi | với mỗi hành vi mới hỏi "cái gì đỏ": hàng 15 (guard sai ⇒ `0011`, `TC-05` khẳng định `Expired` + `AttemptCount` không tăng) · nhánh đua `23505` (`TC-30` Integration khẳng định **đúng 1** `QueueEvent(Suppressed)` trên mục `DaHuy`) | 0 phát hiện **mới** |
| D4 SQL chạy được | quét toàn bộ fence: 3 khối `mermaid` (dòng 136, 192, 248) + 2 khối `csharp` (517, 1013); **0** khối `sql`/`powershell`/`bash` | 0 phát hiện — **chưa chạy: không có khối lệnh** (không có gì để chạy trong `BEGIN…ROLLBACK`) |
| D5 test/probe/tệp có thật | đối chiếu từng tên trong vùng đổi: `TC-05` (dòng **969**) và `TC-30` (dòng **994**) có thật ở §14.1; `D-208` có thật ở `docs/nhanh/decisions.md:222`; `not-002…:108` = **Basic Flow** của `NOT-002` (bước 1 `SendAsync`, bước 6–7 timeout + "retry có giới hạn", lỗi tạm thời ⇒ `ChoThuLai` + backoff) | **0 phát hiện** |
| D6 nhãn tin cậy | header `v0.5 — 26/11/2026` (dòng **11**) ⇄ dòng `v0.5` ở §15.4 (dòng **1233**); trạng thái header vẫn 🟡 `Draft` (dòng **9**) — **không** bị đổi thành Approved | **0 phát hiện** |
| D7 14 quy tắc + đối chiếu Nhanh | đọc tay cạnh Mermaid **mới** `DangBanGiao --> HetHan : «quá ExpiresAtUtc khi NOT-002 lỗi tạm thời»` (dòng **203**): không `;` trong text, dùng `«»`, không placeholder `<...>`; #1 (BR kiểm chứng được qua `TC-05`), #5 (index idempotency không đổi), #6 (tên khoá `notification:queue-dispatch:{tenantId}` không đổi), #9 (đếm ≠ hiển thị không liên quan) — ORCH đã chạy `npm --prefix tools run check:mermaid` = **213 khối / 0 lỗi** (không chạy lại trong vai) | 0 phát hiện **mới** |

### Đối chiếu bắt buộc của vòng 4 — 2 P2 #24 / #26

- **#24 cạnh `DangBanGiao → HetHan`** · **đã đóng, nhất quán 6 chỗ**: sơ đồ §3 dòng **203** có cạnh; bảng §3 **hàng 15** (dòng **232**) guard `NOT-002` lỗi tạm thời **và** `now > ExpiresAtUtc` (`ExpiresAtUtc` **không** null) → `QueueEvent(Expired)` + metric, `AttemptCount` **không** tăng, guard sai ⇒ **NÉM** `0011`, "**phân biệt hàng 9** (mục đang `ChoGui`)"; **hàng 14** (dòng **231**) nay ghi "quá `ExpiresAtUtc` ⇒ **không** đi hàng 14 — job gọi **hàng 15** (`HetHan`) thay vì thử lại"; §12 **dòng 917** ("quá `ExpiresAtUtc` ⇒ `HetHan` (**hàng 15 §3** — **không** thử lại)") + **dòng 919** ("**hàng 9** nếu đang `ChoGui`; **hàng 15 §3** nếu đang `DangBanGiao`"); `TC-05` (dòng **969**) đếm **15** hàng và khẳng định hàng 15 phát `Expired`, **không** tăng `AttemptCount`; `EventType` §5.5 dòng **481** có `Expired`. Grep `hàng 9`: chỉ còn dòng **226** (chính hàng 9) và **919** (đúng ngữ cảnh `ChoGui`) ⇒ **không còn** câu "quá `ExpiresAtUtc` ⇒ đi hàng 9" cho mục `DangBanGiao`; **không** có cạnh trạng thái không tồn tại. Bù trừ guard hàng 14 `now <= ExpiresAtUtc` ⇄ hàng 15 `now > ExpiresAtUtc` nhất quán (trừ trường hợp `NULL` — phát hiện mới **#29**). Hàng 12 (cấm lùi) không bị ảnh hưởng: `DangBanGiao`/`ChoGui` đều **không** thuộc tập hấp thụ.
- **#26 nhánh đua `23505` trong UoW MỚI** · **đã đóng, nhất quán 5 chỗ**: §3 **hàng 13** (dòng **230**) "bên thua bắt `23505` ⇒ **rollback** UoW đã lỗi rồi ghi **lần hai trong UoW MỚI `requiresNew`** (mục `DaHuy` bucket `NULL` + **đúng 1** `QueueEvent(Suppressed)`) ⇒ `0019` — bên thua **CÓ** ghi mục + event … dùng lại UoW đã abort ⇒ PostgreSQL 17 trả `25P02`"; §7.4 **L5** (dòng **733**) cùng cơ chế; §12 **dòng 911** cùng cơ chế + "bên thua **CÓ** ghi mục + event; **không** ghi mục gửi thứ hai"; "Chiến lược transaction" dòng **926** nêu ngoại lệ **duy nhất** + **cấm** dùng lại transaction đã abort; `TC-30` (dòng **994**) khẳng định `0019` + mục `DaHuy` có **đúng 1** `QueueEvent(Suppressed)` (ghi trong UoW mới sau rollback) + **1** mục gửi. Không còn chỗ nào chỉ nói "trả `0019` + metric" mà bỏ mục/event của bên thua. Logic đếm đúng: event `Suppressed` của lần ghi đầu **bị rollback** ⇒ sau lần ghi thứ hai còn **đúng 1** (không phải 2). `requiresNew` là tham số có thật của `IUnitOfWorkManager.Begin` (ABP) — không phải API tự chế.
- **#25 (P3 vòng 3) đóng kèm**: dòng **911** nay chốt "bên thua **CÓ** ghi mục + event" (vòng 3 chỉ có "trả `0019` + metric, **không** ghi mục gửi thứ hai" ⇒ không rõ).

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 29 | P3 | §3 hàng 14 dòng **231** · hàng 15 dòng **232** · hàng 9 dòng **226** · §5.4 dòng **447** · §12 dòng **917** | Cặp guard bù trừ **không phủ `ExpiresAtUtc IS NULL`** (tin "không hạn" — `ExpiresAtUtc` nullable, dòng 447 "`null` = không hạn"): hàng 15 ghi rõ "`ExpiresAtUtc` **không** null" + "null ⇒ job **không gọi** hàm hết hạn", hàng 9 dòng 226 cũng có vế null, nhưng **hàng 14 (đường thử lại) không có vế null** ⇒ mục `DangBanGiao` **không hạn** + `NOT-002` lỗi tạm thời không khớp **hàng 14** (`now <= NULL` không thoả) lẫn **hàng 15** (`now > NULL` không thoả) ⇒ chỉ no-op tới khi lease hết hạn (hàng 7) đưa về `ChoGui`, trong khi §12 dòng 917 nói lỗi tạm thời ⇒ `ChoGui` **ngay**. Không mất dữ liệu (lease cứu) nên P3. | Thêm vế vào guard hàng 14: "**và** (`ExpiresAtUtc` **null** **hoặc** `now <= ExpiresAtUtc`)" (tin không hạn ⇒ vẫn thử lại), **hoặc** ghi rõ ở hàng 14 "`ExpiresAtUtc` null ⇒ job no-op + chờ lease (hàng 7)" — chọn **một** và đối chiếu lại `TC-05`. |

> P3 **còn** (ngoài vùng đổi, **không** mở lại theo phạm vi kiểm nhanh): **#27** (hàng 14 không backoff/trần `AttemptCount` — dòng **231** vẫn "giữ `ScheduledAtUtc` cho nhịp sau"), **#28** (§4.1 không có nhánh lỗi tạm thời — dòng **246–296** không nằm trong `git diff`), và 12 P3 của vòng 1/2 ghi ở bảng "Vòng 2 — báo cáo gốc" bên dưới (trạng thái **giữ nguyên như vòng 3**).

## Không kiểm được

- **D4:** tài liệu **không có** khối ```` ```sql ````/```` ```powershell ````/```` ```bash ```` nào (chỉ 3 `mermaid` + 2 `csharp`) ⇒ **chưa chạy: không có khối lệnh**.
- **`25P02`:** **chưa đo lại** (vai chỉ đọc; phiên này không dùng `psql`/`docker`). Tài liệu nay **đã ghi đúng** hệ quả và cách tránh (UoW mới `requiresNew`), nên đây không còn là phát hiện; nếu muốn đo, ORCH chạy `psql … -c "BEGIN; <câu lỗi>; <câu ghi>; ROLLBACK;"` để thấy `25P02`.
- **D7 mermaid:** không chạy lại trong vai — dùng số đo của ORCH (`check:mermaid` = **213 khối / 0 lỗi**); đã đọc tay cạnh mới (dòng **203**).
- Module `Notification` **chưa có trong code** ⇒ §5/§6 vẫn chỉ đối chiếu được với **tiền lệ** + `NOT-002`, không với entity/DTO thật.

## Bằng chứng đồ thị (vòng 4)

- `graph: codegraph_explore "NotificationQueueItems NotificationQueueEvents NotificationErrorCodes ChannelKind ConfigureNotification module Notification"` ⇒ **not in the index**: `NotificationQueueItems`, `NotificationQueueEvents`, `NotificationErrorCodes`, `ChannelKind`, `ConfigureNotification`; không file/ký hiệu nào trong index dùng các tên này ⇒ module `Notification` **chưa tồn tại**; câu "kế hoạch GĐ3" của tài liệu **đúng** và không có code thật để mâu thuẫn với hàng 15 / `EventType` mới.
- `Read docs/nhanh/decisions.md:220–224` ⇒ `D-208` mục (6) đúng là điều kiện "**Trước WP code:** BA sửa **2 P2** — #24 (thêm **hàng 15** … event `Expired`) + #26 (UoW mới `requiresNew` + assert "đúng **1** `QueueEvent(Suppressed)`" vào `TC-30`)" ⇒ bản `v0.5` sửa **đúng phạm vi** được giao.
- `grep` trên `not-002-kenh-gui-email-sms-zalo-oa.md` ⇒ dòng **108** là bảng **Basic Flow** (§1) có `INotificationSender.SendAsync` + "retry có giới hạn" + lỗi tạm thời ⇒ `ChoThuLai` + backoff ⇒ tham chiếu `not-002…:108` **có thật và đúng nghĩa**.

## Vòng trước (kết quả vòng 3 — 0 P1 · 2 P2 · 3 P3)

Cột 2 là **trạng thái** (không phải "Mức") để script không đếm trùng. Bảng phát hiện vòng 3 được chép đủ nội dung ở đây (kèm trạng thái vòng 4); không lặp lại bảng riêng ở mục lịch sử vòng 3.

| # vòng 3 | Trạng thái | Mức lúc đó | Nội dung vòng 3 (vấn đề → đề nghị) | Kiểm vòng 4 (tệp:dòng) |
|---|---|---|---|---|
| 24 | **đã đóng** | P2 | Hàng 14 dẫn "quá `ExpiresAtUtc` ⇒ đi hàng 9" nhưng không có cạnh `DangBanGiao → HetHan` ⇒ guard sai **NÉM** `0011` → thêm hàng 15 hoặc sửa câu | hàng 15 dòng **232**; sơ đồ **203**; hàng 14 **231**; §12 **917/919**; `TC-05` **969**; `Expired` **481**; grep `hàng 9` sạch |
| 25 | **đã đóng** (đóng kèm #26) | P3 | Không rõ bên thua `23505` có ghi mục `DaHuy` + event hay không → chốt một hành vi + thêm vế vào `TC-30` | hàng 13 **230**; §12 **911** ("bên thua **CÓ** ghi mục + event"); `TC-30` **994** |
| 26 | **đã đóng** | P2 | Nhánh bắt `23505` ghi lần hai phải chạy trong **UoW mới `requiresNew`** sau rollback (nếu không PostgreSQL 17 trả `25P02` mất mục/event hoặc `500`); thêm assert "đúng 1 `Suppressed`" | hàng 13 **230**; §7.4 L5 **733**; §12 **911**; chiến lược transaction **926**; `TC-30` **994** |
| 27 | **còn** (ngoài vùng đổi) | P3 | Hàng 14 giữ nguyên `ScheduledAtUtc` ⇒ thử lại mỗi nhịp, không backoff/trần | hàng 14 dòng **231** vẫn "giữ `ScheduledAtUtc` cho nhịp sau" |
| 28 | **còn** (ngoài vùng đổi) | P3 | §4.1 sequence thiếu nhánh "lỗi tạm thời" | §4.1 dòng **246–296** không nằm trong `git diff` |

## Vòng 3 — báo cáo gốc (lịch sử, 24/11/2026)

**VERDICT (vòng 3): DOC-OK** (0 P1 · 2 P2 #24/#26 · 3 P3 #25/#27/#28)

| | |
|---|---|
| Tài liệu | `v0.4 — 24/11/2026` |
| Đã đọc | `git diff -U3` các hunk đổi: header · §0 · §1 · §3 · §5.1–§5.6 · §6.1 · §6.2 · §7.1 · §7.4 · §12 · §14 · §15.4 · "Đối chiếu mô hình Nhanh" + đọc trực tiếp dòng **244–298, 723–782, 895–1010, 1132–1250** |
| Máy | `check-feature-doc.ps1 -WP14` → exit **0** · 16/16 heading · 25/25 BR · mục Nhanh dòng 1233 · §15 còn 12 câu `Open` |
| Phạm vi | 5 P2 của vòng 2 (#13–#17) + tương tác trực tiếp |

### Đã kiểm (vòng 3)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep các ràng buộc **cấm** mới (`0018` một nghĩa, `0017` cho lô không tồn tại, `Moi → ChoDuyet` **chỉ** trong `CreateBatchAsync`, mục bị khử trùng bucket `NULL`, mọi chuyển có **một** event) rồi đối chiếu §1/§3/§5.4/§5.5/§6.2/§7.4/§12/§14 | 2 phát hiện (#24, #25) |
| D2 chéo tài liệu / code / D-xx | đọc gốc `D-197`/`D-198`/`D-199` (`decisions.md:211–213`); grep `NotificationChannelKind` trong `not-002…`; `codegraph_explore` + `Read` cho `eCommerceHttpApiHostModule.cs` | 1 phát hiện (#26); `D-197`/`D-198`/`D-199` áp **đúng** |
| D3 cơ chế thực thi | 5 cơ chế mới → cái gì **đỏ**: pre-check + partial UNIQUE (`TC-30` **Integration**) · `QueuedForApproval` (`TC-05`) · `0017` (FK + guard) · enum (`TC-44` + §14.3 e) · hàng 14 (`TC-05`) · lô marketing (`TC-45`) | 1 phát hiện (#26 — nhánh `23505` không có test khẳng định mục `DaHuy`/event) |
| D4 SQL chạy được | quét ```` ```sql ````/```` ```powershell ````/```` ```bash ```` | 0 phát hiện — **chưa chạy: không có khối lệnh** |
| D5 test/probe/tệp có thật | `TC-30`/`TC-44`/`TC-45` có thật (§14.1); `not-002…:440,461,503` khớp `NotificationChannelKind`; đọc `eCommerceHttpApiHostModule.cs:218–255` | 0 phát hiện **mới**; P3 #8 (vòng 2) **còn** |
| D6 nhãn tin cậy | `v0.4` + §15.4 khớp nội dung 5 sửa; rà nhãn ✅ ở "Đối chiếu mô hình Nhanh" | 0 phát hiện **mới**; P3 #20 (vòng 2) **còn** |
| D7 14 quy tắc + đối chiếu Nhanh | #1 · #5 · #6 · #9 · #11; mục Nhanh có thật + khớp `D-197`/`D-198`/`D-199` | 3 phát hiện (#24, #27, #28) |

### Đối chiếu bắt buộc của vòng 3 — 5 P2 #13–#17

- **#13 lô không tồn tại ⇒ `0017`** · **đã đóng**: §3 hàng 2 (dòng **218**) trả `0017` (`404`), ghi rõ `D-198` ①(3) **cấm** mở rộng nghĩa `0018`; §6.2 (dòng **660**) `0018` còn **một** nghĩa. Khớp `decisions.md:212`.
- **#14 event cho `Moi → ChoDuyet`** · **đã đóng**: §3 hàng 2 side effect `QueueEvent(QueuedForApproval)` (dòng **218**); `EventType` §5.5 (dòng **479** cũ / **481** nay) thêm `QueuedForApproval` + `RetryScheduled` **ở cuối** dải (`D-17`); TC-05 nhất quán.
- **#15 khử trùng cửa sổ** · **đã đóng ở mức cơ chế**: hàng 13 (**dòng 229** cũ / **230** nay) chốt **pre-check** → mục `DaHuy` bucket `NULL` (ngoài partial index) + `Suppressed`; index chỉ là **backstop**; đua `23505` ⇒ `0019`; §5.4, §7.4 L5, §12 khớp; TC-30 nay là **Integration**, có **cả** tuần tự **và** 2 luồng song song. Hai chi tiết còn mờ: **#25** và **#26**.
- **#16 một tên enum** · **đã đóng ở mức tài liệu**: §6.1 chỉ dùng `ChannelKind`, có comment cấm enum thứ hai; ADR-14, NFR-018 (dòng **952**), TC-44 (**dòng 1005**), §14.3(e) (**dòng 1154**) đều nêu nợ đổi tên `not-002` **trong cùng PR** (grep: `not-002…:440,461,503` **vẫn** khai `NotificationChannelKind` — đúng như nợ đã ghi).
- **#17 `DangBanGiao → ChoGui` khi `NOT-002` lỗi tạm thời** · **đã đóng**: §3 **hàng 14** có guard + `QueueEvent(RetryScheduled)` + `AttemptCount++` + `LastErrorCode`; phân biệt hàng 7; §12 (dòng **915** cũ), §5.4 (dòng **443** cũ), TC-05 khớp. Nhánh **quá `ExpiresAtUtc`** của hàng 14 mở ra **#24** (vòng 4 đã đóng).

### Phát hiện vòng 3 (giữ nội dung gốc; mức đặt ở **cột 3** để script không đếm trùng — trạng thái vòng 4 ở bảng "Vòng trước")

| Vị trí | Vấn đề | Mức lúc đó | Đề nghị |
|---|---|---|---|
| §3 hàng 14 dòng 230 · §12 dòng 915 | Hàng 14 dẫn "quá `ExpiresAtUtc` ⇒ đi hàng 9 (`HetHan`)", nhưng hàng 9 là `ChoGui → HetHan` — không có cạnh `DangBanGiao → HetHan` ⇒ làm theo sẽ **NÉM** `0011` hoặc mục chỉ rời `DangBanGiao` khi lease hết hạn | P2 | Thêm hàng 15 `DangBanGiao → HetHan` (guard `now > ExpiresAtUtc`, event `Expired`) **hoặc** sửa câu thành "chờ hàng 7 về `ChoGui` rồi hàng 9" — và cập nhật TC-05 |
| §3 hàng 13 dòng 229 vs §12 dòng 909 | Nhánh đua: hàng 13 nói bên thua ghi mục `DaHuy` + `Suppressed` + `0019`; §12 chỉ nói "trả `0019` + metric" ⇒ không rõ bên thua có ghi mục + event | P3 | Chốt **một** hành vi ở cả hai chỗ và thêm vế đó vào Expected của TC-30 |
| §3 hàng 13 dòng 229 · §7.4 L5 dòng 731 · §12 dòng 909 · TC-30 dòng 991 | Cơ chế "bắt `23505` rồi ghi mục + event" không nói phải rollback transaction đã lỗi và mở **UoW mới** ⇒ PostgreSQL 17 trả `25P02`; TC-30 không bắt được | P2 | Ghi rõ UoW mới (`requiresNew`); thêm assert "đúng **1** `QueueEvent(Suppressed)`" |
| §3 hàng 14 dòng 230 · §12 dòng 915 · §5.4 dòng 443–444 | Hàng 14 giữ `ScheduledAtUtc` ⇒ thử lại mỗi nhịp, không backoff/trần `AttemptCount` | P3 | Nêu backoff tối thiểu hoặc trần `AttemptCount`, hoặc ghi "thử lại mỗi nhịp là chủ ý" |
| §4.1 sơ đồ dòng 285–291 | Sơ đồ tuần tự thiếu nhánh **lỗi tạm thời** (hàng 14) | P3 | Thêm nhánh `else «lỗi tạm thời»` hoặc ghi chú trỏ §3 hàng 14 |

### Không kiểm được (vòng 3)

- **D4:** không có khối `sql`/`powershell`/`bash` ⇒ chưa chạy.
- **`25P02` (#26):** môi trường vai không có `psql`/`docker` ⇒ **chưa đo**.
- **D7 mermaid:** `npm` bị hook chặn ⇒ **chưa chạy**; đã đọc tay 2 sơ đồ §2 và §3.
- Module `Notification` **chưa có trong code** ⇒ §5/§6 chỉ đối chiếu được với tiền lệ + `NOT-002`.

### Bằng chứng đồ thị (vòng 3)

- `graph: codegraph_explore "Notification module NotificationErrorCodes NotificationQueueItems NotificationBodyFormat ChannelKind eCommerceDbContext OutgoingEvents IncomingEvents"` ⇒ các ký hiệu `Notification*`/`ChannelKind` **not in the index**; `eCommerceDbContext.OnModelCreating` **không** gọi `ConfigureNotification`.
- `Read src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:218–255` + grep `TransactionBehavior|AbortOnConnectFail` ⇒ `KeyPrefix` **222/237**, `AbortOnConnectFail` **251**, `TransactionBehavior` **94**; ADR-10 đã sửa đúng, P3 #8 **còn** ở dòng 49/56/99/735/781/912/922.
- `grep NotificationChannelKind docs/features/notifications/not-002…` ⇒ dòng **440, 461, 503**.
- Đọc gốc `decisions.md:211–213`: `D-198` ①(3), `D-199` ①(4).

### Vòng trước (kết quả vòng 2 — 0 P1 · 5 P2 · 12 P3)

Cột 2 là **trạng thái**. Trạng thái dưới đây là **tại vòng 3** và **giữ nguyên ở vòng 4** (ngoài vùng đổi của vòng 4).

| # vòng 2 | Trạng thái | Mức lúc đó | Nội dung vòng 2 | Kiểm vòng 3 (tệp:dòng) |
|---|---|---|---|---|
| 13 | **đã đóng** | P2 | Lô không tồn tại lại trả `0018` | §3 hàng 2 dòng **218** (`0017`); §6.2 dòng **660**; grep `0018` sạch |
| 14 | **đã đóng** | P2 | `Moi → ChoDuyet` không có event | §3 hàng 2 dòng **218** + §5.5 dòng **479**; TC-05 dòng **966** |
| 15 | **đã đóng** | P2 | Khử trùng cửa sổ: hai cơ chế chồng nhau | §3 hàng 13 dòng **229**; §5.4 **440/458**; §7.4 L5 **731**; §12 **909**; TC-30 **991** |
| 16 | **đã đóng** | P2 | Hai tên enum `ChannelKind` / `NotificationChannelKind` | §6.1 **525–526/574/602**; TC-44 **1005**; §14.3(e) **1154** |
| 17 | **đã đóng** | P2 | Không có cạnh cho "NOT-002 lỗi tạm thời" | §3 hàng 14 **230**; §12 **915**; §5.4 **443**; TC-05 **966** |
| 7 | còn (chưa kiểm lại) | P3 | §11 dẫn `eCommerceMetrics.cs:75–78` cho `Allowed` | chưa đọc lại dòng 884 |
| 8 | **còn** | P3 | Dẫn số dòng `eCommerceHttpApiHostModule.cs` sai | dòng **49**, **56**, **99**, **735**, **781**, **912**, **922**; code thật **222/237/251/94** |
| 9 | còn (chưa kiểm lại) | P3 | `StockLedger.cs` dẫn đường dẫn cũ | chưa đọc lại |
| 10 | **còn** | P3 | Q11 `TenantId = NULL` còn `Open`; Basic Flow không nêu cách vượt query-filter | Q11 dòng **1189** vẫn `Open`; §1 Basic Flow bước 3 **không đổi** |
| 11 | còn (chưa kiểm lại) | P3 | `0010` gộp hai nghĩa | chưa đọc lại dòng 650 |
| 12 | **còn** | P3 | Hàng 8 guard sai lại NÉM mã `NOT-002` | §3 hàng 8 dòng **224** vẫn vậy |
| 18 | **còn** | P3 | Cùng test dẫn hai kiểu (`69–105` vs `86–105`) | §0 dòng **49** vẫn `69–105`; ADR-08 **104** + §14.4 **1162** đã `86–105` |
| 19 | **còn** | P3 | F9 dẫn "§6.3 tài liệu này" (không có §6.3) | F9 dòng **1205** vẫn "§6.3" |
| 20 | **còn** | P3 | Mục Nhanh ghi `NOT-001` có `DOC-OK` | dòng **1237** vẫn `DOC-OK` |
| 21 | còn (chưa kiểm lại) | P3 | `ContentFingerprint` tính ở renderer thuần nhưng công thức cần `RecipientHash`/`TemplateVersionId` | chưa đối chiếu lại |
| 22 | **còn** | P3 | `FromStatus` NOT NULL nhưng event `Enqueued` không có trạng thái nguồn | §5.5 dòng **480** vẫn **không** nói `Enqueued` |
| 23 | còn (chưa kiểm lại) | P3 | `NOT-002` ghi `NOT-001` tiêu thụ `NotificationSentEto`; §7.2 không có | chưa đối chiếu lại |

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
