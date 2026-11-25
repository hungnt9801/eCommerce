<#
.SYNOPSIS
    Bang phien chay (session runner) cua WP — registry JSON + bang Markdown sinh tu registry.

.DESCRIPTION
    Nguon su that: agents/state/phien-wp14.json. Bang trong docs/agents/bang-phien-wp14.md (giua hai marker
    <!-- BANG-PHIEN:BAT-DAU --> / <!-- BANG-PHIEN:KET-THUC -->) la VIEW — sinh lai bang `sync`, khong sua tay.
    Skill /chay-phien goi script nay o dau va cuoi moi phien; nguoi chi can dan `/chay-phien`.
    Khuon theo §14 cua SelfLearningAI ke-hoach-xay-dung-cms.md (cms-session.ps1), them `next`, `dodang`.

    Lenh:
      status                                   in bang
      next                                     in Id hang phai chay tiep (dang-lam > do-dang > chua co phu thuoc xong)
      start  -Id P-xx [-Session Nxx]           nhan hang (kiem phu thuoc, khong 2 hang dang-lam); bo -Session => script
                                               tu cap so phien = max(handover, cot Phien) + 1, in dong `PHIEN=Nxx`
      done   -Id P-xx -Evidence "..." [-Handover <tep>]   hoan thanh (bat buoc bang chung)
      dodang -Id P-xx -Reason "..."   [-Handover <tep>]   phien ket thuc nhung hang chua dat DoD
      blocked -Id P-xx -Reason "..."           bi chan ben ngoai
      release -Id P-xx [-Reason "..."]         tra ve chua bat dau (hang dang-lam: chi voi -Force — co the la phien khac)
      sync                                     sinh lai bang Markdown tu registry
      check                                    bang khop registry, toi da 1 hang dang-lam, hang xong co bang chung
      doisoat -Id P-xx                         doi soat hang doc-gate voi docreview + handover moi nhat (phien chay kieu cu):
                                               exit 0 = moi ma DOC-OK va handover §2 khong con nhac ma => nen `done`
      uyquyen -Id P-xx -Evidence "D-xx"        chuyen hang cua NGUOI sang decider lam thay (chi sau khi hang
                                               phien uy quyen `thao-luan` da xong; hang camUyQuyen bi tu choi)
      show   -Id P-xx                          in JSON {hang, chuoi} — bo chay + skill /chay-phien-code doc nhanh/buoc cua hang

    -Chuoi <ten>: dung registry agents/state/phien-<ten>.json + bang docs/agents/bang-phien-<ten>.md (mac dinh: WP-14).
      Chuoi code GD3 (`D-xx` phuong an B): `-Chuoi gd3-1`.

    Ma thoat: 0 OK · 1 loi tham so/tep/check · 2 phu thuoc chua xong · 3 chi con viec cua NGUOI / bi chan
              4 da co hang khac dang-lam · 5 het viec · 6 hang cua nguoi khong `start` duoc

.EXAMPLE
    pwsh -File tools/agents/phien.ps1 next
    pwsh -File tools/agents/phien.ps1 next -Chuoi gd3-1
    pwsh -File tools/agents/phien.ps1 done -Id P-04 -Evidence "check-feature-doc exit 0; DOC-OK 0 P1" -Handover docs/nhanh/handover-n25.md
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)][ValidateSet('status', 'next', 'start', 'done', 'dodang', 'blocked', 'release', 'sync', 'check', 'doisoat', 'uyquyen', 'show')][string]$Lenh = 'status',
    [string]$Id,
    [string]$Session,
    [string]$Evidence,
    [string]$Reason,
    [string]$Handover,
    [string]$Root,
    [string]$Registry = 'agents/state/phien-wp14.json',
    [string]$Doc = 'docs/agents/bang-phien-wp14.md',
    [string]$Chuoi,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
