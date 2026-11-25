# Doc review — «PRC-002» SRS (WP-16, kiểm nhanh vòng 4)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/pricing/prc-002-gia-bac-thang-pipeline-tinh-gia.md` (1.425 dòng) |
| Ngày · vòng | 25/11/2026 · vòng 4 — **kiểm hẹp 2 điểm** theo `D-228` (không kiểm lại toàn bộ D1–D7) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N72/WP-16 hàng `W16-2`) |
| Đã đọc | §1 (94–113) · §5.2 (385–429) · §6.2 (582–606) · §8.1 (652–676) · §9 (700–749) · §14 (853–989). Code: `MoneyErrorCodes.cs`, `Currency.cs`, `Money.cs` (toàn văn) · `PricingDbContextModelCreatingExtensions.cs` (100–140 + grep toàn tệp). `decisions.md` `D-228`. |
| Máy | `check-feature-doc.ps1 -WP14` → exit **1** (2 `LOI`: 4 BR `BR-PRC-002-010/011/013/014` chưa có test ở §14 + thiếu mục "Đối chiếu mô hình Nhanh") — **được chấp nhận** theo `D-227` (nợ có chủ BA, **không** thuộc DoD WP-16) |

## Bối cảnh & phạm vi

Vòng 4, sau `D-228` (thay thế **một phần** `D-225` ở `TC-14` + dòng §14 592). ORCH giao **kiểm hẹp 2 điểm**:

