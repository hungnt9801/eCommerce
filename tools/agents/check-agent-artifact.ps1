<#
.SYNOPSIS
    Kiem artifact cua vai agent bang tieu chi may doc duoc (khong doc cam tinh) — ORCH chay sau moi vai.

.DESCRIPTION
    -Kind docreview  docs/reviews/<MA>-docreview.md (vai DOCREV):
                     co dong `VERDICT: DOC-OK | DOC-CHANGES | DOC-BLOCKED`; bang "Da kiem" nhac du D1..D7;
                     DOC-OK => 0 dong phat hien muc P1 va < 3 dong P2 (luat verdict cua skill doc-review).
    -Kind review     docs/reviews/<MA>-review.md (vai REV): `VERDICT: PASS | CHANGES`; PASS => 0 dong P1.
    -Kind testplan   docs/tests/<MA>-testplan.md (vai QA): can -Srs; moi BR cua SRS xuat hien >= 1 lan (ma day du
                     hoac viet tat BR-NN); co muc "negative control" kem >= 2 dong co "exit" (do roi xanh).
    -Kind decision   docs/nhanh/decisions.md (vai decider): moi dong `TẠM CHỐT (DEC)` / `ĐỀ XUẤT (DEC)`
                     (hoac chi dong -Id) phai co "④ Lùi:" va "⑤ Xem lại trước:" khong rong.

    Ma thoat: 0 dat · 1 khong dat · 2 tham so/tep sai.
    Ke hoach: docs/agents/ke-hoach-v2-claude-code-pro.md §8. Ca thu: tools/test-cong-kiem.ps1 (nhom "tools/agents").
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('docreview', 'review', 'testplan', 'decision')][string]$Kind,
    [Parameter(Mandatory)][string]$Path,
    [string]$Srs,
    [string]$Id
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

function DocTep([string]$p) {
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { Write-Output "LOI: khong thay tep $p"; exit 2 }
    return [IO.File]::ReadAllText((Resolve-Path -LiteralPath $p), [Text.Encoding]::UTF8)
}

# Dem dong bang phat hien theo muc: cot "Muc" (o thu 2) la P1/P2/P3, co the in dam.
function DemMuc([string]$noiDung, [string]$muc) {
    $n = 0
    foreach ($d in ($noiDung -split "`r?`n")) {
        if (-not $d.StartsWith('|')) { continue }
        $o = $d.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim().Trim('*').Trim() }
        if ($o.Count -ge 2 -and $o[1] -eq $muc) { $n++ }
    }
    return $n
}

$loi = [Collections.Generic.List[string]]::new()
$t = DocTep $Path
Write-Output ("Kiem artifact [{0}]: {1}" -f $Kind, $Path)

