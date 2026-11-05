<#
.SYNOPSIS
    Cổng **OpenAPI drift** (`WP-09`): controller/DTO đổi mà `snapshot` + `schema.d.ts` **không** được sinh lại
    ⇒ ĐỎ.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VÌ SAO CẦN CỔNG NÀY (đây là lỗi **im lặng**, không phải lỗi compile):

    `packages/api-client` là **hợp đồng** giữa BE và FE (`D-33`): type của mọi DTO trong FE lấy từ
    `schema.d.ts` sinh tự OpenAPI. Nếu ai sửa controller/DTO ở `/api/v1` mà **quên**:

        pnpm spec:snapshot      # ghi openapi.v1.json từ swagger của Host ĐANG CHẠY
        pnpm gen:api            # sinh lại schema.d.ts từ snapshot đó

    thì **`tsc` vẫn xanh** — vì `schema.d.ts` vẫn mô tả DTO **cũ**. FE đọc `undefined` trong im lặng, và chỉ
    phát hiện khi người dùng thấy màn hình trống. 📌 Đây đúng lớp lỗi `D-33` sinh ra để chặn.

    ✅ Cách đo: **so TRƯỚC và SAU khi sinh lại**, không so với `git HEAD` — vì working tree có thể đang có
    thay đổi hợp lệ chưa commit (đúng tình trạng `N7`+`N8`+`N9`), so với HEAD sẽ báo "lệch" vô nghĩa.

.PARAMETER GiuHost
    Không tắt Host sau khi đo (dùng khi muốn chạy tiếp probe khác).

.EXAMPLE
    pwsh -File .\tools\kiem-openapi-drift.ps1
#>
[CmdletBinding()]
param(
    [switch]$GiuHost
)

$ErrorActionPreference = 'Stop'

# 🔑 `nen-tang.ps1` = lop "mot cho" cho moi thu khac nhau giua Windows va Linux (`XC-007` P-01/P-02).
#    🚨 KHONG dung `Get-NetTCPConnection`: tren Linux no **im lang tra rong** ⇒ tuong "cong trong" trong khi
#    Host cu van dang nghe ⇒ do nham build (dung bay da tra gia o `S9`).
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$goc = Split-Path -Parent $PSScriptRoot
$api = 'http://localhost:54275'
$bin = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host' -GocRepo $goc
$snapshot = [IO.Path]::Combine($goc, 'packages', 'api-client', 'openapi.v1.json')
$schema = [IO.Path]::Combine($goc, 'packages', 'api-client', 'src', 'schema.d.ts')
$thuMucLog = [IO.Path]::Combine($goc, 'tools', '_tam-kiem')
New-Item -ItemType Directory -Force -Path $thuMucLog | Out-Null

if (-not (Test-Path (Join-Path $bin 'eCommerce.HttpApi.Host.dll'))) {
    Write-Output "🚨 Khong thay binary Host tai $bin — chay 'dotnet build eCommerce.slnx' truoc."
    exit 2
}

function Lay-Hash {
    param([string]$Tep)

    if (-not (Test-Path $Tep)) { return '(khong co)' }
    return (Get-FileHash -Path $Tep -Algorithm SHA256).Hash
}

function Chay {
    param([string]$Nhan, [string[]]$ThamSo)

    $log = Join-Path $thuMucLog ("drift-" + ($Nhan -replace '[^A-Za-z0-9]', '-') + '.log')

    # ⚠️ `*>` (không `|`): ong danh lam `$LASTEXITCODE` thanh cua lenh cuoi trong ong ⇒ "xanh" tren mot lan DO.
    & pnpm @ThamSo *> $log
    $ma = $LASTEXITCODE

    if ($ma -ne 0) {
        Get-Content $log | Select-Object -Last 10 | ForEach-Object { "   $_" }
        throw "Buoc '$Nhan' THAT BAI (exit $ma) — xem $log"
    }

    return $ma
}

$hashSnapshotTruoc = Lay-Hash $snapshot
$hashSchemaTruoc = Lay-Hash $schema
$soPathTruoc = if (Test-Path $snapshot) {
    ((Get-Content $snapshot -Raw | ConvertFrom-Json).paths.PSObject.Properties.Name | Measure-Object).Count
}
else { 0 }

Write-Output '== 1. Bat Host (build hien tai) =='
$giuCong = @(Lay-PidDangNghe -Cong 54275)
foreach ($pid_ in $giuCong) { Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue }

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $api
$env:AuthServer__Authority = $api

# 📌 KHONG dat `-WindowStyle` (chi co tren Windows) — da co `-RedirectStandard*` nen khong mo cua so moi.
$hostProc = Start-Process dotnet -ArgumentList 'eCommerce.HttpApi.Host.dll' -WorkingDirectory $bin -PassThru `
    -RedirectStandardOutput (Join-Path $thuMucLog 'drift-host.log') `
    -RedirectStandardError (Join-Path $thuMucLog 'drift-host.err')

try {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $api `
        -LogFile (Join-Path $thuMucLog 'drift-host.log') -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) { throw 'Host khong len duoc — xem tools/_tam-kiem/drift-host.log' }

    Push-Location $goc
    try {
        Write-Output '== 2. Sinh lai snapshot + schema.d.ts =='
        Chay 'spec-snapshot' @('--filter', '@ecommerce/api-client', 'spec:snapshot') | Out-Null
        Chay 'gen-api' @('run', 'gen:api') | Out-Null
    }
    finally {
        Pop-Location
    }
}
finally {
    if (-not $GiuHost) {
        Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
    }
}

Write-Output '== 3. So TRUOC / SAU =='
$hashSnapshotSau = Lay-Hash $snapshot
$hashSchemaSau = Lay-Hash $schema
$soPathSau = ((Get-Content $snapshot -Raw | ConvertFrom-Json).paths.PSObject.Properties.Name | Measure-Object).Count

Write-Output ("   openapi.v1.json : {0} path (truoc {1})" -f $soPathSau, $soPathTruoc)
Write-Output ("   schema.d.ts     : {0}" -f $(if ($hashSchemaTruoc -eq $hashSchemaSau) { 'KHONG DOI' } else { 'DA DOI' }))

$lech = @()
if ($hashSnapshotTruoc -ne $hashSnapshotSau) { $lech += 'openapi.v1.json' }
if ($hashSchemaTruoc -ne $hashSchemaSau) { $lech += 'schema.d.ts' }

if ($lech.Count -gt 0) {
    Write-Output ''
    Write-Output ("🚨 OPENAPI DRIFT: {0} da thay doi khi sinh lai." -f ($lech -join ', '))
    Write-Output '   ⇒ Controller/DTO /api/v1 da doi ma hop dong da commit chua duoc cap nhat.'
    Write-Output '   ⇒ Sua: commit chinh cac tep vua sinh lai (chung la HOP DONG, khong phai rac build).'
    exit 1
}

Write-Output ''
Write-Output 'PASS — OpenAPI drift: 0 (snapshot + schema.d.ts sinh lai KHONG doi).'
exit 0
