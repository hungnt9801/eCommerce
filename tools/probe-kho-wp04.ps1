<#
.SYNOPSIS
    Probe HTTP thật cho **WP-04 (BE)** — phiếu kho kiểu Nhanh + tồn kho 9 chiều.

.DESCRIPTION
    Chạy **Host thật** (HTTP 54275) + **PostgreSQL thật** rồi đo 10 ca bằng HTTP, in **HTTP STATUS trước** rồi
    mới tới body (quy ước `tools/README-probe.md`):

      ① tạo phiếu `Supplier`/`In` 10 đơn vị (mode Nhanh)             → 201
      ② `GET /api/v1/stock/bills/{id}`                              → 200, direction=In, mode=Supplier
      ③ `POST …/post`                                               → 200, postedEntries=1
      ④ `GET /api/v1/stock/balances?sku=`                            → 200, OnHand=10, Available=10
      ⑤ `POST …/post` **lần hai**                                    → KHÔNG đổi tồn, mã `Inventory:0010`
      ⑥ `POST …/cancel` phiếu đã ghi sổ                              → mã `Inventory:0013`
      ⑦ tạo lại phiếu với **cùng `idempotencyKey`**                  → mã `Inventory:0035` (không tạo phiếu 2)
      ⑧ phiếu `Retail`/`Out` 15 khi chỉ có 10                        → khi `post`: `Inventory:0001`
      ⑨ `GET /api/v1/stock/bills?direction=In&mode=Supplier`         → 200, có phiếu vừa tạo
      ⑩ tổng sổ cái = ảnh chụp (SQL)                                 → bằng nhau

    🔒 Không in token. Không sửa dữ liệu sản phẩm khác. Mã `WP04-<random>` để chạy lại nhiều lần không đụng nhau.

.PARAMETER BoQuaHost
    Dùng khi Host đã chạy sẵn (script sẽ không bật/tắt Host).

.EXAMPLE
    pwsh -File .\tools\probe-kho-wp04.ps1
#>
[CmdletBinding()]
param(
    [switch]$BoQuaHost
)

$ErrorActionPreference = 'Stop'
# 🔑 `nen-tang.ps1` = lop "mot cho" cho moi thu khac nhau giua Windows va Linux (`XC-007` P-01…P-06).
#    🚨 Truoc `N12` tep nay con 3 khuon chi-Windows (duong dan output viet cung · co WindowStyle ·
#    goi ban curl cua Windows) ⇒ tren `ubuntu-latest` no **do vi moi truong**, khong phai vi san pham.
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$gocRepo = Split-Path -Parent $PSScriptRoot
$base = 'http://localhost:54275'
$ngauNhien = (Get-Random -Minimum 100000 -Maximum 999999)
$sku = "WP04-SKU-$ngauNhien"
$maKho = "WP04-$ngauNhien"
$maPhieu = "PN-WP04-$ngauNhien"
$maPhieuBan = "PX-WP04-$ngauNhien"
$idem = "WP04-IDEM-$ngauNhien"


function Goi {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [hashtable]$Headers = @{},
        $Body = $null
    )

    $uri = "$base$Path"
    $thamSo = @{
        Uri                = $uri
        Method             = $Method
        Headers            = $Headers
        SkipHttpErrorCheck = $true
        MaximumRedirection = 0
    }

    if ($null -ne $Body) {
        $thamSo['ContentType'] = 'application/json'
        $thamSo['Body'] = ($Body | ConvertTo-Json -Depth 8)
    }

    $r = Invoke-WebRequest @thamSo

    [pscustomobject]@{
        Status = [int]$r.StatusCode
        Body   = [string]$r.Content
    }
}

function In-KetQua {
    param([string]$Nhan, $Kq)

    Write-Host ("{0}" -f $Nhan)
    Write-Host ("   HTTP {0}" -f $Kq.Status) -ForegroundColor $(if ($Kq.Status -lt 400) { 'Green' } else { 'Yellow' })
    if ($Kq.Body.Length -gt 0) {
        $gon = if ($Kq.Body.Length -gt 700) { $Kq.Body.Substring(0, 700) + '…' } else { $Kq.Body }
        Write-Host ("   {0}" -f $gon)
    }
}

