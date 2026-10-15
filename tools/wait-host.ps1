#!/usr/bin/env pwsh
<#
.SYNOPSIS
    🚨 **HÀM CHỜ CHUẨN** — chờ tiến trình lên (`/health-status` = 200); **hết hạn thì IN ĐUÔI LOG rồi mới ném lỗi**.

.VÌ SAO CÓ TỆP NÀY (bài học #12 · `web-001` §68.3)
    Bốn vòng liên tiếp (69 · 75 · 77 · 79) hỏng **công cụ ĐO**, không phải sản phẩm. Riêng vòng 79:
    probe `throw` đúng một câu *"Host tại … KHONG len trong 90 giay"* mà **KHÔNG in log** ⇒ **không biết VÌ SAO**
    (*cổng bị chiếm · lỗi khởi động · kết nối DB · thiếu module*…).
    📌 Đó **đúng** lỗi đã ghi ở `inv-001` §27.3 (*"im lặng không phân biệt được với không chạy"*) — nhưng ở
      **chính công cụ đo**. ⇒ **Ghi quy tắc KHÔNG ĐỦ** (bài học Phụ lục C): cần **công cụ** khiến việc **đúng**
      thành việc **dễ nhất**. Đây là công cụ đó — **dùng chung cho MỌI probe**.

.CÁCH DÙNG
    pwsh -File tools/wait-host.ps1 -Url http://localhost:5000 -LogFile "$env:TEMP\host.log"
    pwsh -File tools/wait-host.ps1 -Url http://localhost:5100 -LogFile "$env:TEMP\web.log" -TimeoutSeconds 120 -ProcessId 1234

.NGUYÊN TẮC
    • Chờ bằng **`/health-status` = 200** (không đoán bằng `Start-Sleep` mù).
    • Hết hạn ⇒ **IN 15 DÒNG CUỐI của log** (nếu có) **rồi** ném lỗi có ngữ cảnh ⇒ **thất bại to và CÓ THÔNG TIN**.
    • Nếu truyền `-ProcessId` và tiến trình **đã thoát** ⇒ in **mã thoát** ngay (không chờ vô ích).
#>
param(
    [Parameter(Mandatory = $true)][string]$Url,
    [string]$LogFile = '',
    [int]$TimeoutSeconds = 90,
    [int]$ProcessId = 0
)

$ErrorActionPreference = 'Stop'

function InDuoiLog([string]$duongDan, [int]$soDong = 15) {
    if ([string]::IsNullOrWhiteSpace($duongDan)) { return @('(không có -LogFile để in)') }
    if (-not (Test-Path $duongDan)) { return @("(không tìm thấy log: $duongDan)") }
    return @(Get-Content $duongDan -Encoding utf8 -Tail $soDong -ErrorAction SilentlyContinue)
}

$diemKiemTra = "$($Url.TrimEnd('/'))/health-status"
$soLan = [Math]::Max(1, [int]($TimeoutSeconds / 2))
$batDau = Get-Date

Write-Host "⏳ Chờ $diemKiemTra (tối đa ${TimeoutSeconds}s)…" -ForegroundColor DarkGray

for ($i = 0; $i -lt $soLan; $i++) {

    # Tiến trình đã thoát ⇒ không chờ vô ích (và nói RÕ mã thoát)
    if ($ProcessId -gt 0) {
        $tienTrinh = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($null -eq $tienTrinh) {
            $duoi = InDuoiLog $LogFile
            Write-Host "🚨 Tiến trình PID $ProcessId ĐÃ THOÁT trước khi lên. 15 dòng cuối log:" -ForegroundColor Red
            $duoi | ForEach-Object { Write-Host "    $_" }
            throw "Tiến trình PID $ProcessId thoát trước khi $diemKiemTra trả 200."
        }
    }

    try {
        $traLoi = Invoke-WebRequest $diemKiemTra -UseBasicParsing -TimeoutSec 5 -SkipHttpErrorCheck
        if ([int]$traLoi.StatusCode -eq 200) {
            $giay = [Math]::Round(((Get-Date) - $batDau).TotalSeconds, 1)
            Write-Host "✅ LÊN sau ${giay}s — $diemKiemTra = 200" -ForegroundColor Green
            exit 0
        }
    }
    catch { }

    Start-Sleep -Seconds 2
}

# ── HẾT HẠN: in đuôi log TRƯỚC, rồi ném lỗi CÓ NGỮ CẢNH
$duoiLog = InDuoiLog $LogFile
Write-Host "🚨 HẾT HẠN ${TimeoutSeconds}s — $diemKiemTra KHÔNG trả 200. 15 dòng cuối log ($LogFile):" -ForegroundColor Red
$duoiLog | ForEach-Object { Write-Host "    $_" }

throw "Không chờ được $diemKiemTra trong ${TimeoutSeconds}s. Xem đuôi log ở trên (nếu trống: kiểm -LogFile và đường dẫn)."
