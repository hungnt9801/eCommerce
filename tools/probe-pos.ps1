<#
.SYNOPSIS
    Probe HTTP thật cho **WP-06 — POS: hoá đơn bán lẻ + ca thu ngân + thanh toán đa phương thức + in**.

.DESCRIPTION
    Chạy **Host thật** (54275) + **Next thật** (3000) + **PostgreSQL thật**, đăng nhập qua **NextAuth**
    (đúng đường người dùng đi), rồi đo bằng HTTP qua **BFF** (`/api/proxy` — `D-12`):

      ①  trang POS `/{business}/pos/bill`                        → 200 + "Bán lẻ tại quầy" + "CHƯA MỞ CA"
      ②  `POST …/pos/sessions/open` (tiền đầu ca 500.000)        → 200, status Open
      ③  `GET …/pos/sessions/current`                            → 200, đúng ca vừa mở
      ④  `POST …/pos/bills` **3 SP + 2 phương thức**             → 200, totalPayment = Σ dòng,
                                                                    paidAmount = 150.000, nợ 0, thối 0
      ⑤  `GET …/pos/bills/{A}`                                   → 2 phương thức (tiền mặt + CK)
      ⑥  `GET …/stock/balances` sau khi bán                      → OnHand giảm ĐÚNG số đã bán
      ⑦  gửi lại **cùng `ClientBillId`**                         → 200 + `alreadyExisted = true`,
                                                                    tồn kho **KHÔNG** đổi (idempotent)
      ⑧  chiết khấu **CẢ dòng VÀ tổng**                          → 403 + `eCommerce:Pos:0004`
      ⑨  ghi nợ mà **thiếu khách/hạn**                           → 403 + `eCommerce:Pos:0006`
      ⑩  `POST …/pos/returns` trả 1 dòng                         → 200, hàng trả = giảm nợ + hoàn tiền + phí,
                                                                    kho **tăng** lại, `ReturnedQuantity` ghi sổ
      ⑪  trả **vượt** số đã mua                                  → 403 + `eCommerce:Pos:0019`
      ⑫  `POST …/bills/{C}/void` (huỷ hoá đơn khác)              → 200 + nhập lại kho; huỷ lần 2 → `voided=false`
      ⑬  trang in `/{business}/print/bill/{A}`                   → 200 + số hoá đơn + "PHẢI TRẢ"
      ⑭  `POST …/sessions/{id}/close` (bàn giao 500.000)         → 200, `expectedCash`/`variance` khớp công thức
      ⑮  huỷ hoá đơn **của ca đã kết**                           → 403 + `eCommerce:Pos:0010`
      ⑯  bất biến **tổng sổ cái = OnHand** (SQL)                 → bằng nhau

    🔒 Không in token/cookie. Mã `WP06-<random>` để chạy lại nhiều lần không đụng nhau.
    📌 Probe **để lại** 1 kho + 3 phiếu nhập + 3 hoá đơn + 1 phiếu trả + 1 ca (rác kiểm chứng) trong DB dev
    — **cố ý**, để phiên sau kiểm lại bằng SQL.

.PARAMETER BoQuaHost
    Host đã chạy sẵn ⇒ không bật/tắt Host.

.PARAMETER BoQuaWeb
    Next đã chạy sẵn ở cổng 3000 ⇒ không bật/tắt Next.

.NOTES
    📌 **KHÔNG seed SQL** (khác `probe-don-hang.ps1`): mọi thứ đi qua **API thật** — tạo kho, nhập kho, mở ca,
    bán, trả, huỷ, kết ca. Đây chính là điểm khác biệt của `WP-06`: POS **không** cần sổ giải trình giá/giữ chỗ
    như đường checkout của đơn online, nên probe đo được **toàn bộ** vòng đời bằng HTTP.

.EXAMPLE
    pwsh -File .\tools\probe-pos.ps1
#>
[CmdletBinding()]
param(
    [switch]$BoQuaHost,
    [switch]$BoQuaWeb
)

$ErrorActionPreference = 'Stop'
# 🔑 `nen-tang.ps1` = lop "mot cho" cho moi thu khac nhau giua Windows va Linux (`XC-007` P-01…P-06).
#    🚨 Truoc `N12` tep nay con 3 khuon chi-Windows (duong dan output viet cung · co WindowStyle ·
#    goi ban curl cua Windows) ⇒ tren `ubuntu-latest` no **do vi moi truong**, khong phai vi san pham.
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$gocRepo = Split-Path -Parent $PSScriptRoot
$api = 'http://localhost:54275'
$web = 'http://localhost:3000'
$business = '230222'

$ngauNhien = (Get-Random -Minimum 100000 -Maximum 999999)
$maKho = "WP06-$ngauNhien"
$skuA = "WP06-SP-A-$ngauNhien"
$skuB = "WP06-SP-B-$ngauNhien"
$skuC = "WP06-SP-C-$ngauNhien"

