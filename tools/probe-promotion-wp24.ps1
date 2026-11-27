<#
.SYNOPSIS
    Probe HTTP THẬT cho `PRO-002`/`W24-4` — bề mặt `/api/v1/promotion/**` (7 route, `D-285`) — `D-291` ②(4) / P2 #5.

.DESCRIPTION
    Chạy Host thật (54275) + PostgreSQL thật rồi đo bằng HTTP, in `HTTP STATUS` TRƯỚC body:
      T01-T03  ẩn danh ⇒ 401 (View / Manage / Approve)
      T04-T06  user thiếu quyền ⇒ 403 (View / Manage / Approve)
      T07      user có `Promotions.View` ⇒ 200 trên `GET /rule-catalog`
      T08      user có View nhưng thiếu Manage ⇒ 403 trên `POST /` (tách quyền)
      T09      admin ⇒ 200 `GET /rule-catalog`
      T10-T12  admin `POST /` ⇒ 200/201 · `POST /{id}/approve` ⇒ 200 · approve lần hai ⇒ 409 + `0006`
      T13      `POST /{id}/save-tree` 65 node ⇒ 400 + `eCommerce:Promotion:0001`
    🔒 Không in token. Mã `PROBE-PROMO-<random>`; dọn user/role qua API + xoá promotion probe qua SQL.
#>
[CmdletBinding()]
param([switch]$BoQuaHost)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$gocRepo = Split-Path -Parent $PSScriptRoot
$base = 'http://localhost:54275'
$ngauNhien = (Get-Random -Minimum 100000 -Maximum 999999)
$vaiTro = "PROBE-PROMO-$ngauNhien"
$email = "probe-promotion-$ngauNhien@local"
$matKhau = 'Probe1q2w3E*'
$maKm = "PROBE-PROMO-$ngauNhien"
$vaiTroId = $null
$nguoiDungId = $null
$token = $null

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

