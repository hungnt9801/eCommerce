<#
.SYNOPSIS
    Probe HTTP thật cho **WP-08 — Báo cáo (read-model + Dashboard + 2 báo cáo)**.

.DESCRIPTION
    Chạy **Host thật** (54275) + **Next thật** (3000) + **PostgreSQL thật**, đăng nhập qua **NextAuth**
    (đúng đường người dùng đi), rồi đo:

      ①  `POST /api/v1/reports/aggregate` (hôm nay)            → 200; có dòng sự kiện + dòng tổng hợp + ảnh tồn kho
      ②  chạy LẠI cùng khoảng                                  → **số dòng KHÔNG đổi** (DoD: không nhân đôi)
      ③  `GET  /api/v1/reports/revenue?tuNgay&denNgay`         → tổng doanh thu **= SUM(RetailBills + Orders)**
                                                                   (**tính độc lập bằng SQL**, cùng kỳ)
      ④  `GET  /api/v1/reports/orders/summary`                 → có đơn của probe; giá trị = Σ dòng − chiết khấu
      ⑤  `GET  /api/v1/reports/inventory/summary`              → `daCoDuLieu = true`, ngày chụp = hôm nay
      ⑥  `GET  /api/v1/reports/dashboard`                      → 4 KPI khớp báo cáo doanh thu hôm nay
      ⑦  HOÁ ĐƠN ĐÃ HUỶ + ĐƠN ĐÃ HUỶ                          → **không** vào doanh thu (chạy lại rồi so)
      ⑧  trang `/{business}/report/revenue` + `/report/inventory` + `/{business}` → 200 + chữ kỳ vọng
      ⑨  `GET /api/v1/reports/revenue` khi chưa tổng hợp kỳ cũ → `daTongHop = false` (**không** phải "0 đồng")

    🔒 Không in token/cookie. Mã `WP08-<random>` để chạy lại nhiều lần không đụng nhau.
    📌 Probe **để lại** 1 kho + 1 SKU + vài hoá đơn/đơn hàng (rác kiểm chứng — cố ý, để phiên sau kiểm SQL).

    🚨 **MỘT chỗ dùng SQL ghi dữ liệu**: đơn hàng của probe được chèn thẳng vào `OrdOrders`/`OrdOrderLines`.
    Lý do: `WP-08` là **báo cáo**; đường tạo đơn thật (`CheckoutAppService.TinhVaGiuChoAsync`) cần dựng bảng
    giá/giữ chỗ — việc của `WP-05`/`WP-06` và sẽ làm probe này đo **sai thứ khác**. Đơn chèn có `Number`
    riêng `WP08-DON-…`, **cùng định nghĩa tiền** với đơn thật (Σ dòng − chiết khấu đơn) ⇒ phép so sánh vẫn
    đúng nghĩa. 📌 Tiền lệ: `probe-khach-hang-wp07.ps1` cũng dùng SQL cho `CreatorId`.

.PARAMETER BoQuaHost
    Host đã chạy sẵn ⇒ không bật/tắt Host.

.PARAMETER BoQuaWeb
    Next đã chạy sẵn ở cổng 3000 ⇒ không bật/tắt Next.
#>
[CmdletBinding()]
param(
    [switch]$BoQuaHost,
    [switch]$BoQuaWeb
)

$ErrorActionPreference = 'Stop'
# 🔑 `nen-tang.ps1` = lop "mot cho" cho moi thu khac nhau giua Windows va Linux (`XC-007` P-01…P-06).
#    🚨 Truoc `N12` tep nay con 3 khuon chi-Windows (duong dan output viet cung · co WindowStyle ·
#    goi ban curl cua Windows) ⇒ tren `ubuntu-latest` no **do vi moi truong**, khong phai vi san pham.
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$gocRepo = Split-Path -Parent $PSScriptRoot
$api = 'http://localhost:54275'
$web = 'http://localhost:3000'
$business = '230222'

