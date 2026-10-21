#!/usr/bin/env pwsh
<#
.SYNOPSIS
    🚨 **CƠ CHẾ CHỐNG LỖI "SAI ROUTE"** — in route THẬT từ `swagger.json` và in **HTTP STATUS** trước khi đọc nội dung.

.VÌ SAO CÓ TỆP NÀY (bài học đã trả giá BA lần)
    Trong dự án, **ba lần** một lời gọi API **sai route** (`404`/`405`) bị **đọc nhầm** thành vấn đề khác:
      • `PRC-001` §19.7 — `POST …/chot-theo-nguon?referenceId=…` ⇒ **405** (tham số là **đoạn đường dẫn**), bị đọc thành *"lỗi xác thực"*;
      • `inv-001` §33.1 — route `POST /{id}/tra-hang` **không còn** `{warehouseId}` ⇒ **404**, bị đọc thành *"chưa làm xong"*;
      • `web-001` §53.2 — `GET /api/app/price-list/item/{id}` ⇒ **405** (route đúng là **`items`** cho GET, `item` cho POST)
        ⇒ body là **trang lỗi**, probe in **rỗng**, và tôi đoán sai thành *"khác kiểu chữ JSON"*.
    ⇒ **Ghi bài học KHÔNG ĐỦ** (đúng kết luận đã rút ra ở `handover` **Phụ lục C**): cần **công cụ** khiến việc **đúng**
      trở thành việc **dễ nhất**. Tệp này là công cụ đó.

.CÁCH DÙNG
    # 1) Xem route thật khớp một mẫu (KHÔNG đoán):
    pwsh -File tools/route-check.ps1 -Match 'price-list'

    # 2) Gọi thử vài URL — in HTTP STATUS trước, rồi mới tới nội dung:
    pwsh -File tools/route-check.ps1 -Call 'http://localhost:5000/api/app/price-list'

    # 3) Gọi POST:
    pwsh -File tools/route-check.ps1 -Call 'POST http://localhost:5000/api/app/price-list'

.GHI CHÚ
    • Tự **khởi động Host** nếu chưa chạy, và **tự dừng** nếu chính nó khởi động.
    • Body được **giải mã từ `Byte[]`** (bài học §53.2: `Invoke-WebRequest` có thể trả `Byte[]` ⇒ `.Substring` ném lỗi).
    • **KHÔNG BAO GIỜ** in nội dung mà **không** in status: *"rỗng"* phải luôn đọc được là *"rỗng VÌ SAO"*.
#>
param(
    [string]$Match = '.',
    [string[]]$Call = @(),
    [string]$HostUrl = 'http://localhost:5000',
    [int]$WaitSeconds = 90
)

$ErrorActionPreference = 'Stop'

function Lay-GocRepo {
    $thuMuc = Get-Item $PSScriptRoot
    while ($thuMuc -and -not (Test-Path (Join-Path $thuMuc.FullName 'eCommerce.slnx'))) { $thuMuc = $thuMuc.Parent }
    if (-not $thuMuc) { throw "Không tìm thấy gốc repo (mốc eCommerce.slnx) từ '$PSScriptRoot'." }
    return $thuMuc.FullName
}

function Do-Body([object]$PhanHoi) {
    if ($null -eq $PhanHoi) { return '' }

    if ($PhanHoi -is [byte[]]) { return [System.Text.Encoding]::UTF8.GetString($PhanHoi) }

    if ($PhanHoi -is [string]) { return $PhanHoi }

    return ($PhanHoi | Out-String)
}

$goc = Lay-GocRepo

# 🔑 `XC-007` P-02/P-04/P-05 (`S9` 21/10/2026): cong cu nay cung phai chay tren Linux.
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'

# 📌 `$env:TEMP` **khong ton tai tren Linux** ⇒ `Join-Path` se nhan `$null`; dung API cua .NET.
$thuMucTam = [IO.Path]::GetTempPath()
$tienTrinh = $null
$tuKhoiDong = $false

function Cho-Host {
    for ($i = 0; $i -lt [Math]::Max(1, $WaitSeconds / 2); $i++) {
        Start-Sleep -Seconds 2
        try {
            if ((Invoke-WebRequest "$HostUrl/health-status" -UseBasicParsing -TimeoutSec 5 -SkipHttpErrorCheck).StatusCode -eq 200) { return $true }
        } catch { }
    }
    return $false
}

