<#
.SYNOPSIS
    Ca thu cho tools/agents/chay-tu-dong.ps1 — repo gia trong thu muc tam + "claude" gia lap (bien CHAY_TU_DONG_CLAUDE).
.EXAMPLE
    pwsh -File tools/agents/test-chay-tu-dong.ps1     # exit 0 = moi ca dat
#>
$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
$dat = 0; $truot = @()
$goc = Join-Path ([IO.Path]::GetTempPath()) ('test-tu-dong-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))

function Ghi([string]$rel, [string]$noiDung) {
    $p = Join-Path $script:repo $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $p -Parent) | Out-Null
    [IO.File]::WriteAllText($p, $noiDung, $utf8)
}
function RepoMoi([string]$ten, [object[]]$rows) {
    $script:repo = Join-Path $goc $ten
    New-Item -ItemType Directory -Force -Path (Join-Path $repo 'tools/agents') | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'phien.ps1') (Join-Path $repo 'tools/agents/phien.ps1')
    Copy-Item (Join-Path $PSScriptRoot 'chay-tu-dong.ps1') (Join-Path $repo 'tools/agents/chay-tu-dong.ps1')
    Ghi 'agents/state/phien-wp14.json' ([ordered]@{ version = 1; rows = $rows } | ConvertTo-Json -Depth 6)
    Ghi 'docs/agents/bang-phien-wp14.md' "# B`n<!-- BANG-PHIEN:BAT-DAU -->`n<!-- BANG-PHIEN:KET-THUC -->`n"
    Ghi 'docs/nhanh/handover-n5.md' "# N5`n"
}
function Hang($id, $loai, $deps) { [ordered]@{ id = $id; dot = 'A'; loai = $loai; ma = @(); noiDung = "nd $id"; vai = 'x'; dependsOn = $deps; status = 'chua'; phien = @(); evidence = $null; ghiChu = $null; handover = $null } }
# claude gia: $mode = lam (start+done hang ke, viet handover) | khong (khong lam gi) | han-muc-1 (lan dau bao het han muc, sau do lam)
function ClaudeGia([string]$mode) {
    $f = Join-Path $goc "claude-$mode.ps1"
    [IO.File]::WriteAllText($f, @"
param([string]`$Root)
`$ph = Join-Path `$Root 'tools/agents/phien.ps1'
`$dem = Join-Path `$Root 'dem.txt'
`$n = if (Test-Path `$dem) { [int](Get-Content `$dem) } else { 0 }; Set-Content `$dem (`$n + 1)
if ('$mode' -eq 'khong') { '{"result":"khong lam gi"}'; exit 0 }
if ('$mode' -eq 'env') { [Console]::Error.WriteLine('Background tasks still running 10m after the last turn (subagent "BA"); stopping them.'); '{"result":"BG=' + `$env:CLAUDE_CODE_DISABLE_BACKGROUND_TASKS + ' CEIL=' + `$env:CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS + '","permission_denials":[{"tool_name":"Bash","tool_input":{"command":"grep -n x docs/a.md"}}]}'; exit 0 }
if ('$mode' -eq 'han-muc-phanh') { New-Item -ItemType Directory -Force (Join-Path `$Root 'artifacts/agents/tu-dong') | Out-Null; Set-Content (Join-Path `$Root 'artifacts/agents/tu-dong/DUNG') ''; '{"is_error":true,"result":"You''ve hit your 5-hour limit \u00b7 resets 1am (Asia/Bangkok)"}'; exit 1 }
if ('$mode' -eq 'han-muc-tuan' -and `$n -eq 0) { '{"is_error":true,"api_error_status":429,"result":"You''ve hit your weekly limit \u00b7 resets Nov 22, 8pm (Asia/Bangkok)"}'; exit 1 }
if ('$mode' -eq 'han-muc-5h' -and `$n -eq 0) { '{"is_error":true,"api_error_status":429,"result":"You''ve hit your 5-hour limit \u00b7 resets 1am (Asia/Bangkok)"}'; exit 1 }
if ('$mode' -eq 'han-muc-1' -and `$n -eq 0) { '{"is_error":true,"result":"You''ve hit your session limit · resets 3:45pm"}'; exit 1 }
`$id = (& pwsh -NoProfile -File `$ph next -Root `$Root | Select-Object -Last 1).Trim()
`$o = & pwsh -NoProfile -File `$ph start -Id `$id -Root `$Root
`$nx = ((`$o | Select-String 'PHIEN=(N\d+)').Matches[0].Groups[1].Value)
Set-Content (Join-Path `$Root ("docs/nhanh/handover-{0}.md" -f `$nx.ToLower())) "# `$nx"
& pwsh -NoProfile -File `$ph done -Id `$id -Evidence 'gia lap exit 0' -Root `$Root | Out-Null
'{"result":"xong ' + `$id + '"}'
"@, $utf8)
    return $f
}
function Chay([string]$claude, [string[]]$them = @(), [int]$choToiDa = 1) {
    $env:CHAY_TU_DONG_CLAUDE = $claude
    $out = & pwsh -NoProfile -File (Join-Path $repo 'tools/agents/chay-tu-dong.ps1') -Root $repo -ChoToiDaGiay $choToiDa -ChoThemGiay 0 -BuocChoGiay 1 @them 2>&1 | Out-String
    $ma = $LASTEXITCODE
    Remove-Item Env:CHAY_TU_DONG_CLAUDE
    return @{ ma = $ma; out = $out }
}
function Ca([string]$ten, [bool]$dk, $k) {
    if ($dk) { $script:dat++; Write-Output "  OK    $ten" } else { $script:truot += $ten; Write-Output "  TRUOT $ten (exit $($k.ma))`n$($k.out)" }
}
function Reg { (Get-Content (Join-Path $repo 'agents/state/phien-wp14.json') -Raw | ConvertFrom-Json).rows }

