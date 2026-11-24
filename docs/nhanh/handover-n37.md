# Handover `N37` — WP-14: `P-04` (`SHP-002` + `SHP-004`) `DOC-OK` vòng 2, còn 2 P2

> Phiên `N37` (lệnh `/chay-phien`, 21/11/2026; nối các phiên `N34`–`N36` bị cắt ngang không có handover). Nhánh `docs/n22-wp14-dot-a`. WP thuần tài liệu (`D-71`): 0 `.cs`, 0 migration; build/test bỏ qua có lý do "WP thuần tài liệu".

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/agents/check-feature-doc.ps1 -Path docs/features/shipping/shp-004-adapter-ghtk.md -WP14` | 0 | 16/16 mục, 18/18 BR có test, mục đối chiếu Nhanh có (dòng 659), 5 dòng `Open`, 0 TODO |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/SHP-004-docreview.md` | 0 | `DOC-OK`, P1=0, P2=2, P3=15 (vòng 2; vòng 1 = `DOC-CHANGES` 1 P1/8 P2) |
| `pwsh -File tools/agents/check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | 17 dòng đạt (gồm `D-128…D-131`) |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 0 tham chiếu chết |
| `npm --prefix tools run check:mermaid` | 0 | 211 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | — | không đọc được số `# pass/# fail`; chưa đo lại |

`SHP-004` v0.5 (≈92 KB, 18 BR): P1 duy nhất của vòng 1 (adapter NÉM sau khi `SHP-001` đã commit `Submitting`) đã đóng bằng ADR-13/BR-018/TC-29 (`D-130`); cả 8 P2 vòng 1 đã đóng. D3: dòng nháp trong `docs/agents/nhap-bien-ban-gd3-a.md`. Hàng `P-04` đạt DoD (`SHP-002` xong từ `7f3d61b`).

## 2. Chưa xong / cắt giữa

- Hai P2 của DOCREV vòng 2 (không chặn `DOC-OK`; nên sửa trước khi code): (1) `CarrierFeeResult` ghi "khai ở `SHP-001` §6.1" nhưng `shp-001` mới có chữ ký :456 — việc kèm `D-129`; (2) khoá webhook `WebhookHashToken`/`WebhookAllowedIps` lệch `HashToken`/`IpAllowlist` của `SHP-002` §6.4 :657–669.
- Việc kèm `D-129`/`D-130`/`D-131` cho BA `SHP-001` (khai `CarrierFeeResult`; ánh xạ Pha 3 `Business`/`Configuration` ⇒ `SubmitFailed`; dựng request trước commit `Submitting`) — **chưa làm** (ngoài mã của hàng).
- 15 P3 trong docreview (số dòng dẫn trôi, TC-11, TC-21/TC-02, §11 thiếu project `Carriers.Ghtk`…).
- `P-07` (`SAL-006`) ⬜, làm một mình.

## 3. Migration đã áp

Không có.

## 4. Quyết định mới

| D-xx | Trạng thái | Nội dung |
|---|---|---|
| `D-128` | TẠM CHỐT (DEC) | Q9: xác nhận ADR-12 — `SHP-004` không bảng/aggregate/luồng tạo riêng, chỉ cài `IShippingCarrierProvider` |
| `D-129` | TẠM CHỐT (DEC) | Q10: `CarrierFeeResult` theo khuôn lỗi chung; `IsEstimate`/`FailureReason` ở tầng app |
| `D-130` | TẠM CHỐT (DEC) | Q11: Pha 3 `SHP-001` ghi `SubmitFailed` + attempt `Rejected`, giữ nguyên `FailureCode` adapter |
| `D-131` | TẠM CHỐT (DEC) | Q12: dựng request (cửa mã `Address`, COD `long`, cân) xong dưới khoá, trước commit `Submitting` |

Người chốt: không có (hồ sơ uỷ quyền `D-114` HIỆU LỰC). Bốn dòng đã có trong `docs/agents/hang-doi-xem-lai.md`.

## 5. Nợ / rủi ro

| # | Nợ | Chủ |
|---|---|---|
| 1 | 2 P2 + 15 P3 của `SHP-004` (§2) | BA `SHP-004` |
| 2 | `SHP-001`: khai `CarrierFeeResult`, Pha 3, thứ tự dựng request (`D-129…D-131`); cờ `SupportedAddressModel` từ DB cho factory | BA `SHP-001` |
| 3 | `SHP-002` §6.4: thống nhất tên khoá cấu hình webhook GHTK | BA `SHP-002`/`SHP-004` |
| 4 | `SHP-004` nằm danh sách đỏ: Q1/Q3/Q7 chờ GHTK (R3-1) | chủ dự án (liên hệ GHTK) |
| 5 | `hooks.test.mjs` chưa đo số ca | ORCH phiên sau |
| 6 | Tệp đã sửa song song, ngoài phiên, **không** add vào commit: `tools/agents/chay-tu-dong.ps1`, `tools/agents/test-chay-tu-dong.ps1`, `docs/agents/huong-dan-chay-tu-dong.md` (hook gán nhầm cho `docrev`; diff là chỉnh bộ đọc hạn mức của bộ chạy tự động — không phải việc của vai; không `git restore` để khỏi mất việc của người) | chủ dự án |

**Chờ người:** không có câu mới; xem nợ #4.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`P-04` xong; kế `P-07` `SAL-006` một mình; rồi `P-08`).

**Khối B — mở phiên mới:**

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
| `docrev` | 1 (vòng 2) | `DOC-OK`; cảnh báo ghi ngoài vùng 3 tệp = nợ #6 |
| `ba` / `decider` | 0 trong N37 | BA + decider của `SHP-004` chạy ở `N34`–`N36` (`D-128…D-131`) |
