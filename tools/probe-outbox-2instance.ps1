<#
.SYNOPSIS
    Probe OUTBOX 2 INSTANCE — hai Host cùng DB/Redis, cùng relay một hàng đợi.

.DESCRIPTION
    Nợ của P0 #3: "2 instance" (2 process) chưa từng đo. Probe này chạy **hai** `HttpApi.Host` (cổng 5000 và
    5001) trên **cùng** PostgreSQL + Redis, rồi khẳng định:
      ① tạo một event bằng Host A (relay tạm 600 s) ⇒ event nằm trong `AbpEventOutbox`;
      ② bật **cả hai** Host với relay 2 s ⇒ outbox được dọn;
      ③ event đó có **ĐÚNG MỘT** dòng `AbpEventInbox` (không trùng `MessageId`) ⇒ hai relay **không** cùng ghi.

    ⚠️ **GIỚI HẠN ĐO — đọc trước khi trích dẫn:** "đúng một dòng inbox" chứng minh **hai instance phối hợp được**
    và **không tạo hai bản ghi inbox**. Nó **KHÔNG** chứng minh *handler không chạy hai lần* nếu handler
    **không** ghi inbox/idempotent — muốn đo tuyệt đối cần handler có tác dụng phụ **không idempotent**.
    📌 Hệ thống hiện **không** có handler như vậy (mọi handler đều idempotent/theo khoá) ⇒ ghi rõ giới hạn,
    **không** tuyên bố "exactly-once" rộng hơn bằng chứng.
#>
[CmdletBinding()]
param(
    [int]$CongA = 5000,
    [int]$CongB = 5001
)

$ErrorActionPreference = 'Stop'
$goc = Split-Path -Parent $PSScriptRoot
$urlA = "http://localhost:$CongA"
$urlB = "http://localhost:$CongB"
$orderId = 'e0000000-0000-0000-0000-0000000000fc'

. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$script:soDat = 0
$script:soHong = 0

function Kq([string]$ten, [bool]$dat, [string]$chiTiet = '') {
    if ($dat) { $script:soDat++; Write-Host ("      [{0}] DAT | {1}" -f $ten, $chiTiet) }
    else { $script:soHong++; Write-Host ("      [{0}] HONG | {1}" -f $ten, $chiTiet) }
}

function Psql([string]$sql) {
    $r = docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -t -A -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "psql loi: $r" }
    return ($r | Out-String).Trim()
}

function DemOutbox { return [int](Psql 'select count(*) from "AbpEventOutbox";') }

function DonDep {
    Psql 'delete from "OrdStatusHistory" where "OrderId" = ''e0000000-0000-0000-0000-0000000000fc'';' | Out-Null
    Psql 'delete from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fc'';' | Out-Null
    Psql 'delete from "AbpEventOutbox" where "EventName" like ''%OrderStatusChanged%'';' | Out-Null
}

function TatTatCa {
    foreach ($cong in @($CongA, $CongB)) {
        foreach ($k in @(Lay-PidDangNghe -Cong $cong)) { Stop-Process -Id $k -Force -ErrorAction SilentlyContinue }
    }
    Start-Sleep -Seconds 1
}