try {
    Write-Output 'NHOM 6 — bo chay tu dong (chay-tu-dong.ps1)'
    RepoMoi 't1' @((Hang 'P-01' 'doc-gate' @()), (Hang 'P-02' 'doc-gate' @('P-01')), (Hang 'P-03' 'nguoi' @('P-02')))
    $k = Chay (ClaudeGia 'lam')
    Ca 'T1 chay lan luot 2 hang roi dung o hang cua nguoi (exit 3)' (($k.ma -eq 3) -and ((Reg | Where-Object status -eq 'xong').Count -eq 2) -and ($k.out -match 'KET THUC sau 2 phien')) $k
    Ca 'T2 so phien do script cap, khong trung (N6, N7)' ((@((Reg)[0].phien) -join '') -eq 'N6' -and (@((Reg)[1].phien) -join '') -eq 'N7') $k
    Ca 'T3 khoa duoc go sau khi chay' (-not (Test-Path (Join-Path $repo 'artifacts/agents/tu-dong/chay-tu-dong.lock'))) $k

    RepoMoi 't4' @((Hang 'P-01' 'doc-gate' @()))
    $k = Chay (ClaudeGia 'han-muc-1')
    Ca 'T4 het han muc 5 gio => ngu roi chay lai dung hang, khong tinh la phien' (($k.ma -eq 5 -or $k.ma -eq 0) -and ($k.out -match 'het han muc 5 gio, hoi luc \d\d/\d\d 15:45') -and ((Reg)[0].status -eq 'xong') -and ($k.out -match 'KET THUC sau 1 phien')) $k

    RepoMoi 't4b' @((Hang 'P-01' 'doc-gate' @()))
    $k = Chay (ClaudeGia 'han-muc-5h')
    Ca 'T4b mau that 20/11 "5-hour limit · resets 1am (Asia/Bangkok)" => doc duoc gio hoi 01:00' (($k.out -match 'hoi luc \d\d/\d\d 01:00') -and ($k.out -notmatch 'khong doc duoc gio hoi') -and ((Reg)[0].status -eq 'xong')) $k

    RepoMoi 't4c' @((Hang 'P-01' 'doc-gate' @()))
    $k = Chay (ClaudeGia 'han-muc-tuan')
    Ca 'T4c het han muc TUAN "resets Nov 22, 8pm" => mac dinh NGU toi 22/11 20:00 roi chay tiep' (($k.out -match 'het han muc TUAN, hoi luc 22/11 20:00') -and ((Reg)[0].status -eq 'xong')) $k
    RepoMoi 't4d' @((Hang 'P-01' 'doc-gate' @()))
    $k = Chay (ClaudeGia 'han-muc-tuan') @('-DungKhiHetTuan')
    Ca 'T4d -DungKhiHetTuan => dung han (exit 2), khong chay lai' (($k.ma -eq 2) -and ((Reg)[0].status -eq 'chua')) $k

    RepoMoi 't6b' @((Hang 'P-01' 'doc-gate' @()))
    $dongHo = [Diagnostics.Stopwatch]::StartNew()
    $k = Chay (ClaudeGia 'han-muc-phanh') @() 600
    Ca 'T6b dang ngu cho han muc (toi da 10 phut) ma co tep DUNG => dung sau vai giay, khong ngu het' (($dongHo.Elapsed.TotalSeconds -lt 60) -and ($k.out -match 'tep phanh') -and ([int](Get-Content (Join-Path $repo 'dem.txt')) -eq 1)) $k

    RepoMoi 't5' @((Hang 'P-01' 'doc-gate' @()))
    $k = Chay (ClaudeGia 'khong')
    Ca 'T5 cau dao: 2 phien khong tien trien => dung (exit 1)' (($k.ma -eq 1) -and ($k.out -match 'cau dao') -and ([int](Get-Content (Join-Path $repo 'dem.txt')) -eq 2)) $k

    RepoMoi 't6' @((Hang 'P-01' 'doc-gate' @()))
    Ghi 'artifacts/agents/tu-dong/DUNG' ''
    $k = Chay (ClaudeGia 'lam')
    Ca 'T6 tep phanh DUNG => khong mo phien nao' (($k.out -match 'tep phanh') -and -not (Test-Path (Join-Path $repo 'dem.txt'))) $k

    RepoMoi 't7' @((Hang 'U-02' 'thao-luan' @()), (Hang 'P-01' 'doc-gate' @('U-02')))
    $k = Chay (ClaudeGia 'lam')
    Ca 'T7 hang ke la phien ngoi cung => dung, bao nguoi (exit 3)' (($k.ma -eq 3) -and ($k.out -match 'ngoi cung') -and -not (Test-Path (Join-Path $repo 'dem.txt'))) $k

    RepoMoi 't8' @((Hang 'P-01' 'doc-gate' @()))
    Ghi 'artifacts/agents/tu-dong/chay-tu-dong.lock' "$PID"
    $k = Chay (ClaudeGia 'lam')
    Ca 'T8 bo chay khac dang chay (khoa con song) => tu choi (exit 4)' (($k.ma -eq 4) -and -not (Test-Path (Join-Path $repo 'dem.txt'))) $k

    RepoMoi 't9' @((Hang 'P-01' 'doc-gate' @()), (Hang 'P-02' 'doc-gate' @()))
    $k = Chay (ClaudeGia 'lam') @('-ToiDa', '1')
    Ca 'T9 -ToiDa 1 => dung sau 1 phien' (($k.out -match 'da chay du 1 phien') -and ((Reg | Where-Object status -eq 'xong').Count -eq 1)) $k

    RepoMoi 't10' @((Hang 'P-01' 'doc-gate' @()))
    Ghi '.gitignore' "dem.txt`n"
    & git -C $repo init -q; & git -C $repo add -A; & git -C $repo -c user.email=t@t -c user.name=t commit -qm init
    Ghi 'tools/agents/phien.ps1' ((Get-Content (Join-Path $repo 'tools/agents/phien.ps1') -Raw) + "`n# sua`n")
    Ghi 'docs/x.md' 'trong vung'
    $k = Chay (ClaudeGia 'lam')
    Ca 'T10 cay ban ngoai docs/** + agents/state/** => dung TRUOC khi mo phien, liet ke tep (exit 1)' (($k.ma -eq 1) -and ($k.out -match 'tools/agents/phien.ps1') -and ($k.out -notmatch 'docs/x.md') -and -not (Test-Path (Join-Path $repo 'dem.txt'))) $k
    & git -C $repo add -A; & git -C $repo -c user.email=t@t -c user.name=t commit -qm sua
    $k = Chay (ClaudeGia 'lam')
    Ca 'T11 sau khi commit => chay binh thuong' (((Reg)[0].status -eq 'xong') -and ($k.out -match 'KET THUC sau 1 phien')) $k

    RepoMoi 't12' @((Hang 'P-01' 'doc-gate' @()))
    $k = Chay (ClaudeGia 'env') @('-ToiDa', '1')
    Ca 'T12 phien con nhan bien tat tac vu nen; doc JSON dung du stderr co chu; bao lenh bi tu choi + vai nen bi dung' (($k.out -match 'BG=1 CEIL=0') -and ($k.out -match '1 lenh bi tu choi quyen') -and ($k.out -match 'vai chay nen bi dung')) $k
}
finally { Remove-Item -Recurse -Force $goc -ErrorAction SilentlyContinue }

Write-Output ''
Write-Output ("TONG: {0} dat · {1} truot" -f $dat, $truot.Count)
if ($truot.Count) { exit 1 }
exit 0