$ngauNhien = (Get-Random -Minimum 100000 -Maximum 999999)
$maKho = "WP08-$ngauNhien"
$sku = "WP08-SP-$ngauNhien"
$soDon = "WP08-DON-$ngauNhien"
$soDonHuy = "WP08-DONHUY-$ngauNhien"

# Ngày kinh doanh theo **giờ Việt Nam** (UTC+7) — đúng luật `NgayKinhDoanh` phía BE.
$homNay = (Get-Date).ToString('yyyy-MM-dd')
$batDauUtc = ([DateTimeOffset]::Parse("${homNay}T00:00:00+07:00")).UtcDateTime.ToString('o')
$ketThucUtc = ([DateTimeOffset]::Parse("${homNay}T00:00:00+07:00")).AddDays(1).UtcDateTime.ToString('o')

$canhBao = @()
$hostProc = $null
$webProc = $null
$jar = Lay-DuongDanTam "wp08-cookies-$ngauNhien.txt"
$soLanGoiHttp = 0

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
    $bin = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host' -GocRepo $gocRepo
    if (-not (Test-Path (Join-Path $bin 'eCommerce.HttpApi.Host.dll'))) {
        Write-Host "KHONG THAY binary Host tai $bin — build truoc." -ForegroundColor Red
        exit 2
    }

    $giuCong = @(Lay-PidDangNghe -Cong 54275)
    foreach ($pid_ in $giuCong) { Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $api
    $env:AuthServer__Authority = $api

    $hostProc = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa (Lay-DuongDanTam 'wp08-host.log') -TepLogLoi (Lay-DuongDanTam 'wp08-host.err')

    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $api -LogFile (Lay-DuongDanTam 'wp08-host.log') -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Host KHONG len duoc — xem log o tren.' -ForegroundColor Red
        exit 2
    }
}

