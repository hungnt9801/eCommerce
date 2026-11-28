# Doc review — `UI-41` «Kiểm kê giao diện + Kiến trúc nền tảng UI Web Admin» (vòng 2)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/ui/00-kiem-ke.md` (312 dòng, vai `ux`) · `docs/ui/01-kien-truc-ui.md` (482 dòng, vai `arch`) |
| Ngày · vòng | 28/11/2026 · vòng 2 (vòng 1 cùng ngày) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) — phiên N120, chuỗi `ui-1`, hàng `U41-1`, nhánh `feat/ui-wp41-nen`, đỉnh `99e922e` |
| Đã đọc | `00-kiem-ke.md` dòng 1–312 · `01-kien-truc-ui.md` dòng 1–482 — **đọc lại toàn văn** sau khi `ux`/`arch` vá (không chỉ đọc phần đổi, để bắt hồi quy) |
| Máy | `check-agent-artifact.ps1 -Kind docreview` → exit 0 · `kiem-link-tai-lieu.ps1` → PASS (345 tệp, 0 tham chiếu chết) · `check-feature-doc.ps1` → exit 1 (2 LOI) — **không áp dụng**, xem «Không kiểm được» |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc lại toàn văn; đối chiếu chéo 00 ↔ 01 (tầng `Skeleton`/`EmptyState`, BR → Phụ lục B, số đếm); đếm dấu phân cách của **từng dòng bảng** bằng grep rồi so số cột với header | **1 phát hiện** (N1: 4 ô bảng chứa dấu sổ dọc chưa escape) |
| D2 chéo tài liệu / code / `D-xx` | xác nhận lại 13 dòng trọng yếu đã vá: `00:51,121,190-191,198,283` · `01:24,60,83-84,221-224,240-243,281,322-324,422-423`. Kiểm **thêm 3 tuyên bố quyền ghi mới** của bản vá với `.claude/scopes.json`: `ops` → `tools/*.ps1` (`:178`) ✔ · `qa` → `packages/ui/test/**` (`:141`) ✔ · `ds` → `packages/ui/**` (`:215`) ✔ | **1 phát hiện** (N2: "8 tab con" vs 4) |
| D3 cơ chế thực thi | chạy lại đúng lệnh hex đã vá: `grep -RE "#[0-9a-fA-F]{3,8}"` → **26** · mẫu có dấu cách → **0** (khớp hiệu chuẩn tài liệu ghi) ✔; đường lùi `U45-4` tạo script ✔; `jsdom`/`@vitejs/plugin-react` đã vào §10.2 ✔ | 0 phát hiện |
| D4 SQL / lệnh chạy được | vẫn **0 khối `sql`**; lệnh fenced là CSS/JSON/TSX minh hoạ — cú pháp hợp lệ, `@source "../../../packages/ui/src"` đúng tương đối từ `apps/admin-web/app/` | 0 phát hiện |
| D5 test / probe / tệp có thật | kiểm lại các đường dẫn **mới** trong bản vá: `app/login/page.tsx:13` (`force-dynamic` ✔), `app/[business]/layout.tsx:8-9` (dòng 9 là `export const dynamic` ✔), `middleware.ts:8-29` (hàm `middleware` ✔), `tools/kiem-link-tai-lieu.ps1:142` (chỉ lọc `.md` ✔); `tools/kiem-ngan-sach-bundle.ps1` **vẫn chưa có** nhưng tài liệu **đã tự ghi** "do `ops` tạo ở `U45-4`; hiện chưa có" ✔ | 0 phát hiện |
| D6 nhãn tin cậy | 9/9 bản ghim §10.1 vẫn khớp `pnpm-lock.yaml`; §10.2 vẫn ghi "cần xác minh (npm view)" cho mọi gói mới, gồm 2 dòng mới `jsdom`/`@vitejs/plugin-react` ✔; hiệu chuẩn hex ghi kèm số đo (26 vs 0) ✔ | 0 phát hiện |
| D7 14 quy tắc chất lượng | BR-01…09 giờ đều kiểm được; NFR có ngưỡng (< 200 KB, 4.5:1 / 3:1); không Mermaid (0 khối); không TODO; mục N/A có lý do | 0 phát hiện |

## Phát hiện

Chỉ còn **2 phát hiện P3** (không chặn). Cả hai **tồn tại từ vòng 1 và tôi đã bỏ sót** — ghi lại để đóng nốt, không phải lỗi do bản vá sinh ra.

