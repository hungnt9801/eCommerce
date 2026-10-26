<#
.SYNOPSIS
    Probe CHỐNG OVERSELL — hai request ĐỒNG THỜI trên PostgreSQL thật.

.DESCRIPTION
    Vì sao probe này tồn tại: cổng nghiệm thu GĐ1 #2 nói *"bán vượt tồn kho là không thể — có test
    **2 request đồng thời**"*. Trước 24/10/2026 **không có** phép đo nào như vậy (0 `Task.WhenAll`,
    0 `Start-Job` trong `test/`/`tools/`) ⇒ tuyên bố đó chỉ là **niềm tin**.

    Cách đo: seed 1 SKU với `OnHand = 1`, bắn **hai** `POST /api/app/checkout/tinh-va-giu-cho` **cùng lúc**
    (khác `IdempotencyKey`), rồi đọc DB. Khẳng định:
      ① đúng **MỘT** request thành công, request kia bị chặn bằng **mã nghiệp vụ** (không `500`);
      ② `InvStockReservations` của SKU = **1** (không có lượt giữ chỗ thứ hai);
      ③ `InvItems.Reserved <= OnHand` và `Available = 0` — bất biến cuối cùng.

    🔑 Đây là phép đo **hai chiều**: nếu khoá phân tán bị gỡ, hai request cùng đọc `Available = 1` rồi cùng
    giữ chỗ ⇒ ① **ĐỎ**. (Đã kiểm chứng bằng cách tiêm: xem §"KIỂM CHỨNG NGƯỢC" trong tài liệu `inv-002`.)

.NOTES
    Dùng chung Host :5000 với các probe khác; probe tự bật/tắt.
#>
[CmdletBinding()]
param(
    [int]$Cong = 5000,
    [switch]$GiuDuLieu
)

$ErrorActionPreference = 'Stop'
$goc = Split-Path -Parent $PSScriptRoot
$host_ = "http://localhost:$Cong"

# 🔒 `XC-007` P-01…P-06: mọi script của cổng dùng **cùng một chỗ** cho khác biệt Windows/Linux
# (`Lay-PidDangNghe`, `Start-TienTrinhAn`, `Lay-DuongDanBin`) — cấm API chỉ-Windows trong probe.
. (Join-Path $PSScriptRoot 'nen-tang.ps1')
$GUID_KHO = 'e0000000-0000-0000-0000-0000000000f3'
$GUID_SP = 'e0000000-0000-0000-0000-0000000000f4'
$GUID_SKU = 'e0000000-0000-0000-0000-0000000000f5'
$GUID_BG = 'e0000000-0000-0000-0000-0000000000f6'
$GUID_DONG_BG = 'e0000000-0000-0000-0000-0000000000f7'
$GUID_TON = 'e0000000-0000-0000-0000-0000000000f8'
$SKU = 'K9OV-SKU-01'

$script:soDat = 0
$script:soHong = 0

function Kq([string]$ten, [bool]$dat, [string]$chiTiet = '') {
    if ($dat) {
        $script:soDat++
        Write-Host ("      [{0}] DAT | {1}" -f $ten, $chiTiet)
    } else {
        $script:soHong++
        Write-Host ("      [{0}] HONG | {1}" -f $ten, $chiTiet)
    }
}

function Psql([string]$sql) {
    $r = docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -t -A -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "psql loi: $r" }
    return ($r | Out-String).Trim()
}

function DonDep {
    # 🚨 `S8` bài học: bật Host làm `InventoryReconciliationWorker` (`RunOnStart`) chạy NGAY ⇒ nó ghi một
    # dòng lệch THẬT cho `InvItems` của probe (OnHand=1 mà sổ cái = 0). Xoá **cả** dòng lệch + sổ cái của SKU
    # probe, nếu không lần chạy cổng sau sẽ ĐỎ ở bước ⑥ (`ReconRunMismatches` phải rỗng).
    Psql 'delete from "ReconRunMismatches" where "Sku" = ''K9OV-SKU-01'';' | Out-Null
    Psql 'delete from "InvStockLedger" where "Sku" = ''K9OV-SKU-01'';' | Out-Null
    Psql 'delete from "InvStockReservations" where "Sku" = ''K9OV-SKU-01'';' | Out-Null
    Psql 'delete from "PrcPriceCalculationRecords" where "IdempotencyKey" like ''K9OV-%'';' | Out-Null
    Psql 'delete from "PrcPriceListItems" where "Id" = ''e0000000-0000-0000-0000-0000000000f7'';' | Out-Null
    Psql 'delete from "PrcPriceLists" where "Id" = ''e0000000-0000-0000-0000-0000000000f6'';' | Out-Null
    Psql 'delete from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000f8'';' | Out-Null
    Psql 'delete from "CatProductVariants" where "Id" = ''e0000000-0000-0000-0000-0000000000f5'';' | Out-Null
    Psql 'delete from "CatProducts" where "Id" = ''e0000000-0000-0000-0000-0000000000f4'';' | Out-Null
    Psql 'delete from "InvStockDocuments" where "Number" like ''K9OV-%'';' | Out-Null
    Psql 'delete from "InvWarehouses" where "Id" = ''e0000000-0000-0000-0000-0000000000f3'';' | Out-Null
}

