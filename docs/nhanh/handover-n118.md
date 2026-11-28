# Handover N118 — WP-17 / hàng `W17-4` (F4 QA + F5 REV) XONG: testplan K-06, vá 2 lỗi P1 (replay giá vốn + kỳ một ngày)

> Phiên `N118` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-4` (loại `code`, bước `F4-F5`, vai `qa → rev`) ·
> WP-17 "giá vốn" · mã `PRC-001` · nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: dựng testplan + test thật cho **động cơ giá vốn** (14 ca K-06 + kiến trúc + tiền tệ), **negative control**,
> rồi `rev` review → **phát hiện 2 lỗi P1** → `decider` chốt **`D-325`/`D-326`** → `be` sửa → `qa` bổ sung test → **REV vòng 2 PASS**.
> **0 migration mới · 0 tệp FE · 0 merge `main`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Chọn hàng + nhánh | ORCH | `PH next` ⇒ `W17-4`; `PH check` **OK**; nhánh đúng `feat/gd3-wp17-gia-von`, cây sạch; `PH start` ⇒ **`PHIEN=N118`** |
| Điều kiện vào code | ORCH | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` **XANH exit 0** (PRC-001 duyệt GD1) |
| **F4 QA** testplan + test | `qa` | Tạo `docs/tests/PRC-001-testplan.md` phủ **14/14** `BR-PRC-001-001…014`; **14 ca K-06** (K06-01…13 + 08b) đúng ARCH §7; test kiến trúc (**0 tên entity** Inventory trong `modules/Pricing/**`); ca `0014` tiền tệ; `BR-008`; K06-08b trên **PostgreSQL thật**. 6 tệp test mới + `PostgreSqlSchemaTests` **114→116** NND + 2 tên index (`IX_PrcCostEngineRuns_TenantId_RunKey`, `IX_PrcCostPrices_TenantId_IdempotencyKey`) + sinh lại `Assets/schema.sql` (**+67 dòng**) |
| Phát hiện `schema.sql` cũ | `qa` | Nhánh vốn **thiếu** migration `20261127153549_Add_Voucher_Status_Check` (W25/N101) khỏi `schema.sql`; đã bổ sung + khai 2 `CHECK` W25 vào `Data/RangBuocChuaAnhXa.txt` kèm lý do. **Không do WP-17** |
| Negative control (vòng 1) | ORCH | **NC-1/2/3/6 DAT** (bỏ guard `DaDuyet`; bỏ `costMissing`; đảo trạng thái run; sai công thức bình quân) → đỏ→xanh, chèn bằng chứng vào testplan |
| **F5 REV vòng 1** | `rev` | **`VERDICT: CHANGES` · P1=1 · P2=3 · P3=3** — P1: động cơ chỉ nạp `[from,to]`, job quét `[hôm qua,hôm nay]` ⇒ sai đơn giá cho SKU có lịch sử trước `from` |
| Quyết định sửa P1 | `decider` | **`D-325`** (a): replay **toàn lịch sử** từ `DauLichSu = 2000-01-01`; `from` chỉ là mốc dưới của **kỳ được ghi**, không chặn đầu vào tính |
| `be` sửa P1 | `be` | `CostEngineService.XuLyAsync` nạp `LayChuyenDongAsync(DauLichSu, to)` + `DoiSoatAsync` bỏ kỳ `EffectiveFrom < from`; `CostEngineWorker` `tu = DauLichSu`; hằng `DauLichSu` một chỗ. **2 tệp, 0 migration** |
| Phát hiện lỗi kỳ một ngày | `qa` | SKU nhập **2 ngày liền kề** ⇒ kỳ `EffectiveFrom == EffectiveTo`; `CostPrice` ctor/`CapNhatGiaVonChuaDuyet` NÉM `0009` khi `<=` (chặt hơn SRS §5.2 CK `>=` + `CoHieuLucTai` bao gồm) ⇒ run `Loi`. Chuyển `rev`/`be` |
| Quyết định sửa kỳ một ngày | `decider` | **`D-326`** (a): đổi `CostPrice` guard `<=` → `<`; giữ `CoHieuLucTai`/hàm thuần/CK/unique; `PriceList`/`ListItem`/`TierPriceSet` để **nợ riêng** PRC-002 |
| `be` sửa D-326 | `be` | `CostPrice.cs:118` + `:197` `<=`→`<` + thông điệp/chú thích. **1 tệp, 0 migration** |
| `qa` test D-326 | `qa` | Đổi kỳ vọng `EffectiveTo_BANG_EffectiveFrom__NEM_0009` → `..._HOP_LE` (kỳ ngược **vẫn NÉM**); thêm **K06-15** (2 ngày liền kề ghi được, lookup Ngay1=10/Ngay2=15); thêm **K06-14/14b** (cửa sổ giữa lịch sử) + test đẳng thức enum (P2 #4) |
| ARCH đồng bộ | `arch` | `PRC-001-arch.md` §5.2 + §8.1 R7 bỏ mâu thuẫn "giới hạn `[from,to]`" → replay toàn lịch sử (`D-325`); §6 thêm câu kỳ một ngày hợp lệ (`D-326`) |
| Negative control (vòng 2) | ORCH | **NC-7 DAT** (trả `(from,to)` ⇒ K06-14 đỏ) · **NC-8 DAT** (trả `<=` ⇒ K06-15 đỏ) |
| **F5 REV vòng 2** | `rev` | **`VERDICT: PASS` · P1=0 · P2=1 · P3=3** — P1 #1 + lỗi kỳ một ngày đóng thật; P2 còn lại = K06-08b integration mô phỏng SQL tay (không chặn) |

**Baseline mở phiên (code WP, `08` §4):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 Error · 9 Warning** (cũ) |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2650/2650** |
| `dotnet test test/eCommerce.IntegrationTests --no-build` | 0 | **162/162** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS · 1051 link · 2007 backtick |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `docker compose ps` | 0 | postgres + redis running (5433/6380) |

**Cổng đo lại cuối phiên (ORCH tự chạy, không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| G3 build | `dotnet build eCommerce.slnx --no-incremental` | **0 Error / 9 Warning** (0 warning mới) |
| G3 unit | `dotnet test test/eCommerce.Tests --no-build` | **2691/2691** (baseline 2650, **+41**) |
| G3 integration | `dotnet test test/eCommerce.IntegrationTests --no-build` | **171/171** (baseline 162, **+9**) |
| G3 nhẹ | `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0 · 10/10 · 340,3 s** |
| G4 | `check-agent-artifact.ps1 -Kind testplan … -Srs …prc-001…` | **DAT** exit 0 · BR 14/14 |
| G5 | `check-agent-artifact.ps1 -Kind review -Path docs/reviews/PRC-001-review.md` | **DAT** exit 0 · **PASS · P1=0** |
| Negative control | `negative-control.ps1` ×6 | **6/6 DAT** (NC-1/2/3/6/7/8) đỏ→xanh |
| Quyết định | `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | **DAT** (`D-325`, `D-326`) |
| Link | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** · 1051 link · 2019 backtick · 0 chết |

## 2. Chưa xong / cắt giữa

Không có việc dở của `W17-4`. Các mục dưới là **nợ có nhận chủ**, không chặn DoD:

- 🟡 **P2 #2 (REV)**: hai ca integration `K06-08b` "bền vững qua rollback" / "Attempt++" (`PostgreSqlPricingCostEngineTests.cs:211-262,266-299`) **mô phỏng SQL tay**, không gọi `CostEngineService` ⇒ không canh hồi quy thứ tự 3 pha UoW (`D-324`). Nhánh trạng thái đã có ca Application thật. Chủ: `qa` khi mở lại `PRC-001`/`K-06`.
- 🟡 **P3 #5**: `CostEngineService` bỏ `hopLe` của `DocChinhSachLamTron(out _)` (cấu hình làm tròn sai rơi mặc định im lặng — `D-221`). **P3 #6**: `DateTime.UtcNow` thay `IClock`. **P3 #7**: SRS §20.4 :1271-1272 lệch mã về kiểm dấu nhóm Transfer vs `CostFeedLookupAdapter.cs:125-128` (tác động hiện tại = 0). Chủ: `be`/`ba` khi rảnh.
- 🟡 **`PriceList`/`PriceListItem`/`TierPriceSet`** vẫn chặn kỳ một ngày (cùng mẫu `<=`, khác `CostPrice`); ghi nợ cho WP chạm `PRC-002`/bảng giá (`D-326` ⑥).
- 🟡 **`schema.sql` cũ do W25** đã được sửa trong phiên (bổ sung `Add_Voucher_Status_Check` + 2 CHECK vào `RangBuocChuaAnhXa.txt`) — ghi nhận để review lịch sử, không phải thay đổi hành vi.
- 🟠 **`Q4`/`PRC-001-AQ1`** (CostPrice duyệt 2 bước) vẫn `Open` — câu kế toán trưởng; **chặn release**. **`PRC-001-AQ2`** (nhập lùi đụng kỳ đã duyệt: từ chối vs revision) vẫn `Open`.
- 🟡 **`WEB-001`**: nếu cần 2 endpoint giá vốn qua `/api/v1` thì phải thêm controller v1 (BE), tách WP.

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp | Trả lời bằng cách nào |
|---|---|---|---|---|
| 1 | `Q4`/`PRC-001-AQ1`: `CostPrice` bắt buộc luồng duyệt 2 bước? | Giữ **(a) 2 bước** cho GĐ3 (đã code + test) | `Q4` giữ `Open`, không chặn `W17-5` | Kế toán duyệt lô M4/R3-3 trước release — dán sau `/chay-phien-code chuoi=gd3-1` |
| 2 | `PRC-001-AQ2`: nhập lùi đụng kỳ **đã duyệt** có cần revision? | Giữ **(a) từ chối** (`NÉM 0015`); (b) revision = đổi unique = phá huỷ schema | Đã hiện thực (a) | Người quyết nếu muốn (b) |
| 3 | Đa tiền tệ / tiền tệ cơ sở tenant cho giá vốn | **(a) v1 chỉ VND** (`D-324` ③) | v1 VND; `TienTeNhap ≠ VND ⇒ 0014` | Kế toán + kiến trúc khi mở ngoại tệ |
| 4 | `D-325`/`D-326` (TẠM CHỐT DEC) — xác nhận "replay toàn lịch sử" + "kỳ một ngày hợp lệ" | Chấp nhận (đã có test + NC) | Đã code + test + ARCH đồng bộ | Duyệt cùng lô M4/R3-3 hoặc phản hồi nếu muốn khác |

## 3. Migration đã áp

**Không có migration mới trong phiên.** Migration `20261128052137_Added_Pricing_CostEngine` của `W17-2` đã áp ở `N116` (sau `pg_dump` 634674 bytes). Phiên này **0 `pg_dump`, 0 thay đổi schema**; `schema.sql` chỉ **sinh lại** để khớp DB (bao gồm 2 migration đã có: W17-2 + W25).

## 4. Quyết định mới

- **`D-325`** (TẠM CHỐT DEC, uỷ quyền `D-318` C4): động cơ giá vốn **replay toàn lịch sử** từ `DauLichSu = 2000-01-01`; `from` = mốc dưới của kỳ **được ghi/đối soát**, KHÔNG chặn đầu vào tính. Lùi: `git revert` (0 migration) / tắt `Pricing:GiaVonDongCo=false`.
- **`D-326`** (TẠM CHỐT DEC): **kỳ một ngày** (`EffectiveFrom == EffectiveTo`) **hợp lệ** — nới `CostPrice` `<=`→`<` (khớp SRS §5.2 CK `>=` + `CoHieuLucTai`); sửa kỳ vọng test cũ (không nới test); `PriceList`/`ListItem`/`TierPriceSet` để nợ riêng. Lùi: `git revert`.

Cả hai đã ghi `docs/nhanh/decisions.md` + hàng đợi xem lại; `check-agent-artifact -Kind decision` **DAT**.

## 5. Nợ / rủi ro

- 🔴 **Trước release (M4/R3-3)**: kế toán duyệt lô `PRC-001` (gồm `Q4`/`AQ1`, đa tiền tệ) + xác nhận `D-325`/`D-326`.
- 🟠 **Engine mặc định TẮT** (`Pricing:GiaVonDongCo=false`) — bật trên dữ liệu thật là **việc người** (`D-318` C6); chưa backfill.
- 🟡 P2 #2 + 3 P3 như §2.
- 🟡 **`W17-5` (dong-wp)** phải chạy `kiem-tat-ca.ps1 -DayDu` + tài liệu deploy `PRC-001` trong `docs/ops/` (có đường lùi, tạo ở `W17-5`) + FF `tich-hop/gd3`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-4` → mở `W17-5` (F6 OPS + G6 `-DayDu`; đẩy FF `tich-hop/gd3`).**

- `W17-5`: `ops` + G6 — `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` (không chạy build/test song song — N69); tài liệu deploy `PRC-001` trong `docs/ops/` (có đường lùi); `07` §13 một dòng. Xanh ⇒ commit + push nhánh WP rồi **FF** `feat/gd3-wp17-gia-von` → `tich-hop/gd3`.
- `M4` kế toán trước release; `Q4`/`AQ2` vẫn `Open`.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ `5c902ed`.
- Commit việc phiên này (sau `ab08ddf`):
  - `f2e3337` — `test(pricing): N118 - W17-4 F4 QA testplan K06 and cost engine tests`
  - `84d4aa9` — `test(pricing): N118 - W17-4 negative control evidence NC-1 2 3 6`
  - `ac4af17` — `fix(pricing): N118 - W17-4 fix P1 full-history replay and one-day period`
  - `e56dee2` — `test(pricing): N118 - W17-4 negative control NC-7 NC-8 evidence`
- **Sẽ push** `origin/feat/gd3-wp17-gia-von` sau commit handover. **Chưa** đẩy `tich-hop/gd3` (chỉ `W17-5`/`dong-wp` mới FF), **không** force/merge, **không** mở PR, **không** đụng `main`.

---

**Hàng rào agent** (phiên `N118`): `qa` ×3 · `rev` ×2 (vòng 1 CHANGES → vòng 2 PASS) · `be` ×2 · `decider` ×2 · `arch` ×2 · ORCH tự chạy toàn bộ cổng máy. **0 tệp ghi ngoài vùng thật** (`git status` khớp vùng từng vai). 3 cảnh báo hook là **dương tính giả**: (1)(2) `arch`/`qa` chạy song song nên mỗi vai thấy tệp của vai kia trong `git status`; (3) `be` thiếu khoá `testExit` (đã có `expectedFailedTests`); `arch` "thiếu khối JSON" là dương tính giả đã biết (`chanDungKhiThieuJson=false`). Không `git restore` tệp nào (tất cả thuộc đúng vùng của vai đã ghi).
