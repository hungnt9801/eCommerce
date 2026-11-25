<#
.SYNOPSIS
    🚨 TEST CHO CHINH CAC SCRIPT KIEM CHUNG — `kiem-moc-build.ps1` (va cac script cung khuon).

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VI SAO FILE NAY TON TAI:

    Cac script trong `tools\` la **cong chan** cho moi vong lam viec — 📌 nhung chung **tu chung** chi duoc
    kiem **BANG TAY**, va **chi khi toi tinh co tiem dung ca**.

    🚨 **HAI LOI THAT DA XAY RA — VA CA HAI DEU TIM RA NHO MAY MAN:**

      • `sal-006` **§23.4** — `kiem-moc-build.ps1` bi **VIET NGUOC DIEU KIEN**: no bao *"BAN BUILD KHONG VAO"*
        **ngay ca khi moi tep DEU DOI** ⇒ 🔒 **cong LUON DO**. ⚠️ Phat hien duoc vi toi **tinh co** chay no
        tren mot ban build **vua xong**;
      • `sal-006` **§25.5** — phep tiem loi **KHONG he xay ra** (sai cu phap SQL) ⇒ cong bao **XANH** ⇒
        📌 suyt ket luan *"co che khong hoat dong"*. ⚠️ Phat hien duoc vi toi **tinh co** do lai trang thai that.

    🔒 **Va mot cong LUON DO thi nguoi ta hoc cach BO QUA no** ⇒ hong no nghia la **quay lai** dung cai bay
    ma no sinh ra de chong. ⇒ **Phai co test TU DONG, khong phu thuoc vao may man.**

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔒 **HAI QUY TAC THIET KE CUA FILE NAY:**

    **① KHONG THEM PHU THUOC.** 📌 May nay chi co **Pester 3.4.0** (cu, API khac Pester 5), va ca toolchain
    trong `tools\` la **script thuan + exit code**. ✅ Nen file nay cung the: **`Describe`/`It` tu viet**,
    khong `Import-Module` nao. 🔑 Them mot framework o day la doi **cong cu** — khong phai them **gia tri**.

    **② MOI CA PHAI TU DON DEP — va test PHAI CHAY DUOC TREN DB SACH.** 📌 Cac ca "tiem" tao du lieu that;
    ⚠️ neu mot ca **nem giua duong** ma khong don thi lan chay sau se **hong** vi du lieu ca truoc con lai
    ⇒ 🔑 mot test **tu lam hong chinh no**. ✅ Dung `try/finally` cho **moi** ca co side effect.

.PARAMETER ChiKiemMoc
    Chi chay nhom ca cho `kiem-moc-build.ps1` (📌 nhanh, khong cham vao DB).

.PARAMETER ChiKiemDb
    Chi chay nhom ca cho `kiem-db.ps1` (⚠️ **CAN** Docker + PostgreSQL dang chay).

.EXAMPLE
    pwsh -File tools\test-cong-kiem.ps1
    pwsh -File tools\test-cong-kiem.ps1 -ChiKiemMoc
#>
[CmdletBinding()]
param(
    [switch]$ChiKiemMoc,
    [switch]$ChiKiemDb
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $PSScriptRoot '_tam-test'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

$ketQua = [System.Collections.Generic.List[object]]::new()
$nhomHienTai = ''

function Describe {
    param([string]$Ten, [scriptblock]$Than)
    $script:nhomHienTai = $Ten
    Write-Output ''
    Write-Output ("── {0} ──" -f $Ten)
    & $Than
}

function It {
    param([string]$Ten, [scriptblock]$Than)

    try {
        & $Than
        $ketQua.Add([pscustomobject]@{ Nhom = $script:nhomHienTai; Ten = $Ten; Dat = $true; Loi = '' })
        Write-Output ("   ✅ {0}" -f $Ten)
    }
    catch {
        $ketQua.Add([pscustomobject]@{ Nhom = $script:nhomHienTai; Ten = $Ten; Dat = $false; Loi = $_.Exception.Message })
        Write-Output ("   🚨 {0}" -f $Ten)
        Write-Output ("      {0}" -f $_.Exception.Message)
    }
}

<#
    🔒 **HAM SO SANH — KHONG DUNG `Should` CUA PESTER** (Pester 3.4.0 co API khac Pester 5, ⚠️ va viet theo
    Pester 5 se **khong chay** o day). 📌 Mot ham nho, ro rang, va **khong phu thuoc phien ban**.
#>
function KhangDinh {
    param([bool]$DieuKien, [string]$ThongDiep)
    if (-not $DieuKien) { throw $ThongDiep }
}

function BangNhau {
    param($ThucTe, $MongDoi, [string]$Nhan)
    if ($ThucTe -ne $MongDoi) {
        throw ("{0}: mong doi [{1}] nhung do duoc [{2}]" -f $Nhan, $MongDoi, $ThucTe)
    }
}

<#
    🔒 **GOI SCRIPT CON VA LAY EXIT CODE THAT** — dung `*>` ghi ra tep tam, **KHONG** ong danh.
    📌 Cung quy tac nhu `kiem-tat-ca.ps1` (bai hoc: `dotnet build ... | Select-String` 🔒 **CHE MAT exit code**).
#>
function ChayScript {
    param([string]$Tep, [string[]]$ThamSo)

    $log = Join-Path $thuMucTam ('log-' + [Guid]::NewGuid().ToString('N') + '.txt')
    & pwsh -NoProfile -File $Tep @ThamSo *> $log
    $ma = $LASTEXITCODE

    return [pscustomobject]@{ Ma = $ma; Log = $log }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════
# NHOM 1 — `kiem-moc-build.ps1` (📌 nhanh, khong cham DB)
# ══════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemDb) {
    $scriptMoc = Join-Path $PSScriptRoot 'kiem-moc-build.ps1'
    $tepMoc = Join-Path $goc '.moc-build.json'

    # 🔒 **Sao luu moc cu (neu co) va KHOI PHUC o cuoi** — ⚠️ neu khong thi bo test nay **pha** trang thai
    # cua nguoi dang lam viec (📌 va mot test lam hong moi truong la test se bi bo qua).
    $mocCu = if (Test-Path $tepMoc) { Get-Content $tepMoc -Raw } else { $null }

    Describe 'kiem-moc-build.ps1 — cong chong "build exit 0 ma binary KHONG DOI"' {

        It 'A) GhiNho → BUILD → Kiem ⇒ MOI tep DOI ⇒ exit 0 (🛡️ chong cong LUON DO)' {
            # ══════════════════════════════════════════════════════════════════════════════════════
            # 🔑 **DAY LA CA QUAN TRONG NHAT** — va no phai theo **DUNG TRINH TU BAY BUOC** cua cong:
            #   ① `-GhiNho` (ghi moc HIEN TAI)  →  ② **build**  →  ③ `-Kiem` (phai thay moc DOI).
            #
            # 🚨 **VI SAO KHONG DUOC BO BUOC ②:** ban dau toi viet ca nay la *"GhiNho roi Kiem NGAY"* va
            # **ky vong exit 0** — ⚠️ **SAI**: `-GhiNho` ghi moc **sau** ban build truoc do, nen moc **khop**
            # ⇒ 🔑 `exit 1` moi la **DUNG** (noi dung: *"ban build vua roi KHONG lam binary doi"*).
            # 📌 Va do chinh la **§23.4** — nhung lan nay **phep kiem DUNG**, chi **ky vong cua toi SAI**.
            # 🔒 Bai hoc: mot ca do phai duoc **kiem lai ky vong TRUOC khi sua cong** (bai hoc `web-001` §97).
            # ══════════════════════════════════════════════════════════════════════════════════════

            $g = ChayScript $scriptMoc @('-GhiNho', '-Ten', 'eCommerce.Application.dll')
            BangNhau $g.Ma 0 'Buoc ① GhiNho'

            # ② Build **that** ⇒ 🔑 day la buoc lam moc DOI.
            # 🚨 **PHAI `--no-incremental`** — ⚠️ va day la mot phat hien **DO DUOC** khi viet ca nay:
            # ban dau toi de build **tang dan**, va no **khong ghi lai** binary nao (moi thu da len-to-date)
            # ⇒ moc **KHONG doi** ⇒ 🔑 `-Kiem` tra **exit 1 la DUNG**, ⚠️ nhung ca kiem cua toi **that bai**.
            # 📌 Nghia la: mot phep kiem *"moc phai doi sau build"* **khong tat dinh** voi build tang dan —
            # 🔒 va mot ca **khong tat dinh** thi **khong phai luoi an toan** (no do ngau nhien).
            # ✅ `--no-incremental` buoc MSBuild **ghi lai** moi dich ⇒ moc **luon** doi ⇒ ca kiem **tat dinh**.
            # ⚠️ Doi lai: cham hon (~40s). 📌 Nhung day la **test cua cong**, va cong do lai chay **moi vong**
            # ⇒ 🔑 tra gia thoi gian o **mot cho** de biet chac cong **con dung**.
            $buildLog = Join-Path $thuMucTam 'build-a.log'
            & dotnet build (Join-Path $goc 'eCommerce.slnx') -v q --no-incremental /nodeReuse:false *> $buildLog
            $buildMa = $LASTEXITCODE

            BangNhau $buildMa 0 ("Buoc ② build --no-incremental (xem {0})" -f $buildLog)

            # ③ Kiem ⇒ moc **PHAI** doi ⇒ exit 0.
            $k = ChayScript $scriptMoc @('-Kiem', '-Ten', 'eCommerce.Application.dll')
            BangNhau $k.Ma 0 'Buoc ③ Kiem khi moc DOI'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'DA VAO') "Thieu ket luan 'DA VAO'. Log:`n$noiDung"
            KhangDinh (-not ($noiDung -match 'KHONG DOI')) "🚨 BAO DONG SAI (cong LUON DO): moc doi ma bao 'KHONG DOI'. Log:`n$noiDung"
        }

        It 'A2) GhiNho → KHONG build → Kiem ⇒ moc KHONG doi ⇒ exit 1 (🛡️ chong cong LUON XANH)' {
            # 🔒 **CA DOI XUNG CUA A** — va no khoa **chieu con lai**: neu cong **khong bao dong** khi moc
            # khong doi thi no **vo dung** (📌 day chinh la ca ma §23.4 lam hong theo huong nguoc lai).
            # ⚠️ Chay ngay sau A ⇒ binary vua build xong ⇒ `-GhiNho` ghi moc **MOI** ⇒ ⚠️ **khong** build giua
            # hai buoc ⇒ moc **khop** ⇒ 🔑 `exit 1` moi la **DUNG**.
            $g = ChayScript $scriptMoc @('-GhiNho', '-Ten', 'eCommerce.Application.dll')
            BangNhau $g.Ma 0 'GhiNho'

            $k = ChayScript $scriptMoc @('-Kiem', '-Ten', 'eCommerce.Application.dll')
            BangNhau $k.Ma 1 'Kiem khi moc KHONG doi'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'KHONG DOI') "Thieu canh bao 'KHONG DOI'. Log:`n$noiDung"
            KhangDinh ($noiDung -match 'DUNG ket luan gi ve SAN PHAM') "Thieu loi nhac 'DUNG ket luan gi ve SAN PHAM'. Log:`n$noiDung"
        }

        It 'B) Moc CU (lui ve nam 2020) ⇒ tep DOI ⇒ exit 0, KHONG bao dong' {
            $g = ChayScript $scriptMoc @('-GhiNho', '-Ten', 'eCommerce.Application.dll')
            BangNhau $g.Ma 0 'GhiNho'

            # 📌 Sua moc thanh gia tri CU 2020 — 🔑 dung **so tick** (khong phai chuoi ISO): 📌 `ConvertFrom-Json`
            # TU DOI KIEU chuoi ISO thanh `[datetime]`, va `[datetime] -eq [string]` tra **False** ⇒ hai kieu
            # so sanh cho **hai ket qua khac nhau** (bai hoc `sal-006` §25.5).
            $m = @(Get-Content $tepMoc -Raw | ConvertFrom-Json)
            foreach ($x in $m) { $x.Moc = 1 }
            $m | ConvertTo-Json -Depth 3 | Set-Content $tepMoc -Encoding utf8

            $k = ChayScript $scriptMoc @('-Kiem', '-Ten', 'eCommerce.Application.dll')
            BangNhau $k.Ma 0 'Kiem khi moc CU'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'DA VAO') "Thieu 'DA VAO'. Log:`n$noiDung"
        }

        It 'C) Moc RONG ⇒ phep kiem VO NGHIA ⇒ exit 2 (⚠️ KHONG duoc coi la "DA VAO")' {
            # 🔒 **Ma 2 KHAC ma 1** — bai hoc §23.5 #4 (`Write-Error` ep exit 1 lam **lan** hai nguyen nhan
            # khac nhau ⇒ nguoi doc log **khong phan biet duoc** va se **sua sai cho**).
            '[]' | Set-Content $tepMoc -Encoding utf8

            $k = ChayScript $scriptMoc @('-Kiem', '-Ten', 'eCommerce.Application.dll')
            BangNhau $k.Ma 2 'Kiem khi moc RONG'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'VO NGHIA') "Thieu 'VO NGHIA'. Log:`n$noiDung"
        }

        It 'D) Moc SAI DINH DANG (object don, thieu truong) ⇒ exit 2, KHONG "moi tep deu MOI"' {
            # 🚨 **CA NAY khoa dung mot bay THAT** (§23.4): neu tep moc chua **MOT OBJECT** thay vi **MANG**,
            # `foreach` duyet **THUOC TINH** chu khong duyet phan tu ⇒ bang tra cuu **RONG** ⇒ 🔑 **MOI** tep
            # roi vao nhanh *"MOI"* ⇒ va buoc kiem **XANH du binary KHONG DOI**.
            '{ "Ten": "khong-co-truong-Khoa" }' | Set-Content $tepMoc -Encoding utf8

            $k = ChayScript $scriptMoc @('-Kiem', '-Ten', 'eCommerce.Application.dll')
            BangNhau $k.Ma 2 'Kiem khi moc SAI DINH DANG'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'VO NGHIA') "Thieu 'VO NGHIA'. Log:`n$noiDung"
        }

        It 'E) Chua co moc ⇒ exit 2 (⚠️ KHONG duoc coi la "DA VAO")' {
            Remove-Item $tepMoc -Force -ErrorAction SilentlyContinue

            $k = ChayScript $scriptMoc @('-Kiem', '-Ten', 'eCommerce.Application.dll')
            BangNhau $k.Ma 2 'Kiem khi CHUA co moc'
        }

        It 'F) Loc RONG (khong tep nao khop) ⇒ exit 2, KHONG "0 tep ⇒ DA VAO"' {
            # 🛡️ LUOI AN TOAN CHO CHINH PHEP KIEM: 🔑 *"khong tim thay tep nao"* **KHONG** duoc coi la
            # *"moi tep deu doi"* — 📌 neu khong thi mot glob sai se lam cong **XANH** mai mai.
            & pwsh -NoProfile -File $scriptMoc -GhiNho -Ten 'eCommerce.Application.dll' *> $null

            $k = ChayScript $scriptMoc @('-Kiem', '-Ten', 'KhongCoTenNay*.dll')
            BangNhau $k.Ma 2 'Kiem khi loc RONG'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'VO NGHIA') "Thieu 'VO NGHIA'. Log:`n$noiDung"
        }
    }

    # 🔒 **KHOI PHUC** moc cua nguoi dung — ⚠️ `finally` khong dung duoc o day (khong co try bao ngoai),
    # nen khoi phuc **ngay sau** nhom ca (📌 moi ca da tu don dep phan cua no).
    if ($null -ne $mocCu) {
        # ⚠️ `Set-Content -NoNewline` de **khong** them ky tu xuong dong ⇒ noi dung y nguyen.
        [System.IO.File]::WriteAllText($tepMoc, $mocCu)
    }
    else {
        Remove-Item $tepMoc -Force -ErrorAction SilentlyContinue
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════
# NHOM 1b — `kiem-link-tai-lieu.ps1` (📌 nhanh, KHONG cham DB)
#
# 🔑 VI SAO CO NHOM NAY: ngay 17/10/2026 tai lieu duoc to chuc lai thanh **mot thu muc cho moi module**
# (`docs/features/<module>/`). 📌 Moi duong dan tuong doi **sau them mot cap** — va mot tham chieu chet
# **KHONG lam do `dotnet build`**. 🚨 Do dung la loai loi **hong am tham** ma §7 cua
# `docs/design-doc-guidelines.md` da canh bao voi Mermaid.
# 🔒 Va mot cong **chi chay bang tay** thi khong phai cong ⇒ no phai co ca **DO** lan ca **XANH**.
# ══════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemDb) {
    $scriptLink = Join-Path $PSScriptRoot 'kiem-link-tai-lieu.ps1'

    Describe 'kiem-link-tai-lieu.ps1 — cong chong tai lieu tro toi tep KHONG TON TAI' {

        It 'R) Tai lieu SACH (link tro toi tep CO THAT) ⇒ exit 0 (🛡️ chong cong LUON DO)' {
            $tam = Join-Path $thuMucTam ('link-xanh-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'), ('# A' + "`n`n" + 'Xem [B](b.md).' + "`n"))
            [IO.File]::WriteAllText((Join-Path $tam 'b.md'), ('# B' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 0 'Tai lieu sach phai exit 0'
        }

        It 'S) Mot LINK MARKDOWN chet ⇒ exit 1 (🛡️ chong cong LUON XANH)' {
            $tam = Join-Path $thuMucTam ('link-do-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'), ('# A' + "`n`n" + 'Xem [B](thieu.md).' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 1 'Link markdown chet phai exit 1'

            # 🔑 **CHUNG MINH no do vi LY DO DUNG** — 📌 neu khong, *"cong bat duoc"* va *"cong hong vi
            # ly do khac"* trong giong het nhau (bai hoc §25.5: phep tiem khong xay ra ma cong van bao).
            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'thieu\.md') "Log KHONG nhac toi 'thieu.md' ⇒ do vi ly do khac. Log:`n$noiDung"
            KhangDinh ($noiDung -match 'FAIL') "Log thieu neo 'FAIL'. Log:`n$noiDung"
        }

        It 'T) Mot duong dan trong BACKTICK chet ⇒ exit 1 — 🔑 luat (B) KHAC luat (A), phai kiem RIENG' {
            # 📌 Luat (A) la `[chu](duong/dan)`; luat (B) la `` `docs/...md` `` trong backtick.
            # 🔒 Chi kiem (A) thi mot tep chi co duong dan kieu (B) se **lot luoi** — ma trong repo nay
            #    kieu (B) la **kieu pho bien nhat** (280 lan so voi 195 lan kieu A).
            $tam = Join-Path $thuMucTam ('link-do-bt-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            # ⚠️ Chuoi NHÁY DON: dau backtick la ky tu THUONG, khong phai ky tu escape.
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'), ('# A' + "`n`n" + 'Xem `docs/thieu.md` de biet.' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 1 'Duong dan backtick chet phai exit 1'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'thieu\.md') "Log KHONG nhac toi 'thieu.md'. Log:`n$noiDung"
        }

        It 'U) Duong dan backtick CO THAT nhung KHONG phai duong dan (vi du minh hoa) ⇒ KHONG bao sai' {
            # 🔒 **CA CHONG DUONG TINH GIA** (false positive): 📌 mot cong bao sai se bi nguoi ta bo qua.
            # `src/khong/co/duong/dan.cs` KHONG ket thuc bang `.md` ⇒ luat (B) **khong** kiem ⇒ exit 0.
            $tam = Join-Path $thuMucTam ('link-bt-xanh-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'), ('# A' + "`n`n" + 'Vi du: `src/khong/co/duong/dan.cs` va `https://example.com/x.md`.' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 0 'Tep khong co tham chieu .md that phai exit 0'
        }

        It 'V) LINK nam TRONG code span (backtick) ⇒ KHONG phai lien ket ⇒ exit 0' {
            # 🚨 **CA NAY SINH RA TU MOT LOI THAT**, ngay khi viet `tools/README-probe.md` (17/10/2026):
            # tai lieu do mo ta *"dinh dang link markdown"* bang cach **viet chinh link do trong backtick**
            # ⇒ luat (A) quet tho van thay `](…)` ⇒ 🔒 bao chet MOT MINH HOA DUNG.
            # ⚠️ Va ban sua dau tien (`'`[^`]+`'`) **ghep SAI** khi delimiter la HAI backtick
            # (`` `x` ``) — no ghep vao cap ngoai cung ⇒ noi dung ben trong **lot luoi**.
            # ✅ Luat dung: run N backtick mo, va run **N** backtick tiep theo dong (Markdown).
            $tam = Join-Path $thuMucTam ('link-codespan-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'), ('# A' + "`n`n" + 'Cach mo ta: `` `[chu](duong/dan.md)` `` trong backtick.' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 0 'Link nam trong code span KHONG duoc coi la lien ket'
        }

        It 'V2) Link dung SAI HOA/THUONG ⇒ exit 1 — 🔑 Windows KHONG duoc che loi nay (`S11`)' {
            # 🚨 **CA NAY SINH RA TU MOT LOI THAT (21/10/2026, lan chay CI Linux DAU TIEN):**
            # `inv-003` tro toi `docs/tiep-tuc-phien-moI.md` trong khi tep that la `…-moi.md`.
            # Tren Windows `Test-Path` **khong phan biet** hoa/thuong ⇒ cong XANH o may dev, DO tren Linux.
            # 🔑 *"phep do xanh vi he dieu hanh"* — cung ho voi `Path.IsPathRooted` cua kho tep media.
            $tam = Join-Path $thuMucTam ('link-sai-hoa-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'), ('# A' + "`n`n" + 'Xem [B](B.md).' + "`n"))
            [IO.File]::WriteAllText((Join-Path $tam 'b.md'), ('# B' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 1 'Link sai hoa/thuong phai exit 1 (ca tren Windows)'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'B\.md') "Log KHONG nhac toi 'B.md' ⇒ do vi ly do khac. Log:`n$noiDung"
        }

        It 'V4) Link toi `artifacts/` (bang chung KHONG commit) ⇒ exit 0 — 🚨 do duoc tren CI (`N18`)' {
            # 🚨 **CA NAY SINH RA TU MOT LOI THAT (05/11/2026, run `41961196209`):** `01-khao-sat-nhanh-vn.md`
            # nhung 3 anh `../../artifacts/nhanh-survey/*.png`; `README` §6 ghi *"`artifacts/` gitignore —
            # khong commit"*. May dev **co** thu muc do ⇒ cong XANH; **ban clone sach (CI) khong co** ⇒ cong DO
            # o buoc ⑪ ⇒ `ci` do tren PR #1 du moi buoc khac xanh.
            # ✅ Quy uoc: duong dan **trong repo** phai tro toi tep CO THAT; `artifacts/` la **bang chung ngoai
            # VCS** (da khai bao) ⇒ hai luat nay phai **doi xung** (xem `$boQua` cua `kiem-link-tai-lieu.ps1`).
            $tam = Join-Path $thuMucTam ('link-artifacts-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'), ('# A' + "`n`n" + 'Anh: [survey](artifacts/nhanh-survey/x.png).' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 0 'Link toi artifacts/ (ngoai VCS) KHONG duoc bao chet'
        }

        It 'V5) Cung tep do co them mot link chet TRONG repo ⇒ van exit 1 (🛡️ V4 khong duoc nuot loi that)' {
            # 🔒 Chieu nguoc cua V4: neu bo qua `artifacts/` lam cong bo qua MOI thu thi V4 la "tat cong".
            $tam = Join-Path $thuMucTam ('link-artifacts-lan-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Force -Path $tam | Out-Null
            [IO.File]::WriteAllText((Join-Path $tam 'a.md'),
                ('# A' + "`n`n" + 'Anh: [survey](artifacts/nhanh-survey/x.png). Xem [B](thieu.md).' + "`n"))

            $k = ChayScript $scriptLink @('-Goc', $tam)
            BangNhau $k.Ma 1 'Link chet TRONG repo van phai exit 1 du tep co link artifacts/'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'thieu\.md') "Log KHONG nhac toi 'thieu.md'. Log:`n$noiDung"
            KhangDinh (-not ($noiDung -match 'nhanh-survey')) 'Link artifacts/ bi bao chet ⇒ V4 chua dung'
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════
# NHOM 1c — `kiem-tat-ca.ps1` phai CHAY DU cac buoc (📌 nhanh, KHONG cham DB)
#
# 🔑 VI SAO CO NHOM NAY (`S6`, 18/10/2026): mot buoc cua cong la **mot khoi `Buoc '...'`** trong
# `kiem-tat-ca.ps1`. 📌 Neu ai do **xoa/doi ten** mot buoc (hoac gop no vao buoc khac) thi `-DayDu`
# van **XANH** — ⚠️ va **khong ai biet** rang phan kiem do **da ngung chay**.
# 🔒 Dung cau cua ca du an: *"cong khong chay tu dong thi khong phai cong"* ⇒ phai co **ca DO**
# (buoc bi xoa ⇒ bao do) chu khong chi co ca XANH (tep that ⇒ bao xanh).
# ══════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemDb) {
    # 🔑 Chup lai **danh muc buoc bat buoc** — them mot buoc moi thi them mot dong o day.
    $buocBatBuoc = [ordered]@{
        'kiem-moc-build.ps1'   = 'buoc ② build + KIEM MOC nhi phan'
        'kiem-db.ps1'          = 'buoc ⑥ bat bien DB'
        'test-cong-kiem.ps1'   = 'buoc ⑦ test cho chinh cac cong'
        'kiem-e2e.ps1'         = 'buoc ⑧ E2E co phien (HTML that)'
        'probe-cong-tai-lieu.ps1' = 'buoc tai lieu: WP dang code co duyet khong (`D-211`, `D-212`)'
        'probe-so-dia-chi.ps1' = 'buoc ⑨ so dia chi'
        'probe-khach-hang.ps1' = 'buoc ⑩ khach hang'
        'probe-thanh-toan.ps1' = 'buoc ⑪ thanh toan'
        'probe-don-san.ps1'    = 'buoc ⑫ don san (duong chay that)'
        'probe-oversell.ps1'   = 'buoc ⑬ oversell (hai request dong thoi, MOT Host)'
        'probe-outbox.ps1'     = 'buoc ⑭ outbox (process chet sau commit)'
        'probe-outbox-2instance.ps1'      = 'buoc ⑮ outbox 2 instance'
        'probe-giu-cho-2instance.ps1'     = 'buoc ⑯ giu cho 2 instance (cong #2: 2 instance kho)'
    }

    <#
        🔒 **HAM THUAN**: tra ve danh sach buoc **BI THIEU**. 📌 Khong doc tep, khong cham DB ⇒ ca kiem
        chi phu thuoc **noi dung chuoi** ⇒ ca DO va ca XANH chi khac nhau o **du lieu vao**.
    #>
    function BuocBiThieu {
        param([string]$NoiDung, $DanhMuc)

        $thieu = @()

        foreach ($ten in $DanhMuc.Keys) {
            if (-not $NoiDung.Contains("'$ten'")) {
                $thieu += $ten
            }
        }

        return $thieu
    }

    Describe 'kiem-tat-ca.ps1 — cong phai CHAY DU cac buoc (chong "buoc bi xoa ma cong van xanh")' {

        It 'W1) moi buoc BAT BUOC deu co mat trong kiem-tat-ca.ps1 (🛡️ chong cong LUON DO... va chong buoc bi xoa)' {
            $tepCong = Join-Path $PSScriptRoot 'kiem-tat-ca.ps1'
            $noiDung = [IO.File]::ReadAllText($tepCong)

            $thieu = @(BuocBiThieu -NoiDung $noiDung -DanhMuc $buocBatBuoc)

            KhangDinh ($thieu.Count -eq 0) (
                "🚨 Buoc cua cong BI THIEU khoi kiem-tat-ca.ps1 (⇒ phan kiem do KHONG chay ma cong van xanh):`n  " +
                (($thieu | ForEach-Object { "$_  ($($buocBatBuoc[$_]))" }) -join "`n  ")
            )

            # 🛡️ Chong "ca kiem rong": danh muc phai co that va tep phai co it nhat 1 khoi `Buoc '...'`.
            KhangDinh ($buocBatBuoc.Count -ge 8) "Danh muc buoc bat buoc chi co $($buocBatBuoc.Count) muc ⇒ phep kiem thieu."
            KhangDinh ($noiDung.Contains("Buoc 'Don san")) 'Khong thay khoi Buoc cua don san ⇒ phep kiem doc sai tep.'
        }

        It 'W2) XOA mot buoc ⇒ phep kiem PHAI BAO DO (🛡️ chieu nguoc: chong "kiem luon xanh")' {
            # 🔒 Chieu nguoc la **bat buoc**: mot phep kiem khong bao gio do thi khong phai phep kiem.
            # 📌 Dung chinh noi dung that roi **cat** dong cua buoc ⑫ ⇒ ham phai tra ve **dung** ten do.
            $tepCong = Join-Path $PSScriptRoot 'kiem-tat-ca.ps1'
            $noiDung = [IO.File]::ReadAllText($tepCong)

            $dongGoc = "probe-don-san.ps1"
            KhangDinh ($noiDung.Contains($dongGoc)) 'Tep that khong chua buoc ⑫ ⇒ khong the lam ca chieu nguoc.'

            # ⚠️ Thay TEN BUOC bang mot chuoi khac (khong xoa ca khoi) — du de ham thuan bao thieu.
            $noiDungDaXoa = $noiDung.Replace("'probe-don-san.ps1'", "'probe-don-san-DA-BI-XOA.ps1'")
            KhangDinh ($noiDungDaXoa -ne $noiDung) 'Phep thay KHONG xay ra ⇒ ca chieu nguoc VO NGHIA (§26.5 #1).'

            $thieu = @(BuocBiThieu -NoiDung $noiDungDaXoa -DanhMuc $buocBatBuoc)

            BangNhau $thieu.Count 1 'Xoa dung mot buoc thi phai bao thieu dung mot buoc'
            BangNhau $thieu[0] 'probe-don-san.ps1' 'Phai bao thieu DUNG buoc vua bi xoa'
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════
# ══════════════════════════════════════════════════════════════════════════════════════════════
# NHOM 1d — `ci-khang-dinh.ps1` (📌 nhanh, KHONG cham DB)
#
# 🔑 VI SAO CO NHOM NAY (`XC-007` §6.2, `S9` 21/10/2026): `ci-khang-dinh.ps1` la **cong cua chinh cong** —
# no ton tai de bat *"exit 0 ma buoc ⑧ khong chay"*. 📌 Neu chinh no **luon xanh** thi no chi tao cam giac
# an toan: `kiem-tat-ca.ps1` van xanh, CI van xanh, ma buoc ⑧ van co the im lang khong chay.
# ⇒ 🔒 Phai co **ca DO** cho tung dieu kien A2…A6, va mot ca XANH co **dong ten ca chua 🚨** (do la dang
#    cua mot lan chay TOT — xem ghi chu A6 trong `ci-khang-dinh.ps1`).
# ══════════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemDb) {
    $scriptKd = Join-Path $PSScriptRoot 'ci-khang-dinh.ps1'

    <#
        🔒 **HAM THUAN dung log gia LAP** (khong doc log that): 📌 ca kiem phai **tat dinh** — neu no doc
        log cua lan chay hien tai thi hom nay xanh, mai do vi mot ly do khong lien quan.
    #>
    function Tao-NoiDungLogCong {
        param(
            [int]$SoBuoc = 12,
            [bool]$BuildDaVao = $true,
            [bool]$KetLuan = $true,
            [bool]$BoQua = $false,
            [string]$DongThem = ''
        )

        $dong = @()

        for ($i = 0; $i -lt $SoBuoc; $i++) {
            $dong += ('── [{0}] buoc gia' -f ($i + 1))
            $dong += '   ✅ XONG (1s)'
        }

        if ($BuildDaVao) { $dong += '   ✅ Build DA VAO (moi moc deu doi).' }
        if ($BoQua) { $dong += '── [BO QUA] E2E co phien (them -DayDu de chay).' }
        if (-not [string]::IsNullOrEmpty($DongThem)) { $dong += $DongThem }
        if ($KetLuan) { $dong += '════════ TAT CA PHEP KIEM DAT (1s) ════════' }
        $dong += ('TONG SO BUOC DAT: {0}' -f $SoBuoc)

        return ($dong -join "`n")
    }

    function Tao-ThuMucLogGia {
        param([string]$TienTo, [string]$NoiDungE2e)

        $tm = Join-Path $thuMucTam ('kd-' + $TienTo + '-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $tm | Out-Null

        if (-not [string]::IsNullOrEmpty($NoiDungE2e)) {
            [IO.File]::WriteAllText((Join-Path $tm 'e2e.log'), $NoiDungE2e)
        }

        return $tm
    }

    function Chay-KhangDinh {
        param([string]$NoiDungLog, [string]$ThuMucLog)

        $tep = Join-Path $thuMucTam ('gate-' + [Guid]::NewGuid().ToString('N') + '.log')
        [IO.File]::WriteAllText($tep, $NoiDungLog)

        return ChayScript $scriptKd @('-Log', $tep, '-ThuMucLog', $ThuMucLog, '-SoBuocToiThieu', '12')
    }

    Describe 'ci-khang-dinh.ps1 — cong cua chinh cong (exit 0 KHONG duoc tin)' {

        It 'X1) Log day du ⇒ exit 0 — 🔑 va dong ten ca CO chua 🚨 van KHONG lam do (do la lan chay TOT)' {
            # 🚨 **CA NAY SINH RA TU MOT PHEP DO SAI CUA BAN DAC TA.** §6.2 A6 viet *"log khong co 🚨"*.
            #    ⚠️ Nhung do lai mot lan chay XANH that (21/10/2026): co **39** dong chua 🚨, vi ten ca kiem
            #    **dung 🚨 lam neo** (`[S5 🚨 khach BI CHAN dat hang => ...] DAT`). 📌 Cam 🚨 = cam chinh
            #    ten ca ⇒ mot cong **luon do** tren moi lan chay tot.
            # ✅ Dang dung: chi dong **BAT DAU** bang 🚨 moi la bao dong (ca X7 kiem chieu do).
            $log = Tao-NoiDungLogCong -DongThem '      [S5 🚨 khach BI CHAN dat hang => 403] DAT | guard that'
            $tm = Tao-ThuMucLogGia -TienTo 'xanh' -NoiDungE2e ("   [W1] DAT`nE2E: 3/3 ca DAT`n")

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 0 'Log day du phai exit 0'
        }

        It 'X2) THIEU log buoc ⑧ ⇒ exit 1 (A2 — 🛡️ chong "exit 0 ma E2E khong chay")' {
            $log = Tao-NoiDungLogCong
            $tm = Tao-ThuMucLogGia -TienTo 'thieu-e2e' -NoiDungE2e ''

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 1 'Thieu log E2E phai exit 1'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'A2') "Thieu neo 'A2'. Log:`n$noiDung"
            KhangDinh ($noiDung -match 'CHUA CHAY') "Thieu 'CHUA CHAY'. Log:`n$noiDung"
        }

        It 'X3) E2E chi DAT 2/3 ⇒ exit 1 (A2 — chay nhung co ca do)' {
            $log = Tao-NoiDungLogCong
            $tm = Tao-ThuMucLogGia -TienTo 'e2e-do' -NoiDungE2e ("E2E: 2/3 ca DAT`n")

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 1 'E2E 2/3 phai exit 1'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match '2/3') "Log khong nhac '2/3'. Log:`n$noiDung"
        }

        It 'X4) Log co [BO QUA] ⇒ exit 1 (A3 — khong phai lan chay day du)' {
            $log = Tao-NoiDungLogCong -BoQua $true
            $tm = Tao-ThuMucLogGia -TienTo 'boqua' -NoiDungE2e ("E2E: 3/3 ca DAT`n")

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 1 'Co [BO QUA] phai exit 1'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'A3') "Thieu neo 'A3'. Log:`n$noiDung"
        }

        It 'X5) Log thieu "Build DA VAO" ⇒ exit 1 (A4 — exit 0 khong chung minh binary da doi)' {
            $log = Tao-NoiDungLogCong -BuildDaVao $false
            $tm = Tao-ThuMucLogGia -TienTo 'khongbuild' -NoiDungE2e ("E2E: 3/3 ca DAT`n")

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 1 'Thieu "Build DA VAO" phai exit 1'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'A4') "Thieu neo 'A4'. Log:`n$noiDung"
        }

        It 'X6) Chi 11 buoc (mot buoc bi XOA) ⇒ exit 1 (A5 — 🛡️ chong buoc bi xoa ma cong van xanh)' {
            $log = Tao-NoiDungLogCong -SoBuoc 11
            $tm = Tao-ThuMucLogGia -TienTo 'thieubuoc' -NoiDungE2e ("E2E: 3/3 ca DAT`n")

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 1 'Chi 11 buoc phai exit 1'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'A5') "Thieu neo 'A5'. Log:`n$noiDung"
            KhangDinh ($noiDung -match 'san la 12') "Log khong nhac 'san la 12'. Log:`n$noiDung"
        }

        It 'X7) Log co mot DONG BAT DAU bang 🚨 ⇒ exit 1 (A6 — chieu nguoc cua X1)' {
            $log = Tao-NoiDungLogCong -DongThem '🚨 mot loi that'
            $tm = Tao-ThuMucLogGia -TienTo 'baodong' -NoiDungE2e ("E2E: 3/3 ca DAT`n")

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 1 'Co dong bao dong phai exit 1'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'A6') "Thieu neo 'A6'. Log:`n$noiDung"
        }

        It 'X8) TEN CA kiem co chua chuoi "[BO QUA]" ⇒ van exit 0 (A3 chi bat DONG CUA CONG)' {
            # 🚨 **CA NAY SINH RA TU MOT LAN DO THUC TE (21/10/2026):** `A3` ban dau kiem *"log co chua
            #    `[BO QUA]`"* ⇒ mot lan chay **XANH** bi bao **DO**, vi **ten ca kiem** cua buoc ⑦ (chinh ca
            #    `X4`) co chuoi do. 📌 Neu tin phép kiem do, CI se do **ngay ngay dau** — va cach "sua" nhanh
            #    nhat cua nguoi vội la **bo A3 di**, tuc la bo dung cai cong phat hien buoc bi bo.
            # ✅ Dang dung: chi dong **cua cong** (bat dau bang `── [BO QUA]`) moi tinh.
            $log = Tao-NoiDungLogCong -DongThem '      ✅ X4) Log co [BO QUA] ⇒ exit 1 (A3 — khong phai lan chay day du)'
            $tm = Tao-ThuMucLogGia -TienTo 'ten-ca-bo-qua' -NoiDungE2e ("E2E: 3/3 ca DAT`n")

            $k = Chay-KhangDinh -NoiDungLog $log -ThuMucLog $tm
            BangNhau $k.Ma 0 'Ten ca co chuoi [BO QUA] KHONG duoc lam do'
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════
# NHOM 1e — `nen-tang.ps1` va LUAT "khong con API chi-Windows" (`XC-007` P-01…P-06, `S9`)
#
# 🔑 VI SAO CO NHOM NAY: sua cac diem Windows-only **mot lan** thi lan sau **them mot probe** lai sao chep
# dung khuon cu (`Get-NetTCPConnection`, `bin\Debug`, `-WindowStyle Hidden`). 📌 Va khi do cong do **tren
# runner Linux** — noi duy nhat phep do nay co y nghia — chu khong do o may dev.
# ⇒ 🔒 Phai co ca QUET (**hai chieu**) va ca HANH VI that (cong, duong dan, curl).
# ══════════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemDb) {
    $scriptNenTang = Join-Path $PSScriptRoot 'nen-tang.ps1'

    # 🔑 Nap that: cac ca duoi day goi HAM THAT, khong quet chuoi.
    . $scriptNenTang

    <#
        🔒 **HAM THUAN**: tra ve danh sach vi pham cua MOT tep script. 📌 Khong doc dia ⇒ ca DO va ca XANH
        chi khac nhau o **du lieu vao**.

        🚨 **BAY TU THAM CHIEU (do duoc 21/10/2026, ngay lan dau chay ca Y1):** phep quet chay tren **MOI**
        tep trong `tools/` — **ke ca tep nay**. 📌 Neu mau bi cam duoc viet **lien** thi chinh **nguon cua
        mau** khop voi mau ⇒ Y1 bao do **tep cua no**. ✅ Ghep mau tu nhieu manh (`'Get-NetTCP' + 'Connection'`)
        — bieu thuc cuoi **giong het**, nhung khong manh nao tu khop.
    #>
    function Lay-MauApiChiWindows {
        return 'Get-NetTCP' + 'Connection|Win32' + '_Process|curl' + '\.exe|-Window' + 'Style\s|\bbin' + '\\Debug'
    }

    function Lay-MauHamNenTang {
        # 📌 Them `Dat-CoBuocDoiMatKhau`/`Invoke-KhongCoDoiMatKhau` o `WP-13` ① (`D-56`): 🔑 mot probe
        #    sao chep ham mo/bat co bao mat ma **quen nap thu vien** phai bi bat o day — neu khong, no
        #    `CommandNotFoundException` o dong dau tien va thong diep noi ve *"lenh khong ton tai"*.
        return 'Lay-Curl|Lay-DuongDanBin|Lay-PidDangNghe|Test-CongDangMo|Lay-DuongDanTam|Start-' +
        'TienTrinhAn|Start-NextAdmin|Dat-CoBuocDoiMatKhau|Invoke-KhongCoDoiMatKhau|Start-KhongCoDoiMatKhau|BatLai-CoDoiMatKhau'
    }

    function Tim-ViPhamNapThuVien {
        param([string]$Ten, [string]$NoiDung)

        if ($Ten -eq 'nen-tang.ps1') { return @() }

        # 📌 Bo **chu thich** (`<# … #>` va `#…`): chu thich KHONG chay. ⚠️ Bai hoc `S8`: quet tho thi
        #    chinh dong chu thich giai thich loi cu se **tu to cao** va lam ca kiem do vi ly do sai.
        $ma = [regex]::Replace($NoiDung, '(?s)<#.*?#>', '')
        $ma = (($ma -split "`n") | Where-Object { -not $_.TrimStart().StartsWith('#') }) -join "`n"

        $viPham = @()

        # (1) API/dang viet chi-Windows.
        if ($ma -match (Lay-MauApiChiWindows)) {
            $viPham += 'dung API chi-Windows'
        }

        # (2) Dung ham cua nen-tang.ps1 ma KHONG nap thu vien ⇒ `CommandNotFoundException` o dong dau tien.
        if ($ma -match (Lay-MauHamNenTang)) {
            if ($NoiDung -notmatch 'nen-tang\.ps1') { $viPham += 'dung ham nen-tang nhung KHONG nap thu vien' }
        }

        return @($viPham)
    }

    <#
        🚨 `D-66` (`N20`): `probe-giu-cho-2instance.ps1` duoc phep **thu lai 409** (hop dong: *"409 =
        chong request tren cung `InventoryItem`, thu lai"*). 🔑 Nhung neu ai do doi thanh **thu mai**
        hoac **xoa khang dinh bat bien** thi phep do bien thanh **dau dong** — va do dung la cach
        *"tat cong de cho xanh"* ma ca du an cam. Ham **thuan** duoi day nhan **noi dung tep** (khong
        doc dia) ⇒ ca XANH va ca DO chi khac nhau o **du lieu vao** (cung khuon `Tim-ViPhamNapThuVien`).

        Tam dieu phai con: (1) `$SoLanThuLai` khai bao va **<= 3**; (2) vong thu lai chi vi **409**;
        (3) ba khang dinh bat bien — `Reserved <= OnHand`, `Available = 0`, `CA HAI request deu 200`;
        (4) `D-214` ①: ca `P15` phai doc **that** `error.code` VA ma `eCommerce:Db:0002` cua `D-210`;
        (5) ca `P15` phai chon nhanh bang `$script:soXungDot` (0 lan 409 ⇒ `CHUA CHAY`, khong DAT);
        (6) `D-215` ①(3): `409` co **hai** nguyen nhan hop le — tep phai co `$script:coMaDb0002` (dieu kien
        `DAT`) VA `$script:coMaRong` (bat bien "moi 409 phai co ma"); (7) tep phai co hang chan doan
        `eCommerce:Lock:0001` (het han khoa phan tan ⇒ `CHUA CHAY`, khong do oan); (8) nhanh `coMaRong`
        phai dan toi `Kq ... $false` (DO), khong duoc doi thanh `CHUA CHAY`.
    #>
    function Tim-ViPhamThuLai409 {
        param([string]$Ma)

        $viPham = @()

        $m = [regex]::Match($Ma, '(?m)^\s*\$SoLanThuLai\s*=\s*(\d+)\s*$')

        if (-not $m.Success) {
            $viPham += 'KHONG khai bao $SoLanThuLai ⇒ vong thu lai khong co tran'
        }
        elseif ([int]$m.Groups[1].Value -gt 3) {
            $viPham += ('{0} = {1} > 3 ⇒ thu qua nhieu lan (noi long phep do)' -f '$SoLanThuLai', $m.Groups[1].Value)
        }

        if ($Ma -notmatch '-eq\s+409') { $viPham += 'vong thu lai KHONG chi vi 409' }

        foreach ($neo in @('Reserved <= OnHand', 'Available = 0', 'CA HAI request deu 200')) {
            if (-not $Ma.Contains($neo)) { $viPham += ('mat khang dinh: {0}' -f $neo) }
        }

        # 🚨 `D-214` ① (`N69`/WP-15): ca `P15` do **RUNTIME**, nhung tep probe phai doc **that**
        #    `error.code` va ma `eCommerce:Db:0002` (`D-210`) — neu xoa chung thi `P15` khong the bat
        #    duoc `409` thieu ma (dung lo hong `D-66` ⑤ muon bit). 📌 Quet tinh la **lop thu hai**, ca
        #    runtime la lop thu nhat — ca nay chan viec "ha cap" ca runtime thanh quet chuoi.
        if (-not $Ma.Contains('error.code')) {
            $viPham += 'thieu neo error.code (ca P15 khong doc ma loi cua 409)'
        }

        if (-not $Ma.Contains('eCommerce:Db:0002')) {
            $viPham += 'thieu ma eCommerce:Db:0002 (409 phai co ma nghiep vu, D-210)'
        }

        if ($Ma -notmatch 'if\s*\(\s*\$script:soXungDot\s+-gt\s+0\s*\)') {
            $viPham += 'KHONG dung $script:soXungDot de chon nhanh P15 (0 lan 409 phai la CHUA CHAY)'
        }

        # 🚨 `D-215` ①(3)(a): `P15` phan biet **Hai** nguyen nhan hop le cua 409 — phai co CA HAI bien
        #    phan loai. Xoa mot bien ⇒ khong con phan biet "xung dot DB" va "het han khoa".
        foreach ($bien in @('$script:coMaDb0002', '$script:coMaRong')) {
            if (-not $Ma.Contains($bien)) {
                $viPham += ('thieu bien phan loai 409: {0} (D-215)' -f $bien)
            }
        }

        # 🚨 `D-215` ①(3)(b): hang chan doan cho `Lock:0001`. 🔒 Day la ma cua **het han cho khoa phan tan**
        #    (`ChoKhoaToiDaGiay = 30`), mot 409 **hop le** ⇒ `CHUA CHAY`; neu mat hang nay thi bao cao
        #    khong con noi duoc vi sao CHUA CHAY.
        if (-not $Ma.Contains('eCommerce:Lock:0001')) {
            $viPham += 'thieu hang chan doan eCommerce:Lock:0001 (409 het han khoa phan tan, D-215)'
        }

        # 🚨 `D-215` ①(3)(c): nhanh `coMaRong` PHAI dan toi `Kq ... $false` ⇒ 409 co `code` null/rong la DO
        #    (no `D-66` ⑦), khong duoc ha xuong `CHUA CHAY`. 📌 Lay dung khoi than cua `if ($script:coMaRong)`.
        $mNhanhDo = [regex]::Match($Ma, '(?s)if\s*\(\s*\$script:coMaRong\s*\)\s*\{(?<khoi>.*?)\n\s*\}')

        if (-not $mNhanhDo.Success) {
            $viPham += 'KHONG thay nhanh if ($script:coMaRong) { ... } (D-215)'
        }
        elseif ($mNhanhDo.Groups['khoi'].Value -notmatch 'Kq[^\r\n]*\$false') {
            $viPham += 'nhanh coMaRong KHONG dan toi Kq … $false (409 thieu ma phai DO, khong phai CHUA CHAY)'
        }

        return @($viPham)
    }

    <#
        🚨 `D-67` (`N21`): `probe-don-san.ps1` ca **`C10`** do **2/3 luot** tren `main` (cung cay ma voi PR xanh).
        🔑 Cach "sua" re nhat — va sai — la **noi han cho** (`Cho-DieuKien` 30 s) hoac **bo ca**. Ca kiem duoi
        day chan dung hai viec do, va chan luon viec **am tham xoa bang chung chan doan** (khi do lan do sau
        lai *"khong doc duoc nguyen nhan"* — dung cai no `D-67` ③ da ghi).

        📌 `D-213` (`N69`/WP-15): **THU DUOC CHO** doi tu **he qua** (`Outcome = Late`) sang **tien de da
        commit** (`DLQ LyDo = HaWatermark`), **khong** noi `$GiayToiDa` (van `<= 30`), **khong** bo ca,
        **khong** doi khang dinh `tong = 1 VA Late = 1`.

        Nam dieu phai con: (1) khang dinh `tong = 1 VA Late = 1`; (2) `Cho-DieuKien` mac dinh **<= 30** giay;
        (3) khoi `$choDuA` cho **DLQ** (`$mauDemDlqLyDo`, `LyDo = 1`) va **dung MOT** `Cho-DieuKien` dung mau
        DLQ (khong cho trung 30 s + 30 s); (4) du **neo chan doan** (`CD-D67` + 3 ma `[MP-EVENTLOG-*]`);
        (5) khang dinh van doi `Late` (khong chi "co bat ky dong").

        📌 Ham **thuan**: nhan **noi dung tep** (khong doc dia) ⇒ ca XANH va ca DO chi khac nhau o **du lieu vao**.
    #>
    function Tim-ViPhamChanDoanC10 {
        param([string]$Ma)

        $viPham = @()

        if (-not $Ma.Contains('($tongA -eq 1 -and $lateA -eq 1)')) {
            $viPham += 'mat khang dinh C10 (tong = 1 VA Late = 1)'
        }

        # 🚨 `D-213`: dieu kien cho phai doi **DLQ (LyDo = HaWatermark)**; cu phap cu (`Dem-SuKienTheoKetKuc`)
        #    la **he qua** doc truoc luc commit ⇒ het han oan. 📌 Xet **trong khoi `$choDuA`** (khong phai
        #    bat ky chuoi nao trong tep) de khong bi danh lua boi mot cho khac.
        $mCho = [regex]::Match($Ma, '(?m)^\s*\$choDuA\s*=\s*Cho-DieuKien\s*(?<khoi>\{[^\r\n]*\})')

        if (-not $mCho.Success) {
            $viPham += 'KHONG thay khoi $choDuA = Cho-DieuKien { ... }'
        }
        else {
            $khoi = $mCho.Groups['khoi'].Value

            if (-not $khoi.Contains('$mauDemDlqLyDo')) {
                $viPham += 'cho KHONG con doi DLQ ($mauDemDlqLyDo) trong khoi $choDuA'
            }

            if ($khoi -notmatch '\$mauDemDlqLyDo\s+-f\s+[^,]+,\s*1\s*\)') {
                $viPham += 'khoi $choDuA KHONG truyen LyDo = 1 (mau DLQ HaWatermark)'
            }
        }

        if ($Ma -notmatch '"LyDo"\s*=\s*\{1\}') {
            $viPham += 'mau SQL DLQ khong loc theo "LyDo" = {1}'
        }

        # 📌 Dem lan cho **HaWatermark (`LyDo = 1`)** — don B co lan cho DLQ `LyDo = 0` rieng, khong phai
        #    lan cho trung cua `C10`. Bo lan cho trung o `C13` ⇒ ca A chi con **DUNG MOT** lan cho DLQ.
        $soLanChoDlq = @([regex]::Matches($Ma, '(?m)^[^\r\n]*Cho-DieuKien[^\r\n]*\$mauDemDlqLyDo[^\r\n]*,\s*1\s*\)')).Count

        if ($soLanChoDlq -ne 1) {
            $viPham += ('co {0} lan Cho-DieuKien dung $mauDemDlqLyDo voi LyDo = 1 (mong doi DUNG 1 — lan cho trung lam tong cho thanh 60 s)' -f $soLanChoDlq)
        }

        if ($Ma -match '(?m)^[^\r\n]*Cho-DieuKien[^\r\n]*Dem-SuKienTheoKetKuc\s+\$donA\.ExternalOrderId[^\r\n]*') {
            $viPham += 'con lan cho TRUNG tren he qua Late cua don A (C13) — tong thoi gian cho thanh 60 s'
        }

        $m = [regex]::Match($Ma, '(?m)\[int\]\$GiayToiDa\s*=\s*(\d+)')

        if (-not $m.Success) {
            $viPham += 'KHONG khai bao $GiayToiDa ⇒ khong con tran cho'
        }
        elseif ([int]$m.Groups[1].Value -gt 30) {
            $viPham += ('{0} = {1} > 30 ⇒ noi han cho (day nguoi ta bo qua mau do)' -f '$GiayToiDa', $m.Groups[1].Value)
        }

        foreach ($neo in @('CD-D67', '[MP-EVENTLOG-DA-GHI]', '[MP-EVENTLOG-GHI-HONG]', '[MP-EVENTLOG-TRUNG]')) {
            if (-not $Ma.Contains($neo)) { $viPham += ('mat neo chan doan: {0}' -f $neo) }
        }

        return @($viPham)
    }

    Describe 'nen-tang.ps1 — khong con API chi-Windows, va ham chay DUNG tren he dieu hanh nay' {

        It 'Y1) MOI script cua cong khong con API chi-Windows va nap thu vien khi can (🛡️ chong sao chep khuon cu)' {
            $viPham = @()

            foreach ($t in (Get-ChildItem $PSScriptRoot -Filter *.ps1 -File)) {
                $noiDung = [IO.File]::ReadAllText($t.FullName)
                foreach ($v in (Tim-ViPhamNapThuVien -Ten $t.Name -NoiDung $noiDung)) {
                    $viPham += ("{0}: {1}" -f $t.Name, $v)
                }
            }

            KhangDinh ($viPham.Count -eq 0) ("🚨 Script cua cong con phu thuoc Windows (`XC-007` P-01…P-06):`n  " + ($viPham -join "`n  "))

            # 🛡️ Chong "ca kiem rong": neu khong quet duoc tep nao thi phep kiem vo nghia.
            KhangDinh ((Get-ChildItem $PSScriptRoot -Filter *.ps1 -File).Count -ge 10) 'So tep quet qua it ⇒ phep kiem vo nghia.'
        }

        It 'Y2) Dua lai khuon CU (goi curl cua Windows, khong nap thu vien) ⇒ phep quet PHAI BAO (🛡️ chieu nguoc)' {
            # 🚨 **KHONG duoc viet thang chuoi bi cam trong chinh tep nay** — ke ca trong **TEN CA**.
            #    📌 Do la bay TU THAM CHIEU (do duoc 21/10/2026, hai lan lien tiep): phep quet Y1 quet moi
            #    tep trong `tools/`, va ten ca cung nam trong tep ⇒ ten ca lam Y1 do.
            # ✅ Ghep chuoi (`"curl" + ".exe"`) — y nghia khong doi, phep quet van bat duoc khuon cu o tep khac.
            $gia = "`$x = & curl" + ".exe -s http://localhost:5000`n"
            $viPham = @(Tim-ViPhamNapThuVien -Ten 'gia.ps1' -NoiDung $gia)

            KhangDinh ($viPham.Count -ge 1) 'Phep quet KHONG bat duoc khuon cu ⇒ no vo dung.'

            # Va chieu con lai: cung noi dung do, sau khi thay bang ham cua thu vien + nap thu vien ⇒ sach.
            $sach = ". (Join-Path `$PSScriptRoot 'nen-tang.ps1')`n`$x = & (Lay-Curl) -s http://localhost:5000`n"
            $viPhamSach = @(Tim-ViPhamNapThuVien -Ten 'gia.ps1' -NoiDung $sach)
            KhangDinh ($viPhamSach.Count -eq 0) ("Phep quet bao SAI cho tep da sua: " + ($viPhamSach -join ' | '))
        }

        It 'Y3) Test-CongDangMo: cong DANG NGHE ⇒ true, cong DA DONG ⇒ false (va doc duoc PID)' {
            # 🔒 **CA NAY DO P-02 TREN CHINH HE DIEU HANH DANG CHAY.** 📌 Tren Windows duong cu
            #    `Get-NetTCPConnection` chay duoc, nen neu chi kiem tren may dev thi **khong chung minh** duoc
            #    no chay tren Linux — 🔑 phai la ca kiem co that mot listener va **ca hai chieu**.
            $nghe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $cong = 0

            try {
                $nghe.Start()
                $cong = $nghe.LocalEndpoint.Port

                KhangDinh (Test-CongDangMo -Cong $cong) 'Test-CongDangMo tra FALSE cho mot cong DANG NGHE.'

                $pids = @(Lay-PidDangNghe -Cong $cong)
                KhangDinh ($pids -contains $PID) ("Lay-PidDangNghe khong tra ve PID cua chinh tien trinh nay (do duoc: [{0}], can co {1})." -f ($pids -join ','), $PID)
            }
            finally {
                $nghe.Stop()
            }

            # ⚠️ Cho he dieu hanh nha cong. 📌 Neu bo qua buoc nay, ca kiem se **do ngau nhien** (va mot ca
            #    khong tat dinh thi khong phai luoi an toan).
            Start-Sleep -Milliseconds 300
            KhangDinh (-not (Test-CongDangMo -Cong $cong)) 'Test-CongDangMo VAN tra TRUE sau khi da dong cong.'
        }

        It 'Y4) P-04: duong dan `bin` mac dinh cua kiem-moc-build PHAI tim thay tep (hoa theo he dieu hanh)' {
            # 🚨 Day la ca bat dung loi F2/P-04: tren Linux, gia tri mac dinh viet cung `src\...\bin\Debug\net10.0`
            #    lam `Test-Path` = false ⇒ `exit 2` ⇒ cong do o buoc ②, va thong diep noi ve *"loc tep"*.
            $k = ChayScript (Join-Path $PSScriptRoot 'kiem-moc-build.ps1') @('-Ten', 'eCommerce.Application.dll')

            if ($k.Ma -eq 2) {
                $noiDung = Get-Content $k.Log -Raw
                KhangDinh $false ("kiem-moc-build exit 2: duong dan mac dinh KHONG tim thay tep tren he dieu hanh nay. Log:`n$noiDung")
            }

            BangNhau $k.Ma 0 'kiem-moc-build voi duong dan mac dinh'

            $noiDung = Get-Content $k.Log -Raw
            KhangDinh ($noiDung -match 'Tong: \d+ tep') "Log thieu dong 'Tong: N tep'. Log:`n$noiDung"
        }

        It 'Y5) Lay-Curl tra ve lenh CO THAT tren he dieu hanh nay (P-03)' {
            $curl = Lay-Curl

            KhangDinh (-not [string]::IsNullOrWhiteSpace($curl)) 'Lay-Curl tra ve rong.'
            KhangDinh ($null -ne (Get-Command $curl -ErrorAction SilentlyContinue)) ("Lenh '{0}' KHONG ton tai tren may nay (P-03)." -f $curl)
        }

        It 'Y6) Script nao MO co bao mat thi PHAI co dong BAT LAI (🛡️ chan "quen bat lai")' {
            # 🚨 `WP-13` ① (`D-56`): do duoc **4** probe tat co `ShouldChangePasswordOnNextLogin` ma
            #    **khong bao gio** bat lai ⇒ chay xong la **commit mot admin khong bi buoc doi mat khau**,
            #    va cong DB (nhom ② cua `kiem-db.ps1`) do o **lan sau**.
            # 📌 Phep quet **hai chieu**: (a) tep nao mo co phai co `BatLai-CoDoiMatKhau`; (b) phai thay
            #    **that** it nhat 4 tep mo co — 🔑 neu khong thi phep quet *"khong bao gio do"* la vo nghia.
            $mo = @()

            foreach ($t in (Get-ChildItem $PSScriptRoot -Filter *.ps1 -File)) {
                # ⚠️ Bo chinh tep nay: no **chua ten ham** trong bang quet `Lay-MauHamNenTang` ⇒ tu to cao.
                if ($t.Name -eq 'test-cong-kiem.ps1') { continue }

                $noiDung = [IO.File]::ReadAllText($t.FullName)
                $ma = [regex]::Replace($noiDung, '(?s)<#.*?#>', '')
                $ma = (($ma -split "`n") | Where-Object { -not $_.TrimStart().StartsWith('#') }) -join "`n"

                if ($ma -match 'Start-KhongCoDoiMatKhau') {
                    $mo += $t.Name
                    KhangDinh ($ma -match 'BatLai-CoDoiMatKhau') ("{0}: MO co bao mat ma KHONG co dong BAT LAI ⇒ probe de lai mot admin khong bi buoc doi mat khau." -f $t.Name)
                }
            }

            KhangDinh ($mo.Count -ge 4) ("Chi thay {0} script mo co bao mat ⇒ phep quet VO NGHIA (mong doi >= 4)." -f $mo.Count)
        }

        It 'Y7) probe giu cho 2 instance: THU LAI 409 co GIOI HAN, khong mat khang dinh bat bien, VA 409 do that co ma (D-66, D-214, D-215)' {
            # 🚨 `N20` (`D-66`): `D-64` — `probe-giu-cho-2instance` flaky tren CI (`17/17` vs `14/3`;
            #    `P12` `200/409` · `P13` 1 dong · `P14` 1 exception). Da chot huong: **409 = chong request,
            #    thu lai** ⇒ probe gui lai toi da 3 lan CUNG `idempotencyKey`.
            # 🔑 Vi sao can CA NAY: noi long mot phep do la cach nhanh nhat de lam cong xanh — va lan sau
            #    khong ai biet no da bi noi. Ca nay quet **hai chieu**: tep that phai SACH; sua tep theo
            #    huong "thu mai"/"xoa khang dinh"/"bo ma loi"/"ha nhanh DO" thi phep quet PHAI bao.
            # 📌 `D-214` ①: them 2 neo `error.code` + `eCommerce:Db:0002` va nhanh `$script:soXungDot`
            #    (`P15`) — neu khong, ca runtime `409` bi ha cap thanh "co 409 la du" ma khong ai thay.
            # 📌 `D-215` ①(3): them 2 bien phan loai (`coMaDb0002`/`coMaRong`), hang `eCommerce:Lock:0001`,
            #    va nhanh `coMaRong` phai dan toi `Kq ... $false` (409 thieu ma la DO).
            $tep = Join-Path $PSScriptRoot 'probe-giu-cho-2instance.ps1'
            KhangDinh (Test-Path $tep) ("KHONG thay {0} ⇒ ca kiem vo nghia." -f $tep)

            $noiDung = [IO.File]::ReadAllText($tep)
            $ma = [regex]::Replace($noiDung, '(?s)<#.*?#>', '')
            $ma = (($ma -split "`n") | Where-Object { -not $_.TrimStart().StartsWith('#') }) -join "`n"

            $viPham = @(Tim-ViPhamThuLai409 -Ma $ma)
            KhangDinh ($viPham.Count -eq 0) ("probe-giu-cho-2instance.ps1: " + ($viPham -join ' · '))

            # 🛡️ CHIEU NGUOC 1 — noi gioi han len 99 ⇒ phep quet PHAI bao.
            $noiLong = $ma -replace '(?m)^(\s*\$SoLanThuLai\s*=\s*)\d+', '${1}99'
            KhangDinh (@(Tim-ViPhamThuLai409 -Ma $noiLong).Count -gt 0) 'Noi gioi han thu lai len 99 ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 2 — xoa mot khang dinh bat bien ⇒ phep quet PHAI bao.
            $matNeo = $ma.Replace('Reserved <= OnHand', 'BatDauGiuCho')
            KhangDinh (@(Tim-ViPhamThuLai409 -Ma $matNeo).Count -gt 0) 'Xoa khang dinh "Reserved <= OnHand" ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 3 — xoa neo `error.code` ⇒ phep quet PHAI bao (`D-214` ①).
            $matErrorCode = $ma.Replace('error.code', 'errorXcode')
            KhangDinh ($matErrorCode -ne $ma) 'Phep thay neo error.code KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamThuLai409 -Ma $matErrorCode).Count -gt 0) 'Xoa neo error.code ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 4 — xoa ma `eCommerce:Db:0002` ⇒ phep quet PHAI bao (`D-214` ①).
            $matMaXungDot = $ma.Replace('eCommerce:Db:0002', 'X')
            KhangDinh ($matMaXungDot -ne $ma) 'Phep thay ma eCommerce:Db:0002 KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamThuLai409 -Ma $matMaXungDot).Count -gt 0) 'Xoa ma eCommerce:Db:0002 ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 5 — ha nhanh DO cua `coMaRong` xuong `if ($false)` ⇒ phep quet PHAI bao (`D-215` ①(3)).
            $haNhanhDo = $ma.Replace('if ($script:coMaRong) {', 'if ($false) {')
            KhangDinh ($haNhanhDo -ne $ma) 'Phep thay nhanh coMaRong KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamThuLai409 -Ma $haNhanhDo).Count -gt 0) 'Doi nhanh coMaRong thanh $false ma phep quet KHONG bao ⇒ phep quet vo nghia.'
        }

        It 'Y8) probe don san: C10 KHONG bi noi han cho, cho DLQ (khong phai he qua), va con du bang chung chan doan (D-67, D-213)' {
            # 🚨 `N21` (`D-67`): `C10` — *"su kien DEN MUON ⇒ nhat ky DUNG 1 dong, Outcome = Late"* — do **2/3 luot**
            #    tren `main` **cung cay ma** voi PR xanh (`tong = 0 · Late = 0`, trong khi `C11…C14` DAT).
            # 🔑 Vi sao can CA NAY: `D-67` ④ cam **noi `Cho-DieuKien` 30 s** va cam **bo ca** — nhung cam bang
            #    loi van thi khong ai chan duoc. 📌 Ca nay quet **nam chieu xuoi** (tep that phai con du) va
            #    **nam chieu nguoc** (sua tep theo huong "cho xanh" thi phep quet PHAI bao).
            # 📌 `D-213` (`N69`/WP-15): cho doi **DLQ (tien de da commit)**, khong doi **he qua** `Late` nua.
            $tep = Join-Path $PSScriptRoot 'probe-don-san.ps1'
            KhangDinh (Test-Path $tep) ("KHONG thay {0} ⇒ ca kiem vo nghia." -f $tep)

            $noiDung = [IO.File]::ReadAllText($tep)
            $ma = [regex]::Replace($noiDung, '(?s)<#.*?#>', '')
            $ma = (($ma -split "`n") | Where-Object { -not $_.TrimStart().StartsWith('#') }) -join "`n"

            $viPham = @(Tim-ViPhamChanDoanC10 -Ma $ma)
            KhangDinh ($viPham.Count -eq 0) ("probe-don-san.ps1: " + ($viPham -join ' · '))

            # 🛡️ CHIEU NGUOC 1 — noi han cho 30 s len 99 s ⇒ phep quet PHAI bao.
            $noiHan = $ma -replace '(?m)(\[int\]\$GiayToiDa\s*=\s*)\d+', '${1}99'
            KhangDinh ($noiHan -ne $ma) 'Phep thay han cho KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamChanDoanC10 -Ma $noiHan).Count -gt 0) 'Noi han cho len 99 s ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 2 — xoa khang dinh `tong = 1 VA Late = 1` ⇒ phep quet PHAI bao.
            $matKhangDinh = $ma.Replace('($tongA -eq 1 -and $lateA -eq 1)', '($true)')
            KhangDinh ($matKhangDinh -ne $ma) 'Phep thay khang dinh KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamChanDoanC10 -Ma $matKhangDinh).Count -gt 0) 'Xoa khang dinh "tong = 1 VA Late = 1" ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 3 — bo dieu kien doi `Late` (`-and $lateA -eq 1` ⇒ `-and $true`) ⇒ phep quet PHAI bao.
            $boDoiLate = $ma.Replace('-and $lateA -eq 1', '-and $true')
            KhangDinh ($boDoiLate -ne $ma) 'Phep thay "-and $lateA -eq 1" KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamChanDoanC10 -Ma $boDoiLate).Count -gt 0) 'Bo dieu kien doi Outcome Late ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 4 — xoa neo chan doan ⇒ phep quet PHAI bao (neu khong, lan do sau lai khong doc duoc nguyen nhan).
            $matNeo = $ma.Replace('CD-D67', 'X').Replace('[MP-EVENTLOG-GHI-HONG]', 'X')
            KhangDinh ($matNeo -ne $ma) 'Phep thay neo chan doan KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamChanDoanC10 -Ma $matNeo).Count -gt 0) 'Xoa neo chan doan ma phep quet KHONG bao ⇒ phep quet vo nghia.'

            # 🛡️ CHIEU NGUOC 5 — bo dieu kien cho DLQ (doi khoi `$choDuA` thanh `$true`) ⇒ phep quet PHAI bao (`D-213`).
            $boChoDlq = [regex]::Replace($ma, '\$choDuA\s*=\s*Cho-DieuKien\s*\{[^}]*\}', '$choDuA = Cho-DieuKien { $true }')
            KhangDinh ($boChoDlq -ne $ma) 'Phep thay khoi $choDuA KHONG xay ra ⇒ ca chieu nguoc VO NGHIA.'
            KhangDinh (@(Tim-ViPhamChanDoanC10 -Ma $boChoDlq).Count -gt 0) 'Bo dieu kien cho DLQ ma phep quet KHONG bao ⇒ phep quet vo nghia.'
        }

        It 'Y9) Cong tai lieu: -TuKiem DAT va repo THAT xanh o CA HAI che do (D-211, D-212)' {
            # 🚨 `D-211` ① (`N69`/WP-15): QA so huu LOGIC (`tools/probe-cong-tai-lieu.ps1`), OPS so huu
            #    **DAU DAY** — ca nay chong "cong rong": `-TuKiem` phai tu DAT (fixture cua chinh probe),
            #    va probe phai XANH tren **repo that** (khong chi tren fixture).
            # 📌 `exit 2` = KHONG XAC DINH DUOC (fail-closed) ⇒ ca nay DO. 📌 `Y9` la lop thu ba cua
            #    `D-211` ①: (1) `-TuKiem` moi lan cong chay · (2) `Y9` (ca nay) · (3) negative-control cua QA.
            $tepProbe = Join-Path $PSScriptRoot 'probe-cong-tai-lieu.ps1'
            KhangDinh (Test-Path $tepProbe) ("KHONG thay {0} ⇒ ca kiem vo nghia." -f $tepProbe)

            $tuKiem = ChayScript $tepProbe @('-TuKiem')
            KhangDinh ($tuKiem.Ma -eq 0) ("-TuKiem phai exit 0 nhung do duoc {0}. Log:`n{1}" -f $tuKiem.Ma, (Get-Content $tuKiem.Log -Raw))

            # 📌 Chay `Code` tren repo that, de `-WP auto` tu suy (GITHUB_HEAD_REF → GITHUB_REF_NAME → ten nhanh).
            $cheDoCode = ChayScript $tepProbe @('-CheDo', 'Code')
            KhangDinh ($cheDoCode.Ma -eq 0) ("-CheDo Code tren repo that phai exit 0 nhung do duoc {0} (2 = KHONG XAC DINH DUOC = DO). Log:`n{1}" -f $cheDoCode.Ma, (Get-Content $cheDoCode.Log -Raw))

            # 📌 `MergeMain` ghim `-WP WP-15`: che do nay chi chay tren workflow `pull_request` base `main`
            #    (`D-212` (ii)); o cong cuc bo, WP khac co ma thi MergeMain DO **dung y** cho toi khi co chu
            #    ky NGUOI — ghim WP-15 de `Y9` khong bien `kiem-tat-ca.ps1` thanh cong luon DO cho moi WP.
            $cheDoMerge = ChayScript $tepProbe @('-CheDo', 'MergeMain', '-WP', 'WP-15')
            KhangDinh ($cheDoMerge.Ma -eq 0) ("-CheDo MergeMain (WP-15, khong ma) tren repo that phai exit 0 nhung do duoc {0}. Log:`n{1}" -f $cheDoMerge.Ma, (Get-Content $cheDoMerge.Log -Raw))

            Write-Output ("   -TuKiem exit {0} · Code exit {1} · MergeMain (WP-15) exit {2}" -f $tuKiem.Ma, $cheDoCode.Ma, $cheDoMerge.Ma)
        }
    }
}

# NHOM 2 — `ham-tiem.ps1` (📌 nhanh, KHONG cham DB)
# ══════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemDb) {
    # 🔑 Dot-source thu vien: 🔒 no chi co **ham**, khong co `param` cap tep ⇒ nạp an toan.
    . (Join-Path $PSScriptRoot 'ham-tiem.ps1')

    Describe 'ham-tiem.ps1 — thu vien TU KHANG DINH sau moi phep tiem' {

        It 'L) TiemVaoTep: khoi cu KHONG khop ⇒ NEM (🛡️ chan §26.5 #1)' {
            # 🚨 **CA QUAN TRONG NHAT CUA NHOM NAY.** 📌 §26.5 #1: phep `String.Replace` **khong khop** ⇒
            # ⚠️ **phep tiem khong xay ra** ⇒ bo test bao `exit 0` ⇒ 🔑 **suyt ket luan** *"bo test khong bat
            # duoc gi"*. ✅ Ham nay **NEM** ⇒ tu nay *"tiem khong thanh"* la **loi on ao**, khong con la
            # **gia dinh ngam**.
            $tep = Join-Path $thuMucTam 'thu-l-khong-khop.txt'
            [System.IO.File]::WriteAllText($tep, 'noi dung goc')

            $nem = $false
            try { $null = TiemVaoTep -DuongDan $tep -KhoiCu 'KHOI KHONG HE TON TAI' -KhoiMoi 'x' }
            catch { $nem = $true }

            KhangDinh $nem 'TiemVaoTep KHONG nem khi khoi cu khong khop ⇒ 🔒 PHEP TIEM KHONG XAY RA ma im lang'
            BangNhau ([System.IO.File]::ReadAllText($tep)) 'noi dung goc' 'Tep khong duoc doi'
        }

        It 'M) TiemVaoTep: khoi cu khop NHIEU HON MOT lan ⇒ NEM (⚠️ Replace se thay HET)' {
            # 🔑 Nhieu hon mot lan la mot **bay that**: `Replace` thay **het** ⇒ 📌 phep tiem vuot y dinh, va
            # ca kiem se do vi **ly do khac** voi dieu ta tuong.
            $tep = Join-Path $thuMucTam 'thu-m-nhieu-lan.txt'
            [System.IO.File]::WriteAllText($tep, "AAA`nBBB`nAAA")

            $nem = $false
            try { $null = TiemVaoTep -DuongDan $tep -KhoiCu 'AAA' -KhoiMoi 'ZZZ' } catch { $nem = $true }

            KhangDinh $nem 'TiemVaoTep KHONG nem khi khoi cu khop nhieu lan'
            BangNhau ([System.IO.File]::ReadAllText($tep)) "AAA`nBBB`nAAA" 'Tep khong duoc doi'
        }

        It 'N) TiemVaoTep: neo khong xuat hien trong khoi moi ⇒ NEM' {
            $tep = Join-Path $thuMucTam 'thu-n-neo.txt'
            [System.IO.File]::WriteAllText($tep, 'goc')

            $nem = $false
            try { $null = TiemVaoTep -DuongDan $tep -KhoiCu 'goc' -KhoiMoi 'moi' -NeoXacNhan 'K97KHONGCO' } catch { $nem = $true }

            KhangDinh $nem 'TiemVaoTep KHONG nem khi neo khong co trong khoi moi'
        }

        It 'O) TiemVaoTep + KhoiPhucTep: tiem DUOC ⇒ neo CO ⇒ khoi phuc ⇒ neo MAT (vong tron khep kin)' {
            # 🔒 **CA KHANG DINH CA HAI CHIEU** — 🔑 neu chi kiem chieu tiem thi mot phep **khoi phuc hong**
            # se lam **cac ca sau** do vi **ly do vo hinh** (📌 ma ca truoc con lai).
            $tep = Join-Path $thuMucTam 'thu-o-vong-tron.txt'
            [System.IO.File]::WriteAllText($tep, 'dau KHOICUOI cuoi')

            $t = TiemVaoTep -DuongDan $tep -KhoiCu 'KHOICUOI' -KhoiMoi 'K97TIEM' -NeoXacNhan 'K97TIEM'

            try {
                $sauTiem = [System.IO.File]::ReadAllText($tep)
                KhangDinh ($sauTiem -match 'K97TIEM') 'Sau khi tiem, neo KHONG co ⇒ phep tiem khong xay ra'
            }
            finally {
                KhoiPhucTep -TayTiem $t -NeoDaTiem 'K97TIEM'
            }

            $sauKhoiPhuc = [System.IO.File]::ReadAllText($tep)
            BangNhau $sauKhoiPhuc 'dau KHOICUOI cuoi' 'Noi dung sau khi khoi phuc'
            KhangDinh ($sauKhoiPhuc -notmatch 'K97TIEM') 'Neo VAN CON sau khi khoi phuc'
        }

        It 'P) TiemVaoDb: chan cau SQL KHONG co nhay don (🛡️ chan §25.5 TRUOC khi cham DB)' {
            # 🚨 **CA CHAN §25.5** — 📌 mot cau SQL **that su** luon co **it nhat mot nhay don** (moi gia tri
            # chuoi deu can no). ⚠️ Khong co nhay don nao ⇒ gan nhu chac chan chuoi da bi **lop goi an mat**
            # ⇒ 🔑 va phep tiem se **khong xay ra** ma ta **tuong cong khong bat**.
            $nem = $false
            $thongDiep = ''

            try {
                # ⚠️ **KHONG** co nhay don nao ⇒ phai bi chan **TRUOC** khi goi DB.
                $null = TiemVaoDb -Sql 'insert into "X" ("Id") values (abc)'
            }
            catch { $nem = $true; $thongDiep = $_.Exception.Message }

            KhangDinh $nem 'TiemVaoDb KHONG chan cau SQL khong co nhay don'
            KhangDinh ($thongDiep -match '25.5') "Thong diep loi KHONG tro tới §25.5: $thongDiep"
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════════════════════
# NHOM 3 — `kiem-db.ps1` (⚠️ CAN Docker + PostgreSQL)
# ══════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemMoc) {
    $scriptDb = Join-Path $PSScriptRoot 'kiem-db.ps1'

    <#
        🔒 **HAM PSQL RIENG CHO TEST** — dung **here-string** o MOI cho goi.
        🚨 Bai hoc `sal-006` §25.5: toi da thoat nhay (`''`) **ben trong** mot chuoi co nhay ⇒ cau SQL
        **sai cu phap** ⇒ ⚠️ **phep tiem khong xay ra** ma toi **tuong cong khong bat**.
        📌 Trong file nay, moi cau SQL deu la **here-string** ⇒ **khong co phep thoat nhay nao**.
    #>
    function PsqlTest {
        param([string]$Sql)
        $tam = Join-Path $thuMucTam ('sql-' + [Guid]::NewGuid().ToString('N') + '.txt')
        $Sql | docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A -f - *> $tam
        $ma = $LASTEXITCODE
        $dong = @(Get-Content $tam -ErrorAction SilentlyContinue | Where-Object { $_.Trim() -ne '' })
        Remove-Item $tam -Force -ErrorAction SilentlyContinue
        if ($ma -ne 0) { throw ("psql that bai: {0}" -f ($dong -join ' ')) }
        return $dong
    }

    # 🛡️ TIEN DE: neu Docker/PostgreSQL khong chay thi **ca nhom VO NGHIA** — 📌 phai NOI RO, ⚠️ **KHONG**
    # duoc coi la *"DB sach"* (bai hoc: ma 2 = VO NGHIA, khac ma 0 = DAT).
    $dbSong = $true
    try { $null = PsqlTest 'select 1;' } catch { $dbSong = $false }

    if (-not $dbSong) {
        Write-Output ''
        Write-Output '── kiem-db.ps1 ──'
        Write-Output '   ⚠️ BO QUA: khong ket noi duoc PostgreSQL (Docker chua chay?) — ⚠️ KHONG phai "DB sach".'
        $ketQua.Add([pscustomobject]@{ Nhom = 'kiem-db.ps1'; Ten = 'TIEN DE (PostgreSQL)'; Dat = $false; Loi = 'khong ket noi duoc' })
    }
    else {
        Describe 'kiem-db.ps1 — cong bat bien DB' {

            It 'G) DB sach ⇒ 0 bat bien HONG ⇒ exit 0' {
                $k = ChayScript $scriptDb @()
                BangNhau $k.Ma 0 'Kiem DB sach'

                $noiDung = Get-Content $k.Log -Raw
                KhangDinh ($noiDung -match '0 HONG') "Thieu '0 HONG'. Log:`n$noiDung"
            }

            It 'H) Dong XOA MEM trong bang "phai rong" ⇒ exit 1 (⚠️ mot phep DEM THUONG se bo sot)' {
                # 🔒 **CA DANG GIA NHAT CUA NHOM NAY.** Mot dong `IsDeleted = true` **KHONG** hien ra trong
                # truy van thuong, ⚠️ nhung **van chiem** unique index `NULLS NOT DISTINCT` ⇒ lan gieo sau
                # that bai **vi ly do kho hieu**. 📌 Chi dem `count(*)` (khong tach `IsDeleted`) se **BO SOT**.
                $id = 'e0000000-0000-0000-0000-0000000000b9'

                try {
                    PsqlTest @'
delete from "InvChannelLimits" where "Id" = 'e0000000-0000-0000-0000-0000000000b9';
insert into "InvChannelLimits" ("Id","TenantId","ChannelCode","WarehouseId","GiaTri","HanhDong","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted","DeletionTime")
values ('e0000000-0000-0000-0000-0000000000b9',null,'shopee',null,10,0,'{}','k97b9',now(),true,now());
'@ | Out-Null

                    # 🔒 **KHUANG DINH PHEP TIEM DA XAY RA** — bai hoc §25.5: neu khong khang dinh thi
                    # *"cong khong bat"* va *"tiem khong thanh"* **trong GIONG HET NHAU**.
                    $dem = (PsqlTest @'
select count(*)::text from "InvChannelLimits" where "Id" = 'e0000000-0000-0000-0000-0000000000b9';
'@) -join ''
                    BangNhau $dem '1' 'Tien de (dong xoa mem phai TON TAI)'

                    $k = ChayScript $scriptDb @()
                    BangNhau $k.Ma 1 'Kiem khi co dong XOA MEM'

                    $noiDung = Get-Content $k.Log -Raw
                    KhangDinh ($noiDung -match 'xoaMem=1') "Thieu 'xoaMem=1'. Log:`n$noiDung"
                }
                finally {
                    # ⚠️ **DON DEP TRONG `finally`** — neu ca nay nem giua duong ma khong don thi lan chay sau
                    # se **hong** vi du lieu ca truoc con lai ⇒ 🔑 mot test **tu lam hong chinh no**.
                    PsqlTest @'
delete from "InvChannelLimits" where "Id" = 'e0000000-0000-0000-0000-0000000000b9';
'@ | Out-Null
                }
            }

            It 'I) Co bao mat admin = false ⇒ exit 1' {
                try {
                    PsqlTest @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = 'ADMIN';
'@ | Out-Null

                    $k = ChayScript $scriptDb @()
                    BangNhau $k.Ma 1 'Kiem khi co bao mat = false'

                    $noiDung = Get-Content $k.Log -Raw
                    KhangDinh ($noiDung -match 'ADMIN\|false') "Thieu 'ADMIN|false'. Log:`n$noiDung"
                }
                finally {
                    PsqlTest @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@ | Out-Null
                }
            }

            It 'J) Dong probe GUID trong bang KHONG co trong danh sach viet tay ⇒ nhom (6) TU DO bat' {
                # 🔑 **CA KHANG DINH GIA TRI CUA NHOM (6)** — `AbpAuditLogs` **khong** nam trong danh sach
                # viet tay ⇒ 📌 neu khong co tu do thi dong rac nay **LOT**.
                try {
                    PsqlTest @'
insert into "AbpAuditLogs" ("Id","ApplicationName","ExecutionTime","ExecutionDuration","ExtraProperties","ConcurrencyStamp")
values ('e0000000-0000-0000-0000-0000000000b8','eCommerce',now(),1,'{}','k97b8');
'@ | Out-Null

                    $dem = (PsqlTest @'
select count(*)::text from "AbpAuditLogs" where "Id" = 'e0000000-0000-0000-0000-0000000000b8';
'@) -join ''
                    BangNhau $dem '1' 'Tien de (dong AbpAuditLogs phai TON TAI)'

                    $k = ChayScript $scriptDb @()
                    BangNhau $k.Ma 1 'Kiem khi co rac o bang ngoai danh sach'

                    $noiDung = Get-Content $k.Log -Raw
                    KhangDinh ($noiDung -match 'AbpAuditLogs') "Nhom (6) KHONG bat duoc AbpAuditLogs. Log:`n$noiDung"
                    KhangDinh ($noiDung -match 'TU DONG') "Thieu neo 'TU DONG'. Log:`n$noiDung"
                }
                finally {
                    PsqlTest @'
delete from "AbpAuditLogs" where "Id" = 'e0000000-0000-0000-0000-0000000000b8';
'@ | Out-Null
                }
            }

            It 'K) Sau moi ca tiem ⇒ DB SACH LAI (exit 0) — 🔑 test TU DON DEP' {
                # 🔒 **CA NAY BAO VE CHINH BO TEST:** neu mot ca o tren don dep that bai thi ca nay **DO**
                # ⇒ 📌 ta biet ngay, thay vi de lan chay sau **hong vi ly do vo hinh**.
                $k = ChayScript $scriptDb @()
                BangNhau $k.Ma 0 'Kiem DB sau khi don dep'
            }

            It 'L2) `Invoke-KhongCoDoiMatKhau`: co TAT trong khoi lenh + BAT LAI — ke ca khi khoi lenh `exit`' {
                # 🔑 CA NAY CHINH LA NO CUA `WP-13` ① (`D-56`): truoc `N14` **chua ai do** *"co bao mat co
                # bat lai sau luot probe khong"* — ma do la thu duy nhat phan biet *"probe da don"* voi
                # *"probe de lai mot admin khong bi buoc doi mat khau"*.
                # 📌 Do **hai duong**: (a) khoi lenh thuong ⇒ co `false` ben trong, `true` sau;
                # (b) khoi lenh goi `exit` ⇒ ⚠️ phai chay o **TIEN TRINH CON** (neu khong no lam chet bo test)
                # va 🔒 phai giu **nguyen exit code** (7) — mot `finally` nuot exit code la mot cong *"do vi
                # ly do khac"* (bai hoc `D-55` ③).
                # 📌 Nap thu vien **ngay trong ca**: voi `-ChiKiemDb`, khoi `NHOM 1e` (noi nap thu vien cho
                #    ca Y*) bi **BO QUA** ⇒ 🔑 khong nap o day thi ca nay bao *"lenh khong ton tai"* — do vi
                #    thu tu nap, khong vi san pham (dung lop loi `nen-tang.ps1` sinh ra de chong).
                . (Join-Path $PSScriptRoot 'nen-tang.ps1')

                $docCo = {
                    (PsqlTest @'
select "ShouldChangePasswordOnNextLogin"::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN';
'@) -join ''
                }

                try {
                    BangNhau (& $docCo) 'true' 'Tien de: co phai dang BAT truoc khi do'

                    $benTrong = Invoke-KhongCoDoiMatKhau { & $docCo }
                    BangNhau $benTrong 'false' 'Trong khoi lenh: co phai TAT'
                    BangNhau (& $docCo) 'true' 'Sau khoi lenh: co phai BAT LAI'

                    $tep = Join-Path $thuMucTam ('thu-l-exit-' + [Guid]::NewGuid().ToString('N') + '.ps1')
                    $nenTang = Join-Path $PSScriptRoot 'nen-tang.ps1'
                    [IO.File]::WriteAllText($tep, (". '{0}'`nInvoke-KhongCoDoiMatKhau {{ exit 7 }}`n" -f $nenTang))

                    $k = ChayScript $tep @()
                    BangNhau $k.Ma 7 'Khoi lenh goi exit: exit code KHONG duoc bi `finally` nuot'
                    BangNhau (& $docCo) 'true' 'Sau `exit` trong khoi lenh: co phai BAT LAI'
                }
                finally {
                    PsqlTest @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@ | Out-Null
                }
            }
        }
    }
}

# ════════════════════════════════════════════════════════════════════════════════════════════
# NHOM 4 — `tools/agents/*.ps1` — cong cua he agent Claude Code (📌 nhanh, KHONG cham DB)
# Ke hoach: docs/agents/ke-hoach-v2-claude-code-pro.md §8. 🔑 Cung khuon "cong tu test chinh no": moi ca dung
# tai lieu MAU trong `_tam-test`, ⚠️ KHONG dung tai lieu that (tai lieu that doi theo WP-14 ⇒ ca se do vo co).
# ════════════════════════════════════════════════════════════════════════════════════════════
if (-not $ChiKiemDb) {
    $ckDoc = Join-Path $PSScriptRoot 'agents/check-feature-doc.ps1'
    $ckArt = Join-Path $PSScriptRoot 'agents/check-agent-artifact.ps1'

    function TaoTepMau {
        param([string]$Ten, [string]$NoiDung)
        $p = Join-Path $thuMucTam $Ten
        [IO.File]::WriteAllText($p, $NoiDung, [Text.UTF8Encoding]::new($false))
        return $p
    }

    # Tai lieu feature mau DU: 16 muc, 2 BR (1 viet tat, 1 day du o §14), muc doi chieu Nhanh co bang.
    function TaiLieuMau {
        param([int[]]$BoMuc = @(), [string]$PhuBr2 = 'BR-SHP-009-002', [string]$DoiChieu = 'bang')
        $sb = [Text.StringBuilder]::new()
        [void]$sb.AppendLine('# SHP-009 — tai lieu mau')
        for ($i = 0; $i -le 15; $i++) {
            if ($BoMuc -contains $i) { continue }
            [void]$sb.AppendLine("## $i. Muc $i")
            if ($i -eq 1) { [void]$sb.AppendLine('| **Business Rules** | • **BR-SHP-009-001**: a<br>• **BR-SHP-009-002**: b |') }
            if ($i -eq 14) {
                [void]$sb.AppendLine('| ID | Muc tieu | Loai | Input | Expected | Phu |')
                [void]$sb.AppendLine('|---|---|---|---|---|---|')
                [void]$sb.AppendLine('| TC-01 | x | Unit | a | b | BR-01 |')
                [void]$sb.AppendLine("| TC-02 | y | Unit | a | b | $PhuBr2 |")
            }
            if ($i -eq 15) { [void]$sb.AppendLine('| **Q1** | hoi | Dev | 🟠 **`Open`** | x |') }
        }
        if ($DoiChieu -eq 'bang') {
            [void]$sb.AppendLine('## Đối chiếu mô hình Nhanh (14/11/2026)')
            [void]$sb.AppendLine('| D-xx | Cho lech | Da sua |')
            [void]$sb.AppendLine('|---|---|---|')
            [void]$sb.AppendLine('| D-08 | 7 trang thai | 4 truc |')
        }
        elseif ($DoiChieu -eq 'khong-lech') {
            [void]$sb.AppendLine('## Đối chiếu mô hình Nhanh (14/11/2026)')
            [void]$sb.AppendLine('Đã đối chiếu, không lệch.')
        }
        elseif ($DoiChieu -eq 'rong') {
            [void]$sb.AppendLine('## Đối chiếu mô hình Nhanh (14/11/2026)')
            [void]$sb.AppendLine('')
        }
        return $sb.ToString()
    }

    Describe 'tools/agents/check-feature-doc.ps1 — tai lieu feature 16 muc + BR co test + doi chieu Nhanh' {

        It 'A1) Tai lieu du ⇒ exit 0 (🛡️ chong cong LUON DO)' {
            $p = TaoTepMau 'shp-009-mau.md' (TaiLieuMau)
            $k = ChayScript $ckDoc @('-Path', $p, '-WP14')
            BangNhau $k.Ma 0 "Tai lieu du phai DAT — log: $(Get-Content $k.Log -Raw)"
            KhangDinh ((Get-Content $k.Log -Raw) -match "1 dong cau hoi 'Open'") 'Phai dem duoc 1 cau hoi Open o §15'
        }

        It 'A2) Thieu heading ## 7. ⇒ exit 1 va NEU TEN muc thieu' {
            $p = TaoTepMau 'shp-009-thieu7.md' (TaiLieuMau -BoMuc @(7))
            $k = ChayScript $ckDoc @('-Path', $p)
            BangNhau $k.Ma 1 'Thieu heading phai KHONG DAT'
            KhangDinh ((Get-Content $k.Log -Raw).Contains('## 7.')) 'Log phai neu "## 7."'
        }

        It 'A3) BR §1 khong co o §14 ⇒ exit 1 va NEU MA BR (🛡️ BR khong test = loi hua)' {
            $p = TaoTepMau 'shp-009-br.md' (TaiLieuMau -PhuBr2 'NFR-01')
            $k = ChayScript $ckDoc @('-Path', $p)
            BangNhau $k.Ma 1 'BR chua phu phai KHONG DAT'
            KhangDinh ((Get-Content $k.Log -Raw).Contains('BR-SHP-009-002')) 'Log phai neu BR-SHP-009-002'
        }

        It 'A4) -WP14 ma KHONG co muc doi chieu ⇒ exit 1; khong -WP14 ⇒ exit 0' {
            $p = TaoTepMau 'shp-009-khongdc.md' (TaiLieuMau -DoiChieu 'khong-co')
            BangNhau (ChayScript $ckDoc @('-Path', $p, '-WP14')).Ma 1 '-WP14 thieu muc doi chieu'
            BangNhau (ChayScript $ckDoc @('-Path', $p)).Ma 0 'Khong -WP14 thi khong doi muc doi chieu'
        }

        It 'A5) Muc doi chieu RONG ⇒ exit 1; ghi "khong lech" ⇒ exit 0' {
            BangNhau (ChayScript $ckDoc @('-Path', (TaoTepMau 'shp-009-rong.md' (TaiLieuMau -DoiChieu 'rong')), '-WP14')).Ma 1 'Muc rong'
            BangNhau (ChayScript $ckDoc @('-Path', (TaoTepMau 'shp-009-kl.md' (TaiLieuMau -DoiChieu 'khong-lech')), '-WP14')).Ma 0 'Khong lech'
        }

        It 'A6) Tep khong ton tai ⇒ exit 2 (khac exit 1 "khong dat")' {
            BangNhau (ChayScript $ckDoc @('-Path', (Join-Path $thuMucTam 'khong-co.md'))).Ma 2 'Tep khong ton tai'
        }
    }

    Describe 'tools/agents/check-agent-artifact.ps1 — verdict / P1 / D1–D7 / cach lui' {

        $daKiem = "## Đã kiểm`n| Nhóm | Cách | Kết quả |`n|---|---|---|`n| D1 | a | ok |`n| D2 | a | ok |`n| D3 | a | ok |`n| D4 | a | chưa chạy |`n| D5 | a | ok |`n| D6 | a | ok |`n| D7 | a | ok |`n"

        It 'B1) docreview DOC-OK, du D1–D7, 0 P1 ⇒ exit 0' {
            $p = TaoTepMau 'SHP-009-docreview.md' ("# Doc review`n**VERDICT: DOC-OK**`n$daKiem## Phát hiện`n| # | Mức | Vị trí | Vấn đề | Đề nghị |`n|---|---|---|---|---|`n| 1 | P3 | §2 | chính tả | sửa |`n")
            $k = ChayScript $ckArt @('-Kind', 'docreview', '-Path', $p)
            BangNhau $k.Ma 0 "docreview hop le — log: $(Get-Content $k.Log -Raw)"
        }

        It 'B2) docreview DOC-OK nhung con P1 ⇒ exit 1 (🛡️ verdict noi doi)' {
            $p = TaoTepMau 'SHP-009-docreview-p1.md' ("# Doc review`nVERDICT: DOC-OK`n$daKiem## Phát hiện`n| # | Mức | Vị trí | Vấn đề | Đề nghị |`n|---|---|---|---|---|`n| 1 | **P1** | §8 | khoá không tên | ghi tên |`n")
            BangNhau (ChayScript $ckArt @('-Kind', 'docreview', '-Path', $p)).Ma 1 'DOC-OK + P1'
        }

        It 'B3) docreview thieu nhom D7 ⇒ exit 1' {
            $p = TaoTepMau 'SHP-009-docreview-d7.md' ("VERDICT: DOC-CHANGES`n" + $daKiem.Replace('| D7 | a | ok |', ''))
            BangNhau (ChayScript $ckArt @('-Kind', 'docreview', '-Path', $p)).Ma 1 'Thieu D7'
        }

        It 'B4) review: PASS co P1 ⇒ exit 1; CHANGES co P1 ⇒ exit 0' {
            $bang = "| # | Mức | File:dòng | Vấn đề | Đề nghị |`n|---|---|---|---|---|`n| 1 | P1 | a.cs:1 | lộ token | bỏ |`n"
            BangNhau (ChayScript $ckArt @('-Kind', 'review', '-Path', (TaoTepMau 'r1.md' "VERDICT: PASS`n$bang"))).Ma 1 'PASS + P1'
            BangNhau (ChayScript $ckArt @('-Kind', 'review', '-Path', (TaoTepMau 'r2.md' "VERDICT: CHANGES`n$bang"))).Ma 0 'CHANGES + P1'
        }

        It 'B5) decision: dong DEC du "④ Lùi" + "⑤ Xem lại trước" ⇒ 0; thieu ⑤ ⇒ 1' {
            $du = "| D-80 | **x** (DEC, 18/11/2026, phiên N22/ADDR-001) | ① Chọn: a ② Vì: T1 ③ Nguồn: D-08 ④ Lùi: git revert abc ⑤ Xem lại trước: release ⑥ Đỏ khi vi phạm: TC-01 | TẠM CHỐT (DEC) |"
            BangNhau (ChayScript $ckArt @('-Kind', 'decision', '-Path', (TaoTepMau 'd1.md' "| ID | QĐ | Giá trị | TT |`n|---|---|---|---|`n$du`n"))).Ma 0 'Dong DEC du'
            $thieu = $du.Replace('⑤ Xem lại trước: release ', '')
            BangNhau (ChayScript $ckArt @('-Kind', 'decision', '-Path', (TaoTepMau 'd2.md' "$thieu`n"))).Ma 1 'Thieu xem lai truoc'
        }

        It 'B6) testplan: du BR + negative control do→xanh ⇒ 0; thieu BR ⇒ 1' {
            $srs = TaoTepMau 'shp-009-srs.md' (TaiLieuMau)
            $nc = "## Negative control`n| Ca | Mutation | Kết quả mutation | Sau khôi phục |`n|---|---|---|---|`n| NC-1 | bỏ kiểm | exit 1 — Failed: 1 | exit 0 — Passed |`n"
            BangNhau (ChayScript $ckArt @('-Kind', 'testplan', '-Path', (TaoTepMau 't1.md' "BR-SHP-009-001 BR-SHP-009-002`n$nc"), '-Srs', $srs)).Ma 0 'Testplan du'
            BangNhau (ChayScript $ckArt @('-Kind', 'testplan', '-Path', (TaoTepMau 't2.md' "BR-SHP-009-001`n$nc"), '-Srs', $srs)).Ma 1 'Testplan thieu BR-002'
        }
    }

    Describe 'tools/agents/negative-control.ps1 — dot bien DO -> khoi phuc XANH -> ghi bang chung' {

        $nc = Join-Path $PSScriptRoot 'agents/negative-control.ps1'

        # Repo git tam: negative-control tu choi tep chua commit => ca thu phai co repo that.
        $repoNc = Join-Path $thuMucTam ('nc-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $repoNc | Out-Null
        & git -C $repoNc init -q
        & git -C $repoNc config user.email 't@t'
        & git -C $repoNc config user.name 't'

        $tepNc = Join-Path $repoNc 'target.txt'
        [IO.File]::WriteAllText($tepNc, "OK`n", [Text.UTF8Encoding]::new($false))
        $probeNc = Join-Path $repoNc 'probe.ps1'
        [IO.File]::WriteAllText($probeNc, "param([string]`$File)`n`$t = [IO.File]::ReadAllText(`$File, [Text.Encoding]::UTF8)`nif (`$t.Contains('BROKEN')) { Write-Output 'probe: BROKEN => DO'; exit 1 }`nWrite-Output 'probe: XANH'; exit 0`n", [Text.UTF8Encoding]::new($false))
        $planNc = Join-Path $repoNc 'testplan.md'
        [IO.File]::WriteAllText($planNc, "## Negative control`n| Ngay | Ma | Tep:dong | Dot bien | Lenh test | Bang chung |`n|---|---|---|---|---|---|`n", [Text.UTF8Encoding]::new($false))
        & git -C $repoNc add -A
        & git -C $repoNc commit -q -m init

        $lenhNc = 'pwsh -NoProfile -File "{0}" "{1}"' -f $probeNc, $tepNc

        It 'C1) dot bien lam test DO -> khoi phuc XANH => exit 0 + testplan co 2 dong exit' {
            $k = ChayScript $nc @('-File', $tepNc, '-Find', 'OK', '-Replace', 'BROKEN', '-TestCommand', $lenhNc, '-Testplan', $planNc, '-Id', 'NC-1')
            BangNhau $k.Ma 0 "dot bien do roi xanh phai DAT — log: $(Get-Content $k.Log -Raw)"
            $plan = Get-Content $planNc -Raw
            KhangDinh ($plan -match 'exit\s+1\s+\(do\)') 'Testplan phai co "exit 1 (do)"'
            KhangDinh ($plan -match 'exit\s+0\s+\(xanh\)') 'Testplan phai co "exit 0 (xanh)"'
            BangNhau ([IO.File]::ReadAllText($tepNc).Trim()) 'OK' 'Tep dich phai ve nguyen trang'
        }

        It 'C2) tep dich chua commit => exit 3 (khong dot bien tren ban dang sua)' {
            [IO.File]::WriteAllText($tepNc, "OK dirty`n", [Text.UTF8Encoding]::new($false))
            try {
                BangNhau (ChayScript $nc @('-File', $tepNc, '-Find', 'OK', '-Replace', 'BROKEN', '-TestCommand', $lenhNc)).Ma 3 'Tep ban phai bi tu choi'
            }
            finally {
                & git -C $repoNc checkout -- target.txt
            }
        }

        It 'C3) dot bien KHONG lam test do => exit 2 va khoi phuc (🛡️ chong test rong)' {
            $k = ChayScript $nc @('-File', $tepNc, '-Find', 'OK', '-Replace', 'OK!', '-TestCommand', $lenhNc)
            BangNhau $k.Ma 2 'Mutation khong lam do phai KHONG DAT'
            KhangDinh ((Get-Content $k.Log -Raw) -match 'KHONG lam test do') 'Log phai noi ro mutation khong lam do'
            BangNhau ([IO.File]::ReadAllText($tepNc).Trim()) 'OK' 'Tep dich phai ve nguyen trang'
        }
    }
}

# ── TONG KET ─────────────────────────────────────────────────────────────────────────────────
Remove-Item -Recurse -Force $thuMucTam -ErrorAction SilentlyContinue

$soDat = @($ketQua | Where-Object { $_.Dat }).Count
$soHong = $ketQua.Count - $soDat

Write-Output ''
Write-Output ("════════ TEST CONG KIEM: {0}/{1} DAT · {2} HONG ════════" -f $soDat, $ketQua.Count, $soHong)

if ($soHong -gt 0) {
    Write-Output ''
    foreach ($k in ($ketQua | Where-Object { -not $_.Dat })) {
        Write-Output ("   🚨 [{0}] {1}" -f $k.Nhom, $k.Ten)
        Write-Output ("      {0}" -f $k.Loi)
    }
}

exit $(if ($soHong -gt 0) { 1 } else { 0 })
