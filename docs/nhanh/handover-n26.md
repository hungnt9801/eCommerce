# Handover `N26` — WP-14 đợt A: `ADDR-003` đạt `DOC-OK` vòng 4 (đã áp `D-105…D-107` + `D-111`); còn 5 tài liệu đợt A

> Phiên `N26` · 19/11/2026 · nhánh `docs/n22-wp14-dot-a` · `main` = `6052d14`. Hàng `P-02` của `docs/agents/bang-phien-wp14.md`.
> **Không chạy `/mo-phien WP-14 N26` ở đầu phiên** (phiên tiếp ngay sau `N25`, cùng cửa sổ làm việc): baseline mở = số đo đóng của `N25` (`handover-n25.md` §1, HEAD `d076f0d`).
> Làm đúng phần dở ghi ở `handover-n25.md` §2 #1: BA áp `D-105…D-107` → DOCREV kiểm nhanh.

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

**Baseline lúc mở phiên** (HEAD `d076f0d`, lấy từ đo đóng `N25`):

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 753 link, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 34/34 pass |
| `dotnet build` / `dotnet test` / `test-cong-kiem.ps1` | — | bỏ qua: WP thuần tài liệu (`D-71`), không sửa `tools/` |

**Việc đã làm — `/doc-gate ADDR-003` (một tài liệu/phiên):**

| Bước | Kết quả |
|---|---|
| D1 BA áp `D-105…D-107` | xong; `check-feature-doc.ps1 -WP14` exit 0 (16/16 mục, 17/17 BR, 43 TC). Mới: `AddrNormalizationInputTexts` (`freeText` tách bảng xoá được), BR-17, cột `InputHash`, `MappingKind.Withdrawn = 3`, mã `03xx` cho `ConfirmAsync` (số chưa gán), TC-39…TC-43 |
| D1q decider `Q22` | `D-111` TẠM CHỐT — mở rộng phạm vi quét job hàng 7 (3 điều kiện) |
| BA áp `D-111` | exit 0; 17 BR, 47 TC (TC-44…TC-47), 2 index ledger `To*Code`/`From*Code`; `Q22` đóng, mở `Q23` |
| D2 DOCREV vòng kiểm nhanh (vòng 4) | **`DOC-OK`** · 0 P1 · **2 P2** · 37 P3 (`check-agent-artifact.ps1 -Kind docreview` exit 0) |
| D3 nháp biên bản | dòng `ADDR-003` trong `docs/agents/nhap-bien-ban-gd3-a.md` thay bản `N24`: trình duyệt kèm 2 P2 mở |

**Đo lại lúc đóng phiên:**

| Lệnh | Exit | Số đo |
|---|---|---|
| `kiem-link-tai-lieu.ps1` | 0 | PASS — 755 link, 0 chết |
| `check:mermaid` | 0 | 212 khối, 0 lỗi |
| `hooks.test.mjs` | 0 | 34/34 pass |
| `check-agent-artifact.ps1 -Kind decision` | 0 | DAT — 28 dòng DEC |
| `check-agent-artifact.ps1 -Kind docreview` (`ADDR-003`) | 0 | `DOC-OK` · P1=0 · P2=2 |
| `check-feature-doc.ps1 -WP14` (`ADDR-003`) | 0 | DAT — 16/16 mục, 17/17 BR có test, 12 dòng `Open`, 0 TODO |
| `.cs` / migration đổi | — | **0** / **0** |

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**`ADDR-003` xong D1–D3; D4 đạt.** Điểm xanh. Còn lại của đợt A:

1. ⏭ Ghi chú `addr-004` §6.1 theo `D-99` (1 dòng) — vai `ba` không có vùng ghi ⇒ ORCH/người (không đổi từ `N24`/`N25`).
2. ⏭ `/doc-gate SHP-002 SHP-004` (hàng `P-04`; đưa F8–F11 của `SHP-001` vào: `Generation`, `ClientOrderCode`, `Q16`, index partial, khoá `ecommerce:shipping:`); `SHP-003` một mình (`P-05`); `REC-003` (`P-06`); `SAL-006` một mình (`P-07`).
3. ⏭ Sửa **trước WP-15 / WP code** (không làm ở `N26`, người duyệt `D-107` giữ):
   - P2 #1 `ADDR-003`: dòng soát lại chép `InputSnapshot` + `InputHash` + `From*Code` từ dòng `Normalized` gốc **cho cả ba vế** của `D-111` (hiện chỉ vế (2) có luật), luật chọn khi nhiều dòng gốc, `ToThreeLevelAsync` chỉ đọc `InputSnapshot`; thêm vế nội dung vào TC-38.
   - P2 #2 `ADDR-003` (mới): TC-36 — cổng release của `D-105` — xoá trong `BEGIN…ROLLBACK` khác phiên nên **không đỏ được**; đổi sang xoá có commit trên DB test riêng.
