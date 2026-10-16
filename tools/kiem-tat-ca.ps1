<#
.SYNOPSIS
    🚨 CHAY TAT CA PHEP KIEM bang MOT lenh — va THAT BAI NGAY khi mot buoc hong.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VI SAO SCRIPT NAY TON TAI:

    Truoc no, **moi vong lam viec** phai chay **nam buoc bang tay**: build · unit test · integration
    test · mermaid · (va kiem moc DLL). ⚠️ Va moi buoc deu co **bay rieng** da can nguoi:

      • **`dotnet build ... | Select-String`** ⇒ 🔒 **CHE MAT exit code** (`$LASTEXITCODE` khi do la cua
        `Select-String`, **luon 0**) ⇒ *"build 0 loi"* co the la **SAI** (bai hoc `web-001` §97.6);
      • **`dotnet build` `exit 0` ma binary KHONG DOI** (tien trinh dang giu tep) ⇒ 🔒 test do vi **LY DO SAI**
        ⇒ ket luan sai ve **SAN PHAM** (bai hoc §97.6 · §98.6 · `sal-006` §21.6 · §22.7 — **BON LAN**);
      • **quen mot buoc** ⇒ 📌 *"tat ca xanh"* nhung thuc ra **chua chay** buoc do — va **khong ai** biet.

    ✅ Script nay gom lai: **mot lenh, nam buoc, exit code that, dung ngay khi hong.**
    📌 Va no la **nen cua CI**: khi nao them CI thi chi can goi script nay.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔒 **HAI QUY TAC DO CHINH SCRIPT NAY THI HANH (khong phai loi khuyen):**

    **① KHONG BAO GIO ong danh output cua lenh ma ta can exit code.** Moi lenh ghi ra **tep tam**, roi
    doc tep do ⇒ ✅ `$LASTEXITCODE` luon la cua **chinh lenh**.

    **② BUILD XONG PHAI KIEM MOC NHI PHAN** (`tools\kiem-moc-build.ps1`) ⇒ 🚨 `exit 1` nghia la
    **ban build KHONG VAO** ⇒ 🔒 **DUNG** lai ngay, **khong** chay test (test tren binary cu se do vi
    **LY DO SAI** va lam nguoi sua **ket luan sai ve san pham**).

.PARAMETER BoQuaIntegration
    Bo qua buoc integration test (📌 dung khi can vong lap nhanh; ⚠️ **KHONG** dung truoc khi commit).

.PARAMETER BoQuaE2E
    Bo qua buoc probe co phien (`web-001` §97). 📌 Mac dinh **cung** bo qua khi khong co `-DayDu`
    vi buoc nay **khoi dong Web** (cham, va can cong 5100 trong).

.PARAMETER DayDu
    Chay **ca** buoc E2E (khoi dong Web + dang nhap that). ⚠️ Cham hon nhieu.

.EXAMPLE
    # Vong lap nhanh trong luc sua
    pwsh -File tools\kiem-tat-ca.ps1 -BoQuaIntegration

    # Truoc khi commit (mac dinh: build + moc + unit + integration + mermaid)
    pwsh -File tools\kiem-tat-ca.ps1

    # Day du (them E2E co phien)
    pwsh -File tools\kiem-tat-ca.ps1 -DayDu