try {
    # ── 0b. Dev-only: tắt cờ đổi mật khẩu (`D-56` / `WP-13` ①) ─────────────────────────────────────────
    # 📌 `Start-KhongCoDoiMatKhau` (trong `nen-tang.ps1`) THAY cho `UPDATE` viết tay: probe này đi **cả**
    #    đường token Host **và** đăng nhập BFF/NextAuth (BFF cũng xin token bằng password grant) ⇒ cờ phải
    #    tắt **suốt** lượt chạy, và `finally` **BẬT LẠI** (thiếu = commit một admin không bị buộc đổi mật
    #    khẩu + cổng DB nhóm ② đỏ ở lượt sau).
    Start-KhongCoDoiMatKhau

    # ── 1. Token Host (SETUP) ────────────────────────────────────────────────────────────────────────
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
    $soLanGoiHttp += 1
    Write-Host 'Token Host: OK (khong in gia tri).' -ForegroundColor Green

    $kho = Invoke-RestMethod -Method Post -Uri "$api/api/app/warehouse" -Headers $h -ContentType 'application/json' `
        -Body (@{ code = $maKho; name = "Quay WP08 $ngauNhien" } | ConvertTo-Json -Compress)
    $soLanGoiHttp += 1

    $nhap = Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills" -Headers $h -ContentType 'application/json' `
        -Body (@{
            number         = "PN-WP08-$ngauNhien"
            type           = 0
            mode           = 5
            warehouseId    = $kho.id
            idempotencyKey = "WP08-NHAP-$ngauNhien"
            lines          = @(@{ sku = $sku; quantity = 20; unitPrice = 100000 })
        } | ConvertTo-Json -Depth 6 -Compress)
    $soLanGoiHttp += 1
    Invoke-RestMethod -Method Post -Uri "$api/api/v1/stock/bills/$($nhap.id)/post" -Headers $h | Out-Null
    $soLanGoiHttp += 1
    Write-Host "Setup: kho $maKho + nhap 20 x $sku @100.000 ⇒ ton 20." -ForegroundColor Green

    # ── 1b. ĐƠN HÀNG (dev-only SQL) — 3 × 80.000, chiết khấu đơn 15.000 ⇒ doanh thu 225.000 ──────────
    $orderId = [guid]::NewGuid().ToString()
    $orderLineId = [guid]::NewGuid().ToString()

    Sql @"
insert into "OrdOrders"
    ("Id","TenantId","Number","ChannelCode","Status","PaymentStatus","FulfillmentStatus",
     "ReconciliationStatus","ReturnStatus","IsCancelled","FulfillmentVersion","PaymentVersion",
     "ReconciliationVersion","ClosureVersion","RowVersion","Currency","Subtotal","DiscountTotal",
     "TaxTotal","ShippingTotal","GrandTotal","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('$orderId', null, '$soDon', 'web', 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 'VND',
        240000, 15000, 0, 0, 225000, '{}', 'stamp-wp08-don', now(), false);

insert into "OrdOrderLines"
    ("Id","TenantId","OrderId","Sku","Quantity","UnitPrice","DiscountAmount","TaxAmount",
     "CreationTime","IsDeleted")
values ('$orderLineId', null, '$orderId', '$sku', 3, 80000, 0, 0, now(), false);
"@ | Out-Null

    # Đơn ĐÃ HUỶ: 10 × 100.000 ⇒ nếu job tính nhầm thì doanh thu lệch đúng 1.000.000.
    $orderHuyId = [guid]::NewGuid().ToString()
    Sql @"
insert into "OrdOrders"
    ("Id","TenantId","Number","ChannelCode","Status","PaymentStatus","FulfillmentStatus",
     "ReconciliationStatus","ReturnStatus","IsCancelled","FulfillmentVersion","PaymentVersion",
     "ReconciliationVersion","ClosureVersion","RowVersion","Currency","Subtotal","DiscountTotal",
     "TaxTotal","ShippingTotal","GrandTotal","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('$orderHuyId', null, '$soDonHuy', 'web', 5, 0, 0, 0, 0, true, 0, 0, 0, 0, 0, 'VND',
        1000000, 0, 0, 0, 1000000, '{}', 'stamp-wp08-huy', now(), false);

insert into "OrdOrderLines"
    ("Id","TenantId","OrderId","Sku","Quantity","UnitPrice","DiscountAmount","TaxAmount",
     "CreationTime","IsDeleted")
values ('$([guid]::NewGuid())', null, '$orderHuyId', '$sku', 10, 100000, 0, 0, now(), false);
"@ | Out-Null
    Write-Host "Setup: don $soDon (3 × 80.000, CK 15.000 ⇒ 225.000) + don HUY $soDonHuy (1.000.000)." -ForegroundColor Green

    # ── 2. Next ───────────────────────────────────────────────────────────────────────────────────────
    $webDaChay = $false
    try {
        $thu = Invoke-WebRequest -Uri "$web/login" -SkipHttpErrorCheck -TimeoutSec 3
        $webDaChay = ($thu.StatusCode -eq 200)
    }
    catch {
        $webDaChay = $false
    }

    if (-not $webDaChay -and -not $BoQuaWeb) {
        $webProc = Start-NextAdmin -ThuMucLamViec $gocRepo `
            -TepLogRa (Lay-DuongDanTam 'wp08-web.log') -TepLogLoi (Lay-DuongDanTam 'wp08-web.err')

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
            Write-Host 'Next KHONG len duoc o cong 3000 — xem %TEMP%\wp08-web.log' -ForegroundColor Red
            exit 2
        }
    }
    elseif ($webDaChay) {
        Write-Host '⚠️  Dung Next dang chay san o cong 3000 — neu no la build CU thi trang moi se 404.' -ForegroundColor Yellow
    }

    Write-Host "Next: OK ($web)" -ForegroundColor Green

    # ── 3. Đăng nhập NextAuth ─────────────────────────────────────────────────────────────────────────
    $csrf = (& (Lay-Curl) -s -c $jar "$web/api/auth/csrf" | ConvertFrom-Json).csrfToken
    if ([string]::IsNullOrWhiteSpace($csrf)) {
        Write-Host 'KHONG lay duoc csrfToken cua NextAuth.' -ForegroundColor Red
        exit 1
    }

    $body = "csrfToken=$([uri]::EscapeDataString($csrf))&username=admin%40abp.io&password=1q2w3E%2A&callbackUrl=$([uri]::EscapeDataString("$web/$business"))"
    $maDangNhap = (& (Lay-Curl) -s -b $jar -c $jar -o (Lay-DuongDanTam 'dang-nhap.out') -w '%{http_code}' -X POST "$web/api/auth/callback/credentials" `
            -H 'Content-Type: application/x-www-form-urlencoded' --data $body)
    $soLanGoiHttp += 2

    $coSession = (Select-String -Path $jar -Pattern 'authjs.session-token' -Quiet)
    Write-Host ("Dang nhap NextAuth: HTTP {0} · session cookie: {1}" -f $maDangNhap, $(if ($coSession) { 'CO' } else { 'KHONG' })) -ForegroundColor $(if ($coSession) { 'Green' } else { 'Red' })
    if (-not $coSession) { exit 1 }

    function Lay-Trang {
        param([Parameter(Mandatory)][string]$Path)

        $script:soLanGoiHttp += 1
        $raw = (& (Lay-Curl) -s -b $jar -w '__HTTP__%{http_code}' "$web$Path") -join "`n"
        $i = $raw.LastIndexOf('__HTTP__')
        [pscustomobject]@{
            Status = [int]$raw.Substring($i + 8)
            Body   = $raw.Substring(0, $i)
        }
    }

    function Goi-Bff {
        param([Parameter(Mandatory)][string]$Method, [Parameter(Mandatory)][string]$Path, $Body = $null)

        $script:soLanGoiHttp += 1
        $thamSo = @('-s', '-b', $jar, '-w', '__HTTP__%{http_code}', '-X', $Method, "$web$Path")
        if ($null -ne $Body) {
            $tep = Lay-DuongDanTam "wp08-body-$ngauNhien.json"
            ($Body | ConvertTo-Json -Depth 10 -Compress) | Set-Content -Path $tep -Encoding utf8 -NoNewline
            $thamSo += @('-H', 'Content-Type: application/json', '--data-binary', "@$tep")
        }

        $raw = (& (Lay-Curl) @thamSo) -join "`n"
        $i = $raw.LastIndexOf('__HTTP__')
        [pscustomobject]@{
            Status = [int]$raw.Substring($i + 8)
            Body   = $raw.Substring(0, $i)
        }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ① TẠO CHỨNG TỪ BÁN: mở ca + bán 2 món @100.000 (trả đủ)
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $moCa = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/sessions/open' -Body @{
        warehouseId = $kho.id
        openingCash = 0
    }
    if ($moCa.Status -notin @(200, 201)) { $canhBao += "Mo ca tra HTTP $($moCa.Status)" }

    $clientBillA = [guid]::NewGuid().ToString()
    $banA = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = $clientBillA
        warehouseId  = $kho.id
        lines        = @(@{ sku = $sku; quantity = 2; unitPrice = 100000 })
        payments     = @(@{ method = 1; amount = 200000 })
    }
    In-KetQua -Nhan '①a BAN 2 mon @100.000 (hoa don A)' -Status $banA.Status -Body $banA.Body
    if ($banA.Status -ne 200) { $canhBao += "Ban hoa don A tra HTTP $($banA.Status)" }

    # Hoá đơn B rồi HUỶ: nếu job tính cả hoá đơn huỷ thì doanh thu lệch 100.000.
    $clientBillB = [guid]::NewGuid().ToString()
    $banB = Goi-Bff -Method POST -Path '/api/proxy/v1/pos/bills' -Body @{
        clientBillId = $clientBillB
        warehouseId  = $kho.id
        lines        = @(@{ sku = $sku; quantity = 1; unitPrice = 100000 })
        payments     = @(@{ method = 1; amount = 100000 })
    }
    $hoaDonB = $null
    if ($banB.Status -eq 200) {
        $hoaDonB = ($banB.Body | ConvertFrom-Json).bill
        $huy = Goi-Bff -Method POST -Path "/api/proxy/v1/pos/bills/$($hoaDonB.id)/void" -Body @{ reason = 'Probe WP08: kiem tra hoa don huy KHONG vao doanh thu' }
        In-KetQua -Nhan '①b HUY hoa don B (khong duoc vao doanh thu)' -Status $huy.Status -Body $huy.Body
        if ($huy.Status -ne 200) { $canhBao += "Huy hoa don B tra HTTP $($huy.Status)" }
    }
    else { $canhBao += "Ban hoa don B tra HTTP $($banB.Status)" }

    $hoaDonA = if ($banA.Status -eq 200) { ($banA.Body | ConvertFrom-Json).bill } else { $null }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ② CHẠY TỔNG HỢP (lần 1) — đọc lại số dòng đã ghi
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $chay1 = Goi-Bff -Method POST -Path '/api/proxy/v1/reports/aggregate' -Body @{
        tuNgay      = $homNay
        denNgay     = $homNay
        ghiAnhTonKho = $true
    }
    In-KetQua -Nhan '②a CHAY TONG HOP lan 1' -Status $chay1.Status -Body $chay1.Body
    if ($chay1.Status -ne 200) {
        $canhBao += "Chay tong hop lan 1 tra HTTP $($chay1.Status) — kiem tra quyen eCommerce.Reports.Aggregate (chay DbMigrator)"
    }
    $kq1 = if ($chay1.Status -eq 200) { $chay1.Body | ConvertFrom-Json } else { $null }

    $soDongDb1 = [int](Sql "select count(*) from ""RptFactSalesLines"" where ""BusinessDate"" = date '$homNay';")
    $soAggDb1 = [int](Sql "select count(*) from ""RptAggDailySales"" where ""BusinessDate"" = date '$homNay';")
    $soAnhDb1 = [int](Sql "select count(*) from ""RptFactInventorySnapshots"" where ""SnapshotDate"" = date '$homNay';")

    Write-Host ("   DB sau lan 1: {0} dong su kien · {1} dong tong hop · {2} anh ton kho" -f $soDongDb1, $soAggDb1, $soAnhDb1) -ForegroundColor Green

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ③ CHẠY LẠI (lần 2) — DoD: **không nhân đôi**
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $chay2 = Goi-Bff -Method POST -Path '/api/proxy/v1/reports/aggregate' -Body @{
        tuNgay      = $homNay
        denNgay     = $homNay
        ghiAnhTonKho = $true
    }
    In-KetQua -Nhan '②b CHAY TONG HOP lan 2 (cung khoang)' -Status $chay2.Status -Body $chay2.Body

    $soDongDb2 = [int](Sql "select count(*) from ""RptFactSalesLines"" where ""BusinessDate"" = date '$homNay';")
    $soAggDb2 = [int](Sql "select count(*) from ""RptAggDailySales"" where ""BusinessDate"" = date '$homNay';")
    $soAnhDb2 = [int](Sql "select count(*) from ""RptFactInventorySnapshots"" where ""SnapshotDate"" = date '$homNay';")

    Write-Host ("   DB sau lan 2: {0} dong su kien · {1} dong tong hop · {2} anh ton kho" -f $soDongDb2, $soAggDb2, $soAnhDb2) -ForegroundColor Green
    if ($soDongDb1 -ne $soDongDb2) { $canhBao += "Chay lai lam NHAN DOI dong su kien ($soDongDb1 -> $soDongDb2)" }
    if ($soAggDb1 -ne $soAggDb2) { $canhBao += "Chay lai lam NHAN DOI dong tong hop ($soAggDb1 -> $soAggDb2)" }
    if ($soAnhDb1 -ne $soAnhDb2) { $canhBao += "Chay lai lam NHAN DOI anh ton kho ($soAnhDb1 -> $soAnhDb2)" }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ④ DoD: DOANH THU BÁO CÁO = SUM(RetailBills + Orders) — SQL tính ĐỘC LẬP
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $doanhThuApi = Goi-Bff -Method GET -Path "/api/proxy/v1/reports/revenue?tuNgay=$homNay&denNgay=$homNay"
    In-KetQua -Nhan '③ BAO CAO DOANH THU hom nay' -Status $doanhThuApi.Status -Body $doanhThuApi.Body
    if ($doanhThuApi.Status -ne 200) { $canhBao += "Bao cao doanh thu tra HTTP $($doanhThuApi.Status)" }
    $bc = if ($doanhThuApi.Status -eq 200) { $doanhThuApi.Body | ConvertFrom-Json } else { $null }

    $sqlBanLe = Sql @"