4. ⚠️ **Cây làm việc chưa commit** — xem §5 #1.

## 3. Migration đã áp

**Không có** (WP thuần tài liệu, `D-71`).

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

- **TẠM CHỐT (DEC)** — `D-111` (`AddressRenormalizeJob` quét hợp ba điều kiện: (1) giữ `D-100` (6); (2) địa chỉ `DaChuanHoa` có ledger `Normalized` với `To*` = `FromCode` của nhóm đổi, tự loại V1→V2; (3) review `Pending` có `From*` = `FromCode` của nhóm đổi — mới/thay/rút. Kết quả luôn `NeedsReview`, không tự `Normalized`. Xem lại **trước WP-15**).
- **Không có quyết định của người** trong phiên này. `D-102`, `D-104`, `D-108…D-110`, `D-111` đều chưa có người chốt.

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Chưa commit:** `addr-003-…md`, `docs/reviews/ADDR-003-docreview.md`, `decisions.md` (`D-111`), `hang-doi-xem-lai.md`, `nhap-bien-ban-gd3-a.md`, `handover-n26.md`, dòng `07` §13 — mất `D-111` + `DOC-OK` nếu cây bị ghi đè (bài học `5aa3dce`). **Nhận chủ: chủ dự án (commit).**
2. 🚨 **7 tệp hạ tầng bảng phiên chưa theo dõi/sửa** (`agents/state/…`, `tools/agents/phien.ps1`, `test-phien.ps1`, 3 tệp `docs/agents/*` mới, `ke-hoach-v2-claude-code-pro.md` bị sửa) — như `handover-n25.md` §5 #2; **không** có trong commit đề xuất. Chủ: chủ dự án.
3. ⚠️ **Hàng `P-02`, `P-03` của bảng phiên chưa đóng:** `P-03` phụ thuộc `U-02` chưa làm (`phien.ps1 start` exit 2); `P-02` chưa `done`. Đóng bằng `phien.ps1 done -Id P-xx -Evidence … -Handover …` **chỉ khi chủ dự án chấp nhận** làm trước `U-02`. Phiên này không ghi vào registry.
4. ⚠️ **`D-111` lệch có chủ ý với `D-100` (6)** (cả hai TẠM CHỐT DEC) và **diễn giải `D-107` ②** (do người chốt) theo nghĩa "chép cột `To*` của dòng gốc vào `From*` của dòng mới" cho vế (2): **người cần xác nhận**.
5. ⚠️ **`D-101`/`D-105` chặn release:** thân `addr-003` nay theo (ii), nhưng code chưa có; `Q21` (cơ chế xoá `AddrNormalizationInputTexts`) mở, `needs = nguoi`; văn bản thay NĐ 13/2023 **chưa xác minh**.
6. ⚠️ `Q23` (địa chỉ không có ledger `ADDR-003` hoặc đang `DaXacMinh` gặp nhóm B→C — nợ N1 của `D-111`, cùng loại `D-95` N2) thuộc `ADDR-001`/`ADDR-004`; chưa qua decider, không chặn DoD. Nhận chủ: decider ở `/doc-gate` của `ADDR-004` hoặc trước WP-15.
7. ⚠️ Docreview `ADDR-003` vòng 4: 37 P3 mở. Đáng nhắc: #5 vế (1) không nói địa chỉ nào (`SourceEntityId` hay `ResultAddressId`); #6 TC-47 cấp tỉnh ra ứng viên không có xã ⇒ CSKH không xác nhận được; #7 index vế (3) thiếu `FromDistrictCode`; #13 hàng `D-101` của mục đối chiếu lỗi thời.
8. ⚠️ **SQL chưa chạy thử** (máy không có `psql`/docker): index `To*Code`/`From*Code`, unique `(TenantId, LedgerId)` `NULLS NOT DISTINCT`, CHECK `MappingKind in (0,1,2)`. QA chạy ở WP code.
9. Nợ N22–N25 **còn nguyên**: nợ `SHP-001` (xem `handover-n25.md` §5: `Q12`/`Q3` cần NCC, 18 dòng `Open`, `Q16`/`Q20` chặn migration đầu, SQL/UoW chưa đo) · `D-93` file ĐVHC · `EInvoice` băm `tinh.Name`/`xa.Name` · `XC-003` tự mâu thuẫn · toàn bộ phần code `D-77…D-111` thuộc **WP-15+** · `MSB3491` · `-ChiKiemMoc` 43/50.

