# Handover N76 — WP-18 / hàng `W18-0` (cổng tài liệu `ADDR-001` + `ADDR-002`) XONG

> Phiên `N76` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W18-0` · WP-18 · mã `ADDR-001`, `ADDR-002` · nhánh
> `feat/gd3-wp18-dia-chi` (base `origin/tich-hop/gd3` @ `7d15f84`) · 26/11/2026.
> Phạm vi phiên: **cổng tài liệu `D-141`** — DOCREV kiểm nhanh v0.5 đóng **2 P2 mỗi** tài liệu → **DOC-OK · 0 P1 · 0 P2** cả hai.
> **W18-0: 0 `.cs` sản phẩm, 0 migration, 0 package, 0 đổi FE/CI/compose.**

## 1. Đã xong

- **D2 ADDR-001 — DOCREV kiểm nhanh v0.5 → `DOC-OK`**: 0 P1 · **0 P2** · 19 P3.
  - P2 #1 vòng 2 (bản ghi lô TỪ CHỐI ghi UoW riêng `requiresNew: true` trước khi NÉM `0008`) **đã đóng** — E5, §8.1 #5,
    §12, BR-009 thống nhất; TC-21 ca (f) + TC-24 ca (e) kiểm lại được.
  - P2 #2 vòng 2 (khoá bận ⇒ `BusinessException` `eCommerce:Lock:0001` → 409, không `TimeoutException`) **đã đóng** —
    §8.2 #1 + TC-25 kiểm `ex.Code`; code xác nhận qua `codegraph_explore` (`LockErrorCodes.cs:20`, map `:377`).
  - `D-93` (Q2: nguồn ĐVHC production = file công bố cơ quan nhà nước, cơ quan/định dạng/đường dẫn **chưa xác định**)
    áp đúng, không sinh D1/D2 mới.
- **D2 ADDR-002 — DOCREV kiểm nhanh v0.5 → `DOC-OK`** (0 P1 · 1 P2 mới `K1` · 2 P3) → BA sửa `K1` → DOCREV kiểm nhanh
  bổ sung → **`DOC-OK` 0 P1 · 0 P2** · 2 P3 (K2, K3 để người duyệt).
  - P2 #1 vòng 2 (§12 tách chiều ngược `ResolveCarrierAddressCodeAsync` NÉM `0501`/`0502`/`AnhXaNguocKhongDuyNhat`;
    chiều xuôi `TraMaHangAsync` thành hàng riêng) **đã đóng**.
  - P2 #2 vòng 2 (§5.2 `AddrCarrierSyncRuns`: "mọi lượt **đã giành được khoá**", `D-90`) **đã đóng**.
  - `K1` (P2 mới): §1 Post-condition + §3.2 R5 còn gán chiều xuôi `Tra` cho tạo vận đơn → BA sửa (v0.6, N76),
    DOCREV xác nhận **đã đóng**; P3 K2 (BR-003 thiếu điều kiện "đã giành khoá") + K3 (thứ tự hàng v0.4/v0.5) để nguyên.
- **`D-231` (TẠM CHỐT, DEC) — sửa cổng tài liệu đỏ oan với mã 4 chữ cái**: `tools/probe-cong-tai-lieu.ps1` trích mã bằng
  `[A-Z]{3}-\d{3}` ⇒ `ADDR-001` ra chuỗi con `DDR-001` ⇒ `-WP WP-18` exit 1 dù biên bản đúng. Vai `qa` sửa **1 tệp**
  (`{3}` → `{3,4}` ở `Lay-HangWp` + `Doc-BienBan`) + 2 ca `-TuKiem` (CA7 xuôi 4 chữ cái ⇒ 0; CA8 ngược ⇒ 1). Lùi = revert 1 commit.
- **D3**: `docs/agents/nhap-bien-ban-gd3-a.md` cập nhật dòng `ADDR-001`/`ADDR-002` + tổng hợp đợt A (N76).
- **`07` §13**: thêm một dòng tiến độ W18-0 (vùng ORCH).
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build` | — | **bỏ qua có lý do**: hàng `doc-gate`, 0 `.cs` (WP-18 F0–F2b chưa mở) |
| `dotnet test` | — | **bỏ qua có lý do**: hàng `doc-gate`, 0 `.cs` |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` (mở phiên) | 0 | PASS · 244 tệp · 940 link · 1483 backtick · 0 chết |
| Baseline `npm --prefix tools run check:mermaid` (mở phiên) | 0 | 213 khối · 0 lỗi |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `check-feature-doc.ps1 -WP14` ADDR-001 | 0 | DAT (16/16 heading · 14/14 BR · đối chiếu Nhanh dòng 1028) |
| `check-feature-doc.ps1 -WP14` ADDR-002 | 0 | DAT (16/16 heading · 16/16 BR · đối chiếu Nhanh dòng 942) |
| `check-agent-artifact.ps1 -Kind docreview` ADDR-001 | 0 | DAT (verdict=DOC-OK · P1=0 · P2=0) |
| `check-agent-artifact.ps1 -Kind docreview` ADDR-002 | 0 | DAT (verdict=DOC-OK · P1=0 · P2=0) |
| `check-agent-artifact.ps1 -Kind decision` | 0 | DAT (115 dòng DEC) |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-18` | 0 | **XANH** (trước sửa: exit 1) |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-20` / `WP-21` | 0 / 0 | XANH (không hồi quy) |
| `probe-cong-tai-lieu.ps1 -TuKiem` | 0 | **10/10 ca DAT** |
| `pwsh -File tools/test-cong-kiem.ps1 -ChiKiemMoc` | 0 | **54/54 DAT · 0 HỎNG** |
| `negative-control.ps1 -Id NC-N76-1` | 0 | **exit 1 (đỏ) → exit 0 (xanh)** — đột biến `{3,4}`→`{3}` |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đóng phiên) | 0 | PASS · 244 tệp · 940 link · 1484 backtick · 0 chết |
| `npm --prefix tools run check:mermaid` (đóng phiên) | 0 | 213 khối · 0 lỗi |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W18-0`.** DoD đạt: 2 tài liệu `DOC-OK` 0 P2 · probe `Code` WP-18 xanh · link/mermaid PASS.
Hàng kế của chuỗi: **`W18-1`** (F0–F2b WP-18: đọc SRS theo mục, ARCH khi chạm ≥ 2 module/schema/contract; G1/G2 theo M2;
mã GHTK chưa có ⇒ không code nhánh đó; `ADDR-003` + PII ngoài WP).

