<#
.SYNOPSIS
    Probe PHÁT LẠI DLQ (`S16b`) — "replay" = **publish lại event gốc lên BUS**, không phải đánh dấu.

.DESCRIPTION
    🚨 VÌ SAO CẦN PROBE RIÊNG (không phải chỉ unit test):
      Unit test dùng `IDistributedEventBus` GIẢ ⇒ nó chứng minh **lời gọi**, KHÔNG chứng minh **event đã đi**.
      Chuỗi thật dài hơn: `PhatLaiAsync` → ghi **outbox** (cùng transaction với dấu `ReplayedAtUtc`) →
      `OutboxSender` relay (2 s) → RabbitMQ → consumer → **`AbpEventInbox` có dòng**. Chỉ khi thấy dòng inbox
      mới được nói *"đã phát lại"*. 📌 Đây đúng là bài học `XC-005` §19: *"test xanh" ≠ "có chạy thật"*.

    CÁCH ĐO (một chiều có sự kiện + một chiều KHÔNG):
      ① Gieo 1 dòng `MsgDeadLetters` với `TenSuKien` BIẾT TRƯỚC (`OrderStatusChangedEto`) + payload thật.
      ② POST replay qua HTTP ⇒ `200`, `DaPhatLai = true`, DB có `ReplayedAtUtc`/`ReplayCount = 1`/`DaXuLy`.
      ③ Đo **đường đi thật**: `AbpEventInbox` có dòng MỚI cho event đó ⇒ event đã qua outbox + relay + consumer.
      ④ POST lần HAI ⇒ `200` + `DaPhatLai = false` + lý do "BỎ QUA"; **không** có dòng inbox thứ hai;
         `ReplayCount` **vẫn 1** ⇒ idempotent thật (không chỉ là cờ trong RAM).
      ⑤ Dọn sạch + khẳng định đã sạch (đếm lại = 0).

    ⚠️ GIỚI HẠN: probe dùng **một** Host; không đo ca 2 instance (đã có `probe-outbox-2instance.ps1`).
#>
[CmdletBinding()]
param(
    [int]$Cong = 5000
)

$ErrorActionPreference = 'Stop'
$goc = Split-Path -Parent $PSScriptRoot
$host_ = "http://localhost:$Cong"

# 🔒 GUID nằm trong KHỐI DÀNH RIÊNG của probe (`e0000000-…-fd`) ⇒ `kiem-db.ps1` nhóm ⑥ tự dò được nếu dọn hụt.
$idDlq = 'e0000000-0000-0000-0000-0000000000fd'
$khoaDlq = 'K9S16B:order:1'

. (Join-Path $PSScriptRoot 'nen-tang.ps1')

$script:soDat = 0
$script:soHong = 0

function Kq([string]$ten, [bool]$dat, [string]$chiTiet = '') {
    if ($dat) { $script:soDat++; Write-Host ("      [{0}] DAT | {1}" -f $ten, $chiTiet) }
    else { $script:soHong++; Write-Host ("      [{0}] HONG | {1}" -f $ten, $chiTiet) }
}

function Psql([string]$sql) {
    $r = docker exec ecommerce-postgres psql -U ecommerce -d ecommerce -t -A -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "psql loi: $r" }
    return ($r | Out-String).Trim()
}

function DemDlq { return [int](Psql ("select count(*) from ""MsgDeadLetters"" where ""KhoaNghiepVu"" = '{0}';" -f $khoaDlq)) }
function DemOutbox { return [int](Psql 'select count(*) from "AbpEventOutbox";') }
function DemInboxTheoMoc([string]$moc) {
    return [int](Psql ("select count(*) from ""AbpEventInbox"" where ""EventName"" like '%OrderStatusChanged%' and ""CreationTime"" > '{0}';" -f $moc))
}

function DonDep {
    # 🚨 Dọn bằng SQL HERE-STRING — không thoát nháy (bài học `README-probe.md` §25.5).
    Psql @'
delete from "MsgDeadLetters" where "KhoaNghiepVu" like 'K9S16B%';
'@ | Out-Null
    Psql @'
delete from "AbpEventInbox" where "EventName" like '%OrderStatusChanged%';
'@ | Out-Null
    Psql @'
delete from "AbpEventOutbox" where "EventName" like '%OrderStatusChanged%';
'@ | Out-Null
}

