# Review code `REP-001` — 27/11/2026 (cập nhật **vòng 2**)

- Nhánh: `feat/gd3-wp26-read-model` · base `origin/tich-hop/gd3` · HEAD `a0ade44`
- Vòng 1: review tại HEAD `7f6c8f4` (verdict `CHANGES`, P1 = 1). Vòng 2: review diff **`608915d..HEAD`** = commit `4a964e6` (vá P1/P2 + 5 test) + `a0ade44` (bằng chứng NC-4).
- Diff vòng 2 (code sản phẩm + test mới, **trừ tài liệu**): ~**192 dòng thêm / ~16 xoá** (`TongHopBaoCaoService.cs` +48, `RepVanHanhAppServiceTests.cs` +116, `ReportAdminAppService.cs` +19, `ReportsController.cs` ±4, `HaTangBaoCao.cs` +5). Toàn bộ diff cộng dồn tính cả migration: `18.980` dòng (migration Designer/snapshot là generated).
- SRS: `docs/features/reporting/rep-001-read-model-job-tong-hop.md` (§3.3 máy trạng thái chốt kỳ, §5.2.4–§5.2.6, §6.2 endpoint, §7.3 idempotency, §8.1 15 BR, §8.2 xử lý đồng thời, §9 phân quyền, §12 xử lý lỗi). Arch: `REP-001-arch.md`. Testplan: `docs/tests/REP-001-testplan.md` (vòng 2: 18 ca Application, NC 4/4).

## Đã kiểm vòng 2 (trọng tâm: 5 phát hiện vòng 1)

| Nhóm | Kết quả |
|---|---|
| P1 — cổng `0011` ở **đường ghi** | ✅ `TongHopBaoCaoService` tiêm **bắt buộc** `IRepository<RepPeriodClose, Guid> _kyChot` (không null-guard fail-open); `ChanNeuGiaoKyDaChotAsync(ngay)` kiểm `Status == DaChot && FromDate <= ngay && ToDate >= ngay` và được gọi **trước mọi `DELETE`** ở cả 3 đường ghi fact: `ChayAsync` (dòng 159, trong vòng lặp ngày), `ChayKhoangAsync` (dòng ~268, đầu hàm), `ChupAnhTonKhoAsync` (dòng ~418). Xác nhận **không còn đường ghi fact nào khác** (`DeleteAsync`/`InsertManyAsync` trên `_dongSuKien`/`_tongHopNgay`/`_anhTonKho` chỉ nằm trong 3 hàm này). |
| P2 #1 — 23505 → 500 | ✅ `StartRebuildAsync` pre-check `FirstOrDefaultAsync(JobName == Rebuild && Projection == input.Projection && Status IN (Queued, Running))` ⇒ trả lượt đang chạy với `AlreadyRequested = true`, **không** chèn (đứng trước unique `UX_..._DangChay`). |
| P2 #2 — kind `AnhTonKho` | ✅ `DoiChieuMotNgayAsync` truyền số ảnh qua `anhTonKhoNguon/anhTonKhoReadModel` (2 tham số cuối); `SoDoiChieu.KiemNgay` phát `MismatchKind.AnhTonKho` với `Expected = nguồn`, `Actual = read-model`, `Delta = actual − expected` ⇒ dấu đúng (test: nguồn 1, read-model 0 ⇒ `Delta = −1`). |
| P3 #5 — 202 → 200 | ✅ `[ProducesResponseType(202)]` → `[ProducesResponseType(200)]` ở cả `StartRebuildAsync` và `ResumeRebuildAsync`; SRS §6.2 không ràng buộc 202. |
| P3 #6 — BR-002 allow-list đối chiếu | ⏸️ **Để nguyên** theo chỉ đạo (chủ `ba`, ghi nợ) — **không** chặn PASS. |
| Test mới (5 ca) | ✅ Có khoá hành vi thật, **không rỗng**: (1) `ChayKhoangAsync` ngày trong kỳ `DaChot` ⇒ ném `KyDaChot` **và** `SoLanGoiXoa == 0` cho Fact+Agg, `SoLanXoa == 0`, `SoLanChen == 0` (chứng minh cổng đứng **trước** `DELETE`); (2) `ChayAsync` gặp ngày trong kỳ ⇒ `KyDaChot`; (3) kỳ **không** phủ ngày ⇒ chạy được (`SoLanGoiXoa == 1` mỗi repo — chống chặn quá tay); (4) đã có lượt Queued cùng projection ⇒ `AlreadyRequested`, `Id` = lượt cũ, `SoLanChen == 0`, đúng 1 dòng; (5) `TonKho` lệch số ảnh ⇒ `AnhTonKho`, `Expected 1m`, `Actual 0m`, `Delta −1m`. |

