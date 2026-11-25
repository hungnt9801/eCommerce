<#
.SYNOPSIS
    Ca thu cho tools/agents/phien.ps1 (bang phien chay) — chay tren repo gia trong thu muc tam, khong cham repo that.
.EXAMPLE
    pwsh -File tools/agents/test-phien.ps1      # exit 0 = moi ca dat
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'phien.ps1'
$tam = Join-Path ([IO.Path]::GetTempPath()) ('test-phien-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$utf8 = [Text.UTF8Encoding]::new($false)
$dat = 0; $truot = @()

function Ghi([string]$rel, [string]$noiDung) {
    $p = Join-Path $tam $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $p -Parent) | Out-Null
    [IO.File]::WriteAllText($p, $noiDung, $utf8)
}
function Chay([string[]]$a) {
    $out = & pwsh -NoProfile -File $script @a -Root $tam 2>&1 | Out-String
    return @{ ma = $LASTEXITCODE; out = $out }
}
function Ca([string]$ten, [int]$maMongDoi, [string[]]$a, [string]$phaiCo) {
    $k = Chay $a
    $ok = ($k.ma -eq $maMongDoi) -and (-not $phaiCo -or $k.out -match $phaiCo)
    if ($ok) { $script:dat++; Write-Output "  OK    $ten" }
    else { $script:truot += $ten; Write-Output "  TRUOT $ten (exit $($k.ma), mong $maMongDoi)`n$($k.out)" }
}
function Reg { return ([IO.File]::ReadAllText((Join-Path $tam 'agents/state/phien-wp14.json'), $utf8) | ConvertFrom-Json) }
function Kiem([string]$ten, [bool]$dk) { if ($dk) { $script:dat++; Write-Output "  OK    $ten" } else { $script:truot += $ten; Write-Output "  TRUOT $ten" } }

