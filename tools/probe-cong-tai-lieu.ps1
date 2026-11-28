<#
.SYNOPSIS
    CONG TAI LIEU — chan code mot WP GD3 khi tai lieu cua no CHUA dat (2 che do `Code` / `MergeMain`).

.DESCRIPTION
    🚨 **VI SAO CO PROBE NAY (`D-209` ⑥, `D-211`, `D-212`):**
    `D-209` ⑥ giao **QA** "ca quet muc nay voi 3 bien ban (ma nao 'chua' ma co WP dang code ⇒ DO)" va
    **OPS** "dung cong". Cong cu "merge `main` khi bien ban chua ky ⇒ do" (`D-115` ⑤) **chua co**; probe nay
    la **MOT** nguon logic duy nhat, hai che do:

      · `Code`      — chay trong `kiem-tat-ca.ps1` (OPS dau day): chan **CODE** mot WP khi tai lieu chua dat.
      · `MergeMain` — chay o workflow `pull_request` base `main` (OPS dau day): chan **MERGE** khi chua co
                      bien ban **NGUOI KY** (`D-115` ⑤). 📌 KHONG nhet vao `kiem-tat-ca.ps1` vi cong do chay
                      ca tren `push`/`schedule` va PR vao `tich-hop/*` — noi chua can chu ky nguoi.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **MA THOAT (fail-closed — "khong xac dinh duoc" KHONG phai xanh):**
      0 = XANH · 1 = VI PHAM · 2 = KHONG XAC DINH DUOC (nguoi doc phai xem).

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **SUY WP (`D-212` (i)):** `-WP` explicit > `GITHUB_HEAD_REF` > `GITHUB_REF_NAME` >
    `git branch --show-current`; regex `(?i)(?:^|[-_/])wp[-_]?(\d{1,2})(?:[-_/]|$)`. `D-328`: KHONG con tran
    tren; so `< 15` ⇒ `2` (san), so `>= 15` do hang `07` §WP-15+ quyet (co hang ⇒ xac dinh duoc, khong co ⇒ `2`).
    **Khong suy duoc WP ⇒ fail-closed CO GIOI HAN:** diff so `merge-base(origin/main,HEAD)` (CI co the truyen
    `GITHUB_BASE_SHA`) **khong** cham `modules/**`,`src/**`,`shared/**`,`packages/**`,`apps/**`,`test/**`
    ⇒ `0` + in `KHONG_AP_DUNG (chi tai lieu/van hanh)`; **co** cham ⇒ `1` (doi ma nguon ma khong khai WP).

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **ANH XA WP → MA (`D-212` (i)):** doc `docs/nhanh/07-ke-hoach-trien-khai.md`, doan giua `### WP-15+`
    va `---` ke tiep, hang `| **WP-xx** |`, lay moi `[A-Z]{3,4}-\d{3}` trong o **Pham vi**.
    Tap ma RONG ⇒ tra o **Cong tai lieu**: co cau `Không có tài liệu feature` ⇒ **XANH + in ly do**;
    rong ma khong co cau do, hoac khong thay hang WP ⇒ `2`.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **TRANG THAI DUYET (`D-212` (iv), `D-217` ④):**
      · `Code`      = glob `docs/bien-ban-tam-duyet-gd3-*.md`, cot **Ket luan DEC**: o bat dau `Tạm duyệt`
                      ⇒ dat-code; `Chưa` ⇒ khong; khong thay dong/cot, hoac nhieu dong cung ma lech nhau
                      ⇒ khong (fail-closed, in ca hai).
      · `MergeMain` = glob `docs/bien-ban-duyet-gd3-*.md` (hom nay **0** tep) voi dong co ma + cot ket luan
                      `Đã duyệt` + cot **Nguoi ky** khac rong; khong co tep/dong ⇒ DO.
      · **Nguon NGUOI GD1 (`D-217` ④)** — CA HAI che do doc them glob `docs/bien-ban-duyet-gd1-*.md`
                      (bien ban duyet cua CHU DU AN o GD1): tep phai co **dong** chua CA `Hình thức` VA
                      `DUYỆT` (nguoi duyet that) + **bang co cot `Mã`**; moi ma `^[A-Z]{3,4}-\d{3}$` trong
                      bang ⇒ DAT cho ca `Code` va `MergeMain` (bo qua kiem tam-duyet/nguoi-ky cho ma do).
                      Tep `bien-ban-duyet-gd1-*` KHONG co dong `Hình thức…DUYỆT` ⇒ **bo qua** (fail-closed).
      · `docs/features/README.md` §2 chi in **CANH BAO** khi lech — **KHONG** doi mau.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **`-TuKiem` (`D-211` ①, `D-217` ④):** dung fixture trong thu muc tam (`Lay-DuongDanTam`, khuon
    `XC-007`), >= 12 ca (ma `Chưa` + WP dang code ⇒ `1` · tam duyet ⇒ `0` o Code / `1` o MergeMain · WP-15
    khong ma ⇒ `0` o CA HAI · khong suy duoc WP + co tep ma nguon ⇒ `1` · khong suy duoc WP + chi tai lieu
    ⇒ `0` · co bien ban nguoi ky (`Đã duyệt` + `Nguoi ky`) ⇒ MergeMain `0` · ma 4 chu cai (`ADDR-002`)
    tam duyet ⇒ Code `0` · bien ban KHONG co ma do ⇒ Code `1` · **GD1 co ma ⇒ `0` o Code VA MergeMain ·
    GD1 thieu ma ⇒ `1` o CA HAI · GD1 KHONG co `Hình thức: DUYỆT` ⇒ khong tinh la nguon ⇒ `1` ·
    **`D-328`: WP-41 co hang `07` ⇒ `0` o CA HAI (khong con tran cung `>34`) · WP-49 khong co hang ⇒ `2` ·
    WP-14 < san 15 ⇒ `2`**). Moi ca
    in `DAT/DO` + ma thoat ky vong. Cuoi `-TuKiem`: moi ca DAT ⇒ `exit 0`, nguoc lai `exit 1`.

    📌 **WP-15 XANH o CA HAI che do** vi hang WP-15 co tap ma rong + cau `Không có tài liệu feature`
    (`D-212` (iii)). Probe nay **khong** cham DB, **khong** chay Host — chi doc tep.

