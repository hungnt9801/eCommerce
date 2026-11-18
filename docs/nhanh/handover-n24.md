# Handover `N24` — WP-14 đợt A: `ADDR-003` đạt `DOC-OK`; còn 6 tài liệu đợt A

> Phiên `N24` · 18/11/2026 · nhánh `docs/n22-wp14-dot-a` · `main` = `6052d14`.
> Mở bằng `/mo-phien WP-14 N24`; làm tiếp **đúng phần dở** của `handover-n23.md` §2 (áp `D-98…D-101` → DOCREV).

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

**Baseline lúc mở phiên** (HEAD `5fcceb6`):

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 148 tệp, 736 link, 800 đường dẫn, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 33/33 pass |
| `dotnet build` / `dotnet test` / `test-cong-kiem.ps1` | — | bỏ qua: WP thuần tài liệu (`D-71`), không sửa `tools/` |

**Việc đã làm:**

1. **Commit `5fcceb6`** (đã push): `chore(agents): N24 - sua hook va doc-gate sau N22/N23 (D-102)` — 10 tệp hệ agent + dòng `D-102`.
2. `/doc-gate ADDR-003` (một tài liệu/phiên, `D-102`):

| Bước | Kết quả |
|---|---|
| D1 BA áp `D-98…D-101` | xong (+72/−35 dòng); `check-feature-doc.ps1 -WP14` exit 0 |
| D2 DOCREV vòng 2 | `DOC-CHANGES` · 0 P1 · 5 P2 · 20 P3 |
| Sửa 5 P2 (BA) + `Q20` → decider `D-103` (tạm chốt) → BA áp `D-103` | `check-feature-doc.ps1` exit 0 sau mỗi lượt |
| D2 DOCREV vòng kiểm nhanh (vòng 3) | **`DOC-OK`** · 0 P1 · 1 P2 · 26 P3 (`check-agent-artifact.ps1 -Kind docreview` exit 0) |
| D3 nháp biên bản | dòng `ADDR-003` trong `docs/agents/nhap-bien-ban-gd3-a.md`: **trình duyệt**, kèm P2 mở (điều kiện trước WP-15) |

3. **Người chốt** (chủ dự án, cuối phiên) ⇒ `D-105`, `D-106`, `D-107` `ĐÃ CHỐT (người)`; dọn: `mo-ta-pr-wp14-dot-a.md` → `artifacts/pr/` (không commit), chèn `D-104`, sắp lại `hang-doi-xem-lai.md` (`D-102` < `D-103` < `D-104`).

**Đo lại lúc đóng phiên:**

| Lệnh | Exit | Số đo |
|---|---|---|
| `kiem-link-tai-lieu.ps1` | 0 | PASS — 739 link, 799 đường dẫn, 0 chết |
| `check:mermaid` | 0 | 212 khối, 0 lỗi |
| `hooks.test.mjs` | 0 | 33/33 pass |
| `check-agent-artifact.ps1 -Kind decision` | 0 | DAT — 24 dòng DEC |
| `check-agent-artifact.ps1 -Kind docreview` (`ADDR-003`) | 0 | `DOC-OK` · P1=0 · P2=1 |
| `check-feature-doc.ps1 -WP14` (`ADDR-003`) | 0 | DAT — 12 câu `Open`, 0 TODO |
| `git status --porcelain` | — | chỉ `docs/**` · **0** `.cs` · **0** migration |

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**`ADDR-003` đã `DOC-OK` (D2 xong) — việc còn là áp quyết định của người, rồi đến 6 tài liệu.** DoD đợt A **chưa đạt**.

