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
      - co tep phanh artifacts/agents/tu-dong/DUNG: dang ngu cho han muc => dung trong <= 30 giay;
        dang co phien chay => dung SAU KHI phien do xong (khong cat ngang phien);
      - 2 phien lien tiep khong tien trien (registry + handover khong doi) — cau dao, tranh dot han muc;
      - da chay du -ToiDa phien; het han muc TUAN chi khi bat -DungKhiHetTuan.
    Het han muc 5 gio / tuan ("You've hit your 5-hour limit · resets 1am", "... weekly limit · resets Nov 22, 8pm"):
    ngu toi gio hoi + 3 phut roi chay lai dung hang (moi lan ngu toi da -ChoToiDaGiay, thuc day thu lai — lan thu khong ton han muc).

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
    [int]$ChoToiDaGiay = 6 * 3600,   # tran mot lan ngu (ngu dai hon => thuc day thu lai, khong ton han muc); ca thu dat nho
    [switch]$DungKhiHetTuan,         # mac dinh: het han muc tuan thi NGU toi gio hoi; bat cong tac nay de dung han
    [int]$BuocChoGiay = 30           # khi ngu: cu moi buoc nay kiem tep phanh DUNG
)

$ErrorActionPreference = 'Stop'
# Windows: console mac dinh la code page OEM => tieng Viet tu tien trinh con (phien.ps1, claude) thanh "?". Doi sang UTF-8 cho ca cha lan con.
try { [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); [Console]::InputEncoding = [Text.UTF8Encoding]::new($false) } catch { }
$OutputEncoding = [Text.UTF8Encoding]::new($false)
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
# Cay lam viec ban ngoai docs/** va agents/state/** => /chay-phien se tu dung (§0.1) — kiem truoc de khoi ton mot phien.
function TepBanNgoaiVung {
    $out = & git -C $Root status --porcelain 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }   # khong phai repo git (ca thu) => bo qua
    return @($out | ForEach-Object { $_.Substring(3).Trim('"') } | ForEach-Object { ($_ -split ' -> ')[-1] } | Where-Object { $_ -and $_ -notmatch '^(docs/|agents/state/|artifacts/)' })
}
function DauVet {
    $hd = Get-ChildItem -LiteralPath (Join-Path $Root 'docs/nhanh') -Filter 'handover-n*.md' -File -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Name + ':' + $_.Length } | Sort-Object
    return (Get-FileHash -LiteralPath $reg -Algorithm SHA256).Hash + '|' + ($hd -join ',')
}
# Mau thong bao (do duoc 20–21/11/2026 tren 2.1.328):
#   "You've hit your 5-hour limit · resets 1am (Asia/Bangkok)"      "... · resets 6:20am (Asia/Bangkok)"
#   "You've hit your weekly limit · resets Nov 22, 8pm (Asia/Bangkok)"   "... weekly limit · resets 8pm (Asia/Bangkok)"
#   tai lieu: "... session limit · resets 3:45pm" / "... weekly limit · resets Mon 12:00am" / "Opus limit" / "Sonnet limit"
function DocHanMuc([string]$text) {
    $m = [regex]::Match($text, "hit your ([\w\s-]+?) limit\s*(?:\u00b7|-|,)?\s*resets\s+(?:([A-Za-z]{3,9})\.?\s+)?(?:(\d{1,2}),\s*)?(\d{1,2})(?::(\d{2}))?\s*([ap]m)", 'IgnoreCase')
    if (-not $m.Success) {
        if ($text -match "hit your [\w\s-]+ limit|usage limit|rate_limit") { return @{ loai = 'khong-ro' } }
        return $null
    }
    $loai = if ($m.Groups[1].Value -match 'week') { 'tuan' } else { 'phien' }
    $h = [int]$m.Groups[4].Value % 12; if ($m.Groups[6].Value -ieq 'pm') { $h += 12 }
    $mi = if ($m.Groups[5].Success) { [int]$m.Groups[5].Value } else { 0 }
    $bayGio = Get-Date
    $t = $bayGio.Date.AddHours($h).AddMinutes($mi)
    $tu = $m.Groups[2].Value.ToLower()
    $thang = @('jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec')
    $thu = @('sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat')
    $k3 = if ($tu.Length -ge 3) { $tu.Substring(0, 3) } else { '' }
    if ($m.Groups[3].Success -and [Array]::IndexOf($thang, $k3) -ge 0) {
        # "Nov 22, 8pm"
        $t = [datetime]::new($bayGio.Year, [Array]::IndexOf($thang, $k3) + 1, [int]$m.Groups[3].Value, $h, $mi, 0)
        if ($t -lt $bayGio.AddDays(-1)) { $t = $t.AddYears(1) }
    }
    elseif ([Array]::IndexOf($thu, $k3) -ge 0) {
        # "Mon 12:00am"
        while ([int]$t.DayOfWeek -ne [Array]::IndexOf($thu, $k3) -or $t -le $bayGio) { $t = $t.AddDays(1) }
    }
    elseif ($t -le $bayGio) { $t = $t.AddDays(1) }
    return @{ loai = $loai; luc = $t; chu = $m.Value }
}
function Ngu([int]$giay, [string]$vi) {
    $giay = [Math]::Min([Math]::Max($giay, 1), $ChoToiDaGiay)
    Ghi ("ngu {0} phut ({1}); tao artifacts/agents/tu-dong/DUNG de dung han" -f [Math]::Round($giay / 60), $vi)
    # Ngu tung buoc ngan de tep phanh co tac dung ngay ca khi dang cho han muc (23/11: DUNG tao luc 01:35 ma bo chay ngu toi 4 gio)
    $het = (Get-Date).AddSeconds($giay)
    while ((Get-Date) -lt $het) {
        if (Test-Path -LiteralPath $phanh) { return }
        Start-Sleep -Seconds ([Math]::Max(1, [Math]::Min($BuocChoGiay, [int]($het - (Get-Date)).TotalSeconds)))
    }
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
# N34 (20/11): phien chinh goi vai `ba` CHAY NEN, ket thuc luot "cho thong bao" => `claude -p` cho 10 phut roi GIET vai, phien mat trang.
# Tat tac vu nen (vai chay tien canh, phien chinh doi ket qua) + bo tran cho 10 phut phong khi van co tac vu nen.
$env:CLAUDE_CODE_DISABLE_BACKGROUND_TASKS = '1'
$env:CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS = '0'
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

        $ban = TepBanNgoaiVung
        if ($ban.Count) {
            Ghi ("DUNG: cay lam viec co thay doi ngoai docs/** va agents/state/** — commit tay truoc (phien se tu choi chay):`n    " + ($ban -join "`n    ") + "`n    goi y: git add -- " + ($ban -join ' ') + "; git commit -m `"chore(agents): ...`"")
            $maThoat = 1; break
        }
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
        $stdout = [string](Get-Content -LiteralPath $tep -Raw -ErrorAction SilentlyContinue)
        $stderr = [string](Get-Content -LiteralPath "$tep.err" -Raw -ErrorAction SilentlyContinue)
        $ra = $stdout + "`n" + $stderr
        $ketQua = ''; $j = $null
        try { $j = ($stdout.Trim() | ConvertFrom-Json -ErrorAction Stop); $ketQua = [string]$j.result } catch { $ketQua = $ra }
        $tom = ($ketQua -replace '\s+', ' ').Trim(); if ($tom.Length -gt 300) { $tom = $tom.Substring(0, 300) + '…' }
        Ghi ("  ket thuc exit {0}: {1}" -f $p.ExitCode, $tom)
        if ($j -and $j.permission_denials -and @($j.permission_denials).Count) {
            Ghi ("  canh bao: {0} lenh bi tu choi quyen (khong ai duyet): {1}" -f @($j.permission_denials).Count, ((@($j.permission_denials) | ForEach-Object { $c = [string]$_.tool_input.command; if ($c.Length -gt 60) { $c = $c.Substring(0, 60) + '…' }; "$($_.tool_name): $c" }) -join ' | '))
        }
        if ($stderr -match 'Background tasks still running' -or ($j -and $j.subagent_stats -and $j.subagent_stats.killed.system -gt 0)) {
            Ghi '  canh bao: vai chay nen bi dung khi phien ket thuc — phien sau lam tiep hang nay'
        }

        $hm = DocHanMuc ($ketQua + "`n" + $ra)   # $ketQua da giai ma JSON ("\u00b7" -> "·")
        if ($hm) {
            $soPhien--
            if ($hm.loai -eq 'tuan' -and $DungKhiHetTuan) { Ghi "DUNG: het han muc TUAN ($($hm.chu)) — chay lai sau khi hoi"; $maThoat = 2; break }
            if ($hm.loai -in @('phien', 'tuan')) {
                $ten = if ($hm.loai -eq 'tuan') { 'het han muc TUAN' } else { 'het han muc 5 gio' }
                Ngu ([int]($hm.luc - (Get-Date)).TotalSeconds + $ChoThemGiay) ("{0}, hoi luc {1}" -f $ten, $hm.luc.ToString('dd/MM HH:mm'))
            }
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
