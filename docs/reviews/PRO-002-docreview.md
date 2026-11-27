# Doc review — PRO-002 «Cây điều kiện & engine khuyến mãi» (kiểm nhanh v0.18, vòng 2)

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/promotions/pro-002-cay-dieu-kien-engine-khuyen-mai.md` (v0.18, 1.396 dòng) |
| Ngày · vòng | 27/11/2026 · vòng 2 (kiểm nhanh phần sửa v0.17/v0.18 theo `D-292`; không phải doc-gate đầy đủ) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | vòng 1: các mục ghi ở "Giới hạn lượt kiểm". Vòng 2: đọc lại đúng 3 P2 + 4 P3 (dòng 102–103, 188, 247–254, 601–602, 700, 943, 1054, 1069–1073, 1175, 1297–1298, 1344, 1361, 1395–1396) + đọc lại `PromotionEngine.DanhGiaCandidateAsync` toàn bộ |
| Máy | `check-feature-doc.ps1 -Path … -WP14` → exit 0 · **DAT** (16/16 heading, 20/20 BR có test §14, mục đối chiếu Nhanh dòng 1302) · `check-agent-artifact.ps1 -Kind docreview` → exit 0 |
| Đồ thị | `graph: codegraph_explore "DanhGiaCandidateAsync PromotionTreeEvaluator DanhGia CustomerRequired MaxUsagePerCustomer"` → `DanhGiaCandidateAsync` (`PromotionEngine.cs:280`) gọi `PromotionTreeEvaluator.DanhGia` tại `:342`, khối kiểm lượt ở `:368-421`; nhánh `CustomerRequired` chỉ khi `!isVoucherSource` (`:396-400`). Xác nhận thứ tự **cây trước, lượt sau** đúng `D-292`. |

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | grep "TRƯỚC cây/trước bước 5/sau bước 6", "latent/không lộ", "không xảy ra ca này"; đối chiếu A1 ↔ §3.3 ↔ §4.2 ↔ §6.1 q8 ↔ §12 ↔ blockquote | Không còn câu cũ. A1 (v) dòng 103, §4.2 bước 5–7 + blockquote dòng 253, §6.1 q8 dòng 601, §12 dòng 943, Basic Flow dòng 102 đều nói **cây trước, hết lượt sau** (`D-292`) — nhất quán nội bộ và khớp code. 0 phát hiện |
| D2 chéo tài liệu / code / D-xx | đọc lại `PromotionEngine.DanhGiaCandidateAsync` (`:280-429`) + `PromotionVoucherOutcome` (`PromotionEngineContracts.cs:46-68`) + `PromotionAppService.ValidateVoucherAsync` (`:337-385`) + `PromotionDiscountCalculator.cs:45-52` + `PromotionErrorCodes.cs:47,50,53`; đối chiếu `D-292`, `D-291`, `D-144`, `D-147`, `D-161`, `D-162`, `D-17` | Thứ tự code **đã đảo** đúng `D-292` ①(1): guard trạng thái `:296-301` → guard tĩnh `:308` → khung/kênh/`MinOrderAmount` `:310-331` → **cây** `:342` → ngân sách `:346-360` → `!Satisfied ⇒ ConditionFalse` `:362-366` → **hết lượt** `:368-421`; voucher cây `false` không ném, voucher cây `true` vẫn ném `0007`/`0119`/`0104`. Trích dẫn dòng trong SRS (`:342`, `:368`, `:392-401`, `:368-421`) **đúng**. 0 phát hiện |
| D3 cơ chế thực thi | đọc `TC-72`…`TC-76` §14.1 + DoD §14.3 + "Chưa làm" dòng 1395 | 5 luật vá có **đúng 1 TC/luật**, mỗi TC ghi kỳ vọng + "gỡ guard/đột biến ⇒ ĐỎ"; DoD `57 → 62`; "Chưa làm" ghi rõ `qa` bổ sung (`D-291` ⑥). 0 phát hiện |
| D4 SQL chạy được | grep khối ```; `git status` | Không có khối `sql`; lượt vá 0 migration ⇒ không có gì để chạy/không có DDL mâu thuẫn |
| D5 test/probe/tệp có thật | `ls test/eCommerce.Tests/Promotions/`; kiểm các tên tệp SRS nhắc | `PromotionV1SurfaceTests.cs`/`PromotionRuleCatalogTests.cs`/`PromotionGetLimitsTests.cs` có thật; dòng 1361 đã ghi đúng "đã có từ N96". Không có tên test nào bị khẳng định sai |
| D6 nhãn tin cậy | đọc blockquote §6.4 dòng 700 + "Vá REV vòng 1" + changelog v0.17/v0.18 | `0101` ghi **reachable hôm nay** qua `/api/app` → chưa map Host ⇒ `403` (không phải 404) — đúng thực tế (Host đăng ký conventional controller cho mọi `eCommerce*.Application`, `eCommerceHttpApiHostModule.cs:494-506`). Không còn nhãn "latent". Không tuyên bố đã chạy test cho lượt vá |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14; mục đối chiếu Nhanh | 16 mục + mục đối chiếu Nhanh có thật; `check-feature-doc` DAT; dòng Mermaid sửa không `;`/không `<...>`. `check:mermaid` chưa chạy (hook vai chặn `npm`). 0 phát hiện mới |

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | "Đối chiếu mô hình Nhanh (23/11/2026)" dòng 1302–1320 | `D-292` ③ ghi "bảng đối chiếu thêm/sửa hàng `D-292`" nhưng bảng **không có** hàng `D-292` (bảng chỉ liệt kê `D-07`…`D-163` của mô hình Nhanh). Nội dung `D-292` đã nằm đủ ở §3.3/§4.2/blockquote/changelog nên **không ảnh hưởng hành vi**. | Hoặc thêm 1 hàng `D-292` vào bảng, hoặc ghi chú trong `D-292` rằng bảng Nhanh không áp dụng cho quyết định nội bộ này. Không chặn. |