1. ⏭ **BA áp `D-105…D-107` vào `addr-003`** (một lượt, sửa tại chỗ): §15.1 đóng `Q12` (`D-106`), `Q15b` (`D-105`), `Q17` (`D-98` giữ `Strict`), `Q18` (thêm `MappingKind` `Withdrawn`), `Q19` (NÉM mã mới + mở review mới); sửa §5.2/§6.1/§11/TC-36 cho `freeText` tách bảng riêng (`D-105`); §6.2/§12/§14 cho `Withdrawn`/`Q19`; mục Đối chiếu thêm `D-104…D-107` nếu liên quan. Sau đó D1s + DOCREV **kiểm nhanh** (chỉ `changedSections`).
2. ⏭ **Ghi chú `addr-004` §6.1 theo `D-99`** (1 dòng) — người duyệt đã đồng ý (`D-107`); vai `ba` **không** có vùng ghi `addr-004` ⇒ làm ở phiên có quyền (ORCH/người), không qua vai.
3. ⏭ D4 điểm xanh cho `ADDR-003` sau (1); rồi `/doc-gate SHP-001 SHP-002` (2 tài liệu/phiên); `SAL-006` một phiên riêng; còn `SHP-003`, `SHP-004`, `REC-003`.
4. ⚠️ **P2 mở của `ADDR-003` (docreview vòng 3 #1):** dòng `NeedsReview`/`MappingSuperseded` do job soát lại ghi thiếu `InputSnapshot` + `From*Code` — chép từ dòng ledger gốc, thêm vế vào TC-38. **Chủ dự án chốt (`D-107`): không sửa ở N24; sửa trước khi code WP-15.** Ghi ở nháp biên bản.
5. ⚠️ **Cây làm việc chưa commit:** `addr-003`, `ADDR-003-docreview.md`, `decisions.md` (`D-103…D-107`), `hang-doi-xem-lai.md`, `nhap-bien-ban-gd3-a.md`, cộng `handover-n24.md` + dòng `07` §13.

## 3. Migration đã áp

**Không có** (WP thuần tài liệu, `D-71`).

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

- **TẠM CHỐT (DEC)** — `D-103` (dòng soát lại hàng 7 = `NeedsReview` + `ReasonCode` `MappingSuperseded = 5`; bỏ `Outcome.ReNormalizeRequired`; CHECK `Outcome in (0,1,2,3)`). Hạn xem lại: trước WP code đầu tiên của `ADDR-003`.
- **ĐỀ XUẤT** — `D-104` (hệ agent: tắt chặn dừng "thiếu JSON", công tắc `chanDungKhiThieuJson` mặc định `false`) — chờ người xác nhận; `D-102` (sửa hệ agent sau N22/N23) cũng còn `ĐỀ XUẤT`.
- **ĐÃ CHỐT (người, 18/11/2026):** `D-105` (PII `freeText` hướng (ii) tách bảng) · `D-106` (`Q17` giữ `Strict` · `Q18` `Withdrawn` · `Q19` NÉM mã mới · `Q12` nạp lô A→C) · `D-107` (ghi chú `addr-004` §6.1 · P2 `MappingSuperseded` trước WP-15 · `Q2`/`D-93` giữ chờ).

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Chưa commit** các thay đổi §2 #5 — mất `D-103…D-107` + `DOC-OK` của `ADDR-003` nếu cây bị ghi đè (bài học `5aa3dce` ở N23). Nhận chủ: chủ dự án (commit).
2. ⚠️ **Hook "thiếu JSON" vẫn báo nhầm** trong N24 (`docrev` 2/2 lượt, `json: false` dù mọi báo cáo đều có khối JSON): `D-104` tắt chặn dừng — `D-102` + `D-104` còn `ĐỀ XUẤT`, người duyệt. Nhận chủ: B3/B4.
3. ⚠️ **`D-101`/`D-105`:** hướng (ii) đã chọn nhưng thân `addr-003` chưa theo — **chặn release** tới khi cả tài liệu lẫn code theo (ii). Pháp lý: văn bản thay NĐ 13/2023 **chưa xác minh** (nguồn thứ cấp).
4. ⚠️ **`D-97` ⑥:** test đỏ-khi-vi-phạm của `D-97` chưa có TC riêng ở §14 (chỉ TC-28/05/06) — BA bổ sung khi áp (1).
5. ⚠️ Docreview vòng 3: 26 P3 mở (6 mới: cột guard hàng 7 "tự động tới C" vs "vẫn `NeedsReview`"; khoá `:Invalid` thiếu ở bảng khoá §8; `GetReviewQueueAsync` thiếu dòng cho lọc `Confirmed`; nhãn §10.2; hàng 4 "kiểm định dạng thuần" không suy ra `0001`; job bỏ qua `DaXacMinh` không nêu lý do) — để người duyệt.
6. ⚠️ **SQL chưa chạy thử** (máy không có `psql`/`docker`): unique partial `NULLS NOT DISTINCT … WHERE`, CHECK `Outcome`/`IsActive` mới chỉ là mô tả. QA chạy ở WP code.
7. Nợ N22/N23 **còn nguyên**: `EInvoice` băm `tinh.Name`/`xa.Name` · `D-93` file ĐVHC chưa xác định · `XC-003` tự mâu thuẫn · toàn bộ phần code `D-77…D-107` thuộc **WP-15+** · `MSB3491` · `-ChiKiemMoc` 43/50.

**Chờ người** (gom một lần; đã trả lời `Q12`, `Q15b`, `Q17`, `Q18`, `Q19`, ghi chú `addr-004` — xem §4):

| # | Câu | Khuyến nghị | Đã làm tiếp |
|---|---|---|---|
| 1 | `ADDR-003-Q2` / `D-93` — file máy đọc được của phụ lục NQ sắp xếp ĐVHC cấp xã (cơ quan, định dạng, đường dẫn)? | có file — bạn cung cấp nguồn | thiết kế trung lập, `AddrUnitMappings` chưa nạp dữ liệu |
| 2 | Xác nhận `D-102` + `D-104` (hệ agent, đang `ĐỀ XUẤT`) | duyệt: chặn nhầm 12/12 ở N23, 2/2 ở N24 | công tắc mặc định tắt chặn, lùi bằng 1 dòng `scopes.json` |
| 3 | Văn bản pháp luật hiện hành thay NĐ 13/2023 + `freeText` có vào bản xuất dữ liệu cá nhân không (`D-101` ❓) | pháp chế xác minh trước release | giữ nợ, chặn release |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`ADDR-003` `DOC-OK`, còn áp `D-105…D-107`; còn 6 tài liệu đợt A; DoD đợt A chưa đạt).

