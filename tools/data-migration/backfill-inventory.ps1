<#
.SYNOPSIS
    Backfill `InvItems` (tồn kho 9 chiều) từ **sổ cái** `InvStockLedger` — `WP-04` việc 5.

.DESCRIPTION
    🚨 **NGUỒN SỰ THẬT LÀ SỔ CÁI, KHÔNG PHẢI ẢNH CHỤP.** `InvItems` chỉ là snapshot để đọc nhanh; nếu nó lệch sổ
    cái thì **báo cáo vẫn ra số, chỉ là số sai** (đúng họ lỗi mà `Reconciliation` sinh ra để phát hiện). Script
    này **dựng lại** snapshot từ sổ cái cho 10 cột số lượng và **đối soát trước/sau** bằng cùng một truy vấn:

      `OnHand`   = Σ(dấu × delta) với các loại **ảnh hưởng OnHand** (theo `DoiSoatTonKho.Dau`);
      `Reserved` = Σ(Reservation) − Σ(ReservationRelease);
      `Shipping`, `Damaged`, `Warranty`, `Holding` = Σ(chiều vào) − Σ(chiều ra);
      `Transferring`, `WarrantyHolding`, `PreOrder`, `WaitingInbound` = **0** (WP-04 chưa có đường ghi).

    🔒 **Mặc định là DRY-RUN.** Phải truyền `-Apply` mới ghi. Lần chạy đầu **bắt buộc** là dry-run (`07 §8`).

    🚨 **Script TỪ CHỐI dựng lại một dòng KHÔNG HỢP LỆ.** Nếu sổ cái cho ra `Reserved > OnHand` (hoặc âm), thì
    dựng lại sẽ vi phạm `CHECK` của `InvItems` — và **ghi đè** lên một dòng đang có là **phá dữ liệu tốt để thay
    bằng dữ liệu sai**. Những cặp đó được đếm riêng (`KHONG-HOP-LE`), in ra kèm số, và script thoát `1` để
    **người quyết định** (điều tra sổ cái) — không tự "sửa cho xanh".

    Bốn mức thoát:
      0 = khớp sổ cái (hoặc đã sửa xong và **đo lại** bằng 0 lệch);
      1 = dữ liệu: còn lệch / còn dòng thiếu / có cặp không hợp lệ;
      2 = môi trường: không thấy docker/container/psql, hoặc không đọc được số đo (script hỏng).

.PARAMETER Apply
    Ghi thật: UPDATE snapshot theo sổ cái + INSERT dòng `InvItems` còn thiếu (chỉ dòng **hợp lệ**).

.PARAMETER Container
    Tên container PostgreSQL (mặc định `ecommerce-postgres`).

.PARAMETER Database
    Tên DB (mặc định `ecommerce`).

.PARAMETER User
    User PostgreSQL (mặc định `ecommerce`).

.EXAMPLE
    pwsh -File .\tools\data-migration\backfill-inventory.ps1

.EXAMPLE
    pwsh -File .\tools\data-migration\backfill-inventory.ps1 -Apply
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$Container = 'ecommerce-postgres',
    [string]$Database = 'ecommerce',
    [string]$User = 'ecommerce',

    # 🚨 CHỈ dùng khi đã **điều tra** các cặp "sổ cái vi phạm bất biến" và chấp nhận chúng là **nợ dữ liệu cũ**
    # (ví dụ dữ liệu E2E để lại trong DB dev). KHÔNG dùng để che một lệch MỚI: `LECH`/`THIEU-DONG` vẫn chặn.
    [switch]$ChapNhanKhongHopLe
)

$ErrorActionPreference = 'Stop'

$docker = Get-Command docker -ErrorAction SilentlyContinue
if ($null -eq $docker) {
    Write-Host 'KHONG TIM THAY docker trong PATH.' -ForegroundColor Red
    exit 2
}

$dangChay = (& docker ps --filter "name=$Container" --format '{{.Names}}') 2>$null
if ("$dangChay".Trim() -ne $Container) {
    Write-Host "Container '$Container' KHONG chay. Bat bang: docker compose up -d" -ForegroundColor Red
    exit 2
}

function Psql {
    param([Parameter(Mandatory)][string]$Sql)

    # 📌 Nạp SQL qua **stdin** (không qua -c) để tránh PowerShell tách/nuốt dấu ngoặc kép của PostgreSQL.
    $Sql | & docker exec -i $Container psql -U $User -d $Database -v ON_ERROR_STOP=1 -t -A -F '|'
    if ($LASTEXITCODE -ne 0) {
        throw "psql tra exit $LASTEXITCODE"
    }
}

