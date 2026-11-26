# Handover N93 — WP-24 / hàng `W24-1` (F0–F2b) XONG — **mở F3 BE (`W24-2`)**

> Phiên `N93` · chuỗi `gd3-1` (`D-216` phương án B) · hàng `W24-1` · WP-24 · mã `PRO-002` · module `Promotions` (**mới**) ·
> nhánh `feat/gd3-wp24-dieu-kien-km` (base `origin/tich-hop/gd3` @ `84157a1`) · 27/11/2026.
> Phạm vi phiên: **F0** (đọc SRS theo mục + danh sách người review K-08) → **F2** `arch` viết `PRO-002-arch.md` v0.3 →
> **F2b** `docrev` **DOC-OK 0 P1/0 P2** (3 vòng) → **G1/G2 theo M2** (`D-169` (3) + `D-277`). **0 dòng code**, **0 migration**.

## 1. Đã xong

- **F0** — đọc SRS `PRO-002` v0.9 theo mục (§0 ADR-01…15, §1, §3, §5.1–§5.3, §6.1–§6.4, §15) + `docs/reviews/PRO-002-docreview.md`
  (DOC-OK vòng 4, 0 P1/0 P2, 2 P3). **Người review bắt buộc (K-08, `design-doc-guidelines.md` §6):** tài liệu có **state machine**
  ⇒ **Kiến trúc trưởng + BA**; chạm tiền (`DiscountTotal`, VAT) ⇒ **+ kế toán** trước release. Không có tích hợp NCC/pháp lý.
- **Cổng vào WP-24** — `pwsh -File tools/probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-24` = **exit 0 (XANH)**: mọi mã của WP-24
  đều `Tạm duyệt` trong biên bản (`D-169`).
- **F2 ARCH** — `docs/features/promotions/PRO-002-arch.md` **mới, 601 dòng, 15 mục** (khuôn `REP-001-arch.md`), v0.1 → **v0.3**:
  - §2 **ranh giới**: module `Promotions` = **4 project**; `Sales.Application` **chỉ** thêm `Promotions.Application.Contracts`
    (in-process `IPromotionEngine`); `Pos` **0** tham chiếu; `ProPromotionRedemptions.OrderId` là **tham chiếu mềm** (không FK xuyên module);
    ngoại lệ **duy nhất** (v0.3): `Promotions.Application ──▶ Messaging.Domain` **chỉ** `IEventCoverageProbe`
    (`IEventCoverageProbe.cs:17`; tiền lệ `Sales.Application.csproj:16`).
  - §5 **EF hợp nhất** `eCommerceDbContext` + `ConfigurePromotions`, **7 `DbSet`** (BR-18), **7 bảng** `Pro*`;
    §5.5 **4 index `UNIQUE NULLS NOT DISTINCT` + filter** của `ProPromotionRedemptions` ghi rõ `UNIQUE` (đóng P1 vòng 1).
  - §9 **một** migration `Add_Promotion_Module` (EF sinh, expand-only), **không** `btree_gist` (chỉ Tax/Reporting dùng),
    `pg_dump` trước khi áp, đường lùi = cờ Setting + revert code + **giữ bảng**.
  - §4.4 **13 mã lỗi → HTTP** (`eCommerce:Promotion:0001–0010` + `Money:0001` + `Messaging:0001/0002`); `0119/0104/0105/0115` thuộc `PRO-001`.
  - §6.2 `PromotionAppliedEto` **mang dữ liệu ⇒ fail-closed** (không `INotificationEvent`); §6.1 thêm guard
    `CheckoutInput.OrderDiscount ≠ 0 ⇒ NÉM` (`D-143` (2), as-built `CheckoutAppService.cs:109` truyền thẳng).
  - §12.1 **5 `docDeviation`** (D1 enum chuỗi vs int as-built; D2 `char(3)` vs `varchar(8)`; D3 **không có** `IntegrationEventBase`;
    D4 namespace `.Contracts` vs gốc module; D5 `OrderCancelledEto` **chưa có** — SAL-004) + **D6** guard `OrderDiscount`.
  - §11 **10 câu mở** đã được `decider` quyết (xem §4); **Q8** (điều kiện địa chỉ/vùng giao) **giữ chặn**, 0 rule/cột/DTO.
