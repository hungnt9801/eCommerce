# Hồ sơ uỷ quyền của chủ dự án cho `decider`

> **Trạng thái: HIỆU LỰC** từ 19/11/2026 (phiên `U-02`, `N28`) · `D-114` (luật chơi + chính sách) · `D-115` (bổ sung `D-71`, M2) · xem lại: **sau lô duyệt đầu tiên**
> (đo tỉ lệ đảo; ngưỡng 20 %/nhóm) và trước release đầu tiên của GĐ3.
>
> **Chỉ chủ dự án** đổi tệp này, trong phiên `U-02` hoặc khi tự yêu cầu rõ ràng. Không vai nào có vùng ghi tệp này
> (`.claude/scopes.json`); ORCH chỉ ghi khi chủ dự án đang ngồi cùng và đã xác nhận nội dung.
> Thiết kế: [de-xuat-uy-quyen-va-chay-tu-dong.md](de-xuat-uy-quyen-va-chay-tu-dong.md) §4 (K1), §5 (K2), §11 (M1–M6).
> Câu hỏi gốc + rổ: [phien-uy-quyen-cau-hoi.md](phien-uy-quyen-cau-hoi.md).

## 1. Mục tiêu và thứ tự ưu tiên

- **Khi trễ cắt gì trước:** phần phụ trong WP (công cụ vận hành P8 → UI tiện ích → tối ưu); ghi nợ có chủ. **Không** cắt BR, bất biến (`CLAUDE.md` §7), test.
- **Hai phương án ngang nhau:** chọn **rẻ nhất để lùi (T6)**, rồi đo được (T8). Việc thiếu phát hiện giữa chừng ⇒ ghi hàng đợi, trừ việc cần để đạt DoD của chính WP.
- **"Chuẩn" khi tài liệu mơ hồ (P15):** tài liệu đã duyệt / `D-xx` → Nhanh có bằng chứng đo được → đa số KiotViet/Sapo/Haravan/MISA → cấu hình với mặc định an toàn.
- **Sản phẩm của decider:** mọi quyết định ghi `TẠM CHỐT (DEC)` + `reviewBefore`, chủ dự án duyệt lô.

## 2. Luật chơi M1–M6, V1–V3

| # | Câu | Chủ dự án chốt | `D-xx` |
|---|---|---|---|
| M1 | Phiên uỷ quyền một lần + duyệt lô khi rảnh | **Đồng ý.** Mở rộng quyền decider (`D-113`) do người chốt. | `D-113`, `D-114` |
| M2 | `DOC-OK` ngoài danh sách đỏ ⇒ decider tạm duyệt để code trên nhánh | **Có, trừ danh sách đỏ** (§2.1). Không merge `main`. Cần OPS dựng cổng máy "merge `main` khi biên bản chưa ký ⇒ đỏ" **trước** khi code dựa trên tạm duyệt chạm `main`. | `D-115` |
| M3 | Agent merge vào nhánh tích hợp `tich-hop/<giai-doan>` khi cổng máy xanh | **Có; `main` chỉ người.** Người tạo nhánh + branch protection; cần CI trên `tich-hop` + cầu dao "CI đỏ ⇒ dừng". | `D-114` |
| M4 | Câu kế toán (~42): ai trả lời | **DEC tạm chốt + kế toán duyệt lô trước release.** Kế toán = chủ dự án (R3-3). | `D-114` |
| M5 | Câu NCC: ai liên hệ; agent gọi sandbox? | **Có sandbox; production cấm.** Token sandbox do người tạo, để ngoài repo. | `D-114` |
| M6 | Ngân sách / giờ chạy | **Không bật credits; chạy khi máy bật, ưu tiên ngoài giờ chat.** | `D-114` |
| V1 | Pilot | Công nhận Pilot-D đã chạy (N22–N26); Pilot-C = WP-15, mức L1 rồi L2. | `D-114` |
| V2 | Danh sách đỏ | Giữ 8 loại ở `ke-hoach-he-thong-agent.md` §4.3 + danh sách mã §2.1. | `D-114` |
| V3 | Agent chạy `dotnet ef database update` | **Cấm**; migration qua cổng / DbMigrator do người chạy. | `D-75` |

