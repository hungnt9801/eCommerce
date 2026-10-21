<#
.SYNOPSIS
    🚨 Kiem MOC THOI GIAN cua nhi phan SAU khi build — chong bay "build exit 0 ma binary khong doi".

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════
    🔑 VI SAO SCRIPT NAY TON TAI (no KHONG phai tien nghi — no la ket qua cua BON LAN TRA GIA):

    `dotnet build` co the tra **exit 0** + in **"Build succeeded."** ⚠️ NHUNG **khong ghi** nhi phan,
    vi mot tien trinh dang chay (Host/Web) **dang giu tep**. 📌 Triệu chung duy nhat la:
    **ma nguon DUNG, DLL CU** ⇒ test/kiem chung DO vi LY DO SAI, va nguoi sua **ket luan sai ve SAN PHAM**.

    Cac lan da tra gia (ghi trong tai lieu):
      • `web-001` §97.6  — build exit 0, moc DLL khong doi
      • `web-001` §98.6  — chay test tren binary CU ⇒ 7 ca do du ma da dung
      • `sal-006` §21.6  — job KHONG co neo nao; tuong dang ky hong, that ra Host giu DLL cu
      • `sal-006` §22.7  — khoi phuc ma xong test VAN do ⇒ suyt ket luan "khoi phuc khong sach"
    => 🔒 BON LAN. Mot quy tac trong DAU khong du ⇒ phai la **script**.

    ══════════════════════════════════════════════════════════════════════════════════════════════
    🚨 BAI HOC QUAN TRONG NHAT (tu §21.6): phai kiem **BAN COPY TRONG `bin` CUA TIEN TRINH SE CHAY**,
    KHONG phai ban trong thu muc du an. 📌 `src\eCommerce.Web\bin\...` KHAC
    `src\eCommerce.HttpApi.Host\bin\...\eCommerce.Web.dll` — va thuong la **BAN SAU** moi la ban duoc nap.

.PARAMETER Ten
    Loc theo TEN TEP (wildcard), vi du `eCommerce.Application.dll`. Mac dinh: moi `.dll` trong `bin`.

