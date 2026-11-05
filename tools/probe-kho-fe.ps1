<#
.SYNOPSIS
    Probe HTTP thật cho **WP-04 (FE)** — DS phiếu kho, tạo phiếu, cột tồn, và luật "ghi sổ mới đổi tồn".

.DESCRIPTION
    Chạy **Host thật** (API 54275) + **Next thật** (3000) + **PostgreSQL thật**, đăng nhập qua **NextAuth**
    (đúng đường người dùng đi), rồi đo bằng HTTP qua **BFF** (`/api/proxy` — chính là đường mà client component
    gọi, `D-12`):

      ① trang DS phiếu `/{business}/inventory/bill`                 → 200, có "Phiếu kho"
      ② trang tạo phiếu `/{business}/inventory/bill/new`             → 200, có "Kiểu giao dịch"
      ③ `POST /api/proxy/v1/stock/bills` (tạo phiếu nhập 10)         → 201
      ④ trang chi tiết TRƯỚC khi ghi sổ                              → 200, "Nháp" + "chưa có dòng tồn"
      ⑤ `POST /api/proxy/v1/stock/bills/{id}/post`                   → 200, postedEntries=1
      ⑥ `POST …/post` lần hai                                        → 403 + `eCommerce:Inventory:0010`
      ⑦ trang chi tiết SAU khi ghi sổ                                → 200, "Đã ghi sổ" + tồn `Bán 10`
      ⑧ trang tồn kho `/{business}/product/inventory?sku=`           → 200, có SKU + `Bán 10`
      ⑨ `GET /api/proxy/v1/stock/balances?sku=`                      → 200, onHand = 10 (qua BFF)
      ⑩ DS phiếu sau khi ghi sổ                                      → 200, có số phiếu + "Đã ghi sổ"
      ⑪ bất biến tổng sổ cái = ảnh chụp (SQL)                        → bằng nhau

    🔒 Không in token/cookie. Mã `WPF5-<random>` để chạy lại nhiều lần không đụng nhau.
    📌 Probe **để lại** 1 kho + 1 phiếu + 1 dòng tồn (rác kiểm chứng) trong DB dev — cố ý, để phiên sau
    kiểm lại được bằng SQL.

.PARAMETER BoQuaHost
    Host đã chạy sẵn ⇒ không bật/tắt Host.

.PARAMETER BoQuaWeb
    Next đã chạy sẵn ở cổng 3000 ⇒ không bật/tắt Next.

.EXAMPLE
    pwsh -File .\tools\probe-kho-fe.ps1
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
$sku = "WPF5-SKU-$ngauNhien"
$maKho = "WPF5-$ngauNhien"
$maPhieu = "PN-WPF5-$ngauNhien"

$canhBao = @()
$hostProc = $null
$webProc = $null
$jar = Lay-DuongDanTam "wpf5-cookies-$ngauNhien.txt"

function In-KetQua {
    param([string]$Nhan, [int]$Status, [string]$Body)

    Write-Host $Nhan
    Write-Host ("   HTTP {0}" -f $Status) -ForegroundColor $(if ($Status -lt 400) { 'Green' } else { 'Yellow' })
    if ($Body.Length -gt 0) {
        $gon = if ($Body.Length -gt 500) { $Body.Substring(0, 500) + '…' } else { $Body }
        Write-Host ("   {0}" -f ($gon -replace '\s+', ' '))
    }
}

# ── 0. Host ───────────────────────────────────────────────────────────────────────────────────────────────
if (-not $BoQuaHost) {
    $bin = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host' -GocRepo $gocRepo
    if (-not (Test-Path (Join-Path $bin 'eCommerce.HttpApi.Host.dll'))) {
        Write-Host "KHONG THAY binary Host tai $bin — build truoc." -ForegroundColor Red
        exit 2
    }

    Get-Process eCommerce.HttpApi.Host -ErrorAction SilentlyContinue | Stop-Process -Force

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $api
    $env:AuthServer__Authority = $api

    $hostProc = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa (Lay-DuongDanTam 'wpf5-host.log') -TepLogLoi (Lay-DuongDanTam 'wpf5-host.err')

    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $api -LogFile (Lay-DuongDanTam 'wpf5-host.log') -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Host KHONG len duoc — xem log o tren.' -ForegroundColor Red
        exit 2
    }
}

