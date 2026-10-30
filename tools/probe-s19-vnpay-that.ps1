<#
.SYNOPSIS
    S19 - MOT GIAO DICH VNPAY SANDBOX THAT: do "tien da vao so" bang DB, KHONG bang loi khai.

.DESCRIPTION
    Cong bat buoc cua GD1 (ban-giao §2.1, dong 1 cua checklist §7). Probe nay tra loi DUNG mot cau:
    "mot giao dich that di qua cong sandbox VNPay co lam `PayTransactions.Status = Succeeded`
     VA `OrdOrders.PaymentStatus = Paid` khong?" — va in ra BANG CHUNG do duoc (ma giao dich do CONG cap,
    thoi diem, so tien).

    --- VI SAO PHAI LA PROBE CHAY TAY, KHONG PHAI MOT BUOC CUA CONG ---
    No can BA thu ma CI khong co: (1) credential merchant sandbox THAT, (2) mot NGUOI thanh toan tren
    trang cua VNPay, (3) Internet. ⇒ No KHONG nam trong `kiem-tat-ca.ps1` (15 buoc van phai xanh ma
    khong can mang). `README-probe` §A9: mot muc dich, mot probe.

    --- DUONG XAC NHAN: `querydr`, KHONG phai IPN ---
    VNPay CHI goi IPN toi mot URL mo ra Internet (`PAY-001` §7.7.1 su that #4). Tren may local thi
    KHONG co IPN that — bat ke credential that hay gia. Vi vay bang chung o day la `querydr` tra `00`
    (dieu kien 1 cua §2.1 cho phep: "nhan IPN that (hoac querydr tra 00)"), qua CHINH duong san pham
    `POST /api/app/payment/truy-van-trang-thai/{id}` — khong phai mot lenh curl tu che.
    ⇒ Probe nay KHONG chung minh duong IPN that; no ghi ro dieu do trong tep bang chung.

    --- CAI NO KHONG LAM ---
    * KHONG tu ky/IPN gia roi goi la "that". Moi con so trong bang chung doc tu DB sau khi CONG tra loi.
    * KHONG ghi credential vao bat ky tep nao trong repo (kiem bang `git grep -f` o cuoi — tu tep tam).
    * KHONG de lai rac: dong `%K9S9%` + don `e0000000-...c9` bi xoa trong `finally` (tru khi `-GiuLaiDuLieu`),
      va co KHANG DINH lai so dong = 0.

.PARAMETER SoTien
    So tien (VND) cua giao dich. VNPay nhan so tien theo don vi nho nhat (x100) — probe so sanh bang
    MINOR UNITS (so nguyen), khong bao gio bang `decimal ==`.

.PARAMETER ChoThanhToanGiay
    Thoi gian toi da cho nguoi van hanh thanh toan tren trang sandbox (giay). Het han ⇒ KHONG DAT.

.PARAMETER ChuKyHoiGiay
    Nhip goi `querydr` trong luc cho. 📌 Dung 20 s: du nhanh de thay ket qua, du thua de khong dot
    quota cua cong (moi lan hoi la mot loi goi ra ngoai).

.PARAMETER TimeoutGiay
    Thoi gian cho Host len (`wait-host.ps1`).

.PARAMETER GiuLaiDuLieu
    KHONG don dep sau khi do (de nguoi van hanh soi tay). ⚠️ Cong `kiem-tat-ca.ps1` se DO o nhom bat bien
    "phai rong" cho toi khi don — dung quen.

.EXAMPLE
    pwsh -File tools\probe-s19-vnpay-that.ps1

.EXAMPLE
    # Sau khi da dat 2 bien o scope User (setx) — probe tu doc registry User/Machine neu Process rong:
    setx PaymentHub__VnPay__TmnCode "<ma merchant sandbox>"
    setx PaymentHub__VnPay__HashSecret "<hash secret sandbox>"
    pwsh -File tools\probe-s19-vnpay-that.ps1 -SoTien 10000

.NOTES
    Tien to du lieu: `K9S9` (kiem-db nhom ⑤ quet `like 'k9%'`; nhom ⑥ quet GUID `e0000000-...c9`).
    Ma ket qua: 0 = DAT · 2 = THIEU credential (DUNG, khong phai su co) · 3 = loi MOI TRUONG · 1 = do that.
#>
[CmdletBinding()]
param(
    [int]$SoTien = 10000,
    [int]$ChoThanhToanGiay = 600,
    [int]$ChuKyHoiGiay = 20,
    [int]$TimeoutGiay = 120,
    [switch]$GiuLaiDuLieu
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $PSScriptRoot '_tam-s19'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

# 🔑 Thu vien dung chung: `ham-tiem` (TiemVaoDb/Invoke-Psql) + `nen-tang` (Lay-DuongDanBin/Start-TienTrinhAn/
#    Lay-PidDangNghe). 📌 Chi co HAM, khong co `param` cap tep ⇒ nap an toan.
. (Join-Path $PSScriptRoot 'ham-tiem.ps1')
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$hostApi = 'http://localhost:5000'
$tienTrinh = $null
$ketQua = [System.Collections.Generic.List[object]]::new()
$daDatS19 = $false
$tepLogHost = Join-Path $thuMucTam 'host.log'

# 📌 Moi dau vet cua probe mang tien to `K9S9` / GUID trong khoi `e0000000-...` ⇒ kiem-db bat duoc neu con sot.
$maSoDon = 'K9S9-DON-01'
$idDon = 'e0000000-0000-0000-0000-0000000000c9'
$urlCongSandbox = 'https://sandbox.vnpayment.vn/paymentv2/vpcpay.html'

$soTienSql = $SoTien.ToString([System.Globalization.CultureInfo]::InvariantCulture)

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# HAM TRO GIUP — 📌 moi ham phai CHIU DUOC dau vao rong (`README-probe` G2) va KHONG dung ten bien
# he thong (`$pid`, `$host`, `$home`, `$input`, `$args`, `$error`, `$matches` — `README-probe` G1).
# ══════════════════════════════════════════════════════════════════════════════════════════════════

function Kq {
    param([string]$Ten, [bool]$Dat, [string]$ChiTiet = '')

    $ketQua.Add([pscustomobject]@{ Ten = $Ten; Dat = $Dat; ChiTiet = $ChiTiet })
    Write-Output ("   [{0}] {1} | {2}" -f $Ten, $(if ($Dat) { 'DAT' } else { 'KHONG DAT' }), $ChiTiet)
}

function Dem-Db {
    param([string]$Sql)

    $dong = @(Invoke-Psql -Sql $Sql)
    if ($dong.Count -eq 0) { return -1 }

    return [int](($dong[0]).Trim())
}

function Goi-Api {
    param(
        [string]$PhuongThuc,
        [string]$DuongDan,
        [string]$Token,
        $Body = $null
    )

    # ⚠️ `-StatusCodeVariable` chi co tren PowerShell 7 (`probe-thanh-toan` da vap bay nay).
    $maHttp = 0
    $thamSo = @{
        Method             = $PhuongThuc
        Uri                = ($hostApi + $DuongDan)
        Headers            = @{ Authorization = "Bearer $Token" }
        SkipHttpErrorCheck = $true
        StatusCodeVariable = 'maHttp'
        ContentType        = 'application/json'
        TimeoutSec         = 60
    }

    if ($null -ne $Body) {
        $thamSo.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }

    $tra = Invoke-RestMethod @thamSo

    return [pscustomobject]@{
        Ma     = [int]$maHttp
        DuLieu = $tra
        Than   = ($tra | ConvertTo-Json -Depth 8 -Compress)
    }
}

<#
    Doc mot bien moi truong theo BA pham vi, **Process truoc**.
    🔑 VI SAO CAN CA `User`/`Machine`: `setx` ghi vao registry — tien trinh PowerShell DANG CHAY khong
    thay no (bien moi truong chi duoc ke thua LUC KHOI DONG). 📌 Doc thang registry cho phep nguoi van
    hanh dat credential bang `setx` ma KHONG phai khoi dong lai phien lam viec — va van KHONG co bi mat
    nao trong repo.
#>
function Lay-BienMoiTruong {
    param([Parameter(Mandatory)][string]$Ten)

    foreach ($phamVi in @('Process', 'User', 'Machine')) {
        $giaTri = [Environment]::GetEnvironmentVariable($Ten, $phamVi)

        if (-not [string]::IsNullOrWhiteSpace($giaTri)) {
            return [pscustomobject]@{ Ten = $Ten; GiaTri = $giaTri.Trim(); PhamVi = $phamVi }
        }
    }

    return [pscustomobject]@{ Ten = $Ten; GiaTri = $null; PhamVi = $null }
}

<#
    🚨 DEM SO LAN MOT GIA TRI BI MAT XUAT HIEN TRONG REPO — **khong** dua gia tri vao argv.
    📌 `git grep -f <tep mau>` doc mau tu TEP ⇒ gia tri khong nam trong `CommandLine` cua tien trinh
    (argv cua Windows doc duoc tu ngoai). Tra ve **-1** khi khong do duoc (⇒ phai coi la CHUA CHUNG MINH).
#>
function Dem-XuatHienTrongRepo {
    param([string]$GiaTri)

    if ([string]::IsNullOrWhiteSpace($GiaTri)) { return -1 }

    $tepMau = [System.IO.Path]::GetTempFileName()

    try {
        Set-Content -LiteralPath $tepMau -Value $GiaTri -NoNewline -Encoding utf8

        # 🔒 `--untracked`: quet ca tep CHUA duoc theo doi (cay lam viec), khong chi index ⇒ "0 lan xuat hien"
        #    la su that cua THU DANG NAM TRONG REPO, khong phai su that cua rieng `git ls-files`.
        $dong = @(git -C $goc grep --untracked -l -F -f $tepMau -- . 2>$null)
        $ma = $LASTEXITCODE

        # 🔒 `git grep` tra 1 khi KHONG khop (dung la dieu ta muon), 0 khi co khop, >= 2 khi LOI THUC THI.
        if ($ma -ge 2) { return -1 }

        return $dong.Count
    }
    finally {
        Remove-Item -LiteralPath $tepMau -Force -ErrorAction SilentlyContinue
    }
}

<#
    Don sach dau vet probe. 🔒 Thu tu: `PayRefunds` (con) → `PayTransactions` → `OrdOrderLines` → `OrdOrders`.
    📌 Xoa theo TIEN TO (`%K9S9%`) chu khong theo `Id`: `MerchantReference` do `SinhMaThamChieu` sinh ra co
    ma probe nam O GIUA (`PAY20261030-K9S9-DON-01-1`) — bai hoc `S5-5` cua `PAY-001` §16.6.
#>
function Xoa-DuLieuProbe {
    Invoke-Psql -Sql @'
delete from "PayRefunds" where "PaymentTransactionId" in (select "Id" from "PayTransactions" where "MerchantReference" like '%K9S9%');
'@ | Out-Null

    Invoke-Psql -Sql @'
delete from "PayTransactions" where "MerchantReference" like '%K9S9%';
'@ | Out-Null

    Invoke-Psql -Sql @'
delete from "OrdOrderLines" where "OrderId" = 'e0000000-0000-0000-0000-0000000000c9';
'@ | Out-Null

    Invoke-Psql -Sql @'
delete from "OrdOrders" where "Id" = 'e0000000-0000-0000-0000-0000000000c9';
'@ | Out-Null
}

function Kiem-SachDuLieuProbe {
    param([string]$Nhan)

    $soGd = Dem-Db @'
select count(*)::text from "PayTransactions" where "MerchantReference" like '%K9S9%';
'@

    $soDonCon = Dem-Db @'
select count(*)::text from "OrdOrders" where "Id" = 'e0000000-0000-0000-0000-0000000000c9';
'@

    Kq ("{0} dong PayTransactions con lai = 0" -f $Nhan) ($soGd -eq 0) ("con = {0}" -f $soGd)
    Kq ("{0} dong OrdOrders con lai = 0" -f $Nhan) ($soDonCon -eq 0) ("con = {0}" -f $soDonCon)
}

<#
    Ghi tep bang chung — CHI khi DA DAT. 🔒 Hai chan:
      ① khong in credential (quet lai noi dung: neu chua `HashSecret`/`TmnCode` ⇒ NEM, khong ghi);
      ② khong co so lieu nao doan: moi con so la tham so truyen vao tu ket qua DO duoc.
    📌 Tep nam trong `docs/bang-chung/` — thu muc chi duoc tao khi co that mot giao dich that.
#>
function Ghi-BangChung {
    param(
        [string]$MerchantReference,
        [string]$ProviderTransactionId,
        [string]$IdempotencyKey,
        [string]$PaidAtUtc,
        [string]$SoTienVnd,
        [string]$SoTienMinorUnits,
        [string]$PaymentStatusDon,
        [string]$SoLanNhanWebhook,
        [int]$SoDongQuerydrThuDuoc,
        [string]$TmnCode,
        [string]$HashSecret,
        [int]$SoLanTmnTrongRepo,
        [int]$SoLanKhoaTrongRepo
    )

    $thoiDiem = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $thuMucBangChung = Join-Path (Join-Path $goc 'docs') 'bang-chung'
    $tepRa = Join-Path $thuMucBangChung ('s19-vnpay-sandbox-' + (Get-Date).ToString('yyyyMMdd-HHmmss') + '.md')

    # 🔒 HERE-STRING **SINGLE-QUOTED** + `.Replace` (khong phai `@"…"@`): hai ly do do duoc —
    #    ① trong `@"…"@` mot dau backtick la KY TU ESCAPE ⇒ moi `` `code` `` bi an mat, tep bang chung ra
    #      markdown hong (va lan chay thu da cho thay dung nhu vay);
    #    ② gia tri do duoc (vi du `MerchantReference`) khong bao gio bi PowerShell dien giai.
    $noiDung = @'
# BANG CHUNG `S19` — mot giao dich VNPay **sandbox THẬT**

> Sinh tu dong boi `tools/probe-s19-vnpay-that.ps1` (chay __THOI_DIEM__, GMT+7). **KHONG sua tay**:
> moi con so duoi day doc tu DB/HTTP cua lan chay that. 🔒 Neu dong nao khong do duoc thi probe da DUNG
> va khong ghi tep nay.

| Muc | Gia tri do duoc |
|---|---|
| MerchantReference (`vnp_TxnRef`) | `__MERCHANT_REF__` |
| **ProviderTransactionId** (so giao dich do CONG cap) | `__PROVIDER_TXN__` |
| `IdempotencyKey` (khoa chong trung = ma cong cap) | `__IDEM__` |
| `PaidAtUtc` (UTC) | `__PAID_AT__` |
| So tien (VND) | `__SO_TIEN__` |
| So tien (minor units, = VND x 100) | `__SO_TIEN_MINOR__` |
| `PayTransactions.Status` | `Succeeded` |
| `OrdOrders.PaymentStatus` | `__PAY_STATUS_DON__` (2 = `Paid`) |
| `WebhookReceivedCount` | `__SO_WEBHOOK__` |
| Duong xac nhan | `querydr` tra `00` qua `POST /api/app/payment/truy-van-trang-thai/{id}` |

## Log Host (neo grep duoc)

| Neo | So dong trong lan chay nay |
|---|---:|
| `[PAY-QUERYDR-THU-DUOC]` | __SO_DONG_QUERYDR__ |

## Khong co bi mat nao trong repo (do, khong khai)

| Gia tri | So lan xuat hien trong cay lam viec |
|---|---:|
| `PaymentHub:VnPay:TmnCode` | __SO_LAN_TMN__ |
| `PaymentHub:VnPay:HashSecret` | __SO_LAN_KHOA__ |

Lenh do: `git grep --untracked -l -F -f <tep-mau-tam> -- .` (mau nam trong TEP TAM, khong nam trong argv).

## Dieu bang chung nay **KHONG** chung minh

1. **Duong IPN that** — VNPay chi goi IPN toi URL mo ra Internet (`PAY-001` §7.7.1 su that #4); tren may
   local khong co. Lan chay nay xac nhan bang `querydr` (dieu kien 1 cua `ban-giao` §2.1 cho phep).
   📌 `CreatePaymentRequest.IpnUrl` **khong** duoc gui len cong (VNPay cau hinh IPN o trang merchant) ⇒
   muon do IPN that phai dang ky URL cong khai (tunnel) — viec RIENG, chua lam.
2. **Hoan tien that** (`refund`) — chua chay.
3. **Che do production** — URL la `sandbox.vnpayment.vn`.

## Lenh tai lap

```powershell
# 1) dat credential (KHONG nam trong repo): scope User/Machine hoac bien moi truong cua tien trinh
setx PaymentHub__VnPay__TmnCode "<ma merchant sandbox>"
setx PaymentHub__VnPay__HashSecret "<hash secret sandbox>"

# 2) chay
pwsh -File tools\probe-s19-vnpay-that.ps1 -SoTien __SO_TIEN__
```
'@

    $thayThe = @{
        '__THOI_DIEM__'      = $thoiDiem
        '__MERCHANT_REF__'   = $MerchantReference
        '__PROVIDER_TXN__'   = $ProviderTransactionId
        '__IDEM__'           = $IdempotencyKey
        '__PAID_AT__'        = $PaidAtUtc
        '__SO_TIEN__'        = $SoTienVnd
        '__SO_TIEN_MINOR__'  = $SoTienMinorUnits
        '__PAY_STATUS_DON__' = $PaymentStatusDon
        '__SO_WEBHOOK__'     = $SoLanNhanWebhook
        '__SO_DONG_QUERYDR__' = [string]$SoDongQuerydrThuDuoc
        '__SO_LAN_TMN__'     = [string]$SoLanTmnTrongRepo
        '__SO_LAN_KHOA__'    = [string]$SoLanKhoaTrongRepo
    }

    foreach ($khoa in $thayThe.Keys) {
        $noiDung = $noiDung.Replace($khoa, [string]$thayThe[$khoa])
    }

    if (-not [string]::IsNullOrWhiteSpace($HashSecret) -and $noiDung.Contains($HashSecret)) {
        throw 'BANG CHUNG CHUA HashSecret ⇒ TU CHOI GHI (chan ro ri bi mat).'
    }

    if (-not [string]::IsNullOrWhiteSpace($TmnCode) -and $noiDung.Contains($TmnCode)) {
        throw 'BANG CHUNG CHUA TmnCode ⇒ TU CHOI GHI (dieu kien 2 cua S19 la grep = 0).'
    }

    New-Item -ItemType Directory -Force -Path $thuMucBangChung | Out-Null
    Set-Content -LiteralPath $tepRa -Value $noiDung -Encoding utf8

    # 🔒 Tra ve DUONG DAN (mot gia tri duy nhat): neu ham nay cung `Write-Output` thong bao thi noi goi
    #    se nhan MOT MANG gom 2 phan tu — va phep kiem "co tep bang chung" se xanh vi LY DO SAI.
    return $tepRa
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# ⓪ TIEN DE: CREDENTIAL (fail-closed) - ROI MOI TRUONG - CONG SANDbox
# ══════════════════════════════════════════════════════════════════════════════════════════════════
Write-Output ''
Write-Output '════ S19 — MOT GIAO DICH VNPAY SANDBOX THAT (cong bat buoc cua GD1) ════'

$bienTmn = Lay-BienMoiTruong -Ten 'PaymentHub__VnPay__TmnCode'
$bienKhoa = Lay-BienMoiTruong -Ten 'PaymentHub__VnPay__HashSecret'

$thieuBien = @()
if ($null -eq $bienTmn.GiaTri) { $thieuBien += 'PaymentHub__VnPay__TmnCode' }
if ($null -eq $bienKhoa.GiaTri) { $thieuBien += 'PaymentHub__VnPay__HashSecret' }

if ($thieuBien.Count -gt 0) {
    Write-Output ''
    Write-Output '  ⛔ KHONG CO CREDENTIAL ⇒ S19 KHONG DO DUOC. DAY LA KET LUAN, KHONG PHAI SU CO.'
    Write-Output ''
    Write-Output ("     Thieu: {0}" -f ($thieuBien -join ' , '))
    Write-Output '     Da kiem: Process → User (registry) → Machine (registry). Ca ba deu rong.'
    Write-Output ''
    Write-Output '  🔒 Theo `ban-giao` §2.1 dieu kien 3: KHONG co credential thi DUNG va ghi ro.'
    Write-Output '     Probe nay KHONG tao giao dich, KHONG goi cong, KHONG ghi tep bang chung, KHONG ghi DB.'
    Write-Output '     📌 Cam bia giao dich hoac dung chu ky gia roi goi la "that".'
    Write-Output ''
    Write-Output '  Cach dat (khong nam trong repo):'
    Write-Output '     setx PaymentHub__VnPay__TmnCode   "<ma merchant sandbox>"'
    Write-Output '     setx PaymentHub__VnPay__HashSecret "<hash secret sandbox>"'
    Write-Output '     # roi chay lai:  pwsh -File tools\probe-s19-vnpay-that.ps1'
    Write-Output ''
    Write-Output '  📌 Neu da `setx` trong cua so khac: probe van doc duoc (no doc thang registry User/Machine).'
    Write-Output ''

    exit 2
}

$tmnCode = $bienTmn.GiaTri
$biMat = $bienKhoa.GiaTri

# 🚨 CHAN GIA TRI DEV: khoa dev nam NGAY trong repo (`appsettings.Development.json`) ⇒ do voi no thi
#    "giao dich that" la VO NGHIA (va lan chay se "thanh cong" ma khong co dong tien nao).
$tmnDev = @('GIALAPDEV', 'K9S5TMN')
if ($tmnDev -contains $tmnCode -or $biMat -like 'gia-lap-dev*' -or $biMat -like 'k9s5-*') {
    Write-Output ''
    Write-Output '  ⛔ GIA TRI DANG LA KHOA DEV (co trong repo) ⇒ KHONG PHAI merchant sandbox THAT.'
    Write-Output '     Do voi khoa dev thi "giao dich that" khong chung minh duoc gi. DUNG.'
    Write-Output ''
    exit 2
}

if ($biMat.Length -lt 16) {
    Write-Output ''
    Write-Output ("  ⛔ HashSecret chi dai {0} ky tu ⇒ gan nhu chac chan la gia tri SAI (VNPay sandbox: 32)." -f $biMat.Length)
    Write-Output '     Khong doan tiep: mot chu ky sai se lam MOI goi cong that bai va ket luan se SAI ve san pham.'
    Write-Output ''
    exit 2
}

Write-Output ("   Credential: TmnCode = {0}*** (dai {1}, tu scope {2}) · HashSecret = *** (dai {3}, tu scope {4})" -f
    $tmnCode.Substring(0, [Math]::Min(2, $tmnCode.Length)), $tmnCode.Length, $bienTmn.PhamVi,
    $biMat.Length, $bienKhoa.PhamVi)

if ($tmnCode.Length -ne 8) {
    Write-Output ("   ⚠️ TmnCode dai {0} (VNPay thuong 8 ky tu) — van do, nhung ghi lai de doi chieu." -f $tmnCode.Length)
}

# ── DB phai song (neu tat ⇒ SUA MOI TRUONG, khong sua ma: ky luat §5.1) ─────────────────────────────
try {
    $soDb = Dem-Db 'select 1::text;'
}
catch {
    $soDb = -1
}

if ($soDb -ne 1) {
    Write-Output ''
    Write-Output '  ⛔ KHONG KET NOI DUOC PostgreSQL (docker `ecommerce-postgres`) ⇒ DAY LA LOI MOI TRUONG.'
    Write-Output '     🔒 Ky luat §5.1: do la MOI TRUONG thi SUA MOI TRUONG, khong sua ma. Bat Docker + `docker compose up -d`.'
    Write-Output ''
    exit 3
}

# ── Cong sandbox phai len duoc tu may nay ──────────────────────────────────────────────────────────
$maCong = -1
$loiCong = ''

try {
    # ⚠️ KHONG dat `-MaximumRedirection 0`: PowerShell 7 coi "vuot so lan chuyen huong" la loi ⇒ voi
    #    `$ErrorActionPreference = 'Stop'` no bien mot CONG DANG SONG thanh "khong goi duoc" (da vap dung
    #    o lan chay thu: HTTP -1 + `Operation is not valid due to the current state of the object`).
    #    📌 Ta chi can biet "goi duoc tu may nay", khong can biet dich cuoi.
    $traCong = Invoke-WebRequest -Method Get -Uri $urlCongSandbox -SkipHttpErrorCheck -TimeoutSec 25
    $maCong = [int]$traCong.StatusCode
}
catch {
    $loiCong = $_.Exception.Message
}

if ($maCong -lt 200 -or $maCong -ge 400) {
    Write-Output ''
    Write-Output ("  ⛔ KHONG GOI DUOC CONG SANDBOX: {0} ⇒ HTTP {1} {2}" -f $urlCongSandbox, $maCong, $loiCong)
    Write-Output '     🔒 Loi MOI TRUONG (mang/proxy/TLS) ⇒ sua moi truong, khong ket luan gi ve san pham.'
    Write-Output ''
    exit 3
}

Write-Output ("   Cong sandbox: {0} ⇒ HTTP {1} (goi duoc tu may nay)" -f $urlCongSandbox, $maCong)

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# THAN PROBE - moi thu co side effect nam trong try, don dep trong finally
# ══════════════════════════════════════════════════════════════════════════════════════════════════
try {
    # ── ① DON DAU VET CU TRUOC KHI DO ─────────────────────────────────────────────────────────────
    Xoa-DuLieuProbe

    $conCu = Dem-Db @'
select count(*)::text from "PayTransactions" where "MerchantReference" like '%K9S9%';
'@

    Kq 'T00 tien de: khong con du lieu probe cu' ($conCu -eq 0) ("giao dich con = {0}" -f $conCu)

    if ($conCu -ne 0) {
        throw 'Con du lieu probe cu ⇒ moi phep do sau do co the XANH VI LY DO SAI.'
    }

    # ── ② KHOI DONG HOST **KEM CREDENTIAL THAT** (qua bien moi truong — KHONG ghi tep nao) ────────
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    # 🔒 `PaymentHub__Provider = VnPay`: `appsettings.Development.json` KHONG dat khoa nay (mac dinh = VnPay),
    #    nhung dat TUONG MINH o day de mot lan doi cau hinh dev sang Fake khong am tham lam probe do
    #    "cong that" bang cong gia.
    $env:PaymentHub__Provider = 'VnPay'
    $env:PaymentHub__VnPay__TmnCode = $tmnCode
    $env:PaymentHub__VnPay__HashSecret = $biMat

    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'

    foreach ($cong in @(5000)) {
        foreach ($k in @(Lay-PidDangNghe -Cong $cong)) {
            Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
        }
    }

    Start-Sleep -Seconds 1

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $tepLogHost -TepLogLoi (Join-Path $thuMucTam 'host.err')

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $hostApi -LogFile $tepLogHost `
        -TimeoutSeconds $TimeoutGiay -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    if ($LASTEXITCODE -ne 0) {
        throw ("Host KHONG len trong {0}s. Xem log: {1}" -f $TimeoutGiay, $tepLogHost)
    }

    # 🚨 Tat co bao mat TRONG LUC CHAY (admin co `ShouldChangePasswordOnNextLogin = true` ⇒ OpenIddict tu choi
    #    `password` grant). 🔒 BAT LAI trong `finally` + khang dinh (kiem-db nhom ②).
    TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = 'ADMIN';
'@ -KiemSql @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and not "ShouldChangePasswordOnNextLogin";
'@ -SoDongMongDoi 1 | Out-Null

    # ── ③ TOKEN ──────────────────────────────────────────────────────────────────────────────────
    $thanToken = @{
        grant_type    = 'password'
        client_id     = 'eCommerce_App'
        client_secret = '1q2w3e*'
        username      = 'admin@abp.io'
        password      = '1q2w3E*'
        scope         = 'eCommerce offline_access'
    }

    $jwt = Invoke-RestMethod -Method Post -Uri ($hostApi + '/connect/token') -Body $thanToken `
        -ContentType 'application/x-www-form-urlencoded'

    $token = $jwt.access_token
    Kq 'T01 lay token admin' (-not [string]::IsNullOrWhiteSpace($token)) 'password grant (co tat co bao mat trong luc chay)'

    # ── ④ DON HANG THAT TRONG DB (PaymentStatus = 0 Unpaid) ──────────────────────────────────────
    # 🔑 Can DON THAT vi dieu kien 1 cua S19 doi CA HAI: `PayTransactions.Status = Succeeded` VA
    #    `OrdOrders.PaymentStatus = Paid`. Chi kiem giao dich thi *"cau noi sang don"* khong duoc do
    #    (`PAY-001` §3.5 — no #1 cua tai lieu, da va o `S4`, nhung bang chung THAT thi chua co).
    $sqlSeed = @'
insert into "OrdOrders" ("Id","TenantId","Number","ChannelCode","CustomerId","Status","PaymentStatus","Currency","Subtotal","DiscountTotal","TaxTotal","ShippingTotal","GrandTotal","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000c9',null,'K9S9-DON-01','web',null,4,0,'VND',__SOTIEN__,0,0,0,__SOTIEN__,'{}','k9s9s19',now(),false);
'@.Replace('__SOTIEN__', $soTienSql)

    TiemVaoDb -Sql $sqlSeed -KiemSql @'
select count(*)::text from "OrdOrders" where "Id" = 'e0000000-0000-0000-0000-0000000000c9' and "PaymentStatus" = 0;
'@ -SoDongMongDoi 1 | Out-Null

    Kq 'T02 tien de: co DON that, PaymentStatus = 0 (Unpaid)' $true ("so don = {0} · GrandTotal = {1} VND" -f $maSoDon, $soTienSql)

    # ── ⑤ TAO GIAO DICH QUA API (duong vao DUY NHAT cua `PayTransactions`) ───────────────────────
    $tao = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/payment/tao-giao-dich' -Token $token -Body @{
        orderId   = $idDon
        soDon     = $maSoDon
        soTien    = $SoTien
        tienTe    = 'VND'
        returnUrl = 'https://localhost:5000/health-status'
        ipnUrl    = 'https://localhost:5000/api/webhooks/vnpay'
        noiDung   = ('S19 sandbox that ' + $maSoDon)
    }

    Kq 'T03 POST tao-giao-dich => 200' ($tao.Ma -eq 200) ("HTTP {0} · body = {1}" -f $tao.Ma, $tao.Than)

    if ($tao.Ma -ne 200) {
        throw ("Khong tao duoc giao dich (HTTP {0}) ⇒ cac ca sau VO NGHIA: {1}" -f $tao.Ma, $tao.Than)
    }

    $idGd = [string]$tao.DuLieu.paymentTransactionId
    $maGd = [string]$tao.DuLieu.merchantReference
    $urlCheckout = [string]$tao.DuLieu.checkoutUrl
    $maCongSanPham = [string]$tao.DuLieu.providerCode
    $soTienTraVe = [string]$tao.DuLieu.amount

    # 🔑 `vnpay` (KHONG phai `fake`) la bang chung dang chay CONG THAT.
    Kq 'T04 dung CONG THAT: providerCode = vnpay (khong phai cong gia lap)' ($maCongSanPham -eq 'vnpay') ("providerCode = {0}" -f $maCongSanPham)

    Kq 'T05 co CheckoutUrl da ky de khach thanh toan' (-not [string]::IsNullOrWhiteSpace($urlCheckout)) `
        ("co URL = {0} · dai {1}" -f (-not [string]::IsNullOrWhiteSpace($urlCheckout)), $urlCheckout.Length)

    Kq 'T06 so tien tra ve = so tien yeu cau (VND)' ($soTienTraVe -eq $soTienSql) ("amount = {0}" -f $soTienTraVe)

    $trangThaiTruoc = Dem-Db (@'
select "Status"::text from "PayTransactions" where "Id" = '{0}';
'@ -f $idGd)

    Kq 'T07 tien de: giao dich trong DB o Initiated (0) TRUOC khi thanh toan' ($trangThaiTruoc -eq 0) ("Status = {0}" -f $trangThaiTruoc)

    if ($trangThaiTruoc -ne 0) {
        throw 'Tien de SAI ⇒ phep do "cong lam doi trang thai" khong con y nghia.'
    }

    # ── ⑥ GIAO CHO NGUOI VAN HANH THANH TOAN TREN CONG SANDBOX ──────────────────────────────────
    Write-Output ''
    Write-Output '   ┌──────────────────────────────────────────────────────────────────────────────────────┐'
    Write-Output '   │  BAY GIO CAN MOT NGUOI THANH TOAN THAT TREN TRANG CUA VNPAY (sandbox).               │'
    Write-Output '   └──────────────────────────────────────────────────────────────────────────────────────┘'
    Write-Output ''
    Write-Output '   1. Mo URL duoi day bang trinh duyet (URL da ky bang HashSecret THAT cua merchant):'
    Write-Output ''
    Write-Output ("      {0}" -f $urlCheckout)
    Write-Output ''
    Write-Output ("   2. Thanh toan DUNG {0} VND. The test VNPay sandbox (PAY-001 §7.7.1):" -f $soTienSql)
    Write-Output '        Ngan hang NCB · so the 9704198526191432198 · chu the NGUYEN VAN A · 07/15 · OTP 123456'
    Write-Output ''
    Write-Output '   3. Khong can quay lai trang ket qua: probe tu hoi CONG (querydr) va tu ghi nhan khi tien da vao.'
    Write-Output ''
    Write-Output '   📌 `IpnUrl` KHONG duoc gui len cong (VNPay cau hinh IPN o trang merchant) ⇒ tren may local'
    Write-Output '      KHONG the co IPN that. Bang chung cua lan chay nay la `querydr` tra 00 (dieu kien 1 cho phep).'
    Write-Output ''
    Write-Output ("   ⏳ Cho toi da {0} giay · hoi cong moi {1} giay · Ctrl+C de dung." -f $ChoThanhToanGiay, $ChuKyHoiGiay)
    Write-Output ''

    # ── ⑦ CHO CONG XAC NHAN BANG `querydr` QUA CHINH DUONG SAN PHAM ─────────────────────────────
    $han = (Get-Date).AddSeconds($ChoThanhToanGiay)
    $trangThai = 'Initiated'
    $soLanHoi = 0
    $lan = 0

    while ((Get-Date) -lt $han) {
        $lan++
        $soLanHoi = $lan

        try {
            $tv = Goi-Api -PhuongThuc 'Post' -DuongDan ('/api/app/payment/truy-van-trang-thai/' + $idGd) -Token $token
        }
        catch {
            Write-Output ("   [{0}] LAN {1}: loi goi API ({2}) — thu lai." -f ((Get-Date).ToString('HH:mm:ss')), $lan, $_.Exception.Message)
            Start-Sleep -Seconds $ChuKyHoiGiay
            continue
        }

        if ($tv.Ma -ne 200) {
            Write-Output ("   [{0}] LAN {1}: querydr => HTTP {2} · body = {3}" -f ((Get-Date).ToString('HH:mm:ss')), $lan, $tv.Ma, $tv.Than)

            if ($tv.Ma -eq 403) {
                throw 'querydr bi 403 ⇒ admin thieu quyen `eCommerce.Payments.Refund` ⇒ tien de SAI, ket luan se sai.'
            }

            Start-Sleep -Seconds $ChuKyHoiGiay
            continue
        }

        $trangThai = [string]$tv.DuLieu.status
        Write-Output ("   [{0}] LAN {1}: querydr => Status = {2}" -f ((Get-Date).ToString('HH:mm:ss')), $lan, $trangThai)

        if (@('Succeeded', 'Failed', 'Cancelled', 'Refunded', 'PartiallyRefunded') -contains $trangThai) {
            break
        }

        Start-Sleep -Seconds $ChuKyHoiGiay
    }

    Kq 'T08 cong xac nhan DA THU TIEN (querydr => Succeeded)' ($trangThai -eq 'Succeeded') `
        ("Status = {0} sau {1} lan hoi" -f $trangThai, $soLanHoi)

    if ($trangThai -ne 'Succeeded') {
        # 🔑 In SO DONG NEO (khong phai nguyen van dong log): log cua Host ra file theo ma trang console
        #    (khong phai UTF-8) ⇒ in nguyen van se ra chu mojibake, va nguoi doc tuong "probe hong".
        #    🔒 Va `S19-4` nghia la dong log do **khong** chua ma phan hoi tho ⇒ in ca dong cung khong
        #    them thong tin; con so nay tra loi dung cau hoi: *"querydr co di qua duong that khong?"*.
        $soChuaBiet = 0
        $soThuDuoc = 0

        if (Test-Path $tepLogHost) {
            $soChuaBiet = @(Get-Content $tepLogHost | Select-String -SimpleMatch '[PAY-QUERYDR-CHUA-BIET]').Count
            $soThuDuoc = @(Get-Content $tepLogHost | Select-String -SimpleMatch '[PAY-QUERYDR-THU-DUOC]').Count
        }

        Write-Output ("      log: [PAY-QUERYDR-CHUA-BIET] = {0} · [PAY-QUERYDR-THU-DUOC] = {1}" -f $soChuaBiet, $soThuDuoc)

        throw ("Cong KHONG xac nhan thu tien trong {0}s (Status = {1}). 📌 'chua thanh toan' KHONG phai 'san pham hong' — dung ket luan ve ma khi chua tra tien." -f $ChoThanhToanGiay, $trangThai)
    }

    # ── ⑧ BANG CHUNG TU DB (khong tin body HTTP) ────────────────────────────────────────────────
    $dongGd = @(Invoke-Psql -Sql (@'
select "Status"::text || '|' || coalesce("ProviderTransactionId",'') || '|' || coalesce("IdempotencyKey",'') || '|' || coalesce(to_char("PaidAtUtc" at time zone 'UTC','YYYY-MM-DD"T"HH24:MI:SS'),'') || '|' || (("Amount" * 100)::bigint)::text || '|' || "WebhookReceivedCount"::text || '|' || coalesce("OrderId"::text,'') from "PayTransactions" where "Id" = '{0}';
'@ -f $idGd))

    if ($dongGd.Count -eq 0) {
        throw 'Khong doc duoc dong PayTransactions ⇒ phep do VO NGHIA.'
    }

    $phan = ([string]$dongGd[0]).Trim().Split('|')

    if ($phan.Count -lt 7) {
        throw ("Dong DB khong du truong (doc duoc {0}) ⇒ phep do VO NGHIA." -f $phan.Count)
    }

    $ttDb = $phan[0]
    $maCongDb = $phan[1]
    $idemDb = $phan[2]
    $paidAtDb = $phan[3]
    $minorDb = $phan[4]
    $soWebhook = $phan[5]
    $orderIdDb = $phan[6]

    Kq 'T09 DB: PayTransactions.Status = Succeeded' ($ttDb -eq 'Succeeded') ("Status = {0}" -f $ttDb)

    # 🚨 DIEU KIEN 2 CUA S19: `ProviderTransactionId` do CONG cap phai CO THAT.
    Kq 'T10 🚨 DB: ProviderTransactionId do CONG cap (khac rong)' (-not [string]::IsNullOrWhiteSpace($maCongDb)) `
        ("ProviderTransactionId = {0}" -f $(if ([string]::IsNullOrWhiteSpace($maCongDb)) { '(RONG)' } else { $maCongDb }))

    Kq 'T11 DB: IdempotencyKey = ma cong cap (D8)' ($idemDb -eq $maCongDb -and -not [string]::IsNullOrWhiteSpace($idemDb)) `
        ("IdempotencyKey = {0}" -f $(if ([string]::IsNullOrWhiteSpace($idemDb)) { '(RONG)' } else { $idemDb }))

    Kq 'T12 DB: PaidAtUtc da ghi' (-not [string]::IsNullOrWhiteSpace($paidAtDb)) ("PaidAtUtc = {0}" -f $paidAtDb)

    $minorMongDoi = [long]$SoTien * 100
    Kq 'T13 DB: so tien khop theo MINOR UNITS (khong dung decimal ==)' ([long]$minorDb -eq $minorMongDoi) `
        ("DB = {0} · mong doi = {1}" -f $minorDb, $minorMongDoi)

    Kq 'T14 DB: giao dich gan dung don K9S9-DON-01' ($orderIdDb -eq $idDon) ("OrderId = {0}" -f $orderIdDb)

    # ── ⑨ DIEU KIEN 1 (phan DON): `OrdOrders.PaymentStatus = Paid` ──────────────────────────────
    # 📌 Doc SAU khi querydr da ghi + phat ETO. Neu 0 (Unpaid) thi *"cau noi"* chua thap — dung la
    #    loai loi im lang ma `S4` sinh ra de dong (`PAY-001` §3.5).
    $payStatusDon = -1
    $hanChoDon = (Get-Date).AddSeconds(20)

    while ((Get-Date) -lt $hanChoDon) {
        $payStatusDon = Dem-Db (@'
select "PaymentStatus"::text from "OrdOrders" where "Id" = '{0}';
'@ -f $idDon)

        if ($payStatusDon -eq 2) { break }

        Start-Sleep -Seconds 2
    }

    Kq 'T15 🚨 DB: OrdOrders.PaymentStatus = 2 (Paid) — cau noi sang don THAP' ($payStatusDon -eq 2) `
        ("PaymentStatus = {0} (2 = Paid)" -f $payStatusDon)

    # ── ⑩ NEO LOG: querydr co di qua duong that khong ──────────────────────────────────────────
    $soDongQuerydr = 0

    if (Test-Path $tepLogHost) {
        $soDongQuerydr = @(Get-Content $tepLogHost -Encoding utf8 | Select-String -SimpleMatch '[PAY-QUERYDR-THU-DUOC]').Count
    }

    Kq 'T16 log Host co neo [PAY-QUERYDR-THU-DUOC] (duong querydr da chay)' ($soDongQuerydr -ge 1) ("so dong = {0}" -f $soDongQuerydr)

    # ── ⑪ DIEU KIEN 2 (phan BI MAT): grep = 0 ──────────────────────────────────────────────────
    $soLanTmn = Dem-XuatHienTrongRepo -GiaTri $tmnCode
    $soLanKhoa = Dem-XuatHienTrongRepo -GiaTri $biMat

    Kq 'T17 TmnCode KHONG xuat hien trong repo (grep = 0)' ($soLanTmn -eq 0) ("so lan = {0}" -f $soLanTmn)
    Kq 'T18 HashSecret KHONG xuat hien trong repo (grep = 0)' ($soLanKhoa -eq 0) ("so lan = {0}" -f $soLanKhoa)

    if ($soLanTmn -ne 0 -or $soLanKhoa -ne 0) {
        throw 'BI MAT DA LOT VAO REPO ⇒ dung, khong ghi bang chung, phai don tay truoc.'
    }

    # ── ⑫ BANG CHUNG ───────────────────────────────────────────────────────────────────────────
    $tepBangChung = Ghi-BangChung -MerchantReference $maGd -ProviderTransactionId $maCongDb `
        -IdempotencyKey $idemDb -PaidAtUtc $paidAtDb -SoTienVnd $soTienSql -SoTienMinorUnits $minorDb `
        -PaymentStatusDon $payStatusDon -SoLanNhanWebhook $soWebhook -SoDongQuerydrThuDuoc $soDongQuerydr `
        -TmnCode $tmnCode -HashSecret $biMat -SoLanTmnTrongRepo $soLanTmn -SoLanKhoaTrongRepo $soLanKhoa

    Kq 'T19 da ghi tep bang chung (khong chua bi mat)' (-not [string]::IsNullOrWhiteSpace([string]$tepBangChung)) ([string]$tepBangChung)

    $daDatS19 = -not ($ketQua | Where-Object { -not $_.Dat })
}
finally {
    # ══════════════════════════════════════════════════════════════════════════════════════════════
    # DON DEP — ⚠️ LUON chay, ke ca khi than probe NEM (`TrongKhiDonDep` cung tinh than nay).
    # 🔒 Thu tu: du lieu probe → tat Host → BAT LAI co bao mat → khang dinh lai.
    # ══════════════════════════════════════════════════════════════════════════════════════════════
    Write-Output ''
    Write-Output '── DON DEP ────────────────────────────────────────────────────────────────────────────'

    if ($GiuLaiDuLieu) {
        Write-Output '   ⚠️ -GiuLaiDuLieu: KHONG xoa du lieu probe.'
        Write-Output '      📌 Tu day `kiem-tat-ca.ps1` se DO o bat bien "PayTransactions phai rong" cho toi khi don:'
        Write-Output '         delete from "PayRefunds" where "PaymentTransactionId" in (select "Id" from "PayTransactions" where "MerchantReference" like ''%K9S9%'');'
        Write-Output '         delete from "PayTransactions" where "MerchantReference" like ''%K9S9%'';'
        Write-Output '         delete from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000c9'';'
    }
    else {
        try {
            Xoa-DuLieuProbe
            Kiem-SachDuLieuProbe -Nhan 'sau don'
        }
        catch {
            Write-Output ("   🚨 DON DEP THAT BAI: {0}" -f $_.Exception.Message)
            Write-Output '      ⇒ Du lieu probe CO THE CON LAI. Chay lai probe, hoac xoa tay theo 3 lenh SQL o tren.'
        }
    }

    if ($null -ne $tienTrinh) {
        try {
            if (-not $tienTrinh.HasExited) {
                Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
            Write-Output ("   ⚠️ Khong tat duoc Host (PID {0})." -f $tienTrinh.Id)
        }
    }

    # 🔒 BAT LAI co bao mat — va KHANG DINH (kiem-db nhom ② se do neu quen).
    try {
        TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@ -KiemSql @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and "ShouldChangePasswordOnNextLogin";
'@ -SoDongMongDoi 1 | Out-Null

        Write-Output '   ✅ Da BAT LAI ShouldChangePasswordOnNextLogin = true (khang dinh 1 dong).'
    }
    catch {
        Write-Output ("   🚨 KHONG BAT LAI DUOC co bao mat: {0}" -f $_.Exception.Message)
        Write-Output '      ⇒ Cong `kiem-tat-ca.ps1` se DO o nhom ② — do la co che dung, dung bo qua.'
    }

    # Xoa credential khoi phien hien tai (khong de ro sang lenh sau trong cung cua so).
    Remove-Item Env:PaymentHub__VnPay__HashSecret -ErrorAction SilentlyContinue
    Remove-Item Env:PaymentHub__VnPay__TmnCode -ErrorAction SilentlyContinue
    Remove-Item Env:PaymentHub__Provider -ErrorAction SilentlyContinue

    Write-Output '─────────────────────────────────────────────────────────────────────────────────────'
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# TONG KET
# ══════════════════════════════════════════════════════════════════════════════════════════════════
$soDat = @($ketQua | Where-Object { $_.Dat }).Count
$soCa = $ketQua.Count

Write-Output ''
Write-Output ("════ S19: {0}/{1} ca DAT ════" -f $soDat, $soCa)

if ($daDatS19) {
    Write-Output ''
    Write-Output '  ✅ S19 DAT: mot giao dich sandbox THAT da di qua cong va duoc ghi so DUNG.'
    Write-Output '     📌 Ban giao: cap nhat `PAY-001` §17 va `docs/ban-giao-s19-va-con-lai-2026-10-29.md` §2.1/§7.'
    Write-Output '     📌 Chay lai cong day du TRUOC khi commit:  pwsh -File tools\kiem-tat-ca.ps1 -DayDu'
    Write-Output ''
    exit 0
}

Write-Output ''
Write-Output '  ⛔ S19 CHUA DAT (xem cac ca KHONG DAT o tren).'
Write-Output '     🔒 KHONG duoc ghi "S19 xong": dieu kien cua no la MOT giao dich that + bang chung do duoc.'
Write-Output ''

exit 1
