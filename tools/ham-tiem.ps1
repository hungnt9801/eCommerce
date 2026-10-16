<#
    🚨 THU VIEN TIEM — `ham-tiem.ps1` (KHONG phai script chay truc tiep).

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VI SAO FILE NAY TON TAI — VA NO CHAN MOT LOI DA VAP **HAI LAN TRONG HAI VONG**:

      • `sal-006` **§25.5** — toi tiem mot dong probe bang `INSERT` co **thoat nhay** ⇒ **sai cu phap SQL**
        ⇒ ⚠️ **dong do KHONG HE TON TAI** ⇒ nhung toi **tuong cong khong bat** va **suyt ket luan sai ve san pham**;
      • `sal-006` **§26.5 #1** — toi tiem lai loi bang `String.Replace` va **phep thay KHONG KHOP** ⇒ bo test bao
        `exit 0` ⇒ 📌 lai **suyt ket luan** *"bo test khong bat duoc gi"*. 🔒 **CACH NHAU DUNG MOT VONG.**

    🔒 **DIEM CHUNG CUA CA HAI:** phep tiem **KHONG XAY RA**, nhung **khong ai kiem** ⇒ va *"cong khong bat"*
    **trong GIONG HET NHAU** voi *"tiem khong thanh"*. 📌 Vi vay mot ca **XANH** co the xanh **vi ly do sai**.

    ✅ **CACH CHAN:** moi ham trong file nay **TU KHANG DINH** rang phep tiem **DA XAY RA** — 🔑 va **NEM** neu
    khong. 📌 Tu day, *"tiem khong thanh"* tro thanh mot **loi on ao**, khong con la mot **gia dinh ngam**.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔒 **CACH DUNG (dot-source, KHONG chay truc tiep):**

        . (Join-Path $PSScriptRoot 'ham-tiem.ps1')     # nap cac ham

        $t = TiemVaoTep -DuongDan $f -KhoiCu $cu -KhoiMoi $moi
        try { ... chay phep kiem ... } finally { KhoiPhucTep $t }

    ⚠️ **KHONG** dat tham so (`param`) o cap tep: 🔑 mot tep co `[CmdletBinding()]` o cap cao nhat se **doi
    tham so** khi bi dot-source ⇒ 📌 no **treo** hoac **loi** tuy ngu canh goi. ✅ File nay chi co **ham**.
#>

<#
.SYNOPSIS
    Tiem mot khoi ma vao tep — VA **TU KHANG DINH** phep tiem da xay ra.
.DESCRIPTION
    🔑 Tra ve mot doi tuong `{ DuongDan; NoiDungCu }` de `KhoiPhucTep` khoi phuc.
    🚨 **NEM** neu: khong tim thay khoi cu, khoi cu xuat hien **nhieu hon mot** lan, hoac **phep thay khong doi**
    noi dung tep. 📌 Tat ca deu la *"phep tiem khong xay ra"* — dung loai loi da vap **hai lan**.
.PARAMETER DuongDan
    Tep can tiem.
.PARAMETER KhoiCu
    Khoi ma GOC (phai khop **chinh xac** va **duy nhat**).
.PARAMETER KhoiMoi
    Khoi ma THAY THE.
.PARAMETER NeoXacNhan
    ⚠️ **TUY CHON nhung RAT NEN dung:** mot chuoi **phai xuat hien trong `KhoiMoi`** — 🔑 de khang dinh phep
    tiem theo **dau hieu cua chinh khoi moi**, khong chi theo *"noi dung tep doi"*.
.EXAMPLE
    $t = TiemVaoTep -DuongDan $f -KhoiCu $cu -KhoiMoi $moi -NeoXacNhan 'K97TIEM'
