# Bản câu hỏi cho phiên uỷ quyền `U-02`

> **Trạng thái: ĐÃ CHUẨN BỊ** 19/11/2026 (phiên `N27`, hàng `U-01`) — 166 câu §15 + 37 dòng hàng đợi `D-xx` + 9 câu luật chơi đã xếp rổ.
> Dùng trong `U-02`: chủ dự án trả lời theo rổ; câu trả lời vào [hồ sơ uỷ quyền](uy-quyen-chu-du-an.md) và `docs/nhanh/decisions.md`.
> Nguồn xếp rổ: `decider` 5 lượt (chế độ chuẩn bị, **không** tạo `D-xx`); hồ sơ chưa hiệu lực nên mọi rổ xếp theo ma trận quyền + vùng cấm của `decision-policy`.

## 0. Số câu theo rổ

| Rổ | Ý nghĩa | Số câu |
|---|---|---|
| R0 | Không hỏi — có tiền lệ / code thật đã trả lời / kỹ thuật thuần, lùi rẻ (phụ lục) | 50 câu §15 · 24 dòng `D-xx` |
| R1 | Hỏi **một câu chính sách** cho cả nhóm | 58 câu §15 gom thành **16** câu chính sách (§2) + 9 câu luật chơi (§1) |
| R2 | Duyệt theo ngoại lệ — "đồng ý tất cả, trừ dòng…" | 30 câu §15 + 11 dòng `D-xx` (9 dòng ⚠ nhìn kỹ) |
| R3 | Không ai quyết được ngay (NCC, pháp lý, hợp đồng) — chỉ hỏi ai lo, khi nào | 28 câu §15 + 2 dòng `D-xx` |
| | **Tổng** | 166 câu §15 · 37 dòng `D-xx` |

Dự kiến thời lượng `U-02`: §1 (9 câu, ~3 lượt hỏi) → §2 (16 câu, ~4 lượt) → §3–§5 (bảng, "đồng ý trừ…") → §4 (R3: ai lo, khi nào) ≈ 1–1,5 giờ.

**3 câu cũ — code đã trả lời, BA sửa §15 thành Closed (không hỏi người):** `ADDR-001-Q7` (`AdministrativeUnitResolver.cs:140-147`) · `REP-001-Q2` + `REP-002-Q5`
(migration `20261013012648_Add_Tax_Module.cs:118-123` đã dùng `btree_gist`; ca `PostgreSqlSchemaTests.cs:92` — chưa chạy lại phiên này).
Cũng cũ: `PRO-002-Q3` (`Money` có ở `shared/eCommerce.Kernel/Money/`), `REP-001-Q1` (`TinhButToanChungTu.cs:49-55`), `SHP-004-Q6` (`IHttpClientFactory` đã dùng),
`SHP-001-Q8`/`REP-002-Q6` (`MetricsTests.cs:64` đã bỏ đếm cứng).

## 1. Luật chơi M1–M6 và V1–V3

| # | Câu | Khuyến nghị (đứng đầu) | Hệ quả · rủi ro |
|---|---|---|---|
| **M1** | Phiên uỷ quyền một lần + duyệt lô khi rảnh thay cho hỏi lẻ? | **Đồng ý** | Câu đã trả lời ở `U-02` thành `ĐÃ CHỐT`, DEC trả `TIEN_LE` không hỏi lại. Rủi ro: quyết định tạm sai dồn thành công làm lại ⇒ duyệt lô tuần đầu + ngưỡng đảo 20 %/nhóm. **Kèm `D-113`** (DEC mở rộng quyền của chính mình): người chốt cùng M1. |
| **M2** | `DOC-OK` ngoài danh sách đỏ ⇒ DEC **tạm duyệt** để code trên nhánh (không merge `main`)? | **Có, trừ danh sách đỏ** (dưới bảng) | Đổi `D-71` (`ĐÃ CHỐT`) + `implementation-plan.md` §A.5 ⇒ phải là dòng `D-xx` **của người** "bổ sung `D-71`". Rủi ro: người không ký biên bản thì code trên nhánh làm lại; chưa có cổng máy "merge `main` khi biên bản chưa ký ⇒ đỏ" ⇒ giao OPS dựng trước khi bật M2. |
| **M3** | Agent merge PR vào nhánh tích hợp `tich-hop/<giai-doan>` khi cổng máy xanh? | **Có; `main` chỉ người** | WP nối tiếp không chờ người. Rủi ro: lỗi dồn trên `tich-hop`, PR lô vào `main` lớn ⇒ cần CI trên `tich-hop` + cầu dao "CI đỏ ⇒ dừng". Người tạo nhánh + protection. |
| **M4** | Câu kế toán (~42): ai trả lời? | **DEC tạm chốt + kế toán duyệt lô trước release** | Không chặn code, chỉ chặn release. Rủi ro: kế toán đảo giá vốn/ghi nhận khi đã có dữ liệu thật ⇒ backfill (vùng cấm) ⇒ cần R3 "ai là kế toán, hạn nào". |
| **M5** | Câu NCC (~27): ai liên hệ hãng; agent gọi **sandbox**? | **Có sandbox; production cấm** | Gỡ phần lớn câu R3 NCC. Rủi ro: tài khoản/token sandbox là secret do **người** tạo, để ngoài repo; sandbox ≠ production ⇒ vẫn chặn release. Cần tên người liên hệ GHTK (đang chặn duyệt `SHP-004`). |
| **M6** | Ngân sách + giờ chạy | **Không bật credits; chạy bất cứ lúc máy bật, ưu tiên ngoài giờ chat** | Không phát sinh chi phí; trần cứng = hạn mức tuần. Rủi ro: chạy trùng giờ chat làm cạn hạn mức của người; định dạng lỗi hết hạn mức của `-p` chưa đo. |
| **V1** (= v2 Q3) | Pilot | **(a)** công nhận Pilot-D đã chạy ở N22–N26; Pilot-C = WP-15, mức L1 (người đọc báo cáo sau mỗi mục) rồi mới L2 · (b) chạy lại Pilot-D bằng bộ chạy tự động trước | |
| **V2** (= v2 Q7) | Danh sách đỏ đủ chưa? | **(a)** giữ 8 loại ở `ke-hoach-he-thong-agent.md` §4.3 + danh sách mã cụ thể dưới đây (dùng cho M2 và luật "luôn Opus") · (b) giữ nguyên, không danh sách mã | |
| **V3** (= v2 Q15 = `D-75`) | Agent chạy `dotnet ef database update`? | **(a) Cấm**; migration đi qua cổng / DbMigrator do người chạy · (b) cho phép trên DB dev riêng của luồng | Đụng vùng dữ liệu thật (§3 mục 2) ⇒ người xác nhận. |

