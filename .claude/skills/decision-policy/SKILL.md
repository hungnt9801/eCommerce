---
name: decision-policy
description: Chính sách ra quyết định thay chủ dự án eCommerce — ma trận quyền theo tính đảo ngược, vùng cấm, quy trình 7 bước, thứ tự tiêu chí (tiền lệ D-xx → bất biến kỹ thuật → pháp lý VN → hành vi Nhanh.vn → thông lệ phần mềm bán hàng VN → rẻ nhất để lùi), cách ghi D-xx TẠM CHỐT và hàng đợi xem lại. Dùng bởi agent decider, hoặc khi orchestrator phân vân có nên hỏi người dùng.
---

# Chính sách quyết định — `eCommerce`

> Vì sao có: dây chuyền nhiều agent dừng nhiều nhất ở **câu hỏi mở** — phần lớn là chi tiết **đảo ngược được**
> (tên trường, mặc định, thứ tự làm, enum Nhanh chưa rõ nghĩa). Lịch sử `D-22…D-68` cho thấy chủ dự án gần như
> luôn chọn theo **cùng một bộ nguyên tắc** (số đo > kế hoạch, append-only, một cửa ghi, không nới bất biến,
> hoãn có lý do). Chính sách này viết bộ nguyên tắc đó ra để agent áp dụng thay người.

## 1. Ba kết quả có thể

| Kết quả | Khi nào | Trạng thái ghi |
|---|---|---|
| `TIEN_LE` | Đã có dòng `ĐÃ CHỐT` trả lời câu hỏi (kể cả suy ra trực tiếp) | Không ghi dòng mới; trích `D-xx` |
| `TAM_CHOT` | Ngoài vùng cấm, có ≥ 2 phương án, chọn được theo §5 | Dòng mới `TẠM CHỐT (DEC)` + hàng đợi xem lại |
| `CHUYEN_NGUOI` | Thuộc vùng cấm §3, **hoặc** `confidence = THAP` **và** `reverseCost = CAO` | Dòng mới `ĐỀ XUẤT (DEC)` có khuyến nghị; dây chuyền làm `continueWhileWaiting` |

Người dùng duyệt hàng đợi theo lô: đổi `TẠM CHỐT (DEC)` ⇒ `ĐÃ CHỐT`, hoặc thêm dòng thay thế.

## 2. Ma trận quyền: độ tin cậy × chi phí lùi

| | Lùi rẻ (1 commit, không dữ liệu) | Lùi vừa (migration additive, đổi API v1 chưa có client ngoài) | Lùi đắt (dữ liệu thật, hợp đồng ngoài, tiền) |
|---|---|---|---|
| **Tin cậy CAO** (tiền lệ gần / bằng chứng Nhanh đo được) | TẠM CHỐT | TẠM CHỐT | CHUYỂN NGƯỜI (kèm khuyến nghị) |
| **Tin cậy VỪA** (thông lệ VN, suy từ tài liệu) | TẠM CHỐT | TẠM CHỐT + `reviewBefore` gần nhất | CHUYỂN NGƯỜI |
| **Tin cậy THẤP** (không nguồn) | TẠM CHỐT phương án trung lập | Phương án trung lập (cấu hình/adapter) hoặc hoãn có lý do | CHUYỂN NGƯỜI |

**Chi phí lùi** đo bằng câu: *"nếu sai, cần làm gì để về như cũ?"* — trả lời được bằng một lệnh/tệp ⇒ rẻ.

## 3. Vùng cấm — luôn `CHUYEN_NGUOI`

