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
        'src\eCommerce.HttpApi.Host\bin\Debug\net10.0',
        'src\eCommerce.Web\bin\Debug\net10.0'
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
        $thay += [pscustomobject]@{
            Khoa = $khoa
            Moc  = $t.LastWriteTime.ToString('o')
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
    Write-Output "⇒ PHEP KIEM VO NGHIA. Kiem lai -ThuMuc/-Ten."
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

    $mocCu = Get-Content $tepMoc -Raw | ConvertFrom-Json
    $bangCu = @{}
    foreach ($m in $mocCu) { $bangCu[$m.Khoa] = $m.Moc }

    $khongDoi = @()
    $moi = @()

    foreach ($t in $thay) {
        if (-not $bangCu.ContainsKey($t.Khoa)) {
            $moi += $t.Khoa
            continue
        }

        if ($bangCu[$t.Khoa] -eq $t.Moc) {
            $khongDoi += [pscustomobject]@{ Khoa = $t.Khoa; Moc = $t.Moc }
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
