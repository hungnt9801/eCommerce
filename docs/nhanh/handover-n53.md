# Handover N53 — WP-14 · hàng P-11 (`LOY-001` v0.8 `DOC-OK`, `D-168`)

> Phiên `N53` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N52`. Hàng `P-11` (`PRO-002` + `LOY-001`) **đạt DoD và đóng**; `PRO-002` đã `DOC-OK` ở N51, phiên này làm nốt `LOY-001`.

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/loyalty/loy-001-diem-thuong.md -WP14` | 0 | `KET QUA: DAT` — 16/16 mục · 24/24 BR có test · 0 câu `Open`/TODO · mục đối chiếu Nhanh dòng 925 |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/LOY-001-docreview.md` | 0 | DAT — `verdict=DOC-OK · P1=0 · P2=1` (vòng 4 kiểm nhanh) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT — 53 dòng DEC |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết (184 tệp · 845 link · 1101 đường dẫn backtick) |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối · 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 40/40 pass |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **Baseline đầu phiên (N53):** link PASS (0 chết) · mermaid 211/0 · hooks 40/40 · `PH check` OK.
- **`D-168` — P1 chéo POS (decider, TẠM CHỐT, uỷ quyền `D-114`):** cả **3** đường POS (bán `RetailBillAppService.cs:262-268`, huỷ `:323-329`, trả `RetailReturnAppService.cs:183-188`) đi cổng `ILoyaltyEarnGateway` trong cùng UoW; điểm do server tính theo `LoyProgramRules` (**bỏ tin** `PointsEarned`/`PointsUsed`/`PointsReversed` của client); `modules/Pos/**` còn 0 lời gọi thẳng `ICustomerLedger`; thêm cột nullable `LoyRedemptions.RetailBillId` + unique; **không** nới `NFR-003`/`§8.1 #2`/`TC-49` (cấm lô "không hết hạn"); CR POS là nợ có chủ (arch `Pos`), phải merge trước/cùng đợt release với migration Loyalty đầu tiên.
- **`D-167` cập nhật (P2 #2):** chữ ký cổng 8 tham số có `noGiam`; Loyalty chuyển **nguyên** `noGiam` xuống `ICustomerLedger.GhiNhanTraHangAsync` (5 tham số, `IDataScopeResolver.cs:86`, trả `Task<bool>`; kế hoạch `KetQuaDaoKhach` theo G8). Giữ TẠM CHỐT.
- **BA v0.8:** áp `D-168` (P1) + đóng P2 #3 (§10 tách "điểm tích luỹ" = `Customer.Points` ↔ "số dư khả dụng" = `maxRedeemable`) + 6 P3 (#3–#8: `GhiNhanBanHangAsync` 6 tham số, `StockLedger.cs:260-262`, `0015` đủ 3 điều kiện, KeyPrefix 222/237, 19 nhóm permission, `Forbidden` 219-222). Thêm `TC-52` (kiến trúc quét `modules/Pos/**`), `TC-53` (Integration POS bán/tiêu/huỷ/trả), mở rộng `TC-49`; `Q12` ⇒ `Closed (D-168)`; `G9`.
- **DOCREV vòng 4 kiểm nhanh: `DOC-OK`** — 0 P1 · 1 P2 · 2 P3; xác nhận 3 phát hiện vòng 3 đã đóng.
- **D3:** cập nhật dòng `LOY-001` trong `docs/agents/nhap-bien-ban-gd3-b.md` (3/3 tài liệu đợt B `DOC-OK`); ghi mục "Đã giải quyết — phiên N53" vào `docs/agents/doc-gate-2026-11-24.md`.

## 2. Chưa xong / cắt giữa

Không có — hàng `P-11` đạt DoD. Việc còn lại (không chặn DoD, để **người duyệt** xử lý cùng WP code):

1. **P2 (DOCREV vòng 4):** §11 danh sách unique index thiếu `LoyRedemptions(TenantId, RetailBillId) WHERE RetailBillId IS NOT NULL` và `(TenantId, OrderId)` mà `D-168` (3) và §5.2 đã định nghĩa — nên sửa **trước** khi viết migration Loyalty.
2. **P3 #1:** §7.1 ghi "6 biên vào" nhưng bảng có 7 hàng (thêm hàng Pos) ⇒ đổi 6→7.
3. **P3 #2:** trích `PosDtos.cs:35,122` cho `PointsEarned`/`PointsUsed`; thực tế `PointsEarned` ở 35/122, `PointsUsed` ở 33/119 ⇒ trích đủ 33,35,119,122.
4. **CR chéo bắt buộc** (chủ khác, không sửa ở WP-14): module **Pos** (`D-168` — 3 đường gọi + DTO + UI, arch `Pos`) · **`SAL-006`** BR-023/§7.1:318 gọi cổng `D-167` · **`Customers`** đổi `ICustomerLedger.GhiNhanTraHangAsync` trả `Task<KetQuaDaoKhach>` (`G8`, `CUS-001` xác nhận) · **`ACC-001`** `LoyaltyDiscount` (`D-166`).

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

- **TẠM CHỐT (DEC, uỷ quyền `D-114`, phiên N53):** `D-168` (P1 chéo POS — POS đi cổng Loyalty; 3 đường bán/huỷ/trả; bỏ tin điểm client; `RetailBillId`; giữ `NFR-003`/`TC-49`; TC-52/TC-53). Có cách lùi + hạn xem lại (`release`, M4 kế toán) ở `decisions.md` + `docs/agents/hang-doi-xem-lai.md`.
- **Cập nhật dòng TẠM CHỐT `D-167`** (không tạo `D-xx` mới): 8 tham số có `noGiam`; chuyển nguyên `noGiam` xuống `ICustomerLedger` (5 tham số); ghi ngày 24/11/2026, phiên N53.
- **`TIEN_LE` (không tạo `D-xx`):** không có mới trong phiên này.

## 5. Nợ / rủi ro

- **P2 §11** (thiếu unique index trong danh sách) — người viết migration Loyalty theo §11 sẽ bỏ sót unique chống trùng lô tiêu POS; sửa trước WP code.
- **CR POS** (`D-168`) là điều kiện release: chưa có CR POS thì **không** bật Loyalty ở môi trường có POS bán. Chủ nợ: arch `Pos` + BA `POS` (chưa có tài liệu POS).
- **`D-167` cần `KetQuaDaoKhach`** — chữ ký `ICustomerLedger.GhiNhanTraHangAsync` hôm nay trả `Task<bool>`; đổi là additive nhưng cần `CUS-001` xác nhận.
- **SQL/trigger/CHECK/EXCLUDE chưa chạy trên PostgreSQL thật** (module Loyalty chưa có trong code, không có `psql`/docker) — ghi DoD GĐ4; `TC-49`/`TC-52`/`TC-53` là kế hoạch.
- **Chờ người:** **không có** câu `CHUYEN_NGUOI`; mọi câu `needs = DEC | ke-toan` đã được decider tạm chốt theo hồ sơ `D-114`.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `P-11` → mở `P-12`.**

- `P-12` (tong-hop): chuẩn bị hồ sơ duyệt đợt B — `docs/agents/nhap-bien-ban-gd3-b.md` đã đủ 3 dòng `DOC-OK`; rà lại danh sách người review bắt buộc + bảng câu chờ người, rồi đạt DoD `P-12`.
- Trước khi đóng `P-12`, cân nhắc đưa P2 §11 + 2 P3 vào hàng "người duyệt sửa cùng WP code" của biên bản.
- Lệnh: `/chay-phien`.

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; commit đóng phiên N53 = `__COMMIT__` (**đã push**). `main` = `be301c5`. Chưa PR.

---

**Hàng rào agent** (phiên N53): `decider` ×2 (`acb8d40713bf29968` — chạm trần 40 lượt, tiếp qua `SendMessage` cùng agent; `a5e3e967cc71a1541`) · `ba` ×1 (`a53c25df78ed1b2cd`) · `docrev` ×1 (`aabcaf392e9eb4d4d`). Cảnh báo `canh-bao.jsonl`: 1 lượt `ba` thiếu khối JSON cuối (hạn chế đã biết của Claude Code; `scopes.json` `chanDungKhiThieuJson: false`; báo cáo JSON vẫn có trong nội dung); công thật `check-agent-artifact` đều **exit 0**. **0** lần ghi ngoài vùng (`git status` chỉ `docs/**` + `agents/state/**`).
