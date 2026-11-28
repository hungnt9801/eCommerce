# Handover N112 — WP-17 / hàng `W17-q` (nợ `D-217` ④) XONG — probe đọc biên bản NGƯỜI GĐ1, xoá tệp gương

> Phiên `N112` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-q` (loại `code`, vai `qa`, bước `F4 QA — nợ `D-217` ④`) · WP-17 "giá vốn" · mã `PRC-001` ·
> nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: `tools/probe-cong-tai-lieu.ps1` đọc `docs/bien-ban-duyet-gd1-*.md` (dòng `Hình thức: DUYỆT` + bảng cột `Mã`)
> làm nguồn ĐẠT cho **cả `Code` và `MergeMain`**; `-TuKiem` CA9–CA12; `Y9`; **xoá** tệp gương cùng commit.
> **0 `.cs` sản phẩm · 0 tệp FE · 0 migration · 0 `pg_dump`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| Sửa probe | `qa` ×1 | `tools/probe-cong-tai-lieu.ps1`: hàm `Doc-BienBanGd1` (glob `bien-ban-duyet-gd1-*.md`, đòi dòng chứa **cả** `Hình thức` + `DUYỆT`, bảng cột `Mã`; fail-closed bỏ tệp nếu thiếu); nhánh `Code` + `MergeMain` nhận GD1 là nguồn ĐẠT; `-TuKiem` thêm **CA9–CA12** (GD1 ⇒ 0 ở 2 chế độ; thiếu mã ⇒ 1; thiếu `Hình thức` ⇒ 1), ngưỡng ≥ 8 → **≥ 12**. **Không** mở rộng `REP-003` (`D-318` ①(8)) |
| Testplan | `qa` ×1 | `docs/tests/W17-q-testplan.md` (mới): 8 yêu cầu `R-01…R-08`, số ca 10 → 15, negative control tay trước commit + bảng NC chính thức sau commit |
| Cập nhật `Y9` | `ops` ×1 | `tools/test-cong-kiem.ps1`: `Y9` thêm `MergeMain WP-17` = 0, `Code`/`MergeMain WP-16` = 0 (chứng minh xoá gương không đỏ), ca âm `MergeMain WP-20` = 1; không nới khẳng định cũ |
| Xoá tệp gương | ORCH | `git rm docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25.md` (cùng commit `ba36bee`) |
| Sửa tham chiếu chết | ORCH | `kiem-link` FAIL 9 tham chiếu tới tệp gương đã xoá (`decisions.md`, `hang-doi-xem-lai.md`, `bang-phien-gd3-1.md`, handover n71/n72/n73/n76/n111) ⇒ bỏ đuôi `.md` khỏi tên tệp (giữ nghĩa lịch sử); registry `noiDung` + `PH sync` |

**Baseline mở phiên (`N112`):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 Error · 9 Warning** |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2650 passed · 0 failed · 0 skipped** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS · **1940** đường dẫn backtick · 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | **214** khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | 1 | **53/54** — `Y9` HỎNG vì `Code` repo thật = 1 (`PRC-001`, đúng nợ) |

**DoD của hàng — ORCH tự chạy (không tin số vai báo):**

| Cổng | Lệnh | Số đo |
|---|---|---|
| Probe logic | `pwsh -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | **exit 0 · 15/15 DAT** (trước 10/10) |
| Đỏ oan đã hết | `… -CheDo Code -WP WP-17` | **exit 0** (trước **1**) — `PRC-001: duyệt GĐ1 (nguồn NGƯỜI) ⇒ đạt` |
| MergeMain nhận GD1 | `… -CheDo MergeMain -WP WP-17` | **exit 0** (trước **1**) |
| Xoá gương không đỏ | `… -CheDo Code -WP WP-16` / `-CheDo MergeMain -WP WP-16` | **0 / 0** (`PRC-002` nhờ GD1) |
| Không hồi quy | `Code WP-18/20/21` · `MergeMain WP-15` | **0 / 0 / 0 · 0** |
| Cổng vẫn chặt | `MergeMain WP-20` / `WP-21` | **1 / 1** (`SHP-001`/`SHP-002` không có trong biên bản nào) |
| Cổng tĩnh | `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | **exit 0 · 54/54 DAT** (trước 53/54) |
| Cổng tĩnh đầy đủ | `pwsh -File tools/test-cong-kiem.ps1` (qua `kiem-tat-ca`) | **exit 0 · 60/60 DAT** |
| G3 | `pwsh -File tools/kiem-tat-ca.ps1` | **exit 0 · 10/10 bước · 368,3 s** — build 76,1 s · unit 23,1 s · integration 41,3 s · cổng tài liệu 0,8 s · test cổng 60/60 |
| Negative control | `negative-control.ps1 -Id NC-N112-1` | **DAT** — đột biến **exit 1** (13/15) → khôi phục **exit 0** (15/15); log `artifacts/negative-control/20261128-080852-do.log` / `…-xanh.log` |
| Tài liệu | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** — 318 tệp · 1029 link · **1936** đường dẫn · 0 chết |
| Mermaid | `npm --prefix tools run check:mermaid` | **214 khối · 0 lỗi** |
| Hàng rào hook | `node --test .claude/hooks/test/hooks.test.mjs` | **49/49** |
| Artifact quyết định | `check-agent-artifact.ps1 -Kind decision` | **DAT** (113 dòng) |
| Testplan | `check-agent-artifact.ps1 -Kind testplan` | **không áp dụng** — hàng hạ tầng không có SRS/`BR` (tiền lệ `WP-15-testplan.md`); thay bằng NC + số đo |
| Bảng phiên | `PH check` | **OK: bang khop registry** |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W17-q`.** DoD đạt toàn bộ; tệp gương đã xoá; link PASS.