1. **Hành động không đảo ngược ngoài repo:** merge vào `main`, đổi branch protection/required check, force-push, xoá nhánh người khác.
2. **Dữ liệu thật:** xoá/sửa dữ liệu có thật, backfill **thật** (dry-run thì được), `DROP`/đổi kiểu cột có dữ liệu, `SET NOT NULL` trên cột có dữ liệu.
3. **Tiền thật & bên ngoài:** gọi API production (thanh toán, hoàn tiền, phát hành HĐĐT, gửi SMS/Zalo), chọn/ký nhà cung cấp có phí, tăng chi phí hạ tầng, tạo tài khoản, tạo/đổi secret/khoá/chứng thư số.
4. **Đảo quyết định `ĐÃ CHỐT`** của người (chỉ được đề xuất thay thế).
5. **Phạm vi dự án:** thêm việc **không có** trong lộ trình chính, bỏ việc đã cam kết, cam kết thời hạn với bên ngoài.
   ✅ **Được** chọn **thứ tự** hoặc **WP kế tiếp** trong số việc **đã có** trong lộ trình chính (TẠM CHỐT, `reviewBefore = mở phiên`).
   📌 **Lộ trình chính — `D-69` (ĐÃ CHỐT 14/11/2026):** `docs/nhanh/05-lo-trinh-rui-ro.md` + `docs/nhanh/07-ke-hoach-trien-khai.md`
   thắng `docs/implementation-plan.md` khi trùng; việc **chỉ** có trong `implementation-plan.md` (GĐ2 cũ: `PRO-*`, `SHP-*`,
   `REC-002/003`) được ánh xạ vào giai đoạn tương ứng của `05` — ánh xạ rõ ràng ⇒ TẠM CHỐT; không ánh xạ được ⇒ `CHUYEN_NGUOI`.
   Ghi nguồn "D-69".
   🚫 Tệp nhãn **L** (lịch sử) ở `docs/agents/ke-hoach-he-thong-agent.md` §14.1 — ví dụ `docs/SESSION-KICKOFF.md`,
   `docs/tiep-tuc-phien-moi.md` — **không** phải nguồn cho câu hỏi "làm gì tiếp" hay số đo hiện tại.
6. **Pháp lý cho go-live:** kết luận "đã tuân thủ" NĐ/TT về hoá đơn, thuế, dữ liệu cá nhân, kế toán.
   ✅ **Được** chốt tạm **thiết kế** (mô hình, trạng thái, adapter) để code chạy với **sandbox/giả lập**, `reviewBefore = release`.
7. **Bảo mật & phân quyền mặc định mở rộng quyền** (ví dụ cho role mới quyền ghi tiền, tắt kiểm `[Authorize]`).

## 4. Quy trình 7 bước

1. **Chuẩn hoá câu hỏi** thành một câu có/không hoặc chọn-một. Tách câu ghép thành nhiều câu.
2. **Tìm tiền lệ:** `grep` từ khoá trong `docs/nhanh/decisions.md`, tài liệu feature cùng module (`docs/features/<module>/`),
   handover (`docs/nhanh/handover-n*.md` §4), `docs/coding-conventions.md` §13 (việc KHÔNG được làm). Có ⇒ `TIEN_LE`.
   Câu trả lời dựa vào một tài liệu feature ⇒ kiểm tài liệu đó **đã duyệt chưa** theo thứ tự: biên bản duyệt
   (`docs/bien-ban-duyet-gd1-2026-10-29.md` và các biên bản sau cùng khuôn) → `docs/features/README.md` §2 → header
   (header `Draft` **không** có nghĩa là chưa duyệt — biên bản là nơi ghi). Tài liệu chưa duyệt ⇒ hạ `confidence` một bậc.
   Nhiều nguồn mâu thuẫn ⇒ tệp nhãn **N** thắng tệp **Q**/**B**/**L** (`docs/agents/ke-hoach-he-thong-agent.md` §14.1).
3. **Phân vùng:** khớp §3 ⇒ `CHUYEN_NGUOI` (vẫn làm bước 4–5 để có khuyến nghị).
4. **Liệt kê ≥ 2 phương án**, luôn cân nhắc thêm hai phương án chuẩn: *"cấu hình hoá với mặc định an toàn"* và *"hoãn có lý do + ghi nợ có nhận chủ"* (mẫu `D-34`).
5. **Chấm theo §5** (dừng ở tiêu chí đầu tiên phân biệt được các phương án).
6. **Cần sự thật ngoài repo?** (luật, chính sách sàn, API hãng): tra web, ghi **URL + ngày tra**; nguồn chính thức (`thuvienphapluat.vn`, cổng Chính phủ, tài liệu developer chính thức của sàn/hãng) > blog. Không tìm thấy ⇒ **không suy đoán**, quay lại phương án trung lập.
7. **Ghi và trả:** ghi §6, trả JSON theo `.claude/agents/decider.md` §5.

## 5. Thứ tự tiêu chí (cao → thấp)

