# Handover N102 — WP-25 / hàng `W25-3` (F3 FE) XONG — **mở `W25-4`**

> Phiên `N102` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W25-3` · WP-25 · mã `PRO-001` · module `Promotions` + `Sales` ·
> nhánh `feat/gd3-wp25-voucher` (base `origin/tich-hop/gd3` @ `e7aa32c`) · 28/11/2026.
> Phạm vi phiên: **F3 FE** theo `D-307` (**0 dòng code màn hình** — màn voucher thuộc WP `WEB-001`) và **`D-308`**
> (`fe` tái sinh hợp đồng OpenAPI `openapi.v1.json` + `schema.d.ts` ngay trong hàng này, **chỉ thêm**).

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Quyết định phạm vi | `decider` | **`D-307`**: hàng `W25-3` = **`KHONG_CO_FE_DONE_LY_DO`** — 0 dòng code trong `apps/admin-web/**` (SRS `pro-001` §10 `:743` gán màn admin cho `WEB-001`; DoD `bang-phien-gd3-1.md:80` ghi "WP không có FE ⇒ done kèm lý do"; tiền lệ `D-276`/`D-244`). **`D-308`**: `fe` chạy `pnpm spec:snapshot` → `pnpm gen:api` **trong `W25-3`** (bề mặt 7 route đã mở ở `W25-2`, `D-299`; không mở hàng `be`/contract mới). |
| F3 FE — quyết định "không có màn" | `fe` | Đo 28/11: `apps/admin-web/app/[business]/` **0** thư mục `voucher`; `openapi.v1.json` **75** path / **0** `voucher`; `schema.d.ts` **0** DTO quản trị voucher. **0** dòng code sản phẩm dưới `apps/admin-web/**` (đúng `D-307`). |
| Tái sinh hợp đồng | `fe` | `pnpm spec:snapshot` (Host đúng HEAD, Debug/net10.0, lên 200 sau ~26 s; đã `Stop-Process`, cổng 54275 đóng) → **75 → 82 path**; `pnpm gen:api` → `schema.d.ts` **+417 dòng, 0 dòng xoá**. 7 path mới đúng: `/api/v1/voucher`, `/api/v1/voucher/{id}`, `/api/v1/voucher/generate`, `/api/v1/voucher/{id}/issue`, `/api/v1/voucher/{id}/revoke`, `/api/v1/voucher/{id}/override-limit`, `/api/v1/voucher/redemptions`. |
| Bằng chứng additive (ORCH tự đo lại) | ORCH | So **ngữ nghĩa JSON** `HEAD` ↔ mới: path **75→82**, **removed = 0**, shared path/operation **đổi = 0**; `components.schemas` **440→450**, **removed = 0**; mọi khác biệt ở 9 mục đều là **thêm property** (`CheckoutInput.voucherCode`, `KetQuaCheckoutDto.voucherRedemptionId/voucherDiscountTotal/voucherExpiresAtUtc/voucherReasonCode` — `D-298`; `TaoDonTuCheckoutInput.voucherRedemptionId/voucherCode`; `TinhGioInput.khongGhiSoGiaiTrinh` — `D-291`; `RedemptionDto.expiresAtUtc` — `D-302`). `git diff --diff-algorithm=patience --numstat`: **727/0** (`openapi.v1.json`) · **417/0** (`schema.d.ts`). Myers báo 4 dòng xoá — **chỉ là artifact căn lề** (2 đoạn `expiresAtUtc`/`laKetQuaDaCo` được chèn lại sau field mới; so ngữ nghĩa **0 removed**, 0 value đổi). `pnpm gen:api` lần 2 ⇒ sha256 hai tệp **không đổi** (**drift 0**). |

**Cổng ORCH tự chạy (không tin số vai):**

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở) | 0 | **0 error · 9 warning** (Address/Sales có sẵn) · 61,1 s |
| Baseline `dotnet test test/eCommerce.Tests --no-build` (mở) | 0 | **2593/2593** · 0 skipped · 14 s |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở) | 0 | **PASS** · 997 link · 1824 backtick |
| Baseline `npm --prefix tools run check:mermaid` (mở) | 0 | **214 khối · 0 lỗi** |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` (mở) | 0 | **49/49** pass |
| Baseline FE `pnpm --filter @ecommerce/admin-web typecheck` (mở) | 0 | 0 lỗi |
| Baseline FE `pnpm --filter @ecommerce/admin-web test` (mở) | 0 | **60/60** (6 tệp) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-25` (mở) | 0 | **XANH** — mọi mã `Tạm duyệt` |
| **Đo lại** `pwsh -File tools/kiem-tat-ca.ps1` (ORCH, đóng) | 0 | **10/10 ĐẠT** · **430,8 s**: build 123,1 s (**0 err/9 warn**) · unit 27,9 s (**2593/2593**) · integration 46,3 s (**156/156**) · mermaid 11,1 s (**214/0**) · cổng tài liệu 1,7 s · FE typecheck+Vitest 11,3 s (**60/60**) · `gen:api` drift **0** (82/82 path) · DB **25/25** · test cổng **60/60** (194,8 s) |
| **Đo lại** FE `typecheck` (ORCH) | 0 | 0 lỗi |
| **Đo lại** FE `test` (ORCH) | 0 | **60/60** (6 tệp) |
| `pnpm gen:api` lần 2 (ORCH) | 0 | sha256 hai tệp **không đổi** ⇒ **drift 0** |
| FE `pnpm --filter @ecommerce/admin-web lint` | — | **không có script** — nợ môi trường `N70`/`N98`, **không thêm script** (`D-308` ⑥) |
| FE `pnpm --filter @ecommerce/admin-web build` | 1 | Next compile xong; đỏ ở bước copy standalone: `EPERM: symlink` (Windows, `output: standalone`) — **nợ cũ K-05/`N95`**, không phải do hàng này; **không** sửa `next.config.ts` để lách |
| `check-agent-artifact.ps1 -Kind decision` | 0 | **DAT** · 117 dòng |

## 2. Chưa xong / cắt giữa

**Không có việc code dở của `W25-3`.** Các mục còn lại **không thuộc hàng này** hoặc **bị chặn ngoài phiên**:

1. **Màn voucher (4 vùng + menu) — chủ WP `WEB-001`, CHƯA có lịch.** `D-307` (5)(a) đã ghi đặc tả ràng buộc: danh sách **phải hiện 3 số** `UsedCount`·`ReservedCount`·`MaxUsage` cạnh nhau; form sinh lô hiện `MaxBatchSize` + khoá nút + lý do `0108`; chi tiết **chỉ đọc** + "Thu hồi mã" (`reason ≥ 10`) + `OverrideLimitAsync` là hành động riêng; tra cứu lượt phân biệt `Reserved`/`Applied`. Chờ người xếp lịch (`D-247`).
2. **Áp migration `Add_Voucher_Status_Check` — BỊ CHẶN NGOÀI PHIÊN (từ `W25-2`).** File đã commit, `pg_dump` đã có; lệnh `dotnet run --project src/eCommerce.DbMigrator` vẫn phải do người chạy ngoài auto mode (mục §3).
3. **SRS §6.2 còn ghi `/api/app/voucher/**`** trong khi as-built là `/api/v1/voucher/**` (+ giữ auto `/api/app/voucher-admin/**`, `D-299`). Chủ **`ba`** sửa cùng nhánh/PR (`D-307` (5)(b)).
4. **`W25-4` (F4 QA → F5 REV)** — hàng kế, chưa mở.

## 3. Migration đã áp

**Không có migration mới trong phiên này.**

- `20261127153549_Add_Voucher_Status_Check` (EF sinh ở `W25-2`) vẫn **CHƯA ÁP** DB dev (`__EFMigrationsHistory` chưa có).
  `pg_dump` trước đó: `artifacts/pg-dump/N101-W25-2.dump` — **576.910 byte** (PostgreSQL 17, cổng 5433, `pg_dump -Fc`).
- Người chạy ngoài auto mode: `dotnet run --project src/eCommerce.DbMigrator`, rồi kiểm `__EFMigrationsHistory` có `20261127153549_Add_Voucher_Status_Check`.
- Lùi: `Down` của migration = 2 `DropCheckConstraint` (`CK_ProVoucherCodes_Status`, `CK_ProPromotionRedemptions_Status`); 0 cột/index/dữ liệu.

## 4. Quyết định mới

`decider` ghi **2 dòng** `TẠM CHỐT (DEC, uỷ quyền)`, `check-agent-artifact -Kind decision` **DAT** (117 dòng):

| D-xx | Nội dung (ngắn) | Phạm vi ảnh hưởng |
|---|---|---|
| `D-307` | `W25-3` = **0 dòng code màn hình** (`KHONG_CO_FE_DONE_LY_DO`); màn voucher thuộc WP `WEB-001` (`D-247` chờ người). Nợ có chủ: WP `WEB-001` (4 vùng + menu) · `ba` sửa SRS §6.2 + 1 dòng §10 · `qa` `W25-4` thêm `VoucherV1SurfaceTests` + ca đọc `openapi.v1.json`. Cấm viết màn voucher trong WP-25; cấm tuyên bố done mà không liệt kê nợ | WP-25 + WP `WEB-001` + SRS + `W25-4` |
| `D-308` | `fe` tái sinh hợp đồng **trong `W25-3`**: `spec:snapshot` + `gen:api`, **chỉ thêm** 75→82 path, **0 dòng xoá**, **0** path/operation/kiểu cũ đổi; vi phạm ⇒ dừng, không commit. Cấm hand-edit; Host đúng HEAD; commit riêng `feat(api-client): …`; `pnpm lint` chưa có script ⇒ ghi nợ, không thêm script | `packages/api-client/**` + `W25-4`/`W25-5` |

Hàng đợi xem lại: `docs/agents/hang-doi-xem-lai.md` thêm `D-307`, `D-308` (và 1 dòng cổng cho `W25-4`, không cần `D-xx`).

## 5. Nợ / rủi ro

- 🔴 **Màn voucher chưa có** — chủ **WP `WEB-001`** (`D-247` chờ người xếp lịch). Hợp đồng `/api/v1/voucher/**` đã sẵn trong `openapi.v1.json`/`schema.d.ts` nhờ `D-308` ⇒ WP đó không phải tái sinh lại.
- 🔴 **Migration chưa áp** — chủ **người** (ngoài auto mode), như `N101` §3.
- 🟡 **`qa` `W25-4` bắt buộc**: (i) `VoucherV1SurfaceTests` — **đúng 7** route + `[Authorize]` cấp class `Promotions.Default` + map method→permission (khuôn `PromotionV1SurfaceTests.cs`); (ii) ca đọc `packages/api-client/openapi.v1.json` — **đúng 7** path voucher + template khớp `VoucherController`; (iii) phủ integration còn nợ: Σ sau UPDATE/rollback, `UsageSequence` hết trần/`25P02`, worker `0 dòng`/deadlock, probe 200/401/403; (iv) `F-1` metric 0 dòng (chủ `rev/ba` chốt câu chữ hoặc code). **Không** đóng `W25-4` khi thiếu (`D-307`).
- 🟡 **`ba` sửa SRS §6.2** (`/api/app/voucher` → `/api/v1/voucher` as-built) + thêm 1 dòng §10 "màn voucher thuộc `WEB-001`; `openapi.v1.json` đã tái sinh ở `W25-3`" — cùng nhánh/PR.
- 🟡 **P3 docrev vòng 4** của `PRO-001-arch` (5 mục nhỏ) — chủ `arch`/`ba`, không chặn.
- 🟡 **Nợ `PRO-002`**: `PromotionExpiryWorker` cũ chưa đọc cờ `Promotions:Enabled`; `promotion_applied_total` còn trong `ChuaCoNoiPhat` (nợ `SAL-001`).
- 🟡 **Môi trường (không phải hàng này)**: `pnpm build` EPERM symlink standalone (`K-05`, `N95`); `pnpm lint` không có script (`N70`); `gen:api` nửa nặng (controller → snapshot) chỉ chạy khi Host bật — CI job `openapi-drift` lo.
- **Chờ người** (gom một lần ở câu trả lời cuối): (a) **áp migration** bằng DbMigrator ngoài auto mode; (b) duyệt lô `D-277…D-308`; (c) kế toán duyệt lô M4 (`D-143`/`D-148`/`D-149`/`D-152`/`D-158`) trước release; (d) xếp lịch WP `WEB-001` (màn voucher) — `D-247`/`D-307`; (e) merge `main` + PR nháp `tich-hop/gd3 → main` khi chuỗi xong; (f) AQ10/Q8 GHTK xác minh địa chỉ 2 cấp.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W25-3` → mở `W25-4`.**

- Hàng kế = **`W25-4`** (loại `code`, bước **F4 QA → F5 REV**), vai **`qa` · `rev`**.
- `qa` nhớ 4 việc bắt buộc ở §5 (surface test + snapshot test + integration còn nợ + `F-1`); `rev` review `git diff` nhánh này (chỉ 2 tệp hợp đồng sinh tự động + docs, **0 dòng code sản phẩm** — đừng kỳ vọng diff lớn).
- Đọc trước: `D-307`/`D-308` (`docs/nhanh/decisions.md`), SRS `pro-001` **v0.12** §6.1/§6.2/§10/§14, arch **v0.6** §10.1/§10.4, handover này §2/§5.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-25
và handover gần nhất `docs/nhanh/handover-n102.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng W25-4** (phiên N103). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

Một dòng cho chuỗi: `/chay-phien-code chuoi=gd3-1`

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp25-voucher`; base `origin/tich-hop/gd3` @ `e7aa32c`.
- Commit phiên: `docs(nhanh)` (`D-307`/`D-308` + bảng) · `feat(api-client)` (snapshot 75→82 + `schema.d.ts` +417/0) · `docs(nhanh)` (handover + registry). **HEAD sau commit** — xem `git log -1` (SHA ghi ở `PH done -Evidence`).
- **Không** đẩy `tich-hop/gd3` (hàng `code`; việc đó ở `W25-5` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N102`): `decider` ×1 · `fe` ×1 · **0 lần chặn dừng**. `canh-bao.jsonl` ghi "thiếu khối JSON cuối" ở cả 2 lượt — **không phải thiếu thật** (giới hạn hook đã biết, `chanDungKhiThieuJson=false`; cả 2 vai đều trả đủ khối JSON cuối). **`ngoaiVung = []`** ở mọi lượt: `fe` chỉ đổi `packages/api-client/openapi.v1.json` + `schema.d.ts`; `decider` chỉ đổi `docs/nhanh/decisions.md` + `docs/agents/hang-doi-xem-lai.md`.
