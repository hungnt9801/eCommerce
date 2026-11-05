<#
.SYNOPSIS
    Probe GIU CHO 2 INSTANCE — hai Host cung DB/Redis, cung giu cho MOT SKU chi con 1 mon.

.DESCRIPTION
    No cuoi cua GD1 cung #2 (checklist §7 cua `ban-giao`): *"2 instance KHO"*.

    ⚠️ **DOC KY TRUOC KHI TRICH DAN:** `probe-oversell.ps1` (buoc 13) do **hai request dong thoi tren
    MOT Host**. No chung minh khoa phan tan hoat dong **trong mot tien trinh** — 🔑 do la ca DE nhat:
    mot khoa `lock` kieu in-memory cung du de no xanh. Cau hoi cua cong #2 la cau khac:
    *"hai TIEN TRINH khac nhau (2 instance) co cung ton trong mot cho khong?"*
    => Probe nay chay **hai** `eCommerce.HttpApi.Host` (5000 + 5001) tren **cung** PostgreSQL + Redis,
    ban 1 request vao MOI Host **cung luc**, roi doc DB.

    Khang dinh (chieu DUONG):
      ① dung MOT request 200, request kia bi chan bang MA NGHIEP VU (khong 5xx);
      ② DB co dung MOT dong `InvStockReservations` cho SKU;
      ③ `InvItems.Reserved <= OnHand` va `Available = 0` — bat bien cuoi.

    Khang dinh (chieu NGUOC — 🔑 khong co no thi phep do co the xanh vi LY DO SAI):
      ④ cung hai Host, cung dong thoi, chi doi `OnHand = 2` => **CA HAI** request phai 200 va
         `Reserved = 2`. 📌 Neu buoc nay cung "mot thang", thi buoc ① do mot nguyen nhan KHAC
         (vi du moi request luon bi tu choi) chu khong phai khoa phan tan.

    🚨 **HOP DONG `409` (`D-66`, `N20`) — THU LAI CO GIOI HAN, KHONG NOI PHEP KIEM:**
      Khoa phan tan duoc nha o **cuoi `StockReservationService.GiuChoAsync`**, con transaction cua
      checkout commit o **cuoi app service** ⇒ trong khe giua hai moc do, request thu hai co the doc
      **anh cu** cua cung `InventoryItem` roi vap `AbpDbConcurrencyException` (`ConcurrencyStamp`) o
      buoc flush ⇒ **HTTP 409**, `code = null` (do duoc: `_tam-kiem` §1.3 cua `handover-n20`).
      ⇒ Hop dong: **409 = chong request, THU LAI**. Probe gui lai **chinh** request do (cung
      `idempotencyKey`) toi da `$SoLanThuLai` lan. 📌 Day KHONG phai noi long khang dinh: bat bien
      `Reserved <= OnHand` / `Available = 0` / "dung MOT dong giu cho" giu **nguyen**; chi dieu kien
      "ca hai 200" duoc do **sau khi thu lai** (dung hanh vi client that). ⛔ Khong tang gioi han de
      "cho xanh": ca `Y7` cua `test-cong-kiem.ps1` quet chinh tep nay.

    Duong giu cho that: `POST /api/app/checkout/tinh-va-giu-cho` (Khoa: `InventoryLockKeys.TheoSku`
    qua `IAbpDistributedLock` — Redis, `StockReservationService.GiuChoAsync`).

    ⚠️ **GIOI HAN DO:** probe chung minh *"hai instance khong cung giu duoc mot mon hang"*.
    No **KHONG** chung minh dieu do dung khi Redis chet (khi do `AbpDistributedLock` khong con bao dam —
    viec cua mot ca rieng), va **khong** do hieu nang.

.NOTES
    Tien to du lieu: `K9HC` · GUID khoi `e0000000-...-d2..d7` (kiem-db nhom ⑤/⑥ bat duoc neu con sot).
    Chay sau buoc 15 (khong tranh cong voi cac probe khac): moi probe tu bat/tat Host cua no.
