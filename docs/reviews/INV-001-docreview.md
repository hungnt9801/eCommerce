# Doc review — INV-001 Nhập, chuyển, kiểm kê và sổ cái kho (append-only)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/inventory/inv-001-nhap-chuyen-kiem-ke-so-cai-kho.md` (2845 dòng) |
| Ngày · vòng | 28/11/2026 · **vòng 2** (kiểm nhanh `changedSections` sau khi BA đóng 2 P2 vòng 1 — phiên N113 / WP-17) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | dòng 1–108 (§0 + ADR) · 318–339 (§5.2.2) · 980–1031 (§15.1–§15.4) + `git diff` toàn tệp |
| Máy | `check-feature-doc.ps1 -WP14` → exit **1** (**2** lỗi: `khong tim thay BR nao o §1`; thiếu mục "Doi chieu mo hinh Nhanh") — **cả hai có trước N113**, ngoài `changedSections` (xem "Ngoài phạm vi") |
| Đối chiếu | `D-186` · `D-318` · `prc-001` §15.1 Q8 (:897) + §20.4 (:1217) · `rep-003` Q11 (:938) · `acc-001` §0 (:73) + (:515) + TC-23/24 (:1002) · as-built `StockDocumentLine.UnitPrice` |

## Đối chiếu as-built (graph — bằng chứng lệnh)

- `graph: codegraph_explore "StockDocumentLine UnitPrice StockDocument Domain"` → `StockDocumentLine` (`modules/Inventory/eCommerce.Inventory.Domain/StockDocument.cs:332`); `UnitPrice` khai báo **:367** kiểu `decimal?`, comment **:365** ghi `numeric(18,2)`; gán trong `DatChiTiet` **:466**; DTO `CreateStockDocumentLineDto.UnitPrice` (`modules/Inventory/eCommerce.Inventory.Application.Contracts/StockDocumentDtos.cs:186`, `decimal?`). ⇒ Câu ánh xạ mới ở §0 (:57) *"`UnitCost` (tài liệu) ↔ `UnitPrice` (as-built)"* là **đúng tên**; **kiểu** vẫn lệch (`numeric(18,4)` tài liệu vs `numeric(18,2)` as-built) — **chính là P3 #3 cũ**, chưa sửa (xem bảng Phát hiện).
- `git diff --unified=3` trên tệp: đúng **3 hunk** — §0 (:53-58), §5.2.2 (:334), §15.1 Q8 (:993) + §15.4 dòng `v0.2` (:1028). Không có hunk nào ngoài các mục BA báo đã sửa.
- `grep` (văn bản) `D-186` trong `decisions.md` dòng 200: chủ động cơ giá vốn = **Pricing (`PRC-001`)**; `INV-001` **chỉ cấp đơn giá nhập để tra cứu**; `ACC-001` **chỉ tiêu thụ, KHÔNG ghi ngược**; ⑥ nêu rõ phương án "ACC-001 sở hữu" là trái chủ trương. `D-318` phạm vi G1 = *"CHỈ đồng bộ đúng `D-186` ⑥"* cho `inv-001` §0.
- `npm --prefix tools run check:mermaid` → **214 khối · 0 lỗi**.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | `grep 'ACC-001\|giá vốn\|định giá\|giá trị tồn\|UnitCost\|UnitPrice\|PRC-001'` toàn tài liệu; đối chiếu §0 (:56-58) ↔ §5.2.2 (:334) ↔ §15.1 Q8 (:993) ↔ §15.4 (:1028) | **0** mâu thuẫn — 3 chỗ đã đồng nhất *"định giá/giá vốn = `PRC-001`; `ACC-001` chỉ tiêu thụ; `INV-001` giữ đơn giá nhập để tra cứu"* |
| D2 chéo tài liệu / code / `D-xx` | §0/§5.2.2/Q8 vs `D-186`:200 · `D-318`:332 · `prc-001` Q8:897 · `rep-003` Q11:938 · `acc-001`:73,515,1002 — **khớp từng vế**. As-built `UnitPrice` (`StockDocument.cs:367`, `decimal?`/`numeric(18,2)`) khớp tên ánh xạ §0 mới | **0** phát hiện mới trong phạm vi; lệch **kiểu** = P3 #3 kế thừa (không đổi mức) |
| D3 cơ chế thực thi | Phần đổi là **câu phạm vi/sở hữu** (prose) + đổi trạng thái Q8 `Open`→`Closed`; **không** thêm BR "phải/không được" nào ⇒ không cần hàng rào mới. `BR-017`/`TC-22` không đổi | 0 |
| D4 SQL chạy được | `grep -c '^```sql'` = **0**; **0** khối fenced `sql`/`powershell`/`bash` (chỉ `mermaid`×3, `csharp`×3). Phần đổi không thêm khối lệnh ⇒ không có gì phải chạy | 0 |
| D5 test/probe/tệp có thật | Phần đổi không nêu test/probe/tệp mới. Tên as-built được kiểm bằng graph: `StockDocumentLine.UnitPrice` (`:367`/`:466`) + DTO (`:186`) **có thật**; `D-186`/`D-318` **có thật** (`decisions.md:200`/`:332`) | 0 |
| D6 nhãn tin cậy | Q8 nay mang `✅ Closed (D-186)` — đúng vì `D-186` **đã chốt thật** (đọc dòng 200), không phải ✅ chưa đo. Dòng `v0.2` dẫn `D-186` đúng. Không có số liệu mới không nguồn | 0 |
| D7 14 quy tắc + đối chiếu Nhanh | Phần đổi không chạm BR/NFR/Mermaid/API/tiền; mermaid **0** lỗi. Mục "Đối chiếu mô hình Nhanh" **vẫn thiếu** — lỗi **có trước**, ngoài `changedSections` (xem "Ngoài phạm vi") | 0 (trong phạm vi) |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §5.2.3 :376-377 · §6.1 :497-498 · BR-017 :685 | **Kế thừa vòng 1, chưa sửa, ngoài phạm vi vòng 2.** Ánh xạ mới ở §0 (:57) đúng **tên** (`UnitCost` ↔ `UnitPrice`) nhưng **kiểu** vẫn lệch: tài liệu khai `UnitCost numeric(18,4)` + `UnitCostCurrency char(3)`; as-built `StockDocumentLine.UnitPrice` là `decimal?` `numeric(18,2)` (`StockDocument.cs:365-367`) và **không có** cột tiền tệ. Người đồng bộ §5 theo as-built sẽ lưỡng lự giữa thêm cột thứ hai hay đổi độ chính xác. | Khi rà toàn tài liệu (WP-14): ghi rõ ánh xạ **cả kiểu** (`UnitPrice numeric(18,2)`, không có `UnitCostCurrency` as-built), hoặc tách bạch "kế hoạch" vs "as-built" ở §5.2.3/§6.1/BR-017. |
| 2 | P3 | Header :11 so với §15.4 :1028 | **Kế thừa vòng 1, chưa sửa, ngoài phạm vi vòng 2.** Header vẫn `v0.1 — 13/10/2026`; §15.4 đã có `v0.2 — 28/11/2026`. Người duyệt đọc header thấy bản cũ. Không ảnh hưởng hành vi. | Đồng bộ header `Phiên bản / ngày` = `v0.2 — 28/11/2026`. |

