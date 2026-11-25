# Bảng phiên chạy — chuỗi WP code GĐ3 số 1 (`gd3-1`)

> Chuỗi các WP code **không cần người / sandbox hãng / kế toán** theo [07 §WP-15+](../nhanh/07-ke-hoach-trien-khai.md):
> WP-16 → WP-18 → WP-22 → WP-26 → WP-24 → WP-25 → WP-23 (phần trung lập) → WP-17 (đỏ — cổng có người, xếp cuối).
> Thiết kế + quyết định: [chay-tu-dong-code.md](chay-tu-dong-code.md) (`D-216`, phương án **B** — nhánh xếp chồng).
> Nguồn sự thật: `agents/state/phien-gd3-1.json`; bảng §3 **sinh bằng script** (`phien.ps1 -Chuoi gd3-1 sync`), không sửa tay.

## 0. Chạy tự động

```powershell
pwsh -File tools/agents/chay-tu-dong.ps1 -Chuoi gd3-1 -ChiTiet
```

Mỗi vòng: `phien.ps1 -Chuoi gd3-1 next` → `claude -p "/chay-phien-code chuoi=gd3-1"` → phiên tự chuyển/cắt nhánh WP, làm đúng bước của hàng,
kiểm cổng máy, commit + push nhánh WP; hàng `dong-wp` đẩy fast-forward vào `tich-hop/gd3`. Dừng khi: hết việc · tới hàng 👤 · CI trên
`tich-hop/gd3` **đỏ** (cầu dao, xem bằng `gh run list --branch tich-hop/gd3`) · 2 phiên liền không tiến triển · tệp phanh
`artifacts/agents/tu-dong/DUNG`. Hết việc mà muốn mở PR xếp chồng cho từng WP: thêm `-MoPRCuoi` (không bao giờ merge).

Chạy tay một phiên (có người ngồi xem): mở Claude Code tại repo, dán `/chay-phien-code chuoi=gd3-1`.

## 1. Việc của chủ dự án — một lần trước khi chạy

