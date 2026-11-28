# Testplan `W17-q` — 28/11/2026

> **Hàng `W17-q` — nợ `D-217` ④ (QA):** sửa `tools/probe-cong-tai-lieu.ps1` để đọc **biên bản duyệt của NGƯỜI ở GĐ1**
> (`docs/bien-ban-duyet-gd1-*.md`: dòng `Hình thức: DUYỆT` + bảng có cột `Mã`) làm **nguồn ĐẠT cho CẢ `Code` và
> `MergeMain`**, thêm ca `-TuKiem` mới + ca ngược. Chuỗi `gd3-1`, phiên `N112`, nhánh `feat/gd3-wp17-gia-von`.
>
> - Căn cứ: `D-217` ④ (`docs/nhanh/decisions.md` dòng `| D-217 |`) + `D-318` ①(8) (phiên uỷ quyền `U-17`,
>   `C8=(c)`: giữ `W17-q` đúng phạm vi `D-217` ④, **KHÔNG** mở rộng cho `REP-003`).
> - Lý do (đỏ oan hôm nay): `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` = **exit 1**
>   (`PRC-001: KHONG thay dong trong bien ban tam duyet`) trong khi `PRC-001` **đã được chủ dự án duyệt thật** ở
>   `Q5` — `docs/bien-ban-duyet-gd1-2026-10-29.md` §1 (`Hình thức: DUYỆT`) + §2.2 dòng 4.
> - **Không có SRS / tài liệu feature** cho hàng hạ tầng này ⇒ **không có `BR-*`**; dùng bảng yêu cầu
>   **`R-01…R-08`** dưới đây. 📌 **Không chạy** `check-agent-artifact.ps1 -Kind testplan -Srs …` (script đòi `-Srs`
>   có `BR` ở §1) — tiền lệ `docs/tests/WP-15-testplan.md`. Thay bằng **Negative control** + số đo thật ở §3–§4.
> - Vùng QA sở hữu: `test/**`, `tools/probe-*.ps1`, `docs/tests/**`. **Không** sửa `tools/test-cong-kiem.ps1`
>   (`Y9`/cổng tĩnh thuộc `ops` — cập nhật ngay sau phiên này), `docs/features/**`, `docs/nhanh/**`, `.claude/**`;
>   **không xoá tệp gương** (ORCH xoá cùng commit khi đóng phiên).

## 1. Phủ yêu cầu (R-01…R-08)

| Mã | Yêu cầu (theo D-xx) | Test / bằng chứng | Tầng | Trạng thái |
|---|---|---|---|---|
| **R-01** | `D-217` ④: `Code` đọc `docs/bien-ban-duyet-gd1-*.md` (dòng chứa `Hình thức`+`DUYỆT` + bảng cột `Mã`) làm nguồn ĐẠT — mã có trong GD1 ⇒ `0`, **bỏ qua** kiểm tam-duyet cho mã đó | Hàm `Doc-BienBanGd1` (`tools/probe-cong-tai-lieu.ps1:192`); nhánh `Code` `:467-468`; `-TuKiem` **CA9**; thật: `-CheDo Code -WP WP-17` = **0** (trước sửa = **1**) | Probe logic + runtime | ✅ |
| **R-02** | `D-217` ④: `MergeMain` cũng nhận GD1 là nguồn ký NGƯỜI — mã có trong GD1 ⇒ `0`; lỗi cứng "không có biên bản người ký" chỉ thêm khi **không có nguồn ký nào** | Nhánh `MergeMain` `:536`; `-TuKiem` **CA10**; thật: `-CheDo MergeMain -WP WP-17` = **0** (trước = **1**); `-WP WP-16` = **0** | Probe logic + runtime | ✅ |
| **R-03** | Fail-closed, **không nới kiểm**: mã KHÔNG có trong bất kỳ biên bản nào vẫn ĐỎ ở **CẢ HAI** chế độ; tệp `bien-ban-duyet-gd1-*` **không** có dòng `Hình thức: DUYỆT` không tính là nguồn | `-TuKiem` **CA11** (Code + MergeMain = 1), **CA12** (GD1 thiếu Hình thức ⇒ Code = 1); thật: `MergeMain WP-20`/`WP-21` = **1** (`SHP-001`/`SHP-002` không có trong GD1) | Probe logic + runtime | ✅ |
| **R-04** | `D-318` ①(8): **KHÔNG** mở rộng cho `REP-003`; chỉ đọc biên bản NGƯỜI GĐ1 `docs/bien-ban-duyet-gd1-*.md`, không đụng `rep-003` | Glob cố định `bien-ban-duyet-gd1-*.md` trong `Doc-BienBanGd1` (`:200`); `git diff --stat` chỉ chạm `tools/probe-cong-tai-lieu.ps1` + `docs/tests/W17-q-testplan.md` | Kiểm phạm vi | ✅ |
| **R-05** | Tệp gương `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25` (đuôi md, đã xoá) **không còn cần** sau khi xoá (WP-16 phải xanh nhờ GD1, không nhờ gương) | **ĐÃ XOÁ** (`git rm` cùng commit `ba36bee`). Sau xoá: `Code WP-16` = **0** (`PRC-002: duyệt GĐ1 (nguồn NGƯỜI) ⇒ đạt`), `MergeMain WP-16` = **0** ⇒ xoá gương **không** làm đỏ; WP-16 xanh nhờ GD1 | Runtime | ✅ |
| **R-06** | `-TuKiem` phủ mọi nhánh mới, mọi ca DAT, ngưỡng không giảm | `-TuKiem` = **15/15 DAT**, exit **0** (trước = 10/10, ngưỡng ≥8 → nay ≥12) | Probe logic | ✅ |
| **R-07** | Không hồi quy: `Code WP-18/20/21` = 0, `MergeMain WP-15` = 0 (như trước sửa) | §4 | Runtime | ✅ |
| **R-08** | `Y9` / `tools/test-cong-kiem.ps1` do `ops` cập nhật — **QA KHÔNG sửa** | `git diff` không chạm `tools/test-cong-kiem.ps1`; **`ops` ĐÃ cập nhật `Y9`**: `-ChiKiemMoc` = **54/54 DAT · 0 HỎNG**, `Y9` DAT (7 exit: TuKiem 0 · Code auto 0 · MergeMain WP-17 0 · Code WP-16 0 · MergeMain WP-16 0 · MergeMain WP-15 0 · MergeMain WP-20 1 mong đợi 1); `test-cong-kiem` đầy đủ = **60/60 DAT · 0 HỎNG** | Phạm vi | ✅ |