# 🔒 So **SỐ** cho cột `numeric(18,3)` (`D-55` ①): `psql` in `10.000`, và trên máy `vi-VN` dấu `.` là
#    **phân cách nghìn** ⇒ parse **invariant** (không để culture máy quyết định `[decimal]'10.000'`).
function So {
    param([string]$Chuoi)

    return [decimal]::Parse($Chuoi, [System.Globalization.CultureInfo]::InvariantCulture)
}

# ── 0. Host ───────────────────────────────────────────────────────────────────────────────────────────────
$hostProc = $null

if (-not $BoQuaHost) {
    $bin = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host' -GocRepo $gocRepo
    if (-not (Test-Path (Join-Path $bin 'eCommerce.HttpApi.Host.dll'))) {
        Write-Host "KHONG THAY binary Host tai $bin — build truoc." -ForegroundColor Red
        exit 2
    }

    Get-Process eCommerce.HttpApi.Host -ErrorAction SilentlyContinue | Stop-Process -Force

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $base
    $env:AuthServer__Authority = $base

    $hostProc = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa (Lay-DuongDanTam 'wp04-host.log') -TepLogLoi (Lay-DuongDanTam 'wp04-host.err')

    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $base -LogFile (Lay-DuongDanTam 'wp04-host.log') -ProcessId $hostProc.Id
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Host KHONG len duoc — xem log o tren.' -ForegroundColor Red
        exit 2
    }
}

