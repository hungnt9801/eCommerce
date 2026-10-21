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
        'probe-so-dia-chi.ps1' = 'buoc ⑨ so dia chi'
        'probe-khach-hang.ps1' = 'buoc ⑩ khach hang'
        'probe-thanh-toan.ps1' = 'buoc ⑪ thanh toan'
        'probe-don-san.ps1'    = 'buoc ⑫ don san (duong chay that)'
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
        return 'Lay-Curl|Lay-DuongDanBin|Lay-PidDangNghe|Test-CongDangMo|Start-' + 'TienTrinhAn'
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