#>
[CmdletBinding()]
param(
    [int]$CongA = 5000,
    [int]$CongB = 5001,
    [switch]$GiuDuLieu
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$urlA = "http://localhost:$CongA"
$urlB = "http://localhost:$CongB"

# 🔒 `XC-007` P-01…P-06: cung mot cho cho khac biet Windows/Linux.
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$GUID_KHO = 'e0000000-0000-0000-0000-0000000000d2'
$GUID_SP = 'e0000000-0000-0000-0000-0000000000d3'
$GUID_SKU = 'e0000000-0000-0000-0000-0000000000d4'
$GUID_BG = 'e0000000-0000-0000-0000-0000000000d5'
$GUID_DONG_BG = 'e0000000-0000-0000-0000-0000000000d6'
$GUID_TON = 'e0000000-0000-0000-0000-0000000000d7'
$SKU = 'K9HC-SKU-01'

$script:soDat = 0
$script:soHong = 0

# 🚨 `D-66`: so lan da gap **409** va phai **thu lai** (bao cao o cuoi, KHONG phai ca kiem).
$script:soXungDot = 0

function Kq([string]$ten, [bool]$dat, [string]$chiTiet = '') {
    if ($dat) {
        $script:soDat++
        Write-Host ("      [{0}] DAT | {1}" -f $ten, $chiTiet)
    }
    else {
        $script:soHong++
        Write-Host ("      [{0}] HONG | {1}" -f $ten, $chiTiet)
    }
}

function Psql([string]$sql) {
    $r = docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -t -A -c $sql 2>&1

    if ($LASTEXITCODE -ne 0) { throw "psql loi: $r" }

    return ($r | Out-String).Trim()
}

<#
    🔒 `D-31`: `OnHand`/`Reserved` la `decimal(18,3)` ⇒ psql in **`1.000`** ⇒ so **SO**, khong so chuoi
    (`WP-12` ① — cung lop loi voi `probe-oversell`). Parse bang invariant de `'.'` khong bi coi la phan cach
    nghin tren may `vi-VN`.
#>
function So([string]$s) {
    return [decimal]::Parse($s.Trim(), [System.Globalization.CultureInfo]::InvariantCulture)
}

<#
    Don sach dau vet probe. 📌 Thu tu: giu cho → so cai → bang gia → ton → SKU → SP → kho.
    🚨 `S8` bai hoc: bat Host lam `InventoryReconciliationWorker` (`RunOnStart`) chay NGAY ⇒ no ghi mot
    dong lech THAT cho `InvItems` cua probe (OnHand ma so cai = 0). Xoa CA dong lech, neu khong buoc ⑥
    cua cong se DO o lan chay sau.
#>
function DonDep {
    Psql 'delete from "ReconRunMismatches" where "Sku" = ''K9HC-SKU-01'';' | Out-Null
    Psql 'delete from "InvStockLedger" where "Sku" = ''K9HC-SKU-01'';' | Out-Null
    Psql 'delete from "InvStockReservations" where "Sku" = ''K9HC-SKU-01'';' | Out-Null
    Psql 'delete from "PrcPriceCalculationRecords" where "IdempotencyKey" like ''K9HC-%'';' | Out-Null
    Psql 'delete from "PrcPriceListItems" where "Id" = ''e0000000-0000-0000-0000-0000000000d6'';' | Out-Null
    Psql 'delete from "PrcPriceLists" where "Id" = ''e0000000-0000-0000-0000-0000000000d5'';' | Out-Null
    Psql 'delete from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000d7'';' | Out-Null
    Psql 'delete from "CatProductVariants" where "Id" = ''e0000000-0000-0000-0000-0000000000d4'';' | Out-Null
    Psql 'delete from "CatProducts" where "Id" = ''e0000000-0000-0000-0000-0000000000d3'';' | Out-Null
    Psql 'delete from "InvStockDocuments" where "Number" like ''K9HC-%'';' | Out-Null
    Psql 'delete from "InvWarehouses" where "Id" = ''e0000000-0000-0000-0000-0000000000d2'';' | Out-Null
}

function TatTatCa {
    foreach ($cong in @($CongA, $CongB)) {
        foreach ($k in @(Lay-PidDangNghe -Cong $cong)) {
            Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
        }
    }

    Start-Sleep -Seconds 1
}

function BatHost([int]$cong, [string]$hauTo) {
    $env:ASPNETCORE_URLS = "http://localhost:$cong"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
    $logHost = Join-Path $goc ("_probe-giu-cho-{0}.log" -f $hauTo)

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $logHost -TepLogLoi (Join-Path $goc ("_probe-giu-cho-{0}.err" -f $hauTo))

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url "http://localhost:$cong" -LogFile $logHost `
        -TimeoutSeconds 150 -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    if ($LASTEXITCODE -ne 0) { throw "Host $hauTo khong len" }

    return $tienTrinh
}

function DatTon([int]$onHand) {
    # 🚨 BAY DA VAP (lan chay dau): viet cau SQL nay trong chuoi **nhay kep** thi `''` la HAI dau nhay
    #    (khong phai mot) ⇒ SQL nhan `''e0000000…''` = chuoi RONG roi toi dinh danh ⇒ `syntax error`.
    #    📌 Dung chuoi **nhay don** (noi `''` = mot dau nhay) — dung khuon moi lenh SQL khac cua probe.
    $sql = 'update "InvItems" set "OnHand" = {0}, "Reserved" = 0 where "Id" = ''e0000000-0000-0000-0000-0000000000d7'';' -f $onHand

    Psql $sql | Out-Null
    Psql 'delete from "InvStockReservations" where "Sku" = ''K9HC-SKU-01'';' | Out-Null
}

$DUONG_GIU_CHO = '/api/app/checkout/tinh-va-giu-cho'

<#
    🚨 `D-66`: so lan THU LAI toi da khi gap **409** (chong request tren cung `InventoryItem`).
    📌 Gioi han **3** la CO Y (du de thang mot lan dua; ⛔ khong phai "thu mai cho xanh") va ca `Y7`
    (`test-cong-kiem.ps1`) quet tep nay: gia tri > 3 hoac vong lap khong co tran ⇒ ca do DO.
#>
$SoLanThuLai = 3
$ChoGiuaLanThuMs = 300

<# Noi dung request — MOT cho, de request ban dau va request thu lai giong **het** nhau (cung khoa). #>
function TaoBody([string]$khoa) {
    return (@{
            channelCode    = 'web'
            warehouseId    = $GUID_KHO
            customerId     = $null
            items          = @(@{ sku = $SKU; quantity = 1; taxRate = 0 })
            orderDiscount  = 0
            orderFee       = 0
            ttlPhut        = 15
            idempotencyKey = $khoa
        } | ConvertTo-Json -Depth 5)
}

<#
    Ban MOT request vao Host :cong va MOT request vao Host kia **cung luc**.
    🔒 `PostAsync` KHONG await ngay ⇒ hai request nam cung luc tren duong; `WaitAll` moi cho ca hai.
    📌 Tra ve mang 2 phan tu: `{ Url; Khoa; Ma; Than }` — giu `Url`/`Khoa` de **thu lai dung request do**
    (cung Host, cung `idempotencyKey`) khi gap 409.
#>
function BanHaiRequest([string]$url1, [string]$khoa1, [string]$url2, [string]$khoa2, [string]$token) {
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(90)

    try {
        $client.DefaultRequestHeaders.Authorization =
            [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)

        $nd1 = [System.Net.Http.StringContent]::new((TaoBody $khoa1), [System.Text.Encoding]::UTF8, 'application/json')
        $nd2 = [System.Net.Http.StringContent]::new((TaoBody $khoa2), [System.Text.Encoding]::UTF8, 'application/json')

        $t1 = $client.PostAsync(($url1 + $DUONG_GIU_CHO), $nd1)
        $t2 = $client.PostAsync(($url2 + $DUONG_GIU_CHO), $nd2)

        [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]@($t1, $t2))

        return @(
            [pscustomobject]@{ Url = $url1; Khoa = $khoa1; Ma = [int]$t1.Result.StatusCode; Than = $t1.Result.Content.ReadAsStringAsync().Result }
            [pscustomobject]@{ Url = $url2; Khoa = $khoa2; Ma = [int]$t2.Result.StatusCode; Than = $t2.Result.Content.ReadAsStringAsync().Result }
        )
    }
    finally {
        $client.Dispose()
    }
}

<# Gui lai **dung** mot request da co (cung Host + cung `idempotencyKey`) — dung cho vong thu lai. #>
function GuiLaiMotRequest([string]$url, [string]$khoa, [string]$token) {
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(90)

    try {
        $client.DefaultRequestHeaders.Authorization =
            [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)

        $nd = [System.Net.Http.StringContent]::new((TaoBody $khoa), [System.Text.Encoding]::UTF8, 'application/json')
        $t = $client.PostAsync(($url + $DUONG_GIU_CHO), $nd)

        [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]@($t))

        return [pscustomobject]@{ Url = $url; Khoa = $khoa; Ma = [int]$t.Result.StatusCode; Than = $t.Result.Content.ReadAsStringAsync().Result }
    }
    finally {
        $client.Dispose()
    }
}

<#
    🚨 `D-66`: `409` = **chong request tren cung `InventoryItem`** (`ConcurrencyStamp`), KHONG phai loi
    nghiep vu va KHONG phai 5xx. Hop dong: client **thu lai** ⇒ probe gui lai toi da `$SoLanThuLai` lan,
    **dem** so lan da thu lai (`$script:soXungDot`) de bao cao ti le, roi tra ve ket qua **cuoi cung**.
#>
function ThuLaiNeu409([object]$kq, [string]$token) {
    $soLan = 0

    while ($kq.Ma -eq 409 -and $soLan -lt $SoLanThuLai) {
        $script:soXungDot++
        $soLan++

        Start-Sleep -Milliseconds $ChoGiuaLanThuMs

        $kq = GuiLaiMotRequest -url $kq.Url -khoa $kq.Khoa -token $token
    }

    return $kq
}

Write-Host '════ PROBE GIU CHO 2 INSTANCE (hai Host, cung DB/Redis, cung mot mon hang) ════'

$hostA = $null
$hostB = $null

try {
    # ── ① TIEN DE ─────────────────────────────────────────────────────────────────────────────
    TatTatCa
    DonDep
    Kq 'P00 tien de: da don du lieu probe cu' ((Psql 'select count(*) from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000d7'';') -eq '0') 'con ton = 0'

    Psql @'
insert into "InvWarehouses" ("Id","TenantId","Code","Name","Address","IsActive","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000d2',null,'K9HC-KHO','K9HC Kho probe 2 instance','Ha Noi',true,'{}','k9hck',now(),false);
'@ | Out-Null

    Psql @'
insert into "CatProducts" ("Id","TenantId","Code","Name","Slug","Description","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted","Status")
values ('e0000000-0000-0000-0000-0000000000d3',null,'K9HC-SP-01','K9HC San pham','k9hc-san-pham','Probe giu cho 2 instance.','{}','k9hcp',now(),false,1);
insert into "CatProductVariants" ("Id","TenantId","ProductId","Sku","Price","IsActive","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000d4',null,'e0000000-0000-0000-0000-0000000000d3','K9HC-SKU-01',100000,true,now(),false);
'@ | Out-Null

    Psql @'
insert into "PrcPriceLists" ("Id","TenantId","Code","Name","Scope","ChannelCode","ChannelShopId","Currency","EffectiveFrom","EffectiveTo","Priority","Status","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000d5',null,'K9HC-BG','K9HC Bang gia','0',null,null,'VND',now() - interval '1 day',null,0,2,'{}','k9hcbg',now(),false);
insert into "PrcPriceListItems" ("Id","TenantId","PriceListId","Sku","AmountValue","CurrencyCode","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000d6',null,'e0000000-0000-0000-0000-0000000000d5','K9HC-SKU-01',100000,'VND','{}','k9hcd',now(),false);
'@ | Out-Null

    # 🔑 `OnHand = 1` — CHI du cho MOT luot giu cho. Dieu kien cua phan chieu DUONG.
    Psql @'
insert into "InvItems" ("Id","TenantId","WarehouseId","Sku","OnHand","Reserved","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000d7',null,'e0000000-0000-0000-0000-0000000000d2','K9HC-SKU-01',1,0,'{}','k9hct',now(),false);
'@ | Out-Null

    $tonSeed = Psql 'select "OnHand" || ''/'' || "Reserved" from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000d7'';'
    $phanSeed = $tonSeed.Split('/')
    Kq 'P01 seed: OnHand = 1 · Reserved = 0' (((So $phanSeed[0]) -eq 1) -and ((So $phanSeed[1]) -eq 0)) ("ton = $tonSeed")

    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = ''ADMIN'';' | Out-Null

    # ── ② HAI HOST CUNG SONG ──────────────────────────────────────────────────────────────────
    $hostA = BatHost $CongA 'a'
    $hostB = BatHost $CongB 'b'

    Kq 'P02 HAI Host cung song (hai TIEN TRINH, cung DB/Redis)' ($null -ne $hostA -and $null -ne $hostB) ("$CongA + $CongB")

    $than = @{
        grant_type = 'password'; client_id = 'eCommerce_App'; client_secret = '1q2w3e*'
        username   = 'admin@abp.io'; password = '1q2w3E*'; scope = 'eCommerce'
    }
    $jwt = Invoke-RestMethod -Method Post -Uri "$urlA/connect/token" -Body $than -ContentType 'application/x-www-form-urlencoded'
    Kq 'P03 lay token admin tu Host A' (-not [string]::IsNullOrWhiteSpace($jwt.access_token)) 'password grant'

    # ── ③ CHIEU DUONG: OnHand = 1 ⇒ DUNG MOT thang ─────────────────────────────────────────────
    # 🚨 `D-66`: hai request ban **dong thoi** (do duoc khoa phan tan), nhung request thua co the vap
    # 409 trong khe "nha khoa → commit" ⇒ **thu lai** truoc khi dem. Chieu DUONG cung thu lai: neu
    # thu lai ma van 2 thang ⇒ bat bien `Reserved <= OnHand` da bi pha (P09/P10 se bat).
    $kq1 = @(BanHaiRequest -url1 $urlA -khoa1 'K9HC-P1-A' -url2 $urlB -khoa2 'K9HC-P1-B' -token $jwt.access_token)
    $kq1 = @($kq1 | ForEach-Object { ThuLaiNeu409 -kq $_ -token $jwt.access_token })

    Kq 'P04 ca HAI Host deu TRA LOI (khong co loi ket noi ⇒ hai tien trinh that su phuc vu)' `
        ($kq1.Count -eq 2 -and $kq1[0].Ma -gt 0 -and $kq1[1].Ma -gt 0) `
        ("HTTP A = {0} · HTTP B = {1}" -f $kq1[0].Ma, $kq1[1].Ma)

    $soThang = @($kq1 | Where-Object { $_.Ma -eq 200 }).Count
    Kq 'P05 DUNG MOT request thanh cong (khong phai hai)' ($soThang -eq 1) `
        ("thanh cong = {0} · HTTP = {1}/{2}" -f $soThang, $kq1[0].Ma, $kq1[1].Ma)

    $hong = @($kq1 | Where-Object { $_.Ma -ne 200 })
    $maHong = if ($hong.Count -gt 0) { [string]$hong[0].Ma } else { '(khong co)' }
    $thanHong = if ($hong.Count -gt 0) { [string]$hong[0].Than } else { '' }

    Kq 'P06 request bi chan = 4xx (KHONG phai 5xx, khong phai im lang)' `
        ($hong.Count -eq 1 -and [int]$maHong -ge 400 -and [int]$maHong -lt 500) ("HTTP = $maHong")

    # 🔒 Ma nghiep vu: `Inventory:0001` (doc lai ton sau khi co khoa). `Lock:0001` cung la "bi chan tu te"
    #    (khong gianh duoc khoa trong thoi han) — nhung 📌 neu chi thay `Lock:0001` thi phai nghi ngo khoa
    #    dang giu qua lau; ghi lai ma THUC TE de nguoi doc thay.
    $bodyHongRutGon = if ($thanHong.Length -gt 200) { $thanHong.Substring(0, 200) } else { $thanHong }
    Kq 'P07 bi chan bang MA NGHIEP VU (Inventory:0001 hoac Lock:0001)' `
        ($thanHong -match 'eCommerce:(Inventory|Lock):\d+') $bodyHongRutGon

    # ── ④ BAT BIEN CUOI (DB — khong tin HTTP) ──────────────────────────────────────────────────
    $soGiuCho = Psql 'select count(*) from "InvStockReservations" where "Sku" = ''K9HC-SKU-01'';'
    Kq 'P08 DB: DUNG MOT dong giu cho cho SKU (khong co luot thu hai)' ($soGiuCho -eq '1') ("so dong = $soGiuCho")

    $ton = Psql 'select "OnHand" || ''/'' || "Reserved" || ''/'' || ("OnHand" - "Reserved") from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000d7'';'
    $phan = $ton.Split('/')
    Kq 'P09 DB: Reserved <= OnHand' ((So $phan[1]) -le (So $phan[0])) ("OnHand/Reserved/Available = $ton")
    Kq 'P10 DB: Available = 0 (da ban het)' ((So $phan[2]) -eq 0) ("Available = $($phan[2])")

    # ── ⑤ CHIEU NGUOC: OnHand = 2 ⇒ CA HAI thang ──────────────────────────────────────────────
    # 🔑 Khong co buoc nay thi mot probe "luon bao mot thang" (vi du moi request deu bi tu choi vi ly do
    #    khac — sai bang gia, thieu quyen…) van xanh o P05. Doi DUY NHAT so ton ⇒ ket qua phai doi.
    DatTon 2
    $ton2 = Psql 'select "OnHand" || ''/'' || "Reserved" from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000d7'';'
    $phan2 = $ton2.Split('/')
    Kq 'P11 chieu nguoc — tien de: OnHand = 2 va da xoa giu cho cu' `
        (((So $phan2[0]) -eq 2) -and ((So $phan2[1]) -eq 0)) ("ton = $ton2")

    $kq2 = @(BanHaiRequest -url1 $urlA -khoa1 'K9HC-P2-A' -url2 $urlB -khoa2 'K9HC-P2-B' -token $jwt.access_token)
    $kq2 = @($kq2 | ForEach-Object { ThuLaiNeu409 -kq $_ -token $jwt.access_token })

    $soThang2 = @($kq2 | Where-Object { $_.Ma -eq 200 }).Count
    Kq 'P12 chieu nguoc: du ton cho HAI ⇒ CA HAI request deu 200 sau khi thu lai 409 (phep do KHONG luon bao mot thang)' `
        ($soThang2 -eq 2) ("thanh cong = {0} · HTTP = {1}/{2}" -f $soThang2, $kq2[0].Ma, $kq2[1].Ma)

    $soGiuCho2 = Psql 'select count(*) from "InvStockReservations" where "Sku" = ''K9HC-SKU-01'';'
    Kq 'P13 chieu nguoc — DB: dung HAI dong giu cho' ($soGiuCho2 -eq '2') ("so dong = $soGiuCho2")

    # ── ⑥ LOG: chi duoc phep co XUNG DOT da khai bao (409), khong duoc co loi KHAC ──────────────────
    # 🚨 `D-66`: truoc day mau kiem gop `AbpDbConcurrencyException` vao "loi THAT" ⇒ moi lan xung dot
    #    lam P14 DO, du do chinh la hop dong `409` (da khai bao). Nay tach **hai** nhom:
    #      ① `Unhandled exception|OutOfMemoryException` ⇒ **KHONG duoc co** (loi that);
    #      ② `AbpDbConcurrencyException` ⇒ **duoc phep**, nhung moi dong trong log PHAI co mot `409`
    #         tuong ung ma probe da thay (⇒ khong co xung dot bi NUOT im lang).
    #    📌 Do duoc (`handover-n20` §1.3): ca 409 in dung **1** dong `AbpDbConcurrencyException`, **0**
    #    dong `Unhandled exception` ⇒ phep dem nay khong phu thuoc vao chuoi cua ABP.
    $loiA = @(Select-String -Path (Join-Path $goc '_probe-giu-cho-a.log') -Pattern 'Unhandled exception|OutOfMemoryException' -ErrorAction SilentlyContinue).Count
    $loiB = @(Select-String -Path (Join-Path $goc '_probe-giu-cho-b.log') -Pattern 'Unhandled exception|OutOfMemoryException' -ErrorAction SilentlyContinue).Count
    $xungDotA = @(Select-String -Path (Join-Path $goc '_probe-giu-cho-a.log') -Pattern 'AbpDbConcurrencyException' -ErrorAction SilentlyContinue).Count
    $xungDotB = @(Select-String -Path (Join-Path $goc '_probe-giu-cho-b.log') -Pattern 'AbpDbConcurrencyException' -ErrorAction SilentlyContinue).Count

    $tongLoi = $loiA + $loiB
    $tongXungDotLog = $xungDotA + $xungDotB

    Kq 'P14 log: KHONG co loi that; xung dot trong log deu da tra 409 cho client (khong nuot im lang)' `
        ($tongLoi -eq 0 -and ($tongXungDotLog -eq 0 -or $script:soXungDot -ge 1)) `
        ("loi that A = $loiA · B = $loiB · xung dot trong log A = $xungDotA · B = $xungDotB · 409 da thay = $($script:soXungDot)")

    Write-Host ("   ℹ️ THU LAI 409 (`D-66`): {0} lan · gioi han {1} lan/request · HTTP cuoi: chieu duong {2}/{3} · chieu nguoc {4}/{5}" -f `
            $script:soXungDot, $SoLanThuLai, $kq1[0].Ma, $kq1[1].Ma, $kq2[0].Ma, $kq2[1].Ma)
}
catch {
    Write-Host ("   ❌ NGOAI LE: " + $_.Exception.Message)
    $script:soHong++
}
finally {
    if ($hostA) { Stop-Process -Id $hostA.Id -Force -ErrorAction SilentlyContinue }
    if ($hostB) { Stop-Process -Id $hostB.Id -Force -ErrorAction SilentlyContinue }

    TatTatCa

    if ($GiuDuLieu) {
        Write-Host '   ⚠️ -GiuDuLieu: KHONG xoa du lieu probe (cong se DO o buoc ⑥ cho toi khi don).'
    }
    else {
        DonDep
    }

    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = ''ADMIN'';' | Out-Null

    Kq 'P15 don dep: khong con giu cho / ton / don vi probe' `
        ((Psql 'select count(*) from "InvStockReservations" where "Sku" = ''K9HC-SKU-01'';') -eq '0' -and
         (Psql 'select count(*) from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000d7'';') -eq '0') `
        'giu cho = 0 · ton = 0'

    Kq 'P16 don dep: da BAT LAI co ShouldChangePasswordOnNextLogin' `
        ((Psql 'select count(*) from "AbpUsers" where "NormalizedUserName" = ''ADMIN'' and "ShouldChangePasswordOnNextLogin";') -eq '1') 'co = 1'

    Remove-Item Env:\ASPNETCORE_URLS -ErrorAction SilentlyContinue
}

Write-Host ("════ PROBE GIU CHO 2 INSTANCE: {0} DAT / {1} HONG ════" -f $script:soDat, $script:soHong)

if ($script:soHong -gt 0) { exit 1 }

exit 0
