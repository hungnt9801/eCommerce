# nen-tang.ps1 — THU VIEN "MOT CHO" CHO MOI THU KHAC NHAU GIUA WINDOWS VA LINUX.
#
# 🔑 VI SAO TEP NAY TON TAI (`XC-007` P-01…P-06, `S9` 21/10/2026):
#
#   Cong kiem cua repo nay duoc viet tren **Windows** va chi chung minh duoc **tren Windows**. `XC-007`
#   chon runner **`ubuntu-latest`** (vi integration test ghim `postgres:17-alpine` = Linux ⇒ runner Windows
#   KHONG chay duoc image Linux), nen **moi cho Windows-only la mot cho CI se do** — va do **vi moi truong**,
#   khong phai vi san pham.
#
#   🚨 Bai hoc cua chinh du an: *"mot phep do bat ngo thuong la phep do SAI"*. Nhung mot cong **do vi moi
#   truong** con te hon: nguoi ta **hoc cach bo qua no** (`XC-007` §12 E-01/E-08).
#
#   📌 VI SAO GOP VE MOT TEP: ban dau `XC-007` §7.4 liet ke **6 diem** (P-01…P-06). Do lai ngay 21/10/2026
#   thi co **8 diem** — vi `S2`/`S3`/`S5`/`S6` them **4 probe**, ma moi probe **sao lai** dung 3 khuon
#   Windows-only (cong `Get-NetTCPConnection`, duong dan `bin\Debug\net10.0`, `-WindowStyle Hidden`).
#   ⇒ 🔒 Sua **tung cho** thi lan sau them probe lai **sao lai** cai sai; sua **mot cho** thi khong.
#   ⚠️ Nhung sao chep **van con** (moi probe phai `. (Join-Path $PSScriptRoot 'nen-tang.ps1')`), nen
#      `test-cong-kiem.ps1` co **ca kiem quet** : moi script cua cong **phai** nap thu vien nay.
#
# 🔒 HAI QUY TAC CUA TEP NAY:
#
#   **① KHONG co `param(...)` o cap tep.** Tep nay duoc **dot-source** (`. nen-tang.ps1`) tu script khac;
#     mot khoi `param` o day se **ghi de** tham so cua script goi no. 📌 Cung ly do `ham-tiem.ps1` lam vay.
#
#   **② KHONG pha hanh vi tren Windows.** Moi ham giu **nguyen** duong cu khi `$LaWindows` = true ⇒
#     phep do tren may dev (va 254 test cua cong) khong doi y nghia. 🚨 Neu khong, "va kha chuyen" se thanh
#     "doi cong cu" — va moi con so baseline cu mat gia tri so sanh.

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# HE DIEU HANH
# ══════════════════════════════════════════════════════════════════════════════════════════════════

# 📌 `$env:OS` = 'Windows_NT' tren Windows, **khong ton tai** tren Linux ⇒ don gian va tat dinh hon
#    `$IsWindows` (bien tu dong chi co tu PowerShell 6+).
$script:LaWindows = ($env:OS -eq 'Windows_NT')

# 📌 GOC REPO — tinh **mot lan** luc dot-source (⚠️ KHONG dung `$PSScriptRoot` ben trong ham: khi
#    dot-source thi `$PSScriptRoot` la cua tep dang nap, va no doi theo tep goi ⇒ de nham).
$script:GocRepo = Split-Path -Parent $PSScriptRoot

<#
    🔒 TEN TEP THUC THI CUA CURL.

    📌 Tren Windows phai goi `curl.exe`: `curl` tran bi PowerShell 5.1 dat lam **alias** cua
    `Invoke-WebRequest` ⇒ cau lenh *"chay duoc"* nhung **khong** co `-w '%{http_code}'`, va ta do nham thu.
    Tren Linux khong co `curl.exe` ⇒ phai la `curl`.
#>
$script:Curl = if ($script:LaWindows) { 'curl.exe' } else { 'curl' }

function Lay-Curl {
    return $script:Curl
}

