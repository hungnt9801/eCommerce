# Handover `N31` — WP-14: áp R2 + `D-116`/`D-118` vào `SHP-001` (v0.5.4), `DOC-OK`, còn 1 P2

> Phiên `N31` · 20/11/2026 · nhánh `docs/n22-wp14-dot-a` · lệnh `/chay-phien P-03`. WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".
> ⚠️ Phiên `N30` chạy **song song** trên cùng cây làm việc (sửa `shp-002`, `shp-003`, `SHP-002-docreview`, `agents/state/phien-wp14.json`, bảng phiên). Phiên này **không** đụng và **không** commit các tệp đó.

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/shipping/shp-001-… -WP14` | 0 | 16/16 mục, 24/24 BR có test, 57 TC, 4 dòng `Open`, 0 TODO |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/SHP-001-docreview.md` | 0 | `DOC-OK`, P1=0, P2=1 |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision` | 0 | `D-116`, `D-118` đạt |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `pwsh -File tools/agents/phien.ps1 check` | 0 | bảng khớp registry |

Việc đã làm cho `SHP-001`: BA áp R2 `D-114` (Q1–Q20, v0.5.2), `D-116` (Q5b làm tròn cân, Q7b một `IShipmentAppService`, Q9 TTL 300 s; v0.5.3), `D-118` (TC-53/TC-19 danh sách đóng, v0.5.4); mã lỗi dải `0101`–`0112`; DOCREV kiểm nhanh 3 lượt → `DOC-OK` 0 P1 · 1 P2 · 20 P3. Nháp biên bản đợt A đã cập nhật dòng `SHP-001`.

## 2. Chưa xong / cắt giữa

- **P2 #45** (`SHP-001` §6.1 dòng ~488, BR-024): quy tắc ghép khoá cache không có dấu phân cách ⇒ hai request khác nhau cùng khoá (cân 1250 + COD 250000 va cân 12502 + COD 50000). Cần BA sửa (dạng ghép không va: phân cách có thoát / tiền tố độ dài / JSON rồi băm) + thêm ca va vào TC-56, rồi DOCREV kiểm nhanh. Đã chạm trần làm lại của `ba` cho mã này ⇒ để phiên sau / người duyệt.
- P3 mới #42, #44, #46–#48: #47 `shp-004` §6.1 khai lại `CreateShipmentAsync`/`CancelShipmentAsync` ⇒ TC-53(b) đỏ ở WP code SHP-004; #48 `shp-003` §6.1 thiếu câu trỏ `D-118`: "`IShipmentCodAppService` là interface thứ hai được `TC-53` của `SHP-001` cho phép theo `D-118`; không khai `CreateShipmentAsync`, `CancelShipmentAsync`, `EstimateOrderFeeAsync`, `EstimateCartFeeAsync`." — **chờ phiên `N30` xong `shp-003`/`shp-004`** rồi BA thêm.
- Bảng phiên: `P-03` đang `xong` (đóng từ N29 theo `doisoat`) dù R2 chỉ áp xong ở phiên này — phiên này **không** sửa registry vì `N30` đang giữ nó.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

`D-116` (Q5b/Q7b/Q9, `TẠM CHỐT (DEC)`), `D-118` (làm rõ `D-116 ⑥`, `TẠM CHỐT (DEC)`). `D-117` do phiên `N30` ghi (nằm chung `decisions.md`). Cả hai có dòng ở `docs/agents/hang-doi-xem-lai.md`.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | P2 #45 (trên) | BA, phiên sau |
| 2 | TTL 300 s chưa đo; Q5b/Q9 xem lại trước release | DEC/vận hành |
| 3 | Hook `bao-orch` báo "ghi ngoài vùng" cho tệp của phiên song song (false positive) — hai phiên cùng cây làm việc nên tránh | chủ dự án |
| 4 | Nợ §5 của handover-n28/n29 vẫn nguyên | như cũ |

**Chờ người:** không có câu mới thuộc vùng cấm; chỉ cần quyết **một** phiên chạy tại một thời điểm trên nhánh này.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14.** Đối soát với `N30` (xem `git log`, `PH status`), BA sửa #45 + #47/#48, DOCREV kiểm nhanh, rồi `P-04`.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1`; xem câu trả lời cuối về việc đã push chưa. Chưa PR.

---

**Hàng rào agent** (chỉ lượt `N31`)

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ORCH | phiên hội thoại | — |
| `ba` | 3 | v0.5.2, v0.5.3, v0.5.4 |
| `docrev` | 2 giao (+1 lượt cắt do hạn mức, đã nối lại) | `DOC-OK` mỗi lượt |
| `decider` | 2 | `D-116`, `D-118` |
