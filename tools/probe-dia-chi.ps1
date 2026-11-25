<#
.SYNOPSIS
    PROBE MA DIA CHI THEO HANG (ADDR-002) - HTTP THAT tren Host :5000 (P2 `D-233` / `D-243`).

.DESCRIPTION
    Chung minh RUNTIME rang: (a) endpoint `GetUnmappedCodesAsync` co that + dung quyen; (b) `get-unmapped-codes`
    tra 200 khi co token, 401 khi khong; (c) loi nghiep vu 05xx duoc MAP dung status (khong con 403 mac dinh):
       0501 -> 404 · 0502 -> 409 · 0504 -> 409 · 0505 -> 400 · 0510 -> 409.

    HANG RAO: in `HTTP STATUS` TRUOC body (README-probe B1) · NEO ASCII · tien de kiem truoc (A8) ·
    don dep + bat lai co bao mat trong `finally` (D2). Seed du lieu bang SQL (TiemVaoDb).

.EXAMPLE
    pwsh -File tools\probe-dia-chi.ps1
#>
[CmdletBinding()]
param(
    [int]$TimeoutGiay = 180
)

$ErrorActionPreference = 'Stop'

$thuMucTam = Join-Path $PSScriptRoot '_tam-diachi-probe'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

. (Join-Path $PSScriptRoot 'ham-tiem.ps1')
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$host_ = 'http://localhost:5000'
$token = $null
$tienTrinh = $null
$ketQua = [System.Collections.Generic.List[object]]::new()
$chuaDo = [System.Collections.Generic.List[object]]::new()

# Ma hãng + mã dùng cho seed (marker de don dep).
$maHang = 'GHN'
$maChuaXacMinh = 'PROBE-P0'
$maDaDong = 'PROBE-P4'
$unmappedCode = 'PROBE-P9'

# D-249: user thuong KHONG co permission `Addresses.CarrierMapping.Verify` de do 403 runtime.
$probeEmail = 'probe-khong-quyen@local'
$probeRole = 'probe-khong-quyen'
$probeMatKhau = 'Probe1234!'
$probeUserId = $null
$probeRoleId = $null
$tokenThuong = $null

function Kq {
    param([string]$Ten, [bool]$Dat, [string]$ChiTiet = '')
    $ketQua.Add([pscustomobject]@{ Ten = $Ten; Dat = $Dat; ChiTiet = $ChiTiet })
    Write-Output ("   [{0}] {1} | {2}" -f $Ten, $(if ($Dat) { 'DAT' } else { 'KHONG DAT' }), $ChiTiet)
}

# "CHUA DO DUOC" khac "KHONG DAT": moi truong khong cho do (D-249) => KHONG duoc ghi "403 OK",
# nhung cung KHONG tinh la ca that bai (khong ha ky vong, chi ghi no co chu).
function ChuaDo {
    param([string]$Ten, [string]$LyDo = '')
    $chuaDo.Add([pscustomobject]@{ Ten = $Ten; LyDo = $LyDo })
    Write-Output ("   [~] {0} | CHUA DO DUOC | {1}" -f $Ten, $LyDo)
}

function Goi-Api {
    param(
        [string]$PhuongThuc,
        [string]$DuongDan,
        [string]$Token,
        [object]$Body = $null,
        [bool]$KemToken = $true
    )

    $headers = @{}
    if ($KemToken -and -not [string]::IsNullOrWhiteSpace($Token)) {
        $headers['Authorization'] = "Bearer $Token"
    }

    $thamSo = @{
        Method             = $PhuongThuc
        Uri                = ($host_ + $DuongDan)
        Headers            = $headers
        SkipHttpErrorCheck = $true
        StatusCodeVariable = 'maHttp'
        ContentType        = 'application/json'
    }

    if ($null -ne $Body) {
        $thamSo.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }

    $tra = Invoke-RestMethod @thamSo
    $than = ($tra | ConvertTo-Json -Depth 8 -Compress)

    return [pscustomobject]@{ Ma = [int]$maHttp; Than = $than }
}

function Xoa-DuLieuProbe {
    TiemVaoDb -Sql @'
delete from "AddrCarrierMappings" where "CarrierAddressCode" like 'PROBE-%';
'@ -KhongNemKhiThieuKiem | Out-Null

    TiemVaoDb -Sql @'
delete from "AddrAdministrativeUnits" where "Name" like 'PROBEUNIT%';
'@ -KhongNemKhiThieuKiem | Out-Null
}