1. Merge PR WP-15 (#20) và PR bộ chạy này vào `main`.
2. Tạo nhánh tích hợp từ `main` (M3 của [hồ sơ uỷ quyền](uy-quyen-chu-du-an.md): người tạo nhánh):
   `git fetch origin; git push origin origin/main:refs/heads/tich-hop/gd3`.
   Nếu đặt branch protection cho `tich-hop/gd3`: **cho phép** push fast-forward (không force) — agent đẩy vào đây ở hàng `dong-wp`.
3. Mở **một PR nháp** `tich-hop/gd3` → `main` để CI chạy trên mọi lần đẩy (cầu dao CI đọc lần chạy `ci` của nhánh này):
   `gh pr create --draft --base main --head tich-hop/gd3 --title "GD3 chuoi 1 (nhap - chi de CI)" --body "Refs: D-216"`.
   📌 Lúc vừa tạo, `tich-hop/gd3` **trùng** `main` ⇒ GitHub từ chối PR (`pull request create failed: GraphQL` — không có commit khác biệt).
   Mở PR nháp **sau** hàng `dong-wp` đầu tiên (`W16-5`) đẩy vào `tich-hop/gd3`; trước đó cầu dao CI in `CI: chua ro` và chạy tiếp.
4. Máy dev: biến môi trường `ConnectionStrings__Default` / `Redis__Configuration` trỏ cổng 5433/6380 (`.env` của compose); `pnpm@10.22.0`
   có trên PATH; tắt chế độ ngủ của Windows.

Trong lúc chạy: hàng 👤 (`W17-0n`, `W17-1n`) — ký/duyệt xong thì dán `/chay-phien-code chuoi=gd3-1 W17-0n đã duyệt, biên bản ở <tệp>`.

## 2. Luật của chuỗi

- **Tuần tự tuyệt đối:** một phiên một lúc, một chủ migration; không mở cửa sổ tay khi bộ chạy đang chạy.
- **Nhánh:** mỗi WP một nhánh `feat/gd3-wpNN-<tên>` cắt từ đỉnh `tich-hop/gd3` (đã chứa mọi WP trước). Tên nhánh chứa `wpNN` để
  `tools/probe-cong-tai-lieu.ps1` suy được WP.
- **Không PR mỗi phiên.** CI chạy qua PR nháp `tich-hop/gd3` → `main`. `main` chỉ người merge, cần biên bản người ký + cổng OPS (`D-115` ⑤).
- **Đỏ ⇒ không commit phần đỏ** (`stash` có tên, ghi handover), hàng `dodang`; không bao giờ `reset`/`--force`.
- **Migration:** `pg_dump` DB dev vào `artifacts/pg-dump/` trước mọi lần áp.

## 3. Bảng

<!-- BANG-PHIEN:BAT-DAU -->
| # | Đợt | Nội dung | Vai | Phụ thuộc | Prompt để dán | Phiên | Trạng thái | Bằng chứng / ghi chú |
|---|---|---|---|---|---|---|---|---|
| `W16-1` | WP-16 | F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2 (PRC-001/002 đã duyệt Q5; câu mới ⇒ decider) | ORCH · arch · docrev · decider | — | `/chay-phien-code chuoi=gd3-1 W16-1` | N71 | ✅ Hoàn thành | DOCREV DOC-OK 0 P1/0 P2 + check-agent-artifact docreview DAT + kiem-link PASS 235 tep/930 link + probe Code WP-16 exit 0 (D-217 mirror) + D-217..D-224 + commit 028d20d · `docs/nhanh/handover-n71.md` |
| `W16-2` | WP-16 | F3 BE: domain + app service + endpoint + migration (expand-only, pg_dump trước khi áp) + test đơn vị | be | W16-1 | `/chay-phien-code chuoi=gd3-1 W16-2` | N72 | ✅ Hoàn thành | F3 BE 4 hang muc PRC-002; build 0 error/9 warning; unit 1999/1999 (+33); kiem-tat-ca exit 0 (10/10, 331.8s); kiem-link PASS 237 tep/931 link; DOCREV DOC-OK P1=0; 0 migration; D-225..D-228; commit 100db0a · `docs/nhanh/handover-n72.md` |
| `W16-3` | WP-16 | F3 FE: màn Web Admin theo SRS (contract-first `schema.d.ts`); WP không có FE ⇒ done kèm lý do | fe | W16-2 | `/chay-phien-code chuoi=gd3-1 W16-3` | N73 | ✅ Hoàn thành | F3 FE WP khong co FE (SRS 10 -> WEB-001); admin-web 23 route khong Pricing/tier-price; grep ma FE 0 hit; W16-2 khong doi DTO => schema.d.ts khong doi; kiem-tat-ca exit 0 (10/10, 290.5s); build 0 error/9 warning; unit 1999/1999; integration 96/96; FE typecheck 0 loi; Vitest 25/25; gen:api schema.d.ts khong doi; DB 25/25; test cong 60/60; kiem-link PASS 238 tep/932 link; hooks 49/49; 0 .cs/0 migration · `docs/nhanh/handover-n73.md` |
| `W16-4` | WP-16 | F4 QA (mọi BR- có test + negative control đỏ→xanh) → F5 REV (`VERDICT: PASS`, P1 = 0) | qa · rev | W16-3 | `/chay-phien-code chuoi=gd3-1 W16-4` | N74 | ✅ Hoàn thành | F4 QA: testplan 21/21 BR, +22 test (2021/2021), NC 4/4 do->xanh, check testplan DAT; F5 REV PASS P1=0; kiem-tat-ca 10/10 287.4s (build 0 err/9 warn, integration 96/96); D-229, D-230; commit 798cca9 · `docs/nhanh/handover-n74.md` |
| `W16-5` | WP-16 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; `07` §13 | ops · ORCH | W16-4 | `/chay-phien-code chuoi=gd3-1 W16-5` | N75 | ✅ Hoàn thành | F6 ops: docs/ops/PRC-002-deploy.md (rollback config 3 khoa Pricing:* + revert 100db0a; 0 migration/khong pg_dump); G6 kiem-tat-ca -DayDu exit 0 (20/20, 744.3s); G4 DAT (BR 21/21); G5 DAT (PASS P1=0); pricing test 144/144; doc-gate probe WP-16 XANH; push fast-forward feat/gd3-wp16-bang-gia -> tich-hop/gd3 (base e481094); closing SHA in final answer · `docs/nhanh/handover-n75.md` |
| `W18-0` | WP-18 | Cổng tài liệu `D-141`: DOCREV kiểm nhanh v0.5 đóng 2 P2 **mỗi** tài liệu; mã GHTK không code bằng suy đoán (R3-1); `ADDR-003` + PII ngoài WP | ba · docrev · decider | W16-5 | `/chay-phien-code chuoi=gd3-1 W18-0` | — | ⬜ Chưa bắt đầu |  |
| `W18-1` | WP-18 | F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2 (mã GHTK chưa có ⇒ không code nhánh đó) | ORCH · arch · docrev · decider | W18-0 | `/chay-phien-code chuoi=gd3-1 W18-1` | — | ⬜ Chưa bắt đầu |  |
| `W18-2` | WP-18 | F3 BE: domain + app service + endpoint + migration (expand-only, pg_dump trước khi áp) + test đơn vị | be | W18-1 | `/chay-phien-code chuoi=gd3-1 W18-2` | — | ⬜ Chưa bắt đầu |  |
| `W18-3` | WP-18 | F3 FE: màn Web Admin theo SRS (contract-first `schema.d.ts`); WP không có FE ⇒ done kèm lý do | fe | W18-2 | `/chay-phien-code chuoi=gd3-1 W18-3` | — | ⬜ Chưa bắt đầu |  |
| `W18-4` | WP-18 | F4 QA (mọi BR- có test + negative control đỏ→xanh) → F5 REV (`VERDICT: PASS`, P1 = 0) | qa · rev | W18-3 | `/chay-phien-code chuoi=gd3-1 W18-4` | — | ⬜ Chưa bắt đầu |  |
| `W18-5` | WP-18 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; `07` §13 | ops · ORCH | W18-4 | `/chay-phien-code chuoi=gd3-1 W18-5` | — | ⬜ Chưa bắt đầu |  |
| `W22-0` | WP-22 | Cổng tài liệu `D-208`: BA sửa 2 P2 #24/#26 → DOCREV kiểm nhanh **trước** `migrations add`/state machine (15 P3 làm cùng WP) | ba · docrev · decider | W18-5 | `/chay-phien-code chuoi=gd3-1 W22-0` | — | ⬜ Chưa bắt đầu |  |
| `W22-1` | WP-22 | F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2 (module `Notification` mới, D-197; 15 P3 cùng WP) | ORCH · arch · docrev · decider | W22-0 | `/chay-phien-code chuoi=gd3-1 W22-1` | — | ⬜ Chưa bắt đầu |  |
| `W22-2` | WP-22 | F3 BE: domain + app service + endpoint + migration (expand-only, pg_dump trước khi áp) + test đơn vị | be | W22-1 | `/chay-phien-code chuoi=gd3-1 W22-2` | — | ⬜ Chưa bắt đầu |  |
| `W22-3` | WP-22 | F3 FE: màn Web Admin theo SRS (contract-first `schema.d.ts`); WP không có FE ⇒ done kèm lý do | fe | W22-2 | `/chay-phien-code chuoi=gd3-1 W22-3` | — | ⬜ Chưa bắt đầu |  |
| `W22-4` | WP-22 | F4 QA (mọi BR- có test + negative control đỏ→xanh) → F5 REV (`VERDICT: PASS`, P1 = 0) | qa · rev | W22-3 | `/chay-phien-code chuoi=gd3-1 W22-4` | — | ⬜ Chưa bắt đầu |  |
| `W22-5` | WP-22 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; `07` §13 | ops · ORCH | W22-4 | `/chay-phien-code chuoi=gd3-1 W22-5` | — | ⬜ Chưa bắt đầu |  |
| `W26-1` | WP-26 | F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2 (D-208 tạm duyệt có điều kiện, điều kiện đỏ đã đạt; 10 P3 cùng WP; KHÔNG đụng cột giá vốn) | ORCH · arch · docrev · decider | W22-5 | `/chay-phien-code chuoi=gd3-1 W26-1` | — | ⬜ Chưa bắt đầu |  |
| `W26-2` | WP-26 | F3 BE: domain + app service + endpoint + migration (expand-only, pg_dump trước khi áp) + test đơn vị | be | W26-1 | `/chay-phien-code chuoi=gd3-1 W26-2` | — | ⬜ Chưa bắt đầu |  |
| `W26-3` | WP-26 | F3 FE: màn Web Admin theo SRS (contract-first `schema.d.ts`); WP không có FE ⇒ done kèm lý do | fe | W26-2 | `/chay-phien-code chuoi=gd3-1 W26-3` | — | ⬜ Chưa bắt đầu |  |
| `W26-4` | WP-26 | F4 QA (mọi BR- có test + negative control đỏ→xanh) → F5 REV (`VERDICT: PASS`, P1 = 0) | qa · rev | W26-3 | `/chay-phien-code chuoi=gd3-1 W26-4` | — | ⬜ Chưa bắt đầu |  |
| `W26-5` | WP-26 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; `07` §13 | ops · ORCH | W26-4 | `/chay-phien-code chuoi=gd3-1 W26-5` | — | ⬜ Chưa bắt đầu |  |
| `W24-1` | WP-24 | F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2 (module `Promotions` mới; `Add_Promotion_Module` là nguồn schema duy nhất, D-151; Q8 chỉ chặn nhánh điều kiện địa chỉ) | ORCH · arch · docrev · decider | W26-5 | `/chay-phien-code chuoi=gd3-1 W24-1` | — | ⬜ Chưa bắt đầu |  |
| `W24-2` | WP-24 | F3 BE: domain + app service + endpoint + migration (expand-only, pg_dump trước khi áp) + test đơn vị | be | W24-1 | `/chay-phien-code chuoi=gd3-1 W24-2` | — | ⬜ Chưa bắt đầu |  |
| `W24-3` | WP-24 | F3 FE: màn Web Admin theo SRS (contract-first `schema.d.ts`); WP không có FE ⇒ done kèm lý do | fe | W24-2 | `/chay-phien-code chuoi=gd3-1 W24-3` | — | ⬜ Chưa bắt đầu |  |
| `W24-4` | WP-24 | F4 QA (mọi BR- có test + negative control đỏ→xanh) → F5 REV (`VERDICT: PASS`, P1 = 0) | qa · rev | W24-3 | `/chay-phien-code chuoi=gd3-1 W24-4` | — | ⬜ Chưa bắt đầu |  |
| `W24-5` | WP-24 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; `07` §13 | ops · ORCH | W24-4 | `/chay-phien-code chuoi=gd3-1 W24-5` | — | ⬜ Chưa bắt đầu |  |
| `W25-0` | WP-25 | Cổng tài liệu `D-169`: BA sửa P2 #20 đúng hướng `D-169` (**cấm** gán `UsageSequence = 1`) → DOCREV kiểm nhanh; P3 cùng WP | ba · docrev · decider | W24-5 | `/chay-phien-code chuoi=gd3-1 W25-0` | — | ⬜ Chưa bắt đầu |  |
| `W25-1` | WP-25 | F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2 (trên engine WP-24; M4 trước release) | ORCH · arch · docrev · decider | W25-0 | `/chay-phien-code chuoi=gd3-1 W25-1` | — | ⬜ Chưa bắt đầu |  |
| `W25-2` | WP-25 | F3 BE: domain + app service + endpoint + migration (expand-only, pg_dump trước khi áp) + test đơn vị | be | W25-1 | `/chay-phien-code chuoi=gd3-1 W25-2` | — | ⬜ Chưa bắt đầu |  |
| `W25-3` | WP-25 | F3 FE: màn Web Admin theo SRS (contract-first `schema.d.ts`); WP không có FE ⇒ done kèm lý do | fe | W25-2 | `/chay-phien-code chuoi=gd3-1 W25-3` | — | ⬜ Chưa bắt đầu |  |
| `W25-4` | WP-25 | F4 QA (mọi BR- có test + negative control đỏ→xanh) → F5 REV (`VERDICT: PASS`, P1 = 0) | qa · rev | W25-3 | `/chay-phien-code chuoi=gd3-1 W25-4` | — | ⬜ Chưa bắt đầu |  |
| `W25-5` | WP-25 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; `07` §13 | ops · ORCH | W25-4 | `/chay-phien-code chuoi=gd3-1 W25-5` | — | ⬜ Chưa bắt đầu |  |
| `W23-0` | WP-23 | Cổng tài liệu `D-208`: BA sửa P2 #1 + P3 → DOCREV kiểm nhanh | ba · docrev · decider | W25-5 | `/chay-phien-code chuoi=gd3-1 W23-0` | — | ⬜ Chưa bắt đầu |  |
| `W23-1` | WP-23 | F0–F2b: đọc SRS theo mục, ARCH khi chạm ≥ 2 module / schema / contract; G1/G2 theo M2 — **chỉ** hợp đồng adapter + `fake` sender + fail-closed `0003`; `Q1`/`Q3` + HMAC production ngoài phạm vi (R3-6) | ORCH · arch · docrev · decider | W23-0 | `/chay-phien-code chuoi=gd3-1 W23-1` | — | ⬜ Chưa bắt đầu |  |
| `W23-2` | WP-23 | F3 BE: domain + app service + endpoint + migration (expand-only, pg_dump trước khi áp) + test đơn vị | be | W23-1 | `/chay-phien-code chuoi=gd3-1 W23-2` | — | ⬜ Chưa bắt đầu |  |
| `W23-3` | WP-23 | F3 FE: SRS không có màn ⇒ done kèm lý do (kiểm lại SRS) | fe | W23-2 | `/chay-phien-code chuoi=gd3-1 W23-3` | — | ⬜ Chưa bắt đầu |  |
| `W23-4` | WP-23 | F4 QA (mọi BR- có test + negative control đỏ→xanh) → F5 REV (`VERDICT: PASS`, P1 = 0) | qa · rev | W23-3 | `/chay-phien-code chuoi=gd3-1 W23-4` | — | ⬜ Chưa bắt đầu |  |
| `W23-5` | WP-23 | F6 OPS + G6 `kiem-tat-ca.ps1 -DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; `07` §13 | ops · ORCH | W23-4 | `/chay-phien-code chuoi=gd3-1 W23-5` | — | ⬜ Chưa bắt đầu |  |
| `W17-0` | WP-17 | Cổng tài liệu `D-186` ⑥: BA sửa `prc-001` §15.1/§20.4 + `inv-001` §0 + `rep-003` §0/Q11 **cùng nhánh** → DOCREV kiểm nhanh (REP-003 đỏ cứng ⇒ không tạm duyệt) | ba · docrev | W23-5 | `/chay-phien-code chuoi=gd3-1 W17-0` | — | ⬜ Chưa bắt đầu |  |
| `W17-0n` | WP-17 | **Người ký** phần sửa `rep-003` §0/Q11 (đỏ cứng) + xác nhận WP-17 chạy mức L1 (lõi tồn/tiền: mọi cổng có người) | người | W17-0 | — (việc của người) | — | 👤 Chờ người |  |
| `W17-1` | WP-17 | F0–F2b: ARCH động cơ bình quân gia quyền di động (`PRC-001` §20; `D-177` ④–⑦, `D-186`) — BE dùng Opus cho lõi sổ cái | ORCH · arch · docrev | W17-0n | `/chay-phien-code chuoi=gd3-1 W17-1` | — | ⬜ Chưa bắt đầu |  |
| `W17-1n` | WP-17 | **Người duyệt G2** thiết kế ARCH giá vốn (danh sách đỏ §4.3 — decider không tạm duyệt) | người | W17-1 | — (việc của người) | — | 👤 Chờ người |  |
| `W17-2` | WP-17 | F3 BE (model opus): đường ghi + duyệt + động cơ giá vốn; migration expand-only + pg_dump | be | W17-1n | `/chay-phien-code chuoi=gd3-1 W17-2` | — | ⬜ Chưa bắt đầu |  |
| `W17-3` | WP-17 | F3 FE: màn duyệt giá vốn theo SRS; không có ⇒ done kèm lý do | fe | W17-2 | `/chay-phien-code chuoi=gd3-1 W17-3` | — | ⬜ Chưa bắt đầu |  |
| `W17-4` | WP-17 | F4 QA (domain invariant mọi luật tiền/tồn — K-06) → F5 REV | qa · rev | W17-3 | `/chay-phien-code chuoi=gd3-1 W17-4` | — | ⬜ Chưa bắt đầu |  |
| `W17-5` | WP-17 | F6 OPS + G6 `-DayDu` ⇒ đẩy fast-forward vào `tich-hop/gd3`; **M4 trước release** | ops · ORCH | W17-4 | `/chay-phien-code chuoi=gd3-1 W17-5` | — | ⬜ Chưa bắt đầu |  |
<!-- BANG-PHIEN:KET-THUC -->
