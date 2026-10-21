#!/usr/bin/env pwsh
<#
.SYNOPSIS
    🚨 **CONG CUA CHINH CONG** (`XC-007` §6.2) — `exit 0` KHONG duoc tin: khang dinh 6 dieu kien DO DUOC.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VI SAO TEP NAY TON TAI (`XC-007` **D4/BR-003**, `S9` 21/10/2026):

    `tools/kiem-tat-ca.ps1` chay buoc ⑧ (E2E) **chi khi** co `-DayDu`; nhanh con lai **in `[BO QUA]` roi
    DI TIEP toi `exit 0`**. ⇒ 🔑 Goi thieu `-DayDu` cho ra **mot ma thoat Y HET** mot lan chay day du, chi
    khac **mot dong log**. 📌 Nghia la *"CI xanh"* co the la *"buoc ⑧ chua he chay"*.

    ⇒ Tren CI, **`exit 0` khong phai bang chung**. Phai khang dinh **dau ra** (muc §6.2 cua `XC-007`).

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🚨 **HAI DIEU CHINH SO VOI BAN DAC TA §6.2 — CA HAI DO DUOC, KHONG PHAI SUY DOAN** (21/10/2026):

      **A2 — `E2E: N/N` KHONG co trong log cua cong.** Ban dac ta noi *"log khớp `^E2E: \d+/\d+ ca DAT$`"*.
        ⚠️ Do lai thi **KHONG BAO GIO khop — ke ca khi moi thu DAT**: `kiem-tat-ca.ps1` chi in **26 dong
        dau tien** khop `✅|🚨|E2E:` cua log E2E (`Select-Object -First 26`), ma dong tong ket `E2E:` nam o
        **cuoi** tep ⇒ 🔑 mot cong **luon do** — dung loai ma nguoi ta se hoc cach bo qua.
        ✅ **Cach dung:** doc dong do tu **chinh log cua buoc** (`tools/_tam-kiem/e2e.log`), va yeu cau tep
        do **MOI** (mtime >= luc bat dau chay) — 📌 neu khong thi mot lan chay **hong tu buoc ②** van thay
        `e2e.log` **cu** cua lan truoc ⇒ **XANH vi LY DO SAI**.

      **A6 — `🚨` XUAT HIEN 39 LAN TRONG MOT LAN CHAY XANH.** Ban dac ta cam *"mot 🚨 cung khong duoc co"*.
        ⚠️ Nhung ten ca kiem **dung 🚨 lam neo** (*"[S5 🚨 khach BI CHAN dat hang => ...] DAT"*), nen cam
        `🚨` la cam chinh **ten ca**. ✅ **Cach dung:** cam dong **BAT DAU bang `🚨`** sau khi `Trim()` — do
        la dung dang cua *dong bao dong* (`"   🚨 ..."` / `"🚨 {ten ca} :: ..."`), khong phai ten ca.

.PARAMETER Log
    Log cua ca lan chay cong (tep ma CI ghi bang `*>` — ⚠️ KHONG ong danh, xem quy tac ① cua `kiem-tat-ca`).

.PARAMETER ThuMucLog
    Thu muc log tung buoc (`tools/_tam-kiem`).

.PARAMETER LinkLog
    Log cua cong ⑨ (tham chieu tai lieu). Rong ⇒ bo qua phep khang dinh A7.

.PARAMETER SoBuocToiThieu
    San so buoc DAT. 📌 12 = so buoc hien tai cua `-DayDu` (`S8`). ⚠️ Them buoc thi phai nang so nay
    **cung luc** — day la **hop dong**, khong phai so trang tri.

.PARAMETER BatDauUtc
    Thoi diem bat dau chay cong (chuoi ISO-8601, UTC). Dung de kiem **do MOI cua log** (A2).
    📌 Nhan **chuoi** chu khong phai `[datetime]`: CI ghi moc ra tep bang `ToString('o')`, va chuoi do mang
    hau to `Z` — neu de PowerShell tu doi kieu thi `Kind` se thanh `Local` va phep so sanh voi
    `LastWriteTimeUtc` se **lech mui gio** (mot loi im lang: tren runner UTC no "tinh co" dung).
    Bo trong ⇒ khong kiem do moi.