.PARAMETER CheDo
    `Code` (mac dinh) hoac `MergeMain`.

.PARAMETER WP
    `WP-xx` / `xx` / `auto` (mac dinh `auto`).

.PARAMETER Goc
    Goc repo (mac dinh goc repo chua script).

.PARAMETER TuKiem
    Chay fixture trong thu muc tam — KHONG can repo that, KHONG doc bien ban that.

.EXAMPLE
    pwsh -File tools\probe-cong-tai-lieu.ps1 -TuKiem

.EXAMPLE
    pwsh -File tools\probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-15
#>
[CmdletBinding()]
param(
    [ValidateSet('Code', 'MergeMain')][string]$CheDo = 'Code',
    [string]$WP = 'auto',
    [string]$Goc = '',
    [switch]$TuKiem
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Goc)) {
    $Goc = Split-Path -Parent $PSScriptRoot
}

# 🔒 `XC-007` P-01…P-06: cung mot cho cho khac biet Windows/Linux (`Lay-DuongDanTam`).
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# HAM THUAN — doc/parse (khong cham DB, khong chay Host)
# ══════════════════════════════════════════════════════════════════════════════════════════════════

<# Chuan hoa mot o markdown: bo dam/nhay/code va khoang trang dau-cuoi. #>
function Chuan-O([string]$s) {
    if ($null -eq $s) { return '' }
    return (($s -replace '\*', '' -replace '`', '' -replace '"', '').Trim())
}

<# Tach mot dong bang markdown thanh mang o (bo `|` dau-cuoi). #>
function Lay-O([string]$dong) {
    $t = $dong.Trim()
    if ($t.StartsWith('|')) { $t = $t.Substring(1) }
    if ($t.EndsWith('|')) { $t = $t.Substring(0, $t.Length - 1) }
    return , @($t -split '\|' | ForEach-Object { $_.Trim() })
}

<# Tim chi so o dau tien khop mau; khong thay ⇒ -1. #>
function Tim-Cot {
    param([string[]]$O, [string]$Mau)
    for ($i = 0; $i -lt $O.Count; $i++) {
        if ($O[$i] -match $Mau) { return $i }
    }
    return -1
}

<#
    Doc cac bien ban theo glob `docs/<glob>` va tra hashtable: ma ⇒ mang PSCustomObject { KetLuan; NguoiKy; Tep }.
    🔒 Fail-closed: header phai co cot MA va cot ket luan; thieu cot ⇒ bo qua tep do (khong doan).
#>
function Doc-BienBan {
    param(
        [string]$GocRepo,
        [string]$MauGlob,
        [string]$CotKetLuanMau,
        [string]$CotNguoiKyMau = ''
    )

    $ketQua = @{}
    $thuMuc = Join-Path $GocRepo 'docs'

    if (-not (Test-Path $thuMuc)) { return $ketQua }

    $cacTep = @(Get-ChildItem -Path $thuMuc -Filter $MauGlob -File -ErrorAction SilentlyContinue | Sort-Object Name)

    foreach ($f in $cacTep) {
        $dong = [IO.File]::ReadAllLines($f.FullName, [Text.Encoding]::UTF8)

        $idxMa = -1; $idxKq = -1; $idxKy = -1

        for ($i = 0; $i -lt $dong.Count; $i++) {
            if (-not $dong[$i].TrimStart().StartsWith('|')) { continue }

            $o = Lay-O $dong[$i]
            $jMa = Tim-Cot -O $o -Mau '(?i)^\s*m[ãa]\s*$'

            if ($jMa -ge 0) {
                $jKq = Tim-Cot -O $o -Mau $CotKetLuanMau

                if ($jKq -ge 0) {
                    $idxMa = $jMa
                    $idxKq = $jKq
                    $idxKy = if ([string]::IsNullOrWhiteSpace($CotNguoiKyMau)) { -1 } else { Tim-Cot -O $o -Mau $CotNguoiKyMau }
                    break
                }
            }
        }

        if ($idxMa -lt 0 -or $idxKq -lt 0) { continue }

        for ($i = 0; $i -lt $dong.Count; $i++) {
            if (-not $dong[$i].TrimStart().StartsWith('|')) { continue }

            $o = Lay-O $dong[$i]
            $ma = Chuan-O $o[$idxMa]

            if ($ma -notmatch '^[A-Z]{3,4}-\d{3}$') { continue }

            $kqO = if ($idxKq -lt $o.Count) { Chuan-O $o[$idxKq] } else { '' }
            $kyO = if ($idxKy -ge 0 -and $idxKy -lt $o.Count) { Chuan-O $o[$idxKy] } else { '' }

            if (-not $ketQua.ContainsKey($ma)) { $ketQua[$ma] = @() }

            $ketQua[$ma] += [pscustomobject]@{ KetLuan = $kqO; NguoiKy = $kyO; Tep = $f.Name }
        }
    }

    return $ketQua
}

