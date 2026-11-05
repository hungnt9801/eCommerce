# Nhập sản phẩm từ website nguồn vào Nhanh.vn — bộ công cụ & bài học

Nguồn đã dùng: **thucphamnhanh.com** (WordPress + WooCommerce, Store API công khai).
Đích: **Nhanh.vn** `businessId=230222`, form `product/item/add2`.

## Các file

| File | Việc |
|---|---|
| `lay-catalog-woo.mjs` | Kéo **toàn bộ** catalog qua `/wp-json/wc/store/products` → `_woo-catalog.json` |
| `trich-xuat-san-pham.mjs` | Trích 1 URL bất kỳ → JSON chuẩn hoá (JSON-LD → OG → render bằng Chrome) |
| `tao-danh-muc.mjs` | Tạo bộ 12 danh mục trong Nhanh qua UI |
| `dien-nhanh-sp.mjs` | Điền **1** sản phẩm vào form add2 (có `--extras`, `--save`) |
| `nhap-hang-loat.mjs` | Nhập **hàng loạt**, rải đều theo nhóm, có state chống nhập trùng |
| `nhanh-cdp.mjs` | Cầu CDP: `url / eval / fill / type / tap / tapxy / key / shot / cookie` |

## Quy trình

```powershell
# 0) Mở Chrome có CDP (đăng nhập 1 lần, profile riêng)
& "C:\Program Files\Google\Chrome\Application\chrome.exe" `
  --remote-debugging-port=9222 `
  --user-data-dir="$env:LOCALAPPDATA\DSH\nhanh-chrome-profile" `
  "https://nhanh.vn/product/item/add2?businessId=230222"

# 1) Kéo catalog nguồn
node tools/lay-catalog-woo.mjs https://thucphamnhanh.com --out tools/_woo-catalog.json

# 2) Tạo 12 danh mục trong Nhanh (một lần)
node tools/tao-danh-muc.mjs

# 3) Nhập hàng loạt
node tools/nhap-hang-loat.mjs --limit 20 --images 5      # thử
node tools/nhap-hang-loat.mjs --limit 100 --images 5     # mở rộng (tự bỏ qua SP đã nhập)
```

## Bốn cái bẫy của form Nhanh.vn (đã kiểm chứng bằng thực nghiệm)

1. **Ô tiền & khối lượng là AutoNumeric.** Chỉ nhận giá trị qua `ClipboardEvent('paste')` + `DataTransfer`.
   - ❌ `el.value = '66000'` → hiện lên, blur → **rỗng**
   - ❌ `Input.insertText` → hiện lên, blur → **rỗng**
   - ❌ Gõ `keyDown`/`char` từng phím → **không vào được ký tự nào**
   - ✅ `new ClipboardEvent('paste', {clipboardData: dt})` → `66.000`, giữ nguyên
2. **Ô text thường thì ngược lại** — phải dùng native setter + `input`/`change`/`blur`.
   Dùng paste cho ô text thì giá trị **không vào** (component `preventDefault`).
3. **Dropdown & tab của Angular không mở bằng click chuột thật** (kể cả `Input.dispatchMouseEvent`).
   Phải bắn chuỗi `pointerdown → mousedown → pointerup → mouseup → click` **trên chính input**.
   Menu điều hướng cũng có chữ "Website"/"Sản phẩm" — phải lọc tab theo `getBoundingClientRect().y > 100`.
4. **Không dùng index để chọn field.** Cùng một form lúc render 25 input, lúc 34 → index lệch là điền sai ô.
   Luôn tra theo **nhãn** (`form-floating label`); riêng field SEO nằm layout ngang nên label không nằm trong box của input → phải tìm label rồi đi lên tìm input cùng hàng.

Ngoài ra: editor mô tả là **HugeRTE** (fork của TinyMCE) → `window.hugerte.get(id).setContent(html)` rồi `.save()`.

## 5. Bẫy lớn nhất: tab không focus thì CDP nuốt mọi cú click

Triệu chứng: bấm "Lưu" **không có phản ứng gì** — không request mạng, không toast, không lỗi console, form vẫn nguyên dữ liệu. Mất rất lâu mới tìm ra:

```js
await cdp.send('Page.bringToFront');
await cdp.send('Emulation.setFocusEmulationEnabled', { enabled: true });
// rồi mới dispatch mousePressed/mouseReleased
```

Kiểm chứng bằng `document.addEventListener('click', ...)` → trước khi focus: **0 sự kiện**; sau khi focus: `trusted: true`. Ngoài ra `scrollIntoView` có thể là smooth-scroll → phải chờ ~700ms rồi **đo lại toạ độ**, đo ngay là click trượt.

## 6. Đơn hàng vs Bán lẻ (POS)

- `order/manage/add2`: bắt buộc **Nguồn đơn hàng**; tài khoản này chưa có nguồn nào và `/order/source/index` trả *"Bạn không có quyền truy cập"* → form tự huỷ, **không gửi request**.
- `pos/bill/add` (bản v1) tạo được hóa đơn bán lẻ: không cần nguồn đơn, có khách, dòng hàng, trừ tồn kho, lên báo cáo doanh thu.
- `pos/bill/add2` hay giữ **giỏ nháp** qua lần reload → dùng `pos/bill/add` cho sạch.
- Gõ SĐT vào ô `Số điện thoại (F4)` **không** tự gắn khách có sẵn → hóa đơn rơi vào khách lẻ mặc định.

## An toàn

- `_da-nhap.json` ghi lại SKU đã lưu → chạy lại không tạo trùng.
- `--dry` để điền mà không bấm Lưu.
- Bỏ qua sản phẩm có giá = 0.
- `--skus A,B,C` để nhập/retry đúng vài SKU.
- Tự chọn radio **"Danh sách sản phẩm"** trước khi Lưu, vì mặc định "Thêm mới" khiến app ở lại form → không phân biệt được đã lưu hay chưa.
- Điều kiện "form sẵn sàng" phải thấy **cả nhãn `Tên sản phẩm` và `Giá bán lẻ`**, ổn định 3 nhịp; nếu thiếu thì **không lưu** (đã từng tạo ra sản phẩm giá rỗng).

## Ưu tiên danh mục

Sản phẩm hải sản trên nguồn thường gắn `Thực phẩm tươi sống` **trước** `Thủy hải sản`. Nếu lấy danh mục đầu tiên thì 156 SP hải sản rơi hết vào nhóm "Tươi sống" và nhóm "Thủy hải sản" rỗng. Vì vậy `groupOf()` ưu tiên danh mục **đặc thù** (`Thủy hải sản`) trước danh mục chung.

## Kết quả đợt 1

- **108 sản phẩm** trong Nhanh (`businessId=230222`), phủ **12/12 nhóm**.
- 553 sản phẩm giá = 0: bỏ qua theo yêu cầu.
- Tốc độ thực tế **~26–30 giây/sản phẩm** (5 ảnh + mô tả + danh mục).