#>
function TiemVaoTep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$DuongDan,
        [Parameter(Mandatory)][AllowEmptyString()][string]$KhoiCu,
        [Parameter(Mandatory)][AllowEmptyString()][string]$KhoiMoi,
        [string]$NeoXacNhan = ''
    )

    if (-not (Test-Path $DuongDan)) { throw ("TiemVaoTep: KHONG thay tep [{0}]." -f $DuongDan) }

    $noiDungCu = [System.IO.File]::ReadAllText($DuongDan)

    # ⚠️ Dem so lan khop: 🔑 **nhieu hon mot** lan la mot bay that — `Replace` se thay **HET** ⇒ 📌 phép tiem
    # vuot qua y dinh, va ca kiem se do vi **ly do khac** voi dieu ta tuong.
    $soLan = ([regex]::Matches($noiDungCu, [regex]::Escape($KhoiCu))).Count

    if ($soLan -eq 0) {
        throw ("TiemVaoTep: khoi cu KHONG khop trong [{0}] ⇒ PHEP TIEM KHONG XAY RA." -f $DuongDan)
    }

    if ($soLan -gt 1) {
        throw ("TiemVaoTep: khoi cu khop {0} lan trong [{1}] ⇒ doi hoi DUY NHAT (⚠️ Replace se thay HET)." -f $soLan, $DuongDan)
    }

    $noiDungMoi = $noiDungCu.Replace($KhoiCu, $KhoiMoi)

    # 🚨 **KHANG DINH PHEP TIEM DA DOI NOI DUNG** — 🔑 day la ca chan cho §26.5 #1.
    if ($noiDungMoi -ceq $noiDungCu) {
        throw ("TiemVaoTep: noi dung tep KHONG DOI sau khi thay ⇒ PHEP TIEM KHONG XAY RA [{0}]." -f $DuongDan)
    }

    if ($NeoXacNhan -ne '') {
        # 🔑 Khang dinh theo **dau hieu cua chinh khoi moi** — manh hon *"noi dung doi"* (⚠️ noi dung co the
        # doi vi ly do khac, vi du mot phep thay khac da chay truoc).
        if ($noiDungMoi.IndexOf($NeoXacNhan, [StringComparison]::Ordinal) -lt 0) {
            throw ("TiemVaoTep: neo [{0}] KHONG co trong noi dung sau khi tiem ⇒ PHEP TIEM KHONG XAY RA [{1}]." -f $NeoXacNhan, $DuongDan)
        }
    }

    [System.IO.File]::WriteAllText($DuongDan, $noiDungMoi)

    # 🔒 **DOC LAI TU DIA** — ⚠️ khong tin bien trong bo nho: 🔑 mot tep bi **khoa** (tien trinh khac dang giu)
    # co the **ghi hut** mot cach im lang. 📌 Cung ho voi bay "build `exit 0` ma binary khong doi".
    $daGhi = [System.IO.File]::ReadAllText($DuongDan)

    if ($daGhi -cne $noiDungMoi) {
        throw ("TiemVaoTep: doc lai tu dia KHAC noi dung vua ghi ⇒ tep bi khoa / ghi hut [{0}]." -f $DuongDan)
    }

    return [pscustomobject]@{ DuongDan = $DuongDan; NoiDungCu = $noiDungCu; NeoXacNhan = $NeoXacNhan }
}

<#
.SYNOPSIS
    Khoi phuc tep sau khi tiem — VA **TU KHANG DINH** noi dung da tro ve dung ban goc.
.PARAMETER TayTiem
    Doi tuong do `TiemVaoTep` tra ve.
.PARAMETER NeoDaTiem
    ⚠️ Neu truyen: khang dinh neo **KHONG con** trong tep sau khi khoi phuc (🔑 chieu nguoc lai cua `NeoXacNhan`).
#>
function KhoiPhucTep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$TayTiem,
        [string]$NeoDaTiem = ''
    )

    [System.IO.File]::WriteAllText($TayTiem.DuongDan, $TayTiem.NoiDungCu)

    # 🚨 **KHANG DINH KHOI PHUC THAT SU** — 🔑 neu khong thi mot phep khoi phuc hong se lam **cac ca sau**
    # do vi **ly do vo hinh** (du lieu/ma ca truoc con lai) ⇒ 📌 mot test **tu lam hong chinh no**.
    $daGhi = [System.IO.File]::ReadAllText($TayTiem.DuongDan)

    if ($daGhi -cne $TayTiem.NoiDungCu) {
        throw ("KhoiPhucTep: noi dung KHONG tro ve ban goc [{0}]." -f $TayTiem.DuongDan)
    }

    if ($NeoDaTiem -ne '' -and $daGhi.IndexOf($NeoDaTiem, [StringComparison]::Ordinal) -ge 0) {
        throw ("KhoiPhucTep: neo [{0}] VAN CON sau khi khoi phuc ⇒ PHEP KHOI PHUC KHONG DAY DU [{1}]." -f $NeoDaTiem, $TayTiem.DuongDan)
    }
}

<#
.SYNOPSIS
    🚨 **TIEM DU LIEU PROBE VAO DB** — va **TU KHANG DINH** dong da vao.
