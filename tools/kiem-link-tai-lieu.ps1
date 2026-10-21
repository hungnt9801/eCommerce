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

# 🚨 **DAU PHAN CACH PHAI LA CUA HE DIEU HANH** (`XC-007` **P-06**, do lai o `S9` 21/10/2026).
# 📌 Ban cu viet `'\\node_modules\\|\\bin\\|...'` — tren Linux duong dan dung `/` ⇒ **KHONG** loai truoc duoc
#    gi ⇒ script **quet ca `node_modules/` va `.git/`** (hang chuc nghin tep `.md`) ⇒ cham, va tep .md trong
#    `node_modules/` tro toi duong dan khong ton tai ⇒ 🔑 **bao chet GIA** ⇒ nguoi ta bo qua cong.
# ✅ Dung lop ky tu `[\\/]` = **chap nhan ca hai** — mot bieu thuc chay dung tren ca hai he dieu hanh.
$boQua = '[\\/](node_modules|bin|obj|\.git|artifacts)[\\/]'
$tepTaiLieu = Get-ChildItem $Goc -Recurse -File -Filter *.md |
    Where-Object { $_.FullName -notmatch $boQua }

Write-Output "Cong kiem LINK TAI LIEU — goc: $Goc"
Write-Output ("Tep .md duoc quet: {0}" -f $tepTaiLieu.Count)

# Tien to trong backtick duoc coi la DUONG DAN (khong phai vi du minh hoa)
$tienToDuongDan = '^(docs|src|test|tools|modules|shared|apps|build|\.github|scripts)/'

$tongLink = 0
$tongPath = 0
$loi = New-Object System.Collections.Generic.List[object]

# 🚨 **TỒN TẠI PHẢI PHÂN BIỆT HOA/THƯỜNG** (`S11`, 21/10/2026 — đo được trên CI Linux):
# trên Windows `Test-Path 'docs/tiep-tuc-phien-moI.md'` trả **TRUE** dù tệp thật là `…-moi.md`
# ⇒ cổng **XANH trên máy dev** nhưng **ĐỎ trên Linux**. 🔑 Đó là *"phép đo xanh vì hệ điều hành"* —
# cùng họ với lỗi `Path.IsPathRooted` của kho tệp media (`CAT-003` §18.8).
# ✅ Đi từng ĐOẠN đường dẫn và so **`-ceq`** ⇒ bắt được cả trên Windows; trên Linux kết quả không đổi.
function Test-DuongDanTonTai {
    param([Parameter(Mandatory)][string]$DuongDan)

    if (-not (Test-Path -LiteralPath $DuongDan)) { return $false }

    $dayDay = [IO.Path]::GetFullPath($DuongDan)
    $gocDay = [IO.Path]::GetFullPath($Goc)

    # 📌 Ngoài gốc repo (hiếm): tin `Test-Path` — không có "cây thật" để so từng đoạn.
    if (-not $dayDay.StartsWith($gocDay, [StringComparison]::OrdinalIgnoreCase)) { return $true }

    $tuongDoi = [IO.Path]::GetRelativePath($gocDay, $dayDay)
    $hienTai = $gocDay

    foreach ($doan in ($tuongDoi -split '[\\/]' | Where-Object { $_ -ne '' })) {
        $khop = $null

        # 🛡️ Lưới an toàn: không đọc được thư mục ⇒ KHÔNG kết luận "chết" (báo sai làm người ta bỏ cổng).
        try {
            foreach ($e in [IO.Directory]::EnumerateFileSystemEntries($hienTai)) {
                if ([IO.Path]::GetFileName($e) -ceq $doan) { $khop = $e; break }
            }
        }
        catch {
            return $true
        }

        if (-not $khop) { return $false }

        $hienTai = $khop
    }

    return $true
}

foreach ($t in $tepTaiLieu) {
    $noiDung = [IO.File]::ReadAllText($t.FullName, [Text.Encoding]::UTF8)
    $thuMuc = Split-Path $t.FullName -Parent

    # 📌 `Replace("$Goc\")` chi dung tren Windows (`XC-007` **P-06**): tren Linux khong co `\` sau `$Goc`
    # ⇒ phep thay **khong xay ra** ⇒ bao cao in **duong dan TUYET DOI** (khong lam sai ket luan, nhung kho doc
    # va kho so sanh giua hai may). ✅ `GetRelativePath` dung **dau phan cach cua he dieu hanh**.
    $tuongDoi = [IO.Path]::GetRelativePath($Goc, $t.FullName)
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
        if (-not (Test-DuongDanTonTai -DuongDan $day)) {
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
        if (-not (Test-DuongDanTonTai -DuongDan $dayGoc) -and -not (Test-DuongDanTonTai -DuongDan $dayTuongDoi)) {
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
