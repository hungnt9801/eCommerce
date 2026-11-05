<#
.SYNOPSIS
    Chạy **8 luồng E2E** (`WP-09`) bằng MỘT lệnh — và luôn chạy trên **build hiện tại**.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VÌ SAO CẦN TỆP NÀY (Playwright tự bật server rồi, script này thêm được gì?):

    `playwright.config.ts` có `webServer` với `reuseExistingServer: true` — đúng cho máy dev (không phải tắt
    server đang làm việc). ⚠️ NHƯNG nó tạo ra **đúng cái bẫy** mà `N5` đã trả giá: `next start`/Host đang chạy
    sẵn có thể là **build CŨ** ⇒ bộ E2E xanh trên mã **hôm qua** và người ta merge mã hôm nay.

    ✅ Script này làm phần mà `reuseExistingServer` **không được phép** làm: **GIẾT** tiến trình cũ trên
    cổng 3000/54275 trước khi chạy, kiểm **build đã tồn tại** (không tự build ngầm — build ngầm che lỗi
    compile vào giữa lúc test), rồi mới gọi Playwright ⇒ cả hai server do Playwright bật = **build hiện tại**.

    📌 Hai điều kiện (báo lỗi rõ nếu thiếu, KHÔNG tự sửa):
      ① `dotnet build eCommerce.slnx` — có `src/eCommerce.HttpApi.Host/bin/Debug/net10.0/eCommerce.HttpApi.Host.dll`
      ② `pnpm build` — có `apps/admin-web/.next/BUILD_ID`
      ③ `docker compose up -d` — PostgreSQL + Redis (E2E seed 1 đơn bằng SQL, xem `e2e/support/don-hang.ts`)

.PARAMETER GiuServer
    Không giết tiến trình đang giữ cổng 3000/54275 (dùng khi đang debug bằng tay và CHẤP NHẬN build cũ).

.EXAMPLE
    pwsh -File .\tools\e2e.ps1
#>
[CmdletBinding()]
param(
    [switch]$GiuServer
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$goc = Split-Path -Parent $PSScriptRoot
$adminWeb = [IO.Path]::Combine($goc, 'apps', 'admin-web')
$hostDll = Join-Path (Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host' -GocRepo $goc) 'eCommerce.HttpApi.Host.dll'
$nextBuildId = [IO.Path]::Combine($adminWeb, '.next', 'BUILD_ID')

function Dung-Cong {
    param([int]$Cong)

    $cacPid = @(Lay-PidDangNghe -Cong $Cong)

    if ($cacPid.Count -eq 0) {
        Write-Output ("   cong {0}: TRONG" -f $Cong)
        return
    }

    foreach ($p in $cacPid) {
        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
    }

    # 📌 Chờ cổng THẬT SỰ nhả: `Stop-Process` trả về trước khi socket đóng ⇒ Playwright bật server sẽ đỏ
    #    với `EADDRINUSE` và thông điệp đó chỉ về phía Playwright, không chỉ về tiến trình vừa giết.
    $hetHan = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $hetHan) {
        if (-not (Test-CongDangMo -Cong $Cong)) { break }
        Start-Sleep -Milliseconds 300
    }

    if (Test-CongDangMo -Cong $Cong) {
        throw "Cổng $Cong vẫn bị chiếm sau khi giết PID $($cacPid -join ', ')."
    }

    Write-Output ("   cong {0}: da dung PID {1}" -f $Cong, ($cacPid -join ', '))
}

Write-Output '== 0. Dieu kien (build phai co san — KHONG build ngam) =='

$thieu = @()
if (-not (Test-Path $hostDll)) { $thieu += 'Host DLL (dotnet build eCommerce.slnx)' }
if (-not (Test-Path $nextBuildId)) { $thieu += 'Next build (.next/BUILD_ID — pnpm build)' }
if ($thieu.Count -gt 0) {
    Write-Output ('   🚨 Thieu: ' + ($thieu -join ' · '))
    exit 2
}
Write-Output '   ✅ Co Host DLL + Next build.'

Write-Output '== 1. Don tien trinh cu tren cong 3000 / 54275 =='
if ($GiuServer) {
    Write-Output '   ⚠️  -GiuServer: KHONG giet tien trinh dang chay (co the la build CU).'
}
else {
    Dung-Cong -Cong 3000
    Dung-Cong -Cong 54275
}

Write-Output '== 2. Playwright (8 luong) =='
Write-Output '   📌 webServer cua Playwright se bat Host (54275) + Next (3000) tu build HIEN TAI.'

Push-Location $goc
try {
    # 🔒 KHONG ong danh (`| Tee-Object`, `| Select-String`): khi do `$LASTEXITCODE` la cua lenh cuoi trong ong
    #    danh (thuong 0) ⇒ "8/8 xanh" tren mot lan DO. Ghi ra tep roi doc lai.
    $tepLog = [IO.Path]::Combine($goc, 'tools', '_tam-kiem', 'e2e-playwright-run.log')
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $tepLog) | Out-Null

    & pnpm --filter '@ecommerce/admin-web' e2e *> $tepLog
    $ma = $LASTEXITCODE

    Get-Content $tepLog | Select-String -Pattern '^\s+(ok|✘|×)\s+\d|passed|failed|flaky|Error:' |
        Select-Object -First 40 | ForEach-Object { "   $($_.Line.Trim())" }

    if ($ma -ne 0) {
        Write-Output ''
        Write-Output "   🚨 E2E DO (exit $ma) — xem log: $tepLog"
        exit $ma
    }

    Write-Output ''
    Write-Output '   ✅ E2E: 8/8 luong XANH.'
    exit 0
}
finally {
    Pop-Location
}