**Danh sách đỏ đề xuất cho M2 (không được tạm duyệt; người hoặc kế toán ký biên bản):**

- **Đỏ cứng (tiền / kế toán / đối soát):** `ACC-001` · `REP-002` · `REP-003` · `REC-003` · `SHP-003` · `SAL-006` (5 mã `ACC-001`, `REP-002`, `REC-003`, `SHP-003`, `SAL-006` trùng danh sách "luôn Opus" của v2).
- **Đỏ có điều kiện:** `REP-001` (tới khi chốt P-GIÁ-VỐN + P-GHI-NHẬN) · `LOY-001` (tới khi chốt P10) · `SHP-004` (tới khi `SHP-004-Q1` có trả lời GHTK) · `ADDR-003` (tạm duyệt được, **trừ** PII `freeText`: `D-101`/`D-105`/`Q21` chặn release) · `NOT-002` (tạm duyệt thiết kế; khoá HMAC production + chọn kênh Zalo là việc người).
- **Họ mã luôn đỏ (ngoài WP-14):** `EINV-*` · `SIGN-*` · `PAY-*` · mọi tài liệu sửa `XC-001` · mọi thay đổi phá huỷ contract liên module hoặc schema.
- **Tạm duyệt được:** `ADDR-001` · `ADDR-002` · `SHP-001` · `SHP-002` (phần ghi nhận doanh thu vẫn chặn release theo P-GHI-NHẬN) · `PRO-001` · `PRO-002` (sau P-TIỀN-TÍNH) · `NOT-001`.

## 2. R1 — 16 câu chính sách (mỗi câu trả lời cho cả nhóm; (a) là khuyến nghị)