function BatHost {
    $env:AbpEventBusBoxes__PeriodSeconds = '2'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    $binHost = Lay-DuongDanBin -TenDuAn 'eCommerce.HttpApi.Host'
    $logHost = Join-Path $goc '_probe-dlq-replay-host.log'

    foreach ($k in @(Lay-PidDangNghe -Cong $Cong)) { Stop-Process -Id $k -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 1

    $tienTrinh = Start-TienTrinhAn -Tep 'dotnet' -ThamSo 'eCommerce.HttpApi.Host.dll' -ThuMucLamViec $binHost `
        -TepLogRa $logHost -TepLogLoi (Join-Path $goc '_probe-dlq-replay-host.err')

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wait-host.ps1') -Url $host_ -LogFile $logHost `
        -TimeoutSeconds 120 -ProcessId $tienTrinh.Id | Select-Object -Last 1 | ForEach-Object { Write-Output ("   $_") }

    if ($LASTEXITCODE -ne 0) { throw 'Host khong len' }

    return $tienTrinh
}

function TatHost($tienTrinh) {
    try { Stop-Process -Id $tienTrinh.Id -Force -ErrorAction SilentlyContinue } catch { }
    foreach ($k in @(Lay-PidDangNghe -Cong $Cong)) { Stop-Process -Id $k -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 1
}

Write-Host '════ PROBE DLQ REPLAY (S16b — "phat lai" = publish lai event len bus) ════'

$tienTrinh = $null

try {
    DonDep
    Kq 'D00 tien de: khong con dong DLQ probe' ((DemDlq) -eq 0) ("dlq = " + (DemDlq))
    Kq 'D01 tien de: outbox rong' ((DemOutbox) -eq 0) ("outbox = " + (DemOutbox))

    # ── ① GIEO 1 dòng DLQ với loại sự kiện BIẾT TRƯỚC ─────────────────────────────────────────
    Psql @'
insert into "MsgDeadLetters"
 ("Id","TenantId","TenSuKien","KhoaNghiepVu","PayloadJson","PayloadHash","LyDo","MoTaLyDo","Loi",
  "VaoDlqLucUtc","CorrelationId","DaXuLy","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values
 ('e0000000-0000-0000-0000-0000000000fd', null, 'OrderStatusChangedEto', 'K9S16B:order:1',
  '{"OrderId":"e0000000-0000-0000-0000-0000000000fc","TenantId":null,"Number":"K9S16B-001","Axis":"Fulfillment","FromStatus":0,"ToStatus":1,"VersionAfter":1,"ActorType":"Staff","ActorId":null,"ReasonCode":"ProbeReplay","CorrelationId":"k9s16b","EventId":"e0000000-0000-0000-0000-0000000000fb","OccurredAtUtc":"2026-10-29T03:00:00Z"}',
  repeat('a', 64), 0, 'probe S16b — dong DLQ gia de do phat lai', null,
  now(), 'k9s16b', false, '{}', 'stamp-s16b', now(), false);
'@ | Out-Null

    # 🔑 ĐỌC LẠI điều kiện tiên quyết (nếu gieo hỏng, mọi ca sau VÔ NGHĨA).
    Kq 'D02 gieo 1 dong DLQ' ((DemDlq) -eq 1) ("dlq = " + (DemDlq))
    Kq 'D03 dong DLQ CHUA phat lai (ReplayCount=0, ReplayedAtUtc NULL)' `
        ((Psql ("select (""ReplayCount""::text || '|' || coalesce(""ReplayedAtUtc""::text,'NULL')) from ""MsgDeadLetters"" where ""Id"" = '{0}';" -f $idDlq)) -eq '0|NULL') `
        (Psql ("select (""ReplayCount""::text || '|' || coalesce(""ReplayedAtUtc""::text,'NULL')) from ""MsgDeadLetters"" where ""Id"" = '{0}';" -f $idDlq))

    # ── ② HOST + TOKEN ────────────────────────────────────────────────────────────────────────
    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = false where "NormalizedUserName" = ''ADMIN'';' | Out-Null

    $tienTrinh = BatHost
    Kq 'D04 Host len (relay 2 s)' $true 'wait-host exit = 0'

    # 🔑 A6 — IN ROUTE THẬT từ swagger TRƯỚC khi gọi (lỗi sai route đã tái phạm 3 lần trong dự án).
    $sw = Invoke-RestMethod -Uri "$host_/swagger/v1/swagger.json" -TimeoutSec 30
    $route = $sw.paths.PSObject.Properties | Where-Object { $_.Name -match 'dead-letter' -and $_.Name -match 'phat-lai' } |
        Select-Object -First 1 -ExpandProperty Name

    Kq 'D05 route phat-lai TON TAI trong swagger' ($null -ne $route) ("route = " + $route)

    $than = @{ grant_type='password'; client_id='eCommerce_App'; client_secret='1q2w3e*'; username='admin@abp.io'; password='1q2w3E*'; scope='eCommerce' }
    $jwt = Invoke-RestMethod -Method Post -Uri "$host_/connect/token" -Body $than -ContentType 'application/x-www-form-urlencoded'
    $h = @{ Authorization = "Bearer $($jwt.access_token)"; 'Content-Type' = 'application/json' }

    $urlPhatLai = "$host_$($route -replace '\{id\}', $idDlq)"

    # ── ③ PHÁT LẠI LẦN ĐẦU ─────────────────────────────────────────────────────────────────────
    # ⏱️ MỐC: chỉ đếm dòng inbox MỚI (dọn nền có thể xoá dòng cũ).
    $mocInbox = Psql 'select now();'

    $resp = Invoke-WebRequest -Method Post -Uri $urlPhatLai -Headers $h -SkipHttpErrorCheck
    Write-Host ("      HTTP STATUS (lan 1) = {0}" -f $resp.StatusCode)   # 🔑 B1: in STATUS TRƯỚC body
    $kq1 = $resp.Content | ConvertFrom-Json

    Kq 'D06 POST phat-lai lan 1 = 200' ($resp.StatusCode -eq 200) ("status = " + $resp.StatusCode)

    # ⚠️ Chấp nhận CẢ hai kiểu đặt tên: ABP mặc định camelCase, nhưng một cấu hình khác có thể trả PascalCase.
    # 📌 Chịu được cả hai KHÔNG làm phép đo yếu đi — ca "sai" vẫn là `DaPhatLai` KHÔNG phải true.
    $da1 = if ($null -ne $kq1.daPhatLai) { $kq1.daPhatLai } else { $kq1.DaPhatLai }
    Kq 'D07 ket qua DaPhatLai = true' ($da1 -eq $true) ("daPhatLai = " + $da1)

    # ── ④ ĐO DB: dấu phát lại ─────────────────────────────────────────────────────────────────
    $dau = Psql ("select (""ReplayCount""::text || '|' || (""ReplayedAtUtc"" is not null)::text || '|' || ""DaXuLy""::text || '|' || coalesce(""ReplayedBy"",'NULL')) from ""MsgDeadLetters"" where ""Id"" = '{0}';" -f $idDlq)
    Kq 'D08 DB: ReplayCount=1 · ReplayedAtUtc CO · DaXuLy=true · ReplayedBy co ten' `
        (($dau -split '\|')[0] -eq '1' -and ($dau -split '\|')[1] -eq 'true' -and ($dau -split '\|')[2] -eq 'true' -and ($dau -split '\|')[3] -ne 'NULL') `
        ("dau = " + $dau)

    # ── ⑤ ĐO ĐƯỜNG ĐI THẬT: outbox → relay → consumer ────────────────────────────────────────
    $daRa = $false
    for ($i = 0; $i -lt 40; $i++) {
        if ((DemInboxTheoMoc $mocInbox) -ge 1) { $daRa = $true; break }
        Start-Sleep -Milliseconds 500
    }

    $soInbox = DemInboxTheoMoc $mocInbox
    Kq 'D09 event DA DI THAT (consumer ghi AbpEventInbox)' $daRa ("inbox moi = $soInbox · moc = $mocInbox")

    $outboxCon = $false
    for ($i = 0; $i -lt 20; $i++) {
        if ((DemOutbox) -eq 0) { $outboxCon = $true; break }
        Start-Sleep -Milliseconds 500
    }

    Kq 'D10 outbox duoc relay don sach (event khong bi treo)' $outboxCon ("outbox = " + (DemOutbox))

    # ── ⑥ GỌI LẦN HAI ⇒ IDEMPOTENT ────────────────────────────────────────────────────────────
    $mocLan2 = Psql 'select now();'

    $resp2 = Invoke-WebRequest -Method Post -Uri $urlPhatLai -Headers $h -SkipHttpErrorCheck
    Write-Host ("      HTTP STATUS (lan 2) = {0}" -f $resp2.StatusCode)
    $kq2 = $resp2.Content | ConvertFrom-Json

    $da2 = if ($null -ne $kq2.daPhatLai) { $kq2.daPhatLai } else { $kq2.DaPhatLai }
    $lyDo2 = if ($null -ne $kq2.lyDo) { $kq2.lyDo } else { $kq2.LyDo }

    Kq 'D11 POST lan 2 = 200 (khong nem loi)' ($resp2.StatusCode -eq 200) ("status = " + $resp2.StatusCode)
    Kq 'D12 lan 2 DaPhatLai = false' ($da2 -eq $false) ("daPhatLai = " + $da2)
    Kq 'D13 lan 2 tra LY DO "BO QUA"' (([string]$lyDo2) -match 'BỎ QUA') ("lyDo = " + $lyDo2)

    Start-Sleep -Seconds 3   # đủ để relay phát NẾU có event thứ hai (2 s/nhịp)
    $soInboxLan2 = DemInboxTheoMoc $mocLan2
    Kq 'D14 KHONG co event thu hai (inbox moi sau lan 2 = 0)' ($soInboxLan2 -eq 0) ("inbox moi = $soInboxLan2")

    $dau2 = Psql ("select ""ReplayCount""::text from ""MsgDeadLetters"" where ""Id"" = '{0}';" -f $idDlq)
    Kq 'D15 DB: ReplayCount VAN = 1 (dau khong bi ghi de)' ($dau2 -eq '1') ("ReplayCount = $dau2")

    # ── ⑦ FAIL-CLOSED qua HTTP: loại sự kiện KHÔNG ánh xạ được ───────────────────────────────
    # 🚨 Bất biến: KHÔNG được publish một event "đoán". Đo **hai** thứ: HTTP status (409) + KHÔNG có event đi.
    $idXau = 'e0000000-0000-0000-0000-0000000000fe'

    Psql @'
insert into "MsgDeadLetters"
 ("Id","TenantId","TenSuKien","KhoaNghiepVu","PayloadJson","PayloadHash","LyDo","MoTaLyDo","Loi",
  "VaoDlqLucUtc","CorrelationId","DaXuLy","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
values
 ('e0000000-0000-0000-0000-0000000000fe', null, 'LoaiSuKienKhongTonTai', 'K9S16B:order:2',
  '{"x":1}', repeat('a', 64), 0, 'probe S16b — fail-closed (loai su kien khong anh xa duoc)', null,
  now(), 'k9s16b', false, '{}', 'stamp-s16b', now(), false);
'@ | Out-Null

    Kq 'D17 gieo dong DLQ voi loai KHONG ton tai' ((Psql 'select count(*) from "MsgDeadLetters" where "TenSuKien" = ''LoaiSuKienKhongTonTai'';') -eq '1') 'dong = 1'

    $mocXau = Psql 'select now();'
    $urlXau = "$host_$($route -replace '\{id\}', $idXau)"

    $respXau = Invoke-WebRequest -Method Post -Uri $urlXau -Headers $h -SkipHttpErrorCheck
    Write-Host ("      HTTP STATUS (loai khong anh xa duoc) = {0}" -f $respXau.StatusCode)

    Kq 'D18 tra 409 (KHONG phai 403 "khong co quyen")' ($respXau.StatusCode -eq 409) ("status = " + $respXau.StatusCode)

    $dauXau = Psql ("select (""ReplayCount""::text || '|' || (""ReplayedAtUtc"" is null)::text || '|' || ""DaXuLy""::text) from ""MsgDeadLetters"" where ""Id"" = '{0}';" -f $idXau)
    Kq 'D19 DB KHONG ghi dau phat lai (0|true|false)' ($dauXau -eq '0|true|false') ("dau = " + $dauXau)

    Start-Sleep -Seconds 3
    Kq 'D20 KHONG co event nao duoc phat (outbox=0, inbox moi=0)' `
        (((DemOutbox) -eq 0) -and ((DemInboxTheoMoc $mocXau) -eq 0)) `
        ("outbox = " + (DemOutbox) + " · inbox moi = " + (DemInboxTheoMoc $mocXau))
}
catch {
    Write-Host ("   ❌ NGOAI LE: " + $_.Exception.Message)
    $script:soHong++
}
finally {
    if ($tienTrinh) { TatHost $tienTrinh }
    DonDep
    Psql 'update "AbpUsers" set "ShouldChangePasswordOnNextLogin" = true where "NormalizedUserName" = ''ADMIN'';' | Out-Null
    Kq 'D16 don dep: DLQ probe = 0 · outbox = 0 · co bao mat = true' `
        (((DemDlq) -eq 0) -and ((DemOutbox) -eq 0) -and ((Psql 'select "ShouldChangePasswordOnNextLogin"::text from "AbpUsers" where "NormalizedUserName" = ''ADMIN'';') -eq 'true')) `
        'dlq = 0 · outbox = 0'
    Remove-Item Env:\AbpEventBusBoxes__PeriodSeconds -ErrorAction SilentlyContinue
}

Write-Host ("════ PROBE DLQ REPLAY: {0} DAT / {1} HONG ════" -f $script:soDat, $script:soHong)
if ($script:soHong -gt 0) { exit 1 }
exit 0