.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔒 **BON QUY TAC DUOC THI HANH NGAY TRONG HAM NAY:**

    **① Cau SQL PHAI la HERE-STRING (hoac chuoi nguyen van khac), KHONG phai chuoi ghep bang `+`.**
    📌 `sal-006` §25.5: toi da viet `INSERT` voi **thoat nhay** (`''`) ben trong mot **chuoi co nhay** ⇒
    **sai cu phap SQL** ⇒ ⚠️ **dong do KHONG he ton tai** ⇒ nhung toi **tuong cong khong bat**.
    ✅ Ham nay **KIEM** dieu do bang mot **dau hieu nhan ra duoc**: mot cau SQL that su **luon** co **it nhat
    mot nhay don** (moi gia tri chuoi deu can no). ⚠️ Neu **khong** co nhay don nao thi **gan nhu chac chan**
    ta dang truyen mot chuoi da bi **lop goi an mat** — 🔑 va do la dung ca §25.5.
    📌 **KHONG** kiem *"co `''` hay khong"*: ⚠️ `''` la **cu phap HOP LE** (mot nhay don **bi thoat** trong
    literal) — 🔒 cam no se **sai** voi nhung cau dung nhu `where "Id" = ''x''`. 📌 Toi da vap dung loi nay
    khi viet ban dau ⇒ ghi lai day.

    **② `Invoke-Psql` NEM khi `psql` tra ve exit code khac 0.** 🔑 Nho vay mot cau SQL sai cu phap **KHONG**
    bi hieu nham la *"0 dong"* ⇒ 📌 va *"0 dong"* **KHONG** bi hieu nham la *"sach"*.

    **③ SAU KHI GHI, PHAI DOC LAI.** ⚠️ Neu khong khang dinh thi *"cong khong bat"* va *"tiem khong thanh"*
    **trong GIONG HET NHAU**. ✅ Truyen `-KiemSql` + `-SoDongMongDoi` ⇒ ham **tu doc lai** va **NEM** neu lech.

    **④ Cau don dep van duoc khuyen khich doc lai**, nhung co the bo qua bang `-KhongNemKhiThieuKiem`
    (📌 khi ta da biet y dinh va khong can khang dinh).
    ══════════════════════════════════════════════════════════════════════════════════════════════════
.PARAMETER Sql
    Cau `INSERT`/`UPDATE`/`DELETE`. 🔑 **HERE-STRING**.
.PARAMETER KiemSql
    🚨 Cau `SELECT` doc lai de khang dinh. 📌 Neu bo qua thi ham **CANH BAO**.
.PARAMETER SoDongMongDoi
    So dong ma `KiemSql` phai tra ve.
.PARAMETER HostPg
    Ten container PostgreSQL (mac dinh `ecommerce-postgres`).
.PARAMETER TenDb
    Ten database (mac dinh `ecommerce`).
.PARAMETER KhongNemKhiThieuKiem
    ⚠️ Cho phep bo qua `-KiemSql` **khong canh bao** (📌 dung cho cau **don dep**).
.EXAMPLE
    TiemVaoDb -Sql @'
    insert into "AbpAuditLogs" ("Id","ApplicationName","ExecutionTime","ExecutionDuration","ExtraProperties","ConcurrencyStamp")
    values ('e0000000-0000-0000-0000-0000000000b8','eCommerce',now(),1,'{}','k97b8');
    '@ -KiemSql @'
    select count(*)::text from "AbpAuditLogs" where "Id" = 'e0000000-0000-0000-0000-0000000000b8';
    '@ -SoDongMongDoi 1