### 2.1. Danh sách đỏ (M2 — decider **không** được tạm duyệt; người hoặc kế toán ký biên bản)

- **Đỏ cứng (tiền / kế toán / đối soát):** `ACC-001` · `REP-002` · `REP-003` · `REC-003` · `SHP-003` · `SAL-006`.
- **Đỏ có điều kiện:** `REP-001` (tới khi chốt giá vốn + ghi nhận) · `LOY-001` (tới khi chốt P13 được người duyệt) · `SHP-004` (tới khi `SHP-004-Q1` có trả lời GHTK)
  · `ADDR-003` (tạm duyệt được, **trừ** PII `freeText`: `D-101`/`D-105`/`Q21` chặn release) · `NOT-002` (tạm duyệt thiết kế; khoá HMAC production + chọn kênh Zalo là việc người).
- **Họ mã luôn đỏ (ngoài WP-14):** `EINV-*` · `SIGN-*` · `PAY-*` · mọi tài liệu sửa `XC-001` · mọi thay đổi phá huỷ contract liên module hoặc schema.
- **Tạm duyệt được:** `ADDR-001` · `ADDR-002` · `SHP-001` · `SHP-002` (phần ghi nhận doanh thu vẫn chặn release) · `PRO-001` · `PRO-002` (sau P3) · `NOT-001`.
- **Ngoại lệ WP-17 (`D-318`, người, phiên uỷ quyền `U-17` 28/11/2026):** `decider` được duyệt thay **G1** (`prc-001` §15.1/§20.4 + `inv-001` §0 — **không** `REP-003`) và **G2** (ARCH giá vốn `W17-1`) tại cổng `W17-0`/`W17-1` **có điều kiện** (8 tiêu chí C5 + 7 điều kiện C4 của `D-318`); `REP-003` §0/Q11 (**đỏ cứng**) **chủ dự án ký** kiểu (c): ký ngay, ORCH đối chiếu diff ở `W17-0`, lệch từng dòng phạm vi ⇒ chữ ký **vô hiệu**; grain giá vốn theo **SKU toàn hệ thống**; giữ mức **L1**. Mọi thứ khác vẫn của người.

## 3. Chính sách theo nhóm câu (R1)

Tất cả theo phương án **(a)** của `phien-uy-quyen-cau-hoi.md` §2 (chủ dự án chọn đúng khuyến nghị cả 16 câu). Nội dung `D-114`.

