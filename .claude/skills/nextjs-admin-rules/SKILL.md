---
name: nextjs-admin-rules
description: Luật frontend Next.js của eCommerce (apps/admin-web, BFF) — chỉ gọi endpoint có trong schema.d.ts sinh từ OpenAPI, pnpm gen:api không được đổi schema, 3 trạng thái loading/empty/error, chống bấm lặp, token UI Nhanh, token lưu server-side, định dạng Việt Nam, ngưỡng hiệu năng, cổng lint/typecheck/test/build. Nạp sẵn cho fe và rev; dùng khi code hoặc review apps/admin-web và packages/api-client.
user-invocable: false
---

# Next.js admin — eCommerce

Nguồn: `docs/nhanh/04-web-admin-nextjs.md` (§7 dữ liệu, §8 auth, §9 định dạng VN, §10 hiệu năng, §12 thứ tự) ·
`docs/coding-conventions.md` §12 · `docs/nhanh/09-parallel-sessions.md` §7 (contract-first). Skill **dẫn**; mâu thuẫn ⇒ tệp nguồn thắng.

## 1. Hợp đồng API (contract-first)

| # | Luật |
|---|---|
| F1 | **Chỉ** gọi endpoint có trong `packages/api-client/schema.d.ts` (sinh từ `openapi.v1.json`). Không bịa endpoint, không tự viết URL. |
| F2 | `pnpm gen:api` **không** được đổi `schema.d.ts` trong PR của FE; API đổi ⇒ BE cập nhật snapshot + báo FE trước. Cổng `tools/kiem-openapi-drift.ps1`. |
| F3 | Đổi API **phá vỡ** (xoá field/đổi kiểu) ⇒ báo luồng FE trước khi merge; FE chỉ chạy trên API **đã merge** (tuần tự trên Pro). |

## 2. Tầng dữ liệu và trạng thái

| # | Luật |
|---|---|
| F4 | Danh sách: **Server Component** gọi `api.GET` với cookie, render HTML; client nhận `initialData`. Tương tác lọc/sort/page qua URL state (`nuqs`). |
| F5 | Mutation: **Server Action** → API → `revalidatePath`/`router.refresh()`. Optimistic chỉ ở POS/gắn nhãn/đổi trạng thái. |
| F6 | **Ba trạng thái** cho mọi màn có dữ liệu: loading (skeleton) · **empty ≠ error** · error (map `problem+json` → toast + lỗi field nếu có `details[].field`). |
| F7 | **Chống bấm lặp**: disable + single-flight ở nút gửi thanh toán/tạo/huỷ; không tạo 2 request đồng thời. |
| F8 | Cache: `next: { revalidate: 30 }` cho reference data; **không cache** tồn kho. |
| F9 | Quyền: server `requirePermission('<permission>')`; client `<Can permission="...">` chỉ là trải nghiệm — server **vẫn** kiểm. |

## 3. Xác thực

| # | Luật |
|---|---|
| A1 | Browser → Auth.js (OIDC/PKCE) → OpenIddict; token lưu **server-side**; session cookie `httpOnly`, `SameSite=Lax`, `Secure`. |
| A2 | **CẤM** lưu access/refresh token trong `localStorage`/`sessionStorage`. |
| A3 | Middleware chặn `/{business}` nếu user không thuộc business ⇒ **404** (không lộ tồn tại). |
| A4 | Refresh token trong route handler có **single-flight lock**, retry **1 lần**. |

## 4. UI và định dạng Việt Nam

| # | Luật |
|---|---|
| U1 | Token màu/font/khoảng cách theo `docs/nhanh/04-web-admin-nextjs.md` §3 — **không** tự đổi design system. |
| U2 | Tiền: `Intl.NumberFormat('vi-VN')`, dấu phẩy phân cách (`150,000`), `₫` chỉ ở tổng; nhập tiền qua `MoneyInput` (raw `66000`). |
| U3 | Số lượng cân đo: 3 chữ số thập phân, dấu `,` (`1,250 kg`). Ngày `dd/MM/YYYY`, giờ `HH:mm`. Điện thoại 10–11 số bắt đầu `0`. |
| U4 | Bố cục/mật độ/nhãn giống Nhanh; **không** copy logo/asset/HTML-CSS-JS của Nhanh. |

## 5. Hiệu năng (ngưỡng đo được)

| Chỉ tiêu | Ngưỡng |
|---|---|
| LCP danh sách 20k SP | < 1,5 s |
| Lọc | < 300 ms |
| Bundle trang danh sách | < 200 KB gzip |
| Bảng 1000 dòng | 60 fps (virtualization) |
| POS mở tab mới | < 100 ms |
| In hoá đơn | < 800 ms |

## 6. Cổng bắt buộc (trong `apps/admin-web`)

```powershell
pnpm lint
pnpm typecheck
pnpm test
pnpm build
pnpm gen:api    # sau đó: git diff --exit-code packages/api-client/schema.d.ts
```

Thiếu một cổng ⇒ G3 đỏ. Test UI lõi phải có Vitest: `MoneyInput`, `DataTable`, `OrderStatusBadge`.
Playwright giữ 6 luồng: đăng nhập · tạo SP · nhập kho · bán lẻ · đổi trạng thái đơn · xuất báo cáo.
