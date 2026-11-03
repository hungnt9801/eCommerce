# 08 — Chia phiên làm việc (session protocol)

> Trả lời câu hỏi: **"chạy cả kế hoạch 12 tuần trong một session hay chia nhiều session?"**
> **Trả lời ngắn: chia nhiều session** — 1 session ≈ 1 work package, mở/đóng bằng số đo.
> Tài liệu này là quy ước để phiên sau **không phải đọc lại toàn bộ** lịch sử.

---

## 1. Vì sao **không** chạy một session

| Lý do | Hệ quả nếu cố chạy liền |
|---|---|
| **Context có hạn** — 12 tuần ≈ 120 dev-week, mỗi WP đã tốn 30–80k token cho đọc code + build/test log | Đến WP-04 sẽ phải "nhớ lại" bằng tóm tắt ⇒ **mất chi tiết quyết định**, dễ sửa sai chỗ đã chốt |
| **Lỗi tích luỹ** — một migration sai ở WP-03 sẽ làm mọi bước sau vô nghĩa | Càng dài, chi phí quay lui càng lớn; không có điểm dừng xanh để so sánh |
| **Cửa sổ kiểm chứng** — `dotnet test` 1.634 test + build + DB probe mỗi vòng | Chạy 6 WP trong 1 session ⇒ hàng chục vòng đo, log nuốt hết ngân sách context |
| **Bài học của chính repo** | GĐ1 đã chia **S1…S20**, có `SESSION-KICKOFF.md` + biên bản bàn giao; đó là lý do 1.634 test vẫn xanh sau 20 phiên |

**Ngược lại, cũng đừng chia quá nhỏ** (mỗi hàm một session): chi phí dựng lại ngữ cảnh (đọc 2–4 tài liệu + chạy baseline) ≈ 20–30% thời gian mỗi phiên.

---

## 2. Nguyên tắc chia

1. **1 session = 1 work package** (WP-01…WP-10) hoặc **nửa WP** nếu WP > 10 ngày công.
2. **Chỉ cắt ở điểm xanh**: `dotnet build` 0 error + `dotnet test` ≥ baseline. **Không** cắt giữa migration/backfill.
3. **Mỗi session một nhánh**: `feature/wp-03-catalog` → merge khi xanh; không làm 2 WP trên 1 nhánh.
4. **Mở session bằng baseline, đóng session bằng số đo** — nếu số lệch baseline cũ, dừng và báo, không tự diễn giải.
5. **Không chạy song song 2 session cùng module.** Song song chỉ khi chạm **file rời nhau** (ví dụ: FE dựng màn mới + BE viết endpoint khác module) và phải có người merge.
6. **Mọi quyết định mới** ghi vào [decisions.md](decisions.md) trong session đó, không để "nhớ trong đầu".

---

## 3. Bản đồ session đề xuất (GĐ0–GĐ2)