- **F2b DOCREV** — `docs/reviews/PRO-002-arch-docreview.md` (`docrev`, ngữ cảnh sạch), **3 vòng**:
  vòng 1 `DOC-CHANGES` **P1=1/P2=6/P3=6** → vòng 2 `DOC-CHANGES` **P1=0/P2=1** (P2 mới: ranh giới `IEventCoverageProbe`) →
  vòng 3 **`DOC-OK` P1=0/P2=0**, còn **1 P3** (thu hẹp allowlist test `eCommerce.Messaging.EventCoverage` + nêu tên test → `W24-2`).
- **G1/G2 theo M2** — `decider` ghi **6 dòng** `D-277`…`D-282`; ORCH thêm **§3** vào
  `docs/bien-ban-tam-duyet-gd3-b-2026-11-24.md` (tạm duyệt **`PRO-002-arch.md` v0.3** cho **F3 trên nhánh WP**; **không** phải biên bản
  người, **không** merge `main`).

| Lệnh | Exit | Số đo |
|---|---|---|
| Baseline `dotnet build eCommerce.slnx --no-incremental` (mở phiên) | 0 | **0 error · 9 warning** (không warning mới) |
| Baseline `dotnet test test/eCommerce.Tests --no-build` | 0 | **2376/2376** · 0 skipped |
| Baseline `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · 284 tệp · 966 link · 1713 backtick · 0 chết |
| Baseline `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |
| `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-24` | 0 | **XANH** — mọi mã WP-24 `Tạm duyệt` |
| `check-agent-artifact.ps1 -Kind docreview -Path docs/reviews/PRO-002-arch-docreview.md` | 0 | **DAT** · `verdict=DOC-OK` · **P1=0 · P2=0** |
| `check-agent-artifact.ps1 -Kind decision -Path docs/nhanh/decisions.md` | 0 | **DAT** · 117 dòng cần kiểm |
| `npm --prefix tools run check:mermaid` (sau ARCH) | 0 | **214 khối · 0 lỗi** |
| **Đo lại lúc đóng** `dotnet build eCommerce.slnx --no-incremental` | 0 | **0 error · 9 warning** · 96,1 s |
| **Đo lại** `dotnet test test/eCommerce.Tests --no-build` | 0 | **2376/2376** · 13 s |
| **Đo lại** `pwsh -File tools/kiem-link-tai-lieu.ps1` | 0 | **PASS** · **285** tệp · **969** link · **1719** backtick · 0 chết |
| **Đo lại** `node --test .claude/hooks/test/hooks.test.mjs` | 0 | **49/49** pass |

> Không chạy `eCommerce.IntegrationTests` và `kiem-tat-ca.ps1` phiên này: hàng `W24-1` **0 tệp `.cs`/`.sql`/schema/route** ⇒
> không có bề mặt để integration/e2e kiểm thêm; G6 thuộc `W24-5`.

## 2. Chưa xong / cắt giữa

**Không có việc dở của `W24-1`** — DoD của hàng đã đạt (F0 + F2 + F2b `DOC-OK` + G1/G2 theo M2). **WP-24 còn 4 hàng**: `W24-2` (F3 BE),
`W24-3` (F3 FE), `W24-4` (F4 QA + F5 REV), `W24-5` (F6 OPS + G6).

Việc **bắt buộc** `W24-2` phải làm theo `D-277` (đừng để rơi):
1. **P3 còn lại** của tài liệu ARCH: allowlist test ghi **`eCommerce.Messaging.EventCoverage`** (không `eCommerce.Messaging.*`) + nêu **tên test**
   (vd `RanhGioiPromotionsTests`) ở §10.2.
