<#
.SYNOPSIS
    Probe HTTP thật cho **WP-07 — Khách hàng (điểm/công nợ/cấp) + Nhân viên & phạm vi dữ liệu**.

.DESCRIPTION
    Chạy **Host thật** (54275) + **Next thật** (3000) + **PostgreSQL thật**, đăng nhập qua **NextAuth**
    (đúng đường người dùng đi), rồi đo bằng HTTP qua **BFF** (`/api/proxy` — `D-12`):

      ①  trang `/{business}/customer/list`                        → 200 + "Khách hàng"
      ②  `POST …/customers`                                       → 200, khách mới
      ③  `PUT …/customers/{id}/segment` (loại + hạn mức nợ)       → 200, đọc lại ĐÚNG
      ④  `POST …/pos/bills` (dùng 3 điểm, trả một phần)           → 200, nợ 50.000
      ⑤  `GET …/customers/{id}`                                   → điểm 12 · tích luỹ 100.000 · nợ 50.000 ·
                                                                    **tự lên cấp** (ngưỡng 100.000)
      ⑥  `GET …/customers/tabs`                                   → tab "Còn nợ" ≥ 1
      ⑦  bán tiếp làm **vượt hạn mức nợ**                         → **409 `Customers:0019`** + **KHÔNG** có hoá đơn
                                                                    mới (rollback cả transaction POS)
      ⑧  `POST …/pos/returns` trả lại 1 món                       → điểm/tích luỹ/nợ của khách **giảm đúng**
      ⑨  huỷ một hoá đơn khác                                     → sổ khách **không đổi** (đảo về 0)
      ⑩  `GET …/customers/{id}/balance`                           → hai cờ đối chiếu **KHỚP**
      ⑪  đổi phạm vi dữ liệu `admin` ⇒ `Own`                      → `GET …/pos/bills` chỉ còn hoá đơn của mình;
                                                                    hoá đơn của người khác ⇒ **403 `Pos:0028`**
      ⑫  trả phạm vi `admin` về `All`                              → thấy lại toàn bộ
      ⑬  `GET …/settings/permissions`                             → có `eCommerce.Settings.Manage`
      ⑭  `PUT …/settings/permissions` (permission KHÔNG tồn tại)  → **400 `Settings:0001`**
      ⑮  `POST …/settings/staff` + `GET …/settings/staff`         → 200, nhân viên có trong DS
      ⑯  trang `/{business}/customer/{id}` + `/{business}/settings/staff` → 200 + chữ kỳ vọng

    🔒 Không in token/cookie. Mã `WP07-<random>` để chạy lại nhiều lần không đụng nhau.
    📌 Probe **để lại** 1 kho + 1 khách + 1 cấp khách + vài hoá đơn/phiếu trả + 1 nhân viên (rác kiểm chứng).

.PARAMETER BoQuaHost
    Host đã chạy sẵn ⇒ không bật/tắt Host.

.PARAMETER BoQuaWeb
    Next đã chạy sẵn ở cổng 3000 ⇒ không bật/tắt Next.

.NOTES
    🚨 **Ca ⑪/⑫ dùng SQL để đổi `CreatorId` của MỘT hoá đơn probe** — cách duy nhất chứng minh phạm vi
    `Own` **thật sự lọc** khi chỉ có một tài khoản đăng nhập. 📌 Đây là thao tác **dev-only** trên dữ liệu
    probe (không phải đường nghiệp vụ) và được **trả lại** ngay sau khi đo.
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
$maKho = "WP07-$ngauNhien"
$sku = "WP07-SP-$ngauNhien"
$maCap = "VIP-$ngauNhien"

$canhBao = @()
$hostProc = $null
$webProc = $null
$jar = Lay-DuongDanTam "wp07-cookies-$ngauNhien.txt"

