# BIÊN BẢN DUYỆT TÀI LIỆU — `Q5` (29/10/2026)

> 🎯 **Một việc:** ghi nhận **quyết định duyệt tài liệu** của **chủ dự án** cho phạm vi **`7` tài liệu `XC-*` +
> `19` tài liệu GĐ1** — tức **`26` tài liệu**, là **toàn bộ** phạm vi của **cổng 0** (`Q5`, `§A.5`).
>
> 📌 Biên bản này là **bản ghi truy vết TRUNG TÂM** của `Q5`: trạng thái duyệt **không** được chép vào tiêu đề /
> `§0` của từng tài liệu (xem §5). Khi cần đối chiếu *"tài liệu này đã được duyệt chưa?"* ⇒ **tra biên bản này trước**.

---

## 1. Thông tin phiên

| Mục | Giá trị |
|---|---|
| **Ngày quyết định** | **29/10/2026** |
| **Người quyết** | **Chủ dự án** |
| **Hình thức** | **DUYỆT** — quyết định của chủ dự án, ghi nhận bằng biên bản này. 🚫 **KHÔNG** phải *"thoả thuận song song bằng văn bản"* (phương án thứ hai nêu ở `Q5`) |
| **Câu hỏi được trả lời** | `Q5` — *"Duyệt tài liệu: duyệt thật hay thoả thuận song song bằng văn bản?"* ⇒ **duyệt thật** |
| **Người lập biên bản** | Agent tài liệu (phiên GĐ1, 29/10/2026) — **ghi lại** quyết định, không tự quyết |
| **Căn cứ** | Quyết định của chủ dự án trong **phiên làm việc ngày 29/10/2026** (cùng phiên `S14H`) |
| **Bằng chứng nguồn** | [`docs/features/README.md`](features/README.md) §3 (phân bố theo giai đoạn — nguồn của danh sách 7 + 19) · [`docs/ban-giao-gd1-con-lai.md`](ban-giao-gd1-con-lai.md) §3 `Q5` + §6 cổng 0 |

---

## 2. Phạm vi được duyệt — `26` tài liệu

### 2.1 Bảy tài liệu **nền** (`XC-001 … XC-007`)

| # | Mã | Tệp |
|---:|---|---|
| 1 | `XC-001` | [Money, Currency & chính sách làm tròn](features/cross-cutting/XC-001-money-currency-lam-tron.md) |
| 2 | `XC-002` | [Outbox / Inbox & chống trùng sự kiện](features/cross-cutting/XC-002-outbox-inbox-chong-trung-su-kien.md) |
| 3 | `XC-003` | [Khoá phân tán: quy ước & phạm vi](features/cross-cutting/XC-003-khoa-phan-tan-quy-uoc.md) |
| 4 | `XC-004` | [Xác thực, phân quyền & chiều tenant](features/cross-cutting/XC-004-xac-thuc-phan-quyen-tenant.md) |
| 5 | `XC-005` | [Observability: log có cấu trúc, CorrelationId & metric](features/cross-cutting/XC-005-observability-log-metric.md) |
| 6 | `XC-006` | [Phân loại thuế GTGT theo SKU & mức thuế suất theo thời gian](features/cross-cutting/XC-006-phan-loai-thue-gtgt.md) |
| 7 | `XC-007` | [CI/CD: cổng kiểm tự động và quy trình phát hành](features/cross-cutting/XC-007-ci-cd-pipeline.md) |

> ⚠️ **`XC-006-phieu-hoi-ke-toan.md` KHÔNG nằm trong phạm vi** — đó là **phiếu hỏi kế toán**, không phải tài liệu
> thiết kế ([`README`](features/README.md) §"Tệp khác trong cây").

### 2.2 Mười chín tài liệu **GĐ1** — "Bán trên website"