.PARAMETER MaCong
    Ma thoat cua lan chay cong. 📌 `2` = **MOI TRUONG** (`VO NGHIA`) — phan biet voi `1` = san pham.

.PARAMETER XuatBangChung
    Duong dan ghi `Evidence.json` (`XC-007` §5.2). Bo trong ⇒ khong ghi.

.EXAMPLE
    pwsh -NoProfile -File tools/ci-khang-dinh.ps1 -Log gate.log -MaCong 0
#>
[CmdletBinding()]
param(
    [string]$Log = 'gate.log',
    [string]$ThuMucLog,
    [string]$LinkLog = '',
    [int]$SoBuocToiThieu = 12,
    [string]$BatDauUtc = '',
    [int]$MaCong = 0,
    [string]$XuatBangChung = ''
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ThuMucLog)) {
    $ThuMucLog = Join-Path $PSScriptRoot '_tam-kiem'
}

# 📌 Chuoi ISO co hau to `Z` ⇒ phai ep **AdjustToUniversal**, neu khong `Kind` = Local va phep so sanh
#    voi `LastWriteTimeUtc` se lech dung bang mui gio cua may (tren runner UTC thi loi nay **an thanh dung**).
$batDau = [datetime]::MinValue

if (-not [string]::IsNullOrWhiteSpace($BatDauUtc)) {
    $batDau = [datetime]::Parse(
        $BatDauUtc,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor [System.Globalization.DateTimeStyles]::AssumeUniversal)
}

$loi = [System.Collections.Generic.List[string]]::new()

function Them-Loi {
    param([string]$Ma, [string]$ThongDiep)
    $loi.Add(("{0}: {1}" -f $Ma, $ThongDiep))
}

<#
    🔒 DOC TEP LOG — va NOI RO khi tep khong ton tai.
    📌 `Get-Content -Raw` tren tep khong ton tai **nem**; o day ta can **ghi loi co ma** de nguoi doc log
    biet *"thieu bang chung"*, chu khong phai mot stack trace.
#>
function Doc-Log {
    param([string]$DuongDan)
    if ([string]::IsNullOrWhiteSpace($DuongDan) -or -not (Test-Path $DuongDan)) { return $null }
    return [IO.File]::ReadAllText((Resolve-Path $DuongDan).Path)
}

<#
    🔒 DONG DAU TIEN KHOP MOT MAU (thay vi dong cuoi) — dung cho cac con so tong ket.
    📌 Vi sao *"dong dau tien"*: mot lan chay co the in mot con so **trung gian** roi moi in tong ket, va
    `Select-String` khong cho biet dong nao la **phien ban cuoi**. O day cac mau deu duoc neo chat
    (`^E2E: `…) nen chi co mot dong khop.
#>
function Tim-Dong {
    param([string]$NoiDung, [string]$Mau)
    if ($null -eq $NoiDung) { return $null }
    $m = [regex]::Match($NoiDung, $Mau, 'Multiline')
    if ($m.Success) { return $m }
    return $null
}

