# Doc review — «ACC-001» Xuất liệu kế toán Excel/CSV

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/accounting/acc-001-xuat-lieu-ke-toan-excel-csv.md` |
| Ngày · vòng | 24/11/2026 · vòng 2 (đầy đủ) → kiểm nhanh §7.4 ngày 24/11/2026 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | vòng 2: dòng 1–1120 (toàn văn) · kiểm nhanh §7.4: §3 167–205 · §6.1 436–515 · §6.3 547–563 · §7.4 644–656 · §8.2 682–697 · §14.1 TC-34 dòng 914 |
| Máy | `check-feature-doc.ps1 -WP14` → exit 0 (16/16 heading; 18/18 BR có test; mục đối chiếu Nhanh dòng 1093; 6 câu `Open` ở §15) · `check-agent-artifact.ps1 -Kind docreview` → exit 0 |

## Vòng kiểm nhanh §7.4 (24/11/2026)

**Phạm vi:** chỉ §7.4 (hàng rào DB của "Huỷ"), đối chiếu §3 / §6.1 / §14.1 TC-34; **không** mở lại mục khác.

**P2#1 vòng 2 — đã đóng.** Dòng 650 nay:

| Hạng mục | Nội dung sau sửa | Đối chiếu |
|---|---|---|
| Hàng rào "Huỷ" | `UPDATE … WHERE Status IN (DangTao, DaTao)` + ghi chú "khớp 0 dòng ⇒ ném lỗi, không im lặng" | Khớp **§3** dòng 196 (`DangTao→DaHuy` hợp lệ: kỳ đổi trạng thái E6 **hoặc** huỷ tay có quyền + lý do ≥ 10) và dòng 198 (`DaTao→DaHuy`); khớp **§6.1** dòng 459 ("Huỷ khi CHƯA công bố (gồm cả `DangTao`)" = {`DangTao`,`DaTao`}); khớp **TC-34** dòng 914 (`DangTao→DaHuy` + `0005`/`0007`, `DaCongBo→DaHuy` ⇒ `0008`) |

**Không sinh lỗi mới trong §7.4:**

- Tập `IN (DangTao, DaTao)` = đúng tập "chưa công bố" ⇒ phủ cả hai cạnh huỷ hợp lệ, **không** mở nhầm `DaCongBo` (dòng 199 = CẤM ⇒ 0 dòng ⇒ ném `0008`, đúng §3/TC-34) hay trạng thái kết thúc `DaHuy`/`ThatBai`/`TuChoi`.
- Hàng "Công bố / thay thế" dòng 649 vẫn `WHERE Status = DaTao` — đúng **§3** dòng 197 (`DaTao→DaCongBo`) và **§8.2 #3** dòng 688 ("0 dòng ⇒ ném `0008`"); **không** bị ảnh hưởng bởi thay đổi cạnh huỷ.
- Không còn chỗ nào khác ghi hàng rào huỷ `= DaTao` (grep `WHERE Status|DaTao, DaHuy|huỷ tay` toàn tài liệu: chỉ dòng 649–650 + 688; dòng 650 là hàng rào huỷ duy nhất).

**Đối chiếu code thật (phạm vi này là kế hoạch GĐ3, không as-built):**
`graph: codegraph_explore "AccExportRuns ExportRunStatus DaHuy DangTao DaTao CancelAsync AccountingExportAppService AccExportArtifacts"` → **không có** `AccExportRuns`/`ExportRunStatus`/`DangTao`/`DaTao`/`AccExportArtifacts`/`AccountingExportAppService` trong index (74 symbol khớp chỉ thuộc Inventory/Sales/Catalog) ⇒ §7.4 là **kế hoạch**, chưa có bảng/enum để xung đột; **không** có P1/P2 về code thật.

**Trạng thái:** P2#1 vòng 2 **đã đóng**; các P3 vòng 2 (#2–#6) **giữ nguyên** cho người duyệt, không mở lại.

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Đọc đối chiếu §3 ↔ §6.1/§7.4/§14.1 (cạnh `DangTao→DaHuy` mới); §5.2 `CancelReason`/`SupersedeReason` ↔ §3/§5.3/BR-18; §6.4 cột 10/11/15 ↔ BR-08/§8.1 #3; grep "cấm/bắt buộc/WHERE Status/DaTao/DangTao" toàn tài liệu. **Kiểm nhanh §7.4: hàng rào huỷ nay `IN (DangTao, DaTao)` khớp §3/§6.1/TC-34, không còn mâu thuẫn** | vòng 2: 1 phát hiện (#1 P2 — **nay đã đóng**) + 1 P3 (#6) |
| D2 chéo tài liệu / code / D-xx | `graph: codegraph_explore "eCommerceMetrics eCommerceMetricNames All metric names count"` → `All` **16** tên (dòng 81–107), comment "Phát 16 metric" tại `:226` ✅. `graph: codegraph_explore "TaxErrorCodes TaxLockKeys Classification RateSchedule eCommerceConsts LockKeys InventorySku"` → `TaxLockKeys.Classification` = `tax:classification:…`, `RateSchedule` = `tax:rate-schedule:…`, `InventorySku` = `inventory:…` — **không** tiền tố ✅. `graph: codegraph_explore "IReportAppService …"` → **5** method tổng hợp (`IReportAppService.cs:13-29`); `IReportFactReader` **không có** trong index ✅. `graph: codegraph_explore "Order OrderLine Sku …"` → `Order` ở `modules/Sales/eCommerce.Sales.Domain/Order.cs:11`, `OrderLine`:888, `OrderLine.Sku`:814. Đọc `eCommerceHttpApiHostModule.cs:112-151,218-253`, `eCommerceMetrics.cs:130-229`, `eCommercePermissionDefinitionProvider.cs` | 3 P3 (#2 #3 #4) |
| D3 cơ chế thực thi | 18 BR "phải/không được" → unique index / guard ném / test §14; đối chiếu BR-05/NFR-05 (UNIQUE `AccExportRuns(TenantId,IdempotencyKey)`), BR-18 (CHECK `CancelReason`/`SupersedeReason`). **Kiểm nhanh §7.4: hàng rào `UPDATE … WHERE Status IN (DangTao, DaTao)` + "0 dòng ⇒ ném lỗi" là cơ chế nguyên tử cho cạnh huỷ** | 1 P3 (#5 — index artifact §5.3 vs §8.2 #7) |
| D4 SQL chạy được | `grep '^```(sql\|powershell\|bash\|ps1)'` → **0** khối (kể cả sau sửa §7.4 — chỉ là pseudo-SQL trong bảng, không phải khối `sql`) | không có khối sql (không áp dụng) |
| D5 test/probe/tệp có thật | `graph: codegraph_explore "MetricsTests TC06_phat_ra_DUNG… LockKeyPrefixTests"` → `TC06_phat_ra_DUNG_bang_metric_da_dang_ky`:39, `LockKeyPrefixTests`:19/32/42/66 tồn tại; `IReportFactReader`/`RepPeriodClose` **chưa có** (đúng "kế hoạch"). **Kiểm nhanh: TC-34 là test kế hoạch GĐ3, chưa có trong `test/**` — đúng trạng thái "kế hoạch" (không phải as-built)**. `grep` đường dẫn backtick tới code | 1 P3 (#3 — path `Order.cs` sai) |
| D6 nhãn tin cậy | ✅ có số đo đi kèm? §0:53 "16 metric", §13:830–832 "16 metric / không đếm cứng" — **đo lại đúng 16**; F1/F7 nay ghi "24/11/2026 đã kiểm" khớp code. §7.4 không dùng nhãn ✅ | 0 phát hiện mới |
| D7 14 quy tắc + đối chiếu Nhanh | Rà #1–#14 (`design-doc-guidelines.md` §5): tiền `decimal`/không `==` (BR-06, ADR-09), index idempotency (BR-12), tên khoá `accounting:…` (§5.6), NFR có ngưỡng (§13), không hard-code endpoint NCC (§7.3). Mục "Đối chiếu mô hình Nhanh" dòng 1093 có thật, bảng `D-xx` khớp `D-03…D-196` + dòng tổng kết 1118. **Kiểm nhanh §7.4: #4 guard sai thì làm gì ("0 dòng ⇒ ném lỗi"), #5 idempotency ở biên huỷ có khoá `acc:cancel:…`** | 1 P3 (#6 — `TotalNet` chưa định nghĩa); mục đối chiếu Nhanh đạt |

## Phát hiện

> #1 (P2) của vòng 2 đã đóng ở "Vòng kiểm nhanh §7.4" — đã chuyển xuống mục "Vòng trước". Các P3 dưới đây giữ nguyên cho người duyệt.

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 2 | P3 | §5.6 dòng 416–417; §7.4 dòng 656; §15.2 F1 dòng 1057 | Trích sai số dòng của `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs`: `KeyPrefix = "ecommerce:"` ở **:222 và :237** (tài liệu ghi :127), câu "ABP ghép … KHÔNG dấu phân cách" ở **:229** (ghi :119), `AbortOnConnectFail = false` ở **:245–251** (ghi :135–144). Nội dung đúng, chỉ số dòng lỗi thời. | Cập nhật số dòng về :222/:237, :229, :245–251. |
| 3 | P3 | §0 dòng 49; §15.2 F4 dòng 1060; §6.4 dòng 575–577 | Đường dẫn `src/eCommerce.Domain/Entities/Orders/Order.cs` **không tồn tại**; thực tế `modules/Sales/eCommerce.Sales.Domain/Order.cs` (`Order`:11, `OrderLine`:888, `OrderLine.Sku`:814). `Order.cs:124` cho `OrderLine.Sku` sai dòng. Nội dung khẳng định (OrderLine thiếu snapshot thuế) vẫn đúng. | Sửa path + số dòng cho khớp `modules/Sales/eCommerce.Sales.Domain/Order.cs`. |
| 4 | P3 | §0 dòng 51 | "Đăng ký permission … **6 nhóm** permission" — `eCommercePermissionDefinitionProvider.cs` có **1** nhóm ABP (`AddGroup` dòng 25) + nhiều permission con, không phải 6 nhóm. | Sửa thành "1 nhóm `eCommerce` + nhiều permission con". |
| 5 | P3 | §5.3 dòng 369 vs §8.2 #7 dòng 692 | §5.3 khai `AccExportArtifacts (TenantId, RunId)` là **INDEX** thường, còn §8.2 #7 nói unique `(TenantId, RunId, FileName)`. Hai chỗ mô tả cùng ràng buộc khác nhau ⇒ người làm theo §5.3 sẽ tạo index thiếu tính duy nhất. | Đồng bộ: thêm unique `(TenantId, RunId, FileName)` vào §5.3 (hoặc ghi rõ §8.2 #7 chỉ là khuôn chống-trùng ở tầng khoá/trạng thái). |
| 6 | P3 | §5.2 dòng 332; §14.2 TC-03 dòng 948 | `TotalNet/TotalTax/TotalGross` ghi "tổng kiểm chứng" nhưng **không** định nghĩa `TotalNet`; TC-03 ngầm định `TotalGross = TotalNet + TotalTax` (nên `TotalNet` phải là **net sau chiết khấu**). Không nêu rõ dễ khiến `TotalNet = Σ ThanhTienChuaThue` (gộp) ⇒ TC-03 lệch đúng bằng chiết khấu. | Định nghĩa `TotalNet = Σ(ThanhTienChuaThue − ChietKhau)`, `TotalGross = Σ TongThanhToan` ngay ở §5.2/§6.4. |

## Không kiểm được

- D4: tài liệu **không có** khối `sql`/`powershell`/`bash` nào (grep = 0) ⇒ không áp dụng. Hàng rào `UPDATE … WHERE Status IN (DangTao, DaTao)` là pseudo-SQL trong bảng, không phải khối chạy được.
- D7 #14 `check:mermaid`: **chưa chạy** — vai chỉ được shell `read`+`psql-rollback`, hook `guard-shell.mjs` chặn `npm`. Đọc bằng mắt 3 sơ đồ (§2, §3, §4.1): dùng `«…»`, **không** `;` trong text, **không** `<...>` làm placeholder ⇒ không thấy lỗi cú pháp.
- D5: xác nhận **có** `MetricsTests.cs`, `LockKeyPrefixTests.cs`, `Mau01Part2CalculatorTests.cs` qua codegraph; **không** chạy `dotnet test` (ngoài phạm vi vai). TC-34 chưa có trong `test/**` (đúng trạng thái kế hoạch GĐ3).

## Vòng trước (lịch sử vòng 1–2)

<!-- Cột 2 KHÔNG phải "Mức": check-agent-artifact.ps1 đếm P1/P2 theo cột 2 của mọi bảng. -->

| # vòng 1 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — baseline metric 16, bỏ "chốt cứng 12", số học 20 → 24; đo lại code `All` = 16, `MetricsTests:70-74` không đếm cứng | P2 |
| 2 | đã đóng — F1 sửa đúng: `TaxLockKeys.*`/`InventorySku` không tiền tố, `LockKeyPrefixTests` có thật | P2 |
| 3 | đã đóng — `TongThanhToan = (ThanhTienChuaThue − ChietKhau) + TienThue` ở BR-08/§8.1 #3/§6.4 cột 15 | P2 |
| 4 | đã đóng — thêm `CancelReason`/`SupersedeReason` + 2 CHECK độ dài; đồng bộ §3/§5.2/§5.3/§6.1/BR-18 | P2 |
| 5 | đã đóng — `TienTe` chuyển sang cột CẤM (§5.4), quy tắc #8 định nghĩa lại "không cột tiền tệ"; bảng §6.4 đúng **18** cột | P2 |
| 6 | đã đóng hoàn toàn — cạnh `DangTao → DaHuy` (§3/BR-18/TC-34) **và** hàng rào DB §7.4 nay `IN (DangTao, DaTao)` (kiểm nhanh 24/11/2026); không còn run `DangTao` kẹt khi huỷ | P2 |
| 7 | còn — path `Order.cs` sai (§0/F4/§6.4); đã chép lên bảng Phát hiện #3 | P3 |
| 8 | còn — "6 nhóm permission"; đã chép lên bảng Phát hiện #4 | P3 |

| # vòng 2 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 | đã đóng — §7.4 hàng rào huỷ đổi `= DaTao` → `IN (DangTao, DaTao)`; khớp §3/§6.1/TC-34, không sinh lỗi mới (kiểm nhanh 24/11/2026) | P2 |