| # | Mã | Tệp |
|---:|---|---|
| 1 | `CAT-001` | [Sản phẩm và biến thể (SKU)](features/catalog/cat-001-san-pham-va-bien-the-sku.md) |
| 2 | `CAT-002` | [Cây danh mục & thuộc tính/tổ hợp biến thể](features/catalog/cat-002-cay-danh-muc-thuoc-tinh-bien-the.md) |
| 3 | `CAT-003` | [Media, SEO slug và hiển thị theo kênh](features/catalog/cat-003-media-seo-slug-hien-thi-theo-kenh.md) |
| 4 | `PRC-001` | [Bảng giá và giá theo kênh](features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md) |
| 5 | `PRC-002` | [Giá bậc thang (tier) và pipeline tính giá + làm tròn](features/pricing/prc-002-gia-bac-thang-pipeline-tinh-gia.md) |
| 6 | `INV-001` | [Nhập, chuyển, kiểm kê và sổ cái kho (append-only)](features/inventory/inv-001-nhap-chuyen-kiem-ke-so-cai-kho.md) |
| 7 | `INV-002` | [Giữ chỗ (reservation) + TTL và job nhả kho](features/inventory/inv-002-giu-cho-reservation-ttl.md) |
| 8 | `INV-003` | [Phân bổ tồn theo kênh và tra tồn khả dụng](features/inventory/inv-003-phan-bo-ton-theo-kenh.md) |
| 9 | `SAL-001` | [Giỏ hàng](features/sales/sal-001-gio-hang.md) |
| 10 | `SAL-002` | [Checkout và đặt hàng (tính lại giá + giữ kho)](features/sales/sal-002-checkout-va-dat-hang.md) |
| 11 | `SAL-003` | [State machine 4 trục và lịch sử trạng thái](features/sales/sal-003-state-machine-4-truc.md) |
| 12 | `SAL-004` | [Huỷ đơn và compensation (bù trừ)](features/sales/sal-004-huy-don-va-compensation.md) |
| 13 | `SAL-005` | [Webhook đơn từ sàn (idempotent, chịu được sai thứ tự)](features/sales/sal-005-webhook-don-tu-san.md) |
| 14 | `CUS-001` | [Hồ sơ khách hàng và sổ địa chỉ](features/customers/cus-001-ho-so-khach-hang-so-dia-chi.md) |
| 15 | `REC-001` | [Đối soát thanh toán (querydr định kỳ)](features/reconciliation/rec-001-doi-soat-thanh-toan-querydr.md) |
| 16 | `PAY-001` | [Cổng thanh toán VNPay: tạo giao dịch, IPN/webhook, hoàn tiền & đối soát](features/payment/pay-001-cong-thanh-toan-vnpay.md) |
| 17 | `WEB-001` | [UI admin: khung, đăng nhập, menu, phân quyền](features/web/web-001-ui-admin-khung-dang-nhap-phan-quyen.md) |
| 18 | `WEB-002` | [Storefront: duyệt và chi tiết sản phẩm (SEO)](features/web/web-002-storefront-duyet-chi-tiet-san-pham.md) |
| 19 | `WEB-003` | [Storefront: giỏ hàng và checkout](features/web/web-003-storefront-gio-hang-checkout.md) |

**Tổng: `7` + `19` = `26` tài liệu** — danh sách **lấy từ [`README`](features/README.md) §3 + §6**, không viết tay
theo trí nhớ; tên tệp đối chiếu với **cây thật** (`docs/features/**`) và được cổng `kiem-link-tai-lieu.ps1` kiểm.

---

## 3. Nội dung duyệt

Duyệt 26 tài liệu trên **là thiết kế đủ điều kiện để làm code theo** (`design-doc-guidelines.md` §4 bước 4 —
*"Approved = ĐIỀU KIỆN ĐỂ BẮT ĐẦU CODE"*), với nghĩa:

1. **Đặc tả nghiệp vụ** (state machine, guard, bất biến, mã lỗi, phân quyền) **được chấp nhận** làm nguồn sự thật;
2. Code **đi theo tài liệu**; nếu buộc phải lệch ⇒ **sửa tài liệu trước**, trong cùng thay đổi
   (`design-doc-guidelines.md` §4 "quy tắc vàng");
3. Các **câu hỏi mở / nợ có mã** *trong* tài liệu (ví dụ `Q2`, `Q4`, `Q6`, nợ ETO của `SAL-006` §32.5) **vẫn giữ
   nguyên trạng thái nợ** — duyệt tài liệu **không** có nghĩa là các nợ đó đã đóng.

---

## 4. Hệ quả đo được

| Hạng mục | Trước | Sau biên bản này |
|---|---|---|
| Cổng 0 — *tài liệu GĐ1 + `XC-001…007` Approved* | ❌ `1/55` (chỉ `ADDR-004`) | ✅ **`26/26` trong phạm vi cổng** |
| Toàn bộ 55 tài liệu thiết kế | `1/55` Approved | **`27/55`** Approved (`26` phạm vi này + `ADDR-004` duyệt 17/10/2026) |
| `Q5` (`ban-giao` §3) | ❓ mở | ✅ **đã chốt 29/10/2026 — duyệt thật** |

> 📌 `ADDR-004` (duyệt 17/10/2026) thuộc **GĐ0**, **không** nằm trong 26 tài liệu của biên bản này ⇒ hai con số
> **không** cộng trùng: `1 + 26 = 27`.