<#
    Doc bien ban duyet cua CHU DU AN o GD1 (`docs/bien-ban-duyet-gd1-*.md`, `D-217` ④) va tra hashtable:
    ma ⇒ mang PSCustomObject { KetLuan; NguoiKy; Tep } (cung khuon `Doc-BienBan` de dung chung logic).
    🔒 Fail-closed: tep phai co MOT dong chua CA `Hình thức` VA `DUYỆT` (nguoi duyet that); khong co
    ⇒ bo qua ca tep (khong doan). Trong tep: quet tung bang co cot `Mã`, moi o `Mã` khop
    `^[A-Z]{3,4}-\d{3}$` ⇒ them. Day la NGUON DAT cho CA `Code` va `MergeMain`.
#>
function Doc-BienBanGd1 {
    param([string]$GocRepo)

    $ketQua = @{}
    $thuMuc = Join-Path $GocRepo 'docs'

    if (-not (Test-Path $thuMuc)) { return $ketQua }

    $cacTep = @(Get-ChildItem -Path $thuMuc -Filter 'bien-ban-duyet-gd1-*.md' -File -ErrorAction SilentlyContinue | Sort-Object Name)

    foreach ($f in $cacTep) {
        $dong = [IO.File]::ReadAllLines($f.FullName, [Text.Encoding]::UTF8)

        # 🔒 Chi nhan tep co dong chua CA `Hình thức` VA `DUYỆT` — neu khong, bo qua (fail-closed).
        $coHinhThucDuyet = $false

        for ($k = 0; $k -lt $dong.Count; $k++) {
            if (($dong[$k] -match '(?i)hình thức') -and ($dong[$k] -match '(?i)duyệt')) { $coHinhThucDuyet = $true; break }
        }

        if (-not $coHinhThucDuyet) { continue }

        # Quet TUNG bang markdown: header co cot `Mã`, roi cac dong du lieu lien tiep.
        $i = 0

        while ($i -lt $dong.Count) {
            if (-not $dong[$i].TrimStart().StartsWith('|')) { $i++; continue }

            $o = Lay-O $dong[$i]
            $idxMa = Tim-Cot -O $o -Mau '(?i)^\s*m[ãa]\s*$'

            if ($idxMa -lt 0) { $i++; continue }

            # Day la header cua mot bang co cot `Mã` ⇒ cac dong pipe lien tiep la du lieu cua bang.
            $j = $i + 1

            while ($j -lt $dong.Count -and $dong[$j].TrimStart().StartsWith('|')) {
                $oj = Lay-O $dong[$j]
                $ma = if ($idxMa -lt $oj.Count) { Chuan-O $oj[$idxMa] } else { '' }

                if ($ma -match '^[A-Z]{3,4}-\d{3}$') {
                    if (-not $ketQua.ContainsKey($ma)) { $ketQua[$ma] = @() }

                    $ketQua[$ma] += [pscustomobject]@{ KetLuan = 'Đã duyệt (GD1)'; NguoiKy = 'Chủ dự án (biên bản GĐ1)'; Tep = $f.Name }
                }

                $j++
            }

            $i = $j
        }
    }

    return $ketQua
}

<# Doc `07` §WP-15+, tim hang `| **WP-xx** |`; tra { TimThay; Ma; CongTaiLieu }. #>
function Lay-HangWp {
    param([string]$GocRepo, [int]$WpSo)

    $tep = Join-Path $GocRepo 'docs/nhanh/07-ke-hoach-trien-khai.md'

    if (-not (Test-Path $tep)) {
        return [pscustomobject]@{ TimThay = $false; Ma = @(); CongTaiLieu = ''; LyDo = "khong thay $tep" }
    }

    $dong = [IO.File]::ReadAllLines($tep, [Text.Encoding]::UTF8)

    $batDau = -1

    for ($i = 0; $i -lt $dong.Count; $i++) {
        if ($dong[$i] -match '^###\s+WP-15\+') { $batDau = $i; break }
    }

    if ($batDau -lt 0) {
        return [pscustomobject]@{ TimThay = $false; Ma = @(); CongTaiLieu = ''; LyDo = "khong thay muc '### WP-15+'" }
    }

    $ketThuc = $dong.Count

    for ($i = $batDau + 1; $i -lt $dong.Count; $i++) {
        if ($dong[$i] -match '^---\s*$') { $ketThuc = $i; break }
    }

    $mauHang = '^\|\s*\*\*WP-{0}\*\*\s*\|' -f $WpSo

    for ($i = $batDau + 1; $i -lt $ketThuc; $i++) {
        if ($dong[$i] -match $mauHang) {
            $o = Lay-O $dong[$i]

            # 📌 `Lay-O` da bo `|` dau-cuoi ⇒ o[0] = WP, o[1] = Pham vi, o[2] = Cong tai lieu.
            $phamVi = if ($o.Count -gt 1) { $o[1] } else { '' }
            $cong = if ($o.Count -gt 2) { $o[2] } else { '' }

            $ma = @([regex]::Matches($phamVi, '[A-Z]{3,4}-\d{3}') | ForEach-Object { $_.Value } | Sort-Object -Unique)

            return [pscustomobject]@{ TimThay = $true; Ma = $ma; CongTaiLieu = $cong; LyDo = '' }
        }
    }

    return [pscustomobject]@{ TimThay = $false; Ma = @(); CongTaiLieu = ''; LyDo = "khong thay hang WP-$WpSo" }
}

