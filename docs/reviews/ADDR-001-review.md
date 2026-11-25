# Review code `ADDR-001` — 26/11/2026

- Nhánh `feat/gd3-wp18-dia-chi` · base `7d15f84` (merge-base `origin/tich-hop/gd3`) · head `540ccd2`
- Diff: **9.830 dòng / 77 tệp** (đã trừ `**/Migrations/**Designer.cs`, `eCommerceDbContextModelSnapshot.cs`,
  `schema.sql`) — code 3.416/37 · test 3.952/21 · docs 2.495/19. Hai migration thật (65 + 106 dòng) đã kiểm riêng.
- SRS: `docs/features/address/addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md` (§1 BR-001…014, §11 schema, §13 NFR, §14 TC/AC)
  · `docs/features/address/ADDR-001-arch.md` (§3.5 ETO, §5.4 cache, §7 test/NC) · `docs/nhanh/decisions.md` `D-232`…`D-251`
  · `docs/tests/ADDR-001-testplan.md`.
- Vai `rev` chỉ ghi tệp này; **không** sửa code/test/docs khác.

**P1 = 0** · P2 = 3 · P3 = 4 · **VERDICT: PASS**

## Đã kiểm

| Nhóm | Cách kiểm (đích danh) | Kết quả |
|---|---|---|
| A1 hợp đồng | Đối chiếu BR-001…014 với code: danh mục là dữ liệu (`AdministrativeUnitSynchronizer`), mọi truy vấn có `SchemaVersion` (`AdministrativeUnitAppService` mọi method), `DoiTen` bắt buộc `legalBasis` + `0009` (`AdministrativeUnit.cs:141-152`), hàng rào 5 `0008` kiểm **trước** hàng rào 4 (`Synchronizer.cs:217-224`), `LapKeHoach` ghi lô mọi lượt (`CatalogSyncRun`), guard khởi động `AddressSyncGuard` gọi trực tiếp trong `ConfigureServices` (`eCommerceHttpApiHostModule.cs:171-176`) | Khớp BR; lệch duy nhất là tài liệu stale — P3 #6 |
| A2 mã lỗi | `0601…0605` + `0008` + `0002` map HTTP đúng (`HostModule` `D-240`); `0009` **không** map vì không reachable qua HTTP (mọi call site `ApDung` đều truyền `danhMuc.Nguon`) | Đúng dải `eCommerce:Address:` |
| A3 máy trạng thái | `AdministrativeUnit` đổi trạng thái qua `DoiTen`/`MoLai`/`Dong`; enum `CatalogSyncTrigger`/`CatalogSyncOutcome` khai `0,1 (,2)` — chỉ thêm ở cuối | Không có nhánh im lặng |
| A4 idempotency | `SourceHash` tính **server** trên danh mục chuẩn hoá (`AddressSourceHash.cs`), không unique (idempotency là nội dung); `ImportDatasetAsync` không nhận hash từ client | Đúng `D-78` |
| A5 ranh giới | `Address.Application.Contracts.csproj` **chỉ** `Volo.Abp.Ddd.Application.Contracts`; `grep "Address.Domain\|Address.Application"` trong Contracts = chỉ comment (0 using/tham chiếu) | Không có phụ thuộc ngược |
| B1/B2 tiền/số | ADDR-001 không có trường tiền; số đếm `int`; thời gian `timestamptz` + `ChuanHoaUtc` (`CatalogSyncRun.cs:152-158`) | N/A tiền |
| B3 append-only | `CatalogSyncRun : CreationAuditedAggregateRoot<Guid>` (không `IsDeleted`), 0 public setter, chỉ factory `Ghi`; `AddressSyncInvariantTests` kiểm reflection | Đúng |
| B4 unique tenant | Migration `20261125191757` chỉ tạo bảng + index **không unique**; `NULLS NOT DISTINCT` không áp dụng (không có unique index mới cho ADDR-001) | Đúng |
| B5 UoW | `AdministrativeUnitSyncWorker` mở `Begin(requiresNew: true, isTransactional: true)` cho lô; dòng `BiChan`/`Loi` ghi UoW **riêng** (`GhiLoThatBaiAsync:263-305`); `ImportDatasetAsync` ghi lô trong UoW của lời gọi, lượt chặn/lỗi trong `requiresNew` (`:503-544`) | Đúng E2 |
| B6 migration | `Up` chỉ `CREATE TABLE`/`CREATE INDEX`; `Down` chỉ `DropTable`; `eCommerceDbContextModelSnapshot.cs` +261/−0; `schema.sql` khớp 1-1 (kể cả CHECK) | Expand-only |
| C1 quyền | `[Authorize]` cấp class ở `AdministrativeUnitAppService.cs:29`; `CatalogImport` gắn **cấp method** `:267`; hằng `eCommerceConsts.cs:376-385` + `AddChild(...).MultiTenancySide = Host` (`eCommercePermissionDefinitionProvider.cs:172-176`) | Định nghĩa 2 chiều, side Host |
| C2 anonymous/claims | `grep AllowAnonymous modules/Address` = 0 khai báo thật (chỉ 1 comment); không đổi `MapInboundClaims`/`NameClaimType` | Đúng |
| C3 tenant/PII | `CatalogSyncRun`/`AdministrativeUnit` là `IMultiTenant`, truy vấn qua repository (filter ABP); dòng lô chỉ lưu `ErrorType = ex.GetType().FullName`, **không** message/stack/PII | Đúng BR-011 |
| D1/D2 test | +47 unit +9 integration (testplan §2), mỗi BR có ca; NC **8/8 đỏ→xanh** có dòng `exit` (testplan §4); reflection `TC-18/TC-19`; integration `PostgreSqlAddressSyncTests` là test hành vi thật (không tautology) | Đạt |
| D3 cổng | Chạy lại độc lập ở mục "Bằng chứng" bên dưới | Đạt |
| D4 phạm vi | `git diff --name-only` **0** tệp `apps/` · `/api/v1` · adapter GHN/GHTK · `eCommerce.slnx` · `Directory.Packages.props` · `pnpm-lock.yaml`; `tools/probe-cong-tai-lieu.ps1` là sửa **công cụ** để nhận mã 4 chữ (`ADDR-###`) — cần cho cổng tài liệu của chính WP | 0 tệp cấm; độ lớn diff ghi ở P2 #7 |

