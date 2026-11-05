<#
.SYNOPSIS
    Kiểm **browser thật** cho POS (`WP-06`) — ba thứ CHỈ đo được trong trình duyệt.

.DESCRIPTION
    Khởi động **Host** (54275) + **Next** (3000) + **Chrome headless** (CDP 9222), rồi chạy
    `tools/kiem-pos-browser.mjs` để đo:

      ① trang POS render với ca đang mở;
      ② gõ SKU + Enter ⇒ thêm dòng vào hoá đơn;
      ③ **F1** mở tab hoá đơn thứ hai;
      ④ **F5 (reload) KHÔNG mất tab** — tab đọc lại từ **IndexedDB** (`04 §6.3`);
      ⑤ **rớt mạng** (`Network.emulateNetworkConditions offline`) + **F9** ⇒ hoá đơn vào **outbox**;
      ⑥ mạng lại + **Đồng bộ** ⇒ hoá đơn được tạo trên server;
      ⑦ server có **ĐÚNG 1** hoá đơn (idempotent theo `ClientBillId`, không tạo trùng).

    🔒 Không in mật khẩu/token. Chrome dùng **profile tạm** ở `%TEMP%` (không đụng profile của bạn).

.PARAMETER BoQuaHost
    Host đã chạy sẵn ⇒ không bật/tắt.

.PARAMETER BoQuaWeb
    Next đã chạy sẵn ở 3000 ⇒ không bật/tắt.

.EXAMPLE
    pwsh -File .\tools\kiem-pos-browser.ps1
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
$cdpPort = 9222

$hostProc = $null
$webProc = $null
$chromeProc = $null
$profile = Lay-DuongDanTam ("wp06-chrome-" + (Get-Random -Minimum 100000 -Maximum 999999))

function Tim-Chrome {
    $ungVien = @(
        (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe'),
        (Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe')
    )

    foreach ($u in $ungVien) {
        if ($u -and (Test-Path $u)) { return $u }
    }

    return $null
}

# ── Host ────────────────────────────────────────────────────────────────────────────────────────────────
if (-not $BoQuaHost) {
    $bin = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host' -GocRepo $gocRepo
    $giuCong = @(Lay-PidDangNghe -Cong 54275)
    foreach ($pid_ in $giuCong) { Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $api
    $env:AuthServer__Authority = $api

    $hostProc = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa (Lay-DuongDanTam 'wp06b-host.log') -TepLogLoi (Lay-DuongDanTam 'wp06b-host.err')

    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $api -LogFile (Lay-DuongDanTam 'wp06b-host.log') -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) { Write-Host 'Host KHONG len duoc.' -ForegroundColor Red; exit 2 }
}

try {
    # ── Next ────────────────────────────────────────────────────────────────────────────────────────────
    $webDaChay = $false
    try {
        $thu = Invoke-WebRequest -Uri "$web/login" -SkipHttpErrorCheck -TimeoutSec 3
        $webDaChay = ($thu.StatusCode -eq 200)
    }
    catch { $webDaChay = $false }

    if (-not $webDaChay -and -not $BoQuaWeb) {
        $webProc = Start-NextAdmin -ThuMucLamViec $gocRepo `
            -TepLogRa (Lay-DuongDanTam 'wp06b-web.log') -TepLogLoi (Lay-DuongDanTam 'wp06b-web.err')

        $daLen = $false
        for ($i = 0; $i -lt 60; $i++) {
            Start-Sleep -Seconds 1
            try {
                $thu = Invoke-WebRequest -Uri "$web/login" -SkipHttpErrorCheck -TimeoutSec 2
                if ($thu.StatusCode -eq 200) { $daLen = $true; break }
            }
            catch { }
        }

        if (-not $daLen) { Write-Host 'Next KHONG len duoc o 3000.' -ForegroundColor Red; exit 2 }
    }

    Write-Host "Next: OK ($web)" -ForegroundColor Green

    # ── Chrome headless ────────────────────────────────────────────────────────────────────────────────
    $chrome = Tim-Chrome
    if (-not $chrome) {
        Write-Host 'KHONG tim thay Chrome/Edge ⇒ khong kiem duoc browser.' -ForegroundColor Red
        exit 2
    }

    New-Item -ItemType Directory -Force -Path $profile | Out-Null

    # 🚨 Kill **đúng tiến trình đang giữ cổng CDP**: Chrome spawn tiến trình con và tiến trình cha thoát ngay,
    # nên `taskkill /PID` ở `finally` có thể **không** giết trình duyệt thật ⇒ lượt sau nối vào Chrome CŨ
    # (còn cookie phiên trước ⇒ test "đăng nhập" xanh **giả**). Đã dặp đúng bẫy này ở lượt chạy đầu.
    $giuCdp = @(Lay-PidDangNghe -Cong $cdpPort)
    foreach ($pid_ in $giuCdp) { Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue }

    $chromeProc = Start-TienTrinhAn -Tep $chrome -DanhSachThamSo @(
        "--remote-debugging-port=$cdpPort",
        "--user-data-dir=$profile",
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        'about:blank'
    )

    $cdpSanSang = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        try {
            $list = Invoke-RestMethod -Uri "http://127.0.0.1:$cdpPort/json/list" -TimeoutSec 2
            if ($list.Count -gt 0) { $cdpSanSang = $true; break }
        }
        catch { }
    }

    if (-not $cdpSanSang) { Write-Host 'Chrome KHONG mo duoc CDP.' -ForegroundColor Red; exit 2 }

    Write-Host "Chrome CDP: OK ($cdpPort)" -ForegroundColor Green

    $env:CDP_PORT = "$cdpPort"
    $env:WEB = $web
    $env:BUSINESS = '230222'

    node (Join-Path $PSScriptRoot 'kiem-pos-browser.mjs')
    $ma = $LASTEXITCODE

    Write-Host ''
    if ($ma -eq 0) { Write-Host 'KET LUAN BROWSER: TAT CA CA DO KHOP.' -ForegroundColor Green }
    else { Write-Host "KET LUAN BROWSER: CO CA KHONG KHOP (exit $ma)." -ForegroundColor Red }

    exit $ma
}
finally {
    if ($null -ne $chromeProc -and -not $chromeProc.HasExited) {
        & taskkill.exe /PID $chromeProc.Id /T /F 2>$null | Out-Null
    }

    if (Test-Path $profile) { Remove-Item $profile -Recurse -Force -ErrorAction SilentlyContinue }

    if ($null -ne $webProc -and -not $webProc.HasExited) {
        & taskkill.exe /PID $webProc.Id /T /F 2>$null | Out-Null
        Write-Host 'Da dung Next do chinh script khoi dong.'
    }

    if ($null -ne $hostProc -and -not $hostProc.HasExited) {
        Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'Da dung Host do chinh script khoi dong.'
    }
}