# ── Phần dùng CHUNG: sổ cái → 10 cột + cờ hợp lệ. Cùng một định nghĩa cho "trước" và "sau". ────────────────
$soCaiCte = @'
with so_cai as (
    select
        l."TenantId", l."WarehouseId", l."Sku",
        sum(case when l."MovementType" in (0, 4, 5, 6, 7, 10) then l."QuantityDelta"
                 when l."MovementType" in (3, 8, 9, 19, 11, 14, 15, 20, 21) then -l."QuantityDelta"
                 else 0 end) as on_hand,
        sum(case when l."MovementType" = 1 then l."QuantityDelta"
                 when l."MovementType" = 2 then -l."QuantityDelta" else 0 end) as reserved,
        sum(case when l."MovementType" = 11 then l."QuantityDelta"
                 when l."MovementType" = 12 then -l."QuantityDelta" else 0 end) as shipping,
        sum(case when l."MovementType" = 13 then l."QuantityDelta"
                 when l."MovementType" = 14 then -l."QuantityDelta" else 0 end) as damaged,
        sum(case when l."MovementType" = 15 then l."QuantityDelta"
                 when l."MovementType" = 16 then -l."QuantityDelta" else 0 end) as warranty,
        sum(case when l."MovementType" = 17 then l."QuantityDelta"
                 when l."MovementType" = 18 then -l."QuantityDelta" else 0 end) as holding
    from "InvStockLedger" l
    where l."IsDeleted" = false
    group by 1, 2, 3
),
doi_chieu as (
    select
        coalesce(i."TenantId", s."TenantId")        as tenant_id,
        coalesce(i."WarehouseId", s."WarehouseId")  as warehouse_id,
        coalesce(i."Sku", s."Sku")                  as sku,
        coalesce(i."OnHand", 0)                     as on_hand_anh_chup,
        coalesce(s.on_hand, 0)                      as on_hand_so_cai,
        coalesce(i."Reserved", 0)                   as reserved_anh_chup,
        coalesce(s.reserved, 0)                     as reserved_so_cai,
        coalesce(i."Shipping", 0)                   as shipping_anh_chup,
        coalesce(s.shipping, 0)                     as shipping_so_cai,
        coalesce(i."Damaged", 0)                    as damaged_anh_chup,
        coalesce(s.damaged, 0)                      as damaged_so_cai,
        coalesce(i."Warranty", 0)                   as warranty_anh_chup,
        coalesce(s.warranty, 0)                     as warranty_so_cai,
        coalesce(i."Holding", 0)                    as holding_anh_chup,
        coalesce(s.holding, 0)                      as holding_so_cai,
        (i."Id" is null)                            as thieu_dong,
        -- Một dòng dựng lại chỉ HỢP LỆ nếu thoả bất biến của `InvItems` (DB có CHECK tương ứng).
        (coalesce(s.on_hand, 0) >= 0
         and coalesce(s.reserved, 0) >= 0
         and coalesce(s.reserved, 0) <= coalesce(s.on_hand, 0)
         and coalesce(s.shipping, 0) >= 0
         and coalesce(s.damaged, 0) >= 0
         and coalesce(s.warranty, 0) >= 0
         and coalesce(s.holding, 0) >= 0)          as hop_le
    from "InvItems" i
    full outer join so_cai s
        on i."TenantId" is not distinct from s."TenantId"
       and i."WarehouseId" = s."WarehouseId"
       and i."Sku" = s."Sku"
    where (i."IsDeleted" = false or i."Id" is null)
)
'@

$sqlTongHop = $soCaiCte + @'
select
    count(*)                                                             as so_cap,
    count(*) filter (where not hop_le)                                   as so_khong_hop_le,
    count(*) filter (where thieu_dong and hop_le)                        as so_thieu_dong,
    count(*) filter (where not thieu_dong and hop_le and (
            on_hand_anh_chup <> on_hand_so_cai
         or reserved_anh_chup <> reserved_so_cai
         or shipping_anh_chup <> shipping_so_cai
         or damaged_anh_chup <> damaged_so_cai
         or warranty_anh_chup <> warranty_so_cai
         or holding_anh_chup <> holding_so_cai))                         as so_lech
from doi_chieu
'@

$sqlChiTiet = $soCaiCte + @'
select sku || ' | anh_chup OnHand=' || on_hand_anh_chup || ' Reserved=' || reserved_anh_chup
       || ' | so_cai OnHand=' || on_hand_so_cai || ' Reserved=' || reserved_so_cai
       || ' | ' || case when not hop_le then 'KHONG-HOP-LE'
                        when thieu_dong then 'THIEU-DONG'
                        else 'LECH' end
from doi_chieu
where (not hop_le) or thieu_dong
   or on_hand_anh_chup <> on_hand_so_cai
   or reserved_anh_chup <> reserved_so_cai
   or shipping_anh_chup <> shipping_so_cai
   or damaged_anh_chup <> damaged_so_cai
   or warranty_anh_chup <> warranty_so_cai
   or holding_anh_chup <> holding_so_cai
order by sku
limit 20
'@