$tienTrinh = $null
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
    $logHost = Lay-DuongDanTam 'probe-promotion-host.log'
    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $bin `
        -TepLogRa $logHost -TepLogLoi (Lay-DuongDanTam 'probe-promotion-host.err')
    & pwsh -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $base -LogFile $logHost -ProcessId $tienTrinh.Id
    if ($LASTEXITCODE -ne 0) { Write-Host 'Host KHONG len duoc.' -ForegroundColor Red; exit 2 }
}

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

    $idGia = '00000000-0000-0000-0000-0000000000aa'
    $tr = '/api/v1/promotion'

    # ── Ẩn danh ⇒ 401 ──────────────────────────────────────────────────────────────────────────
    InKq 'T01 an danh GET /rule-catalog => 401' (Goi -Method GET -Path "$tr/rule-catalog")
    InKq 'T02 an danh POST / => 401' (Goi -Method POST -Path $tr -Body @{ code = $maKm; name = 'x'; startsAtUtc = '2026-01-01T00:00:00Z'; discountKind = 'FixedAmount'; discountValue = 1 })
    InKq "T03 an danh POST /$idGia/approve => 401" (Goi -Method POST -Path "$tr/$idGia/approve")

    # ── User thiếu quyền ───────────────────────────────────────────────────────────────────────
    # ⚠️ Host KHÔNG expose `/api/identity/**` (404, như probe `ADDR-004`) ⇒ không tạo được user
    # thường qua HTTP ⇒ **CHƯA ĐO ĐƯỢC 403** (D-291 ②(4): ghi trung thực, KHÔNG hạ kỳ vọng).
    $coUser = $false
    $r = Goi -Method POST -Path '/api/identity/roles' -Headers $hAdmin -Body @{ name = $vaiTro; isPublic = $false; isDefault = $false }
    Write-Host ("Tao role: HTTP {0}" -f $r.Status)
    if ($r.Status -in @(200, 201)) {
        $vaiTroId = ($r.Body | ConvertFrom-Json).id

        $r = Goi -Method POST -Path '/api/identity/users' -Headers $hAdmin -Body @{
            userName = $email; email = $email; password = $matKhau; name = 'Probe'; surname = 'Promotion'
            isActive = $true; lockoutEnabled = $true; emailConfirmed = $true; phoneNumberConfirmed = $true; roleNames = @($vaiTro)
        }
        Write-Host ("Tao user: HTTP {0}" -f $r.Status)
        if ($r.Status -in @(200, 201)) {
            $coUser = $true
            $nguoiDungId = ($r.Body | ConvertFrom-Json).id
        }
    }

    if (-not $coUser) {
        Write-Host 'CHUA_DO: T04-T08 (403 khi thieu quyen) — Host khong expose /api/identity/** (404), khong tao duoc user thuong.' -ForegroundColor Yellow
    }
    else {
        $jwtThuong = Invoke-RestMethod -Method Post -Uri "$base/connect/token" -ContentType 'application/x-www-form-urlencoded' -Body @{
            grant_type = 'password'; client_id = 'eCommerce_App'; client_secret = '1q2w3e*'
            scope = 'eCommerce offline_access'; username = $email; password = $matKhau
        }
        $hThuong = @{ Authorization = "Bearer $($jwtThuong.access_token)" }

        InKq 'T04 user KHONG quyen GET /rule-catalog => 403' (Goi -Method GET -Path "$tr/rule-catalog" -Headers $hThuong)
        InKq 'T05 user KHONG quyen POST / => 403' (Goi -Method POST -Path $tr -Headers $hThuong -Body @{ code = "X-$maKm"; name = 'x'; startsAtUtc = '2026-01-01T00:00:00Z'; discountKind = 'FixedAmount'; discountValue = 1 })
        InKq "T06 user KHONG quyen POST /$idGia/approve => 403" (Goi -Method POST -Path "$tr/$idGia/approve" -Headers $hThuong)

        # ── Cấp DUY NHẤT `Promotions.View` cho role ⇒ GET 200 nhưng POST vẫn 403 (tách quyền) ────────
        $cap = Goi -Method PUT -Path ("/api/permission-management/permissions?providerName=R&providerKey=" + $vaiTro) -Headers $hAdmin -Body @{
            permissions = @(
                @{ name = 'eCommerce.Promotions'; isGranted = $true },
                @{ name = 'eCommerce.Promotions.View'; isGranted = $true }
            )
        }
        InKq 'T06b cap Promotions.View cho role' $cap

        InKq 'T07 user co View GET /rule-catalog => 200' (Goi -Method GET -Path "$tr/rule-catalog" -Headers $hThuong)
        InKq 'T08 user co View POST / => 403 (thieu Manage)' (Goi -Method POST -Path $tr -Headers $hThuong -Body @{ code = "X-$maKm"; name = 'x'; startsAtUtc = '2026-01-01T00:00:00Z'; discountKind = 'FixedAmount'; discountValue = 1 })
    }

    # ── Admin 200 + nghiệp vụ ──────────────────────────────────────────────────────────────────
    InKq 'T09 admin GET /rule-catalog => 200' (Goi -Method GET -Path "$tr/rule-catalog" -Headers $hAdmin)

    $tao = Goi -Method POST -Path $tr -Headers $hAdmin -Body @{
        code = $maKm; name = "KM probe $ngauNhien"; kind = 'Discount'; isCumulative = $false; priority = 0
        startsAtUtc = '2026-01-01T00:00:00Z'; discountKind = 'FixedAmount'; discountValue = 1000; currency = 'VND'
    }
    InKq 'T10 admin POST / => 200/201' $tao
    $kmId = $null
    if ($tao.Status -in @(200, 201)) { $kmId = ($tao.Body | ConvertFrom-Json).id }

    if ($kmId) {
        # T13 TRƯỚC approve (promotion còn `Draft`, không đụng trạng thái) ⇒ ca `0001`⇒400 không bị nhầm với `409/0006`.
        $nodes = @()
        for ($i = 0; $i -lt 65; $i++) {
            $nodes += @{ isGroup = $false; ruleSystemName = 'MinOrderAmount'; configJson = '{"minAmount":0}'; sortOrder = $i; depth = 1 }
        }
        InKq "T10a save-tree 65 node => 400 + 0001" (Goi -Method POST -Path "$tr/$kmId/save-tree" -Headers $hAdmin -Body @{ nodes = $nodes })

        # Cây hợp lệ tối thiểu (đúng 1 gốc lá) — approve đòi cây không rỗng.
        InKq "T10b save-tree 1 la hop le => 200" (Goi -Method POST -Path "$tr/$kmId/save-tree" -Headers $hAdmin -Body @{
            nodes = @(@{ isGroup = $false; ruleSystemName = 'MinOrderAmount'; sortOrder = 0; depth = 1 })
        })
        InKq "T11 admin POST /{id}/approve => 200" (Goi -Method POST -Path "$tr/$kmId/approve" -Headers $hAdmin)
        InKq "T12 approve LAN HAI => 409 + 0006" (Goi -Method POST -Path "$tr/$kmId/approve" -Headers $hAdmin)
    }
}
finally {
    # ── DỌN DẸP ────────────────────────────────────────────────────────────────────────────────
    try {
        if ($null -ne $token) {
            if ($nguoiDungId) { Goi -Method DELETE -Path ('/api/identity/users/' + $nguoiDungId) -Headers $hAdmin | Out-Null }
            if ($vaiTroId) { Goi -Method DELETE -Path ('/api/identity/roles/' + $vaiTroId) -Headers $hAdmin | Out-Null }
        }
        $sql = "delete from ""ProPromotions"" where ""Code"" like 'PROBE-PROMO-%';"
        docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -c $sql | Out-Null
        $con = docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -tAc "select count(*) from ""ProPromotions"" where ""Code"" like 'PROBE-PROMO-%';"
        Write-Host ("DON DEP: ProPromotions con lai = {0}" -f ($con | Select-Object -First 1))
    }
    catch { Write-Host ("DON DEP loi: {0}" -f $_.Exception.Message) }

    if ($null -ne $tienTrinh -and -not $tienTrinh.HasExited) {
        Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
    }
}
