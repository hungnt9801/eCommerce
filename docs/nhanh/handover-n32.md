# Handover `N32` — WP-14: `P-05` (`SHP-003`) `DOC-OK` vòng 2, còn 2 P2

> Phiên mở là `N30` (lệnh `/chay-phien P-05`, 19–20/11/2026); handover đánh số `N32` vì `N31` (phiên song song, `P-03`/`SHP-001`) đã chiếm số `N31`. Nhánh `docs/n22-wp14-dot-a`. WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".
> ⚠️ Hai phiên song song cùng cây làm việc: phiên `P-04` (`SHP-002`, commit `7f3d61b`, cũng ghi mã `N30`) và `N31`. Phiên này **không** đụng `shp-001`, `shp-002`, `SHP-001/002-docreview`. Cảnh báo "ghi ngoài vùng" của hook về các tệp đó là do hai phiên kia, không phải vai của phiên này.

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/shipping/shp-003-… -WP14` | 0 | 16/16 mục, 22/22 BR có test, mục đối chiếu Nhanh có, 7 dòng `Open`, 0 TODO |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/SHP-003-docreview.md` | 0 | `DOC-OK`, P1=0, P2=2, P3=10 (vòng 2) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | `D-117`, `D-120` đạt |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | — | không đọc được số `# pass/# fail` ở bước đóng (grep rỗng); chưa đo lại, xem §5 |

Việc đã làm cho `SHP-003`: BA ×4 (đối chiếu; áp 8 quyết định; sửa 3 P1 + 12 P2 + 11 P3 vòng 1; áp `D-120`) → decider ×4 → DOCREV ×2 (`DOC-CHANGES` 3 P1/12 P2 → `DOC-OK`). Tài liệu v0.5, 60 TC, 22 BR. D3: dòng nháp trong `docs/agents/nhap-bien-ban-gd3-a.md`. D4: link/mermaid xanh. SQL (partial UNIQUE, CHECK) **chưa đo** — máy không có `psql`/Docker.

## 2. Chưa xong / cắt giữa

- `P-05` đạt DoD của hàng (`SHP-003` `DOC-OK` + D3 + D4). Còn 2 P2 của DOCREV vòng 2, nên sửa trước khi người duyệt/trước khi code (không bắt buộc cho `DOC-OK`): (1) mã lỗi nhập OTP sau khi sai đủ số lần — §3.2 #8/#9/#15 ↔ TC-11 (`0302`/`0303`/`0306`); (2) `Unknown` không có lối ra cho kế toán — thiếu method đóng yêu cầu.
- `P-04` (`SHP-002` xong ở `7f3d61b`; còn `SHP-004` adapter GHTK): hàng bị phiên này `release` nhầm để mở `P-05` ⇒ cần đưa về 🟨 (xem §5 #1).
- `P-06…P-07` còn ⬜.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

| D-xx | Trạng thái | Nội dung |
|---|---|---|
| `D-117` | TẠM CHỐT (DEC, uỷ quyền `D-114`) | Q10: Sales tiêu thụ `CodAdjustmentConfirmedEto`, hàm `Order` so-và-đặt; không ghi `OrderStatusHistory`; Sales không phát lại ETO COD |
| `D-120` | TẠM CHỐT (DEC) | Q12–Q14: `RequestKey` bắt buộc ở body (header chỉ bản sao, lệch ⇒ `0315`); `PendingApproval`/`Unknown` không TTL; giữ `0301` riêng |

Q5–Q8, Q11 áp tiền lệ (`D-82`, `D-114`), không `D-xx` mới. Người chốt: không có trong phiên.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | Phiên này đã `phien.ps1 release -Id P-04` để `start P-05` trong lúc phiên `P-04` song song đang làm (đã commit `7f3d61b`). Cần đưa `P-04` về 🟨 `dodang` (đã thử ở cuối phiên — xem câu trả lời cuối) | ORCH phiên sau |
| 2 | 2 P2 của `SHP-003` (§2) + 10 P3 trong docreview | BA |
| 3 | `SHP-003` §6.2: mã `0025`, `QuotaExceeded` do BA đặt ở lượt đầu chưa có `D-xx` (BA vòng sau đã chuyển sang `03xx`, kiểm lại khi sửa 2 P2) | BA / DEC |
| 4 | Hàm `Order` so-và-đặt + mã lỗi Sales (`D-117` N1/N2); `DatVanChuyen` ghi đè `CodAmount` | BA `SAL-003` |
| 5 | CSKH tự huỷ `PendingApproval` (F9) | BA `SHP-003` |
| 6 | `hooks.test.mjs` chưa đo số ca ở bước đóng | ORCH phiên sau |
| 7 | Hai phiên song song cùng cây làm việc ghi chung `phien-wp14.json`/bảng/`decisions.md`/`hang-doi` — dễ đè nhau | chủ dự án: chạy một phiên một lúc |

**Chờ người:** xem bảng ở câu trả lời cuối (Q4 vào R3; xác nhận "người duyệt ≠ người yêu cầu" mặc định tắt; ngưỡng duyệt COD; Q1–Q3/Q9 R3).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`P-04` còn `SHP-004`; rồi `P-06`).

1. Chạy **một** phiên một lúc; mở cửa sổ mới.

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; SHA lấy từ `git log -1`; xem câu trả lời cuối về việc đã push chưa. Chưa PR. Commit phiên này chỉ chứa tệp của `SHP-003` + bảng/registry + `decisions`/`hang-doi`/`07`/nháp biên bản.

---

**Hàng rào agent** (chỉ lượt của phiên này)

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| ORCH | phiên hội thoại | — |
| `ba` | 4 | 0 lần ghi ngoài vùng do chính vai |
| `decider` | 4 | `D-117`, `D-120`; 3 lượt `TIEN_LE`/`CHUYEN_NGUOI` không ghi `D-xx` |
| `docrev` | 2 | vòng 1 `DOC-CHANGES`, vòng 2 `DOC-OK`; chạm hạn mức 5 giờ 2 lần, được nối lại bằng `SendMessage` |
| Cảnh báo ngoài vùng | nhiều | đều là tệp `shp-001`/`shp-002`/`SHP-00x-docreview`/`decisions`/`hang-doi` do phiên song song; không `git restore` để khỏi xoá việc của họ |