| # | Nhóm | Câu chính sách | Lựa chọn | Câu §15 áp |
|---|---|---|---|---|
| P1 | **P-NGƯỠNG** (tiền) | Ngưỡng duyệt / trần / dung sai khi chưa có số của kế toán? | **(a)** Setting theo tenant; trống = luôn cần duyệt, dung sai 0, cờ ngoại lệ tắt; chặn release tới khi kế toán điền · (b) chủ dự án đặt một ngưỡng chung · (c) hoãn luồng duyệt | `SHP-003-Q6`, `LOY-001-Q6`, `SAL-006-Q2`, `REC-003-Q10`, ngưỡng COD của `SHP-001-Q10` |
| P2 | **P-GIÁ-VỐN** | Phương pháp giá vốn + chụp khi nào? | **(a)** bình quân gia quyền di động, chụp vào fact lúc bán, có cấu hình, chặn release tới khi kế toán duyệt · (b) bình quân cuối kỳ · (c) FIFO. *Phụ:* `CostPrice` chưa có trong code ⇒ thuộc WP nào của lộ trình (`D-69`)? | `REP-001-Q7`, `REP-002-Q2`, `REP-003-Q2` |
| P3 | **P-LÃI-GỘP + P-TIỀN-TÍNH** | Định nghĩa chỉ tiêu lãi/chi phí, làm tròn, chiết khấu chồng, thứ tự áp khuyến mại? | **(a)** DEC tạm chốt theo thông lệ + `XC-001` + `RoundingPolicy`/`MoneyAllocator` (LG1 = DT ròng − giá vốn; LG2 = LG1 − phí sàn − phí ship trả hãng; phí sàn dự kiến có cờ `SettlementPending`; chi phí thông báo = chi phí kỳ; điểm tách cột), `reviewBefore = release`, kế toán duyệt lô · (b) gom lô gửi kế toán, tính năng chờ · (c) chủ dự án từng câu | `REP-002-Q7`, `REP-003-Q6`, `ACC-001-Q8`, `ACC-001-Q9`, `NOT-002-Q9` |
| P4 | **P-GHI-NHẬN** | Ghi nhận doanh thu lúc nào, vào kỳ nào? | **(a)** khi Delivered đã đối soát; kỳ = ngày UTC+7 của sự kiện; kỳ chốt không sửa, lệch điều chỉnh vào kỳ sau · (b) theo ngày tạo đơn · (c) Manual ghi nhận ngay | `SHP-002-Q6`, `REP-001-Q5` |
| P5 | **P1 — thiếu sự thật từ NCC** | Khi NCC chưa xác nhận khả năng (id sự kiện, API tra cứu, chống trùng, quota)? | **(a)** thiết kế theo giả định xấu nhất, khả năng tuỳ chọn của adapter + Setting, fail-closed, `reviewBefore = release` · (b) dừng phần đó tới khi NCC trả lời · (c) giả định theo tài liệu công khai, sai thì sửa | `SHP-003-Q8`, `NOT-002-Q4` (+ việc chờ của mọi R3 SHP/ADDR-002) |
| P6 | **P2 — máy gợi ý, người xác nhận** | Khi tự động hoá không chắc chắn? | **(a)** thêm lệnh thủ công quyền riêng + nhật ký append-only; máy chỉ gợi ý · (b) chỉ job tự động · (c) máy tự áp gợi ý tin cậy cao (trái `ADDR-002` §18, phải sửa SRS trước) | `SHP-001-Q19`, `ADDR-002-Q4` |
| P7 | **P3 — PII mặc định hẹp** | Câu bảo mật/PII chưa có yêu cầu cụ thể? | **(a)** chọn phạm vi hẹp nhất (không lưu thô, không sửa HTML, không gửi ra ngoài), mở bằng cờ sau · (b) làm đủ ngay · (c) chờ Security | `ADDR-002-Q6`, `NOT-001-Q2`, `ACC-001-Q6`, `NOT-002-Q2`, `NOT-001-Q9`, `NOT-002-Q5` |
| P8 | **P4 — ngưỡng vận hành chưa đo** | TTL, chu kỳ, timeout, khung giờ, cỡ lô chưa có số đo? | **(a)** ABP Setting + mặc định DEC đề xuất, đo lại sau pilot, `reviewBefore = release` · (b) vận hành cấp số trước khi code | 16 câu: `ADDR-001-Q8`, `ADDR-002-Q5`, `ADDR-003-Q6`, `SHP-001-Q6/Q9`, `SHP-002-Q7`, `SAL-006-Q8`, `PRO-001-Q2/Q6`, `PRO-002-Q1`, `REP-002-Q8`, `ACC-001-Q10`, `NOT-001-Q5/Q6/Q8`, `NOT-002-Q6` |
| P9 | **P6 — thêm gói NuGet/npm** | Agent thêm gói mới? | **(a)** được nếu license MIT/Apache-2.0 + dòng `D-xx`; một luồng sửa `Directory.Packages.props`; thương mại/copyleft thì người duyệt; mặc định không thêm · (b) không bao giờ | `REC-003-Q5` (ClosedXML/MiniExcel, loại EPPlus — **license chưa tra web, người kiểm lại**) |
| P10 | **P7 — bề mặt ẩn danh** ⚠ *vùng cấm §3.7: hồ sơ không uỷ quyền được* | Mở endpoint `[AllowAnonymous]`? | **(a)** không mở nào tới khi có rate limit + người duyệt từng endpoint · (b) mở có điều kiện (nêu điều kiện) | `ADDR-001-Q5`, `PRO-002-Q7`, `PRO-001-Q5` |
| P11 | **P8 — công cụ phụ trợ vận hành** | API/CLI tra cứu, so sánh, gỡ treo, dry-run? | **(a)** CÓ: chỉ đọc, permission riêng; gỡ/ghi qua cửa ghi có sẵn + audit; dry-run hàm thuần; ưu tiên thấp, trễ thì cắt trước (tiền lệ `XC-003` Q5, `XC-002` Q7) · (b) không làm GĐ3 · (c) làm đủ kể cả sửa tự động | `ADDR-003-Q8`, `PRO-001-Q7/Q9`, `PRO-002-Q10`, `REP-003-Q9`, `NOT-001-Q12` |
| P12 | **P9 — phân quyền & duyệt mặc định** | Quyền mặc định và "người duyệt ≠ người dựng"? | **(a)** mỗi hành động một permission, mặc định admin tenant/vai sát việc; "khác người" là Setting mặc định tắt; luôn audit · (b) mặc định bắt buộc hai người · (c) một quyền chung module | `SHP-001-Q10`, `PRO-002-Q5`, `NOT-001-Q1`, `NOT-001-Q3` |
| P13 | **P10 — chính sách với người mua** | Đổi trả, điểm theo kênh, nợ điểm, hạn điểm? | **(a)** cấu hình theo tenant, giá trị mẫu thận trọng (đổi trả 7 ngày; một tỉ lệ điểm chung; không hiện nợ điểm cho khách; điểm không hết hạn), `reviewBefore = release`, chủ dự án duyệt một lần · (b) chốt từng số ngay · (c) hoãn tính năng | `SAL-006-Q1`, `LOY-001-Q3/Q4/Q5` |
| P14 | **P-ƯU-TIÊN** | Khi trễ cắt gì? Hai phương án ngang nhau? Phát hiện việc thiếu? | **(a)** cắt phần phụ trong WP (công cụ P8 → UI tiện ích → tối ưu), ghi nợ có chủ, không cắt BR/bất biến/test; khi hoà chọn **rẻ nhất để lùi (T6)** rồi đo được (T8); việc thiếu ghi hàng đợi, trừ việc cần để đạt DoD của chính WP · (b) lùi cả WP · (c) giữ phạm vi, chấp nhận trễ. *(Sửa lỗi mẫu `de-xuat…` §4.3: "rẻ nhất để lùi" là T6, không phải T8.)* | mọi hàng; `decision-policy` |
| P15 | **P-CHUẨN** | Tài liệu mơ hồ, "chuẩn" là ai? | **(a)** tài liệu đã duyệt / `D-xx` → Nhanh có bằng chứng đo được → đa số KiotViet/Sapo/Haravan/MISA → cấu hình với mặc định an toàn · (b) thông lệ VN trước Nhanh · (c) khác | mọi hàng |
| P16 | **P-VẬN-HÀNH BỘ CHẠY** (báo cáo + câu treo) | Báo cáo thế nào? Câu chuyển người treo quá hạn? | **(a)** báo cáo: tệp mỗi ngày `docs/agents/bao-cao-chay/<ngày>` + tóm tắt khi bộ chạy dừng; treo: câu vùng cấm/§6 hồ sơ không bao giờ tự chốt, câu khác treo > 7 ngày ⇒ DEC `TẠM CHỐT` phương án trung lập, `reviewBefore = release` · (b) báo cáo chỉ mỗi ngày; treo: không bao giờ tự chốt · (c) chỉ khi dừng; treo sau N ngày do người đặt | hồ sơ §7 |

