# Doc review — «REP-002» «Báo cáo doanh thu và lãi gộp»

**VERDICT: DOC-OK**

| | |
|---|---|
| Tài liệu | `docs/features/reporting/rep-002-bao-cao-doanh-thu-lai-gop.md` |
| Ngày · vòng | 24/11/2026 · **vòng 4 — kiểm nhanh** (chỉ 3 chỗ BA vừa sửa; vòng 3 `DOC-CHANGES`, vòng 2 `DOC-CHANGES`) |
| Người kiểm | agent `docrev` (ngữ cảnh sạch) |
| Đã đọc | vòng 4: dòng **459–476, 571–588, 654–683, 925–959** + grep toàn tệp `CostUnitAmount\|CostMissing\|PrcCostPrices\|đúng một\|hai mục\|allow-list\|resolver`; vòng 3: 1–115, 280–1011; vòng 2: 1–995 toàn văn |
| Máy | `check-feature-doc.ps1 -Path <tệp> -WP14` → `KET QUA: DAT` (16/16 heading; 18/18 BR có test; mục đối chiếu Nhanh dòng 961; §15 còn 0 câu `Open`) · `check:mermaid` → **212 khối, 0 lỗi** |

## Vòng 4 — kiểm nhanh (24/11/2026)

Kiểm đúng 3 P2 vòng 3; không mở lại các mục đã đóng.

| # vòng 3 | Trạng thái vòng 4 | Bằng chứng (dòng hiện tại) |
|---|---|---|
| P2 #1 — Q12 vẫn "allow-list **đúng một** method" | **đã đóng** | §15.1 Q12 dòng 936: "`TC-02` allow-list **hai** mục (job `TongHopBaoCaoService` + `GetTongQuanDonHangAsync`), BR-001 chỉ áp đường đọc". Khớp §8.1 dòng 658 + ghi chú dòng 677–679, TC-02 dòng 840, NFR-008 dòng 805 (cả bốn chỗ đều "hai mục"). |
| P2 #2 — §1 đọc `PrcCostPrices` ở **đường đọc** | **đã đóng** | §1 dòng 107 (nhánh kế hoạch): "`GET /gross-margin` đọc `RptFactSalesLines` (đã có `CostUnitAmount`/`CostMissing` do job chụp lúc tổng hợp) … **không** đọc `PrcCostPrices` lúc truy vấn". Khớp §5.2.2 dòng 439, §5.2.4 dòng 474, §6.3 dòng 580 ("đọc số đã chụp trên fact, không tra lại bảng kỳ"). |
| P2 #3 — mô tả CHECK nhận sai "chặn `0`" | **đã đóng** | §5.2.2 dòng 447: CHECK `CostMissing = (CostUnitAmount IS NULL)` "chặn **cờ lệch**: không thể `CostMissing = false` khi `CostUnitAmount IS NULL`. **Không** chặn giá trị `0` (giá vốn `0` hợp lệ — `CostPrice.cs:86-87`); vế 'cấm `0`' do resolver (không tra được ⇒ ghi `NULL` + cờ) + TC-16, không do CHECK". Khớp §8.1 BR-002 dòng 659 (CHECK fact dùng `CostUnitAmount`) và TC-16 dòng 854. |

### Đối chiếu chéo 3 chỗ sửa — không phát sinh mâu thuẫn mới

- **Q12 ↔ §8.1/TC-02/NFR-008:** cả bốn chỗ thống nhất "allow-list **hai** mục (job + `GetTongQuanDonHangAsync`)". Vế "**đúng một** method" còn lại chỉ nói về **ngoại lệ ngoài phạm vi** `REP-002` (dòng 658), không phải về allow-list của TC-02 — không mâu thuẫn.
- **§1 ↔ §5.2.2/§5.2.4/§6.3:** cùng một chỗ chụp giá vốn (`RptFactSalesLines.CostUnitAmount` + `CostMissing`) và cùng một hành vi đọc (không tra lại `PrcCostPrices` lúc truy vấn). §1 nằm trong câu "**Nhánh kế hoạch (lãi gộp)**", nên "đã có … do job chụp" hiểu là trạng thái **sau khi** job ghi — khớp nhãn ⬜ "kế hoạch" ở §5.1 dòng 401 và §5.2.2 dòng 449.
- **§5.2.2 ↔ §8.1 BR-002 ↔ TC-16/TC-36/TC-40/TC-41:** CHECK trên fact chỉ chặn **cờ lệch** theo đúng biểu thức `CostMissing = (CostUnitAmount IS NULL)`; vế "cấm `0` thay cho không biết" do resolver ghi `NULL` + cờ và TC-16 kiểm (`CostAmount IS NULL`, `CostMissing = true`). Ba test còn lại của BR-002 vẫn giữ nguyên phạm vi (snapshot, phí sàn dự kiến/thực).

