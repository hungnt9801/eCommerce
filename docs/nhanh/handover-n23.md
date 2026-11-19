# Handover `N23` — WP-14 đợt A, phần 2 (`ADDR-003` dở ở vòng sửa 1 → DOCREV vòng 2)

> Phiên `N23` · 18/11/2026 · nhánh `docs/n22-wp14-dot-a` (tiếp nhánh `N22`) · `main` = `6052d14`.
> Phiên dừng **giữa `ADDR-003`** vì chạm hạn mức 5 giờ của gói Pro (mở lại **23:50 18/11/2026**, giờ máy 21:16 lúc đó).

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

**Baseline lúc mở phiên (`/mo-phien WP-14 N23`):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 145 tệp, 730 link, 755 đường dẫn, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 30/30 pass (`N22` ghi 26; bản sửa hook B3 do chủ dự án commit `5aa3dce`) |
| `dotnet build` / `dotnet test` / `test-cong-kiem.ps1` | — | bỏ qua: WP thuần tài liệu (`D-71`), không sửa `tools/` |

**Việc đã làm:**

1. **Cây bẩn lúc mở phiên** (9 tệp `.claude/**` + 3 tệp `docs/**`): dừng theo `mo-phien` §2, chủ dự án commit `5aa3dce` rồi cho chạy tiếp.
2. 🚨 **Commit `5aa3dce` đã XOÁ `D-77…D-94`** khỏi `decisions.md` (−18 dòng) và 16 dòng khỏi `hang-doi-xem-lai.md` — phát hiện nhờ BA báo "`D-77..D-94` chưa có trong `decisions.md`". Chủ dự án chọn **khôi phục**: `git checkout 7020d77 -- docs/nhanh/decisions.md docs/agents/hang-doi-xem-lai.md` (18 dòng `D-77…D-94` có lại; đang **staged**, chưa commit).
3. `/doc-gate ADDR-003 SHP-001` — chỉ làm tới `ADDR-003`:

| Bước | Kết quả |
|---|---|
| D1 BA đối chiếu | xong vòng 1; `check-feature-doc.ps1 -WP14` exit 0 (16/16 mục, 16/16 BR có test) |
| D1q decider | `Q9` → `D-95` · `Q10` → `D-96` · `Q11` → `D-97` · `Q16` → `D-98` · `Q13` → `D-99` · `Q14` → `D-100` · `Q15` → `D-101` (7 `TẠM CHỐT`) |
| D2 DOCREV vòng 1 | **`DOC-CHANGES` 1 P1 / 17 P2 / 18 P3** (`check-agent-artifact.ps1 -Kind docreview` exit 0) |
| Sửa P1/P2 | BA sửa #1–#18 (đợt 1, đã `DAT`); áp `D-98…D-101` **mới xong đợt 1 thì bị cắt** (hạn mức) |

4. Tệp mới: `docs/reviews/ADDR-003-docreview.md` (vòng 1). ⚠️ Dòng 13 ghi tóm tắt **"16 P2, 19 P3"** nhưng bảng đếm **17 P2, 18 P3** — sửa ở vòng 2.
5. **`SHP-001`: chưa làm.**