| Session | WP | Nội dung | Điều kiện xanh cuối session |
|---|---|---|---|
| **N1** ✅ đã xong | WP-01 + WP-02 | Next.js shell + login + BFF, `HttpApi` v1, 3 endpoint | login thật → trang 200; `next build` ✓; 1634 test |
| **N2** ✅ đã xong | WP-03 (nửa đầu) | `ProductType/Unit/ParentId/VAT` + entity `ProductUnit`, migration **51** additive | build + test xanh (1671); tạo SP 3 ĐVT qua API — [handover](handover-n2.md) |
| **N3** ✅ đã xong | WP-03 (nửa sau) | Backfill `ProductVariant → Product con` (dry-run → thật), luật cha–con + publish theo ĐVT gốc, `OrderLine.ProductId`, adapter đọc cũ | backfill **2 lần cùng kết quả** (2 → 2 rồi 2 → 0, DB y nguyên); `/SanPham` **200** cho cả hai mô hình; 1684 test — [handover](handover-n3.md) |
| **N4** ✅ đã xong | WP-04 (BE) | 8 chiều tồn + `Mode` phiếu + bút toán theo `(mode, chiều)` + API `/api/v1/stock/*`, migration **53**; `N4b`: `decimal(18,3)` hoá 16 cột số lượng, migration **54** | bất biến `tổng sổ cái = ảnh chụp` **10 = 10**; nhập 10 → tồn 10; số lẻ sống sót (`1,5 kg`, 1000 × 0,001 = 1,000) — probe HTTP 12 ca `exit 0`; **1773** test — [handover N4](handover-n4.md) · [N4b](handover-n4b.md) |
| **N5** ✅ đã xong | WP-04 (FE) | DS phiếu (tab + bulk + cột) · tạo phiếu (form động) · chi tiết (ghi sổ/huỷ) · tồn 9 chiều · in phiếu · **tiền (đơn giá → thành tiền → tổng)** | Post → tồn đổi **trên UI** (`Bán 10`); Post lại **403 `0010`**; bulk **2 thành công / 0 lỗi**, skip **1 bỏ qua**; tiền **75.000/6.000/81.000**; probe FE **19/19** — [handover](handover-n5.md) |
| **N6** ✅ đã xong | WP-05 | 18 trạng thái (append 9) · `OrderType/OrderSource/OrderReasonCode` · 11 cột đơn · bảng cạnh Nhanh + 15 cạnh huỷ · nhãn suy từ 4 trục · API `/api/v1/orders` (DS **14 tab** · chi tiết · `transitions` · `cancel` · `history`), migration **56** · FE 2 màn | đơn đi trọn `Mới → Thành công` (**8/8 lời gọi 200**); xác nhận ⇒ `OnHand 10→8` (`ChotGiuCho`); hoàn ⇒ `OnHand 7→8` + phiếu `HOAN-…`; **403 đúng mã** `0003`/`0001`/`0010`; probe **29 lời gọi** `exit 0`; **1855** test — [handover](handover-n6.md) |
| **N7** | WP-06 (nửa đầu) | `RetailBill` + `CashSession` + thanh toán + in | bán 3 SP, chia 2 phương thức, in, kết ca |
| **N8** | WP-06 (nửa sau) | Offline IndexedDB + `ClientBillId` idempotent | rớt mạng vẫn lưu; gửi 2 lần không tạo trùng |
| **N9** | WP-07 + WP-08 | Khách/nhân viên + read-model + Dashboard | data scope lọc đúng; báo cáo khớp `SUM` |
| **N10** | WP-09 + WP-10 | Playwright 8 luồng + CI + Docker | 8/8 E2E xanh; `pnpm gen:api` không diff |

> ⏱ Mỗi session N2…N10 ước **2–4 giờ máy chạy** (kể cả build/test). Nếu một session chưa xong WP: **cắt ở điểm xanh**, ghi handover, session sau làm tiếp — **không** kéo dài quá 1,5× dự kiến.

---

## 4. Mở session (kickoff) — copy 6 dòng này

