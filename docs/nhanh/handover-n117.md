# Handover N117 — WP-17 / hàng `W17-3` (F3 FE) XONG: không có màn FE mới + đóng nợ OpenAPI drift

> Phiên `N117` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-3` (loại `code`, bước `F3 FE`, vai `fe`) ·
> WP-17 "giá vốn" · mã `PRC-001` · nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3`) · 28/11/2026.
> Phạm vi: xác nhận WP-17 **không** có màn FE mới (ARCH §1.1, SRS §10 → `WEB-001`); tái sinh hợp đồng OpenAPI
> (`openapi.v1.json` + `schema.d.ts`) cho 2 app service mới của `W17-2`; đồng bộ mục as-built SRS.
> **0 tệp BE · 0 migration · 0 màn hình · 0 merge `main`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Chọn hàng + nhánh | ORCH | `PH next` ⇒ `W17-3`; `PH check` **OK**; nhánh đúng `feat/gd3-wp17-gia-von`, cây sạch; `PH start` ⇒ **`PHIEN=N117`** |
| Điều kiện vào code | ORCH | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` **XANH exit 0** (PRC-001 duyệt GD1) |
| Xác nhận phạm vi FE | ORCH + `fe` | ARCH `PRC-001-arch.md:12` "**Không có** màn hình mới trong WP-17"; SRS `§10` dòng 662 "UI thuộc `WEB-001`"; `apps/admin-web` **0** tham chiếu `gia-von` ⇒ **không có màn FE** |
| Tái sinh hợp đồng OpenAPI | `fe` | `tools/kiem-openapi-drift.ps1` **lần 1 exit 1** (đúng nợ) sinh lại `openapi.v1.json` (**+106 dòng**) + `schema.d.ts` (**+35 dòng**), thuần **additive**; **lần 2 exit 0 · drift 0**. `pnpm gen:api` không đổi `schema.d.ts` |
| Phát hiện endpoint | `fe` | 2 app service lộ ở **`/api/app/cost-price-admin`** + **`/api/app/cost-engine/chay`** (conventional ABP) — **đúng SRS §6.2**; `snapshot.mjs` chỉ giữ `/api/v1` ⇒ snapshot **82 path trước = sau**; 2 DTO component + 2 tag được thêm do swagger gom components |
| Đồng bộ SRS as-built | `ba` | Sửa 1 bullet "nợ `W17-3`" thành **ĐÃ ĐÓNG NỢ** + thêm dòng `N117 / W17-3` (không màn FE, chỉ tái sinh hợp đồng); **+10/−1**, không đổi §0–§20 |
| Cổng FE | `fe` | `pnpm typecheck` **0** · `pnpm test` **60/60** · `pnpm build` **exit 1 do `EPERM` symlink** — nợ môi trường Windows **K-05/N95**, **không do phiên** (build thật chạy CI Linux) · `pnpm lint` **không có script** (nợ **N70**, root no-op exit 0) |

**Baseline mở phiên (code WP, `08` §4):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 Error · 9 Warning** (cũ) |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2650/2650** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS · 329 tệp · 1050 link · 2006 backtick |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `docker compose ps` | 0 | postgres + redis running (5433/6380) |

**Cổng G3 — ORCH tự chạy trên cây cuối (không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| Cổng nhẹ đầy đủ | `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0 · 10/10 · 354,8 s** — build **0 Error/9 Warning** · unit **2650/2650** · integration **162/162** · FE Vitest **60/60** · `gen:api` drift **0** · DB bất biến **25/25** · test cổng **60/60** · mermaid **214/0** · cổng tài liệu XANH |
| OpenAPI drift (nặng) | `pwsh -File tools/kiem-openapi-drift.ps1` ×2 | lần 1 **exit 1** (sinh lại) → lần 2 **exit 0 · drift 0** |
| Cổng tài liệu | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` | **XANH exit 0** |
| Link | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** · 1050 link · 2006 backtick · 0 chết |
| Quyết định | `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | **exit 0 · DAT** (114 dòng) |
| Kích thước | `git show --stat ae200fd` | 3 tệp · **+150/−1** (openapi.v1.json +106, schema.d.ts +35, SRS +10/−1) |

## 2. Chưa xong / cắt giữa

Không có việc dở của `W17-3`. Hai mục dưới là **nợ môi trường có sẵn**, không do phiên này và không chặn DoD:

- **`pnpm build` exit 1 — `EPERM: operation not permitted, symlink`** khi copy `output: standalone`. Log cho thấy compile + static pages **thành công** (`✓ Compiled successfully`, `✓ Generating static pages (4/4)`) rồi đỏ ở bước copy. Đây là nợ **K-05/N95** (Windows symlink), build thật chạy CI Linux; **không** sửa `next.config.ts` để lách.
- **`pnpm lint` không có script** trong `apps/admin-web` (nợ **N70**); chạy ở gốc là no-op exit 0. Không thêm script (ngoài phạm vi, mở việc mới cần `D-xx`).

## 3. Migration đã áp

**Không có.** Phiên này **0 migration, 0 `pg_dump`, 0 thay đổi schema**, 0 `.cs`.

## 4. Quyết định mới

