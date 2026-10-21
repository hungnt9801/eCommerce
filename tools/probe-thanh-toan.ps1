<#
.SYNOPSIS
    🚨 **PROBE THANH TOÁN** (`PAY-001` phần B — `S5`) — kiểm **HTTP THẬT** trên Host: :5000.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **VÌ SAO CÓ PROBE NÀY (ba câu hỏi mà unit + integration test KHÔNG THỂ trả lời):**

      ① 🚨 **IPN GỬI TRÙNG THÌ CHỈ ĐƯỢC GHI MỘT LẦN** — qua **endpoint thật**. `TC-08` của `PAY-001` đã được
         chứng minh ở **hai tầng** (`MarkSucceeded` trả `false` — unit; unique index `23505` — integration),
         nhưng **chưa** qua **đường HTTP**. 📌 Và đó là đường **duy nhất** cổng thật đi qua: nếu controller
         trả `500`/`404`, mọi tầng dưới đúng cũng **vô nghĩa**.

      ② 🚨 **SAI CHỮ KÝ ⇒ 400 VÀ KHÔNG ĐỔI GÌ** — đo **cả hai chiều**: chữ ký đúng ⇒ `200` + `RspCode 00`
         **và** DB đổi; chữ ký sai ⇒ `400` + `RspCode 97` **và** DB **KHÔNG** đổi (kể cả bộ đếm webhook).
         📌 Đây là hàng rào chống **IPN giả** (`PAY-001` §7.7.4 #1) — bỏ nó là **giao hàng miễn phí**.

      ③ **THAM SỐ RỖNG + HOÀN VƯỢT SỐ ĐÃ THU** — `F-PAY-1` (mức 🔴 Cao: *từ chối IPN THẬT*) và `0002`
         (hoàn vượt ⇒ **ném lỗi**, không được im lặng ghi sổ). 🔑 Cả hai đều là **quyết định về tiền** nên
         phải đo ở đường thật, không chỉ ở hàm thuần.

    🔒 **SÁU QUY TẮC ĐƯỢC THI HÀNH (mỗi cái là một bài học đã trả giá):**
      1. **In HTTP STATUS trước body** (`README-probe` **B1**): `404`/`405` từng bị đọc thành *"rỗng"*.
      2. **NEO ASCII** trong mọi câu khẳng định — tiếng Việt trong console bị hỏng dấu (**I4**).
      3. **KHẲNG ĐỊNH TIỀN ĐỀ** trước khi đo: giao dịch probe phải **có thật** trong DB và ở `Initiated`
         trước khi gửi IPN (**A8**).
      4. **DỌN DẸP trong `finally`** + **khẳng định đã sạch** — nếu không, `kiem-db.ps1` đỏ vì lý do vô hình.
      5. **KHÔNG viết cứng route**: route đo được là `/api/webhooks/vnpay` và `/api/app/payment/tao-giao-dich`
         (ABP sinh từ TÊN METHOD — bài học `S2-1`); 🔒 coi `404` là **ĐỎ**, không "đoán".
      6. 🚨 **KHOÁ KÝ KHÔNG BAO GIỜ ĐƯỢC COMMIT**: probe **tự đặt** `PaymentHub__VnPay__HashSecret` bằng
         **biến môi trường** cho tiến trình Host. 📌 Nhờ vậy ta chạy được **HMAC-SHA512 THẬT** mà **không**
         commit một bí mật nào — và bản sửa `F-PAY-1` được kiểm bằng chính thuật toán của sản phẩm.

.PARAMETER TimeoutGiay
    Chờ Host lên (giây).

.EXAMPLE
    pwsh -File tools\probe-thanh-toan.ps1
#>
[CmdletBinding()]
param(
    [int]$TimeoutGiay = 120
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $PSScriptRoot '_tam-thanhtoan'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

# 🔑 Nạp thư viện tiêm: chỉ có HÀM, không có `param` cấp tệp ⇒ nạp an toàn.
. (Join-Path $PSScriptRoot 'ham-tiem.ps1')

# 🔑 Thư viện "một chỗ" cho các khác biệt Windows/Linux (`XC-007` P-02/P-04/P-05 — `S9` 21/10/2026).
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$host_ = 'http://localhost:5000'
$tienTrinh = $null
$ketQua = [System.Collections.Generic.List[object]]::new()

# 📌 Tiền tố `K9S5` cho MỌI dữ liệu probe: `kiem-db.ps1` nhóm ⑤ quét `like 'k9%'` trên tên/email/mã.
$tienTo = 'K9S5'
$maThamChieuDon1 = 'K9S5-DON-01'
$maThamChieuDon2 = 'K9S5-DON-02'
$tmnCode = 'K9S5TMN'

# 🔒 Khoá ký **chỉ dùng cho probe**: đặt bằng BIẾN MÔI TRƯỜNG cho tiến trình Host (không ghi vào tệp nào).
#    📌 Nếu khoá này bị commit vào `appsettings.json` thì (a) nó thành "bí mật" giả, (b) môi trường thật có thể
#    vô tình dùng nó. Dài ≥ 32 ký tự để không ai nhầm với giá trị mẫu.
$biMatProbe = 'k9s5-probe-bi-mat-hmac-sha512-khong-dung-o-production'

function Kq {
    param([string]$Ten, [bool]$Dat, [string]$ChiTiet = '')

    $ketQua.Add([pscustomobject]@{ Ten = $Ten; Dat = $Dat; ChiTiet = $ChiTiet })
    Write-Output ("   [{0}] {1} | {2}" -f $Ten, $(if ($Dat) { 'DAT' } else { 'KHONG DAT' }), $ChiTiet)
}

function Dem-Db {
    param([string]$Sql)

    $dong = @(Invoke-Psql -Sql $Sql)
    if ($dong.Count -eq 0) { return -1 }

    return [int](($dong[0]).Trim())
}

function Tinh-Hmac {
    param([string]$BiMat, [string]$Chuoi)

    $hmac = [System.Security.Cryptography.HMACSHA512]::new([System.Text.Encoding]::UTF8.GetBytes($BiMat))
    try {
        $bam = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Chuoi))
        return ([System.Convert]::ToHexString($bam)).ToLowerInvariant()
    }
    finally {
        $hmac.Dispose()
    }
}

