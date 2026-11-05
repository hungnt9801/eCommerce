<#
.SYNOPSIS
    Probe OPS cho **`WP-10`: `docker compose` dựng Admin Web production + `/api/health` = 200/503 theo Host**.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 ĐO CÁI GÌ (và vì sao phải đo bằng HTTP thật, không bằng "đã cấu hình"):

      ①  `docker compose config --quiet`                    → tệp compose **hợp lệ** (sai cú pháp thì
                                                               `up` mới báo, và báo bằng thông điệp khó đọc)
      ②  `docker compose up -d admin-web`                    → container **Up** trên **cổng mặc định 3000**
                                                               (đúng `docker compose up` của DoD, KHÔNG đổi cổng)
      ③  Host **CHẾT**  → `GET /api/health`  = **503** + `checks.host = Unreachable`
      ④  Host **SỐNG**  → `GET /api/health`  = **200** + `checks.host = Healthy`
      ⑤  `docker inspect … .State.Health.Status`            → `unhealthy` (③) rồi `healthy` (④)
                                                               ⇒ healthcheck của compose **thật sự** nối vào
                                                               `/api/health`, không phải chỉ khai báo
      ⑥  `pnpm start` (bản `next build`, KHÔNG Docker)      → 200 khi Host sống, 503 khi Host chết
                                                               ⇒ hành vi health **không** phụ thuộc Docker

    🚨 **VÌ SAO PHẢI CÓ CẢ ③ VÀ ④:** một endpoint health luôn trả `200` thì **không phân biệt được** với
    một endpoint health đúng — cả hai đều "xanh" khi hệ thống khoẻ. Chỉ phép đo **hai chiều** mới chứng minh
    nó thật sự **hỏi** Host. 📌 Cùng lý do, probe **tự tay** bật/tắt Host (không dựa vào "chắc nó đang chạy").

    🔒 KHÔNG in token/cookie (endpoint này không cần đăng nhập — đúng thiết kế).
    📌 Probe **để lại**: image `ecommerce-admin-web:local`, và **tắt** container ở bước cuối (nhả cổng 3000
    cho `pwsh -File .\tools\e2e.ps1` chạy sau).

.PARAMETER Build
    Build lại image trước khi chạy (`docker compose build admin-web`). Không có cờ này và image **chưa có**
    thì compose tự build — nhưng khi đó probe đo một image **không rõ nguồn gốc**, nên nó **báo rõ**.

.PARAMETER BoQuaDocker
    Bỏ phần Docker (máy không có Docker). ⚠️ Khi đó DoD *"`docker compose up` dựng được Admin Web"* **CHƯA
    được đo** — probe in cảnh báo và exit `2`, **không** được coi là xanh.

.EXAMPLE
    pwsh -File .\tools\kiem-ops-wp10.ps1 -Build
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$BoQuaDocker
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$goc = $script:GocRepo
$congWeb = 3000
$congHost = 54275
$webLocal = "http://127.0.0.1:$congWeb"
$api = "http://127.0.0.1:$congHost"
$tenContainer = 'ecommerce-admin-web'

$script:SoLanGoi = 0
$script:Loi = @()
$hostProc = $null
$webProc = $null
$tempLog = Join-Path $goc 'tools\_tam-kiem'
New-Item -ItemType Directory -Force -Path $tempLog | Out-Null

function Khang-Dinh {
    param([bool]$DieuKien, [string]$ThongDiep)

    if (-not $DieuKien) { throw $ThongDiep }
}

function Lay-SucKhoe {
    param([string]$Url)

    $script:SoLanGoi++

    try {
        $r = Invoke-WebRequest -Uri $Url -SkipHttpErrorCheck -TimeoutSec 20
        return [pscustomobject]@{ Ma = [int]$r.StatusCode; Body = "$($r.Content)" }
    }
    catch {
        # 📌 Không ném: "không gọi được" LÀ một kết quả đo (và là kết quả đúng khi Host chết).
        return [pscustomobject]@{ Ma = 0; Body = $_.Exception.Message }
    }
}