#>
[CmdletBinding()]
param(
    [switch]$BoQuaIntegration,
    [switch]$BoQuaE2E,
    [switch]$DayDu
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $goc 'tools\_tam-kiem'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

$ketQua = [System.Collections.Generic.List[object]]::new()
$dongHo = [System.Diagnostics.Stopwatch]::StartNew()

function Buoc {
    param([string]$Ten, [scriptblock]$Than)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Output ""
    Write-Output ("── [{0}] {1}" -f ($ketQua.Count + 1), $Ten)
    Write-Output ("   {0}" -f ('─' * 70))

    & $Than

    $sw.Stop()
    $ketQua.Add([pscustomobject]@{ Ten = $Ten; Giay = [Math]::Round($sw.Elapsed.TotalSeconds, 1) })
    Write-Output ("   ✅ XONG ({0}s)" -f [Math]::Round($sw.Elapsed.TotalSeconds, 1))
}

<#
    🔒 **HAM CHAY LENH KHONG ONG DANH** — tra ve `exit code` THAT.
    📌 `[ref]` cho output vi PowerShell khong cho tra hai gia tri bang mot `return`.
#>
function ChayLenh {
    param([string]$Tep, [string[]]$ThamSo, [string]$TenTepLog)

    $duongLog = Join-Path $thuMucTam $TenTepLog

    # ⚠️ `*>` (khong phai `|`) ghi TAT CA luong ra tep ⇒ khong co ong danh nao che exit code.
    & $Tep @ThamSo *> $duongLog
    $ma = $LASTEXITCODE

    return [pscustomobject]@{ Ma = $ma; Log = $duongLog }
}

# ── BUOC 0: DON TIEN TRINH CU (chong bay "binary khong doi") ────────────────────────────────
Buoc 'Don tien trinh eCommerce cu + cho cong TRONG' {
    # 🚨 PHAI lam TRUOC khi build: mot tien trinh dang chay **giu tep** ⇒ build ghi hut ma van `exit 0`.
    # 📌 Day chinh la nguyen nhan cua **BON LAN** tra gia (xem `.DESCRIPTION`).
    $cacTienTrinh = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like '*eCommerce*' })

    Write-Output ("   Tim thay {0} tien trinh eCommerce dang chay." -f $cacTienTrinh.Count)

    foreach ($p in $cacTienTrinh) {
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    }

    if ($cacTienTrinh.Count -gt 0) {
        Write-Output "   Da dung. Cho cong 5000/5100 TRONG..."
        $hetHan = (Get-Date).AddSeconds(60)

        while ((Get-Date) -lt $hetHan) {
            $biChiem = @()
            foreach ($cong in @(5000, 5100)) {
                $biChiem += @(Get-NetTCPConnection -LocalPort $cong -State Listen -ErrorAction SilentlyContinue)
            }

            if ($biChiem.Count -eq 0) { break }
            Start-Sleep -Milliseconds 500
        }
    }

    foreach ($c in @(5000, 5100)) {
        if (Get-NetTCPConnection -LocalPort $c -State Listen -ErrorAction SilentlyContinue) {
            Write-Output ("   🚨 CONG {0} VAN BI CHIEM ⇒ build se ghi hut (binary khong doi)." -f $c)
            throw "Cong $c bi chiem."
        }
    }

    Write-Output "   ✅ Cong 5000/5100 TRONG, khong con tien trinh eCommerce."
}

# ── BUOC 1: GHI NHO MOC + BUILD + KIEM MOC ─────────────────────────────────────────────────
Buoc 'Build (va KIEM MOC nhi phan)' {
    # ① Ghi nho moc TRUOC khi build — 🔑 bat buoc, neu khong thi buoc kiem moc la VO NGHIA.
    $g = ChayLenh 'pwsh' @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'kiem-moc-build.ps1'),
        '-GhiNho', '-Ten', 'eCommerce.*.dll') 'moc-ghi.log'

    if ($g.Ma -ne 0) {
        Get-Content $g.Log | Select-Object -Last 5 | ForEach-Object { "   $_" }
        throw "Ghi nho moc THAT BAI (exit $($g.Ma))."
    }

    # ② Build. ⚠️ `--no-incremental` + `/nodeReuse:false` de khong co cache nao che viec ghi hut.
    $b = ChayLenh 'dotnet' @('build', (Join-Path $goc 'eCommerce.slnx'), '-v', 'm', '--no-incremental',
        '/nodeReuse:false') 'build.log'

    $soLoi = @(Select-String -Path $b.Log -Pattern ': error ' -SimpleMatch -ErrorAction SilentlyContinue).Count
    Write-Output ("   dotnet build exit={0} · so dong loi={1}" -f $b.Ma, $soLoi)

    if ($b.Ma -ne 0 -or $soLoi -gt 0) {
        # 🔑 In **dong loi** ra (khong chi so dem) — neu khong thi nguoi doc phai mo tep log.
        Select-String -Path $b.Log -Pattern ': error ' -SimpleMatch |
            Select-Object -Unique -First 10 | ForEach-Object { "   $($_.Line.Trim())" }
        throw "BUILD HONG (exit $($b.Ma), $soLoi dong loi)."
    }

    # ③ 🚨 KIEM MOC — `exit 1` nghia la **ban build KHONG VAO**.
    $k = ChayLenh 'pwsh' @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'kiem-moc-build.ps1'),
        '-Kiem', '-Ten', 'eCommerce.*.dll') 'moc-kiem.log'

    Get-Content $k.Log | Where-Object { $_ -match 'KIEM MOC|✅|🚨|KHONG DOI' } |
        Select-Object -First 6 | ForEach-Object { "   $_" }

    if ($k.Ma -eq 1) {
        # 🔒 **DUNG NGAY, KHONG chay test.** Test tren binary cu se do vi **LY DO SAI** ⇒ nguoi sua
        # **ket luan sai ve san pham** — dung bay da can **BON LAN**.
        Get-Content $k.Log | Select-Object -Last 12 | ForEach-Object { "   $_" }
        throw "BAN BUILD KHONG VAO (moc nhi phan khong doi). DUNG: khong chay test tren binary cu."
    }

    if ($k.Ma -ne 0) {
        Get-Content $k.Log | Select-Object -Last 5 | ForEach-Object { "   $_" }
        throw "Phep kiem moc VO NGHIA (exit $($k.Ma)) — kiem lai loc tep."
    }

    Write-Output "   ✅ Build DA VAO (moi moc deu doi)."
}

