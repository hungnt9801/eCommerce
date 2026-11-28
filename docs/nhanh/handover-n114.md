# Handover N114 — WP-17 / hàng `W17-1` (F0–F2b) XONG: ARCH động cơ giá vốn `PRC-001` + DOCREV **DOC-OK** (P1=0 · P2=0)

> Phiên `N114` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W17-1` (loại `code`, bước `F0–F2b`, vai `ORCH · arch · docrev`) ·
> WP-17 "giá vốn" · mã `PRC-001` · nhánh `feat/gd3-wp17-gia-von` (base `origin/tich-hop/gd3` @ `5c902ed`) · 28/11/2026.
> Phạm vi: viết **`docs/features/pricing/PRC-001-arch.md`** (động cơ bình quân gia quyền di động + đường ghi/duyệt `CostPrice` +
> port đọc chéo module) và **`docs/reviews/PRC-001-arch-docreview.md`** qua 3 vòng DOCREV. **0 `.cs` sản phẩm · 0 tệp FE · 0 migration · 0 `pg_dump`.**

## 1. Đã xong

| Bước | Vai | Kết quả |
|---|---|---|
| F0 chuẩn bị | ORCH | Cổng vào `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` **XANH (exit 0)**; đọc SRS `prc-001` §20/§15.1/§5.2, `inv-001` §0, `D-177`/`D-186`/`D-181`/`D-318 C4/C5/C6/C9`/`D-09` |
| F2 ARCH | `arch` (Opus) | `docs/features/pricing/PRC-001-arch.md` **557 dòng**, 10 mục: ranh giới một chiều (`Pricing → Inventory.Application.Contracts` qua port trần `ICostFeedLookup`; adapter `CostFeedLookupAdapter`; không `Inventory.Domain`); hợp đồng additive (`ICostPriceLookup` port trần + `ICostPriceAdminAppService` + `ICostPriceAppService` mở rộng); schema expand-only (**bảng mới `PrcCostEngineRuns`** + **4 cột nullable `PrcCostPrices`** + filtered unique index NND); luồng a–f; công thức BQGGQ có ví dụ số; **ca K-06 (K06-01…13 + 08b)**; Q4 giữ Open; đường lùi từng quyết định |
| F2b DOCREV vòng 1 | `docrev` | **DOC-CHANGES** P1=1 · P2=10 (P1: đọc qua app service có `[Authorize]` ⇒ job nền nổ; P2: mã lỗi, lưu kỳ `null`, revision, idempotency, `RunKey`, `OtherOut=21`, `Range`, DTO…) |
| F2b ARCH sửa | `arch` | Đóng **11/11 mustFix**: port trần `ICostPriceLookup` + adapter; mã lỗi `0014`/`0015`; **không INSERT kỳ `avg=null`**; **từ chối** nhập lùi kỳ đã duyệt (NÉM `0015`); `IdempotencyKey` bắt buộc; duyệt lặp NÉM `0009`; `RunKey` tìm-trước; `OtherOut=21`; nhánh nhập có giá sau `costMissing`; `Range` `numeric(18,4)`; DTO thay tuple |
| F2b DOCREV vòng 2 | `docrev` | **P1=0 · P2=2 mới** (thiếu `ProjectReference` Reporting→Pricing.Contracts; "trả run cũ" bỏ qua `TrangThai`) |
| F2b ARCH sửa | `arch` | Thêm `ProjectReference` + đường lùi; `ThanhCong` trả cũ / `DangChay` ⇒ 409 / `Loi` ⇒ retry `Attempt++`; **ca K06-08b** |
| F2b DOCREV vòng 3 | `docrev` | **`DOC-OK` · P1=0 · P2=0 · P3=10** |

**Baseline mở phiên (`N114`):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 Error · 9 Warning** |
| `dotnet test test/eCommerce.Tests --no-build` | 0 | **2650 passed · 0 failed · 0 skipped** |
| `pwsh -File tools/kiem-link-tai-lieu.ps1` (đầu phiên) | 0 | PASS · 1039 link · 1953 backtick · 0 chết |
| `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` | 0 | **XANH** |
| `docker compose ps` | 0 | `ecommerce-postgres` + `ecommerce-redis` healthy (5433/6380) |

**DoD của hàng — ORCH tự chạy (không tin số vai báo), đo lại sau commit `f2534b4`:**

| Cổng | Lệnh | Số đo |
|---|---|---|
| DOCREV `PRC-001` ARCH | `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/PRC-001-arch-docreview.md` | **exit 0** · `verdict=DOC-OK` · P1=0 · P2=0 · P3=10 |
| Tài liệu | `pwsh -File tools/kiem-link-tai-lieu.ps1` | **PASS** — 0 tham chiếu chết (1041 link · 1962 backtick lần đo cuối) |
| Cổng `Code` WP-17 | `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-17` | **exit 0 · XANH** (vẫn xanh sau khi thêm ARCH) |
| Quyết định | `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | **exit 0** · 114 dòng (không có `D-xx` mới) |
| TODO trong ARCH | `grep -c TODO` | **0** |
| Số ca K-06 trong ARCH | `grep -c "K06-"` | **17** (K06-01…13 + 08b + tham chiếu) |
| Build/test | không có `.cs` đổi ⇒ giữ baseline | **0 error/9 warning · 2650/2650** |

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W17-1`.** Các mục dưới là **cắt có quyết định**:

- **P3=10** của DOCREV (không chặn G2): giữ trong `docs/reviews/PRC-001-arch-docreview.md`; xử ở `W17-2` nếu chạm code.
- **`openQuestions` `PRC-001-AQ1…AQ5`** trong ARCH — chuyển sang hàng `W17-1n` (G2, `decider`):
  `AQ1` = `Q4` luồng duyệt 2 bước (**người/kế toán**, vẫn `Open`); `AQ2` nhập lùi kỳ đã duyệt (người, nếu muốn revision thay vì từ chối);
  `AQ3` loại chuyển động + tiền tệ đơn giá nhập (DEC + BA); `AQ4` nhập thiếu `UnitPrice` (DEC); `AQ5` nhịp chạy động cơ (DEC).
- **G2 chưa duyệt trong phiên này**: theo `D-318` C4, việc duyệt thay thuộc hàng **`W17-1n`** (`loai: duyet-dec`, phụ thuộc `W17-1`) — phiên này chỉ đóng F2b.

## 3. Migration đã áp

**Không có.** 0 `.cs`, 0 migration, không chạm DB ⇒ không `pg_dump` (K-01 không kích hoạt). ARCH chỉ **mô tả** migration cho `W17-2`.

## 4. Quyết định mới

- **Không có `D-xx` mới** trong phiên. `D-319` (tạm duyệt G1) và `D-318` (uỷ quyền G2 7 điều kiện) giữ nguyên hiệu lực.
- `Q4` **vẫn `⬜ Open`** — ARCH ghi thành `PRC-001-AQ1`, không tự chốt (`D-318` C2).

## 5. Nợ / rủi ro

- 🟡 **`W17-1n` (G2)** — `decider` duyệt thay theo `D-318` C4, phải thoả **đủ 7 điều**; ARCH đã tự chứng 7/7 (additive · cột nullable · enum cuối · 0 chạm `XC-001`/`RoundingPolicy` · tiền qua `Money` · **K-06 có** · bảng schema + revert). Nếu `decider` thấy thiếu ⇒ `CHUYEN_NGUOI` (`PH blocked`).
- 🟡 **`Q4`** (`CostPrice` duyệt 2 bước) — kế toán trưởng (M4/R3-3).
- 🟡 **`PRC-001-AQ3/AQ4/AQ5`** — câu kỹ thuật/nghiệp vụ còn mở, cần DEC trước `W17-2` (ảnh hưởng trực tiếp code đường ghi + động cơ).
- 🟡 **P3=10** tài liệu ARCH — không chặn G2, dọn dần khi chạm code.
- 🟡 Nợ kế thừa (không thuộc phiên): `check-feature-doc` của `prc-001`/`inv-001` (nợ WP-14); `D-186` dẫn tệp sai dòng; P2 retry storm WP-23.

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG `W17-1` → mở `W17-1n` (G2 — `decider` duyệt thay theo `D-318` C4).**

- `W17-1n`: gọi `decider` **một lần** với đúng 7 tiêu chí `D-318` C4 + bằng chứng: `docs/features/pricing/PRC-001-arch.md` + `docs/reviews/PRC-001-arch-docreview.md` (`DOC-OK`, P1=0/P2=0). Đạt ⇒ thêm dòng vào `docs/bien-ban-tam-duyet-gd3-wp17-<ngày>.md` + hàng đợi xem lại; thiếu ⇒ `PH blocked`.
- Sau G2: `W17-2` (F3 BE — `be` Opus cho lõi sổ cái; migration expand-only + `pg_dump` trước khi áp).

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-17
và handover gần nhất `docs/nhanh/handover-n114.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng W17-1n** (phiên N115). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp17-gia-von`; base `origin/tich-hop/gd3` @ **`5c902ed`**.
- Commit phiên: **`f2534b4`** — `docs(pricing): N114 - W17-1 F2 ARCH gia von DOC-OK (PRC-001)` (4 tệp: ARCH 557 dòng + docreview 105 dòng + registry 2 tệp).
- **Đã push** `origin/feat/gd3-wp17-gia-von`. **Chưa** đẩy `tich-hop/gd3` (chỉ `dong-wp` mới FF), **không** force/merge, **không** mở PR (`D-216` ③), **không** đụng `main`.

---

**Hàng rào agent** (phiên `N114`): `arch` ×1 agent (3 lần gọi liên tiếp do chạm trần 30 lượt — cùng một ngữ cảnh) · `docrev` ×1 agent (3 vòng) · **0 tệp ngoài vùng** · **3 cảnh báo** `artifacts/agents/canh-bao.jsonl` (docrev "thiếu khối JSON cuối" do hook không đọc được transcript — **cả 3 báo cáo đều có khối JSON đầy đủ**, ORCH tự kiểm mọi số đo bằng máy).