## 3. R2 — bảng duyệt theo ngoại lệ

Trả lời bằng: **"đồng ý tất cả, trừ dòng …"** (số dòng + lựa chọn của anh/chị). Cột cuối = tin cậy/chi phí lùi.

### 3.1. Câu §15 (30 dòng)

| # | Câu | Khuyến nghị của DEC (dùng lại khuyến nghị BA nếu có) | T/L |
|---|---|---|---|
| 1 | `SHP-003-Q7` | Chỉ chuyển Collected khi dòng đối soát REC-003 khớp; trạng thái hãng chỉ tham khảo | VỪA/VỪA |
| 2 | `REC-003-Q3` | Dùng 8 ReasonCode BA đề xuất (§5.5); thêm cuối dải (D-17) | VỪA/THẤP |
| 3 | `REC-003-Q7` | GĐ3 dừng ở "hãng báo đã thu"; thêm cột nullable ngày/số tiền về TK, nhập tay | VỪA/THẤP |
| 4 | `REC-003-Q8` | Không nạp lại phiên Excel cũ; chỉ nhập số dư COD đầu kỳ (backfill thật = vùng cấm) | VỪA/THẤP |
| 5 | `SAL-006-Q3` | Hoàn phí ship theo ReasonCode: đổi ý không hoàn; lỗi người bán/hàng lỗi hoàn toàn bộ | VỪA/THẤP |
| 6 | `SAL-006-Q4` | Hàng hỏng vào chiều Damaged (D-27); huỷ hàng = GoodsIssue Mode mới cuối dải (D-30) | VỪA/VỪA |
| 7 | `SAL-006-Q6` | RefundStatus=NotRequired + điều chỉnh công nợ COD qua REC-003, không hoàn qua cổng | VỪA/VỪA |
| 8 | `PRO-001-Q4` | Thêm cột ShippingDiscountTotal riêng (additive), không gộp DiscountTotal (D-45/D-48) | VỪA/VỪA |
| 9 | `PRO-002-Q9` | Thêm trường "nguồn tài trợ" (Shop/Platform) trên dòng Breakdown | VỪA/THẤP |
| 10 | `LOY-001-Q2` | Đảo điểm theo tỷ lệ giá trị hàng trả, qua đúng một cửa DaoGhiNhanMuaHang (D-45) | VỪA/THẤP |
| 11 | `REP-002-Q9` | Có: số trong ngày gắn nhãn "tạm tính", không tách bảng | VỪA/THẤP |
| 12 | `REP-003-Q1` | Vòng quay = COGS / tồn bình quân hai đầu kỳ; có CoveredDays | VỪA/THẤP |
| 13 | `ACC-001-Q5` | Mặc định chỉ số tổng + mã chứng từ; cột tên/MST người mua tuỳ chọn, cần quyền | VỪA/THẤP |
| 14 | `ADDR-003-Q4` | Không dùng dịch vụ ngoài ở GĐ này; giữ resolver nội bộ, đo tỉ lệ NeedsReview | VỪA/THẤP |
| 15 | `SHP-004-Q4` | Khớp danh mục qua chuẩn hoá ADDR-003; gửi hãng tên snapshot nguyên văn | VỪA/THẤP |
| 16 | `PRO-002-Q6` | Đếm theo CustomerId; khách vãng lai khớp theo SĐT chuẩn hoá | VỪA/VỪA |
| 17 | `PRO-002-Q8` | Không làm GĐ này; nếu cần theo mã tỉnh ĐVHC của ta | VỪA/THẤP |
| 18 | `NOT-002-Q7` | Danh sách email trong Setting (rỗng ⇒ cảnh báo lúc khởi động); GĐ3 không có kênh 2 | VỪA/THẤP |
| 19 | `NOT-002-Q8` | Opt-out nội bộ là nguồn; NCC báo từ chối ⇒ ghi opt-out kênh đó | VỪA/THẤP |
| 20 | `SHP-001-Q17` | (a) COD lẻ ⇒ NÉM; hàm đổi sang số nguyên ở ShippingHub | VỪA/THẤP |
| 21 | `SHP-001-Q18` | (b) CodAmount null ⇒ NÉM mã mới trước Pha 1; null ≠ 0 | CAO/THẤP |
| 22 | `LOY-001-Q1` | Tiêu điểm cùng UoW với PlaceOrder; khác UoW thì giữ chỗ → xác nhận, hết hạn hoàn | VỪA/VỪA |
| 23 | `REP-003-Q4` | Chưa có REC-002 ⇒ phí "dự kiến" có nhãn, cột thực tế null (D-34) | VỪA/THẤP |
| 24 | `NOT-001-Q11` | Template IsSystem ở host chỉ đọc cho mọi tenant, cấm dữ liệu khách (D-92) | VỪA/THẤP |
| 25 | `ADDR-003-Q3` | GĐ3 chỉ địa chỉ VN; mở sau bằng cột CountryCode nullable | VỪA/THẤP |
| 26 | `SHP-001-Q5` | Order.WeightGram trước; null ⇒ Σ SKU×SL qua interface Sales; thiếu ⇒ NÉM | VỪA/THẤP |
| 27 | `REC-003-Q9` | Không hỗ trợ .xls; chỉ xlsx/csv; xem lại khi có file thật | VỪA/THẤP |
| 28 | `PRO-001-Q8` | Cả hai trần cùng áp, chạm trước thì NÉM; kiểm dưới khoá giữ lượt | VỪA/THẤP |
| 29 | `REP-003-Q3` | GĐ3 chỉ T+1; làn trong ngày hoãn có chủ (D-34) | VỪA/THẤP |
| 30 | `REP-003-Q5` | Cả hai; ChannelShopId nullable + NULLS NOT DISTINCT thay Guid.Empty; BA sửa | VỪA/VỪA |

