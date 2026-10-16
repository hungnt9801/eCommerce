<#
.SYNOPSIS
    🚨 KIEM BAT BIEN TRANG THAI DB — cong thu 6 cua `tools\kiem-tat-ca.ps1`.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VI SAO SCRIPT NAY TON TAI:

    Moi vong lam viec, toi van **kiem DB bang tay** (5-6 cau `psql`) truoc khi commit. ⚠️ Va do la
    **buoc de quen nhat** — 📌 quen no thi *"tat ca xanh"* nhung **du lieu thu nghiem con nam trong DB**,
    va lan sau mot phep do se **doc phai chung** roi ket luan sai.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🚨🚨 **RANH GIOI BAT BIEN — BAI HOC QUAN TRONG NHAT CUA SCRIPT NAY:**

    **CO nhung bang PHAI RONG** (chung la **dau vet thu nghiem**):
      `AbpSettings` · `CusCustomers` · `CusGroups` · `CatProductMedia` · `CatProductChannelVisibilities` ·
      `MsgDeadLetters` · `SalMarketplaceOrderIntakes` · `InvChannelLimits` · `PayTransactions` ·
      `ReconRunMismatches`

    **CO nhung bang PHAI GIU LICH SU** (🚨 **KHONG** duoc coi la rac):
      `ReconRuns` — **nhat ky luot quet doi soat**. 📌 Job `InventoryReconciliationWorker` ghi **mot dong
      moi nhịp** ⇒ con so **tang dan la DUNG**. 🔒 Xoa no la **xoa bang chung kiem toan** — va te hon:
      mot phep kiem *"phai rong"* dat len bang nay se **DO** sau vai gio chay job, roi nguoi ta se
      **vo hieu hoa phep kiem** (hoac te hon: **xoa du lieu that** de cho no xanh).

    🔑 **BAI HOC CHUNG:** mot phep kiem *"bang phai rong"* chi dung voi bang **khong co y nghia nghiep vu**.
    ⚠️ Ap no len bang **co lich su** la **tu tao ra mot cong luon do** — 📌 va **cong luon do thi nguoi ta
    hoc cach bo qua no** (cung bai hoc §23.4 cua `sal-006`).

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔒 **NAM NHOM BAT BIEN:**

    **① BANG PHAI RONG** — va kiem **CA** ban ghi **XOA MEM** (`IsDeleted`). 📌 Mot dong xoa mem **khong**
    hien ra trong truy van thuong, nhung **van chiem** unique index `NULLS NOT DISTINCT` ⇒ lan gieo sau
    se **that bai** vi **ly do kho hieu**. 🔑 Nen phai dem **TONG**, khong chi dem *"con song"*.

    **② CO BAO MAT** — `AbpUsers.NormalizedUserName = 'ADMIN'` phai co
    `ShouldChangePasswordOnNextLogin = true`. 🚨 Phep kiem probe **tat co nay** de dang nhap duoc ⇒
    🔒 **quen bat lai** nghia la **commit mot tai khoan admin khong bi buoc doi mat khau**.

    **③ SO UNIQUE INDEX `NULLS NOT DISTINCT`** — 📌 con so nay **phai khop** voi `PostgreSqlSchemaTests`.
    ⚠️ Nhung **KHONG** viet cung `48` o day: 🔑 mot con so cung **trong script** se **lech** voi con so
    trong **test** ⇒ hai nguon su that. ✅ Doc tu **chinh test** (xem `-DocTuTest`).

    **④ QUYEN PHai CON** — `eCommerce.Permissions` (goc) va vai quyen **da tung bi mat** trong qua khu.
    🔒 Phep kiem quyen la **co so** cho `WEB-001` (menu theo quyen) va cho `BatBienPhanQuyenTests`.

    **⑤ DAU VET THU NGHIEM** — moi tien to ma probe dung (`K9`, `K97`) **khong** duoc con dong nao.
    📌 Day la phep kiem **manh nhat** vi no bat duoc rac o **moi** bang co cot ma, khong chi 10 bang tren.