## Truy vấn đồ thị (bắt buộc trước khi kết luận code)

- `graph: codegraph_explore "TongHopBaoCaoService ChayAsync ChayKhoangAsync"` → 3 điểm `DeleteAsync` fact nằm ở `ChayAsync:165-166`, `ChayKhoangAsync:277-278`, `ChupAnhTonKhoAsync:424` — cả 3 đều có `ChanNeuGiaoKyDaChotAsync(ngay)` đứng trước (diff `608915d..HEAD`).
- `graph: codegraph_explore "ReportAdminAppService StartRebuildAsync ResumeRebuildAsync"` → `StartRebuildAsync` nay có pre-check `Queued/Running` trước `InsertAsync`; `ResumeRebuildAsync` → worker → `ChayKhoangAsync`/`ChupTonKhoAsync` nên **đi qua** cổng đường-ghi.
- `graph: codegraph_explore "SoDoiChieu KiemNgay AnhTonKho"` → `KiemNgay:61-65` phát `AnhTonKho` khi `anhTonKhoNguon.HasValue && anhTonKhoReadModel.HasValue && decimal.Compare(actual − expected, 0) != 0`.

## Lịch sử phát hiện vòng 1 — trạng thái

| # | Mức (gốc) | Vị trí | Trạng thái |
|---|---|---|---|
| 1 | P1 · đã vá | `ReportRebuildWorker.cs:128-147` · `TongHopBaoCaoService.cs` (3 đường ghi) · `ReportAdminAppService.cs:128-134` | ✅ **Đã vá**: cổng `0011` ở đường ghi (3 hàm), resume đi qua cổng; test chứng minh 0 lời gọi xoá/0 dòng chèn khi bị chặn; NC-4 đỏ→xanh. P1 = 0. |
| 2 | P2 · đã vá | `ReportAdminAppService.cs:97-123` | ✅ **Đã vá**: pre-check `Queued/Running` ⇒ `AlreadyRequested`, không chèn. |
| 3 | P2 · đã vá | `ReportAdminAppService.cs:553-567` (`ChanNeuGiaoKyDaChotAsync`) | ✅ **Đã vá**: 3 ca `BR011_*` + NC-4 khoá cổng 0011 (đường ghi). |
| 4 | P3 · đã vá | `ReportAdminAppService.cs:480-490` | ✅ **Đã vá**: phát `AnhTonKho`, `Delta` đúng dấu (ca `BR012_*`). |
| 5 | P3 · đã vá | `ReportsController.cs:77,84` | ✅ **Đã vá**: `ProducesResponseType(200)`. |
| 6 | P3 · nợ có chủ | `ReportAdminAppService.cs:457-491` · `RepBatBienTests.cs:62-63` · SRS §8.1 BR-002 | ⏸️ **Nợ `ba`** (theo chỉ đạo, không chặn): BR-002 allow-list chưa nêu "đối chiếu đọc nguồn"; `TC02` chỉ quét `ReportAppService.cs`. |

## Phát hiện vòng 2 (còn lại — không chặn)