switch ($Kind) {
    'docreview' {
        $v = [regex]::Match($t, 'VERDICT:\s*\**\s*(DOC-OK|DOC-CHANGES|DOC-BLOCKED)\b')
        if (-not $v.Success) { $loi.Add('thieu dong VERDICT: DOC-OK | DOC-CHANGES | DOC-BLOCKED') }
        $thieuNhom = @(1..7 | Where-Object { -not [regex]::IsMatch($t, "\bD$_\b") } | ForEach-Object { "D$_" })
        if ($thieuNhom.Count) { $loi.Add("bang 'Da kiem' thieu nhom: $($thieuNhom -join ', ')") }
        $p1 = DemMuc $t 'P1'
        $p2 = DemMuc $t 'P2'
        Write-Output ("INFO verdict={0} · P1={1} · P2={2}" -f $(if ($v.Success) { $v.Groups[1].Value } else { '?' }), $p1, $p2)
        if ($v.Success -and $v.Groups[1].Value -eq 'DOC-OK' -and ($p1 -gt 0 -or $p2 -ge 3)) {
            $loi.Add("DOC-OK nhung con P1=$p1 / P2=$p2 (luat: DOC-OK chi khi P1=0 va P2<3)")
        }
    }
    'review' {
        $v = [regex]::Match($t, 'VERDICT:\s*\**\s*(PASS|CHANGES)\b')
        if (-not $v.Success) { $loi.Add('thieu dong VERDICT: PASS | CHANGES') }
        $p1 = DemMuc $t 'P1'
        Write-Output ("INFO verdict={0} · P1={1}" -f $(if ($v.Success) { $v.Groups[1].Value } else { '?' }), $p1)
        if ($v.Success -and $v.Groups[1].Value -eq 'PASS' -and $p1 -gt 0) { $loi.Add("PASS nhung con $p1 dong P1") }
    }
    'testplan' {
        if (-not $Srs) { Write-Output 'LOI: -Kind testplan can -Srs <tai lieu feature>'; exit 2 }
        $s = DocTep $Srs
        $m1 = [regex]::Match($s, "(?ms)^##\s+1\.[^\n]*\n(.*?)(?=^##\s+\d+\.|\z)").Groups[1].Value
        $ma = ''
        $mm = [regex]::Match([IO.Path]::GetFileNameWithoutExtension($Srs), '^([a-z]+)-(\d{3})', 'IgnoreCase')
        if ($mm.Success) { $ma = ('{0}-{1}' -f $mm.Groups[1].Value, $mm.Groups[2].Value).ToUpperInvariant() }
        $br = [ordered]@{}
        if ($ma) { foreach ($x in [regex]::Matches($m1, "\bBR-$([regex]::Escape($ma))-(\d{3})\b")) { $br[$x.Value] = [int]$x.Groups[1].Value } }
        foreach ($x in [regex]::Matches($m1, '\*\*BR-(\d{1,3})\*\*')) { $k = 'BR-' + $x.Groups[1].Value; if (-not $br.Contains($k)) { $br[$k] = [int]$x.Groups[1].Value } }
        $so = [Collections.Generic.HashSet[int]]::new()
        foreach ($x in [regex]::Matches($t, '\bBR-0*(\d{1,3})\b')) { [void]$so.Add([int]$x.Groups[1].Value) }
        $chua = @($br.Keys | Where-Object { -not $t.Contains($_) -and -not $so.Contains($br[$_]) })
        Write-Output ("INFO BR trong SRS={0} · chua co trong testplan={1}" -f $br.Count, $chua.Count)
        if ($br.Count -eq 0) { $loi.Add('SRS khong co BR o §1 — khong doi chieu duoc') }
        if ($chua.Count) { $loi.Add("BR chua co test: $($chua -join ', ')") }
        $bd = BoDau $t
        $i = $bd.IndexOf('negative control')
        if ($i -lt 0) { $loi.Add('thieu muc negative control') }
        elseif (@([regex]::Matches($bd.Substring($i), '\bexit\s+\d')).Count -lt 2) { $loi.Add('negative control chua co bang chung: can >= 2 dong "exit N" (do khi mutation, xanh khi khoi phuc)') }
    }
    'decision' {
        $dong = @(($t -split "`r?`n") | Where-Object { $_ -match '^\|\s*D-\d+' })
        if ($Id) { $dong = @($dong | Where-Object { $_ -match "^\|\s*$([regex]::Escape($Id))\s*\|" }) ; if (-not $dong.Count) { $loi.Add("khong thay dong $Id") } }
        else { $dong = @($dong | Where-Object { (BoDau $_) -match '\((dec)\)\s*\|\s*$' }) }
        Write-Output ("INFO so dong can kiem: {0}" -f $dong.Count)
        foreach ($d in $dong) {
            $ma = [regex]::Match($d, 'D-\d+').Value
            $bd = BoDau $d
            if ($bd -notmatch 'lui:\s*\S') { $loi.Add("$ma thieu '④ Lùi:' (cach lui cu the)") }
            if ($bd -notmatch 'xem lai truoc:\s*\S') { $loi.Add("$ma thieu '⑤ Xem lại trước:'") }
        }
    }
}

foreach ($l in $loi) { Write-Output "LOI  $l" }
if ($loi.Count -eq 0) { Write-Output 'KET QUA: DAT'; exit 0 }
Write-Output ("KET QUA: KHONG DAT — {0} loi" -f $loi.Count)
exit 1
