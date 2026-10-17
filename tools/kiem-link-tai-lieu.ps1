# kiem-link-tai-lieu.ps1 — CONG KIEM: moi tham chieu duong dan trong tai lieu phai TRO TOI tep CO THAT.
#
# Vi sao can: tai lieu duoc to chuc lai theo module (docs/features/<module>/), va
# mot tham chieu chet KHONG lam do build — no chi lam nguoi doc tin vao mot tai lieu khong ton tai.
# Cung loai loi voi Mermaid: hong am tham (xem docs/design-doc-guidelines.md §7).
#
# Kiem 2 loai:
#   (A) LINK MARKDOWN   [chu](duong/dan.md)  -> giai theo thu muc cua tep chua no
#   (B) DUONG DAN TRONG BACKTICK  `docs/features/x.md`  -> giai theo GOC REPO, roi thu tuong doi
#
# Thoat 1 neu co tham chieu chet.

param(
    [string]$Goc = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$ChiTiet
)

$ErrorActionPreference = 'Stop'

$boQua = '\\node_modules\\|\\bin\\|\\obj\\|\\.git\\|\\artifacts\\|\\node_modules/'
$tepTaiLieu = Get-ChildItem $Goc -Recurse -File -Filter *.md |
    Where-Object { $_.FullName -notmatch $boQua }

Write-Output "Cong kiem LINK TAI LIEU — goc: $Goc"
Write-Output ("Tep .md duoc quet: {0}" -f $tepTaiLieu.Count)

# Tien to trong backtick duoc coi la DUONG DAN (khong phai vi du minh hoa)
$tienToDuongDan = '^(docs|src|test|tools|modules|shared|apps|build|\.github|scripts)/'

$tongLink = 0
$tongPath = 0
$loi = New-Object System.Collections.Generic.List[object]

foreach ($t in $tepTaiLieu) {
    $noiDung = [IO.File]::ReadAllText($t.FullName, [Text.Encoding]::UTF8)
    $thuMuc = Split-Path $t.FullName -Parent
    $tuongDoi = $t.FullName.Replace("$Goc\", '')
    $soDong = 0

    # Khối CODE SPAN — 🔑 mot LINK viet TRONG backtick la VAN BAN MINH HOA, khong phai lien ket that
    # (vi du trong tools/README-probe.md: cach mo ta dinh dang link).
    # ⚠️ PHAI ghep dung theo LUAT MARKDOWN: mot RUN N dau backtick mo, va RUN N backtick tiep theo dong.
    #    📌 Regex don gian '`[^`]+`' ghep SAI khi delimiter la HAI backtick (`` `x` ``) — no ghep vao
    #    cap ngoai cung va de lot noi dung ben trong. Do la loi DO DUOC khi viet script nay.
    $khoangCode = [System.Collections.Generic.List[object]]::new()
    $runs = [regex]::Matches($noiDung, '`+')
    $i = 0
    while ($i -lt $runs.Count) {
        $doDai = $runs[$i].Length
        $dong = -1
        for ($j = $i + 1; $j -lt $runs.Count; $j++) {
            if ($runs[$j].Length -eq $doDai) { $dong = $j; break }
        }
        if ($dong -lt 0) { $i++; continue }   # khong co cap dong ⇒ backtick nay la ky tu thuong
        $khoangCode.Add([pscustomobject]@{ BatDau = $runs[$i].Index; Cuoi = $runs[$dong].Index + $runs[$dong].Length })
        $i = $dong + 1
    }

    # (A) LINK MARKDOWN
    foreach ($m in [regex]::Matches($noiDung, '\]\(([^)\s]+)\)')) {
        $dich = $m.Groups[1].Value
        if ($dich -match '^https?://' -or $dich -match '^mailto:' -or $dich -match '^#') { continue }
        $duongDan = ($dich -split '#')[0]
        if ([string]::IsNullOrWhiteSpace($duongDan)) { continue }

        $trongCode = $false
        foreach ($k in $khoangCode) {
            if ($m.Index -ge $k.BatDau -and $m.Index -lt $k.Cuoi) { $trongCode = $true; break }
        }
        if ($trongCode) { continue }

        $tongLink++
        $soDong++
        $day = if ($duongDan.StartsWith('/')) { Join-Path $Goc $duongDan.TrimStart('/') } else { Join-Path $thuMuc $duongDan }
        if (-not (Test-Path $day)) {
            $loi.Add([pscustomobject]@{ Loai = 'LINK'; Tep = $tuongDoi; Dong = $soDong; Dich = $dich })
        }
    }

    # (B) DUONG DAN TRONG BACKTICK
    foreach ($m in [regex]::Matches($noiDung, '`([^`\r\n]+)`')) {
        $dich = $m.Groups[1].Value.Trim()
        if ($dich -notmatch '\.md$') { continue }
        if ($dich -notmatch $tienToDuongDan) { continue }
        # Mẫu/glob hoặc đường dẫn RÚT GỌN — không phải đường dẫn cụ thể:
        #   `*` `?` `<` `>` `|`  = mẫu
        #   `…` hoặc `...`       = tác giả CỐ Ý rút gọn (ví dụ `docs/features/XC-001…XC-006`)
        # 🔑 Bỏ qua đúng những dạng này là điều kiện để cổng KHÔNG báo dương tính giả —
        #    và một cổng báo sai thì người ta bỏ qua nó (ca P của test-cong-kiem.ps1).
        if ($dich -match '[\*\?\<\>\|…]' -or $dich -match '\.\.\.') { continue }
        $tongPath++
        $dayGoc = Join-Path $Goc $dich
        $dayTuongDoi = Join-Path $thuMuc $dich
        if (-not (Test-Path $dayGoc) -and -not (Test-Path $dayTuongDoi)) {
            $loi.Add([pscustomobject]@{ Loai = 'PATH'; Tep = $tuongDoi; Dong = 0; Dich = $dich })
        }
    }
}

Write-Output ("Link markdown da kiem : {0}" -f $tongLink)
Write-Output ("Duong dan backtick    : {0}" -f $tongPath)
Write-Output ""

if ($loi.Count -eq 0) {
    Write-Output "PASS — 0 tham chieu chet."
    exit 0
}

Write-Output ("FAIL — {0} tham chieu chet:" -f $loi.Count)
$loi | Group-Object Tep | Sort-Object Name | ForEach-Object {
    Write-Output ("  {0}  ({1} tham chieu)" -f $_.Name, $_.Count)
    $_.Group | Sort-Object Dich -Unique | ForEach-Object {
        Write-Output ("      -> {0}" -f $_.Dich)
    }
}
exit 1
