# Sinh file Excel nhập kho từ template của Nhanh (giữ nguyên cấu trúc workbook, chỉ thay sheet "Nhập kho").
# Dùng: powershell -File tools/tao-file-nhap-kho.ps1
#
# Vì sao làm thế này: thêm 100 dòng bằng UI rất dễ trượt gợi ý (đã thử, ~50% lỗi).
# File Excel là đường chính thức của Nhanh -> một lần là xong, có kiểm tra trước khi lưu.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
$tpl  = Join-Path $PSScriptRoot '_mau\nhap-kho.xlsm'
$out  = Join-Path $PSScriptRoot '_mau\nhap-kho-filled.xlsx'

$catalog = Get-Content (Join-Path $PSScriptRoot '_woo-catalog.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$imported = (Get-Content (Join-Path $PSScriptRoot '_da-nhap.json') -Raw -Encoding UTF8 | ConvertFrom-Json).done
$stocked = @{}
$stPath = Join-Path $PSScriptRoot '_phieu-nhap.json'
if (Test-Path $stPath) {
  $st = Get-Content $stPath -Raw -Encoding UTF8 | ConvertFrom-Json
  foreach ($p in $st.stocked.PSObject.Properties) { $stocked[$p.Name] = $true }
}

# sản phẩm cần nhập kho: đã có trong Nhanh, có giá, chưa có tồn
$rows = @()
foreach ($p in $catalog.products) {
  if (-not $imported.PSObject.Properties[$p.sku]) { continue }
  if ($stocked.ContainsKey($p.sku)) { continue }
  if (-not $p.price -or $p.price -le 0) { continue }
  $rows += $p
}
if ($rows.Count -eq 0) { Write-Host 'Không còn sản phẩm nào cần nhập kho.'; exit 0 }

function Esc([string]$s) {
  if ($null -eq $s) { return '' }
  return $s.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;').Replace('"','&quot;')
}

function QtyFor([string]$sku) {
  $h = 0
  foreach ($ch in $sku.ToCharArray()) { $h = ($h * 31 + [int]$ch) % 97 }
  return 30 + ($h % 61)   # 30..90
}

$sb = [System.Text.StringBuilder]::new()
$r = 2
foreach ($p in $rows) {
  $qty = QtyFor $p.sku
  $price = [math]::Max(1000, [math]::Round(($p.price * 0.7) / 500) * 500)
  $name = Esc $p.name
  $sku  = Esc $p.sku
  [void]$sb.Append("<row r=""$r"" spans=""1:12"">")
  [void]$sb.Append("<c r=""B$r"" t=""inlineStr""><is><t>$sku</t></is></c>")
  [void]$sb.Append("<c r=""D$r"" t=""inlineStr""><is><t>$name</t></is></c>")
  [void]$sb.Append("<c r=""E$r""><v>$qty</v></c>")
  [void]$sb.Append("<c r=""F$r""><v>$price</v></c>")
  [void]$sb.Append("<c r=""G$r""><v>0</v></c>")
  [void]$sb.Append("<c r=""H$r""><v>0</v></c>")
  [void]$sb.Append("</row>")
  $r++
}
$lastRow = $r - 1

# đọc sheet gốc
Copy-Item $tpl $out -Force
$zip = [System.IO.Compression.ZipFile]::Open($out, 'Update')
try {
  $entry = $zip.GetEntry('xl/worksheets/sheet2.xml')
  $sr = New-Object System.IO.StreamReader($entry.Open())
  $xml = $sr.ReadToEnd(); $sr.Close()

  $header = [regex]::Match($xml, '<row r="1".*?</row>').Value
  if (-not $header) { throw 'không tìm thấy dòng tiêu đề trong sheet Nhập kho' }

  $newData = "<sheetData>$header$($sb.ToString())</sheetData>"
  $xml = [regex]::Replace($xml, '<sheetData>.*?</sheetData>', $newData)
  $xml = [regex]::Replace($xml, '<dimension ref="[^"]*"/>', "<dimension ref=""A1:L$lastRow""/>")

  $entry.Delete()
  $new = $zip.CreateEntry('xl/worksheets/sheet2.xml')
  $sw = New-Object System.IO.StreamWriter($new.Open(), (New-Object System.Text.UTF8Encoding($false)))
  $sw.Write($xml); $sw.Close()
}
finally { $zip.Dispose() }

Write-Host "Đã tạo: $out"
Write-Host "Số dòng dữ liệu: $($rows.Count)"
$rows | Select-Object -First 3 | ForEach-Object { Write-Host ("  {0} | {1} | {2}" -f $_.sku, $_.name, $_.price) }