- Tổng yêu cầu: **8** · đã có bằng chứng (phần QA): **8** · chưa: **0**.

## 2. Số ca theo tầng (K-06)

| Tầng | Trước | Sau | Ghi chú |
|---|---|---|---|
| Probe logic (`-TuKiem`) | **10 ca** (display); ngưỡng code cũ `≥ 8` | **15 ca**; ngưỡng `≥ 12` | +**CA9** (GD1 ⇒ Code `0`), +**CA10** (GD1 ⇒ MergeMain `0`), +**CA11** ×2 (GD1 thiếu mã ⇒ Code/MergeMain `1`), +**CA12** (GD1 thiếu `Hình thức: DUYỆT` ⇒ không tính nguồn ⇒ Code `1`) |
| Probe runtime (repo thật) | `Code WP-17` = **1** · `MergeMain WP-17` = **1** | `Code WP-17` = **0** · `MergeMain WP-17` = **0** | GD1 là nguồn NGƯỜI (`D-217` ④) |
| Application / API / UI / Domain | — | **N/A** | Hàng hạ tầng **chỉ** sửa 1 probe `.ps1`; 0 `.cs` sản phẩm, 0 endpoint, 0 DTO, 0 màn hình ⇒ không có tầng Application/API/UI/Domain mới để phủ |

> 📌 Task mô tả "trước 8" là **ngưỡng** trong code; số ca **đếm thật** của `-TuKiem` trước khi sửa là **10** (CA2
> và CA3 mỗi ca 2 lần gọi), sau khi sửa là **15**. Số ca **chỉ tăng**.

## 3. Negative control (bắt buộc)

> 🚨 `tools/agents/negative-control.ps1` **từ chối tệp chưa commit** (`exit 3`). Vì `tools/probe-cong-tai-lieu.ps1`
> đang **dirty** (chưa commit), QA làm **negative control tay** trước commit (sao lưu byte + sha256 → đột biến →
> chạy → khôi phục → so hash). Sau khi ORCH commit nhánh, chạy lại bằng script (**NC-N112-1** dưới đây).

### 3.1 Đột biến canonical — tắt nguồn GD1 (ORCH chạy lại sau commit)

