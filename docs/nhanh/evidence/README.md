# Bằng chứng khảo sát Nhanh.vn (bản commit được)

Hai tệp JSON dưới đây được **trích trực tiếp từ DOM** của phiên đăng nhập Nhanh.vn
(businessId 230222, 02/11/2026) bằng `tools/nhanh-cdp.mjs`. Chúng nhỏ, không chứa dữ liệu cá nhân,
và là **nguồn sự thật** cho các con số trong [01 — Khảo sát](../01-khao-sat-nhanh-vn.md).

| Tệp | Nội dung | Đo được |
|---|---|---|
| `routes-raw.json` | Mọi `href` nội bộ trên trang danh sách sản phẩm | **207** mục (gồm vài link blog `.html`; **204** route ứng dụng) |
| `menu-sidebar.json` | Cây menu sidebar 2 cấp (mục nhóm + mục con) | **180** mục |

## Cách tạo lại

```powershell
node tools/nhanh-cdp.mjs targets          # phải thấy tab nhanh.vn
node tools/nhanh-cdp.mjs goto "https://nhanh.vn/product/item/index?businessId=230222"
Start-Sleep 6
node tools/nhanh-cdp.mjs eval "<đoạn JS trong docs/nhanh/06-phuong-phap-khao-sat-rea.md §5>"
```

## Không commit gì ở đây

- **Ảnh chụp màn hình** (`.png`) và **tài liệu API bóc chữ** nằm trong `artifacts/nhanh-survey/`
  — thư mục này **đã gitignore** (`artifacts/`) vì ảnh UI là nội dung của bên thứ ba.
- Không chứa cookie/token/header Authorization. Nếu cần chụp network, dùng
  `capture_browser_scenario` và **kiểm tra tệp trước khi commit**.