function In-KetQua {
    param([string]$Nhan, $Kq, [string]$MongDoi)

    # 📌 Cắt body: một trang HTML dài hàng chục KB trong log làm người đọc **bỏ qua** đúng dòng quan trọng
    #    (mã HTTP + `checks.host`) — thứ mà probe này sinh ra để chỉ ra.
    $than = "$($Kq.Body)"
    if ($than.Length -gt 160) { $than = $than.Substring(0, 160) + '…' }

    $mau = if ($Kq.Ma -eq 200) { 'Green' } elseif ($Kq.Ma -eq 503) { 'Yellow' } else { 'Red' }
    Write-Host ("   {0}" -f $Nhan)
    Write-Host ("      HTTP {0} :: {1}" -f $Kq.Ma, $than) -ForegroundColor $mau
    Write-Host ("      mong doi: {0}" -f $MongDoi)
}

function Cho-CongMo {
    param([int]$Cong, [int]$Giay = 120)

    $hetHan = (Get-Date).AddSeconds($Giay)
    while ((Get-Date) -lt $hetHan) {
        if (Test-CongDangMo -Cong $Cong) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Cho-CongTrong {
    param([int]$Cong, [int]$Giay = 60)

    $hetHan = (Get-Date).AddSeconds($Giay)
    while ((Get-Date) -lt $hetHan) {
        if (-not (Test-CongDangMo -Cong $Cong)) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Dung-TienTrinhGiuCong {
    param([int]$Cong)

    foreach ($p in @(Lay-PidDangNghe -Cong $Cong)) {
        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
    }
}

function Lay-TrangThaiSucKhoeContainer {
    $raw = (& docker inspect $tenContainer --format '{{.State.Health.Status}}' 2>$null) -join ''
    return $raw.Trim()
}

function Cho-TrangThaiSucKhoeContainer {
    param([string]$MongDoi, [int]$Giay = 120)

    $batDau = Get-Date
    $hetHan = (Get-Date).AddSeconds($Giay)
    $thay = ''

    while ((Get-Date) -lt $hetHan) {
        $thay = Lay-TrangThaiSucKhoeContainer

        if ($thay -eq $MongDoi) {
            Write-Host ("      container = {0} (sau {1:N0}s)" -f $thay, ((Get-Date) - $batDau).TotalSeconds)
            return $true
        }

        Start-Sleep -Seconds 5
    }

    Write-Host ("      🚨 container = '{0}', mong doi '{1}'" -f $thay, $MongDoi) -ForegroundColor Red
    return $false
}

function Bat-Host {
    param([string]$HauTo)

    # 🚨 `0.0.0.0` (không phải `localhost`): container gọi vào qua `host.docker.internal`, mà địa chỉ đó
    #    KHÔNG đi vào loopback của máy ⇒ bind `localhost` thì container luôn thấy "Host chết".
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://0.0.0.0:$congHost"
    $env:AuthServer__Authority = "http://localhost:$congHost"

    $logRa = Join-Path $tempLog "ops-wp10-host-$HauTo.log"
    $proc = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' `
        -ThuMucLamViec (Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host') `
        -TepLogRa $logRa -TepLogLoi "$logRa.err"

    if (-not (Cho-CongMo -Cong $congHost -Giay 180)) {
        throw "Host khong mo cong $congHost sau 180s — xem log: $logRa"
    }

    # 📌 Chờ **HTTP** trả lời, không chỉ cổng mở: cổng mở mà app còn khởi tạo thì lời gọi đầu sẽ lỗi.
    for ($i = 0; $i -lt 60; $i++) {
        if ((Lay-SucKhoe "$api/health-status").Ma -eq 200) { return $proc }
        Start-Sleep -Milliseconds 500
    }

    throw "Host mo cong nhung /health-status khong tra 200 sau 30s — xem log: $logRa"
}

function Tat-Host {
    param($Proc)

    if ($null -ne $Proc) {
        Stop-Process -Id $Proc.Id -Force -ErrorAction SilentlyContinue
    }

    Dung-TienTrinhGiuCong -Cong $congHost
    Khang-Dinh (Cho-CongTrong -Cong $congHost -Giay 30) "Cong $congHost van mo sau khi tat Host."
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
Write-Host '══ kiem-ops-wp10 — Docker service admin-web + /api/health (WP-10) ══' -ForegroundColor Cyan

# ── 0. Điều kiện ──────────────────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '── 0. Dieu kien ──'

Khang-Dinh (Test-Path (Join-Path $goc 'apps\admin-web\Dockerfile')) 'Thieu apps/admin-web/Dockerfile.'
Khang-Dinh (Test-Path (Join-Path $goc '.dockerignore')) 'Thieu .dockerignore o goc repo (context se gui ca node_modules).'
Khang-Dinh (Test-Path (Join-Path $goc 'apps\admin-web\.env.example')) 'Thieu apps/admin-web/.env.example.'

# 📌 Đường dẫn `bin/<CauHinh>/<Khung>` phải hỏi `nen-tang.ps1` (dùng dấu phân cách của hệ điều hành đang chạy).
$hostBin = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
Khang-Dinh (Test-Path (Join-Path $hostBin 'eCommerce.HttpApi.Host.dll')) "Thieu DLL Host o $hostBin — chay `dotnet build eCommerce.slnx` truoc."

$buildId = Join-Path $goc 'apps\admin-web\.next\BUILD_ID'
if (-not (Test-Path $buildId)) {
    throw 'Thieu apps/admin-web/.next/BUILD_ID — chay `pnpm build` truoc (probe KHONG build ngam: se do sai binary).'
}

$coDocker = $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
Write-Host ("   docker: {0} · BUILD_ID: co · Dockerfile/.dockerignore/.env.example: co" -f $(if ($coDocker) { 'co' } else { 'KHONG' }))

if (-not $coDocker -or $BoQuaDocker) {
    Write-Host '   🚨 BO QUA phan Docker ⇒ DoD "docker compose up dung duoc Admin Web production" CHUA duoc do.' -ForegroundColor Red
}

# 📌 Trạng thái đầu vào KHÔNG được đoán: dọn sạch hai cổng rồi tự bật cái mình cần.
Write-Host ''
Write-Host '── 1. Don trang thai cu (cong 3000 / 54275) ──'

# 🚨 THỨ TỰ Ở ĐÂY LÀ MỘT BẪY ĐÃ ĐO: khi container `admin-web` đang chạy, cổng 3000 do **`com.docker.backend`**
#    giữ (proxy publish port của Docker Desktop), KHÔNG phải tiến trình Next. Giết nó = **Docker Desktop mất
#    mạng** (container sống mà không ra được ngoài), và thông điệp lỗi sau đó chỉ về phía `docker compose`.
#    ⇒ Dừng **container trước**, rồi mới dọn tiến trình còn giữ cổng.
if ($coDocker -and -not $BoQuaDocker) {
    $dangChay = ((& docker inspect $tenContainer --format '{{.State.Status}}' 2>$null) -join '').Trim()
    if ($dangChay -eq 'running') {
        Write-Host '   ⚠️  Container admin-web dang chay tu luot truoc ⇒ dung container TRUOC khi don cong.'
        & docker compose stop admin-web *> (Join-Path $tempLog 'ops-wp10-docker-pre-stop.log')
    }
}

Dung-TienTrinhGiuCong -Cong $congWeb
Dung-TienTrinhGiuCong -Cong $congHost
Khang-Dinh (Cho-CongTrong -Cong $congWeb -Giay 30) "Cong $congWeb van bi chiem."
Khang-Dinh (Cho-CongTrong -Cong $congHost -Giay 30) "Cong $congHost van bi chiem."
Write-Host '   ✅ Hai cong trong — Host dang TAT, khong co Next nao chay.'

try {
    # ══════════════════════════════════════════════════════════════════════════════════════════════
    # PHAN A — DOCKER
    # ══════════════════════════════════════════════════════════════════════════════════════════════
    if ($coDocker -and -not $BoQuaDocker) {

        Write-Host ''
        Write-Host '── A1. docker compose config (hop le cu phap) ──'
        & docker compose config --quiet 2>&1 | Out-Null
        Khang-Dinh ($LASTEXITCODE -eq 0) "docker compose config exit $LASTEXITCODE — tep compose khong hop le."
        Write-Host '   ✅ compose config: OK'

        Write-Host ''
        Write-Host '── A2. docker compose up -d admin-web (cong MAC DINH 3000) ──'

        if ($Build) {
            $logBuild = Join-Path $tempLog 'ops-wp10-docker-build.log'
            & docker compose build admin-web *> $logBuild
            Khang-Dinh ($LASTEXITCODE -eq 0) "docker compose build exit $LASTEXITCODE — xem log: $logBuild"
            Write-Host '   ✅ build: OK'
        }

        $logUp = Join-Path $tempLog 'ops-wp10-docker-up.log'
        & docker compose up -d admin-web *> $logUp
        Khang-Dinh ($LASTEXITCODE -eq 0) "docker compose up exit $LASTEXITCODE — xem log: $logUp"

        Khang-Dinh (Cho-CongMo -Cong $congWeb -Giay 180) "Container khong mo cong $congWeb sau 180s."

        # 📌 Phải có **HTTP trả lời** trước khi kết luận 503: 503 do app trả khác 503 do proxy của Docker
        #    trả khi app chưa lên. Chờ tới khi mã thuộc {200, 503}.
        $traLoi = $false
        for ($i = 0; $i -lt 120; $i++) {
            $kq = Lay-SucKhoe "$webLocal/api/health"
            if ($kq.Ma -eq 200 -or $kq.Ma -eq 503) { $traLoi = $true; break }
            Start-Sleep -Milliseconds 500
        }
        Khang-Dinh $traLoi 'Container mo cong nhung /api/health khong tra 200/503 sau 60s.'

        $trangThai = ((& docker inspect $tenContainer --format '{{.State.Status}}' 2>$null) -join '').Trim()
        Khang-Dinh ($trangThai -eq 'running') "Container khong o trang thai running (do duoc: '$trangThai')."
        Write-Host ("   ✅ container Up · /api/health tra loi (HTTP {0})" -f $kq.Ma)

        # ── A3: Host CHẾT ⇒ 503 ────────────────────────────────────────────────────────────────────
        Write-Host ''
        Write-Host '── A3. Host CHET ⇒ /api/health = 503 ──'

        $kq = Lay-SucKhoe "$webLocal/api/health"
        In-KetQua 'GET /api/health' $kq '503 + checks.host = Unreachable'
        Khang-Dinh ($kq.Ma -eq 503) "Mong doi 503 khi Host chet, do duoc $($kq.Ma)."
        Khang-Dinh ($kq.Body -match '"host":"Unreachable"') "Body thieu host=Unreachable: $($kq.Body)"
        Khang-Dinh ($kq.Body -notmatch 'Exception|stack|Npgsql|ConnectionStrings') 'Body LO CHI TIET ha tang (vi pham XC-005 BR-012).'

        # 📌 Không chỉ tin HTTP: chờ **healthcheck của compose** cũng đổi trạng thái ⇒ chứng minh healthcheck
        #    nối vào /api/health chứ không phải một lệnh luôn-thành-công.

        Khang-Dinh (Cho-TrangThaiSucKhoeContainer -MongDoi 'unhealthy' -Giay 120) 'Healthcheck compose khong chuyen sang unhealthy khi Host chet.'

        # ── A4: Host SỐNG ⇒ 200 ────────────────────────────────────────────────────────────────────
        Write-Host ''
        Write-Host '── A4. Host SONG ⇒ /api/health = 200 ──'
        $hostProc = Bat-Host -HauTo 'docker'

        $kq = Lay-SucKhoe "$webLocal/api/health"
        In-KetQua 'GET /api/health' $kq '200 + checks.host = Healthy'
        Khang-Dinh ($kq.Ma -eq 200) "Mong doi 200 khi Host song, do duoc $($kq.Ma)."
        Khang-Dinh ($kq.Body -match '"host":"Healthy"') "Body thieu host=Healthy: $($kq.Body)"


        Khang-Dinh (Cho-TrangThaiSucKhoeContainer -MongDoi 'healthy' -Giay 120) 'Healthcheck compose khong tro lai healthy khi Host song.'

        # ── A5: nhả cổng 3000 ─────────────────────────────────────────────────────────────────────
        Write-Host ''
        Write-Host '── A5. docker compose stop admin-web (nha cong 3000) ──'
        & docker compose stop admin-web *> (Join-Path $tempLog 'ops-wp10-docker-stop.log')
        Khang-Dinh (Cho-CongTrong -Cong $congWeb -Giay 60) "Cong $congWeb van bi container giu sau khi stop."
        Write-Host '   ✅ da dung container.'
    }

    # ══════════════════════════════════════════════════════════════════════════════════════════════
    # PHAN B — LOCAL (`next start`, bản production build, KHÔNG Docker)
    # ══════════════════════════════════════════════════════════════════════════════════════════════
    Write-Host ''
    Write-Host '── B1. next start (KHONG Docker) + Host dang song ⇒ 200 ──'

    $logWeb = Join-Path $tempLog 'ops-wp10-next.log'
    $webProc = Start-TienTrinhAn -Tep 'node' -ThamSo "node_modules\next\dist\bin\next start -p $congWeb" `
        -ThuMucLamViec (Join-Path $goc 'apps\admin-web') -TepLogRa $logWeb -TepLogLoi "$logWeb.err"

    Khang-Dinh (Cho-CongMo -Cong $congWeb -Giay 120) "next start khong mo cong $congWeb sau 120s — xem log: $logWeb"

    $traLoi = $false
    for ($i = 0; $i -lt 120; $i++) {
        $kq = Lay-SucKhoe "$webLocal/api/health"
        if ($kq.Ma -eq 200 -or $kq.Ma -eq 503) { $traLoi = $true; break }
        Start-Sleep -Milliseconds 500
    }
    Khang-Dinh $traLoi 'next start mo cong nhung /api/health khong tra 200/503.'

    $kq = Lay-SucKhoe "$webLocal/api/health"
    In-KetQua 'GET /api/health' $kq '200 (Host dang song)'
    Khang-Dinh ($kq.Ma -eq 200) "Mong doi 200 (local), do duoc $($kq.Ma)."

    Write-Host ''
    Write-Host '── B2. tat Host ⇒ 503 (local) ──'
    Tat-Host -Proc $hostProc
    $hostProc = $null

    $kq = Lay-SucKhoe "$webLocal/api/health"
    In-KetQua 'GET /api/health' $kq '503 + checks.host = Unreachable'
    Khang-Dinh ($kq.Ma -eq 503) "Mong doi 503 (local, Host chet), do duoc $($kq.Ma)."

    # 📌 Trang đăng nhập vẫn phải render được khi API chết — nếu không, người dùng thấy lỗi 500 và
    #    tưởng "sập cả hệ thống" trong khi chỉ có API chết.
    $login = Lay-SucKhoe "$webLocal/login"
    In-KetQua 'GET /login (Host chet)' $login '200 — BFF song doc lap voi API'
    Khang-Dinh ($login.Ma -eq 200) "/login tra $($login.Ma) khi Host chet — BFF phu thuoc API qua muc can thiet."

    # ── Tong ket ─────────────────────────────────────────────────────────────────────────────────
    Write-Host ''
    Write-Host ("══ KET QUA: {0} loi goi HTTP · 0 loi ══" -f $script:SoLanGoi) -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ''
    Write-Host ("🚨 THAT BAI: {0}" -f $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally {
    # 📌 Dọn theo thứ tự NGƯỢC: tắt Next của probe, tắt Host nếu còn, và **để container dừng** (đã dừng ở A5).
    if ($null -ne $webProc) {
        Stop-Process -Id $webProc.Id -Force -ErrorAction SilentlyContinue
    }

    Dung-TienTrinhGiuCong -Cong $congWeb

    if ($null -ne $hostProc) {
        Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
    }

    Dung-TienTrinhGiuCong -Cong $congHost
}