$canhBao = @()
$hostProc = $null
$webProc = $null
$jar = Lay-DuongDanTam "wp06-cookies-$ngauNhien.txt"

function In-KetQua {
    param([string]$Nhan, [int]$Status, [string]$Body)

    Write-Host $Nhan
    Write-Host ("   HTTP {0}" -f $Status) -ForegroundColor $(if ($Status -lt 400) { 'Green' } else { 'Yellow' })
    if ($Body.Length -gt 0) {
        $gon = if ($Body.Length -gt 400) { $Body.Substring(0, 400) + '…' } else { $Body }
        Write-Host ("   {0}" -f ($gon -replace '\s+', ' '))
    }
}

function Sql {
    param([Parameter(Mandatory)][string]$CauLenh)

    return ((($CauLenh | docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A) | Out-String).Trim())
}

# ── 0. Host ───────────────────────────────────────────────────────────────────────────────────────────────
if (-not $BoQuaHost) {
    $bin = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host' -GocRepo $gocRepo
    if (-not (Test-Path (Join-Path $bin 'eCommerce.HttpApi.Host.dll'))) {
        Write-Host "KHONG THAY binary Host tai $bin — build truoc." -ForegroundColor Red
        exit 2
    }

    # 📌 Kill theo **PID giữ cổng** — `Stop-Process -Name eCommerce.HttpApi.Host` KHÔNG khớp vì Host chạy
    # bằng `dotnet <dll>` (bài học `handover-n6` §1.4 bẫy ②).
    $giuCong = @(Lay-PidDangNghe -Cong 54275)
    foreach ($pid_ in $giuCong) { Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $api
    $env:AuthServer__Authority = $api

    $hostProc = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa (Lay-DuongDanTam 'wp06-host.log') -TepLogLoi (Lay-DuongDanTam 'wp06-host.err')

    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $api -LogFile (Lay-DuongDanTam 'wp06-host.log') -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Host KHONG len duoc — xem log o tren.' -ForegroundColor Red
        exit 2
    }
}