<# CANH BAO (khong doi mau) khi `docs/features/README.md` §2 lech bien ban. #>
function Get-CanhBaoReadme {
    param([string]$GocRepo, [string[]]$Ma, [hashtable]$TrangThai)

    $canhBao = @()
    $tep = Join-Path $GocRepo 'docs/features/README.md'

    if (-not (Test-Path $tep) -or $Ma.Count -eq 0) { return $canhBao }

    $dong = [IO.File]::ReadAllLines($tep, [Text.Encoding]::UTF8)

    $batDau = -1; $ketThuc = $dong.Count

    for ($i = 0; $i -lt $dong.Count; $i++) {
        if ($dong[$i] -match '^##\s+2\.') { $batDau = $i; break }
    }

    if ($batDau -lt 0) { return $canhBao }

    for ($i = $batDau + 1; $i -lt $dong.Count; $i++) {
        if ($dong[$i] -match '^##\s+\d+\.') { $ketThuc = $i; break }
    }

    foreach ($m in $Ma) {
        $trang = if ($TrangThai.ContainsKey($m)) { $TrangThai[$m] } else { '' }

        for ($i = $batDau + 1; $i -lt $ketThuc; $i++) {
            if ($dong[$i] -notmatch [regex]::Escape($m)) { continue }

            $coChua = $dong[$i] -match '(?i)chưa'
            $coTam = $dong[$i] -match '(?i)tạm duyệt'

            if ($trang -eq 'tam' -and $coChua -and -not $coTam) {
                $canhBao += ("{0}: README §2 noi 'chua' nhung bien ban tam duyet (chi CANH BAO, khong doi mau)" -f $m)
            }
            elseif ($trang -eq 'chua' -and $coTam -and -not $coChua) {
                $canhBao += ("{0}: README §2 noi 'tam duyet' nhung bien ban 'Chua' (chi CANH BAO, khong doi mau)" -f $m)
            }
        }
    }

    return $canhBao
}

<# Suy so WP tu mot chuoi (ten nhanh/ref); khong khop ⇒ $null. #>
function Suy-WpTuChuoi {
    param([string]$s)

    if ([string]::IsNullOrWhiteSpace($s)) { return $null }

    $m = [regex]::Match($s, '(?i)(?:^|[-_/])wp[-_]?(\d{1,2})(?:[-_/]|$)')

    if ($m.Success) { return [int]$m.Groups[1].Value }

    return $null
}

<# Suy WP theo thu tu `D-212` (i) — tra so WP hoac $null (khong suy duoc). #>
function Suy-Wp {
    param([string]$WpTuyChon, [string]$GocRepo)

    if ($null -ne $WpTuyChon -and $WpTuyChon -ne 'auto' -and -not [string]::IsNullOrWhiteSpace($WpTuyChon)) {
        $m = [regex]::Match($WpTuyChon, '(\d{1,2})')

        if ($m.Success) { return [int]$m.Groups[1].Value }

        return -1   # khai WP nhung khong doc duoc so ⇒ khong xac dinh
    }

    foreach ($nguon in @($env:GITHUB_HEAD_REF, $env:GITHUB_REF_NAME)) {
        $so = Suy-WpTuChuoi -s $nguon

        if ($null -ne $so) { return $so }
    }

    try {
        # 📌 Doc raw TRUOC roi moi loc: `... | Select-Object -First 1` lam `$LASTEXITCODE` KHONG duoc dat
        #    (do duoc 25/11/2026 — nhanh dung ma van roi vao "khong suy duoc").
        $nhanhDong = @(& git -C $GocRepo branch --show-current 2>$null)
        $maGit = $LASTEXITCODE

        if ($maGit -eq 0 -and $nhanhDong.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$nhanhDong[0])) {
            $so = Suy-WpTuChuoi -s ([string]$nhanhDong[0]).Trim()

            if ($null -ne $so) { return $so }
        }
    }
    catch {
        return $null
    }

    return $null
}