$noiDung = Doc-Log -DuongDan $Log

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# A1 — CO LOG VA CONG DA TUYEN BO "DAT"
# ══════════════════════════════════════════════════════════════════════════════════════════════════
if ($null -eq $noiDung) {
    Them-Loi 'A1' ("khong doc duoc log cua cong: {0}" -f $Log)
}
else {
    # 📌 Dong ket luan cuoi cung **chi in khi moi buoc da xong** (`kiem-tat-ca.ps1`) ⇒ thieu no = co buoc nem.
    if ($noiDung -notmatch 'TAT CA PHEP KIEM DAT') {
        Them-Loi 'A1' 'log KHONG co dong ket luan "TAT CA PHEP KIEM DAT" ⇒ mot buoc da nem / cong khong chay xong'
    }

    if ($MaCong -ne 0) {
        if ($MaCong -eq 2) {
            Them-Loi 'A1' 'ma thoat = 2 ⇒ MOI TRUONG / PHEP KIEM VO NGHIA (KHONG duoc coi la "san pham hong")'
        }
        else {
            Them-Loi 'A1' ("ma thoat = {0} ⇒ cong DO" -f $MaCong)
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# A2 — BUOC ⑧ (E2E) PHAI DA CHAY: dong `E2E: N/N` trong log CUA BUOC, va tep log phai MOI
# ══════════════════════════════════════════════════════════════════════════════════════════════════
$tepE2e = Join-Path $ThuMucLog 'e2e.log'
$e2eNoiDung = Doc-Log -DuongDan $tepE2e
$soE2e = $null

if ($null -eq $e2eNoiDung) {
    Them-Loi 'A2' ("KHONG co log cua buoc ⑧ ({0}) ⇒ buoc E2E CHUA CHAY (thieu -DayDu?)" -f $tepE2e)
}
else {
    # 🚨 **A2 tren log CU la mot lan chay XANH GIA.** Phep kiem phai biet *"tep nay co phai cua lan nay khong"*.
    if ($batDau -gt [datetime]::MinValue) {
        $mtime = (Get-Item $tepE2e).LastWriteTimeUtc
        if ($mtime -lt $batDau.AddSeconds(-10)) {
            Them-Loi 'A2' ("e2e.log la tep CU (mtime {0:o} < bat dau {1:o}) ⇒ KHONG phai bang chung cua lan chay nay" -f $mtime, $batDau)
        }
    }

    $m = Tim-Dong -NoiDung $e2eNoiDung -Mau '(?m)^\s*E2E:\s*(\d+)/(\d+)\s+ca DAT\s*$'

    if ($null -eq $m) {
        Them-Loi 'A2' 'log cua buoc ⑧ KHONG co dong "E2E: N/N ca DAT" ⇒ buoc ⑧ khong chay den cuoi'
    }
    else {
        $dat = [int]$m.Groups[1].Value
        $tong = [int]$m.Groups[2].Value
        $soE2e = "$dat/$tong"

        if ($dat -le 0) { Them-Loi 'A2' 'E2E chay 0 ca ⇒ phep kiem vo nghia' }
        if ($dat -ne $tong) { Them-Loi 'A2' ("E2E chi DAT {0}/{1} ca" -f $dat, $tong) }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# A3 — KHONG BUOC NAO BI BO QUA
# ══════════════════════════════════════════════════════════════════════════════════════════════════
if ($null -ne $noiDung) {
    # 🚨 **PHAI NEO VAO DAU DONG — DO DUOC 21/10/2026 tren log XANH THAT.** Ban dau A3 kiem *"log co chua
    # `[BO QUA]`"*; ⚠️ nhung **ten ca kiem** cua buoc ⑦ co chuoi do (`X4) Log co [BO QUA] ⇒ exit 1`) ⇒ 🔒
    # **mot lan chay XANH cung bi bao DO** — dung loai cong luon-do ma nguoi ta se hoc cach bo qua.
    # ✅ Dong cua cong khi bo that mot buoc luon bat dau bang `── [BO QUA]` (`kiem-tat-ca.ps1`) ⇒ neo theo
    #    **dau dong**. 📌 Day la lan thu **BA** cung mot ho loi trong `S9`: *"phép quét văn bản thô không phân
    #    biệt mã với tên ca / chú thích"* (xem `XC-007` §16.5 **S9-2**).
    $cacDong = @($noiDung -split "`n" | Where-Object { $_ -match '^\s*──\s*\[BO QUA\]' } | Select-Object -First 5)

    if ($cacDong.Count -gt 0) {
        Them-Loi 'A3' ("co buoc bi BO QUA ⇒ khong phai lan chay day du:`n      " + ($cacDong -join "`n      "))
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# A4 — BUOC ② PHAI CHUNG MINH "BUILD DA VAO" (moc nhi phan doi), KHONG tin exit 0
# ══════════════════════════════════════════════════════════════════════════════════════════════════
if ($null -ne $noiDung) {
    # 🚨 Hai chuoi KHAC NHAU o hai tep: `moc-kiem.log` in "MOI tep deu DOI moc"; `kiem-tat-ca.ps1` in lai
    #    "Build DA VAO (moi moc deu doi)". 📌 Khang dinh nham chuoi ⇒ **DO GIA** tren mot lan chay TOT.
    if ($noiDung -notmatch 'Build DA VAO') {
        Them-Loi 'A4' 'log thieu "Build DA VAO" ⇒ khong chung minh duoc nhi phan da doi'
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# A5 — DU SO BUOC (khong buoc nao bi bo im lang)
# ══════════════════════════════════════════════════════════════════════════════════════════════════
$soBuoc = $null

if ($null -ne $noiDung) {
    $mSoBuoc = Tim-Dong -NoiDung $noiDung -Mau '(?m)^TONG SO BUOC DAT:\s*(\d+)\s*$'
    $soXong = ([regex]::Matches($noiDung, '✅ XONG')).Count

    if ($null -eq $mSoBuoc) {
        Them-Loi 'A5' 'log thieu dong "TONG SO BUOC DAT: N" ⇒ khong doc duoc so buoc THUC chay'
    }
    else {
        $soBuoc = [int]$mSoBuoc.Groups[1].Value

        # 🔒 **HAI CON SO PHAI KHOP** — mot ben la *"cong tu dem so buoc da chay"*, mot ben la
        # *"so dong hoan thanh in ra"*. 📌 Lech nhau nghia la mot trong hai phep dem sai ⇒ phai DO.
        if ($soXong -ne $soBuoc) {
            Them-Loi 'A5' ("log co {0} dong '✅ XONG' nhung cong bao {1} buoc ⇒ co buoc khong chay tron ven" -f $soXong, $soBuoc)
        }

        if ($soBuoc -lt $SoBuocToiThieu) {
            Them-Loi 'A5' ("chi chay {0} buoc, san la {1} ⇒ mot buoc da bi XOA khoi cong" -f $soBuoc, $SoBuocToiThieu)
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# A6 — KHONG CO DONG BAO DONG (⚠️ KHONG phai "khong co 🚨": ten ca kiem DUNG 🚨 lam neo)
# ══════════════════════════════════════════════════════════════════════════════════════════════════
if ($null -ne $noiDung) {
    $dongBaoDong = @($noiDung -split "`n" | Where-Object { $_.TrimStart().StartsWith('🚨') } | Select-Object -First 5)

    if ($dongBaoDong.Count -gt 0) {
        $chiTiet = ("log co {0} dong BAT DAU bang 🚨 (ke ca khi exit 0):`n      " -f $dongBaoDong.Count) +
        ($dongBaoDong -join "`n      ")
        Them-Loi 'A6' $chiTiet
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# A7 — CONG ⑨ (tham chieu tai lieu): 0 tham chieu chet
# ══════════════════════════════════════════════════════════════════════════════════════════════════
$linkPass = $null

if (-not [string]::IsNullOrWhiteSpace($LinkLog)) {
    $linkNoiDung = Doc-Log -DuongDan $LinkLog

    if ($null -eq $linkNoiDung) {
        Them-Loi 'A7' ("khong doc duoc log cong ⑨: {0}" -f $LinkLog)
    }
    else {
        if ($linkNoiDung -notmatch 'PASS — 0 tham chieu chet') {
            Them-Loi 'A7' 'log cong ⑨ KHONG co "PASS — 0 tham chieu chet" ⇒ con tham chieu chet (hoac cong khong chay)'
        }
        else {
            $linkPass = $true
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# KET LUAN + BANG CHUNG
# ══════════════════════════════════════════════════════════════════════════════════════════════════
if ($loi.Count -gt 0) {
    Write-Output ''
    $loi | ForEach-Object { Write-Output ("🚨 {0}" -f $_) }
    Write-Output ''
    Write-Output ("CONG CUA CONG: {0} dieu kien KHONG DAT ⇒ 🔒 KHONG duoc coi la xanh." -f $loi.Count)
    exit 1
}

# ── Cac con so dem doc tu log (de nguoi doc thay GIA TRI THAT, khong phai mot dong "OK" suong) ────
function Lay-SoDem {
    param([string]$TenTep, [string]$Mau)
    $n = Doc-Log -DuongDan (Join-Path $ThuMucLog $TenTep)
    $m = Tim-Dong -NoiDung $n -Mau $Mau
    if ($null -eq $m) { return '' }
    return $m.Value.Trim()
}

$demUnit = Lay-SoDem -TenTep 'unit.log' -Mau 'Passed!\s+- Failed:\s+\d+,\s+Passed:\s+\d+'
$demTichHop = Lay-SoDem -TenTep 'integration.log' -Mau 'Passed!\s+- Failed:\s+\d+,\s+Passed:\s+\d+'
$demMermaid = Lay-SoDem -TenTep 'mermaid.log' -Mau 'Tổng:\s*\d+ khối mermaid · lỗi:\s*\d+'
$demDb = Lay-SoDem -TenTep 'db.log' -Mau 'DB:\s*\d+/\d+ bat bien DAT'
$demCong = Lay-SoDem -TenTep 'test-cong.log' -Mau 'TEST CONG KIEM:\s*\d+/\d+ DAT'
$demNnd = Lay-SoDem -TenTep 'db.log' -Mau 'NND thucTe=\d+ · tuTest=\d+'
$demQuyen = Lay-SoDem -TenTep 'db.log' -Mau 'co \d+ quyen'
$demE2e = Lay-SoDem -TenTep 'e2e.log' -Mau 'E2E:\s*\d+/\d+ ca DAT'
$demDiaChi = Lay-SoDem -TenTep 'diachi.log' -Mau 'PROBE SO DIA CHI:\s*\d+/\d+ DAT'
$demKhach = Lay-SoDem -TenTep 'khachhang.log' -Mau 'PROBE KHACH HANG:\s*\d+/\d+ DAT'
$demThanhToan = Lay-SoDem -TenTep 'thanhtoan.log' -Mau 'PROBE THANH TOAN:\s*\d+/\d+ DAT'
$demDonSan = Lay-SoDem -TenTep 'donsan.log' -Mau 'PROBE DON SAN:\s*\d+/\d+ DAT'

$bangDem = [ordered]@{
    '② build (moc nhi phan)' = 'Build DA VAO'
    '③ unit'                 = $demUnit
    '④ integration'          = $demTichHop
    '⑤ mermaid'              = $demMermaid
    '⑥ bat bien DB'          = $demDb
    '⑦ test cho cac cong'    = $demCong
    '⑧ E2E'                  = $demE2e
    '⑨ so dia chi'           = $demDiaChi
    '⑩ khach hang'           = $demKhach
    '⑪ thanh toan'           = $demThanhToan
    '⑫ don san'              = $demDonSan
}

Write-Output ''
Write-Output '✅ CONG CUA CONG: DU BANG CHUNG (A1…A6).'
Write-Output ("   Buoc DAT: {0}" -f $soBuoc)
foreach ($k in $bangDem.Keys) {
    Write-Output ("   {0,-24} {1}" -f $k, $bangDem[$k])
}

if ($null -ne $linkPass) { Write-Output '   ⑨ tham chieu tai lieu   PASS — 0 tham chieu chet' }

# ── Evidence.json (`XC-007` §5.2) — 📌 chi ghi thu DO DUOC; thu khong do duoc thi ghi `null` + ly do ──
if (-not [string]::IsNullOrWhiteSpace($XuatBangChung)) {
    $tepCong = @(
        'kiem-tat-ca.ps1', 'kiem-moc-build.ps1', 'kiem-db.ps1', 'kiem-e2e.ps1',
        'test-cong-kiem.ps1', 'kiem-link-tai-lieu.ps1', 'nen-tang.ps1', 'ham-tiem.ps1',
        'ci-khang-dinh.ps1'
    ) | Where-Object { Test-Path (Join-Path $PSScriptRoot $_) }

    $bamScript = [ordered]@{}
    foreach ($t in $tepCong) {
        $bamScript[$t] = (Get-FileHash (Join-Path $PSScriptRoot $t) -Algorithm SHA256).Hash.ToLowerInvariant()
    }

    $tepLog = @(Get-ChildItem $ThuMucLog -File -Filter *.log -ErrorAction SilentlyContinue |
            Sort-Object Name | ForEach-Object {
                [ordered]@{
                    fileName = $_.Name
                    sha256   = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                    sizeBytes = $_.Length
                }
            })

    # 📌 `git log -1 -- tools/` = commit da sinh ra ket qua nay (BR-007: cong bi doi thi phai thay).
    $gateVersionSha = $null
    try { $gateVersionSha = (& git -C $goc log -1 --format=%H -- tools/ 2>$null) } catch { }

    $sdk = $null
    try { $sdk = (& dotnet --version 2>$null) } catch { }

    # 📌 `try/catch` KHONG phai bieu thuc trong PowerShell ⇒ phai tinh truoc roi moi dua vao hashtable.
    $node = $null
    try { $node = (& node --version 2>$null) } catch { }

    $maHead = $null
    try { $maHead = (& git -C $goc rev-parse HEAD 2>$null) } catch { }

    $nhanh = $null
    try { $nhanh = (& git -C $goc rev-parse --abbrev-ref HEAD 2>$null) } catch { }

    $schemaPath = Join-Path $goc 'test/eCommerce.IntegrationTests/Assets/schema.sql'
    $schemaSha = if (Test-Path $schemaPath) { (Get-FileHash $schemaPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }

    # 📌 "Digest artifact": sha256 cua hai DLL ma tien trinh THAT nap (`XC-007` D6) — do duoc, khong suy doan.
    $bamNhiPhan = [ordered]@{}
    foreach ($dll in @(
            'src/eCommerce.Web/bin/Debug/net10.0/eCommerce.Web.dll',
            'src/eCommerce.HttpApi.Host/bin/Debug/net10.0/eCommerce.HttpApi.Host.dll')) {
        $p = Join-Path $goc $dll
        if (Test-Path $p) { $bamNhiPhan[$dll] = (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant() }
    }

    $bangChung = [ordered]@{
        commitSha         = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { $maHead }
        runId             = if ($env:GITHUB_RUN_ID) { $env:GITHUB_RUN_ID } else { $null }
        runAttempt        = if ($env:GITHUB_RUN_ATTEMPT) { [int]$env:GITHUB_RUN_ATTEMPT } else { $null }
        ref               = if ($env:GITHUB_REF) { $env:GITHUB_REF } else { $nhanh }
        lyDo              = $env:LY_DO_CHAY_TAY   # workflow_dispatch: BR-015
        runnerOs          = if ($env:RUNNER_OS) { $env:RUNNER_OS } else { [System.Runtime.InteropServices.RuntimeInformation]::OSDescription }
        dotNetSdk         = $sdk
        nodeVersion       = $node
        gateVersionSha    = $gateVersionSha
        verdict           = 'Passed'
        soBuocDat         = $soBuoc
        soBuocToiThieu    = $SoBuocToiThieu
        e2e               = $demE2e
        nndThucTeVaTuTest = $demNnd
        quyen             = $demQuyen
        soDem             = $bangDem
        scriptSha256      = $bamScript
        logs              = $tepLog
        artifactSha256    = $bamNhiPhan
        schemaSqlSha256   = $schemaSha
    }

    $bangChung | ConvertTo-Json -Depth 8 | Set-Content -Path $XuatBangChung -Encoding utf8
    Write-Output ("   Bang chung: {0}" -f $XuatBangChung)
}

exit 0