## Vòng trước

| # vòng 1 | Trạng thái | Mức lúc đó | Ghi chú đóng |
|---|---|---|---|
| 1 | đã đóng | P2 | §6.4 dòng 700: bỏ "latent/không lộ"; ghi `0101` reachable qua `/api/app` → `403` hiện tại, `PRO-001` map **trước khi có client** (đúng như đề nghị) |
| 2 | đã đóng | P2 | `DanhGiaCandidateAsync` đảo về **cây trước, hết lượt sau** (`:342` trước `:368-421`, xác nhận bằng `Read` + graph); SRS A1 (v)/§3.3/§4.2/bước 6–7/§6.1 q8/§12/`TC-43` (ix)/`TC-66` (iv) hoàn nguyên theo `D-292`; voucher cây `false` không ném, cây `true` vẫn ném — khớp code |
| 3 | đã đóng | P2 | Thêm `TC-72`…`TC-76` (mỗi luật 1 TC, có ca đột biến ĐỎ), DoD `57 → 62`, "Chưa làm" dòng 1395 ghi `qa` bổ sung `D-291` ⑥ |
| 4 | đã đóng | P3 | Dòng 1344 thêm "(as-built N94; **N97 sửa additive** `Pricing` — `KhongGhiSoGiaiTrinh`)"; khớp `PriceCalculationDtos.cs:39-45` + `PriceCalculationAppService.cs:251-253` |
| 5 | đã đóng | P3 | Dòng 1361 ghi 3 tệp test **đã có** từ N96 F4 |
| 6 | đã đóng | P3 | Con trỏ `Subtotal` đổi sang `PriceCalculationAppService.cs:240` (dòng 525, 602, A2 (2a)); BR-11 dùng `TinhTienDon.cs:173` |
| 7 | đã đóng | P3 | Dòng 102 + 943: "đường voucher **ghi** không xảy ra ca này (0118 trước engine); đường **chỉ đọc** cho qua candidate voucher vãng lai (`PromotionEngine.cs:392-401`) ⇒ `IsValid` có thể `true`, chỉ đọc, không mất lượt" — đúng code |

## Giới hạn lượt kiểm

- Vòng 2 chỉ kiểm **đúng 7 phát hiện vòng 1** + phần thân bị chúng chạm; **không** khảo sát lại toàn văn. Các mục không đọc lại toàn văn ở vòng 1: header 1–19, §6.3 phần 612–640, §7 (702–765), §8 (765–855), §9, §11 (898–918), §13 (964–1004), §14.1–14.2 (1004–1155) — **chưa khẳng định bằng 0** mâu thuẫn mới ở đó.
- D4: không có khối `sql` ⇒ không chạy; không kết nối PostgreSQL (0 migration).
- Chưa chạy `dotnet build`/`dotnet test`/`pnpm`/`check:mermaid` (vai chỉ đọc bị hook chặn). Kết luận code là **đọc mã**, không phải exit code; test `TC-72`…`TC-76` **chưa tồn tại** (chủ `qa`, đã ghi trong SRS dòng 1395).