| # | Đột biến | Tệp:dòng | Test | Khi đột biến | Sau khôi phục |
|---|---|---|---|---|---|
| **NC-N112-1** (tay, trước commit) | Tắt nguồn GD1: đổi glob `-Filter 'bien-ban-duyet-gd1-*.md'` → `-Filter 'bien-ban-duyet-gd1-KHONGCO-*.md'` (hàm `Doc-BienBanGd1` trả rỗng) | `tools/probe-cong-tai-lieu.ps1:200` | `pwsh -NoProfile -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | **exit 1** — `CA9`/`CA10` **DO**, **13/15** DAT | **exit 0** — **15/15** DAT; sha256 khôi phục = bản sửa `9ac19255…` |

### 3.2 Đột biến biến thể — bỏ nhánh GD1 ở `Code` (đã chạy, bằng chứng phụ)

| # | Đột biến | Tệp:dòng | Test | Khi đột biến | Sau khôi phục |
|---|---|---|---|---|---|
| **NC-N112-1b** (tay, trước commit) | Bỏ nhánh GD1 ở `Code`: `if ($gd1.ContainsKey($ma)) {` → `if ($false) {` | `tools/probe-cong-tai-lieu.ps1:468` | `pwsh -NoProfile -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | **exit 1** — `CA9` **DO**, **14/15** DAT | **exit 0** — **15/15** DAT; sha256 khôi phục = bản sửa `9ac19255…` |

### 3.3 sha256 (thật, 28/11/2026)

| Trạng thái tệp | sha256 |
|---|---|
| Bản **gốc TRƯỚC** sửa | `3d131f17fa016c2bf28c456455503f510c590d0fc8f214ea3ec9ebda3e6e84aa` |
| Bản **đã sửa** (giữ làm chuẩn) | `9ac1925592754e3819c98132d3bff5880a39b105e3e68099fbb7cf144866d4d0` |
| Bản đột biến **NC-N112-1** (tắt glob GD1) | `b78ddb33ff8b3444713c72a843f85842af3304839cf47763b80b2b19f0127771` |
| Bản đột biến **NC-N112-1b** (`if ($false)`) | `7c479b7062cefc7443334a2714d13ddd3e24cae4e376b2e35b47395296fe6bcb` |
| Sau **khôi phục** (cả hai) | `9ac1925592754e3819c98132d3bff5880a39b105e3e68099fbb7cf144866d4d0` (= bản sửa, khớp) |

### 3.4 Lệnh ORCH chạy **sau commit** (tệp đã sạch ⇒ script không còn `exit 3`)

```powershell
pwsh -File tools/agents/negative-control.ps1 -File tools/probe-cong-tai-lieu.ps1 `
  -Find "-Filter 'bien-ban-duyet-gd1-*.md'" `
  -Replace "-Filter 'bien-ban-duyet-gd1-KHONGCO-*.md'" `
  -TestCommand "pwsh -NoProfile -File tools/probe-cong-tai-lieu.ps1 -TuKiem" `
  -Testplan docs/tests/W17-q-testplan.md -Id NC-N112-1
```

> - Chuỗi `-Find` xuất hiện **đúng 1 lần** (`:200`) — thoả điều kiện "phải xuất hiện đúng MỘT lần" của
>   `negative-control.ps1` (nếu dùng `if ($gd1.ContainsKey($ma)) {` sẽ **fail** vì xuất hiện 2 lần `:468`/`:536`).
> - Hôm nay (tệp chưa commit) script trả **exit 3**; đây là **bằng chứng tay tương đương** ở §3.1–§3.3.

### 3.5 Sau commit — chính thức (script `negative-control.ps1`)

> ✅ **`NC-N112-1` = KET QUA: DAT** (tệp đã commit `ba36bee`). Đột biến ⇒ **exit 1** (log
> `artifacts/negative-control/20261128-080852-do.log`) → khôi phục ⇒ **exit 0** (log
> `artifacts/negative-control/20261128-080852-xanh.log`). Script đã tự thêm dòng `NC-N112-1` (ngày 2026-11-28) vào
> bảng "Negative control" cuối tệp — **giữ nguyên**.

## 4. Số đo thật (28/11/2026, nhánh `feat/gd3-wp17-gia-von`)

| Lệnh | Trước sửa | Sau sửa |
|---|---|---|
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | exit **0** · **10/10** ca DAT | exit **0** · **15/15** ca DAT |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` | exit **1** (`PRC-001: KHONG thay dong…`) | exit **0** (`PRC-001: duyệt GĐ1 (nguồn NGƯỜI) ⇒ đạt`) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo MergeMain -WP WP-17` | exit **1** (`KHONG co bien ban nguoi ky…`) | exit **0** (GD1 là nguồn ký NGƯỜI) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-18` | exit **0** | exit **0** (không hồi quy) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-20` | exit **0** | exit **0** (không hồi quy) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-21` | exit **0** | exit **0** (không hồi quy) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo MergeMain -WP WP-15` | exit **0** | exit **0** (không hồi quy) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` | exit **0** (nhờ **tệp gương**) | exit **0** (`PRC-002: duyệt GĐ1` ⇒ **không cần gương**; ORCH đo lại sau `git rm`) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo MergeMain -WP WP-16` | exit **1** | exit **0** (`PRC-002: duyệt GĐ1`) |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo MergeMain -WP WP-20` | (không đo) | exit **1** — `SHP-001` **không** trong GD1 ⇒ **cổng vẫn chặt** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo MergeMain -WP WP-21` | (không đo) | exit **1** — `SHP-002` **không** trong GD1 ⇒ **cổng vẫn chặt** |
| Parse cú pháp `[Parser]::ParseFile` | — | **0 lỗi** |

- Đột biến canonical (NC-N112-1): **exit 1**, `13/15` (CA9 + CA10 DO); khôi phục: **exit 0**, `15/15`.
- Đột biến biến thể (NC-N112-1b): **exit 1**, `14/15` (CA9 DO); khôi phục: **exit 0**, `15/15`.
- `git diff --stat` sau khôi phục: chỉ chạm `tools/probe-cong-tai-lieu.ps1` (+`docs/tests/W17-q-testplan.md` mới);
  `grep 'if ($false) {'` rỗng ⇒ **không còn dấu vết đột biến**.

### 4.1 Sau commit — ORCH đo lại (commit `ba36bee`)

| # | Lệnh | Kết quả |
|---|---|---|
| (1) | `probe -CheDo Code -WP WP-16` (sau `git rm` gương) | **exit 0** — `PRC-002: duyệt GĐ1 (nguồn NGƯỜI) ⇒ đạt`; `-CheDo MergeMain -WP WP-16` = **exit 0** ⇒ xoá gương **không** làm đỏ |
| (2) | `tools/agents/negative-control.ps1 -Id NC-N112-1` | **KET QUA: DAT** — đột biến **exit 1** (log `artifacts/negative-control/20261128-080852-do.log`) → khôi phục **exit 0** (log `...-xanh.log`); đã append dòng vào bảng "Negative control" cuối tệp |
| (3) | `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | **exit 0 · 54/54 DAT · 0 HỎNG** · `Y9` DAT (7 exit: TuKiem 0 · Code auto 0 · MergeMain WP-17 0 · Code WP-16 0 · MergeMain WP-16 0 · MergeMain WP-15 0 · MergeMain WP-20 1 mong đợi 1) |
| (4) | `pwsh -File tools/test-cong-kiem.ps1` (đầy đủ, qua `kiem-tat-ca.ps1`) | **60/60 DAT · 0 HỎNG** |
| (5) | `pwsh -File tools/kiem-tat-ca.ps1` (nhẹ) | **exit 0 · 10/10 bước · 368,3 s** (build 76,1 s · unit 23,1 s · integration 41,3 s · cổng tài liệu 0,8 s · test cổng 60/60) |

## 5. Chưa phủ / nợ (ghi rõ chủ)

1. **ORCH (đóng phiên) — ĐÃ XONG:** `git rm` tệp gương `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25` (đuôi md) cùng commit
   `ba36bee`; sau xoá `Code WP-16` = **0** (`PRC-002: duyệt GĐ1`) và `MergeMain WP-16` = **0** (§1 R-05, §4.1).
2. **ORCH (sau commit) — ĐÃ XONG:** `negative-control.ps1 -Id NC-N112-1` = **DAT** — đột biến exit **1** → khôi
   phục exit **0** (log `artifacts/negative-control/20261128-080852-do.log`/`-xanh.log`); dòng bằng chứng đã append
   cuối tệp (§3.5, §4.1).
3. **`ops` (ngay sau phiên) — ĐÃ XONG:** `ops` đã cập nhật `Y9` (`tools/test-cong-kiem.ps1`) gọi
   `probe-cong-tai-lieu.ps1 -TuKiem`; `-ChiKiemMoc` = **54/54 DAT · 0 HỎNG**, `test-cong-kiem` đầy đủ **60/60 DAT**.
   QA **không** sửa tệp `ops`.
4. **`check-agent-artifact.ps1 -Kind testplan`:** **không áp dụng** — hàng `W17-q` không có SRS/`BR` (đòi `-Srs`
   có `BR` ở §1); thay bằng Negative control + số đo thật §3–§4 (tiền lệ `WP-15-testplan.md`).

## Negative control

> ORCH chạy `tools/agents/negative-control.ps1 -Id NC-N112-1` (lệnh ở §3.4) **sau khi commit**; script tự ghi một
> dòng bằng chứng vào bảng dưới đây. Bằng chứng **tay trước commit** đầy đủ ở §3.1–§3.3.

| Ngay | Ma | Tep:dong | Dot bien | Lenh test | Bang chung |
|---|---|---|---|---|---|
| 2026-11-28 | NC-N112-1 | tools/probe-cong-tai-lieu.ps1:200 | -Filter 'bien-ban-duyet-gd1-*.md' -> -Filter 'bien-ban-duyet-gd1-KHONGCO-*.md' | pwsh -NoProfile -File tools/probe-cong-tai-lieu.ps1 -TuKiem | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |

