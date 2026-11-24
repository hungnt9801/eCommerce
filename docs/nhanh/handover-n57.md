# Handover N57 — WP-14 · hàng P-14 (doc-gate đợt C: `REP-001` XONG `DOC-OK`, `REP-002` DỞ ở DOCREV vòng 2)

> Phiên `N57` · WP-14 (thuần tài liệu, `D-71`) · nhánh `docs/n22-wp14-dot-a` · chạy bằng `/chay-phien` (bộ chạy tự động). Nối tiếp `N56`.
> Hàng `P-14` (`doc-gate`) **DỞ**: `REP-001` đã `DOC-OK` (vòng 3 kiểm nhanh); `REP-002` chặn ở DOCREV **vòng 2** `DOC-CHANGES` (0 P1 · **5 P2**) — hết trần 2 vòng.

## 1. Đã xong — việc + số đo

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết (**192** tệp · **875** link · **1170** đường dẫn backtick) — *baseline: 191/874/1164* |
| `npm --prefix tools run check:mermaid` | 0 | **212** khối · 0 lỗi (*baseline: 211/0*) |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 40/40 pass |
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/reporting/rep-001-read-model-job-tong-hop.md -WP14` | 0 | **DAT** — 16/16 heading · 17/17 BR có test · 3 dòng `Open` là mô tả lịch sử |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/REP-001-docreview.md` | 0 | DAT — **`DOC-OK`** vòng 3 (P1=0 · P2=0 · P3=10) |
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/reporting/rep-002-bao-cao-doanh-thu-lai-gop.md -WP14` | 0 | **DAT** — 16/16 heading · **18/18 BR** có test · **0** câu `Open` |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/REP-002-docreview.md` | 0 | DAT — `DOC-CHANGES` vòng 2 (P1=0 · P2=5 · P3=3) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT — **60** dòng kiểm (tới `D-175`) |
| `pwsh -File tools/agents/phien.ps1 check` | 0 | OK: bảng khớp registry (đầu phiên) |
| `git status --porcelain` | — | chỉ `docs/**` + `agents/state/**` (WP-14: 0 tệp `.cs`, 0 migration) |

- **Baseline đầu phiên (N57):** link PASS 191 tệp/874 link/1164 backtick · mermaid 211/0 · hooks 40/40 · `PH check` OK. Build/test **bỏ qua có lý do** "WP thuần tài liệu" (`D-71`; build máy dev đỏ `MSB3491` — chủ `WP-15`).
- **`REP-001` — sửa 8 P2 vòng 2 (điểm xanh 1):** BA sửa tại chỗ 39 dòng đúng 8 P2 (kiểu tiền `numeric(18,2)`, `TenantId` nullable, Testcontainers đã có, BR-002 ngoại lệ có kiểm, route `/revenue`, khoá `report:tong-hop`, DTO `DinhNghiaTien`); `changedSections` = §0·§1·§3.1·§5.2.1·§6.2·§8.1·§14.1·§14.2·§15.2, không chạm P3. DOCREV **vòng 3 kiểm nhanh `DOC-OK`** (0 P1 · 0 P2 · 10 P3); mỗi kết luận code có dòng `graph:`. ⇒ `REP-001` **đủ điều kiện trình duyệt**.
- **`REP-002` — dây chuyền đầy đủ (điểm dừng 2):** BA đối chiếu v0.1→v0.2 (as-built `WP-08`, `D-47`/`D-48`) + **7 câu §15** → decider:
  - 6 `TIEN_LE` (không tạo `D-xx`): Q2 giá vốn = **bình quân gia quyền DI ĐỘNG + chụp lúc bán** (`D-114` P2; BA phải sửa nhãn "theo kỳ" → "di động"); Q4 **không trừ VAT lần nữa** (`D-48`/`D-114` P3); Q7 phí sàn dự kiến **có cấn trừ + cờ `SettlementPending`** (`D-114` P3); Q9 nhãn **tạm tính + `asOfUtc`** không tách bảng (`D-114`+`D-172`); Q10 ghi nhận **mọi đơn giao vận khi `Delivered` + `Source=Reconciliation`** (`D-114` P4 — BA đề xuất hybrid "chỉ COD" bị loại); Q11 **CSV ở FE**, xuất kế toán có audit thuộc `ACC-001` (`D-03`/`D-36`).
  - 1 `TẠM CHỐT`: **`D-175`** — chấp nhận ngoại lệ `GetTongQuanDonHangAsync` **ngoài phạm vi** `REP-002`, cơ chế đỏ TC-02 allow-list đúng một method (đồng nhất `REP-001` BR-002/TC-02).
  - BA áp 7 quyết định (D1s exit 0, 18 BR) → DOCREV **vòng 1 `DOC-CHANGES`** (1 P1 · 6 P2 · 3 P3) → BA sửa P1/P2 (P1: PRC-001/PRC-002 **đã có code** `PrcCostPrices`) → **vòng 2 `DOC-CHANGES`** (0 P1 · 5 P2 · 3 P3) ⇒ hết trần 2 vòng ⇒ ghi [`doc-gate-2026-11-24.md`](../agents/doc-gate-2026-11-24.md) mục `REP-002`.
- Cập nhật [`docs/agents/nhap-bien-ban-gd3-c.md`](../agents/nhap-bien-ban-gd3-c.md): dòng `REP-001` đề xuất **trình duyệt** (DOC-OK); dòng `REP-002` "chưa" (còn 5 P2). Cập nhật `07` §13 thêm dòng `N57`.

## 2. Chưa xong / cắt giữa

Hàng `P-14` **chưa đạt DoD** (DoD: mỗi mã `check-feature-doc` exit 0 · DOCREV `DOC-OK` · D3 có dòng · D4 xanh). Trạng thái từng mã:

1. **`REP-001` — XONG `DOC-OK`.** 10 P3 để người duyệt (không sửa sau `DOC-OK`): ERD 6/8 bảng · "23" vs 25 test · metric `*_upserted` · `NhipMacDinhPhut` vs `NhipJobPhut` · empty-state Reconciliation/Rebuild (chưa có) · TC-13 quét route no-op · TC-18 Unit vs Integration · timeout khoá 30 s/2 s · tiền tố permission `eCommerce.Reporting.*` vs `eCommerce.Reports.*`.
2. **`REP-002` — chặn ở D2 sau vòng 2** (`DOC-CHANGES`, 0 P1 · 5 P2 · 3 P3). 5 P2 còn mở (chi tiết + dẫn chứng trong [`doc-gate-2026-11-24.md`](../agents/doc-gate-2026-11-24.md)):
   1. **§4.3 dòng 303 (Mermaid):** node vẫn ghi "PRC-001/PRC-002 — CHƯA CÓ trong code" (thân đã sửa) → "bảng đọc `PrcCostPrices` (chưa có cửa ghi)".
   2. **§8.1 dòng 651/670-672 + TC-02:** allow-list "đúng một `GetTongQuanDonHangAsync`" quét `modules/Reporting/**` sẽ đỏ trên `TongHopBaoCaoService` (`:70-71` inject `IRepository<Order>/<OrderLine>`) → giới hạn phạm vi đường đọc hoặc allow-list thêm job.
   3. **§5.2.4 dòng 466-467 + §5.1 ERD + §5.2.2:** "chụp giá vốn vào fact lúc bán" nhưng `RptFactSalesLine` không có cột giá vốn; ERD đặt `CostUnitAmount` ở bảng kỳ → chốt chỗ lưu snapshot rồi cập nhật ERD/bảng cột/§6.3.
   4. **§11 dòng 757 + §0 dòng 50:** "SAL-002 chưa có code" sai (`CheckoutAppService.cs:39` đã có); chỉ thiếu lưu `TaxRate`/`TaxClassificationId` trên `OrderLine`.
   5. **§8.1 BR-018 + §1 Post-Condition:** "chứng từ huỷ không bao giờ xuất hiện trong `Rpt*`" sai khi huỷ SAU khi ngày đã tổng hợp (job chạy mốc+1) → thu hẹp phát biểu + nêu backfill/điều chỉnh kỳ sau + thêm TC.
   - **P3 #6–#8** để người duyệt: INDEX `(TenantId, BusinessDate)` không tồn tại · KeyPrefix đúng `:222,225-238` · TC-35 phủ `NFR-003`+`D-172` thay `NFR-007`.

## 3. Migration đã áp

Không có (WP thuần tài liệu; 0 tệp `.cs`, 0 migration).

## 4. Quyết định mới

