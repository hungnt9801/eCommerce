<#
.SYNOPSIS
    🚨 **KIEM E2E CO PHIEN DANG NHAP** — KHANG DINH HTML THAT cua cac trang admin.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **VI SAO SCRIPT NAY TON TAI — VA VI SAO NO LA NO LON NHAT CON LAI.**

    O `web-001` **§97**, khi toi lam **duong goi CO PHIEN**, no **ngay lap tuc** phat hien **HAI loi san pham**
    ma **1101 unit test + 11 ca probe deu KHONG thay**:

      ① `MenuViewComponent` **CHUA BAO GIO render MUC CON** ⇒ nhom *"Van hanh"* thanh `href="#"` — mot
         **lien ket CHET**, va 🔒 nguoi dung **KHONG co loi vao** `/HanMucKenh` + `/DoiSoatThanhToan`;
      ② **Can cu phap ly `91/2025/QH15`** chi nam trong **chu thich Razor** ⇒ **KHONG render** ⇒ nguoi dung
         **va nguoi kiem toan** khong thay can cu — trong khi do la **LY DO** man hinh bi gioi han truong.

    🚨 **VI SAO CHUNG LOT QUA:** unit test chi soi **DU LIEU** (menu la gi), ⚠️ **khong** soi **HTML sinh ra**;
    va probe cu chi hoi *"chua dang nhap co bi chan khong"* — 📌 **khong** hoi *"menu co muc con khong"*.

    🔒 **VA DO LA LY DO SCRIPT NAY PHAI LA MOT BUOC THUONG TRU, KHONG PHAI SCRIPT TAY:** 📌 moi vong toi
    **phai tu viet lai** probe (hoac **quen**), ⇒ 🔑 mot cong **khong chay tu dong** thi **khong phai** cong.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔒 **NAM QUY TAC DUOC THI HANH TRONG SCRIPT NAY (moi cai deu la mot bai hoc da tra gia):**

    **① PHAI GIAI MA ENTITY SO TRUOC KHI SO KHOP.** 📌 `Razor` escape tieng Viet thanh `&#x110;` (`Đ`) ⇒
    `-match 'Điều hướng'` tra **FALSE DU CHUOI CO**. 🔒 Bai hoc nay da vap **BA LAN** (`sal-005` §19.5 ·
    `web-001` §97.6 · va **mot lan nua** khi viet probe). ✅ `GiaiMaHtml` lo viec do.

    **② NEO SO KHOP PHAI LA ASCII THUAN.** 🔑 Vi `Razor` escape, mot neo co dau la **neo khong dang tin**.
    📌 Nen cac neo o day la `[KHACHHANG-MAN-HINH]`, `[HANMUCKENH-MAN-HINH]`, `KHACHHANG-MAN-HINH`, …

    **③ TIEN DE PHAI DUOC KHANG DINH TRUOC KHI DO.** ⚠️ Cac form hanh dong (`Chan`/`MoChan`) **chi render khi
    co du lieu that** ⇒ 🔑 phai **gieo du lieu TRUOC**, va **khang dinh da gieo** — 📌 neu khong thi mot ca
    **DO** se bi hieu nham la *"san pham hong"* trong khi that ra **tien de thieu**.

    **④ KHONG CO LIEN KET CHET.** 🚨 `href="#"` bam vao **khong di dau** va **trong nhu hong**. ⚠️ **Nhung:**
    phai kiem **TRONG KHOI MENU** — 📌 `href="#"` **duy nhat** tren trang thuong la **dropdown cua THEME**
    (`navbar-nav`), khong phai menu cua ta (bai hoc `web-001` §97.6). ✅ Nen tach khoi menu **theo neo**
    `aria-label` **truoc**, roi moi kiem.

    **⑤ MOI THU PHAI DUOC DON DEP — KE CA KHI DO.** 🔒 Dung `try/finally`: 📌 mot ca E2E **nem giua duong**
    ma khong don se de lai **du lieu probe** ⇒ va lan sau `kiem-db.ps1` se **DO** vi **ly do vo hinh**.
    ✅ Dung `ham-tiem.ps1` de **tu khang dinh** moi cau SQL (bai hoc §25.5: *"tiem khong thanh"* va
    *"cong khong bat"* **trong giong het nhau**).

.PARAMETER BoQuaGieoDuLieu
    ⚠️ Bo qua viec gieo du lieu (📌 chi dung khi biet chac DB da co du lieu — ⚠️ **KHONG** dung trong CI).
.PARAMETER GiuWeb
    ⚠️ **KHONG** dung tien trinh Web khi xong (📌 de lai phuc vu viec kiem bang mat). 🔑 Mac dinh la **dung**.
.PARAMETER TimeoutGiay
    Cho Web len toi da bao nhieu giay (mac dinh 180).
.PARAMETER BoQuaTienDeDb
    ⚠️ Bo qua phep kiem *"DB sach truoc khi chay"* (📌 chi dung khi can chay lai nhanh).

.EXAMPLE
    pwsh -File tools\kiem-e2e.ps1