<#
    🔑 **Dựng query IPN ĐÚNG NHƯ VNPAY KÝ** — và tách rời hai tập tham số để ĐO ĐƯỢC `F-PAY-1`:
      • `-HienThi`  : tham số XUẤT HIỆN trong query (gồm cả cái RỖNG);
      • `-Ky`       : tham số ĐƯỢC KÝ (bỏ cái rỗng — đúng luật VNPay).
    📌 Sắp xếp **Ordinal** theo tên khoá ngay tại đây (không dựa vào thứ tự khai báo) — nếu dựa vào thứ tự thì
    phép đo chỉ kiểm *"chuỗi tôi viết khớp với chính nó"*, còn luật sắp xếp thì **không** được kiểm.
#>
function Dung-IpnQuery {
    param(
        [System.Collections.Specialized.OrderedDictionary]$HienThi,
        [System.Collections.Specialized.OrderedDictionary]$Ky,
        [string]$BiMat
    )

    $sapXep = { param($d) @($d.Keys) | Sort-Object -CaseSensitive }

    $chuoiKy = (@(& $sapXep $Ky) | ForEach-Object {
            "{0}={1}" -f $_, [System.Net.WebUtility]::UrlEncode([string]$Ky[$_])
        }) -join '&'

    $chuoiQuery = (@(& $sapXep $HienThi) | ForEach-Object {
            "{0}={1}" -f $_, [System.Net.WebUtility]::UrlEncode([string]$HienThi[$_])
        }) -join '&'

    $chuKy = Tinh-Hmac -BiMat $BiMat -Chuoi $chuoiKy

    return [pscustomobject]@{
        Query    = $chuoiQuery
        ChuoiKy  = $chuoiKy
        ChuKy    = $chuKy
        DayDu    = ($chuoiQuery + '&vnp_SecureHash=' + $chuKy)
    }
}

