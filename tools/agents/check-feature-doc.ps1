<#
.SYNOPSIS
    Kiem tai lieu feature theo mau 16 muc (docs/templates/feature-design-template.md) — cong may cho vai BA/DOCREV.

.DESCRIPTION
    Cac kiem (moi kiem in mot dong OK / LOI / CANH BAO):
      1. Du 16 heading `## 0.` ... `## 15.`.
      2. Moi BR dinh nghia o §1 duoc phu o §14 (bang test case, cot "Phu"): khop ma day du (BR-SHP-003-001)
         hoac viet tat theo so (BR-01, BR-001). Ho tro hai kieu dinh nghia dang dung trong repo:
         `BR-<MA>-<NNN>` (MA lay tu ten tep, vd SHP-003) va `**BR-NN**`. BR cua tai lieu khac (vd BR-XC-002-001) bo qua.
         §1 khong co BR nao => LOI (mau bat buoc co BR).
      3. -WP14: co heading chua "Doi chieu mo hinh Nhanh" va phan than khong rong (co dong bang hoac cau
         "khong lech") — DoD cua docs/nhanh/07-ke-hoach-trien-khai.md §WP-14 ①.
      4. Thong tin (khong chan): so cau hoi §15 con `Open`, so chu TODO.

    Ma thoat: 0 dat · 1 khong dat · 2 tham so/tep sai.
    Ke hoach: docs/agents/ke-hoach-v2-claude-code-pro.md §8. Ca thu: tools/test-cong-kiem.ps1 (nhom "tools/agents").

.EXAMPLE
    pwsh -File tools\agents\check-feature-doc.ps1 -Path docs\features\address\addr-001-tra-cuu-don-vi-hanh-chinh-2-cap.md -WP14
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [switch]$WP14
)

$ErrorActionPreference = 'Stop'

function BoDau([string]$s) {
    $n = $s.Normalize([Text.NormalizationForm]::FormD)
    $sb = [Text.StringBuilder]::new()
    foreach ($c in $n.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($c) -ne [Globalization.UnicodeCategory]::NonSpacingMark) { [void]$sb.Append($c) }
    }
    return $sb.ToString().Replace([string][char]0x0111, 'd').Replace([string][char]0x0110, 'D').ToLowerInvariant()
}

function Muc([string]$noiDung, [int]$so) {
    $m = [regex]::Match($noiDung, "(?ms)^##\s+$so\.[^\n]*\n(.*?)(?=^##\s+\d+\.|\z)")
    if ($m.Success) { return $m.Groups[1].Value }
    return ''
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    Write-Output "LOI: khong thay tep $Path"
    exit 2
}

$noiDung = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path), [Text.Encoding]::UTF8)
$ten = [IO.Path]::GetFileNameWithoutExtension($Path)
$maTaiLieu = ''
$mm = [regex]::Match($ten, '^([a-z]+)-(\d{3})', 'IgnoreCase')
if ($mm.Success) { $maTaiLieu = ('{0}-{1}' -f $mm.Groups[1].Value, $mm.Groups[2].Value).ToUpperInvariant() }

$loi = [Collections.Generic.List[string]]::new()
Write-Output ("Kiem tai lieu feature: {0} (ma {1})" -f $Path, $(if ($maTaiLieu) { $maTaiLieu } else { '?' }))

# 1. 16 heading
$thieu = @()
for ($i = 0; $i -le 15; $i++) {
    if (-not [regex]::IsMatch($noiDung, "(?m)^##\s+$i\.\s")) { $thieu += "## $i." }
}
if ($thieu.Count -eq 0) { Write-Output 'OK   16/16 heading ## 0. ... ## 15.' }
else { $loi.Add("thieu heading: $($thieu -join ', ')"); Write-Output "LOI  thieu heading: $($thieu -join ', ')" }

