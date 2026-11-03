<#
.SYNOPSIS
    Probe HTTP thật cho **WP-05 — Đơn hàng 18 trạng thái** (transition API + bút toán kho + DS/chi tiết).

.DESCRIPTION
    Chạy **Host thật** (54275) + **Next thật** (3000) + **PostgreSQL thật**, đăng nhập qua **NextAuth**
    (đúng đường người dùng đi), rồi đo bằng HTTP qua **BFF** (`/api/proxy` — đường mà client component gọi,
    `D-12`):

      ①  trang DS đơn `/{business}/order/manage`                     → 200 + "Cần xử lý" + 14 tab
      ②  `GET /api/proxy/v1/orders/tabs`                             → ĐÚNG 14 tab có badge
      ③  `GET /api/proxy/v1/orders?tab=can-xu-ly&q=…`                → có đơn vừa tạo, nhãn "Mới"
      ④  trang chi tiết đơn                                          → 200 + "Mới" + nút "Huỷ đơn…"
      ⑤  Mới → Chờ xác nhận (9)                                      → 200
      ⑥  Chờ xác nhận → Đang xác nhận (10)                           → 200
      ⑦  Đang xác nhận → Đã xác nhận (1)                             → 200 + **stockEffect = ChotGiuCho**
      ⑧  `GET …/stock/balances?sku=`                                 → **OnHand 8** (10−2), **Reserved 1** (3−2)
      ⑨  gửi `expectedVersion` CŨ                                    → 403 + `eCommerce:Order:0003`
      ⑩  nhảy cóc `Đã xác nhận → Thành công`                         → 403 + `eCommerce:Order:0001`
      ⑪  huỷ qua cửa transitions                                     → 403 + `eCommerce:Order:0010`
      ⑫  huỷ đơn B (Mới, Khách huỷ)                                  → 200, `isCancelled`, nhãn "Đã huỷ"
      ⑬  huỷ B lần hai                                               → 200 (**idempotent**, không nhả chỗ lần 2)
      ⑭  huỷ đơn A (đã xác nhận = hàng đã rời kho)                   → 403 + `eCommerce:Order:0001`
      ⑮  A đi tiếp: Đóng gói → Đã đóng gói → Chờ thu gom → Đang chuyển → Thành công → nhãn "Thành công"
      ⑯  `GET …/orders/{A}/history`                                  → ≥ 8 dòng, trục "Fulfillment"
      ⑰  C: xác nhận → … → Đang hoàn → Xác nhận hoàn → Đã hoàn       → **OnHand trở lại 8**, có phiếu `HOAN-…`
      ⑱  bất biến `tổng sổ cái = OnHand` (SQL)                       → bằng nhau

    🔒 Không in token/cookie. Mã `WP05-<random>` để chạy lại nhiều lần không đụng nhau.
    📌 Probe **để lại** 1 kho + 1 phiếu nhập + 3 đơn + 1 phiếu hoàn (rác kiểm chứng) trong DB dev — cố ý,
    để phiên sau kiểm lại bằng SQL.

.PARAMETER BoQuaHost
    Host đã chạy sẵn ⇒ không bật/tắt Host.

.PARAMETER BoQuaWeb
    Next đã chạy sẵn ở cổng 3000 ⇒ không bật/tắt Next.

.NOTES
    📌 **Seed đơn bằng SQL (có lý do):** đường tạo đơn CÔNG KHAI (`/api/app/don-hang/tao-tu-checkout`) đòi
    **sổ giải trình giá** (`SAL-002`) + **lượt giữ chỗ** (`INV-002`) — chuỗi đó là việc của phiên `SAL-001/002`,
    không phải WP-05. Ở đây **giữ chỗ tạo qua API thật** (để bút toán kho là thật), còn **dòng đơn** seed bằng
    SQL để đo đúng thứ WP-05 sinh ra: **state machine + transition API + tác động kho + 2 màn hình**.

.EXAMPLE
    pwsh -File .\tools\probe-don-hang.ps1
#>
[CmdletBinding()]
param(
    [switch]$BoQuaHost,
    [switch]$BoQuaWeb
)

$ErrorActionPreference = 'Stop'

$gocRepo = Split-Path -Parent $PSScriptRoot
$api = 'http://localhost:54275'
$web = 'http://localhost:3000'
$business = '230222'

$ngauNhien = (Get-Random -Minimum 100000 -Maximum 999999)
$sku = "WP05-SKU-$ngauNhien"
$maKho = "WP05-$ngauNhien"
$maPhieuNhap = "PN-WP05-$ngauNhien"

