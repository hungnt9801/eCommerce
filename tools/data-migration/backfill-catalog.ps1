<#
.SYNOPSIS
    Backfill catalog WP-03: `CatProductVariants` → sản phẩm con + ĐVT gốc, và `OrdOrderLines.ProductId` theo `Sku`.

.DESCRIPTION
    Chạy `eCommerce.DbMigrator --backfill-catalog` (migration + seed + backfill) **trong thư mục project** —
    chạy `dotnet run --project …` từ gốc repo làm **content root sai** ⇒ `The ConnectionString property has not
    been initialized` (đã gặp thật ở phiên `N2`, ghi ở `handover-n2` §3.1).

    🔒 Script KHÔNG tự quyết định "chạy được hay không": nó đọc số đo do service in ra và **thất bại** nếu
    đối soát lệch. Ba mức thoát:
      0 = chạy xong, đối soát khớp;
      1 = sản phẩm/dữ liệu: có xung đột, đã DỪNG và KHÔNG ghi gì (hoặc đối soát lệch);
      2 = môi trường: không tìm thấy project / không đọc được số đo (script hỏng) — **không** phải "backfill sai".

.PARAMETER DryRun
    Chỉ đếm và in, KHÔNG ghi. Lần chạy đầu tiên bắt buộc phải là `-DryRun` (quy trình ở `07 §WP-03` mục 5).

.PARAMETER ConnectionString
    Ghi đè chuỗi kết nối (dùng cho **bản sao DB** có biến thể thật). Mặc định: `appsettings.json` của DbMigrator.
    📌 Truyền qua **biến môi trường** `ConnectionStrings__Default` (đúng thứ tự ưu tiên của .NET config), không
    sửa tệp cấu hình — tránh để lại chuỗi kết nối của DB test trong repo.

.EXAMPLE
    pwsh -File .\tools\data-migration\backfill-catalog.ps1 -DryRun

.EXAMPLE
    pwsh -File .\tools\data-migration\backfill-catalog.ps1 `
        -ConnectionString "Host=localhost;Port=5432;Database=ecommerce_n3_copy;User ID=ecommerce;Password=ecommerce_dev_only;"
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'

$gocRepo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$thuMucMigrator = Join-Path $gocRepo 'src\eCommerce.DbMigrator'

if (-not (Test-Path (Join-Path $thuMucMigrator 'eCommerce.DbMigrator.csproj'))) {
    Write-Host "KHONG TIM THAY project DbMigrator tai: $thuMucMigrator" -ForegroundColor Red
    exit 2
}

$daDat = $false

if ($ConnectionString) {
    $env:ConnectionStrings__Default = $ConnectionString
    $daDat = $true
    Write-Host "ConnectionString: (ghi de qua bien moi truong)" -ForegroundColor DarkGray
}

$thamSo = @('run', '--', '--backfill-catalog')

if ($DryRun) {
    $thamSo += '--dry-run'
}

Push-Location $thuMucMigrator

try {
    # 2>&1: giữ cả cảnh báo của `dotnet` — nuốt chúng thì "không chạy" trông giống "chạy không có gì để làm".
    $ketQua = & dotnet @thamSo 2>&1
    $maThoat = $LASTEXITCODE
}
finally {
    Pop-Location

    # 🔒 Không để chuỗi kết nối của bản sao DB "dính" sang lệnh sau trong cùng phiên shell.
    if ($daDat) {
        Remove-Item Env:\ConnectionStrings__Default -ErrorAction SilentlyContinue
    }
}

$ketQua | ForEach-Object { Write-Host $_ }

# ── ⓪ XUNG ĐỘT TRƯỚC TIÊN ──────────────────────────────────────────────────────────────────────────
# 📌 Khi service DỪNG vì xung đột, nó **không** in dòng `RESULT:` (không có gì đã tạo). Nếu kiểm "đọc được
# số đo" trước, script sẽ báo *"khung chạy hỏng"* (exit 2 = lỗi MÔI TRƯỜNG) cho một ca **dữ liệu** — đúng ca
# cần phân biệt rõ nhất. ⇒ Kiểm dấu hiệu dừng trước mọi phép đọc khác.
$coDung = $ketQua | Where-Object { "$_" -match 'ABORT' -or "$_" -match 'CONFLICT:' }

if ($coDung) {
    Write-Host "KET QUA: DUNG vi XUNG DOT — KHONG ghi gi (xem dong CONFLICT o tren)." -ForegroundColor Red
    exit 1
}

# ── Đọc số đo ────────────────────────────────────────────────────────────────────────────────────
$dongDem = $ketQua | Where-Object { "$_" -match 'variants=(\d+) alreadyMapped=(\d+) toCreate=(\d+) conflicts=(\d+)' } | Select-Object -Last 1
$dongKetQua = $ketQua | Where-Object { "$_" -match 'RESULT: (\d+) variant -> (\d+) child' } | Select-Object -Last 1

if (-not $dongDem -or -not $dongKetQua) {
    Write-Host "KHONG DOC DUOC SO DO cua backfill ⇒ script/khung chay hong (khong phai ket qua backfill)." -ForegroundColor Red
    Write-Host "ma thoat cua dotnet run: $maThoat"
    exit 2
}

$null = "$dongDem" -match 'variants=(\d+) alreadyMapped=(\d+) toCreate=(\d+) conflicts=(\d+)'
$tongBienThe = [int]$Matches[1]
$daBanDo = [int]$Matches[2]
$seTao = [int]$Matches[3]
$soXungDot = [int]$Matches[4]

$null = "$dongKetQua" -match 'RESULT: (\d+) variant -> (\d+) child'
$variantTrongKetQua = [int]$Matches[1]
$childTrongKetQua = [int]$Matches[2]

Write-Host ""
Write-Host "── SO DO (script doc lai, khong tin mat) ──" -ForegroundColor Cyan
Write-Host ("  variants={0}  alreadyMapped={1}  toCreate={2}  conflicts={3}" -f $tongBienThe, $daBanDo, $seTao, $soXungDot)
Write-Host ("  RESULT: {0} variant -> {1} child" -f $variantTrongKetQua, $childTrongKetQua)
Write-Host ("  dryRun={0}  exitCode(dotnet)={1}" -f $DryRun.IsPresent, $maThoat)

# 🔒 Bất biến đối soát: mỗi biến thể phải có ĐÚNG MỘT sản phẩm con (đã chuyển hoặc chuyển trong lần này).
if (($daBanDo + $seTao) -ne $tongBienThe) {
    Write-Host ("KET QUA: DOI SOAT LECH — {0} da chuyen + {1} se tao != {2} bien the." -f $daBanDo, $seTao, $tongBienThe) -ForegroundColor Red
    exit 1
}

if ($variantTrongKetQua -ne $tongBienThe) {
    Write-Host ("KET QUA: DOI SOAT LECH — dong RESULT ghi {0} variant, dong dem ghi {1}." -f $variantTrongKetQua, $tongBienThe) -ForegroundColor Red
    exit 1
}

if ($maThoat -ne 0) {
    Write-Host "KET QUA: DbMigrator tra ma khac 0 ⇒ co loi khac (migration/seed/backfill)." -ForegroundColor Red
    exit 1
}

Write-Host "KET QUA: OK — doi soat khop ($tongBienThe variant → $seTao child)." -ForegroundColor Green
exit 0