if ($Chuoi) {
    if ($Chuoi -notmatch '^[a-z0-9][a-z0-9-]*$') { Write-Output "LOI: -Chuoi chi gom a-z 0-9 -"; exit 1 }
    if (-not $PSBoundParameters.ContainsKey('Registry')) { $Registry = "agents/state/phien-$Chuoi.json" }
    if (-not $PSBoundParameters.ContainsKey('Doc')) { $Doc = "docs/agents/bang-phien-$Chuoi.md" }
}
$regPath = if ([IO.Path]::IsPathRooted($Registry)) { $Registry } else { Join-Path $Root $Registry }
$docPath = if ([IO.Path]::IsPathRooted($Doc)) { $Doc } else { Join-Path $Root $Doc }
$utf8 = [Text.UTF8Encoding]::new($false)
$batDau = '<!-- BANG-PHIEN:BAT-DAU -->'
$ketThuc = '<!-- BANG-PHIEN:KET-THUC -->'

if (-not (Test-Path -LiteralPath $regPath)) { Write-Output "LOI: khong thay registry $regPath"; exit 1 }

function Doc-Reg { return ([IO.File]::ReadAllText($regPath, $utf8) | ConvertFrom-Json -AsHashtable) }
function Ghi-Reg($r) {
    $r['capNhat'] = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    [IO.File]::WriteAllText($regPath, ($r | ConvertTo-Json -Depth 8) + "`n", $utf8)
}
function Hang($r, [string]$id) {
    $h = @($r['rows'] | Where-Object { $_['id'] -eq $id })
    if ($h.Count -ne 1) { Write-Output "LOI: khong thay hang $id"; exit 1 }
    return $h[0]
}
function PhuThuocXong($r, $h) {
    foreach ($d in @($h['dependsOn'])) {
        if (-not $d) { continue }
        $x = @($r['rows'] | Where-Object { $_['id'] -eq $d })
        if ($x.Count -ne 1 -or $x[0]['status'] -ne 'xong') { return $d }
    }
    return $null
}
function Nhan($h) {
    switch ($h['status']) {
        'xong' { return '✅ Hoàn thành' }
        'dang-lam' { return '🟨 Đang làm' }
        'do-dang' { return '🔁 Dở dang' }
        'bi-chan' { return '⛔ Bị chặn' }
        default {
            if ($h['loai'] -eq 'nguoi') { return '👤 Chờ người' }
            if ($h['loai'] -eq 'thao-luan') { return '🗣 Chờ ngồi cùng' }
            return '⬜ Chưa bắt đầu'
        }
    }
}
function O([string]$s) { if ($null -eq $s) { return '' }; return ($s -replace '\|', '\|' -replace "`r?`n", ' ') }