**Không có `D-xx` mới.** `D-318`…`D-324` giữ nguyên hiệu lực. Ghi nhận (không phải quyết định):
2 app service của `W17-2` lộ ở `/api/app/...` theo SRS §6.2 — nếu `WEB-001` sau cần chúng qua `/api/v1` thì **BE** phải thêm controller v1 viết tay (ngoài vùng `fe`).

## 5. Nợ / rủi ro

- 🟠 **`Q4`/`PRC-001-AQ1`** (`CostPrice` duyệt 2 bước) — câu **kế toán trưởng** (M4/R3-3), vẫn `Open`; **chặn release**.
- 🟠 **`PRC-001-AQ2`** (nhập lùi đụng kỳ đã duyệt: revision hay từ chối) — câu **người**, vẫn `Open`; hiện thực (a) từ chối `0015`.
- 🟠 **`W17-2` chưa có test nguồn** ⇒ **`W17-4` bắt buộc** dựng 14 ca K-06 + negative control (điều kiện cứng `D-320` (vi)); nếu `W17-4` không xong, động cơ chưa có lưới đỏ.
- 🟠 **`schema.sql` tích hợp chưa sinh lại** (cố ý — `PostgreSqlSchemaTests` ghim tổng NND=114) ⇒ **`W17-4`** cập nhật tổng + thêm 2 tên index khi làm test tích hợp.
- 🟡 **`pnpm build` EPERM** (K-05/N95) và **`pnpm lint` thiếu script** (N70) — nợ môi trường có sẵn, chủ: hạ tầng/CI; không do WP-17.
- 🟡 **`WEB-001`**: nếu cần 2 endpoint giá vốn qua `/api/v1` thì phải thêm controller v1 (BE), tách WP.
- 🟡 **P3=7** của DOCREV SRS + nợ cũ `check-feature-doc` (`BR-PRC-001-008` thiếu test; "Đối chiếu Nhanh") — dọn dần; **không** chặn.

**Chờ người (gom một lần):**

| # | Câu | Khuyến nghị | Việc đã làm tiếp | Trả lời bằng cách nào |
|---|---|---|---|---|
| 1 | `Q4`/`PRC-001-AQ1`: `CostPrice` bắt buộc luồng duyệt 2 bước? | Giữ **(a) 2 bước** cho GĐ3 (đã code) | `Q4` giữ `Open`, không chặn `W17-4/5` | Kế toán duyệt lô M4/R3-3 trước release — dán sau `/chay-phien-code chuoi=gd3-1` |
| 2 | `PRC-001-AQ2`: nhập lùi đụng kỳ **đã duyệt** có cần revision? | Giữ **(a) từ chối** (`NÉM 0015`); (b) revision = đổi unique = phá huỷ schema | Đã hiện thực (a) | Người quyết nếu muốn (b) |
| 3 | Đa tiền tệ / tiền tệ cơ sở tenant cho giá vốn | **(a) v1 chỉ VND** (`D-324` ③) | v1 VND; `TienTeNhap ≠ VND ⇒ 0014` | Kế toán + kiến trúc khi mở ngoại tệ |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-3` → mở `W17-4` (F4 QA: testplan K-06 + negative control).**

- `W17-4`: dựng testplan của `PRC-001` trong `docs/tests/` — 14 ca K-06 (**K06-01…07, K06-08/08b/09/10/11/12/13, 08b trên PostgreSQL thật**: P1 commit bền khi P2 ném, retry `Attempt++`), test kiến trúc quét 4 **tên entity** Inventory trong `modules/Pricing/**`, ca `0014` tiền tệ, `BR-PRC-001-008`; **negative control bắt buộc**; `check-agent-artifact -Kind testplan` **exit 0** (điều kiện cứng `D-320` (vi) — **cấm bỏ/nới** ca). Cập nhật `schema.sql` + tổng NND=114 + 2 tên index.
- `W17-5`: `ops` + G6 `-DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; **M4 kế toán trước release**.

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ `5c902ed`.
- Commit việc: **`ae200fd`** — `feat(pricing): N117 - W17-3 F3 FE regen OpenAPI contract gia von, no new FE screen` (3 tệp).
- **Sẽ push** `origin/feat/gd3-wp17-gia-von` sau commit handover. **Chưa** đẩy `tich-hop/gd3` (chỉ `dong-wp` mới FF), **không** force/merge, **không** mở PR, **không** đụng `main`.

---

**Hàng rào agent** (phiên `N117`): `fe` ×1 (42 lượt, 354,5 s) · `ba` ×1 (27 lượt, 66,5 s) · `decider` ×0 · `docrev` ×0 · ORCH tự chạy toàn bộ cổng máy. **0 tệp ngoài vùng thật** (`git status` khớp vùng `fe` = `packages/api-client/**`, `ba` = `docs/features/**`; `agents/state/**` + `docs/agents/bang-phien-*` do `PH start/done`). Cảnh báo hook "thiếu khối JSON cuối" cho `fe` là **dương tính giả** (báo cáo có khối JSON; `chanDungKhiThieuJson=false`, đã biết N116). `fe` dùng `codegraph` trước `Grep` theo yêu cầu.
