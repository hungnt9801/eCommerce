# Handover N104 — WP-25 / hàng `W25-5` (F6 OPS + G6) XONG — **WP-25 ĐÓNG, `tich-hop/gd3` đã nhận FF**

> Phiên `N104` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W25-5` · WP-25 · mã `PRO-001` · module `Promotions` + `Sales` ·
> nhánh `feat/gd3-wp25-voucher` (base `origin/tich-hop/gd3` @ `e7aa32c`) · 28/11/2026.
> Phạm vi: **F6 OPS** (tài liệu triển khai + đường lùi) → **G6 `-DayDu`** → **FF `tich-hop/gd3`**. Không đổi code sản phẩm.

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Baseline mở phiên | ORCH | build **0 error · 9 warning** (64,35 s) · unit **2619/2619** · `kiem-link` PASS **998 link · 1851 backtick** · mermaid **214 khối/0 lỗi** · hooks **49/49** · FE typecheck **0 lỗi** / Vitest **60/60** · `docker compose ps`: postgres + redis **healthy**. |
| F6 — tài liệu triển khai | `ops` | Tệp mới `docs/ops/PRO-001-deploy.md` (**326 dòng**, 7 mục theo khuôn `PRO-002-deploy.md`): §1 phạm vi (64 tệp, +20.575/−150; **1 migration 2 `CHECK` chưa áp**; **7 route `/api/v1/voucher/**`**; 3 permission; FE **0 dòng** `D-307`; 0 đổi hạ tầng) + §1.1 vùng→thay đổi + §1.2 hành vi mới; §2 **0 env** + 9 ABP Setting (thêm `Promotions:MaxBatchSize` 10000, kẹp 1…100000, `0108`); §3 thứ tự (merge → `pg_dump` → build mới → `DbMigrator` → kiểm `__EFMigrationsHistory` + 2 `CHECK` bằng `pg_constraint` → gán 3 permission → job); §4 **bảng đường lùi bắt buộc**; §5 số đo thật + §5.1 PR/CI + **§5.2 12 bước kiểm sau triển khai** + §5.3 cổng OPS; §6 ai gọi; §7 nợ/rủi ro. |
| F6 — đối chiếu code | `ops` | Sửa **3 chỗ brief ORCH đưa sai** theo code thật: (a) `VoucherExpiryWorker` **ĐỌC** cờ `Promotions:Enabled` (`:54-58`) ⇒ tắt cờ **không nhả** lượt `Reserved` (khác `PromotionExpiryWorker` của `PRO-002`); (b) Host map **17 mã `01xx`** (`0101`–`0108`,`0110`–`0112`,`0114`–`0119`) không phải 18; (c) `openapi.v1.json` **+731/−4** (4 dòng là khối bị di chuyển, **không** mất path/trường), `schema.d.ts` +417/0. |
| G6 — `-DayDu` (lượt 2, detached) | ORCH | **exit 0 · 20/20 bước · 723,3 s** — xem bảng §1 dưới. |
| `07` §13 | ORCH | Một dòng chốt WP-25 kèm số đo G6 + link deploy doc/handover. |

**G6 `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` — exit 0, 20/20 bước, 723,3 s:**

| Bước | Số đo |
|---|---|
| Build + kiểm mốc nhị phân (134 tệp) | **0 error · 9 warning** · 67,8 s · "moi moc deu doi" |
| Unit test | **2619/2619** · 21 s |
| Integration test | **162/162** · 29,6 s |
| Mermaid | **214 khối · 0 lỗi** · 9 s |
| Cổng tài liệu WP đang code | **DAT** · 0,7 s |
| FE typecheck + Vitest | **0 lỗi · 60/60** · 9,6 s |
| `gen:api` drift | **0** · 2,3 s |
| Bất biến DB | **25/25 DAT · 0 HONG** · 8,9 s |
| Test cổng kiểm chứng | **60/60 DAT** · 146,2 s |
| E2E có phiên (HTML thật) | **ĐẠT** · 107,9 s |
| Địa chỉ / Khách / Thanh toán / Đơn sàn (HTTP thật) | ĐẠT · 22,2 / 26,6 / 22,4 / 44,5 s |
| Oversell hai request đồng thời | **11 DAT / 0 HONG** · 24,5 s |
| Outbox (chết sau commit → phát lại) | **9/0** · 32,8 s |
| Outbox 2 instance | **9/0** · 40,8 s |
| Giữ chỗ 2 instance | **17/0** (1 ca **không tính DAT**: 0 lần 409, `D-215`) · 35,7 s |
| Web Admin E2E Playwright | **8/8** · 71,2 s |

**Sự cố môi trường (đã dọn, không phải lỗi sản phẩm):** lượt G6 **đầu** chạy trực tiếp trong Bash bị **tool cap 10 phút kill ở bước 17** (`exit 143`) ⇒ để lại **2 dòng rác probe** + cờ bảo mật admin bị tắt:
- `OrdOrders` `K9OB-001` (`Id` `e0000000-0000-0000-0000-0000000000fb`) — seed của `probe-outbox.ps1` (bước 17).
- `AbpEventOutbox` `eCommerce.Orders.OrderStatusChangedEto` (`Id` `3a25309d-0ce5-3cfc-ad4f-c083e5e708ea`).
- `AbpUsers` `ADMIN.ShouldChangePasswordOnNextLogin = false`.

Đã: `pg_dump` **trước khi xoá** ⇒ `artifacts/pg-dump/N104-W25-5-residue.dump` (**585.744 byte**); xoá đúng **2 dòng theo ID chữ** (đúng câu `DonDep` của `probe-outbox.ps1`: `delete from "OrdOrders" where "Id"='e0000000-…fb'` + `delete from "AbpEventOutbox" where "EventName" like '%OrderStatusChanged%'`); trả cờ bằng `Dat-CoBuocDoiMatKhau -Bat $true` (xác nhận `ADMIN=true`); `kiem-db.ps1` ⇒ **25/25 · 0 HONG**. Lượt G6 thứ hai chạy **detached** qua `artifacts/agents/tu-dong/chay-g6-n104.ps1` (ghi `exit` ra `g6-n104.exit`) để không bị cap 10 phút.

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W25-5`** — mọi DoD của hàng đã đạt (G6 xanh 20/20, deploy doc có đường lùi, `07` §13 một dòng, FF `tich-hop/gd3` thành công). Các mục dưới là **nợ có chủ** đã giao từ trước, không phải phần còn lại của hàng này:

1. **`D-311` P2 #2** — nhánh chốt **đầu tiên** của `VoucherRedemptionService.ThucHienCommitAsync:439-528` **chưa có test nào thực thi**. Chủ **`qa`** (+`be` nếu cần seam), mốc **trước `WEB-001`**.
2. **`D-311` P2 #3** — `DonHangAppService.cs:118-132` guard `BR-06` **một chiều** (đơn có giảm giá mà thiếu `VoucherRedemptionId` không bị chặn). Chủ **`be` (Sales)**, mốc **trước `WEB-001`**.
3. **P2 #4 (pre-existing `PRO-002`)** — 10 chỗ `autoSave: true`, trái `BR-13`/`NFR-09`; QA khoá bằng allowlist. Chủ **`be`/`PRO-002`**.
4. **P3 #9** — ca chống treo `D-309` chỉ chặn ở **mức test**; gỡ guard vẫn treo tiến trình. Chủ **`qa`/`be`**, trước `WEB-001`.
5. **Màn voucher** — WP `WEB-001` (`D-247`/`D-307`), **chưa có lịch**; hợp đồng API đã sẵn (`D-308`).
6. **P3 #6/#7/#8** — comment `csproj` lỗi thời; worker nạp toàn bộ `Reserved` vào RAM; `schema.sql` thiếu 2 `CHECK` (do migration chưa áp). Không chặn.

## 3. Migration đã áp

**Không có migration mới trong phiên này; không áp migration nào.**

- `20261127153549_Add_Voucher_Status_Check` (EF sinh ở `W25-2`, **chỉ 2 `AddCheckConstraint`**) vẫn **CHƯA ÁP** DB dev/production — auto mode chặn; **người** chạy `dotnet run --project src/eCommerce.DbMigrator` ngoài auto mode sau `pg_dump`.
- `pg_dump` liên quan phiên này: `artifacts/pg-dump/N104-W25-5-residue.dump` — **585.744 byte** (chụp **trước** khi dọn 2 dòng rác probe; không dùng để áp migration).
- Dump cũ vẫn dùng được: `artifacts/pg-dump/N101-W25-2.dump` — **576.910 byte**.
- Lùi schema: 2 `DropCheckConstraint` (`CK_ProVoucherCodes_Status`, `CK_ProPromotionRedemptions_Status`); **0** cột/index/dữ liệu.

## 4. Quyết định mới

**Không có `D-xx` mới trong phiên `N104`.** `W25-5` là hàng F6/G6 thuần vận hành, không phát sinh câu hỏi mở; không cần `decider`.
Các dòng còn hiệu lực của WP-25: `D-295`…`D-311` (TẠM CHỐT DEC ở `N99`–`N103`), trong đó `D-309`/`D-310`/`D-311` thuộc lô **chờ người duyệt**.

## 5. Nợ / rủi ro

- 🔴 **Migration `Add_Voucher_Status_Check` CHƯA ÁP** — chủ **người**; chạy `DbMigrator` ngoài auto mode, kiểm `__EFMigrationsHistory` + `pg_constraint`, rồi tái sinh `schema.sql` (đang thiếu 2 `CHECK`).
- 🟠 **P2 #2/#3 (`D-311`)** — chỗ chạm tiền/lượt; **cổng cứng trước `WEB-001`**. Xem §2 #1/#2.
- 🟡 **`VoucherExpiryWorker` đọc cờ `Promotions:Enabled`** — tắt cờ lâu ⇒ lượt `Reserved` **không** được nhả (consumer huỷ/trả đơn `SAL-004`/`SAL-006` chưa có). Đã ghi vào deploy doc §1.2 #4, §4, §6.
- 🟡 **P2 #4 `autoSave: true`** (10 chỗ, `PRO-002`) — pre-existing.
- 🟡 **P3 #9**, **P3 #6/#7/#8** — xem §2 #4/#6.
- 🟡 **Nợ `PRO-002`**: `promotion_applied_total` còn trong `ChuaCoNoiPhat` (`SAL-001`).
- 🟡 **Môi trường**: `pnpm build` EPERM symlink standalone (`K-05`/`N95`); `pnpm lint` không có script (`N70`); `-DayDu` > 10 phút ⇒ **phải chạy detached** (bài học phiên này).
- **Chờ người** (gom một lần ở câu trả lời cuối): (a) **áp migration** bằng DbMigrator ngoài auto mode + `pg_dump` production trước; (b) duyệt lô `D-277…D-311`; (c) kế toán duyệt lô M4 (`D-143`/`D-148`/`D-149`/`D-152`/`D-158`) trước release; (d) xếp lịch WP `WEB-001` (màn voucher) — `D-247`/`D-307`; (e) merge `main` + PR nháp `tich-hop/gd3 → main` khi chuỗi xong; (f) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-25 → mở WP-23.**

- Hàng kế = **`W23-0`** (loại `doc-gate`, WP-23 `NOT-002`), vai **ORCH + `ba`/`docrev`/`decider`**; nhánh `feat/gd3-wp23-kenh-gui` **cắt từ đỉnh `tich-hop/gd3`** (đã chứa WP-25).
- Đọc trước: `D-208` dòng `NOT-002` (sửa P2 #1 + P3 → DOCREV), `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-23, `docs/nhanh/handover-n104.md` (tệp này), deploy doc `docs/ops/PRO-001-deploy.md` nếu cần bối cảnh chuỗi.
- WP-23 là **thiết kế trung lập**: `Q1`/`Q3` + HMAC production **ngoài** tạm duyệt (R3-6) ⇒ chỉ hợp đồng adapter + `fake` sender + fail-closed `0003`.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-23
và handover gần nhất `docs/nhanh/handover-n104.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-23** (phiên N105). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

Một dòng cho chuỗi: `/chay-phien-code chuoi=gd3-1`

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp25-voucher`; base `origin/tich-hop/gd3` @ `e7aa32c`. HEAD trước phiên `8bcd4b0`.
- Commit phiên: `4427a2d` (`docs(ops)` — `PRO-001-deploy.md`) · `docs(nhanh)` chứa tệp này + `07` §13 + registry (**SHA của chính bản handover này là `git log -1`**).
- **Đã push nhánh WP**; **đã đẩy fast-forward `feat/gd3-wp25-voucher:tich-hop/gd3`** (SHA `tich-hop/gd3` sau push = `git rev-parse tich-hop/gd3`). **Không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

**Hàng rào agent** (phiên `N104`): `ops` ×1 lượt (dừng ở trần **30 turn** khi còn đang đọc nguồn, chưa viết tệp ⇒ `SendMessage` tiếp 1 lượt; lượt 2 sửa 1 typo). `canh-bao.jsonl`/`2026-11-28.jsonl` ghi `ngoaiVung: []`, `thieu: []` cho cả 2 lượt `ops`. ORCH tự chạy mọi cổng. Không có vai khác.