try {
    # ── 1. Token Host (để tạo kho + truy vấn SQL) ─────────────────────────────────────────────────────────
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

    # ── 2. Kho (API thật, không qua UI — UI chưa có màn tạo kho) ──────────────────────────────────────────
    $khoKq = Invoke-RestMethod -Method Post -Uri "$api/api/app/warehouse" -Headers $h -ContentType 'application/json' `
        -Body (@{ code = $maKho; name = "Kho WPF5 $ngauNhien" } | ConvertTo-Json -Compress)
    Write-Host ("① Kho tao: {0} ({1})" -f $khoKq.code, $khoKq.id) -ForegroundColor Green

    # ── 3. Next ───────────────────────────────────────────────────────────────────────────────────────────
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
            -TepLogRa (Lay-DuongDanTam 'wpf5-web.log') -TepLogLoi (Lay-DuongDanTam 'wpf5-web.err')

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
            Write-Host 'Next KHONG len duoc o cong 3000 — xem %TEMP%\wpf5-web.log' -ForegroundColor Red
            exit 2
        }
    }
    elseif ($webDaChay) {
        # 🚨 Next đang chạy sẵn có thể là bản `next start` của **build CŨ** ⇒ route mới trả 404 mà
        # không có lỗi nào ở API. Đó chính là ca đã gặp khi làm `N5` (probe báo 404 toàn bộ trang).
        Write-Host '⚠️  Dung Next dang chay san o cong 3000 — neu no la build CU thi trang moi se 404.' -ForegroundColor Yellow
        Write-Host '    Muon chac chan: dung no, chay `pnpm --filter @ecommerce/admin-web build` roi chay lai probe.' -ForegroundColor Yellow
    }

    Write-Host "Next: OK ($web)" -ForegroundColor Green

    # ── 4. Đăng nhập qua NextAuth (đúng đường người dùng) ────────────────────────────────────────────────
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
            $tep = Lay-DuongDanTam "wpf5-body-$ngauNhien.json"
            ($Body | ConvertTo-Json -Depth 8 -Compress) | Set-Content -Path $tep -Encoding utf8 -NoNewline
            $thamSo += @('-H', 'Content-Type: application/json', '--data-binary', "@$tep")
        }

        $raw = (& (Lay-Curl) @thamSo) -join "`n"
        $i = $raw.LastIndexOf('__HTTP__')
        [pscustomobject]@{
            Status = [int]$raw.Substring($i + 8)
            Body   = $raw.Substring(0, $i)
        }
    }

    # ── 5. ① DS phiếu ─────────────────────────────────────────────────────────────────────────────────────
    $ds = Lay-Trang -Path "/$business/inventory/bill"
    In-KetQua -Nhan '① TRANG DS PHIEU KHO' -Status $ds.Status -Body ''
    if ($ds.Status -ne 200) { $canhBao += "DS phieu tra HTTP $($ds.Status)" }
    if ($ds.Body -notmatch 'Phiếu kho') { $canhBao += 'DS phieu khong co chu "Phieu kho"' }
    if ($ds.Body -notmatch 'Tạo phiếu') { $canhBao += 'DS phieu thieu nut "Tao phieu"' }

    # ── 6. ② Trang tạo phiếu ─────────────────────────────────────────────────────────────────────────────
    $tao = Lay-Trang -Path "/$business/inventory/bill/new"
    In-KetQua -Nhan '② TRANG TAO PHIEU (form dong)' -Status $tao.Status -Body ''
    if ($tao.Status -ne 200) { $canhBao += "Trang tao phieu tra HTTP $($tao.Status)" }
    if ($tao.Body -notmatch 'Kiểu giao dịch') { $canhBao += 'Form tao phieu thieu "Kieu giao dich"' }
    if ($tao.Body -notmatch 'Kho WPF5') { $canhBao += 'Form tao phieu khong thay kho vua tao (dropdown kho rong?)' }

    # ── 7. ③ Tạo phiếu qua BFF ────────────────────────────────────────────────────────────────────────────
    $taoPhieu = Goi-Bff -Method POST -Path '/api/proxy/v1/stock/bills' -Body @{
        number           = $maPhieu
        type             = 0
        mode             = 5
        warehouseId      = $khoKq.id
        counterpartyType = 2
        idempotencyKey   = "WPF5-IDEM-$ngauNhien"
        note             = 'WP-04 FE probe'
        lines            = @(@{ sku = $sku; quantity = 10; unitPrice = 25000; vatPercent = 8 })
    }
    In-KetQua -Nhan '③ TAO PHIEU QUA BFF (/api/proxy/v1/stock/bills)' -Status $taoPhieu.Status -Body $taoPhieu.Body
    if ($taoPhieu.Status -ne 201) { $canhBao += "Tao phieu qua BFF tra HTTP $($taoPhieu.Status)" }
    $phieuId = if ($taoPhieu.Status -eq 201) { ($taoPhieu.Body | ConvertFrom-Json).id } else { $null }
    if ($null -eq $phieuId) {
        Write-Host 'Khong tao duoc phieu — dung cac ca sau.' -ForegroundColor Red
        exit 1
    }

    # ── 8. ④ Chi tiết TRƯỚC khi ghi sổ ───────────────────────────────────────────────────────────────────
    $truoc = Lay-Trang -Path "/$business/inventory/bill/$phieuId"
    In-KetQua -Nhan '④ CHI TIET PHIEU TRUOC KHI GHI SO' -Status $truoc.Status -Body ''
    if ($truoc.Status -ne 200) { $canhBao += "Chi tiet phieu tra HTTP $($truoc.Status)" }
    if ($truoc.Body -notmatch [regex]::Escape($maPhieu)) { $canhBao += 'Chi tiet khong hien so phieu' }
    if ($truoc.Body -notmatch 'Nháp') { $canhBao += 'Phieu moi khong o trang thai Nhap' }
    if ($truoc.Body -notmatch 'chưa có dòng tồn') { $canhBao += 'Truoc khi ghi so da thay dong ton (sai: Draft khong duoc doi ton)' }

    # ── 9. ⑤ Ghi sổ ──────────────────────────────────────────────────────────────────────────────────────
    $post1 = Goi-Bff -Method POST -Path "/api/proxy/v1/stock/bills/$phieuId/post"
    In-KetQua -Nhan '⑤ GHI SO (post) qua BFF' -Status $post1.Status -Body $post1.Body
    if ($post1.Status -ne 200) { $canhBao += "Ghi so tra HTTP $($post1.Status)" }
    $soButToan = if ($post1.Status -eq 200) { ($post1.Body | ConvertFrom-Json).postedEntries } else { 0 }
    if ($soButToan -lt 1) { $canhBao += "Ghi so khong sinh but toan (postedEntries=$soButToan)" }

    # ── 10. ⑥ Ghi sổ lần hai (phải bị chặn) ──────────────────────────────────────────────────────────────
    $post2 = Goi-Bff -Method POST -Path "/api/proxy/v1/stock/bills/$phieuId/post"
    In-KetQua -Nhan '⑥ GHI SO LAN HAI (phai bi chan 0010)' -Status $post2.Status -Body $post2.Body
    if ($post2.Status -ne 403) { $canhBao += "Ghi so lan hai tra HTTP $($post2.Status) (mong doi 403)" }
    if ($post2.Body -notmatch 'Inventory:0010') { $canhBao += 'Ghi so lan hai khong tra ma Inventory:0010 (UI se khong hien duoc ly do)' }

    # ── 11. ⑦ Chi tiết SAU khi ghi sổ (tồn đổi TRÊN UI) ───────────────────────────────────────────────────
    $sau = Lay-Trang -Path "/$business/inventory/bill/$phieuId"
    In-KetQua -Nhan '⑦ CHI TIET PHIEU SAU KHI GHI SO' -Status $sau.Status -Body ''
    if ($sau.Status -ne 200) { $canhBao += "Chi tiet sau ghi so tra HTTP $($sau.Status)" }
    if ($sau.Body -notmatch 'Đã ghi sổ') { $canhBao += 'Sau khi ghi so UI khong hien "Da ghi so"' }
    # React SSR chèn `<!-- -->` giữa text và biểu thức ⇒ bỏ comment trước khi so khớp.
    $sauSach = $sau.Body -replace '<!--.*?-->', ''
    if ($sauSach -notmatch 'Bán\s+10') { $canhBao += 'Sau khi ghi so UI khong hien ton "Ban 10"' }

    # ── 12. ⑧ Trang tồn kho ───────────────────────────────────────────────────────────────────────────────
    $ton = Lay-Trang -Path "/$business/product/inventory?sku=$sku"
    In-KetQua -Nhan '⑧ TRANG TON KHO (loc theo SKU)' -Status $ton.Status -Body ''
    if ($ton.Status -ne 200) { $canhBao += "Trang ton kho tra HTTP $($ton.Status)" }
    if ($ton.Body -notmatch [regex]::Escape($sku)) { $canhBao += 'Trang ton kho khong co SKU vua nhap' }
    $tonSach = $ton.Body -replace '<!--.*?-->', ''
    if ($tonSach -notmatch 'Bán\s+10') { $canhBao += 'Trang ton kho khong hien "Ban 10"' }

    # ── 13. ⑨ Tồn qua BFF (JSON) ─────────────────────────────────────────────────────────────────────────
    $tonJson = Goi-Bff -Method GET -Path "/api/proxy/v1/stock/balances?sku=$sku"
    In-KetQua -Nhan '⑨ TON QUA BFF (JSON)' -Status $tonJson.Status -Body $tonJson.Body
    if ($tonJson.Status -eq 200) {
        $dong = ($tonJson.Body | ConvertFrom-Json).items | Select-Object -First 1
        if ($null -eq $dong) { $canhBao += 'BFF khong tra dong ton nao' }
        elseif ([double]$dong.onHand -ne 10) { $canhBao += "onHand qua BFF = $($dong.onHand) (mong doi 10)" }
    }
    else {
        $canhBao += "Ton qua BFF tra HTTP $($tonJson.Status)"
    }

    # ── 13b. ⑯ TIỀN CỦA PHIẾU (BFF) — 10 x 25.000, VAT 8% ⇒ 250.000 / 20.000 / 250.000 ────────────────────
    $tienJson = Goi-Bff -Method GET -Path "/api/proxy/v1/stock/bills/$phieuId"
    In-KetQua -Nhan '⑯ TIEN CUA PHIEU (BFF: totalAmount/totalVat/totalPayment)' -Status $tienJson.Status -Body $tienJson.Body
    if ($tienJson.Status -eq 200) {
        $pt = $tienJson.Body | ConvertFrom-Json
        if ([double]$pt.totalAmount -ne 250000) { $canhBao += "totalAmount = $($pt.totalAmount) (mong doi 250000)" }
        if ([double]$pt.totalVat -ne 20000) { $canhBao += "totalVat = $($pt.totalVat) (mong doi 20000)" }
        if ([double]$pt.totalPayment -ne 270000) { $canhBao += "totalPayment = $($pt.totalPayment) (mong doi 270000 = 250.000 hang + 20.000 VAT)" }
    }
    else {
        $canhBao += "Tien qua BFF tra HTTP $($tienJson.Status)"
    }

    # ── 13c. ⑰ TIỀN TRÊN UI — chi tiết phiếu hiện Đơn giá/Thành tiền/Tổng tiền (định dạng vi-VN) ──────────
    $tienUi = Lay-Trang -Path "/$business/inventory/bill/$phieuId"
    $tienUiSach = $tienUi.Body -replace '<!--.*?-->', ''
    Write-Host '⑰ TIEN TREN UI (chi tiet phieu)'
    Write-Host ("   HTTP {0}" -f $tienUi.Status) -ForegroundColor $(if ($tienUi.Status -eq 200) { 'Green' } else { 'Yellow' })
    if ($tienUi.Status -ne 200) { $canhBao += "Chi tiet (tien) tra HTTP $($tienUi.Status)" }
    foreach ($chuoiUi in @('Đơn giá', 'Thành tiền', 'Tổng tiền hàng', '250.000', '25.000')) {
        if ($tienUiSach -notmatch [regex]::Escape($chuoiUi)) { $canhBao += "Chi tiet thieu chuoi '$chuoiUi'" }
    }

    # ── 13d. ⑱ DÒNG KHÔNG KHAI GIÁ ⇒ tổng phải là NULL (không phải 0 — D-37) ──────────────────────────────
    $khongGia = Goi-Bff -Method POST -Path '/api/proxy/v1/stock/bills' -Body @{
        number         = "PN-WPF5-NOGIA-$ngauNhien"
        type           = 0
        mode           = 5
        warehouseId    = $khoKq.id
        idempotencyKey = "WPF5-NOGIA-$ngauNhien"
        lines          = @(@{ sku = "$sku-NOGIA"; quantity = 2 })
    }
    $khongGiaId = if ($khongGia.Status -eq 201) { ($khongGia.Body | ConvertFrom-Json).id } else { $null }
    In-KetQua -Nhan '⑱ TAO PHIEU KHONG KHAI GIA' -Status $khongGia.Status -Body $khongGia.Body
    if ($null -eq $khongGiaId) {
        $canhBao += "Tao phieu khong gia tra HTTP $($khongGia.Status)"
    }
    else {
        $tongKhongGia = Goi-Bff -Method GET -Path "/api/proxy/v1/stock/bills/$khongGiaId"
        if ($tongKhongGia.Status -eq 200) {
            $tk = $tongKhongGia.Body | ConvertFrom-Json
            Write-Host ("   totalAmount = {0} (phai la null)" -f $tk.totalAmount)
            if ($null -ne $tk.totalAmount) { $canhBao += "Phieu thieu gia nhung totalAmount = $($tk.totalAmount) (phai NULL)" }
        }
        else {
            $canhBao += "Doc phieu khong gia tra HTTP $($tongKhongGia.Status)"
        }
    }

    # ── 13e. ⑲ ĐƠN GIÁ ÂM ⇒ 400 (chặn ở DTO) ────────────────────────────────────────────────────────────
    $giaAm = Goi-Bff -Method POST -Path '/api/proxy/v1/stock/bills' -Body @{
        number         = "PN-WPF5-AM-$ngauNhien"
        type           = 0
        mode           = 5
        warehouseId    = $khoKq.id
        idempotencyKey = "WPF5-AM-$ngauNhien"
        lines          = @(@{ sku = $sku; quantity = 1; unitPrice = -1 })
    }
    In-KetQua -Nhan '⑲ DON GIA AM (phai bi chan)' -Status $giaAm.Status -Body $giaAm.Body
    if ($giaAm.Status -ne 400 -and $giaAm.Status -ne 403) { $canhBao += "Don gia am tra HTTP $($giaAm.Status) (mong doi 400)" }

    # ── 14. ⑩ DS phiếu sau khi ghi sổ ─────────────────────────────────────────────────────────────────────
    $ds2 = Lay-Trang -Path "/$business/inventory/bill?status=Posted"
    In-KetQua -Nhan '⑩ DS PHIEU SAU KHI GHI SO (tab Da ghi so)' -Status $ds2.Status -Body ''
    if ($ds2.Status -ne 200) { $canhBao += "DS phieu (Posted) tra HTTP $($ds2.Status)" }
    if ($ds2.Body -notmatch [regex]::Escape($maPhieu)) { $canhBao += 'Tab "Da ghi so" khong co phieu vua ghi' }

    # ── 14b. ⑬ Thao tác hàng loạt có mặt trên DS phiếu ────────────────────────────────────────────────────
    $dsBulk = Lay-Trang -Path "/$business/inventory/bill"
    $bulkCanCo = @('Ghi sổ phiếu đã chọn', 'Huỷ phiếu đã chọn', 'Xuất CSV', 'In phiếu', 'type="checkbox"')
    $thieuBulk = @($bulkCanCo | Where-Object { $dsBulk.Body -notmatch [regex]::Escape($_) })
    Write-Host '⑬ DS PHIEU CO THAO TAC HANG LOAT (bulk + chon dong + xuat CSV + in)'
    Write-Host ("   HTTP {0} · can co: {1} · thieu: {2}" -f $dsBulk.Status, ($bulkCanCo -join ' | '), $(if ($thieuBulk.Count -eq 0) { 'khong' } else { $thieuBulk -join ' | ' })) -ForegroundColor $(if ($thieuBulk.Count -eq 0) { 'Green' } else { 'Red' })
    if ($dsBulk.Status -ne 200) { $canhBao += "DS phieu (bulk) tra HTTP $($dsBulk.Status)" }
    if ($thieuBulk.Count -gt 0) { $canhBao += "DS phieu thieu: $($thieuBulk -join ', ')" }

    # ── 14c. ⑭ Trang IN PHIẾU ────────────────────────────────────────────────────────────────────────────
    $inPhieu = Lay-Trang -Path "/$business/inventory/bill/print?ids=$phieuId"
    In-KetQua -Nhan '⑭ TRANG IN PHIEU (1 phieu)' -Status $inPhieu.Status -Body ''
    if ($inPhieu.Status -ne 200) { $canhBao += "Trang in tra HTTP $($inPhieu.Status)" }
    if ($inPhieu.Body -notmatch 'PHIẾU') { $canhBao += 'Trang in thieu tieu de "PHIEU"' }
    if ($inPhieu.Body -notmatch [regex]::Escape($maPhieu)) { $canhBao += 'Trang in khong co so phieu' }
    $inSach = $inPhieu.Body -replace '<!--.*?-->', ''
    if ($inSach -notmatch 'Tổng') { $canhBao += 'Trang in thieu dong "Tong"' }

    # ── 14d. ⑮ Form tạo phiếu chọn sẵn kiểu qua `?kind=` ─────────────────────────────────────────────────
    $chuyenKho = Lay-Trang -Path "/$business/inventory/bill/new?kind=transfer"
    In-KetQua -Nhan '⑮ FORM TAO PHIEU ?kind=transfer (chon san kieu)' -Status $chuyenKho.Status -Body ''
    if ($chuyenKho.Status -ne 200) { $canhBao += "Form ?kind=transfer tra HTTP $($chuyenKho.Status)" }
    if ($chuyenKho.Body -notmatch 'Chuyển kho') { $canhBao += 'Form ?kind=transfer khong chon san "Chuyen kho"' }
    if ($chuyenKho.Body -notmatch 'Kho đích') { $canhBao += 'Form chuyen kho thieu o "Kho dich"' }

    # ── 15. ⑪ Bất biến tổng sổ cái = ảnh chụp ────────────────────────────────────────────────────────────
    $sqlTong = @'