# ── BUOC 2: UNIT TEST ──────────────────────────────────────────────────────────────────────
Buoc 'Unit test (eCommerce.Tests)' {
    $t = ChayLenh 'dotnet' @('test', (Join-Path $goc 'test\eCommerce.Tests\eCommerce.Tests.csproj'),
        '--nologo', '--no-build') 'unit.log'

    $dong = Select-String -Path $t.Log -Pattern 'Passed!|Failed!' | Select-Object -First 1
    if ($dong) { Write-Output ("   {0}" -f $dong.Line.Trim()) }

    if ($t.Ma -ne 0) {
        Select-String -Path $t.Log -Pattern 'Failed |Error Message' -Context 0,3 |
            Select-Object -First 8 | ForEach-Object { "   $($_.Line.Trim())" }
        throw "UNIT TEST DO (exit $($t.Ma))."
    }
}

# ── BUOC 3: INTEGRATION TEST ───────────────────────────────────────────────────────────────
if (-not $BoQuaIntegration) {
    Buoc 'Integration test (eCommerce.IntegrationTests)' {
        $t = ChayLenh 'dotnet' @('test', (Join-Path $goc 'test\eCommerce.IntegrationTests\eCommerce.IntegrationTests.csproj'),
            '--nologo') 'integration.log'

        $dong = Select-String -Path $t.Log -Pattern 'Passed!|Failed!' | Select-Object -First 1
        if ($dong) { Write-Output ("   {0}" -f $dong.Line.Trim()) }

        if ($t.Ma -ne 0) {
            # 📌 Integration test dung **Testcontainers** ⇒ neu Docker khong chay thi loi o DAY, va phai
            # **noi ro** vi do la **moi truong**, khong phai **ma nguon**.
            Select-String -Path $t.Log -Pattern 'Failed |Error Message|Docker|container' -Context 0,2 |
                Select-Object -First 6 | ForEach-Object { "   $($_.Line.Trim())" }
            throw "INTEGRATION TEST DO (exit $($t.Ma)) — kiem Docker/Testcontainers truoc khi ket luan ve ma nguon."
        }
    }
}
else {
    Write-Output ""
    Write-Output '── [BO QUA] Integration test (theo -BoQuaIntegration)'
}

# ── BUOC 4: MERMAID ────────────────────────────────────────────────────────────────────────
Buoc 'Tai lieu: kiem khoi mermaid' {
    Push-Location (Join-Path $goc 'tools')
    try {
        $m = ChayLenh 'npm' @('run', 'check:mermaid') 'mermaid.log'
    }
    finally {
        Pop-Location
    }

    Get-Content $m.Log | Select-Object -Last 2 | ForEach-Object { "   $_" }

    if ($m.Ma -ne 0) { throw "KIEM MERMAID DO (exit $($m.Ma))." }
}