## Đã kiểm

| Nhóm | Cách kiểm | Kết quả |
|---|---|---|
| D1 tự mâu thuẫn | đọc 3 chỗ sửa + grep `đúng một`/`hai mục`/`allow-list`/`PrcCostPrices`/`CostMissing`/`resolver`; đối chiếu chéo §1/§5.2.2/§5.2.4/§6.3/§8.1/§14/§15 | 0 phát hiện mới |
| D2 chéo tài liệu / code / D-xx | đồ thị + `Read` xác nhận các mốc dùng trong 3 chỗ sửa: `TongHopBaoCaoService.cs:70-71,255-259`, `ReportAppService.cs:109-168,125-135`, `FactSalesLine.cs:17-61,47-58`, `ReportingDbContextModelCreatingExtensions.cs:78-120`, `CostPrice.cs:86-87,114-115` | 0 phát hiện mới |
| D3 cơ chế thực thi | soi lại BR-002 sau khi tách vế CHECK (fact) / resolver / TC-16: cái gì đỏ khi vi phạm | 0 phát hiện mới |
| D4 SQL chạy được | grep ```` ```sql ```` / ```` ```powershell ```` / ```` ```bash ```` trên tệp | không có khối sql (0 phát hiện) |
| D5 test/probe/tệp có thật | đối chiếu TC-02/TC-16 với code và test hiện có; các tên test khác giữ nguyên từ vòng 2 | 0 phát hiện mới |
| D6 nhãn tin cậy | ✅/🔶/⬜ ở 3 chỗ sửa: §1 nằm trong nhánh "kế hoạch"; §5.2.2 CHECK và §5.2.4 đều ⬜ "kế hoạch"; số dòng dẫn đúng code | 0 phát hiện mới |
| D7 14 quy tắc + đối chiếu Nhanh | `design-doc-guidelines.md` §5 #1–#14; mục "Đối chiếu mô hình Nhanh" dòng 961 có bảng + dòng `graph:`; `check:mermaid` 212 khối / 0 lỗi | 0 phát hiện mới |

## Nguồn đồ thị đã dùng

- `graph: mcp__codegraph__codegraph_explore "TongHopBaoCaoService IRepository Order OrderLine IsCancelled Status Completed GetTongQuanDonHangAsync ReportAppService"` → `TongHopBaoCaoService` inject `IRepository<Order>`/`<OrderLine>` tại `TongHopBaoCaoService.cs:70-71`; `GetTongQuanDonHangAsync` ở `ReportAppService.cs:109-168`, đọc `Order`/`OrderLine` tại `:125-135` (đúng allow-list hai mục của Q12/TC-02/NFR-008).
- `graph: mcp__codegraph__codegraph_explore "TinhNgayAsync TongHopBaoCaoService InternalAsync Completed IsCancelled RetailBillStatus donHang dongDonHang"` → job lọc `RetailBillStatus.Completed` tại `TongHopBaoCaoService.cs:255-256` và `!x.IsCancelled` tại `:258-259`; đọc dòng đơn tại `:299` — khớp số dòng §8.1 dẫn.
- `graph: mcp__codegraph__codegraph_explore "RptFactSalesLine FactSalesLine ConfigureReporting ReportingDbContextModelCreatingExtensions CostUnitAmount CostMissing CostAmount"` → `RptFactSalesLine` (`FactSalesLine.cs:17-61`) **không** có `CostUnitAmount`/`CostMissing` (trường tiền `:47-58`); mapping fact `ReportingDbContextModelCreatingExtensions.cs:78-120` không khai hai cột ⇒ hai cột là **kế hoạch**, khớp nhãn ⬜ ở §5.1/§5.2.2.
- `graph: mcp__codegraph__codegraph_explore "CostPrice PrcCostPrices CostUnitAmount CoHieuLucTai EffectiveFrom EffectiveTo CostPriceAppService"` → `CostPrice.cs:86-87` cố ý **không** đòi `> 0` ("giá vốn `0` là hợp lệ"); test `Gia_von_BANG_0__la_HOP_LE` (`CostPriceTests.cs:101`); `CoHieuLucTai` (`:114-115`).

## Phát hiện

| # | Mức | Vị trí | Vấn đề | Đề nghị |
|---|---|---|---|---|
| 1 | P3 | §4.3 dòng 319 | (mang từ vòng 3 #4) Cạnh `EXT` trỏ vào `P2["2. Tinh — ham THUAN"]`; hàm thuần **không** được đọc DB (BR-009), nên sơ đồ ngụ ý việc chụp giá vốn nằm trong hàm thuần trong khi job (`P1`) mới là nơi đọc `PrcCostPrices`. | Trỏ cạnh về `P1` (hoặc thêm node "job đọc giá vốn"), giữ `P2` thuần. |
| 2 | P3 | §5.2.1 dòng 423 | (mang từ vòng 3 #5) Khai INDEX `(TenantId, BusinessDate)` không tồn tại; code chỉ có UNIQUE `(TenantId, BusinessDate, Nguon)` (`ReportingDbContextModelCreatingExtensions.cs:189-192`). | Xoá dòng INDEX, hoặc ghi "tiền tố `(TenantId, BusinessDate)` của UNIQUE phục vụ truy vấn khoảng". |
| 3 | P3 | §8.2 dòng 701 | (mang từ vòng 3 #6) Dẫn `eCommerceHttpApiHostModule.cs:115-128` cho `AbpDistributedLockOptions.KeyPrefix`; `KeyPrefix` ở `:222`/`:237` (khối `:225-238`), đúng dòng được dẫn thấy code khác. | Sửa số dòng thành `:222,225-238`. |
| 4 | P3 | §14.1 TC-35 dòng 873 | (mang từ vòng 3 #7) TC-35 (nhịp job đọc từ Setting, `D-172`) ghi **Phủ `NFR-007`** — NFR-007 là "Quan sát được", không phải nhịp job. | Trỏ về `NFR-003` (hiệu năng job) + `D-172`; giữ NFR-007 cho log/metric. |
| 5 | P3 | §5.2.4 dòng 474 | (mang từ vòng 3 #8) Ghi "ghi lúc job tổng hợp ngày bán từ bảng đọc `PrcCostPrices`" nhưng không nói tra theo **ngày hiệu lực**; nếu code lấy "giá hiện tại" lúc job chạy thì "chụp lúc bán" sai khi bảng giá vốn có dòng lùi ngày. | Ghi rõ "tra `PrcCostPrices` theo `CoHieuLucTai(BusinessDate)`" trước khi chốt hợp đồng đọc giá vốn. |

> 5 P3 **không chặn** verdict; để nguyên cho người duyệt (3 P3 mang từ vòng 2 + 2 P3 mới ở vòng 3). `DOC-OK` chỉ đòi 0 P1 và < 3 P2.

## Không kiểm được

- **D4:** tài liệu **không có** khối `sql`/`powershell`/`bash` nào → không có lệnh để chạy. Không có PostgreSQL trong phạm vi phiên review.
- Không chạy lại `dotnet test`: nhãn "test `Wp08BaoCaoTests` xanh" (§14.3 dòng 895) là **số đo của repo**, không đo lại trong phiên này.
- Không đọc lại toàn văn các mục **không** đổi (§2, §3, §7, §9, §10, §12) — vòng 2 đã `DOC-OK`; vòng 4 chỉ kiểm 3 chỗ BA đã sửa theo yêu cầu.

## Vòng trước (vòng 3 — 3 P2 + 5 P3)

| # vòng 3 | Trạng thái | Mức lúc đó |
|---|---|---|
| 1 (Q12 "đúng một method") | đã đóng | P2 |
| 2 (§1 đọc `PrcCostPrices` lúc truy vấn) | đã đóng | P2 |
| 3 (mô tả CHECK nhận sai "chặn `0`") | đã đóng | P2 |
| 4 (cạnh Mermaid PRC trỏ hàm thuần) | còn ⇒ Phát hiện #1 | P3 |
| 5 (INDEX 2 cột không tồn tại) | còn ⇒ Phát hiện #2 | P3 |
| 6 (số dòng `KeyPrefix`) | còn ⇒ Phát hiện #3 | P3 |
| 7 (TC-35 phủ `NFR-007`) | còn ⇒ Phát hiện #4 | P3 |
| 8 (§5.2.4 thiếu "theo ngày hiệu lực") | còn ⇒ Phát hiện #5 | P3 |

> Ghi chú: các mục đã `DOC-OK` từ vòng 2 (P2 #1–#5 vòng 2) không mở lại. Vòng 4 không phát hiện P1/P2 mới.
