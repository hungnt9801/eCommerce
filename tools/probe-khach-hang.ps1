<#
.SYNOPSIS
    🚨 **PROBE KHÁCH HÀNG** (`CUS-001` — `S3`) — kiểm **HTTP THẬT** trên Host: :5000.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **VÌ SAO CÓ PROBE NÀY (ba câu hỏi mà unit test KHÔNG THỂ trả lời):**

      ① 🚨 **CỔNG "KHÁCH BỊ CHẶN KHÔNG ĐƯỢC MUA" CÓ THẬT SỰ CHẶN Ở ĐƯỜNG HTTP KHÔNG** (`CUS-001`
         `ADR-04`, nợ §16.4 #4). Đây là lỗi **ĐÃ XẢY RA**: hàm `ChoPhepGiaoDichAsync` tồn tại từ
         16/10/2026 mà **gọi 0 nơi** ⇒ khách `Blocked` **vẫn đặt được hàng**. 📌 Test quét mã
         (`CuaKiemKhachBiChanTests`) chỉ chứng minh *"lời gọi CÓ MẶT trong tệp"* — **không** chứng minh
         nó **chạy**. Ở đây đo **cả hai chiều**: bị chặn ⇒ `403` + `0005` + **DB không có đơn**;
         không bị chặn ⇒ **KHÔNG** phải `0005`.

      ② **PHÂN TRANG CÓ ĐÚNG KHÔNG** (nợ §16.4 #7): trang 2 phải trả dòng **KHÁC** trang 1, và
         `totalCount` phải **KHÔNG đổi** giữa hai trang (📌 đếm-sau-khi-cắt là lỗi im lặng kinh điển).

      ③ **API NHÓM KHÁCH CÓ THẬT KHÔNG** (nợ §16.4 #2): route + verb + mã lỗi `0008` khi trùng mã.
         🔑 *"Định nghĩa trong tài liệu"* **không phải** *"có endpoint"* — chỉ HTTP trả lời được.

    🔒 **NĂM QUY TẮC ĐƯỢC THI HÀNH (mỗi cái là một bài học đã trả giá):**
      1. **In HTTP STATUS trước body** (`README-probe` **B1**): `404`/`405` từng bị đọc thành *"rỗng"*.
      2. **NEO ASCII** trong mọi câu khẳng định — tiếng Việt trong console bị hỏng dấu (**I4**).
      3. **KHẲNG ĐỊNH TIỀN ĐỀ** trước khi đo: seed nhóm `RETAIL/WHOLESALE/VIP` phải **có thật** trong DB,
         nếu không thì mọi ca nhóm khách sẽ đỏ vì lý do **không phải** điều đang kiểm (**A8**).
      4. **DỌN DẸP trong `finally`** + **khẳng định đã sạch** — nếu không, lần sau `kiem-db.ps1` đỏ vì
         lý do vô hình (quy tắc ⑤ của `kiem-e2e`).
      5. **KHÔNG viết cứng route**: dùng đúng route **đo được** bằng `tools/route-check.ps1`
         (`/api/app/customer/danh-sach-phan-trang`, `/api/app/customer-group/...`) và **coi `404` là ĐỎ**
         (nếu route lệch, probe nói ngay chứ không "đoán").

.PARAMETER TimeoutGiay
    Chờ Host lên (giây).

.EXAMPLE
    pwsh -File tools\probe-khach-hang.ps1
#>
[CmdletBinding()]
param(
    [int]$TimeoutGiay = 120
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $PSScriptRoot '_tam-khachhang'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

# 🔑 Nạp thư viện tiêm: chỉ có HÀM, không có `param` cấp tệp ⇒ nạp an toàn.
. (Join-Path $PSScriptRoot 'ham-tiem.ps1')

# 🔑 Thư viện "một chỗ" cho các khác biệt Windows/Linux (`XC-007` P-02/P-04/P-05 — `S9` 21/10/2026).
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$host_ = 'http://localhost:5000'
$tienTrinh = $null
$ketQua = [System.Collections.Generic.List[object]]::new()

# 📌 Tiền tố `K9S3` cho MỌI dữ liệu probe: `kiem-db.ps1` nhóm ⑤ quét `like 'k9%'` trên tên/email/mã.
$tienTo = 'K9S3PROBE'
$emailGoc = 'k9s3probe'                     # + '@example.invalid'
$maNhom = 'K9S3PROBEGROUP'
$khoaCheckoutBiChan = 'K9S3-BICHAN-1'
$khoaCheckoutChoPhep = 'K9S3-CHOPHEP-1'
$khoaCheckoutVangLai = 'K9S3-VANGLAI-1'

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

function Dem-Db {
    param([string]$Sql)

    $dong = @(Invoke-Psql -Sql $Sql)
    if ($dong.Count -eq 0) { return -1 }

    return [int](($dong[0]).Trim())
}

<#
    🔒 **DỌN DẸP THEO ĐÚNG THỨ TỰ KHOÁ NGOẠI** — 📌 sai thứ tự thì `delete` **ném** và dữ liệu probe ở lại
    ⇒ lần sau `kiem-db.ps1` đỏ vì lý do vô hình (quy tắc ④).
    ⚠️ Xoá theo **tiền tố `K9S3`** ở **mọi** bảng đã chạm, không chỉ theo `Id`: khách do **API** tạo có `Id`
    **ngẫu nhiên**, nên nếu chỉ xoá theo `Id` thì lần chạy sau `tao-khach` trả `0003` (liên hệ đã thuộc khách
    khác) và probe đỏ vì **DỮ LIỆU CŨ**, không phải vì sản phẩm.
#>
function Xoa-DuLieuProbe {
    # ① Đơn + dòng đơn (con của khách) — theo SỐ ĐƠN của probe.
    Invoke-Psql -Sql @'
delete from "OrdOrderLines" where "OrderId" in (select "Id" from "OrdOrders" where "Number" like 'WEB-K9S3%');
'@ | Out-Null

    Invoke-Psql -Sql @'
delete from "OrdOrders" where "Number" like 'WEB-K9S3%';
'@ | Out-Null

    # ② Lượt giữ chỗ + sổ giải trình tính giá của các khoá checkout probe.
    Invoke-Psql -Sql @'
delete from "InvStockReservations" where "ReferenceId" like 'Checkout:K9S3%';
'@ | Out-Null

    Invoke-Psql -Sql @'
delete from "PrcPriceCalculationRecords" where "IdempotencyKey" like 'K9S3%';
'@ | Out-Null

    # ③ Nhóm khách probe (không có FK từ `CusCustomers` — `CustomerGroupId` là cột thường, xoá trước cho sạch).
    Invoke-Psql -Sql @'
delete from "CusGroups" where "Code" like 'K9S3%';
'@ | Out-Null

    # ④ Khách probe — SAU CÙNG (vì `OrdOrders.CustomerId` trỏ tới đây).
    Invoke-Psql -Sql @'
delete from "CusCustomers" where "Name" like 'K9S3PROBE%';
'@ | Out-Null
}

Write-Output ''
Write-Output '════ PROBE KHÁCH HÀNG (CUS-001 · S3) — HTTP THẬT :5000 ════'

try {
    # ── ⓪ DỌN DẤU VẾT CŨ TRƯỚC KHI ĐO (quy tắc ④) ────────────────────────────────────────────
    Xoa-DuLieuProbe

    $conKhachCu = Dem-Db @'
select count(*)::text from "CusCustomers" where "Name" like 'K9S3PROBE%';
'@

    Kq 'T00 tien de: khong con du lieu probe cu' ($conKhachCu -eq 0) ("khach con = {0}" -f $conKhachCu)

    if ($conKhachCu -ne 0) {
        throw 'Con du lieu probe cu ⇒ moi phep do sau do co the XANH VI LY DO SAI.'
    }

    # ── ① KHỞI ĐỘNG HOST ─────────────────────────────────────────────────────────────────────
    # 🔒 **XOÁ CACHE QUYỀN TRƯỚC KHI KHỞI ĐỘNG** (`S2` đo được): ABP cache **kết quả âm** của
    # `AbpPermissionGrants`. Permission `eCommerce.Customers.ManageGroups` là **MỚI ở `S3`** ⇒ nếu cache còn
    # bản ghi "denied" từ trước thì API nhóm khách trả **`403` dù DB đã cấp** — 📌 và ta sẽ kết luận sai là
    # *"phân quyền hỏng"* trong khi thật ra **cache cũ**.
    $soKeyQuyen = 0

    try {
        $demKey = & docker exec ecommerce-redis sh -c "redis-cli --scan --pattern '*PermissionGrant*' | wc -l" 2>$null

        if ($null -ne $demKey) { $soKeyQuyen = [int](("$demKey").Trim()) }

        & docker exec ecommerce-redis sh -c 'redis-cli --scan --pattern "*PermissionGrant*" | while read k; do redis-cli DEL "$k" > /dev/null; done' 2>$null | Out-Null
    }
    catch {
        Write-Output ("   ⚠️ Không xoá được cache quyền: {0} (bỏ qua — Host sẽ tra DB nếu cache trống)." -f $_.Exception.Message)
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

    # 🚨 **PHẢI TẮT CỜ BẢO MẬT TRONG LÚC CHẠY PROBE** (y như `kiem-e2e`/`probe-so-dia-chi`): admin có
    # `ShouldChangePasswordOnNextLogin = true` ⇒ OpenIddict từ chối `password` grant
    # (`invalid_grant: ShouldChangePasswordOnNextLogin` — KHÔNG phải sai mật khẩu).
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

    # ── ③ TIỀN ĐỀ: SEED NHÓM KHÁCH PHẢI CÓ THẬT (quy tắc ③) ─────────────────────────────────
    # 🔑 `CustomerGroupDataSeedContributor` chạy trong `DbMigrator`. ⚠️ Nếu chưa chạy migrator thì bảng rỗng
    # ⇒ mọi ca nhóm khách sẽ đỏ vì **TIỀN ĐỀ THIẾU**, không phải vì API hỏng. 📌 Phải nói rõ hai thứ đó khác nhau.
    $soNhomSeed = Dem-Db @'
select count(*)::text from "CusGroups" where "Code" in ('RETAIL','WHOLESALE','VIP') and not "IsDeleted";
'@

    Kq 'T02 tien de: seed du 3 nhom RETAIL/WHOLESALE/VIP trong DB' ($soNhomSeed -eq 3) ("so nhom = {0}" -f $soNhomSeed)

    if ($soNhomSeed -ne 3) {
        throw (("Thieu seed nhom khach (co {0}/3) ⇒ chay ``src\eCommerce.DbMigrator`` TRUOC (dotnet eCommerce.DbMigrator.dll). " +
            "PHEP DO VE NHOM KHACH VO NGHIA neu thieu tien de.") -f $soNhomSeed)
    }

    # ── ④ ROUTE + PHÂN TRANG (`CUS-001` §16.4 #7) ────────────────────────────────────────────
    $trang0 = Goi-Api -PhuongThuc 'Get' -DuongDan '/api/app/customer/danh-sach-phan-trang?skipCount=0&maxResultCount=2' -Token $token
    Kq 'T03 GET danh-sach-phan-trang => 200 (route ton tai, dung verb)' ($trang0.Ma -eq 200) ("HTTP {0}" -f $trang0.Ma)

    if ($trang0.Ma -ne 200) {
        throw ("Route phan trang KHONG tra 200 (HTTP {0}) ⇒ cac ca sau VO NGHIA: {1}" -f $trang0.Ma, $trang0.Than)
    }

    # 🚨 `totalCount` **BẮT BUỘC** có mặt: 📌 thiếu nó thì UI không thể nói *"còn bao nhiêu hồ sơ"* — đúng nợ cũ.
    $coTongSo = $null -ne $trang0.DuLieu.totalCount
    Kq 'T04 phan trang TRA VE totalCount (dieu kien cua phan trang that)' $coTongSo ("totalCount = {0}" -f $trang0.DuLieu.totalCount)

    # ── ⑤ TẠO 3 KHÁCH PROBE QUA API ──────────────────────────────────────────────────────────
    $idKhach = [System.Collections.Generic.List[string]]::new()

    for ($i = 1; $i -le 3; $i++) {
        $tao = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/customer/tao-khach' -Token $token -Body @{
            name  = "$tienTo Khach Phan Trang $i"
            email = "$emailGoc$i@example.invalid"
            phone = "090000030$i"
        }

        if ($tao.Ma -eq 200 -or $tao.Ma -eq 201) {
            $idKhach.Add([string]$tao.DuLieu.id)
        }
    }

    Kq 'T05 tao 3 khach probe qua API' ($idKhach.Count -eq 3) ("tao duoc = {0}/3" -f $idKhach.Count)

    if ($idKhach.Count -ne 3) {
        throw ("Khong tao du 3 khach probe ⇒ cac ca phan trang VO NGHIA (HTTP loi o tren).")
    }

    # ── ⑥ PHÂN TRANG: TRANG 1 và TRANG 2 PHẢI KHÁC NHAU, `totalCount` KHÔNG ĐỔI ─────────────
    $tuKhoa = "$tienTo Khach Phan Trang"

    $p1 = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/customer/danh-sach-phan-trang?keyword=" + [uri]::EscapeDataString($tuKhoa) + "&skipCount=0&maxResultCount=2") -Token $token
    $p2 = Goi-Api -PhuongThuc 'Get' -DuongDan ("/api/app/customer/danh-sach-phan-trang?keyword=" + [uri]::EscapeDataString($tuKhoa) + "&skipCount=2&maxResultCount=2") -Token $token

    $ds1 = @($p1.DuLieu.items)
    $ds2 = @($p2.DuLieu.items)

    Kq 'T06 trang 1 tra DUNG 2 dong (maxResultCount duoc ap dung)' ($p1.Ma -eq 200 -and $ds1.Count -eq 2) ("HTTP {0} · so dong = {1}" -f $p1.Ma, $ds1.Count)
    Kq 'T07 trang 2 tra DU dong con lai (3 khach ⇒ 1 dong)' ($p2.Ma -eq 200 -and $ds2.Count -eq 1) ("HTTP {0} · so dong = {1}" -f $p2.Ma, $ds2.Count)

    # 🔑 **BẤT BIẾN QUAN TRỌNG NHẤT CỦA PHÂN TRANG:** `totalCount` là tổng của **TOÀN BỘ** kết quả khớp bộ lọc,
    # KHÔNG phải số dòng của trang. ⚠️ Nếu đếm sau khi `Skip/Take` thì nó bằng 2 ⇒ 📌 pager **không bao giờ**
    # hiện trang 2 và người dùng tin rằng **chỉ có 2 hồ sơ** (lỗi im lặng).
    Kq 'T08 🚨 totalCount = 3 o CA HAI trang (dem TRUOC khi cat trang)' `
        ($p1.DuLieu.totalCount -eq 3 -and $p2.DuLieu.totalCount -eq 3) `
        ("trang1={0} · trang2={1}" -f $p1.DuLieu.totalCount, $p2.DuLieu.totalCount)

    # 🔑 **ĐO CẢ HAI CHIỀU:** trang 2 phải có dòng **KHÁC** trang 1. ⚠️ Nếu `Skip` bị bỏ qua thì hai trang
    # **giống hệt** nhau — và một phép kiểm chỉ nhìn *"trang 2 có dữ liệu"* sẽ **XANH** dù phân trang hỏng.
    $id1 = @($ds1 | ForEach-Object { [string]$_.id })
    $id2 = @($ds2 | ForEach-Object { [string]$_.id })
    $trung = @($id1 | Where-Object { $id2 -contains $_ })

    Kq 'T09 🚨 trang 2 co dong KHAC trang 1 (Skip thuc su co tac dung)' `
        ($id2.Count -gt 0 -and $trung.Count -eq 0) `
        ("trung id = {0}" -f $trung.Count)

    # 🔑 **Và bộ lọc phải đi cùng phân trang:** `keyword` sai ⇒ 0 dòng. ⚠️ Ca này chặn việc `keyword` bị
    # **bỏ qua âm thầm** (khi đó trang 2 sẽ chứa khách **không khớp** từ khoá người dùng vừa gõ).
    Kq 'T10 bo loc keyword + phan trang: chi 3 khach probe khop' `
        ($p1.DuLieu.totalCount -eq 3 -and $ds1.Count -eq 2 -and $ds2.Count -eq 1) `
        ("tong = {0}" -f $p1.DuLieu.totalCount)

    # ── ⑦ API NHÓM KHÁCH (`CUS-001` §16.4 #2) ────────────────────────────────────────────────
    $nhomDs = Goi-Api -PhuongThuc 'Get' -DuongDan '/api/app/customer-group/danh-sach?skipCount=0&maxResultCount=100' -Token $token
    Kq 'T11 GET danh-sach nhom khach => 200' ($nhomDs.Ma -eq 200) ("HTTP {0}" -f $nhomDs.Ma)

    $maTrongDs = @($nhomDs.DuLieu.items | ForEach-Object { [string]$_.code })
    $coDuBa = ($maTrongDs -contains 'RETAIL') -and ($maTrongDs -contains 'WHOLESALE') -and ($maTrongDs -contains 'VIP')

    Kq 'T12 danh sach nhom co DU 3 ma seed (RETAIL/WHOLESALE/VIP)' $coDuBa ("so nhom = {0}" -f $maTrongDs.Count)

    $taoNhom = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/customer-group/tao-nhom' -Token $token -Body @{
        code        = $maNhom
        name        = "$tienTo Nhom Thu"
        description = 'Nhom do probe S3 tao ra — se bi xoa'
    }

    Kq 'T13 POST tao-nhom => 200' ($taoNhom.Ma -eq 200 -or $taoNhom.Ma -eq 201) ("HTTP {0}" -f $taoNhom.Ma)

    $idNhom = [string]$taoNhom.DuLieu.id

    # 🔑 **MÃ LỖI TRÙNG MÃ NHÓM** (`0008`) — 📌 không được là `500`: hai người cùng tạo một mã là chuyện
    # **bình thường**, và `500` khiến người dùng **bấm lại** (đúng lý do `ViPhamRangBuocDb` tồn tại).
    $taoTrung = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/customer-group/tao-nhom' -Token $token -Body @{
        code = $maNhom
        name = "$tienTo Nhom Thu (trung ma)"
    }

    Kq 'T14 🚨 tao TRUNG ma nhom => 409 + eCommerce:Customers:0008' `
        ($taoTrung.Ma -eq 409 -and $taoTrung.Than -like '*eCommerce:Customers:0008*') `
        ("HTTP {0}" -f $taoTrung.Ma)

    # 🔒 **SỬA TÊN NHÓM** — và **ĐỌC LẠI** (không tin HTTP 200): 📌 *"cổng không bắt"* và *"tiêm không thành"*
    # trông giống hệt nhau nếu chỉ đo mã trả về.
    $tenMoi = "$tienTo Nhom Thu (da doi ten)"
    $sua = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-group/" + $idNhom + "/cap-nhat-nhom") -Token $token -Body @{
        name        = $tenMoi
        description = 'da sua boi probe S3'
    }

    $docLai = Dem-Db ("select count(*)::text from ""CusGroups"" where ""Code"" = '{0}' and ""Name"" = '{1}';" -f $maNhom, $tenMoi)

    Kq 'T15 🚨 sua ten nhom: HTTP 200 VA DB da doi (doc lai, khong tin 200)' `
        (($sua.Ma -eq 200) -and ($docLai -eq 1)) `
        ("HTTP {0} · dong khop ten moi = {1}" -f $sua.Ma, $docLai)

    # ⚠️ **`Code` phải BẤT BIẾN** — gửi `code` khác trong body sửa: DTO **không** có trường `Code` ⇒ bị bỏ qua
    # ⇒ mã cũ **phải còn**. 📌 Ca này chặn việc "sửa được mã" mà không ai biết (bảng giá tham chiếu sẽ trỏ hư).
    $suaMa = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer-group/" + $idNhom + "/cap-nhat-nhom") -Token $token -Body @{
        code = 'K9S3PROBEGROUP2'
        name = $tenMoi
    }

    $maCuCon = Dem-Db ("select count(*)::text from ""CusGroups"" where ""Id"" = '{0}' and ""Code"" = '{1}';" -f $idNhom, $maNhom)

    Kq 'T16 ma nhom BAT BIEN sau khi sua (code gui len bi bo qua)' `
        (($suaMa.Ma -eq 200 -or $suaMa.Ma -eq 400) -and ($maCuCon -eq 1)) `
        ("HTTP {0} · ma cu con = {1}" -f $suaMa.Ma, $maCuCon)

    # ── ⑧ 🚨 CỔNG KHÁCH BỊ CHẶN — CHIỀU CHẶN (DoD của S3) ────────────────────────────────────
    $khachChan = $idKhach[0]

    $chan = Goi-Api -PhuongThuc 'Post' -DuongDan ("/api/app/customer/chan-khach/" + $khachChan + "?lyDo=" + [uri]::EscapeDataString('probe S3: kiem cong chan')) -Token $token -Body @{}
    Kq 'T17 chan khach probe => 200' ($chan.Ma -eq 200) ("HTTP {0}" -f $chan.Ma)

    # ⚠️ **`DungDuoc` KHÔNG phải cột DB** — nó là thuộc tính tính toán `Status != Blocked` ở entity
    # (kiểm bằng `information_schema`: bảng `CusCustomers` **không** có cột `DungDuoc`).
    # 📌 Nếu viết `where not "DungDuoc"` thì PostgreSQL báo **lỗi cột không tồn tại**, và nếu vô tình bọc
    # trong `try/catch` thì ta **tưởng** "0 dòng" = "chưa chặn" ⇒ kết luận sai hoàn toàn. ✅ Dùng `Status = 2`.
    $dungDuoc = Dem-Db ("select count(*)::text from ""CusCustomers"" where ""Id"" = '{0}' and ""Status"" = 2;" -f $khachChan)
    Kq 'T18 TIEN DE: khach da THUC SU bi chan trong DB (Status = Blocked = 2)' ($dungDuoc -eq 1) ("dong khop = {0}" -f $dungDuoc)

    if ($dungDuoc -ne 1) {
        throw 'Khong chan duoc khach probe ⇒ ca cong chan VO NGHIA (khong ket luan ve san pham).'
    }

    $khoGia = 'e0000000-0000-0000-0000-0000000000c1'   # GUID thuoc khoi probe ⇒ khong sinh dong that
    $skulGia = 'K9S3PROBE-SKU-01'

    $checkoutBiChan = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/checkout/tinh-va-giu-cho' -Token $token -Body @{
        channelCode    = 'web'
        warehouseId    = $khoGia
        customerId     = $khachChan
        idempotencyKey = $khoaCheckoutBiChan
        items          = @(@{ sku = $skulGia; quantity = 1; taxRate = 0.1 })
    }

    Kq 'T19 🚨 khach BI CHAN dat hang o CHECKOUT => 403 + eCommerce:Customers:0005' `
        ($checkoutBiChan.Ma -eq 403 -and $checkoutBiChan.Than -like '*eCommerce:Customers:0005*') `
        ("HTTP {0}" -f $checkoutBiChan.Ma)

    # 🔑 **KHÔNG CÓ TÁC DỤNG PHỤ NÀO ĐƯỢC XẢY RA** — đây mới là điều làm cổng đáng tin.
    # 📌 Nếu cổng đặt SAU bước giữ chỗ/tính giá thì lệnh gọi trên sẽ để lại **lượt giữ chỗ** (hàng bị giam 15 phút)
    # hoặc **sổ giải trình** — dù HTTP trả 403. ⚠️ Một phép kiểm chỉ nhìn mã lỗi sẽ **XANH** trong ca đó.
    $soGiuCho = Dem-Db ("select count(*)::text from ""InvStockReservations"" where ""ReferenceId"" = 'Checkout:{0}';" -f $khoaCheckoutBiChan)
    $soSoGia = Dem-Db ("select count(*)::text from ""PrcPriceCalculationRecords"" where ""IdempotencyKey"" = '{0}';" -f $khoaCheckoutBiChan)
    $soDon = Dem-Db ("select count(*)::text from ""OrdOrders"" where ""Number"" like 'WEB-{0}%';" -f $khoaCheckoutBiChan)

    Kq 'T20 🚨 KHONG co tac dung phu: 0 luot giu cho · 0 so giai trinh · 0 don' `
        ($soGiuCho -eq 0 -and $soSoGia -eq 0 -and $soDon -eq 0) `
        ("giuCho={0} · soGia={1} · don={2}" -f $soGiuCho, $soSoGia, $soDon)

    # ── ⑨ 🚨 CỔNG — HAI CHIỀU (chống "chặn TẤT CẢ" trông giống "chặn đúng") ──────────────────
    # 📌 Một cổng **luôn ném** cũng cho ra `0005` ở ca trên. ✅ Chiều ngược lại mới phân biệt được:
    # khách **KHÔNG** bị chặn ⇒ **KHÔNG** phải `0005`.
    $khachChoPhep = $idKhach[1]

    $checkoutChoPhep = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/checkout/tinh-va-giu-cho' -Token $token -Body @{
        channelCode    = 'web'
        warehouseId    = $khoGia
        customerId     = $khachChoPhep
        idempotencyKey = $khoaCheckoutChoPhep
        items          = @(@{ sku = $skulGia; quantity = 1; taxRate = 0.1 })
    }

    Kq 'T21 chieu nguoc: khach KHONG bi chan => KHONG phai 0005 (cong khong chan bua)' `
        ($checkoutChoPhep.Than -notlike '*eCommerce:Customers:0005*') `
        ("HTTP {0}" -f $checkoutChoPhep.Ma)

    # ➕ **KHÁCH VÃNG LAI** (`customerId` rỗng) phải được đi qua — `ADR-03`: vãng lai là hạng nhất.
    # 🚨 Đây là ca dễ hỏng nhất khi viết cổng: bind rỗng thành `Guid.Empty` ⇒ tra DB không thấy ⇒ **chặn tất cả**
    # đơn vãng lai. 📌 Triệu chứng: *"web không bán được cho khách mới"* — rất dễ bị hiểu thành lỗi khác.
    $checkoutVangLai = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/checkout/tinh-va-giu-cho' -Token $token -Body @{
        channelCode    = 'web'
        warehouseId    = $khoGia
        idempotencyKey = $khoaCheckoutVangLai
        items          = @(@{ sku = $skulGia; quantity = 1; taxRate = 0.1 })
    }

    Kq 'T22 🚨 khach VANG LAI (khong customerId) => KHONG bi 0005 chan' `
        ($checkoutVangLai.Than -notlike '*eCommerce:Customers:0005*') `
        ("HTTP {0}" -f $checkoutVangLai.Ma)

    # ── ⑩ 🚨 CỔNG Ở ĐƯỜNG TẠO ĐƠN (đường vào THỨ HAI) ──────────────────────────────────────
    # 📌 `SAL-001` là API công khai cho module/đối tác ⇒ *"checkout đã kiểm"* **KHÔNG** suy ra *"đường tạo đơn
    # đã kiểm"*. 🔒 Đây là lý do cổng được kiểm ở **cả hai** biên.
    $taoDonBiChan = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/don-hang/tao-tu-checkout' -Token $token -Body @{
        checkoutIdempotencyKey = $khoaCheckoutBiChan
        number                 = "WEB-$khoaCheckoutBiChan"
        channelCode            = 'web'
        customerId             = $khachChan
    }

    $soDonSau = Dem-Db ("select count(*)::text from ""OrdOrders"" where ""Number"" like 'WEB-{0}%';" -f $khoaCheckoutBiChan)

    Kq 'T23 🚨 khach BI CHAN tao don o TAO-DON => 403 + 0005 VA khong co don' `
        ($taoDonBiChan.Ma -eq 403 -and $taoDonBiChan.Than -like '*eCommerce:Customers:0005*' -and $soDonSau -eq 0) `
        ("HTTP {0} · don tao duoc = {1}" -f $taoDonBiChan.Ma, $soDonSau)

    # ➕ Chiều ngược: khách **không** bị chặn ⇒ **KHÔNG** phải `0005` (đơn sẽ hỏng ở bước khác vì SKU không có
    # giá — 📌 đó KHÔNG phải điều ta đang kiểm, nhưng nó chứng minh cổng đã cho qua).
    $taoDonChoPhep = Goi-Api -PhuongThuc 'Post' -DuongDan '/api/app/don-hang/tao-tu-checkout' -Token $token -Body @{
        checkoutIdempotencyKey = $khoaCheckoutChoPhep
        number                 = "WEB-$khoaCheckoutChoPhep"
        channelCode            = 'web'
        customerId             = $khachChoPhep
    }

    Kq 'T24 chieu nguoc: tao don voi khach KHONG bi chan => KHONG phai 0005' `
        ($taoDonChoPhep.Than -notlike '*eCommerce:Customers:0005*') `
        ("HTTP {0}" -f $taoDonChoPhep.Ma)
}
finally {
    # ── ⑪ DỌN DẸP — chạy KỂ CẢ KHI ĐỎ (quy tắc ④) ────────────────────────────────────────────
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

        $conKhach = Dem-Db @'
select count(*)::text from "CusCustomers" where "Name" like 'K9S3PROBE%';
'@

        $conNhom = Dem-Db @'
select count(*)::text from "CusGroups" where "Code" like 'K9S3%';
'@

        $conDon = Dem-Db @'
select count(*)::text from "OrdOrders" where "Number" like 'WEB-K9S3%';
'@

        Kq 'T25 DON DEP: khong con khach/nhom/don probe' `
            ($conKhach -eq 0 -and $conNhom -eq 0 -and $conDon -eq 0) `
            ("khach={0} · nhom={1} · don={2}" -f $conKhach, $conNhom, $conDon)

        Write-Output ("   DON DEP: host da dung · khach={0} · nhom={1} · don={2}" -f $conKhach, $conNhom, $conDon)
    }
    catch {
        Write-Output ("   🚨 DON DEP LOI: {0}" -f $_.Exception.Message)
    }
}

$hong = @($ketQua | Where-Object { -not $_.Dat })
$dat = @($ketQua | Where-Object { $_.Dat })

Write-Output ''
Write-Output ("════ PROBE KHACH HANG: {0}/{1} DAT ════" -f $dat.Count, $ketQua.Count)

if ($hong.Count -gt 0) {
    Write-Output 'CAC CA KHONG DAT:'
    foreach ($h in $hong) { Write-Output ("   [{0}] {1}" -f $h.Ten, $h.ChiTiet) }
    Write-Output ("Log host: {0}" -f (Join-Path $thuMucTam 'host.log'))
    exit 1
}

exit 0