Write-Output ''
Write-Output '════ PROBE MA DIA CHI THEO HANG (ADDR-002) - HTTP THAT :5000 ════'

$y = [ordered]@{
    chuaXacMinhId = 'e0000000-0000-0000-0000-0000000000c1'
    donViDongId   = 'e0000000-0000-0000-0000-0000000000c2'
    donViChungId  = 'e0000000-0000-0000-0000-0000000000c3'
}

try {
    Xoa-DuLieuProbe

    # ── ① SEED du lieu cho cac nhanh loi (Truoc khi khoi dong Host) ────────────────────────────
    # (a) Don vi DONG (IsActive=false) + (b) Don vi MO (dung cho 0510).
    TiemVaoDb -Sql @'
insert into "AddrAdministrativeUnits"
  ("Id","TenantId","SchemaVersion","Level","Code","Name","ParentCode","IsActive",
   "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values
  ('e0000000-0000-0000-0000-0000000000c2', null, 2, 2, 'ZC2AB', 'PROBEUNIT-DONG', '01', false, '{}', 's', now(), false),
  ('e0000000-0000-0000-0000-0000000000c3', null, 2, 2, 'ZC3AB', 'PROBEUNIT-MO',   '01', true,  '{}', 's', now(), false);
'@ -KiemSql @'
select count(*)::text from "AddrAdministrativeUnits" where "Name" like 'PROBEUNIT%';
'@ -SoDongMongDoi 2 | Out-Null

    # (b) Anh xa CHUA XAC MINH (0502) + (c) Anh xa tro don vi DA DONG (0504)
    #     + (d) HAI anh xa DaXacMinh cung (hang, don vi) (0510).
    TiemVaoDb -Sql @'
insert into "AddrCarrierMappings"
  ("Id","TenantId","CarrierCode","CarrierLevel","CarrierAddressCode","CarrierAddressName",
   "OurAdministrativeUnitId","MatchMethod","Status","Source","SyncedAtUtc",
   "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values
  ('e0000000-0000-0000-0000-0000000000d1', null, 'GHN', 'Phuong', 'PROBE-P0', 'Cho xac minh',
   'e0000000-0000-0000-0000-0000000000c3', 0, 0, 'probe', now(), '{}', 's', now(), false),
  ('e0000000-0000-0000-0000-0000000000d2', null, 'GHN', 'Phuong', 'PROBE-P4', 'Don vi da dong',
   'e0000000-0000-0000-0000-0000000000c2', 0, 1, 'probe', now(), '{}', 's', now(), false),
  ('e0000000-0000-0000-0000-0000000000d3', null, 'GHN', 'Phuong', 'PROBE-P8', 'Da xac minh A',
   'e0000000-0000-0000-0000-0000000000c3', 0, 1, 'probe', now(), '{}', 's', now(), false),
  ('e0000000-0000-0000-0000-0000000000d4', null, 'GHN', 'Xa', 'PROBE-P9', 'Da xac minh B',
   'e0000000-0000-0000-0000-0000000000c3', 0, 1, 'probe', now(), '{}', 's', now(), false);
'@ -KiemSql @'
select count(*)::text from "AddrCarrierMappings" where "CarrierAddressCode" like 'PROBE-%';
'@ -SoDongMongDoi 4 | Out-Null

    # ── ② KHOI DONG HOST ───────────────────────────────────────────────────────────────────────-
    $soKey = 0
    try {
        $demKey = & docker exec ecommerce-redis sh -c "redis-cli --scan --pattern '*PermissionGrant*' | wc -l" 2>$null
        if ($null -ne $demKey) { $soKey = [int](("$demKey").Trim()) }
        & docker exec ecommerce-redis sh -c 'redis-cli --scan --pattern "*PermissionGrant*" | while read k; do redis-cli DEL "$k" > /dev/null; done' 2>$null | Out-Null
    } catch {
        Write-Output ("   ⚠️ Khong xoa duoc cache quyen: {0}" -f $_.Exception.Message)
    }
    Write-Output ("   Xoa cache quyen: {0} key (0 key = mau SAI, khong phai 'khong co gi')" -f $soKey)

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
    $logHost = Join-Path $thuMucTam 'host.log'

    foreach ($cong in @(5000)) {
        foreach ($k in @(Lay-PidDangNghe -Cong $cong)) {
            Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Seconds 1

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $logHost -TepLogLoi (Join-Path $thuMucTam 'host.err')

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $host_ -LogFile $logHost `
        -TimeoutSeconds $TimeoutGiay -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    if ($LASTEXITCODE -ne 0) {
        throw ("Host KHONG len trong {0}s. Xem log: {1}" -f $TimeoutGiay, $logHost)
    }

    # ── ③ TOKEN (password grant; tat co bao mat tam thoi) ──────────────────────────────────────
    TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = 'ADMIN';
'@ -KiemSql @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and not "ShouldChangePasswordOnNextLogin";
'@ -SoDongMongDoi 1 | Out-Null

    $jwt = Invoke-RestMethod -Method Post -Uri ($host_ + '/connect/token') -Body @{
        grant_type    = 'password'
        client_id     = 'eCommerce_App'
        client_secret = '1q2w3e*'
        username      = 'admin@abp.io'
        password      = '1q2w3E*'
        scope         = 'eCommerce offline_access'
    } -ContentType 'application/x-www-form-urlencoded'

    $token = $jwt.access_token
    Kq 'T01 lay token admin' (-not [string]::IsNullOrWhiteSpace($token)) ("do dai = {0}" -f ($token | Measure-Object -Character).Characters)

    # ── ④ GET GetUnmappedCodesAsync: 401 khong token / 200 co token ─────────────────────────────
    # Route THAT (in bang `tools\route-check.ps1 -Match carrier-address` 26/11/2026): `unmapped-codes` (GET).
    $r401 = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/carrier-address-mapping/unmapped-codes?CarrierCode=" + $maHang) -KemToken $false
    Kq 'T02 GET unmapped-codes KHONG token => 401' ($r401.Ma -eq 401) ("HTTP {0}" -f $r401.Ma)

    $r200 = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/carrier-address-mapping/unmapped-codes?CarrierCode=" + $maHang) -Token $token
    Kq 'T03 GET unmapped-codes CO token => 200' ($r200.Ma -eq 200) ("HTTP {0}" -f $r200.Ma)

    # ── ⑤ POST XacMinhAsync: 401 khong token (proves endpoint gan quyen) ────────────────────────
    # Route THAT: `{id}/xac-minh` (POST) — khong phai `xac-minh/{id}`.
    $rXac401 = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/carrier-address-mapping/" + $y.chuaXacMinhId + "/xac-minh") -KemToken $false
    Kq 'T04 POST {id}/xac-minh KHONG token => 401' ($rXac401.Ma -eq 401) ("HTTP {0}" -f $rXac401.Ma)

    # ── ⑥ LOI NGHIEP VU 05xx => STATUS MAP DUNG (P2 `D-233`) ───────────────────────────────────-
    $r0501 = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/carrier-address-mapping/tra-ma-hang' -Token $token -Body @{
        carrierCode = $maHang; carrierAddressCode = 'KHONG-TON-TAI-0501'
    }
    Kq 'T05 tra-ma-hang ma la => 404 + eCommerce:Address:0501' `
        ($r0501.Ma -eq 404 -and $r0501.Than -like '*eCommerce:Address:0501*') ("HTTP {0} | {1}" -f $r0501.Ma, $r0501.Than)

    $r0505 = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/carrier-address-mapping/tra-ma-hang' -Token $token -Body @{
        carrierCode = ''; carrierAddressCode = 'X'
    }
    Kq 'T06 tra-ma-hang hang rong => 400 + eCommerce:Address:0505' `
        ($r0505.Ma -eq 400 -and $r0505.Than -like '*eCommerce:Address:0505*') ("HTTP {0} | {1}" -f $r0505.Ma, $r0505.Than)

    $r0502 = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/carrier-address-mapping/tra-ma-hang' -Token $token -Body @{
        carrierCode = $maHang; carrierAddressCode = $maChuaXacMinh
    }
    Kq 'T07 tra-ma-hang ma CHUA xac minh => 409 + eCommerce:Address:0502' `
        ($r0502.Ma -eq 409 -and $r0502.Than -like '*eCommerce:Address:0502*') ("HTTP {0} | {1}" -f $r0502.Ma, $r0502.Than)

    $r0504 = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/carrier-address-mapping/tra-ma-hang' -Token $token -Body @{
        carrierCode = $maHang; carrierAddressCode = $maDaDong
    }
    Kq 'T08 tra-ma-hang don vi DA DONG => 409 + eCommerce:Address:0504' `
        ($r0504.Ma -eq 409 -and $r0504.Than -like '*eCommerce:Address:0504*') ("HTTP {0} | {1}" -f $r0504.Ma, $r0504.Than)

    $r0510 = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/carrier-address-mapping/resolve-carrier-address-code' -Token $token -Body @{
        carrierCode = $maHang; ourAdministrativeUnitId = $y.donViChungId
    }
    Kq 'T09 resolve-carrier-address-code HAI DaXacMinh cung dich => 409 + eCommerce:Address:0510' `
        ($r0510.Ma -eq 409 -and $r0510.Than -like '*eCommerce:Address:0510*') ("HTTP {0} | {1}" -f $r0510.Ma, $r0510.Than)

    # ── ⑦ 403 KHI THIEU QUYEN `Addresses.CarrierMapping.Verify` (`D-249`) ────────────────────────
    # (1) dam bao ROLE khong co quyen: tao role moi neu chua co — KHONG cap permission nao (khong sua seed).
    $vaiTroProbe = $null
    $rDsVaiTro = Goi-Api -PhuongThuc 'Get' -DuongDan ('/api/identity/roles?Filter=' + $probeRole + '&MaxResultCount=10') -Token $token

    if ($rDsVaiTro.Ma -eq 404) {
        # HOST KHONG expose Identity HTTP API (thieu `Volo.Abp.Identity.HttpApi`) => khong tao duoc user qua HTTP.
        # D-249: ghi "CHUA DO DUOC", KHONG duoc ghi "403 OK"; no chu `qa`, han `W18-5`.
        ChuaDo 'T11-T13 do 403 (thieu Verify)' `
            'Host KHONG expose /api/identity/** (404) => khong tao duoc user thuong; chua do runtime 403'
    }
    else {
        if ($rDsVaiTro.Ma -eq 200) {
            $vaiTroProbe = @((($rDsVaiTro.Than | ConvertFrom-Json).items) | Where-Object { $_.name -eq $probeRole }) | Select-Object -First 1
        }
        if ($null -eq $vaiTroProbe) {
            $rTaoVaiTro = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/identity/roles' -Token $token -Body @{
                name = $probeRole; isPublic = $false; isDefault = $false
            }
            if ($rTaoVaiTro.Ma -eq 200 -or $rTaoVaiTro.Ma -eq 201) {
                $vaiTroProbe = ($rTaoVaiTro.Than | ConvertFrom-Json)
            }
        }
        if ($null -ne $vaiTroProbe) { $probeRoleId = $vaiTroProbe.id }

        # (2) dam bao USER thuong (email co dinh `probe-khong-quyen@local`), gan role KHONG co quyen Verify.
        $nguoiDungProbe = $null
        $rDsNd = Goi-Api -PhuongThuc 'Get' -DuongDan ('/api/identity/users?Filter=' + $probeEmail + '&MaxResultCount=10') -Token $token
        if ($rDsNd.Ma -eq 200) {
            $nguoiDungProbe = @((($rDsNd.Than | ConvertFrom-Json).items) | Where-Object { $_.email -eq $probeEmail }) | Select-Object -First 1
        }
        if ($null -eq $nguoiDungProbe) {
            $rTaoNd = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/identity/users' -Token $token -Body @{
                userName             = $probeEmail
                email                = $probeEmail
                password             = $probeMatKhau
                name                 = 'Probe'
                surname              = 'KhongQuyen'
                isActive             = $true
                lockoutEnabled       = $true
                emailConfirmed        = $true
                phoneNumberConfirmed = $true
                roleNames            = @($probeRole)
            }
            if ($rTaoNd.Ma -eq 200 -or $rTaoNd.Ma -eq 201) {
                $nguoiDungProbe = ($rTaoNd.Than | ConvertFrom-Json)
            }
            else {
                ChuaDo 'T11 tao user thuong (khong quyen Verify)' `
                    ("KHONG TAO DUOC user probe (HTTP {0}) => chua do duoc 403, KHONG ghi '403 OK'" -f $rTaoNd.Ma)
            }
        }
        if ($null -ne $nguoiDungProbe) {
            $probeUserId = $nguoiDungProbe.id
            # Gan lai role moi lan chay (idempotent) — role nay KHONG co permission nao.
            $rGanVaiTro = Goi-Api -PhuongThuc 'Put' -DuongDan ('/api/identity/users/' + $probeUserId + '/roles') -Token $token -Body @{
                roleNames = @($probeRole)
            }
            Kq 'T11 gan role KHONG co Addresses.CarrierMapping.Verify' `
                ($rGanVaiTro.Ma -eq 200 -or $rGanVaiTro.Ma -eq 204) ("HTTP {0}" -f $rGanVaiTro.Ma)

            # (3) password grant => token thuong; chung minh token DUNG DUOC bang GET unmapped-codes => 200.
            try {
                $jwtThuong = Invoke-RestMethod -Method Post -Uri ($host_ + '/connect/token') -Body @{
                    grant_type    = 'password'
                    client_id     = 'eCommerce_App'
                    client_secret = '1q2w3e*'
                    username      = $probeEmail
                    password      = $probeMatKhau
                    scope         = 'eCommerce offline_access'
                } -ContentType 'application/x-www-form-urlencoded'
                $tokenThuong = $jwtThuong.access_token
            }
            catch {
                $tokenThuong = $null
                ChuaDo 'T11b dang nhap user thuong' ("password grant loi: {0}" -f $_.Exception.Message)
            }

            if (-not [string]::IsNullOrWhiteSpace($tokenThuong)) {
                Kq 'T11b dang nhap user thuong (password grant)' $true ("do dai = {0}" -f ($tokenThuong | Measure-Object -Character).Characters)

                $rThuongGet = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/carrier-address-mapping/unmapped-codes?CarrierCode=" + $maHang) -Token $tokenThuong
                Kq 'T12 GET unmapped-codes bang token THUONG => 200 (token dung duoc)' `
                    ($rThuongGet.Ma -eq 200) ("HTTP {0}" -f $rThuongGet.Ma)

                # (4) POST xac-minh bang user KHONG co quyen => 403 (khong 401, khong 200).
                $rThuongXac = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/carrier-address-mapping/" + $y.chuaXacMinhId + "/xac-minh") -Token $tokenThuong
                Kq 'T13 POST {id}/xac-minh bang token THUONG (thieu Verify) => 403' `
                    ($rThuongXac.Ma -eq 403) ("HTTP {0} | {1}" -f $rThuongXac.Ma, $rThuongXac.Than)
            }
        }
    }
}
finally {
    try {
        # D-249: xoa user/role probe QUA API (host con chay) truoc khi dung host.
        if ($null -ne $token) {
            try {
                if ($null -ne $probeUserId) {
                    $rXoaNd = Goi-Api -PhuongThuc 'Delete' -DuongDan ('/api/identity/users/' + $probeUserId) -Token $token
                    Kq 'T14 DON DEP: xoa user probe' ($rXoaNd.Ma -eq 200 -or $rXoaNd.Ma -eq 204) ("HTTP {0}" -f $rXoaNd.Ma)
                }
                if ($null -ne $probeRoleId) {
                    Goi-Api -PhuongThuc 'Delete' -DuongDan ('/api/identity/roles/' + $probeRoleId) -Token $token | Out-Null
                }
            }
            catch {
                Write-Output ("   ⚠️ Don dep user/role probe loi: {0}" -f $_.Exception.Message)
            }
        }

        if ($null -ne $tienTrinh -and -not $tienTrinh.HasExited) {
            Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
        }

        Xoa-DuLieuProbe

        TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@ -KiemSql @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and "ShouldChangePasswordOnNextLogin";
'@ -SoDongMongDoi 1 | Out-Null

        $conLai = @(Invoke-Psql -Sql @'
select count(*)::text from "AddrCarrierMappings" where "CarrierAddressCode" like 'PROBE-%';
'@)
        $soConLai = if ($conLai.Count -gt 0) { [int]$conLai[0] } else { -1 }
        Kq 'T10 DON DEP: khong con anh xa probe' ($soConLai -eq 0) ("con lai = {0}" -f $soConLai)
    } catch {
        Write-Output ("   🚨 DON DEP LOI: {0}" -f $_.Exception.Message)
    }
}

$hong = @($ketQua | Where-Object { -not $_.Dat })
$dat = @($ketQua | Where-Object { $_.Dat })

Write-Output ''
Write-Output ("════ PROBE MA DIA CHI: {0}/{1} DAT ════" -f $dat.Count, $ketQua.Count)

if ($chuaDo.Count -gt 0) {
    Write-Output ("CHUA DO DUOC ({0}) — KHONG tinh la DAT, KHONG tinh la hong (no chu qa, han W18-5):" -f $chuaDo.Count)
    foreach ($c in $chuaDo) { Write-Output ("   [~] {0}: {1}" -f $c.Ten, $c.LyDo) }
}

if ($hong.Count -gt 0) {
    Write-Output 'CAC CA KHONG DAT:'
    foreach ($h in $hong) { Write-Output ("   [{0}] {1}" -f $h.Ten, $h.ChiTiet) }
    Write-Output ("Log host: {0}" -f (Join-Path $thuMucTam 'host.log'))
    exit 1
}

exit 0
