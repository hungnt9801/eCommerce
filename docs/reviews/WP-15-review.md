# Review code `WP-15` — 25/11/2026 (vòng 2, sau `D-215`)

- Nhánh: `feat/n69-wp15-nen-gd3` · base `main` · head `7267060` (working tree chưa commit)
- Diff: **~1.380 dòng** — tracked **11 tệp (+468 / −28 = 496 dòng)** + **4 tệp untracked (881 dòng)**;
  **trừ migration: 0 dòng** (không có migration nào trong diff)
- SRS: **không có** — hàng `WP-15` ở `docs/nhanh/07-ke-hoach-trien-khai.md:745` ghi *"Không có tài liệu feature"*.
  Hợp đồng đã đọc thay thế: `docs/nhanh/decisions.md` `D-209`①⑥ · `D-210` · `D-211` · `D-212` · `D-213` · `D-214` ·
  **`D-215` (vòng 2, thay ① của `D-214`)** · `D-66`①⑤⑥⑦ · `D-67`③④ · `D-68`②③ · `D-115`⑤ · `D-71`④;
  `docs/tests/WP-15-testplan.md`; `docs/ops/WP-15-deploy.md`; `docs/coding-conventions.md`; `.claude/scopes.json`.

## Đã kiểm

| Nhóm | Kết quả |
|---|---|
| A. Hợp đồng & đúng đắn | `D-210`: mã `eCommerce:Db:0002` **khác** `0001`; nhánh 409 kiểm **trước** nhánh Postgres; body đúng khuôn ABP (`error.code/message/details/data/validationErrors`), `ExceptionHandled = true`, `LogWarning` không lộ secret/PII; `LaXungDotDuLieu` chỉ nhận `AbpDbConcurrencyException` (đi `InnerException` ≤ 10) và **trả `false`** cho `PostgresException`/`null`. **Không** đổi nhánh `403` nào. `D-213`: `C10` chờ `DLQ LyDo = HaWatermark ≥ 1` (`LyDo = 1` = `LyDoVaoDlq.HaWatermark`, `DeadLetterMessage.cs:17`), **một** `Cho-DieuKien` duy nhất, `$GiayToiDa = 30` không nới, giữ `($tongA -eq 1 -and $lateA -eq 1)`, 4 neo + `[CD-D67]`. **`D-215`**: `P15` bốn nhánh đúng thứ tự — (i) `soXungDot = 0` ⇒ `CHUA CHAY`; (ii) `coMaRong` ⇒ **ĐỎ** (`Kq … $false`); (iii) `Db:0002` + message khác rỗng ⇒ **DAT**; (iv) mã khác (vd `Lock:0001`) ⇒ `CHUA CHAY`, không đỏ oan; `$script:soXungDot` **giữ nguyên nghĩa** (chỉ đếm lần thử lại). `D-211`/`D-212`: probe 3 mã thoát 0/1/2, fail-closed, glob 3 biên bản + `07` (không hard-code), README chỉ cảnh báo; workflow `pull_request` base `main`, `fetch-depth: 0`, `GITHUB_BASE_SHA`, không `continue-on-error`, không tự bật required check. |
| B. Tiền, tồn kho, dữ liệu | **Không chạm** `Money`/`decimal`/số lượng/tồn kho/sổ cái; **0 migration**, 0 `UPDATE` dữ liệu mới (chỉ `psql` trong probe, có dọn dẹp). Diff không thêm bảng/cột/index. |
| C. Bảo mật & multi-tenant | Không đổi `[Authorize]`, `MapInboundClaims`, `NameClaimType`, tenant filter, `AllowAnonymous`; không thêm endpoint/DTO; log mới chỉ in mã + loại exception + `Request.Path`. Package `Volo.Abp.Data` kiểm bằng nuspec: chỉ kéo `EventBus.Abstractions`/`ObjectExtending`/`Uow`, **không kéo EF**. |
| D. Test & vận hành | Số ca **tăng**: `eCommerce.Tests` **1961 → 1966** (`+5` Fact; tôi chạy lại `--no-build --filter ViPhamRangBuocDbTests` ⇒ **20/20 pass**); `tools/test-cong-kiem.ps1 -ChiKiemMoc` **tôi chạy lại trên nguồn HIỆN TẠI ⇒ 54/54 DAT · 0 HỎNG** (`Y7`/`Y8`/`Y9` DAT, gồm 3 ràng buộc `D-215` + chiều ngược 5); `$SoLanThuLai = 3` giữ nguyên; không sửa/xoá ca cũ. **Bằng chứng runtime mới** (log thật): `tools/_tam-kiem/giu-cho-2inst.log:17,21` và `tools/_tam-kiem/n69-daydu.log:292,295` — `P15 DAT` với body thật `{"error":{"code":"eCommerce:Db:0002","message":"Dữ liệu vừa bị người khác thay đổi…"}}`, `probe-giu-cho` **18 DAT / 0 HỎNG / 0 CHUA CHAY`. ⚠️ `test-cong.log` **60/60** là lượt **trước** lần sửa cuối (`probe-giu-cho` 02:56, `test-cong-kiem` 03:00) ⇒ 6 ca nhóm DB chưa chạy lại trong review này; 54/54 là số tôi tự đo trên nguồn cuối. |

## Dò đồ thị (bắt buộc cho kết luận về code)

- `graph: codegraph_explore "BoLocViPhamRangBuocDb registration MvcOptions Filters Add AbpExceptionFilter order"` →
  `BoLocViPhamRangBuocDb` là `IAsyncExceptionFilter`, **chỉ** đăng ký toàn cục tại
  `src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs:286` (`options.Filters.Add<…>()`, `Order` mặc định `0`);
  `OnExceptionAsync` gọi `LaXungDotDuLieu` **trước** `TimPostgres`/`AnhXa` ⇒ nhánh 409 nằm đúng **một cửa** đã có.
- `graph: codegraph_explore BoLocViPhamRangBuocDb ViPhamRangBuocDb MaChung AbpDbConcurrencyException` →
  `MaXungDot`/`ThongDiepXungDot`/`LaXungDotDuLieu` khai trong `shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs`,
  filter gọi đúng 3 ký hiệu đó (không có bản sao mã ở module khác).
- **Thứ tự filter với `AbpExceptionFilter` — ĐÃ CÓ BẰNG CHỨNG RUNTIME** (vòng 2): lượt `P15` thật nhận 409 mang
  `error.code = eCommerce:Db:0002`, tức filter của ta thắng `AbpExceptionFilter` (nếu không, `code = null` như
  `D-66`①). Không còn là suy luận từ nhánh `403`.

## Phát hiện

| # | Mức | Vị trí | Phát hiện | Bằng chứng | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | P2 | `tools/probe-cong-tai-lieu.ps1:1` (631 dòng) | **Phạm vi diff ~1.380 dòng > ngưỡng `K-04` ~600** | `git diff --numstat` (+468/−28, 11 tệp) + `wc -l` 4 tệp untracked = 881 dòng; `D-211`① buộc probe + testplan nằm cùng `WP-15` | Chấp nhận cho `WP-15` (cổng + đấu dây + testplan là một hợp đồng, không tách được); ghi hàng đợi review; từ `WP-16` giữ ≤ ~600 |
| 2 | P2 | `tools/probe-giu-cho-2instance.ps1:461` | ✅ **ĐÃ GIẢI QUYẾT (`D-215`)**: trước đây `P15` đòi **mọi** 409 là `Db:0002` trong khi endpoint còn trả 409 `eCommerce:Lock:0001` ⇒ đỏ oan. Nay 4 nhánh: `code` null/rỗng/không parse ⇒ **ĐỎ**; `Db:0002` + message ⇒ **DAT**; mã khác (vd `Lock:0001`, in thêm *"nghi khoa giu qua 30 s — xem P07"*) ⇒ `CHUA CHAY`; 0 lần 409 ⇒ `CHUA CHAY`. `soXungDot` không đổi nghĩa | `probe-giu-cho-2instance.ps1:85-127` (`Ghi-Nhan409`, 4 biến + hằng `MA_KHOA_PHAN_TAN`), `:460-500` (4 nhánh), `:537-545` (câu tổng kết nêu **hai** lý do + số 409 + mã); `D-215`①(1)(2); tôi chạy lại `-ChiKiemMoc` ⇒ **54/54** (Y7 gồm 3 ràng buộc + chiều ngược 5) | Không cần sửa. Duy trì: `CHUA CHAY` **không** được tính DAT (đã đúng) |
| 3 | P2 | `src/eCommerce.HttpApi.Host/Filters/BoLocViPhamRangBuocDb.cs:42` | ✅ **ĐÃ CÓ BẰNG CHỨNG RUNTIME (`P15 DAT`)** — hợp đồng `409 + error.code` đã được đo thật, không còn suy luận; thứ tự filter đã được chứng minh end-to-end. **Nợ giữ nguyên** (`D-215`①(4)/`D-214`②): nhánh **`code = null` ⇒ ĐỎ** chưa có ca **tất định** (cần 409 thật rồi tạm gỡ `LaXungDotDuLieu` trên máy dev, không commit) | `giu-cho-2inst.log:17,21` · `n69-daydu.log:292,295` (body thật `Db:0002` + `18 DAT / 0 HONG / 0 CHUA CHAY`); testplan §4.1/§5.2 | Khi **3** lượt CI liền có **0** lần 409 ⇒ QA thêm đường `psql` giữ `InvItems` + bump `ConcurrencyStamp` rồi chứng minh `code = null ⇒ ĐỎ` (**không** dùng `negative-control` cho `P15` — lượt 0 409 thì đột biến không đổi màu). 📌 `probe-giu-cho` được sửa lần cuối **sau** lượt log 02:52 ⇒ chạy lại **một** lượt sau khi chốt tệp |
| 4 | P3 | `tools/kiem-tat-ca.ps1:276` | Chú thích `-- BUOC 4a` không khớp số bước thực (bước mới là bước **⑥** theo log) | `cong-tai-lieu.log`; `Buoc` tự đánh số bằng `$ketQua.Count + 1` | Sửa chú thích (thuần chú thích) |
| 5 | P3 | `tools/probe-cong-tai-lieu.ps1:433` | `MergeMain` **không** in cảnh báo lệch `docs/features/README.md` §2 (chỉ `Code` gọi `Get-CanhBaoReadme`) | `Kiem-Cong`: `Get-CanhBaoReadme` chỉ trong nhánh `Code`; `D-212`(iv) nói lệch ⇒ cảnh báo, không giới hạn chế độ | Thêm cảnh báo (không đổi màu) ở `MergeMain` khi tiện |
| 6 | P3 | `shared/eCommerce.Kernel/eCommerce.Kernel.csproj:12` | Chú thích nói `Volo.Abp.Data` *"đã nằm trong closure bắc cầu"* — đúng phía app, nhưng với **Kernel** đây là **thêm** 3 gói vào bề mặt biên dịch (`Uow`, `EventBus.Abstractions`, `ObjectExtending`). ✅ Xác nhận **không kéo EF** | `~/.nuget/packages/volo.abp.data/10.7.0/volo.abp.data.nuspec:15-41` | Ghi rõ "Kernel **thêm** 3 gói abstraction, không EF" |
| 7 | P3 | `tools/ci-khang-dinh.ps1:42` | Sàn số bước (`$SoBuocToiThieu = 15` cho `-DayDu`) **không nâng** dù cổng nay nhiều bước hơn. ✅ Bù lại: `test-cong-kiem.ps1:400` đã thêm `probe-cong-tai-lieu.ps1` vào **danh mục bước bắt buộc** (W1 chặn xoá bước) | `buocBatBuoc` + A5 (`$soBuoc -lt $SoBuocToiThieu`) | Nâng sàn khi chốt số bước GĐ3 (không chặn merge) |
| 8 | P3 | `tools/probe-cong-tai-lieu.ps1:380-386` | Nhánh "tập mã **rỗng**" trả `0` **trước** khi xét luật ĐỎ (b) *"ô Cổng tài liệu chứa `chưa`"* ⇒ một WP rỗng mã + ô cổng ghi `chưa` sẽ **lọt xanh** (hôm nay vô hại: `WP-15` có câu `Không có tài liệu feature`) | `Kiem-Cong` (`Ma.Count -eq 0` ⇒ return sớm); `D-212`(iv) luật (b) không loại trừ trường hợp rỗng mã | Kiểm luật (b) **trước** khi return sớm (fail-closed rẻ hơn) |
| 9 | P3 | `docs/tests/WP-15-testplan.md:37,114` · `docs/ops/WP-15-deploy.md:57-58` | **Tài liệu chưa khớp trạng thái cuối**: testplan §2/§5 vẫn ghi *"OPS còn nợ **3 ràng buộc `D-215`**"* trong khi OPS **đã** thêm (tôi chạy lại `-ChiKiemMoc` 54/54, gồm cả chiều ngược 5); deploy §5 vẫn ghi `kiem-tat-ca` **"CHƯA HOÀN TẤT / ORCH chạy ở G6"** trong khi testplan §4.1 đã trích số của lượt `-DayDu` | diff `test-cong-kiem.ps1:742-768,996-1002`; `testplan:114`; `deploy:57-58`; log `-ChiKiemMoc` 54/54 hôm nay | QA/OPS đồng bộ lại 3 chỗ trên **trong PR này** (tài liệu là hợp đồng) |
| 10 | P3 | `tools/_tam-kiem/n69-daydu.log` (cuối tệp) | Lượt `-DayDu` 25/11 **KHÔNG kết thúc xanh**: dừng ở bước E2E Playwright với `browserType.launch: Executable doesn't exist …chrome-headless-shell.exe` (`8 failed`, `E2E PLAYWRIGHT DO (exit 1)`) — **môi trường** (thiếu browser binary), bước này **có sẵn từ trước**, không do `WP-15`. Số `P15 DAT`/`18/0/0` trong log đó là **thật** nhưng là số của lượt chạy **dở** | `n69-daydu.log` tail; `git diff` của `kiem-tat-ca.ps1` chỉ thêm bước cổng tài liệu | Ghi rõ trạng thái này vào deploy §5; cài browser (`pnpm exec playwright install`) rồi chạy lại `-DayDu` cho tới **xanh** trước khi push/merge (ORCH ở G6) |

