<#
.SYNOPSIS
    Bo chay tu dong phien WP-14 bang DeepSeek V4.1-Flash (thay chay-tu-dong.ps1 khi chay qua API DeepSeek).

.DESCRIPTION
    Cung mot vong lap nhu tools/agents/chay-tu-dong.ps1: `phien.ps1 next` chon hang -> chay MOT phien Claude Code
    khong tuong tac -> vong ke. Giu nguyen khoa mot-bo-chay, tep phanh DUNG, cau dao "khong tien trien",
    tran gio moi phien, chan cay lam viec ban ngoai docs/** va agents/state/**, nhat ky JSON tung phien.

    Khac biet so voi ban Anthropic:
      - Provider: ANTHROPIC_BASE_URL=https://api.deepseek.com/anthropic + key DeepSeek (chi dat trong tien trinh con,
        khong ghi vao bien moi truong nguoi dung).
      - Model: deepseek-flash[1m] = DeepSeek-V4.1-Flash-1017 (context 1M), effort mac dinh high.
      - Khong co han muc 5 gio / tuan => bo phan doc "resets 1am" va thay bang xu ly loi API:
          het so du (402 Insufficient Balance) / key sai (401) => DUNG ngay, bao nguoi;
          429 rate limit => cho voi backoff roi lam lai DUNG hang (khong tinh la phien);
          5xx / qua tai / timeout => thu lai toi -SoLanThuLai roi moi tinh la phien hong.
      - Kiem tra API bang 1 request nho (~12 token) truoc khi mo phien dau, de key sai / het tien khong dot 30 phien.
      - Ghi token va chi phi uoc tinh tung phien vao nhat-ky (DeepSeek tinh tien theo token, khong theo goi thang).
      - Bang "vai => model" in luc khoi dong: doc `.claude/agents/*.md` va ap quy tac map cua DeepSeek
        (claude-opus* -> deepseek-v4-pro, claude* khac -> deepseek-flash). Canh bao neu co vai roi vao gia Pro.

    Nhat ky: artifacts/agents/tu-dong/nhat-ky-deepseek.md + tung phien `ds-<gio>-<hang>.json`.
    Khoa va tep phanh dung CHUNG voi ban Anthropic (khong the mo phien song song).

.PARAMETER ApiKey
    Key DeepSeek. Bo trong => lay $env:DEEPSEEK_API_KEY, roi $env:ANTHROPIC_AUTH_TOKEN (bien nguoi dung da dat).

.PARAMETER Effort
    low | high | max. Mac dinh high (muc mac dinh cua model). max = suy luan sau hon, ton token hon.

.PARAMETER KhongChoPhepPro
    Dung han neu co vai nao se chay tren deepseek-v4-pro (gia Pro, dat gap ~4 lan Flash).

.PARAMETER KhongKiemTraApi
    Bo qua request kiem tra API luc khoi dong (dung khi mang bi chan tam thoi).

.EXAMPLE
    pwsh -File tools/agents/chay-tu-dong-deepseek.ps1                 # chay toi khi het viec cua agent
    pwsh -File tools/agents/chay-tu-dong-deepseek.ps1 -ToiDa 3        # toi da 3 phien
    pwsh -File tools/agents/chay-tu-dong-deepseek.ps1 -Effort max     # suy luan sau hon
    New-Item artifacts/agents/tu-dong/DUNG                            # (cua so khac) dung sau phien dang chay
#>
[CmdletBinding()]
param(
    [int]$ToiDa = 30,
    [string]$Model = 'deepseek-flash[1m]',
    [ValidateSet('low', 'high', 'max')][string]$Effort = 'high',
    [string]$ApiKey,
    [double]$GioToiDaMotPhien = 6,
    [string]$Root,
    [int]$SoLanThuLai = 3,          # so lan thu lai khi 5xx / qua tai / timeout
    [int]$ChoLaiDauGiay = 60,       # backoff: 60s, 120s, 240s ...
    [int]$ChoLaiToiDaGiay = 900,    # tran mot lan cho khi 429
    [int]$SoLanChoToiDa = 6,        # qua so lan nay lien tiep => dung
    [int]$ChoToiDaGiay = 6 * 3600,  # tran mot lan ngu (de tep phanh co tac dung)
    [int]$BuocChoGiay = 30,
    [switch]$KhongChoPhepPro,
    [switch]$KhongKiemTraApi,
    # Gia USD / 1M token (gia PEAK 01-04h va 06-10h UTC; ngoai gio nay re bang nua). Sua neu DeepSeek doi gia.
    [double]$GiaVao1M = 0.30,
    [double]$GiaRa1M = 1.20,
    [double]$GiaCache1M = 0.006
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); [Console]::InputEncoding = [Text.UTF8Encoding]::new($false) } catch { }
$OutputEncoding = [Text.UTF8Encoding]::new($false)
if (-not $Root) { $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
$utf8 = [Text.UTF8Encoding]::new($false)
$phien = Join-Path $Root 'tools/agents/phien.ps1'
$reg = Join-Path $Root 'agents/state/phien-wp14.json'
$phanh = Join-Path $Root 'artifacts/agents/tu-dong/DUNG'
$khoa = Join-Path $Root 'artifacts/agents/tu-dong/chay-tu-dong.lock'    # dung chung voi ban Anthropic
$logDir = Join-Path $Root 'artifacts/agents/tu-dong'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$nhatKy = Join-Path $logDir 'nhat-ky-deepseek.md'
$baseUrl = 'https://api.deepseek.com/anthropic'

function Ghi([string]$s) {
    $dong = ('{0}  {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm'), $s)
    Write-Output $dong
    [IO.File]::AppendAllText($nhatKy, "- $dong`n", $utf8)
}
function PH([string[]]$a) {
    $out = & pwsh -NoProfile -File $phien @a -Root $Root 2>&1 | Out-String
    return @{ ma = $LASTEXITCODE; out = $out.Trim() }
}
function Tien([double]$usd) { return ('${0:N4}' -f $usd) }

# Cay lam viec ban ngoai docs/** va agents/state/** => /chay-phien se tu dung — kiem truoc de khoi ton mot phien.
function TepBanNgoaiVung {
    $out = & git -C $Root status --porcelain 2>$null
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($out | ForEach-Object { $_.Substring(3).Trim('"') } | ForEach-Object { ($_ -split ' -> ')[-1] } | Where-Object { $_ -and $_ -notmatch '^(docs/|agents/state/|artifacts/)' })
}
function DauVet {
    $hd = Get-ChildItem -LiteralPath (Join-Path $Root 'docs/nhanh') -Filter 'handover-n*.md' -File -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Name + ':' + $_.Length } | Sort-Object
    return (Get-FileHash -LiteralPath $reg -Algorithm SHA256).Hash + '|' + ($hd -join ',')
}

# ── vai => model: doc khai bao trong .claude/agents/*.md, ap quy tac map cua DeepSeek ───────────
# Quan trong: Claude Code gui NGUYEN ten khai trong frontmatter `model:` cua vai, KHONG di qua
# ANTHROPIC_DEFAULT_*_MODEL tru khi khai bao la alias (opus/sonnet/haiku).
function QuyDoiModel([string]$k) {
    if ($k -eq 'opus') { return $env:ANTHROPIC_DEFAULT_OPUS_MODEL }
    if ($k -eq 'sonnet') { return $env:ANTHROPIC_DEFAULT_SONNET_MODEL }
    if ($k -eq 'haiku') { return $env:ANTHROPIC_DEFAULT_HAIKU_MODEL }
    if ($k -match '^claude-opus') { return 'deepseek-v4-pro' }   # map cua DeepSeek cho claude-opus*
    if ($k -match '^claude') { return 'deepseek-flash' }
    return $k
}
function BangVai {
    $dir = Join-Path $Root '.claude/agents'
    if (-not (Test-Path -LiteralPath $dir)) { return @() }
    $ds = @()
    foreach ($f in Get-ChildItem -LiteralPath $dir -Filter '*.md' -File -ErrorAction SilentlyContinue) {
        $m = [regex]::Match([IO.File]::ReadAllText($f.FullName, $utf8), '(?m)^model:\s*(\S+)')
        if (-not $m.Success) { continue }
        $ds += [pscustomobject]@{ Vai = $f.BaseName; Khai = $m.Groups[1].Value; Chay = (QuyDoiModel $m.Groups[1].Value) }
    }
    return $ds
}

# ── kiem tra API: 1 request ~12 token, de key sai / het tien khong dot ca bo chay ──────────────
function KiemTraApi([string]$key) {
    $headers = @{ 'x-api-key' = $key; 'anthropic-version' = '2023-06-01'; 'content-type' = 'application/json' }
    $body = @{
        model      = 'deepseek-flash'
        max_tokens = 16
        thinking   = @{ type = 'disabled' }
        messages   = @(@{ role = 'user'; content = 'Reply with exactly one word: pong' })
    } | ConvertTo-Json -Depth 6 -Compress
    try {
        $r = Invoke-RestMethod -Uri "$baseUrl/v1/messages" -Method Post -Headers $headers `
            -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 60
        $t = ($r.content | Where-Object { $_.type -eq 'text' } | Select-Object -First 1).text
        return @{ ok = $true; chiTiet = "tra loi '$t'" }
    }
    catch {
        $chiTiet = $_.Exception.Message
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $chiTiet = $_.ErrorDetails.Message }
        return @{ ok = $false; chiTiet = $chiTiet }
    }
}

# Tong token cua CA phien: `modelUsage` gom moi model da goi (phien chinh + vai ba/docrev/decider).
function ThongKeToken($j) {
    if (-not $j) { return $null }
    $v = 0; $c = 0; $r = 0; $theo = @()
    if ($j.modelUsage -and @($j.modelUsage.PSObject.Properties).Count) {
        foreach ($p in $j.modelUsage.PSObject.Properties) {
            $mv = [int]$p.Value.inputTokens; $mc = [int]$p.Value.cacheReadInputTokens; $mr = [int]$p.Value.outputTokens
            $v += $mv; $c += $mc; $r += $mr
            $theo += ('{0}: vao {1}, cache {2}, ra {3}' -f $p.Name, $mv, $mc, $mr)
        }
    }
    elseif ($j.usage) {
        $v = [int]$j.usage.input_tokens; $c = [int]$j.usage.cache_read_input_tokens; $r = [int]$j.usage.output_tokens
        $theo += ('phien chinh: vao {0}, cache {1}, ra {2}' -f $v, $c, $r)
    }
    else { return $null }
    return @{ vao = $v; cache = $c; ra = $r; theoModel = $theo }
}

# ── phan loai loi tra ve tu phien (JSON + stderr) ───────────────────────────────────────────────
function PhanLoaiLoi($j, [string]$text) {
    $st = if ($j -and $j.api_error_status) { [int]$j.api_error_status } else { 0 }
    if ($text -match 'Insufficient Balance|insufficient_quota|so du kha dung') { return 'so-du' }
    if ($st -eq 402) { return 'so-du' }
    if ($text -match 'Authentication Fails|invalid api key|invalid_api_key|Unauthorized') { return 'xac-thuc' }
    if ($st -eq 401 -or $st -eq 403) { return 'xac-thuc' }
    if ($text -match 'rate_limit|Rate limit reached') { return 'gioihan' }
    if ($st -eq 429) { return 'gioihan' }
    if ($text -match 'Model Not Exist|model_not_found') { return 'model' }
    if ($st -ge 500) { return 'qua-tai' }
    if ($text -match 'Overloaded|overloaded_error|API Error: 5\d\d|timeout|ETIMEDOUT|ECONNRESET|fetch failed|socket hang up') { return 'qua-tai' }
    return $null
}
function Ngu([int]$giay, [string]$vi) {
    $giay = [Math]::Min([Math]::Max($giay, 1), $ChoToiDaGiay)
    Ghi ("ngu {0} phut ({1}); tao artifacts/agents/tu-dong/DUNG de dung han" -f [Math]::Round($giay / 60), $vi)
    $het = (Get-Date).AddSeconds($giay)
    while ((Get-Date) -lt $het) {
        if (Test-Path -LiteralPath $phanh) { return }
        Start-Sleep -Seconds ([Math]::Max(1, [Math]::Min($BuocChoGiay, [int]($het - (Get-Date)).TotalSeconds)))
    }
}

# ── key ────────────────────────────────────────────────────────────────────────────────────────
$key = $ApiKey
if (-not $key) { $key = $env:DEEPSEEK_API_KEY }
if (-not $key) { $key = $env:ANTHROPIC_AUTH_TOKEN }
if (-not $key) {
    Write-Output 'LOI: chua co DeepSeek API key. Dat bang mot trong:'
    Write-Output '  -ApiKey sk-...            (tham so dong lenh)'
    Write-Output '  $env:DEEPSEEK_API_KEY     (bien moi truong)'
    Write-Output '  $env:ANTHROPIC_AUTH_TOKEN (bien nguoi dung, neu da chay setup-deepseek-claude.ps1)'
    exit 1
}
$key = $key.Trim()
if (-not $key.StartsWith('sk-')) { Write-Output 'LOI: key khong hop le (phai bat dau bang sk-).'; exit 1 }

# Chi dat cho TIEN TRINH CON (khong ghi vao registry / khong anh huong phien claude tay khac).
$env:ANTHROPIC_BASE_URL = $baseUrl
$env:ANTHROPIC_AUTH_TOKEN = $key
$env:ANTHROPIC_MODEL = $Model
$env:ANTHROPIC_DEFAULT_OPUS_MODEL = $Model       # chi co tac dung khi .claude/agents khai `model: opus`
$env:ANTHROPIC_DEFAULT_SONNET_MODEL = $Model
$env:ANTHROPIC_DEFAULT_HAIKU_MODEL = 'deepseek-flash'
$env:CLAUDE_CODE_SUBAGENT_MODEL = $Model
$env:CLAUDE_CODE_EFFORT_LEVEL = $Effort
$env:CLAUDE_CODE_AUTO_COMPACT_WINDOW = '786432'

if (-not $KhongKiemTraApi) {
    $kt = KiemTraApi $key
    if (-not $kt.ok) {
        Write-Output "LOI: API DeepSeek khong dung duoc - $($kt.chiTiet)"
        Write-Output '  Kiem tra: key con hieu luc? con so du? mang/proxy? (chay lai voi -KhongKiemTraApi de bo qua buoc nay)'
        exit 1
    }
    Write-Output "Kiem tra API: OK ($($kt.chiTiet))"
}

# ── khoa: mot bo chay mot luc (dung chung voi chay-tu-dong.ps1) ────────────────────────────────
if (Test-Path -LiteralPath $khoa) {
    $pidCu = ([IO.File]::ReadAllText($khoa, $utf8)).Trim()
    if ($pidCu -match '^\d+$' -and (Get-Process -Id ([int]$pidCu) -ErrorAction SilentlyContinue)) {
        Write-Output "DUNG: bo chay khac dang chay (PID $pidCu) - khong chay song song"; exit 4
    }
}
[IO.File]::WriteAllText($khoa, "$PID", $utf8)

$claudeTest = $env:CHAY_TU_DONG_CLAUDE
$claude = if ($claudeTest) { $null } else { (Get-Command claude -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
if (-not $claudeTest -and -not $claude) { Write-Output 'LOI: khong thay lenh claude trong PATH'; Remove-Item $khoa -Force; exit 1 }

$env:CHAY_TU_DONG_PHIEN = '1'
$env:CLAUDE_CODE_DISABLE_BACKGROUND_TASKS = '1'
$env:CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS = '0'

$maThoat = 0; $soPhien = 0; $khongTien = 0; $lanCho = 0; $dungTruocKhiChay = $false
$tongVao = 0; $tongRa = 0; $tongCache = 0; $tongPhienHong = 0
try {
    Ghi ("BAT DAU bo chay DeepSeek (toi da {0} phien, model {1}, effort {2})" -f $ToiDa, $Model, $Effort)

    $bang = BangVai
    if ($bang.Count) {
        Ghi 'Bang vai => model cho bo chay nay:'
        foreach ($r in $bang) {
            $canh = if ($r.Chay -match 'v4-pro') { '   <== GIA PRO (dat ~4 lan Flash)' } else { '' }
            Ghi ("  {0,-10} {1,-22} -> {2}{3}" -f $r.Vai, $r.Khai, $r.Chay, $canh)
        }
        $pro = @($bang | Where-Object { $_.Chay -match 'v4-pro' })
        if ($pro.Count) {
            $ten = ($pro | ForEach-Object { $_.Vai }) -join ', '
            Ghi ("  CANH BAO: vai $ten se chay tren deepseek-v4-pro vi .claude/agents khai ten claude-opus*.")
            Ghi "           Muon tat ca ve Flash: doi dong 'model:' cua vai do thanh alias  model: opus  (khi do ANTHROPIC_DEFAULT_OPUS_MODEL moi co tac dung)."
            if ($KhongChoPhepPro) { Ghi 'DUNG: -KhongChoPhepPro va co vai roi vao gia Pro.'; $maThoat = 2; $dungTruocKhiChay = $true }
        }
    }

    if ($dungTruocKhiChay -eq $false) {
    while ($true) {
        if (Test-Path -LiteralPath $phanh) { Ghi 'DUNG: co tep phanh artifacts/agents/tu-dong/DUNG (xoa tep de chay lai)'; break }
        if ($soPhien -ge $ToiDa) { Ghi "DUNG: da chay du $ToiDa phien"; break }
        $n = PH @('next')
        if ($n.ma -eq 5) { Ghi "DUNG: het viec - $($n.out)"; break }
        if ($n.ma -ne 0) { Ghi "DUNG: chi con viec cua nguoi / bi chan - $($n.out)"; $maThoat = 3; break }
        $id = ($n.out -split "`r?`n")[-1].Trim()
        $hang = @((Get-Content -LiteralPath $reg -Raw -Encoding utf8 | ConvertFrom-Json).rows | Where-Object { $_.id -eq $id })[0]
        if ($hang.loai -eq 'thao-luan') { Ghi "DUNG: hang $id can nguoi ngoi cung - mo cua so va dan /chay-phien $id"; $maThoat = 3; break }

        $ban = TepBanNgoaiVung
        if ($ban.Count) {
            Ghi ("DUNG: cay lam viec co thay doi ngoai docs/** va agents/state/** - commit tay truoc (phien se tu choi chay):`n    " + ($ban -join "`n    ") + "`n    goi y: git add -- " + ($ban -join ' ') + "; git commit -m `"chore(agents): ...`"")
            $maThoat = 1; break
        }
        $truoc = DauVet
        $soPhien++
        $tep = Join-Path $logDir ('ds-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + "-$id.json")
        Ghi ("PHIEN {0}: hang {1} - {2}" -f $soPhien, $id, $hang.noiDung)
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
            $p.Kill($true); Ghi "phien $id qua $GioToiDaMotPhien gio - da dung tien trinh (hang o lai, phien sau lam tiep)"
        }
        $stdout = [string](Get-Content -LiteralPath $tep -Raw -ErrorAction SilentlyContinue)
        $stderrRaw = Get-Content -LiteralPath "$tep.err" -Raw -ErrorAction SilentlyContinue
        $stderr = [string]$stderrRaw
        $ra = $stdout + "`n" + $stderr
        $ketQua = ''; $j = $null
        try { $j = ($stdout.Trim() | ConvertFrom-Json -ErrorAction Stop); $ketQua = [string]$j.result } catch { $ketQua = $ra }
        $tom = ($ketQua -replace '\s+', ' ').Trim(); if ($tom.Length -gt 300) { $tom = $tom.Substring(0, 300) + '...' }
        Ghi ("  ket thuc exit {0}: {1}" -f $p.ExitCode, $tom)

        # token + chi phi uoc tinh.
        # PHAI doc modelUsage, khong phai usage: `usage` chi la cua phien chinh (ORCH),
        # con `modelUsage` cong don moi model da goi trong phien — ke ca cac vai
        # ba/docrev/decider. Thuc do 23/11: usage=$0.11 nhung modelUsage=$0.46 (gap 4 lan).
        $tk = ThongKeToken $j
        if ($tk) {
            $tongVao += $tk.vao; $tongRa += $tk.ra; $tongCache += $tk.cache
            $usd = ($tk.vao * $GiaVao1M + $tk.cache * $GiaCache1M + $tk.ra * $GiaRa1M) / 1000000
            Ghi ("  token (ca phien + vai): vao {0} (cache {1}) ra {2} - uoc tinh {3}" -f $tk.vao, $tk.cache, $tk.ra, (Tien $usd))
            if ($tk.theoModel.Count -gt 1) { Ghi ("  theo model: {0}" -f ($tk.theoModel -join ' | ')) }
        }
        if ($j -and $j.permission_denials -and @($j.permission_denials).Count) {
            Ghi ("  canh bao: {0} lenh bi tu choi quyen (khong ai duyet): {1}" -f @($j.permission_denials).Count, ((@($j.permission_denials) | ForEach-Object { $c = [string]$_.tool_input.command; if ($c.Length -gt 60) { $c = $c.Substring(0, 60) + '...' }; "$($_.tool_name): $c" }) -join ' | '))
        }
        if ($stderr -match 'Background tasks still running' -or ($j -and $j.subagent_stats -and $j.subagent_stats.killed.system -gt 0)) {
            Ghi '  canh bao: vai chay nen bi dung khi phien ket thuc - phien sau lam tiep hang nay'
        }

        # loi API: phan biet "dung han" voi "thu lai". Chi phan loai khi phien that bai,
        # tranh regex 'timeout' khop voi chu trong cau tra loi cua model.
        $loai = $null
        if ($j -and $j.is_error) { $loai = PhanLoaiLoi $j ($ketQua + "`n" + $ra) }
        elseif ($p.ExitCode -ne 0 -or $stderr -match 'API Error') { $loai = PhanLoaiLoi $j ($ra + "`n" + $ketQua) }
        if ($loai) {
            $soPhien--
            if ($loai -eq 'so-du') {
                Ghi "DUNG: het so du DeepSeek (402 Insufficient Balance) - nap tien roi chay lai: https://platform.deepseek.com/top_up"
                $maThoat = 2; break
            }
            elseif ($loai -eq 'xac-thuc') {
                Ghi "DUNG: key DeepSeek khong hop le hoac da bi thu hoi (401) - tao key moi: https://platform.deepseek.com/api_keys"
                $maThoat = 2; break
            }
            elseif ($loai -eq 'model') {
                Ghi "DUNG: DeepSeek khong co model '$Model' - ten dung la deepseek-flash hoac deepseek-v4-pro."
                $maThoat = 2; break
            }
            elseif ($loai -eq 'gioihan') {
                $lanCho++
                if ($lanCho -gt $SoLanChoToiDa) { Ghi "DUNG: bi 429 qua $SoLanChoToiDa lan lien tiep - giam -ToiDa hoac ha concurrency."; $maThoat = 2; break }
                $giay = [Math]::Min($ChoLaiDauGiay * [Math]::Pow(2, $lanCho - 1), $ChoLaiToiDaGiay)
                Ngu ([int]$giay) ("bi gioi han toc do (429), lan {0}/{1}" -f $lanCho, $SoLanChoToiDa)
                continue
            }
            else {
                $tongPhienHong++
                if ($tongPhienHong -ge $SoLanThuLai) { Ghi "DUNG: $SoLanThuLai phien lien tiep loi ha tang/qua tai - xem tep .json trong artifacts/agents/tu-dong"; $maThoat = 1; break }
                Ghi ("  loi ha tang/qua tai - thu lai (lan {0}/{1})" -f $tongPhienHong, $SoLanThuLai)
                Ngu ($ChoLaiDauGiay * $tongPhienHong) 'thu lai sau loi ha tang'
                continue
            }
        }
        $lanCho = 0; $tongPhienHong = 0

        if ((DauVet) -eq $truoc) {
            $khongTien++
            Ghi "  canh bao: phien khong tien trien (registry + handover khong doi) - lan $khongTien/2"
            if ($khongTien -ge 2) { Ghi 'DUNG: cau dao - 2 phien lien tiep khong tien trien; xem tep .json trong artifacts/agents/tu-dong'; $maThoat = 1; break }
        }
        else { $khongTien = 0 }
    }
    }
}
finally {
    Remove-Item -LiteralPath $khoa -Force -ErrorAction SilentlyContinue
    $st = PH @('status')
    $usdTong = ($tongVao * $GiaVao1M + $tongCache * $GiaCache1M + $tongRa * $GiaRa1M) / 1000000
    Ghi ("KET THUC sau {0} phien. Token: vao {1} (cache {2}) ra {3} - uoc tinh {4} (gia peak; ngoai gio peak re bang nua)." -f $soPhien, $tongVao, $tongCache, $tongRa, (Tien $usdTong))
    Ghi ("Bang:`n{0}" -f $st.out)
}
exit $maThoat