| # | Mức | Nhóm D | Tệp:dòng | Vấn đề | Vì sao gây lỗi khi làm theo | Đề nghị |
|---|---|---|---|---|---|---|
| N1 | P3 | D1 | `docs/ui/00-kiem-ke.md:77,82,92,120` | 4 ô của bảng markdown chứa **dấu sổ dọc** (U+007C) chưa escape, dù đã nằm trong code span. GFM **vẫn** coi nó là dấu phân cách ô (phải escape **cả trong** code span) | Mỗi dòng đó sinh nhiều ô hơn header ⇒ cả bảng render lệch cột (header 4 cột, dòng vỡ 6 cột), chữ bị đẩy sang cột thừa. Không làm sai nội dung nhưng bảng §2.2 (hợp đồng route) và §2.4 trông gãy; không cổng nào bắt (link check chỉ kiểm `.md`, không có cổng bảng) | Thêm `\` ngay trước dấu sổ dọc ở **cả 4 chỗ** (hoặc đổi sang `&#124;`). Chuỗi cần sửa ghi ở danh sách dưới bảng này |
| N2 | P3 | D2 | `docs/ui/00-kiem-ke.md:283` ↔ `:178` | `UI-41-Q2` hỏi "32 + 7 cục bộ + **8 tab con**" và lấy `32+7+8 = 47` cho phương án (c); nhưng `§3.2 L8` chỉ liệt kê **4** tab (`TabDonHang`/`TabHoaDon`/`TabCongNo`/`TabChamSoc`) — đo code `app/[business]/customer/[id]/page.tsx` đúng **4** hàm (`:161,214,273,338`) | Phép cộng trình bày như suy ra từ cây nhưng không suy ra được (32+7+4 = 43, không phải 47); người đọc Q2 tưởng codebase có 8 tab con. Khuyến nghị (a) 32 **không đổi** nên không ảnh hưởng quyết định | Đổi "8 tab con" → "4 tab con (L8)"; ở phương án (c) bỏ vế cộng, ghi "47 = số **kế hoạch**, không suy từ cây" (hoặc bổ sung 4 tên còn thiếu vào L8 nếu thật có) |

Bốn ô cần escape ở N1 (nguyên văn, để khỏi dò lại):

- `00:77` — `?error=missing|invalid`
- `00:82` — `info|lines|history`
- `00:92` — `?tab=tong-quan|don-hang|hoa-don|cong-no|cham-soc`
- `00:120` — `inventory/transfer|check`

## Vòng trước (13 phát hiện của vòng 1)

Bảng vòng 1 chuyển xuống đây theo khuôn: cột 2 là **trạng thái** (không phải mức), nên `check-agent-artifact.ps1` **không** đếm các mức này vào verdict vòng 2. Không phát hiện nào còn mở.

| # vòng 1 | Trạng thái | Mức lúc đó | Bằng chứng đã đóng (đọc lại nguyên văn) |
|---|---|---|---|
| 1 | đã đóng | P2 | `00:51` BR-08 → "**Phụ lục B**"; `00:294` Phụ lục A → "Phụ lục B/BR-08"; khớp `01:342` |
| 2 | đã đóng | P2 | `00:121` đã bỏ `OrderStatusBadge` khỏi danh sách "không có"; §3.1 #14 vẫn giữ nó trong 32 export (đúng) |
| 3 | đã đóng | P2 | `01:222` lệnh mới `grep -REn "#[0-9a-fA-F]{3,8}" … --include=*.tsx --include=*.css` + loại `src/tokens/`; `01:223-224` hiệu chuẩn **26** vs **0** (tôi chạy lại: 26 / 0 — khớp) |
| 4 | đã đóng | P2 | `01:321-324` bỏ "có sẵn", ghi "do `ops` tạo ở `U45-4` (vùng ghi `ops`; hiện **chưa có**)" + cảnh báo cổng link chỉ kiểm `.md`; `.claude/scopes.json:178` có `tools/*.ps1` cho `ops` ✔ |
| 5 | đã đóng | P2 | `01:240-242` "**cả** `/login` đã `force-dynamic` (`app/login/page.tsx:13`)" ✔; đường lùi → `app/[business]/layout.tsx:8-9` (dòng 9 = `export const dynamic` ✔) |
| 6 | đã đóng | P2 | `01:281` job `storybook` "thêm ở `U42-4` — `ops`"; thêm `01:364` WP-42 có "Storybook (catalog)" ⇒ khớp `07:765` và `bang-phien-ui-1.md:79` |
| 7 | đã đóng | P2 | `01:83` T1 có `Skeleton`, không còn trùng; `01:84` T2 chỉ còn `EmptyState/ErrorState`; `00:190-191` = "Pattern … = **T2**", `00:198` = "Primitive `Skeleton` = **T1**" — ba tệp/mục khớp nhau |
| 8 | đã đóng | P2 | `00:53-55` BR-09 thêm "**trừ** `tools/probe-*.ps1` theo `D-328` ⑤ … đã có ở `99e922e` của N120", và "DoD đo bằng diff nhánh `tich-hop/ui` với ngoại lệ trên" |
| 9 | đã đóng | P2 | `01:422-423` thêm `jsdom` ("bắt buộc cho `environment: "jsdom"`") + `@vitejs/plugin-react`, cả hai ghi "cần xác minh (npm view)" |
| 10 | đã đóng | P3 | `01:24` → "P-01…**P-15**", khớp `01:59` và `00` §4 (đếm lại: 15 dòng `P-xx`) |
| 11 | đã đóng | P3 | `01:60` → "**Nợ a11y 19 mục** … §6 có **22 dòng** A-01…A-22: **19 mục nợ** … 3 dòng không phải nợ"; đếm lại: 22 dòng `A-xx` ✔ |
| 12 | đã đóng | P3 | `01:243` → `middleware.ts:8-29` (đọc code: hàm bắt đầu `:8`, đóng `:29` ✔) |
| 13 | đã đóng | P3 | `01:83` T1 đã có `Combobox` (+ "`cmdk` (Combobox)" ở cột Được phụ thuộc), khớp `01:93` và `00:144` |

