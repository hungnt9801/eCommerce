# Handover `N33` — WP-14: `P-06` (`REC-003`) `DOC-OK` vòng 2, còn 2 P2

> Phiên `N33` (lệnh `/chay-phien P-06`, 20/11/2026). Nhánh `docs/n22-wp14-dot-a`. WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/reconciliation/rec-003-… -WP14` | 0 | 16/16 mục, 16/16 BR có test, mục đối chiếu Nhanh có, 12 dòng `Open`, 0 TODO |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/REC-003-docreview.md` | 0 | `DOC-OK`, P1=0, P2=2, P3=9 (vòng 2) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md -Id D-121…D-127` | 0 | 7 dòng đạt |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | — | không đọc được số `# pass/# fail` (grep rỗng); chưa đo lại |

`REC-003` v0.5 (≈188 KB, 1152 dòng, 44 TC, 16 BR): BA ×4 (đối chiếu; áp `D-121…D-124`; sửa 2 P1 + 14 P2 + P3 vòng 1; áp `D-125…D-127`) → decider ×7 → DOCREV ×2 (`DOC-CHANGES` 2 P1/14 P2 → `DOC-OK`). D3: dòng nháp trong `docs/agents/nhap-bien-ban-gd3-a.md`. D4: link + mermaid xanh.

## 2. Chưa xong / cắt giữa

- `P-06` đạt DoD của hàng. Còn 2 P2 của DOCREV vòng 2 (nên sửa trước G1/G2, không bắt buộc cho `DOC-OK`): (1) dòng `OutOfPeriod` khớp tiền phát `CodLineMismatchedEto`, ShippingHub chỉ nghe `Matched` ⇒ `ShpCodRecords` kẹt `Pending` — cần câu DEC chọn-một; (2) loại trừ `MissingInFile` không loại batch cùng phiên đang bị thay thế (A2) + thứ tự `Superseded`.
- `Q18` (chỉ phát `CodLineResolvedEto` khi batch còn hiệu lực) còn `Open`, DEC chưa chốt, không chặn DoD.
- `P-04` (`SHP-002` xong; còn `SHP-004` adapter GHTK) vẫn 🟨. `P-07` (`SAL-006`) ⬜.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

| D-xx | Trạng thái | Nội dung |
|---|---|---|
| `D-121` | TẠM CHỐT (DEC) | Q11: bảng riêng `RecCodDiscrepancies` theo khuôn `PaymentDiscrepancy`; 0 sửa REC-001 |
| `D-122` | TẠM CHỐT (DEC) | Q12: mã lỗi `eCommerce:Recon:0301–0307` (dải trăm theo feature) |
| `D-123` | TẠM CHỐT (DEC) | Q13: mỗi dòng một ETO `CodLine*Eto`; Sales tự ghi trục 3; REC-003 không gọi Sales/ShippingHub (độ tin cậy mục (4) THẤP) |
| `D-124` | TẠM CHỐT (DEC) | Q14: ô tiền có phần lẻ ⇒ lỗi dòng `CoPhanLe`, không làm tròn |
| `D-125` | TẠM CHỐT (DEC) | Q15: kỳ nullable trên batch; snapshot `MissingInFile`/`OutOfPeriod` chỉ khi có kỳ |
| `D-126` | TẠM CHỐT (DEC) | Q16: vị từ `PhatMismatchedEto()`; `MissingInFile` và Matched+`LedgerChangedAfterImport` không phát `CodLine*Eto` |
| `D-127` | TẠM CHỐT (DEC) | Q17: handler Sales đi `Mismatched → AwaitingReconcile → Matched` một UoW |

Người chốt: không có trong phiên (hồ sơ uỷ quyền `D-114` HIỆU LỰC).

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | 2 P2 của `REC-003` (§2) + 9 P3 trong docreview; chốt `Q18` | BA / DEC |
| 2 | `SHP-003`: `§3.3/§5.2/§7` theo `D-123` (cột `ReconLineId`/`ReconMatchedAtUtc`), hợp đồng đọc sổ COD theo kỳ (`D-125`) — **chưa sửa** (ngoài mã của hàng) | BA `SHP-003` |
| 3 | `SAL-003`: cạnh `Matched → Mismatched`, cạnh ra từ `Resolved` (`D-123`/`D-127`) | BA `SAL-003` |
| 4 | `REC-001`/`REC-002`: mã `Recon:*` (`D-122`); hàng đợi lệch xuyên luồng (`D-121`) | BA `REC-001/002` |
| 5 | SQL §5.3 (CHECK, UNIQUE một phần, `NULLS NOT DISTINCT`) chưa chạy — không có `psql`/docker | QA/BE |
| 6 | `hooks.test.mjs` chưa đo số ca | ORCH phiên sau |
| 7 | Tệp ngoài phiên **không** do vai/phiên này ghi, không add vào commit: `tools/agents/phien.ps1`, `tools/agents/test-phien.ps1`, `tools/agents/chay-tu-dong.ps1`, `tools/agents/test-chay-tu-dong.ps1`, `docs/agents/huong-dan-chay-tu-dong.md` (hook gán nhầm cho docrev/ba; đã kiểm transcript: không vai nào đụng) | chủ dự án |
| 8 | Hook báo "thiếu khối JSON cuối" cho decider Q11 dù báo cáo có khối JSON — cảnh báo giả, ghi lại | ORCH |

**Chờ người:** xem bảng ở câu trả lời cuối.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`P-04` còn `SHP-004`; rồi `P-07`).

1. Mở cửa sổ mới, chạy một phiên một lúc.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1`; xem câu trả lời cuối về việc đã push chưa. Chưa PR.

---

**Hàng rào agent**

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ORCH | phiên hội thoại | — |
| `ba` | 4 | 0 lần ghi ngoài vùng do chính vai |
| `decider` | 7 | `D-121…D-127`, tất cả `TAM_CHOT` |
| `docrev` | 2 | vòng 1 `DOC-CHANGES`, vòng 2 `DOC-OK`; chạm hạn mức 5 giờ 1 lần, nối lại bằng `SendMessage` |
| Cảnh báo ngoài vùng | 2 | đều là tệp song song ngoài phiên (nợ #7), không `git restore` |