1. Commit điểm hiện tại **trước** (chủ dự án) — đảm bảo `D-103…D-107` nằm trong commit (`grep -c "^| D-10[3-7] " docs/nhanh/decisions.md` = 5). Rồi `/mo-phien WP-14 N25`.
2. `/doc-gate ADDR-003` bắt đầu từ bước **BA áp `D-105…D-107`** (§2 #1) → D1s → DOCREV kiểm nhanh → D4. Ghi chú `addr-004` §6.1 (§2 #2) làm ngoài vai.
3. Sau đó `/doc-gate SHP-001 SHP-002`; `SAL-006` một phiên riêng.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-14
và handover `docs/nhanh/handover-n24.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên N24 **dừng giữa** WP-14: làm tiếp **đúng phần còn lại** ghi ở handover §2 — **không** làm lại phần đã
xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a` @ `5fcceb6` (đã push) — thay đổi sau đó **chưa commit** (§2 #5). `main` = `6052d14` (không đổi). Chưa PR.
- `chore/agents-b1-b2` @ `e132f07` — như `N23`. Thứ tự merge đề xuất: PR `chore/agents-b1-b2` trước, rồi PR WP-14 (mở đầu `Refs: D-71`).

---

**Hàng rào agent** (`artifacts/agents/2026-11-18.jsonl`, `canh-bao.jsonl` — chỉ lượt `N24`, từ 14:50Z)

| Vai | Lượt dừng ghi nhận | Nhận diện JSON | Lần chặn dừng | Ghi ngoài vùng |
|---|---|---|---|---|
| `ba` | 3 | 0 | 0 | 0 |
| `decider` | 1 | 0 | 0 | 0 |
| `docrev` | 2 | 0 | 0 | 0 |
| **Tổng** | 6 | 0 | 0 | **0** |

Cảnh báo "thiếu khối JSON" là báo nhầm (mọi hand-back đều có khối JSON); **0** lần ghi ngoài vùng; **0** lần chặn dừng (sau `D-102`). Hạn mức gói Pro: **chưa đo**.
