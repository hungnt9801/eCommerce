#requires -Version 7.0
<#
.SYNOPSIS
    Gieo **danh mục ĐVHC TỐI THIỂU** (1 tỉnh + 1 xã) để các probe/ E2E có dữ liệu tham chiếu
    trên một database MỚI — **không** gọi mạng ngoài.

.DESCRIPTION
    🚨 **VÌ SAO TỆP NÀY TỒN TẠI (`S11`, 21/10/2026 — lần chạy GitHub Actions đầu tiên):**
    runner CI là **máy sạch**: `DbMigrator` tạo schema + seed quyền, nhưng bảng `AddrAdministrativeUnits`
    **rỗng** vì danh mục này được nạp bởi job `AdministrativeUnitSyncWorker` — job **mặc định TẮT**
    (`Address:Sync:Enabled = false`) và gọi **API bên ngoài**. Trên máy dev, danh mục đã có từ trước
    nên **không ai thấy** phụ thuộc này.
    ⇒ Probe ⑨ dừng ở `T00` (*"mã DVHC 01/00004 có thật trong danh mục"*) và **đúng** khi dừng: thiếu
    danh mục thì mọi phép đo sau **vô nghĩa**.

    🔒 **VÌ SAO KHÔNG BẬT SYNC TRÊN CI:** cổng phải **không phụ thuộc dịch vụ ngoài**
    (`XC-007` NFR-011). Bật sync biến một API công cộng thành điều kiện của cổng ⇒ đỏ/xanh theo mạng.

    📌 **ĐÂY LÀ DỮ LIỆU THAM CHIẾU TỐI THIỂU, KHÔNG PHẢI DANH MỤC THẬT:**
    chỉ 2 dòng (tỉnh `01`, xã `00004`) đúng mã + tên của phiên bản schema v2, đủ để probe ⑨ kiểm
    luồng sổ địa chỉ. ⚠️ Nó **không** chứng minh danh mục 3.321 xã đã đầy — đó là việc của
    `AdministrativeUnitSyncWorker` khi người vận hành **chủ động** bật (`ADDR-001`).
    🚨 Không được dùng script này cho production: nó tạo một danh mục **thiếu** mà trông như hợp lệ.

    Idempotent: `ON CONFLICT DO NOTHING` ⇒ chạy lại không nhân đôi, và **không** ghi đè dữ liệu thật
    nếu danh mục đã có.

.PARAMETER TenContainer
    Tên container PostgreSQL (mặc định `ecommerce-postgres`, đúng tên do `docker-compose.yml` đặt).

.PARAMETER TenDb
    Tên database (mặc định `ecommerce`).

.EXAMPLE
    pwsh -File tools/gieo-danh-muc-dvhc.ps1
#>
[CmdletBinding()]
param(
    [string]$TenContainer = 'ecommerce-postgres',
    [string]$TenDb = 'ecommerce'
)

$ErrorActionPreference = 'Stop'

# ── SQL: 2 dòng DUY NHẤT. 📌 `Id` KHÔNG dùng khoảng `e0000000-…` (khoảng dành riêng cho dữ liệu probe —
#    `kiem-db.ps1` nhóm ⑥ quét mọi PK uuid bắt đầu bằng tiền tố đó).
$sql = @'
insert into "AddrAdministrativeUnits"
    ("Id", "TenantId", "SchemaVersion", "Level", "Code", "Name", "ParentCode", "IsActive",
     "LegalBasis", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
values
    ('a1100000-0000-0000-0000-000000000001', null, 2, 1, '01', 'Thành phố Hà Nội', null, true,
     'QĐ 19/2025/QĐ-TTg', '{}', 'seed-dvhc-01', now(), false),
    ('a1100000-0000-0000-0000-000000000002', null, 2, 2, '00004', 'Phường Ba Đình', '01', true,
     'QĐ 19/2025/QĐ-TTg', '{}', 'seed-dvhc-00004', now(), false)
on conflict do nothing;
'@

function Invoke-Psql {
    param([Parameter(Mandatory)][string]$Sql)

    $ra = [IO.Path]::GetTempFileName()
    $loi = [IO.Path]::GetTempFileName()

    try {
        $Sql | docker exec -i $TenContainer psql -U ecommerce -d $TenDb -v ON_ERROR_STOP=1 -t -A -f - 1> $ra 2> $loi
        $ma = $LASTEXITCODE

        if ($ma -ne 0) {
            $noiDungLoi = if (Test-Path $loi) { Get-Content $loi -Raw } else { '' }
            throw ("psql THAT BAI (exit {0}): {1}" -f $ma, $noiDungLoi.Trim())
        }

        if (Test-Path $ra) { Get-Content $ra }
    }
    finally {
        Remove-Item $ra, $loi -Force -ErrorAction SilentlyContinue
    }
}

Write-Output '── Gieo danh muc DVHC TOI THIEU (probe 9 can 01/00004) ──'

Invoke-Psql -Sql $sql | Out-Null

# 🛡️ KHẲNG ĐỊNH: ghi rồi phải ĐỌC LẠI — `insert` không ném không có nghĩa là dòng đã có.
$ketQua = @(Invoke-Psql -Sql @'
select ("SchemaVersion" = 2 and "Level" = 1 and "IsActive")::text || '|' || "Name"
from "AddrAdministrativeUnits" where "Code" = '01' and "ParentCode" is null;
select ("SchemaVersion" = 2 and "Level" = 2 and "IsActive")::text || '|' || "ParentCode" || '|' || "Name"
from "AddrAdministrativeUnits" where "Code" = '00004';
'@ | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })

$tinhOk = @($ketQua | Where-Object { $_ -like 'true|*' }).Count -ge 1
$xaOk = @($ketQua | Where-Object { $_ -eq 'true|01|Phường Ba Đình' }).Count -eq 1

Write-Output ("   Tinh 01: {0}" -f ($(if ($tinhOk) { 'DAT' } else { 'KHONG DAT' })))
Write-Output ("   Xa 00004 (ParentCode = 01): {0}" -f ($(if ($xaOk) { 'DAT' } else { 'KHONG DAT' })))

if (-not ($tinhOk -and $xaOk)) {
    Write-Output '   🚨 Danh muc DVHC toi thieu KHONG dung ⇒ probe 9 se dung o T00 (dung nhu thiet ke).'
    exit 1
}

Write-Output '   ✅ Danh muc DVHC toi thieu SAN SANG (2 dong, khong goi mang ngoai).'
exit 0