**Đo lại lúc đóng phiên (18/11/2026):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 733 link, 785 đường dẫn, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 30/30 pass |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | DAT — 23 dòng DEC |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/ADDR-003-docreview.md` | 0 | `DOC-CHANGES` · P1=1 · P2=17 |
| `check-feature-doc.ps1 -Path …addr-003-… -WP14` | 0 | DAT — 12 câu `Open`, 0 TODO |
| `git status --porcelain` | — | chỉ `docs/**` · **0** `.cs` · **0** migration |

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**`ADDR-003` đang ở D2 sau "sửa P1/P2" — tệp tài liệu `DAT` theo máy nhưng DOCREV chưa `DOC-OK`.**

1. ⏭ **BA áp nốt `D-98…D-101`** vào `docs/features/address/addr-003-chuan-hoa-dia-chi-3-cap-2-cap.md` (đã xong: §3.1 hàng 2/5/7, Basic Flow 4, §6.1 `Strict`, `0302`, `0306/0307`). Còn: ghi `Q13–Q16` ⇒ đóng/trỏ `D-xx`; `D-99` (`ApplyNormalizationAsync` trên **lớp** `AddressDirectory`); `D-100` (`IsActive`/unique, `SupersededByImportId`, 2 unique + CHECK); `D-101` (`AddressInputSnapshot`); TC §14 (TC-07 → Theory, TC-23); mục Đối chiếu thêm `D-98…D-101`. **Cấm** sửa `addr-004-*` (đã duyệt) và `docs/reviews/`.
2. ⏭ Ghi **3 câu hỏi mở mới** (`needs=nguoi`/`DEC`, không chặn DoD): chưa có đường rút hẳn một nhóm ánh xạ · review `NeedsReview` mở trước khi nhóm bị thay thế thì xác nhận thế nào (`D-100`) · giữ `Strict` khi chưa có bên gọi hay bỏ (`D-98`).
3. ⏭ **DOCREV vòng 2** (kèm đường dẫn docreview vòng 1 + `changedSections`): phải `DOC-OK` 0 P1; nhờ docrev sửa câu tóm tắt dòng 13 (17 P2 / 18 P3). Vẫn `DOC-CHANGES` ⇒ `doc-gate-<ngày>.md` (`/doc-gate` §3).
4. ⏭ D3 nháp biên bản (`ADDR-003` chưa có dòng trong `docs/agents/nhap-bien-ban-gd3-a.md`) → D4.
5. ⏭ **Còn 6 tài liệu đợt A:** `SHP-001…004`, `REC-003`, `SAL-006` (`D-76`: 2 tài liệu/phiên, `SAL-006` một mình). `SHP-001` chưa mở.
6. ⚠️ Các tệp **chưa commit**: `addr-003-…md`, `docs/reviews/ADDR-003-docreview.md` (mới), `decisions.md`, `hang-doi-xem-lai.md` (`D-77…D-101`, staged một phần).

## 3. Migration đã áp

**Không có** (WP thuần tài liệu, `D-71`).

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

- **TẠM CHỐT (DEC)** — agent `decider`, đều có cách lùi + hạn xem lại, đều trong [`hang-doi-xem-lai.md`](../agents/hang-doi-xem-lai.md):
  - `D-95` (bỏ `AddrDatasets` → `SchemaVersion` + bản ghi lô ánh xạ) · `D-96` (kết quả xác nhận/từ chối ở ledger; hàng đợi = `ChuaChuanHoa` + `NghiNgo`) · `D-97` (resolver chỉ đi **một** bước; đích đóng ⇒ `NeedsReview`, không ném `0006`) · `D-98` (HTTP luôn `CreateReview`) · `D-99` (cửa chuẩn hoá cho job/CSKH trên lớp, không trên interface) · `D-100` (`IsActive` chỉ cửa nạp ghi, thay theo nhóm) · `D-101` (`AddressInputSnapshot` allowlist; ẩn danh PII = nợ GĐ2).
  - Hạn xem lại: `D-95…D-98` trước WP code ADDR-003 / release (xem từng dòng); `D-99`, `D-101` = **release**; `D-100` = trước sinh migration `AddrUnitMappings`.
- **Người chốt:** không có quyết định mới của chủ dự án trong phiên (ngoài việc chọn khôi phục `D-77…D-94`).

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **`5aa3dce` xoá `D-77…D-94` khỏi nhánh** — đã khôi phục ở cây làm việc nhưng **chưa commit**. Không commit ⇒ PR WP-14 mất 18 quyết định mà `ADDR-001/002/003` đang viện dẫn. Nhận chủ: chủ dự án (commit); phiên sau chạy `grep -c "^| D-9[0-4] " docs/nhanh/decisions.md` = 5 trước khi làm gì khác.
2. 🚨 **Hook báo động giả "thiếu khối JSON" vẫn còn** sau bản sửa `5aa3dce` — 12 cảnh báo mới trong phiên, `json: true` 0/12, trong khi **mọi** báo cáo hand-back đều có khối JSON; `ba` bị chặn dừng 3 lần. Bản sửa B3 chưa đủ. Nhận chủ: B3 (vùng `.claude/**`, không sửa từ trong vai).
3. ⚠️ **Trần lượt vai thấp:** `ba` chạm 60 lượt **2 lần**, `docrev` 40 lượt, `decider` 30 lượt (đều làm tiếp bằng `SendMessage`); **hạn mức 5 giờ chạm giữa `ba`** (mở lại 23:50). Phiên kế: nên **một** tài liệu/phiên cho tài liệu lớn như `ADDR-003` (≈ 1000 dòng, 7 `D-xx` mới). Nhận chủ: B3/B4 (`D-72`, `D-76`).
4. ⚠️ **Hai decider song song va số `D-xx`** (cùng ghi `D-99`, rồi cùng nhảy `D-100`) — tự sửa đúng thứ tự, nhưng nên **không** chạy nhiều decider song song khi cùng ghi `decisions.md`. Nhận chủ: B3 (khoá ghi hoặc ORCH cấp số).
5. ⚠️ `addr-004` §6.1 cần ghi chú *"hợp đồng liên module không có method ghi trạng thái (`D-99`)"* — tài liệu **đã duyệt** ⇒ **người duyệt** quyết. Nhận chủ: chủ dự án.
6. 📌 **`D-101` chặn release** tới khi chọn hướng GĐ2 cho PII `freeText`: (i) một cửa ẩn danh · (ii) tách bảng xoá được (rẻ nhất nếu chọn **trước** WP code ADDR-003) · (iii) giữ có thời hạn. Pháp lý: `decider` tra web — Luật 91/2025/QH15 + NĐ 356/2025 là nguồn **thứ cấp**, chưa xác minh điều khoản thay NĐ 13/2023.
7. Nợ N22 **còn nguyên**: `EInvoice` băm `tinh.Name`/`xa.Name` (§5 #4) · `D-93` file ĐVHC chưa xác định · `XC-003` tự mâu thuẫn · toàn bộ phần code `D-77…D-101` thuộc **WP-15+** · `MSB3491` · `-ChiKiemMoc` 43/50.

**Chờ người** (gom một lần cuối phiên; mỗi câu có khuyến nghị và phần đã làm tiếp):

| # | Câu | Khuyến nghị | Đã làm tiếp |
|---|---|---|---|
| 1 | `ADDR-003-Q2` — file máy đọc được của phụ lục NQ sắp xếp ĐVHC cấp xã (cơ quan, định dạng, đường dẫn)? (`D-93`) | có file — bạn cung cấp nguồn | thiết kế trung lập, `AddrUnitMappings` chưa nạp dữ liệu |
| 2 | `ADDR-003-Q12` — ca `TargetClosed` ra ứng viên rỗng: gợi ý đi tiếp chuỗi hay nạp lô A→C trực tiếp? | (b) nạp lô A→C trực tiếp (không thêm đường suy diễn) | giữ `Open`, không chặn DoD |
| 3 | `D-98` — giữ nhánh `Strict` khi chưa có bên gọi in-process, hay bỏ cho tới khi có? | giữ (mặc định fail-closed, tốn một nhánh) | WP code có thể hoãn cài |
| 4 | `D-101` — hướng GĐ2 cho PII `freeText` (i/ii/iii) + xác minh văn bản pháp luật thay NĐ 13/2023 | (ii) tách bảng, chốt **trước** WP code ADDR-003 | giữ nợ GĐ2, chặn release |
| 5 | Ghi chú `addr-004` §6.1 về `D-99` (tài liệu đã duyệt) | thêm ghi chú, 1 dòng | để nguyên `addr-004` |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`ADDR-003` còn áp `D-98…D-101` + DOCREV vòng 2; còn 6 tài liệu đợt A; DoD đợt A chưa đạt).

1. Commit điểm hiện tại **trước** (chủ dự án) — đảm bảo `D-77…D-101` nằm trong commit (§5 #1). Rồi `/mo-phien WP-14 N24` — đọc §2 của handover này + `D-95…D-101`; baseline (`kiem-link-tai-lieu.ps1`, `check:mermaid`, `hooks.test.mjs`).
2. Gửi BA (`SendMessage` nếu agent còn, hoặc `/doc-gate ADDR-003` bắt đầu từ bước áp `D-98…D-101`) → D1s → DOCREV **vòng 2** → D3 → D4. Một tài liệu/phiên cho `ADDR-003`.
3. Sau đó `/doc-gate SHP-001 SHP-002`; `SAL-006` một phiên riêng.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-14
và handover `docs/nhanh/handover-n23.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên N23 **dừng giữa** WP-14: làm tiếp **đúng phần còn lại** ghi ở handover §2 — **không** làm lại phần đã
xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a` @ `5aa3dce` — thay đổi phiên này **chưa commit**, chưa push, chưa PR. `main` = `6052d14` (không đổi).
- `chore/agents-b1-b2` @ `e132f07` — chưa push, chưa PR (như `N22`). Thứ tự merge đề xuất: PR `chore/agents-b1-b2` trước, rồi PR WP-14 (mở đầu `Refs: D-71`).

---

**Hàng rào agent** (`artifacts/agents/2026-11-18.jsonl`, `canh-bao.jsonl` — chỉ lượt `N23`, từ 12:55Z)

| Vai | Lượt dừng ghi nhận | Nhận diện JSON | Lần chặn dừng | Ghi ngoài vùng |
|---|---|---|---|---|
| `ba` | 4 | 0 | 3 | 0 |
| `decider` | 7 | 0 | 0 | 0 |
| `docrev` | 1 | 0 | 0 | 0 |
| **Tổng** | 12 | 0 | 3 | **0** |

Cảnh báo: 12 dòng mới — **toàn bộ** "thiếu khối JSON" (báo động giả, §5 #2); **0** lần ghi ngoài vùng. Hạn mức gói Pro: **chạm hạn 5 giờ** (mở lại 23:50 18/11/2026), % tuần **chưa đo**.