.PARAMETER ThuMuc
    Cac thu muc output can kiem. Mac dinh: `bin` cua Host + Web (hai tien trinh hay chay).
    ⚠️ Truyen **duong dan TUONG DOI tu goc repo**.
    🚨 **DAU PHAN CACH LA CUA HE DIEU HANH** (`XC-007` **P-04**): tren Linux `\` **khong phai** dau phan
    cach ⇒ gia tri mac dinh kieu `src\...\bin\Debug\net10.0` lam `Test-Path` = false ⇒ script bao *"KHONG
    thay tep nao"* va `exit 2` **ngay tren runner sach** (`S9`, 21/10/2026).

.PARAMETER GhiNho
    Ghi moc hien tai vao `.moc-build.json` (o goc repo) lam **moc so sanh** cho lan sau.
    📌 Khong truyen ⇒ chi IN moc ra (de mat nguoi doc).

.PARAMETER Kiem
    🚨 **CHE DO QUAN TRONG NHAT**: so sanh voi moc da ghi (`.moc-build.json`) va **THAT BAI (exit 1)**
    neu co tep **KHONG DOI**. ✅ Dung sau moi lan build khi nghi ngo.

.EXAMPLE
    # Buoc 1: ghi nho moc TRUOC khi build
    pwsh -File tools\kiem-moc-build.ps1 -GhiNho

    # Buoc 2: build ...
    dotnet build eCommerce.slnx -v m --no-incremental

    # Buoc 3: kiem moc SAU khi build — exit 1 neu binary KHONG doi
    pwsh -File tools\kiem-moc-build.ps1 -Kiem -Ten 'eCommerce.Application.dll'
#>
[CmdletBinding()]
param(
    [string]$Ten = '*.dll',
    [string[]]$ThuMuc = @(
        [IO.Path]::Combine('src', 'eCommerce.HttpApi.Host', 'bin', 'Debug', 'net10.0'),
        [IO.Path]::Combine('src', 'eCommerce.Web', 'bin', 'Debug', 'net10.0')
    ),
    [switch]$GhiNho,
    [switch]$Kiem
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$tepMoc = Join-Path $goc '.moc-build.json'

$thay = @()
$thieu = @()

foreach ($tm in $ThuMuc) {
    $duongDan = Join-Path $goc $tm

    if (-not (Test-Path $duongDan)) {
        $thieu += $tm
        continue
    }

    foreach ($t in (Get-ChildItem -Path $duongDan -Filter $Ten -File -ErrorAction SilentlyContinue)) {
        # Khoa = duong dan TUONG DOI (on dinh giua cac may) — ⚠️ KHONG dung duong dan tuyet doi.
        $khoa = (Join-Path $tm $t.Name).Replace('\', '/')
        # 🚨 **MOC LA SO TICK UTC — KHONG phai chuoi ISO.**
        # 🔒 **LY DO (do duoc ngay 17/10/2026):** `ConvertFrom-Json` **TU DONG** chuyen chuoi ISO-8601
        # thanh `[datetime]`; ⚠️ va `[datetime] -eq [string]` trong PowerShell tra **False**,
        # trong khi `[datetime] -eq [datetime]` tra **True** ⇒ 🔑 phep so sanh **phu thuoc KIEU**
        # chu khong phu thuoc GIA TRI ⇒ ket qua **khong dang tin**.
        # 📌 So tick la **so nguyen** ⇒ khong co chuyen doi ngam nao chen vao giua ⇒ so sanh **chinh xac**.
        $thay += [pscustomobject]@{
            Khoa = $khoa
            Moc  = $t.LastWriteTimeUtc.Ticks
            Ten  = $t.Name
        }
    }
}

# 🛡️ LUOI AN TOAN CHO CHINH SCRIPT: khong thay tep nao ⇒ phep kiem VO NGHIA ⇒ phai NOI RO.
if ($thay.Count -eq 0) {
    # ⚠️ DUNG `Write-Error`: no ep **exit 1**, lam lan voi ma 1 (ma 1 = "binary KHONG doi").
    # 🔒 Hai nguyen nhan KHAC NHAU phai co HAI ma KHAC NHAU, neu khong thi nguoi doc log khong phan biet duoc
    # "ban build khong vao" voi "phep kiem sai" — va se di sua SAI CHO.
    Write-Output ("🚨 KHONG thay tep nao khop '{0}' trong: {1}" -f $Ten, ($ThuMuc -join ' | '))

    # 🚨 **HAI NGUYEN NHAN NAY KHAC NHAU — VA MOT TRONG HAI LA "MOI TRUONG", KHONG PHAI SAN PHAM.**
    # 📌 Do duoc o `S9` (21/10/2026, `XC-007` **F2**): tren **runner SACH** (chua tung build) thi
    # `bin/` **chua ton tai** ⇒ `-GhiNho` roi vao nhanh nay ⇒ `exit 2` ⇒ `kiem-tat-ca.ps1` **nem** o buoc ②
    # ⇒ 🔑 **lan chay DAU TIEN tren CI luon DO**, va do la **moi truong**, khong phai ma nguon.
    # ✅ Cach chua **da chon**: CI **BUILD LAM NONG** truoc khi goi cong (`XC-007` §6.1 / §14.2).
    # 🚨 Cach chua **bi loai**: cho `-GhiNho` "bo qua khi chua co bin" ⇒ khi do `-Kiem` thay **MOI** tep la
    # *"MOI (chua co trong moc)"* ⇒ `$khongDoi` rong ⇒ script in **"DA VAO"** du **khong co bang chung nao**
    # ⇒ 📌 mot phep kiem **XANH vi LY DO SAI** — dung loai hong ma script nay sinh ra de chong.
    if ($thieu.Count -eq $ThuMuc.Count) {
        Write-Output "⇒ MOI TRUONG: CHUA CO BAN BUILD NAO (khong thu muc output nao ton tai)."
        Write-Output "   🔒 CI phai BUILD LAM NONG truoc khi goi cong — ⚠️ KHONG phai loi ma nguon."
    }
    else {
        Write-Output "⇒ PHEP KIEM VO NGHIA. Kiem lai -ThuMuc/-Ten."
    }

    exit 2
}

if ($thieu.Count -gt 0) {
    Write-Warning ("Thu muc KHONG ton tai (bo qua): {0}" -f ($thieu -join ' | '))
}

if ($Kiem) {
    if (-not (Test-Path $tepMoc)) {
        # ⚠️ DUNG `Write-Error` (ep exit 1, lan voi ma 1 = "binary KHONG doi").
        Write-Output ("🚨 Chua co moc de so sanh ({0})." -f $tepMoc)
        Write-Output "⇒ Chay ``-GhiNho`` TRUOC khi build, roi build, roi ``-Kiem``."
        exit 2
    }

    # 🚨 **DOC MOC — VA KIEM NO CO PHAI MANG THAT KHONG.**
    #
    # 🔒 **BAY DA DO DUOC NGAY KHI VIET SCRIPT NAY (17/10/2026):** ban dau toi viet
    #     `$mocCu = Get-Content ... | ConvertFrom-Json`  roi  `foreach ($m in $mocCu) { ... }`
    # ⚠️ Neu tep moc chua **MOT OBJECT** thay vi **MANG** (vi du `ConvertTo-Json` tren mot phan tu don),
    # thi `foreach` **KHONG** duyet phan tu — no duyet **THUOC TINH** cua object ⇒ `$bangCu` **RONG**
    # ⇒ 🔑 **MOI tep bi coi la "MOI"** ⇒ va buoc kiem **XANH** du binary **KHONG DOI**.
    #
    # 🚨 **Va day dung la loai hong ma script nay sinh ra de chong:** mot phep kiem **XANH vi LY DO SAI**
    # ⇒ nguoi sua **tin** rang ban build da vao ⇒ **ket luan sai ve SAN PHAM**.
    # 📌 `@(...)` ep ve **MANG**; va neu mang do **RONG**/thieu truong `Khoa` thi **DUNG** (exit 2) —
    # 🔑 **khong** duoc coi la "moi tep deu moi".
    #
    # 🚨 **BAY THU HAI — VA NO TINH VI HON (cung do duoc ngay 17/10/2026):** `ConvertFrom-Json` **TU DONG**
    # chuyen chuoi ISO-8601 (`2026-10-17T02:06:04.123+07:00`) thanh **`[datetime]`**. ⚠️ Khi do
    # `$bangCu[$khoa] -eq $t.Moc` la phep so sanh **`[datetime]` voi `[string]`** ⇒ 🔒 **LUON FALSE**
    # ⇒ **MOI** tep roi vao nhanh *"MOI (chua co trong moc)"* ⇒ va buoc kiem **XANH** du binary **KHONG DOI**.
    # 📌 Nghia la phep kiem **tu vo hieu hoan toan im lang** — dung loai hong ma no sinh ra de chong.
    # ✅ **CACH CHUA:** ep **CA HAI VE** ve `[string]`. 🔑 So sanh **chuoi voi chuoi** thi khong co chuyen
    # doi ngam nao chen vao giua.
    # 📌 Bai hoc chung: `ConvertFrom-Json` **khong** tra ve "du lieu tho" — no **suy kieu**. 🔒 Voi du lieu
    # dung de **SO SANH**, phai tu ep kieu thay vi tin vao suy kieu.
    $mocCu = @(Get-Content $tepMoc -Raw | ConvertFrom-Json)

    $bangCu = @{}
    foreach ($m in $mocCu) {
        if ($null -ne $m.Khoa -and $null -ne $m.Moc) {
            $bangCu[[string]$m.Khoa] = [long]$m.Moc
        }
    }

    if ($bangCu.Count -eq 0) {
        Write-Output ("🚨 Tep moc {0} KHONG doc duoc dong nao (rong / sai dinh dang)." -f $tepMoc)
        Write-Output "⇒ PHEP KIEM VO NGHIA ⇒ DUNG (⚠️ KHONG duoc coi la 'moi tep deu MOI')."
        exit 2
    }

    # 🚨🔑 **TÊN BIẾN PHẢI NÓI ĐÚNG ĐIỀU KIỆN ĐANG KIỂM — VÌ TÔI ĐÃ VIẾT NGƯỢC Ở ĐÂY (đo được 17/10/2026).**
    #
    # 📌 **Chuyện đã xảy ra:** bản đầu đặt tên `$khongDoi` cho nhánh **SO KHỚP** (`-eq`) rồi **BÁO ĐỘNG**
    # trên nhánh đó ⇒ 🔒 cổng báo *"BAN BUILD KHONG VAO"* **ngay cả khi mọi tệp ĐỀU ĐỔI**.
    # ⚠️ Và tệ hơn: nó **báo động đúng lúc phải im lặng** ⇒ **vô dụng đúng lúc cần nhất** —
    # 📌 một cổng **luôn đỏ** thì người ta **học cách bỏ qua nó**.
    #
    # ✅ **Cách chữa KHÔNG chỉ là đảo `if`:** đặt tên biến theo **ĐIỀU KIỆN ĐANG KIỂM**
    # (*"tệp có ĐỔI không?"*) thay vì theo **kết quả** (*"không đổi"*) — 🔑 như vậy `if ($coDoi)`
    # đọc lên là **hiển nhiên đúng**, không còn chỗ cho việc đảo nhầm.
    #
    # 📌 **BÀI HỌC CHUNG:** điều kiện **PHỦ ĐỊNH** (`$khongDoi`, `$khongLoi`, `$chuaXong`) là nơi
    # **dễ đảo nhầm nhất** — vì cả *"đúng"* lẫn *"sai"* đều đọc ra **có lý**. 🔒 Dùng tên **KHẲNG ĐỊNH**.
    $coDoi = @()
    $khongDoi = @()
    $moi = @()

    foreach ($t in $thay) {
        if (-not $bangCu.ContainsKey($t.Khoa)) {
            # Tệp **chưa có** trong mốc (ví dụ vừa thêm dự án) ⇒ 📌 **KHÔNG** phải lỗi, nhưng cũng
            # **KHÔNG** chứng minh được bản build đã vào ⇒ phải **nói ra**.
            $moi += $t.Khoa
            continue
        }

        if ($bangCu[$t.Khoa] -eq [long]$t.Moc) {
            # 🔑 **KHỚP MỐC = TỆP KHÔNG ĐỔI = BẢN BUILD KHÔNG VÀO** ⇒ đây mới là ca **BÁO ĐỘNG**.
            $khongDoi += [pscustomobject]@{ Khoa = $t.Khoa; Moc = $t.Moc }
        }
        else {
            $coDoi += $t.Khoa
        }
    }

    Write-Output ("=== KIEM MOC BUILD ({0} tep) ===" -f $thay.Count)

    if ($moi.Count -gt 0) {
        Write-Output ("  MOI (chua co trong moc): {0}" -f ($moi -join ' | '))
    }

    if ($khongDoi.Count -gt 0) {
        Write-Output ""
        Write-Output ("🚨 {0} TEP **KHONG DOI** sau build ⇒ BAN BUILD KHONG VAO:" -f $khongDoi.Count)
        foreach ($k in $khongDoi) { Write-Output ("    {0}  (moc {1})" -f $k.Khoa, $k.Moc) }
        Write-Output ""
        Write-Output "⇒ NGUYEN NHAN THUONG GAP: mot tien trinh (Host/Web) dang CHAY va DANG GIU tep."
        Write-Output "⇒ CACH SUA: dung moi tien trinh eCommerce, doi cong 5000/5100 TRONG, roi build lai"
        Write-Output "   voi  --no-incremental /nodeReuse:false  VA kiểm moc lai."
        Write-Output "🔒 DUNG ket luan gi ve SAN PHAM khi moc chua doi — ma nguon co the DUNG nhung binary CU."
        exit 1
    }

    Write-Output "✅ MOI tep deu DOI moc ⇒ ban build DA VAO."
    exit 0
}

if ($GhiNho) {
    $thay | ConvertTo-Json -Depth 3 | Set-Content -Path $tepMoc -Encoding utf8
    Write-Output ("=== DA GHI MOC: {0} tep ⇒ {1} ===" -f $thay.Count, $tepMoc)
}

foreach ($t in $thay | Sort-Object Khoa) {
    Write-Output ("  {0}  {1}" -f $t.Moc, $t.Khoa)
}

Write-Output ("Tong: {0} tep." -f $thay.Count)