$sqlApDung = $soCaiCte + @'
, sua as (
    update "InvItems" i
       set "OnHand" = d.on_hand_so_cai,
           "Reserved" = d.reserved_so_cai,
           "Shipping" = d.shipping_so_cai,
           "Damaged" = d.damaged_so_cai,
           "Warranty" = d.warranty_so_cai,
           "Holding" = d.holding_so_cai
      from doi_chieu d
     where d.hop_le
       and not d.thieu_dong
       and i."IsDeleted" = false
       and i."TenantId" is not distinct from d.tenant_id
       and i."WarehouseId" = d.warehouse_id
       and i."Sku" = d.sku
    returning i."Sku"
),
them as (
    insert into "InvItems" ("Id", "TenantId", "WarehouseId", "Sku", "OnHand", "Reserved",
                            "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
    select gen_random_uuid(), d.tenant_id, d.warehouse_id, d.sku,
           d.on_hand_so_cai, d.reserved_so_cai,
           '{}', gen_random_uuid()::text, now(), false
      from doi_chieu d
     where d.hop_le and d.thieu_dong
    returning "Sku"
)
select (select count(*) from sua) as so_sua, (select count(*) from them) as so_them
'@

try {
    $cheDo = if ($Apply) { 'APPLY (GHI THAT)' } else { 'DRY-RUN (khong ghi)' }
    Write-Host "=== BACKFILL TON KHO (WP-04) — che do: $cheDo ===" -ForegroundColor Cyan

    $tongKet = (Psql -Sql $sqlTongHop).Trim()
    $phan = $tongKet.Split('|')
    if ($phan.Length -lt 4) { throw "So do khong dung dinh dang: '$tongKet'" }

    $soCap = [int]$phan[0]
    $soKhongHopLe = [int]$phan[1]
    $soThieuDong = [int]$phan[2]
    $soLech = [int]$phan[3]

    Write-Host ("Cap (kho, SKU) da do      : {0}" -f $soCap)
    Write-Host ("  - LECH so voi so cai    : {0}" -f $soLech) -ForegroundColor $(if ($soLech -eq 0) { 'Green' } else { 'Yellow' })
    Write-Host ("  - THIEU dong snapshot   : {0}" -f $soThieuDong) -ForegroundColor $(if ($soThieuDong -eq 0) { 'Green' } else { 'Yellow' })
    $mauSoKhongHopLe = if ($ChapNhanKhongHopLe) { 'Yellow' } else { 'Red' }
    Write-Host ("  - KHONG-HOP-LE (so cai vi pham bat bien): {0}" -f $soKhongHopLe) -ForegroundColor $(if ($soKhongHopLe -eq 0) { 'Green' } else { $mauSoKhongHopLe })

    # `-ChapNhanKhongHopLe` chỉ bỏ qua các cặp **đã điều tra**; LECH/THIEU-DONG thì luôn chặn.
    $chanLai = $soLech + $soThieuDong + $(if ($ChapNhanKhongHopLe) { 0 } else { $soKhongHopLe })

    if (($soLech + $soThieuDong + $soKhongHopLe) -gt 0) {
        Write-Host '--- Chi tiet (toi da 20 dong) ---'
        Psql -Sql $sqlChiTiet | ForEach-Object { Write-Host "  $_" }
    }

    if (-not $Apply) {
        if ($chanLai -eq 0) {
            Write-Host 'KET LUAN: snapshot KHOP so cai — khong can ghi.' -ForegroundColor Green
            if ($soKhongHopLe -gt 0) {
                Write-Host ("           ({0} cap KHONG-HOP-LE duoc CHAP NHAN theo tham so -ChapNhanKhongHopLe — khong ghi gi.)" -f $soKhongHopLe) -ForegroundColor Yellow
            }
            exit 0
        }

        Write-Host 'KET LUAN: co chenh lech — chay lai voi -Apply de dung lai snapshot tu so cai.' -ForegroundColor Yellow
        exit 1
    }

    Write-Host '--- Dang ghi: UPDATE snapshot + INSERT dong thieu (CHI dong hop le) ---'
    $ketQuaGhi = (Psql -Sql $sqlApDung).Trim().Split('|')
    Write-Host ("  da sua: {0} dong · da them: {1} dong" -f $ketQuaGhi[0], $ketQuaGhi[1])

    # 🔒 ĐO LẠI bằng ĐÚNG truy vấn trên: một lần chạy đã "sửa" không được coi là xong nếu không đo lại.
    $sau = (Psql -Sql $sqlTongHop).Trim().Split('|')
    $conLai = [int]$sau[2] + [int]$sau[3] + $(if ($ChapNhanKhongHopLe) { 0 } else { [int]$sau[1] })

    Write-Host ("Sau khi ghi: {0} cap con chenh lech (khong hop le {1} · thieu dong {2} · lech {3})" -f
        $conLai, $sau[1], $sau[2], $sau[3]) -ForegroundColor $(if ($conLai -eq 0) { 'Green' } else { 'Red' })

    if ($conLai -ne 0) {
        Write-Host 'KET LUAN: VAN CON LECH sau khi ghi — DUNG, khong coi la thanh cong. Cac cap KHONG-HOP-LE can nguoi dieu tra so cai.' -ForegroundColor Red
        exit 1
    }

    Write-Host 'KET LUAN: snapshot da dung lai tu so cai, do lai = 0 lech.' -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ("LOI MOI TRUONG/SCRIPT: {0}" -f $_.Exception.Message) -ForegroundColor Red
    exit 2
}