### 3.2. Dòng hàng đợi `D-xx` đang `TẠM CHỐT` (R2 — 11 dòng; đồng ý ⇒ `ĐÃ CHỐT`)

| # | `D-xx` | Nội dung | Lưu ý |
|---|---|---|---|
| D1 | `D-77` | Đổi tên ĐVHC tại chỗ (danh tính bất biến, chứng từ lưu snapshot) | ⚠ hoá đơn GĐ0 băm tên từ danh mục mà không lưu chuỗi ⇒ dính HĐĐT |
| D2 | `D-78` | Production không gọi `provinces.open-api.vn`; chỉ nạp file đã duyệt + `SourceHash` | guard NÉM khi `Enabled=true`; người duyệt file: `ADDR-003-Q2` (R3) |
| D3 | `D-85` | `ADDR-002`: giữ `CarrierAddressMappingAppService`, thêm quyền `Verify` | xem quyền mặc định cấp cho vai nào (P12) |
| D4 | `D-86` | Hãng đổi tên hàng loạt ⇒ tra NÉM `0502`, chặn tạo vận đơn tới khi người xác minh lại | ⚠ chặn vận đơn hàng loạt |
| D5 | `D-92` | Ánh xạ mã hãng dùng chung ở host (`TenantId = NULL`) | ⚠ lùi VỪA; còn phụ thuộc `ADDR-001-Q6` + câu NCC "mã có theo tài khoản shop không" |
| D6 | `D-99` | Cửa `ChuanHoaLaiAsync` nội bộ không `[Authorize]` | ⚠ phải chắc không thành `ApplicationService` (ABP tự phơi HTTP) |
| D7 | `D-108` | Địa chỉ nhận của vận đơn = snapshot trên đơn (Sales) | ⚠ khác khuyến nghị BA; sửa `SAL-002` đã Approved + contract liên module mới |
| D8 | `D-109` | `ClientOrderCode` = hàm thuần của `(OrderId, Generation)` | ⚠ "GHN tối đa 50 ký tự, unique theo shop" là sự thật NCC chưa xác minh sandbox (M5) |
| D9 | `D-104` | Hook không chặn dừng khi thiếu JSON của vai (chỉ ghi nhật ký) | ⚠ tắt một lớp kiểm; an toàn chỉ khi ORCH luôn chạy `check-agent-artifact` |
| D10 | `D-112` | `/chay-phien` tự commit + push nhánh WP | ⚠ nới `CLAUDE.md` §8; hook phải chặn đường khác `docs/**`, `agents/state/**` |
| D11 | `D-113` | Hồ sơ uỷ quyền hiệu lực ⇒ decider quyết thay mọi câu ngoài vùng cấm | ⚠ **người chốt cùng M1**, không để `TẠM CHỐT (DEC)` |

