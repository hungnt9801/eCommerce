# Doc review — «ADDR-001» phần `arch` (WP-18, hàng W18-1) — vòng 2

| | |
|---|---|
| Tài liệu | `docs/features/address/ADDR-001-arch.md` (476 dòng — vòng 2, sau `D-238`…`D-242`) |
| Ngày · vòng | 26/11/2026 · vòng 2 |
| Người kiểm | agent `docrev` (ngữ cảnh sạch, phiên N77) |
| Đã đọc | vòng 2: dòng 1–476 (toàn văn); baseline vòng 1: 1–448 |
| Máy | `check-feature-doc.ps1 -Path …-arch.md -WP14` → **3 lỗi** (thiếu 16 mục/BR/"Đối chiếu Nhanh") — **đúng chủng loại** `-arch` (tiền lệ `PRC-002-arch-docreview.md`; `tools/kiem-tat-ca.ps1` không gọi công cụ này, grep = 0). `check-agent-artifact.ps1 -Kind docreview` → **DAT**. |
| TODO | `grep -c TODO` = **0**; `grep "Open"` chỉ còn Q5 (SRS, cố ý không mở) và chuỗi `OpenApiSource` — **0** câu mở của arch (§8 = "Không còn câu hỏi mở"; AQ1…AQ5 → `D-238`…`D-242`). |

`graph: codegraph_explore "AdministrativeUnitAppService AdministrativeUnitSynchronizer AdministrativeUnitSyncWorker AddressErrorCodes AdministrativeUnit"` → xác nhận `TenKhoa` `AdministrativeUnitSyncWorker.cs:38`, `GetListAsync()` `:33,51,85` + `:70-74` (đã lọc), `LapKeHoach` `:112-209`, `ApDung` `:215-253`, `DoiTen` `:138`. `graph: codegraph_explore` (vòng 1) + `Read` xác nhận `AdministrativeModels.cs:86-91` (`DoDaiMa` `_ => throw 0002`) và metadata `Volo.Abp.Authorization.Abstractions` có `set_MultiTenancySide`.

## Vòng 2 (N77)

### Đóng `mustFix` vòng 1

| # vòng 1 | Trạng thái | Mức lúc đó | Bằng chứng đã sửa |
|---|---|---|---|
| P2 #1 metric/NFR | **đã đóng** | P2 | §5.4 (dòng 346–349): counter hit/miss **chỉ** đo tỉ lệ trúng/trượt; "⚠️ **Không** dùng counter để đo p95"; NFR-001 phải đo bằng **benchmark/integration test có đồng hồ** như SRS §13 (hoặc histogram/timer nếu `XC-005` bổ sung). |
| P2 #2 UoW `requiresNew` | **đã đóng** | P2 | §5.5 bước 6 (dòng 363): "UoW của lời gọi (ABP mở sẵn — **KHÔNG** `requiresNew`)"; dòng 375–377: "**Vì sao CHỈ UoW #2 `requiresNew`**" — khớp SRS §8.1 #5/§12. |
| P2 #3 thiếu `TC-12` / NC-6 | **đã đóng** | P2 | §6 (dòng 400) thêm hàng `TC-12` ("căn cứ rỗng ⇒ NÉM `0009`"); §6.1 NC-6 (dòng 422) trỏ `TC-12`, không còn trỏ "§5.6". |
| P2 #4 thiếu nơi gọi `ApDung` | **đã đóng** | P2 | §5.6 (dòng 387–389): thêm `test/eCommerce.Tests/AddressSync/ProvincesOpenApiParserTests.cs:217`; "bỏ sót tệp thứ ba ⇒ **build đỏ**". Đo lại: `ApDung` có đúng ở worker `:130`, 3 ca `AdministrativeUnitSynchronizerTests.cs:118,258,275`, 1 ca `ProvincesOpenApiParserTests.cs:217`. |

Không còn `mustFix` mở.