Write-Host '════ PROBE OVERSELL (GĐ1 #2 — HAI REQUEST ĐỒNG THỜI) — HTTP THAT + DB THAT ════'

try {
    # ── ① TIỀN ĐỀ ─────────────────────────────────────────────────────────────────────────────
    DonDep
    Kq 'T00 tien de: da don du lieu probe cu' ((Psql 'select count(*) from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000f8'';') -eq '0') 'con ton = 0'

    Psql @'
insert into "InvWarehouses" ("Id","TenantId","Code","Name","Address","IsActive","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000f3',null,'K9OV-KHO','K9OV Kho probe','Ha Noi',true,'{}','k9ovk',now(),false);
'@ | Out-Null

    Psql @'
insert into "CatProducts" ("Id","TenantId","Code","Name","Slug","Description","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted","Status")
values ('e0000000-0000-0000-0000-0000000000f4',null,'K9OV-SP-01','K9OV San pham','k9ov-san-pham','Probe oversell.','{}','k9ovp',now(),false,1);
insert into "CatProductVariants" ("Id","TenantId","ProductId","Sku","Price","IsActive","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000f5',null,'e0000000-0000-0000-0000-0000000000f4','K9OV-SKU-01',100000,true,now(),false);
'@ | Out-Null

    Psql @'
insert into "PrcPriceLists" ("Id","TenantId","Code","Name","Scope","ChannelCode","ChannelShopId","Currency","EffectiveFrom","EffectiveTo","Priority","Status","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000f6',null,'K9OV-BG','K9OV Bang gia','0',null,null,'VND',now() - interval '1 day',null,0,2,'{}','k9ovbg',now(),false);
insert into "PrcPriceListItems" ("Id","TenantId","PriceListId","Sku","AmountValue","CurrencyCode","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000f7',null,'e0000000-0000-0000-0000-0000000000f6','K9OV-SKU-01',100000,'VND','{}','k9ovd',now(),false);
'@ | Out-Null

    # 🔑 `OnHand = 1` — CHỈ đủ cho MỘT lượt giữ chỗ. Đây là điều kiện của phép thử.
    Psql @'
insert into "InvItems" ("Id","TenantId","WarehouseId","Sku","OnHand","Reserved","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values ('e0000000-0000-0000-0000-0000000000f8',null,'e0000000-0000-0000-0000-0000000000f3','K9OV-SKU-01',1,0,'{}','k9ovt',now(),false);
'@ | Out-Null

    Kq 'T01 seed: OnHand = 1 · Reserved = 0' ((Psql 'select "OnHand" || ''/'' || "Reserved" from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000f8'';') -eq '1/0') 'ton = 1/0'

    # `ShouldChangePasswordOnNextLogin` chan password grant — cung xu ly nhu cac probe khac.
    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = ''ADMIN'';' | Out-Null

    # ── ② HOST ────────────────────────────────────────────────────────────────────────────────
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
    $logHost = Join-Path $goc '_probe-oversell-host.log'

    foreach ($k in @(Lay-PidDangNghe -Cong $Cong)) {
        Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 1

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $logHost -TepLogLoi (Join-Path $goc '_probe-oversell-host.err')

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $host_ -LogFile $logHost `
        -TimeoutSeconds 120 -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    $len = ($LASTEXITCODE -eq 0)
    Kq 'T02 Host len (:5000)' $len 'wait-host exit = 0'
    if (-not $len) { throw 'Host khong len' }

    # ── ③ TOKEN ───────────────────────────────────────────────────────────────────────────────
    $than = @{
        grant_type = 'password'; client_id = 'eCommerce_App'; client_secret = '1q2w3e*'
        username   = 'admin@abp.io'; password = '1q2w3E*'; scope = 'eCommerce'
    }
    $jwt = Invoke-RestMethod -Method Post -Uri "$host_/connect/token" -Body $than -ContentType 'application/x-www-form-urlencoded'
    Kq 'T03 lay token admin' (-not [string]::IsNullOrWhiteSpace($jwt.access_token)) 'co token'

    # ── ④ HAI REQUEST ĐỒNG THỜI ───────────────────────────────────────────────────────────────
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(60)
    $client.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $jwt.access_token)

    function BodyCheckout([string]$khoa) {
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

    $url = "$host_/api/app/checkout/tinh-va-giu-cho"
    $n1 = [System.Net.Http.StringContent]::new((BodyCheckout 'K9OV-A'), [System.Text.Encoding]::UTF8, 'application/json')
    $n2 = [System.Net.Http.StringContent]::new((BodyCheckout 'K9OV-B'), [System.Text.Encoding]::UTF8, 'application/json')

    # 🔑 `PostAsync` KHÔNG await ngay ⇒ hai request nằm cùng lúc trên đường. `WhenAll` mới chờ cả hai.
    $t1 = $client.PostAsync($url, $n1)
    $t2 = $client.PostAsync($url, $n2)
    [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]@($t1, $t2))

    $r1 = $t1.Result; $r2 = $t2.Result
    $b1 = $r1.Content.ReadAsStringAsync().Result
    $b2 = $r2.Content.ReadAsStringAsync().Result
    $s1 = [int]$r1.StatusCode; $s2 = [int]$r2.StatusCode

    $soThanhCong = @($s1, $s2) | Where-Object { $_ -eq 200 } | Measure-Object | Select-Object -ExpandProperty Count
    Kq 'T04 chi MOT request thanh cong' ($soThanhCong -eq 1) ("HTTP = $s1 / $s2")
    Kq 'T05 request con lai bi chan (KHONG 5xx)' (($s1 -ne 200 -and $s1 -lt 500) -or ($s2 -ne 200 -and $s2 -lt 500)) ("HTTP = $s1 / $s2")

    $bodyHong = if ($s1 -ne 200) { $b1 } else { $b2 }
    $coMaNghiepVu = $bodyHong -match 'eCommerce:(Inventory|Pricing|Db):\d+'
    Kq 'T06 loi co MA nghiep vu (khong phai 500)' $coMaNghiepVu ($bodyHong.Substring(0, [Math]::Min(160, $bodyHong.Length)))

    # ── ⑤ BẤT BIẾN CUỐI (DB) ──────────────────────────────────────────────────────────────────
    $soGiuCho = Psql 'select count(*) from "InvStockReservations" where "Sku" = ''K9OV-SKU-01'';'
    Kq 'T07 DB: chi MOT luot giu cho' ($soGiuCho -eq '1') ("so luot = $soGiuCho")

    $ton = Psql 'select "OnHand" || ''/'' || "Reserved" || ''/'' || ("OnHand" - "Reserved") from "InvItems" where "Id" = ''e0000000-0000-0000-0000-0000000000f8'';'
    $phan = $ton.Split('/')
    Kq 'T08 DB: Reserved <= OnHand' ([int]$phan[1] -le [int]$phan[0]) ("OnHand/Reserved/Available = $ton")
    Kq 'T09 DB: Available = 0 (da ban het)' ($phan[2] -eq '0') ("Available = $($phan[2])")

    # ── ⑥ DON DEP ────────────────────────────────────────────────────────────────────────────
    if (-not $GiuDuLieu) {
        try { Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue } catch { }
        foreach ($k in @(Lay-PidDangNghe -Cong $Cong)) {
            Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
        }

        DonDep
        Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = ''ADMIN'';' | Out-Null
        Kq 'T10 don dep: khong con du lieu probe' ((Psql 'select count(*) from "InvStockReservations" where "Sku" = ''K9OV-SKU-01'';') -eq '0') 'giu cho = 0'
    }
}
catch {
    Write-Host ("   ❌ NGOAI LE: " + $_.Exception.Message)
    $script:soHong++
    try {
        if ($tienTrinh) { Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue }
        foreach ($k in @(Lay-PidDangNghe -Cong $Cong)) {
            Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
        }
        DonDep
    } catch { }
}

Write-Host ("════ PROBE OVERSELL: {0} DAT / {1} HONG ════" -f $script:soDat, $script:soHong)
if ($script:soHong -gt 0) { exit 1 }
exit 0