<#
    🔒 DUONG DAN THU MUC OUTPUT (`bin/<CauHinh>/<Khung>`) CUA MOT DU AN — dung dau phan cach cua HE DIEU HANH.

    🚨 VAN DE DO DUOC (`XC-007` P-04): truoc day moi script viet cung
        `Join-Path $goc 'src\eCommerce.Web\bin\Debug\net10.0'`
    Tren Linux `\` **khong phai** dau phan cach — no la mot **ky tu binh thuong trong ten tep** ⇒
    `Test-Path` = false ⇒ `kiem-moc-build.ps1` bao *"KHONG thay tep nao"* ⇒ **exit 2** ⇒ cong do **ngay
    buoc ②**, va thong diep noi ve *"loc tep"* chu khong noi ve *"he dieu hanh"*.
#>
function Lay-DuongDanBin {
    param(
        [Parameter(Mandatory)][string]$TenDuAn,
        [string]$CauHinh = 'Debug',
        [string]$Khung = 'net10.0',
        [string]$GocRepo = $script:GocRepo
    )

    # 📌 `[IO.Path]::Combine` dung **dau phan cach cua he dieu hanh dang chay**.
    return [IO.Path]::Combine($GocRepo, 'src', $TenDuAn, 'bin', $CauHinh, $Khung)
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# CONG MANG (P-01, P-02)
# ══════════════════════════════════════════════════════════════════════════════════════════════════

<#
    🔒 CONG CO DANG CO NGUOI NGHE KHONG — **thuan .NET**, khong goi lenh he dieu hanh nao.

    📌 Vi sao **khong** dung `Get-NetTCPConnection` (duong cu): no **chi co tren Windows**. Tren Linux
    lenh nay **khong ton tai** ⇒ `-ErrorAction SilentlyContinue` lam no **im lang tra rong** ⇒
    🔑 cong **luon bao "TRONG"** ⇒ bay *"build ghi hut"* quay lai: build `exit 0` ma binary **khong doi**
    ⇒ test do vi **LY DO SAI** (bai hoc da tra gia **BON LAN** — `kiem-tat-ca.ps1` `.DESCRIPTION`).

    ✅ `TcpClient.ConnectAsync` + cho co han: cong **dang nghe** thi connect **thanh cong**; cong trong thi
    he dieu hanh tra *connection refused* **rat nhanh**. ⇒ cung mot phep do, hai he dieu hanh.
#>
function Test-CongDangMo {
    param(
        [Parameter(Mandatory)][int]$Cong,
        [int]$ChoMillis = 500
    )

    $khach = [System.Net.Sockets.TcpClient]::new()

    try {
        $tacVu = $khach.ConnectAsync('127.0.0.1', $Cong)

        # ⚠️ `Wait` (khong phai `.Result`) — `.Result` **nem** `AggregateException` khi bi tu choi.
        if (-not $tacVu.Wait($ChoMillis)) { return $false }

        return $khach.Connected
    }
    catch {
        # 📌 *"Khong ket noi duoc"* o day nghia la **cong TRONG** — dung la thu ta can biet.
        return $false
    }
    finally {
        try { $khach.Dispose() } catch { }
    }
}

<#
    🔒 INODE CUA SOCKET DANG NGHE MOT CONG — doc tu `/proc/net/tcp` (va `tcp6`).

    🚨 **DO DUOC 21/10/2026 tren Linux that (container Ubuntu 24.04):** ban dau `Lay-PidDangNghe` chi biet
    `ss`/`lsof`, va **ca hai deu khong co** trong image toi gian ⇒ ham tra **RONG** ma **khong loi nao**.
    📌 Do dung la kieu hong ma chinh tep nay sinh ra de chong: *"kiem khong chay ma bao DAT"* — o day la
    *"tat khong duoc tien trinh giu cong"* trong khi cong **van bi giu**.

    ✅ `/proc` **luon co** tren moi Linux (kernel cung cấp), khong can cai gi.
    📌 Dinh dang `/proc/net/tcp`: `sl local_address rem_address st ... inode` voi `st = 0A` la `LISTEN`.
#>
function Lay-InodeDangNghe {
    param([Parameter(Mandatory)][int]$Cong)

    $hex = ('{0:X4}' -f $Cong)
    $inode = @()

    foreach ($tep in @('/proc/net/tcp', '/proc/net/tcp6')) {
        if (-not (Test-Path $tep)) { continue }

        foreach ($dong in (Get-Content $tep -ErrorAction SilentlyContinue | Select-Object -Skip 1)) {
            $cot = ($dong.Trim() -split '\s+')

            if ($cot.Count -lt 10) { continue }
            if ($cot[3] -ne '0A') { continue }                            # 0A = LISTEN
            if (-not $cot[1].EndsWith(':' + $hex)) { continue }           # <diaChi>:<cong hex>

            $inode += $cot[9]
        }
    }

    return @($inode | Sort-Object -Unique)
}

<#
    🔒 PID CUA TIEN TRINH DANG **NGHE** MOT CONG (de tat tien trinh cu).

    🚨 VI SAO CAN: mot Web/Host cu con song se (a) giu tep DLL ⇒ build ghi hut, va (b) lam `Start-Process`
    that bai **im lang** ⇒ ta do mot **trang CU** (`web-001` §97.6 #3).

    🔒 **BA LOP, theo thu tu "it phu thuoc nhat truoc":**
      ① **Windows**: `Get-NetTCPConnection` (co san trong Windows).
      ② **Linux**: doc `/proc/net/tcp` lay **inode**, roi quet `/proc/*/fd` tim `socket:[inode]` — 📌 khong
        phu thuoc `ss`/`lsof` (xem ghi chu cua `Lay-InodeDangNghe`: container toi gian **khong co** ca hai).
      ③ Chi khi ② khong cho ket qua: `ss`/`lsof` (neu co) — lop du phong cho he khong co `/proc`.

    ⚠️ Neu **ca ba** deu khong tra ve PID thi ham tra **rong** — va `kiem-tat-ca.ps1` **van** kiem
    `Test-CongDangMo` sau khi doi 60 s: *"khong tat duoc"* phai **on ao**, khong duoc thanh *"cong trong"*.
#>
function Lay-PidDangNghe {
    param([Parameter(Mandatory)][int]$Cong)

    # ① Windows
    if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
        return @(Get-NetTCPConnection -LocalPort $Cong -State Listen -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty OwningProcess -Unique)
    }

    # ② Linux: /proc (khong can cong cu nao)
    $inode = @(Lay-InodeDangNghe -Cong $Cong)

    if ($inode.Count -gt 0) {
        $canTim = @($inode | ForEach-Object { 'socket:[' + $_ + ']' })
        $pids = @()

        foreach ($thuMuc in (Get-ChildItem '/proc' -Directory -ErrorAction SilentlyContinue)) {
            if ($thuMuc.Name -notmatch '^\d+$') { continue }

            $thuMucFd = Join-Path $thuMuc.FullName 'fd'

            foreach ($fd in (Get-ChildItem $thuMucFd -ErrorAction SilentlyContinue)) {
                # 📌 `LinkTarget` = dich cua lien ket tuong trung (`socket:[12345]`), khong phai noi dung tep.
                $dich = $null
                try { $dich = $fd.LinkTarget } catch { continue }

                if (-not [string]::IsNullOrEmpty($dich) -and $canTim -contains $dich) {
                    $pids += [int]$thuMuc.Name
                    break
                }
            }
        }

        if ($pids.Count -gt 0) { return @($pids | Sort-Object -Unique) }
    }

    # ③ Du phong: ss / lsof (neu he dieu hanh co)
    $pids = @()

    if (Get-Command ss -ErrorAction SilentlyContinue) {
        foreach ($dong in @(& ss -ltnpH "sport = :$Cong" 2>$null)) {
            foreach ($m in [regex]::Matches([string]$dong, 'pid=(\d+)')) {
                $pids += [int]$m.Groups[1].Value
            }
        }
    }
    elseif (Get-Command lsof -ErrorAction SilentlyContinue) {
        foreach ($dong in @(& lsof -ti "tcp:$Cong" -sTCP:LISTEN 2>$null)) {
            if ("$dong" -match '^\s*(\d+)') { $pids += [int]$Matches[1] }
        }
    }

    return @($pids | Sort-Object -Unique)
}

<#
    🔒 PID CUA CAC TIEN TRINH `dotnet` DANG CHAY **MOT DU AN eCommerce** (Host/Web/probe).

    🚨 Van de (`XC-007` **P-01**): ban dau dung
        `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"`
    — **WMI chi co tren Windows**. Tren Linux lenh nay **khong ton tai** ⇒ `-ErrorAction SilentlyContinue`
    nuot loi ⇒ danh sach **RONG** ⇒ *"Tim thay 0 tien trinh"* ⇒ 🔑 buoc ① bao **XANH** trong khi mot Host
    cu **van dang giu DLL**. 📌 Do la dang nguy hiem nhat: **kiem khong chay ma bao DAT**.

    ✅ Linux: doc `ps -eo pid=,args=` (co san tren moi runner Ubuntu).
#>
function Lay-TienTrinhECommerce {
    if (Get-Command Get-CimInstance -ErrorAction SilentlyContinue) {
        return @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
                Where-Object { $_.CommandLine -like '*eCommerce*' } |
                ForEach-Object { [int]$_.ProcessId })
    }

    $pids = @()

    foreach ($dong in @(& ps -eo 'pid=,args=' 2>$null)) {
        $chuoi = [string]$dong
        if ($chuoi -match 'dotnet' -and $chuoi -match 'eCommerce' -and $chuoi -match '^\s*(\d+)') {
            $pids += [int]$Matches[1]
        }
    }

    return @($pids)
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# KHOI DONG TIEN TRINH NEN (P-05)
# ══════════════════════════════════════════════════════════════════════════════════════════════════

<#
    🔒 `Start-Process` CO `-WindowStyle Hidden` **CHI TREN WINDOWS**.

    🚨 Van de (`XC-007` **P-05**): `-WindowStyle` la tham so **chi-Windows**; tren Linux PowerShell 7
    **khong bind** duoc no ⇒ `Start-Process` **nem** ⇒ buoc ⑧ (va moi probe) **no ngay khi khoi dong Web**,
    va thong diep loi noi ve *"tham so"*, khong noi ve *"he dieu hanh"*.

    📌 `RedirectStandardOutput`/`RedirectStandardError`/`WorkingDirectory`/`PassThru` **co tren ca hai**.
#>
function Start-TienTrinhAn {
    param(
        [Parameter(Mandatory)][string]$Tep,
        [Parameter(Mandatory)][string]$ThamSo,
        [Parameter(Mandatory)][string]$ThuMucLamViec,
        [Parameter(Mandatory)][string]$TepLogRa,
        [Parameter(Mandatory)][string]$TepLogLoi
    )

    $tuyChon = @{
        PassThru               = $true
        WorkingDirectory       = $ThuMucLamViec
        RedirectStandardOutput = $TepLogRa
        RedirectStandardError  = $TepLogLoi
    }

    if ($script:LaWindows) {
        $tuyChon['WindowStyle'] = 'Hidden'
    }

    return Start-Process $Tep -ArgumentList $ThamSo @tuyChon
}