**R0 hàng đợi (24 dòng, đồng ý mặc định):** `D-72` `D-73` `D-74` `D-76` `D-79` `D-80` `D-81` `D-82` `D-83` `D-84` `D-87` `D-88` `D-89` `D-90` `D-91` `D-95` `D-96` `D-97` `D-98` `D-100` `D-102` `D-103` `D-110` `D-111`.

## 4. R3 — ai lo, khi nào

Không hỏi đáp án — chỉ hỏi **ai lo và khi nào**. Phần dây chuyền làm trong lúc chờ: adapter + cấu hình + fail-closed + chặn release (`continueWhileWaiting`).
Nếu chốt **M5** (người liên hệ hãng + sandbox) thì phần lớn cột "ai lo" gộp được một câu trả lời. Tạo tài khoản sandbox / secret = vùng cấm, **người** làm.

### 4.1. Câu hỏi cho người (chọn tên + mốc)

| # | Việc | Phụ thuộc | Đề xuất ai lo · hạn |
|---|---|---|---|
| R3-1 | Liên hệ **GHTK** (email tại `implementation-plan.md:324` đang treo): mã địa chỉ, đổi COD, webhook | `ADDR-002-Q1/Q2`, `SHP-004-Q1/Q3/Q7`, `SHP-003-Q1` | người liên hệ · trước duyệt `SHP-004` |
| R3-2 | Tài khoản **sandbox GHN** + SĐT nhận OTP | `SHP-001-Q2/Q3`, `SHP-002-Q1`, `SHP-003-Q2/Q3` | chủ dự án · trước WP code `SHP-001` |
| R3-3 | **Kế toán trưởng** (tên? hạn?): chế độ tài khoản + bảng ánh xạ Nợ/Có, file mẫu Bravo/MISA, hạn lưu trữ luật định | `ACC-001-Q1/Q2`, `REP-003-Q7`, `SAL-006-Q5` | trước WP code `ACC-001` / trước release |
| R3-4 | **File bảng kê COD thật** GHN/GHTK + hỏi GHN có API đối soát | `REC-003-Q2`, `SHP-003-Q9` | vận hành · trước WP code `REC-003` |
| R3-5 | **Pháp chế**: ToS robot tải bảng kê; thay NĐ 13/2023 + hạn lưu `freeText` | `REC-003-Q1`, `ADDR-003-Q21`, `D-101` | trước release |
| R3-6 | **Chọn kênh 2 (Zalo/SMS) + NCC, ký hợp đồng**; tạo khoá HMAC production | `NOT-002-Q1/Q3/Q5` (vùng cấm) | chủ dự án · trước WP code kênh 2 (email đi trước) |
| R3-7 | File ĐVHC công bố của cơ quan nhà nước (phụ lục NQ sắp xếp ĐVHC + QĐ 19/2025/QĐ-TTg — **chưa xác minh**) | `ADDR-003-Q2`, `D-93` | chủ dự án/BA · trước WP code đường nạp production |
| R3-8 | Hạ tầng lưu file production (object storage có phí = vùng cấm) | `ACC-001-Q4` | chủ dự án (vận hành) · trước release `ACC-001` |

### 4.2. Chi tiết 28 câu §15 + 2 dòng `D-xx` thuộc R3