try {
    $song = $false
    try { $song = ((Invoke-WebRequest "$HostUrl/health-status" -UseBasicParsing -TimeoutSec 5 -SkipHttpErrorCheck).StatusCode -eq 200) } catch { }

    if (-not $song) {
        Write-Host "Host chưa chạy ⇒ khởi động từ $binHost" -ForegroundColor Yellow
        [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
        $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
            -TepLogRa (Join-Path $thuMucTam 'route-check-host.log') -TepLogLoi (Join-Path $thuMucTam 'route-check-host.err')
        $tuKhoiDong = $true
        if (-not (Cho-Host)) { throw "Host KHÔNG lên trong $WaitSeconds giây — xem $(Join-Path $thuMucTam 'route-check-host.log')." }
    }
    else {
        Write-Host "Host đang chạy sẵn tại $HostUrl" -ForegroundColor DarkGray
    }

    # ── [1] ROUTE THẬT từ swagger (nguồn sự thật do CHÍNH HOST ban hành)
    $sw = Invoke-RestMethod -Uri "$HostUrl/swagger/v1/swagger.json" -TimeoutSec 30
    $khop = $sw.paths.PSObject.Properties | Where-Object { $_.Name -match $Match }

    Write-Host ""
    Write-Host "=== ROUTE THẬT khớp /$Match/ ($($khop.Count) đường dẫn) ===" -ForegroundColor Cyan
    foreach ($d in $khop) {
        foreach ($m in $d.Value.PSObject.Properties) {
            $prm = ($m.Value.parameters | ForEach-Object { "$($_.name)($($_.'in'))" }) -join ' '
            Write-Host ("  {0,-56} {1,-5} {2}" -f $d.Name, $m.Name.ToUpper(), $prm)
        }
    }

    # ── [2] GỌI THỬ: in HTTP STATUS **TRƯỚC**, rồi mới tới nội dung
    if ($Call.Count -gt 0) {
        Write-Host ""
        Write-Host "=== GỌI THỬ ($($Call.Count) lời gọi) ===" -ForegroundColor Cyan
    }

    foreach ($c in $Call) {
        $phan = $c -split '\s+', 2
        $method = if ($phan.Count -eq 2) { $phan[0].ToUpper() } else { 'GET' }
        $url = if ($phan.Count -eq 2) { $phan[1] } else { $phan[0] }

        $traLoi = Invoke-WebRequest $url -Method $method -UseBasicParsing -TimeoutSec 30 -SkipHttpErrorCheck -MaximumRedirection 0 -ErrorAction SilentlyContinue
        $status = if ($traLoi) { [int]$traLoi.StatusCode } else { -1 }
        $loc = if ($traLoi) { "$($traLoi.Headers.Location)" } else { '' }
        $body = Do-Body $(if ($traLoi) { $traLoi.Content } else { $null })

        Write-Host ("  {0,-5} {1}" -f $method, $url)
        Write-Host ("        HTTP {0}{1}" -f $status, $(if ($loc) { "  → Location: $loc" } else { '' })) -ForegroundColor $(if ($status -ge 200 -and $status -lt 300) { 'Green' } elseif ($status -in 301,302,303,307,308) { 'Yellow' } else { 'Red' })
        if ($status -eq 404 -or $status -eq 405) {
            Write-Host "        🚨 SAI ROUTE — ĐỐI CHIẾU danh sách route ở trên TRƯỚC KHI kết luận bất cứ điều gì." -ForegroundColor Red
        }
        Write-Host ("        body: {0}" -f (($body -replace '\s+', ' ').Substring(0, [Math]::Min(200, $body.Length))))
    }

    Write-Host ""
    Write-Host "XONG." -ForegroundColor Green
}
finally {
    if ($tuKhoiDong -and $tienTrinh -and -not $tienTrinh.HasExited) {
        Stop-Process -Id $tienTrinh.Id -Force
        Write-Host "Đã dừng Host do chính công cụ khởi động." -ForegroundColor DarkGray
    }
}