| Nhóm | Chính sách | Áp cho |
|---|---|---|
| P1 Ngưỡng | Setting theo tenant; trống = luôn cần duyệt, dung sai 0, cờ ngoại lệ tắt; chặn release tới khi kế toán điền | `SHP-003-Q6`, `LOY-001-Q6`, `SAL-006-Q2`, `REC-003-Q10`, ngưỡng COD `SHP-001-Q10` |
| P2 Giá vốn | Bình quân gia quyền di động, chụp vào fact lúc bán, có cấu hình; chặn release tới khi kế toán duyệt; `CostPrice` chưa có ⇒ DEC xếp vào WP theo `D-69` | `REP-001-Q7`, `REP-002-Q2`, `REP-003-Q2` |
| P3 Lãi gộp + tiền tính | DEC tạm chốt theo thông lệ + `XC-001` + `RoundingPolicy`/`MoneyAllocator` (LG1 = DT ròng − giá vốn; LG2 = LG1 − phí sàn − phí ship trả hãng; phí sàn dự kiến có cờ `SettlementPending`); `reviewBefore = release`; kế toán duyệt lô | `REP-002-Q7`, `REP-003-Q6`, `ACC-001-Q8/Q9`, `NOT-002-Q9` |
| P4 Ghi nhận | Khi Delivered đã đối soát; kỳ = ngày UTC+7 của sự kiện; kỳ chốt không sửa, lệch điều chỉnh vào kỳ sau | `SHP-002-Q6`, `REP-001-Q5` |
| P5 Thiếu sự thật từ NCC | Thiết kế theo giả định xấu nhất, khả năng tuỳ chọn của adapter + Setting, fail-closed, `reviewBefore = release` | `SHP-003-Q8`, `NOT-002-Q4`, mọi việc chờ NCC |
| P6 Máy gợi ý, người xác nhận | Thêm lệnh thủ công quyền riêng + nhật ký append-only; máy chỉ gợi ý | `SHP-001-Q19`, `ADDR-002-Q4` |
| P7 PII | Phạm vi hẹp nhất (không lưu thô, không sửa HTML, không gửi ra ngoài), mở bằng cờ sau | `ADDR-002-Q6`, `NOT-001-Q2/Q9`, `ACC-001-Q6`, `NOT-002-Q2/Q5` |
| P8 Ngưỡng vận hành | ABP Setting + mặc định DEC đề xuất, đo lại sau pilot, `reviewBefore = release` | 16 câu (§2 R1 của tệp câu hỏi) |
| P9 Gói mới | Được nếu MIT/Apache-2.0 + dòng `D-xx`; một luồng sửa `Directory.Packages.props`; thương mại/copyleft người duyệt; mặc định không thêm. **License ClosedXML/MiniExcel chưa tra web — người kiểm lại trước khi thêm.** | `REC-003-Q5` |
| P10 Ẩn danh ⚠ vùng cấm | **Không mở endpoint `[AllowAnonymous]` nào** tới khi có rate limit + người duyệt từng endpoint (người chốt, không uỷ quyền được) | `ADDR-001-Q5`, `PRO-002-Q7`, `PRO-001-Q5` |
| P11 Công cụ vận hành | CÓ: chỉ đọc, permission riêng; gỡ/ghi qua cửa ghi có sẵn + audit; dry-run hàm thuần; ưu tiên thấp, trễ thì cắt trước | `ADDR-003-Q8`, `PRO-001-Q7/Q9`, `PRO-002-Q10`, `REP-003-Q9`, `NOT-001-Q12` |
| P12 Phân quyền | Mỗi hành động một permission, mặc định admin tenant/vai sát việc; "khác người" là Setting mặc định tắt; luôn audit | `SHP-001-Q10`, `PRO-002-Q5`, `NOT-001-Q1/Q3` |
| P13 Người mua | Giá trị mẫu thận trọng (đổi trả 7 ngày; một tỉ lệ điểm chung; không hiện nợ điểm cho khách; điểm không hết hạn), cấu hình theo tenant, `reviewBefore = release`, **chủ dự án duyệt một lần** | `SAL-006-Q1`, `LOY-001-Q3/Q4/Q5` |
| P14 Ưu tiên | Xem §1 | mọi hàng |
| P15 Chuẩn | Xem §1 | mọi hàng |
| P16 Vận hành bộ chạy | Báo cáo: tệp mỗi ngày `docs/agents/bao-cao-chay/<ngày>` + tóm tắt khi bộ chạy dừng. Treo: câu vùng cấm/§6 không bao giờ tự chốt; câu khác treo > 7 ngày ⇒ DEC `TẠM CHỐT` phương án trung lập, `reviewBefore = release` | hồ sơ §7 |

## 4. Ngoại lệ đã chốt riêng (R2)

Chủ dự án **đồng ý tất cả 30 khuyến nghị §15** (`phien-uy-quyen-cau-hoi.md` §3.1) và **36 dòng `D-xx`** (24 R0 + 11 dòng R2 + `D-75`), **không có ngoại lệ**.
BA áp 30 câu vào tài liệu ở các phiên `/doc-gate` sau; 3 câu §15 cũ (code đã trả lời) BA sửa thành Closed.

