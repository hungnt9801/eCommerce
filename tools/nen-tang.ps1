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

        # 📌 MOT chuoi doi so — khuon cu (15 probe dang dung).
        [string]$ThamSo = '',

        # 📌 MANG doi so: `--user-data-dir=C:\Users\Ten Co Dau Cach\...` — 🔑 noi bang `' '` se lam hong
        #    duong dan co dau cach, nen phai de `Start-Process` tu quote TUNG phan tu.
        [string[]]$DanhSachThamSo,

        [string]$ThuMucLamViec = '',
        [string]$TepLogRa = '',
        [string]$TepLogLoi = ''
    )

    $tuyChon = @{ PassThru = $true }

    if ($ThuMucLamViec -ne '') { $tuyChon['WorkingDirectory'] = $ThuMucLamViec }
    if ($TepLogRa -ne '') { $tuyChon['RedirectStandardOutput'] = $TepLogRa }
    if ($TepLogLoi -ne '') { $tuyChon['RedirectStandardError'] = $TepLogLoi }

    if ($script:LaWindows) {
        $tuyChon['WindowStyle'] = 'Hidden'
    }

    # 📌 Mac dinh = MOT chuoi (giu nguyen hanh vi cu cho 15 probe). Chi truyen mang khi co `-DanhSachThamSo`.
    $doiSo = if ($PSBoundParameters.ContainsKey('DanhSachThamSo')) { $DanhSachThamSo } else { $ThamSo }

    return Start-Process $Tep -ArgumentList $doiSo @tuyChon
}