### Phát hiện (vòng 2 — P3, không chặn)

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §4.3 (dòng 261) + §3.6 | Danh sách "**Tài liệu phải sửa cùng PR**" chỉ nêu `TC-13`; thiếu các dòng SRS nay lệch do quyết định mới: §6.2/§6.3 (`0601…0605` không còn "job, chỉ log" — `D-240`; cần hàng `0009`; `0002/0007` hết nợ), §9/§13 NFR-010 (tên permission đã chốt `Catalog.Import` — `D-238`), §15.1 Q8 (⇒ `D-242`). Bản thân §3.2 đã có `docDeviation` cho DTO nhưng chưa gom các mục này | Bổ sung vào mục "Tài liệu phải sửa cùng PR" của §4.3 để BA cập nhật SRS cùng PR (đúng `CLAUDE.md` §6). |
| 2 | P3 | §5.6, dòng 384 | `D-239` yêu cầu `WithData("lyDo", …)`; arch ghi `WithData("level", …)`, `WithData("code", …)` — lệch khoá payload của decision (chỉ ảnh hưởng log/chẩn đoán) | Dùng đúng `WithData("lyDo", …)` của `D-239` (có thể kèm `level`/`code`). |
| 3 | P3 | §3.2, dòng 123 | `docDeviation` gắn nhãn `(D-241)`, nhưng `D-241` là AQ4 ("chỉ `ImportDatasetAsync` trả DTO, **không** thêm method đọc"), không quyết định hình dạng `CatalogSyncRunDto`; việc DTO giàu hơn SRS §6.1 là một deviation riêng | Ghi "`docDeviation` (SRS §6.1)" thay vì `(D-241)`; `D-241` để ở mục "không thêm method đọc". |
| 4 | P3 | §5.2, dòng 309 | Hàng rào 0b dùng `AdministrativeSchemaVersion.DoDaiMa(level)`; với `Level` **không xác định** (client gửi enum ngoài dải) `DoDaiMa` ném `MaKhongHopLe` = **`0002`** (`AdministrativeModels.cs:86-91`), không phải `0605` ⇒ lượt ghi `Loi` (ErrorCode `null`) thay vì `BiChan`, trái chính lý do đã nêu ở dòng 314 | Thêm bước kiểm `Enum.IsDefined(typeof(AdministrativeLevel), level)` ⇒ `0605` **trước** khi gọi `DoDaiMa`. |
| 5 | P3 | §3.3, dòng 131 | Ô "Định nghĩa" chỉ ghi `addresses.AddChild(…)`; mẫu hiện có `eCommercePermissionDefinitionProvider.cs:168-171` dùng `AddChild(name)` **không** side ⇒ dễ copy thiếu, quyền thành `Both` thay vì `Host` (`D-238`). Metadata `Volo.Abp.Authorization.Abstractions` có `set_MultiTenancySide` | Ghi rõ `addresses.AddChild(name).MultiTenancySide = MultiTenancySides.Host` (hoặc overload `AddChild(name, MultiTenancySides.Host)` nếu có ở ABP 10.7). |
| 6 | P3 | §5.5, dòng 363–364 | Pseudocode ghi `CompleteAsync` trên "UoW của lời gọi"; toàn repo chỉ gọi `uow.CompleteAsync()` trên UoW **tự mở** (`Begin`) — `grep` 15 chỗ đều vậy; gọi `CompleteAsync` trên UoW ambient giữa method có thể commit sớm | Ghi "để **interceptor** của ABP hoàn tất UoW" (không gọi `CompleteAsync` thủ công) — vẫn giữ một transaction. |