```powershell
cd D:\Projects\Tools\eCommerce
git status --short          # phải sạch hoặc chỉ có docs
git switch <nhánh-của-WP>
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests --no-build
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

Câu mở đầu cho agent (dán vào session mới):

> Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-0X
> và handover gần nhất trong `docs/nhanh/`. Chạy baseline 6 lệnh ở `session-protocol.md` §4, ghi số,
> rồi làm **đúng WP-0X**. Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.

---

## 5. Đóng session (handover) — mẫu

Tạo tệp handover (đặt tên `handover-Nx.md`, cùng thư mục này) với **đúng 7 mục** (không viết lan):

1. **Đã xong** — việc + số đo (test/build/endpoint), kèm lệnh kiểm.
2. **Chưa xong / cắt giữa** — chính xác đang ở đâu, file nào đang sửa dở.
3. **Migration đã áp** — số hiệu + DB nào; cách lùi.
4. **Quyết định mới** — dòng `D-xx` đã thêm vào [decisions.md](decisions.md).
5. **Nợ / rủi ro** — điều gì sẽ nổ nếu phiên sau không xử lý.
6. **Việc đầu tiên của phiên sau** — 1–3 gạch đầu dòng, kèm lệnh.
7. **Trạng thái nhánh** — commit SHA, đã push chưa, PR nào.

> 🔑 **Bài học từ GĐ1 (đã trả giá):** handover phải ghi **số đo**, không ghi "đã ổn".
> Một dòng *"test xanh"* mà không có **tên lệnh + số ca** thì phiên sau không phân biệt được
> "xanh thật" với "không chạy".

---

## 6. Dấu hiệu phải dừng session (dù chưa xong WP)

| Dấu hiệu | Hành động |
|---|---|
| 3 vòng liên tiếp sửa–build–fail cùng một lỗi | Dừng, ghi handover, mô tả **giả thuyết sai** đã loại |
| Context đã dùng > ~70% | Dừng ở điểm xanh gần nhất, handover |
| Phải sửa > 5 file ngoài phạm vi WP | Dừng: phạm vi đang trôi → cập nhật plan trước |
| Test đỏ do thay đổi ngoài dự kiến | **Không** sửa test cho xanh; dừng, báo |
| Migration chạm DB có dữ liệu mà chưa backup | Dừng, snapshot trước (`pg_dump`) |

---

## 7. Khi nào **được** chạy song song

| Được | Không được |
|---|---|
| Session khảo sát/đọc tài liệu + session code (file rời nhau) | 2 session cùng sửa `Product`/`StockDocument`/`Order` |
| FE dựng màn mới **trên API đã có** + BE làm endpoint **module khác** | 2 session cùng chạy `dotnet ef migrations add` (đụng `ModelSnapshot`) |
| 2 session đọc/viết tài liệu khác thư mục | 2 session cùng sửa `eCommerce.slnx` / `Directory.Packages.props` |

---

## 8. Kết luận cho trường hợp của bạn

- **Chia 10 session** (N1 đã xong) cho GĐ0–GĐ2, đúng bản đồ §3.
- Session hiện tại nên **đóng sau khi commit** phần WP-01/02 — đã đủ lớn (khảo sát + 6 tài liệu + 2 project + build/login chạy thật).
- **N2** (WP-03 nửa đầu), **N3** (WP-03 nửa sau), **N4** + **N4b** (WP-04 BE), **N5** (WP-04 FE) và
  **N6** (WP-05) **đã xong** — đóng bằng số đo, xem [handover-n2](handover-n2.md), [handover-n3](handover-n3.md),
  [handover-n4](handover-n4.md), [handover-n4b](handover-n4b.md), [handover-n5](handover-n5.md),
  [handover-n6](handover-n6.md).
  📌 `N3` **không** làm form sản phẩm FE (việc FE 3 ngày của WP-03) — vẫn có thể mở một session **FE độc lập**
  trên API đã có (điều kiện §7: chạm file rời nhau với BE). `N5` đã dựng sẵn `StockBadge`, `BalancesTable`,
  `ProductPicker`, `lib/format.ts` (số 3 lẻ + `vi-VN`) ⇒ form sản phẩm dùng lại được, **đừng viết lại**.
  📌 `N6` **chưa** làm 4 bảng phụ của WP-05 (`OrderLabel`/`OrderPacking`/`OrderHandover`/`OrderComplaint`),
  **chưa** có bulk đổi trạng thái và tab **Thanh toán** của đơn (thuộc WP-06) — xem [handover-n6](handover-n6.md) §2.
- Session kế tiếp: **N7 — WP-06 (nửa đầu)**: `RetailBill` + `CashSession` + thanh toán + in. Mở bằng 6 lệnh ở §4
  và câu mở đầu ở §4. Phiên `N6` **đã commit** `N5` + `N6` thành **`036adb6`** trên `feature/wp-03-catalog`
  — phiên sau mở bằng `git switch` + 6 lệnh §4, **đừng** `git add -A` (còn 51 mục rác khảo sát Nhanh).