# ── BUOC 5: BAT BIEN DB ────────────────────────────────────────────────────────────────────
Buoc 'Bat bien DB (khong con dau vet thu nghiem)' {
    # 🔑 **BUOC NAY TON TAI VI "QUEN KIEM DB" LA BUOC DE QUEN NHAT.** Moi vong lam viec toi van kiem DB
    # **bang tay** (5-6 cau `psql`) truoc khi commit — ⚠️ va quen no thi *"tat ca xanh"* nhung **du lieu thu
    # nghiem con nam trong DB** ⇒ 📌 lan sau mot phep do se **doc phai chung** roi **ket luan sai**.
    # 🚨 **VA NO LA BUOC DUY NHAT CHAM VAO TRANG THAI THAT** (khong phai ma nguon): mermaid kiem **tai lieu**,
    # test kiem **ma**, con buoc nay kiem **cai dang nam trong database**.
    $d = ChayLenh 'pwsh' @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'kiem-db.ps1')) 'db.log'

    # 📌 In **cac dong ket qua** (co neo ✅/🚨/DB:) thay vi ca tep — 🔑 de nguoi doc thay **con so THAT**,
    # khong phai mot dong *"OK"* suong.
    Get-Content $d.Log | Where-Object { $_ -match '✅|🚨|DB:|📌' } |
        Select-Object -First 26 | ForEach-Object { "   $_" }

    if ($d.Ma -eq 2) {
        # 🔒 Ma 2 = **khong ket noi duoc / phep kiem VO NGHIA** ⇒ ⚠️ **KHONG** duoc coi la *"DB sach"*.
        # 📌 Phan biet hai ma nay la bai hoc cua `kiem-moc-build.ps1` (⚠️ `Write-Error` ep exit 1 lam **lan**
        # hai nguyen nhan **khac nhau** ⇒ nguoi doc log **khong phan biet duoc** va se **sua sai cho**).
        Get-Content $d.Log | Select-Object -Last 6 | ForEach-Object { "   $_" }
        throw 'KIEM DB VO NGHIA (khong ket noi duoc PostgreSQL?) — kiem Docker/container truoc khi ket luan.'
    }

    if ($d.Ma -ne 0) {
        Get-Content $d.Log | Select-String 'HONG' | Select-Object -First 8 | ForEach-Object { "   $($_.Line.Trim())" }
        throw "BAT BIEN DB HONG (exit $($d.Ma)) — xem log: $($d.Log)"
    }
}

# ── BUOC 6 (tuy chon): E2E CO PHIEN ────────────────────────────────────────────────────────
if ($DayDu -and -not $BoQuaE2E) {
    Buoc 'E2E co phien dang nhap (web-001 §97)' {
        $script = Join-Path $thuMucTam 'e2e.ps1'

        if (-not (Test-Path $script)) {
            # ⚠️ **KHONG** chep lai script E2E o day: no dai va da co noi khac. 📌 Thay vao do, **noi ro**
            # la thieu — 🔑 mot buoc "im lang bo qua" nguy hiem hon mot buoc **bao loi**.
            throw ("Chua co script E2E o {0}.`n" -f $script +
                "   📌 Script do la probe co phien (form login + cookie) — xem web-001 muc 97.2 va tools\README-probe.md.`n" +
                "   🔒 NO DA GHI: probe E2E van la script tay, chua dua vao day.")
        }

        $e = ChayLenh 'pwsh' @('-NoProfile', '-File', $script) 'e2e.log'
        Get-Content $e.Log | Select-Object -Last 12 | ForEach-Object { "   $_" }

        if ($e.Ma -ne 0) { throw "E2E DO (exit $($e.Ma))." }
    }
}
elseif (-not $BoQuaE2E -and -not $DayDu) {
    Write-Output ""
    Write-Output '── [BO QUA] E2E co phien (them -DayDu de chay; ⚠️ no la SCRIPT TAY, xem `web-001` §98.7 #1)'
}

# ── TONG KET ───────────────────────────────────────────────────────────────────────────────
$dongHo.Stop()

Write-Output ""
Write-Output ("════════ TAT CA PHEP KIEM DAT ({0}s) ════════" -f [Math]::Round($dongHo.Elapsed.TotalSeconds, 1))

foreach ($k in $ketQua) {
    Write-Output ("   ✅ {0,-52} {1,6}s" -f $k.Ten, $k.Giay)
}

if ($BoQuaIntegration) {
    Write-Output ""
    Write-Output '⚠️ DA BO QUA integration test ⇒ 🔒 DUNG commit voi ket qua nay.'
}

Write-Output ""
Write-Output "Log day du: $thuMucTam"

exit 0