## Đã kiểm và **không** thành phát hiện (ghi để phiên sau không kiểm lại)

- **Mã `0002` ≠ `0001`**: đúng; `403` giữ `MaChung = eCommerce:Db:0001`; 5 Fact khoá bất biến, chạy **20/20 pass**.
- **Không nuốt lỗi khác**: `LaXungDotDuLieu` chỉ so **kiểu**, `PostgresException` (`23505`/`40001`) và `Exception`
  thường trả `false`; nhánh Postgres và nhánh `500` giữ nguyên thứ tự.
- **`D-213` không che lỗi ghi thật**: `DLQ = 1` mà `Late = 0` ⇒ `C10` **ĐỎ**; khẳng định `tong = 1 VA Late = 1` giữ nguyên;
  dòng `[CD-D67]` + 4 neo giữ; lần chờ trùng ở `C13` đã bỏ (`$mauDemDlqLyDo` chuyển lên trước `$choDuA`), đúng **một** lần chờ.
- **`Y7`/`Y8` có hai chiều**: `Y7` thêm 4 chiều ngược (xoá `error.code`, xoá `eCommerce:Db:0002`, hạ nhánh `coMaRong`
  xuống `$false`, và các neo bất biến cũ); `Y8` thêm 5 chiều ngược (gồm bỏ chờ DLQ). Mỗi chiều khẳng định
  *"phép thay ĐÃ xảy ra"* trước khi khẳng định *"phép quét PHẢI báo"* ⇒ không thể xanh rỗng. Tôi chạy lại: **54/54 DAT**.