| Câu | Khuyến nghị tạm (dây chuyền làm gì) | T/L |
|---|---|---|
| `SHP-003-Q9` | Adapter, mặc định nhập file tay; Vận hành hỏi GHN có API đối soát COD không | THẤP/THẤP |
| `REC-003-Q2` | Parser đọc cột theo cấu hình, file lạ thì từ chối; Vận hành cấp file mẫu thật GHN/GHTK | THẤP/THẤP |
| `SAL-006-Q5` | SAL-006 chỉ phát OrderReturnedEto + TaxRefundedAmount; điều chỉnh/thay HĐĐT để EINV-003 | CAO/THẤP |
| `REP-003-Q7` | Chưa xoá chi tiết tới khi kế toán xác nhận hạn lưu trữ luật định; Setting mặc định vô hạn | THẤP/CAO |
| `ACC-001-Q1` | Ánh xạ SourceKind → Nợ/Có là bảng cấu hình theo tenant; rỗng thì chặn xuất | THẤP/THẤP |
| `ACC-001-Q2` | Adapter định dạng theo cấu hình; chờ file mẫu Bravo/MISA | THẤP/THẤP |
| `NOT-002-Q1` | Vùng cấm (NCC có phí): adapter + giả lập; chủ dự án chọn NCC + ký | CAO/THẤP |
| `ADDR-002-Q1` | Adapter nguồn mã hãng (API + file); GHN đọc tài liệu công khai, GHTK hỏi NCC | VỪA/THẤP |
| `ADDR-002-Q2` | Hỗ trợ cả V1/V2 (D-80); SHP-004 chặn phần địa chỉ tới khi GHTK trả lời | VỪA/VỪA |
| `ADDR-002-Q7` | Không chặn: job đồng bộ định kỳ đủ; webhook là tối ưu sau | VỪA/THẤP |
| `SHP-001-Q2` | Adapter GHN theo tài liệu công khai + sandbox; trường chưa xác minh để cấu hình | VỪA/THẤP |
| `SHP-001-Q3` | Giả định hãng KHÔNG chống trùng: tra trước rồi tạo, theo ClientOrderCode (D-109) | VỪA/THẤP |
| `SHP-002-Q1` | Khoá = ProviderEventId nếu có, không thì hash payload; tham số hash GHTK chờ NCC | VỪA/THẤP |
| `SHP-002-Q2` | Allowlist trong Setting, rỗng ⇒ từ chối; không tin webhook, đối soát chủ động | VỪA/THẤP |
| `SHP-002-Q3` | Không chặn: dùng thời điểm nhận; máy trạng thái chỉ tiến | VỪA/THẤP |
| `SHP-002-Q4` | Chu kỳ poll + trần gọi là Setting, mặc định thấp; 429 ⇒ giãn dần | VỪA/THẤP |
| `SHP-004-Q1` | Chặn phần địa chỉ SHP-004; SHP-001 (GHN) đi trước | VỪA/VỪA |
| `SHP-004-Q3` | Giả định không chống trùng: tra trước rồi tạo + nhật ký attempt | VỪA/THẤP |
| `SHP-004-Q7` | Như SHP-002-Q4 theo hãng | VỪA/THẤP |
| `SHP-003-Q1` | Adapter khai báo có đổi COD được không; không API ⇒ bước thủ công; trần COD là Setting | VỪA/THẤP |
| `SHP-003-Q2` | Giả định mọi lần đổi COD ở GHN cần OTP tới khi sandbox chứng minh khác | VỪA/THẤP |
| `SHP-003-Q3` | TTL/số lần thử OTP là Setting; hết hạn ⇒ Failed, không tự thử lại | VỪA/THẤP |
| `SHP-003-Q4` | Lỗi quota hãng ⇒ NÉM, không thử lại im lặng | VỪA/THẤP |
| `REC-003-Q1` | Mặc định import file tay; không robot tải bảng kê tới khi pháp chế xác nhận ToS | CAO/THẤP |
| `NOT-002-Q3` | GĐ3 = email + adapter kênh 2 giả lập (D-70); chọn Zalo/SMS + giá = vùng cấm | CAO/THẤP |
| `ADDR-003-Q21` | Cửa xoá host-only + job xoá theo hạn, cả hai tắt mặc định; pháp lý chờ | THẤP/CAO |
| `ACC-001-Q4` | Lớp trừu tượng lưu file; dev đĩa/DB; prod chờ hạ tầng | VỪA/VỪA |
| `ADDR-003-Q2` | Production chỉ nạp file công bố (D-93); dev dùng bản giả lập gắn nhãn | THẤP/CAO |

Dòng `D-xx` R3: `D-101` (PII trong ledger append-only, xoá ẩn danh nợ GĐ2 ⇒ pháp lý dữ liệu cá nhân; gộp `ADDR-003-Q21`/`D-105`) · `D-75` (= V3).

## 5. Hàng đợi `D-xx` đang chờ người (duyệt lô)

Xem §3.2 (R2 + R0). Tổng 37 dòng = 24 R0 + 11 R2 + 2 R3. Dòng người đồng ý ⇒ đổi trạng thái `D-xx` thành `ĐÃ CHỐT` và chuyển dòng sang "Đã xử lý" ở `hang-doi-xem-lai.md`.

## Phụ lục — R0 (không hỏi; 50 câu §15)

Có tiền lệ `D-xx` / code thật đã trả lời / kỹ thuật thuần, lùi rẻ. Người có thể xem; BA áp vào tài liệu ở các phiên `/doc-gate` sau.