try {
    # ── 0b. Dev-only: tắt cờ đổi mật khẩu (`D-56` / `WP-13` ①) ─────────────────────────────────────────
    # 📌 `Start-KhongCoDoiMatKhau` (trong `nen-tang.ps1`) THAY cho `UPDATE` viết tay: probe này đi **cả**
    #    đường token Host **và** đăng nhập BFF (`/api/auth/callback/credentials` — BFF cũng xin token bằng
    #    password grant) ⇒ cờ phải tắt **suốt** lượt chạy, và `finally` **BẬT LẠI** (thiếu bước bật lại =
    #    commit một admin không bị buộc đổi mật khẩu + cổng DB nhóm ② đỏ ở lượt sau).
    Start-KhongCoDoiMatKhau
    Write-Host 'Dev-only: đã tắt cờ đổi mật khẩu của admin (DB dev).' -ForegroundColor DarkGray

    # ── 1. Token Host (SETUP: kho + hàng tồn) ─────────────────────────────────────────────────────────
    $token = Invoke-RestMethod -Method Post -Uri "$api/connect/token" -ContentType 'application/x-www-form-urlencoded' `
        -Body @{
        grant_type    = 'password'
        client_id     = 'eCommerce_App'
        client_secret = '1q2w3e*'
        scope         = 'eCommerce offline_access'
        username      = 'admin@abp.io'
        password      = '1q2w3E*'
    }
    $h = @{ Authorization = "Bearer $($token.access_token)" }
    Write-Host 'Token Host: OK (khong in gia tri).' -ForegroundColor Green

    $kho = Invoke-RestMethod -Method Post -Uri "$api/api/app/warehouse" -Headers $h -ContentType 'application/json' `
        -Body (@{ code = $maKho; name = "Quay WP06 $ngauNhien" } | ConvertTo-Json -Compress)
    Write-Host ("① Quay tao: {0} ({1})" -f $kho.code, $kho.id) -ForegroundColor Green

    $maNhap = @{}
    $soPhieu = 0
    foreach ($cap in @(
            @{ sku = $skuA; gia = 50000 },
            @{ sku = $skuB; gia = 30000 },
            @{ sku = $skuC; gia = 20000 })) {
        $soPhieu++
        # 📌 Số phiếu phải KHÁC nhau cho từng SKU — lượt chạy đầu dùng `Sku.Substring(9)` và cả ba SKU ra
        # **cùng** một số (đuôi `-<random>`) ⇒ DB chặn `Inventory:0014`. Đếm thứ tự là cách rõ ràng nhất.
        $so = "PN-WP06-$ngauNhien-$soPhieu"
        $maNhap[$cap.sku] = $so

        $nhap = Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills" -Headers $h -ContentType 'application/json' `
            -Body (@{
                number         = $so
                type           = 0
                mode           = 5
                warehouseId    = $kho.id
                idempotencyKey = "WP06-NHAP-$ngauNhien-$so"
                lines          = @(@{ sku = $cap.sku; quantity = 10; unitPrice = $cap.gia })
            } | ConvertTo-Json -Depth 6 -Compress)

        Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills/$($nhap.id)/post" -Headers $h | Out-Null
    }
    Write-Host "② Nhập kho 10 × 3 SKU (A/B/C) ⇒ tồn 10 mỗi SKU." -ForegroundColor Green

    # Khách cho hoá đơn ghi nợ (bắt buộc: nợ phải biết AI nợ).
    $khach = Invoke-RestMethod -Method Post -Uri "$api/api/app/customer/tao-khach" -Headers $h -ContentType 'application/json' `
        -Body (@{
            name           = "Khách WP06 $ngauNhien"
            phone          = "09$ngauNhien"
            idempotencyKey = "WP06-KHACH-$ngauNhien"
        } | ConvertTo-Json -Compress)
    Write-Host ("③ Khách tạo: {0}" -f $khach.id) -ForegroundColor Green

    # ── 2. Next ───────────────────────────────────────────────────────────────────────────────────────
    $webDaChay = $false
    try {
        $thu = Invoke-WebRequest -Uri "$web/login" -SkipHttpErrorCheck -TimeoutSec 3
        $webDaChay = ($thu.StatusCode -eq 200)
    }
    catch {
        $webDaChay = $false
    }

    if (-not $webDaChay -and -not $BoQuaWeb) {
        $webProc = Start-NextAdmin -ThuMucLamViec $gocRepo `
            -TepLogRa (Lay-DuongDanTam 'wp06-web.log') -TepLogLoi (Lay-DuongDanTam 'wp06-web.err')

        $daLen = $false
        for ($i = 0; $i -lt 60; $i++) {
            Start-Sleep -Seconds 1
            try {
                $thu = Invoke-WebRequest -Uri "$web/login" -SkipHttpErrorCheck -TimeoutSec 2
                if ($thu.StatusCode -eq 200) { $daLen = $true; break }
            }
            catch { }
        }

        if (-not $daLen) {
            Write-Host 'Next KHONG len duoc o cong 3000 — xem %TEMP%\wp06-web.log' -ForegroundColor Red
            exit 2
        }
    }
    elseif ($webDaChay) {
        Write-Host '⚠️  Dung Next dang chay san o cong 3000 — neu no la build CU thi trang moi se 404.' -ForegroundColor Yellow
    }

    Write-Host "Next: OK ($web)" -ForegroundColor Green

    # ── 3. Đăng nhập NextAuth ─────────────────────────────────────────────────────────────────────────
    $csrf = (& (Lay-Curl) -s -c $jar "$web/api/auth/csrf" | ConvertFrom-Json).csrfToken
    if ([string]::IsNullOrWhiteSpace($csrf)) {
        Write-Host 'KHONG lay duoc csrfToken cua NextAuth.' -ForegroundColor Red
        exit 1
    }

    $body = "csrfToken=$([uri]::EscapeDataString($csrf))&username=admin%40abp.io&password=1q2w3E%2A&callbackUrl=$([uri]::EscapeDataString("$web/$business"))"
    $maDangNhap = (& (Lay-Curl) -s -b $jar -c $jar -o (Lay-DuongDanTam 'dang-nhap.out') -w '%{http_code}' -X POST "$web/api/auth/callback/credentials" `
            -H 'Content-Type: application/x-www-form-urlencoded' --data $body)

    $coSession = (Select-String -Path $jar -Pattern 'authjs.session-token' -Quiet)
    Write-Host ("Dang nhap NextAuth: HTTP {0} · session cookie: {1}" -f $maDangNhap, $(if ($coSession) { 'CO' } else { 'KHONG' })) -ForegroundColor $(if ($coSession) { 'Green' } else { 'Red' })
    if (-not $coSession) { exit 1 }

    function Lay-Trang {
        param([Parameter(Mandatory)][string]$Path)

        $raw = (& (Lay-Curl) -s -b $jar -w '__HTTP__%{http_code}' "$web$Path") -join "`n"
        $i = $raw.LastIndexOf('__HTTP__')
        [pscustomobject]@{
            Status = [int]$raw.Substring($i + 8)
            Body   = $raw.Substring(0, $i)
        }
    }

    function Goi-Bff {
        param([Parameter(Mandatory)][string]$Method, [Parameter(Mandatory)][string]$Path, $Body = $null)

        $thamSo = @('-s', '-b', $jar, '-w', '__HTTP__%{http_code}', '-X', $Method, "$web$Path")
        if ($null -ne $Body) {
            $tep = Lay-DuongDanTam "wp06-body-$ngauNhien.json"
            ($Body | ConvertTo-Json -Depth 10 -Compress) | Set-Content -Path $tep -Encoding utf8 -NoNewline
            $thamSo += @('-H', 'Content-Type: application/json', '--data-binary', "@$tep")
        }

        $raw = (& (Lay-Curl) @thamSo) -join "`n"
        $i = $raw.LastIndexOf('__HTTP__')
        [pscustomobject]@{
            Status = [int]$raw.Substring($i + 8)
            Body   = $raw.Substring(0, $i)
        }
    }

    function Ton {
        param([string]$Sku)

        $r = Goi-Bff -Method GET -Path "/api/proxy/v1/stock/balances?sku=$Sku"
        if ($r.Status -ne 200) { return $null }
        return (($r.Body | ConvertFrom-Json).items | Select-Object -First 1)
    }

    # ── 4. ① Trang POS (chưa mở ca) ───────────────────────────────────────────────────────────────────
    $trang = Lay-Trang -Path "/$business/pos/bill?warehouseId=$($kho.id)"
    In-KetQua -Nhan '① TRANG POS (chua mo ca)' -Status $trang.Status -Body ''
    if ($trang.Status -ne 200) { $canhBao += "Trang POS tra HTTP $($trang.Status)" }
    $trangSach = $trang.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @('Bán lẻ tại quầy', 'CHƯA MỞ CA', 'Mở ca')) {
        if ($trangSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang POS thieu chuoi '$chuoi'" }
    }

    # ── 5. ②③ Mở ca + đọc ca hiện tại ────────────────────────────────────────────────────────────────
    $moCa = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/sessions/open' -Body @{
        warehouseId = $kho.id
        openingCash = 500000
    }
    In-KetQua -Nhan '② MO CA (tien dau ca 500.000)' -Status $moCa.Status -Body $moCa.Body
    if ($moCa.Status -ne 200) { $canhBao += "Mo ca tra HTTP $($moCa.Status)" }

    $ca = $moCa.Body | ConvertFrom-Json
    if ($ca.status -ne 'Open') { $canhBao += "Ca sau khi mo co status '$($ca.status)' (mong doi Open)" }

    $moCa2 = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/sessions/open' -Body @{
        warehouseId = $kho.id
        openingCash = 100000
    }
    In-KetQua -Nhan '②b MO CA LAN HAI (mot quay mot ca ⇒ phai chan 0012)' -Status $moCa2.Status -Body $moCa2.Body
    if ($moCa2.Status -ne 403) { $canhBao += "Mo ca lan hai tra HTTP $($moCa2.Status) (mong doi 403)" }
    if ($moCa2.Body -notmatch 'Pos:0012') { $canhBao += 'Mo ca lan hai khong tra ma Pos:0012' }

    $caHienTai = Goi-Bff -Method GET -Path "/api/proxy/v1/pos/sessions/current?warehouseId=$($kho.id)"
    In-KetQua -Nhan '③ CA DANG MO (GET sessions/current)' -Status $caHienTai.Status -Body $caHienTai.Body
    if ($caHienTai.Status -ne 200) { $canhBao += "Ca hien tai tra HTTP $($caHienTai.Status)" }

    # ── 6. ④ BÁN: 3 SP + 2 phương thức ──────────────────────────────────────────────────────────────
    $clientBillA = [guid]::NewGuid().ToString()

    $thanhA = @{
        clientBillId        = $clientBillA
        warehouseId         = $kho.id
        customerGivenAmount = 200000
        lines               = @(
            @{ sku = $skuA; quantity = 2; unitPrice = 50000; productName = 'WP06 A' },
            @{ sku = $skuB; quantity = 1; unitPrice = 30000; productName = 'WP06 B' },
            @{ sku = $skuC; quantity = 1; unitPrice = 20000; productName = 'WP06 C' }
        )
        payments            = @(
            @{ method = 1; amount = 50000 },
            @{ method = 2; amount = 100000; referenceCode = "UNC-WP06-$ngauNhien" }
        )
    }

    $banA = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body $thanhA
    In-KetQua -Nhan '④ BAN 3 SP · chia TIEN MAT 50.000 + CHUYEN KHOAN 100.000' -Status $banA.Status -Body $banA.Body
    if ($banA.Status -ne 200) { $canhBao += "Ban hoa don A tra HTTP $($banA.Status)" }

    $hoaDonA = $null
    if ($banA.Status -eq 200) {
        $kqA = $banA.Body | ConvertFrom-Json
        $hoaDonA = $kqA.bill

        if ($kqA.alreadyExisted) { $canhBao += 'Hoa don A bao alreadyExisted ngay lan dau (SAI)' }

        # 2×50.000 + 30.000 + 20.000 = 150.000
        if ([double]$hoaDonA.totalPayment -ne 150000) { $canhBao += "totalPayment = $($hoaDonA.totalPayment) (mong doi 150000)" }
        if ([double]$hoaDonA.paidAmount -ne 150000) { $canhBao += "paidAmount = $($hoaDonA.paidAmount) (mong doi 150000)" }
        if ([double]$hoaDonA.debtAmount -ne 0) { $canhBao += "debtAmount = $($hoaDonA.debtAmount) (mong doi 0)" }
        if ([double]$hoaDonA.changeAmount -ne 0) { $canhBao += "changeAmount = $($hoaDonA.changeAmount) (mong doi 0)" }
        if ([int](@($hoaDonA.lines)).Count -ne 3) { $canhBao += "So dong hoa don A = $(@($hoaDonA.lines).Count) (mong doi 3)" }

        Write-Host ("   So hoa don: {0} · phai tra {1} · da thu {2} · no {3}" -f `
                $hoaDonA.number, $hoaDonA.totalPayment, $hoaDonA.paidAmount, $hoaDonA.debtAmount) -ForegroundColor Green
    }

    # ── 7. ⑤ Chi tiết + ⑥ tồn kho ───────────────────────────────────────────────────────────────────
    if ($null -ne $hoaDonA) {
        $ctA = Goi-Bff -Method GET -Path "/api/proxy/v1/pos/bills/$($hoaDonA.id)"
        In-KetQua -Nhan '⑤ CHI TIET HOA DON A' -Status $ctA.Status -Body $ctA.Body
        if ($ctA.Status -ne 200) { $canhBao += "Chi tiet hoa don A tra HTTP $($ctA.Status)" }
        else {
            $ct = $ctA.Body | ConvertFrom-Json
            if ([int](@($ct.payments)).Count -ne 2) { $canhBao += "So dong thanh toan = $(@($ct.payments).Count) (mong doi 2)" }
            if ([int](@($ct.paymentMethods)).Count -ne 2) { $canhBao += "So phuong thuc = $(@($ct.paymentMethods).Count) (mong doi 2)" }
        }
    }

    $tonA = Ton -Sku $skuA
    $tonB = Ton -Sku $skuB
    $tonC = Ton -Sku $skuC

    Write-Host '⑥ TON KHO SAU KHI BAN'
    Write-Host ("   A {0} (moi 10, ban 2) · B {1} (moi 10, ban 1) · C {2} (moi 10, ban 1)" -f `
            $tonA.onHand, $tonB.onHand, $tonC.onHand) -ForegroundColor Green
    if ([double]$tonA.onHand -ne 8) { $canhBao += "onHand A = $($tonA.onHand) (mong doi 8)" }
    if ([double]$tonB.onHand -ne 9) { $canhBao += "onHand B = $($tonB.onHand) (mong doi 9)" }
    if ([double]$tonC.onHand -ne 9) { $canhBao += "onHand C = $($tonC.onHand) (mong doi 9)" }

    # ── 8. ⑦ IDEMPOTENT: gửi lại cùng ClientBillId ──────────────────────────────────────────────────
    $lanHai = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body $thanhA
    In-KetQua -Nhan '⑦ GUI LAI CUNG ClientBillId (offline retry)' -Status $lanHai.Status -Body $lanHai.Body
    if ($lanHai.Status -ne 200) { $canhBao += "Gui lai tra HTTP $($lanHai.Status) (mong doi 200)" }
    else {
        $kq2 = $lanHai.Body | ConvertFrom-Json
        if (-not $kq2.alreadyExisted) { $canhBao += 'Gui lai KHONG bao alreadyExisted (se tao hoa don thu hai!)' }
        if ($kq2.bill.number -ne $hoaDonA.number) { $canhBao += 'Gui lai tra so hoa don KHAC' }
    }

    $tonA2 = Ton -Sku $skuA
    Write-Host ("   Ton A sau khi gui lai = {0} (phai la 8 — KHONG duoc tru lan hai)" -f $tonA2.onHand) -ForegroundColor Green
    if ([double]$tonA2.onHand -ne 8) { $canhBao += "Ton A sau gui lai = $($tonA2.onHand) (mong doi 8)" }

    # ── 9. ⑧⑨ Hai ca PHẢI BỊ CHẶN ───────────────────────────────────────────────────────────────────
    $haiTang = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId  = [guid]::NewGuid().ToString()
        warehouseId   = $kho.id
        discountAmount = 10000
        lines         = @(
            @{ sku = $skuA; quantity = 1; unitPrice = 50000; discountAmount = 5000 }
        )
        payments      = @(@{ method = 1; amount = 35000 })
    }
    In-KetQua -Nhan '⑧ CHIET KHAU CA DONG VA TONG (Nhanh: chi MOT tang)' -Status $haiTang.Status -Body $haiTang.Body
    if ($haiTang.Status -ne 403) { $canhBao += "Chiet khau hai tang tra HTTP $($haiTang.Status) (mong doi 403)" }
    if ($haiTang.Body -notmatch 'Pos:0004') { $canhBao += 'Chiet khau hai tang khong tra ma Pos:0004' }

    $ghiNoThieuKhach = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = [guid]::NewGuid().ToString()
        warehouseId  = $kho.id
        lines        = @(@{ sku = $skuA; quantity = 1; unitPrice = 50000 })
        payments     = @(@{ method = 1; amount = 30000 })
    }
    In-KetQua -Nhan '⑨ GHI NO ma THIEU KHACH/HAN' -Status $ghiNoThieuKhach.Status -Body $ghiNoThieuKhach.Body
    if ($ghiNoThieuKhach.Status -ne 403) { $canhBao += "Ghi no thieu khach tra HTTP $($ghiNoThieuKhach.Status) (mong doi 403)" }
    if ($ghiNoThieuKhach.Body -notmatch 'Pos:0006') { $canhBao += 'Ghi no thieu khach khong tra ma Pos:0006' }

    # ── 10. Hoá đơn B: GHI NỢ có khách + hạn (để đo trả hàng trừ nợ trước) ──────────────────────────
    $banB = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = [guid]::NewGuid().ToString()
        warehouseId  = $kho.id
        customerId   = $khach.id
        dueDateUtc   = (Get-Date).AddDays(7).ToUniversalTime().ToString('o')
        lines        = @(@{ sku = $skuB; quantity = 2; unitPrice = 30000; productName = 'WP06 B (nợ)' })
        payments     = @(@{ method = 2; amount = 40000 })
    }
    In-KetQua -Nhan '⑩ BAN HOA DON B (ghi no 20.000 cho khach)' -Status $banB.Status -Body $banB.Body
    $hoaDonB = $null
    if ($banB.Status -eq 200) {
        $hoaDonB = ($banB.Body | ConvertFrom-Json).bill
        if ([double]$hoaDonB.debtAmount -ne 20000) { $canhBao += "no hoa don B = $($hoaDonB.debtAmount) (mong doi 20000)" }
    }
    else {
        $canhBao += "Ban hoa don B tra HTTP $($banB.Status)"
    }

    # ── 11. ⑩ TRẢ HÀNG: trả 1 A — trừ nợ trước, hoàn tiền sau ─────────────────────────────────────
    if ($null -ne $hoaDonA) {
        $traA = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/returns' -Body @{
            retailBillId    = $hoaDonA.id
            returnFeeAmount = 0
            refundMethod    = 1
            lines           = @(@{ sku = $skuA; quantity = 1 })
        }
        In-KetQua -Nhan '⑩ TRA HANG 1 x A (hoa don A da tra du ⇒ hoan tien mat)' -Status $traA.Status -Body $traA.Body
        if ($traA.Status -ne 201) { $canhBao += "Tao phieu tra tra HTTP $($traA.Status) (mong doi 201)" }

        if ($traA.Status -eq 201) {
            $pt = $traA.Body | ConvertFrom-Json
            if ([double]$pt.goodsAmount -ne 50000) { $canhBao += "goodsAmount = $($pt.goodsAmount) (mong doi 50000)" }
            if ([double]$pt.debtReductionAmount -ne 0) { $canhBao += "debtReduction = $($pt.debtReductionAmount) (mong doi 0)" }
            if ([double]$pt.refundAmount -ne 50000) { $canhBao += "refundAmount = $($pt.refundAmount) (mong doi 50000)" }

            $canBang = [double]$pt.goodsAmount - ([double]$pt.debtReductionAmount + [double]$pt.refundAmount + [double]$pt.returnFeeAmount)
            if ($canBang -ne 0) { $canhBao += "Phieu tra KHONG CAN BANG (lech $canBang)" }
        }
    }

    $tonASauTra = Ton -Sku $skuA
    Write-Host ("   Ton A sau khi tra 1 = {0} (phai la 9 = 8 + 1 nhap lai)" -f $tonASauTra.onHand) -ForegroundColor Green
    if ([double]$tonASauTra.onHand -ne 9) { $canhBao += "Ton A sau tra = $($tonASauTra.onHand) (mong doi 9)" }

    # ── 12. ⑪ Trả VƯỢT số đã mua ─────────────────────────────────────────────────────────────────────
    if ($null -ne $hoaDonA) {
        $traVuot = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/returns' -Body @{
            retailBillId    = $hoaDonA.id
            returnFeeAmount = 0
            refundMethod    = 1
            lines           = @(@{ sku = $skuA; quantity = 5 })
        }
        In-KetQua -Nhan '⑪ TRA VUOT so da mua (con 1, doi tra 5)' -Status $traVuot.Status -Body $traVuot.Body
        if ($traVuot.Status -ne 403) { $canhBao += "Tra vuot tra HTTP $($traVuot.Status) (mong doi 403)" }
        if ($traVuot.Body -notmatch 'Pos:0019') { $canhBao += 'Tra vuot khong tra ma Pos:0019' }
    }

    # ── 13. ⑫ HUỶ hoá đơn C + idempotent ────────────────────────────────────────────────────────────
    $banC = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = [guid]::NewGuid().ToString()
        warehouseId  = $kho.id
        lines        = @(@{ sku = $skuC; quantity = 1; unitPrice = 20000 })
        payments     = @(@{ method = 1; amount = 20000 })
    }
    $hoaDonC = $null
    if ($banC.Status -eq 200) { $hoaDonC = ($banC.Body | ConvertFrom-Json).bill }
    else { $canhBao += "Ban hoa don C tra HTTP $($banC.Status)" }

    if ($null -ne $hoaDonC) {
        $huyC = Goi-Bff -Method POST -Path "/api/proxy/v1/pos/bills/$($hoaDonC.id)/void" -Body @{ reason = 'Probe WP06: khach doi y' }
        In-KetQua -Nhan '⑫ HUY HOA DON C (nhap lai kho)' -Status $huyC.Status -Body $huyC.Body
        if ($huyC.Status -ne 200) { $canhBao += "Huy hoa don C tra HTTP $($huyC.Status)" }
        else {
            $kqHuy = $huyC.Body | ConvertFrom-Json
            if (-not $kqHuy.voided) { $canhBao += 'Huy hoa don C nhung voided = false' }
            if ($kqHuy.bill.status -ne 'Voided') { $canhBao += "Trang thai hoa don C = '$($kqHuy.bill.status)' (mong doi Voided)" }
        }

        $huyLanHai = Goi-Bff -Method POST -Path "/api/proxy/v1/pos/bills/$($hoaDonC.id)/void" -Body @{ reason = 'Probe WP06: huy lan hai' }
        In-KetQua -Nhan '⑫b HUY LAN HAI (idempotent — khong nhap kho lan hai)' -Status $huyLanHai.Status -Body $huyLanHai.Body
        if ($huyLanHai.Status -ne 200) { $canhBao += "Huy lan hai tra HTTP $($huyLanHai.Status) (mong doi 200)" }
        elseif (($huyLanHai.Body | ConvertFrom-Json).voided) { $canhBao += 'Huy lan hai VAN bao voided = true (da nhap kho 2 lan)' }

        $tonCSauHuy = Ton -Sku $skuC
        # C đã bị bán **2** lần: 1 ở hoá đơn A và 1 ở hoá đơn C ⇒ sau khi huỷ C, tồn = 10 − 1 = **9**.
        Write-Host ("   Ton C sau huy = {0} (phai la 9 = 10 - 1 da ban o hoa don A)" -f $tonCSauHuy.onHand) -ForegroundColor Green
        if ([double]$tonCSauHuy.onHand -ne 9) { $canhBao += "Ton C sau huy = $($tonCSauHuy.onHand) (mong doi 9)" }
    }

    # ── 14. ⑬ Trang IN hoá đơn ──────────────────────────────────────────────────────────────────────
    if ($null -ne $hoaDonA) {
        $inA = Lay-Trang -Path "/$business/print/bill/$($hoaDonA.id)"
        In-KetQua -Nhan '⑬ TRANG IN HOA DON A' -Status $inA.Status -Body ''
        if ($inA.Status -ne 200) { $canhBao += "Trang in tra HTTP $($inA.Status)" }
        $inSach = $inA.Body -replace '<!--.*?-->', ''
        foreach ($chuoi in @('HOÁ ĐƠN BÁN LẺ', $hoaDonA.number, 'PHẢI TRẢ')) {
            if ($inSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang in thieu chuoi '$chuoi'" }
        }
    }

    # ── 15. ⑭ KẾT CA — công thức Nhanh §5.3 ─────────────────────────────────────────────────────────
    # Doanh thu tiền mặt = 50.000 (hoá đơn A) + 0 (B trả bằng CK) + 0 (C đã huỷ) = 50.000
    # Trả hàng tiền mặt  = 50.000 (phiếu trả A)
    # Tiền cuối ca = 500.000 + 50.000 − 0 − 50.000 = 500.000 ⇒ bàn giao 500.000 ⇒ chênh lệch 0
    $ketCa = Goi-Bff -Method POST -Path "/api/proxy/v1/pos/sessions/$($ca.id)/close" -Body @{
        handoverCash = 500000
        otherExpense = 0
        note         = 'Probe WP06'
    }
    In-KetQua -Nhan '⑭ KET CA (ban giao 500.000)' -Status $ketCa.Status -Body $ketCa.Body
    if ($ketCa.Status -ne 200) { $canhBao += "Ket ca tra HTTP $($ketCa.Status)" }
    else {
        $ka = $ketCa.Body | ConvertFrom-Json
        Write-Host ("   cashRevenue {0} · cashRefund {1} · expectedCash {2} · variance {3}" -f `
                $ka.cashRevenue, $ka.cashRefund, $ka.expectedCash, $ka.variance) -ForegroundColor Green

        if ($ka.status -ne 'Closed') { $canhBao += "Ca sau ket co status '$($ka.status)' (mong doi Closed)" }
        if ([double]$ka.cashRevenue -ne 50000) { $canhBao += "cashRevenue = $($ka.cashRevenue) (mong doi 50000)" }
        if ([double]$ka.cashRefund -ne 50000) { $canhBao += "cashRefund = $($ka.cashRefund) (mong doi 50000)" }
        if ([double]$ka.expectedCash -ne 500000) { $canhBao += "expectedCash = $($ka.expectedCash) (mong doi 500000)" }
        if ([double]$ka.variance -ne 0) { $canhBao += "variance = $($ka.variance) (mong doi 0)" }
    }

    $ketCaLanHai = Goi-Bff -Method POST -Path "/api/proxy/v1/pos/sessions/$($ca.id)/close" -Body @{
        handoverCash = 500000
        otherExpense = 0
    }
    In-KetQua -Nhan '⑭b KET CA LAN HAI (da ket ca la khong quay nguoc ⇒ 0010)' -Status $ketCaLanHai.Status -Body $ketCaLanHai.Body
    if ($ketCaLanHai.Status -ne 403) { $canhBao += "Ket ca lan hai tra HTTP $($ketCaLanHai.Status) (mong doi 403)" }
    if ($ketCaLanHai.Body -notmatch 'Pos:0010') { $canhBao += 'Ket ca lan hai khong tra ma Pos:0010' }

    # ── 16. ⑮ Huỷ hoá đơn của ca đã kết ────────────────────────────────────────────────────────────
    if ($null -ne $hoaDonB) {
        $huySauKetCa = Goi-Bff -Method POST -Path "/api/proxy/v1/pos/bills/$($hoaDonB.id)/void" -Body @{ reason = 'Probe WP06: huy sau khi ket ca' }
        In-KetQua -Nhan '⑮ HUY HOA DON CUA CA DA KET (phai chan 0010)' -Status $huySauKetCa.Status -Body $huySauKetCa.Body
        if ($huySauKetCa.Status -ne 403) { $canhBao += "Huy sau ket ca tra HTTP $($huySauKetCa.Status) (mong doi 403)" }
        if ($huySauKetCa.Body -notmatch 'Pos:0010') { $canhBao += 'Huy sau ket ca khong tra ma Pos:0010' }
    }

    # ── 17. ⑯ Bất biến tổng sổ cái = OnHand (từng SKU) ─────────────────────────────────────────────
    foreach ($sku in @($skuA, $skuB, $skuC)) {
        $sqlTong = @"
select coalesce(sum(case when "MovementType" in (0,4,5,6,7,10) then "QuantityDelta"
                         when "MovementType" in (3,8,9,19,11,14,15,20,21) then -"QuantityDelta" else 0 end), 0)
from "InvStockLedger" where "Sku" = '__SKU__' and "IsDeleted" = false;
"@
        $sqlAnh = @"
select coalesce(sum("OnHand"), 0) from "InvItems" where "Sku" = '__SKU__' and "IsDeleted" = false;
"@

        $tongSoCai = Sql ($sqlTong -replace '__SKU__', $sku)
        $anhChup = Sql ($sqlAnh -replace '__SKU__', $sku)

        Write-Host ("⑯ BAT BIEN {0}: tong so cai = {1} · OnHand = {2}" -f $sku, $tongSoCai, $anhChup)
        if ($tongSoCai -ne $anhChup) { $canhBao += "BAT BIEN SAI ($sku): tong so cai ($tongSoCai) <> OnHand ($anhChup)" }
    }

    # ── Kết luận ─────────────────────────────────────────────────────────────────────────────────────
    Write-Host ''
    if ($canhBao.Count -eq 0) {
        Write-Host 'KET LUAN: TAT CA CA DO KHOP.' -ForegroundColor Green
        exit 0
    }

    Write-Host 'KET LUAN: CO CA KHONG KHOP:' -ForegroundColor Red
    $canhBao | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
finally {
    # 🔒 DÒNG ĐẦU TIÊN: `D-56` — cờ bảo mật phải về `true` **kể cả** khi probe `exit` giữa đường.
    BatLai-CoDoiMatKhau

    if (Test-Path $jar) { Remove-Item $jar -Force -ErrorAction SilentlyContinue }

    if ($null -ne $webProc -and -not $webProc.HasExited) {
        & taskkill.exe /PID $webProc.Id /T /F 2>$null | Out-Null
        Write-Host 'Da dung Next do chinh probe khoi dong.'
    }

    if ($null -ne $hostProc -and -not $hostProc.HasExited) {
        Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'Da dung Host do chinh probe khoi dong.'
    }

    Write-Host ''
    Write-Host "DU LIEU PROBE: kho=$maKho · SKU=$skuA/$skuB/$skuC"
}