$soDonA = "WP05-DH-A-$ngauNhien"
$soDonB = "WP05-DH-B-$ngauNhien"
$soDonC = "WP05-DH-C-$ngauNhien"

$idA = [guid]::NewGuid().ToString()
$idB = [guid]::NewGuid().ToString()
$idC = [guid]::NewGuid().ToString()

$khoaA = "WP05CK-A-$ngauNhien"
$khoaC = "WP05CK-C-$ngauNhien"

$canhBao = @()
$hostProc = $null
$webProc = $null
$jar = Join-Path $env:TEMP "wp05-cookies-$ngauNhien.txt"

function In-KetQua {
    param([string]$Nhan, [int]$Status, [string]$Body)

    Write-Host $Nhan
    Write-Host ("   HTTP {0}" -f $Status) -ForegroundColor $(if ($Status -lt 400) { 'Green' } else { 'Yellow' })
    if ($Body.Length -gt 0) {
        $gon = if ($Body.Length -gt 400) { $Body.Substring(0, 400) + '…' } else { $Body }
        Write-Host ("   {0}" -f ($gon -replace '\s+', ' '))
    }
}

function Sql {
    param([Parameter(Mandatory)][string]$CauLenh)

    return ((($CauLenh | docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A) | Out-String).Trim())
}

