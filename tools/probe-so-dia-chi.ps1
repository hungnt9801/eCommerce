<#
.SYNOPSIS
    🚨 **PROBE SỔ ĐỊA CHỈ** (`ADDR-004` phần B, `S2`) — kiểm **HTTP THẬT** trên Host: :5000.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════
    🔑 VÌ SAO CÓ PROBE NÀY (không phải "cho đủ bộ"):

    Cổng `PostgreSqlSchemaTests` chỉ chứng minh **index** chặn trùng ở tầng DB. Còn ba câu hỏi sống còn
    thì **chỉ** HTTP thật trả lời được:

      ① endpoint có **TỒN TẠI** không và đúng **VERB** không (bài học `CUS-001` §16.2: `TimTheoIdAsync` bị
         ABP sinh thành `POST` — sai ngữ nghĩa HTTP; và module chưa khai `ConventionalControllers` thì **404
         body rỗng**, không lỗi biên dịch nào);
      ② **bỏ-trước-đặt-sau** có đúng thứ tự trong MỘT transaction không — PostgreSQL kiểm unique index **ngay
         từng câu lệnh** (`TC-06b`); sai thứ tự ⇒ `23505` ⇒ khách thấy lỗi khi thao tác **đúng**;
      ③ 🚨 **HAI LUỒNG CÙNG ĐẶT MẶC ĐỊNH ⇒ CHỈ MỘT THẮNG** (DoD của `S2`). Đây là ca mà **không** unit test
         nào bắt được: nó cần hai request **thật** chạy **đồng thời** qua khoá phân tán + DB.

    🔒 **NĂM QUY TẮC ĐƯỢC THI HÀNH (mỗi cái là một bài học đã trả giá):**
      1. **In HTTP STATUS trước body** (`README-probe` **B1**): `404`/`405` từng bị đọc thành *"rỗng"*.
      2. **NEO ASCII** trong mọi câu khẳng định — tiếng Việt trong console bị hỏng dấu (**I4**).
      3. **KHẲNG ĐỊNH TIỀN ĐỀ** trước khi đo: mã ĐVHC `01`+`00004` **có thật** trong danh mục, nếu không thì
         mọi lần gọi sẽ thất bại vì lý do **không phải** điều đang kiểm (**A8**).
      4. **DỌN DẸP trong `finally`** và **khẳng định đã sạch** — nếu không, lần sau `kiem-db.ps1` sẽ đỏ vì
         lý do vô hình (quy tắc ⑤ của `kiem-e2e`).
      5. **KHÔNG viết cứng đường dẫn route**: route thật in bằng `tools/route-check.ps1` khi cần; ở đây dùng
         đúng route của `ADDR-004` §6.2 và **kiểm `404` là thất bại** (nếu route sai, probe nói ngay).

.PARAMETER TimeoutGiay
    Chờ Host lên (giây).

.EXAMPLE
    pwsh -File tools\probe-so-dia-chi.ps1