try {
    # ── 1. Token (password grant — D-12/WP-01) ────────────────────────────────────────────────────────────
    # 🔑 `D-56` (`WP-13` ①): `DbMigrator` **bat** co `ShouldChangePasswordOnNextLogin` cua `admin` ⇒ password
    #    grant bi tu choi `invalid_grant`/`ID2024` (⚠️ KHONG phai sai mat khau). `Invoke-KhongCoDoiMatKhau`
    #    (trong `nen-tang.ps1`) **TAT** co trong luc lay token roi **finally BAT LAI** ⇒ probe khong tu
    #    `UPDATE`, va cong DB (nhom ② cua `kiem-db.ps1`) khong bi do sau luot chay.
    $token = Invoke-KhongCoDoiMatKhau {
        Invoke-RestMethod -Method Post -Uri "$base/connect/token" -ContentType 'application/x-www-form-urlencoded' `
            -Body @{
            grant_type    = 'password'
            client_id     = 'eCommerce_App'
            client_secret = '1q2w3e*'
            scope         = 'eCommerce offline_access'
            username      = 'admin@abp.io'
            password      = '1q2w3E*'
        }
    }
    $h = @{ Authorization = "Bearer $($token.access_token)" }
    Write-Host 'Token: OK (khong in gia tri).' -ForegroundColor Green

    # ── 2. Kho ────────────────────────────────────────────────────────────────────────────────────────────
    $khoKq = Goi -Method POST -Path '/api/app/warehouse' -Headers $h -Body @{
        code = $maKho
        name = "Kho WP-04 $ngauNhien"
    }
    if ($khoKq.Status -ge 400) {
        In-KetQua -Nhan '① TẠO KHO — THAT BAI' -Kq $khoKq
        exit 1
    }
    $khoId = ($khoKq.Body | ConvertFrom-Json).id
    Write-Host ("① Kho: {0} ({1}) — HTTP {2}" -f $maKho, $khoId, $khoKq.Status) -ForegroundColor Green

    # ── 3. Phiếu nhập 10 (mode Supplier) ──────────────────────────────────────────────────────────────────
    $taoPhieu = Goi -Method POST -Path '/api/v1/stock/bills' -Headers $h -Body @{
        number           = $maPhieu
        type             = 'GoodsReceipt'
        mode             = 'Supplier'
        warehouseId      = $khoId
        counterpartyType = 'Supplier'
        idempotencyKey   = $idem
        note             = 'WP-04 probe'
        lines            = @(@{ sku = $sku; quantity = 10; vatPercent = 8 })
    }
    In-KetQua -Nhan '② TẠO PHIẾU NHẬP 10 (mode Supplier, type GoodsReceipt)' -Kq $taoPhieu
    if ($taoPhieu.Status -ne 201) { exit 1 }

    $phieu = $taoPhieu.Body | ConvertFrom-Json
    $phieuId = $phieu.id

    $doc = Goi -Method GET -Path "/api/v1/stock/bills/$phieuId" -Headers $h
    In-KetQua -Nhan '③ ĐỌC LẠI PHIẾU (direction/mode/lineCount)' -Kq $doc

    # ── 4. Ghi sổ ─────────────────────────────────────────────────────────────────────────────────────────
    $post = Goi -Method POST -Path "/api/v1/stock/bills/$phieuId/post" -Headers $h
    In-KetQua -Nhan '④ GHI SỔ (post)' -Kq $post

    # ── 5. Tồn kho 9 chiều ────────────────────────────────────────────────────────────────────────────────
    $soDu = Goi -Method GET -Path "/api/v1/stock/balances?sku=$sku" -Headers $h
    In-KetQua -Nhan '⑤ TỒN KHO sau khi nhập 10' -Kq $soDu

    $canhBao = @()
    if ($soDu.Status -eq 200) {
        $dong = ($soDu.Body | ConvertFrom-Json).items[0]
        if ($null -eq $dong) { $canhBao += 'KHONG co dong ton kho nao cho SKU vua nhap' }
        else {
            if ($dong.onHand -ne 10) { $canhBao += "OnHand = $($dong.onHand) (mong doi 10)" }
            if ($dong.available -ne 10) { $canhBao += "Available = $($dong.available) (mong doi 10)" }
            if ($dong.inWarehouse -ne 10) { $canhBao += "InWarehouse = $($dong.inWarehouse) (mong doi 10)" }
        }
    }

    # ── 6. Ghi sổ lần hai ─────────────────────────────────────────────────────────────────────────────────
    $post2 = Goi -Method POST -Path "/api/v1/stock/bills/$phieuId/post" -Headers $h
    In-KetQua -Nhan '⑥ GHI SỔ LẦN HAI (phai bi chan)' -Kq $post2

    $soDu2 = Goi -Method GET -Path "/api/v1/stock/balances?sku=$sku" -Headers $h
    if ($soDu2.Status -eq 200) {
        $dong2 = ($soDu2.Body | ConvertFrom-Json).items[0]
        if ($dong2.onHand -ne 10) { $canhBao += "SAU KHI POST LAI OnHand = $($dong2.onHand) (ton kho bi cong hai lan!)" }
    }

    # ── 7. Huỷ phiếu đã ghi sổ ────────────────────────────────────────────────────────────────────────────
    $huy = Goi -Method POST -Path "/api/v1/stock/bills/$phieuId/cancel" -Headers $h
    In-KetQua -Nhan '⑦ HUỶ PHIẾU ĐÃ GHI SỔ (phai bi chan)' -Kq $huy

    # ── 8. Tạo lại với cùng idempotencyKey ────────────────────────────────────────────────────────────────
    $trung = Goi -Method POST -Path '/api/v1/stock/bills' -Headers $h -Body @{
        number           = "$maPhieu-DUP"
        type             = 'GoodsReceipt'
        mode             = 'Supplier'
        warehouseId      = $khoId
        counterpartyType = 'Supplier'
        idempotencyKey   = $idem
        lines            = @(@{ sku = $sku; quantity = 1 })
    }
    In-KetQua -Nhan '⑧ TẠO LẠI PHIẾU VỚI CÙNG idempotencyKey (phai bi chan)' -Kq $trung

    # ── 9. Bán lẻ 15 khi chỉ có 10 ────────────────────────────────────────────────────────────────────────
    $phieuBan = Goi -Method POST -Path '/api/v1/stock/bills' -Headers $h -Body @{
        number      = $maPhieuBan
        type        = 'GoodsIssue'   # phiếu XUẤT (Nhanh type=2); bút toán do mode quyết định (Retail ⇒ RetailSaleOut)
        mode        = 'Retail'
        warehouseId = $khoId
        lines       = @(@{ sku = $sku; quantity = 15 })
    }
    $phieuBanId = if ($phieuBan.Status -eq 201) { ($phieuBan.Body | ConvertFrom-Json).id } else { $null }
    In-KetQua -Nhan '⑨ TẠO PHIẾU BÁN LẺ 15 (mode Retail)' -Kq $phieuBan

    if ($null -ne $phieuBanId) {
        $postBan = Goi -Method POST -Path "/api/v1/stock/bills/$phieuBanId/post" -Headers $h
        In-KetQua -Nhan '⑩ GHI SỔ PHIẾU BÁN LẺ 15 > tồn 10 (phai bi chan: khong du hang)' -Kq $postBan
    }

    # ── 10. DS phiếu có lọc ──────────────────────────────────────────────────────────────────────────────
    $ds = Goi -Method GET -Path '/api/v1/stock/bills?direction=In&mode=Supplier&page=1&pageSize=5' -Headers $h
    In-KetQua -Nhan '⑪ DS PHIẾU lọc direction=In&mode=Supplier' -Kq $ds

    # ── 11. Bất biến: tổng sổ cái = ảnh chụp (SQL) ───────────────────────────────────────────────────────
    # 📌 Dùng here-string `@' … '@` + thay `__SKU__`: tránh việc PowerShell nuốt dấu `"` của PostgreSQL.
    $sqlTongMau = @'
select coalesce(sum(case when "MovementType" in (0,4,5,6,7,10) then "QuantityDelta"
                         when "MovementType" in (3,8,9,19,11,14,15,20,21) then -"QuantityDelta" else 0 end), 0)
from "InvStockLedger" where "Sku" = '__SKU__' and "IsDeleted" = false;
'@

    $sqlAnhMau = @'
select coalesce("OnHand", 0) from "InvItems" where "Sku" = '__SKU__' and "IsDeleted" = false;
'@

    $tongTho = ((($sqlTongMau -replace '__SKU__', $sku) | docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A) | Out-String).Trim()
    $anhTho = ((($sqlAnhMau -replace '__SKU__', $sku) | docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce -t -A) | Out-String).Trim()

    Write-Host '⑫ BẤT BIẾN tổng sổ cái = ảnh chụp'
    Write-Host ("   tổng sổ cái = {0} · OnHand = {1}" -f $tongTho, $anhTho)

    # 📌 `D-55` ① (`N13`): hai vế là `numeric(18,3)` nên `psql` in `10.000` ⇒ so **SỐ** (parse **invariant**),
    #    ⚠️ **không** so chuỗi. `handover-n13` §5 #4: sửa `WP-13` ① thì đổi luôn chỗ này — hôm nay hai vế
    #    **cùng** scale 3 nên so chuỗi *"đúng"*, nhưng chỉ cần một vế đổi kiểu/scale là **sai âm thầm**.
    if ($tongTho -eq '' -or $anhTho -eq '') {
        $canhBao += "KHONG doc duoc so tu DB (tong='$tongTho', OnHand='$anhTho')"
    }
    else {
        $tongSoCai = So $tongTho
        $anhChup = So $anhTho
        if ($tongSoCai -ne $anhChup) {
            $canhBao += "BAT BIEN SAI: tong so cai ($tongTho) <> OnHand ($anhTho)"
        }
    }

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
    if ($null -ne $hostProc -and -not $hostProc.HasExited) {
        Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'Da dung Host do chinh probe khoi dong.'
    }

    Write-Host ''
    Write-Host "DU LIEU PROBE: kho=$maKho ($khoId) · SKU=$sku · phieu=$maPhieu · idem=$idem"
}
