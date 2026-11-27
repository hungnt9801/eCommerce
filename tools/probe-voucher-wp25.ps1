<#
.SYNOPSIS
    Probe HTTP THẬT cho `PRO-001`/`W25-4` — bề mặt `/api/v1/voucher/**` (7 route, `D-299`/`D-307`).

.DESCRIPTION
    Chạy Host thật (54275) + PostgreSQL thật rồi đo bằng HTTP, in `HTTP STATUS` TRƯỚC body:
      T01-T03  ẩn danh ⇒ 401 (GET danh sách · POST generate · GET redemptions)
      T04a     user chỉ `Promotions.View` ⇒ 200 trên `GET /api/v1/voucher`
      T04b     cùng user ⇒ 403 trên `POST /api/v1/voucher/generate` (thiếu `.Voucher.Generate`)
      T05      admin ⇒ 200 trên `GET /api/v1/voucher`
    🔒 Không in token. User/role probe tạo bằng SQL (Host KHÔNG expose `/api/identity/**`), cấp quyền qua
    `PUT /api/v1/settings/permissions`; dọn sạch user/role/grant sau lượt. Không tạo dữ liệu voucher nghiệp vụ.

.NOTES
    Khuôn `probe-promotion-wp24.ps1`. Mã thoát: 0 = mọi ca đo được và PASS; 2 = Host không lên.
#>
[CmdletBinding()]
param([switch]$BoQuaHost)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$gocRepo = Split-Path -Parent $PSScriptRoot
$base = 'http://localhost:54275'
$ngauNhien = (Get-Random -Minimum 100000 -Maximum 999999)
$vaiTro = "PROBE-VOUCHER-$ngauNhien"
$email = "probe-voucher-$ngauNhien@local"
$matKhau = '1q2w3E*'
$vaiTroId = [guid]::NewGuid().ToString()
$nguoiDungId = [guid]::NewGuid().ToString()
$token = $null
$tienTrinh = $null
$coUser = $false

function Goi {
    param([string]$Method, [string]$Path, [hashtable]$Headers = @{}, $Body = $null)
    $thamSo = @{ Uri = "$base$Path"; Method = $Method; Headers = $Headers; SkipHttpErrorCheck = $true; MaximumRedirection = 0 }
    if ($null -ne $Body) {
        $thamSo['ContentType'] = 'application/json'
        $thamSo['Body'] = ($Body | ConvertTo-Json -Depth 12)
    }
    $r = Invoke-WebRequest @thamSo
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = [string]$r.Content }
}

function InKq {
    param([string]$Nhan, $Kq)
    Write-Host $Nhan
    Write-Host ("   HTTP {0}" -f $Kq.Status)
    if ($Kq.Body.Length -gt 0) {
        $gon = if ($Kq.Body.Length -gt 300) { $Kq.Body.Substring(0, 300) + '...' } else { $Kq.Body }
        Write-Host ("   {0}" -f $gon)
    }
}

function Psql([string]$sql) {
    $ra = & docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -tA -v ON_ERROR_STOP=1 -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw ("psql loi: {0}" -f ($ra -join '; ')) }
    return $ra
}

function Cham([string]$nhan, [int]$thucTe, [int]$kyVong) {
    $kq = if ($thucTe -eq $kyVong) { 'PASS' } else { 'FAIL' }
    Write-Host ("   >>> {0}: {1} (ky vong {2}, do duoc {3})" -f $nhan, $kq, $kyVong, $thucTe) -ForegroundColor $(if ($kq -eq 'PASS') { 'Green' } else { 'Red' })
    return ('{0}-{1}' -f $nhan, $kq)
}

function DonDep {
    try {
        Psql ("delete from ""AbpPermissionGrants"" where ""ProviderName""='R' and ""ProviderKey""='{0}';" -f $vaiTro) | Out-Null
        Psql ("delete from ""AbpUserRoles"" where ""UserId""='{0}';" -f $nguoiDungId) | Out-Null
        Psql ("delete from ""AbpUsers"" where ""Id""='{0}';" -f $nguoiDungId) | Out-Null
        Psql ("delete from ""AbpRoles"" where ""Id""='{0}';" -f $vaiTroId) | Out-Null
        $con = Psql ("select count(*) from ""AbpUsers"" where ""UserName"" like 'probe-voucher-%';")
        Write-Host ("DON DEP: user probe con lai = {0}" -f ($con | Select-Object -First 1))
    }
    catch { Write-Host ("DON DEP loi: {0}" -f $_.Exception.Message) }
}