try {
    $row = { param($id, $loai, $ma, $deps, $st) [ordered]@{ id = $id; dot = 'A'; loai = $loai; ma = $ma; noiDung = "noi dung $id"; vai = 'ORCH'; dependsOn = $deps; status = $st; phien = @(); evidence = $null; ghiChu = $null; handover = $null } }
    $reg = [ordered]@{ version = 1; wp = 'WP-14'; rows = @(
            (& $row 'P-01' 'doc-gate' @('AAA-001') @() 'xong'),
            (& $row 'P-02' 'doc-gate' @('AAA-002') @() 'chua'),
            (& $row 'P-03' 'doc-gate' @('AAA-003') @('P-02') 'chua'),
            (& $row 'P-04' 'nguoi' @() @('P-03') 'chua'),
            (& $row 'P-05' 'doc-gate' @('BBB-001') @() 'chua'),
            (& $row 'P-06' 'thao-luan' @() @('P-03') 'chua'),
            (& $row 'P-07' 'nguoi' @() @() 'chua'),
            (& $row 'P-08' 'nguoi' @() @() 'chua')
        )
    }
    $reg.rows[6].camUyQuyen = $true
    $reg.rows[0].evidence = 'exit 0'
    Ghi 'agents/state/phien-wp14.json' ($reg | ConvertTo-Json -Depth 6)
    Ghi 'docs/agents/bang-phien-wp14.md' "# Bang`n`n<!-- BANG-PHIEN:BAT-DAU -->`n<!-- BANG-PHIEN:KET-THUC -->`n`nduoi`n"
    Ghi 'docs/nhanh/handover-n9.md' "# N9`n## 2. Chua xong`n- AAA-002 con ap D-1`n## 3. Migration`nKhong`n"
    Ghi 'docs/nhanh/handover-n10.md' "# N10`n## 2. Chua xong`n- khong con gi; tiep BBB-001`n## 3. Migration`nKhong — AAA-002 nhac ngoai muc 2`n"
    Ghi 'docs/reviews/AAA-002-docreview.md' "# r`n`n**VERDICT: DOC-OK**`n"
    Ghi 'docs/reviews/BBB-001-docreview.md' "# r`n`n**VERDICT: DOC-CHANGES**`n"

    Write-Output 'NHOM 5 — bang phien chay (phien.ps1)'
    Ca 'C1 check bao lech khi bang chua sinh' 1 @('check') 'LECH'
    Ca 'C2 sync roi check xanh' 0 @('sync') 'OK'
    Ca 'C3 check xanh sau sync' 0 @('check') 'khop'
    $doc = [IO.File]::ReadAllText((Join-Path $tam 'docs/agents/bang-phien-wp14.md'), $utf8)
    Kiem 'C4 sync giu noi dung ngoai marker + co 8 hang + nhan ngoi cung' (($doc -match 'duoi') -and (([regex]::Matches($doc, '\| `P-0\d` \|')).Count -eq 8) -and ($doc -match '🗣 Chờ ngồi cùng'))
    Ca 'C5 next = hang chua dau tien du phu thuoc' 0 @('next') '^P-02'
    Ca 'C6 start khong -Session => script tu cap so = max(handover n10) + 1' 0 @('start', '-Id', 'P-02') 'PHIEN=N11'
    Ca 'C7 start hang co phu thuoc chua xong' 2 @('start', '-Id', 'P-03', '-Session', 'N11') 'P-02'
    Ca 'C8 start hang cua nguoi bi tu choi' 6 @('start', '-Id', 'P-04', '-Session', 'N11') 'NGUOI'
    Ca 'C9 start P-02' 0 @('start', '-Id', 'P-02', '-Session', 'N11') 'dang lam'
    Ca 'C10 start hang thu hai khi da co hang dang-lam' 4 @('start', '-Id', 'P-05', '-Session', 'N11') 'P-02'
    Ca 'C11 next uu tien hang dang-lam' 0 @('next') '^P-02'
    Ca 'C12 done thieu bang chung bi tu choi' 1 @('done', '-Id', 'P-02') 'Evidence'
    Ca 'C13 done voi handover khong ton tai bi tu choi' 1 @('done', '-Id', 'P-02', '-Evidence', 'x', '-Handover', 'docs/nhanh/handover-n99.md') 'khong ton tai'
    Ca 'C14 dodang' 0 @('dodang', '-Id', 'P-02', '-Reason', 'het han muc', '-Handover', 'docs/nhanh/handover-n10.md') 'do dang'
    Ca 'C15 next uu tien hang do-dang' 0 @('next') '^P-02'
    Ca 'C16 start lai hang do-dang, phien moi duoc ghi them' 0 @('start', '-Id', 'P-02', '-Session', 'N12') 'dang lam'
    Kiem 'C17 registry ghi ca N11 va N12' ((@((Reg).rows[1].phien) -join ',') -eq 'N11,N12')
    Ca 'C39 release hang dang-lam khong -Force bi tu choi (phien khac co the dang chay)' 4 @('release', '-Id', 'P-02') 'dang-lam'
    Ca 'C18 doisoat: DOC-OK va handover moi nhat §2 khong nhac => de xuat done' 0 @('doisoat', '-Id', 'P-02') 'DE-XUAT: done'
    Ca 'C19 doisoat: DOC-CHANGES => lam tiep' 1 @('doisoat', '-Id', 'P-05') 'lam tiep'
    Ca 'C20 done P-02' 0 @('done', '-Id', 'P-02', '-Evidence', 'DOC-OK 0 P1', '-Handover', 'docs/nhanh/handover-n10.md') 'xong'
    Ca 'C21 next mo khoa P-03 sau khi P-02 xong' 0 @('next') '^P-03'
    Ca 'C22 check xanh, bang co dau Hoan thanh' 0 @('check') 'khop'
    Ca 'C23 blocked can ly do' 1 @('blocked', '-Id', 'P-03') 'Reason'
    Ca 'C24 blocked P-03 + P-05' 0 @('blocked', '-Id', 'P-03', '-Reason', 'cho hang') 'bi chan'
    Ca 'C25 blocked P-05' 0 @('blocked', '-Id', 'P-05', '-Reason', 'cho hang') 'bi chan'
    Ca 'C26 next exit 3 khi chi con viec nguoi / bi chan' 3 @('next') 'CHO NGUOI'
    Ca 'C27 release tra ve chua' 0 @('release', '-Id', 'P-05') 'chua bat dau'
    Ca 'C28 done hang nguoi khong can start' 0 @('done', '-Id', 'P-04', '-Evidence', 'bien ban X') 'xong'
    Ca 'C30 uyquyen truoc khi phien uy quyen xong bi tu choi' 2 @('uyquyen', '-Id', 'P-08', '-Evidence', 'D-1') 'uy quyen'
    Ca 'C31 uyquyen hang vung cam bi tu choi' 6 @('uyquyen', '-Id', 'P-07', '-Evidence', 'D-1') 'vung cam'
    Ca 'C32 uyquyen hang khong phai cua nguoi bi tu choi' 1 @('uyquyen', '-Id', 'P-05', '-Evidence', 'D-1') 'khong phai'
    Ca 'C33 start tu cap so vuot ca cot Phien (handover n10, da dung N11 N12 => N13; -Force vi phu thuoc bi chan)' 0 @('start', '-Id', 'P-06', '-Force') 'PHIEN=N13'
    Ca 'C34 done phien uy quyen' 0 @('done', '-Id', 'P-06', '-Evidence', 'ho so HIEU LUC D-2') 'xong'
    Ca 'C35 uyquyen sau phien uy quyen' 0 @('uyquyen', '-Id', 'P-08', '-Evidence', 'D-2') 'decider'
    Kiem 'C36 hang chuyen sang duyet-dec, vai ghi D-2' (((Reg).rows[7].loai -eq 'duyet-dec') -and ((Reg).rows[7].vai -match 'D-2'))
    Ca 'C37 check xanh sau uyquyen' 0 @('check') 'khop'
    # negative control: sua tay bang => check phai do
    $p = Join-Path $tam 'docs/agents/bang-phien-wp14.md'
    [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p, $utf8) -replace '⬜ Chưa bắt đầu', '✅ Hoàn thành'), $utf8)
    Ca 'C38 negative control: sua tay bang => check do' 1 @('check') 'LECH'

    # ── chuoi code (-Chuoi): registry + bang rieng, prompt theo skill cua chuoi, show tra JSON ──
    $regC = [ordered]@{ version = 1; chuoi = 'gd3-t'; skill = 'chay-phien-code'; nhanhTichHop = 'tich-hop/gd3'; nhanhGoc = 'feat/wp15'; rows = @(
            ([ordered]@{ id = 'W16-1'; dot = 'WP-16'; loai = 'code'; ma = @('PRC-002'); noiDung = 'arch'; vai = 'arch'; dependsOn = @(); status = 'chua'; phien = @(); nhanh = 'feat/gd3-wp16'; buoc = 'F0-F2b'; evidence = $null; ghiChu = $null; handover = $null }),
            ([ordered]@{ id = 'W16-2'; dot = 'WP-16'; loai = 'nguoi'; ma = @(); noiDung = 'nguoi ky'; vai = 'nguoi'; dependsOn = @('W16-1'); status = 'chua'; phien = @(); evidence = $null; ghiChu = $null; handover = $null }),
            ([ordered]@{ id = 'W18-1'; dot = 'WP-18'; loai = 'code'; ma = @('ADDR-001'); noiDung = 'be'; vai = 'be'; dependsOn = @('W16-1'); status = 'chua'; phien = @(); nhanh = 'feat/gd3-wp18'; buoc = 'F3'; evidence = $null; ghiChu = $null; handover = $null })
        )
    }
    Ghi 'agents/state/phien-gd3-t.json' ($regC | ConvertTo-Json -Depth 6)
    Ghi 'docs/agents/bang-phien-gd3-t.md' "# Bang chuoi`n<!-- BANG-PHIEN:BAT-DAU -->`n<!-- BANG-PHIEN:KET-THUC -->`n"
    $wp14Truoc = [IO.File]::ReadAllText((Join-Path $tam 'docs/agents/bang-phien-wp14.md'), $utf8)
    Ca 'C39 -Chuoi sync ghi bang cua chuoi' 0 @('sync', '-Chuoi', 'gd3-t') 'bang-phien-gd3-t'
    $bangC = [IO.File]::ReadAllText((Join-Path $tam 'docs/agents/bang-phien-gd3-t.md'), $utf8)
    Kiem 'C40 prompt dung skill + chuoi cua registry' ($bangC -match '/chay-phien-code chuoi=gd3-t W16-1')
    Kiem 'C41 -Chuoi khong cham bang WP-14' ([IO.File]::ReadAllText((Join-Path $tam 'docs/agents/bang-phien-wp14.md'), $utf8) -eq $wp14Truoc)
    Ca 'C42 next chuoi = hang code dau' 0 @('next', '-Chuoi', 'gd3-t') 'W16-1'
    Ca 'C43 show tra JSON co nhanh + buoc + nhanhTichHop' 0 @('show', '-Chuoi', 'gd3-t', '-Id', 'W18-1') '"nhanh":"feat/gd3-wp18".*"buoc":"F3".*"nhanhTichHop":"tich-hop/gd3"'
    Ca 'C44 start + done hang code' 0 @('start', '-Chuoi', 'gd3-t', '-Id', 'W16-1') 'PHIEN='
    Ca 'C45 done hang code' 0 @('done', '-Chuoi', 'gd3-t', '-Id', 'W16-1', '-Evidence', 'build 0 · test 10/10') 'xong'
    Ca 'C46 next bo qua hang nguoi, chon hang doc lap ke' 0 @('next', '-Chuoi', 'gd3-t') 'W18-1'
    Ca 'C47 -Chuoi sai ky tu => loi' 1 @('next', '-Chuoi', '../x') 'LOI'
    Ca 'C48 check chuoi xanh' 0 @('check', '-Chuoi', 'gd3-t') 'khop'
}
finally { Remove-Item -Recurse -Force $tam -ErrorAction SilentlyContinue }

Write-Output ''
Write-Output ("TONG: {0} dat · {1} truot" -f $dat, $truot.Count)
if ($truot.Count) { exit 1 }
exit 0