---

## 5. Vì sao **KHÔNG** sửa tiêu đề / `§0` của 26 tài liệu

Quyết định **có chủ ý**: biên bản này là **bản ghi truy vấn trung tâm**, **không** sửa dòng `**Trạng thái**` hay
`§0` của từng tệp.

| Lý do | Hệ quả |
|---|---|
| **26 lần sửa rời rạc** dễ lệch nhau (sửa 24/26 rồi quên 2) và không ai đọc hết 26 diff | Một bản ghi duy nhất, **đếm được** (`26`) |
| Mỗi tài liệu còn **lịch sử thay đổi riêng**; sửa trạng thái ở 26 chỗ làm nhiễu lịch sử của từng feature | Lịch sử từng tài liệu **nguyên vẹn** |
| Truy vết `Q5` cần **một** chỗ để trả lời *"ai duyệt, ngày nào, phạm vi nào"* | Trả lời được ngay từ tệp này |
| Sửa tiêu đề dễ kéo theo **sửa nội dung** ngoài phạm vi (đổi cả `Draft` → `Approved` trong khi vẫn còn nợ) | Không đụng nội dung nào ngoài 3 tệp chỉ mục |

⚠️ **Hệ quả phải biết:** dòng `**Trạng thái**` trong header của 26 tệp **vẫn ghi `Draft`** — đó **không** phải
"chưa duyệt", mà là **cố ý** để biên bản làm nguồn duy nhất. 🔑 Câu đúng: *"26 tài liệu này **đã được duyệt**
theo biên bản 29/10/2026; nhãn `Draft` trong tệp là **ảnh chụp cũ**, chưa cập nhật."*

---

## 6. Giới hạn — điều biên bản này **KHÔNG** làm

| # | KHÔNG làm | Vì sao |
|---:|---|---|
| 1 | **KHÔNG** thay thế `Q6` (credential VNPay: `VnPay:TmnCode` / `HashSecret`) | `Q6` là **bí mật vận hành**, không phải quyết định tài liệu; duyệt `PAY-001` **không** tạo ra credential |
| 2 | **KHÔNG** làm `S19` xong | `S19` cần **một giao dịch sandbox thật**; hiện vẫn 🟡 `FakePaymentProvider` ⇒ cổng 1 của GĐ1 **vẫn đỏ** |
| 3 | **KHÔNG** đóng các `Q` khác (`Q2` đường ghi `CostPrice`, `Q4` hoàn tiền tự động) | Mỗi `Q` là một quyết định riêng |
| 4 | **KHÔNG** đổi schema / migration / phân quyền / route / mã nguồn | Biên bản là **tài liệu**; `0` thay đổi kỹ thuật |
| 5 | **KHÔNG** tuyên bố *"GĐ1 xong"* | Cổng 0 nay xanh, nhưng cổng 1 (`S19`) và cổng 2 (2 instance cho **giữ chỗ**) **chưa** xanh (`ban-giao` §6) |

---

## 7. Truy vết — nơi đã ghi nhận quyết định này

| Tệp | Ghi nhận |
|---|---|
| [`features/README.md`](features/README.md) §2 | dòng trỏ về biên bản này (kèm ghi chú `1/55` là **ảnh chụp 17/10/2026**) |
| [`ban-giao-gd1-con-lai.md`](ban-giao-gd1-con-lai.md) §3 | `Q5` nay là ✅ đã chốt |
| [`ban-giao-gd1-con-lai.md`](ban-giao-gd1-con-lai.md) §6 | cổng 0: ❌ → ✅ kèm link biên bản |
| [`features/sales/sal-003-state-machine-4-truc.md`](features/sales/sal-003-state-machine-4-truc.md) §3 | `PartiallyReturned` + 2 cạnh 31/32 (`S14H`, cùng ngày) |
| [`features/sales/sal-006-tra-hang-rma-hoan-tien.md`](features/sales/sal-006-tra-hang-rma-hoan-tien.md) §35 | nguồn chuẩn của `S14H` |

---

## 8. Lịch sử

| Phiên bản | Ngày | Người | Thay đổi |
|---|---|---|---|
| v1.0 | 29/10/2026 | *(agent tài liệu, phiên GĐ1)* | **Ghi nhận `Q5` = DUYỆT** (chủ dự án, 29/10/2026) cho **26** tài liệu (`7` `XC-*` + `19` GĐ1) · biên bản là **bản ghi trung tâm**, **không** sửa header `§0` của 26 tệp · **0** thay đổi mã/schema/migration/quyền/route · ghi rõ **giới hạn:** không thay `Q6`, không làm `S19` xong |
