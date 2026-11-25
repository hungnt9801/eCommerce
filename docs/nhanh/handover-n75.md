# Handover N75 — WP-16 / hàng `W16-5` (F6–G6) XONG · ĐÓNG WP-16

> Phiên `N75` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W16-5` · WP-16 · mã `PRC-002` · nhánh
> `feat/gd3-wp16-bang-gia` (base `origin/tich-hop/gd3` @ `e481094`) · 26/11/2026.
> Phạm vi phiên: **F6 OPS** (tài liệu deploy `PRC-002`) → **G6** (`kiem-tat-ca.ps1 -DayDu`) →
> **push fast-forward** nhánh WP vào `tich-hop/gd3`. **W16-5: 0 `.cs`, 0 migration, 0 package, 0 đổi CI/compose/Dockerfile.**

## 1. Đã xong

- **F6 OPS**: `docs/ops/PRC-002-deploy.md` (139 dòng, đủ 6 mục — Phạm vi · Biến môi trường · Thứ tự triển khai ·
  **Đường lùi** · Cổng · Khi hỏng) do vai `ops` viết, trong vùng ghi `docs/ops/**`.
  - Xác nhận bằng diff + đồ thị: **7 tệp sản phẩm** `modules/Pricing/**` (Setting provider, `PricingChinhSach`, mã
    `0010`/`0011`, `TinhTienDon`, `ChonDonGiaCuoi`, 2 app service) + test; **0 migration** (`D-218`②), **0 FE**
    (UI Pricing thuộc `WEB-001`), **0 package**, **0** `.github`/compose/Dockerfile.
  - **Đường lùi** có 2 tầng: (a) **cấu hình không cần deploy** — đặt 3 khoá `Pricing:*` về mặc định / xoá dòng
    `AbpSettings`; `MaxTiersPerSet = 2000000000` để vô hiệu guard `0011`; `OrderTotalNegativePolicy = Clamp` để
    không ném `0010`; (b) **revert code một-commit** `100db0a`. **0 migration ⇒ không revert migration, không `pg_dump`.**
  - Cảnh báo vận hành đã ghi: **KHÔNG** đặt `MaxTiersPerSet > 2.147.483.647` (P2 REV #1 ⇒ `OverflowException` ⇒ 500).
- **G6 (`-DayDu`)**: ORCH tự chạy, **exit 0 — 20/20 bước · 744,3 s**. Chi tiết bảng dưới.
- **G4/G5 còn xanh** (ORCH đo lại): testplan **DAT** (`BR trong SRS=21 · chưa có trong testplan=0`); review **DAT**
  (`verdict=PASS · P1=0`).
- **`07` §13**: thêm một dòng tiến độ W16-5 (vùng ORCH).
- **Đo lại cuối phiên (ORCH tự chạy — không tin số vai báo):**

| Lệnh | Exit | Số đo |
|---|---|---|
| `pwsh -File tools/kiem-tat-ca.ps1 -DayDu` | 0 | **20/20 bước · 744,3 s** |
| └ Build + kiểm mốc | 0 | **0 error · 9 warning** · 60,2 s |
| └ Unit test | 0 | **2021/2021** · 17,1 s |
| └ Integration test | 0 | **96/96** · 27,3 s |
| └ Mermaid | 0 | **213 khối · 0 lỗi** · 7,8 s |
| └ Cổng tài liệu WP-16 | 0 | **XANH** · 0,7 s |
| └ FE typecheck + Vitest | 0 | **0 lỗi · 25/25** · 9,5 s |
| └ `gen:api` | 0 | **`schema.d.ts` không đổi** · 2,2 s |
| └ Bất biến DB | 0 | **25/25** · 9,1 s |
| └ Test cổng kiểm | 0 | **60/60 DAT** · 145,8 s |
| └ E2E có phiên (`web-001`) | 0 | **DAT** · 113,3 s |
| └ Sổ địa chỉ HTTP (`ADDR-004`) | 0 | **17/17 DAT** · 23,2 s |
| └ Khách hàng HTTP (`CUS-001`) | 0 | **26/26 DAT** · 28,3 s |
| └ Thanh toán HTTP (`PAY-001`) | 0 | **30/30 DAT** · 25,7 s |
| └ Đơn sàn (`SAL-005`) | 0 | **DAT** · 50,5 s |
| └ Oversell 2 request | 0 | **DAT** · 29,8 s |
| └ Outbox (process chết) | 0 | **DAT** · 41,3 s |
| └ Outbox 2 instance | 0 | **DAT** · 43,9 s |
| └ Giữ chỗ 2 instance | 0 | **DAT** · 36,2 s |
| └ Playwright 8 luồng | 0 | **8/8** · 71 s |
| Baseline `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 error · 9 warning** · 47 s |
| Baseline `dotnet test test/eCommerce.Tests --no-build` | 0 | **2021/2021** · 9 s |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 242 tệp · 934 link · 1475 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** |
| `dotnet test --filter "FullyQualifiedName~Pricing"` (ops) | 0 | **144/144** · 625 ms |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-16` (ops) | 0 | **XANH** (`moi ma ... 'Tam duyet'`) |
| `check-agent-artifact -Kind testplan -Srs …` (ORCH) | 0 | **DAT** (BR 21 · thiếu 0) |
| `check-agent-artifact -Kind review` (ORCH) | 0 | **DAT** (PASS · P1=0) |

## 2. Chưa xong / cắt giữa

**Không có việc dở.** **WP-16 ĐÓNG** (F0 → G6). Chuỗi `gd3-1` (`D-216`④) còn các WP kế:
**WP-18 → WP-22 → WP-26 → WP-24 → WP-25 → WP-23 → WP-17** (`PH next` chọn hàng kế cụ thể).

## 3. Migration đã áp

- **Không có** — WP-16 giữ **0 migration** (`D-218`②). Phiên này **0 `.cs`**. Không chạm schema/dữ liệu ⇒
  **không cần `pg_dump`** (K-01 không kích hoạt).

## 4. Quyết định mới

- **Không có `D-xx` mới** trong phiên `N75`. `ops` trả **0 `openQuestions`**; không gọi `decider`.
- Quyết định liên quan còn hiệu lực: `D-217`…`D-224` (thiết kế W16-1), `D-229`, `D-230` (W16-4).

## 5. Nợ / rủi ro

Kế thừa `N74`, còn nguyên:

- **P2 REV #1** (`DocSoBacToiDa` tràn `int` khi `MaxTiersPerSet > 2.147.483.647` ⇒ 500): đã có **đường lùi vận hành**
  ghi trong `docs/ops/PRC-002-deploy.md` §4 (`MaxTiersPerSet ≤ 2.147.483.647`, khuyến nghị `2000000000`); vá code vẫn
  thuộc **BE** (không chặn merge).
- **P3 REV #2–#6**: XML doc ngược (`TinhTienDon.cs:84-85`), allowlist không được reader dùng, mất `LogWarning` khi
  setting sai ở `AddTierAsync`, log đường nóng, thiếu ca `PricingSettingDefinitionProvider` (R8).
- **`D-230`** — 4 `docDeviation` SRS↔code: chủ **BA** (+ **ARCH** cho `-arch`), hạn trước `W17-1`/doc-gate kế tiếp/trước
  PR `tich-hop/gd3 → main`.
- **`D-217`④** (QA): sửa `probe-cong-tai-lieu.ps1` đọc biên bản GĐ1 + ca ngược; **cùng commit xoá** tệp gương — chặn
  trước `W17-*` đầu tiên và trước PR `tich-hop/gd3 → main`.
- **`D-228`** (NGƯỜI — danh sách đỏ §2.1): `XC-001` §6.2 lệch `MoneyErrorCodes.cs`.
- **`D-227`** (BA): thêm 4 dòng TC §14 + mục "Đối chiếu mô hình Nhanh" cho `BR-PRC-002-010/011/013/014` —
  `check-feature-doc -WP14` **vẫn đỏ**, không phải cổng WP-16.
- **R8 còn treo**: provider trong `Pricing.Domain` + đọc 4 Setting ở **host + tenant** cần **khởi động Host thật** (M6) —
  bước kiểm sau triển khai bắt buộc ở deploy doc §5.2.
- **M4 trước release:** kế toán duyệt lô cho `D-219`/`D-221`…`D-224`.
- **129 quyết định tạm** của decider đang chờ người duyệt (`docs/agents/hang-doi-xem-lai.md`).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-16 (F0–G6) → mở WP-18 (hàng kế của chuỗi `gd3-1`, `PH next` chọn).**

- `/chay-phien-code chuoi=gd3-1` (bộ chạy tự chọn hàng kế tiếp của chuỗi).
- WP-18 (`ADDR-001` + `ADDR-002`) vào bằng **cổng tài liệu** (`D-141` từng dòng: DOCREV kiểm nhanh đóng 2 P2 mỗi tài liệu);
  mã GHTK không code bằng suy đoán (R3-1); `ADDR-003` + PII **ngoài** WP.
- Nhắc: **không** xoá tệp gương trước khi QA sửa probe (`D-217`④).

```text
/chay-phien-code chuoi=gd3-1
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp16-bang-gia`, base `origin/tich-hop/gd3` @ `e481094`. HEAD trước phiên: **`9ed76d6`** (9 commit
  WP-16 so với nhánh tích hợp).
- Commit đóng phiên (handover này + `07` §13 + `docs/ops/PRC-002-deploy.md` + registry) trên **cùng nhánh**; SHA ghi ở
  câu trả lời cuối.
- **Đã push** `origin/feat/gd3-wp16-bang-gia`; **đã push fast-forward** → `origin/tich-hop/gd3` (không `--force`,
  không merge `main`). Không mở PR (`D-216`③).
- Nhánh tích hợp `tich-hop/gd3` giờ chứa **toàn bộ WP-16** — sẵn sàng cho WP-18 cắt từ đỉnh.

---

**Hàng rào agent** (phiên `N75`): **1 vai** — `ops` (1 lượt, JSON cuối **đúng**, `filesChanged` chỉ
`docs/ops/PRC-002-deploy.md`, **0 tệp ngoài vùng**, **0 lần chặn**). Ghi nhận **1 cảnh báo** trong
`artifacts/agents/canh-bao.jsonl` là của vai `rev` **phiên `N74`** (log trễ, `lanChan=0`, không phải N75). ORCH tự chạy
baseline + `-DayDu` + G4/G5; không chạy song song build/test (tránh hết bộ nhớ `N69`).