| Tài liệu | Câu | Chủ dự án chọn (khác khuyến nghị) | `D-xx` |
|---|---|---|---|
| _(không có)_ | | | |

## 5. Việc không ai quyết được ngay (R3) — ai lo, khi nào

Chủ dự án nhận **tất cả** việc dưới đây. Dây chuyền làm trong lúc chờ: adapter + cấu hình + fail-closed + chặn release (`continueWhileWaiting`).

| Việc | Phụ thuộc | Ai lo | Khi nào |
|---|---|---|---|
| R3-1 Liên hệ GHTK (mã địa chỉ, đổi COD, webhook) | `ADDR-002-Q1/Q2`, `SHP-004-Q1/Q3/Q7`, `SHP-003-Q1` | chủ dự án | trước duyệt `SHP-004` |
| R3-2 Tài khoản sandbox GHN + SĐT nhận OTP (secret ngoài repo) | `SHP-001-Q2/Q3`, `SHP-002-Q1`, `SHP-003-Q2/Q3` | chủ dự án | trước WP code `SHP-001` |
| R3-3 Kế toán (chế độ tài khoản, ánh xạ Nợ/Có, file mẫu Bravo/MISA, hạn lưu trữ luật định; duyệt lô M4) | `ACC-001-Q1/Q2`, `REP-003-Q7`, `SAL-006-Q5` | chủ dự án | trước WP code `ACC-001` / trước release |
| R3-4 File bảng kê COD thật GHN/GHTK + hỏi GHN có API đối soát | `REC-003-Q2`, `SHP-003-Q9` | chủ dự án | trước WP code `REC-003` |
| R3-5 Pháp chế: ToS robot tải bảng kê; thay NĐ 13/2023 + hạn lưu `freeText` | `REC-003-Q1`, `ADDR-003-Q21`, `D-101` | chủ dự án | trước release |
| R3-6 Chọn kênh 2 (Zalo/SMS) + NCC, ký hợp đồng, tạo khoá HMAC production | `NOT-002-Q1/Q3/Q5` | chủ dự án | trước WP code kênh 2 (email đi trước) |
| R3-7 File ĐVHC công bố của cơ quan nhà nước (chưa xác minh NQ/QĐ nào) | `ADDR-003-Q2`, `D-93` | chủ dự án | trước WP code đường nạp production |
| R3-8 Hạ tầng lưu file production (object storage có phí) | `ACC-001-Q4` | chủ dự án | trước release `ACC-001` |

Dòng `D-xx` R3: `D-101` (PII trong ledger append-only) **giữ `TẠM CHỐT`** — chờ R3-5.

## 6. Những điều decider KHÔNG BAO GIỜ quyết, dù có hồ sơ

`CLAUDE.md` §5 + `decision-policy` §3 mục 1–3: merge vào `main` · đổi branch protection · sửa/xoá dữ liệu thật · gọi API production
(thanh toán, HĐĐT, SMS, Zalo) · tạo/đổi secret · chọn NCC có phí / ký hợp đồng · điền dữ liệu bên ngoài (trường XML thuế, endpoint hãng) bằng suy đoán.
Thêm theo `P10`: mở endpoint `[AllowAnonymous]`. Thêm theo `V3`: chạy `dotnet ef database update`.

## 7. Câu treo, xem lại, thu hồi

- Câu chuyển người quá hạn: theo `P16` — câu vùng cấm/§6 không bao giờ tự chốt; câu khác treo > 7 ngày ⇒ DEC `TẠM CHỐT` phương án trung lập, `reviewBefore = release`.
- Đo tỉ lệ quyết định bị chủ dự án đảo khi duyệt lô; vượt 20 % ở một nhóm ⇒ nhóm đó về "chỉ `TIEN_LE`" (ghi `D-xx`).
- Thu hồi toàn bộ: đổi dòng "Trạng thái" thành **THU HỒI** — decider trở về chính sách cũ ngay phiên sau.
