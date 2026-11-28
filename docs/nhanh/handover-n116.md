# Handover N116 — WP-17 / hàng `W17-2` (F3 BE) XONG: đường ghi + duyệt + động cơ giá vốn `PRC-001`, migration expand-only đã áp

> Phiên `N116` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-2` (loại `code`, bước `F3 BE`, vai `be` Opus mức **L1**) ·
> WP-17 "giá vốn" · mã `PRC-001` · nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: chốt `AQ3/AQ4/AQ5` (`D-321/322/323`) + `docDeviation` (`D-324`); code BE đầy đủ + migration expand-only + `pg_dump`;
> sửa SRS/ARCH khớp quyết định; `docrev` `DOC-OK`. **0 tệp FE · 0 merge `main`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Chọn hàng + nhánh | ORCH | `PH next` ⇒ `W17-2`; `PH check` **OK**; nhánh đúng, cây sạch; `PH start` ⇒ **`PHIEN=N116`** |
| Chốt câu mở | `decider` | `D-321` (AQ3 — ánh xạ loại chuyển động + tiền tệ VND), `D-322` (AQ4 — thiếu `UnitPrice` ⇒ `null`+`CostMissing`), `D-323` (AQ5 — job quét + command, không ETO) |
| Sửa SRS/ARCH | `ba` · `arch` | `prc-001` §15.1 Q9–Q11 `✅ Closed`, §20.4 bảng ánh xạ + port trần `ICostPriceLookup`, §15.4 v0.8; `inv-001` §0 nghĩa `UnitPrice`; `PRC-001-arch.md` §8.2 AQ3/4/5 `Closed` + §3.3/§3.4 nhãn |
| F3 BE | `be` (Opus) | Domain: `CostPriceSource`, 4 cột nullable `CostPrice`, entity `CostEngineRun`, hàm thuần `TinhBinhQuanGiaQuyenDiDong`, 4 `SettingDefinition`, `PricingLockKeys.GiaVonTheoSku`; Contracts: `ICostPriceLookup` (port trần), `ICostPriceAdminAppService`, `ICostEngineAppService`, `ICostFeedLookup`; Application: `CostPriceAdminAppService` (2 bước, `.Manage`/`.Approve`), `CostPriceLookupAdapter`, `CostEngineService` (P0/P1/P2/P3 UoW `requiresNew` — `D-324` ④), `CostEngineWorker`, `CostFeedLookupAdapter` (ánh xạ `D-321`, fail-closed `0014`); lỗi `0012/0013` (chỗ giữ chỗ PRC-002) + `0014/0015` |
| Migration | `be` | `20261128052137_Added_Pricing_CostEngine`: `CREATE TABLE PrcCostEngineRuns` + 4 `ADD COLUMN` nullable `PrcCostPrices` + 3 `CREATE INDEX`; **sửa tay** 2 unique index `NULLS NOT DISTINCT`; **0 DROP/SET NOT NULL** trên bảng cũ; áp bằng `dotnet run --project src/eCommerce.DbMigrator` |
| `docDeviation` | `decider` | `D-324` `TẠM CHỐT`: giữ `0012/0013` đúng nghĩa PRC-002 (test dải liên tục); chấp nhận `using eCommerce.Inventory;` (bất biến = 0 **tên entity**); v1 = VND literal; **sửa ngay** sổ run 3 pha UoW |
| Sửa SRS theo DOCREV | `ba` (×2) | §12 thêm 4 mã lỗi; đóng 3 P2 (Q9 đủ 4 nhóm; ngoại lệ nhóm Transfer khi khớp `DoiSoatTonKho.Dau()`; ghi chú ⚠️ W17-2 cho as-built cũ) + 3 P3 |
| DOCREV SRS | `docrev` | vòng 1 `DOC-CHANGES` **P1=0/P2=3** → `ba` sửa → vòng 2 **`DOC-OK` P1=0/P2=0/P3=7** |

**Baseline mở phiên (code WP, `08` §4):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 Error · 9 Warning** (cũ) |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2650/2650** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS · 1049 link · 1994 backtick |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `docker compose ps` | 0 | postgres + redis healthy (5433/6380) |

**Cổng G3 — ORCH tự chạy (không tin số vai báo), lần cuối trên cây hoàn chỉnh:**

| Cổng | Lệnh | Số đo |
|---|---|---|
| Build + test + FE + drift | `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0 · 10/10 (352,9 s)** — build **0 Error/9 Warning** · unit **2650/2650** · integration **162/162** · DB bất biến **25/25** · test cổng **60/60** · `schema.d.ts` drift 0 · mermaid **214/0** |
| Cổng tài liệu | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` | **XANH exit 0** |
| Link | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** · 1049 link · **2005** backtick · 0 chết |
| DOCREV SRS | `check-agent-artifact -Kind docreview -Path docs/reviews/PRC-001-docreview.md` | **exit 0 · DOC-OK · P1=0 · P2=0 · P3=7** |
| Quyết định | `check-agent-artifact -Kind decision -Path docs/nhanh/decisions.md` | **exit 0 · DAT** (114 dòng) |
| DB thật | `\d "PrcCostPrices"` · `pg_indexes` · `__EFMigrationsHistory` | 4 cột nullable · 2 index **NULLS NOT DISTINCT** · migration đã áp |
| Kích thước | `git show --stat` | 32 tệp · **2060 dòng** ngoài migration · **12786** dòng migration (Designer + snapshot) — vượt mốc ~600 vì là feature module mới (domain+contracts+adapter+service+setting+lỗi+migration); chain `D-216` cho phép |

## 2. Chưa xong / cắt giữa

Không có việc dở của `W17-2`. Các mục dưới là **cắt CÓ QUYẾT ĐỊNH** (có `D-xx`/nhận chủ):

- **Test K-06 (F3 "code + test đơn vị")**: `be` **không** ghi được `test/**` — `.claude/scopes.json` giới hạn `be` ở `modules/**`/`src/**`/`shared/**`/`docs/features/**/*.md`. Đã giao **`W17-4` (qa)**: dựng `PRC-001-testplan.md` 14 ca K-06 + negative control (điều kiện cứng `D-320` (vi)); đặc biệt **K06-08b** đo trên PostgreSQL thật (P1 commit bền khi P2 ném, retry `Attempt++`), **K06-08**, test kiến trúc quét 4 **tên entity** Inventory trong `modules/Pricing/**`, ca `TienTeNhap ≠ VND ⇒ 0014`. **Cấm bỏ/nới** ca.
- **OpenAPI snapshot + `schema.d.ts`**: endpoint `cost-price-admin` + `cost-engine` tự lộ qua ConventionalControllers nhưng phiên này **không** sinh lại `openapi.v1.json`/`schema.d.ts` (ràng buộc phiên; cổng nhẹ vẫn xanh). Giao **`W17-3`/`W17-5`** (`-DayDu`/drift).
- **`schema.sql` tích hợp**: cố ý không sinh lại (`PostgreSqlSchemaTests` ghim tổng NND=114) — **`W17-4`** cập nhật tổng + thêm 2 tên index khi làm test tích hợp.
- **ARCH §2.1 câu chữ** "CẤM `using eCommerce.Inventory;`" (mâu thuẫn nội tại với §3.3 tự khai port ở namespace đó) — `D-324` ② chốt **giữ nguyên** `using`, bất biến thật = 0 tên entity; sửa **câu chữ** ARCH là **hàng riêng**, không chặn.
- **P3=7 của DOCREV SRS** — dọn dần; gồm nợ cũ (`BR-PRC-001-008` thiếu test; thiếu "Đối chiếu mô hình Nhanh"; lịch sử version header; `Q8` thiếu ánh xạ `UnitCost`↔`UnitPrice`).
- **Trạng thái `Loi` bền vững**: đã sửa (`D-324` ④, 3 pha UoW); process chết giữa P1–P3 ⇒ run kẹt `DangChay` ⇒ 409 tới khi reset — **nợ W17-4/W17-5** (không thêm lệnh reset trong `W17-2`, bề mặt mới cần `D-xx`).

## 3. Migration đã áp

- **`20261128052137_Added_Pricing_CostEngine`** — DB dev `ecommerce` (container `ecommerce-postgres`, cổng 5433).
- `pg_dump` **trước** khi áp (K-01): `artifacts/pg-dump/N116-W17-2.dump` — **634 674 bytes** (chưa commit; `artifacts/` gitignored).
- Nội dung Up: 1 `CREATE TABLE` `PrcCostEngineRuns` + 4 `ADD COLUMN` nullable (`NguonTinh`, `RunId`, `ComputedAtUtc`, `IdempotencyKey`) + 3 `CREATE INDEX` (2 unique sửa tay `NULLS NOT DISTINCT`, 1 filtered); **0 DROP/ALTER TYPE/SET NOT NULL** trên bảng cũ. `Down`: drop 4 cột + index + bảng.
- Đã kiểm DB thật: `__EFMigrationsHistory`, `\d "PrcCostPrices"`, `pg_indexes`.
- **Đường lùi:** `git revert <SHA N116>` + `dotnet ef migrations remove` (nếu chưa áp) **hoặc** chạy `Down()` sau `pg_dump`; hoặc để nguyên + tắt `Pricing:GiaVonDongCo=false` (0 dòng mới, dữ liệu cũ nguyên vẹn).

## 4. Quyết định mới

- **`D-321`** (AQ3) — ánh xạ loại chuyển động định giá: `NhapCoGia` chỉ `Receipt`/`GoodsReceipt` **có** `UnitPrice`; `NhapKhongGia` = 5 loại + Receipt không giá; `Xuat` = 8 loại; BỎ = 5 loại + cả cụm `Transfer*`; lạ/lệch dấu ⇒ NÉM `0014`; tiền tệ = VND tenant (`TienTeNhap=null`). `TẠM CHỐT (DEC)`.
- **`D-322`** (AQ4) — thiếu `UnitPrice` ⇒ `avg=null`+`CostMissing`, không INSERT kỳ, cấm `0`, v1 không phục hồi. `TẠM CHỐT (DEC)`.
- **`D-323`** (AQ5) — job quét + command on-demand + `BackgroundWorker`; không ETO mỗi `GhiSo`; event additive sau `XC-002`. `TẠM CHỐT (DEC)`.
- **`D-324`** — 4 `docDeviation`: giữ `0012/0013` (nghĩa PRC-002, dải liên tục); chấp nhận `using eCommerce.Inventory;` (bất biến = 0 tên entity); v1 = VND literal, đa tiền tệ = `D-xx` mới; **sửa ngay** sổ run 3 pha UoW `requiresNew`. `TẠM CHỐT (DEC)`.
- **Không** đóng `Q4`/`PRC-001-AQ1` (câu kế toán) và `PRC-001-AQ2` (câu người).
- **Không có quyết định của người** trong phiên. `D-318`/`D-319`/`D-320` giữ nguyên hiệu lực.

## 5. Nợ / rủi ro

- 🟠 **`Q4`/`PRC-001-AQ1`** (`CostPrice` duyệt 2 bước) — câu **kế toán trưởng** (M4/R3-3), vẫn `Open`; **chặn release**.
- 🟠 **`PRC-001-AQ2`** — nhập lùi đụng kỳ đã duyệt: revision hay từ chối; câu **người** (đổi unique = phá huỷ schema), vẫn `Open`.
- 🟠 **`W17-2` chưa có test nguồn** (hook cấm `be` ghi `test/**`) ⇒ **`W17-4` bắt buộc** dựng 14 ca K-06 + negative control; nếu `W17-4` không xong, động cơ **chưa có lưới đỏ** nào ngoài unit mã lỗi.
- 🟠 **OpenAPI drift**: endpoint mới chưa có trong `openapi.v1.json`/`schema.d.ts`; **`-DayDu`/drift sẽ đỏ tới khi `W17-3`/`W17-5` sinh lại**. Cổng nhẹ (G3) vẫn xanh.
- 🟡 **Đa tiền tệ / tiền tệ cơ sở tenant** — repo chưa có setting; v1 = VND (`D-324` ③); mở ngoại tệ = `D-xx` mới (chạm `XC-001`) + kế toán.
- 🟡 **P3=7** của DOCREV SRS + nợ cũ `check-feature-doc` (`BR-PRC-001-008` thiếu test; "Đối chiếu Nhanh") — dọn dần; **không** chặn.
- 🟡 **`D-186` ② câu chữ ARCH §2.1** — đã khớp thực chất qua `D-324`; sửa câu chữ = hàng riêng.
- 🟡 Nợ kế thừa (không thuộc phiên): `check-feature-doc` của `prc-001`/`inv-001` (nợ WP-14); P2 retry storm WP-23.

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp | Trả lời bằng cách nào |
|---|---|---|---|---|
| 1 | `Q4`/`PRC-001-AQ1`: `CostPrice` bắt buộc luồng duyệt 2 bước? | Giữ **(a) 2 bước** cho GĐ3 (đã code 2 bước; (b) chỉ đổi 1 chỗ `CreateAsync`, 0 schema) | Đã code (a); `Q4` giữ `Open`, không chặn `W17-3/4` | Kế toán duyệt lô M4/R3-3 trước release — dán sau `/chay-phien-code chuoi=gd3-1` |
| 2 | `PRC-001-AQ2`: nhập lùi đụng kỳ **đã duyệt** có cần revision? | Giữ **(a) từ chối** (`NÉM 0015`) cho GĐ3; (b) revision = đổi unique = phá huỷ schema | Đã hiện thực (a); (b) không làm | Người quyết nếu muốn (b) — dán sau lệnh phiên kế |
| 3 | Đa tiền tệ / tiền tệ cơ sở tenant cho giá vốn | **(a) v1 chỉ VND** (`D-324` ③); mở ngoại tệ = `D-xx` mới (chạm `XC-001`) | v1 VND; `TienTeNhap ≠ VND ⇒ 0014` | Kế toán + kiến trúc quyết khi mở ngoại tệ |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-2` → mở `W17-3` (F3 FE: màn duyệt giá vốn theo SRS; không có ⇒ `done` kèm lý do).**

- `W17-3`: FE cho đường ghi/duyệt giá vốn theo SRS (`PRC-001` §20) — **hoặc** xác nhận WP không có phần FE (ARCH §1.1 ghi "Không có màn hình mới trong WP-17"; màn vận hành động cơ là `WEB-001`) ⇒ `done` kèm lý do. Nếu có phát sinh: **sinh lại OpenAPI snapshot + `schema.d.ts`** (nợ đã ghi) và chạy `pnpm gen:api` không drift.
- `W17-4`: dựng `PRC-001-testplan.md` (14 ca K-06 + negative control; K06-08b trên PostgreSQL thật; test kiến trúc tên entity; ca `0014` tiền tệ) ⇒ `check-agent-artifact -Kind testplan` exit 0 (điều kiện cứng `D-320` (vi)).
- `W17-5`: `ops` + G6 `-DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; **M4 kế toán trước release**.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ **`5c902ed`**.
- Commit phiên: **`54ab64b`** — `feat(pricing): N116 - W17-2 F3 BE gia von PRC-001 duong ghi duyet dong co migration` (32 tệp).
- **Sẽ push** `origin/feat/gd3-wp17-gia-von` sau commit handover. **Chưa** đẩy `tich-hop/gd3` (chỉ `dong-wp` mới FF), **không** force/merge, **không** mở PR, **không** đụng `main`.

---

**Hàng rào agent** (phiên `N116`): `decider` ×2 (41 + 40 lượt) · `ba` ×3 (48 + 13 + 43) · `arch` ×1 (17) · `be` ×2 lượt-chạy (104 lượt + tiếp; **chạm trần 80 lượt** lần đầu nên phải resume, không mất việc) · `docrev` ×2 (56 + kiểm lại). **0 tệp ngoài vùng thật**: 1 cảnh báo `ngoaiVung` cho `be` ở `prc-001-bang-gia...:898` là **dương tính giả** — dòng đó là sửa của `ba` trong cùng cây chưa commit, hook so cả cây nên gán nhầm; phần `be` tự sửa nằm dưới heading "Trạng thái triển khai" (đúng vùng). Các cảnh báo "thiếu khối JSON cuối" là lỗi hook không đọc được transcript (đã biết, `chanDungKhiThieuJson=false`); báo cáo của mọi vai **đều có** khối JSON. ORCH tự đo lại mọi số bằng máy.