function Sinh-Bang($r) {
    $sb = [Text.StringBuilder]::new()
    [void]$sb.AppendLine('| # | Đợt | Nội dung | Vai | Phụ thuộc | Prompt để dán | Phiên | Trạng thái | Bằng chứng / ghi chú |')
    [void]$sb.AppendLine('|---|---|---|---|---|---|---|---|---|')
    foreach ($h in $r['rows']) {
        $lenhSkill = '/' + $(if ($r['skill']) { $r['skill'] } else { 'chay-phien' }) + $(if ($r['chuoi']) { ' chuoi=' + $r['chuoi'] } else { '' })
        $prompt = if ($h['loai'] -eq 'nguoi') { '— (việc của người)' } else { '`' + $lenhSkill + ' ' + $h['id'] + '`' }
        if ($h['prompt']) { $prompt = $h['prompt'] }
        $dep = (@($h['dependsOn']) | Where-Object { $_ }) -join ', '
        if (-not $dep) { $dep = '—' }
        $phien = (@($h['phien']) | Where-Object { $_ }) -join ', '
        if (-not $phien) { $phien = '—' }
        $bc = if ($h['status'] -eq 'xong') { $h['evidence'] } else { $h['ghiChu'] }
        if ($h['handover']) { $bc = ($bc + ' · `' + $h['handover'] + '`').Trim(' ', '·') }
        [void]$sb.AppendLine(('| `{0}` | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |' -f $h['id'], $h['dot'], (O $h['noiDung']), (O $h['vai']), $dep, $prompt, $phien, (Nhan $h), (O $bc)))
    }
    return $sb.ToString().TrimEnd()
}
function Sync($r) {
    if (-not (Test-Path -LiteralPath $docPath)) { Write-Output "LOI: khong thay $docPath"; exit 1 }
    $t = [IO.File]::ReadAllText($docPath, $utf8)
    $i = $t.IndexOf($batDau); $j = $t.IndexOf($ketThuc)
    if ($i -lt 0 -or $j -lt $i) { Write-Output "LOI: $docPath thieu marker $batDau / $ketThuc"; exit 1 }
    $moi = $t.Substring(0, $i + $batDau.Length) + "`n" + (Sinh-Bang $r) + "`n" + $t.Substring($j)
    if ($moi -ne $t) { [IO.File]::WriteAllText($docPath, $moi, $utf8) }
}
function SoPhienKe($r) {
    $max = 0
    $hdDir = Join-Path $Root 'docs/nhanh'
    if (Test-Path -LiteralPath $hdDir) {
        foreach ($f in Get-ChildItem -LiteralPath $hdDir -Filter 'handover-n*.md' -File) {
            $m = [regex]::Match($f.Name, '^handover-n(\d+)\.md$'); if ($m.Success) { $max = [Math]::Max($max, [int]$m.Groups[1].Value) }
        }
    }
    foreach ($h in $r['rows']) { foreach ($p in @($h['phien'])) { $m = [regex]::Match([string]$p, '^N(\d+)$'); if ($m.Success) { $max = [Math]::Max($max, [int]$m.Groups[1].Value) } } }
    return 'N' + ($max + 1)
}
function Can([string]$ten, [string]$v) { if (-not $v) { Write-Output "LOI: lenh nay bat buoc -$ten"; exit 1 } }
function KiemHandover {
    if (-not $Handover) { return }
    $p = if ([IO.Path]::IsPathRooted($Handover)) { $Handover } else { Join-Path $Root $Handover }
    if (-not (Test-Path -LiteralPath $p)) { Write-Output "LOI: -Handover khong ton tai: $Handover"; exit 1 }
}