## Đã kiểm (vòng 2)

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | Đối chiếu các mục cùng chủ đề sau khi sửa: `requiresNew` (§5.5) vs "một transaction"; `0009` (§3.6) vs §5.6/§6/NC-6; TTL (§5.4) vs §4.3/§8/§6; permission (§1.2/§3.1/§3.3) vs `D-238`; §8 "0 câu mở" vs §1.3 Q5 | 0 mâu thuẫn mới; 1 nit câu lặp "Khai cả hai nơi" (§3.3 dòng 133–135) — không ghi thành phát hiện (hành văn) |
| D2 chéo tài liệu / code / D-xx | `D-238` (giá trị `eCommerce.Addresses.Catalog.Import` = `Default + ".Catalog.Import"`, GroupName = `eCommerce` — `eCommerceConsts.cs:17`), `D-239` (`0009` sau `0008`, không dùng `0106`/`0605`), `D-240` (8 mã: `0002`/`0007`/`0605`→400, `0008`/`0601`…`0604`→409), `D-241` (không method đọc), `D-242` (Setting 86400 s, fallback + `LogWarning`, `Enabled=false`, TTL không vào khoá) | 4 decision khớp; 2 lệch nhỏ (xem #2, #3) + 1 gap `docDeviation` (#1) |
| D3 cơ chế thực thi | Mỗi BR mới có cái gì đỏ: guard (TC-22), hàng rào 5 (TC-24), hàng rào 0b (Mới), cache/TTL (TC-14 + Mới `D-242`), `DoiTen` (TC-12 + NC-6), khoá (TC-25), import (TC-21) | đủ ca; 1 edge chưa phủ (#4) |
| D4 SQL chạy được | Quét toàn văn: **0** khối ` ```sql `/` ```bash `/` ```pwsh `; chỉ ` ```csharp ` + sơ đồ cây | không có khối sql |
| D5 test/probe/tệp/`file:dòng` có thật | Mở các tham chiếu **mới/sửa**: `…Worker.cs:38,130`; `AdministrativeUnitAppService.cs:18-33,47-51,65-76,83-85`; `eCommerceMetrics.cs:81-107`; `ADDR-002-arch.md:182`; `AddressErrorCodes.cs:31`; `AdministrativeModels.cs:86-91`; `eCommerceConsts.cs:17,363-376`; `eCommercePermissionDefinitionProvider.cs:168-171`; `AdministrativeUnitSynchronizerTests.cs:118,258,275`; `ProvincesOpenApiParserTests.cs:217`; `eCommerceEntityFrameworkCoreModule.cs:84-88,99`; `LockErrorCodes.cs:20` | **0** sai; route `import-dataset` đúng quy ước ABP; `SourceHash varchar(64)` khớp ADDR-002 |
| D6 nhãn tin cậy | Nhãn "đã có/chưa có" đo lại: outbox đã bật (đo), `DistributedCache` = 1 tệp, literal `address:sync-catalog` = 1 chỗ; counter cache ghi đúng "chỉ tỉ lệ" (không đo p95) | 0 nhãn sai |
| D7 14 quy tắc + đối chiếu Nhanh | NFR có ngưỡng + cách đo (sửa xong), guard sai làm gì, chống trùng cửa vào, tên khoá chính xác, không hard-code endpoint (BaseUrl cấu hình), mục N/A có lý do, Mermaid (không có) | 0 lỗi D7; `-arch` ngoài "Đối chiếu Nhanh" theo mẫu |

## Vòng trước (vòng 1)

| # vòng 1 | Trạng thái | Mức lúc đó | Ghi chú |
|---|---|---|---|
| 1 | đã đóng (đã chép chứng cứ lên bảng "Đóng mustFix") | P2 | metric/NFR — xem bảng trên |
| 2 | đã đóng | P2 | UoW `requiresNew` — xem bảng trên |
| 3 | đã đóng | P2 | `TC-12`/NC-6 — xem bảng trên |
| 4 | đã đóng | P2 | nơi gọi `ApDung` — xem bảng trên |
| 5 | đã đóng | P3 | §3.1 route → `import-dataset` (dòng 98) |
| 6 | đã đóng | P3 | §1.1 → "3 method đọc cả bảng + `LayXaTheoTinhAsync` đã lọc" (dòng 23) |
| 7 | đã đóng | P3 | `Q8` → §5.4 `D-242` + §8 `AQ5` (dòng 338–349, 455) |
| 8 | đã đóng | P3 | §3.2 `docDeviation` DTO (còn nhãn `D-241` — xem #3 trên) |
| 9 | đã đóng | P3 | §4.1 `SourceHash varchar(64)` khớp ADDR-002 (dòng 222) |
| 10 | đã đóng | P3 | §5.2 không đổi nghĩa `0002` ở `ChuanHoaMa` (dòng 309–313) |
| 11 | đã đóng | P3 | §1.1 `eCommerceMetrics.cs:81-107` (dòng 32) |

## Không kiểm được

- **D4:** không có khối `sql`/`bash`/`pwsh`; lệnh `\d "AddrCatalogSyncRuns"` (§4.3) chưa chạy được vì bảng chưa tồn tại (thiết kế, chưa sinh migration).
- **Test thực thi:** chưa có code cho hàng rào 0b/5, guard, cache/TTL, bảng lô ⇒ ca §6 và negative control §6.1 chỉ kiểm ở mức thiết kế, **chưa chạy**.
- **`check:mermaid`:** tài liệu không có Mermaid ⇒ không áp dụng.

**VERDICT: DOC-OK**
