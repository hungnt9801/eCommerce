<#
.SYNOPSIS
    🚨 **PROBE ĐƠN SÀN** (`SAL-005` · `S6`) — kiểm **ĐƯỜNG CHẠY THẬT** và **DB THẬT**: event lỗi ⇒ DLQ
    (không mất) · watermark chạy trên dữ liệu thật · nhật ký sự kiện ghi đúng một dòng · job `D12` chạy.

.DESCRIPTION
    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔑 **VÌ SAO CÓ PROBE NÀY — nó KHÔNG phải "kiểm lại unit test":**

    `sal-005` **§18.6** ghi thẳng một điều: *"CHƯA ĐO ĐƯỢC RUNTIME … không có đường nào phát
    `ChannelOrderPulledEto` trong probe ⇒ KHÔNG kết luận gì về hành vi runtime"*. 📌 Đó là **nợ §18.7 #1**,
    và cách sửa đã được ghi rõ: **làm ĐƯỜNG GỌI**, đừng tìm cách đo khác.

    ✅ Probe này **là** đường gọi đó — và **không** thêm endpoint riêng cho dev:
      ① gieo SHOP thật (`ChnChannelShops`) + nhật ký nhận đơn thật vào DB;
      ② khởi động **Host thật** với `ChannelHub:Shopee:PocOrdersPerPull = 1`
         (connector PoC sinh đơn **GIẢ nhưng tất định** — cùng con trỏ ⇒ cùng đơn, cùng `update_time`);
      ③ bấm **đúng nút "Đồng bộ ngay"** (`POST /api/app/channel/{id}/sync-now` — ⚠️ route **THẬT** đọc từ
         `swagger.json`; bản đầu của probe dùng `/api/app/channel/sync-now/{id}` và **nhận `404`** — đúng bẫy
         `README-probe` **S2-1**: *route trong tài liệu ≠ route thật*) — đường mà người vận hành
         thật sự dùng, đi qua `ChannelShopSyncer` ⇒ phát `ChannelOrderPulledEto` ⇒ **handler thật** chạy;
      ④ đo **DB thật**: đơn nội bộ, watermark, nhật ký sự kiện, hàng đợi thư chết (DLQ).

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🚨 **BỐN CÂU HỎI CHỈ ĐƯỜNG CHẠY THẬT TRẢ LỜI ĐƯỢC (mỗi câu là một lỗi đã từng xảy ra trong dự án):**

      ① **Event lỗi ⇒ vào DLQ, KHÔNG mất** — sự kiện đến **muộn** và sự kiện **xung đột cùng mốc** phải
         để lại dòng DLQ **chứa payload gốc** (phát lại được). 📌 Nếu chỉ `LogError` thì log **xoay vòng**
         và *"mất dữ liệu nhưng có vẻ như đã xử lý"*;

      ② **Watermark chạy trên dữ liệu THẬT** — trước `S6`, connector **không** trả `update_time` ⇒
         `AggregateVersion = null` ở **mọi** ETO ⇒ luật 4 nhánh **chưa từng** chạy ở runtime;

      ③ **Gửi lại cùng một đơn ⇒ KHÔNG DLQ giả** — ca này **đo được** một lỗi thật đã sửa ở `S6`:
         hash chống trùng từng băm **cả `CorrelationId`** (đổi mỗi lần kéo) ⇒ mọi lần kéo lại bị coi là
         **xung đột payload** ⇒ DLQ giả + cảnh báo mất tác dụng. 📌 Unit test **không** thấy ca này vì nó
         cần **hai** lần gửi với `CorrelationId` **khác nhau**;

      ④ **Job `D12` chạy** — có neo **mở đầu** *và* **kết thúc** (`[D12-JOB-XONG]`), và cảnh báo cờ
         `GapSuspected` **bật**. ⚠️ Bài học `sal-005` §19.5 #2: *"job chết giữa đường"* và *"job chạy xong"*
         **trông giống hệt nhau** nếu chỉ có neo mở đầu.

    ══════════════════════════════════════════════════════════════════════════════════════════════════
    🔒 **SÁU QUY TẮC ĐƯỢC THI HÀNH (mỗi cái là một bài học đã trả giá):**
      1. **In HTTP STATUS trước body** (`README-probe` **B1**) — `404`/`405` từng bị đọc thành *"rỗng"*;
      2. **NEO ASCII** trong mọi câu khẳng định — console log làm hỏng dấu tiếng Việt (**I4**);
      3. **KHẲNG ĐỊNH TIỀN ĐỀ** trước khi đo: shop/intake đã **có thật** trong DB (**A8**, **I5**);
      4. **DỌN DẸP trong `finally`** + **khẳng định đã sạch** — nếu không, `kiem-db.ps1` đỏ vì lý do vô hình;
      5. **Xoá cả bản ghi chống trùng** (`MsgIdempotencyRecords`) — 📌 để lại thì lần chạy sau rơi vào
         `TraVeKetQuaCu` ⇒ handler **không chạy** ⇒ mọi ca sau **XANH vì lý do SAI**;
      6. 🚨 **`PocOrdersPerPull` + nhịp supervisor đặt bằng BIẾN MÔI TRƯỜNG** cho tiến trình Host ⇒ **không**
         commit cấu hình dev vào `appsettings.json`. 📌 Nhịp supervisor đặt **3600 s** để **không** có nhịp nền
         nào chen vào giữa phép đo (mỗi lần đo chỉ do **một** lời gọi HTTP của probe gây ra).

    📌 **MỌI CÂU SQL NẰM TRONG BIẾN here-string, rồi mới `-f`** — ⚠️ không đặt `-f` ngay sau dấu đóng `'@`
    (bẫy here-string, `handover` §5.1 #3) và **không** ghép SQL bằng `+` (bẫy `sal-006` §25.5).

.PARAMETER TimeoutGiay
    Chờ Host lên (giây).

.EXAMPLE
    pwsh -File tools\probe-don-san.ps1
#>
[CmdletBinding()]
param(
    [int]$TimeoutGiay = 120
)

$ErrorActionPreference = 'Stop'

$goc = Split-Path -Parent $PSScriptRoot
$thuMucTam = Join-Path $PSScriptRoot '_tam-donsan'
New-Item -ItemType Directory -Force -Path $thuMucTam | Out-Null

# 🔑 Nạp thư viện tiêm: file này chỉ có HÀM, không có `param` cấp tệp ⇒ nạp an toàn.
. (Join-Path $PSScriptRoot 'ham-tiem.ps1')

# 🔑 Thư viện "một chỗ" cho các khác biệt Windows/Linux (`XC-007` P-02/P-04/P-05 — `S9` 21/10/2026).
. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$host_ = 'http://localhost:5000'
$tienTrinh = $null
$ketQua = [System.Collections.Generic.List[object]]::new()

# ── GUID trong KHỐI PROBE `e0000000-…` ⇒ `kiem-db.ps1` nhóm ⑥ (tự dò mọi bảng có PK `uuid`) phát hiện được
#    nếu probe gieo mà không dọn. 📌 Đây là dấu hiệu KHÔNG phụ thuộc tên bảng.
$shopA = 'e0000000-0000-0000-0000-00000000a601'   # ĐẾN MUỘN  ⇒ DLQ + không ghi đè
$shopB = 'e0000000-0000-0000-0000-00000000a602'   # XUNG ĐỘT   ⇒ DLQ + không ghi đè
$shopC = 'e0000000-0000-0000-0000-00000000a603'   # ÁP DỤNG   ⇒ tạo đơn + ghi watermark
$shopD = 'e0000000-0000-0000-0000-00000000a604'   # NHẢY MỐC   ⇒ GapSuspected + job D12 thấy

$intakeA = 'e0000000-0000-0000-0000-00000000b601'
$intakeB = 'e0000000-0000-0000-0000-00000000b602'
$intakeD = 'e0000000-0000-0000-0000-00000000b604'

# 🔒 64 hex — KHÔNG phải hash thật (cố ý khác) để phân biệt "hash đã áp" với "hash vừa nhận".
$hashGieoA = ('a' * 64)
$hashGieoB = ('b' * 64)
$hashGieoD = ('d' * 64)

function Kq {
    param([string]$Ten, [bool]$Dat, [string]$ChiTiet = '')

    $ketQua.Add([pscustomobject]@{ Ten = $Ten; Dat = $Dat; ChiTiet = $ChiTiet })
    Write-Output ("   [{0}] {1} | {2}" -f $Ten, $(if ($Dat) { 'DAT' } else { 'KHONG DAT' }), $ChiTiet)
}

function So-Db {
    param([string]$Sql)

    $dong = @(Invoke-Psql -Sql $Sql)
    if ($dong.Count -eq 0) { return -1 }

    return [int](($dong[0]).Trim())
}

function Chuoi-Db {
    param([string]$Sql)

    $dong = @(Invoke-Psql -Sql $Sql)
    if ($dong.Count -eq 0) { return '' }

    return ([string]$dong[0]).Trim()
}

<#
    🔒 **MỐC UTC** — dựng bằng `ParseExact` + `SpecifyKind(Utc)`, KHÔNG dùng `[datetime]::Parse` (nó trả `Local`
    ⇒ `.Ticks` lệch **7 tiếng** so với giá trị Postgres trả về ⇒ `ExternalOrderId` tính ra **khác** mã đơn
    connector sinh ra ⇒ probe đo một đơn **không tồn tại** — đúng bẫy `handover` §5.1 #6).
#>
function Moc-Utc {
    param([string]$Chuoi)

    $tho = [datetime]::ParseExact($Chuoi, 'yyyy-MM-dd HH:mm:ss', [System.Globalization.CultureInfo]::InvariantCulture)
    return [datetime]::SpecifyKind($tho, [System.DateTimeKind]::Utc)
}

<#
    🧮 **TÁI TẠO ĐÚNG MÃ ĐƠN MÀ CONNECTOR PoC SẼ SINH RA** — điều kiện để gieo được nhật ký nhận đơn "đã có"
    cho đúng đơn sắp tới.

    📌 Nguồn sự thật là `ShopeeConnector.TaoDonGia`:
       `ExternalOrderId = "POC-{ExternalShopId}-{ticks(LastSyncedAtUtc)}-{i}"`
       `PlacedAtUtc = LastSyncedAtUtc + i phút` · `UpdatedAtUtc = PlacedAtUtc + 1 phút`
       watermark = **epoch giây** của `UpdatedAtUtc` (`PayloadCanonicalizer.ThanhPhienBan`, làm tròn XUỐNG).
    ⚠️ Nếu ai đổi công thức trong connector mà **không** sửa hàm này thì probe sẽ **ĐỎ** — và đó là **chủ ý**:
    hai nơi lệch nhau thì phép đo phải nói ra, không được im lặng.
#>
function Tinh-Don {
    param([string]$Moc, [string]$TenShop, [int]$ChiSo = 0)

    $thoiDiem = Moc-Utc $Moc
    $ticks = $thoiDiem.Ticks
    $placedAt = $thoiDiem.AddMinutes($ChiSo)
    $updatedAt = $placedAt.AddMinutes(1)

    return [pscustomobject]@{
        Moc             = $Moc
        Ticks           = $ticks
        ExternalOrderId = "POC-$TenShop-$ticks-$ChiSo"
        PlacedAtUtc     = $placedAt.ToString('yyyy-MM-dd HH:mm:ss')
        UpdatedAtUtc    = $updatedAt.ToString('yyyy-MM-dd HH:mm:ss')
        PhienBan        = [long]([System.DateTimeOffset]::new($updatedAt)).ToUnixTimeSeconds()
    }
}

$donA = Tinh-Don -Moc '2026-01-10 00:00:00' -TenShop 'K9S6-A'
$donB = Tinh-Don -Moc '2026-01-11 00:00:00' -TenShop 'K9S6-B'
$donC = Tinh-Don -Moc '2026-01-12 00:00:00' -TenShop 'K9S6-C'
$donD = Tinh-Don -Moc '2026-01-13 00:00:00' -TenShop 'K9S6-D'

<#
    🔒 **DỌN DẸP THEO ĐÚNG THỨ TỰ KHOÁ** — con trước, cha sau.
    📌 Xoá **cả `MsgIdempotencyRecords`**: nếu còn bản ghi chống trùng thì lần chạy sau handler **không chạy**
    (`TraVeKetQuaCu`) ⇒ mọi ca sau XANH vì **lý do SAI** (quy tắc ⑤ ở `.DESCRIPTION`).
    ⚠️ Xoá theo **tiền tố `POC-K9S6-%`** (mã đơn) chứ không theo `Id`: dòng do **handler** tạo có `Id` ngẫu nhiên.
#>
function Xoa-DuLieuProbe {
    $cacCau = @(
        'delete from "OrdOrderLines" where "OrderId" in (select "Id" from "OrdOrders" where "ExternalOrderId" like ''POC-K9S6-%'');'
        'delete from "OrdOrders" where "ExternalOrderId" like ''POC-K9S6-%'';'
        'delete from "SalMarketplaceOrderIntakes" where "ExternalOrderId" like ''POC-K9S6-%'';'
        'delete from "SalMarketplaceEventLogs" where "ExternalOrderId" like ''POC-K9S6-%'';'
        'delete from "MsgDeadLetters" where "KhoaNghiepVu" like ''%POC-K9S6-%'';'
        'delete from "MsgIdempotencyRecords" where "KeyValue" like ''POC-K9S6-%'';'
        'delete from "ChnChannelShops" where "ExternalShopId" like ''K9S6-%'';'
    )

    foreach ($cau in $cacCau) {
        Invoke-Psql -Sql $cau | Out-Null
    }
}

<#
    🚨 **ĐẾM DẤU VẾT CÒN LẠI** trên **MỌI** bảng probe chạm tới. 📌 Một phép dọn "thành công" mà còn rác là
    đúng loại lỗi `README-probe` §D cảnh báo: lần đo sau đọc phải rác rồi kết luận sai.
#>
function Dem-DauVet {
    return [pscustomobject]@{
        Don        = (So-Db 'select count(*)::text from "OrdOrders" where "ExternalOrderId" like ''POC-K9S6-%'';')
        NhatKy     = (So-Db 'select count(*)::text from "SalMarketplaceOrderIntakes" where "ExternalOrderId" like ''POC-K9S6-%'';')
        SuKien     = (So-Db 'select count(*)::text from "SalMarketplaceEventLogs" where "ExternalOrderId" like ''POC-K9S6-%'';')
        Dlq        = (So-Db 'select count(*)::text from "MsgDeadLetters" where "KhoaNghiepVu" like ''%POC-K9S6-%'';')
        ChongTrung = (So-Db 'select count(*)::text from "MsgIdempotencyRecords" where "KeyValue" like ''POC-K9S6-%'';')
        Shop       = (So-Db 'select count(*)::text from "ChnChannelShops" where "ExternalShopId" like ''K9S6-%'';')
    }
}

<#
    ⏳ **CHỜ CÓ ĐIỀU KIỆN** — 📌 sự kiện đi qua `IDistributedEventBus` **in-process** nên handler chạy **sau** khi
    lời gọi HTTP trả về. Không chờ ⇒ đo **trước** khi dữ liệu kịp ghi ⇒ ca ĐỎ vì **lý do thời điểm**.
    ⚠️ Hết hạn ⇒ trả `$false` (KHÔNG ném) để ca kiểm tự báo ĐỎ bằng số đo thật.
#>
function Cho-DieuKien {
    param([scriptblock]$DieuKien, [int]$GiayToiDa = 30)

    $hetHan = (Get-Date).AddSeconds($GiayToiDa)

    while ((Get-Date) -lt $hetHan) {
        if (& $DieuKien) { return $true }
        Start-Sleep -Milliseconds 400
    }

    return $false
}

function Goi-Api {
    param([string]$PhuongThuc, [string]$DuongDan, [string]$Token, [object]$Body = $null)

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
    🔒 **XOÁ CACHE QUYỀN TRƯỚC KHI KHỞI ĐỘNG HOST** (`ADDR-004`/`S2` đo được 18/10/2026, lặp lại ở `S6`):
    ABP cache **kết quả ÂM** (`KHÔNG được cấp`) của `AbpPermissionGrants`. Khi permission **mới** được cấp
    (DbMigrator), bản ghi "denied" cũ vẫn nằm trong Redis ⇒ API mới trả **`403`** dù DB **đã** cấp.
    📌 Và `0 key` là **dấu hiệu MẪU SAI**, không phải *"không có gì để xoá"* (`README-probe` **I1**) ⇒ phải **in** số key.
    ⚠️ **`S6` cũng đo được bẫy liền kề:** thêm permission vào provider **KHÔNG** tự cấp cho `admin` —
    phải chạy **DbMigrator** (seeder `IdentityDataSeedContributor`) thì `AbpPermissionGrants` mới có dòng.
    Thiếu bước đó ⇒ `403` **cho chính admin**, triệu chứng **không trỏ** tới chỗ sai (`XC-004` D2).
#>
function Xoa-CacheQuyen {
    $soKey = 0

    try {
        $dem = & docker exec ecommerce-redis sh -c "redis-cli --scan --pattern '*PermissionGrant*' | wc -l" 2>$null

        if ($null -ne $dem) { $soKey = [int](("$dem").Trim()) }

        & docker exec ecommerce-redis sh -c 'redis-cli --scan --pattern "*PermissionGrant*" | while read k; do redis-cli DEL "$k" > /dev/null; done' 2>$null | Out-Null
    }
    catch {
        # 📌 Không xoá được cache KHÔNG phải lỗi sản phẩm — nhưng phải NÓI RA (không im lặng).
        Write-Output ("   ⚠️ Không xoá được cache quyền trong Redis: {0}" -f $_.Exception.Message)
    }

    return $soKey
}


function Dem-SuKien {
    param([string]$MaDon)

    $mau = @'
select count(*)::text from "SalMarketplaceEventLogs" where "ExternalOrderId" = '{0}';
'@
    return (So-Db ($mau -f $MaDon))
}

function Dem-SuKienTheoKetCuc {
    param([string]$MaDon, [int]$KetCuc)

    $mau = @'
select count(*)::text from "SalMarketplaceEventLogs" where "ExternalOrderId" = '{0}' and "Outcome" = {1};
'@
    return (So-Db ($mau -f $MaDon, $KetCuc))
}

function Lay-Watermark {
    param([string]$MaDon)

    $mau = @'
select coalesce("AppliedSourceVersion", -1)::text from "SalMarketplaceOrderIntakes" where "ExternalOrderId" = '{0}';
'@
    return [long](Chuoi-Db ($mau -f $MaDon))
}

Write-Output ''
Write-Output '════ PROBE DON SAN (SAL-005 · S6) — DUONG CHAY THAT + DB THAT :5000 ════'

try {
    # ── ⓪ DỌN DẤU VẾT CŨ TRƯỚC KHI ĐO ─────────────────────────────────────────────────────────
    Xoa-DuLieuProbe

    $cu = Dem-DauVet
    $tongCu = $cu.Don + $cu.NhatKy + $cu.SuKien + $cu.Dlq + $cu.ChongTrung + $cu.Shop

    Kq 'C01 tien de: khong con du lieu probe cu' ($tongCu -eq 0) (
        'don={0} · nhatky={1} · sukien={2} · dlq={3} · chongtrung={4} · shop={5}' -f `
            $cu.Don, $cu.NhatKy, $cu.SuKien, $cu.Dlq, $cu.ChongTrung, $cu.Shop)

    if ($tongCu -ne 0) {
        throw 'Con du lieu probe cu ⇒ moi phep do sau do co the XANH VI LY DO SAI.'
    }

    # ── ① GIEO SHOP + NHẬT KÝ NHẬN ĐƠN (tiền đề của mọi phép đo) ────────────────────────────────
    $mauShop = @'
insert into "ChnChannelShops"
    ("Id","TenantId","ChannelCode","ExternalShopId","ShopName","LastSyncedAtUtc","IsActive",
     "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values
    ('{0}', null, 'shopee', 'K9S6-A', 'K9S6 shop DEN MUON', '{1}', true, '{{}}', 'k9s6a', now(), false),
    ('{2}', null, 'shopee', 'K9S6-B', 'K9S6 shop XUNG DOT', '{3}', true, '{{}}', 'k9s6b', now(), false),
    ('{4}', null, 'shopee', 'K9S6-C', 'K9S6 shop AP DUNG',  '{5}', true, '{{}}', 'k9s6c', now(), false),
    ('{6}', null, 'shopee', 'K9S6-D', 'K9S6 shop NHAY MOC', '{7}', true, '{{}}', 'k9s6d', now(), false);
'@

    $sqlShop = $mauShop -f $shopA, $donA.Moc, $shopB, $donB.Moc, $shopC, $donC.Moc, $shopD, $donD.Moc

    $kiemShop = @'
select count(*)::text from "ChnChannelShops" where "ExternalShopId" like 'K9S6-%';
'@

    TiemVaoDb -Sql $sqlShop -KiemSql $kiemShop -SoDongMongDoi 4 | Out-Null

    Kq 'C02 da gieo 4 shop (dieu kien de dong bo that)' $true 'K9S6-A/B/C/D'

    # 🚨 Ba nhật ký nhận đơn "ĐÃ CÓ" — mỗi cái cố ý đặt **trạng thái watermark khác nhau** để buộc handler
    # rơi vào **ba nhánh khác nhau** của luật 4 nhánh:
    #   • A: watermark **cao hơn** sự kiện sắp tới   ⇒ `DenMuon` (KHÔNG ghi đè + DLQ);
    #   • B: watermark **bằng** nhưng hash khác      ⇒ `XungDot` (KHÔNG đoán + DLQ);
    #   • D: watermark **thấp hơn 7200 s**           ⇒ `ApDung` + **cờ gap** (job D12 thấy).
    $vA = $donA.PhienBan + 100000
    $vB = $donB.PhienBan
    $vD = $donD.PhienBan - 7200

    $mauIntake = @'
insert into "SalMarketplaceOrderIntakes"
    ("Id","TenantId","ChannelCode","ExternalShopId","ExternalOrderId","OrderId","MarketplacePlacedAtUtc",
     "PayloadHash","ReceivedAtUtc","CorrelationId","SkipReason",
     "AppliedSourceVersion","AppliedSourceUpdatedAtUtc","AppliedPayloadHash","GapSuspected","LastReconciledAtUtc",
     "ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values
    ('{0}', null, 'shopee', 'K9S6-A', '{1}', null, '{2}', '{3}', now(), null, null,
     {4}, '{5}', '{3}', false, null, '{{}}', 'k9s6i1', now(), false),
    ('{6}', null, 'shopee', 'K9S6-B', '{7}', null, '{8}', '{9}', now(), null, null,
     {10}, '{11}', '{9}', false, null, '{{}}', 'k9s6i2', now(), false),
    ('{12}', null, 'shopee', 'K9S6-D', '{13}', null, '{14}', '{15}', now(), null, null,
     {16}, '{17}', '{15}', true, null, '{{}}', 'k9s6i3', now(), false);
'@

    $sqlIntake = $mauIntake -f $intakeA, $donA.ExternalOrderId, $donA.PlacedAtUtc, $hashGieoA, $vA, $donA.UpdatedAtUtc,
        $intakeB, $donB.ExternalOrderId, $donB.PlacedAtUtc, $hashGieoB, $vB, $donB.UpdatedAtUtc,
        $intakeD, $donD.ExternalOrderId, $donD.PlacedAtUtc, $hashGieoD, $vD, $donD.UpdatedAtUtc

    $kiemIntake = @'
select count(*)::text from "SalMarketplaceOrderIntakes" where "ExternalOrderId" like 'POC-K9S6-%';
'@

    TiemVaoDb -Sql $sqlIntake -KiemSql $kiemIntake -SoDongMongDoi 3 | Out-Null

    # 🔒 ĐỌC LẠI **giá trị** (không chỉ số dòng): nếu cột watermark ghi sai thì mọi kết luận nhánh đều sai.
    $mauDemGap = @'
select count(*)::text from "SalMarketplaceOrderIntakes" where "ExternalOrderId" = '{0}' and "GapSuspected";
'@

    $wmGieoA = Lay-Watermark $donA.ExternalOrderId
    $gapGieoD = So-Db ($mauDemGap -f $donD.ExternalOrderId)

    Kq 'C03 da gieo 3 nhat ky nhan don voi watermark CO Y khac nhau' ($wmGieoA -eq $vA -and $gapGieoD -eq 1) `
        ('wmA={0} (mong doi {1}) · gapD={2}' -f $wmGieoA, $vA, $gapGieoD)

    if ($wmGieoA -ne $vA -or $gapGieoD -ne 1) {
        throw 'Tien de SAI (watermark/co gap ghi khong dung) ⇒ cac phep do sau KHONG con nghia.'
    }

    # ── ② KHỞI ĐỘNG HOST (bật connector PoC + giãn nhịp supervisor) ─────────────────────────────
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    # 🔑 Biến môi trường THẮNG `appsettings.json` (mẫu `__` = `:` trong ASP.NET Core).
    #    `PocOrdersPerPull = 1` ⇒ mỗi lần kéo trả **đúng 1** đơn giả (tất định) ⇒ phép đo đếm được.
    #    `SupervisorPeriodSeconds = 3600` ⇒ KHÔNG có nhịp nền nào chen vào giữa phép đo.
    $env:ChannelHub__Shopee__PocOrdersPerPull = '1'
    $env:ChannelHub__SupervisorPeriodSeconds = '3600'

    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
    $logHost = Join-Path $thuMucTam 'host.log'

    Remove-Item $logHost -Force -ErrorAction SilentlyContinue

    foreach ($cong in @(5000)) {
        foreach ($k in @(Lay-PidDangNghe -Cong $cong)) {
            Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
        }
    }

    Start-Sleep -Seconds 1

    # 🔒 Xoá cache quyền **trước** khi Host lên (xem chú thích `Xoa-CacheQuyen`) — 📌 và **in số key**:
    # `0 key` là dấu hiệu **mẫu sai**, không phải "không có gì để xoá".
    $soKeyQuyen = Xoa-CacheQuyen
    Write-Output ("   Xoa cache quyen: {0} key (mau *PermissionGrant*)" -f $soKeyQuyen)

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $logHost -TepLogLoi (Join-Path $thuMucTam 'host.err')

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $host_ -LogFile $logHost `
        -TimeoutSeconds $TimeoutGiay -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    $maChoHost = $LASTEXITCODE

    Kq 'C04 Host len (/health-status = 200)' ($maChoHost -eq 0) ('wait-host exit = {0}' -f $maChoHost)

    if ($maChoHost -ne 0) {
        throw ('Host KHONG len trong {0}s. Xem log: {1}' -f $TimeoutGiay, $logHost)
    }

    # 🚨 PHẢI TẮT CỜ BẢO MẬT trong lúc chạy probe (admin có `ShouldChangePasswordOnNextLogin = true` ⇒
    # OpenIddict từ chối `password` grant). 🔒 BẬT LẠI trong `finally` + khẳng định.
    $moCoBaoMat = @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = 'ADMIN';
'@

    $kiemCoBaoMat = @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and not "ShouldChangePasswordOnNextLogin";
'@

    TiemVaoDb -Sql $moCoBaoMat -KiemSql $kiemCoBaoMat -SoDongMongDoi 1 | Out-Null

    # ── ③ TOKEN ────────────────────────────────────────────────────────────────────────────────
    $thanToken = @{
        grant_type    = 'password'
        client_id     = 'eCommerce_App'
        client_secret = '1q2w3e*'
        username      = 'admin@abp.io'
        password      = '1q2w3E*'
        scope         = 'eCommerce offline_access'
    }

    $jwt = Invoke-RestMethod -Method Post -Uri ($host_ + '/connect/token') -Body $thanToken `
        -ContentType 'application/x-www-form-urlencoded'

    $token = $jwt.access_token
    Kq 'C05 lay token admin (password grant)' (-not [string]::IsNullOrWhiteSpace($token)) `
        ('do dai = {0}' -f ($token | Measure-Object -Character).Characters)

    # ── ④ JOB D12 — neo MỞ ĐẦU **và** KẾT THÚC + cảnh báo cờ gap ───────────────────────────────
    $coNeoMoDau = Cho-DieuKien { Select-String -Path $logHost -Pattern '[D12-JOB-BAT-DAU]' -SimpleMatch -Quiet }
    $coNeoKetThuc = Cho-DieuKien { Select-String -Path $logHost -Pattern '[D12-JOB-XONG]' -SimpleMatch -Quiet }

    Kq 'C06 job D12 CO chay (neo mo dau)' $coNeoMoDau '[D12-JOB-BAT-DAU]'
    Kq 'C07 job D12 chay XONG (neo ket thuc — phan biet "xong" voi "chay roi nem")' $coNeoKetThuc '[D12-JOB-XONG]'

    $dongGap = @(Select-String -Path $logHost -Pattern '[D12-GAP-TON-DONG]' -SimpleMatch -ErrorAction SilentlyContinue)
    $gapInKhoa = @($dongGap | Where-Object { $_.Line.Contains($donD.ExternalOrderId) }).Count -gt 0

    Kq 'C08 job D12 bao co GAP va in KHOA NGHIEP VU cua don' ($dongGap.Count -gt 0 -and $gapInKhoa) `
        ('so dong canh bao = {0} · co ma don = {1}' -f $dongGap.Count, $gapInKhoa)

    # ── ⑤ KỊCH BẢN A — SỰ KIỆN ĐẾN MUỘN ⇒ DLQ (KHÔNG ghi đè) ──────────────────────────────────
    $kqA = Goi-Api -PhuongThuc 'Post' -DuongDan ('/api/app/channel/' + $shopA + '/sync-now') -Token $token

    Kq 'C09 POST sync-now shop A => 200 (route that, dung verb)' ($kqA.Ma -eq 200) `
        ('HTTP {0} · don keo = {1}' -f $kqA.Ma, $kqA.DuLieu.ordersPulled)

    if ($kqA.Ma -ne 200) {
        throw ('Khong dong bo duoc shop A (HTTP {0}) ⇒ cac ca sau VO NGHIA: {1}' -f $kqA.Ma, $kqA.Than)
    }

    Cho-DieuKien { (Dem-SuKien $donA.ExternalOrderId) -ge 1 } | Out-Null

    $tongA = Dem-SuKien $donA.ExternalOrderId
    $lateA = Dem-SuKienTheoKetCuc $donA.ExternalOrderId 2

    Kq 'C10 su kien DEN MUON => nhat ky DUNG 1 dong, Outcome = Late (2)' ($tongA -eq 1 -and $lateA -eq 1) `
        ('tong = {0} · Late = {1}' -f $tongA, $lateA)

    $wmSauA = Lay-Watermark $donA.ExternalOrderId

    Kq 'C11 ban den MUON KHONG ghi de watermark (bat bien trung tam)' ($wmSauA -eq $vA) `
        ('watermark = {0} · truoc = {1}' -f $wmSauA, $vA)

    $mauDemDon = @'
select count(*)::text from "OrdOrders" where "ExternalOrderId" = '{0}';
'@

    $mauDemDlqLyDo = @'
select count(*)::text from "MsgDeadLetters" where "KhoaNghiepVu" like '%{0}%' and "LyDo" = {1};
'@

    $mauDemDlqPayload = @'
select count(*)::text from "MsgDeadLetters" where "KhoaNghiepVu" like '%{0}%' and length("PayloadJson") > 100;
'@

    $donNoiBoA = So-Db ($mauDemDon -f $donA.ExternalOrderId)

    Kq 'C12 ban den MUON KHONG tao don noi bo (khong co tac dung phu)' ($donNoiBoA -eq 0) ('so don = {0}' -f $donNoiBoA)

    $dlqA = So-Db ($mauDemDlqLyDo -f $donA.ExternalOrderId, 1)
    $payloadDayDuA = So-Db ($mauDemDlqPayload -f $donA.ExternalOrderId)

    Kq 'C13 su kien DEN MUON VAO DLQ (LyDo = HaWatermark)' ($dlqA -eq 1) ('so dong DLQ = {0}' -f $dlqA)
    Kq 'C14 DLQ giu PAYLOAD GOC (phat lai duoc ⇒ KHONG mat du lieu)' ($payloadDayDuA -eq 1) `
        ('payload > 100 ky tu = {0}' -f $payloadDayDuA)

    # ── ⑥ KỊCH BẢN B — XUNG ĐỘT CÙNG MỐC ⇒ DLQ (KHÔNG đoán) ────────────────────────────────────
    $kqB = Goi-Api -PhuongThuc 'Post' -DuongDan ('/api/app/channel/' + $shopB + '/sync-now') -Token $token

    Kq 'C15 POST sync-now shop B => 200' ($kqB.Ma -eq 200) ('HTTP {0}' -f $kqB.Ma)

    if ($kqB.Ma -ne 200) {
        throw ('Khong dong bo duoc shop B (HTTP {0}).' -f $kqB.Ma)
    }

    Cho-DieuKien { (Dem-SuKien $donB.ExternalOrderId) -ge 1 } | Out-Null

    $tongB = Dem-SuKien $donB.ExternalOrderId
    $xungDotB = Dem-SuKienTheoKetCuc $donB.ExternalOrderId 3
    $wmSauB = Lay-Watermark $donB.ExternalOrderId
    $dlqB = So-Db ($mauDemDlqLyDo -f $donB.ExternalOrderId, 0)

    Kq 'C16 su kien XUNG DOT CUNG MOC => Outcome = Conflict (3)' ($tongB -eq 1 -and $xungDotB -eq 1) `
        ('tong = {0} · Conflict = {1}' -f $tongB, $xungDotB)
    Kq 'C17 xung dot KHONG ghi de watermark' ($wmSauB -eq $vB) ('watermark = {0} · truoc = {1}' -f $wmSauB, $vB)
    Kq 'C18 xung dot VAO DLQ (LyDo = XungDotPayload)' ($dlqB -eq 1) ('so dong DLQ = {0}' -f $dlqB)

    # ── ⑦ KỊCH BẢN C — ÁP DỤNG: tạo đơn + ghi watermark ────────────────────────────────────────
    $kqC = Goi-Api -PhuongThuc 'Post' -DuongDan ('/api/app/channel/' + $shopC + '/sync-now') -Token $token

    Kq 'C19 POST sync-now shop C => 200' ($kqC.Ma -eq 200) ('HTTP {0}' -f $kqC.Ma)

    if ($kqC.Ma -ne 200) {
        throw ('Khong dong bo duoc shop C (HTTP {0}).' -f $kqC.Ma)
    }

    Cho-DieuKien { (So-Db ($mauDemDon -f $donC.ExternalOrderId)) -ge 1 } | Out-Null

    $donNoiBoC = So-Db ($mauDemDon -f $donC.ExternalOrderId)

    $mauDemNhatKy = @'
select count(*)::text from "SalMarketplaceOrderIntakes" where "ExternalOrderId" = '{0}';
'@

    $mauLayHash = @'
select coalesce("AppliedPayloadHash", '(rong)') from "SalMarketplaceOrderIntakes" where "ExternalOrderId" = '{0}';
'@

    $nhC = So-Db ($mauDemNhatKy -f $donC.ExternalOrderId)
    $wmC = Lay-Watermark $donC.ExternalOrderId
    $appliedC = Dem-SuKienTheoKetCuc $donC.ExternalOrderId 0
    $hashC = Chuoi-Db ($mauLayHash -f $donC.ExternalOrderId)

    Kq 'C20 su kien AP DUNG => tao DUNG 1 don + 1 nhat ky' ($donNoiBoC -eq 1 -and $nhC -eq 1) `
        ('don = {0} · nhat ky = {1}' -f $donNoiBoC, $nhC)

    # 🔑 **ĐÂY LÀ CA CHỨNG MINH `update_time` ĐÃ ĐI QUA ĐƯỢC**: `AppliedSourceVersion` **không** còn `null`.
    # 📌 Trước `S6`, connector không trả `update_time` ⇒ cột này `null` ở **mọi** đơn ⇒ watermark vô hiệu.
    Kq 'C21 WATERMARK duoc ghi tu `update_time` cua san (KHAC null)' ($wmC -eq $donC.PhienBan) `
        ('wm = {0} · epoch(update_time) = {1}' -f $wmC, $donC.PhienBan)

    Kq 'C22 hash cua ban da ap = 64 hex thuong (khong rong)' ($hashC -match '^[0-9a-f]{64}$') `
        ('hash = {0}' -f $hashC.Substring(0, [Math]::Min(8, $hashC.Length)))

    Kq 'C23 nhat ky su kien Outcome = Applied (0)' ($appliedC -eq 1) ('so dong = {0}' -f $appliedC)

    # ── ⑧ KỊCH BẢN D — NHẢY MỐC ⇒ vẫn áp dụng + cờ GapSuspected ────────────────────────────────
    $kqD = Goi-Api -PhuongThuc 'Post' -DuongDan ('/api/app/channel/' + $shopD + '/sync-now') -Token $token

    Kq 'C24 POST sync-now shop D => 200' ($kqD.Ma -eq 200) ('HTTP {0}' -f $kqD.Ma)

    if ($kqD.Ma -ne 200) {
        throw ('Khong dong bo duoc shop D (HTTP {0}).' -f $kqD.Ma)
    }

    Cho-DieuKien { (Dem-SuKien $donD.ExternalOrderId) -ge 1 } | Out-Null

    $wmD = Lay-Watermark $donD.ExternalOrderId
    $gapD = So-Db ($mauDemGap -f $donD.ExternalOrderId)
    $outcomeGapD = Dem-SuKienTheoKetCuc $donD.ExternalOrderId 4

    Kq 'C25 NHAY MOC van AP DUNG (ban moi hon la dung)' ($wmD -eq $donD.PhienBan) `
        ('wm = {0} · epoch = {1}' -f $wmD, $donD.PhienBan)
    Kq 'C26 co GAP van BAT co GapSuspected (dua don vao pham vi job D12)' ($gapD -eq 1) ('co = {0}' -f $gapD)
    Kq 'C27 nhat ky su kien Outcome = GapSuspected (4)' ($outcomeGapD -eq 1) ('so dong = {0}' -f $outcomeGapD)

    # ── ⑨ 🚨 GỬI LẠI CÙNG MỘT ĐƠN — KHÔNG DLQ GIẢ (ca đo được lỗi hash đã sửa ở `S6`) ──────────
    # 📌 Đặt con trỏ đồng bộ của shop C về **đúng** mốc cũ ⇒ connector sinh **đúng** đơn đó, **đúng** nội dung,
    # nhưng `ChannelShopSyncer` sinh `CorrelationId` **MỚI** cho lần kéo này.
    $mauDatConTro = @'
update "ChnChannelShops" set "LastSyncedAtUtc" = '{0}' where "Id" = '{1}';
'@

    Invoke-Psql -Sql ($mauDatConTro -f $donC.Moc, $shopC) | Out-Null

    $wmTruocGuiLai = Lay-Watermark $donC.ExternalOrderId

    $mauDemDlqTong = @'
select count(*)::text from "MsgDeadLetters" where "KhoaNghiepVu" like '%POC-K9S6-%';
'@

    $dlqTruocGuiLai = So-Db $mauDemDlqTong

    $kqC2 = Goi-Api -PhuongThuc 'Post' -DuongDan ('/api/app/channel/' + $shopC + '/sync-now') -Token $token

    Kq 'C28 POST sync-now shop C LAN 2 (san gui lai) => 200' ($kqC2.Ma -eq 200) ('HTTP {0}' -f $kqC2.Ma)

    Cho-DieuKien { (Dem-SuKienTheoKetCuc $donC.ExternalOrderId 1) -ge 1 } | Out-Null

    $donSauGuiLai = So-Db ($mauDemDon -f $donC.ExternalOrderId)
    $nhSauGuiLai = So-Db ($mauDemNhatKy -f $donC.ExternalOrderId)
    $dupC = Dem-SuKienTheoKetCuc $donC.ExternalOrderId 1
    $wmSauGuiLai = Lay-Watermark $donC.ExternalOrderId
    $dlqSauGuiLai = So-Db $mauDemDlqTong

    Kq 'C29 gui lai KHONG tao don thu hai' ($donSauGuiLai -eq 1) ('so don = {0}' -f $donSauGuiLai)
    Kq 'C30 gui lai KHONG tao nhat ky nhan thu hai' ($nhSauGuiLai -eq 1) ('so nhat ky = {0}' -f $nhSauGuiLai)
    Kq 'C31 gui lai CO VET: nhat ky su kien Outcome = Duplicate (1)' ($dupC -eq 1) ('so dong = {0}' -f $dupC)
    Kq 'C32 gui lai KHONG lam DLQ tang (khong con DLQ GIA)' ($dlqSauGuiLai -eq $dlqTruocGuiLai) `
        ('DLQ truoc = {0} · sau = {1}' -f $dlqTruocGuiLai, $dlqSauGuiLai)
    Kq 'C33 gui lai KHONG doi watermark' ($wmSauGuiLai -eq $wmTruocGuiLai) `
        ('wm = {0} · truoc = {1}' -f $wmSauGuiLai, $wmTruocGuiLai)

    # ── ⑩ BẤT BIẾN "MỖI LẦN NHẬN ĐÚNG MỘT DÒNG" + NEO LOG CỦA ĐƯỜNG GHI ────────────────────────
    # 📌 5 lần nhận: A(muộn) · B(xung đột) · C(lần đầu) · D(nhảy mốc) · C(gửi lại) ⇒ **5 dòng**, không hơn.
    $mauDemTongSuKien = @'
select count(*)::text from "SalMarketplaceEventLogs" where "ExternalOrderId" like 'POC-K9S6-%';
'@

    $mauDemCapTrung = @'
select count(*)::text from (
    select "ExternalOrderId", "SourceVersion", "PayloadHash", "Outcome", count(*) as so
    from "SalMarketplaceEventLogs" where "ExternalOrderId" like 'POC-K9S6-%'
    group by 1,2,3,4 having count(*) > 1
) as trung;
'@

    $tongSuKien = So-Db $mauDemTongSuKien
    $soCapTrung = So-Db $mauDemCapTrung

    Kq 'C34 5 lan nhan => DUNG 5 dong nhat ky (khong mat, khong trung)' ($tongSuKien -eq 5 -and $soCapTrung -eq 0) `
        ('tong dong = {0} · cap trung = {1}' -f $tongSuKien, $soCapTrung)

    $coNeoGhi = Select-String -Path $logHost -Pattern '[MP-EVENTLOG-DA-GHI]' -SimpleMatch -Quiet
    $coNeoDlq = Select-String -Path $logHost -Pattern '[DLQ-DA-DAY]' -SimpleMatch -Quiet
    $coNeoWm = Select-String -Path $logHost -Pattern '[WM-DA-AP]' -SimpleMatch -Quiet

    Kq 'C35 log co neo duong ghi nhat ky su kien' $coNeoGhi '[MP-EVENTLOG-DA-GHI]'
    Kq 'C36 log co neo day DLQ' $coNeoDlq '[DLQ-DA-DAY]'
    Kq 'C37 log co neo ap watermark' $coNeoWm '[WM-DA-AP]'

    # ── ⑪ 🚨 CỔNG VẬN HÀNH: ĐỌC WATERMARK + **HẠ CỜ** `GapSuspected` (`SAL-005` §21) ─────────────
    # 🔑 **VÌ SAO PHẢI ĐO Ở ĐÂY:** job `D12` phát hiện cờ nhưng **cố ý không tự hạ** (§19.2). Trước `S6`,
    # `DanhDauDaDoiSoat` **không có người gọi nào** ⇒ cờ chỉ hạ được bằng **SQL tay** ⇒ vế *"để cờ không tồn
    # đọng"* của `S6` mới đúng **một nửa**. Ba câu hỏi chỉ HTTP thật trả lời được:
    #   ① route + verb + **permission mới** có thật không (thiếu định nghĩa ⇒ `403` cho MỌI người kể cả admin);
    #   ② **thiếu lý do ⇒ bị chặn** và cờ **KHÔNG** đổi (đo **cả hai chiều**);
    #   ③ hạ cờ **KHÔNG** được đụng **watermark** (BR-008: watermark chỉ tiến qua luật 4 nhánh).
    $duongIntake = '/api/app/marketplace-intake/intake?ChannelCode=shopee&ExternalOrderId='
    $duongDoiSoat = '/api/app/marketplace-intake/mark-reconciled'

    $kqKhongToken = Goi-Api -PhuongThuc 'Get' -DuongDan ($duongIntake + $donD.ExternalOrderId) -Token ''

    Kq 'C38 GET intake KHONG token => 401 (cong duoc bao ve)' ($kqKhongToken.Ma -eq 401) `
        ('HTTP {0}' -f $kqKhongToken.Ma)

    $kqGetD = Goi-Api -PhuongThuc 'Get' -DuongDan ($duongIntake + $donD.ExternalOrderId) -Token $token

    $gapApi = $kqGetD.DuLieu.gapSuspected
    $wmApi = $kqGetD.DuLieu.appliedSourceVersion

    Kq 'C39 GET intake => 200 + tra DUNG co gap va watermark' `
        ($kqGetD.Ma -eq 200 -and $gapApi -eq $true -and [long]$wmApi -eq $donD.PhienBan) `
        ('HTTP {0} · gap={1} · wm={2} (mong doi {3})' -f $kqGetD.Ma, $gapApi, $wmApi, $donD.PhienBan)

    # 🚨 **THIẾU LÝ DO ⇒ bị chặn.** ⚠️ ABP biến `BusinessException` thành **`403`** (không phải `400`) —
    # bài học `S2-3`; 📌 nếu probe kỳ vọng `400` thì nó sẽ **KHÔNG BAO GIỜ** xanh, hoặc tệ hơn: ai đó "sửa"
    # sản phẩm cho ra `400` để chiều phép đo.
    $kqThieuLyDo = Goi-Api -PhuongThuc 'Post' -DuongDan $duongDoiSoat -Token $token `
        -Body @{ ChannelCode = 'shopee'; ExternalOrderId = $donD.ExternalOrderId; Reason = '   ' }

    $maLoiThieuLyDo = $kqThieuLyDo.Than -match 'eCommerce:Marketplace:0005'
    $gapSauThieuLyDo = So-Db ($mauDemGap -f $donD.ExternalOrderId)

    Kq 'C40 🚨 ha co THIEU ly do => 403 + ma 0005' ($kqThieuLyDo.Ma -eq 403 -and $maLoiThieuLyDo) `
        ('HTTP {0} · co ma 0005 = {1}' -f $kqThieuLyDo.Ma, $maLoiThieuLyDo)

    Kq 'C41 🚨 thieu ly do thi co VAN BAT (khong ha trom)' ($gapSauThieuLyDo -eq 1) ('co = {0}' -f $gapSauThieuLyDo)

    $kqDoiSoat = Goi-Api -PhuongThuc 'Post' -DuongDan $duongDoiSoat -Token $token `
        -Body @{
            ChannelCode     = 'shopee'
            ExternalOrderId = $donD.ExternalOrderId
            Reason          = 'K9S6 probe: da doi chieu don nay voi san, khop'
        }

    Kq 'C42 POST mark-reconciled (co ly do) => 200 + gap = false' `
        ($kqDoiSoat.Ma -eq 200 -and $kqDoiSoat.DuLieu.gapSuspected -eq $false) `
        ('HTTP {0} · gap={1}' -f $kqDoiSoat.Ma, $kqDoiSoat.DuLieu.gapSuspected)

    $wmSauDoiSoat = Lay-Watermark $donD.ExternalOrderId
    $gapSauDoiSoat = So-Db ($mauDemGap -f $donD.ExternalOrderId)
    $coMocDoiSoat = So-Db (@'
select count(*)::text from "SalMarketplaceOrderIntakes" where "ExternalOrderId" = '{0}' and "LastReconciledAtUtc" is not null;
'@ -f $donD.ExternalOrderId)

    Kq 'C43 🚨 ha co KHONG dung toi WATERMARK (BR-008)' ($wmSauDoiSoat -eq $donD.PhienBan) `
        ('wm = {0} · truoc = {1}' -f $wmSauDoiSoat, $donD.PhienBan)
    Kq 'C44 ha co that: co = false + co moc doi soat (audit)' ($gapSauDoiSoat -eq 0 -and $coMocDoiSoat -eq 1) `
        ('co = {0} · moc doi soat = {1}' -f $gapSauDoiSoat, $coMocDoiSoat)

    $kqGetLai = Goi-Api -PhuongThuc 'Get' -DuongDan ($duongIntake + $donD.ExternalOrderId) -Token $token

    Kq 'C45 GET lai => 200 + gap = false (da luu that)' `
        ($kqGetLai.Ma -eq 200 -and $kqGetLai.DuLieu.gapSuspected -eq $false) `
        ('HTTP {0} · gap={1} · moc doi soat={2}' -f $kqGetLai.Ma, $kqGetLai.DuLieu.gapSuspected, $kqGetLai.DuLieu.lastReconciledAtUtc)

    # 📌 `404` cho đơn **chưa từng** được nhận — *"không có"* KHÁC *"có và đang chờ đối soát"*.
    $kqKhongCo = Goi-Api -PhuongThuc 'Get' -DuongDan ($duongIntake + 'K9S6-KHONG-CO-DON-NAY') -Token $token

    Kq 'C46 GET don chua tung nhan => 404 (khong nhao 200 + rong)' ($kqKhongCo.Ma -eq 404) `
        ('HTTP {0}' -f $kqKhongCo.Ma)

    $coNeoDoiSoat = Select-String -Path $logHost -Pattern '[MP-DA-DOI-SOAT]' -SimpleMatch -Quiet

    Kq 'C47 log co neo doi soat (duong ha co chay that)' $coNeoDoiSoat '[MP-DA-DOI-SOAT]'
}
finally {
    # ── DỌN DẸP — kể cả khi một ca trên đã ném ────────────────────────────────────────────────
    if ($null -ne $tienTrinh -and -not $tienTrinh.HasExited) {
        Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue
        $tienTrinh.WaitForExit(10000) | Out-Null
    }

    foreach ($cong in @(5000)) {
        foreach ($k in @(Lay-PidDangNghe -Cong $cong)) {
            Stop-Process -Id $k -Force -ErrorAction SilentlyContinue
        }
    }

    Xoa-DuLieuProbe

    # 🔒 BẮT BUỘC bật lại cờ bảo mật — nếu quên thì ta **commit một tài khoản admin không bị buộc đổi mật khẩu**
    # (`kiem-db.ps1` nhóm ② sẽ đỏ, nhưng đừng để phải nhờ tới nó).
    $batLaiCoBaoMat = @'
update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = 'ADMIN';
'@

    Invoke-Psql -Sql $batLaiCoBaoMat | Out-Null

    $sau = Dem-DauVet
    $tongSau = $sau.Don + $sau.NhatKy + $sau.SuKien + $sau.Dlq + $sau.ChongTrung + $sau.Shop

    $mauDemCoBaoMat = @'
select count(*)::text from "AbpUsers" where "NormalizedUserName" = 'ADMIN' and "ShouldChangePasswordOnNextLogin";
'@

    $coBaoMat = So-Db $mauDemCoBaoMat

    Write-Output ''
    Write-Output ('   DON DEP: host da dung · don={0} · nhatky={1} · sukien={2} · dlq={3} · chongtrung={4} · shop={5} · co bao mat={6}' -f `
            $sau.Don, $sau.NhatKy, $sau.SuKien, $sau.Dlq, $sau.ChongTrung, $sau.Shop, $coBaoMat)

    Kq 'C48 DON DEP: khong con du lieu probe' ($tongSau -eq 0) (
        'don={0} · nhatky={1} · sukien={2} · dlq={3} · chongtrung={4} · shop={5}' -f `
            $sau.Don, $sau.NhatKy, $sau.SuKien, $sau.Dlq, $sau.ChongTrung, $sau.Shop)

    Kq 'C49 DON DEP: da BAT LAI co ShouldChangePasswordOnNextLogin' ($coBaoMat -eq 1) ('co = {0}' -f $coBaoMat)

    # 📌 Xoá biến môi trường của probe để không ảnh hưởng tiến trình khác trong cùng phiên PowerShell.
    Remove-Item Env:ChannelHub__Shopee__PocOrdersPerPull -ErrorAction SilentlyContinue
    Remove-Item Env:ChannelHub__SupervisorPeriodSeconds -ErrorAction SilentlyContinue

    $soDat = @($ketQua | Where-Object { $_.Dat }).Count
    $soHong = $ketQua.Count - $soDat

    Write-Output ''
    Write-Output ('════ PROBE DON SAN: {0}/{1} DAT ════' -f $soDat, $ketQua.Count)

    if ($soHong -gt 0) {
        Write-Output ''
        Write-Output 'CAC CA KHONG DAT:'
        foreach ($k in ($ketQua | Where-Object { -not $_.Dat })) {
            Write-Output ('   KHONG DAT [{0}] {1}' -f $k.Ten, $k.ChiTiet)
        }
    }

    Write-Output ('Log Host: {0}' -f (Join-Path $thuMucTam 'host.log'))

    exit $(if ($soHong -gt 0) { 1 } else { 0 })
}