| # | Tiêu chí | Nguồn |
|---|---|---|
| T1 | Không vi phạm quyết định `ĐÃ CHỐT` và bất biến kỹ thuật đã ghi | `docs/nhanh/decisions.md`, `docs/coding-conventions.md`, `references/tech-defaults.md` |
| T2 | Không sai pháp lý Việt Nam (theo căn cứ **đã tra trong repo**, ghi ngày tra) | `docs/business-modules-proposal.md` §6.7, `references/vn-commerce-domain.md` §Pháp lý |
| T3 | Không làm sai tiền / tồn kho / trạng thái đơn **im lặng** (ưu tiên phương án NÉM LỖI hơn bỏ qua) | `docs/features/cross-cutting/XC-001-money-currency-lam-tron.md`, sổ cái kho, máy trạng thái |
| T4 | Giống **hành vi Nhanh.vn có bằng chứng đo được** (số đo > tài liệu Nhanh > kế hoạch của ta) | `docs/nhanh/01-khao-sat-nhanh-vn.md`, `docs/nhanh/nhanh-pos-v3-api-inventory.md`, `docs/nhanh/evidence/README.md` |
| T5 | Thông lệ phần mềm bán hàng VN (KiotViet, Sapo, Haravan, MISA eShop, Pancake) và quy tắc sàn (Shopee, Lazada, TikTok Shop) | `references/vn-commerce-domain.md` |
| T6 | Rẻ nhất để lùi (additive, append, cấu hình, cờ, adapter) | §2 |
| T7 | Ít mã mới nhất, đi qua **cửa có sẵn** (một cửa ghi, hàm thuần tính tiền, `nen-tang.ps1`) | D-35, D-42, D-45, D-54 |
| T8 | Đo được: chỉ ra được test/cổng sẽ đỏ khi vi phạm | `tools/kiem-tat-ca.ps1`, test hiện có |

**Không bao giờ dùng làm tiêu chí:** "đẹp hơn", "hiện đại hơn", "giống Shopify", "agent thấy hợp lý" mà không có nguồn.

## 6. Cách ghi

**`docs/nhanh/decisions.md`** — đọc lại tệp **ngay trước khi ghi** để lấy số `D-xx` lớn nhất + 1 (có thể có phiên song song).
Thêm **một dòng** vào bảng chính, đúng 4 cột hiện có:

```text
| D-xx | **<tiêu đề ngắn>** (DEC, <ngày>, phiên <Nx>/<MÃ feature>) | ① Chọn: … ② Vì: T<n> — … ③ Nguồn: … ④ Lùi: … ⑤ Xem lại trước: … ⑥ Đỏ khi vi phạm: … | TẠM CHỐT (DEC) |
```

Trạng thái chỉ dùng: `TẠM CHỐT (DEC)` hoặc `ĐỀ XUẤT (DEC)`. Không bao giờ tự ghi `ĐÃ CHỐT`.

**`docs/agents/hang-doi-xem-lai.md`** — thêm một dòng vào bảng "Đang chờ", nhóm theo `reviewBefore`.

## 7. Dấu hiệu quyết sai — tự kiểm trước khi trả

- Lý do không trích được **một** nguồn cụ thể (D-xx, tệp §mục, URL) ⇒ hạ `confidence` xuống `THAP`.
- `revert` là câu chung chung ("sửa lại code") ⇒ chưa đủ; phải là tệp/lệnh.
- Phương án chọn **nới** một kiểm tra đang có ⇒ sai loại, chọn lại.
- Phương án cần điền dữ liệu từ bên ngoài bằng suy đoán (trường XML thuế, mã lỗi NCC) ⇒ sai, dùng adapter + giả lập.
- Câu trả lời mâu thuẫn với một handover gần đây ⇒ dừng, trích cả hai, `CHUYEN_NGUOI`.
- Phương án khiến code **lệch tài liệu feature** mà không kèm việc "BA sửa SRS trên cùng nhánh, cùng PR" ⇒ thiếu; quy tắc vàng
  của dự án là *sửa tài liệu trước* (`docs/design-doc-guidelines.md` §4).
- Phương án vượt một trong 5 dấu hiệu dừng phiên (`docs/nhanh/08-session-protocol.md` §6) — ví dụ phải sửa > 5 tệp ngoài
  phạm vi WP — ⇒ không phải quyết định nhỏ nữa; ghi nợ có nhận chủ thay vì làm ngay.
