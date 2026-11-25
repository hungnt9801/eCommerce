# Testplan `WP-15` — 25/11/2026

> **WP-15 "Nền GĐ3 + 2 nợ `D-71` ④"** — nhánh `feat/n69-wp15-nen-gd3`, phiên `N69`.
> **Không có SRS / tài liệu feature**: hàng `WP-15` trong `docs/nhanh/07-ke-hoach-trien-khai.md` (:745) ghi
> **"Không có tài liệu feature"** ⇒ không có `BR-*`; dùng bảng yêu cầu **`R-01…R-10`** dưới đây.
> 📌 Vì không có SRS, **không chạy** `check-agent-artifact.ps1 -Kind testplan -Srs …` (script đòi `-Srs` và
> đòi SRS có `BR` ở §1) — thay bằng mục **Negative control** + số đo thật ở §3–§4.

- Căn cứ: `D-210`…`D-214` (TẠM CHỐT `docs/nhanh/decisions.md`), `D-71` ④, `D-209` ①⑥, `D-67`/`D-68` (C10),
  `D-66` ①⑦ (hợp đồng `409`), `D-115` ⑤ (biên bản người ký).
- Vùng QA sở hữu: `test/**`, `tools/probe-*.ps1`, `docs/tests/**`. **Không** sửa `tools/test-cong-kiem.ps1`,
  `tools/kiem-tat-ca.ps1`, `.github/**`, `docs/ops/**` (OPS sở hữu, cập nhật **ngay sau** phiên này).

## 1. Phủ yêu cầu (R-01…R-10)

