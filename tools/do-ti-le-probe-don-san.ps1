# ĐO TỈ LỆ FLAKY — chạy `probe-don-san.ps1` N lượt, ghi kết quả từng lượt vào log.
#
# 🔑 VÌ SAO CÓ (D-67 / D-68): `C10` của `probe-don-san` từng **ĐỎ 2/9 lượt trên CI cùng một cây mã**
#    (máy dev KHÔNG tái hiện) ⇒ **không** được kết luận "flaky/không flaky" từ **một** lượt. Công cụ này
#    chạy lặp và in **tỉ lệ**; số neo `[CD-D67]` (do `probe-don-san` in) phân biệt *"đo quá sớm"* (`Late = 1`)
#    với *"ghi hỏng"* (`Late = 0` + `DLQ = 1`) — xem `D-68` §1.4.
#
# CÁCH DÙNG:  pwsh -NoProfile -File .\tools\do-ti-le-probe-don-san.ps1 -SoLuot 10
# 📌 Log từng lượt vào `tools/_tam-kiem/n21/` (tên giữ nguyên để khớp bằng chứng của `N21`/`D-68`);
#    `tools/_*` đã được gitignore ⇒ log **không** vào VCS.
param([int]$SoLuot = 10)

$thuMuc = Join-Path $PSScriptRoot '_tam-kiem\n21'
New-Item -ItemType Directory -Force -Path $thuMuc | Out-Null

$tong = 0
$hong = 0

for ($i = 1; $i -le $SoLuot; $i++) {
    $log = Join-Path $thuMuc ("don-san-lan-{0}.log" -f $i)
    $batDau = Get-Date

    pwsh -NoProfile -File (Join-Path $PSScriptRoot 'probe-don-san.ps1') *> $log
    $ma = $LASTEXITCODE

    $giay = [math]::Round(((Get-Date) - $batDau).TotalSeconds, 1)
    $dongC10 = @(Select-String -Path $log -Pattern '\[C10 ' -SimpleMatch)
    $dongCd = @(Select-String -Path $log -Pattern '[CD-D67]' -SimpleMatch)
    $tongKet = @(Select-String -Path $log -Pattern 'PROBE DON SAN:' -SimpleMatch)

    if ($ma -eq 0) { $tong++ } else { $hong++ }

    Write-Output ("LAN {0}: exit={1} · {2} s · {3} · {4}" -f $i, $ma, $giay,
        ($(if ($dongC10.Count -gt 0) { $dongC10[0].Line.Trim() } else { '(khong thay C10)' })),
        ($(if ($tongKet.Count -gt 0) { $tongKet[0].Line.Trim() } else { '(khong thay tong ket)' })))

    if ($dongCd.Count -gt 0) { Write-Output ("          " + $dongCd[0].Line.Trim()) }
}

Write-Output ("== TONG: {0} luot XANH / {1} luot DO (tong {2}) ==" -f $tong, $hong, $SoLuot)