select coalesce(sum(case when "MovementType" in (0,4,5,6,7,10) then "QuantityDelta"
                         when "MovementType" in (3,8,9,19,11,14,15,20,21) then -"QuantityDelta" else 0 end), 0)
from "InvStockLedger" where "Sku" = '__SKU__' and "IsDeleted" = false;
'@

    $sqlAnh = @'
select coalesce("OnHand", 0) from "InvItems" where "Sku" = '__SKU__' and "IsDeleted" = false;
'@

    $tongSoCai = ((($sqlTong -replace '__SKU__', $sku) | docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A) | Out-String).Trim()
    $anhChup = ((($sqlAnh -replace '__SKU__', $sku) | docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A) | Out-String).Trim()

    Write-Host '⑪ BAT BIEN tong so cai = anh chup'
    Write-Host ("   tong so cai = {0} · OnHand = {1}" -f $tongSoCai, $anhChup)
    if ($tongSoCai -ne $anhChup) { $canhBao += "BAT BIEN SAI: tong so cai ($tongSoCai) <> OnHand ($anhChup)" }

    # ── Kết luận ─────────────────────────────────────────────────────────────────────────────────────────
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
    if (Test-Path $jar) { Remove-Item $jar -Force -ErrorAction SilentlyContinue }

    if ($null -ne $webProc -and -not $webProc.HasExited) {
        # `cmd /c pnpm …` sinh cây tiến trình con (node) ⇒ phải kill cả cây, không chỉ cmd.
        & taskkill.exe /PID $webProc.Id /T /F 2>$null | Out-Null
        Write-Host 'Da dung Next do chinh probe khoi dong.'
    }

    if ($null -ne $hostProc -and -not $hostProc.HasExited) {
        Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'Da dung Host do chinh probe khoi dong.'
    }

    Write-Host ''
    Write-Host "DU LIEU PROBE: kho=$maKho ($($khoKq.id)) · SKU=$sku · phieu=$maPhieu"
}