| Mã | Yêu cầu (theo D-xx) | Test / bằng chứng | Tầng | Trạng thái |
|---|---|---|---|---|
| **R-01** | `D-210`: `ViPhamRangBuocDb.MaXungDot == "eCommerce:Db:0002"` **VÀ** `!= MaChung` (`0001`) | `test/eCommerce.Tests/Inventory/ViPhamRangBuocDbTests.cs` · `Ma_xung_dot_la_0002_va_KHONG_trung_ma_chung_0001` | Unit (Kernel) | ✅ |
| **R-02** | `D-210`: `LaXungDotDuLieu` nhận `AbpDbConcurrencyException` **lồng trong `Exception`** | `…Tests.cs` · `LaXungDotDuLieu_nhan_AbpDbConcurrencyException_long_trong_Exception` | Unit (Kernel) | ✅ |
| **R-03** | `D-210`: `LaXungDotDuLieu` nhận xung đột **lồng trong `DbUpdateException`** (đường `SaveChangesAsync` thật) | `…Tests.cs` · `LaXungDotDuLieu_nhan_AbpDbConcurrencyException_long_trong_DbUpdateException` | Unit (Kernel) | ✅ |
| **R-04** | `D-210`: `LaXungDotDuLieu` trả **`false`** cho `PostgresException` (`23505`/`40001`) và `null`; `Exception` thường không có xung đột ⇒ `false` | `…Tests.cs` · `LaXungDotDuLieu_KHONG_nhan_PostgresException_va_null` + `LaXungDotDuLieu_dung_o_dung_khi_tim_thay__khong_nham_Exception_thuong` | Unit (Kernel) | ✅ |
| **R-05** | `D-213`: `C10` chờ **tiền đề đã commit** — `DLQ (LyDo = HaWatermark) ≥ 1` qua `$mauDemDlqLyDo`, **cùng `$GiayToiDa ≤ 30`**, **MỘT** lần chờ; bỏ `Cho-DieuKien` trùng của `C13` | `tools/probe-don-san.ps1:510-567` (khối ⑤) + runtime `C10` | Probe + runtime | ✅ (xem §4) |
| **R-06** | `D-213`: **giữ nguyên** `Kq 'C10…' ($tongA -eq 1 -and $lateA -eq 1)`, `$chanDoanA`, dòng `[CD-D67]`, đủ **4 neo** `[MP-EVENTLOG-DA-GHI]`/`GHI-HONG`/`TRUNG`/`THATBAI-HONG`; **không** nới 30 s, **không** bỏ ca | `grep` 4 neo + `C10` runtime | Probe + runtime | ✅ |
| **R-07** | `D-215` ①(1) (thay ① `D-214`): giữ `$script:bodyXungDot` = body **cuối**; thêm `$script:soLan409` (mọi 409), `$script:coMaDb0002` + `$script:bodyDb0002` (body **đầu tiên** có `error.code = eCommerce:Db:0002`), `$script:coMaRong` (code null/rỗng **hoặc** không parse), `$script:dsMa409`; hằng `$MA_KHOA_PHAN_TAN = 'eCommerce:Lock:0001'` **chỉ chẩn đoán**; **không đổi nghĩa `$script:soXungDot`** | `tools/probe-giu-cho-2instance.ps1:74-130` (`Ghi-Nhan409`), gọi ở 2 chỗ 409 | Probe | ✅ |
| **R-08** | `D-215` ①(2): `P15` **bốn nhánh** — (i) `soXungDot = 0` ⇒ `CHUA CHAY` + `soCaChuaChay++` (giữ nhánh `if ($script:soXungDot -gt 0)` để `Y7` quét); (ii) `coMaRong` ⇒ **ĐỎ** (`Kq` nhận `$false`) + in body ≤ 200 ký tự + `dsMa409`; (iii) `coMaDb0002` + `error.message` body đó khác rỗng ⇒ **`DAT`**; (iv) còn lại (mã khác rỗng nhưng không `Db:0002`, vd `Lock:0001`) ⇒ **`CHUA CHAY`** + đếm riêng, in `CHUA CHAY (N lan 409, ma = [...], khong phai xung dot DB)`, có `Lock:0001` ⇒ in `nghi khoa giu qua 30 s — xem P07`. `CHUA CHAY` **không** tính `DAT`, **không** tính `HONG`. Câu tổng kết cuối nêu **hai** lý do + số 409 + danh sách mã | `probe-giu-cho-2instance.ps1:451-500` + runtime `P15` | Probe + runtime | ✅ runtime **`DAT`** (§4) |
| **R-09** | `D-211`: `tools/probe-cong-tai-lieu.ps1` **MỚI** với `-CheDo Code\|MergeMain`, `-WP`, `-Goc`, `-TuKiem`; mã thoát `0` xanh · `1` vi phạm · `2` không xác định (fail-closed) | `-TuKiem` **8/8** · `-CheDo Code` **exit 0** · `-CheDo MergeMain` **exit 0** | Probe logic | ✅ |
| **R-10** | `D-212`: suy WP (`-WP` > `GITHUB_HEAD_REF` > `GITHUB_REF_NAME` > git branch; regex; ngoài `15…34` ⇒ `2`; không suy được ⇒ diff chạm mã nguồn = `1`, chỉ tài liệu = `0`); ánh xạ WP→mã từ `07`; luật ĐỎ Code (a)(b) + MergeMain (c); `README` §2 chỉ **CẢNH BÁO**, không đổi màu; **WP-15 XANH ở cả hai** | `-TuKiem` CA1–CA6 + chạy thật 2 chế độ | Probe logic | ✅ |

- Tổng yêu cầu: **10** · đã có bằng chứng: **10** · chưa: **0**.

## 2. Số ca theo tầng (K-06)

| Tầng | Trước | Sau | Ghi chú |
|---|---|---|---|
| Domain invariant / Unit (Kernel) | 1961 (toàn bộ `eCommerce.Tests`) | **1966** | +5 `Fact` trong `ViPhamRangBuocDbTests` (`D-210`); 0 fail |
| Probe logic (`-TuKiem`) | 0 | **8 ca** (CA1–CA6, CA2 và CA3 mỗi ca 2 chế độ) | là **ca ĐỎ/XANH** của chính cổng tài liệu (`D-211` ①) |
| Cổng tĩnh (`test-cong-kiem.ps1`) | Y7 DAT · Y8 DAT | **Y7 DAT · Y8 DAT** | OPS **đã cập nhật** `Tim-ViPhamChanDoanC10`/`Y8` (`D-213`), mở rộng `Y7` (`D-214` ①) **và thêm 3 ràng buộc + chiều ngược 5 của `D-215` ①(3)**. Log thật: **`test-cong-kiem` 60/60 DAT**, **`-ChiKiemMoc` 54/54 DAT** (`Y7`/`Y8`/`Y9` DAT). QA **không sửa** tệp OPS. |
| Probe runtime (`P15`) | **`CHUA CHAY`** (0 lần `409`) | **`DAT`** | Lượt `-DayDu` 25/11/2026 (**20/20, 836,4 s**) thấy `409` thật `{"error":{"code":"eCommerce:Db:0002","message":"Dữ liệu vừa bị người khác thay đổi…"}}` ⇒ **`DAT`** (chấm dứt `CHUA CHAY`). Cơ chế `D-215` vẫn phân biệt: `Lock:0001`/0 lần 409 ⇒ `CHUA CHAY`; `code` rỗng ⇒ **ĐỎ**. |
| Probe runtime (`C10`) | — | **1 ca** | xem §4 |
| Application / API `/api/v1` / UI | — | **N/A** | WP-15 **không** thêm app service, endpoint, DTO hay màn hình; BE chỉ thêm 2 hằng + 1 hàm thuần + 1 nhánh `if` trong exception filter ⇒ không có tầng Application/API/UI mới để phủ. Hợp đồng `409` được đo **runtime** ở `P15`. |