# 2. BR §1 -> §14
$m1 = Muc $noiDung 1
$m14 = Muc $noiDung 14
$dinhNghia = [ordered]@{}
if ($maTaiLieu) {
    foreach ($x in [regex]::Matches($m1, "\bBR-$([regex]::Escape($maTaiLieu))-(\d{3})\b")) { $dinhNghia[$x.Value] = [int]$x.Groups[1].Value }
}
foreach ($x in [regex]::Matches($m1, '\*\*BR-(\d{1,3})\*\*')) {
    $k = 'BR-' + $x.Groups[1].Value
    if (-not $dinhNghia.Contains($k)) { $dinhNghia[$k] = [int]$x.Groups[1].Value }
}
if ($dinhNghia.Count -eq 0) {
    $loi.Add('khong tim thay BR nao o §1 (mau bat buoc co Business Rules)')
    Write-Output 'LOI  khong tim thay BR nao o §1'
}
else {
    $soPhu = [Collections.Generic.HashSet[int]]::new()
    foreach ($x in [regex]::Matches($m14, '\bBR-0*(\d{1,3})\b')) { [void]$soPhu.Add([int]$x.Groups[1].Value) }
    $chuaPhu = @()
    foreach ($k in $dinhNghia.Keys) {
        if ($m14.Contains($k) -or $soPhu.Contains($dinhNghia[$k])) { continue }
        $chuaPhu += $k
    }
    if ($chuaPhu.Count -eq 0) { Write-Output ("OK   {0}/{0} BR o §1 co test o §14" -f $dinhNghia.Count) }
    else {
        $loi.Add("BR chua co test o §14: $($chuaPhu -join ', ')")
        Write-Output ("LOI  {0}/{1} BR chua co test o §14: {2}" -f $chuaPhu.Count, $dinhNghia.Count, ($chuaPhu -join ', '))
    }
}

# 3. Doi chieu mo hinh Nhanh (WP-14)
if ($WP14) {
    $dong = $noiDung -split "`r?`n"
    $vt = -1
    for ($i = 0; $i -lt $dong.Count; $i++) {
        if ($dong[$i] -match '^#{2,4}\s' -and (BoDau $dong[$i]).Contains('doi chieu mo hinh nhanh')) { $vt = $i; break }
    }
    if ($vt -lt 0) {
        $loi.Add('thieu muc "Doi chieu mo hinh Nhanh" (07 §WP-14 ①)')
        Write-Output 'LOI  thieu muc "Doi chieu mo hinh Nhanh"'
    }
    else {
        $than = @()
        for ($j = $vt + 1; $j -lt $dong.Count -and $dong[$j] -notmatch '^#{2,4}\s'; $j++) { $than += $dong[$j] }
        $coBang = @($than | Where-Object { $_ -match '^\|.*\|' }).Count -ge 3
        $khongLech = (BoDau ($than -join ' ')).Contains('khong lech')
        if ($coBang -or $khongLech) { Write-Output ("OK   muc doi chieu mo hinh Nhanh o dong {0}" -f ($vt + 1)) }
        else {
            $loi.Add('muc "Doi chieu mo hinh Nhanh" rong (can bang cho lech, hoac ghi "da doi chieu, khong lech")')
            Write-Output 'LOI  muc "Doi chieu mo hinh Nhanh" rong'
        }
    }
}

# 4. Thong tin
$m15 = Muc $noiDung 15
$open = @(($m15 -split "`r?`n") | Where-Object { $_.StartsWith('|') -and $_ -match '\bOpen\b' }).Count
$todo = @([regex]::Matches($noiDung, '\bTODO\b')).Count
Write-Output ("INFO §15 con {0} dong cau hoi 'Open' · {1} chu TODO" -f $open, $todo)

if ($loi.Count -eq 0) {
    Write-Output 'KET QUA: DAT'
    exit 0
}
Write-Output ("KET QUA: KHONG DAT — {0} loi" -f $loi.Count)
exit 1