- **`D-175`** — `TẠM CHỐT (DEC)` (uỷ quyền `D-114` M4): chấp nhận ngoại lệ `GetTongQuanDonHangAsync` (đọc `OrdOrders`/`OrdOrderLines`) **ngoài phạm vi** `REP-002`; cơ chế đỏ TC-02 allow-list đúng một method. Đã ghi `decisions.md` (dòng 189) + hàng đợi `hang-doi-xem-lai.md` nhóm `release`. Lùi = xoá dòng + mở lại Q12; 0 code.
- **6 `TIEN_LE` (không tạo `D-xx` mới):** Q2 → `D-114` P2 · Q4 → `D-48`/`D-114` P3 · Q7 → `D-114` P3 · Q9 → `D-114`+`D-172` · Q10 → `D-114` P4 · Q11 → `D-03`/`D-36`.
- **Không** dòng `ĐÃ CHỐT` nào mới; không đảo dòng cũ. `decisions.md` = **60** dòng kiểm, DAT.

## 5. Nợ / rủi ro

- **`REP-002` chưa `DOC-OK`** ⇒ **không** trình duyệt, không dùng làm căn cứ code. Còn **5 P2** (sai as-built: node Mermaid PRC, allow-list TC-02 đỏ trên job, chỗ chụp giá vốn, SAL-002, BR-018 huỷ muộn) — đều là lỗi **sai im lặng** nếu người code làm theo.
- **`REP-002` là đỏ cứng** (`D-114` §2.1): decider chỉ tạm chốt câu thiết kế (M4); **kế toán** phải ký biên bản trước khi code. `D-175` vào hàng đợi người duyệt lô.
- **Chờ người (đã có từ trước, không phát sinh câu mới phiên này):** `P-19` duyệt đợt C (bắt buộc kế toán cho `ACC-001`/`REP-002`) · `P-21` merge PR (vùng cấm) · `P13` mở khoá `LOY-001`.
- **Nợ chéo bắt buộc** (chủ khác, không sửa ở WP-14): `REP-003` §5.2 dòng 256 đổi `StockMovementType.Transfer=6` → `TransferIn`/`TransferOut` + `numeric(18,3)` (`D-170`); `XC-006` §14.5 cảnh báo `EXCLUDE` "chưa từng chạy thật" đã lạc hậu (`D-171`).
- **Phạm vi `P-14`** còn `REP-002` (5 P2 + vòng kiểm nhanh) trước khi chuyển `P-15` (`REP-003`, `ACC-001`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG `P-14` → mở lại `P-14`.**

- BA sửa **5 P2** `REP-002` theo [`doc-gate-2026-11-24.md`](../agents/doc-gate-2026-11-24.md) → `check-feature-doc.ps1 -WP14` → **DOCREV vòng kiểm nhanh** (chỉ `changedSections`).
- `REP-002` `DOC-OK` ⇒ `PH done -Id P-14`; nếu vẫn dở ⇒ `PH dodang` với lý do mới. Sau đó `P-15` (`REP-003` + `ACC-001`).
- Lệnh xác nhận hàng: `pwsh -File tools/agents/phien.ps1 next`.

**Khối B — mở phiên mới:**

```text
/chay-phien
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a`; `HEAD` trước commit phiên = `bdf419b`, `main` = `be301c5`. Commit phiên `N57` = `3675332` (REP-001) + `8465058` (REP-002 + `D-175`) (**đã push** lên `origin/docs/n22-wp14-dot-a`; bản cập nhật handover là commit kế). Chưa PR.

---

**Hàng rào agent** (phiên N57): **14** lượt gọi vai — `ba` ×4 (+1 lượt tiếp sau trần 80 lượt ở lượt áp 7 quyết định), `decider` ×7 (tuần tự Q2/Q4/Q7/Q9/Q10/Q11/Q12), `docrev` ×3 (`REP-001` kiểm nhanh + `REP-002` vòng 1 + vòng 2). **0** lần chặn dừng (`lanChan = 0`). **0** lần ghi ngoài vùng (`git status` chỉ `docs/**` + `agents/state/**`). **5** cảnh báo `SubagentStop` "thiếu khối JSON cuối" (ba ×2, decider ×2, docrev ×1) — **dương tính giả** đã biết (`D-104`: hook chỉ ghi nhật ký; mọi báo cáo thật đều có đủ khối JSON). Decider dùng đồ thị: `codegraph_explore` (Q4, Q7, Q9, Q10, Q11, Q12) · BA dùng `codegraph_explore` (REP-001 8 P2, REP-002 as-built) · DOCREV 8+ truy vấn `graph:` mỗi vòng.
