# Review code `PRC-001` — 28/11/2026

- Nhánh: `feat/gd3-wp17-gia-von` · base `origin/tich-hop/gd3` (`5c902ed`) · head `e56dee2`
- Phạm vi: WP-17, chuỗi `gd3-1` — `54ab64b` (W17-2 BE) · `ae200fd` (W17-3 FE no-screen) · `f2e3337` + `84d4aa9` (W17-4 QA/NC) · **`ac4af17` + `e56dee2` (F5 vòng 2: sửa P1 #1 + kỳ một ngày)**.
- Diff: ~18.600 dòng chèn; trừ nội dung generated (`...Added_Pricing_CostEngine.Designer.cs` 12.525 · `eCommerceDbContextModelSnapshot.cs` 122 · `openapi.v1.json` 106 · `schema.d.ts` 35 · `Assets/schema.sql` 67 ⇒ ~12.855) còn **~3.400 dòng mã + test** (BE ~2.040 · test ~1.420) + docs. Vượt mốc ~600 dòng K-04 nhưng thuộc **ngoại lệ chuỗi code GĐ3 `D-216`** (mỗi phiên một hàng, PR xếp chồng mở cuối chuỗi) — ghi nhận, không chặn.
- SRS: `docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md` (§1 BR-001…014, §5.2 CK `EffectiveFrom/EffectiveTo`, §12 bảng lỗi, §20.4) · ARCH `docs/features/pricing/PRC-001-arch.md` (§2 ranh giới, §3 port, §4 schema, §5.2–§5.6, §6 công thức, §7 K-06, §8 rủi ro) · testplan `docs/tests/PRC-001-testplan.md`
- Quyết định: `D-318` (C4/C9), `D-320` (tạm duyệt G2 + ca K-06 cứng), `D-321` (ánh xạ loại chuyển động), `D-322` (thiếu giá ⇒ null), `D-323` (job + command), `D-324` (0012/0013 giữ chỗ · `using eCommerce.Inventory` · VND · sổ run `requiresNew`), **`D-325`** (replay toàn lịch sử `[DauLichSu, to]`; `from` = mốc dưới kỳ được ghi), **`D-326`** (kỳ MỘT NGÀY `EffectiveTo == EffectiveFrom` hợp lệ).

## Đã kiểm

| Nhóm | Kết quả |
|---|---|
| A. Hợp đồng & đúng đắn | **0** sai chữ ký cũ (additive): `ICostPriceAppService`, `StockDocumentLine(Dto)`, mapping cột `CostPrice` cũ giữ nguyên. Mã lỗi append đúng dải (`0014`/`0015` sau `0011`; `0012`/`0013` giữ chỗ `PRC-002` — `D-324` ①). Máy trạng thái `CostEngineRun` (`BatDauLai`/`HoanTat`/`ThatBai`) **NÉM** `InvalidOperationException` ở nhánh sai; `CostPrice.Duyet` ném `0009` khi đã duyệt. **Vòng 2:** `CostPrice` cho kỳ MỘT NGÀY (`EffectiveTo == EffectiveFrom`) — khớp CK `CK_PrcCostPrices_EffectiveRange` (`>=`) + `CoHieuLucTai` bao gồm (`at <= EffectiveTo`); kỳ NGƯỢC (`<`) vẫn ném `0009` (`EffectiveTo_TRUOC_EffectiveFrom__NEM_0009` xanh). |
| B. Tiền, tồn kho, dữ liệu | Toàn bộ phép toán tiền qua `Money` (`*`, `/`, `+`, `-`, `CompareTo`); **0** `double`/`float`; **0** `Math.Round`/`decimal.Round` (làm tròn chỉ `IRoundingPolicy.Round` ở `CostEngineService.cs:302`); **0** so tiền bằng `==`. Số lượng `decimal`; tiền `HasPrecision(18,4)`. Thiếu giá ⇒ **KHÔNG** INSERT kỳ, **CẤM** `0`; giá vốn `0` hợp lệ. Tồn kho: adapter **chỉ đọc** sổ cái, **0** ghi; `StockLedger`/`InvStockLedger` không đổi. Unique tenant có `NULLS NOT DISTINCT` (SQL thô trong migration). |
| C. Bảo mật & multi-tenant | `[Authorize]` **cấp class** cho `CostPriceAdminAppService` + `CostEngineAppService`; permission `.CostPrice.Manage`/`.CostPrice.Approve` khai **đủ hai nơi** (`PricingPermissions` + `eCommercePermissionDefinitionProvider.cs:69-70`). `ICostPriceLookup`/`CostPriceLookupAdapter` **không** `IApplicationService`/endpoint. Entity `IMultiTenant` đi qua tenant filter `IRepository`; khoá phân tán `PricingLockKeys.GiaVonTheoSku` **không** lặp tiền tố `ecommerce:`. Không log secret/PII. |
| D. Test & vận hành (đo thật) | `dotnet test test/eCommerce.Tests --filter "CostEngine|CostPrice|LoaiDongGiaVon"` ⇒ **56/56 pass** (0 fail), gồm `K06_14`, `K06_14b`, `K06_15`, `LoaiDongGiaVon_khop_1_1...`. Test phủ BR-001…014 + 15 ca K-06. Negative control testplan: **NC-1/2/3/6/7/8 DAT** (exit 1 đỏ → khôi phục → exit 0); NC-7/NC-8 đúng là ca canh P1 #1 và kỳ-một-ngày của vòng 2. Migration **expand-only** (chỉ `AddColumn` nullable + `CreateTable` + `CreateIndex`), có `Down()`. |
| Ranh giới module | `modules/Pricing/**` **0 TÊN ENTITY** Inventory (`CostEngineKienTrucTests.Pricing_KHONG_nhac_ten_entity_cua_Inventory` pass); chỉ `using eCommerce.Inventory;` (Contracts enum/DTO) — hợp lệ `D-324` ②. `Pricing.Application → Inventory.Application.Contracts`; không trỏ `Inventory.Domain`; không `module → app`. |
| Idempotency + UoW run | `RunKey = cost:{tenant}:{from}:{to}:{trigger}` unique `(TenantId, RunKey)` NND; `DangChay` ⇒ 409 (`LockErrorCodes.KhongGianhDuocKhoa` map `409` ở Host), `Loi` ⇒ `Attempt++` cùng dòng, `ThanhCong` ⇒ trả run cũ. `CostPrice.IdempotencyKey` bắt buộc + unique filtered NND. Sổ run 3 pha UoW `requiresNew` (P1 commit trước khi tính). |
| Vòng 2 — P1 #1 (D-325) | **ĐÓNG thật.** `XuLyAsync` nạp `_feed.LayChuyenDongAsync(DauLichSu, to)` (`CostEngineService.cs:253`); `DauLichSu = 2000-01-01` (không `DateOnly.MinValue` vì adapter −7h underflow — đã kiểm: `1999-12-31T17:00Z` hợp lệ); `DoiSoatAsync` bỏ kỳ `ky.EffectiveFrom < from` (`:302`) ⇒ `from` chỉ là mốc dưới của kỳ ĐƯỢC GHI, đầu vào tính là toàn lịch sử; `CostEngineWorker` `tu = DauLichSu` (`:57`). `K06_14` (1200/110 ⇒ 11, KHÔNG 20) + `K06_14b` (kỳ trước `from` không ghi/không đổi, không đếm `SkippedApproved`) chứng minh. NC-7 (trả `(from,to)`) đỏ. |
| Vòng 2 — kỳ MỘT NGÀY (D-326) | **ĐÓNG thật.** `CostPrice` ctor `:118` + `CapNhatGiaVonChuaDuyet` `:197` đổi `<=` → `<`; `K06_15` (2 ngày liền kề ⇒ kỳ `From == To` ghi được, run `ThanhCong`, lookup Ngay1=10/Ngay2=15) xanh; `CostPriceTests.EffectiveTo_BANG_EffectiveFrom__HOP_LE` thay ca sai cũ. NC-8 (`<=`) đỏ. Khớp SRS §5.2 CK `>=` + `CoHieuLucTai`. |

**Dò code bằng đồ thị (bắt buộc khi kết luận về code):**
- `graph: codegraph_explore "Money operators RoundingPolicy Round CostPrice DanhDauDongCo CapNhatGiaVonChuaDuyet Duyet" -> Money có operator * / CompareTo; Round chỉ ở RoundHalfAwayFromZeroPolicy; CostPrice.Amount = Money.Cua(...)`.
- `graph: codegraph_explore "StockLedgerEntry QuantityDelta ApplyMovement GhiSo TinhButToanChungTu StockMovementType" -> StockLedgerEntry.QuantityDelta ghi = ButToan.DoLon (độ lớn DƯƠNG, dấu do MovementType) tại StockPostingService.cs:186-187` ⇒ giả định "QuantityDelta luôn dương" của adapter **đúng** với đường ghi thật.
- `graph: codegraph_explore "StockLedgerEntry QuantityDelta ..." -> ApplyMovement: Receipt/... tăng OnHand, Sale/... giảm OnHand` ⇒ dấu áp theo loại, khớp bảng `D-321`.

## Vòng 2 — phát hiện đã đóng (vòng 1)

- Vòng 1 #1 → nay **ĐÓNG** bởi `D-325` (`DauLichSu` + guard `EffectiveFrom < from` + `K06_14`/`K06_14b` + NC-7). Không còn đường tới lỗi.
- Vòng 1 #3 (thiếu ca cửa sổ giữa lịch sử) → **ĐÓNG** bằng `K06_14` + `K06_14b`.
- Vòng 1 #4 (cast enum không có test khoá đẳng thức) → **ĐÓNG** bằng `LoaiDongGiaVonKhopInventoryTests` (so tên + số + cast hai chiều).
- **Bug cùng loại phát sinh khi sửa #1** (kỳ MỘT NGÀY `EffectiveFrom == EffectiveTo` làm `CostPrice` ném `0009`, run `Loi`) → **ĐÃ SỬA** `D-326` và có `K06_15` + NC-8.

## Phát hiện (còn lại — không chặn merge)

| # | Mức | Vị trí | Phát hiện | Bằng chứng | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | P2 | `test/eCommerce.IntegrationTests/PostgreSqlPricingCostEngineTests.cs:211-262` · `:266-299` | Hai ca `K06-08b` "bền vững sổ run qua rollback UoW ngoài" và "lỗi retry `Attempt++`" **mô phỏng bằng SQL thủ công** (mở transaction tay, `update ... "Attempt" = "Attempt" + 1`) chứ **không gọi `CostEngineService`**. Chúng chỉ chứng minh DB làm đúng khi tự tay làm đúng — **không** canh được hồi quy thứ tự 3 pha UoW của `D-324` (dời `uowRun.CompleteAsync()` xuống sau P2 sẽ không làm ca này đỏ). Nhánh trạng thái vẫn có ca Application thật (`K06_08b_run_DangChay`/`_Loi`), chỉ riêng tính **bền vững** là mô phỏng. | Đọc nguyên văn: dòng 216-217 ghi run bằng `ChenRunAsync`; 220-245 tự `BeginTransaction`/`Rollback`; 272-280 tự `UPDATE`. Không thấy `new CostEngineService(...)`/`ChayAsync`. | Thêm (hoặc đổi) một ca integration dựng `CostEngineService` thật trên PostgreSQL với feed giả bơm lỗi ở P2, khẳng định dòng run vẫn còn `DangChay`/`Loi` sau rollback. Giữ SQL ca hiện tại làm phòng vệ chiều sâu. |
| 2 | P3 | `modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs:113-114` | `DocChinhSachLamTron(..., out _)` **vứt** `hopLe`: setting làm tròn sai/rỗng ⇒ rơi mặc định **im lặng**, không `LogWarning` (D-221 nói chỗ gọi phải ghi cảnh báo). | Đọc dòng 113-114; `PricingChinhSach.DocChinhSachLamTron` trả `hopLe=false` khi giá trị lạ. | Giữ `out var lamTronHopLe` và `LogWarning` khi `false` (không đổi hành vi tính). |
| 3 | P3 | `modules/Pricing/eCommerce.Pricing.Application/CostEngineService.cs:177,184,223,228` | Dùng `DateTime.UtcNow` trực tiếp thay `IClock` (quy ước dự án) ⇒ `StartedAtUtc`/`FinishedAtUtc`/`ComputedAtUtc` khó kiểm tất định. | Đọc các dòng dùng `DateTime.UtcNow`. | Tiêm `IClock`, dùng `_clock.Now`. |
| 4 | P3 | `docs/features/pricing/prc-001-bang-gia-va-gia-theo-kenh.md:1271-1272` (SRS §20.4) so với `modules/Inventory/eCommerce.Inventory.Application/CostFeedLookupAdapter.cs:125-128` | SRS ghi adapter *"KHÔNG xét dấu nhóm `Transfer` (bỏ trước khi kiểm dấu)"*, nhưng mã kiểm `QuantityDelta <= 0 ⇒ NÉM` **trước** `switch` ⇒ **có** kiểm dấu cả nhóm bị bỏ. Tác động hiện tại bằng 0 (mọi bút toán sổ cái ghi độ lớn dương — xác nhận bằng đồ thị), nhưng là **lệch doc/code** không có `docDeviation` (K-09). | `CostFeedLookupAdapter.cs:125-128` (kiểm dấu) đứng trước `switch` 132-206; SRS :1271-1272 nói ngược. | Sửa câu SRS cho khớp mã (hoặc dời kiểm dấu vào sau loại nhóm "bỏ"), ghi lý do. |

Ghi nhận (không phải phát hiện của diff này, **ngoài phạm vi WP-17**): `PriceList`/`PriceListItem`/`TierPriceSet` vẫn chặn kỳ MỘT NGÀY (`EffectiveTo <= EffectiveFrom`), trong khi `CostPrice` nay cho `==`. Khác ngữ nghĩa là đúng nếu SRS từng entity yêu cầu; nếu không, cân nhắc thống nhất ở WP chạm các entity đó.

## Đối chiếu BR / K-06

| BR | Kết luận |
|---|---|
| BR-001 (tiền qua `Money` + cột tiền tệ) | ĐẠT — `AmountValue` + `CurrencyCode` luôn đi cặp; phép toán qua `Money`. |
| BR-002 (cấm `==` trên `decimal` tiền) | ĐẠT — so bằng `Money.CompareTo`; `K06_12_khong_so_sanh_tien_bang_dau_bang` pass; test mới cũng dùng `CompareTo`. |
| BR-003 (giá theo ngày) | ĐẠT — `CoHieuLucTai(at)`; **vòng 2** kỳ MỘT NGÀY hiệu lực đúng ngày duy nhất (`K06_15`). |
| BR-008 / BR-011 (grain SKU + tenant + unique NND) | ĐẠT — unique `(TenantId, Sku, EffectiveFrom)`; migration sửa tay NND cho 2 index mới; `PostgreSqlSchemaTests` đếm 116. |
| BR-010 (cấm quy đổi tiền tệ) | ĐẠT — `TienTeNhap` khác VND ⇒ NÉM `0014`. |
| BR-012 (idempotency biên vào) | ĐẠT — `IdempotencyKey` bắt buộc + unique filtered NND; `RunKey` + sổ run. |
| BR-013 (giá vốn không lộ API công khai) | ĐẠT — `ICostPriceLookup` không `IApplicationService`. |
| BR-014 (dấu vết kiểm toán) | ĐẠT — `DanhDauDongCo`/`NguonTinh`/`RunId`/`ComputedAtUtc`; `CostEngineRun` audit ABP. |
| K06-01…K06-13 | ĐẠT theo testplan (đã chạy 56/56). |
| K06-14 / K06-14b (`D-325`) | ĐẠT — replay toàn lịch sử; `from` chỉ chặn kỳ ĐƯỢC GHI; `K06_14` = 11 (không 20), `K06_14b` không đổi kỳ trước `from`. |
| K06-15 (`D-326`) | ĐẠT — kỳ MỘT NGÀY ghi được; lookup ngày 1 = 10, ngày 2 = 15. |

## Kết luận

Vòng 2 xác nhận **P1 của vòng 1 đã đóng thật** bằng mã + test + negative control (`DauLichSu` replay toàn lịch sử, guard `EffectiveFrom < from`, `K06_14`/`K06_14b`, NC-7), và **bug kỳ MỘT NGÀY cùng loại** đã sửa đúng chuẩn SRS/DB (`D-326`, `K06_15`, NC-8, ca test cũ đã đổi kỳ vọng cho khớp SRS/DB — không phải nới test để xanh, kỳ NGƯỢC vẫn NÉM). Không phát sinh P1 mới: replay toàn lịch sử chỉ đổi phạm vi ĐỌC (fail-closed `null` thay vì số sai khi kỳ trước `from` không được ghi), 0 đổi công thức/hợp đồng/schema. Các phát hiện còn lại là **1 P2 (test bền vững sổ run mô phỏng SQL) + 3 P3** — không chặn merge, nên dọn ở phiên chạm Pricing kế tiếp. **P1 = 0.**

VERDICT: PASS