<# Tep thay doi so voi `merge-base(origin/main,HEAD)` (CI co the truyen `GITHUB_BASE_SHA`). $null = khong xac dinh. #>
function Lay-TepThayDoi {
    param([string]$GocRepo)

    $base = $env:GITHUB_BASE_SHA

    if ([string]::IsNullOrWhiteSpace($base)) { $base = $env:GITHUB_EVENT_BASE_SHA }

    if ([string]::IsNullOrWhiteSpace($base)) {
        $mb = @(& git -C $GocRepo merge-base origin/main HEAD 2>$null)
        $maMb = $LASTEXITCODE

        if ($maMb -ne 0 -or $mb.Count -eq 0 -or [string]::IsNullOrWhiteSpace([string]$mb[-1])) {
            return $null
        }

        $base = ([string]$mb[-1]).Trim()
    }

    $out = @(& git -C $GocRepo diff --name-only "$base...HEAD" 2>$null)
    $maDiff = $LASTEXITCODE

    if ($maDiff -ne 0) { return $null }

    return , @($out | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
}

<# Khong suy duoc WP ⇒ fail-closed CO GIOI HAN (`D-212` (i)). #>
function Kiem-KhongSuyDuocWp {
    param([string[]]$Tep)

    if ($null -eq $Tep) {
        return [pscustomobject]@{ Ma = 2; ChiTiet = @('khong xac dinh duoc danh sach tep thay doi (git/merge-base loi)') }
    }

    $mau = @('modules/', 'src/', 'shared/', 'packages/', 'apps/', 'test/')
    $cham = @($Tep | Where-Object {
            $p = ([string]$_ -replace '\\', '/')
            @($mau | Where-Object { $p.StartsWith($_) }).Count -gt 0
        })

    if ($cham.Count -gt 0) {
        return [pscustomobject]@{ Ma = 1; ChiTiet = @('doi ma nguon ma khong khai WP: ' + ($cham -join ', ')) }
    }

    return [pscustomobject]@{ Ma = 0; ChiTiet = @('KHONG_AP_DUNG (chi tai lieu/van hanh)') }
}

<#
    LOI CHINH: kiem cong cho mot WP da biet. Tra { Ma; ChiTiet }.
    `Ma = 0` xanh · `1` vi pham · `2` khong xac dinh duoc.
#>
function Kiem-Cong {
    param([string]$GocRepo, [string]$CheDoCong, [int]$WpSo)

    # 🔒 `D-328` (1): SAN `< 15` — `07` §WP-15+ khong co hang nao < 15 ⇒ fail-closed, "khong xac dinh duoc".
    # KHONG con TRAN tren: WP >= 15 do `Lay-HangWp` quyet (co hang ⇒ xac dinh duoc; khong co hang ⇒ 2).
    if ($WpSo -lt 15) {
        return [pscustomobject]@{ Ma = 2; ChiTiet = @("WP-$WpSo < 15: 07 muc WP-15+ khong co hang nao duoi WP-15 ⇒ KHONG XAC DINH DUOC") }
    }

    $hang = Lay-HangWp -GocRepo $GocRepo -WpSo $WpSo

    if (-not $hang.TimThay) {
        return [pscustomobject]@{ Ma = 2; ChiTiet = @($hang.LyDo) }
    }

    if ($hang.Ma.Count -eq 0) {
        if ($hang.CongTaiLieu -match 'Không có tài liệu feature') {
            return [pscustomobject]@{ Ma = 0; ChiTiet = @("WP-$WpSo khong co ma tai lieu (07 ghi 'Không có tài liệu feature') ⇒ XANH") }
        }

        return [pscustomobject]@{ Ma = 2; ChiTiet = @("WP-$WpSo khong co ma tai lieu VA o Cong tai lieu KHONG noi 'Không có tài liệu feature'") }
    }

    if ($CheDoCong -eq 'Code') {
        $bb = Doc-BienBan -GocRepo $GocRepo -MauGlob 'bien-ban-tam-duyet-gd3-*.md' -CotKetLuanMau '(?i)kết luận dec'
        $gd1 = Doc-BienBanGd1 -GocRepo $GocRepo

        $chiTiet = @()
        $thongTin = @()
        $trangThai = @{}

        foreach ($ma in $hang.Ma) {
            # 🔑 Nguon NGUOI o GD1 (`D-217` ④): da duyet that ⇒ dat, bo qua kiem tam-duyet cho ma nay.
            if ($gd1.ContainsKey($ma)) {
                $trangThai[$ma] = 'gd1'
                $thongTin += ("{0}: duyệt GĐ1 (nguồn NGƯỜI) ⇒ đạt" -f $ma)
                continue
            }

            $dong = if ($bb.ContainsKey($ma)) { @($bb[$ma]) } else { @() }

            if ($dong.Count -eq 0) {
                $chiTiet += ("{0}: KHONG thay dong trong bien ban tam duyet" -f $ma)
                $trangThai[$ma] = 'chua'
                continue
            }

            $giaTri = @($dong | ForEach-Object { $_.KetLuan } | Sort-Object -Unique)

            if ($giaTri.Count -gt 1) {
                $chiTiet += ("{0}: nhieu dong cung ma LECH nhau ({1})" -f $ma, ($giaTri -join ' | '))
                $trangThai[$ma] = 'chua'
                continue
            }

            if ($giaTri[0].StartsWith('Tạm duyệt')) {
                $trangThai[$ma] = 'tam'
            }
            else {
                $trangThai[$ma] = 'chua'
                $chiTiet += ("{0}: ket luan = '{1}' (khong bat dau 'Tạm duyệt')" -f $ma, $giaTri[0])
            }
        }

        if ($hang.CongTaiLieu -match 'chưa') {
            $chiTiet += ("o Cong tai lieu cua WP-{0} chua chu 'chưa' (luat nghiem hon thang)" -f $WpSo)
        }

        $canhBao = @(Get-CanhBaoReadme -GocRepo $GocRepo -Ma $hang.Ma -TrangThai $trangThai)

        if ($chiTiet.Count -gt 0) {
            return [pscustomobject]@{ Ma = 1; ChiTiet = (@($chiTiet) + @($canhBao | ForEach-Object { "CANH BAO: $_" })) }
        }

        $moTa = if ($thongTin.Count -eq 0) {
            "moi ma cua WP-$WpSo deu 'Tạm duyệt' trong bien ban tam duyet"
        }
        else {
            "moi ma cua WP-$WpSo deu dat (nguon GD1/tam duyet)"
        }

        return [pscustomobject]@{ Ma = 0; ChiTiet = (@($thongTin) + @($moTa) + @($canhBao | ForEach-Object { "CANH BAO: $_" })) }
    }

    # ── MergeMain ──────────────────────────────────────────────────────────────────────────────────
    $bb = Doc-BienBan -GocRepo $GocRepo -MauGlob 'bien-ban-duyet-gd3-*.md' -CotKetLuanMau '(?i)kết luận' -CotNguoiKyMau '(?i)người ký'
    $gd1 = Doc-BienBanGd1 -GocRepo $GocRepo

    $chiTiet = @()
    $thongTin = @()

    $thuMuc = Join-Path $GocRepo 'docs'
    $coBienBan = @(Get-ChildItem -Path $thuMuc -Filter 'bien-ban-duyet-gd3-*.md' -File -ErrorAction SilentlyContinue)

    # 🔒 Chi bao loi cung khi KHONG co nguon ky nao (khong co gd3 VA GD1 rong).
    if ($coBienBan.Count -eq 0 -and $gd1.Count -eq 0) {
        $chiTiet += 'KHONG co bien ban nguoi ky (docs/bien-ban-duyet-gd3-*.md) VA khong co bien ban GD1 ⇒ DO (D-115 ⑤)'
    }

    foreach ($ma in $hang.Ma) {
        # 🔑 Nguon NGUOI o GD1 (`D-217` ④): da duyet that ⇒ dat.
        if ($gd1.ContainsKey($ma)) {
            $thongTin += ("{0}: duyệt GĐ1 (nguồn NGƯỜI) ⇒ đạt" -f $ma)
            continue
        }

        $dong = if ($bb.ContainsKey($ma)) { @($bb[$ma]) } else { @() }

        $dat = @($dong | Where-Object {
                $_.KetLuan -eq 'Đã duyệt' -and -not [string]::IsNullOrWhiteSpace($_.NguoiKy)
            })

        if ($dat.Count -eq 0) {
            $chiTiet += ("{0}: KHONG co dong 'Đã duyệt' + 'Người ký' khac rong" -f $ma)
        }
    }

    if ($chiTiet.Count -gt 0) {
        return [pscustomobject]@{ Ma = 1; ChiTiet = (@($chiTiet) + @($thongTin)) }
    }

    return [pscustomobject]@{ Ma = 0; ChiTiet = (@($thongTin) + @("moi ma cua WP-$WpSo co bien ban nguoi ky 'Đã duyệt' + 'Người ký' hoac bien ban GD1")) }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# TU KIEM (`-TuKiem`) — fixture trong thu muc tam, >= 8 ca
# ══════════════════════════════════════════════════════════════════════════════════════════════════

function Ghi-Fixture07 {
    param([string]$GocTam)

    $thuMuc = Join-Path $GocTam 'docs/nhanh'
    New-Item -ItemType Directory -Force -Path $thuMuc | Out-Null

    $s = @'
### WP-15+ — fixture tu kiem cong tai lieu

| WP | Phạm vi | Cổng tài liệu (điều kiện vào) | Phụ thuộc |
|---|---|---|---|
| **WP-15** | **Nền GĐ3** | Không có tài liệu feature. Vào được khi build/test chạy | — |
| **WP-16** | `PRC-002` phần còn thiếu | `PRC-002` đã duyệt `Q5` | WP-15 |
| **WP-18** | `ADDR-002` (ma 4 chu cai) | fixture dieu kien | WP-16 |
| **WP-17** | `PRC-001` | fixture GD1 | WP-16 |
| **WP-19** | `PRC-099` | fixture am | WP-16 |
| **WP-41** | fixture | Không có tài liệu feature. fixture | — |

---
'@

    [IO.File]::WriteAllText((Join-Path $thuMuc '07-ke-hoach-trien-khai.md'), $s, [Text.UTF8Encoding]::new($false))
}

function Ghi-FixtureTamDuyet {
    param([string]$GocTam, [string]$KetLuan, [string]$Ma = 'PRC-002')

    $thuMuc = Join-Path $GocTam 'docs'
    New-Item -ItemType Directory -Force -Path $thuMuc | Out-Null

    $s = @'
# Fixture bien ban tam duyet (chi de tu kiem probe)

| # | MÃ | Tài liệu | DOCREV | Kết luận DEC | Lý do | Điều kiện | Người review còn thiếu |
|---|---|---|---|---|---|---|---|
| 1 | __MA__ | fixture | DOC-OK | **__KETLUAN__** | fixture | fixture | fixture |
'@

    $s = $s.Replace('__KETLUAN__', $KetLuan).Replace('__MA__', $Ma)

    [IO.File]::WriteAllText((Join-Path $thuMuc 'bien-ban-tam-duyet-gd3-tukiem-2026-11-24.md'), $s, [Text.UTF8Encoding]::new($false))
}

function Ghi-FixtureNguoiKy {
    param([string]$GocTam)

    $thuMuc = Join-Path $GocTam 'docs'
    New-Item -ItemType Directory -Force -Path $thuMuc | Out-Null

    $s = @'
# Fixture bien ban NGUOI KY (chi de tu kiem probe)

| # | MÃ | Tài liệu | Kết luận | Người ký |
|---|---|---|---|---|
| 1 | PRC-002 | fixture | Đã duyệt | Nguyễn Văn A |
'@

    [IO.File]::WriteAllText((Join-Path $thuMuc 'bien-ban-duyet-gd3-tukiem-2026-11-24.md'), $s, [Text.UTF8Encoding]::new($false))
}

function Xoa-FixtureNguoiKy {
    param([string]$GocTam)

    $tep = Join-Path $GocTam 'docs/bien-ban-duyet-gd3-tukiem-2026-11-24.md'

    if (Test-Path $tep) { Remove-Item -Force $tep }
}

<# Fixture bien ban duyet cua CHU DU AN o GD1 (`D-217` ④). `-KhongHinhThuc` = bien the thieu dong
   `Hình thức: DUYỆT` ⇒ phai bi bo qua (ca am fail-closed). #>
function Ghi-FixtureGd1 {
    param([string]$GocTam, [switch]$KhongHinhThuc)

    $thuMuc = Join-Path $GocTam 'docs'
    New-Item -ItemType Directory -Force -Path $thuMuc | Out-Null

    $hinhThuc = if ($KhongHinhThuc) { '' } else { '| **Hình thức** | **DUYỆT** — fixture |' }

    $s = @"
# Fixture bien ban duyet GD1 (chi de tu kiem probe)

$hinhThuc

| # | Mã | Tài liệu |
|---|---|---|
| 1 | PRC-001 | fixture GD1 |
"@

    [IO.File]::WriteAllText((Join-Path $thuMuc 'bien-ban-duyet-gd1-tukiem-2026-10-29.md'), $s, [Text.UTF8Encoding]::new($false))
}

function Xoa-FixtureGd1 {
    param([string]$GocTam)

    $tep = Join-Path $GocTam 'docs/bien-ban-duyet-gd1-tukiem-2026-10-29.md'

    if (Test-Path $tep) { Remove-Item -Force $tep }
}

function Invoke-TuKiem {
    $gocTam = Lay-DuongDanTam ("wp15-cong-tai-lieu-" + [Guid]::NewGuid().ToString('N'))

    $script:soCaTuKiem = 0
    $script:soCaTuKiemDat = 0

    function Ca([string]$Ten, [int]$MongDoi, [int]$ThucTe) {
        $script:soCaTuKiem++

        if ($ThucTe -eq $MongDoi) {
            $script:soCaTuKiemDat++
            Write-Host ("   [DAT] {0}: exit {1} (mong doi {1})" -f $Ten, $ThucTe)
        }
        else {
            Write-Host ("   [DO ] {0}: exit {1} (mong doi {2})" -f $Ten, $ThucTe, $MongDoi)
        }
    }

    try {
        New-Item -ItemType Directory -Force -Path $gocTam | Out-Null
        Ghi-Fixture07 -GocTam $gocTam

        Write-Host '════ TU KIEM CONG TAI LIEU (fixture trong thu muc tam) ════'

        # Ca 1 — ma `Chưa` + WP dang code ⇒ DO (1).
        Ghi-FixtureTamDuyet -GocTam $gocTam -KetLuan 'Chưa'
        Xoa-FixtureNguoiKy -GocTam $gocTam
        Ca 'CA1 ma Chua + WP dang code (Code)' 1 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 16).Ma)

        # Ca 2 — tam duyet ⇒ XANH o Code (0) / DO o MergeMain (1, chua co bien ban nguoi ky).
        Ghi-FixtureTamDuyet -GocTam $gocTam -KetLuan 'Tạm duyệt có điều kiện'
        Xoa-FixtureNguoiKy -GocTam $gocTam
        Ca 'CA2 tam duyet ⇒ Code XANH' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 16).Ma)
        Ca 'CA2 tam duyet ⇒ MergeMain DO (chua ky)' 1 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'MergeMain' -WpSo 16).Ma)

        # Ca 3 — WP-15 khong ma ⇒ XANH o CA HAI che do.
        Ca 'CA3 WP-15 khong ma ⇒ Code XANH' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 15).Ma)
        Ca 'CA3 WP-15 khong ma ⇒ MergeMain XANH' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'MergeMain' -WpSo 15).Ma)

        # Ca 4 — khong suy duoc WP + co tep ma nguon ⇒ DO (1).
        Ca 'CA4 khong suy duoc WP + co ma nguon ⇒ DO' 1 ((Kiem-KhongSuyDuocWp -Tep @('modules/Inventory/X.cs', 'docs/a.md')).Ma)

        # Ca 5 — khong suy duoc WP + chi tai lieu ⇒ XANH (0).
        Ca 'CA5 khong suy duoc WP + chi tai lieu ⇒ XANH' 0 ((Kiem-KhongSuyDuocWp -Tep @('docs/nhanh/decisions.md', 'docs/features/README.md')).Ma)

        # Ca 6 — co bien ban NGUOI KY (Đã duyệt + Người ký) ⇒ MergeMain XANH (0).
        Ghi-FixtureTamDuyet -GocTam $gocTam -KetLuan 'Tạm duyệt có điều kiện'
        Ghi-FixtureNguoiKy -GocTam $gocTam
        Ca 'CA6 co bien ban nguoi ky ⇒ MergeMain XANH' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'MergeMain' -WpSo 16).Ma)

        # Ca 7 — ma 4 chu cai (`ADDR-002`) tam duyet ⇒ Code XANH (0). Chong hoi quy regex `{3}` cu
        # (chi nhan 3 chu cai ⇒ cat thanh `DDR-002` va bo sot dong bien ban `ADDR-002`).
        Ghi-FixtureTamDuyet -GocTam $gocTam -KetLuan 'Tạm duyệt có điều kiện' -Ma 'ADDR-002'
        Xoa-FixtureNguoiKy -GocTam $gocTam
        Ca 'CA7 ma 4 chu cai (ADDR-002) tam duyet ⇒ Code XANH' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 18).Ma)

        # Ca 8 — bien ban KHONG co ma do (chi co `ADDR-099`) ⇒ Code DO (1).
        Ghi-FixtureTamDuyet -GocTam $gocTam -KetLuan 'Tạm duyệt có điều kiện' -Ma 'ADDR-099'
        Xoa-FixtureNguoiKy -GocTam $gocTam
        Ca 'CA8 bien ban KHONG co ADDR-002 ⇒ Code DO' 1 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 18).Ma)

        # ── Cac ca NGUON NGUOI GD1 (`D-217` ④) — tao fixture GD1 truoc CA9, xoa sau CA12. ──
        Ghi-FixtureGd1 -GocTam $gocTam

        # Ca 9 — GD1 (nguon NGUOI) co PRC-001, KHONG co bien ban tam duyet ⇒ Code XANH (0).
        Ca 'CA9 GD1 co PRC-001 ⇒ Code XANH (nguon NGUOI)' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 17).Ma)

        # Ca 10 — cung fixture GD1 ⇒ MergeMain XANH (0) (GD1 la nguon ky nguoi).
        Ca 'CA10 GD1 co PRC-001 ⇒ MergeMain XANH' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'MergeMain' -WpSo 17).Ma)

        # Ca 11 — GD1 co PRC-001 nhung KHONG co PRC-099 (WP-19) ⇒ Code VA MergeMain DO (1).
        Ca 'CA11 GD1 thieu PRC-099 ⇒ Code DO' 1 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 19).Ma)
        Ca 'CA11 GD1 thieu PRC-099 ⇒ MergeMain DO' 1 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'MergeMain' -WpSo 19).Ma)

        # Ca 12 — tep GD1 KHONG co dong `Hình thức: DUYỆT` ⇒ KHONG tinh la nguon ⇒ Code DO (1) (fail-closed).
        Ghi-FixtureGd1 -GocTam $gocTam -KhongHinhThuc
        Ca 'CA12 GD1 khong co Hinh thuc DUYET ⇒ Code DO' 1 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 17).Ma)

        Xoa-FixtureGd1 -GocTam $gocTam

        # Ca 13 (xuoi, `D-328`) — WP-41 co hang trong `07` §WP-15+ (tap ma rong + `Không có tài liệu feature`)
        # ⇒ XANH o CA HAI che do. Chong hoi quy TRAN CUNG `>34` (truoc day WP-41 ⇒ exit 2).
        Ca 'CA13 WP-41 co hang 07 ⇒ Code XANH (khong tran cung)' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 41).Ma)
        Ca 'CA13 WP-41 co hang 07 ⇒ MergeMain XANH' 0 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'MergeMain' -WpSo 41).Ma)

        # Ca 14 (nguoc / fail-closed, `D-328`) — KHONG co hang trong fixture ⇒ 2 (WP-49); so < 15 ⇒ 2 (WP-14).
        Ca 'CA14 WP-49 KHONG co hang 07 ⇒ KHONG XAC DINH DUOC' 2 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 49).Ma)
        Ca 'CA14 WP-14 < san 15 ⇒ KHONG XAC DINH DUOC' 2 ((Kiem-Cong -GocRepo $gocTam -CheDoCong 'Code' -WpSo 14).Ma)
    }
    finally {
        if (Test-Path $gocTam) { Remove-Item -Recurse -Force $gocTam -ErrorAction SilentlyContinue }
    }

    Write-Host ("════ TU KIEM: {0}/{1} ca DAT ════" -f $script:soCaTuKiemDat, $script:soCaTuKiem)

    if ($script:soCaTuKiem -ge 12 -and $script:soCaTuKiemDat -eq $script:soCaTuKiem) { return 0 }

    return 1
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# MAIN
# ══════════════════════════════════════════════════════════════════════════════════════════════════