function BatHost([int]$cong, [int]$giay, [string]$hauTo) {
    $env:ASPNETCORE_URLS = "http://localhost:$cong"
    $env:AbpEventBusBoxes__PeriodSeconds = "$giay"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
    $logHost = Join-Path $goc ("_probe-2inst-{0}.log" -f $hauTo)

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $logHost -TepLogLoi (Join-Path $goc ("_probe-2inst-{0}.err" -f $hauTo))

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url "http://localhost:$cong" -LogFile $logHost `
        -TimeoutSeconds 120 -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    if ($LASTEXITCODE -ne 0) { throw "Host $hauTo khong len" }

    return $tienTrinh
}

Write-Host '════ PROBE OUTBOX 2 INSTANCE (hai Host, cùng DB/Redis) ════'

$hostA = $null
$hostB = $null

try {
    TatTatCa
    DonDep
    Kq 'I00 tien de: outbox rong' ((DemOutbox) -eq 0) ("outbox = " + (DemOutbox))

    Psql @'
insert into "OrdOrders"
 ("Id","TenantId","Number","ChannelCode","CustomerId","Status","PaymentStatus","Currency",
  "Subtotal","DiscountTotal","TaxTotal","ShippingTotal","GrandTotal",
  "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted",
  "FulfillmentStatus","ReconciliationStatus","ReturnStatus","IsCancelled",
  "FulfillmentVersion","PaymentVersion","ReconciliationVersion","ClosureVersion","RowVersion")
values
 ('e0000000-0000-0000-0000-0000000000fc', null, 'K9OB2-001', 'web', null, 0, 0, 'VND',
  100000, 0, 0, 0, 100000,
  '{}', 'x', now(), false,
  0, 1, 0, false,
  0, 0, 0, 0, 0);
'@ | Out-Null
    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = ''ADMIN'';' | Out-Null
    Kq 'I01 seed don Pending' ((Psql 'select count(*) from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fc'';') -eq '1') 'don = 1'

    # ① Host A relay 600 s ⇒ tạo event nhưng KHÔNG phát
    $hostA = BatHost $CongA 600 'a600'

    $than = @{ grant_type='password'; client_id='eCommerce_App'; client_secret='1q2w3e*'; username='admin@abp.io'; password='1q2w3E*'; scope='eCommerce' }
    $jwt = Invoke-RestMethod -Method Post -Uri "$urlA/connect/token" -Body $than -ContentType 'application/x-www-form-urlencoded'
    $h = @{ Authorization = "Bearer $($jwt.access_token)"; 'Content-Type' = 'application/json' }

    $body = @{ orderId=$orderId; toStatus=1; expectedVersion=0; reasonCode='XacNhanDon'; reasonText='probe 2 instance' } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$urlA/api/app/order-status/change-fulfillment-status" -Headers $h -Body $body | Out-Null

    Start-Sleep -Seconds 1
    Kq 'I02 event da vao outbox (relay 600 s chua phat)' ((DemOutbox) -ge 1) ("outbox = " + (DemOutbox))

    Stop-Process -Id $hostA.Id -Force -ErrorAction SilentlyContinue
    $hostA = $null
    TatTatCa

    $moc = Psql 'select now();'

    # ② HAI Host cùng relay 2 s
    $hostA = BatHost $CongA 2 'a2'
    $hostB = BatHost $CongB 2 'b2'
    Kq 'I03 HAI Host cung song' ($null -ne $hostA -and $null -ne $hostB) '5000 + 5001'

    $daDọn = $false
    for ($i = 0; $i -lt 40; $i++) {
        if ((DemOutbox) -eq 0) { $daDọn = $true; break }
        Start-Sleep -Milliseconds 500
    }
    Kq 'I04 outbox duoc don (mot trong hai relay xu ly)' $daDọn ("outbox = " + (DemOutbox))

    Start-Sleep -Seconds 1
    $soInbox = [int](Psql ("select count(*) from ""AbpEventInbox"" where ""EventName"" like '%OrderStatusChanged%' and ""CreationTime"" > '{0}';" -f $moc))
    $soMessage = [int](Psql ("select count(distinct ""MessageId"") from ""AbpEventInbox"" where ""EventName"" like '%OrderStatusChanged%' and ""CreationTime"" > '{0}';" -f $moc))

    Kq 'I05 event co DUNG MOT dong inbox (khong trung)' ($soInbox -eq 1 -and $soMessage -eq 1) ("inbox = $soInbox · distinct MessageId = $soMessage")

    # ⚠️ KHÔNG đếm `fail:`: self-check khởi động CỐ Ý log `fail:` cho các ETO chưa có người nhận
    # (`AddressChangedEto`…). Đó là nợ đã biết, không phải lỗi của relay. Chỉ tìm exception THẬT.
    $mauLoi = 'Unhandled exception|OutOfMemoryException|AbpDbConcurrencyException'
    $loiA = @(Select-String -Path (Join-Path $goc '_probe-2inst-a2.log') -Pattern $mauLoi -ErrorAction SilentlyContinue).Count
    $loiB = @(Select-String -Path (Join-Path $goc '_probe-2inst-b2.log') -Pattern $mauLoi -ErrorAction SilentlyContinue).Count
    Kq 'I06 khong co exception THAT trong log hai Host' ($loiA -eq 0 -and $loiB -eq 0) ("exception A = $loiA · exception B = $loiB")

    Kq 'I07 don da doi trang thai that (Confirmed, v1)' `
        ((Psql 'select "FulfillmentStatus" || ''/'' || "FulfillmentVersion" from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fc'';') -eq '1/1') `
        (Psql 'select "FulfillmentStatus" || ''/'' || "FulfillmentVersion" from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fc'';')
}
catch {
    Write-Host ("   ❌ NGOAI LE: " + $_.Exception.Message)
    $script:soHong++
}
finally {
    if ($hostA) { Stop-Process -Id $hostA.Id -Force -ErrorAction SilentlyContinue }
    if ($hostB) { Stop-Process -Id $hostB.Id -Force -ErrorAction SilentlyContinue }
    TatTatCa
    DonDep
    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = ''ADMIN'';' | Out-Null
    if ($moc) {
        Psql ("delete from ""AbpEventInbox"" where ""EventName"" like '%OrderStatusChanged%' and ""CreationTime"" > '{0}';" -f $moc) | Out-Null
    }
    Kq 'I08 don dep: khong con don/outbox probe' `
        (((Psql 'select count(*) from "OrdOrders" where "Number" = ''K9OB2-001'';') -eq '0') -and ((DemOutbox) -eq 0)) `
        'don = 0 · outbox = 0'
    Remove-Item Env:\AbpEventBusBoxes__PeriodSeconds -ErrorAction SilentlyContinue
    Remove-Item Env:\ASPNETCORE_URLS -ErrorAction SilentlyContinue
}

Write-Host ("════ PROBE OUTBOX 2 INSTANCE: {0} DAT / {1} HONG ════" -f $script:soDat, $script:soHong)
if ($script:soHong -gt 0) { exit 1 }
exit 0