## Bằng chứng (tự chạy phiên này, không tin lời)

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental --nologo -v q` | **0** | **0 error / 9 warning** — cả 9 warning đều ở tệp **không thuộc** diff ADDR-001 (`AddressAppService`, `AddressDirectory`, `Sales`), 0 warning mới |
| `dotnet test test/eCommerce.Tests/eCommerce.Tests.csproj --no-build --nologo` | **0** | **2166 passed / 0 failed / 0 skipped** |
| `dotnet test test/eCommerce.IntegrationTests/eCommerce.IntegrationTests.csproj --no-build --nologo` | **0** | **106 passed / 0 failed / 0 skipped** (PostgreSQL 17 thật qua Testcontainers) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind testplan -Path docs/tests/ADDR-001-testplan.md -Srs docs/features/address/addr-001-…md` | **0** | `BR trong SRS=14 · chua co trong testplan=0 · KET QUA: DAT` |
| `git diff 7d15f84..HEAD --numstat` (lọc tệp sinh tự động) | — | 9.830 dòng / 77 tệp; code 3.416/37 · test 3.952/21 · docs 2.495/19 |

> Migration: `20261125191757_Add_Addr001_CatalogSyncRuns.cs` (65 dòng) — `CREATE TABLE` + 2 index + 3 CHECK,
> `Down` = `DropTable`; `schema.sql` dòng mới khớp cột/CHECK; `PostgreSqlSchemaTests` đếm **94** index
> `NULLS NOT DISTINCT` và test integration xanh trên PostgreSQL thật.

## Phát hiện

