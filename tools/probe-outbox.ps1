<#
.SYNOPSIS
    Probe OUTBOX — "process chết sau commit ⇒ event CÒN ⇒ restart PHÁT LẠI".

.DESCRIPTION
    Cổng nghiệm thu GĐ1 #5 nói *"không mất event"*. `AbpEventOutbox` tồn tại **chưa đủ** để chứng minh:
    cần đo đúng kịch bản nguy hiểm nhất — **process chết sau khi đã commit** (event mới chỉ nằm trong outbox).

    Cách đo (không cần handler lỗi, không cần "poison payload"):
      ① Bật Host với nhịp relay **600 s** (`AbpEventBusBoxes__PeriodSeconds`) ⇒ event **không** được phát.
      ② Gọi API chuyển trạng thái đơn ⇒ ABP ghi **một** dòng vào `AbpEventOutbox` (cùng transaction).
      ③ **Tắt Host** (mô phỏng process chết) ⇒ khẳng định dòng **CÒN** trong DB.
      ④ Bật lại Host với nhịp **2 s** ⇒ `OutboxSender` phát lại ⇒ dòng **biến mất** + `AbpEventInbox` tăng
         (consumer đã nhận). 🔑 Đây là *"phát lại được"*, không chỉ *"không bị xoá"*.

    ⚠️ Giới hạn: probe chạy **một** Host; biến thể **2 instance** (2 process + Redis lock) vẫn là nợ riêng.
#>
[CmdletBinding()]
param(
    [int]$Cong = 5000
)

$ErrorActionPreference = 'Stop'
$goc = Split-Path -Parent $PSScriptRoot
$host_ = "http://localhost:$Cong"
$orderId = 'e0000000-0000-0000-0000-0000000000fb'

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
function DemInbox { return [int](Psql 'select count(*) from "AbpEventInbox";') }

function DonDep {
    Psql 'delete from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fb'';' | Out-Null
    Psql 'delete from "AbpEventOutbox" where "EventName" like ''%OrderStatusChanged%'';' | Out-Null
}

