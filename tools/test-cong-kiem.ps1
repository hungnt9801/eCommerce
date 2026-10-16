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