## 3. Migration đã áp

- **Không có.** Hàng `doc-gate`, 0 `.cs`, 0 migration ⇒ không chạm DB, không cần `pg_dump` (K-01 không kích hoạt).

## 4. Quyết định mới

- **`D-231` — TẠM CHỐT (DEC, uỷ quyền)**: sửa `tools/probe-cong-tai-lieu.ps1` nhận mã tài liệu 4 chữ cái (`[A-Z]{3,4}-\d{3}`)
  + 2 ca `-TuKiem` + ca ngược, ngay trong phiên (vai `qa`, 1 tệp, lùi = revert 1 commit). Đã vào hàng đợi xem lại
  (`docs/agents/hang-doi-xem-lai.md`), `reviewBefore = mở W18-1 · trước PR tich-hop/gd3 → main`.
- **Không có `D-xx` nào khác.** Hai `docDeviation`/`openQuestions` mới: 0 (docrev trả 0, BA trả 0, qa 0).

## 5. Nợ / rủi ro

- **`D-217`④ vẫn nguyên** (chưa quá hạn): QA sửa probe đọc biên bản **người** GĐ1 (`docs/bien-ban-duyet-gd1-*.md`) làm
  nguồn ĐẠT cho `Code` + `MergeMain`, + ca ngược, + **cùng commit xoá tệp gương** `docs/bien-ban-tam-duyet-gd3-guong-q5-2026-11-25`.
  Hạn: trước hàng `W17-*` đầu tiên và trước PR `tich-hop/gd3 → main`. `D-231` **không** giải quyết nợ này (hai sửa đổi độc lập trên cùng tệp).
- **Điều kiện còn lại của tạm duyệt `D-141`**: (a) DOCREV kiểm nhanh v0.5 — **ĐẠT** cả hai tài liệu; (b) `ADDR-001`:
  nguồn ĐVHC production (`D-93`, R3-7) trước WP code đường nạp production (W18-1/W18-2 **không** làm nhánh đó);
  `ADDR-002`: mã địa chỉ GHTK (R3-1/Q1/Q2) — **không code bằng suy đoán**, dùng adapter/giả lập. `ADDR-003` + PII **ngoài** WP.
- **P3 mở**: `ADDR-001` 19 P3; `ADDR-002` 2 P3 (K2 BR-003 thiếu điều kiện "đã giành khoá"; K3 thứ tự hàng lịch sử §15.4).
  Không chặn duyệt (theo `D-141` tạm duyệt chỉ đòi 2 P2).
- **`D-217`④ + `D-230` (4 `docDeviation` SRS↔code PRC-002) + `D-227`/`D-228`** — kế thừa, chủ BA/NGƯỜI như `N75` §5.
- **Hàng đợi xem lại**: 129 quyết định tạm (N75) **+ `D-231`** = **130** dòng chờ người.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-18/W18-0 (cổng tài liệu) → mở `W18-1` (F0–F2b WP-18).**

- `/chay-phien-code chuoi=gd3-1` — bộ chạy chọn hàng kế (`W18-1`).
- `W18-1`: đọc SRS `ADDR-001`/`ADDR-002` theo mục; ARCH khi chạm ≥ 2 module/schema/contract; G1/G2 theo M2
  (mã không đỏ, DOCREV `DOC-OK` sẵn). **Không** code nhánh mã địa chỉ GHTK (R3-1) bằng suy đoán.
- Nhắc: **không** xoá tệp gương trước khi QA sửa probe (`D-217`④).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp18-dia-chi`, base `origin/tich-hop/gd3` @ `7d15f84`. Commit phiên: **`f9d7161`** (ADDR-001),
  **`b319ee4`** (ADDR-002 + K1), **`1fca19d`** (`D-231` + probe fix), cộng commit đóng phiên (handover này + `07` §13 + registry).
- **Đã push** `origin/feat/gd3-wp18-dia-chi`. **Chưa** đẩy `tich-hop/gd3` — đúng luật: chỉ đẩy nhánh tích hợp ở hàng `dong-wp` (`W18-5`) khi G6 xanh.
- Không mở PR (`D-216`③).

---

**Hàng rào agent** (phiên `N76`): **4 vai** — `docrev` (3 lượt: ADDR-001, ADDR-002 vòng 1, ADDR-002 bổ sung K1) ·
`ba` (1 lượt sửa K1) · `decider` (1 lượt, `D-231`) · `qa` (1 lượt sửa probe). **0 lần chặn dừng**; **0 tệp ngoài vùng**
(`git status` trước mỗi commit chỉ có vùng của vai đã làm). **3 cảnh báo** `artifacts/agents/canh-bao.jsonl`: `docrev`
(kiểm nhanh bổ sung), `decider`, `qa` — đều ghi "thiếu khối JSON cuối"; cả ba vẫn trả nội dung JSON đầy đủ ở cuối báo cáo
(nội dung đúng, chỉ thiếu đúng dạng rào ```json ở vị trí cuối); ORCH kiểm lại mọi số đo bằng máy trước khi dùng.