#>
function TiemVaoDb {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Sql,
        [string]$KiemSql = '',
        [int]$SoDongMongDoi = -1,
        [string]$HostPg = 'ecommerce-postgres',
        [string]$TenDb = 'ecommerce',
        [switch]$KhongNemKhiThieuKiem
    )

    # ── ① CHAN "CHUOI DA BI LOP GOI AN MAT" ──────────────────────────────────────────────────
    # 🔑 Mot cau SQL that su **luon** co **it nhat mot nhay don** (moi gia tri chuoi deu can no).
    # ⚠️ Khong co nhay don nao ⇒ gan nhu chac chan chuoi da bi xu ly o lop goi (vi du `''` bi an thanh `'`).
    if (-not $Sql.Contains("'")) {
        throw ("TiemVaoDb: cau SQL KHONG co nhay don nao ⇒ gan nhu chac chan chuoi da bi LOP GOI AN MAT.`n" +
            "🚨 Day chinh la loi §25.5: dong probe KHONG HE duoc ghi, nhung ta TUONG cong khong bat.`n" +
            "⇒ Dung HERE-STRING:  @'  ...  '@  va truyen THANG vao tham so nay.")
    }

    $dong = @(Invoke-Psql -Sql $Sql -HostPg $HostPg -TenDb $TenDb)

    # ── ② DOC LAI DE KHANG DINH ──────────────────────────────────────────────────────────────
    if ($KiemSql -eq '') {
        if (-not $KhongNemKhiThieuKiem) {
            Write-Warning ("TiemVaoDb: KHONG co -KiemSql ⇒ khong khang dinh duoc dong da ghi.`n" +
                "   📌 'cong khong bat' va 'tiem khong thanh' TRONG GIONG HET NHAU ⇒ hay truyen -KiemSql.")
        }
        return $dong
    }

    if ($SoDongMongDoi -lt 0) {
        throw 'TiemVaoDb: co -KiemSql thi PHAI truyen -SoDongMongDoi (🔑 so dong mong doi).'
    }

    $ketQua = @(Invoke-Psql -Sql $KiemSql -HostPg $HostPg -TenDb $TenDb)
    $soThucTe = if ($ketQua.Count -eq 0) { 0 } else { [int](($ketQua[0]).Trim()) }

    if ($soThucTe -ne $SoDongMongDoi) {
        throw ("TiemVaoDb: KHANG DINH THAT BAI ⇒ doc lai duoc {0} dong, mong doi {1}.`n" +
            "🚨 PHEP TIEM KHONG XAY RA (hoac ghi sai cho) — 🔒 DUNG ket luan gi ve CONG khi chua khang dinh duoc." -f $soThucTe, $SoDongMongDoi)
    }

    return $dong
}

<#
.SYNOPSIS
    Chay mot cau SQL qua `docker exec` — 🔒 **ghi ra tep tam roi doc**, **KHONG** ong danh.
.DESCRIPTION
    📌 Cung quy tac nhu `kiem-tat-ca.ps1`: ⚠️ ong danh **che mat exit code** (📌 `docker` that bai se bi hieu
    nham la *"0 dong"* ⇒ 🔑 va *"0 dong"* lai bi hieu nham la *"sach"*).
#>
function Invoke-Psql {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Sql,
        [string]$HostPg = 'ecommerce-postgres',
        [string]$TenDb = 'ecommerce'
    )

    $tam = [System.IO.Path]::GetTempFileName()

    try {
        $Sql | docker exec -i $HostPg psql -U ecommerce -d $TenDb -t -A -f - *> $tam
        $ma = $LASTEXITCODE

        $dong = @(Get-Content $tam -ErrorAction SilentlyContinue | Where-Object { $_.Trim() -ne '' })

        if ($ma -ne 0) {
            throw ("Invoke-Psql: psql THAT BAI (exit {0}): {1}" -f $ma, ($dong -join ' '))
        }

        return $dong
    }
    finally {
        Remove-Item $tam -Force -ErrorAction SilentlyContinue
    }
}

<#
.SYNOPSIS
    🛡️ Chay mot ca co side effect, va **LUON** don dep — ke ca khi ca do **NEM**.
.DESCRIPTION
    🔑 Vi sao can: 📌 mot ca tiem ma **nem giua duong** se de lai du lieu/ma ⇒ lam **cac ca sau** do vi
    **ly do vo hinh** ⇒ 🔒 mot test **tu lam hong chinh no**.
.PARAMETER Than
    Khoi ca kiem.
.PARAMETER DonDep
    Khoi don dep — **luon** chay, ke ca khi `Than` nem.
.EXAMPLE
    TrongKhiDonDep { ... ca kiem ... } { ... don dep ... }
#>
function TrongKhiDonDep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock]$Than,
        [Parameter(Mandatory)][scriptblock]$DonDep
    )

    try {
        & $Than
    }
    finally {
        # ⚠️ Neu `DonDep` cung nem thi loi cua `Than` se bi **thay the** — 📌 nhung do la **danh doi dung**:
        # 🔑 mot phep don dep hong la loi **nghiem trong hon** (no lam hong cac ca sau), nen no **phai** noi len.
        & $DonDep
    }
}
