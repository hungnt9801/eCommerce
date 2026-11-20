<#
.SYNOPSIS
    Bo chay tu dong bang phien WP-14: mo phien -> cho xong -> mo phien ke, khong can nguoi ngoi may.

.DESCRIPTION
    Moi vong: `phien.ps1 next` chon hang -> chay MOT phien Claude Code khong tuong tac:
        claude -p "/chay-phien" --model sonnet --permission-mode auto --permission-prompts none --output-format json
    (moi phien mot ngu canh sach; skill /chay-phien tu mo phien, lam hang, dong phien, cap nhat bang, commit + push nhanh WP).
    Xong phien -> vong ke. Thiet ke: docs/agents/de-xuat-uy-quyen-va-chay-tu-dong.md §7 (phuong an B).

    Dung khi:
      - het viec / chi con viec cua NGUOI (`next` exit 3/5) hoac hang ke la phien ngoi cung (`thao-luan`);
      - co tep phanh artifacts/agents/tu-dong/DUNG (tao tep do de dung sau phien dang chay);
      - 2 phien lien tiep khong tien trien (registry + handover khong doi) — cau dao, tranh dot han muc;
      - het han muc TUAN; da chay du -ToiDa phien.
    Het han muc 5 gio ("You've hit your session limit · resets 3:45pm"): ngu toi gio hoi + 3 phut roi chay lai dung hang.

    Chi MOT bo chay mot luc (tep khoa artifacts/agents/tu-dong/chay-tu-dong.lock). Khi bo chay dang chay: KHONG mo cua so /chay-phien tay.
    Nhat ky: artifacts/agents/tu-dong/<gio>.json (ket qua tung phien) + artifacts/agents/tu-dong/nhat-ky.md.

.EXAMPLE
    pwsh -File tools/agents/chay-tu-dong.ps1                # chay toi khi het viec cua agent
    pwsh -File tools/agents/chay-tu-dong.ps1 -ToiDa 3       # toi da 3 phien
    New-Item artifacts/agents/tu-dong/DUNG                              # (cua so khac) dung sau phien dang chay
#>
[CmdletBinding()]
param(
    [int]$ToiDa = 30,
    [string]$Model = 'sonnet',
    [double]$GioToiDaMotPhien = 6,
    [string]$Root,
    [int]$ChoKhongRoGiay = 1800,     # het han muc ma khong doc duoc gio hoi
    [int]$ChoThemGiay = 180,         # cho them sau gio hoi
    [int]$ChoToiDaGiay = 6 * 3600    # tran mot lan ngu (ca thu dat nho)
)

$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
$utf8 = [Text.UTF8Encoding]::new($false)
$phien = Join-Path $Root 'tools/agents/phien.ps1'
$reg = Join-Path $Root 'agents/state/phien-wp14.json'
$phanh = Join-Path $Root 'artifacts/agents/tu-dong/DUNG'
$khoa = Join-Path $Root 'artifacts/agents/tu-dong/chay-tu-dong.lock'
$logDir = Join-Path $Root 'artifacts/agents/tu-dong'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$nhatKy = Join-Path $logDir 'nhat-ky.md'

function Ghi([string]$s) {
    $dong = ('{0}  {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm'), $s)
    Write-Output $dong
    [IO.File]::AppendAllText($nhatKy, "- $dong`n", $utf8)
}
function PH([string[]]$a) {
    $out = & pwsh -NoProfile -File $phien @a -Root $Root 2>&1 | Out-String
    return @{ ma = $LASTEXITCODE; out = $out.Trim() }
}
function DauVet {
    $hd = Get-ChildItem -LiteralPath (Join-Path $Root 'docs/nhanh') -Filter 'handover-n*.md' -File -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Name + ':' + $_.Length } | Sort-Object
    return (Get-FileHash -LiteralPath $reg -Algorithm SHA256).Hash + '|' + ($hd -join ',')
}
# "You've hit your session limit · resets 3:45pm" / "... weekly limit · resets Mon 12:00am"
function DocHanMuc([string]$text) {
    $m = [regex]::Match($text, "hit your (session|weekly|Opus|Sonnet) limit\s*(?:\u00b7|-)?\s*resets\s+([A-Za-z]{3}\s+)?(\d{1,2})(?::(\d{2}))?\s*([ap]m)", 'IgnoreCase')
    if (-not $m.Success) {
        if ($text -match "hit your (session|weekly|Opus|Sonnet) limit|usage limit|rate_limit") { return @{ loai = 'khong-ro' } }
        return $null
    }
    $loai = $m.Groups[1].Value.ToLower()
    if ($loai -eq 'weekly') { return @{ loai = 'tuan'; chu = $m.Value } }
    $h = [int]$m.Groups[3].Value % 12; if ($m.Groups[5].Value -ieq 'pm') { $h += 12 }
    $mi = if ($m.Groups[4].Success) { [int]$m.Groups[4].Value } else { 0 }
    $t = (Get-Date).Date.AddHours($h).AddMinutes($mi)
    if ($t -le (Get-Date)) { $t = $t.AddDays(1) }
    return @{ loai = 'phien'; luc = $t; chu = $m.Value }
}
function Ngu([int]$giay, [string]$vi) {
    $giay = [Math]::Min([Math]::Max($giay, 1), $ChoToiDaGiay)
    Ghi ("ngu {0} phut ({1}); tao artifacts/agents/tu-dong/DUNG de dung han" -f [Math]::Round($giay / 60), $vi)
    Start-Sleep -Seconds $giay
}