#>
[CmdletBinding()]
param(
    [switch]$BoQuaGieoDuLieu,
    [switch]$GiuWeb,
    [int]$TimeoutGiay = 180,
    [switch]$BoQuaTienDeDb
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $PSScriptRoot '_tam-e2e'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

# 🔑 Nap thu vien tiem: 🔒 no chi co **ham**, khong co `param` cap tep ⇒ nap an toan.
. (Join-Path $PSScriptRoot 'ham-tiem.ps1')

$ketQua = [System.Collections.Generic.List[object]]::new()
$web = 'http://localhost:5100'
$jar = Join-Path $thuMucTam 'cookie.txt'
$tienTrinh = $null

function Kq {
    param([string]$Ten, [bool]$Dat, [string]$ChiTiet = '')
    $ketQua.Add([pscustomobject]@{ Ten = $Ten; Dat = $Dat; ChiTiet = $ChiTiet })
    Write-Output ("   [{0}] {1} | {2}" -f $Ten, $(if ($Dat) { 'DAT' } else { 'KHONG DAT' }), $ChiTiet)
}

<#
    🔒 **GIAI MA ENTITY SO** — ⚠️ **BAT BUOC** truoc moi phep so khop chuoi co dau.
    📌 `Razor` escape `Đ` thanh `&#x110;`, `ả` thanh `&#x1EA3;` ⇒ 🔑 so khop chuoi goc se **THAT BAI DU
    CHUOI CO**. ✅ Ho tro ca dang thap phan (`&#273;`) va dang hex (`&#x110;`).
#>
function GiaiMaHtml {
    param([string]$Html)

    $h = [regex]::Replace($Html, '&#x([0-9A-Fa-f]+);', {
            param($m)
            [string][char][Convert]::ToInt32($m.Groups[1].Value, 16)
        })

    $h = [regex]::Replace($h, '&#(\d+);', {
            param($m)
            [string][char][int]$m.Groups[1].Value
        })

    return $h
}

<#
    🔒 **TAI TRANG VA TRA VE HTML DA GIAI MA** — dung `curl.exe` voi **cookie jar**.
    ⚠️ **KHONG** dung `Invoke-WebRequest -MaximumRedirection 0`: 📌 no **nem** va **khong** lo `StatusCode`
    (bai hoc probe cu).
#>
function TaiTrang {
    param([string]$DuongDan, [string]$TepLuu)

    $ma = & curl.exe -s -b $jar -c $jar -o $TepLuu -w '%{http_code}' ($web + $DuongDan)
    $html = if (Test-Path $TepLuu) { [System.IO.File]::ReadAllText($TepLuu, [Text.Encoding]::UTF8) } else { '' }

    return [pscustomobject]@{ Ma = ([string]$ma).Trim(); Html = $html; HtmlGoc = $html }
}

try {
    Write-Output '=== KIEM E2E CO PHIEN DANG NHAP ==='

    # ── TIEN DE: DB phai SACH truoc khi chay ────────────────────────────────────────────────
    if (-not $BoQuaTienDeDb) {
        # 🛡️ LY DO: 🔑 neu DB **da co** du lieu probe tu lan chay truoc (vi mot lan chay **nem giua duong**),
        # thi cac ca *"co form hanh dong"* se **XANH vi du lieu CU** ⇒ 📌 mot ca **XANH vi LY DO SAI**.
        $dem = (@(Invoke-Psql -Sql @'
select count(*)::text from "CusCustomers" where "Name" like 'K97E2E%';
'@) -join '').Trim()

        # 🔑 **KIEM CA DON HANG SOT** — 📌 mot don mo coi (khach da bi xoa) cung lam ca `D8` do vi **ly do
        # vo hinh**: no **khong thuoc** khach probe nao, nhung **van** nam trong DB.
        $demDon = (@(Invoke-Psql -Sql @'
select count(*)::text from "OrdOrders" where "Number" like 'K97E2E-%';
'@) -join '').Trim()

        # 🔑 **KIEM CA NHAT KY SOT** — 📌 neu con nhat ky tu lan truoc thi ca `D9a` (mong doi **dung 1**) se
        # do vi **ly do vo hinh** (⚠️ no la `2`, khong phai vi nhat ky hong).
        $demNhatKy = (@(Invoke-Psql -Sql @'
select count(*)::text from "CusDataExportLogs" where "CustomerId" = 'e0000000-0000-0000-0000-0000000000e1';
'@) -join '').Trim()

        if ($dem -ne '0' -or $demDon -ne '0' -or $demNhatKy -ne '0') {
            throw ("TIEN DE: con {0} khach 'K97E2E%', {1} don 'K97E2E-%', {2} nhat ky xuat trong DB ⇒ lan chay truoc KHONG don dep.`n" +
                "🚨 Dung lai va don truoc — ⚠️ neu khong thi cac ca 'co form hanh dong' se XANH VI DU LIEU CU." -f $dem, $demDon, $demNhatKy)
        }

        Write-Output '   ✅ Tien de: khong con du lieu probe cu (khach + don + nhat ky xuat).'
    }

    # ── ① DUNG WEB CU (neu co) + CHO CONG TRONG ─────────────────────────────────────────────
    # ⚠️ Phai lam TRUOC khi khoi dong: 🔑 mot tien trinh cu dang giu cong 5100 se lam `Start-Process` **that bai
    # im lang**, va ta se **do mot trang CU** (📌 bai hoc "doc ban CU" — `web-001` §97.6 #3).
    foreach ($cong in @(5100)) {
        foreach ($k in @(Get-NetTCPConnection -LocalPort $cong -State Listen -ErrorAction SilentlyContinue)) {
            Stop-Process -Id $k.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Seconds 2

    # ── ② GIAO DU LIEU (TRUOC khi do — quy tac ③) ───────────────────────────────────────────
    $idKhach = 'e0000000-0000-0000-0000-0000000000e1'

    if (-not $BoQuaGieoDuLieu) {
        # 🔑 **Gieo TRUOC khi do** vi cac nut hanh dong (`Chan`) **chi render khi co dong that**.
        # ⚠️ **TAT co bao mat** de dang nhap duoc ⇒ 🔒 phai **BAT LAI** trong `finally`.
        TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = 'ADMIN';
'@ -KhongNemKhiThieuKiem | Out-Null

        TiemVaoDb -Sql @'
delete from "CusCustomers" where "Id" = 'e0000000-0000-0000-0000-0000000000e1';
'@ -KhongNemKhiThieuKiem | Out-Null

        # 🚨 **KHANG DINH PHEP TIEM DA XAY RA** (bai hoc §25.5) — 🔑 `CusCustomers` co `IsDeleted` nen phai
        # keo theo cot do, ⚠️ va ten cot phai la **THAT** (📌 doan ten cot da tra gia **BA LAN**).
        # ➕ Email/sdt o day duoc ca **D7b** doi chieu ⇒ 📌 chung phai la gia tri **CO THAT trong DB**,
        # khong phai chuoi minh tu nghi ra (🔑 neu khong thi D7b se do vi **ly do sai**).
        TiemVaoDb -Sql @'
insert into "CusCustomers" ("Id","TenantId","Name","Email","Phone","Status","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000e1',null,'K97E2E Khach Probe','k97e2e@example.invalid','0900000001',1,'{}','k97e2e1',now(),false);
'@ -KiemSql @'
select count(*)::text from "CusCustomers" where "Id" = 'e0000000-0000-0000-0000-0000000000e1';
'@ -SoDongMongDoi 1 | Out-Null

        # 🚨 **GIE0 THEM 1 DON HANG** (`CUS-001` §20.7 #3) — 🔑 vi **phan LON** du lieu ca nhan that nam o
        # **don hang**, khong o ho so khach. ⚠️ Neu khong gieo thi ca `D8` se **khong do duoc gi**.
        # 📌 Don hang PHAI tro ve **dung khach probe** (`CustomerId`) — 🔑 neu khong thi no **khong thuoc**
        # ban xuat cua khach do, va ca kiem se do vi **ly do sai**.
        TiemVaoDb -Sql @'
delete from "OrdOrderLines" where "OrderId" = 'e0000000-0000-0000-0000-0000000000e2';
'@ -KhongNemKhiThieuKiem | Out-Null

        TiemVaoDb -Sql @'
delete from "OrdOrders" where "Id" = 'e0000000-0000-0000-0000-0000000000e2';
'@ -KhongNemKhiThieuKiem | Out-Null

        TiemVaoDb -Sql @'
insert into "OrdOrders" ("Id","TenantId","Number","ChannelCode","CustomerId","Status","PaymentStatus","Currency","Subtotal","DiscountTotal","TaxTotal","ShippingTotal","GrandTotal","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000e2',null,'K97E2E-DON-001','web','e0000000-0000-0000-0000-0000000000e1',4,2,'VND',250000,0,0,0,250000,'{}','k97e2e2',now(),false);
'@ -KiemSql @'
select count(*)::text from "OrdOrders" where "Id" = 'e0000000-0000-0000-0000-0000000000e2';
'@ -SoDongMongDoi 1 | Out-Null

        TiemVaoDb -Sql @'
insert into "OrdOrderLines" ("Id","TenantId","OrderId","Sku","Quantity","UnitPrice","DiscountAmount","TaxAmount","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000e3',null,'e0000000-0000-0000-0000-0000000000e2','K97E2E-SKU-01',2,125000,0,0,now(),false);
'@ -KiemSql @'
select count(*)::text from "OrdOrderLines" where "Id" = 'e0000000-0000-0000-0000-0000000000e3';
'@ -SoDongMongDoi 1 | Out-Null

        Write-Output '   ✅ Da gieo 1 khach probe (K97E2E) + 1 don hang (K97E2E-DON-001) + tat co bao mat de dang nhap.'
    }

    # ── ③ KHOI DONG WEB + CHO LEN ───────────────────────────────────────────────────────────
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $binWeb = Join-Path $goc 'src\eCommerce.Web\bin\Debug\net10.0'
    $logWeb = Join-Path $thuMucTam 'web.log'

    $tienTrinh = Start-Process dotnet -ArgumentList 'eCommerce.Web.dll' -WorkingDirectory $binWeb `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $logWeb `
        -RedirectStandardError (Join-Path $thuMucTam 'web.err')

    $cho = Join-Path $PSScriptRoot 'wait-host.ps1'
    & pwsh -NoProfile -File $cho -Url $web -LogFile $logWeb -TimeoutSeconds $TimeoutGiay -ProcessId $tienTrinh.Id |
        Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    if ($LASTEXITCODE -ne 0) {
        throw ("Web KHONG len trong {0}s. Xem log: {1}" -f $TimeoutGiay, $logWeb)
    }

    # ── ④ DANG NHAP THAT (form + cookie) ────────────────────────────────────────────────────
    $trangLogin = Join-Path $thuMucTam 'login.html'
    & curl.exe -s -c $jar -o $trangLogin ($web + '/Account/Login') | Out-Null

    $htmlLogin = [System.IO.File]::ReadAllText($trangLogin, [Text.Encoding]::UTF8)

    # ⚠️ **KHONG doan ten truong** `__RequestVerificationToken` — 📌 doc tu **HTML that**.
    $m = [regex]::Match($htmlLogin, 'name="__RequestVerificationToken"[^>]*value="(?<t>[^"]+)"')

    if (-not $m.Success) {
        # 🔑 **KHONG** co token nghia la trang dang nhap **khong render** ⇒ 📌 moi ca sau do **VO NGHIA**
        # (⚠️ khong duoc coi la *"san pham hong"*).
        throw "Trang /Account/Login KHONG co __RequestVerificationToken ⇒ PHEP KIEM VO NGHIA (trang khong render?)."
    }

    $token = $m.Groups['t'].Value

    & curl.exe -s -b $jar -c $jar -o (Join-Path $thuMucTam 'login-post.html') -w '%{http_code}' -L `
        --data-urlencode ("__RequestVerificationToken=" + $token) `
        --data-urlencode 'LoginInput.UserNameOrEmailAddress=admin@abp.io' `
        --data-urlencode 'LoginInput.Password=1q2w3E*' `
        ($web + '/Account/Login') | Out-Null

    # 🚨 **KHANG DINH CO PHIEN THAT** — 🔑 `200` tren `/KhachHang` (thay vi `302` ve trang dang nhap) la
    # **bang chung duy nhat** rang cookie phien da co tac dung. ⚠️ Neu bo qua buoc nay thi **moi ca sau**
    # se do vi **ly do sai** (📌 do trang dang nhap chu khong do san pham).
    $kh = TaiTrang -DuongDan '/KhachHang' -TepLuu (Join-Path $thuMucTam 'khachhang.html')

    Kq 'B1 CO PHIEN THAT (GET /KhachHang = 200)' ($kh.Ma -eq '200') ("HTTP {0}" -f $kh.Ma)

    if ($kh.Ma -ne '200') {
        throw ("DANG NHAP THAT BAI ⇒ PHEP KIEM VO NGHIA (HTTP {0} tren /KhachHang).`n" +
            "🚨 Day la loi DANG NHAP, KHONG phai loi san pham — kiem mat khau `1q2w3E*` truoc khi ket luan." -f $kh.Ma)
    }

    # ── ⑤ DO HTML THAT ──────────────────────────────────────────────────────────────────────
    $hm = TaiTrang -DuongDan '/HanMucKenh' -TepLuu (Join-Path $thuMucTam 'hanmuc.html')
    # ⚠️ **KHONG** dat ten bien la `$home`: 🔒 PowerShell co bien **CHI-DOC** `$HOME` ⇒ script **chet giua duong**.

    $htmlKh = GiaiMaHtml $kh.Html
    $htmlHm = GiaiMaHtml $hm.Html
    $trangChu = TaiTrang -DuongDan '/' -TepLuu (Join-Path $thuMucTam 'trangchu.html')
    $htmlHome = GiaiMaHtml $trangChu.Html

    Write-Output ("   HTTP: /KhachHang={0} · /HanMucKenh={1} · /= {2}" -f $kh.Ma, $hm.Ma, $trangChu.Ma)

    # 🚨 **D1/D6 — NEO HIEN THI + CAN CU PHAP LY.** 🔑 Neo ASCII de **khong** phu thuoc cach Razor escape;
    # va can cu phap ly phai **HIEN THI** (⚠️ khong phai trong chu thich Razor — loi `web-001` #2).
    Kq 'D1 NEO [KHACHHANG-MAN-HINH] HIEN THI' ($htmlKh -match '\[KHACHHANG-MAN-HINH\]') 'neo ASCII'
    Kq 'D6 CAN CU PHAP LY 91/2025/QH15 HIEN THI' ($htmlKh -match '91/2025/QH15') 'khong nam trong chu thich Razor'
    Kq 'D2 NEO [HANMUCKENH-MAN-HINH] HIEN THI' ($htmlHm -match '\[HANMUCKENH-MAN-HINH\]') 'neo ASCII'

    # 🚨 **D3 — FORM DOI CHINH SACH + O CHON DON VI (INV-003 muc 5 viec 2).** 🔑 `DonVi` theo tung dong la no
    # so 1 ve nghiep vu; neu man hinh **khong** co o chon thi nguoi van hanh **khong dat duoc** no.
    Kq 'D3 /HanMucKenh co O CHON DON VI (INV-003)' ($htmlHm -match 'name="donVi"') 'o chon don vi theo dong'

    # 🔒 **D4 — NOI RO "0 = TAT".** ⚠️ Neu khong noi ro thi nguoi van hanh hieu nham `0` la *"chan het"*.
    Kq 'D4 /HanMucKenh NOI RO "0 = TAT"' ($htmlHm -match '0\s*=\s*TẮT') 'chong hieu nguoc'

    # 🔑 **D5 — FORM HANH DONG voi DU LIEU THAT.** ⚠️ Chi render khi co dong ⇒ 🔑 day la ly do phai **gieo
    # du lieu TRUOC** (quy tac ③).
    Kq 'D5 /KhachHang co NUT HANH DONG (voi du lieu that)' ($htmlKh -match 'Chan|MoChan') 'nut chan/go chan'

    # 🚨 **D7 — XUAT DU LIEU CA NHAN (Luat 91/2025/QH15).** 🔑 Quyen **TRUY CAP** cua chu the — neu nut nay
    # khong co thi tinh nang **khong ton tai** doi voi nguoi dung (📌 *"co API"* KHONG phai *"co tinh nang"*).
    $coNutXuat = $htmlKh -match 'XuatDuLieu'
    Kq 'D7 /KhachHang co NUT "Xuat du lieu" (Luat 91/2025/QH15)' $coNutXuat 'loi vao tinh nang xuat'

    if ($coNutXuat) {
        # ① Tai TEP THAT qua cookie phien ⇒ 🔑 do **duong di THAT cua nguoi dung**, khong phai goi API truc tiep.
        $tepXuat = Join-Path $thuMucTam 'xuat-du-lieu.json'
        # 🚨 **PHAI GUI `lyDo`** (`CUS-001` §26): 🔑 ly do la **BAT BUOC** ⇒ ⚠️ thieu no thi guard o app service
        # **NEM** ⇒ `500`, 📌 va ca D7 se do **vi ly do dung** (khong phai loi san pham).
        # 🔒 Va day chinh la dieu ta muon: **khong the** xuat PII ma khong ghi ly do.
        $lyDoXuat = 'K97E2E khach yeu cau qua dien thoai'
        $urlXuat = $web + "/KhachHang/XuatDuLieu/" + $idKhach + "?lyDo=" + [uri]::EscapeDataString($lyDoXuat)

        # 🚨 **BAI HOC PowerShell (DO DUOC o `CUS-001` §26.5) — `?` LA ALIAS CUA `Where-Object`:**
        # ⚠️ Chuoi `"$idKhach?lyDo="` **KHONG** cho `idKhach` + `?lyDo=` — 🔒 no bi hieu la **pipeline**
        # `$idKhach | ?lyDo=` ⇒ 📌 ket qua chi con **`=`**.
        # 🔑 **HAU QUA DO DUOC:** URL thanh `/KhachHang/XuatDuLieu/=K97E2E%20...` ⇒ `{id}` **rong** ⇒
        # ⚠️ guard `lyDo` **NEM** ⇒ **`HTTP 500`** ⇒ 📌 va toi **suyt ket luan rang guard cua san pham sai**
        # (🔒 trong khi san pham dung; **phep do cua toi** moi sai).
        # ✅ **CACH DUNG (da do ca bon):** noi bang `+` · hoac `"${idKhach}?lyDo="` (ngoac nhon) ·
        # hoac `"$($idKhach)?lyDo="`. ➕ Va **tranh** `"?..."` ngay sau mot bien trong ngoi suy.
        # 📌 Ghi lai o day vi 🔑 **nơi nguy hiem nhat la query string** — dung cho moi URL co tham so.

        $maXuat = ([string](& curl.exe -s -b $jar -c $jar -o $tepXuat -w '%{http_code}' $urlXuat)).Trim()

        $jsonXuat = if (Test-Path $tepXuat) { [System.IO.File]::ReadAllText($tepXuat, [Text.Encoding]::UTF8) } else { '' }

        Kq 'D7a GET /KhachHang/XuatDuLieu/{id} tra 200' ($maXuat -eq '200') ("HTTP " + $maXuat)

        if ($maXuat -eq '200') {
            # ② 🔒 **KHANG DINH NOI DUNG** — 📌 mot tep 200 ma **rong** thi tinh nang **khong** hoat dong,
            # nhung `HTTP 200` **mot minh** se bao **DAT** (dung loai *"XANH vi LY DO SAI"*).
            Kq 'D7b tep xuat chua PII THAT (email + sdt)' `
                (($jsonXuat -match 'k97e2e@example.invalid') -and ($jsonXuat -match '0900000001')) `
                'email + sdt cua khach probe'

            # 🔑 Moi dong PHAI co `nguon` + `lyDo` — 📌 ban xuat khong co nguon thi **khong kiem chung duoc**,
            # va nguoi van hanh khong tra loi duoc *"sao cac anh co cai nay?"* (muc dich xu ly).
            Kq 'D7c MOI dong co `nguon` + `lyDo` (kiem chung duoc)' `
                (($jsonXuat -notmatch '"nguon":\s*""') -and ($jsonXuat -notmatch '"lyDo":\s*""')) `
                'khong co dong nao thieu nguon/lyDo'

            # 🔒 Canh bao **tai lieu mat** phai co TRONG CHINH TEP — 📌 khong chi trong tai lieu ky thuat.
            Kq 'D7d tep xuat co CANH BAO "tai lieu mat" + can cu luat' `
                (($jsonXuat -match 'TAI LIEU MAT') -and ($jsonXuat -match '91/2025/QH15')) `
                'canh bao nam TRONG tep nguoi dung nhan'

            # ══════════════════════════════════════════════════════════════════════════════════════
            # 🚨 **D8 — DON HANG TRONG BAN XUAT** (`CUS-001` §20.7 #3).
            # 🔑 **VI SAO DAY LA CA QUAN TRONG NHAT SAU D7:** *"toi da mua gi?"* moi la **ly do** chu the
            # du lieu hoi — 📌 mot ban xuat chi co *"ten, email, sdt"* la **thieu hut nghiem trong**.
            # ⚠️ Va ca nay do **du lieu THAT trong DB**, khong phai chuoi trong ma nguon.
            # ══════════════════════════════════════════════════════════════════════════════════════
            Kq 'D8a ban xuat CO don hang (so don > 0)' ($jsonXuat -match '"tongSoDon":\s*[1-9]') 'tongSoDon > 0'

            Kq 'D8b don hang co MA DON that + kenh + tong tien' `
                (($jsonXuat -match 'K97E2E-DON-001') -and ($jsonXuat -match '"maKenh":\s*"web"') -and ($jsonXuat -match '250000')) `
                'ma don + kenh + tong tien'

            Kq 'D8c dong hang co SKU that + so luong' `
                (($jsonXuat -match 'K97E2E-SKU-01') -and ($jsonXuat -match '"soLuong":\s*2')) `
                'SKU + so luong'

            # 🚨 **KHONG CAT BOT** — 🔑 neu `donHangBiCat = true` thi ca nay **khong** ap dung (⚠️ va script
            # phai **noi ro** no khong kiem duoc, chu **khong** duoc coi la DAT).
            $coCat = $jsonXuat -match '"donHangBiCat":\s*true'
            Kq 'D8d ban xuat KHONG bi cat (chi 1 don < tran 200)' (-not $coCat) ("biCat=" + $coCat)

            # 🔑 **MOI DONG HANG CO `NGUON`** — 📌 cung bat bien nhu `CacDong`: ban xuat khong co nguon thi
            # **khong kiem chung duoc**.
            Kq 'D8e dong hang co `nguon` (kiem chung duoc)' ($jsonXuat -match 'OrdOrderLines\.Sku') 'nguon = OrdOrderLines.*'

            # ══════════════════════════════════════════════════════════════════════════════════════
            # 🚨 **D9 — NHAT KY KIEM TOAN** (`CUS-001` §22).
            # 🔑 **VI SAO CA NAY QUAN TRONG:** cau hoi luat la *"**AI** da xuat du lieu cua toi, **KHI NAO**,
            # **BAO NHIEU LAN**"*. 🔒 Log **KHONG** tra loi duoc (bi xoay vong, khong tra cuu duoc, nam NGOAI
            # giao dich) ⇒ 📌 phai co **BANG TRA DUOC**.
            # ⚠️ Va ca nay do **DB THAT**, khong phai ma nguon: 🔑 mot repository duoc tiem ma **khong ghi**
            # thi ban xuat van `200` — 📌 ta chi biet khi **doc bang**.
            # ══════════════════════════════════════════════════════════════════════════════════════
            $soNhatKy = (@(Invoke-Psql -Sql @'
select count(*)::text from "CusDataExportLogs" where "CustomerId" = 'e0000000-0000-0000-0000-0000000000e1';
'@) -join '').Trim()

            Kq 'D9a DB CO dong nhat ky xuat (bang CusDataExportLogs)' ($soNhatKy -eq '1') ("soDong=" + $soNhatKy)

            # 🔑 **NOI DUNG phai dung** — ⚠️ mot dong **rong** cung lam `count = 1` ⇒ 📌 phai kiem tung truong.
            # 🚨 **DOC GIA TRI `LyDo`, KHONG phai `is null`** — ⚠️ **BAY PHEP DO (da mac that o §26.5):**
            # ban dau cau nay doc `("LyDo" is null)::text` ⇒ 🔒 no tra **`true`/`false`**, **KHONG** tra
            # gia tri ⇒ 📌 ca `D9f` (kiem ly do **duoc luu**) **khong bao gio** doc duoc ly do ⇒ ⚠️ no DO
            # **du san pham dung**.
            # 🔑 **Bai hoc:** khi mot truong duoc **kiem**, phai doc **GIA TRI** cua no — 📌 `is null` chi tra
            # loi *"co hay khong"*, 🔒 va cau hoi o day la *"gia tri la gi"*.
            $nhatKy = (@(Invoke-Psql -Sql @'
select "NguoiXuat" || '|' || "SoDongHoSo"::text || '|' || "SoDonHang"::text || '|' || "DonHangBiCat"::text || '|' || coalesce("LyDo", '(khong co)') from "CusDataExportLogs" where "CustomerId" = 'e0000000-0000-0000-0000-0000000000e1' order by "CreationTime" desc limit 1;
'@) -join '').Trim()

            Write-Output ("   nhat ky = " + $nhatKy)

            # 📌 `admin` = ten dang nhap cua nguoi dang nhap (🔑 do **DB tra ve**, khong do ta doan).
            Kq 'D9b nhat ky co NGUOI XUAT that (khong rong)' ($nhatKy -match '^admin\|') 'nguoi xuat = admin'
            Kq 'D9c nhat ky co SO DON = 1 (khop ban xuat)' ($nhatKy -match '\|1\|') 'soDonHang = 1'
            Kq 'D9d nhat ky ghi donHangBiCat = false' ($nhatKy -match '\|false\|') 'khong bi cat'

            # 🚨 **D9f — LY DO DUOC LUU** (`CUS-001` §26). 🔑 Ca nay chung minh ly do **di tu UI → controller →
            # app service → nhat ky** (⚠️ khong chi duoc **kiem** roi **bo di**).
            # 📌 `LyDo` la truong **cuoi** trong cau select ⇒ 🔑 khong the trung voi phan nao khac.
            Kq 'D9f 🚨 nhat ky LUU DUOC ly do xuat (UI -> DB)' `
                ($nhatKy -match 'K97E2E khach yeu cau qua dien thoai') `
                'ly do co trong nhat ky'

            # 🚨 **D9g — GUARD: THIEU LY DO THI KHONG XUAT DUOC** (`CUS-001` §26).
            # 🔑 **CA QUAN TRONG NHAT CUA §26:** ⚠️ neu thieu ly do ma **van xuat duoc** thi rang buoc
            # **khong ton tai** — 📌 va `required` o HTML chi la **tien nghi** (🔒 script/API khong biet no).
            # ✅ Do **THANG HTTP**, khong qua form ⇒ 📌 do dung lop bao ve **that**.
            $maKhongLyDo = ([string](& curl.exe -s -b $jar -c $jar -o (Join-Path $thuMucTam 'khong-ly-do.txt') -w '%{http_code}' ($web + "/KhachHang/XuatDuLieu/$idKhach"))).Trim()

            Kq 'D9g 🚨 THIEU ly do ⇒ KHONG xuat duoc (guard that, khong chi required o HTML)' `
                ($maKhongLyDo -ne '200') `
                ("HTTP = " + $maKhongLyDo + " (phai KHAC 200)")

            # ➕ Va khang dinh **khong co dong nhat ky MOI** nao duoc ghi cho lan that bai do —
            # 🔑 neu co thi nhat ky **dem ca lan bi tu choi**, 📌 va do la thong tin **sai**.
            $soNhatKySau = (@(Invoke-Psql -Sql @'
select count(*)::text from "CusDataExportLogs" where "CustomerId" = 'e0000000-0000-0000-0000-0000000000e1';
'@) -join '').Trim()

            Kq 'D9h lan xuat BI TU CHOI khong ghi them nhat ky' ($soNhatKySau -eq '1') ("soNhatKy = " + $soNhatKySau + " (mong doi 1: chi cua lan xuat THANH CONG o D7)")

            # 🔒 **VA NHAT KY KHONG DUOC CHUA PII** — 🔑 no chi ghi **so lieu**, khong ghi **noi dung**.
            # ⚠️ Neu nhat ky chua PII thi ta vua **tao ban sao thu hai** cua du lieu ca nhan o mot noi **khong
            # ai kiem soat** — 📌 dung cai ma `ADR-11` cam.
            #
            # 🚨 **PHEP DO PHAI CHINH XAC (da mac that o §26.5):** ban dau toi cam **`K97E2E`** o **moi noi**
            # trong dong nhat ky ⇒ ⚠️ no DO **du san pham dung**, vi 🔑 `NguoiXuat` **that su** la
            # `K97E2E-nhanvien-b` trong ca `D10` (📌 ten **nhan vien probe**, khong phai PII cua khach).
            # ✅ **CHI** cam **PII cua KHACH** (`k97e2e@` · `0900000001` · ten khach) — 🔑 do moi la dieu
            # `ADR-11` bao ve. 📌 Con ten **nguoi xuat** thi **PHAI** co (nhat ky ma khong biet **AI** thi vo dung).
            Kq 'D9e 🚨 nhat ky KHONG chua PII cua KHACH (chi so lieu + nguoi xuat)' `
                (($nhatKy -notmatch 'k97e2e@') -and ($nhatKy -notmatch '0900000001') -and ($nhatKy -notmatch 'K97E2E Khach Probe')) `
                'khong co email/sdt/ten KHACH trong nhat ky'

            # ══════════════════════════════════════════════════════════════════════════════════════
            # 🚨 **D10 — MAN HINH NHAT KY KIEM TOAN** (`/KhachHang/NhatKy`, `CUS-001` §23).
            # 🔑 **VI SAO CA NAY CAN THIET:** o §22 bang nhat ky **da co**, nhung 🔒 mot bang chung **khong ai
            # doc duoc** thi **khong phuc vu** cau hoi nao. 📌 Ca nay do **HTML THAT** cua man hinh do.
            # ⚠️ Va no do **2 dong** (khong phai 1): 🔑 voi 1 dong thi **khong phan biet duoc** *"man hinh
            # hien dung"* voi *"man hinh chi hien dong dau"* — 📌 mot loi rat de mac.
            # ══════════════════════════════════════════════════════════════════════════════════════
            # 📌 Gieo THEM 1 dong nhat ky **co so lieu KHAC HAN** (17 dong ho so) ⇒ 🔑 de kiem duoc rang man
            # hinh hien **dung tung dong**, khong phai lap lai mot dong.
            TiemVaoDb -Sql @'
insert into "CusDataExportLogs" ("Id","TenantId","CustomerId","NguoiXuat","XuatLucUtc","SoDongHoSo","SoDonHang","DonHangBiCat","LyDo","CreationTime","CreatorId")
values ('e0000000-0000-0000-0000-0000000000e4',null,'e0000000-0000-0000-0000-0000000000e1','K97E2E-nhanvien-b','2020-01-02 03:04:05+00',17,0,false,'K97E2E ly do, co "nhay kep" trong do',now(),null);
'@ -KiemSql @'
select count(*)::text from "CusDataExportLogs" where "Id" = 'e0000000-0000-0000-0000-0000000000e4';
'@ -SoDongMongDoi 1 | Out-Null

            $trangNk = TaiTrang -DuongDan "/KhachHang/NhatKy?customerId=$idKhach" -TepLuu (Join-Path $thuMucTam 'nhatky.html')
            $htmlNk = GiaiMaHtml $trangNk.Html

            Kq 'D10a GET /KhachHang/NhatKy tra 200' ($trangNk.Ma -eq '200') ("HTTP " + $trangNk.Ma)

            # 🔑 **NEO MAN HINH** — 📌 chung minh day dung la trang nhat ky (khong phai trang khac tra 200).
            Kq 'D10b man hinh co NEO [NHATKYXUATDULIEU-MAN-HINH]' ($htmlNk -match '\[NHATKYXUATDULIEU-MAN-HINH\]') 'neo ASCII'

            # 🚨 **NOI RO QUY MO** — 🔑 quy tac (1) cua man hinh: mot bang **im lang cat bot** bien bang chung
            # thanh **ket luan sai**. 📌 Phai thay **CA HAI** con so (hien thi / tong).
            Kq 'D10c man hinh NOI RO "hien thi / tong" (chong cat bot im lang)' `
                (($htmlNk -match 'dòng đang hiển thị trên tổng') -and ($htmlNk -match '>2<')) `
                'co cau noi ro quy mo + tong = 2'

            Kq 'D10d man hinh hien CA HAI dong (nguoi xuat a VA b)' `
                (($htmlNk -match 'K97E2E-nhanvien-b') -and ($htmlNk -match 'admin')) `
                'hai dong voi hai nguoi xuat khac nhau'

            # ⚠️ Dong cu (2020) co `SoDongHoSo = 17` ⇒ 🔑 phai hien **dung 17**, khong phai lap lai so cua dong kia.
            Kq 'D10e man hinh hien DUNG so lieu tung dong (17)' ($htmlNk -match '>17<') 'SoDongHoSo = 17'

            # ✅ **KHONG bi cat** (2 < tran 100) ⇒ 📌 phai noi *"dang hien thi TAT CA"*, ⚠️ khong duoc bao dong gia.
            Kq 'D10f KHONG bi cat thi noi "tat ca" (khong bao dong gia)' `
                (($htmlNk -match 'Đang hiển thị <strong>tất cả') -and ($htmlNk -notmatch 'ĐÃ BỊ CẮT')) `
                'khong bao dong gia'

            # ══════════════════════════════════════════════════════════════════════════════════════
            # 🚨 **D11–D15 — LOC (NGAY + NGUOI XUAT)** — `CUS-001` §23.6 #4 va #6.
            # 🔑 **VI SAO PHAI DO CA HAI CHIEU:** ⚠️ mot bo loc **luon tra VE TAT CA** thi *"loc duoc"* nom
            # **dung** o moi ca chi kiem *"co thay dong X khong"*. 📌 Nen moi bo loc phai duoc do **ca**
            # *"co dong can thay"* **VA** *"KHONG co dong phai bi loai"*.
            # ⚠️ Va 🔑 **logic ngay NUA MO** (`< den + 1 ngay`) moi them o §23 **chua co test nao** —
            # 📌 do la **no ke tiep** da ghi; cac ca duoi day khoa no lai.
            #
            # 📌 **DU LIEU:** dong cu = `K97E2E-nhanvien-b` luc **2020-01-02** (17 dong ho so);
            # dong moi = `admin` luc **HOM NAY** (1 dong ho so). 🔑 Hai dong **khac ca ngay LAN nguoi xuat**
            # ⇒ do duoc **ca hai** bo loc **doc lap**.
            # ══════════════════════════════════════════════════════════════════════════════════════

            # ── ① LOC NGAY: khoang chi chua dong CU ────────────────────────────────────────────
            $trangNgay = TaiTrang -DuongDan "/KhachHang/NhatKy?customerId=$idKhach&tuNgay=2020-01-01&denNgay=2020-01-31" -TepLuu (Join-Path $thuMucTam 'nhatky-ngay.html')
            $htmlNgay = GiaiMaHtml $trangNgay.Html

            # 🚨 **TACH `<tbody>` TRUOC KHI KIEM** — 🔒 **BAT BUOC**, va day la mot bai hoc DO DUOC:
            # 📌 lan dau toi kiem `>admin<` tren **CA TRANG** ⇒ no khop **`<option>admin</option>` cua DROPDOWN**
            # ⇒ ⚠️ ca kiem **DO DU SAN PHAM DUNG** (bo loc hoat dong tot).
            # 🔑 **Cung ho voi bai hoc §97.6 #3** (`href="#"` duy nhat tren trang la **dropdown cua THEME**):
            # **phai tach dung vung TRUOC khi khang dinh** — neu khong, ta do **mot thu khac** voi thu minh tuong.
            function LayThanBang {
                param([string]$Html)
                $i = $Html.IndexOf('<tbody>')
                if ($i -lt 0) { return '' }
                $j = $Html.IndexOf('</tbody>', $i)
                if ($j -lt 0) { return '' }
                return $Html.Substring($i, $j - $i)
            }

            $thanNgay = LayThanBang $htmlNgay

            # 🔑 Chong "do MOT VUNG RONG": 🔒 neu `LayThanBang` tra `''` thi **moi** ca "KHONG co X" se
            # **XANH vi ly do SAI** (⚠️ khong co gi thi khong co gi de tim). 📌 Phai khang dinh vung do **khong rong**.
            Kq 'D11a tach duoc <tbody> (vung do KHONG rong)' ($thanNgay.Length -gt 50) ("thanBang = " + $thanNgay.Length + " ky tu")

            Kq 'D11b loc ngay [2020-01-01..2020-01-31] => 200' ($trangNgay.Ma -eq '200') ("HTTP " + $trangNgay.Ma)

            # 🔑 **CHIEU "CO"**: dong trong khoang PHAI hien.
            Kq 'D11c loc ngay GIU dong trong khoang (K97E2E-nhanvien-b)' ($thanNgay -match 'K97E2E-nhanvien-b') 'dong 2020-01-02 co mat'

            # 🚨 **CHIEU "KHONG"** — 🔑 CA QUAN TRONG HON: dong HOM NAY **phai bi loai**.
            # ⚠️ Neu bo loc **khong hoat dong** thi ca duoi day se DO ⇒ 📌 va do chinh la dieu ta can.
            $conAdmin = $thanNgay -match '>admin<'
            Kq 'D11d 🚨 loc ngay LOAI dong ngoai khoang (admin hom nay)' (-not $conAdmin) ("trong <tbody> con admin = " + $conAdmin)

            # 📌 Va tong phai la **1** — 🔑 khong chi *"khong thay admin"* ma phai **dem dung**.
            Kq 'D11e loc ngay => tong = 1 (khong chi "khong thay")' ($htmlNgay -match 'trên tổng\s*<strong>1</strong>') 'tong = 1'

            # ── ② LOC NGAY: moc `denNgay` phai BAO GOM ca ngay do (logic NUA MO) ────────────────
            # 🚨 **CA KHOA LOGIC NUA MO.** 🔒 Neu ai doi thanh `<= denNgay` (moc `00:00`) thi dong luc
            # `2020-01-02 03:04:05` se **BI MAT** khi loc `denNgay=2020-01-02` ⇒ ⚠️ va do la loai loi
            # **im lang** nguy hiem nhat voi man hinh **kiem toan**.
            # ➕ Va ca nay cung khoa **loi mui gio** (`ToUniversalTime()` dich som 7 tieng — §24.5).
            $trangMoc = TaiTrang -DuongDan "/KhachHang/NhatKy?customerId=$idKhach&tuNgay=2020-01-02&denNgay=2020-01-02" -TepLuu (Join-Path $thuMucTam 'nhatky-moc.html')
            $htmlMoc = GiaiMaHtml $trangMoc.Html
            $thanMoc = LayThanBang $htmlMoc

            Kq 'D12 🚨 denNgay BAO GOM ca ngay do (nua mo) — dong 03:04:05 van hien' `
                (($thanMoc -match 'K97E2E-nhanvien-b') -and ($htmlMoc -match 'trên tổng\s*<strong>1</strong>')) `
                'tuNgay = denNgay = 2020-01-02 van thay dong 03:04:05'

            # ── ③ LOC NGUOI XUAT ──────────────────────────────────────────────────────────────
            $trangNv = TaiTrang -DuongDan "/KhachHang/NhatKy?customerId=$idKhach&nguoiXuat=K97E2E-nhanvien-b" -TepLuu (Join-Path $thuMucTam 'nhatky-nv.html')
            $htmlNv = GiaiMaHtml $trangNv.Html
            $thanNv = LayThanBang $htmlNv

            Kq 'D13a loc nguoi xuat => 200' ($trangNv.Ma -eq '200') ("HTTP " + $trangNv.Ma)
            Kq 'D13b loc nguoi xuat GIU dong dung nguoi do' (($thanNv -match 'K97E2E-nhanvien-b') -and ($htmlNv -match 'trên tổng\s*<strong>1</strong>')) 'chi 1 dong'

            # 🚨 **CHIEU "KHONG"** — 🔑 loc theo nguoi khac thi dong `admin` **phai bi loai**.
            $conAdminNv = $thanNv -match '>admin<'
            Kq 'D13c 🚨 loc nguoi xuat LOAI dong cua nguoi khac (admin)' (-not $conAdminNv) ("trong <tbody> con admin = " + $conAdminNv)

            # 🔑 **DROPDOWN den tu DU LIEU THAT** — 📌 ca hai nguoi xuat phai co trong `<select>`.
            # ⚠️ Neu dropdown **viet cung** trong view thi no se **khong** co ten nhan vien probe.
            Kq 'D13d dropdown co CA HAI nguoi xuat THAT' `
                (($htmlNk -match 'value="K97E2E-nhanvien-b"') -and ($htmlNk -match 'value="admin"')) `
                'dropdown tu du lieu that, khong viet cung'

            # ── ④ GIU `customerId` KHI LOC ────────────────────────────────────────────────────
            # ⚠️ **BAY THAT:** neu form khong gui lai `customerId` thi bam "Loc" se **mat ngu canh khach**
            # ⇒ 📌 tra ve nhat ky cua **MOI** khach, trong khi nguoi dung **tuong** dang xem cua mot nguoi.
            Kq 'D14 form loc GIU customerId (khong mat ngu canh khach)' `
                ($htmlNk -match 'name="customerId"') `
                'co hidden customerId trong form loc'

            # ── ⑤ XUAT TEP CSV (`CUS-001` §25) ────────────────────────────────────────────────
            # 🔑 **VI SAO DO TEP THAT (khong chi doc ma nguon):** 🔒 mot action tra `File(...)` co the
            # sai **BOM**, sai **escape**, hoac sai **ten tep** — ⚠️ va `HTTP 200` **mot minh** se bao
            # **DAT** cho ca ba loi do (📌 dung loai *"XANH vi LY DO SAI"*).
            $tepCsv = Join-Path $thuMucTam 'nhatky.csv'
            $hdrCsv = Join-Path $thuMucTam 'nhatky-hdr.txt'

            # 📌 **KHONG** dung `Invoke-WebRequest`: no **giai ma** noi dung ⇒ 🔒 se **an mat BOM**
            # (⚠️ chinh thu ta can kiem). ✅ Dung `curl.exe -D` de lay header + ghi byte tho.
            & curl.exe -s -b $jar -c $jar -D $hdrCsv -o $tepCsv ($web + "/KhachHang/NhatKyCsv?customerId=$idKhach") | Out-Null

            Kq 'D15a tai duoc tep CSV (tep co du lieu)' ((Test-Path $tepCsv) -and ((Get-Item $tepCsv).Length -gt 50)) `
                ("bytes = " + (Get-Item $tepCsv -ErrorAction SilentlyContinue).Length)

            if (Test-Path $tepCsv) {
                $b = [IO.File]::ReadAllBytes($tepCsv)

                # 🚨 **BOM UTF-8** — 🔑 thieu no thi Excel doc tieng Viet thanh **rac**, 📌 va tep do la thu
                # **duoc gui cho co quan kiem toan**. ⚠️ `Encoding.UTF8.GetString` se **bo qua** BOM khi doc
                # ⇒ 🔒 nen phai kiem bang **BYTE THO**.
                Kq 'D15b 🚨 tep co BOM UTF-8 (EF BB BF) — Excel moi doc dung tieng Viet' `
                    ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) `
                    ("3 byte dau = {0:X2} {1:X2} {2:X2}" -f $b[0], $b[1], $b[2])

                $csv = [Text.Encoding]::UTF8.GetString($b)

                $hdr = if (Test-Path $hdrCsv) { [IO.File]::ReadAllText($hdrCsv) } else { '' }

                Kq 'D15c header Content-Type = text/csv' ($hdr -match 'text/csv') 'content-type'
                Kq 'D15d header Content-Disposition co ten tep nhat-ky-...csv' `
                    ($hdr -match 'nhat-ky-xuat-du-lieu-ca-nhan-.*\.csv') 'ten tep co moc thoi gian'

                # 🔑 **DONG TIEN DE** — 📌 tep **roi khoi he thong** nen phai **tu mo ta** duoc.
                Kq 'D15e tep co DONG TIEN DE (can cu luat + KHONG chua PII)' `
                    (($csv -match '91/2025/QH15') -and ($csv -match 'KHONG chua noi dung du lieu ca nhan')) `
                    'dong tien de'

                # 🔑 **DU LIEU THAT** trong tep — ⚠️ khong chi tieu de.
                Kq 'D15f tep co DONG DU LIEU that (K97E2E-nhanvien-b + 17)' `
                    (($csv -match 'K97E2E-nhanvien-b') -and ($csv -match '"17"')) 'dong du lieu'

                # 🚨 **ESCAPE RFC 4180** — 🔑 **CA QUAN TRONG NHAT CUA D15.** 📌 `LyDo` co **dau phay VA
                # nhay kep** ⇒ 🔒 neu khong escape thi cot bi **LECH**, va ⚠️ nguoi doc **khong he biet**
                # (bang van trong hop le).
                Kq 'D15g 🚨 escape RFC 4180: dau phay + nhay kep duoc boc va nhan doi' `
                    ($csv -match '"K97E2E ly do, co ""nhay kep"" trong do"') `
                    'truong duoc boc "..." va " -> ""'

                # 🔑 **SO COT DUNG** — 📌 kiem he qua cua escape: dong tieu de phai co **8** truong.
                $dongTieuDe = @($csv -split "`r`n" | Where-Object { $_ -match '^"LucUtc"' } | Select-Object -First 1)
                $soTruong = if ($dongTieuDe.Count -gt 0) { (@([regex]::Matches($dongTieuDe[0], '","')).Count + 1) } else { 0 }
                Kq 'D15h dong tieu de co DUNG 8 truong (khong bi lech cot)' ($soTruong -eq 8) ("soTruong = " + $soTruong)

                # ⛔ **KHONG CAT** (2 < tran 100) ⇒ 🔒 tep **KHONG** duoc co dong canh bao.
                Kq 'D15i 🚨 KHONG bi cat thi tep KHONG co dong canh bao (khong bao dong gia)' `
                    ($csv -notmatch 'CANH BAO') 'khong co dong canh bao'

                # ══════════════════════════════════════════════════════════════════════════════════
                # 🚨 **D16 — CHIEU CON LAI: EP TEP BI CAT ⇒ DONG CANH BAO PHAI XUAT HIEN.**
                #
                # 🔑 **VI SAO CA NAY BAT BUOC (khong the bo qua):** ⚠️ `D15i` chi kiem *"KHONG bi cat thi
                # KHONG co canh bao"*. 🔒 Neu chi co no thi mot ban **bo han dong canh bao** van **XANH
                # toan bo** — 📌 tuc la phep kiem **khong bao gio chay** o nhanh quan trong.
                # ✅ Do **CA HAI CHIEU** la nguyen tac da dung o §24 (`D11`/`D13`): 🔑 mot phep kiem chi do
                # **mot phia** thi *"luon dung"* va *"khong hoat dong"* **trong giong het nhau**.
                #
                # 📌 `toiDa=1` trong khi co **2** dong ⇒ 🔒 buoc phai bi cat ⇒ ⛔ phai co dong canh bao.
                # ══════════════════════════════════════════════════════════════════════════════════
                $tepCat = Join-Path $thuMucTam 'nhatky-cat.csv'
                & curl.exe -s -b $jar -c $jar -o $tepCat ($web + "/KhachHang/NhatKyCsv?customerId=$idKhach&toiDa=1") | Out-Null

                $csvCat = if (Test-Path $tepCat) { [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($tepCat)) } else { '' }

                Kq 'D16a 🚨 tep bi cat thi CO dong CANH BAO (chieu con lai)' `
                    ($csvCat -match 'CANH BAO') 'co dong canh bao'

                # 🔑 **VA DONG CANH BAO PHAI CO CA HAI CON SO** — ⚠️ *"bi cat"* ma khong noi **cat con bao nhieu**
                # thi nguoi doc **khong biet** phan thieu lon den dau.
                # 📌 **LUU Y PHEP DO:** hai con so nam **TRONG CAU** (`Chi chua 1 tren tong 2 lan xuat`)
                # ⇒ 🔑 **KHONG** kiem `"1"` (o rieng) — ⚠️ do la **phep do sai** toi da mac lan dau.
                Kq 'D16b dong canh bao co CA HAI con so (1 hien thi / 2 tong)' `
                    (($csvCat -match 'Chi chua 1 tren tong 2 lan xuat') -and ($csvCat -match 'Dang hien thi: 1 / tong 2')) `
                    'hai con so trong cau'

                # 🔑 **VA CHI CO 1 DONG DU LIEU** — 📌 chung minh `toiDa` **that su** duoc ap dung
                # (⚠️ khong chi la *"co chu CANH BAO o dau do"*).
                #
                # 🚨 **PHEP DO PHAI DUNG HAI DIEU:**
                #   (a) **THU TU SAP XEP:** `GetNhatKyXuatDuLieuAsync` sap **moi nhat TRUOC** ⇒ 🔒 `toiDa = 1`
                #       giu dong **HOM NAY** (`admin`), ⚠️ **KHONG** phai dong 2020 (`K97E2E-nhanvien-b`).
                #       📌 Lan dau toi kiem `nhanvien-b` ⇒ **0 dong** ⇒ ⚠️ **suyt ket luan rang `toiDa` khong
                #       hoat dong** — trong khi su that la **phep do cua toi sai**;
                #   (b) **MOI O DEU DUOC BOC `"`** ⇒ 🔒 neo phai bat dau bang `"` (⚠️ lan hai toi van dem **0**
                #       vi neo `^\d{4}-` **khong khop** mot dong bat dau bang `"`). 📌 Cung ho voi bai hoc
                #       *"phai tach dung VUNG / dung DINH DANG truoc khi khang dinh"* (§24.5, §97.6 #3).
                # 🔑 Cach kiem **dung**: dem **SO DONG DU LIEU** (bat ke la ai) — 📌 do moi la dieu `toiDa`
                # dieu khien.
                $soDongDuLieu = @($csvCat -split "`r`n" | Where-Object { $_ -match '^"\d{4}-\d{2}-\d{2} ' }).Count
                Kq 'D16c tep bi cat CHI co 1 dong du lieu (toiDa duoc ap dung that)' ($soDongDuLieu -eq 1) ("soDong = " + $soDongDuLieu)

                # ➕ Va khang dinh **dong nao** duoc giu — 🔑 khoa luan diem *"moi nhat truoc"* (⚠️ neu ai
                # doi thu tu sap xep thi ca nay DO, 📌 va do la chu y: nguoi kiem toan can **moi nhat truoc**).
                # 📌 Neo phai tinh **dau boc `"`** — xem (b) o tren.
                # 🔑 **IN DONG THAT khi do** — ⚠️ day la lan THU BA phep do cua toi sai; 📌 nen thay vi
                # doan them, **in ra** roi moi ket luan (🔒 cung bai hoc *"do chuoi THAT thay vi suy luan"*).
                $dongDuLieu = @($csvCat -split "`r`n" | Where-Object { $_ -match '^"\d{4}-\d{2}-\d{2} ' })
                if ($dongDuLieu.Count -gt 0) { Write-Output ("   dong du lieu trong tep bi cat = " + $dongDuLieu[0]) } else { Write-Output '   (khong co dong du lieu nao)' }

                $giuMoiNhat = $dongDuLieu.Count -gt 0 -and $dongDuLieu[0] -match '","admin",'
                Kq 'D16d dong duoc giu la dong MOI NHAT (admin hom nay)' $giuMoiNhat 'giu dong moi nhat'
            }
        }
    }

    # ── ⑥ MENU HAI TANG (tach khoi menu TRUOC khi kiem — quy tac ④) ─────────────────────────
    # 🔒 **TACH KHOI MENU THEO NEO `aria-label`** — ⚠️ neu kiem `href="#"` tren **CA TRANG** thi se bat
    # **dropdown cua THEME** (`navbar-nav`) ⇒ 📌 ca kiem **DO vi LY DO SAI** (bai hoc `web-001` §97.6).
    # 🔑 **TIM NEO THEO CA HAI BAN** — ⚠️ `Razor` escape tieng Viet thanh entity so ⇒ ban **da giai ma** co
    # `Điều hướng`, con ban **goc** co `&#x110;i&#x1EC1;u`. 📌 Thu **ca hai**, va sua cuoi cung la `nav-pills`
    # (🔑 chuoi ASCII nay **khong bao gio** bi escape ⇒ la neo **dang tin nhat**).
    $iMenu = $htmlHome.IndexOf('Điều hướng quản trị')

    if ($iMenu -lt 0) { $iMenu = $kh.HtmlGoc.IndexOf('nav-pills') }
    if ($iMenu -lt 0) { $iMenu = $htmlHome.IndexOf('nav-pills') }
    if ($iMenu -lt 0) { $iMenu = $kh.HtmlGoc.IndexOf('aria-label') }

    # ⚠️ Neo co the bi escape ⇒ thu **ca** ban goc va ban da giai ma; neu van khong thay thi **khong doan**.
    if ($iMenu -lt 0) {
        # 🔑 Thu lai tren ban **GOC** (chua giai ma) — 📌 neu ban da giai ma ma khong thay, co the `aria-label`
        # nam trong **thuoc tinh** va van con entity o ban goc.
        $iMenu = $kh.HtmlGoc.IndexOf('nav-pills')
        if ($iMenu -lt 0) { $iMenu = $htmlHome.IndexOf('nav-pills') }
    }

    # 🚨🚨 **LAY KHOI MENU THEO RANH GIOI THAT — KHONG dung do dai CO DINH.**
    #
    # 📌 **LOI DA DO DUOC (17/10/2026, ngay khi viet script nay):** ban dau toi cat
    # `Substring($iMenu, 2000)` — ⚠️ **2000 KY TU CO DINH**. Va menu that **DAI HON 2200 ky tu**
    # (do duoc: nhom *"Van hanh"* nam **ngoai** cua so ⇒ `IndexOf('Van h')` tra **-1**).
    # 🔒 **Hau qua:** ca **E5** (*"khong co lien ket chet"*) **LUON XANH** — ⚠️ vi `href="#"` cua nhom nam o
    # **CUOI** menu, **ngoai** cua so. 📌 Nghia la phep kiem **tu vo hieu** dung o cho no quan trong nhat:
    # no sinh ra de bat **lien ket chet**, ma lien ket chet lai chinh la thu **bi cat mat**.
    # 🔑 Va no **im lang**: E5 bao `DAT` ⇒ 🔒 dung loai *"XANH vi LY DO SAI"* ma ca du an dang chong.
    #
    # ✅ **CACH DUNG:** cat tu neo menu den **the dong `</nav>`** — 🔑 do la **ranh gioi THAT** cua khoi menu,
    # khong phu thuoc do dai. ➕ Va neu **khong thay** `</nav>` thi phai **NOI RO** (⚠️ khong duoc cat bua).
    $khoiMenu = ''
    $coRanhGioi = $false

    if ($iMenu -ge 0) {
        # 🔑 **CAT THEO `<ul>` DAU TIEN SAU `nav-pills`** — ⚠️ **KHONG** dung `</nav>`.
        #
        # 🚨 **LOI DA DO DUOC (17/10/2026, ngay khi viet script nay):** ban dau toi cat
        # `Substring($iMenu, 2000)` (do dai **CO DINH**) ⇒ 🔒 va menu that **DAI HON 2200 ky tu**
        # (do duoc: nhom *"Van hanh"* nam **ngoai** cua so ⇒ `IndexOf('Van h')` tra **-1**)
        # ⇒ ca **E5** (*"khong co lien ket chet"*) **LUON XANH**, ⚠️ vi `href="#"` cua nhom nam o **CUOI** menu.
        # 📌 Nghia la phep kiem **tu vo hieu dung o cho no quan trong nhat** — no sinh ra de bat **lien ket chet**,
        # ma lien ket chet lai chinh la thu **bi cat mat**. 🔑 Va no **im lang** (`DAT`) ⇒ 🔒 dung loai
        # *"XANH vi LY DO SAI"* ma ca du an dang chong.
        #
        # ➕ **VA `</nav>` CUNG SAI:** `MenuViewComponent` render `<nav><ul>…</ul></nav>` — 🔑 `<ul>` **KHONG**
        # co `</ul>` dong ngay sau `</nav>`; nhung khoi `Substring` tu `nav-pills` den `</nav>` **van** dung.
        # 📌 Cai **sai that** la **do dai co dinh**. ✅ Nen: cat tu `nav-pills` den **`</nav>`** (ranh gioi that),
        # va neu **khong thay** `</nav>` thi **NOI RO** (⚠️ khong cat bua).
        $iDong = $htmlHome.IndexOf('</nav>', $iMenu)

        if ($iDong -gt $iMenu) {
            $khoiMenu = $htmlHome.Substring($iMenu, $iDong - $iMenu + '</nav>'.Length)
            $coRanhGioi = $true
        }
        else {
            # ⚠️ Khong thay the dong ⇒ **KHONG** cat bua: bao ro la **khong do duoc**.
            Write-Output '   ⚠️ Khong thay `</nav>` sau neo menu ⇒ KHONG xac dinh duoc ranh gioi khoi menu.'
        }
    }

    Write-Output ("   Khoi menu: {0} ky tu (ranh gioi `</nav>`={1})" -f $khoiMenu.Length, $coRanhGioi)

    if (-not $coRanhGioi) {
        # ⚠️ **KHONG do duoc khoi menu** ⇒ 🔒 **KHONG** duoc coi la *"khong co lien ket chet"*.
        # 📌 Day la bai hoc trung tam: *"0 tim thay"* KHONG phai *"sach"* (`kiem-db.ps1` nhom (6)).
        Kq 'E1 MENU RENDER (co neo dieu huong)' $false 'khong thay neo `aria-label`/`nav-pills` hoac the dong `</nav>`'
        Kq 'E2 MENU co MUC CHA "Van hanh"' $false 'khong do duoc (thieu ranh gioi khoi menu)'
        Kq 'E3 🚨 MENU co MUC CON /HanMucKenh' $false 'khong do duoc (thieu ranh gioi khoi menu)'
        Kq 'E5 TRONG KHOI MENU khong co lien ket chet href=hash' $false 'khong do duoc (thieu ranh gioi khoi menu)'
    }
    else {
        Kq 'E1 MENU RENDER (co neo dieu huong)' $true ("khoi menu {0} ky tu" -f $khoiMenu.Length)

        # 🚨 **E2 — MUC CHA.** 🔑 *"menu co nhom"* **KHONG** suy ra *"nhom co MUC CON"* ⇒ 📌 phai kiem **ca hai**.
        $coNhom = $khoiMenu -match 'Vận hành'
        Kq 'E2 MENU co MUC CHA "Van hanh"' $coNhom 'nhom hai tang'

        # 🚨 **E3 — MUC CON.** 🔑 **CA QUAN TRONG NHAT CUA SCRIPT NAY:** day chinh la loi `web-001` #1 —
        # `MenuDieuHuong` **co** khai muc con, `BoLocDieuHuong` **co** loc quyen cho con, ⚠️ nhung
        # `MenuViewComponent` **chi render `muc.Url`** ⇒ 📌 muc con **KHONG BAO GIO hien**, va nhom thanh
        # `href="#"` — **lien ket CHET**. 🔒 Trieu chung te nhat: trang `200`, menu render, **khong loi nao**.
        $coMucCon = $khoiMenu -match '/HanMucKenh'
        Kq 'E3 🚨 MENU co MUC CON /HanMucKenh' $coMucCon 'chong loi MenuViewComponent #1'

        # 🚨 **E5 — KHONG LIEN KET CHET.** ⚠️ Kiem **TRONG KHOI MENU** (quy tac ④).
        $coHash = $khoiMenu -match 'href="#"'
        Kq 'E5 TRONG KHOI MENU khong co lien ket chet href=hash' (-not $coHash) 'chong lien ket chet'
    }

    # ── ⑦ TONG KET ──────────────────────────────────────────────────────────────────────────
    $soDat = @($ketQua | Where-Object { $_.Dat }).Count

    Write-Output ''
    Write-Output ("E2E: {0}/{1} ca DAT" -f $soDat, $ketQua.Count)

    if ($soDat -ne $ketQua.Count) {
        Write-Output 'CAC CA KHONG DAT:'
        foreach ($k in ($ketQua | Where-Object { -not $_.Dat })) {
            Write-Output ("   🚨 {0} :: {1}" -f $k.Ten, $k.ChiTiet)
        }
    }
}
finally {
    # 🔒 **DON DEP — KE CA KHI DO.** 📌 Mot ca E2E nem giua duong ma khong don se de lai **du lieu probe**
    # ⇒ va lan sau `kiem-db.ps1` se **DO** vi **ly do vo hinh**.

    # ① Don du lieu probe.
    try {
        # 🔑 **THU TU QUAN TRONG: DONG HANG TRUOC, KHACH SAU.**
        # 📌 `OrdOrders.CustomerId` tro toi `CusCustomers` ⇒ ⚠️ xoa khach truoc se **vi pham khoa ngoai**
        # (hoac de lai don **mo coi**), va 🔒 lan sau ca `D8` se do vi **ly do vo hinh**.
        Invoke-Psql -Sql @'
delete from "OrdOrderLines" where "OrderId" = 'e0000000-0000-0000-0000-0000000000e2';
'@ | Out-Null

        Invoke-Psql -Sql @'
delete from "OrdOrders" where "Id" = 'e0000000-0000-0000-0000-0000000000e2';
'@ | Out-Null

        # 🚨 **XOA NHAT KY KIEM TOAN CUA PROBE** (`CUS-001` §22) — 📌 `CusDataExportLogs` la bang **DU LIEU
        # THAT** (khong phai bang trong nhu `CusCustomers`), nen ⚠️ no **KHONG** nam trong danh sach
        # *"phai rong"* cua `kiem-db.ps1` ⇒ 🔑 neu khong don thi no **tich luy** qua moi lan chay
        # (📌 va do la **dung** voi bang that — nhung **sai** voi mot lan chay probe).
        Invoke-Psql -Sql @'
delete from "CusDataExportLogs" where "CustomerId" = 'e0000000-0000-0000-0000-0000000000e1';
'@ | Out-Null

        Invoke-Psql -Sql @'
delete from "CusCustomers" where "Id" = 'e0000000-0000-0000-0000-0000000000e1';
'@ | Out-Null

        Invoke-Psql -Sql @'
delete from "CusCustomers" where "Name" like 'K97E2E%';
'@ | Out-Null

        # 🚨 **BAT LAI CO BAO MAT** — 🔑 quen buoc nay nghia la **commit mot admin KHONG bi buoc doi mat khau**.
        Invoke-Psql -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@ | Out-Null

        # 🔒 **KHANG DINH DA DON SACH** — ⚠️ neu khong thi ta **tin** rang da don trong khi du lieu **van con**
        # (📌 dung loai "tin sai" ma `ham-tiem.ps1` sinh ra de chong).
        $con = (@(Invoke-Psql -Sql @'
select count(*)::text from "CusCustomers" where "Name" like 'K97E2E%';
'@) -join '').Trim()

        $baoMat = (@(Invoke-Psql -Sql @'
select "ShouldChangePasswordOnNextLogin"::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN';
'@) -join '').Trim()

        Write-Output ''
        Write-Output ("   DON DEP: khach probe con={0} (mong doi 0) · co bao mat={1} (mong doi True)" -f $con, $baoMat)

        if ($con -ne '0' -or $baoMat -ne 'True') {
            Write-Output '   🚨 DON DEP KHONG DAY DU ⇒ lan chay sau co the XANH VI DU LIEU CU.'
        }
    }
    catch {
        # ⚠️ Khong nem tiep: loi don dep **khong** duoc che loi cua ca kiem — nhung phai **noi len**.
        Write-Output ("   🚨 DON DEP THAT BAI: {0}" -f $_.Exception.Message)
    }

    # ② Dung Web (🔑 mac dinh la dung — de cong 5100 TRONG cho lan sau).
    if ($null -ne $tienTrinh -and -not $GiuWeb) {
        Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Output '   Da dung Web (cong 5100 TRONG).'
    }
    elseif ($GiuWeb) {
        Write-Output ("   ⚠️ -GiuWeb: Web VAN CHAY (pid {0}) — 🔒 nho dung truoc khi build." -f $tienTrinh.Id)
    }

    Remove-Item -Recurse -Force $thuMucTam -ErrorAction SilentlyContinue
}

$soDat = @($ketQua | Where-Object { $_.Dat }).Count

exit $(if ($soDat -ne $ketQua.Count) { 1 } else { 0 })