.PARAMETER HostPg
    Ten container PostgreSQL (mac dinh `ecommerce-postgres`).

.PARAMETER TenDb
    Ten database (mac dinh `ecommerce`).

.PARAMETER DocTuTest
    🔑 Doc so `NULLS NOT DISTINCT` **tu tep test** thay vi viet cung ⇒ mot nguon su that.
    📌 Mac dinh: `test\eCommerce.IntegrationTests\PostgreSqlSchemaTests.cs`.

.PARAMETER ChoPhepRong
    ⚠️ Cho phep cac bang "phai rong" **co du lieu** (in **CANH BAO** thay vi that bai).
    🔒 Dung khi can chay tren DB **dang phat trien** — ⚠️ **KHONG** dung truoc khi commit.

.EXAMPLE
    pwsh -File tools\kiem-db.ps1
#>
[CmdletBinding()]
param(
    [string]$HostPg = 'ecommerce-postgres',
    [string]$TenDb = 'ecommerce',
    [string]$DocTuTest = 'test\eCommerce.IntegrationTests\PostgreSqlSchemaTests.cs',
    [switch]$ChoPhepRong
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot

# 🚨 **TAP HOP BAT BIEN — HAI NHOM KHAC NHAU, VA SU KHAC NHAU LA CO Y.**
# 📌 Nhom ①: dau vet THU NGHIEM ⇒ phai RONG.
$bangPhaiRong = @(
    'AbpSettings',                     # setting doi trong probe (doi chinh sach) ⇒ phai xoa
    'CusCustomers',                    # probe CUS-001 gieo khach
    'CusGroups',
    'CatProductMedia',                 # probe CAT-003 gieo media
    'CatProductChannelVisibilities',   # probe CAT-003 gieu hien thi theo kenh
    'MsgDeadLetters',                  # probe XC-002 day vao DLQ
    'SalMarketplaceOrderIntakes',      # probe SAL-005 gieo don san
    'InvChannelLimits',                # probe INV-003 gieo han muc
    'PayTransactions',                 # probe REC-001 gieo giao dich
    'ReconRunMismatches'               # dong lech cua luot quet
)

# 📌 Nhom ②: bang GIU LICH SU ⇒ 🚨 **KHONG** kiem "phai rong". 📌 Liet ke ra day de **ghi ro y dinh**.
$bangGiuLichSu = @(
    'ReconRuns'    # 🔑 nhat ky luot quet doi soat — CON SO TANG DAN LA DUNG
)

$ketQua = [System.Collections.Generic.List[object]]::new()

function ThemKq {
    param([string]$Nhom, [string]$Ten, [bool]$Dat, [string]$ChiTiet)
    $ketQua.Add([pscustomobject]@{ Nhom = $Nhom; Ten = $Ten; Dat = $Dat; ChiTiet = $ChiTiet })
}

function Psql {
    param([string]$Sql)

    # 🔒 Ghi ra **tep tam** roi doc — ⚠️ **KHONG** ong danh: ong danh che mat `exit code` cua `docker`.
    # 📌 Cung quy tac nhu `tools\kiem-tat-ca.ps1` (bai hoc `sal-006` §23.2).
    $tam = [System.IO.Path]::GetTempFileName()
    $Sql | docker exec -i $HostPg psql -U ecommerce -d $TenDb -t -A -f - *> $tam
    $ma = $LASTEXITCODE

    $dong = @(Get-Content $tam -ErrorAction SilentlyContinue | Where-Object { $_.Trim() -ne '' })
    Remove-Item $tam -Force -ErrorAction SilentlyContinue

    if ($ma -ne 0) {
        throw ("psql THAT BAI (exit {0}) voi cau: {1}" -f $ma, $Sql.Trim())
    }

    return $dong
}

# ── TIEN DE: ket noi duoc khong? ────────────────────────────────────────────────────────────
Write-Output '=== KIEM BAT BIEN DB ==='

try {
    $phienBan = (Psql 'select version();') -join ' '
}
catch {
    Write-Output ("🚨 KHONG ket noi duoc PostgreSQL ({0}:{1})." -f $HostPg, $TenDb)
    Write-Output ("   {0}" -f $_.Exception.Message)
    Write-Output '⇒ PHEP KIEM VO NGHIA (⚠️ KHONG duoc coi la "DB sach").'
    exit 2
}

Write-Output ("   PostgreSQL: {0}" -f $phienBan.Split(',')[0])

# ── ① BANG PHAI RONG (dem CA ban ghi XOA MEM) ───────────────────────────────────────────────
Write-Output ''
Write-Output '── ① Bang phai RONG (dau vet thu nghiem) ──'

foreach ($bang in $bangPhaiRong) {
    # 🔒 Kiem cot `IsDeleted` TRUOC: 📌 bang khong co cot do thi `count(*) filter (...)` se **LOI SQL**,
    # ⚠️ va mot loi SQL o day de bi hieu nham la *"bang co du lieu"*.
    $coIsDeleted = ((Psql ("select column_name from information_schema.columns where table_name='{0}' and column_name='IsDeleted';" -f $bang)) -join '') -eq 'IsDeleted'

    $sql = if ($coIsDeleted) {
        # 📌 In **ca ba** con so: TONG | CON SONG | XOA MEM. 🔑 Mot dong xoa mem **khong** hien trong truy
        # van thuong nhung **van chiem** unique index `NULLS NOT DISTINCT` ⇒ lan gieo sau that bai.
        'select count(*)::text || ''|'' || (count(*) filter (where not "IsDeleted"))::text || ''|'' || (count(*) filter (where "IsDeleted"))::text from "{0}";' -f $bang
    }
    else {
        'select count(*)::text from "{0}";' -f $bang
    }

    $dong = Psql $sql
    $phan = ($dong -join '').Split('|')
    $tong = [int]$phan[0]

    $chiTiet = if ($coIsDeleted) {
        "TONG={0} · conSong={1} · xoaMem={2}" -f $phan[0], $phan[1], $phan[2]
    }
    else {
        "TONG={0} (khong co IsDeleted)" -f $phan[0]
    }

    $dat = ($tong -eq 0)
    ThemKq '① phai rong' $bang $dat $chiTiet

    $nhan = if ($dat) { '✅' } else { if ($ChoPhepRong) { '⚠️' } else { '🚨' } }
    Write-Output ("   {0} {1,-32} {2}" -f $nhan, $bang, $chiTiet)
}

# ── ② CO BAO MAT CUA ADMIN ─────────────────────────────────────────────────────────────────
Write-Output ''
Write-Output '── ② Co bao mat cua admin ──'

$admin = (Psql 'select "NormalizedUserName" || ''|'' || "ShouldChangePasswordOnNextLogin"::text from "AbpUsers" where "NormalizedUserName" = ''ADMIN'';') -join ''
$adminDat = ($admin -eq 'ADMIN|true')

ThemKq '② bao mat' 'ADMIN.ShouldChangePasswordOnNextLogin' $adminDat $(if ($admin -eq '') { 'KHONG thay tai khoan ADMIN' } else { $admin })
Write-Output ("   {0} ADMIN = {1}" -f $(if ($adminDat) { '✅' } else { '🚨' }), $(if ($admin -eq '') { '(khong thay)' } else { $admin }))

if (-not $adminDat) {
    Write-Output '      ⇒ 🚨 Probe TAT co nay de dang nhap duoc ⇒ 🔒 quen bat lai = commit mot admin KHONG bi buoc doi mat khau.'
}

# ── ③ SO UNIQUE INDEX `NULLS NOT DISTINCT` (doc tu TEST — mot nguon su that) ────────────────
Write-Output ''
Write-Output '── ③ Unique index NULLS NOT DISTINCT (doi chieu voi test) ──'

$nndThucTe = [int](((Psql 'select count(*)::text from pg_indexes where schemaname=''public'' and indexdef like ''%NULLS NOT DISTINCT%'';') -join '').Trim())

$tepTest = Join-Path $goc $DocTuTest
$nndTuTest = $null

if (Test-Path $tepTest) {
    # 🔑 Doc so tu **chinh test** thay vi viet cung ⇒ 📌 mot nguon su that. ⚠️ Viet cung `48` o day se
    # **lech** voi con so trong test khi schema doi ⇒ hai nguon su that, va nguoi sua se **sua nham cho**.
    # 🚨 **DOC SO TU DONG `Assert.Equal` CUA CA F1** — khong doan theo tu khoa.
    # 📌 Ban dau toi loc theo chuoi `NND`/`NullsNotDistinct`, ⚠️ va **khong khop** vi dong khao sat nam o
    # `Assert.Equal(48L, await DemAsync(...)` cach xa dong ten ca. 🔒 Ket qua: script **im lang** chuyen sang
    # che do *"chi in so thuc te"* ⇒ 📌 **phep kiem tu yeu di ma khong ai biet** — dung loai hong ma ca
    # du an dang chong.
    # ✅ Cach doc **chac chan**: tim dong `Assert.Equal(<so>L?, ...)` **trong ca co ten chua `NND`/`NULLS NOT
    # DISTINCT`** ⇒ 🔑 gan chat so voi **chinh ca kiem** thay vi voi mot tu khoa co the doi.
    $dongTest = Get-Content $tepTest
    $viTriCa = -1

    for ($k = 0; $k -lt $dongTest.Count; $k++) {
        if ($dongTest[$k] -match 'public\s+async\s+Task\s+\w*(NND|NULLS_NOT_DISTINCT|NullsNotDistinct)\w*') {
            $viTriCa = $k
            break
        }
    }

    if ($viTriCa -ge 0) {
        # Tim `Assert.Equal(<so>...` trong vong 40 dong ke tu ten ca.
        for ($k = $viTriCa; $k -lt [Math]::Min($viTriCa + 40, $dongTest.Count); $k++) {
            $m = [regex]::Match($dongTest[$k], 'Assert\.Equal\(\s*(\d+)\s*L?\s*,')

            if ($m.Success) {
                $nndTuTest = [int]$m.Groups[1].Value
                break
            }
        }
    }
}

if ($null -eq $nndTuTest) {
    # ⚠️ **KHONG** that bai vi khong doc duoc so tu test — nhung phai **NOI RO** la dang so sanh voi gi.
    Write-Output ("   ⚠️ Khong doc duoc so ky vong tu {0} ⇒ chi IN so thuc te (khong ket luan)." -f $DocTuTest)
    Write-Output ("      NND thuc te = {0}" -f $nndThucTe)
    ThemKq '③ nnd' 'NND (khong co so doi chieu)' $true ("thucTe={0}" -f $nndThucTe)
}
else {
    $dat = ($nndThucTe -eq $nndTuTest)
    ThemKq '③ nnd' 'NND' $dat ("thucTe={0} · tuTest={1}" -f $nndThucTe, $nndTuTest)
    Write-Output ("   {0} NND thucTe={1} · tuTest={2}" -f $(if ($dat) { '✅' } else { '🚨' }), $nndThucTe, $nndTuTest)
}

# ── ④ QUYEN PHAI CON ───────────────────────────────────────────────────────────────────────
Write-Output ''
Write-Output '── ④ Quyen phai con (co so cho WEB-001 + BatBienPhanQuyenTests) ──'

# 🔑 Danh sach nay la **co so** cho `WEB-001` (menu theo quyen): 📌 thieu mot quyen o day thi **ca mot
# nhom menu bien mat** — va no bien mat **im lang** (menu chi don gian la khong hien).
$quyenPhaiCon = @(
    'eCommerce.Pricing',            # 🔒 quyen goc — da tung bi mat va da co lan phai sua
    'eCommerce.Operations',         # XC-002 DLQ
    'eCommerce.Operations.DeadLetters',
    'eCommerce.ChannelLimits',      # INV-003 (muc 1 cua goi ban giao)
    'eCommerce.ChannelLimits.Manage',
    'eCommerce.Customers',          # CUS-001
    'eCommerce.Payments',           # REC-001 / SAL-006
    'eCommerce.Payments.Refund',
    'eCommerce.Inventory'           # INV-001/002/003
)

$granted = @(Psql 'select "Name" from "AbpPermissionGrants" order by "Name";' | ForEach-Object { $_.Trim() })

# 🛡️ LUOI AN TOAN CHO CHINH PHEP KIEM: neu doc duoc **0** quyen thi phep kiem VO NGHIA (khong phai "thieu quyen").
if ($granted.Count -eq 0) {
    Write-Output '   🚨 Doc duoc 0 quyen ⇒ PHEP KIEM VO NGHIA (⚠️ khong duoc coi la "thieu quyen").'
    ThemKq '④ quyen' 'Danh sach quyen' $false 'doc duoc 0 dong'
}
else {
    $thieu = @($quyenPhaiCon | Where-Object { $_ -notin $granted })

    ThemKq '④ quyen' 'Quyen bat buoc phai con' ($thieu.Count -eq 0) ("co={0} · can={1} · thieu={2}" -f $granted.Count, $quyenPhaiCon.Count, $thieu.Count)

    Write-Output ("   {0} co {1} quyen · can {2} · thieu {3}" -f $(if ($thieu.Count -eq 0) { '✅' } else { '🚨' }), $granted.Count, $quyenPhaiCon.Count, $thieu.Count)
    foreach ($q in $thieu) { Write-Output ("      🚨 THIEU: {0}" -f $q) }
}

# ── ⑤ DAU VET THU NGHIEM (moi tien to probe) ────────────────────────────────────────────────
Write-Output ''
Write-Output '── ⑤ Dau vet thu nghiem (tien to probe: K9 · K97) ──'

# 🔑 Phep kiem nay **manh nhat** vi no bat duoc rac o **MOI** bang co cot ma — 📌 khong chi 10 bang o nhom ①.
# ⚠️ Ten bang/cot phai la **that** — 🚨 doan ten cot la loi da tra gia (bai hoc `CAT-003` §16.6 #2 va
# `REC-001` #14): `CatProducts` dung `Code` **khong** phai `SKU`.
$dauVet = @(
    @{ Bang = 'PayTransactions'; Cot = 'MerchantReference' },
    @{ Bang = 'InvChannelLimits'; Cot = 'ChannelCode' },
    @{ Bang = 'CatProducts'; Cot = 'Code' },
    @{ Bang = 'CatProducts'; Cot = 'Slug' },
    @{ Bang = 'CatProductVariants'; Cot = 'Sku' },
    @{ Bang = 'CusCustomers'; Cot = 'Name' },
    @{ Bang = 'MsgDeadLetters'; Cot = 'TenSuKien' }
)

$tongDauVet = 0

foreach ($dv in $dauVet) {
    # 📌 `lower(...)` de bat ca `K9` lan `k9` — 🔑 probe tung dung **ca hai** cach viet.
    $sql = 'select count(*)::text from "{0}" where lower("{1}") like ''k9%'';' -f $dv.Bang, $dv.Cot
    $so = [int](((Psql $sql) -join '').Trim())
    $tongDauVet += $so

    $dat = ($so -eq 0)
    ThemKq '⑤ dau vet' ($dv.Bang + '.' + $dv.Cot) $dat ("soDong={0}" -f $so)

    if ($so -ne 0) {
        Write-Output ("   🚨 {0}.{1} = {2} dong ⇒ RAC TU PROBE (phai xoa truoc khi commit)." -f $dv.Bang, $dv.Cot, $so)
    }
}

if ($tongDauVet -eq 0) {
    Write-Output '   ✅ Khong con dong nao mang tien to probe (K9/k9).'
}

# ── ⑥ TU DO DAU VET PROBE — QUET MOI BANG CO KHOA CHINH `uuid` ──────────────────────────────
Write-Output ''
Write-Output '── ⑥ Tu do dau vet probe (moi bang co PK uuid) ──'

# ══════════════════════════════════════════════════════════════════════════════════════════════
# 🔑 **VI SAO CO NHOM NAY — VA NO SUA MOT NO THAT CUA CHINH SCRIPT NAY:**
#
# Nhom ⑤ o tren quet mot **DANH SACH VIET TAY** (7 cap bang/cot). ⚠️ Danh sach do **lech** khi schema
# doi: 🔑 bang **moi** them se **khong** duoc quet, va 📌 **khong ai biet** — phep kiem van XANH.
# 📌 Cung **mot ho no** nhu con so `48` viet cung (da sua o nhom ③) va danh sach "phai rong" (nhom ①):
# **danh sach thu cong se lech, va lech thi IM LANG.**
#
# ✅ **CACH SUA — TU DO thay vi liet ke:** moi dong probe deu duoc gieo bang mot **GUID trong khoi danh rieng**
# `e0000000-0000-0000-0000-0000000000XX` (xem `tools\README-probe.md`). 🔑 Do la **dau hieu chung** khong
# phu thuoc ten bang/cot ⇒ quet **MOI** cot khoa chinh kieu `uuid` la bat duoc **tat ca**.
#
# 📌 **VI SAO QUET COT KHOA CHINH (khong quet moi cot uuid):**
#   • **Re:** moi bang co PK uuid deu nam trong mot lan quet index ⇒ **re**;
#   • **Dung:** dong probe **luon** duoc gieo bang `Id` tuong minh (xem cac probe PowerShell) ⇒ PK la
#     **dau hieu dung**;
#   • **Khong nhieu:** quet **moi** cot uuid (FK, `CreatorId`, `TenantId`…) se bat ca dong **that** tro toi
#     dong probe (⚠️ nhieu gia) va lam cham.
#   ⚠️ **Danh doi da biet:** mot dong probe gieo **khong** co `Id` tuong minh (de DB sinh) se **khong** bi
#   bat boi nhom nay — 📌 nhung nhom ⑤ (tien to ma) van bat no **neu** bang do co cot ma.
# ══════════════════════════════════════════════════════════════════════════════════════════════════

# 📌 Dung **chinh `Psql`** (khong phai `docker exec` truc tiep) ⇒ 🔒 cung mot duong ong dan, cung mot quy tac
# "ghi ra tep tam roi doc" — ⚠️ ong danh se che mat `exit code`.
$cotPkUuid = @(Psql @'
select c.table_name || '|' || c.column_name
from information_schema.columns c
join information_schema.table_constraints tc
  on tc.table_schema = c.table_schema and tc.table_name = c.table_name and tc.constraint_type = 'PRIMARY KEY'
join information_schema.key_column_usage kcu
  on kcu.constraint_schema = tc.constraint_schema
 and kcu.constraint_name = tc.constraint_name
 and kcu.column_name = c.column_name
where c.table_schema = 'public' and c.data_type = 'uuid'
order by c.table_name;
'@ | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })

# 🛡️ LUOI AN TOAN CHO CHINH PHEP KIEM: neu phep do metadata tra ve **0** dong thi phep kiem VO NGHIA.
# ⚠️ **KHONG** duoc im lang coi la *"khong co dau vet"* — 🔑 do dung la cach mot phep kiem **tu chet**.
if ($cotPkUuid.Count -eq 0) {
    Write-Output '   🚨 Phep do metadata tra ve 0 cot PK uuid ⇒ PHEP KIEM VO NGHIA.'
    ThemKq '⑥ tu do' 'Metadata PK uuid' $false 'doc duoc 0 cot'
}
else {
    Write-Output ("   Phep do: {0} cot khoa chinh kieu uuid (quet MOI bang)." -f $cotPkUuid.Count)

    # 🔒 Ghep **MOT** cau UNION ALL: 📌 mot lan di-vong qua DB thay vi 80 lan ⇒ nhanh hon nhieu.
    # ⚠️ Ten bang/cot o day den tu **metadata cua chinh DB** (khong phai input nguoi dung) ⇒ khong phai
    # duong tiem SQL. 🔒 Van boc `""` de an toan voi ten co ky tu dac biet.
    $manh = foreach ($m in $cotPkUuid) {
        $phan = $m.Split('|')
        'select ''{0}.{1}'' as cho, count(*)::text as so from "{0}" where "{1}"::text like ''e0000000-%''' -f $phan[0], $phan[1]
    }

    $sqlTuDo = ($manh -join "`nunion all`n") + "`norder by cho;"

    $dongTuDo = @(Psql $sqlTuDo)

    $coDauVet = @()
    $soCho = 0

    foreach ($d in $dongTuDo) {
        $phan = $d.Trim().Split('|')
        if ($phan.Count -ne 2) { continue }
        $soCho++
        if ([int]$phan[1] -ne 0) { $coDauVet += [pscustomobject]@{ Cho = $phan[0]; So = [int]$phan[1] } }
    }

    # 🛡️ Luoi an toan thu hai: neu cau UNION tra ve **it** dong hon so cot thi co gi do sai ⇒ phai NOI RO.
    if ($soCho -lt $cotPkUuid.Count) {
        Write-Output ("   ⚠️ Cau quet tra ve {0}/{1} cho ⇒ co the thieu ⇒ 📌 doc ket qua DUOI day voi gian doan." -f $soCho, $cotPkUuid.Count)
    }

    if ($coDauVet.Count -eq 0) {
        Write-Output ("   ✅ Khong bang nao con dong mang GUID probe (da quet {0} cho)." -f $soCho)
        ThemKq '⑥ tu do' 'Dau vet GUID probe (moi bang PK uuid)' $true ("daQuet={0} cho" -f $soCho)
    }
    else {
        $tong = ($coDauVet | Measure-Object -Property So -Sum).Sum
        foreach ($v in $coDauVet) {
            Write-Output ("   🚨 {0} = {1} dong ⇒ RAC TU PROBE (phat hien TU DONG, khong co trong danh sach viet tay)." -f $v.Cho, $v.So)
        }
        ThemKq '⑥ tu do' 'Dau vet GUID probe (moi bang PK uuid)' $false ("{0} cho co rac · tong {1} dong" -f $coDauVet.Count, $tong)
    }
}

# ── GHI RO NHOM ② DE KHONG AI TUONG LA BO SOT ──────────────────────────────────────────────
Write-Output ''
Write-Output '── ② (ghi chu) Bang GIU LICH SU — CO Y khong kiem "phai rong" ──'
foreach ($b in $bangGiuLichSu) {
    $so = [int](((Psql ('select count(*)::text from "{0}";' -f $b)) -join '').Trim())
    Write-Output ("   📌 {0,-32} {1} dong — 🔑 CON SO TANG DAN LA DUNG (nhat ky luot quet cua job)." -f $b, $so)
}

# ── TONG KET ───────────────────────────────────────────────────────────────────────────────
$soDat = @($ketQua | Where-Object { $_.Dat }).Count
$soHong = $ketQua.Count - $soDat
$soHongNghiemTrong = if ($ChoPhepRong) { @($ketQua | Where-Object { -not $_.Dat -and $_.Nhom -ne '① phai rong' }).Count } else { $soHong }

Write-Output ''
Write-Output ("════════ DB: {0}/{1} bat bien DAT · {2} HONG ════════" -f $soDat, $ketQua.Count, $soHong)

if ($soHong -gt 0) {
    Write-Output ''
    Write-Output 'Cac bat bien HONG:'
    foreach ($k in ($ketQua | Where-Object { -not $_.Dat })) {
        Write-Output ("   🚨 [{0}] {1} :: {2}" -f $k.Nhom, $k.Ten, $k.ChiTiet)
    }
}

if ($ChoPhepRong -and $soHong -gt 0) {
    Write-Output ''
    Write-Output '⚠️ -ChoPhepRong: bang "phai rong" co du lieu duoc BO QUA ⇒ 🔒 DUNG commit voi ket qua nay.'
}

exit $(if ($soHongNghiemTrong -gt 0) { 1 } else { 0 })