- **`Y9` chống cổng rỗng**: đòi `-TuKiem` `exit 0` **và** repo thật xanh ở **cả hai** chế độ; dùng `ChayScript` giữ `$LASTEXITCODE`.
  Log thật trong lượt của tôi: `-TuKiem exit 0 · Code exit 0 · MergeMain (WP-15) exit 0`.
- **Fail-closed**: `exit 2` bị `kiem-tat-ca.ps1` coi là **ĐỎ** (throw), workflow không có `continue-on-error` ⇒ 2 cũng đỏ.
- **Không sửa tệp ngoài phạm vi**: `Directory.Packages.props` + `decisions.md` + `hang-doi-xem-lai.md` đều do vai `be`/`ops`/ORCH
  sở hữu (`.claude/scopes.json`), không phải `rev`/QA vượt vùng.
- **Ngoại lệ đã biết, không phải lỗi WP**: `next build` trên Windows lỗi **EPERM symlink** (môi trường);
  `pnpm run gen:api` lệch **byte** do `core.autocrlf` (nội dung y hệt, blob `be3cebf…` = HEAD — `fe-gen-api.log`);
  root `pnpm lint` trỏ script không tồn tại ở `apps/admin-web` (nợ có sẵn); E2E Playwright thiếu browser binary (mục P3 #10).

## Điều kiện trước merge (không phải phát hiện P1/P2 — không tự sửa)

1. **Cổng đầy đủ `-DayDu` phải chạy tới XANH.** Lượt 25/11 dừng ở E2E Playwright do thiếu browser binary (P3 #10, môi trường);
   cài browser rồi chạy lại. Ngoài ra `probe-giu-cho-2instance.ps1` được sửa **sau** lượt log 02:52 ⇒ chạy lại **một** lượt
   để `P15` khớp đúng bản cuối (dự kiến vẫn `DAT`).
2. **Negative control `NC-1` bằng `tools/agents/negative-control.ps1`** sau khi ORCH commit (tệp phải đã commit, không dirty):
   hiện có bằng chứng **đột biến tay** đỏ→xanh (`7/8 → 8/8`, sha256 `66eab23…`) ở `WP-15-testplan.md` §3; `D-211`① chốt
   lớp thứ ba này phải chạy **sau commit** và ghi dòng bằng chứng vào testplan.
3. **Không bật `cong-tai-lieu` thành required check** — việc NGƯỜI (`decision-policy` §3.1); workflow hiện **không** tự bật.
4. **Nợ `D-215`①(4)/`D-214`②**: khi có 3 lượt CI liền 0 lần 409 ⇒ thêm đường tạo xung đột tất định + ca chứng minh
   `code = null ⇒ ĐỎ`; **không** dùng `negative-control.ps1` trên `P15` làm bằng chứng chính.

## Kết luận

- **P1 = 0** — không có lỗi nào sẽ gây sai tiền/tồn/quyền/lộ dữ liệu/hỏng production: không đổi nhánh 403, không nuốt
  exception, không migration, không nới/xoá ca, cổng mới **fail-closed** và có ca ĐỎ hai chiều.
- P2 = 3: **#1 phạm vi diff** (chấp nhận, có lý do `D-211`); **#2 đã giải quyết (`D-215`)**; **#3 đã có bằng chứng
  runtime (`P15 DAT`)** — nợ còn lại đã ghi rõ và có chủ. Không P2 nào chạm tiền/tồn/quyền/idempotency ⇒ **không chặn merge**.
- P3 = 7 (#4…#10) — ghi khuyến nghị, không tự sửa.

P1 = 0 · P2 = 3 (2 đã giải quyết) · P3 = 7

VERDICT: PASS
