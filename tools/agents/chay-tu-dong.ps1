<#
.SYNOPSIS
    Bo chay tu dong bang phien (WP-14, hoac chuoi WP code GD3 voi -Chuoi): mo phien -> cho xong -> mo phien ke, khong can nguoi ngoi may.

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

    -Chuoi <ten> (chuoi WP code, docs/agents/bang-phien-<ten>.md): registry agents/state/phien-<ten>.json; moi phien chay
    `claude -p "/<skill> chuoi=<ten>"` (skill lay tu truong `skill` cua registry, mac dinh chay-phien-code).
      - Cau dao CI (M3 cua ho so uy quyen, D-114): truoc moi phien doc lan chay `ci` HOAN TAT moi nhat tren nhanh tich hop
        (truong `nhanhTichHop`) bang `gh`; `failure` => DUNG (exit 1). Dang chay / bi huy => chay tiep. Tat: -KhongKiemCI.
      - -MoPRCuoi: khi het viec, mo moi WP mot PR xep chong (WP dau -> `nhanhGoc`, WP sau -> nhanh WP truoc); khong bao gio merge.

.EXAMPLE
    pwsh -File tools/agents/chay-tu-dong.ps1                # chay toi khi het viec cua agent
    pwsh -File tools/agents/chay-tu-dong.ps1 -ToiDa 3       # toi da 3 phien
    pwsh -File tools/agents/chay-tu-dong.ps1 -Chuoi gd3-1 -ChiTiet   # chuoi WP code GD3 (docs/agents/bang-phien-gd3-1.md)
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
    [int]$BuocChoGiay = 30,          # khi ngu: cu moi buoc nay kiem tep phanh DUNG
    [switch]$ChiTiet,                # in ca tung lenh/tep cua moi vai (mac dinh: chi vai bat dau/xong + moc chinh)
    [string]$Chuoi,                  # chuoi WP code (vd gd3-1); bo trong = bang WP-14
    [switch]$KhongKiemCI,            # bo cau dao CI tren nhanh tich hop
    [switch]$MoPRCuoi                # het viec => mo PR xep chong cho tung WP (khong merge)
)