| # | Mức | Vị trí | Phát hiện | Kịch bản cụ thể | Khuyến nghị |
|---|---|---|---|---|---|
| R1 | P3 | `TongHopBaoCaoService.cs:459-476` (cổng) ↔ `ReportAdminAppService.ClosePeriodAsync` | **Khe TOCTOU hẹp**: cổng đọc `RepPeriodCloses` trong UoW của ngày, rồi worker `DELETE`+`INSERT`; `report:rebuild` và `report:period-close` là **hai khoá khác nhau** (§8.2 #7 — có chủ đích). | Một `POST /period-close` commit kỳ `DaChot` phủ `ngay` **đúng** khoảng mili-giây giữa lúc cổng đọc (đã qua) và `DELETE` commit, **và** nguồn của ngày đã đổi so với bản tổng hợp cũ ⇒ lượt rebuild ghi đè số đã chốt. Cần 2 thao tác admin chạy đua cùng ngày + dữ liệu nguồn thay đổi. | Không nâng P1 (cửa sổ cực hẹp; SRS đã chọn 2 khoá khác nhau). Nếu muốn đóng hẳn: khoá chung khi ghi fact, hoặc trigger DB chặn write vào kỳ `DaChot` — ghi nợ có chủ. |
| R2 | P3 | `ReportAdminAppService.cs:106-116` (pre-check) | **Đua tranh khi double-submit**: hai `POST /rebuild` **đồng thời** cùng tenant/projection đều thấy pre-check rỗng (2 transaction độc lập) ⇒ một `InsertAsync` vẫn `23505` → 500. | Hai instance/hai tab gửi rebuild gần như cùng lúc. Lỗi chỉ là HTTP 500 (không mất/ghi sai dữ liệu — lượt đầu thắng); thường gặp thì hiếm. | Nếu cần: bọc pre-check + insert dưới khoá `report:rebuild:*`, hoặc bắt `23505` → trả `AlreadyRequested`/`0015`. Không chặn PASS (không phải P1: không hỏng dữ liệu). |
| R3 | P3 | `ReportAdminAppService.cs:101-116` | `AlreadyRequested = true` được trả cho **khoảng khác** với lượt đang chạy — gộp nghĩa "cùng yêu cầu đã xếp hàng" (idempotency theo khoảng+lý do) với "một rebuild KHÁC đang chạy". | Admin xin rebuild [20..25], nhận về run [01..05] với `AlreadyRequested = true` ⇒ tưởng khoảng của mình đã được xếp hàng. | Trả thêm cờ/lý do phân biệt (vd `DangCoLuotKhacChay`) hoặc message rõ; ghi vào tài liệu API. |

## Ghi chú (không tính phát hiện)

- **NC-4**: `artifacts/negative-control/20261127-034726-*.log` cho thấy `do` = **2 ca `BR011_*` FAIL**, `xanh` = **71/71 pass** ⇒ test khoá được cổng. 📌 Testplan §4 ghi vị trí đột biến tại `RepPeriodClose.cs:115` (`Chot()` giữ `DangChot`) trong khi thông điệp ORCH nêu "đảo `==`→`!=` ở `TongHopBaoCaoService.cs`" — **chênh lệch mô tả**, nhưng cả hai đều vô hiệu cổng 0011 và bằng chứng đỏ→xanh hợp lệ. Đề nghị ORCH đồng nhất lại dòng NC-4 cho khớp log.
- **Vận hành**: nếu một kỳ `DaChot` phủ **hôm nay**, `ReportingAggregationWorker` (nhịp 15 phút) sẽ ném `0011` mỗi nhịp ⇒ 1 dòng `RepRunLog` `Failed`/hượt (không ghi số). Đúng nghĩa "đóng băng dữ liệu", nhưng nên ghi hướng dẫn vận hành "chốt kỳ tới hôm qua" để tránh nhiễu log/metric.
- Phần lõi giữ nguyên tốt: 10 route §6.2, map lỗi `0010–0017` đúng HTTP, máy trạng thái NÉM `0015`/`0017`, tiền so bằng `Delta` (`decimal.Compare`), 3 entity `IMultiTenant` + 4 index NND, worker đổi tenant trước UoW, migration expand-only.

## Kết luận

Vòng 2: **P1 = 0**. P1 vòng 1 (cổng `0011` ở đường ghi) **đã vá đúng** — cả 3 đường ghi fact đều kiểm kỳ `DaChot` **trước `DELETE`**, dependency bắt buộc, resume đi qua cổng, và test chứng minh bất biến "0 dòng thay đổi" (0 lời gọi xoá/0 dòng chèn) + NC-4 đỏ→xanh. Hai P2 vòng 1 đã vá. Còn **3 P3** (R1–R3: TOCTOU hẹp, đua double-submit → 500, ngữ nghĩa `AlreadyRequested`) và **1 nợ P3 có chủ `ba`** (BR-002) — không chặn merge.

VERDICT: PASS