function In-KetQua {
    param([string]$Nhan, [int]$Status, [string]$Body)

    Write-Host $Nhan
    Write-Host ("   HTTP {0}" -f $Status) -ForegroundColor $(if ($Status -lt 400) { 'Green' } else { 'Yellow' })
    if ($Body.Length -gt 0) {
        $gon = if ($Body.Length -gt 500) { $Body.Substring(0, 500) + '…' } else { $Body }
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

    # 📌 Kill theo **PID giữ cổng** (Host chạy bằng `dotnet <dll>` — `Stop-Process -Name` không khớp).
    $giuCong = @(Lay-PidDangNghe -Cong 54275)
    foreach ($pid_ in $giuCong) { Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $api
    $env:AuthServer__Authority = $api

    $hostProc = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa (Lay-DuongDanTam 'wp07-host.log') -TepLogLoi (Lay-DuongDanTam 'wp07-host.err')

    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $api -LogFile (Lay-DuongDanTam 'wp07-host.log') -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Host KHONG len duoc — xem log o tren.' -ForegroundColor Red
        exit 2
    }
}

try {
    # ── 0b. Dev-only: tắt cờ đổi mật khẩu (`D-56` / `WP-13` ①) ─────────────────────────────────────────
    # 📌 `Start-KhongCoDoiMatKhau` (trong `nen-tang.ps1`) THAY cho `UPDATE` viết tay: probe này đi **cả**
    #    đường token Host **và** đăng nhập BFF/NextAuth (BFF cũng xin token bằng password grant) ⇒ cờ phải
    #    tắt **suốt** lượt chạy, và `finally` **BẬT LẠI** (thiếu = commit một admin không bị buộc đổi mật
    #    khẩu + cổng DB nhóm ② đỏ ở lượt sau).
    Start-KhongCoDoiMatKhau
    Write-Host 'Dev-only: đã tắt cờ đổi mật khẩu của admin (DB dev).' -ForegroundColor DarkGray

    # ── 1. Token Host (SETUP: kho + tồn + cấp khách) ──────────────────────────────────────────────────
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
        -Body (@{ code = $maKho; name = "Quay WP07 $ngauNhien" } | ConvertTo-Json -Compress)

    $nhap = Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills" -Headers $h -ContentType 'application/json' `
        -Body (@{
            number         = "PN-WP07-$ngauNhien"
            type           = 0
            mode           = 5
            warehouseId    = $kho.id
            idempotencyKey = "WP07-NHAP-$ngauNhien"
            lines          = @(@{ sku = $sku; quantity = 20; unitPrice = 100000 })
        } | ConvertTo-Json -Depth 6 -Compress)
    Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills/$($nhap.id)/post" -Headers $h | Out-Null
    Write-Host "Setup: kho $maKho + nhap 20 x $sku @100.000 ⇒ ton 20." -ForegroundColor Green

    # Cấp khách **ngưỡng 100.000** — hoá đơn đầu tiên sẽ đưa khách lên cấp này (đo luật tự xếp cấp).
    $cap = Invoke-RestMethod -Method Post -Uri "$api/api/v1/customer-levels" -Headers $h -ContentType 'application/json' `
        -Body (@{
            code                 = $maCap
            name                 = "VIP $ngauNhien"
            minAccumulatedAmount = 100000
            discountPercent      = 5
            sortOrder            = 10
        } | ConvertTo-Json -Compress)
    Write-Host "Setup: cap khach $maCap (nguong 100.000)." -ForegroundColor Green

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
            -TepLogRa (Lay-DuongDanTam 'wp07-web.log') -TepLogLoi (Lay-DuongDanTam 'wp07-web.err')

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
            Write-Host 'Next KHONG len duoc o cong 3000 — xem %TEMP%\wp07-web.log' -ForegroundColor Red
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
            $tep = Lay-DuongDanTam "wp07-body-$ngauNhien.json"
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

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ① TRANG DS KHÁCH
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $trangDs = Lay-Trang -Path "/$business/customer/list"
    In-KetQua -Nhan '① TRANG DS KHACH (5 tab phan khuc)' -Status $trangDs.Status -Body ''
    if ($trangDs.Status -ne 200) { $canhBao += "Trang DS khach tra HTTP $($trangDs.Status)" }
    $dsSach = $trangDs.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @('Khách hàng', 'Khách lẻ', 'Còn nợ', 'Nhân viên')) {
        if ($dsSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang DS khach thieu chuoi '$chuoi'" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ② TẠO KHÁCH qua BFF
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $taoKhach = Goi-Bff -Method POST -Path '/api/proxy/v1/customers' -Body @{
        name           = "Khach WP07 $ngauNhien"
        phone          = "08$ngauNhien"
        idempotencyKey = "WP07-KHACH-$ngauNhien"
    }
    In-KetQua -Nhan '② TAO KHACH (POST /api/v1/customers)' -Status $taoKhach.Status -Body $taoKhach.Body
    if ($taoKhach.Status -notin @(200, 201)) { $canhBao += "Tao khach tra HTTP $($taoKhach.Status)" }
    $khach = $taoKhach.Body | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($khach.id)) { Write-Host 'Khong co id khach — dung.' -ForegroundColor Red; exit 1 }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ③ PHÂN KHÁCH (loại + hạn mức nợ)
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $phanKhach = Goi-Bff -Method PUT -Path "/api/proxy/v1/customers/$($khach.id)/segment" -Body @{
        type      = 1
        levelId   = $null
        debtLimit = 100000
        taxCode   = $null
        birthday  = $null
    }
    In-KetQua -Nhan '③ PHAN KHACH (loai=buon · han muc no 100.000)' -Status $phanKhach.Status -Body $phanKhach.Body
    if ($phanKhach.Status -ne 200) { $canhBao += "Phan khach tra HTTP $($phanKhach.Status)" }
    else {
        $pk = $phanKhach.Body | ConvertFrom-Json
        if ($pk.type -ne 'Wholesale') { $canhBao += "Loai khach = '$($pk.type)' (mong doi Wholesale)" }
        if ([double]$pk.debtLimit -ne 100000) { $canhBao += "Han muc no = $($pk.debtLimit) (mong doi 100000)" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ④ MỞ CA + BÁN 1 món dùng 3 điểm, trả một phần
    #    100.000 − 3.000 (3 điểm × 1.000) = 97.000; trả tiền mặt 47.000 ⇒ nợ 50.000; tích 15 điểm.
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $moCa = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/sessions/open' -Body @{
        warehouseId = $kho.id
        openingCash = 0
    }
    In-KetQua -Nhan '④a MO CA' -Status $moCa.Status -Body $moCa.Body
    if ($moCa.Status -ne 200) { $canhBao += "Mo ca tra HTTP $($moCa.Status)" }

    $clientBillA = [guid]::NewGuid().ToString()
    $banA = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId        = $clientBillA
        warehouseId         = $kho.id
        customerId          = $khach.id
        pointsUsed          = 3
        pointsEarned        = 15
        customerGivenAmount = 47000
        dueDateUtc          = (Get-Date).AddDays(15).ToString('o')
        lines               = @(@{ sku = $sku; quantity = 1; unitPrice = 100000 })
        payments            = @(@{ method = 1; amount = 47000 })
    }
    In-KetQua -Nhan '④b BAN 1 mon (dung 3 diem, tra 47.000, no 50.000)' -Status $banA.Status -Body $banA.Body
    if ($banA.Status -ne 200) { $canhBao += "Ban hoa don A tra HTTP $($banA.Status)" }
    $hoaDonA = $null
    if ($banA.Status -eq 200) {
        $hoaDonA = ($banA.Body | ConvertFrom-Json).bill
        if ([double]$hoaDonA.totalPayment -ne 97000) { $canhBao += "totalPayment = $($hoaDonA.totalPayment) (mong doi 97000)" }
        if ([double]$hoaDonA.debtAmount -ne 50000) { $canhBao += "debtAmount = $($hoaDonA.debtAmount) (mong doi 50000)" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑤ SỔ KHÁCH sau khi bán: điểm 12 · tích luỹ 100.000 · nợ 50.000 · TỰ LÊN CẤP
    #    📌 Tích luỹ = `Subtotal − DiscountTotal` = 100.000 (KHÔNG trừ điểm, KHÔNG gồm VAT — `D-45`).
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $chiTiet = Goi-Bff -Method GET -Path "/api/proxy/v1/customers/$($khach.id)"
    In-KetQua -Nhan '⑤ CHI TIET KHACH sau khi ban' -Status $chiTiet.Status -Body $chiTiet.Body
    if ($chiTiet.Status -ne 200) { $canhBao += "Chi tiet khach tra HTTP $($chiTiet.Status)" }
    else {
        $ct = $chiTiet.Body | ConvertFrom-Json
        Write-Host ("   diem {0} · tich luy {1} · no {2} · cap {3} · so hoa don {4}" -f `
                $ct.points, $ct.accumulatedAmount, $ct.debtBalance, $ct.levelName, $ct.soHoaDonBanLe) -ForegroundColor Green

        if ([double]$ct.points -ne 12) { $canhBao += "Diem khach = $($ct.points) (mong doi 12 = 15 tich - 3 tieu)" }
        if ([double]$ct.accumulatedAmount -ne 100000) { $canhBao += "Tien tich luy = $($ct.accumulatedAmount) (mong doi 100000)" }
        if ([double]$ct.debtBalance -ne 50000) { $canhBao += "Du no khach = $($ct.debtBalance) (mong doi 50000)" }
        if ([string]::IsNullOrWhiteSpace($ct.levelName)) { $canhBao += 'Khach KHONG duoc tu len cap du da dat nguong 100.000' }
        if ([int]$ct.soHoaDonBanLe -ne 1) { $canhBao += "So hoa don ban le = $($ct.soHoaDonBanLe) (mong doi 1)" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑥ TAB "CÒN NỢ"
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $tabs = Goi-Bff -Method GET -Path '/api/proxy/v1/customers/tabs'
    In-KetQua -Nhan '⑥ TAB DEM (5 tab phan khuc)' -Status $tabs.Status -Body $tabs.Body
    if ($tabs.Status -ne 200) { $canhBao += "Tab dem tra HTTP $($tabs.Status)" }
    else {
        $td = $tabs.Body | ConvertFrom-Json
        $tabNo = $td.tabs | Where-Object { $_.key -eq 'debt' }
        if ($null -eq $tabNo -or [int]$tabNo.count -lt 1) { $canhBao += 'Tab "Con no" dem < 1 sau khi khach ghi no' }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑦ VƯỢT HẠN MỨC NỢ ⇒ 409 `Customers:0019` + KHÔNG có hoá đơn mới (rollback cả transaction POS)
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $soHoaDonTruoc = Sql "select count(*) from ""PosRetailBills"" where ""CustomerId"" = '$($khach.id)' and ""IsDeleted"" = false;"

    $clientBillB = [guid]::NewGuid().ToString()
    $banB = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = $clientBillB
        warehouseId  = $kho.id
        customerId   = $khach.id
        dueDateUtc   = (Get-Date).AddDays(15).ToString('o')
        lines        = @(@{ sku = $sku; quantity = 1; unitPrice = 100000 })
        payments     = @()
    }
    In-KetQua -Nhan '⑦ VUOT HAN MUC NO (no cu 50.000 + 100.000 > 100.000)' -Status $banB.Status -Body $banB.Body
    if ($banB.Status -ne 409) { $canhBao += "Vuot han muc no tra HTTP $($banB.Status) (mong doi 409 — map tu Customers:0019)" }
    if ($banB.Body -notmatch 'Customers:0019') { $canhBao += 'Vuot han muc no khong tra ma Customers:0019' }

    $soHoaDonSau = Sql "select count(*) from ""PosRetailBills"" where ""CustomerId"" = '$($khach.id)' and ""IsDeleted"" = false;"
    Write-Host ("   so hoa don truoc {0} · sau {1} (phai BANG NHAU — rollback ca transaction)" -f $soHoaDonTruoc, $soHoaDonSau)
    if ($soHoaDonTruoc -ne $soHoaDonSau) { $canhBao += "Hoa don van duoc tao du so khach nem 0019 ($soHoaDonTruoc -> $soHoaDonSau)" }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑧ TRẢ HÀNG ⇒ sổ khách giảm đúng: điểm 0 · tích luỹ 0 · nợ 0
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    if ($null -ne $hoaDonA) {
        $traHang = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/returns' -Body @{
            retailBillId   = $hoaDonA.id
            returnFeeAmount = 0
            refundMethod   = 1
            pointsReversed = 15
            note           = 'Probe WP07: tra lai 1 mon'
            lines          = @(@{ sku = $sku; quantity = 1 })
        }
        In-KetQua -Nhan '⑧ TRA HANG (hang tra 100.000 · hoan 50.000 · tru no 50.000)' -Status $traHang.Status -Body $traHang.Body
        if ($traHang.Status -notin @(200, 201)) { $canhBao += "Tra hang tra HTTP $($traHang.Status)" }
        else {
            $th = $traHang.Body | ConvertFrom-Json
            if ([double]$th.goodsAmount -ne 100000) { $canhBao += "goodsAmount = $($th.goodsAmount) (mong doi 100000)" }
            if ([double]$th.debtReductionAmount -ne 50000) { $canhBao += "debtReductionAmount = $($th.debtReductionAmount) (mong doi 50000)" }
            if ([double]$th.refundAmount -ne 50000) { $canhBao += "refundAmount = $($th.refundAmount) (mong doi 50000)" }
        }

        $ctSauTra = Goi-Bff -Method GET -Path "/api/proxy/v1/customers/$($khach.id)"
        if ($ctSauTra.Status -eq 200) {
            $s = $ctSauTra.Body | ConvertFrom-Json
            Write-Host ("   sau tra: diem {0} · tich luy {1} · no {2}" -f $s.points, $s.accumulatedAmount, $s.debtBalance) -ForegroundColor Green
            if ([double]$s.points -ne 0) { $canhBao += "Diem sau tra = $($s.points) (mong doi 0 — kep o 0, khong am)" }
            if ([double]$s.accumulatedAmount -ne 0) { $canhBao += "Tich luy sau tra = $($s.accumulatedAmount) (mong doi 0)" }
            if ([double]$s.debtBalance -ne 0) { $canhBao += "No sau tra = $($s.debtBalance) (mong doi 0)" }
        }
        else { $canhBao += "Doc khach sau tra tra HTTP $($ctSauTra.Status)" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑨ HUỶ HOÁ ĐƠN ⇒ sổ khách KHÔNG đổi (không âm)
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $clientBillC = [guid]::NewGuid().ToString()
    $banC = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = $clientBillC
        warehouseId  = $kho.id
        customerId   = $khach.id
        lines        = @(@{ sku = $sku; quantity = 1; unitPrice = 100000 })
        payments     = @(@{ method = 1; amount = 100000 })
    }
    In-KetQua -Nhan '⑨a BAN HOA DON C (tra du 100.000)' -Status $banC.Status -Body $banC.Body

    if ($banC.Status -eq 200) {
        $hoaDonC = ($banC.Body | ConvertFrom-Json).bill

        $ctTruocHuy = (Goi-Bff -Method GET -Path "/api/proxy/v1/customers/$($khach.id)").Body | ConvertFrom-Json

        $huyC = Goi-Bff -Method POST -Path "/api/proxy/v1/pos/bills/$($hoaDonC.id)/void" -Body @{ reason = 'Probe WP07: khach doi y' }
        In-KetQua -Nhan '⑨b HUY HOA DON C' -Status $huyC.Status -Body $huyC.Body
        if ($huyC.Status -ne 200) { $canhBao += "Huy hoa don C tra HTTP $($huyC.Status)" }

        $ctSauHuy = (Goi-Bff -Method GET -Path "/api/proxy/v1/customers/$($khach.id)").Body | ConvertFrom-Json
        Write-Host ("   truoc huy: diem {0} · tich luy {1} · no {2} | sau huy: {3} · {4} · {5}" -f `
                $ctTruocHuy.points, $ctTruocHuy.accumulatedAmount, $ctTruocHuy.debtBalance,
            $ctSauHuy.points, $ctSauHuy.accumulatedAmount, $ctSauHuy.debtBalance) -ForegroundColor Green

        if ([double]$ctSauHuy.accumulatedAmount -ne 0) { $canhBao += "Tich luy sau huy = $($ctSauHuy.accumulatedAmount) (mong doi 0 — da dao ve 0)" }
        if ([double]$ctSauHuy.points -lt 0) { $canhBao += "Diem sau huy AM ($($ctSauHuy.points)) — vi pham bat bien" }
        if ([double]$ctSauHuy.debtBalance -ne 0) { $canhBao += "No sau huy = $($ctSauHuy.debtBalance) (mong doi 0)" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑩ ĐỐI CHIẾU SỔ (hai cờ phải KHỚP)
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $doiChieu = Goi-Bff -Method GET -Path "/api/proxy/v1/customers/$($khach.id)/balance"
    In-KetQua -Nhan '⑩ DOI CHIEU SO DIEM / CONG NO' -Status $doiChieu.Status -Body $doiChieu.Body
    if ($doiChieu.Status -ne 200) { $canhBao += "Doi chieu so tra HTTP $($doiChieu.Status)" }
    else {
        $dc = $doiChieu.Body | ConvertFrom-Json
        if (-not $dc.tienTichLuyKhop) { $canhBao += 'Tien tich luy KHONG khop voi (Σ hoa don − Σ tra hang)' }
        if (-not $dc.congNoKhop) { $canhBao += 'Cong no KHONG khop voi Σ no tren hoa don' }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑪⑫ PHẠM VI DỮ LIỆU `Own` — hoá đơn của NGƯỜI KHÁC phải biến mất (và trả lại sau khi đo)
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # Tạo 1 hoá đơn rồi đổi `CreatorId` sang GUID khác (dev-only) — cách duy nhất chứng minh `Own` LỌC THẬT
    # khi chỉ có một tài khoản đăng nhập.
    $clientBillD = [guid]::NewGuid().ToString()
    $banD = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = $clientBillD
        warehouseId  = $kho.id
        customerId   = $khach.id
        lines        = @(@{ sku = $sku; quantity = 1; unitPrice = 100000 })
        payments     = @(@{ method = 1; amount = 100000 })
    }
    In-KetQua -Nhan '⑪a BAN HOA DON D (de do pham vi du lieu)' -Status $banD.Status -Body $banD.Body

    if ($banD.Status -eq 200) {
        $hoaDonD = ($banD.Body | ConvertFrom-Json).bill
        $nguoiKhac = [guid]::NewGuid().ToString()

        # ⚠️ Dev-only: giả lập *"hoá đơn do người khác tạo"*. Trả lại `CreatorId` ngay sau khi đo.
        $creatorGoc = Sql "select ""CreatorId"" from ""PosRetailBills"" where ""Id"" = '$($hoaDonD.id)';"
        Sql "update ""PosRetailBills"" set ""CreatorId"" = '$nguoiKhac' where ""Id"" = '$($hoaDonD.id)';" | Out-Null

        $datPhamVi = Goi-Bff -Method PUT -Path '/api/proxy/v1/settings/data-scopes' -Body @{ roleName = 'admin'; scope = 2 }
        In-KetQua -Nhan '⑪b DAT PHAM VI admin = Own (chi du lieu cua toi)' -Status $datPhamVi.Status -Body $datPhamVi.Body
        if ($datPhamVi.Status -ne 200) { $canhBao += "Dat pham vi tra HTTP $($datPhamVi.Status)" }

        $toi = Goi-Bff -Method GET -Path '/api/proxy/v1/settings/data-scopes/me'
        if ($toi.Status -eq 200) {
            $t = $toi.Body | ConvertFrom-Json
            Write-Host ("   pham vi hieu luc cua toi: {0} — {1}" -f $t.phamVi, $t.lyDo) -ForegroundColor Green
            if ($t.phamVi -ne 'Own') { $canhBao += "Pham vi cua toi = '$($t.phamVi)' (mong doi Own)" }
        }
        else { $canhBao += "Doc pham vi cua toi tra HTTP $($toi.Status)" }

        # ① DS **không** được chứa hoá đơn D (CreatorId = người khác).
        $dsHoaDon = Goi-Bff -Method GET -Path '/api/proxy/v1/pos/bills?maxResultCount=200'
        if ($dsHoaDon.Status -eq 200) {
            $ds = $dsHoaDon.Body | ConvertFrom-Json
            $coD = @($ds.items | Where-Object { $_.id -eq $hoaDonD.id }).Count
            Write-Host ("   pham vi Own: DS hoa don co {0} dong · chua hoa don D? {1}" -f @($ds.items).Count, $(if ($coD -gt 0) { 'CO (SAI)' } else { 'KHONG (dung)' })) -ForegroundColor Green
            if ($coD -gt 0) { $canhBao += 'Pham vi Own nhung DS VAN chua hoa don cua nguoi khac — loc o CLIENT, khong phai server' }
        }
        else { $canhBao += "DS hoa don (pham vi Own) tra HTTP $($dsHoaDon.Status)" }

        # ② Đọc CHI TIẾT hoá đơn D ⇒ **403 `Pos:0028`** (không được 200).
        $docD = Goi-Bff -Method GET -Path "/api/proxy/v1/pos/bills/$($hoaDonD.id)"
        In-KetQua -Nhan '⑪c DOC CHI TIET hoa don NGOAI PHAM VI' -Status $docD.Status -Body $docD.Body
        if ($docD.Status -ne 403) { $canhBao += "Doc hoa don ngoai pham vi tra HTTP $($docD.Status) (mong doi 403)" }
        if ($docD.Body -notmatch 'Pos:0028') { $canhBao += 'Doc hoa don ngoai pham vi khong tra ma Pos:0028' }

        # ⑫ Trả lại `All` + `CreatorId` gốc.
        $traLai = Goi-Bff -Method PUT -Path '/api/proxy/v1/settings/data-scopes' -Body @{ roleName = 'admin'; scope = 0 }
        In-KetQua -Nhan '⑫ TRA PHAM VI admin = All' -Status $traLai.Status -Body $traLai.Body
        if ($traLai.Status -ne 200) { $canhBao += "Tra pham vi tra HTTP $($traLai.Status)" }

        Sql "update ""PosRetailBills"" set ""CreatorId"" = '$creatorGoc' where ""Id"" = '$($hoaDonD.id)';" | Out-Null

        $dsLai = Goi-Bff -Method GET -Path '/api/proxy/v1/pos/bills?maxResultCount=200'
        if ($dsLai.Status -eq 200) {
            $lai = $dsLai.Body | ConvertFrom-Json
            $coLai = @($lai.items | Where-Object { $_.id -eq $hoaDonD.id }).Count
            Write-Host ("   sau khi tra All: DS co {0} dong · thay lai hoa don D? {1}" -f @($lai.items).Count, $(if ($coLai -gt 0) { 'CO (dung)' } else { 'KHONG (sai)' })) -ForegroundColor Green
            if ($coLai -eq 0) { $canhBao += 'Tra pham vi All nhung DS VAN khong thay hoa don D' }
        }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑬⑭ MA TRẬN QUYỀN
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $maTran = Goi-Bff -Method GET -Path '/api/proxy/v1/settings/permissions'
    In-KetQua -Nhan '⑬ MA TRAN QUYEN' -Status $maTran.Status -Body ''
    if ($maTran.Status -ne 200) { $canhBao += "Ma tran quyen tra HTTP $($maTran.Status)" }
    else {
        $mt = $maTran.Body | ConvertFrom-Json
        $tenQuyen = @($mt.groups | ForEach-Object { $_.permissions } | ForEach-Object { $_.name })
        foreach ($can in @('eCommerce.Customers.Manage', 'eCommerce.Settings.Manage', 'eCommerce.Settings')) {
            if ($tenQuyen -notcontains $can) { $canhBao += "Ma tran thieu quyen '$can'" }
        }
        Write-Host ("   {0} nhom · {1} quyen · {2} vai tro" -f @($mt.groups).Count, $tenQuyen.Count, @($mt.roles).Count) -ForegroundColor Green
        if (@($mt.roles) -notcontains 'admin') { $canhBao += "Ma tran khong co vai tro 'admin'" }
    }

    $quyenSai = Goi-Bff -Method PUT -Path '/api/proxy/v1/settings/permissions' -Body @{
        roleName       = 'admin'
        permissionName = 'eCommerce.Khong.Ton.Tai'
        isGranted      = $true
    }
    In-KetQua -Nhan '⑭ DAT QUYEN KHONG TON TAI (phai chan Settings:0001)' -Status $quyenSai.Status -Body $quyenSai.Body
    if ($quyenSai.Status -ne 400) { $canhBao += "Dat quyen khong ton tai tra HTTP $($quyenSai.Status) (mong doi 400)" }
    if ($quyenSai.Body -notmatch 'Settings:0001') { $canhBao += 'Dat quyen khong ton tai khong tra ma Settings:0001' }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑮ NHÂN VIÊN
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $taoNv = Goi-Bff -Method POST -Path '/api/proxy/v1/settings/staff' -Body @{
        code           = "NV-$ngauNhien"
        name           = "Nhan vien WP07 $ngauNhien"
        identityUserId = $null
        storeId        = $null
        departmentId   = $null
        roleName       = 'admin'
        phone          = $null
        note           = 'Probe WP07'
        isActive       = $true
    }
    In-KetQua -Nhan '⑮a TAO NHAN VIEN' -Status $taoNv.Status -Body $taoNv.Body
    if ($taoNv.Status -notin @(200, 201)) { $canhBao += "Tao nhan vien tra HTTP $($taoNv.Status)" }
    else {
        $nv = $taoNv.Body | ConvertFrom-Json
        if ($nv.phamViHieuLuc -ne 'All') { $canhBao += "Pham vi hieu luc cua NV = '$($nv.phamViHieuLuc)' (mong doi All)" }
    }

    $dsNv = Goi-Bff -Method GET -Path '/api/proxy/v1/settings/staff'
    In-KetQua -Nhan '⑮b DS NHAN VIEN' -Status $dsNv.Status -Body ''
    if ($dsNv.Status -ne 200) { $canhBao += "DS nhan vien tra HTTP $($dsNv.Status)" }
    elseif ($dsNv.Body -notmatch "NV-$ngauNhien") { $canhBao += 'DS nhan vien KHONG co nhan vien vua tao' }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑯ TRANG CHI TIẾT KHÁCH + TRANG NHÂN VIÊN
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $trangCt = Lay-Trang -Path "/$business/customer/$($khach.id)"
    In-KetQua -Nhan '⑯a TRANG CHI TIET KHACH (5 tab)' -Status $trangCt.Status -Body ''
    if ($trangCt.Status -ne 200) { $canhBao += "Trang chi tiet khach tra HTTP $($trangCt.Status)" }
    $ctSach = $trangCt.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @('Tổng quan', 'Đơn hàng', 'Hoá đơn bán lẻ', 'Công nợ', 'Chăm sóc')) {
        if ($ctSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang chi tiet khach thieu tab '$chuoi'" }
    }

    $trangNv = Lay-Trang -Path "/$business/settings/staff"
    In-KetQua -Nhan '⑯b TRANG NHAN VIEN & MA TRAN QUYEN' -Status $trangNv.Status -Body ''
    if ($trangNv.Status -ne 200) { $canhBao += "Trang nhan vien tra HTTP $($trangNv.Status)" }
    $nvSach = $trangNv.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @('Nhân viên', 'Ma trận quyền', 'Phạm vi dữ liệu')) {
        if ($nvSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang nhan vien thieu chuoi '$chuoi'" }
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
    Write-Host "DU LIEU PROBE: kho=$maKho · SKU=$sku · cap=$maCap"
}