## 3. Migration đã áp

**Không có.** 0 `.cs`, 0 migration, không chạm DB ⇒ không `pg_dump` (K-01 không kích hoạt).

## 4. Quyết định mới

- **Không có `D-xx` mới.** `W17-q` thi hành đúng `D-217` ④ trong phạm vi `D-318` ①(8) (không mở `REP-003`).
- Chỉ sửa **chữ** của `D-217` ①: bỏ đuôi `.md` khỏi tên tệp gương đã xoá (nội dung quyết định không đổi; dòng vẫn `TẠM CHỐT (DEC)`, vẫn chờ người xem lại).

## 5. Nợ / rủi ro

- 🟢 **`D-217` ④ ĐÓNG** (đã thi hành + kiểm bằng máy). `D-217` **vẫn `TẠM CHỐT (DEC)`** — việc người xem lại quyết định (hàng đợi) **không** đổi.
- 🟠 **Rủi ro mới (thấp, có chủ):** probe nay coi **mọi mã** trong bảng cột `Mã` của tệp `bien-ban-duyet-gd1-*.md` có dòng `Hình thức…DUYỆT` là ĐẠT cho **cả hai** chế độ. Một mã ghi nhầm vào biên bản NGƯỜI sẽ mở cổng — nhưng biên bản NGƯỜI là tệp ký cố định; fail-closed vẫn bỏ tệp thiếu `Hình thức`. Không có mã `REP-003` trong GD1.
- 🔴 **`REP-003` vẫn đỏ cứng**: chữ ký người thật ở `W17-0` (kiểu (c), `D-318` ①(3)), ORCH đối chiếu diff khớp 2 vị trí; lệch ⇒ chữ ký vô hiệu.
- 🟡 **9 tài liệu lịch sử** (`decisions`, `hang-doi`, `bang-phien`, handover n71/n72/n73/n76/n111) đã bỏ đuôi `.md` khỏi tên tệp gương đã xoá — chỉ là tên, không phải đường dẫn; nghĩa lịch sử giữ nguyên.
- 🟡 **`D-318` G1/G2** vẫn chờ `decider` duyệt thay ở `W17-0`/`W17-1` (đủ tiêu chí C5/C4).
- 🟡 **Nợ kế thừa WP-23** (không thuộc phiên): P2 retry storm (`D-263`), `D-317` ca Host boot, NCC/HMAC (`R3-6`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-q` (nợ `D-217` ④) → mở `W17-0` (doc-gate WP-17).**

- `W17-0`: `ba` sửa `prc-001` §15.1/§20.4 + `inv-001` §0 + `rep-003` §0/Q11 **cùng nhánh** → `docrev`; G1 của `prc-001`/`inv-001` do `decider` duyệt thay theo `D-318` (đủ C5).
- `W17-0n` (hàng 👤): ORCH đối chiếu diff `REP-003` khớp **2 vị trí** đã ký; lệch ⇒ chữ ký vô hiệu; tạo `docs/bien-ban-duyet-gd3-wp17-<ngày>.md`.
- `W17-1`: ARCH động cơ giá vốn (G2 do `decider` duyệt thay, đủ 7 điều kiện C4).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ **`5c902ed`**.
- Commit việc: **`ba36bee`** (`fix(probe): N112 - W17-q ...`), gồm probe + `Y9` + testplan + xoá tệp gương.
- Commit đóng phiên: **`git log -1`** (sửa 9 tham chiếu chết + testplan §3.5/§4.1 + `07` §13 + handover + registry).
- **Đã push** `origin/feat/gd3-wp17-gia-von`. **Chưa** đẩy `tich-hop/gd3` (chỉ `dong-wp` mới FF), **không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

---

**Hàng rào agent** (phiên `N112`): `qa` ×2 (thi công probe/testplan + điền số đo sau commit) · `ops` ×1 (`Y9`). **3 cảnh báo** `artifacts/agents/canh-bao.jsonl` (qa ×2, ops ×1) — đều "thiếu khối JSON cuối" do hook không đọc được transcript, nhưng **cả ba báo cáo đều có khối JSON đầy đủ** ở cuối; ORCH kiểm lại mọi số đo bằng máy. **0 tệp ngoài vùng**; **0 lần hook chặn dừng**. (Cảnh báo `decider` lúc `00:28Z` là của phiên `N111`/`U-17`, trước phiên này.)