## Không kiểm được

- **D4 SQL:** 0 khối `sql` trong hai tệp ⇒ không gọi `psql`/PostgreSQL (không áp dụng).
- **`npm view`:** không chạy (vai chỉ đọc, shell không dùng mạng) ⇒ 15 gói §10.2 (gồm `jsdom`, `@vitejs/plugin-react` mới thêm) **chưa xác minh bản tồn tại**; tài liệu đã tự ghi "cần xác minh (npm view)" ở từng dòng nên **không** phải phát hiện.
- **`check-feature-doc.ps1 -WP14`:** exit 1 trên cả hai tệp ("thiếu heading `## 0.`…`## 15.`", "không tìm thấy BR nào ở §1"). **Không áp dụng**: `07-ke-hoach-trien-khai.md:765` ghi WP-41…51 "Không có tài liệu feature"; `ui-pipeline` §4 dùng khung 7 mục.
- **`npm --prefix tools run check:mermaid`:** 0 khối Mermaid (đếm lại 0/0) ⇒ không áp dụng.
- **Axe / tương phản / ảnh chuẩn / Bundle thật:** không chạy — thuộc `U41-3`/`U42-3`/`U45-4`.
- **Đọc markdown bằng renderer thật:** tôi kết luận N1 theo **quy tắc GFM** (dấu sổ dọc phải escape cả trong code span), không chạy renderer ⇒ nếu bản xem của dự án tự bỏ qua dấu sổ dọc trong code span thì N1 thành "không có" — nhưng sửa vẫn rẻ và vô hại.

## Dòng cần sửa (vòng 2 — việc của `ux`)

| # | Tệp | Dòng | Sửa gì |
|---|---|---|---|
| N1 | `docs/ui/00-kiem-ke.md` | 77, 82, 92, 120 | thêm `\` trước từng dấu sổ dọc trong 4 ô (danh sách nguyên văn ở mục «Phát hiện») |
| N2 | `docs/ui/00-kiem-ke.md` | 283 | "8 tab con" → "4 tab con (L8)"; phương án (c) ghi "47 = số kế hoạch, không suy từ cây" |

## Kết luận

**VERDICT: DOC-OK** — **P1 = 0 · P2 = 0 · P3 = 2**.

13/13 phát hiện vòng 1 đã đóng, đúng `mustFix`, không phát hiện nào bị vá nửa vời. Bản vá **không sinh** P1/P2 mới: ba tuyên bố quyền ghi mới (`ops`, `qa`, `ds`) đều khớp `.claude/scopes.json`; các `file:dòng` mới thêm (`login/page.tsx:13`, `layout.tsx:8-9`, `middleware.ts:8-29`, `kiem-link-tai-lieu.ps1:142`) đều đúng; hiệu chuẩn hex (26 vs 0) tôi chạy lại khớp; số đếm 22 dòng `A-xx`, 15 dòng `P-xx`, 32 export, 28 trang vẫn nhất quán. Hai P3 còn lại là lỗi render bảng (có từ vòng 1, tôi bỏ sót) và một phép cộng trong câu hỏi mở — không chặn `decider` TẠM CHỐT theo `ui-pipeline` §1.4.