# ── khoa: mot bo chay mot luc ───────────────────────────────────────────
if (Test-Path -LiteralPath $khoa) {
    $pidCu = ([IO.File]::ReadAllText($khoa, $utf8)).Trim()
    if ($pidCu -match '^\d+$' -and (Get-Process -Id ([int]$pidCu) -ErrorAction SilentlyContinue)) {
        Write-Output "DUNG: bo chay khac dang chay (PID $pidCu) — khong chay song song"; exit 4
    }
}
[IO.File]::WriteAllText($khoa, "$PID", $utf8)

$claudeTest = $env:CHAY_TU_DONG_CLAUDE   # ca thu: duong dan .ps1 gia lap claude
$claude = if ($claudeTest) { $null } else { (Get-Command claude -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
if (-not $claudeTest -and -not $claude) { Write-Output 'LOI: khong thay lenh claude trong PATH'; Remove-Item $khoa -Force; exit 1 }

$env:CHAY_TU_DONG_PHIEN = '1'   # phien con biet minh do bo chay mo (skill /chay-phien buoc 0)
$maThoat = 0; $soPhien = 0; $khongTien = 0
try {
    Ghi ("BAT DAU bo chay (toi da {0} phien, model {1})" -f $ToiDa, $Model)
    while ($true) {
        if (Test-Path -LiteralPath $phanh) { Ghi 'DUNG: co tep phanh artifacts/agents/tu-dong/DUNG (xoa tep de chay lai)'; break }
        if ($soPhien -ge $ToiDa) { Ghi "DUNG: da chay du $ToiDa phien"; break }
        $n = PH @('next')
        if ($n.ma -eq 5) { Ghi "DUNG: het viec — $($n.out)"; break }
        if ($n.ma -ne 0) { Ghi "DUNG: chi con viec cua nguoi / bi chan — $($n.out)"; $maThoat = 3; break }
        $id = ($n.out -split "`r?`n")[-1].Trim()
        $hang = @((Get-Content -LiteralPath $reg -Raw -Encoding utf8 | ConvertFrom-Json).rows | Where-Object { $_.id -eq $id })[0]
        if ($hang.loai -eq 'thao-luan') { Ghi "DUNG: hang $id can nguoi ngoi cung — mo cua so va dan /chay-phien $id"; $maThoat = 3; break }

        $truoc = DauVet
        $soPhien++
        $tep = Join-Path $logDir ((Get-Date -Format 'yyyyMMdd-HHmmss') + "-$id.json")
        Ghi ("PHIEN {0}: hang {1} — {2}" -f $soPhien, $id, $hang.noiDung)
        if ($claudeTest) {
            $p = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoProfile', '-File', $claudeTest, $Root) -WorkingDirectory $Root `
                -RedirectStandardOutput $tep -RedirectStandardError "$tep.err" -NoNewWindow -PassThru
        }
        else {
            $thamSo = @('-p', '/chay-phien', '--model', $Model, '--permission-mode', 'auto', '--permission-prompts', 'none', '--output-format', 'json')
            $exe = $claude
            if ($claude -match '\.(cmd|bat)$') { $exe = $env:ComSpec; $thamSo = @('/c', $claude) + $thamSo }
            $p = Start-Process -FilePath $exe -WorkingDirectory $Root -RedirectStandardOutput $tep -RedirectStandardError "$tep.err" -NoNewWindow -PassThru `
                -ArgumentList $thamSo
        }
        if (-not $p.WaitForExit([int]($GioToiDaMotPhien * 3600 * 1000))) {
            $p.Kill($true); Ghi "phien $id qua $GioToiDaMotPhien gio — da dung tien trinh (hang o lai, phien sau lam tiep)"
        }
        $ra = (Get-Content -LiteralPath $tep -Raw -ErrorAction SilentlyContinue) + "`n" + (Get-Content -LiteralPath "$tep.err" -Raw -ErrorAction SilentlyContinue)
        $ketQua = ''
        try { $j = ($ra.Trim() | ConvertFrom-Json -ErrorAction Stop); $ketQua = [string]$j.result } catch { $ketQua = $ra }
        $tom = ($ketQua -replace '\s+', ' ').Trim(); if ($tom.Length -gt 300) { $tom = $tom.Substring(0, 300) + '…' }
        Ghi ("  ket thuc exit {0}: {1}" -f $p.ExitCode, $tom)

        $hm = DocHanMuc $ra
        if ($hm) {
            $soPhien--
            if ($hm.loai -eq 'tuan') { Ghi "DUNG: het han muc TUAN ($($hm.chu)) — chay lai sau khi hoi"; $maThoat = 2; break }
            if ($hm.loai -eq 'phien') { Ngu ([int]($hm.luc - (Get-Date)).TotalSeconds + $ChoThemGiay) "het han muc 5 gio, hoi luc $($hm.luc.ToString('HH:mm'))" }
            else { Ngu $ChoKhongRoGiay 'het han muc, khong doc duoc gio hoi' }
            continue
        }
        if ((DauVet) -eq $truoc) {
            $khongTien++
            Ghi "  canh bao: phien khong tien trien (registry + handover khong doi) — lan $khongTien/2"
            if ($khongTien -ge 2) { Ghi 'DUNG: cau dao — 2 phien lien tiep khong tien trien; xem tep .json trong artifacts/agents/tu-dong'; $maThoat = 1; break }
        }
        else { $khongTien = 0 }
    }
}
finally {
    Remove-Item -LiteralPath $khoa -Force -ErrorAction SilentlyContinue
    $st = PH @('status')
    Ghi ("KET THUC sau {0} phien. Bang:`n{1}" -f $soPhien, $st.out)
}
exit $maThoat