select coalesce(sum("Subtotal" - "DiscountTotal"), 0)
from "PosRetailBills"
where "Status" = 0 and "IsDeleted" = false
  and "CreationTime" >= '$batDauUtc' and "CreationTime" < '$ketThucUtc';
"@

    $sqlDonHang = Sql @"
select coalesce(sum(s.amount - o."DiscountTotal"), 0)
from "OrdOrders" o
join (
    select "OrderId", sum("UnitPrice" * "Quantity" - "DiscountAmount") as amount
    from "OrdOrderLines"
    where "IsDeleted" = false
    group by "OrderId"
) s on s."OrderId" = o."Id"
where o."IsCancelled" = false and o."IsDeleted" = false
  and o."CreationTime" >= '$batDauUtc' and o."CreationTime" < '$ketThucUtc';
"@

    $tongSql = [decimal]$sqlBanLe + [decimal]$sqlDonHang
    Write-Host ("   SQL doc lap: ban le {0} + don hang {1} = {2}" -f $sqlBanLe, $sqlDonHang, $tongSql) -ForegroundColor Green
    Write-Host ("   API bao cao: {0}" -f $bc.tongDoanhThu) -ForegroundColor Green

    if ($null -ne $bc) {
        if (-not $bc.daTongHop) { $canhBao += 'Bao cao hom nay tra daTongHop = false (phai la true sau khi chay tong hop)' }
        if ([math]::Abs([decimal]$bc.tongDoanhThu - $tongSql) -gt 0.01) {
            $canhBao += "DoD SAI: bao cao $($bc.tongDoanhThu) != SQL $tongSql (ban le $sqlBanLe + don $sqlDonHang)"
        }
        # Hoá đơn A (200.000) + đơn WP08 (225.000) PHẢI nằm trong tổng.
        if ([decimal]$bc.tongDoanhThu -lt 425000) {
            $canhBao += "Tong doanh thu $($bc.tongDoanhThu) < 425.000 (thieu chung tu cua probe: hoa don A 200.000 + don 225.000)"
        }
        # Hoá đơn B đã huỷ (100.000) và đơn huỷ (1.000.000) KHÔNG được vào.
        if ($null -ne $hoaDonB) {
            $coB = [int](Sql "select count(*) from ""RptFactSalesLines"" where ""DocumentId"" = '$($hoaDonB.id)';")
            Write-Host ("   hoa don HUY B co trong read-model? {0} (phai 0)" -f $coB)
            if ($coB -ne 0) { $canhBao += "Hoa don DA HUY van co $coB dong trong RptFactSalesLines" }
        }
        $coDonHuy = [int](Sql "select count(*) from ""RptFactSalesLines"" where ""DocumentId"" = '$orderHuyId';")
        if ($coDonHuy -ne 0) { $canhBao += "Don DA HUY van co $coDonHuy dong trong RptFactSalesLines" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑤ TỔNG QUAN ĐƠN HÀNG
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $donHang = Goi-Bff -Method GET -Path "/api/proxy/v1/reports/orders/summary?tuNgay=$homNay&denNgay=$homNay"
    In-KetQua -Nhan '④ TONG QUAN DON HANG' -Status $donHang.Status -Body $donHang.Body
    if ($donHang.Status -ne 200) { $canhBao += "Tong quan don hang tra HTTP $($donHang.Status)" }
    else {
        $dh = $donHang.Body | ConvertFrom-Json
        Write-Host ("   {0} don · {1} don huy · gia tri {2}" -f $dh.tongDon, $dh.soDonHuy, $dh.giaTriDon) -ForegroundColor Green
        if ([int]$dh.tongDon -lt 2) { $canhBao += "Tong don = $($dh.tongDon) (mong doi >= 2: don WP08 + don huy)" }
        if ([int]$dh.soDonHuy -lt 1) { $canhBao += "So don huy = $($dh.soDonHuy) (mong doi >= 1 — don huy phai duoc DEM rieng)" }
        if ([decimal]$dh.giaTriDon -lt 225000) { $canhBao += "Gia tri don = $($dh.giaTriDon) (mong doi >= 225.000)" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑥ TỔNG QUAN TỒN KHO (ảnh chụp)
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $tonKho = Goi-Bff -Method GET -Path '/api/proxy/v1/reports/inventory/summary'
    In-KetQua -Nhan '⑤ TONG QUAN TON KHO' -Status $tonKho.Status -Body $tonKho.Body
    if ($tonKho.Status -ne 200) { $canhBao += "Tong quan ton kho tra HTTP $($tonKho.Status)" }
    else {
        $tk = $tonKho.Body | ConvertFrom-Json
        Write-Host ("   ngay chup {0} · {1} kho · {2} SKU · ton {3} · kha dung {4}" -f `
                $tk.ngayChup, $tk.soKho, $tk.soSku, $tk.tongOnHand, $tk.tongKhaDung) -ForegroundColor Green
        if (-not $tk.daCoDuLieu) { $canhBao += 'Ton kho tra daCoDuLieu = false sau khi chay tong hop' }
        if ($tk.ngayChup -ne $homNay) { $canhBao += "Ngay chup = $($tk.ngayChup) (mong doi $homNay)" }
        if ([decimal]$tk.tongOnHand -lt 20) { $canhBao += "Tong ton = $($tk.tongOnHand) (mong doi >= 20)" }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑦ DASHBOARD 4 KPI
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $dashboard = Goi-Bff -Method GET -Path '/api/proxy/v1/reports/dashboard'
    In-KetQua -Nhan '⑥ DASHBOARD 4 KPI' -Status $dashboard.Status -Body $dashboard.Body
    if ($dashboard.Status -ne 200) { $canhBao += "Dashboard tra HTTP $($dashboard.Status)" }
    else {
        $db = $dashboard.Body | ConvertFrom-Json
        if ($null -ne $bc -and [math]::Abs([decimal]$db.doanhThuHomNay - [decimal]$bc.tongDoanhThu) -gt 0.01) {
            $canhBao += "Dashboard ($($db.doanhThuHomNay)) KHAC bao cao doanh thu hom nay ($($bc.tongDoanhThu))"
        }
        if ([int]$db.soHoaDonBanLeHomNay -lt 1) { $canhBao += "Dashboard: so hoa don ban le hom nay = $($db.soHoaDonBanLeHomNay)" }
        if ($null -eq $db.ngayTonKhoMoiNhat) { $canhBao += 'Dashboard: ngayTonKhoMoiNhat = null (chua chup anh ton)' }
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑧ KỲ CHƯA TỔNG HỢP ⇒ `daTongHop = false` (KHÔNG phải "0 đồng")
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $tuXa = (Get-Date).AddDays(-200).ToString('yyyy-MM-dd')
    $denXa = (Get-Date).AddDays(-190).ToString('yyyy-MM-dd')
    $kyCu = Goi-Bff -Method GET -Path "/api/proxy/v1/reports/revenue?tuNgay=$tuXa&denNgay=$denXa"
    if ($kyCu.Status -eq 200) {
        $cu = $kyCu.Body | ConvertFrom-Json
        Write-Host ("   ky cu {0}..{1}: daTongHop={2} · tong={3}" -f $tuXa, $denXa, $cu.daTongHop, $cu.tongDoanhThu) -ForegroundColor Green
        if ($cu.daTongHop) { $canhBao += "Ky CU tra daTongHop = true (phai la false: chua chay tong hop)" }
    }
    else { $canhBao += "Bao cao ky cu tra HTTP $($kyCu.Status)" }

    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    # ⑨ TRANG FE
    # ══════════════════════════════════════════════════════════════════════════════════════════════════
    $trangDt = Lay-Trang -Path "/$business/report/revenue"
    In-KetQua -Nhan '⑦ TRANG BAO CAO DOANH THU' -Status $trangDt.Status -Body ''
    if ($trangDt.Status -ne 200) { $canhBao += "Trang bao cao doanh thu tra HTTP $($trangDt.Status)" }
    $dtSach = $trangDt.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @('Doanh thu', 'Biểu đồ doanh thu', 'Tổng doanh thu', 'Xuất CSV', 'Định nghĩa tiền')) {
        if ($dtSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang doanh thu thieu chuoi '$chuoi'" }
    }

    $trangTk = Lay-Trang -Path "/$business/report/inventory"
    In-KetQua -Nhan '⑧ TRANG BAO CAO TON KHO' -Status $trangTk.Status -Body ''
    if ($trangTk.Status -ne 200) { $canhBao += "Trang bao cao ton kho tra HTTP $($trangTk.Status)" }
    $tkSach = $trangTk.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @('Tồn kho', 'Mặt hàng sắp hết', 'Khả dụng', 'Xuất CSV')) {
        if ($tkSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang ton kho thieu chuoi '$chuoi'" }
    }

    $trangDb = Lay-Trang -Path "/$business"
    In-KetQua -Nhan '⑨ TRANG DASHBOARD' -Status $trangDb.Status -Body ''
    if ($trangDb.Status -ne 200) { $canhBao += "Trang dashboard tra HTTP $($trangDb.Status)" }
    $dbSach = $trangDb.Body -replace '<!--.*?-->', ''
    foreach ($chuoi in @('Tổng quan', 'Doanh thu hôm nay', 'Hoá đơn bán lẻ', 'Tồn khả dụng', 'Doanh thu 7 ngày')) {
        if ($dbSach -notmatch [regex]::Escape($chuoi)) { $canhBao += "Trang dashboard thieu chuoi '$chuoi'" }
    }

    # ── Kết luận ─────────────────────────────────────────────────────────────────────────────────────
    Write-Host ''
    Write-Host ("Tong so loi goi HTTP trong probe: {0}" -f $soLanGoiHttp)
    if ($canhBao.Count -eq 0) {
        Write-Host 'KET LUAN: TAT CA CA DO KHOP.' -ForegroundColor Green
        exit 0
    }

    Write-Host 'KET LUAN: CO CA KHONG KHOP:' -ForegroundColor Red
    $canhBao | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
finally {
    # 🔒 DÒNG ĐẦU TIÊN: `D-56` — cờ bảo mật phải về `true` **kể cả** khi probe `exit` giữa đường.
    BatLai-CoDoiMatKhau

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
    Write-Host "DU LIEU PROBE: kho=$maKho · SKU=$sku · don=$soDon · ngay=$homNay"
}