<#
    🔒 DUONG DAN MOT TEP TAM — `$env:TEMP` **KHONG TON TAI TREN LINUX**.

    🚨 Van de do duoc: probe cu viet `"$env:TEMP\wp06-host.log"`. Tren Linux `$env:TEMP` rong ⇒ duong dan
    thanh `\wp06-host.log`, tuc la **mot tep rac ngay trong repo** (ten co dau `\`), va `-LogFile` cua
    `wait-host.ps1` doc mot tep **khong ton tai** ⇒ thong diep loi noi ve *"log"*, khong noi ve *"he dieu hanh"*.

    📌 `[IO.Path]::GetTempPath()` la CUNG mot thu muc voi `$env:TEMP` tren Windows ⇒ hanh vi may dev KHONG doi.
#>
function Lay-DuongDanTam {
    param([Parameter(Mandatory)][string]$Ten)

    return [IO.Path]::Combine([IO.Path]::GetTempPath(), $Ten)
}

<#
    🔒 KHOI DONG WEB ADMIN (`pnpm --filter @ecommerce/admin-web start`) — AN, GHI LOG, tra ve `Process`.

    🚨 **DO DUOC TREN MAY DEV (`N12`, 04/11/2026):** `Start-Process pnpm` **NEM**
    *"%1 is not a valid Win32 application"* — vi `pnpm` la shim `.cmd` chu khong phai tep thi hanh.
    ⇒ Tren Windows phai boc qua `cmd.exe /c "…"` (do duoc: `exit 0` + in dung `10.22.0`).
    📌 Tren Linux `pnpm` la script co shebang ⇒ chay truc tiep duoc, va `cmd.exe` **khong ton tai**
    ⇒ giu khuon cu thi 6 probe chi chay duoc tren Windows.

    🔑 Vi sao o DAY chu khong trong tung probe: 6 probe dang sao chep dung khuon `cmd.exe` + co WindowStyle;
    sua tung cho thi lan sau them probe lai sao lai cai sai (dung ly do `nen-tang.ps1` sinh ra).
#>
function Start-NextAdmin {
    param(
        [Parameter(Mandatory)][string]$ThuMucLamViec,
        [Parameter(Mandatory)][string]$TepLogRa,
        [Parameter(Mandatory)][string]$TepLogLoi
    )

    $thamSo = @{
        ThuMucLamViec = $ThuMucLamViec
        TepLogRa      = $TepLogRa
        TepLogLoi     = $TepLogLoi
    }

    if ($script:LaWindows) {
        # 📌 `/c "<chuoi>"`: do duoc `exit 0` + in dung phien ban pnpm (xem ghi chu cua ham).
        return Start-TienTrinhAn -Tep 'cmd.exe' -ThamSo '/c "pnpm --filter @ecommerce/admin-web start"' @thamSo
    }

    return Start-TienTrinhAn -Tep 'pnpm' -ThamSo '--filter @ecommerce/admin-web start' @thamSo
}

# ══════════════════════════════════════════════════════════════════════════════════════════════════
# CO DOI MAT KHAU CUA `admin` — MO TAM ROI **BAT LAI** (`D-56`, `WP-13` ①)
# ══════════════════════════════════════════════════════════════════════════════════════════════════

<#
    🔒 `Dat-CoBuocDoiMatKhau` — dat co `AbpUsers.ShouldChangePasswordOnNextLogin` cua `admin`.

    📌 Day la **ham phu** cua `Invoke-KhongCoDoiMatKhau`; probe **KHONG** goi truc tiep no (xem ly do o
    ham duoi). No ton tai de phep `UPDATE` + phep **XAC NHAN** nam o **mot cho**.

    🚨 Vi sao phai XAC NHAN sau khi ghi: mot probe tuong *"da tat co"* ma that ra **khong** (sai tai khoan,
    sai container, `psql` loi) se chet o buoc xin token voi `invalid_grant` — va thong diep do noi ve
    *"dang nhap hong"*, khong noi ve *"co bao mat"* ⇒ nguoi doc di sai duong (bai hoc `N13` §1.2).
#>
function Dat-CoBuocDoiMatKhau {
    param(
        [Parameter(Mandatory)][bool]$Bat,
        [string]$HostPg = 'ecommerce-postgres',
        [string]$TenDb = 'ecommerce',
        [string]$TaiKhoan = 'ADMIN'
    )

    $mongDoi = if ($Bat) { 'true' } else { 'false' }
    $nhan = if ($Bat) { 'BAT' } else { 'TAT' }

    $sql = @"
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = $mongDoi where "NormalizedUserName" = '$TaiKhoan';
select count(*)::text from "AbpUsers" where "NormalizedUserName" = '$TaiKhoan' and "ShouldChangePasswordOnNextLogin" = $mongDoi;
"@

    # 🔒 Ghi ra **tep tam** roi doc — ⚠️ **KHONG** ong danh: ong danh che mat `exit code` cua `docker`
    #    (cung quy tac nhu `kiem-db.ps1` / bai hoc `sal-006` §23.2).
    $tam = [IO.Path]::GetTempFileName()
    $sql | docker exec -i $HostPg psql -U $TenDb -d $TenDb -q -t -A -f - *> $tam
    $ma = $LASTEXITCODE

    $dong = @(Get-Content $tam -ErrorAction SilentlyContinue | Where-Object { $_.Trim() -ne '' })
    Remove-Item $tam -Force -ErrorAction SilentlyContinue

    if ($ma -ne 0) {
        throw ("psql THAT BAI (exit {0}) khi {1} co ShouldChangePasswordOnNextLogin (container '{2}')." -f $ma, $nhan, $HostPg)
    }

    # 📌 `-q` bo dong tag `UPDATE 1` ⇒ dong cuoi la con so cua `select count(*)`.
    if (($dong | Select-Object -Last 1) -ne '1') {
        throw ("Khong {0} duoc co ShouldChangePasswordOnNextLogin cho '{1}' (doc duoc: '{2}')." -f $nhan, $TaiKhoan, ($dong -join '|'))
    }
}

<#
    🔒 `Invoke-KhongCoDoiMatKhau` — CHAY MOT KHOI LENH trong luc co doi mat khau **dang tat**, roi
    **finally BAT LAI**. Day la **cho DUY NHAT** mo/bat co nay.

    🚨 VAN DE DO DUOC (`WP-13` ①, `N14`, 04/11/2026): `DbMigrator` **bat** co nay cho `admin`
    (`D-49` ③a) ⇒ OpenIddict tu choi password grant voi
    `invalid_grant: ShouldChangePasswordOnNextLogin` (ID2024) — **KHONG** phai sai mat khau.
    Vi the moi probe xin token bang password grant phai tat co trong luc chay.

    📌 Va day la ly do ham nay ton tai: do lai **14** tep probe co `UPDATE` co nay —
      • **4** tep tat ma **KHONG** bao gio bat lai (`probe-pos`, `probe-don-hang`, `probe-bao-cao-wp08`,
        `probe-khach-hang-wp07`) ⇒ chay xong la **commit mot admin khong bi buoc doi mat khau**, va cong DB
        (nhom ② cua `kiem-db.ps1`) do o **lan sau** — ⚠️ nguoi ta se *"sua"* bang cach tat phep kiem;
      • `probe-kho-wp04` thi **khong** tat ⇒ **khong dang nhap duoc** (DoD cua `WP-13` ①).
    🔑 Sua **tung cho** thi lan sau them probe lai sao lai cai sai — dung ly do `nen-tang.ps1` sinh ra.

    🔒 `finally` cua PowerShell **CHAY ca khi** khoi lenh goi `exit` (do duoc: `try { exit 3 } finally { … }`
    van in ra `FINALLY-RAN`, exit code = 3) ⇒ khong con duong nao thoat ma co bi de **false**.

    ⚠️ `probe-kho-fe` **khong** dung ham nay: no di qua **BFF/NextAuth** (khong xin token bang password
    grant) ⇒ khong can cham vao co bao mat — do la duong **tot hon** khi da co Next chay.
#>
function Invoke-KhongCoDoiMatKhau {
    param(
        [Parameter(Mandatory)][scriptblock]$ThucThi,
        [string]$HostPg = 'ecommerce-postgres',
        [string]$TenDb = 'ecommerce',
        [string]$TaiKhoan = 'ADMIN'
    )

    Dat-CoBuocDoiMatKhau -Bat $false -HostPg $HostPg -TenDb $TenDb -TaiKhoan $TaiKhoan

    try {
        return (& $ThucThi)
    }
    finally {
        Dat-CoBuocDoiMatKhau -Bat $true -HostPg $HostPg -TenDb $TenDb -TaiKhoan $TaiKhoan
    }
}

<#
    🔒 `Start-KhongCoDoiMatKhau` + `BatLai-CoDoiMatKhau` — cap ham cho probe can co **TAT SUOT luot chay**
    (khong phai chi mot khoi ngan).

    📌 Vi sao van can cap nay: 4 probe `probe-pos`/`probe-don-hang`/`probe-bao-cao-wp08`/
    `probe-khach-hang-wp07` do **ca hai duong** — token Host (password grant) **va** dang nhap qua
    **BFF/NextAuth** (`/api/auth/callback/credentials` ⇒ BFF cung xin token bang password grant). ⇒ Mo co
    quanh mot khoi ngan la **khong du**, phai tat tu dau den cuoi luot chay.

    🚨 Nhung doi hoi nay **khong** duoc phep quay lai kieu cu (moi probe tu viet `UPDATE` va **quen**
    `finally`): ⇒ 🔒 `BatLai-CoDoiMatKhau` **PHAI** nam o dong dau tien cua `finally` cua probe, va no
    **XAC NHAN** da bat lai (nem neu khong). 📌 Mot co bi de `false` = commit mot admin **khong bi buoc
    doi mat khau** (`kiem-db.ps1` nhom ② se do — va nguoi ta se "sua" bang cach tat phep kiem).
#>
function Start-KhongCoDoiMatKhau {
    param(
        [string]$HostPg = 'ecommerce-postgres',
        [string]$TenDb = 'ecommerce',
        [string]$TaiKhoan = 'ADMIN'
    )

    Dat-CoBuocDoiMatKhau -Bat $false -HostPg $HostPg -TenDb $TenDb -TaiKhoan $TaiKhoan
}

<#
    🔒 `BatLai-CoDoiMatKhau` — BAT LAI co (goi trong `finally`; xem `Start-KhongCoDoiMatKhau`).

    📌 `-NeuDangTat` KHONG can: ham nay **luon** dat `true` — 🔑 co phai la `true` moi la bat bien cua repo
    (`DbMigrator` bat no, `kiem-db.ps1` nhom ② kiem no), nen *"bật lại"* luon co nghia la `true`.
#>
function BatLai-CoDoiMatKhau {
    param(
        [string]$HostPg = 'ecommerce-postgres',
        [string]$TenDb = 'ecommerce',
        [string]$TaiKhoan = 'ADMIN'
    )

    Dat-CoBuocDoiMatKhau -Bat $true -HostPg $HostPg -TenDb $TenDb -TaiKhoan $TaiKhoan
}