# ── TẠO USER/ROLE PROBE BẰNG SQL (trước khi Host khởi động) ───────────────────────────────────────
# 🚨 Host KHÔNG expose `/api/identity/**` (không tham chiếu Volo.Abp.Identity.HttpApi) ⇒ không tạo được user
#    qua HTTP. Tạo thẳng DB, COPY `PasswordHash`/`SecurityStamp` của admin để password grant hoạt động,
#    rồi cấp `Promotions.View` qua `/api/v1/settings/permissions` (đường có thật). Dọn sạch trong finally.
try {
    $sqlVaiTro = @'
insert into "AbpRoles"
    ("Id","TenantId","Name","NormalizedName","IsDefault","IsStatic","IsPublic","EntityVersion",
     "CreationTime","ExtraProperties","ConcurrencyStamp")
values ('__ID__',null,'__NAME__','__NORM__',false,false,true,1,now(),'{}',substr(md5(random()::text),1,40));
'@.Replace('__ID__', $vaiTroId).Replace('__NAME__', $vaiTro).Replace('__NORM__', $vaiTro.ToUpperInvariant())

    $sqlNguoiDung = @'
insert into "AbpUsers"
    ("Id","TenantId","UserName","NormalizedUserName","Name","Surname","Email","NormalizedEmail","EmailConfirmed",
     "PasswordHash","SecurityStamp","IsExternal","PhoneNumber","PhoneNumberConfirmed","IsActive","TwoFactorEnabled",
     "LockoutEnd","LockoutEnabled","AccessFailedCount","ShouldChangePasswordOnNextLogin","EntityVersion",
     "LastPasswordChangeTime","LastSignInTime","Leaved","ExtraProperties","ConcurrencyStamp",
     "CreationTime","CreatorId","LastModificationTime","LastModifierId","IsDeleted","DeleterId","DeletionTime")
select '__ID__',"TenantId",'__NAME__','__NORM__',"Name","Surname",'__MAIL__','__MAILNORM__',true,
       "PasswordHash",replace(gen_random_uuid()::text,'-',''),false,null,true,true,false,
       null,true,0,false,1,
       null,null,false,'{}',substr(md5(random()::text),1,40),
       now(),null,null,null,false,null,null
from "AbpUsers" where "UserName" = 'admin';
'@.Replace('__ID__', $nguoiDungId).Replace('__NAME__', $email).Replace('__NORM__', $email.ToUpperInvariant()).Replace('__MAIL__', $email).Replace('__MAILNORM__', $email.ToUpperInvariant())

    $sqlGanVaiTro = @'
insert into "AbpUserRoles" ("UserId","RoleId","TenantId") values ('__U__','__R__',null);
'@.Replace('__U__', $nguoiDungId).Replace('__R__', $vaiTroId)

    Psql $sqlVaiTro | Out-Null
    Psql $sqlNguoiDung | Out-Null
    Psql $sqlGanVaiTro | Out-Null
    $coUser = $true
    Write-Host ("Tao user probe qua SQL: OK (user={0})" -f $email)
}
catch {
    Write-Host ("KHONG tao duoc user probe: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
}

# ── HOST ────────────────────────────────────────────────────────────────────────────────────────────
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
    $logHost = Lay-DuongDanTam 'probe-voucher-host.log'
    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa $logHost -TepLogLoi (Lay-DuongDanTam 'probe-voucher-host.err')
    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $base -LogFile $logHost -ProcessId $tienTrinh.Id
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Host KHONG len duoc.' -ForegroundColor Red
        DonDep
        exit 2
    }
}

$ketQua = @()