$r = Doc-Reg
switch ($Lenh) {
    'status' {
        foreach ($h in $r['rows']) { Write-Output ('{0,-6} {1,-2} {2,-16} {3}' -f $h['id'], $h['dot'], (Nhan $h), $h['noiDung']) }
        exit 0
    }
    'next' {
        $dl = @($r['rows'] | Where-Object { $_['status'] -eq 'dang-lam' })
        if ($dl.Count) { Write-Output $dl[0]['id']; exit 0 }
        $dd = @($r['rows'] | Where-Object { $_['status'] -eq 'do-dang' })
        if ($dd.Count) { Write-Output $dd[0]['id']; exit 0 }
        foreach ($h in $r['rows']) {
            if ($h['status'] -eq 'chua' -and $h['loai'] -ne 'nguoi' -and -not (PhuThuocXong $r $h)) { Write-Output $h['id']; exit 0 }
        }
        $con = @($r['rows'] | Where-Object { $_['status'] -ne 'xong' })
        if (-not $con.Count) { Write-Output 'HET VIEC: moi hang da xong'; exit 5 }
        $nguoi = @($con | Where-Object { $_['loai'] -eq 'nguoi' -or $_['status'] -eq 'bi-chan' } | ForEach-Object { $_['id'] + ' ' + $_['noiDung'] })
        Write-Output ('CHO NGUOI / BI CHAN: ' + ($nguoi -join ' ; '))
        exit 3
    }
    'start' {
        Can 'Id' $Id
        $h = Hang $r $Id
        if ($h['loai'] -eq 'nguoi') { Write-Output "LOI: $Id la viec cua NGUOI — dung 'done' khi nguoi da lam"; exit 6 }
        if ($h['status'] -eq 'xong') { Write-Output "LOI: $Id da xong"; exit 1 }
        $d = PhuThuocXong $r $h
        if ($d -and -not $Force) { Write-Output "BLOCKED: phu thuoc $d chua xong"; exit 2 }
        $khac = @($r['rows'] | Where-Object { $_['status'] -eq 'dang-lam' -and $_['id'] -ne $Id })
        if ($khac.Count -and -not $Force) { Write-Output ("BLOCKED: {0} dang lam — dong no truoc (done/dodang/release)" -f $khac[0]['id']); exit 4 }
        if (-not $Session) { $Session = SoPhienKe $r }
        $h['status'] = 'dang-lam'
        if (-not (@($h['phien']) -contains $Session)) { $h['phien'] = @(@($h['phien']) | Where-Object { $_ }) + $Session }
        $h['batDau'] = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
        Ghi-Reg $r; Sync $r
        Write-Output ("OK: {0} dang lam (phien {1}) — {2}" -f $Id, $Session, $h['noiDung'])
        Write-Output ("PHIEN={0}" -f $Session)
        exit 0
    }
    'done' {
        Can 'Id' $Id; Can 'Evidence' $Evidence; KiemHandover
        $h = Hang $r $Id
        if ($h['loai'] -ne 'nguoi' -and $h['status'] -notin @('dang-lam', 'do-dang')) { Write-Output "LOI: $Id chua 'start' (dang: $($h['status']))"; exit 1 }
        $h['status'] = 'xong'; $h['evidence'] = $Evidence; $h['ghiChu'] = $null
        if ($Handover) { $h['handover'] = $Handover }
        $h['xongLuc'] = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
        Ghi-Reg $r; Sync $r
        Write-Output "OK: $Id xong"
        exit 0
    }
    'dodang' {
        Can 'Id' $Id; Can 'Reason' $Reason; KiemHandover
        $h = Hang $r $Id
        if ($h['status'] -ne 'dang-lam') { Write-Output "LOI: $Id khong o trang thai dang-lam"; exit 1 }
        $h['status'] = 'do-dang'; $h['ghiChu'] = $Reason
        if ($Handover) { $h['handover'] = $Handover }
        Ghi-Reg $r; Sync $r
        Write-Output "OK: $Id do dang — phien sau /chay-phien se lam tiep"
        exit 0
    }
    'blocked' {
        Can 'Id' $Id; Can 'Reason' $Reason
        $h = Hang $r $Id
        $h['status'] = 'bi-chan'; $h['ghiChu'] = $Reason
        Ghi-Reg $r; Sync $r
        Write-Output "OK: $Id bi chan"
        exit 0
    }
    'release' {
        Can 'Id' $Id
        $h = Hang $r $Id
        if ($h['status'] -eq 'dang-lam' -and -not $Force) { Write-Output "BLOCKED: $Id dang-lam (co the phien khac dang chay) — khong release; that su bo hang thi them -Force"; exit 4 }
        $h['status'] = 'chua'; $h['ghiChu'] = $Reason
        Ghi-Reg $r; Sync $r
        Write-Output "OK: $Id tra ve chua bat dau"
        exit 0
    }
    'doisoat' {
        Can 'Id' $Id
        $h = Hang $r $Id
        if ($h['loai'] -ne 'doc-gate') { Write-Output "LOI: doisoat chi cho hang doc-gate"; exit 1 }
        $hdDir = Join-Path $Root 'docs/nhanh'
        $hd = Get-ChildItem -LiteralPath $hdDir -Filter 'handover-n*.md' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^handover-n(\d+)\.md$' } |
            Sort-Object { [int]([regex]::Match($_.Name, '\d+').Value) } | Select-Object -Last 1
        $muc2 = ''
        if ($hd) {
            $t = [IO.File]::ReadAllText($hd.FullName, $utf8) -replace "`r`n", "`n"
            $m = [regex]::Match($t, '(?ms)^## 2\..*?(?=^## 3\.|\z)')
            if ($m.Success) { $muc2 = $m.Value }
            Write-Output ("handover moi nhat: docs/nhanh/{0}" -f $hd.Name)
        }
        else { Write-Output 'handover moi nhat: (khong co)' }
        $tot = $true; $bc = @()
        foreach ($ma in @($h['ma'])) {
            $dr = Join-Path $Root ("docs/reviews/{0}-docreview.md" -f $ma)
            $v = '(chua co docreview)'
            if (Test-Path -LiteralPath $dr) {
                $vm = [regex]::Matches([IO.File]::ReadAllText($dr, $utf8), 'VERDICT:\s*\**\s*`?([A-Z-]+)')
                if ($vm.Count) { $v = $vm[$vm.Count - 1].Groups[1].Value }
            }
            $nhac = $muc2 -match [regex]::Escape($ma)
            if ($v -ne 'DOC-OK' -or $nhac) { $tot = $false }
            $bc += "$ma $v"
            Write-Output ("  {0,-9} docreview={1,-12} handover§2-con-nhac={2}" -f $ma, $v, $(if ($nhac) { 'CO' } else { 'khong' }))
        }
        if ($tot) { Write-Output ("DE-XUAT: done -Evidence `"{0}; handover {1} §2 khong con viec`"" -f ($bc -join ' · '), $hd.Name); exit 0 }
        Write-Output 'DE-XUAT: lam tiep (start roi /doc-gate phan con lai theo handover §2)'
        exit 1
    }
    'uyquyen' {
        Can 'Id' $Id; Can 'Evidence' $Evidence
        $h = Hang $r $Id
        if ($h['loai'] -ne 'nguoi') { Write-Output "LOI: $Id khong phai hang cua nguoi"; exit 1 }
        if ($h['camUyQuyen']) { Write-Output "LOI: $Id thuoc vung cam (CLAUDE.md §5) — khong uy quyen duoc"; exit 6 }
        $tl = @($r['rows'] | Where-Object { $_['loai'] -eq 'thao-luan' })
        if (-not $tl.Count -or @($tl | Where-Object { $_['status'] -ne 'xong' }).Count) { Write-Output "BLOCKED: phien uy quyen (hang thao-luan) chua xong"; exit 2 }
        $h['loai'] = 'duyet-dec'; $h['vai'] = "ORCH · decider (uỷ quyền $Evidence)"
        $h['ghiChu'] = "uỷ quyền theo $Evidence — chủ dự án xem lại theo lô"
        Ghi-Reg $r; Sync $r
        Write-Output "OK: $Id chuyen sang decider lam thay ($Evidence)"
        exit 0
    }
    'show' {
        Can 'Id' $Id
        $h = Hang $r $Id
        $c = [ordered]@{ chuoi = $r['chuoi']; skill = $r['skill']; nhanhTichHop = $r['nhanhTichHop']; nhanhGocChuoi = $r['nhanhGoc'] }
        Write-Output ([ordered]@{ hang = $h; chuoi = $c } | ConvertTo-Json -Depth 8 -Compress)
        exit 0
    }
    'sync' { Sync $r; Write-Output "OK: da sinh lai bang trong $Doc"; exit 0 }
    'check' {
        $loi = @()
        $t = if (Test-Path -LiteralPath $docPath) { [IO.File]::ReadAllText($docPath, $utf8) } else { '' }
        $i = $t.IndexOf($batDau); $j = $t.IndexOf($ketThuc)
        if ($i -lt 0 -or $j -lt $i) { $loi += "thieu marker bang trong $Doc" }
        else {
            $hienTai = $t.Substring($i + $batDau.Length, $j - $i - $batDau.Length).Trim() -replace "`r`n", "`n"
            if ($hienTai -ne ((Sinh-Bang $r) -replace "`r`n", "`n")) { $loi += "bang trong $Doc LECH registry — chay: pwsh -File tools/agents/phien.ps1 sync (khong sua tay)" }
        }
        $dl = @($r['rows'] | Where-Object { $_['status'] -eq 'dang-lam' })
        if ($dl.Count -gt 1) { $loi += ("nhieu hang dang-lam: " + (($dl | ForEach-Object { $_['id'] }) -join ', ')) }
        foreach ($h in $r['rows']) { if ($h['status'] -eq 'xong' -and -not $h['evidence']) { $loi += "$($h['id']) xong nhung thieu bang chung" } }
        foreach ($l in $loi) { Write-Output "LOI  $l" }
        if ($loi.Count) { exit 1 }
        Write-Output 'OK: bang khop registry'
        exit 0
    }
}