$ErrorActionPreference = 'Stop'
# Windows: console mac dinh la code page OEM => tieng Viet tu tien trinh con (phien.ps1, claude) thanh "?". Doi sang UTF-8 cho ca cha lan con.
try { [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); [Console]::InputEncoding = [Text.UTF8Encoding]::new($false) } catch { }
$OutputEncoding = [Text.UTF8Encoding]::new($false)
if (-not $Root) { $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
$utf8 = [Text.UTF8Encoding]::new($false)
$phien = Join-Path $Root 'tools/agents/phien.ps1'
if ($Chuoi -and $Chuoi -notmatch '^[a-z0-9][a-z0-9-]*$') { Write-Output 'LOI: -Chuoi chi gom a-z 0-9 -'; exit 1 }
$reg = if ($Chuoi) { Join-Path $Root "agents/state/phien-$Chuoi.json" } else { Join-Path $Root 'agents/state/phien-wp14.json' }
if (-not (Test-Path -LiteralPath $reg)) { Write-Output "LOI: khong thay registry $reg"; exit 1 }
$regInfo = Get-Content -LiteralPath $reg -Raw -Encoding utf8 | ConvertFrom-Json
$lenhPhien = if ($Chuoi) { '/' + $(if ($regInfo.skill) { $regInfo.skill } else { 'chay-phien-code' }) + " chuoi=$Chuoi" } else { '/chay-phien' }
$ghTest = $env:CHAY_TU_DONG_GH   # ca thu: duong dan .ps1 gia lap gh
# Start-Process noi -ArgumentList bang dau cach, KHONG tu them ngoac kep => moi doi so co dau cach phai boc san:
# lenh '/chay-phien-code chuoi=gd3-1', duong dan 'C:\Users\Tran Hung\...' (TEMP, claude.cmd) — 25/11: thieu boc => "-File 'C:\Users\Tran' failed".
function QA([string[]]$a) { return @($a | ForEach-Object { if ($_ -match '\s' -and $_ -notmatch '^".*"$') { '"' + $_ + '"' } else { $_ } }) }
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
    if ($Chuoi) { $a = @($a) + @('-Chuoi', $Chuoi) }
    $out = & pwsh -NoProfile -File $phien @a -Root $Root 2>&1 | Out-String
    return @{ ma = $LASTEXITCODE; out = $out.Trim() }
}
# Cay lam viec ban ngoai docs/** va agents/state/** => /chay-phien se tu dung (§0.1) — kiem truoc de khoi ton mot phien.
function TepBanNgoaiVung {
    $out = & git -C $Root status --porcelain 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }   # khong phai repo git (ca thu) => bo qua
    return @($out | ForEach-Object { $_.Substring(3).Trim('"') } | ForEach-Object { ($_ -split ' -> ')[-1] } | Where-Object { $_ -and $_ -notmatch '^(docs/|agents/state/|artifacts/)' })
}
# 25/11: ham ten `GH` tung goi `& gh` = goi CHINH NO (PowerShell khong phan biet hoa/thuong, ham thang lenh ngoai)
# => "call depth overflow". Ten ham khac han + chi goi tep thuc thi (-CommandType Application).
function GoiGh([string[]]$a) {
    if ($ghTest) { $out = & pwsh -NoProfile -File $ghTest @a 2>&1 | Out-String }
    else {
        $ghExe = (Get-Command gh -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
        if (-not $ghExe) { return @{ ma = 127; out = 'khong thay lenh gh' } }
        Push-Location $Root; try { $out = & $ghExe @a 2>&1 | Out-String } finally { Pop-Location }
    }
    return @{ ma = $LASTEXITCODE; out = $out.Trim() }
}
# Cau dao CI: lan chay `ci` HOAN TAT moi nhat tren nhanh tich hop. Tra 'do' / 'xanh' / 'chua-ro' (+ mo ta).
function TrangThaiCI {
    $nh = [string]$regInfo.nhanhTichHop
    if (-not $Chuoi -or $KhongKiemCI -or -not $nh) { return @{ kq = 'bo-qua' } }
    $k = GoiGh @('run', 'list', '--branch', $nh, '--workflow', 'ci', '--limit', '10', '--json', 'databaseId,status,conclusion,headSha')
    if ($k.ma -ne 0) { return @{ kq = 'chua-ro'; mo = "gh exit $($k.ma): $($k.out)" } }
    try { $runs = @($k.out | ConvertFrom-Json) } catch { return @{ kq = 'chua-ro'; mo = 'gh tra ve khong phai JSON' } }
    $xong = @($runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -notin @('cancelled', 'skipped') })
    if (-not $xong.Count) { return @{ kq = 'chua-ro'; mo = "chua co lan ci hoan tat tren $nh" } }
    $r0 = $xong[0]
    $sha = [string]$r0.headSha; if ($sha.Length -gt 7) { $sha = $sha.Substring(0, 7) }
    $mo = "run $($r0.databaseId) ($sha) = $($r0.conclusion)"
    if ($r0.conclusion -eq 'success') { return @{ kq = 'xanh'; mo = $mo } }
    return @{ kq = 'do'; mo = $mo }
}
# Het viec: moi WP (theo thu tu xuat hien trong registry) mot PR xep chong — khong merge, khong mo trung.
function MoPRXepChong {
    $rows = @((Get-Content -LiteralPath $reg -Raw -Encoding utf8 | ConvertFrom-Json).rows)
    $nhanhs = [Collections.Generic.List[object]]::new()
    foreach ($h in $rows) { if ($h.nhanh -and -not ($nhanhs | Where-Object { $_.nhanh -eq $h.nhanh })) { $nhanhs.Add([pscustomobject]@{ nhanh = $h.nhanh; dot = $h.dot }) } }
    $base = if ($regInfo.nhanhGoc) { [string]$regInfo.nhanhGoc } else { 'main' }
    foreach ($w in $nhanhs) {
        $chuaXong = @($rows | Where-Object { $_.nhanh -eq $w.nhanh -and $_.status -ne 'xong' })
        if ($chuaXong.Count) { Ghi "  PR: dung o $($w.dot) — con hang chua xong ($((@($chuaXong) | ForEach-Object { $_.id }) -join ', '))"; break }
        $co = GoiGh @('pr', 'list', '--head', $w.nhanh, '--state', 'open', '--json', 'number,url')
        $daCo = $null; try { $daCo = @($co.out | ConvertFrom-Json) | Select-Object -First 1 } catch { }
        if ($daCo) { Ghi "  PR: $($w.dot) da co $($daCo.url)" }
        else {
            $hd = @($rows | Where-Object { $_.nhanh -eq $w.nhanh -and $_.handover } | ForEach-Object { '- ' + $_.id + ': ' + $_.handover }) -join "`n"
            $body = "Refs: $($regInfo.refs)`n`nChuoi $Chuoi · $($w.dot) · base = $base (PR xep chong; merge theo thu tu, GitHub tu doi base khi nhanh truoc duoc merge).`n`nHandover:`n$hd`n`n🤖 Generated with [Claude Code](https://claude.com/claude-code)"
            $k = GoiGh @('pr', 'create', '--base', $base, '--head', $w.nhanh, '--title', "feat($($w.dot)): chuoi $Chuoi - $($w.dot)", '--body', $body)
            Ghi ("  PR: {0} -> {1}: exit {2} {3}" -f $w.nhanh, $base, $k.ma, $k.out)
            if ($k.ma -ne 0) { break }
        }
        $base = $w.nhanh
    }
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

# ── theo doi truc tiep: doc stream-json cua `claude -p` trong luc phien chay ───────────────
# Moi dong la mot su kien JSON. Su kien cua vai phu mang `parent_tool_use_id` = id lenh Agent da goi vai do.
$script:vaiTheoId = @{}; $script:batDauVai = @{}
# Trong luc theo doi, in bang Write-Host (khong lan vao gia tri tra ve cua TheoDoi); dong quan trong ghi them vao nhat ky
function GhiHost([string]$s) {
    $dong = ('{0}  {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm'), $s)
    Write-Host $dong
    [IO.File]::AppendAllText($nhatKy, "- $dong`n", $utf8)
}
function TomLenh($ten, $inp) {
    switch -Regex ($ten) {
        '^(Read|Edit|Write|MultiEdit)$' { return "$ten " + ([string]$inp.file_path -replace '^.*?(docs|tools|modules|src|agents|\.claude)[\\/]', '$1/') }
        '^(Grep|Glob)$' { return "$ten " + [string]$inp.pattern }
        '^(Bash|PowerShell)$' { $c = ([string]$inp.command -replace '\s+', ' ').Trim(); if ($c.Length -gt 90) { $c = $c.Substring(0, 90) + '…' }; return "$ten $c" }
        '^mcp__' { return ($ten -replace '^mcp__', '') }
        default { return $ten }
    }
}
function InSuKien([string]$dong) {
    if (-not $dong.Trim().StartsWith('{')) { return }
    try { $e = $dong | ConvertFrom-Json -ErrorAction Stop } catch { return }
    $cha = [string]$e.parent_tool_use_id
    $vai = if ($cha -and $script:vaiTheoId.ContainsKey($cha)) { $script:vaiTheoId[$cha] } else { $null }
    $noi = if ($e.message -and $e.message.content -is [array]) { $e.message.content } else { @() }
    if ($e.type -eq 'assistant') {
        foreach ($b in $noi) {
            if ($b.type -ne 'tool_use') { continue }
            if ($b.name -in @('Agent', 'Task')) {
                $loai = if ($b.input.subagent_type) { [string]$b.input.subagent_type } else { 'vai' }
                $script:vaiTheoId[[string]$b.id] = $loai; $script:batDauVai[[string]$b.id] = Get-Date
                GhiHost ("    > {0} bat dau: {1}" -f $loai, [string]$b.input.description)
            }
            elseif ($vai) { if ($ChiTiet) { Write-Host ("{0}      {1} : {2}" -f (Get-Date -Format 'HH:mm'), $vai, (TomLenh $b.name $b.input)) } }
            else {
                $t = TomLenh $b.name $b.input
                if ($ChiTiet -or $t -match 'phien\.ps1 (start|done|dodang|blocked)|git (commit|push)|check-feature-doc|check-agent-artifact|kiem-link') {
                    Write-Host ("{0}      ORCH : {1}" -f (Get-Date -Format 'HH:mm'), $t)
                }
            }
        }
    }
    elseif ($e.type -eq 'user' -and -not $vai) {
        foreach ($b in $noi) {
            $id = [string]$b.tool_use_id
            if ($b.type -eq 'tool_result' -and $script:vaiTheoId.ContainsKey($id)) {
                $phut = [Math]::Round(((Get-Date) - $script:batDauVai[$id]).TotalMinutes, 1)
                GhiHost ("    < {0} xong sau {1} phut{2}" -f $script:vaiTheoId[$id], $phut, $(if ($b.is_error) { ' (LOI)' } else { '' }))
            }
        }
    }
    elseif ($e.type -eq 'system' -and $e.subtype -eq 'api_retry') {
        Write-Host ("{0}      (thu lai API lan {1}: {2})" -f (Get-Date -Format 'HH:mm'), $e.attempt, $e.error)
    }
}
function TheoDoi($proc, [string]$tep, [int]$giayToiDa) {
    $viTri = 0L; $du = ''; $het = (Get-Date).AddSeconds($giayToiDa)
    while ($true) {
        $xong = $proc.HasExited
        if (Test-Path -LiteralPath $tep) {
            $fs = [IO.File]::Open($tep, 'Open', 'Read', 'ReadWrite')
            try {
                if ($fs.Length -gt $viTri) {
                    $fs.Seek($viTri, 'Begin') | Out-Null
                    $buf = New-Object byte[] ($fs.Length - $viTri); $n = $fs.Read($buf, 0, $buf.Length); $viTri += $n
                    $du += $utf8.GetString($buf, 0, $n)
                    $dongs = $du -split "`n"; $du = $dongs[-1]
                    foreach ($d in $dongs[0..($dongs.Count - 2)]) { InSuKien $d }
                }
            }
            finally { $fs.Dispose() }
        }
        if ($xong) { if ($du) { InSuKien $du }; return $true }
        if ((Get-Date) -gt $het) { return $false }
        Start-Sleep -Milliseconds 1000
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
    Ghi ("BAT DAU bo chay (toi da {0} phien, model {1}, {2})" -f $ToiDa, $Model, $(if ($Chuoi) { "chuoi $Chuoi, lenh $lenhPhien" } else { 'WP-14' }))
    if ($Chuoi) { $env:CHAY_TU_DONG_CHUOI = $Chuoi }
    while ($true) {
        if (Test-Path -LiteralPath $phanh) { Ghi 'DUNG: co tep phanh artifacts/agents/tu-dong/DUNG (xoa tep de chay lai)'; break }
        if ($soPhien -ge $ToiDa) { Ghi "DUNG: da chay du $ToiDa phien"; break }
        $ci = TrangThaiCI
        if ($ci.kq -eq 'do') { Ghi "DUNG: cau dao CI — lan ci hoan tat moi nhat tren $($regInfo.nhanhTichHop) DO: $($ci.mo). Xem: gh run view <id> --log-failed"; $maThoat = 1; break }
        if ($ci.kq -eq 'chua-ro') { Ghi "  CI: chua ro ($($ci.mo)) — chay tiep" }
        elseif ($ci.kq -eq 'xanh') { Ghi "  CI: xanh ($($ci.mo))" }
        $n = PH @('next')
        if ($n.ma -eq 5) { Ghi "DUNG: het viec — $($n.out)"; if ($MoPRCuoi -and $Chuoi) { MoPRXepChong }; break }
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
            $p = Start-Process -FilePath 'pwsh' -ArgumentList (QA @('-NoProfile', '-File', $claudeTest, $Root, $lenhPhien)) -WorkingDirectory $Root `
                -RedirectStandardOutput $tep -RedirectStandardError "$tep.err" -NoNewWindow -PassThru
        }
        else {
            $thamSo = QA @('-p', $lenhPhien, '--model', $Model, '--permission-mode', 'auto', '--permission-prompts', 'none', '--output-format', 'stream-json', '--verbose')
            $exe = $claude
            # cmd /c bo cap ngoac dau/cuoi khi co > 2 dau ngoac => boc CA dong lenh them mot cap: cmd /c ""C:\x y\claude.cmd" -p "/..." ..."
            if ($claude -match '\.(cmd|bat)$') { $exe = $env:ComSpec; $thamSo = @('/c', ('"' + ((@(QA @($claude)) + $thamSo) -join ' ') + '"')) }
            $p = Start-Process -FilePath $exe -WorkingDirectory $Root -RedirectStandardOutput $tep -RedirectStandardError "$tep.err" -NoNewWindow -PassThru `
                -ArgumentList $thamSo
        }
        $script:vaiTheoId = @{}; $script:batDauVai = @{}
        if (-not (TheoDoi $p $tep ([int]($GioToiDaMotPhien * 3600)))) {
            $p.Kill($true); Ghi "phien $id qua $GioToiDaMotPhien gio — da dung tien trinh (hang o lai, phien sau lam tiep)"
        }
        $p.WaitForExit()
        $stdout = [string](Get-Content -LiteralPath $tep -Raw -ErrorAction SilentlyContinue)
        $stderr = [string](Get-Content -LiteralPath "$tep.err" -Raw -ErrorAction SilentlyContinue)
        # stream-json: su kien cuoi `type = result` mang ket qua (giong --output-format json); khong co thi lay dong JSON cuoi doc duoc
        $ketQua = ''; $j = $null
        $dongJson = @($stdout -split "`r?`n" | Where-Object { $_.Trim().StartsWith('{') })
        for ($k = $dongJson.Count - 1; $k -ge 0 -and -not $j; $k--) {
            try { $x = $dongJson[$k] | ConvertFrom-Json -ErrorAction Stop; if ($x.type -eq 'result' -or -not $x.type) { $j = $x } } catch { }
        }
        if ($j) { $ketQua = [string]$j.result }
        $ra = $ketQua + "`n" + $stderr
        if (-not $j) { $ketQua = $stdout + "`n" + $stderr; $ra = $ketQua }
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