#>
[CmdletBinding()]
param(
    [int]$TimeoutGiay = 120
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $PSScriptRoot '_tam-diachi'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

# 🔑 Nạp thư viện tiêm: chỉ có HÀM, không có `param` cấp tệp ⇒ nạp an toàn.
. (Join-Path $PSScriptRoot 'ham-tiem.ps1')

# 🔑 Thư viện "một chỗ" cho các khác biệt Windows/Linux (`XC-007` P-02/P-04/P-05 — `S9` 21/10/2026).
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$host_ = 'http://localhost:5000'
$khachId = 'e0000000-0000-0000-0000-0000000000d1'
$tienTrinh = $null
$ketQua = [System.Collections.Generic.List[object]]::new()

function Kq {
    param([string]$Ten, [bool]$Dat, [string]$ChiTiet = '')

    $ketQua.Add([pscustomobject]@{ Ten = $Ten; Dat = $Dat; ChiTiet = $ChiTiet })
    Write-Output ("   [{0}] {1} | {2}" -f $Ten, $(if ($Dat) { 'DAT' } else { 'KHONG DAT' }), $ChiTiet)
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

function Xoa-DuLieuProbe {
    # 🔒 Dọn theo ĐÚNG thứ tự khoá ngoài: liên kết → địa chỉ → khách. ⚠️ `AddrAddresses` có thể còn dòng do
    # lần chạy trước (không xoá được nếu còn liên kết) ⇒ xoá liên kết TRƯỚC.
    # 📌 Dọn theo **EMAIL** chứ không chỉ `Id`: khách do API tạo có `Id` **ngẫu nhiên** ⇒ nếu chỉ xoá theo Id
    # thì lần chạy sau `tao-khach` trả `0003` (liên hệ đã thuộc khách khác) và probe đỏ vì **DỮ LIỆU CŨ**,
    # không phải vì sản phẩm (bài học A8: phải đo điều kiện tiên quyết).
    TiemVaoDb -Sql @'
delete from "CusAddressLinks" where "CustomerId" in (select "Id" from "CusCustomers" where "Email" = 'k9s2probe@example.invalid');
'@ -KhongNemKhiThieuKiem | Out-Null

    TiemVaoDb -Sql @'
delete from "AddrAddresses" where "DiaChiDayDu" like 'S2PROBE%';
'@ -KhongNemKhiThieuKiem | Out-Null

    TiemVaoDb -Sql @'
delete from "CusCustomers" where "Email" = 'k9s2probe@example.invalid';
'@ -KhongNemKhiThieuKiem | Out-Null
}

Write-Output ''
Write-Output '════ PROBE SỔ ĐỊA CHỈ (ADDR-004 phần B) — HTTP THẬT :5000 ════'

try {
    # ── ⓪ DỌN DẤU VẾT CŨ TRƯỚC KHI ĐO (quy tắc ④) ────────────────────────────────────────────
    Xoa-DuLieuProbe

    # ── ① TIỀN ĐỀ: mã ĐVHC phải CÓ THẬT (quy tắc ③) ─────────────────────────────────────────
    $soXa = 0

    $dongXa = @(Invoke-Psql -Sql @'
select count(*)::text from "AddrAdministrativeUnits"
where "SchemaVersion" = 2 and "IsActive" and "Code" = '00004' and "ParentCode" = '01';
'@)

    if ($dongXa.Count -gt 0) { $soXa = [int]($dongXa[0]) }

    Kq 'T00 tien de: ma DVHC 01/00004 co that trong danh muc' ($soXa -eq 1) ("so dong = {0}" -f $soXa)

    if ($soXa -ne 1) {
        throw 'Thieu du lieu danh muc DVHC ⇒ moi phep do sau do VO NGHIA (khong ket luan ve san pham).'
    }

    # ── ② KHỞI ĐỘNG HOST ─────────────────────────────────────────────────────────────────────
    # 🔒 **XOÁ CACHE QUYỀN TRƯỚC KHI KHỞI ĐỘNG** (`ADDR-004`/`S2` đo được 18/10/2026): ABP cache **kết quả
    # âm** (`KHÔNG được cấp`) của `AbpPermissionGrants`. Khi một permission MỚI được cấp (DbMigrator), các bản
    # ghi cache "denied" cũ vẫn còn ⇒ API mới trả **`403`** dù DB đã cấp — và log chẩn đoán nói đúng:
    # *"quyền 'eCommerce.Addresses' · đã định nghĩa · KHÔNG được cấp cho principal hiện tại"*.
    # 📌 Đây đúng họ lỗi `README-probe` **I1/I2** (xoá **ĐÚNG key** + **in số key đã xoá**: `0 key` là dấu
    # hiệu **mẫu sai**, không phải "không có gì để xoá").
    $soKeyQuyen = 0

    try {
        $demKey = & docker exec ecommerce-redis sh -c "redis-cli --scan --pattern '*PermissionGrant*' | wc -l" 2>$null

        if ($null -ne $demKey) { $soKeyQuyen = [int](("$demKey").Trim()) }

        & docker exec ecommerce-redis sh -c 'redis-cli --scan --pattern "*PermissionGrant*" | while read k; do redis-cli DEL "$k" > /dev/null; done' 2>$null | Out-Null
    }
    catch {
        Write-Output ("   ⚠️ Không xoá được cache quyền: {0} (bỏ qua — Host sẽ tự tra DB nếu cache trống)." -f $_.Exception.Message)
    }

    Write-Output ("   Xoá cache quyền: {0} key `c:…PermissionGrant…` (📌 0 key = mẫu SAI, không phải 'không có gì để xoá')" -f $soKeyQuyen)

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

    # ── ③ LẤY TOKEN (password grant — admin seed) ─────────────────────────────────────────────
    # 🚨 **PHẢI TẮT CỜ BẢO MẬT TRONG LÚC CHẠY PROBE** (y như `kiem-e2e`): admin có
    # `ShouldChangePasswordOnNextLogin = true` ⇒ OpenIddict từ chối `password` grant với
    # `invalid_grant: ShouldChangePasswordOnNextLogin` (đo được 18/10/2026 — KHÔNG phải sai mật khẩu).
    # 🔒 Và **BẮT BUỘC BẬT LẠI** trong `finally` + khẳng định: quên là commit một **admin không bị buộc
    #    đổi mật khẩu** (đúng nhóm ② của `kiem-db.ps1`).
    TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = 'ADMIN';
'@ -KiemSql @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and not "ShouldChangePasswordOnNextLogin";
'@ -SoDongMongDoi 1 | Out-Null

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

    # ── ④ ROUTE THẬT: GET sổ địa chỉ phải là GET, không phải POST ─────────────────────────────
    $r404 = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/customer-address/so-dia-chi/" + $khachId) -Token $token
    Kq 'T02 GET so dia chi (khach chua ton tai) => 404, KHONG phai 405' ($r404.Ma -eq 404) ("HTTP {0}" -f $r404.Ma)

    $rSaiVerb = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-address/so-dia-chi/" + $khachId) -Token $token -Body @{}
    Kq 'T03 POST len route chi-doc => 405 (chung minh route la GET)' ($rSaiVerb.Ma -eq 405) ("HTTP {0}" -f $rSaiVerb.Ma)

    # ── ⑤ TẠO KHÁCH THẬT qua API ──────────────────────────────────────────────────────────────
    $taoKhach = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/customer/tao-khach' -Token $token -Body @{
        name  = 'K9S2PROBE Khach So Dia Chi'
        email = 'k9s2probe@example.invalid'
        phone = '0900000009'
    }

    Kq 'T04 tao khach qua API' ($taoKhach.Ma -eq 200 -or $taoKhach.Ma -eq 201) ("HTTP {0}" -f $taoKhach.Ma)

    $khachThat = $taoKhach.DuLieu.id

    if ([string]::IsNullOrWhiteSpace($khachThat)) {
        throw ("Khong tao duoc khach qua API (HTTP {0}): {1}" -f $taoKhach.Ma, $taoKhach.Than)
    }

    # ── ⑥ THÊM 2 ĐỊA CHỈ (chưa mặc định) ─────────────────────────────────────────────────────
    $them1 = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-address/them-dia-chi/" + $khachThat) -Token $token -Body @{
        schemaVersion = 2
        tinhCode      = '01'
        xaCode        = '00004'
        soNhaDuong    = 'S2PROBE 12 Le Loi'
        label         = 'Nha rieng'
        addressType   = 'Shipping'
        laMacDinh     = $false
    }

    $them2 = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-address/them-dia-chi/" + $khachThat) -Token $token -Body @{
        schemaVersion = 2
        tinhCode      = '01'
        xaCode        = '00004'
        soNhaDuong    = 'S2PROBE 99 Tran Hung Dao'
        label         = 'Van phong'
        addressType   = 'Shipping'
        laMacDinh     = $false
    }

    Kq 'T05 them dia chi #1' ($them1.Ma -eq 200) ("HTTP {0}" -f $them1.Ma)
    Kq 'T06 them dia chi #2 (cung tinh/xa, KHAC so nha ⇒ phai la 2 dia chi)' ($them2.Ma -eq 200) ("HTTP {0}" -f $them2.Ma)

    $link1 = $them1.DuLieu.linkId
    $link2 = $them2.DuLieu.linkId
    $addr1 = $them1.DuLieu.addressId
    $addr2 = $them2.DuLieu.addressId

    Kq 'T07 hai dia chi KHAC nhau (idempotent theo NOI DUNG, khong gop nham)' ($addr1 -ne $addr2) ("addr1={0} addr2={1}" -f $addr1, $addr2)

    # ── ⑦ THÊM LẠI ĐÚNG ĐỊA CHỈ #1 ⇒ phải trả 409 kèm `linkId` (mã 0010) ─────────────────────
    $themTrung = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-address/them-dia-chi/" + $khachThat) -Token $token -Body @{
        schemaVersion = 2
        tinhCode      = '01'
        xaCode        = '00004'
        soNhaDuong    = 'S2PROBE 12 Le Loi'
        label         = 'Nha rieng'
        addressType   = 'Shipping'
        laMacDinh     = $false
    }

    $thanTrung = $themTrung.Than
    Kq 'T08 them TRUNG dia chi => 409 + ma eCommerce:Customers:0010' `
        ($themTrung.Ma -eq 409 -and $thanTrung -like '*eCommerce:Customers:0010*') `
        ("HTTP {0}" -f $themTrung.Ma)

    # ── ⑧ 🚨 HAI LUỒNG CÙNG ĐẶT MẶC ĐỊNH (DoD của S2) ───────────────────────────────────────
    # 🔑 Chạy ĐỒNG THỜI bằng hai job nền: khoá phân tán tuần tự hoá, và partial unique index là chốt cuối.
    $job1 = Start-ThreadJob -ScriptBlock {
        param($hostUrl, $tk, $kh, $lk)
        Invoke-RestMethod -Method Post -Uri ("$hostUrl/api/app/customer-address/dat-mac-dinh?customerId=$kh&linkId=$lk") `
            -Headers @{ Authorization = "Bearer $tk" } -SkipHttpErrorCheck -StatusCodeVariable 'ma' `
            -ContentType 'application/json' | Out-Null
        return [int]$ma
    } -ArgumentList $host_, $token, $khachThat, $link1

    $job2 = Start-ThreadJob -ScriptBlock {
        param($hostUrl, $tk, $kh, $lk)
        Invoke-RestMethod -Method Post -Uri ("$hostUrl/api/app/customer-address/dat-mac-dinh?customerId=$kh&linkId=$lk") `
            -Headers @{ Authorization = "Bearer $tk" } -SkipHttpErrorCheck -StatusCodeVariable 'ma' `
            -ContentType 'application/json' | Out-Null
        return [int]$ma
    } -ArgumentList $host_, $token, $khachThat, $link2

    $ma1 = Receive-Job -Job $job1 -Wait
    $ma2 = Receive-Job -Job $job2 -Wait

    $soThanhCong = @($ma1, $ma2 | Where-Object { $_ -eq 200 }).Count
    $soLoi5xx = @($ma1, $ma2 | Where-Object { $_ -ge 500 }).Count

    # 📌 **BẤT BIẾN ĐÚNG Ở ĐÂY LÀ TRẠNG THÁI CUỐI**, không phải "một request phải đỏ": khoá phân tán **tuần tự
    # hoá** hai thao tác ⇒ luồng sau **hợp lệ** khi trở thành mặc định (bỏ-trước-đặt-sau). 🚨 Cái KHÔNG được
    # xảy ra: `5xx`, hoặc **HAI** mặc định cùng lúc (kiểm ở T11), hoặc cả hai cùng đỏ (khoá chặn nhầm người dùng).
    Kq 'T09 🚨 hai luong cung dat mac dinh => KHONG 5xx va IT NHAT mot 200 (bat bien cuoi o T11)' `
        ($soLoi5xx -eq 0 -and $soThanhCong -ge 1) `
        ("ma1={0} ma2={1} · 200={2} · 5xx={3}" -f $ma1, $ma2, $soThanhCong, $soLoi5xx)

    # ── ⑨ ĐỌC LẠI SỔ: đúng MỘT mặc định, và mặc định phải là một trong hai mục ───────────────
    $so = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/customer-address/so-dia-chi/" + $khachThat) -Token $token

    $ds = @($so.DuLieu.cacDiaChi)
    $macDinh = @($ds | Where-Object { $_.isDefault -eq $true })

    Kq 'T10 so dia chi = 2' ($ds.Count -eq 2) ("dem = {0}" -f $ds.Count)
    Kq 'T11 🚨 DUNG MOT mac dinh sau tranh chap' ($macDinh.Count -eq 1) ("dem mac dinh = {0}" -f $macDinh.Count)
    Kq 'T12 mac dinh la mot trong hai muc vua tao' `
        ($macDinh.Count -eq 1 -and (@($link1, $link2) -contains $macDinh[0].linkId)) `
        ("link mac dinh = {0}" -f $macDinh[0].linkId)

    # ── ⑩ BẤT BIẾN APPEND-ONLY: gỡ mục mặc định ⇒ phải có mặc định THAY THẾ (A4) ─────────────
    $go = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-address/go-dia-chi?customerId=" + $khachThat + "&linkId=" + $macDinh[0].linkId) -Token $token -Body @{}

    Kq 'T13 go dia chi dang mac dinh => 200' ($go.Ma -eq 200 -or $go.Ma -eq 204) ("HTTP {0}" -f $go.Ma)

    $so2 = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/customer-address/so-dia-chi/" + $khachThat) -Token $token
    $ds2 = @($so2.DuLieu.cacDiaChi)
    $macDinh2 = @($ds2 | Where-Object { $_.isDefault -eq $true })

    Kq 'T14 🚨 A4: go mac dinh ⇒ con lai DUOC dat mac dinh thay the' `
        ($ds2.Count -eq 1 -and $macDinh2.Count -eq 1 -and $macDinh2[0].coDungDuoc) `
        ("con lai = {0} · mac dinh = {1}" -f $ds2.Count, $macDinh2.Count)

    # ── ⑪ XOÁ MỀM: mục đã gỡ KHÔNG được phục hồi (0014) ─────────────────────────────────────
    $phucHoi = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-address/dat-mac-dinh?customerId=" + $khachThat + "&linkId=" + $macDinh[0].linkId) -Token $token -Body @{}
    Kq 'T15 dat mac dinh cho muc DA GO => 404 (0011) — khong hoi sinh' ($phucHoi.Ma -eq 404) ("HTTP {0}" -f $phucHoi.Ma)
}
finally {
    # ── ⑫ DỌN DẸP — chạy KỂ CẢ KHI ĐỎ (quy tắc ④) ────────────────────────────────────────────
    try {
        if ($null -ne $tienTrinh -and -not $tienTrinh.HasExited) {
            Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
        }

        Xoa-DuLieuProbe

        # 🔒 BẬT LẠI cờ bảo mật + KHẲNG ĐỊNH (nhóm ② của `kiem-db.ps1`) — kể cả khi probe đỏ.
        TiemVaoDb -Sql @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@ -KiemSql @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and "ShouldChangePasswordOnNextLogin";
'@ -SoDongMongDoi 1 | Out-Null

        $conLai = @(Invoke-Psql -Sql @'
select count(*)::text from "CusAddressLinks"
where "CustomerId" in (select "Id" from "CusCustomers" where "Email" = 'k9s2probe@example.invalid');
'@)

        $soConLai = if ($conLai.Count -gt 0) { [int]$conLai[0] } else { -1 }

        Kq 'T16 DON DEP: khong con lien ket cua khach probe' ($soConLai -eq 0) ("con lai = {0}" -f $soConLai)

        Write-Output ("   DON DEP: host da dung · khach probe con={0}" -f $soConLai)
    }
    catch {
        Write-Output ("   🚨 DON DEP LOI: {0}" -f $_.Exception.Message)
    }
}

$hong = @($ketQua | Where-Object { -not $_.Dat })
$dat = @($ketQua | Where-Object { $_.Dat })

Write-Output ''
Write-Output ("════ PROBE SO DIA CHI: {0}/{1} DAT ════" -f $dat.Count, $ketQua.Count)

if ($hong.Count -gt 0) {
    Write-Output 'CAC CA KHONG DAT:'
    foreach ($h in $hong) { Write-Output ("   [{0}] {1}" -f $h.Ten, $h.ChiTiet) }
    Write-Output ("Log host: {0}" -f (Join-Path $thuMucTam 'host.log'))
    exit 1
}

exit 0
