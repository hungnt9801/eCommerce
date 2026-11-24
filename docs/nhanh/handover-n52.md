# Handover N52 — WP-14 · hàng P-11 (`LOY-001` v0.7, `DOC-CHANGES` vòng 3, `D-164…D-167`)

> Phiên `N52` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N51`; `PRO-002` đã `DOC-OK` ở N51, phiên này làm **`LOY-001`** (phần còn lại của hàng `P-11`).

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `check-feature-doc.ps1 -Path …/loy-001-diem-thuong.md -WP14` | 0 | `KET QUA: DAT` — 16/16 mục · 24/24 BR có test · **51** TC · 0 câu `Open` |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/LOY-001-docreview.md` | 0 | DAT — `verdict=DOC-CHANGES · P1=1 · P2=2` (vòng 3) |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT — 52 dòng DEC |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết (181 tệp · 1092 đường dẫn backtick) |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 40/40 pass |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **`LOY-001` đối chiếu (BA, v0.2):** sửa §0.2/§0.3 theo code + `D-45`/`D-133` (một cửa `ICustomerLedger`, mốc `Delivered`); thêm `TC-24…TC-28` phủ `BR-013/014/016/017/018`; thêm mục "Đối chiếu mô hình Nhanh"; thêm `Q9`.
- **7 câu D1q** (hồ sơ uỷ quyền HIỆU LỰC): `Q9` → **`TIEN_LE` `D-45` ① / `D-133`** (một cửa, sổ lô chỉ là sổ phụ FIFO) · `Q1` → **`D-164`** (lô tiêu mồ côi: Sales `void` trong `catch` + job chỉ void khi `IOrderExistenceProbe` chứng minh đơn không tồn tại, fail-closed, auto-void mặc định TẮT) · `Q2` → **`D-165`** (công thức thu hồi luỹ kế `floor2(E×V/T) − floor2(E×V_trước/T)`, `V>T` ⇒ NÉM `0015`) · `Q3/Q4/Q5` → **`TIEN_LE` `D-114` P13** (không phân kênh; không hiện nợ điểm cho khách; mặc định không hết hạn, `PointExpiryDays` nullable) · `Q6` → **`TIEN_LE` `D-114` P1** (ngưỡng duyệt điều chỉnh từ Setting, mặc định trống = mọi điều chỉnh phải duyệt).
- **`Q10` → `D-166`** (hạch toán điểm đã tiêu = **giảm trừ doanh thu**, kỳ `Delivered`, `LoyaltyDiscount` dòng giảm riêng, phân loại qua `AccAccountMappings`) · **`Q11` → `D-167`** (trả hàng: Sales gọi cổng `GhiNhanDaoDiemTraHangAsync` trong UoW đóng phiếu; giảm lô = `DiemDaDao` thực, lô gốc trước rồi FIFO; giữ NFR-003).
- **Dây chuyền DOCREV:** vòng 1 `DOC-CHANGES` (1 P1 · 5 P2 · 3 P3) → BA sửa P1/P2 (v0.5) → vòng 2 `DOC-CHANGES` (1 P1 · 1 P2 · 6 P3 — P1/P2 **mới** do `D-167` áp giữa 2 vòng) → BA sửa (v0.7) → vòng 3 kiểm nhanh `DOC-CHANGES` (1 P1 · 2 P2 · 0 P3 — P1 **mới**, chéo module POS).
- Ghi **`docs/agents/doc-gate-2026-11-24.md`** (quá trần + việc cần + CR chéo) và thêm hàng `LOY-001` vào `docs/agents/nhap-bien-ban-gd3-b.md`.

## 2. Chưa xong / cắt giữa

- **`LOY-001` CHƯA `DOC-OK`** — dừng ở `/doc-gate` §3 (3 vòng liên tiếp `DOC-CHANGES`, P1 vượt phạm vi WP-14). Chi tiết + việc cần: `docs/agents/doc-gate-2026-11-24.md`. Còn:
  1. **P1 vòng 3 (chéo POS):** POS ghi điểm không tạo lô (`RetailBillAppService.cs:262–268`, `RetailReturnAppService.cs:183–188`) ⇒ phải chốt POS đi cổng Loyalty hay backfill lô + giới hạn NFR-003.
  2. **P2 vòng 3 #2:** `D-167` trong `decisions.md` còn 7 tham số (thiếu `noGiam`) trong khi tài liệu v0.7 đã 8.
  3. **P2 vòng 3 #3:** §10 gọi `Customer.Points` là "số dư khả dụng" trái §5.2/§6.1.
  4. **P3 #3–#8** (vòng 2) để người duyệt.