## 3. Negative control (bắt buộc)

> 🚨 `tools/agents/negative-control.ps1` **chưa chạy được** trong phiên: nó **từ chối tệp chưa commit**
> (`exit 3`) — `tools/probe-cong-tai-lieu.ps1` đang **untracked**, `ViPhamRangBuocDbTests.cs` đang **dirty**.
> Bằng chứng đỏ→xanh **trước commit** = đột biến tay trên tệp QA (dưới đây, có khôi phục + so hash) +
> các ca `-TuKiem` vốn dĩ là **chiều ngược** dựng trong fixture. Sau khi **ORCH commit nhánh**, chạy lại:
>
> ```powershell
> pwsh -File tools/agents/negative-control.ps1 -File tools/probe-cong-tai-lieu.ps1 `
>   -Find "if (`$giaTri[0].StartsWith('Tạm duyệt')) {" -Replace "if (`$true) {" `
>   -TestCommand "pwsh -NoProfile -File tools/probe-cong-tai-lieu.ps1 -TuKiem" `
>   -Testplan docs/tests/WP-15-testplan.md -Id NC-1
> ```

| # | Đột biến | Tệp:dòng | Test | Khi đột biến | Sau khôi phục |
|---|---|---|---|---|---|
| **NC-1** | Bỏ nhánh `Chưa`: `if ($giaTri[0].StartsWith('Tạm duyệt'))` → `if ($true)` (mã `Chưa` bị coi là đạt) | `tools/probe-cong-tai-lieu.ps1:411` | `pwsh -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | **exit 1** (đỏ — CA1 báo `DO`) | **exit 0** (xanh — 8/8 `DAT`; sha256 khôi phục = bản gốc `66eab23…`) |
| **NC-2** | Fixture chiều ngược **dựng sẵn** trong `-TuKiem`: CA1 (mã `Chưa` + WP đang code) và CA4 (không suy được WP + có tệp mã nguồn) | `tools/probe-cong-tai-lieu.ps1` · `Invoke-TuKiem` | `pwsh -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | **exit 1** (đỏ) nếu logic bỏ qua `Chưa`/coi PR mã nguồn là "không áp dụng" | **exit 0** (xanh) |
| **NC-3** | Âm tính của `LaXungDotDuLieu`: `PostgresException 23505/40001` và `null` phải `false` (`D-210` **không** nuốt vi phạm ràng buộc vào nhánh `409`) | `test/…/ViPhamRangBuocDbTests.cs` | `dotnet test test/eCommerce.Tests --no-build` | (không đột biến code sản phẩm — `shared/**` thuộc BE; QA **không** sửa) | — |
| **NC-4** | `D-215` ①(4) — bốn body quyết định màu `P15`: (a) `{"error":{"code":null}}` ⇒ **ĐỎ**; (b) `{"error":{"code":"eCommerce:Lock:0001",…}}` ⇒ **CHUA CHAY** (không DAT/HONG); (c) `{"error":{"code":"eCommerce:Db:0002","message":"…"}}` ⇒ **`DAT`**; (d) 0 lần 409 ⇒ **CHUA CHAY** | `tools/probe-giu-cho-2instance.ps1:451-500` | `pwsh -File tools/probe-giu-cho-2instance.ps1` | (a) **chưa** chứng minh runtime — cần `409` thật rồi gỡ nhánh `LaXungDotDuLieu` trên máy dev, **không commit** (nợ `D-215` ①(4)/`D-214` ②) | (b)/(c)/(d) đúng nhánh trong code; **(c) đã đạt runtime**: `P15 DAT`, `ma = eCommerce:Db:0002` |

- Bằng chứng NC-1 (thật, 25/11/2026): đột biến ⇒ `TU KIEM: 7/8 ca DAT` và **exit 1**; khôi phục ⇒
  `TU KIEM: 8/8 ca DAT` và **exit 0**; `sha256sum` trước/sau = `66eab237794f0ee5ed0910f5e441ff76274e8e9cb6927502ed0c4a512d7a4acf`.

## 4. Số đo thật (25/11/2026, nhánh `feat/n69-wp15-nen-gd3`)

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | **exit 0** · `0 Error(s)` · **`9 Warning(s)`** (= baseline 9) |
| `dotnet test test/eCommerce.Tests --no-build` | **exit 0** · `Passed: 1966` · `Failed: 0` · `Skipped: 0` · **+5** so baseline **1961** |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -TuKiem` | **exit 0** · **8/8** ca `DAT` |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code` | **exit 0** · `WP = WP-15` ⇒ *"khong co ma tai lieu (07 ghi 'Không có tài liệu feature') ⇒ XANH"* |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo MergeMain` | **exit 0** · `WP = WP-15` (không có mã nào cần chữ ký) |
| `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | **exit 0** · **54/54 DAT** · **`Y7`/`Y8`/`Y9` DAT** — OPS đã cập nhật `D-211`/`D-213`/`D-214` **và 3 ràng buộc + chiều ngược 5 của `D-215` ①(3)**; QA **không** sửa tệp OPS |
| `pwsh -File tools/probe-giu-cho-2instance.ps1` | **exit 0** · **18 DAT / 0 HONG / 0 CHUA CHAY** · `P14`: 409 đã thấy = **1** (log A = 0 · B = 1) · **`P15` = `DAT`** (`so lan 409 = 1 · ma = eCommerce:Db:0002`) · `P16`/`P17` dọn dẹp DAT |
| `pwsh -File tools/probe-don-san.ps1` | **exit 0** · **49/49 DAT** · **`C10` DAT**: `tong = 1 · Late = 1 · choDu = True · log Host: GHI = 1 · GHI-HONG = 0 · TRUNG = 0` · `C13` DAT (`so dong DLQ = 1`) · `[CD-D67]` in `… · THATBAI-HONG = 0` · dọn dẹp `C48`/`C49` DAT |

**`C10` (probe-don-san):** **DAT** — `tong = 1 · Late = 1`, `choDu = True` (chờ **DLQ `LyDo = HaWatermark ≥ 1`** được thoả
trong **một** lần chờ `≤ 30 s`, không chờ lại ở `C13`), đủ **4 neo** log Host: `GHI = 1 · GHI-HONG = 0 · TRUNG = 0`
và dòng `[CD-D67]` in thêm `THATBAI-HONG = 0`. Điều kiện chờ mới **không** nới 30 s, **không** bỏ ca.

### 4.1 Bằng chứng runtime MỚI — lượt `-DayDu` 25/11/2026 (OPS chạy, QA ghi lại)

> ✅ **`-DayDu` ĐÃ CHẠY TRỌN trên cây cuối:** **exit 0 · 20/20 bước · 836,4 s** (lượt **ngoài phiên**,
> log `artifacts/dayDu-wp15.txt`) — gồm **E2E Playwright 8/8**, **`P15 DAT`** (`Db:0002`), `C10 DAT`,
> test cổng **60/60**, DB **25/25**. Nợ môi trường "hết bộ nhớ" của `N69` **đã đóng**; không chạy lại `-DayDu`.

- `probe-giu-cho-2instance.ps1`: **18 DAT / 0 HONG / 0 CHUA CHAY** — **`P15` = `DAT`**: thấy `409` thật
  `{"error":{"code":"eCommerce:Db:0002","message":"Dữ liệu vừa bị người khác thay đổi…"}}` ⇒ **chấm dứt `CHUA CHAY`**.
- `probe-don-san.ps1`: **`C10` DAT**.
- `tools/test-cong-kiem.ps1`: **60/60 DAT** · `-ChiKiemMoc`: **54/54 DAT** · **`Y7`/`Y8`/`Y9` DAT**.
- 📌 Diễn giải `P15` theo `D-215`: `Lock:0001` (hết hạn chờ khoá phân tán) ⇒ `CHUA CHAY` — **không** tính `DAT`,
  **không** đỏ oan; chỉ `error.code` `null`/rỗng/không parse ⇒ **ĐỎ**.

- 📌 Lượt chạy `probe-giu-cho` **đầu tiên** của vòng trước (chạy **song song** `dotnet test`) có `P03` lỗi môi trường:
  `/connect/token` trả `500` — `FileNotFoundException: Microsoft.IdentityModel.Abstractions, Version=8.19.2.0`
  (assembly **có** trong `bin/`, đúng version). Chạy lại **khi không có tiến trình build/test song song** ⇒
  `P03 DAT`. Đây là **lỗi môi trường tải assembly khi build song song**, không phải lỗi WP.
- 📌 Vòng 2 (`D-215`): lượt chạy **cục bộ** `probe-giu-cho` cũng thấy `409` thật ⇒ `P15 DAT`, khớp lượt `-DayDu`.

## 5. Chưa phủ / nợ (ghi rõ chủ)

1. **OPS:** **đã xong** — `Tim-ViPhamChanDoanC10`/`Y8` cho `D-213`; `Y7` mở rộng cho `P15` (`D-214` ①)
   **+ 3 ràng buộc và chiều ngược 5 của `D-215` ①(3)**; `Y9` gọi `probe-cong-tai-lieu.ps1 -TuKiem` + chạy repo
   thật (`D-211` ①); `Buoc` trong `tools/kiem-tat-ca.ps1` + workflow `cong-tai-lieu.yml` (`D-212` (ii)).
   Log thật: `test-cong-kiem` **60/60 DAT**, `-ChiKiemMoc` **54/54 DAT**, **`Y7`/`Y8`/`Y9` DAT**. QA **không** sửa tệp OPS.
2. **`P15` diễn giải theo `D-215`:** `409` mang `eCommerce:Db:0002` + `error.message` khác rỗng ⇒ **`DAT`**
   (xung đột `ConcurrencyStamp`); `409` mang `eCommerce:Lock:0001` (hết hạn `ChoKhoaToiDaGiay = 30`) ⇒
   **`CHUA CHAY`** — **không** tính `DAT`, **không** đỏ oan; mọi `409` có `error.code` `null`/rỗng/không parse ⇒
   **ĐỎ**; 0 lần `409` ⇒ `CHUA CHAY`. **Nợ `D-214` ② giữ nguyên:** chỉ cần thêm đường tạo xung đột **tất định**
   (`psql` giữ `InvItems` + bump `ConcurrencyStamp`) nếu **3** lượt CI liền có `0` lần `409`; **không** dùng
   `negative-control.ps1` trên `P15` làm bằng chứng chính (lượt 0 lần `409` thì đột biến không đổi màu).
3. **`negative-control.ps1` — ĐÃ CHẠY:** sau commit `dab2567`, **NC-1 DAT** — đột biến ⇒ **exit 1** (`7/8` ca, log
   `artifacts/negative-control/20261125-195435-do.log`); khôi phục byte gốc ⇒ **exit 0** (`8/8`, log
   `…-xanh.log`); bằng chứng ghi ở mục "Negative control" cuối tệp. Bằng chứng trước commit: NC-1 (đột biến tay) +
   `-TuKiem` CA1–CA6 + 5 ca unit.
4. **`check-agent-artifact.ps1 -Kind testplan`:** **không áp dụng** — WP-15 không có SRS/BR (đòi `-Srs` có `BR` §1).
5. **Nợ môi trường — ĐÃ ĐÓNG:** `-DayDu` **chạy trọn 20/20 · 836,4 s** trên cây cuối (log
   `artifacts/dayDu-wp15.txt`), gồm **E2E 8/8** và **`P15 DAT`**; `pnpm@10.22.0` đã cài toàn cục cho user,
   shim corepack ở `%TEMP%` đã bỏ.

## Negative control
| Ngay | Ma | Tep:dong | Dot bien | Lenh test | Bang chung |
|---|---|---|---|---|---|
| 2026-11-25 | NC-1 | tools/probe-cong-tai-lieu.ps1:411 | if ($giaTri[0].StartsWith('Tạm duyệt')) { -> if ($true) { | pwsh -NoProfile -File tools/probe-cong-tai-lieu.ps1 -TuKiem | exit 1 (do) -> khoi phuc -> exit 0 (xanh) |