function BatHost([int]$giay) {
    $env:AbpEventBusBoxes__PeriodSeconds = "$giay"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
    $logHost = Join-Path $goc ("_probe-outbox-host-{0}.log" -f $giay)

    foreach ($k in @(Lay-PidDangNghe -Cong $Cong)) { Stop-Process -Id $k -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 1

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $logHost -TepLogLoi (Join-Path $goc ("_probe-outbox-host-{0}.err" -f $giay))

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $host_ -LogFile $logHost `
        -TimeoutSeconds 120 -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    if ($LASTEXITCODE -ne 0) { throw 'Host khong len' }

    return $tienTrinh
}

function TatHost($tienTrinh) {
    try { Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue } catch { }
    foreach ($k in @(Lay-PidDangNghe -Cong $Cong)) { Stop-Process -Id $k -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 1
}

Write-Host '════ PROBE OUTBOX (GĐ1 #5 — "process chết ⇒ event còn ⇒ restart phát lại") ════'

$tienTrinh = $null

try {
    DonDep
    Kq 'O00 tien de: outbox rong' ((DemOutbox) -eq 0) ("outbox = " + (DemOutbox))

    # ── ① SEED đơn Pending ─────────────────────────────────────────────────────────────────────
    Psql @'
insert into "OrdOrders"
 ("Id","TenantId","Number","ChannelCode","CustomerId","Status","PaymentStatus","Currency",
  "Subtotal","DiscountTotal","TaxTotal","ShippingTotal","GrandTotal",
  "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted",
  "FulfillmentStatus","ReconciliationStatus","ReturnStatus","IsCancelled",
  "FulfillmentVersion","PaymentVersion","ReconciliationVersion","ClosureVersion","RowVersion")
values
 ('e0000000-0000-0000-0000-0000000000fb', null, 'K9OB-001', 'web', null, 0, 0, 'VND',
  100000, 0, 0, 0, 100000,
  '{}', 'x', now(), false,
  0, 1, 0, false,
  0, 0, 0, 0, 0);
'@ | Out-Null
    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = ''ADMIN'';' | Out-Null
    Kq 'O01 seed don Pending' ((Psql 'select count(*) from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fb'';') -eq '1') 'don = 1'

    # ── ② HOST nhịp 600 s ⇒ event KHÔNG được phát ────────────────────────────────────────────
    $tienTrinh = BatHost 600
    Kq 'O02 Host (relay 600 s) len' $true 'wait-host exit = 0'

    $than = @{ grant_type='password'; client_id='eCommerce_App'; client_secret='1q2w3e*'; username='admin@abp.io'; password='1q2w3E*'; scope='eCommerce' }
    $jwt = Invoke-RestMethod -Method Post -Uri "$host_/connect/token" -Body $than -ContentType 'application/x-www-form-urlencoded'
    $h = @{ Authorization = "Bearer $($jwt.access_token)"; 'Content-Type' = 'application/json' }

    $body = @{ orderId=$orderId; toStatus=1; expectedVersion=0; reasonCode='XacNhanDon'; reasonText='probe outbox' } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$host_/api/app/order-status/change-fulfillment-status" -Headers $h -Body $body | Out-Null

    Start-Sleep -Seconds 1
    $soOutbox = DemOutbox
    Kq 'O03 commit xong ⇒ outbox CO dong (relay 600 s chua phat)' ($soOutbox -ge 1) ("outbox = $soOutbox")

    # ⏱️ MỐC THỜI GIAN: đếm **đúng event của probe tạo SAU mốc này**, không đếm tổng (dọn dẹp nền có thể xoá dòng cũ).
    $mocInbox = Psql 'select now();'

    # ── ③ TẮT Host (process chết) ⇒ dòng PHẢI còn ────────────────────────────────────────────
    TatHost $tienTrinh
    $tienTrinh = $null

    $soSauKhiChet = DemOutbox
    Kq 'O04 process CHET ⇒ event VAN CON trong DB (khong mat)' ($soSauKhiChet -ge 1) ("outbox = $soSauKhiChet")

    # ── ④ BẬT LẠI Host nhịp 2 s ⇒ PHÁT LẠI ────────────────────────────────────────────────────
    $tienTrinh = BatHost 2

    $daPhat = $false
    for ($i = 0; $i -lt 20; $i++) {
        if ((DemOutbox) -eq 0) { $daPhat = $true; break }
        Start-Sleep -Milliseconds 500
    }

    Kq 'O05 restart ⇒ relay PHAT LAI (outbox ve 0)' $daPhat ("outbox = " + (DemOutbox))

    Start-Sleep -Seconds 1
    $demMoi = [int](Psql ("select count(*) from ""AbpEventInbox"" where ""EventName"" like '%OrderStatusChanged%' and ""CreationTime"" > '{0}';" -f $mocInbox))
    Kq 'O06 consumer DA NHAN (inbox co dong MOI cho event)' ($demMoi -ge 1) ("moi = $demMoi · moc = $mocInbox")

    Kq 'O07 don da doi trang thai that (Confirmed, v1)' `
        ((Psql 'select "FulfillmentStatus" || ''/'' || "FulfillmentVersion" from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fb'';') -eq '1/1') `
        (Psql 'select "FulfillmentStatus" || ''/'' || "FulfillmentVersion" from "OrdOrders" where "Id" = ''e0000000-0000-0000-0000-0000000000fb'';')
}
catch {
    Write-Host ("   ❌ NGOAI LE: " + $_.Exception.Message)
    $script:soHong++
}
finally {
    if ($tienTrinh) { TatHost $tienTrinh }
    DonDep
    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = ''ADMIN'';' | Out-Null
    Kq 'O08 don dep: khong con don/outbox probe' `
        (((Psql 'select count(*) from "OrdOrders" where "Number" = ''K9OB-001'';') -eq '0') -and ((DemOutbox) -eq 0)) `
        'don = 0 · outbox = 0'
    Remove-Item Env:\AbpEventBusBoxes__PeriodSeconds -ErrorAction SilentlyContinue
}

Write-Host ("════ PROBE OUTBOX: {0} DAT / {1} HONG ════" -f $script:soDat, $script:soHong)
if ($script:soHong -gt 0) { exit 1 }
exit 0
