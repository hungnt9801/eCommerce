# Handover `N25` — WP-14 đợt A: `SHP-001` đạt `DOC-OK`; `ADDR-003` còn áp `D-105…D-107`; còn 5 tài liệu đợt A

> Phiên `N25` · 18–19/11/2026 · nhánh `docs/n22-wp14-dot-a` · `main` = `6052d14`.
> Mở bằng `/mo-phien WP-14 N25`; phiên này chạy `/doc-gate SHP-001` (một tài liệu 134 KB một mình, hàng `P-03` của `docs/agents/bang-phien-wp14.md`).
> **Không** làm phần dở của `ADDR-003` (handover-n24 §2 #1–#2): vẫn nguyên.

## 1. Đã xong — việc + số đo (kèm lệnh kiểm)

**Baseline lúc mở phiên** (HEAD `3731dea`):

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | PASS — 741 link, 805 đường dẫn, 0 chết |
| `npm --prefix tools run check:mermaid` | 0 | 212 khối, 0 lỗi |
| `node --test .claude/hooks/test/hooks.test.mjs` | 0 | 34/34 pass |
| `dotnet build` / `dotnet test` / `test-cong-kiem.ps1` | — | bỏ qua: WP thuần tài liệu (`D-71`), không sửa `tools/` |

**Việc đã làm:**

1. Commit `3731dea` (đã push): `chore(agents): N25 - tat chan dung khi thieu JSON (D-104)` — 3 tệp `.claude/` (`scopes.json`, `check-agent-stop.mjs`, `hooks.test.mjs`); hook 34/34 pass sau commit; `grep -c "^| D-104 |"` = 1.
2. `/doc-gate SHP-001`:

| Bước | Kết quả |
|---|---|
| D1 BA đối chiếu | xong; `check-feature-doc.ps1 -WP14` exit 0 (16/16 mục, 20/20 BR có test) |
| D1q decider `Q11` | `D-108` TẠM CHỐT — chọn (b) snapshot giao trên đơn, **không** theo khuyến nghị (a) của BA (trái `ADDR-004` đã duyệt) |
| BA áp `D-108` | exit 0; 21/21 BR; thêm `Q13`, `Q14` |
| D1q decider `Q14`, rồi `Q13` (tuần tự) | `D-109` (`ClientOrderCode` theo `Generation`, ≤ 50 ký tự — tra web GHN 18/11/2026) · `D-110` (cửa tra mã → mã hãng nhận mã) |
| BA áp `D-109` + `D-110` | exit 0; 22/22 BR, 48 TC |
| D2 DOCREV vòng 1 | `DOC-CHANGES` · **5 P1** · 11 P2 · 11 P3 |
| BA sửa mọi P1 + P2 | exit 0; 22 BR, 52 TC (thêm TC-49…TC-52); thêm `Q15…Q20`, `F11` |
| D2 DOCREV vòng 2 | `DOC-OK` · 0 P1 · 2 P2 (do bản sửa sinh ra) · 16 P3 |
| BA sửa 2 P2 (#28 §13 NFR-004, #29 DoR §14.4) + #34 | exit 0 |
| D2 DOCREV vòng kiểm nhanh | **`DOC-OK`** · 0 P1 · **0 P2** · 17 P3 (`check-agent-artifact.ps1 -Kind docreview` exit 0) |
| D3 nháp biên bản | dòng `SHP-001` trong `docs/agents/nhap-bien-ban-gd3-a.md`: **trình duyệt** sau khi người xem `D-108…D-110` |

**Đo lại lúc đóng phiên:**

| Lệnh | Exit | Số đo |
|---|---|---|
| `kiem-link-tai-lieu.ps1` | 0 | PASS — 753 link, 821 đường dẫn, 0 chết |
| `check:mermaid` | 0 | 212 khối, 0 lỗi |
| `hooks.test.mjs` | 0 | 34/34 pass |
| `check-agent-artifact.ps1 -Kind decision` | 0 | DAT — 27 dòng DEC |
| `check-agent-artifact.ps1 -Kind docreview` (`SHP-001`) | 0 | `DOC-OK` · P1=0 · P2=0 |
| `check-feature-doc.ps1 -WP14` (`SHP-001`) | 0 | DAT — 16/16 mục, 22/22 BR có test, 18 dòng `Open`, 0 TODO |
| `.cs` / migration đổi | — | **0** / **0** |

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**`SHP-001` xong D1–D3; D4 đạt về link/mermaid/0 `.cs`.** Điểm xanh. Còn lại của đợt A:

1. ⏭ **`ADDR-003`** — BA áp `D-105…D-107` (§15.1 `Q12`/`Q15b`/`Q17`/`Q18`/`Q19`; `freeText` tách bảng §5.2/§6.1/§11/TC-36; `Withdrawn` §6.2/§12/§14), rồi D1s + DOCREV kiểm nhanh. Không đổi so với handover-n24 §2 #1.
2. ⏭ Ghi chú `addr-004` §6.1 theo `D-99` (1 dòng) — vai `ba` không có vùng ghi ⇒ ORCH/người (handover-n24 §2 #2).
3. ⏭ `/doc-gate SHP-002 SHP-004` (hàng `P-04`), `SHP-003` một mình (`P-05`), `REC-003` (`P-06`), `SAL-006` một mình (`P-07`).
4. ⏭ **Sửa tài liệu khác do `D-108…D-110` mà vai `ba` không có vùng ghi trong lượt `SHP-001`** (ghi ở `shp-001` §15.2 F8–F11): `SHP-002` §5.2 (cột `Generation`, `ShipmentStatus` số — `Q16`, index partial) + §7.3 + §8.2 (khoá `ecommerce:shipping:`); `SHP-004` (`ClientOrderCode`, dòng ~286/342/621/635); `ADDR-002` §0/§6.1/§14 (cửa mã `D-110`, chọn kiểu `Level`). **Làm trong `/doc-gate` của chính tài liệu đó**, không qua vai `ba` của `SHP-001`.
5. ⚠️ **Cây làm việc chưa commit** — xem §5 #1.

## 3. Migration đã áp

**Không có** (WP thuần tài liệu, `D-71`).

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

- **TẠM CHỐT (DEC)** — `D-108` (địa chỉ nhận = snapshot trên đơn qua hợp đồng Sales `IOrderShippingDirectory`; xem lại **trước release**) · `D-109` (`ClientOrderCode = {OrderId:N}-{Generation}`, cột `ShpShipments.Generation`, unique `(TenantId, OrderId, Generation)` — trước release) · `D-110` (`ICarrierAddressCodeDirectory.ResolveCarrierAddressCodeByCodeAsync`, 0 migration — xem lại **trước WP-15**).
- **Không có quyết định của người** trong phiên này. `D-102` + `D-104` vẫn `ĐỀ XUẤT` (người duyệt).

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Chưa commit:** `shp-001-…md` (+ `docs/reviews/SHP-001-docreview.md` mới), `decisions.md` (`D-108…D-110`), `hang-doi-xem-lai.md`, `nhap-bien-ban-gd3-a.md`, `handover-n25.md`, dòng `07` §13 — mất `D-108…D-110` + `DOC-OK` nếu cây bị ghi đè (bài học `5aa3dce`). **Nhận chủ: chủ dự án (commit).**
2. 🚨 **7 tệp chưa theo dõi/sửa không thuộc dây chuyền `SHP-001`:** `agents/state/phien-wp14.json`, `tools/agents/phien.ps1`, `tools/agents/test-phien.ps1`, `docs/agents/bang-phien-wp14.md`, `phien-uy-quyen-cau-hoi.md`, `uy-quyen-chu-du-an.md`, và `ke-hoach-v2-claude-code-pro.md` bị sửa (ghi lúc 23:17 18/11). Là hạ tầng **bảng phiên chạy** (`/chay-phien`). Hook gán nhầm cho vai `ba`. **Phiên này không xoá, không restore, không đưa vào commit đề xuất.** Chủ: chủ dự án (commit riêng).
3. ⚠️ **Hàng `P-03` của bảng phiên chưa đóng:** phụ thuộc `U-02` (phiên uỷ quyền) chưa làm ⇒ `phien.ps1 start` sẽ exit 2; `doisoat -Id P-03` đề xuất "làm tiếp" vì handover-n24 §2 còn nhắc `SHP-001`. Sau khi commit handover này, chạy lại `doisoat`; ghi `done` chỉ khi chủ dự án chấp nhận làm trước `U-02`.
4. ⚠️ **SQL + UoW chưa đo** (máy không có `psql`/docker): index partial `(TenantId, ProviderCode, ProviderTrackingCode) WHERE … IS NOT NULL`, các CHECK `Outcome`/`AttemptNo`, unique `Generation`; P1 #1 (`[UnitOfWork(isTransactional: false)]` chống `TransactionBehavior.Auto` ở host `:94`) chỉ xác nhận theo thiết kế. Bằng chứng thật: TC-49, TC-09 (Testcontainers) ở WP code. Nhận chủ: QA.
5. ⚠️ **18 dòng `Open` ở §15 của `SHP-001`**, gồm `Q4`, `Q5`, `Q15…Q20` BA mở thêm **chưa qua decider** (không chặn DoD WP-14). DoR §14.4: `Q16` (số enum `ShipmentStatus`, không đổi được sau migration — `D-17`) và `Q20` (`ShpShipmentCreateAttempts.TenantId` NOT NULL vs 1 tenant `NULL`) **chặn migration đầu**. Nhận chủ: decider ở lần `/doc-gate SHP-002` hoặc trước WP code.
6. ⚠️ Docreview `SHP-001`: 17 P3 mở (#30 `TotalWeightGrams` ghi như đã chốt khi `Q5` còn mở; #31 TTL theo từng khoá chưa có chỗ áp; #33 TC-50 thiếu `TenantId = NULL` ở Input; #35 F5 và hàng `D-89` chưa nhắc `D-110`; #36 header còn `v0.5` trong khi changelog có v0.5.1…) — để người duyệt.
7. ⚠️ **Phụ thuộc thứ tự code mới** (đưa vào đề xuất §WP-15+): cột snapshot giao trên `Order` + `IOrderShippingDirectory` (`SAL-002`, đóng `addr-004` F9) phải có **trước** WP code của `SHP-001` (`D-108` N2); `D-108` N3: snapshot là dữ liệu cá nhân, thuộc nợ ẩn danh `addr-004` ADR-11.
8. Nợ N22–N24 **còn nguyên**: `ADDR-003` áp `D-105…D-107` · P2 mở `NeedsReview`/`MappingSuperseded` (`D-107`, sửa trước WP-15) · `D-101`/`D-105` chặn release · `EInvoice` băm `tinh.Name`/`xa.Name` · `D-93` file ĐVHC · `XC-003` tự mâu thuẫn · `MSB3491` · `-ChiKiemMoc` 43/50.

**Chờ người** (gom một lần):

| # | Câu | Khuyến nghị | Đã làm tiếp |
|---|---|---|---|
| 1 | `SHP-001-Q12` — mã hãng GHN còn dùng phường/xã cũ không, khi một đích có > 1 ánh xạ `DaXacMinh` (`D-89` N2)? (`needs = NCC`) | (a) chặn vận đơn tới khi người vận hành bỏ xác nhận bớt một ánh xạ | thiết kế trung lập (a), nhánh bị chặn ghi ở §12 |
| 2 | `SHP-001-Q3` — API GHN có tra theo `ClientOrderCode` không; hành vi với mã của đơn đã huỷ? (`needs = NCC`) | người đã liên hệ GHN xác nhận | `D-109` đúng trong cả hai khả năng (có `Generation`) |
| 3 | Xem + duyệt `D-108`, `D-109`, `D-110` (TẠM CHỐT) và `D-102`/`D-104` (ĐỀ XUẤT) | duyệt `D-108` (khớp `ADDR-004` đã duyệt) | công tắc/cách lùi ghi ở từng dòng |
| 4 | Commit điểm hiện tại (§5 #1) + quyết định hạ tầng bảng phiên (§5 #2, #3): có đưa vào nhánh WP-14 không, và `P-03` có `done` trước `U-02` không | commit tài liệu WP-14 trước; hạ tầng `phien.ps1` commit riêng | không đụng |

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: DỞ DANG WP-14 → mở lại WP-14** (`SHP-001` `DOC-OK`; `ADDR-003` còn áp `D-105…D-107`; còn `SHP-002`, `SHP-003`, `SHP-004`, `REC-003`, `SAL-006`; DoD đợt A chưa đạt).

1. Commit điểm hiện tại **trước** (chủ dự án) — đảm bảo `grep -c "^| D-10[8-9] \|^| D-110 " docs/nhanh/decisions.md` = 3 và `docs/reviews/SHP-001-docreview.md` nằm trong commit. Rồi `/mo-phien WP-14 N26`.
2. `/doc-gate ADDR-003` từ bước **BA áp `D-105…D-107`** (handover-n24 §2 #1; một tài liệu/phiên) → D1s → DOCREV kiểm nhanh → D4.
3. Sau đó `/doc-gate SHP-002 SHP-004` (đưa F8–F11 của `SHP-001` vào: `Generation`, `ClientOrderCode`, `Q16`); `SHP-003` và `SAL-006` mỗi phiên một mình.

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-14
và handover `docs/nhanh/handover-n25.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md` §4, ghi số.
Phiên N25 **dừng giữa** WP-14: làm tiếp **đúng phần còn lại** ghi ở handover §2 — **không** làm lại phần đã
xong, **không** mở rộng phạm vi. Kết thúc bằng đo lại + cập nhật handover.
```

## 7. Trạng thái nhánh

- `docs/n22-wp14-dot-a` @ `3731dea` (đã push) — thay đổi sau đó **chưa commit** (§5 #1, #2). `main` = `6052d14` (không đổi). Chưa PR.
- `chore/agents-b1-b2` @ `e132f07` — như `N23`/`N24`. Thứ tự merge đề xuất: PR `chore/agents-b1-b2` trước, rồi PR WP-14 (mở đầu `Refs: D-71`).

---

**Hàng rào agent** (`artifacts/agents/*.jsonl`, `canh-bao.jsonl` — chỉ lượt `N25`)

| Vai | Số lượt gọi | Ghi chú |
|---|---|---|
| `ba` | 5 (đối chiếu · áp `D-108` · áp `D-109`+`D-110` · sửa P1/P2 · sửa 2 P2) | mọi lượt có khối JSON cuối |
| `decider` | 3 (`Q11`, `Q14`, `Q13` — tuần tự, không trùng số `D-xx`) | `D-108`, `D-109`, `D-110` |
| `docrev` | 3 (vòng 1, vòng 2, kiểm nhanh) | mọi lượt có khối JSON cuối |
| **Tổng** | 11 | **0** lần chặn dừng (`D-104`) |

- Cảnh báo "thiếu khối JSON" hiện ra trong phiên: 5 lần (đếm từ các thông báo hook, chưa đối chiếu `canh-bao.jsonl` từng dòng), **đều báo nhầm** (mọi hand-back có khối JSON; `D-104` đã tắt chặn).
- Cảnh báo "ghi ngoài vùng" cho `ba` (7 tệp): **báo nhầm** — hạ tầng bảng phiên, mốc ghi 23:17 không khớp lượt BA; không xoá (§5 #2). Số lần ghi ngoài vùng **thật** lọt qua: **0** (`git diff --stat` sau mỗi lượt BA chỉ có `shp-001`).
- Hạn mức 5 giờ / tuần: **chưa đo** (người không ở `/usage`).