**Chờ người** (gom một lần):

| # | Câu | Khuyến nghị | Đã làm tiếp |
|---|---|---|---|
| 1 | `ADDR-003-Q21` — cơ chế xoá `AddrNormalizationInputTexts` (ai, khi nào, đường nào) + văn bản thay NĐ 13/2023 + `freeText` có vào bản xuất dữ liệu cá nhân không (`needs = nguoi`) | (c) cả hai: method host-only theo yêu cầu khách (permission mới + audit) và job xoá theo thời hạn lưu; pháp chế xác minh trước release | thiết kế bảng + ràng buộc, chưa có đường xoá; chặn release |
| 2 | Xác nhận `D-111` (mở rộng quét vs `D-100` (6)) và cách đọc `D-107` ② | duyệt: fail-closed, quét thừa chỉ thêm việc CSKH | công tắc lùi: bỏ vế (2)/(3) trong truy vấn 1 tệp + index |
| 3 | Xem + duyệt `D-102`, `D-104`, `D-108…D-110` (từ `N25`) | duyệt `D-108` (khớp `ADDR-004` đã duyệt) | cách lùi ghi ở từng dòng |
| 4 | `ADDR-003-Q2`/`D-93` — file máy đọc được của NQ sắp xếp ĐVHC cấp xã (cơ quan, định dạng, đường dẫn) | có file — bạn cung cấp nguồn | thiết kế trung lập, `AddrUnitMappings` chưa nạp |
| 5 | Commit điểm hiện tại + hạ tầng bảng phiên (§5 #2, #3) + `P-02`/`P-03` có `done` trước `U-02` không | commit tài liệu WP-14 trước; hạ tầng `phien.ps1` commit riêng | không đụng |
| 6 | Còn từ `N25`: `SHP-001-Q12`, `SHP-001-Q3` (cần NCC/GHN) | xem `handover-n25.md` §5 | `D-109` đúng trong cả hai khả năng |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`ADDR-003` và `SHP-001` đều `DOC-OK`; còn `SHP-002`, `SHP-003`, `SHP-004`, `REC-003`, `SAL-006`; DoD đợt A chưa đạt).

1. Commit điểm hiện tại **trước** (chủ dự án) — đảm bảo `grep -c "^| D-111 " docs/nhanh/decisions.md` = 1 và `docs/reviews/ADDR-003-docreview.md` nằm trong commit. Rồi `/mo-phien WP-14 N27`.
2. `/doc-gate SHP-002 SHP-004` (hàng `P-04`): đưa F8–F11 của `SHP-001` vào (`Generation`, `ClientOrderCode`, `Q16`, index partial); mỗi mã một lượt vì `SHP-002` và `SHP-004` còn nợ từ `D-109`.
3. Sau đó `SHP-003` một mình, `REC-003`, `SAL-006` một mình. Ghi chú `addr-004` §6.1 theo `D-99` làm ngoài vai (§2 #1).

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-14
và handover `docs/nhanh/handover-n26.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên N26 **dừng giữa** WP-14: làm tiếp **đúng phần còn lại** ghi ở handover §2 — **không** làm lại phần đã
xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a` @ `d076f0d` (đã push) — thay đổi sau đó **chưa commit** (§5 #1, #2). `main` = `6052d14` (không đổi). Chưa PR.
- `chore/agents-b1-b2` @ `e132f07` — như `N23`…`N25`. Thứ tự merge đề xuất: PR `chore/agents-b1-b2` trước, rồi PR WP-14 (mở đầu `Refs: D-71`).

---

**Hàng rào agent** (`artifacts/agents/*.jsonl`, `canh-bao.jsonl` — chỉ lượt `N26`)

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| `ba` | 2 (áp `D-105…D-107` · áp `D-111`) | cả hai có khối JSON cuối; `git status` sau mỗi lượt chỉ có `addr-003` ngoài phần decider ghi |
| `decider` | 1 (`Q22`) | `D-111`; không trùng số `D-xx` |
| `docrev` | 1 (kiểm nhanh vòng 4) | có khối JSON cuối |
| **Tổng** | 4 | **0** lần chặn dừng (`D-104`) |

- Cảnh báo "thiếu khối JSON": **báo nhầm** (mọi hand-back có khối JSON; số lần chính xác chưa đối chiếu từng dòng `canh-bao.jsonl`). Cảnh báo "ghi ngoài vùng": **0** lần trong `N26`.
- Hạn mức 5 giờ / tuần: **chưa đo** (người không ở `/usage`).