| # | Mức | Vị trí | Phát hiện | Bằng chứng | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | P2 | `modules/Address/eCommerce.Address.Domain/Sync/AdministrativeUnitSynchronizer.cs:307-315` | Overload tương thích `ApDung(keHoach, tenantId)` tự bơm `legalBasis = "Tương thích ngược: kế hoạch đã có lý do thay đổi"`. Bất kỳ caller nào dùng overload này sẽ **đổi tên** đơn vị mà **không** có căn cứ pháp lý thật ⇒ `0009` của BR-007/`D-239` không bao giờ ném. Hiện **0** call site production (4 call site test đã chuyển sang dạng 2 tham số), nên chưa ra production — nhưng là "cửa hậu" của hợp đồng. | `graph: codegraph_explore "… AdministrativeUnitSynchronizer ApDung"` → overload `:314-315`; testplan §6.5 `D-250` ghi nợ chủ `be` | Xoá overload ở phiên BE kế; **hạn cứng: trước PR `tich-hop/gd3 → main`** (cơ chế đỏ = trình biên dịch nếu còn call site 1 tham số). Không chặn merge vào nhánh tích hợp. |
| 2 | P2 | `modules/Address/eCommerce.Address.Domain/CatalogSyncRun.cs:137` + `modules/Address/eCommerce.Address.Application/AdministrativeUnitAppService.cs:277-293` | `SourceName` chỉ `Trim()`, **không** kiểm độ dài; cột DB `character varying(512)`. Client `ImportDatasetAsync` gửi `SourceName` dài hơn 512 (không bị chặn ở input) ⇒ INSERT dòng lô lỗi lúc commit — commit của UoW ABP nằm **ngoài** `try/catch` của app service ⇒ **500 và không có dòng lô** (vi phạm BR-009 "mọi lượt ghi lô"). Nhánh `GhiLoThatBaiAsync` cũng hỏng y hệt vì cùng gọi `Ghi` (không giới hạn). Không mất tiền/tồn, nhưng mất dấu vết lượt nạp. | Migration dòng 22 `varchar(512)`; `CatalogSyncRun.Ghi:137` không có `Check.Length`; so sánh: `CarrierAddressSyncRun.Ghi` **có** `CatChuoi(source, 512)` (`CarrierAddressSyncRun.cs:140`) | Thêm giới hạn độ dài trong `CatalogSyncRun.Ghi` (ném `0002`/`ArgumentException` trước khi chạm DB, khuôn `CatChuoi` của ADDR-002) **và** validate `ImportDatasetInput.SourceName ≤ 512` **trước** khoá ⇒ trả `400` thay vì 500, đồng thời lượt sai không để lại lô rác. |
| 3 | P3 | `modules/Address/eCommerce.Address.Application.Contracts/Events/AddressEvents.cs:75-79` | Khối doc-comment bị cắt dời: 3 dòng `///` (consumer của `AddressChangedEto`/`CustomerDefaultAddressChangedEto`) nằm **sau** `AddressCacheInvalidatedEto`, gắn nhầm vào `CustomerDefaultAddressChangedEto` (không có `<summary>` mở, mất mô tả gốc). | `Read` dòng 45-81 (thứ tự `AddressChangedEto` → `AddressCacheInvalidatedEto` → khối `///` mồ côi → `CustomerDefaultAddressChangedEto`) | Chuyển `AddressCacheInvalidatedEto` xuống **sau** `CustomerDefaultAddressChangedEto` (hoặc khôi phục khối `///` về đúng chỗ). Không đổi runtime. |
| 4 | P3 | `shared/eCommerce.Kernel/Observability/eCommerceMetrics.cs:38` | Dòng chú thích mục bị ghép với thẻ doc: `// ---- … ----    /// <summary>` ⇒ `<summary>` nằm trong comment `//`; doc của `SalesOrderTransitionTotal` mất thẻ mở (XML doc không hợp lệ nếu bật `GenerateDocumentationFile`). | `Read` 32-42 | Khôi phục xuống dòng trước `/// <summary>`. |
| 5 | P3 | `modules/Address/eCommerce.Address.Application.Contracts/Events/AddressEvents.cs:59` | `AddressCacheInvalidatedEto` **không** gắn `INotificationEvent`. Theo E1 (`abp-module-rules`): mất sự kiện chỉ trễ xoá cache (TTL 24 h chặn) ⇒ là **thông báo**, nên gắn marker; hiện không gắn nên cổng `EventCoverageChecker` coi là sự kiện mang dữ liệu ⇒ host **không start** nếu handler bị bỏ (chặt hơn cần thiết) và lệch E1. Hôm nay handler có thật nên cổng xanh. | `arch` §3.5 ghi rõ lựa chọn outbox; `grep INotificationEvent` cho thấy `OrderStatusChangedEto`/`ProductPublishedEto` cùng bản chất đều gắn | Cân nhắc gắn `INotificationEvent` (vẫn giữ `useOutbox: true`), hoặc ghi `docDeviation` + lý do trong arch. Không chặn merge. |
| 6 | P3 | `docs/tests/ADDR-001-testplan.md:59` và `:116` | Testplan ghi integration **105** nhưng số đo thật hôm nay **106/106** (ca `TC19` của pass ADDR-002 đã thêm). | `dotnet test …IntegrationTests --no-build` → 106; testplan ADDR-002 §2 đã ghi 106 | Cập nhật §2/§5 = 106 (hoặc ghi rõ mốc thời gian đo). |
| 7 | P2 | toàn diff | **Phạm vi**: code phi-migration **3.416 dòng / 37 tệp** (chưa kể test 3.952/21, docs 2.495/19) > K-04 ~600. Đây là **chuỗi WP-18** (`D-216`) gộp các phiên N76–N80 trên một nhánh, không phải một phiên; **0** tệp ngoài vùng cấm (không FE/`/api/v1`/adapter/`eCommerce.slnx`/`Directory.Packages.props`/`pnpm-lock.yaml`). | `git diff --numstat`; `git diff --name-only \| grep -E "apps/\|api/v1\|Directory.Packages\|eCommerce.slnx\|pnpm-lock"` = rỗng | Chấp nhận theo `D-216`; khi mở PR phải ghi rõ là chuỗi WP-18 và chia mô tả theo `W18-1`…`W18-6` để người merge đọc được từng phần. |

## Truy vấn đồ thị

- `graph: mcp__codegraph__codegraph_explore "CatalogSyncRun Ghi SourceName ImportDatasetAsync GhiLoThatBaiAsync AdministrativeUnitSynchronizer ApDung CarrierAddressSyncWorker TienToKhoa"` → **56 ký hiệu / 4 tệp**; xác nhận flow `ImportDatasetAsync:267 → GhiLoThatBaiAsync:503 → Ghi:83`, overload `ApDung:314-315`, `TienToKhoa:52`. Dùng để kết luận P2 #1/#2 (đường gọi thật + nơi thiếu kiểm độ dài), không dựa vào suy đoán.

## Kết luận

**P1 = 0** · P2 = 3 · P3 = 4. Không có phát hiện chặn merge vào nhánh tích hợp. P2 #1 là nợ có chủ (`D-250`, `be`) **phải** đóng trước PR `tich-hop/gd3 → main`; P2 #2/#7 nên xử lý trong WP-18 hoặc mở nợ có chủ rõ ràng.

VERDICT: PASS