# ── 0. Host ───────────────────────────────────────────────────────────────────────────────────────────────
if (-not $BoQuaHost) {
    $bin = Join-Path $gocRepo 'src\eCommerce.HttpApi.Host\bin\Debug\net10.0'
    if (-not (Test-Path (Join-Path $bin 'eCommerce.HttpApi.Host.dll'))) {
        Write-Host "KHONG THAY binary Host tai $bin — build truoc." -ForegroundColor Red
        exit 2
    }

    Get-Process eCommerce.HttpApi.Host -ErrorAction SilentlyContinue | Stop-Process -Force

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $api
    $env:AuthServer__Authority = $api

    $hostProc = Start-Process dotnet -ArgumentList 'eCommerce.HttpApi.Host.dll' -WorkingDirectory $bin -PassThru `
        -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\wp05-host.log" -RedirectStandardError "$env:TEMP\wp05-host.err"

    & pwsh -File (Join-Path $gocRepo 'tools\wait-host.ps1') -Url $api -LogFile "$env:TEMP\wp05-host.log" -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Host KHONG len duoc — xem log o tren.' -ForegroundColor Red
        exit 2
    }
}

try {
    # ── 0b. Dev-only: cờ đổi mật khẩu do DbMigrator bật lại (bài học `N5`) ───────────────────────────────
    # 📌 `DbMigrator` bật `ShouldChangePasswordOnNextLogin` ⇒ token trả `invalid_grant`/`ID2024` và probe
    # báo "đăng nhập hỏng" trong khi hệ thống bình thường. Chỉ sửa **DB dev**, ghi log để không ai tưởng là bước thật.
    Sql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "UserName" = ''admin'';' | Out-Null
    Write-Host 'Dev-only: đã tắt cờ đổi mật khẩu của admin (DB dev).' -ForegroundColor DarkGray

    # ── 1. Token Host (tạo kho/phiếu/giữ chỗ — phần SETUP, dùng API thật) ────────────────────────────────
    $token = Invoke-RestMethod -Method Post -Uri "$api/connect/token" -ContentType 'application/x-www-form-urlencoded' `
        -Body @{
        grant_type    = 'password'
        client_id     = 'eCommerce_App'
        client_secret = '1q2w3e*'
        scope         = 'eCommerce offline_access'
        username      = 'admin@abp.io'
        password      = '1q2w3E*'
    }
    $h = @{ Authorization = "Bearer $($token.access_token)" }
    Write-Host 'Token Host: OK (khong in gia tri).' -ForegroundColor Green

    # ── 2. Kho + nhập 10 + giữ chỗ 2 (A) và 1 (C) ───────────────────────────────────────────────────────
    $kho = Invoke-RestMethod -Method Post -Uri "$api/api/app/warehouse" -Headers $h -ContentType 'application/json' `
        -Body (@{ code = $maKho; name = "Kho WP05 $ngauNhien" } | ConvertTo-Json -Compress)
    Write-Host ("① Kho tao: {0} ({1})" -f $kho.code, $kho.id) -ForegroundColor Green

    $nhap = Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills" -Headers $h -ContentType 'application/json' `
        -Body (@{
            number         = $maPhieuNhap
            type           = 0
            mode           = 5
            warehouseId    = $kho.id
            idempotencyKey = "WP05-NHAP-$ngauNhien"
            lines          = @(@{ sku = $sku; quantity = 10 })
        } | ConvertTo-Json -Depth 6 -Compress)
    Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills/$($nhap.id)/post" -Headers $h | Out-Null
    Write-Host ("② Nhập kho 10 × {0} ⇒ tồn 10." -f $sku) -ForegroundColor Green

    foreach ($gc in @(
            @{ khoa = "Checkout:$khoaA"; soLuong = 2 },
            @{ khoa = "Checkout:$khoaC"; soLuong = 1 })) {
        Invoke-RestMethod -Method Post -Uri "$api/api/app/stock-reservation/giu-cho" -Headers $h -ContentType 'application/json' `
            -Body (@{
                warehouseId = $kho.id
                sku         = $sku
                quantity    = $gc.soLuong
                referenceType = 'Checkout'
                referenceId   = $gc.khoa
            } | ConvertTo-Json -Compress) | Out-Null
    }
    Write-Host '③ Giữ chỗ 2 (đơn A) + 1 (đơn C) ⇒ Reserved 3, Available 7.' -ForegroundColor Green

    # ── 3. Seed đơn (SQL, có lý do ở .NOTES) ────────────────────────────────────────────────────────────
    $seed = @"
insert into "OrdOrders" ("Id","TenantId","Number","ChannelCode","CustomerId","Status","PaymentStatus","Currency",
 "Subtotal","DiscountTotal","TaxTotal","ShippingTotal","GrandTotal","Note","ExtraProperties","ConcurrencyStamp",
 "CreationTime","IsDeleted","CheckoutIdempotencyKey","ClosureVersion","FulfillmentStatus","FulfillmentVersion",
 "IsCancelled","PaymentVersion","ReconciliationStatus","ReconciliationVersion","ReturnStatus","RowVersion",
 "Source","Type","WarehouseId","CodAmount","WeightGram")
values
 ('$idA',null,'$soDonA','web',null,0,0,'VND',500000,0,0,0,500000,'WP-05 probe A','{}','wp05-$ngauNhien-a',now(),false,'$khoaA',0,0,0,false,0,1,0,0,0,2,1,'$($kho.id)',500000,500),
 ('$idB',null,'$soDonB','web',null,0,0,'VND',100000,0,0,0,100000,'WP-05 probe B','{}','wp05-$ngauNhien-b',now(),false,null,0,0,0,false,0,1,0,0,0,1,2,null,null,null),
 ('$idC',null,'$soDonC','web',null,0,0,'VND',250000,0,0,0,250000,'WP-05 probe C (hoàn)','{}','wp05-$ngauNhien-c',now(),false,'$khoaC',0,0,0,false,0,1,0,0,0,1,1,'$($kho.id)',250000,500);

-- ⚠️ `OrdOrderLines` là `FullAuditedEntity` (KHÔNG có `ExtraProperties`/`ConcurrencyStamp` — khác `OrdOrders`).
-- 📌 Lượt chạy đầu của probe đã đặp đúng lỗi này: chèn dòng đơn thất bại (psql báo lỗi) ⇒ đơn C "Đã hoàn"
-- báo `Order:0011` *"Đơn KHÔNG có dòng hàng"*. Giữ ghi chú để không ai thêm lại hai cột đó.
insert into "OrdOrderLines" ("Id","TenantId","OrderId","Sku","Quantity","UnitPrice","DiscountAmount","TaxAmount",
 "ReturnedQuantity","CreationTime","IsDeleted")
values
 (gen_random_uuid(),null,'$idA','$sku',2,250000,0,0,0,now(),false),
 (gen_random_uuid(),null,'$idB','$sku',1,100000,0,0,0,now(),false),
 (gen_random_uuid(),null,'$idC','$sku',1,250000,0,0,0,now(),false);
"@

    Sql $seed | Out-Null
    $soDongSeed = Sql ("select count(*) from ""OrdOrderLines"" where ""OrderId"" in ('$idA','$idB','$idC');")
    if ($soDongSeed -ne '3') {
        Write-Host ("SEED DONG DON THAT BAI: dem duoc {0} dong (mong doi 3)." -f $soDongSeed) -ForegroundColor Red
        exit 1
    }
    Write-Host ("④ Seed 3 đơn: A={0} · B={1} · C={2}" -f $soDonA, $soDonB, $soDonC) -ForegroundColor Green

    # ── 4. Next ───────────────────────────────────────────────────────────────────────────────────────────
    $webDaChay = $false
    try {
        $thu = Invoke-WebRequest -Uri "$web/login" -SkipHttpErrorCheck -TimeoutSec 3
        $webDaChay = ($thu.StatusCode -eq 200)
    }
    catch {
        $webDaChay = $false
    }

    if (-not $webDaChay -and -not $BoQuaWeb) {
        $webProc = Start-Process cmd.exe -ArgumentList '/c', 'pnpm --filter @ecommerce/admin-web start' -WorkingDirectory $gocRepo -PassThru `
            -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\wp05-web.log" -RedirectStandardError "$env:TEMP\wp05-web.err"

        $daLen = $false
        for ($i = 0; $i -lt 60; $i++) {
            Start-Sleep -Seconds 1
            try {
                $thu = Invoke-WebRequest -Uri "$web/login" -SkipHttpErrorCheck -TimeoutSec 2
                if ($thu.StatusCode -eq 200) { $daLen = $true; break }
            }
            catch { }
        }

        if (-not $daLen) {
            Write-Host 'Next KHONG len duoc o cong 3000 — xem %TEMP%\wp05-web.log' -ForegroundColor Red
            exit 2
        }
    }
    elseif ($webDaChay) {
        # 🚨 `next start` đang chạy sẵn có thể là **build CŨ** ⇒ trang mới 404 trong khi API xanh (đã dặp ở N5).
        Write-Host '⚠️  Dung Next dang chay san o cong 3000 — neu no la build CU thi trang moi se 404.' -ForegroundColor Yellow
    }

    Write-Host "Next: OK ($web)" -ForegroundColor Green

    # ── 5. Đăng nhập NextAuth (đúng đường người dùng) ───────────────────────────────────────────────────
    $csrf = (curl.exe -s -c $jar "$web/api/auth/csrf" | ConvertFrom-Json).csrfToken
    if ([string]::IsNullOrWhiteSpace($csrf)) {
        Write-Host 'KHONG lay duoc csrfToken cua NextAuth.' -ForegroundColor Red
        exit 1
    }

    $body = "csrfToken=$([uri]::EscapeDataString($csrf))&username=admin%40abp.io&password=1q2w3E%2A&callbackUrl=$([uri]::EscapeDataString("$web/$business"))"
    $maDangNhap = (curl.exe -s -b $jar -c $jar -o NUL -w '%{http_code}' -X POST "$web/api/auth/callback/credentials" `
            -H 'Content-Type: application/x-www-form-urlencoded' --data $body)

    $coSession = (Select-String -Path $jar -Pattern 'authjs.session-token' -Quiet)
    Write-Host ("Dang nhap NextAuth: HTTP {0} · session cookie: {1}" -f $maDangNhap, $(if ($coSession) { 'CO' } else { 'KHONG' })) -ForegroundColor $(if ($coSession) { 'Green' } else { 'Red' })
    if (-not $coSession) { exit 1 }

    function Lay-Trang {
        param([Parameter(Mandatory)][string]$Path)

        $raw = (curl.exe -s -b $jar -w '__HTTP__%{http_code}' "$web$Path") -join "`n"
        $i = $raw.LastIndexOf('__HTTP__')
        [pscustomobject]@{
            Status = [int]$raw.Substring($i + 8)
            Body   = $raw.Substring(0, $i)
        }
    }

    function Goi-Bff {
        param([Parameter(Mandatory)][string]$Method, [Parameter(Mandatory)][string]$Path, $Body = $null)

        $thamSo = @('-s', '-b', $jar, '-w', '__HTTP__%{http_code}', '-X', $Method, "$web$Path")
        if ($null -ne $Body) {
            $tep = Join-Path $env:TEMP "wp05-body-$ngauNhien.json"
            ($Body | ConvertTo-Json -Depth 8 -Compress) | Set-Content -Path $tep -Encoding utf8 -NoNewline
            $thamSo += @('-H', 'Content-Type: application/json', '--data-binary', "@$tep")
        }

        $raw = (curl.exe @thamSo) -join "`n"
        $i = $raw.LastIndexOf('__HTTP__')
        [pscustomobject]@{
            Status = [int]$raw.Substring($i + 8)
            Body   = $raw.Substring(0, $i)
        }
    }

    function Chuyen {
        param([string]$DonId, [int]$Den, [long]$PhienBan, [string]$Nhan)

        $r = Goi-Bff -Method POST -Path "/api/proxy/v1/orders/$DonId/transitions" -Body @{
            toStatus        = $Den
            expectedVersion = $PhienBan
            reasonCode      = "ProbeWP05_$Den"
        }

        In-KetQua -Nhan $Nhan -Status $r.Status -Body $r.Body
        if ($r.Status -ne 200) { $canhBao += "$Nhan tra HTTP $($r.Status)" }

        if ($r.Status -eq 200) { return ($r.Body | ConvertFrom-Json) }
        return $null
    }

    # ── 6. ① DS đơn ───────────────────────────────────────────────────────────────────────────────────────
    $ds = Lay-Trang -Path "/$business/order/manage"
    In-KetQua -Nhan '① TRANG DS DON HANG' -Status $ds.Status -Body ''
    if ($ds.Status -ne 200) { $canhBao += "DS don tra HTTP $($ds.Status)" }
    foreach ($chuoi in @('Đơn hàng', 'Cần xử lý', 'Chờ/Đang xác nhận', 'Đã huỷ')) {
        if ($ds.Body -notmatch [regex]::Escape($chuoi)) { $canhBao += "DS don thieu chuoi '$chuoi'" }
    }

    # ── 7. ② Bộ đếm 14 tab ────────────────────────────────────────────────────────────────────────────────
    $tabs = Goi-Bff -Method GET -Path '/api/proxy/v1/orders/tabs'
    $soTab = if ($tabs.Status -eq 200) { (@(($tabs.Body | ConvertFrom-Json).tabs)).Count } else { 0 }
    Write-Host '② BO DEM 14 TAB (/api/proxy/v1/orders/tabs)'
    Write-Host ("   HTTP {0} · so tab = {1}" -f $tabs.Status, $soTab) -ForegroundColor $(if ($soTab -eq 14) { 'Green' } else { 'Red' })
    if ($tabs.Status -ne 200) { $canhBao += "Tab counts tra HTTP $($tabs.Status)" }
    if ($soTab -ne 14) { $canhBao += "So tab = $soTab (mong doi 14)" }

    # ── 8. ③ DS đơn lọc theo tab + từ khoá ────────────────────────────────────────────────────────────────
    $loc = Goi-Bff -Method GET -Path "/api/proxy/v1/orders?tab=can-xu-ly&q=$soDonA"
    $soDong = if ($loc.Status -eq 200) { (@(($loc.Body | ConvertFrom-Json).items)).Count } else { -1 }
    In-KetQua -Nhan '③ DS DON ?tab=can-xu-ly&q=<so don A>' -Status $loc.Status -Body $loc.Body
    if ($loc.Status -ne 200) { $canhBao += "DS don (loc) tra HTTP $($loc.Status)" }
    if ($soDong -ne 1) { $canhBao += "Loc theo so don A ra $soDong dong (mong doi 1)" }
    if ($loc.Body -notmatch 'Mới') { $canhBao += 'Dong don A khong co nhan "Moi"' }

    # ── 9. ④ Trang chi tiết ───────────────────────────────────────────────────────────────────────────────
    $ct = Lay-Trang -Path "/$business/order/manage/$idA"
    In-KetQua -Nhan '④ TRANG CHI TIET DON A' -Status $ct.Status -Body ''
    if ($ct.Status -ne 200) { $canhBao += "Chi tiet don tra HTTP $($ct.Status)" }
    $ctSach = $ct.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @($soDonA, 'Mới', 'Huỷ đơn', 'Đã xác nhận')) {
        if ($ctSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Chi tiet don thieu chuoi '$chuoi'" }
    }

    # ── 10. ⑤⑥⑦ Đi 3 bước đầu — bước xác nhận sinh bút toán kho ─────────────────────────────────────────
    $a = Chuyen -DonId $idA -Den 9 -PhienBan 0 -Nhan '⑤ A: Moi → Cho xac nhan (9)'
    $a = Chuyen -DonId $idA -Den 10 -PhienBan $a.fulfillmentVersion -Nhan '⑥ A: Cho xac nhan → Dang xac nhan (10)'
    $a = Chuyen -DonId $idA -Den 1 -PhienBan $a.fulfillmentVersion -Nhan '⑦ A: Dang xac nhan → Da xac nhan (1) = XUAT KHO'
    if ($null -ne $a) {
        if ($a.stockEffect -ne 'ChotGiuCho') { $canhBao += "stockEffect = $($a.stockEffect) (mong doi ChotGiuCho)" }
        if ([int]$a.stockAffectedLines -ne 1) { $canhBao += "stockAffectedLines = $($a.stockAffectedLines) (mong doi 1)" }
    }

    # ── 11. ⑧ Tồn kho sau khi chốt giữ chỗ ────────────────────────────────────────────────────────────────
    $ton = Goi-Bff -Method GET -Path "/api/proxy/v1/stock/balances?sku=$sku"
    In-KetQua -Nhan '⑧ TON KHO SAU KHI XAC NHAN (BFF)' -Status $ton.Status -Body $ton.Body
    if ($ton.Status -eq 200) {
        $dongTon = ($ton.Body | ConvertFrom-Json).items | Select-Object -First 1
        if ([double]$dongTon.onHand -ne 8) { $canhBao += "onHand = $($dongTon.onHand) (mong doi 8 = 10 - 2 da xuat)" }
        if ([double]$dongTon.reserved -ne 1) { $canhBao += "reserved = $($dongTon.reserved) (mong doi 1 = 3 - 2 da chot)" }
    }
    else {
        $canhBao += "Ton qua BFF tra HTTP $($ton.Status)"
    }

    # ── 12. ⑨⑩⑪ Ba ca PHẢI BỊ CHẶN ──────────────────────────────────────────────────────────────────────
    $cu = Goi-Bff -Method POST -Path "/api/proxy/v1/orders/$idA/transitions" -Body @{
        toStatus = 2; expectedVersion = 1; reasonCode = 'PhienBanCu'
    }
    In-KetQua -Nhan '⑨ PHIEN BAN CU (expectedVersion=1, hien tai 3)' -Status $cu.Status -Body $cu.Body
    if ($cu.Status -ne 403) { $canhBao += "Phien ban cu tra HTTP $($cu.Status) (mong doi 403)" }
    if ($cu.Body -notmatch 'Order:0003') { $canhBao += 'Phien ban cu khong tra ma Order:0003' }

    $nhayCoc = Goi-Bff -Method POST -Path "/api/proxy/v1/orders/$idA/transitions" -Body @{
        toStatus = 5; expectedVersion = 3; reasonCode = 'NhayCoc'
    }
    In-KetQua -Nhan '⑩ NHAY COC (Da xac nhan → Thanh cong)' -Status $nhayCoc.Status -Body $nhayCoc.Body
    if ($nhayCoc.Status -ne 403) { $canhBao += "Nhay coc tra HTTP $($nhayCoc.Status) (mong doi 403)" }
    if ($nhayCoc.Body -notmatch 'Order:0001') { $canhBao += 'Nhay coc khong tra ma Order:0001' }

    $huyQuaCuaSai = Goi-Bff -Method POST -Path "/api/proxy/v1/orders/$idA/transitions" -Body @{
        toStatus = 14; expectedVersion = 3; reasonCode = 'HuyQuaCuaSai'
    }
    In-KetQua -Nhan '⑪ HUY QUA CUA transitions (phai chi sang /cancel)' -Status $huyQuaCuaSai.Status -Body $huyQuaCuaSai.Body
    if ($huyQuaCuaSai.Status -ne 403) { $canhBao += "Huy qua transitions tra HTTP $($huyQuaCuaSai.Status) (mong doi 403)" }
    if ($huyQuaCuaSai.Body -notmatch 'Order:0010') { $canhBao += 'Huy qua transitions khong tra ma Order:0010' }

    # ── 13. ⑫⑬ Huỷ đơn B (chưa xuất kho) + idempotent ───────────────────────────────────────────────────
    $huyB = Goi-Bff -Method POST -Path "/api/proxy/v1/orders/$idB/cancel" -Body @{
        cancelledBy = 14; expectedVersion = 0; reasonCode = 'KhachYeuCauHuy'; businessReason = 4
    }
    In-KetQua -Nhan '⑫ HUY DON B (Khach huy, ly do "Don trung")' -Status $huyB.Status -Body $huyB.Body
    if ($huyB.Status -ne 200) { $canhBao += "Huy don B tra HTTP $($huyB.Status)" }
    if ($huyB.Status -eq 200) {
        $b = $huyB.Body | ConvertFrom-Json
        if (-not $b.isCancelled) { $canhBao += 'Don B sau khi huy van isCancelled = false' }
        if ($b.statusLabel -ne 'Đã huỷ') { $canhBao += "Nhan don B sau khi huy = '$($b.statusLabel)' (mong doi 'Da huy')" }
    }

    $huyBLan2 = Goi-Bff -Method POST -Path "/api/proxy/v1/orders/$idB/cancel" -Body @{
        cancelledBy = 14; expectedVersion = 0; reasonCode = 'HuyLan2'; businessReason = 4
    }
    In-KetQua -Nhan '⑬ HUY DON B LAN HAI (idempotent — khong nha cho lan 2)' -Status $huyBLan2.Status -Body $huyBLan2.Body
    if ($huyBLan2.Status -ne 200) { $canhBao += "Huy lan hai tra HTTP $($huyBLan2.Status) (mong doi 200 idempotent)" }

    # ── 14. ⑭ Huỷ đơn A (đã xuất kho) ⇒ phải bị chặn ─────────────────────────────────────────────────────
    $huyA = Goi-Bff -Method POST -Path "/api/proxy/v1/orders/$idA/cancel" -Body @{
        cancelledBy = 14; expectedVersion = 0; reasonCode = 'HuySauXuatKho'; businessReason = 16
    }
    In-KetQua -Nhan '⑭ HUY DON A (da xuat kho ⇒ phai TRa HANG, khong duoc huy)' -Status $huyA.Status -Body $huyA.Body
    if ($huyA.Status -ne 403) { $canhBao += "Huy don da xuat kho tra HTTP $($huyA.Status) (mong doi 403)" }
    if ($huyA.Body -notmatch 'Order:0001') { $canhBao += 'Huy don da xuat kho khong tra ma Order:0001' }

    # ── 15. ⑮ A đi tiếp tới Thành công ──────────────────────────────────────────────────────────────────
    $a = Chuyen -DonId $idA -Den 2 -PhienBan 3 -Nhan '⑮a A: Da xac nhan → Dang dong goi (2)'
    $a = Chuyen -DonId $idA -Den 13 -PhienBan $a.fulfillmentVersion -Nhan '⑮b A: → Da dong goi (13)'
    $a = Chuyen -DonId $idA -Den 3 -PhienBan $a.fulfillmentVersion -Nhan '⑮c A: → Cho thu gom (3)'
    $a = Chuyen -DonId $idA -Den 4 -PhienBan $a.fulfillmentVersion -Nhan '⑮d A: → Dang chuyen (4)'
    $a = Chuyen -DonId $idA -Den 5 -PhienBan $a.fulfillmentVersion -Nhan '⑮e A: → THANH CONG (5)'
    if ($null -ne $a -and $a.statusLabel -ne 'Thành công') {
        $canhBao += "Nhan cuoi cua A = '$($a.statusLabel)' (mong doi 'Thanh cong')"
    }

    # ── 16. ⑯ Lịch sử trạng thái ────────────────────────────────────────────────────────────────────────
    $ls = Goi-Bff -Method GET -Path "/api/proxy/v1/orders/$idA/history?maxResultCount=50"
    $soLichSu = if ($ls.Status -eq 200) { [int](($ls.Body | ConvertFrom-Json).totalCount) } else { -1 }
    Write-Host '⑯ LICH SU TRANG THAI DON A'
    Write-Host ("   HTTP {0} · so dong = {1}" -f $ls.Status, $soLichSu) -ForegroundColor $(if ($soLichSu -ge 8) { 'Green' } else { 'Red' })
    if ($ls.Status -ne 200) { $canhBao += "Lich su tra HTTP $($ls.Status)" }
    if ($soLichSu -lt 8) { $canhBao += "Lich su chi co $soLichSu dong (mong doi >= 8)" }

    # ── 17. ⑰ Đơn C: đi đường HOÀN ⇒ nhập lại kho ──────────────────────────────────────────────────────
    $c = Chuyen -DonId $idC -Den 1 -PhienBan 0 -Nhan '⑰a C: Moi → Da xac nhan (1) = XUAT KHO'
    $c = Chuyen -DonId $idC -Den 2 -PhienBan $c.fulfillmentVersion -Nhan '⑰b C: → Dang dong goi (2)'
    $c = Chuyen -DonId $idC -Den 13 -PhienBan $c.fulfillmentVersion -Nhan '⑰c C: → Da dong goi (13)'
    $c = Chuyen -DonId $idC -Den 3 -PhienBan $c.fulfillmentVersion -Nhan '⑰d C: → Cho thu gom (3)'
    $c = Chuyen -DonId $idC -Den 4 -PhienBan $c.fulfillmentVersion -Nhan '⑰e C: → Dang chuyen (4)'
    $c = Chuyen -DonId $idC -Den 7 -PhienBan $c.fulfillmentVersion -Nhan '⑰f C: → Dang hoan (7)'
    $c = Chuyen -DonId $idC -Den 17 -PhienBan $c.fulfillmentVersion -Nhan '⑰g C: → Xac nhan hoan (17)'
    $c = Chuyen -DonId $idC -Den 8 -PhienBan $c.fulfillmentVersion -Nhan '⑰h C: → DA HOAN (8) = NHAP LAI KHO'
    if ($null -ne $c) {
        if ($c.stockEffect -ne 'NhapHangHoan') { $canhBao += "stockEffect buoc hoan = $($c.stockEffect) (mong doi NhapHangHoan)" }
        if ($c.statusLabel -ne 'Đã hoàn') { $canhBao += "Nhan don C = '$($c.statusLabel)' (mong doi 'Da hoan')" }
    }

    $tonSauHoan = Goi-Bff -Method GET -Path "/api/proxy/v1/stock/balances?sku=$sku"
    In-KetQua -Nhan '⑰i TON KHO SAU KHI DON C HOAN (phai ve 8)' -Status $tonSauHoan.Status -Body $tonSauHoan.Body
    if ($tonSauHoan.Status -eq 200) {
        $dongTon2 = ($tonSauHoan.Body | ConvertFrom-Json).items | Select-Object -First 1
        if ([double]$dongTon2.onHand -ne 8) { $canhBao += "onHand sau hoan = $($dongTon2.onHand) (mong doi 8 = 7 + 1 nhap lai)" }
    }

    $phieuHoan = Sql ("select count(*) from ""InvStockDocuments"" where ""Number"" = 'HOAN-$soDonC' and ""Type"" = 3 and ""Status"" = 1;")
    Write-Host ("⑰j PHIEU NHAP HANG HOAN 'HOAN-{0}' (Type=3 CustomerReturn, Status=1 Posted) = {1}" -f $soDonC, $phieuHoan) -ForegroundColor $(if ($phieuHoan -eq '1') { 'Green' } else { 'Red' })
    if ($phieuHoan -ne '1') { $canhBao += "Khong thay phieu CustomerReturn da ghi so 'HOAN-$soDonC' (dem = $phieuHoan)" }

    # ── 18. ⑱ Bất biến tổng sổ cái = ảnh chụp ──────────────────────────────────────────────────────────
    $sqlTong = @"
select coalesce(sum(case when "MovementType" in (0,4,5,6,7,10) then "QuantityDelta"
                         when "MovementType" in (3,8,9,19,11,14,15,20,21) then -"QuantityDelta" else 0 end), 0)
from "InvStockLedger" where "Sku" = '__SKU__' and "IsDeleted" = false;
"@
    $sqlAnh = @"
select coalesce(sum("OnHand"), 0) from "InvItems" where "Sku" = '__SKU__' and "IsDeleted" = false;
"@

    $tongSoCai = Sql ($sqlTong -replace '__SKU__', $sku)
    $anhChup = Sql ($sqlAnh -replace '__SKU__', $sku)

    Write-Host '⑱ BAT BIEN tong so cai = anh chup'
    Write-Host ("   tong so cai = {0} · OnHand = {1}" -f $tongSoCai, $anhChup)
    if ($tongSoCai -ne $anhChup) { $canhBao += "BAT BIEN SAI: tong so cai ($tongSoCai) <> OnHand ($anhChup)" }

    # ── Kết luận ─────────────────────────────────────────────────────────────────────────────────────────
    Write-Host ''
    if ($canhBao.Count -eq 0) {
        Write-Host 'KET LUAN: TAT CA CA DO KHOP.' -ForegroundColor Green
        exit 0
    }

    Write-Host 'KET LUAN: CO CA KHONG KHOP:' -ForegroundColor Red
    $canhBao | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
finally {
    if (Test-Path $jar) { Remove-Item $jar -Force -ErrorAction SilentlyContinue }

    if ($null -ne $webProc -and -not $webProc.HasExited) {
        & taskkill.exe /PID $webProc.Id /T /F 2>$null | Out-Null
        Write-Host 'Da dung Next do chinh probe khoi dong.'
    }

    if ($null -ne $hostProc -and -not $hostProc.HasExited) {
        Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'Da dung Host do chinh probe khoi dong.'
    }

    Write-Host ''
    Write-Host "DU LIEU PROBE: kho=$maKho · SKU=$sku · don A=$soDonA · B=$soDonB · C=$soDonC"
}