## Ngoài phạm vi thay đổi (ghi nhận, **không** tính vào verdict vòng 2)

- `check-feature-doc` lỗi 1 — **"khong tim thay BR nao o §1"**: §1 (:98) trỏ BR sang §8.1 (`BR-001`…`BR-024`), không đúng dạng regex script đòi.
- `check-feature-doc` lỗi 2 — **thiếu heading "Đối chiếu mô hình Nhanh (dd/mm/yyyy)"**: `grep` = **0**. Theo `.claude/agents/docrev.md` §5 đây là DoD WP-14 (P1 **toàn tài liệu**), nhưng **có trước N113** và ngoài `changedSections` ⇒ không tính verdict kiểm nhanh; cần một lượt WP-14 riêng cho `INV-001`.
- §15.5 không có dòng cho `StockDocumentLine.UnitPrice`/`StockDocument` dù as-built đã có từ WP-04 — lệch rộng hơn (tài liệu mô tả `GoodsReceipt`/`StockTransfer`/`Stocktake`, as-built là `StockDocument` hợp nhất). Có trước, ngoài phạm vi.
- `D-186`:200 dẫn `inv-001` §0 **:56-57**; bản sửa phủ :56-58 (thêm dòng 58) — không phải lỗi.

## Không kiểm được

- **Toàn văn**: vòng 2 là **kiểm nhanh changedSections** (không đọc lại 2845 dòng). Các mục ngoài 1–108 / 318–339 / 980–1031 dựa trên kết quả vòng 1 (đã đọc toàn văn) và `git diff` (chỉ 3 hunk, không chạm phần còn lại).
- **D4**: 0 khối `sql`/`powershell`/`bash` ⇒ không có gì phải chạy trên PostgreSQL. SQL **inline** §11:792 và phép đo §16.2/§19 **chưa chạy** vì máy dev không có PostgreSQL (`F5`) — ghi rõ, không bỏ qua vì "chắc đúng".
- **Test/`dotnet test`**: §14 TC là **kế hoạch**; phần đổi không có test mới để chạy.
- Không đọc lời giải thích của BA; chỉ đối chiếu tài liệu, `D-xx`, tài liệu liên quan và as-built.

## Vòng trước

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. Phát hiện còn mở phải chép lại vào bảng "Phát hiện". -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 (§5.2.2 :334) | **đã đóng** — nay *"Định giá / giá trị tồn kho thuộc `PRC-001` (`D-186` ⑥); bút toán kế toán tiền chỉ tiêu thụ ở `ACC-001`"* | P2 |
| 2 (§15.1 Q8 :993) | **đã đóng** — `Open` → `✅ Closed (D-186)`; giữ đơn giá nhập ở `INV-001`, **không** chuyển sang `ACC-001`, định giá/giá vốn thuộc `PRC-001` | P2 |
| 3 (§5.2.3/§6.1/BR-017) | **còn** (đã chép lên bảng Phát hiện #1, không đổi mức) | P3 |
| 4 (Header :11) | **còn** (đã chép lên bảng Phát hiện #2, không đổi mức) | P3 |

```json
{
  "agent": "DOCREV",
  "doc": "docs/features/inventory/inv-001-nhap-chuyen-kiem-ke-so-cai-kho.md",
  "artifact": "docs/reviews/INV-001-docreview.md",
  "round": 2,
  "verdict": "DOC-OK",
  "p1": 0,
  "p2": 0,
  "p3": 2,
  "sqlRun": "không có khối sql (0 khối fenced sql/powershell/bash; SQL inline §11:792 chưa chạy — máy dev không có PostgreSQL)",
  "readRanges": ["1-108", "318-339", "980-1031"],
  "mustFix": []
}
```