if ($TuKiem) {
    exit (Invoke-TuKiem)
}

Write-Host '════ CONG TAI LIEU ════'
Write-Host ("   Goc     = {0}" -f $Goc)
Write-Host ("   Che do  = {0}" -f $CheDo)
Write-Host ("   WP      = {0}" -f $WP)

$wpSo = Suy-Wp -WpTuyChon $WP -GocRepo $Goc

if ($null -eq $wpSo) {
    $tepThayDoi = Lay-TepThayDoi -GocRepo $Goc
    $kq = Kiem-KhongSuyDuocWp -Tep $tepThayDoi

    Write-Host '   WP: KHONG suy duoc tu -WP/ref/ten nhanh ⇒ fail-closed co gioi han'
    foreach ($d in $kq.ChiTiet) { Write-Host ("   · {0}" -f $d) }
    Write-Host ("KET QUA: {0}" -f $(if ($kq.Ma -eq 0) { 'XANH' } elseif ($kq.Ma -eq 1) { 'VI PHAM' } else { 'KHONG XAC DINH DUOC' }))

    exit $kq.Ma
}

# 🔑 `D-328`: KHONG con tran cung `>34` o day — "xac dinh duoc WP" do `Kiem-Cong`/`Lay-HangWp` quyet
# (co hang `WP-xx` trong `07` §WP-15+ ⇒ xac dinh duoc; khong co hang ⇒ 2; san `< 15` giu trong `Kiem-Cong`).
$kq = Kiem-Cong -GocRepo $Goc -CheDoCong $CheDo -WpSo $wpSo

Write-Host ("   WP      = WP-{0}" -f $wpSo)
foreach ($d in $kq.ChiTiet) { Write-Host ("   · {0}" -f $d) }
Write-Host ("KET QUA: {0} (exit {1})" -f $(if ($kq.Ma -eq 0) { 'XANH' } elseif ($kq.Ma -eq 1) { 'VI PHAM' } else { 'KHONG XAC DINH DUOC' }), $kq.Ma)

exit $kq.Ma