1. `§14.1 TC-14` + `§9` dòng 726 đã đúng theo `D-228` và **khớp code** — không còn khẳng định sai (`"vnd" ⇒ 0002`, khác tiền tệ ⇒ `0005`, CHECK DB cho bảng của `PRC-002`).
2. **Không mở rộng phạm vi**: 4 dòng TC cho `BR-PRC-002-010/011/013/014` + mục "Đối chiếu mô hình Nhanh" vẫn **hoãn** theo `D-227` — đây là **đúng**, không phải lỗi.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Grep toàn tệp `CHECK`, `[A-Z]{3}`, `0002`, `0005`, `Money:0001`, `BR-PRC-002-01`; đọc TC-14 (872) ↔ §5.2 (402) ↔ §8.1 (658) | **1 P2** (Phát hiện #1): TC-14(d) nay nói **KHÔNG** có CHECK trên bảng `PRC-002`, trong khi §5.2:402 + §8.1:658 vẫn khẳng định **CÓ** |
| D2 chéo tài liệu / code / D-xx | TC-14 (a)–(d) ↔ `MoneyErrorCodes.cs:10/16/22`, `Currency.cs:24,69`, `Money.cs:36,49-57`, `PricingDbContextModelCreatingExtensions.cs:113`; `D-228` ↔ TC-14/§9; `D-227` ↔ §14 | **0** mâu thuẫn ở 2 điểm được giao; 1 P2 ở §5.2/§8.1 (kế thừa — Phát hiện #1) |
| D3 cơ chế thực thi | TC-14(d) khớp code (không CHECK); §8.1 invariant #1 ghi "CHECK ở DB" — cơ chế đó **không tồn tại**, chỉ domain `Currency`/`Money` kiểm | Gộp vào Phát hiện #1 |
| D4 SQL chạy được | SRS **không có** khối ```` ```sql ````/`bash`/`pwsh` (chỉ C#/json) ⇒ không có lệnh để chạy; không mở `psql` | không có khối sql |
| D5 test/probe/tệp có thật | Graph + Read: `Currency.FromCode` (`Currency.cs:67`), `Money.Cua` (`Money.cs:36`), `CK_PrcCostPrices_Currency_HopLe` (`PricingDbContextModelCreatingExtensions.cs:113`) đều có thật; `BR-PRC-002-01[0-4]` chỉ ở §1 (định nghĩa BR), **không** có dòng TC ở §14 | **0** phát hiện (đúng `D-227`) |
| D6 nhãn tin cậy | Không có nhãn ✅/🔶 **mới** trong 2 điểm bị `D-228` sửa (TC-14, §9:726) | **0** phát hiện |
| D7 14 quy tắc + đối chiếu Nhanh | `check-feature-doc -WP14` exit 1 (2 LOI); thiếu mục "Đối chiếu mô hình Nhanh" + 4 BR chưa có dòng test — `D-227` đã chốt **nợ có chủ BA**, không thuộc DoD WP-16 | **0** P1/P2; ghi **1 P3** truy vết (`D-227`) — không chặn |

**Bằng chứng đồ thị (bắt buộc cho kết luận về code):**

- `graph: codegraph_explore "Currency.FromCode Money.Cua MoneyErrorCodes CurrencyMismatch UnsupportedCurrency"` → `MoneyErrorCodes.CurrencyMismatch = eCommerce:Money:0001` (:10), `InvalidDivisor = 0002` (:13), `UnsupportedCurrency = eCommerce:Money:0003` (:16), `InvalidAmount = 0005` (:22); `Currency.FromCode` dùng `Dictionary(StringComparer.OrdinalIgnoreCase)` (:24) + `code.Trim()` (:69) ⇒ `"vnd"` trả `Vnd`, **không ném**; chưa đăng ký ⇒ `UnsupportedCurrency` (:74-75). `Money.Cua` (:36) và `YeuCauCungLoaiTien` (:49-57) ném `CurrencyMismatch` khi khác loại tiền. Khớp TC-14(a)–(c).

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P2 | §5.2 dòng 402 + §8.1 dòng 658 (đối chiếu §14.1 TC-14 dòng 872) | TC-14(d) nay khẳng định "**không** có CHECK `^[A-Z]{3}$` trên bảng của `PRC-002`" (khớp code: chỉ `CK_PrcCostPrices_Currency_HopLe` ở `PricingDbContextModelCreatingExtensions.cs:113`), nhưng §5.2 dòng 402 vẫn liệt kê CHECK `"Currency" ~ '^[A-Z]{3}$'` cho `TierPriceSets`/`TierPrices`/`PriceCalculationRecords`, và §8.1 dòng 658 vẫn ghi cơ chế bảo vệ invariant #1 là "CHECK ở DB" ⇒ **tự mâu thuẫn trong cùng tệp** ngay sau `D-228`; QA `W16-4` đọc §5.2/§8.1 có thể viết test chờ DB từ chối `"vnd"` (sẽ đỏ), hoặc BE thêm CHECK trái TC-14(d). | BA chọn một: (a) đánh dấu dòng 402/658 là "**kế hoạch — chưa triển khai**" và ghi rõ theo `D-228` chỉ `PrcCostPrices` (WP-17) có CHECK; hoặc (b) bỏ CHECK trên bảng `PRC-002` khỏi §5.2/§8.1 cho khớp code + TC-14(d). Nếu giữ, sửa tên cột `"Currency"` → `"CurrencyCode"` (tên cột thật — dòng 60/85/121/166/190). |
| 2 | P3 | §8.1 dòng 658 | Cơ chế "#1" ghi `Money.Of` — factory này **không có** trong code; thật là `Money.Cua` (`Money.cs:36`). `D-228` ③ đã ghi nợ BA sửa nhãn `Money.Of` nhưng chỉ dẫn `PRC-002` dòng 98/592; **dòng 658 còn sót** (TC-14(a) nay đã dùng đúng `Money.Cua`). | BA sửa `Money.Of` → `Money.Cua` ở dòng 658 (rà toàn tệp cùng dòng 98/592 theo `D-228` ③) ở `doc-gate` kế tiếp; không đổi hành vi, không chặn. |

## Không kiểm được

- **D4:** SRS không có khối `sql`/`bash`/`pwsh` nào (chỉ C#/json) ⇒ không có lệnh để chạy; **không** mở `psql`.
- **Test thực thi:** các `TC-*` ở §14 là **đặc tả**, chưa chạy trên PostgreSQL.
- **Toàn văn D1–D7:** vòng 4 là **kiểm hẹp 2 điểm** theo `D-228`; các mục ngoài phạm vi không rà lại toàn bộ.

## Vòng trước (vòng 3 và trước)

| # vòng 3 | Trạng thái | Ghi chú |
|---|---|---|
| 1 (P2 — `"vnd"` ⇒ `Money:0002`) | đã đóng | `D-228`/TC-14(a): `"vnd"` chuẩn hoá → `VND`, không ném |
| 2 (P2 — mã tiền tệ lạ ⇒ `0002`) | đã đóng | TC-14(b) nay = `eCommerce:Money:0003` (`UnsupportedCurrency`) |
| 3 (P3 — §9 ngoặc đơn "allowlist `none`") | đã đóng | §9:726 nay = "(`D-221`: allowlist = {`half-away-from-zero`}; **loại** `none`)" |
| 4 (P3 — 4 BR chưa có TC + thiếu mục Nhanh) | còn (nợ có chủ `D-227`) | **không phải lỗi**: hoãn có chủ, đóng khi `check-feature-doc -WP14` xanh |

## Ghi chú tích cực (không phải phát hiện)

- **Điểm 1 — ĐÚNG & KHỚP CODE.** TC-14 (872) đúng `D-228` (a)–(d): (a) `Currency.FromCode("vnd")` → `"VND"`, không ném (`Currency.cs:24,69`); (b) `"XXX"` → `eCommerce:Money:0003` (`UnsupportedCurrency`, `MoneyErrorCodes.cs:16`); (c) khác tiền tệ → `eCommerce:Money:0001` (`CurrencyMismatch`, `Money.cs:49-57`), **không** `0005`; (d) không CHECK trên bảng `PRC-002`, chỉ `PrcCostPrices` (`:113`, WP-17). Không còn `"vnd" ⇒ 0002` / khác tiền tệ ⇒ `0005` ở TC-14.
- **Điểm 2 — ĐÚNG PHẠM VI.** §14 chỉ có TC-01…TC-30; `BR-PRC-002-010/011/013/014` xuất hiện **chỉ** ở §1 (định nghĩa BR), **không** có dòng TC — đúng `D-227` (nợ có chủ BA). §9:726 đúng câu chữ `D-228`: "(`D-221`: allowlist = {`half-away-from-zero`}; **loại** `none`)".
- Hai phát hiện (#1 P2, #2 P3) nằm ở §5.2/§8.1 (ngoài 2 điểm `D-228` sửa trực tiếp), **không chặn BE** (`W16-2` không đụng xử lý tiền tệ); chúng chạm QA `W16-4` / migration sau — verdict giữ **DOC-OK**.