| Câu | Quyết định áp dụng | T/L |
|---|---|---|
| `SHP-004-Q8` | Không đổi Submission: đổi COD là lệnh/aggregate riêng (SHP-003) | VỪA/THẤP |
| `REC-003-Q4` | REC-003 có bảng COD riêng; REP-001 chỉ đọc qua hợp đồng | VỪA/VỪA |
| `SAL-006-Q10` | Đọc từ read-model IReportFactReader như REP-002 | VỪA/THẤP |
| `REP-001-Q4` | Giữ 5 phút/3 ngày/900 s làm mặc định Setting | VỪA/THẤP |
| `REP-001-Q6` | Như REC-003-Q4; webhook GHTK không HMAC ⇒ không tin cho tiền | VỪA/VỪA |
| `REP-001-Q8` | Không làm fact theo giờ (ngoài 12 báo cáo D-05) | CAO/THẤP |
| `REP-002-Q4` | D-48 tách VAT ⇒ GrossProfit = RevenueNet − Cost − Fee, không trừ thuế | CAO/THẤP |
| `REP-003-Q8` | REP-003 xuất file + hash, ACC-001 lưu vết append-only (D-03) | VỪA/THẤP |
| `ACC-001-Q7` | Như REP-003-Q8 | VỪA/THẤP |
| `ADDR-001-Q1` | Đóng theo D-93 + D-78; mã hãng chuyển ADDR-002-Q1 | CAO/THẤP |
| `SHP-001-Q1` | Hỗ trợ cả V1/V2 (D-80/D-95); mô hình riêng từng hãng ⇒ SHP-004-Q1 | CAO/THẤP |
| `SHP-001-Q12` | (a) theo D-89: NÉM fail-closed tới khi bỏ xác minh bớt một ánh xạ | CAO/THẤP |
| `NOT-001-Q13` | 10 s trong Setting, khớp timeout NCC NOT-002 | VỪA/THẤP |
| `ADDR-001-Q3` | Lưu vĩnh viễn: giữ mã V1, IsActive=false, không xoá (D-77/95/100) | CAO/VỪA |
| `ADDR-001-Q6` | Một bản dùng chung TenantId=NULL ở host (D-92, D-82) | CAO/VỪA |
| `ADDR-003-Q1` | Chỉ chặn khi tạo vận đơn (SHP-001); đơn POS/sàn không địa chỉ vẫn vào (D-108) | VỪA/THẤP |
| `SHP-001-Q4` | Tên khoá không `ecommerce:` (KeyPrefix lo); BA sửa SHP-002 §8.2 | CAO/THẤP |
| `SHP-001-Q7` | Chia dải trăm: SHP-001 01xx · 002 02xx · 003 03xx · 004 04xx | VỪA/THẤP |
| `SHP-001-Q8` | Không đếm cứng; mỗi PR tự thêm metric | CAO/THẤP |
| `SHP-001-Q15` | (a) A5 gặp Draft đi tiếp Pha 2 dưới khoá; không thêm job | VỪA/THẤP |
| `SHP-001-Q16` | (a) giữ số SHP-002; Draft/Submitting/SubmitFailed thêm cuối dải (D-17) | CAO/THẤP |
| `SHP-001-Q20` | (a) TenantId attempt nullable + unique NULLS NOT DISTINCT | CAO/THẤP |
| `SHP-002-Q5` | Tên khoá không tiền tố, KeyPrefix lo | CAO/THẤP |
| `SHP-002-Q8` | v1 không AggregateVersion; sự kiện thô idempotent, chỉ chuyển tiến | VỪA/THẤP |
| `SHP-004-Q2` | Dùng IAddressDirectory + mã hãng (D-89, D-110) | CAO/THẤP |
| `SHP-004-Q5` | Như SHP-002-Q5 | CAO/THẤP |
| `SHP-004-Q6` | Dùng IHttpClientFactory như Address/PaymentHub; không gói mới | CAO/THẤP |
| `SHP-003-Q5` | Như SHP-002-Q5; BA bỏ `ecommerce:` | CAO/THẤP |
| `REC-003-Q6` | Thêm con vào nhóm eCommerceConsts.Permissions.Reconciliation | VỪA/THẤP |
| `SAL-006-Q7` | Sales sở hữu OrdRefunds; PaymentHub sở hữu PaymentTransaction (D-18) | VỪA/THẤP |
| `PRO-001-Q1` | FreeShipping lấy tiếp từ 0023 trong eCommerce:Promotion | VỪA/THẤP |
| `PRO-001-Q3` | Dùng ProPromotionMappings có index; không ConfigJson | VỪA/THẤP |
| `PRO-001-Q11` | Đã thống nhất (KeyPrefix); mỗi tài liệu tự sửa | CAO/THẤP |
| `PRO-002-Q2` | Không trigger DB; validator + guard một cửa ghi + test domain | VỪA/THẤP |
| `PRO-002-Q3` | Đã có Kernel Money/MoneyAllocator/RoundingPolicy; BA sửa "grep = 0" | CAO/THẤP |
| `REP-001-Q1` | Hai bút toán TransferOut/TransferIn (đã có) | CAO/THẤP |
| `REP-001-Q3` | Không thêm Dapper; EF Core SqlQuery<T>/FromSql | VỪA/THẤP |
| `REP-002-Q1` | `Rpt` (code thật D-47); BA sửa REP-001 | CAO/THẤP |
| `REP-002-Q3` | Đánh tiếp từ 0010 trong eCommerce:Reporting | VỪA/THẤP |
| `REP-002-Q6` | Không có tổng cứng; mỗi PR thêm metric + test chống trùng | CAO/THẤP |
| `NOT-001-Q7` | Tự viết thay {{…}} có escape HTML, không gói mới | VỪA/THẤP |
| `NOT-001-Q10` | Một module Notification, tiền tố `Not` dùng chung | VỪA/THẤP |
| `NOT-002-Q10` | Module mới modules/Notification; Messaging chỉ là hạ tầng sự kiện | VỪA/THẤP |
| `ADDR-001-Q7` | Câu cũ: KHÔNG, mã đóng ⇒ MaDaDong; dropdown lọc IsActive | CAO/THẤP |
| `ADDR-003-Q23` | (a) như BA; DEC chốt khi mở ADDR-001/ADDR-004 | VỪA/VỪA |
| `SAL-006-Q9` | RT-{yyyyMMdd}-{seq} theo tenant, không theo kênh | VỪA/THẤP |
| `PRO-002-Q4` | Có NULLS NOT DISTINCT (CLAUDE.md §7) | CAO/THẤP |
| `REP-001-Q2` | Câu cũ: chạy được (Tax migration + test btree_gist PG17) | CAO/THẤP |
| `REP-002-Q5` | Câu cũ: như REP-001-Q2 | CAO/THẤP |
| `NOT-001-Q4` | Chỉ vi; Locale đã có, thêm en = dữ liệu | CAO/THẤP |