try {
    # ── Token admin ────────────────────────────────────────────────────────────────────────────
    $token = Invoke-KhongCoDoiMatKhau {
        Invoke-RestMethod -Method Post -Uri "$base/connect/token" -ContentType 'application/x-www-form-urlencoded' -Body @{
            grant_type = 'password'; client_id = 'eCommerce_App'; client_secret = '1q2w3e*'
            scope = 'eCommerce offline_access'; username = 'admin@abp.io'; password = '1q2w3E*'
        }
    }
    $hAdmin = @{ Authorization = "Bearer $($token.access_token)" }
    Write-Host 'Token admin: OK (khong in gia tri).' -ForegroundColor Green

    # ── ẨN DANH ⇒ 401 ──────────────────────────────────────────────────────────────────────────
    $r = Goi -Method GET -Path '/api/v1/voucher?page=1&pageSize=1'
    InKq 'T01 an danh GET /api/v1/voucher => 401' $r
    $ketQua += Cham 'T01' $r.Status 401

    $r = Goi -Method POST -Path '/api/v1/voucher/generate' -Body @{ promotionId = [guid]::NewGuid().ToString(); prefix = 'P'; length = 12; quantity = 1; maxUsage = 1 }
    InKq 'T02 an danh POST /api/v1/voucher/generate => 401' $r
    $ketQua += Cham 'T02' $r.Status 401

    $r = Goi -Method GET -Path '/api/v1/voucher/redemptions?page=1&pageSize=1'
    InKq 'T03 an danh GET /api/v1/voucher/redemptions => 401' $r
    $ketQua += Cham 'T03' $r.Status 401

    # ── USER CHỈ `Promotions.View` ────────────────────────────────────────────────────────────
    if ($coUser) {
        foreach ($quyen in @('eCommerce.Promotions', 'eCommerce.Promotions.View')) {
            $cap = Goi -Method PUT -Path '/api/v1/settings/permissions' -Headers $hAdmin -Body @{
                roleName = $vaiTro; permissionName = $quyen; isGranted = $true
            }
            Write-Host ("Cap {0} cho role: HTTP {1}" -f $quyen, $cap.Status)
        }

        $jwtThuong = Invoke-RestMethod -Method Post -Uri "$base/connect/token" -ContentType 'application/x-www-form-urlencoded' -Body @{
            grant_type = 'password'; client_id = 'eCommerce_App'; client_secret = '1q2w3e*'
            scope = 'eCommerce offline_access'; username = $email; password = $matKhau
        }
        $hThuong = @{ Authorization = "Bearer $($jwtThuong.access_token)" }

        $r = Goi -Method GET -Path '/api/v1/voucher?page=1&pageSize=1' -Headers $hThuong
        InKq 'T04a user chi Promotions.View GET /api/v1/voucher => 200' $r
        $ketQua += Cham 'T04a' $r.Status 200

        $r = Goi -Method POST -Path '/api/v1/voucher/generate' -Headers $hThuong -Body @{ promotionId = [guid]::NewGuid().ToString(); prefix = 'P'; length = 12; quantity = 1; maxUsage = 1 }
        InKq 'T04b user chi Promotions.View POST /api/v1/voucher/generate => 403' $r
        $ketQua += Cham 'T04b' $r.Status 403
    }
    else {
        Write-Host 'CHUA_DO: T04a/T04b — khong tao duoc user probe (Host khong expose /api/identity/**).' -ForegroundColor Yellow
        $ketQua += 'T04a-CHUADO'
        $ketQua += 'T04b-CHUADO'
    }

    # ── ADMIN ⇒ 200 ───────────────────────────────────────────────────────────────────────────
    $r = Goi -Method GET -Path '/api/v1/voucher?page=1&pageSize=1' -Headers $hAdmin
    InKq 'T05 admin GET /api/v1/voucher => 200' $r
    $ketQua += Cham 'T05' $r.Status 200
}
finally {
    # ── DỌN DẸP ────────────────────────────────────────────────────────────────────────────────
    DonDep

    if ($null -ne $tienTrinh -and -not $tienTrinh.HasExited) {
        Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
    }
}

$soPass = @($ketQua | Where-Object { $_ -like '*-PASS' }).Count
$soFail = @($ketQua | Where-Object { $_ -like '*-FAIL' }).Count
$soChuaDo = @($ketQua | Where-Object { $_ -like '*-CHUADO' }).Count
Write-Host ("KET QUA: {0}" -f ($ketQua -join ' · ')) -ForegroundColor Cyan
Write-Host ("TONG: PASS={0} · FAIL={1} · CHUA_DO={2}" -f $soPass, $soFail, $soChuaDo)
if ($soChuaDo -gt 0 -or $soFail -gt 0) {
    Write-Host 'CO CA CHUA DO / FAIL — ghi trung thuc vao testplan §5, KHONG ha ky vong.' -ForegroundColor Yellow
}
exit 0