function Goi-Ipn {
    param([string]$Query)

    # ⚠️ **BẪY ĐÃ VẤP NGAY LƯỢT CHẠY ĐẦU CỦA PROBE NÀY:** `-StatusCodeVariable` **chỉ** có trên
    # `Invoke-RestMethod` (từ PowerShell 7.4), **KHÔNG** có trên `Invoke-WebRequest` ⇒
    # *"A parameter cannot be found that matches parameter name 'StatusCodeVariable'"* — và 📌 thông điệp đó
    # **không** nói rằng IPN hỏng; nó nói **công cụ đo** sai. ✅ Dùng `$tra.StatusCode` trực tiếp.
    # 🔒 `-SkipHttpErrorCheck` là **bắt buộc**: thiếu nó thì `400` (chính là ca ta cần ĐO) **ném** và ta không
    # đọc được body — đúng loại "phép đo tự vô hiệu đúng ở chỗ quan trọng nhất".
    $tra = Invoke-WebRequest -Method Get -Uri ($host_ + '/api/webhooks/vnpay?' + $Query) `
        -SkipHttpErrorCheck -MaximumRedirection 0

    return [pscustomobject]@{ Ma = [int]$tra.StatusCode; Than = $tra.Content }
}

function Goi-Api {
    param(
        [string]$PhuongThuc,
        [string]$DuongDan,
        [string]$Token,
        [object]$Body = $null
    )

    $thamSo = @{
        Method             = $PhuongThuc
        Uri                = ($host_ + $DuongDan)
        Headers            = @{ Authorization = "Bearer $Token" }
        SkipHttpErrorCheck = $true
        StatusCodeVariable = 'maHttp'
        ContentType        = 'application/json'
    }

    if ($null -ne $Body) {
        $thamSo.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }

    $tra = Invoke-RestMethod @thamSo
    $ma = [int]$maHttp

    return [pscustomobject]@{ Ma = $ma; DuLieu = $tra; Than = ($tra | ConvertTo-Json -Depth 8 -Compress) }
}

<#
    🔒 **DỌN DẸP THEO ĐÚNG THỨ TỰ KHOÁ** — dòng hoàn tiền là CON của giao dịch (theo nghĩa dữ liệu), xoá trước.
    📌 Xoá theo **tiền tố** ở mọi bảng đã chạm (không chỉ theo `Id`): dòng do **API** tạo có `Id` ngẫu nhiên, và
    `RefundRequestId` (`RF001`…) là **chung** cho mọi giao dịch ⇒ nếu xoá theo nó thì **xoá cả dữ liệu thật**.

    🚨 **BẪY ĐÃ VẤP NGAY LƯỢT CHẠY ĐẦU — `%K9S5%` chứ KHÔNG phải `K9S5%`:** `MerchantReference` do
    `SinhMaThamChieu` sinh ra có dạng `PAY<ngày>-<số đơn>-<lần thử>` ⇒ mã probe nằm **Ở GIỮA**
    (`PAY20261018-K9S5-DON-01-1`). 📌 Với mẫu `K9S5%` thì phép dọn **không khớp dòng nào**, rác ở lại, và ca
    `T03` (mã ổn định) **ĐỎ vì lần thử = 3** — đúng bài học `README-probe` §25.5: *"dọn không thành"* và
    *"sản phẩm sai"* **trông giống hệt nhau**.
#>
function Xoa-DuLieuProbe {
    Invoke-Psql -Sql @'
delete from "PayRefunds" where "PaymentTransactionId" in (select "Id" from "PayTransactions" where "MerchantReference" like '%K9S5%');
'@ | Out-Null

    Invoke-Psql -Sql @'
delete from "PayTransactions" where "MerchantReference" like '%K9S5%';
'@ | Out-Null
}

Write-Output ''
Write-Output '════ PROBE THANH TOAN (PAY-001 phan B · S5) — HTTP THAT :5000 ════'

try {
    # ── ⓪ DỌN DẤU VẾT CŨ TRƯỚC KHI ĐO ─────────────────────────────────────────────────────────
    Xoa-DuLieuProbe

    $conCu = Dem-Db @'
select count(*)::text from "PayTransactions" where "MerchantReference" like '%K9S5%';
'@

    Kq 'T00 tien de: khong con du lieu probe cu' ($conCu -eq 0) ("giao dich con = {0}" -f $conCu)

    if ($conCu -ne 0) {
        throw 'Con du lieu probe cu ⇒ moi phep do sau do co the XANH VI LY DO SAI.'
    }

    # ── ① KHỞI ĐỘNG HOST **KÈM KHOÁ KÝ** (qua biến môi trường — KHÔNG commit) ──────────────────
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    # 🔒 **BIẾN MÔI TRƯỜNG THẮNG `appsettings.json`** trong ASP.NET Core (mẫu `__` = `:`).
    # 📌 Nhờ vậy ta kiểm **HMAC-SHA512 thật** trên đường HTTP mà **không** commit bí mật nào.
    $env:PaymentHub__VnPay__HashSecret = $biMatProbe
    $env:PaymentHub__VnPay__TmnCode = $tmnCode

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

    # 🚨 **PHẢI TẮT CỜ BẢO MẬT TRONG LÚC CHẠY PROBE** (y như `kiem-e2e`/`probe-khach-hang`): admin có
    # `ShouldChangePasswordOnNextLogin = true` ⇒ OpenIddict từ chối `password` grant.
    # 🔒 Và **BẮT BUỘC BẬT LẠI** trong `finally` + khẳng định (nhóm ② của `kiem-db.ps1`).
    TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = 'ADMIN';
'@ -KiemSql @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and not "ShouldChangePasswordOnNextLogin";
'@ -SoDongMongDoi 1 | Out-Null

    # ── ② TOKEN ──────────────────────────────────────────────────────────────────────────────
    $thanToken = @{
        grant_type    = 'password'
        client_id     = 'eCommerce_App'
        client_secret = '1q2w3e*'
        username      = 'admin@abp.io'
        password      = '1q2w3E*'
        scope         = 'eCommerce offline_access'
    }

    $jwt = $null

    try {
        $jwt = Invoke-RestMethod -Method Post -Uri ($host_ + '/connect/token') -Body $thanToken `
            -ContentType 'application/x-www-form-urlencoded'
    }
    catch {
        Kq 'T01 lay token admin' $false ("{0}" -f $_.Exception.Message)
        throw
    }

    $token = $jwt.access_token
    Kq 'T01 lay token admin (password grant)' (-not [string]::IsNullOrWhiteSpace($token)) ("do dai = {0}" -f ($token | Measure-Object -Character).Characters)

    # ── ③ TẠO GIAO DỊCH QUA API (đường vào DUY NHẤT của `PayTransactions`) ────────────────────
    $tao1 = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/payment/tao-giao-dich' -Token $token -Body @{
        soDon     = $maThamChieuDon1
        soTien    = 250000
        tienTe    = 'VND'
        returnUrl = 'https://localhost:5100/DatHang/KetQua'
        ipnUrl    = 'https://localhost:5000/api/webhooks/vnpay'
    }

    Kq 'T02 POST tao-giao-dich => 200 (route ton tai, dung verb)' ($tao1.Ma -eq 200) ("HTTP {0}" -f $tao1.Ma)

    if ($tao1.Ma -ne 200) {
        throw ("Khong tao duoc giao dich (HTTP {0}) ⇒ cac ca sau VO NGHIA: {1}" -f $tao1.Ma, $tao1.Than)
    }

    $idGd1 = [string]$tao1.DuLieu.paymentTransactionId
    $maGd1 = [string]$tao1.DuLieu.merchantReference
    $urlCheckout = [string]$tao1.DuLieu.checkoutUrl

    Kq 'T03 giao dich co MaThamChieu ON DINH (PAY<ngay>-<so don>-<lan thu>)' `
        ($maGd1 -match '^PAY\d{8}-.*-1$') ("ma = {0}" -f $maGd1)

    Kq 'T04 co CheckoutUrl (VNPay dung URL, KHONG goi mang)' (-not [string]::IsNullOrWhiteSpace($urlCheckout)) `
        ("co URL = {0}" -f (-not [string]::IsNullOrWhiteSpace($urlCheckout)))

    # 🔑 **TIỀN ĐỀ** (quy tắc ③): giao dịch phải ở `Initiated` (0) **và** có thật trong DB trước khi gửi IPN.
    $trangThaiTruoc = Dem-Db (@'
select "Status"::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    Kq 'T05 tien de: giao dich trong DB o trang thai Initiated (0)' ($trangThaiTruoc -eq 0) ("Status = {0}" -f $trangThaiTruoc)

    if ($trangThaiTruoc -ne 0) {
        throw 'Tien de SAI ⇒ phep do IPN khong con y nghia.'
    }

    # ── ④ IPN HỢP LỆ (chữ ký THẬT) ───────────────────────────────────────────────────────────
    $maCong = '14000999'
    $soTienX100 = '25000000'

    $dayDu = [ordered]@{
        'vnp_Amount'            = $soTienX100
        'vnp_CurrCode'          = 'VND'
        'vnp_ResponseCode'      = '00'
        'vnp_TmnCode'           = $tmnCode
        'vnp_TransactionNo'     = $maCong
        'vnp_TransactionStatus' = '00'
        'vnp_TxnRef'            = $maGd1
    }

    $ipn1 = Dung-IpnQuery -HienThi $dayDu -Ky $dayDu -BiMat $biMatProbe
    $kq1 = Goi-Ipn -Query $ipn1.DayDu

    Kq 'T06 IPN chu ky DUNG => 200' ($kq1.Ma -eq 200) ("HTTP {0} · body = {1}" -f $kq1.Ma, $kq1.Than)
    Kq 'T07 body tra RspCode 00 (dung hinh dang VNPay)' ($kq1.Than -match '"RspCode":"00"') ("body = {0}" -f $kq1.Than)

    $sau1 = Dem-Db (@'
select "Status"::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    Kq 'T08 DB DA DOI: Status = Succeeded (2) — khong tin HTTP 200' ($sau1 -eq 2) ("Status = {0}" -f $sau1)

    $idem = Dem-Db (@'
select count(*)::text from "PayTransactions" where "MerchantReference" = '{0}' and "IdempotencyKey" = '{1}';
'@ -f $maGd1, $maCong)

    Kq 'T09 IdempotencyKey = vnp_TransactionNo (chong trung o tang DB)' ($idem -eq 1) ("dong khop = {0}" -f $idem)

    # ── ⑤ IPN GỬI TRÙNG THÊM 2 LẦN (`TC-08` QUA ĐƯỜNG HTTP) ─────────────────────────────────
    $kq2 = Goi-Ipn -Query $ipn1.DayDu
    $kq3 = Goi-Ipn -Query $ipn1.DayDu

    Kq 'T10 IPN trung van tra 200/00 (cong khong duoc retry vo ich)' `
        ($kq2.Ma -eq 200 -and $kq3.Ma -eq 200 -and $kq2.Than -match '"RspCode":"00"') `
        ("lan2 HTTP {0} · lan3 HTTP {1}" -f $kq2.Ma, $kq3.Ma)

    $soDong = Dem-Db (@'
select count(*)::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    $boDem = Dem-Db (@'
select "WebhookReceivedCount"::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    $statusSau3 = Dem-Db (@'
select "Status"::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    Kq 'T11 🚨 IPN trung 3 lan => chi MOT dong, Status KHONG doi' `
        ($soDong -eq 1 -and $statusSau3 -eq 2) ("so dong = {0} · Status = {1}" -f $soDong, $statusSau3)

    Kq 'T12 🚨 bo dem = 3 (bang chung duy nhat phan biet "gui trung" voi "IPN mat")' `
        ($boDem -eq 3) ("WebhookReceivedCount = {0}" -f $boDem)

    # ── ⑥ SAI CHỮ KÝ ⇒ 400 VÀ **KHÔNG ĐỔI GÌ** (đo cả hai chiều) ─────────────────────────────
    $bodySai = $ipn1.DayDu -replace 'vnp_Amount=25000000', 'vnp_Amount=99000000'
    $kqSai = Goi-Ipn -Query $bodySai

    Kq 'T13 🚨 sai chu ky => HTTP 400 (cong ra S5: sai chu ky ⇒ 400)' ($kqSai.Ma -eq 400) ("HTTP {0}" -f $kqSai.Ma)
    Kq 'T14 body sai chu ky => RspCode 97 (dung hinh dang VNPay)' ($kqSai.Than -match '"RspCode":"97"') ("body = {0}" -f $kqSai.Than)

    $boDemSai = Dem-Db (@'
select "WebhookReceivedCount"::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    Kq 'T15 🚨 IPN sai chu ky KHONG cham DB (bo dem van = 3)' ($boDemSai -eq 3) ("WebhookReceivedCount = {0}" -f $boDemSai)

    # ── ⑦ `F-PAY-1` — THAM SỐ RỖNG QUA ĐƯỜNG HTTP (CẢ HAI CHIỀU) ─────────────────────────────
    $coRong = [ordered]@{
        'vnp_Amount'            = $soTienX100
        'vnp_BankCode'          = ''          # ⇐ tham số RỖNG: VNPay gửi cho một số phương thức
        'vnp_CurrCode'          = 'VND'
        'vnp_ResponseCode'      = '00'
        'vnp_TmnCode'           = $tmnCode
        'vnp_TransactionNo'     = $maCong
        'vnp_TransactionStatus' = '00'
        'vnp_TxnRef'            = $maGd1
    }

    $boRong = [ordered]@{}
    foreach ($k in $coRong.Keys) {
        if ([string]$coRong[$k] -ne '') { $boRong[$k] = $coRong[$k] }
    }

    # ① Cổng ký **BỎ** tham số rỗng (đúng luật VNPay) ⇒ phải được CHẤP NHẬN.
    $ipnRong = Dung-IpnQuery -HienThi $coRong -Ky $boRong -BiMat $biMatProbe
    $kqRong = Goi-Ipn -Query $ipnRong.DayDu

    Kq 'T16 🚨 F-PAY-1: IPN co tham so RONG, ky theo luat VNPay => 200/00' `
        ($kqRong.Ma -eq 200 -and $kqRong.Than -match '"RspCode":"00"') `
        ("HTTP {0} · body = {1}" -f $kqRong.Ma, $kqRong.Than)

    $boDemRong = Dem-Db (@'
select "WebhookReceivedCount"::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    Kq 'T17 IPN hop le do => bo dem tang (4) ⇒ no THAT SU di qua duong ghi' `
        ($boDemRong -eq 4) ("WebhookReceivedCount = {0}" -f $boDemRong)

    # ② Chiều NGƯỢC: ký **GIỮ** tham số rỗng (cách bản CŨ của ta) ⇒ cổng KHÔNG ký như vậy ⇒ phải bị từ chối.
    $ipnRongCu = Dung-IpnQuery -HienThi $coRong -Ky $coRong -BiMat $biMatProbe
    $kqRongCu = Goi-Ipn -Query $ipnRongCu.DayDu

    Kq 'T18 chieu NGUOC: chu ky tinh theo cach CU (giu tham so rong) => 400' `
        ($kqRongCu.Ma -eq 400) ("HTTP {0}" -f $kqRongCu.Ma)

    # ── ⑧ LỆCH SỐ TIỀN ⇒ 400 + mã `0008`, VÀ KHÔNG GHI ───────────────────────────────────────
    $tao2 = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/payment/tao-giao-dich' -Token $token -Body @{
        soDon     = $maThamChieuDon2
        soTien    = 120000
        tienTe    = 'VND'
        returnUrl = 'https://localhost:5100/DatHang/KetQua'
        ipnUrl    = 'https://localhost:5000/api/webhooks/vnpay'
    }

    if ($tao2.Ma -ne 200) {
        throw ("Khong tao duoc giao dich thu 2 (HTTP {0}): {1}" -f $tao2.Ma, $tao2.Than)
    }

    $idGd2 = [string]$tao2.DuLieu.paymentTransactionId
    $maGd2 = [string]$tao2.DuLieu.merchantReference

    $lech = [ordered]@{
        'vnp_Amount'            = '99990000'   # ⇐ 999 900 đ trong khi giao dịch là 120 000 đ
        'vnp_CurrCode'          = 'VND'
        'vnp_ResponseCode'      = '00'
        'vnp_TmnCode'           = $tmnCode
        'vnp_TransactionNo'     = '14000998'
        'vnp_TransactionStatus' = '00'
        'vnp_TxnRef'            = $maGd2
    }

    $ipnLech = Dung-IpnQuery -HienThi $lech -Ky $lech -BiMat $biMatProbe
    $kqLech = Goi-Ipn -Query $ipnLech.DayDu

    Kq 'T19 🚨 chuyen khoan LECH so tien (chu ky DUNG) => 400 + ma 0008' `
        ($kqLech.Ma -eq 400 -and $kqLech.Than -match '0008') ("HTTP {0} · body = {1}" -f $kqLech.Ma, $kqLech.Than)

    $statusGd2 = Dem-Db (@'
select "Status"::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd2)

    Kq 'T20 🚨 lech tien => DB KHONG doi (Status van Initiated = 0)' ($statusGd2 -eq 0) ("Status = {0}" -f $statusGd2)

    # ── ⑨ HOÀN VƯỢT SỐ ĐÃ THU ⇒ NÉM `0002` (không được im lặng ghi sổ) ───────────────────────
    # 🔑 `ConHoanDuoc` của giao dịch 1 = 250 000 (chưa hoàn lần nào) ⇒ yêu cầu 999 999 **phải** bị chặn ở app
    # service **TRƯỚC KHI** gọi cổng ⇒ phép đo **nhanh, tất định**, không phụ thuộc mạng.
    $hoanVuot = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/payment/hoan-tien/" + $idGd1) -Token $token -Body @{
        soTien        = 999999
        lyDo          = 'K9S5 probe hoan vuot so da thu'
        nguoiThucHien = 'k9s5@example.invalid'
    }

    Kq 'T21 🚨 hoan VUOT so da thu => KHONG PHAI 200 va ma 0002' `
        ($hoanVuot.Ma -ne 200 -and $hoanVuot.Than -match 'eCommerce:Payment:0002') `
        ("HTTP {0} · body = {1}" -f $hoanVuot.Ma, $hoanVuot.Than)

    $daHoan = Dem-Db (@'
select count(*)::text from "PayRefunds" where "PaymentTransactionId" = '{0}'::uuid;
'@ -f $idGd1)

    $refundedAmt = Dem-Db (@'
select (case when "RefundedAmount" = 0 then 1 else 0 end)::text from "PayTransactions" where "MerchantReference" = '{0}';
'@ -f $maGd1)

    Kq 'T22 hoan vuot => KHONG tao dong hoan tien va RefundedAmount van 0' `
        ($daHoan -eq 0 -and $refundedAmt -eq 1) ("dong hoan = {0} · RefundedAmount=0 = {1}" -f $daHoan, $refundedAmt)

    # ── ⑩ ĐO LẠI ĐIỀU KIỆN TIÊN QUYẾT SAU KHI KẾT THÚC ──────────────────────────────────────
    $maTrenCung = Dem-Db @'
select count(*)::text from (select "MerchantReference", count(*) c from "PayTransactions" where "MerchantReference" like '%K9S5%' group by 1 having count(*) > 1) x;
'@

    Kq 'T23 khong co MaThamChieu nao bi trung (unique index khong bi no)' ($maTrenCung -eq 0) ("ma trung = {0}" -f $maTrenCung)
}
finally {
    # ── DỌN DẸP (LUÔN chạy, kể cả khi ĐỎ) ────────────────────────────────────────────────────
    Xoa-DuLieuProbe

    # 🔒 BẬT LẠI cờ bảo mật của admin — quên là commit một **admin không bị buộc đổi mật khẩu**.
    Invoke-Psql -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@ | Out-Null

    if ($tienTrinh -ne $null -and -not $tienTrinh.HasExited) {
        Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
    }

    # Xoá biến môi trường của probe khỏi phiên hiện tại (không để rò sang lệnh sau).
    Remove-Item Env:PaymentHub__VnPay__HashSecret -ErrorAction SilentlyContinue
    Remove-Item Env:PaymentHub__VnPay__TmnCode -ErrorAction SilentlyContinue

    Start-Sleep -Seconds 1

    $conLai = Dem-Db @'
select count(*)::text from "PayTransactions" where "MerchantReference" like '%K9S5%';
'@

    $conHoan = Dem-Db @'
select count(*)::text from "PayRefunds" where "RefundRequestId" like '%K9S5%' or "Reason" like '%K9S5%';
'@

    $coBaoMat = Dem-Db @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and "ShouldChangePasswordOnNextLogin";
'@

    Write-Output ("   DON DEP: host da dung · giao dich con = {0} · dong hoan con = {1} · co bao mat = {2}" -f $conLai, $conHoan, $coBaoMat)

    Kq 'T24 DON DEP: khong con giao dich/dong hoan probe' ($conLai -eq 0 -and $conHoan -eq 0) `
        ("giao dich = {0} · dong hoan = {1}" -f $conLai, $conHoan)

    Kq 'T25 DON DEP: da BAT LAI co ShouldChangePasswordOnNextLogin' ($coBaoMat -eq 1) ("co = {0}" -f $coBaoMat)
}

# ── TỔNG KẾT ─────────────────────────────────────────────────────────────────────────────────
$soDat = @($ketQua | Where-Object { $_.Dat }).Count
$soKhong = @($ketQua | Where-Object { -not $_.Dat })

Write-Output ''
Write-Output ("════ PROBE THANH TOAN: {0}/{1} DAT ════" -f $soDat, $ketQua.Count)

if ($soKhong.Count -gt 0) {
    Write-Output ''
    Write-Output 'CAC CA KHONG DAT:'
    foreach ($k in $soKhong) {
        Write-Output ("   🚨 [{0}] {1}" -f $k.Ten, $k.ChiTiet)
    }

    exit 1
}

exit 0