- **CR chéo** (chủ khác, không sửa ở WP-14): `SAL-006` BR-023/§7.1 (gọi cổng thay vì thẳng) · `Customers` `ICustomerLedger.GhiNhanTraHangAsync` trả `Task<KetQuaDaoKhach>` (`G8`) · `ACC-001` `LoyaltyDiscount` (`D-166`) · POS (P1).

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

- **TẠM CHỐT (DEC, uỷ quyền `D-114`, phiên N52):** `D-164` (Q1 lô tiêu mồ côi) · `D-165` (Q2 công thức thu hồi) · `D-166` (Q10 hạch toán điểm) · `D-167` (Q11 cổng đảo điểm trả hàng). Mỗi dòng có cách lùi + hạn xem lại (`release`, M4 kế toán) ở `decisions.md` + `docs/agents/hang-doi-xem-lai.md`.
- **`TIEN_LE` (không tạo `D-xx`):** `Q9` = `D-45` ①/`D-133`; `Q3/Q4/Q5` = `D-114` P13; `Q6` = `D-114` P1.

## 5. Nợ / rủi ro

- **P1 chéo POS** (ở §2) — cần `decider` rồi BA + DOCREV vòng kế; có thể cần CR sang module POS.
- **P2 #2:** lệch `D-167` (7 vs 8 tham số) — người code theo `decisions.md` sẽ lặp lỗi; `decider` cập nhật hoặc thêm `D-168`.
- **P2 #3, P3 #3–#8:** sửa rẻ cùng lượt P1 nếu được.
- **SQL §11 chưa chạy trên PostgreSQL thật** (trigger `LoyLedgerEntries_AppendOnly`, `EXCLUDE`, `CHECK`) — không có `psql`/docker; ghi DoD GĐ4.
- **Bất biến `pointsFromLots == Customer.Points`** phụ thuộc quyết định POS (P1).
- **Chờ người:** **không có** câu `CHUYEN_NGUOI`; mọi câu `needs = DEC | ke-toan` đã được decider tạm chốt theo hồ sơ `D-114`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG `P-11` → mở lại `P-11`, tiếp `LOY-001`.**

- Đọc `docs/agents/doc-gate-2026-11-24.md` (việc cần + CR chéo).
- Trình tự đề xuất: `decider` quyết **P1 POS** + cập nhật `D-167` (8 tham số) → BA sửa P1/P2/P3 → `check-feature-doc` → DOCREV **vòng 4 kiểm nhanh** (chỉ mục đã đổi) → D3 → `DOC-OK`.
- Lệnh: `/chay-phien` (mở phiên N53).

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; commit đóng phiên `b2a0031` (**đã push**). `main` = `be301c5`. Chưa PR.

---

**Hàng rào agent** (phiên N52): `ba` ×6 (`a1e8a109`, `a663cab2`, `ab7c4924`, `ad1d34e6`, `aaa68680`, `a0a1dae2` — 2 lượt `a1e8a109`/`a663cab2` chạm trần 80 lượt, tiếp qua `SendMessage` cùng agent) · `decider` ×9 (`a36c0dbf`, `a321640b`, `a9d5cf7e`, `a33d5bf5`, `aa8a9d92`, `af1b9f61`, `a8623468`, `ae671db0`, `a1a96c96`) · `docrev` ×3 (`a7e353e0`, `a5d5c6de`, `a52b2c07`). Cảnh báo `canh-bao.jsonl`: các lượt thiếu khối JSON cuối (hạn chế đã biết của Claude Code; `scopes.json` `chanDungKhiThieuJson: false`); công thật `check-agent-artifact` đều **exit 0**. **0** lần ghi ngoài vùng.