2. **5 `docDeviation` D1–D5** — BA ghi rõ trong SRS (enum chuỗi; `char(3)`; ETO tự khai trường thay `IntegrationEventBase`; namespace `.Contracts`;
   `OrderCancelledEto` chờ SAL-004).
3. **D6** — guard `CheckoutInput.OrderDiscount ≠ 0 ⇒ NÉM` ở `CheckoutAppService` (as-built đang truyền thẳng `:109`) + test.
4. **2 P3 của SRS** (`PRO-002-docreview.md` #1: §15.4 `54 → 55`; #2: delta `pro-001` §6.1 :467/:469 trích số dòng cũ → đổi sang **tên mục**).
5. `ProPromotionRuleCosts` seed qua `IDataSeedContributor` (upsert, **0 DELETE**) — **không** `INSERT` tay trong migration.

## 3. Migration đã áp

- **Không có** migration nào trong phiên này (hàng thiết kế thuần). **0 `pg_dump`** (không chạm schema/dữ liệu).
- `W24-2` sẽ tạo **một** migration `Add_Promotion_Module` (7 bảng `Pro*`, expand-only); **trước khi áp** phải `pg_dump`
  (`artifacts/pg-dump/N94-W24-2.dump`) — DB dev **đang có dữ liệu**.

## 4. Quyết định mới

`decider` ghi 6 dòng, `check-agent-artifact -Kind decision` **DAT**:

| D-xx | Trạng thái | Nội dung (ngắn) |
|---|---|---|
| `D-277` | TẠM CHỐT (DEC) | **G2**: tạm duyệt `PRO-002-arch.md` v0.3 (DOC-OK v3, 0 P1/0 P2) để code **F3 trên nhánh WP**; **không** biên bản người / **không** merge `main`; kèm D1–D5 + D6 + P3 |
| `D-278` | TẠM CHỐT (DEC) | **AQ1**: 7 trần là **ABP Setting** `64/8/32/1024/50/512/5000`, `MaxDepth` cứng `1…8`; **AQ2** trigger DB: **đã có tiền lệ** `D-159`, không dòng mới |
| `D-279` | TẠM CHỐT (DEC) | **AQ3/AQ7/AQ8/AQ9**: chưa cưỡng chế 4 mắt (chỉ `.Approve`); giữ **hai** context; job duyệt tenant bằng `DISTINCT TenantId` + `ICurrentTenant.Change`; `OrderCancelledEto` ở `Sales.Application.Contracts`, hoãn consumer tới SAL-004 |
| `D-280` | TẠM CHỐT (DEC) | **AQ5/AQ6**: lỗi nạp context khách ⇒ lá `false`, đơn vẫn tạo (**fail-closed**); lỗi cấu hình `0001/0002/0004/0010` **NÉM ở mọi nguồn** (`D-154` (5)) |
| `D-281` | TẠM CHỐT (DEC) | **AQ4**: v1 `Source = Internal`; **cấm** cộng khuyến mãi sàn vào `DiscountTotal` |
| `D-282` | **ĐỀ XUẤT (DEC)** | **AQ10/Q8**: điều kiện địa chỉ/vùng giao **tiếp tục chặn** (0 rule/cột/DTO) — **CHUYỂN NGƯỜI** (R3-1, GHTK) |

Hàng đợi xem lại: `docs/agents/hang-doi-xem-lai.md` thêm 6 dòng tương ứng.

## 5. Nợ / rủi ro

- 🟡 **P3 ARCH còn 1** — allowlist test chưa thu hẹp (chủ: `W24-2`, `D-277`).
- 🟡 **5 `docDeviation` chưa vào SRS** (chủ: `ba` ở `W24-2`) — SRS hiện vẫn nói "kế thừa `IntegrationEventBase`" và enum "chuỗi" mà không nêu as-built.
- 🟡 **`OrderCancelledEto` chưa tồn tại** (SAL-004) ⇒ đường **hoàn lượt khi huỷ đơn** (`D-148`) **chưa nối được** ở `W24-2`; ghi "chưa kiểm được" thay vì giả lập ETO.
- 🟡 **`Promotions.Enabled=false`** là cờ Setting do ARCH **đề xuất**, **chưa** có trong SRS ⇒ `W24-2` phải thêm và test;
  nếu không, đường lùi chỉ còn revert code.
- 🟡 **Q8 điều kiện địa chỉ** vẫn **chuyển người** (GHTK xác minh 2 cấp, R3-1) — không chặn WP.
- 🟡 **Cảnh báo hook**: `docrev` bị báo "thiếu khối JSON cuối" **2 lần** — báo cáo thực **có** khối JSON (false positive đã biết, `scopes.json` `chanDungKhiThieuJson=false`).
- 🟡 **`arch` chạm trần 30 lượt công cụ** ở lượt đầu (chưa ghi tệp) ⇒ phải `SendMessage` tiếp; lượt sau hoàn tất. Ghi để phiên sau chia nhỏ brief.
- **Chờ người (vùng cấm §6 hồ sơ)**: (a) duyệt lô `D-277…D-282`; (b) **AQ10/Q8** — GHTK xác minh địa chỉ 2 cấp; (c) kế toán duyệt lô M4
  (`D-158`/`D-160`/`D-161`/`D-162`/`D-163` + flash `D-156`) **trước release**; (d) merge `main` (biên bản người ký).

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG W24-1 (WP-24 F0–F2b) → mở `W24-2` (WP-24 F3 BE).**

- Hàng kế = **`W24-2`** (loại `code`, bước `F3 BE`): dựng module `Promotions` **4 project** + 7 `DbSet` + **một** migration
  `Add_Promotion_Module` (expand-only, `pg_dump` trước khi áp) + engine `IPromotionEngine` + guard 3 tầng + job `promotion-expiry` + test đơn vị.
  Điều kiện vào: `probe-cong-tai-lieu.ps1 -CheDo Code -WP WP-24` **exit 0** (đã đo) **và** `D-277` hiệu lực.
- Đọc trước: `docs/features/promotions/PRO-002-arch.md` (601 dòng, nhất là §2, §5, §9, §12.1) + `docs/reviews/PRO-002-arch-docreview.md` §"Vòng 3".
- **Nhớ**: 4 việc bắt buộc ở §2 trên (P3 allowlist · 5 `docDeviation` · guard D6 · 2 P3 của SRS).

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-24
và handover gần nhất `docs/nhanh/handover-n93.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng W24-2 (WP-24 F3 BE)** (phiên N94). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

## 7. Trạng thái nhánh

- Nhánh `feat/gd3-wp24-dieu-kien-km`; base `origin/tich-hop/gd3` @ `84157a1`.
- HEAD trước phiên `84157a1`; commit của phiên này ở §"Câu trả lời cuối" (đẩy lên `origin/<nhánh>` ngay sau khi tạo).
- **Không** đẩy lên `tich-hop/gd3` (chỉ ở hàng `dong-wp` = `W24-5` khi G6 xanh). **Không** mở PR (`D-216` ③). **Không** đụng `main`.

---

**Hàng rào agent** (phiên `N93`): `arch` ×1 agent (3 lượt sửa tài liệu; lượt đầu chạm trần 30 tool-use phải tiếp) · `docrev` ×1 agent (3 vòng:
DOC-CHANGES → DOC-CHANGES → DOC-OK) · `decider` ×1. **0** lần chặn dừng; **2** cảnh báo "thiếu khối JSON" của `docrev` là false positive đã biết.
`git status` cuối phiên: `docs/**` + `agents/state/**` + 2 tệp ARCH/DOCREVIEW mới.
