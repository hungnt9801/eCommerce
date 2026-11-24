<#
.SYNOPSIS
    Negative control cua eCommerce - dot bien that -> test PHAI DO -> khoi phuc -> test PHAI XANH -> ghi bang chung.

.DESCRIPTION
    Chong "test rong": mot test xanh mai khong chung minh gi. Script nay:
      1. Tu choi khi tep dich co thay doi CHUA COMMIT (hoac khong nam trong repo git) - exit 3.
      2. Ap dung bien `-Find` -> `-Replace` (phai xuat hien dung MOT lan) - exit 1 neu khong tim thay/khong duy nhat.
      3. Chay `-TestCommand` => PHAI exit KHAC 0 (do). Xanh => exit 2.
      4. Khoi phuc nguyen ban (byte goc) => chay lai => PHAI exit 0 (xanh). Do => exit 2.
      5. Ghi mot dong bang chung vao `-Testplan` (neu co), roi exit 0.

    Moi duong thoat deu khoi phuc byte goc trong `finally`; sau khoi phuc script kiem lai `git status`
    cho dung tep dich - khong tin vao tri nho.

    Nguon luat: .claude/skills/testing-negative-control/SKILL.md muc 2 * docs/agents/ke-hoach-v2-claude-code-pro.md muc 8.
    Cung khuon voi tools/agents/*.ps1: ASCII, pwsh 7, UTF-8 tuong minh, exit code ro rang.

.PARAMETER File
    Tep nguon chua dot bien (phai da commit, khong co thay doi chua commit).

.PARAMETER Find
    Chuoi goc can dot bien - phai xuat hien dung MOT lan trong tep.

.PARAMETER Replace
    Chuoi thay the (dot bien that: dao dieu kien, bo guard, doi dau so sanh...).

.PARAMETER TestCommand
    Lenh test (chuoi PowerShell) chay bang `pwsh -NoProfile -Command`. Vi du: dotnet test test/eCommerce.Tests --no-build

.PARAMETER Testplan
    Tep testplan de ghi bang chung (docs/tests/<MA>-testplan.md). Khong co => chi in dong bang chung ra stdout.

.PARAMETER Id
    Ma ca negative control (mac dinh NC-<thoi gian>).

.PARAMETER MoTa
    Mo ta ngan cua dot bien (mac dinh "<Find> -> <Replace>").

.PARAMETER Root
    Goc repo (mac dinh: thu muc chua tep dich).

.EXAMPLE
    pwsh -File tools/agents/negative-control.ps1 -File modules/Inventory/X.cs -Find "if (soLuong > 0)" -Replace "if (soLuong >= 0)" -TestCommand "dotnet test test/eCommerce.Tests --no-build" -Testplan docs/tests/INV-001-testplan.md -Id NC-1

.NOTES
    Ma thoat: 0 dat * 1 tham so/tep loi * 2 dot bien khong lam test do (hoac khoi phuc khong xanh) * 3 tep dich ban/chua commit.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$File,
    [Parameter(Mandatory)][string]$Find,
    [Parameter(Mandatory)][string]$Replace,
    [Parameter(Mandatory)][string]$TestCommand,
    [string]$Testplan,
    [string]$Id,
    [string]$MoTa,
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)

function Loi([string]$thongDiep, [int]$ma) {
    Write-Output ("LOI: {0}" -f $thongDiep)
    exit $ma
}

function Chay-Git([string]$gocRepo, [string[]]$thamSo) {
    $ra = & git -C $gocRepo @thamSo 2>$null
    return [pscustomobject]@{ Ma = $LASTEXITCODE; Ra = @($ra) }
}

function Chay-Test([string]$lenh, [string]$tepLog) {
    & pwsh -NoProfile -Command $lenh *> $tepLog
    return $LASTEXITCODE
}

function Cot([string]$s) {
    if ($null -eq $s) { return '' }
    return ($s -replace "`r?`n", ' ').Replace('|', '\|').Trim()
}

function Ghi-BangChung([string]$tepPlan, [string]$dong) {
    if (-not (Test-Path -LiteralPath $tepPlan)) { Loi "khong thay testplan $tepPlan" 1 }
    $t = [IO.File]::ReadAllText($tepPlan, $utf8)
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($d in ($t -split "`r?`n")) { $lines.Add($d) }

    $sec = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^#{1,6}\s*negative control') { $sec = $i; break }
    }

    if ($sec -lt 0) {
        if ($t.Length -gt 0 -and -not $t.EndsWith("`n")) { $lines.Add('') }
        $lines.Add('## Negative control')
        $lines.Add('| Ngay | Ma | Tep:dong | Dot bien | Lenh test | Bang chung |')
        $lines.Add('|---|---|---|---|---|---|')
        $lines.Add($dong)
    }
    else {
        $end = $lines.Count
        for ($i = $sec + 1; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^#{1,6}\s') { $end = $i; break }
        }
        $sep = -1
        for ($i = $sec + 1; $i -lt $end; $i++) {
            if ($lines[$i] -match '^\|[\s\-:\|]+\|$') { $sep = $i; break }
        }
        if ($sep -lt 0) {
            $lines.Insert($sec + 1, '| Ngay | Ma | Tep:dong | Dot bien | Lenh test | Bang chung |')
            $lines.Insert($sec + 2, '|---|---|---|---|---|---|')
            $lines.Insert($sec + 3, $dong)
        }
        else {
            $last = $sep
            for ($i = $sep + 1; $i -lt $end; $i++) {
                if ($lines[$i].TrimStart().StartsWith('|')) { $last = $i } else { break }
            }
            $lines.Insert($last + 1, $dong)
        }
    }

    $ra = ($lines -join "`n")
    if (-not $ra.EndsWith("`n")) { $ra += "`n" }
    [IO.File]::WriteAllText($tepPlan, $ra, $utf8)
}

# -- Tham so / tep ----------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { Loi "khong thay tep $File" 1 }
if ($Find.Length -eq 0) { Loi '-Find rong' 1 }
if ($Find -ceq $Replace) { Loi '-Find va -Replace giong nhau (khong phai dot bien)' 1 }

$tepDayDu = (Resolve-Path -LiteralPath $File).Path
if (-not $Root) { $Root = Split-Path -Parent $tepDayDu }

$gocRepo = ''
try { $gocRepo = (& git -C $Root rev-parse --show-toplevel 2>$null | Select-Object -First 1) } catch { $gocRepo = '' }
if (-not $gocRepo) { Loi "thu muc $Root khong nam trong repo git - khong chung minh duoc tep da commit" 3 }
$gocRepo = (Resolve-Path -LiteralPath $gocRepo).Path
$tepTuongDoi = [IO.Path]::GetRelativePath($gocRepo, $tepDayDu).Replace('\', '/')

$st = Chay-Git $gocRepo @('status', '--porcelain', '--', $tepTuongDoi)
if ($st.Ma -ne 0) { Loi "git status loi tren $tepTuongDoi" 3 }
if ($st.Ra.Count -gt 0) { Loi "tep dich co thay doi CHUA COMMIT - commit hoac khoi phuc truoc: $($st.Ra -join '; ')" 3 }

$byteGoc = [IO.File]::ReadAllBytes($tepDayDu)
$coBom = ($byteGoc.Length -ge 3 -and $byteGoc[0] -eq 0xEF -and $byteGoc[1] -eq 0xBB -and $byteGoc[2] -eq 0xBF)
$enc = [Text.UTF8Encoding]::new($coBom)
$vanBan = $enc.GetString($byteGoc)
if ($coBom -and $vanBan.Length -gt 0 -and $vanBan[0] -eq [char]0xFEFF) { $vanBan = $vanBan.Substring(1) }

$soLan = [regex]::Matches($vanBan, [regex]::Escape($Find)).Count
if ($soLan -ne 1) { Loi "chuoi -Find xuat hien $soLan lan trong $tepTuongDoi (can dung 1 lan de dot bien ro rang)" 1 }

$viTri = $vanBan.IndexOf($Find, [StringComparison]::Ordinal)
$soDong = ($vanBan.Substring(0, $viTri) -split "`n").Count
$vanBanDotBien = $vanBan.Substring(0, $viTri) + $Replace + $vanBan.Substring($viTri + $Find.Length)

$thuMucLog = Join-Path $gocRepo 'artifacts/negative-control'
try { New-Item -ItemType Directory -Force -Path $thuMucLog | Out-Null } catch { $thuMucLog = [IO.Path]::GetTempPath() }
$nhan = Get-Date -Format 'yyyyMMdd-HHmmss'
$logDo = Join-Path $thuMucLog "$nhan-do.log"
$logXanh = Join-Path $thuMucLog "$nhan-xanh.log"

Write-Output ("Negative control: {0}:{1} * dot bien: {2}" -f $tepTuongDoi, $soDong, $(if ($MoTa) { $MoTa } else { "$Find -> $Replace" }))
Write-Output ("Lenh test: {0}" -f $TestCommand)

$maDo = -1
$maXanh = -1
$ketLuan = ''
try {
    [IO.File]::WriteAllText($tepDayDu, $vanBanDotBien, $enc)
    $maDo = Chay-Test $TestCommand $logDo
    if ($maDo -eq 0) {
        $ketLuan = 'dot bien KHONG lam test do - test vo gia tri, sua test truoc'
    }
    else {
        [IO.File]::WriteAllBytes($tepDayDu, $byteGoc)
        $maXanh = Chay-Test $TestCommand $logXanh
        if ($maXanh -ne 0) {
            $ketLuan = 'sau khoi phuc test VAN DO - khoi phuc sai hoac test hong san'
        }
    }
}
finally {
    [IO.File]::WriteAllBytes($tepDayDu, $byteGoc)
}

$st2 = Chay-Git $gocRepo @('status', '--porcelain', '--', $tepTuongDoi)
if ($st2.Ma -ne 0 -or $st2.Ra.Count -gt 0) { Loi "sau khoi phuc tep van ban: $($st2.Ra -join '; ')" 2 }

Write-Output ("  dot bien : exit {0} (log {1})" -f $maDo, $logDo)
Write-Output ("  khoi phuc: exit {0} (log {1})" -f $maXanh, $logXanh)

if ($ketLuan) { Loi $ketLuan 2 }

$maCa = if ($Id) { $Id } else { 'NC-' + (Get-Date -Format 'HHmmss') }
$moTaCot = Cot $(if ($MoTa) { $MoTa } else { "$Find -> $Replace" })
$dong = '| {0} | {1} | {2}:{3} | {4} | {5} | exit {6} (do) -> khoi phuc -> exit {7} (xanh) |' -f `
    (Get-Date -Format 'yyyy-MM-dd'), (Cot $maCa), (Cot $tepTuongDoi), $soDong, $moTaCot, (Cot $TestCommand), $maDo, $maXanh

if ($Testplan) {
    Ghi-BangChung $Testplan $dong
    Write-Output ("Da ghi bang chung vao {0}" -f $Testplan)
}
else {
    Write-Output 'Bang chung (dan vao testplan):'
    Write-Output $dong
}

Write-Output 'KET QUA: DAT - dot bien DO, khoi phuc XANH.'
exit 0
