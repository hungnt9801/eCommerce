# Handover `N10` — WP-09: **Test & cổng** (Playwright 8 luồng + Vitest + `openapi-drift` + cổng có bước FE)

> Ngày 04/11/2026 · nhánh `feature/wp-03-catalog` (tiếp từ checkpoint **`39b8766`**)
> Kế hoạch: [07 §WP-09](07-ke-hoach-trien-khai.md) · Chia phiên: [08 §3](08-session-protocol.md) ·
> Quyết định: [D-49](decisions.md), [D-50](decisions.md)
> **Mục tiêu phiên:** làm **đúng WP-09** (Playwright 8 luồng · `dotnet test` + `pnpm test` + `pnpm e2e` trong
> CI · job `openapi-drift` · mở rộng `tools/kiem-tat-ca.ps1` thêm bước FE), **không mở rộng phạm vi**.
> ⚠️ **`WP-10` (Ops: Docker service `admin-web`, `.env.example`, health BFF) KHÔNG làm ở phiên này** — mặc dù
> `08 §3` gộp `WP-09 + WP-10` vào `N10`; câu mở phiên chỉ định **đúng WP-09**. Việc đó sang **`N11`**.

---

## 1. Đã xong — việc + **số đo** (kèm lệnh kiểm)

### 1.1 Baseline đo TRƯỚC khi sửa (6 lệnh §4 của [08](08-session-protocol.md))

| # | Lệnh | Kết quả đo |
|---|---|---|
| 1 | `git status --short` | **KHÔNG sạch** — **144 mục** (**36 ` M`** của `N7`/`N8`/`N9` chưa commit + **108 `??`** rác khảo sát/probe) |
| 2 | `git log --oneline -1` | `39b8766` · nhánh `feature/wp-03-catalog` |
| 3 | `docker compose ps` | `ecommerce-postgres` + `ecommerce-redis` **Up (healthy)** |
| 4 | `dotnet build eCommerce.slnx` | **0 error / 0 warning** (incremental) |
| 5 | `dotnet test test\eCommerce.Tests --no-build` | **1961/1961 pass** |
| 6 | `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** — **111** tệp `.md`, **545** link, **360** đường dẫn, **0 chết** |

📌 Baseline **khớp** `handover-n9` (1961 test · migration 59 · NND 93 · 23 route). Không có sai lệch phải dừng.

### 1.2 Đã thi công (đúng 3 việc của `07 §WP-09`)

| # | Việc (`07 §WP-09`) | Nội dung | Đo được |
|---|---|---|---|
| 1 | **Playwright 8 luồng** | `apps/admin-web/e2e/`: `tests/01…08` · `support/{env,state,api,psql,don-hang,ui}.ts` · `global-setup.ts` (chờ API + **mở khoá cờ đổi mật khẩu** + sinh `runId`) · `global-teardown.ts` (**bật lại** cờ) · `playwright.config.ts` (`workers: 1`, `webServer` = Host **+** Next) · `tools/e2e.ps1` (giết server cũ ⇒ **không** xanh trên build cũ) | **`8 passed`** — xem §1.4 (thứ tự luồng + số liệu từng luồng) |
| 2 | **`pnpm test` (= Vitest)** | `vitest.config.ts` (`environment: "node"`) + `test/{format,report,pos,order}.test.ts` | **25/25** ca: `vi-VN` (`1,5` · `1.000` · `1.234,5`), BOM + `;` + bọc ô CSV, `lastDays` qua ranh giới năm, `nguon=0` **không bị bỏ**, công thức tiền POS (+VAT/điểm/tiền thối/nợ), `thanhPayload` **không** gửi tổng tiền, `dueDate` → UTC cuối ngày, `toPage` nhận **cả** `total`/`totalCount`, `sortTransitions` theo số enum |
| 3 | **CI + cổng** | `.github/workflows/ci.yml`: 2 **job mới** — `openapi-drift` (bật Host → `spec:snapshot` + `gen:api` → so **trước/sau**) và `e2e-playwright` (chạy trên **DB TRỐNG**: chỉ migrate + seed); `cong-15-buoc` **giữ nguyên tên** (required check) + thêm bước 6b/6c (pnpm install, `pnpm build`, chromium). Cổng `tools/kiem-tat-ca.ps1` thêm **2 bước luôn chạy** (⑥ typecheck+Vitest, ⑦ `gen:api` không đổi `schema.d.ts`) và **bước ⑰ Playwright** chỉ khi `-DayDu`. Thêm `tools/kiem-openapi-drift.ps1` | `tools/kiem-openapi-drift.ps1` → **PASS**, **51/51** path `/api/v1`, `schema.d.ts` **không đổi** · cổng: bước ⑥ **`Tests 25 passed`** + ⑦ **`schema.d.ts khop`** ✅ · `ci.yml` **parse được** (3 job: `cong-15-buoc` 19 bước · `openapi-drift` 8 · `e2e-playwright` 11) |

### 1.3 Đo LẠI cuối phiên

| Lệnh | Kết quả |
|---|---|
| `dotnet build eCommerce.slnx -t:Rebuild` | **0 error / 9 warning** — **không** phát sinh warning mới (9 `CS8604` có sẵn ở Address/Sales) |
| `dotnet test test\eCommerce.Tests` | **1961/1961 pass** (không đổi — WP-09 **không** thêm test BE) |
| `dotnet test test\eCommerce.IntegrationTests` | **96/96 pass** |
| `pnpm --filter @ecommerce/admin-web typecheck` | **✓** (0 lỗi — **gồm cả** `e2e/**` và `test/**`) |
| `pnpm test` | **25/25 pass** (4 tệp) |
| `pnpm build` | **✓ — 23 route** (không thêm route nào) |
| `pwsh -File .\tools\e2e.ps1` | **`8 passed`** · **`exit 0`** (chạy **2 lần liên tiếp** đều xanh; lần **nguội** 46s — Playwright tự bật Host + Next) |
| `pwsh -File .\tools\kiem-openapi-drift.ps1` | **PASS** — **0** drift, **51/51** path |
| `pwsh -File .\tools\kiem-tat-ca.ps1` | **DỪNG ở bước ⑧ (DB)** — bước ①②③④⑤**⑥⑦** xanh; **1 HỒNG có sẵn** `CusCustomers` (xem §5 #1); nhóm **②** của cổng DB đã xanh nhờ teardown. ⚠️ Bước **⑰ (Playwright, chỉ `-DayDu`)** *không tới được* vì cổng dừng ở ⑧ ⇒ đã kiểm nó bằng cách gọi **đúng lệnh** `tools/e2e.ps1` (8/8) |
| `pwsh -File .\tools\kiem-link-tai-lieu.ps1` | **PASS** (số đo ở §1.5) |
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| NND (DB dev) | **93** (không đổi — WP-09 không đổi schema) |

### 1.4 Bằng chứng E2E — `tools/e2e.ps1` (`exit 0`, **8 passed**)

Bộ test chạy **tuần tự** như **một ngày bán hàng**: mỗi luồng ghi chứng từ của mình vào
`apps/admin-web/e2e/.run/state.json` (gitignore) và luồng sau **dùng đúng chứng từ đó**.

| # | Luồng | Khẳng định (đo được trên lượt chạy cuối) |
|---|---|---|
| ① | Đăng nhập | sai mật khẩu ⇒ `?error=invalid` + câu *"Sai email hoặc mật khẩu"*; đúng ⇒ `/{business}` + **4 KPI**; **token không nằm ở `localStorage`** và cookie session **không đọc được bằng JS** (`D-12`) |
| ② | Tạo SP | `POST /api/proxy/v1/products` **201** (qua **BFF**, token server-side) + 1 **SKU**; DS sản phẩm (`?q=`) hiện đúng **1 dòng** có tên/mã/SKU/giá `15.000`/trạng thái `Mới` |
| ③ | Nhập kho | UI: chọn kho → ProductPicker → **10 × 10.000** → `Lưu phiếu (Nháp)` → chi tiết `PN-20261104-2149` → **Ghi sổ** ⇒ *"Đã ghi sổ … bút toán sổ cái"*; **API `onHand = 10`** + trang tồn hiện **10** |
| ④ | Bán lẻ | Mở ca (**500.000**) → gõ SKU → SL **2** → bấm **Tiền mặt** → `Lưu (F9)` ⇒ **`POS-20261104-0027` · 30.000**; API xác nhận `totalPayment 30.000` + dòng SKU |
| ⑤ | In | `/print/bill/{id}` có **HOÁ ĐƠN BÁN LẺ** + số HĐ + `PHẢI TRẢ` + `30.000 đ`; `window.print` được gọi **1 lần tự động** (sau ~400 ms) + **1 lần** khi bấm nút (đo bằng stub) |
| ⑥ | Đổi trạng thái đơn | Đơn seed `E2E-DH-…` ở **Mới** → bấm **“→ Chờ xác nhận”** (nút do `allowedTransitions` của server trả) ⇒ **`FulfillmentStatus = 9`**, nhãn `Chờ/Đang xác nhận`, **1** dòng lịch sử (`toStatus = 9`) |
| ⑦ | Kết ca | Bàn giao **530.000** ⇒ tự tính `ExpectedCash 530.000` · **`chênh lệch 0`**; API `sessions/current` ⇒ **204** (ca đã đóng). SQL độc lập: `Status=1 · exp=530000.00 · var=0.00` |
| ⑧ | Xuất CSV | `POST /reports/aggregate` (hôm nay, qua BFF) ⇒ **CSV doanh thu** có **BOM** `\uFEFF`, header `Kỳ;Bán lẻ;Đơn hàng;Tổng doanh thu;VAT;Số chứng từ`, dòng `2026-11-04` **số thô**; **CSV tồn kho** có **BOM** + header `Kho;Số SKU;Tồn;Khả dụng;Tạm giữ` + mọi dòng **5 cột** |

> 📌 **Đối chiếu DB độc lập** (psql, sau lượt chạy): `OrdOrders.FulfillmentStatus = 9` · `OrdStatusHistory` **1** dòng ·
> `PosCashSessions` `Status=1 exp=530000.00 var=0.00` · `PosRetailBills` `Status=0 total=30000.00` ·
> `InvItems.OnHand = 8.000` (nhập 10 − bán 2). 📌 Ảnh chụp E2E **không** dùng để tính điểm — **số là số đo**.

### 1.5 Cổng tài liệu

`pwsh -File .\tools\kiem-link-tai-lieu.ps1` → **PASS** — **112** tệp `.md`, **561** link markdown,
**365** đường dẫn backtick, **0 chết**. Mọi tài liệu mới/sửa (`handover-n10`, `decisions` D-49/D-50,
`07 §WP-09` + `§13`, `08 §3`, `README`) đã được cổng này quét.

---

## 2. Chưa xong / cắt giữa (chính xác đang ở đâu)

**Không có tệp nào đang sửa dở.** Các mục **thuộc phạm vi test/cổng** nhưng **cố ý cắt** (mỗi mục có **nhận chủ**):

1. **`WP-10` (Ops) — CHƯA LÀM, là việc của `N11`**: `docker-compose.yml` thêm service `admin-web` (production
   build + `depends_on`), `.env.example` (`API_BASE_URL`, `AUTH_SECRET`, `AUTH_ISSUER`, `NEXT_PUBLIC_*`),
   health `/api/health` ở BFF → `/health-status` của Host. 📌 `08 §3` gộp `WP-09 + WP-10` vào `N10` nhưng câu
   mở phiên ghi **đúng WP-09** ⇒ đã **không** mở rộng phạm vi.
2. **Luồng "tạo SP" đi qua API của BFF, KHÔNG qua form** (`D-49` ④): **chưa có form sản phẩm FE** — việc
   **FE 3 ngày của `WP-03`**, đã ghi nợ từ `handover-n3`. Nhận chủ: **`WP-03` (FE form)**.
3. **E2E phải tạo thêm "biến thể" cũ để có SKU** — vì `ProductListItemDto.Sku` vẫn đọc từ `ProductVariant`
   (`ProductApiMapper.ToListItem`: `Sku = first?.Sku`), trong khi mô hình Nhanh (`D-07`) giữ giá/tồn ở **ĐVT**.
   📌 Hệ quả **THẬT**: sản phẩm chỉ-có-ĐVT **không bán được ở POS** (POS lọc bỏ dòng không có SKU). Nhận chủ:
   **`WP-03`** (quyết `Sku` = `Product.Code` hay cột `Sku` trên `ProductUnit`). Xem §5 #2.
4. **Seed 1 đơn hàng bằng SQL** (`e2e/support/don-hang.ts`): **chưa có API tạo đơn** dùng được — đường
   `POST /api/app/don-hang/tao-tu-checkout` đòi **sổ giải trình giá** (`SAL-002`) + **lượt giữ chỗ**
   (`INV-002`). Nhận chủ: **`WP-05` P1** (*"tạo đơn từ Admin"*).
5. **8 luồng chưa phủ** (nhận chủ **P1**, không phải bỏ sót): **trả hàng** (`pos/return`), **huỷ hoá đơn**
   (`VoidBillButton`), **offline/outbox** (F5 + rớt mạng — đã có `kiem-pos-browser.ps1` đo riêng), **phạm vi dữ
   liệu** (`Own` ⇒ 403 `Pos:0028`), **ma trận quyền** (`settings/staff`), **trả đơn nhiều bước** (`→ Thành công`).
6. **Chưa có test E2E cho `/login` khi Host CHẾT** (màn hình phải nói *"không gọi được API"* chứ không treo).
7. **Chưa chạy CI thật trên GitHub** (nhánh chưa push) ⇒ 3 giả định **mới kiểm trên Windows**:
   (a) `playwright install --with-deps` trên `ubuntu-latest`; (b) `webServer` bật `dotnet <dll>` trên Linux;
   (c) `corepack prepare pnpm@10.22.0`. Xem §5 #6.

---

## 3. Migration đã áp

| Mục | Giá trị |
|---|---|
| Migration | **KHÔNG có migration mới** — **59** (`20261104033300_Wp08_BaoCao_Reporting`) vẫn là mới nhất |
| DB đã áp | PostgreSQL dev `ecommerce` @ `localhost:5432` (đã ở migration 59 từ `N9`) |
| Vì sao không có | `WP-09` là **test + cổng**: không thêm bảng/cột/enum. ✅ Bằng chứng: `dotnet test test\eCommerce.IntegrationTests` **96/96** (gồm `PostgreSqlSchemaTests` đếm **93** index `NULLS NOT DISTINCT`) và `schema.sql` **không đổi** |
| Cách lùi | Không cần (không có thay đổi schema) |

📌 **Dữ liệu E2E để lại (cố ý)** — như quy ước của các probe: sản phẩm/kho/phiếu/hoá đơn/đơn mang tiền tố
`E2E-<runId>` và **1 biến thể SKU**; lượt chạy mới **không** đụng lượt cũ (mọi mã đều có `runId`).

---

## 4. Quyết định mới (đã thêm vào [decisions.md](decisions.md))

| ID | Nội dung ngắn |
|---|---|
| **D-49** | **E2E (`WP-09`)**: 8 luồng = **một ngày bán hàng TUẦN TỰ** (`workers: 1` + `e2e/.run/state.json`); `webServer` bật **cả** Host + Next từ **build hiện tại**, `tools/e2e.ps1` **giết** tiến trình cũ (chống *"xanh trên build hôm qua"*), chạy **DLL** chứ không `dotnet run`; **đúng 2 ngoại lệ SQL** — ① tắt cờ `ShouldChangePasswordOnNextLogin` ở setup và **BẬT LẠI ở teardown** (giữ bất biến cổng DB nhóm ②), ② seed **1 đơn** vì chưa có API tạo đơn (nhận chủ `WP-05` P1) |
| **D-50** | **Cổng FE (`WP-09`)**: `pnpm test` = **Vitest `node`** (25 ca hàm thuần — không jsdom/RTL ở P0); cổng `kiem-tat-ca.ps1` thêm **3 bước FE**; **drift đo "sinh lại rồi so TRƯỚC/SAU"** (không so `git diff` — working tree có thay đổi hợp lệ chưa commit); CI **tách 2 job** (`openapi-drift`, `e2e-playwright` trên **DB TRỐNG**) và **giữ nguyên** `cong-15-buoc` làm required check |

---

## 5. Nợ / rủi ro (điều sẽ nổ nếu phiên sau không xử lý)

1. 🚨 **Cổng DB đang ĐỎ SẴN ở `CusCustomers`** — **6 dòng** probe `WP06-KHACH-*` / `WP07-KHACH-*` do `N7`/`N8`
   để lại (`IdempotencyKey` ghi rõ nguồn). 📌 Phiên này **không** thêm dòng nào (E2E bán **khách vãng lai**,
   không tạo `Customer`), và đã **bật lại** cờ bảo mật của admin ⇒ cổng DB **23/25 → 24/25**. Kết quả
   `kiem-tat-ca.ps1` vì vậy **dừng ở bước ⑧** — **không phải** do thay đổi của `N10`.
   ⇒ **Việc của phiên sau (nếu muốn cổng xanh):** quyết định **dứt điểm** với 6 dòng đó (xoá kèm dữ liệu phụ
   thuộc, hoặc thêm ngoại lệ có ghi lý do vào `kiem-db.ps1`); ⚠️ **đừng** tắt cổng.
2. 🚨 **Sản phẩm Nhanh-model KHÔNG có SKU** ⇒ không bán được ở POS (`D-07` nói giá/tồn ở ĐVT, nhưng
   `Sku` của DTO vẫn đọc từ `ProductVariant`). Đây là **lỗ hổng mô hình**, không phải lỗi test. 📌 Hệ quả: mọi
   sản phẩm tạo bằng `POST /api/v1/products` (đường P0) đều **không xuất hiện ở POS/ProductPicker**. Cần chốt:
   `Sku` = `Product.Code` (như `ProductLegacyReader` đang làm cho ĐVT gốc) hay thêm cột `Sku` cho `ProductUnit`.
3. ⚠️ **`allowedTransitions` trả CẢ 3 trạng thái huỷ** (`CustomerCancelled`/`SystemCancelled`/`CarrierCancelled`)
   ⇒ màn chi tiết vẽ nút **“→ Khách huỷ”**, bấm vào gọi `POST /orders/{id}/transitions` và nhận **`0010`**
   (`D-40`: huỷ phải đi cửa `/cancel`). 📌 Phát hiện khi viết luồng ⑥ (test chỉ bấm nút *“→ Chờ xác nhận”* nên
   **không** che lỗi này). Sửa: lọc 3 giá trị đó ở **app service** (một chỗ) hoặc ở `OrderActions`.
4. ⚠️ **`pnpm e2e` cần Docker + PostgreSQL dev**: `global-setup` dùng `docker exec … psql` (seed đơn + cờ
   admin). Máy không có container ⇒ đỏ với thông điệp rõ (*"Kiểm tra docker compose up -d"*), **không** im lặng.
5. ⚠️ **Bộ E2E là TUẦN TỰ** (~33–46s): không được đổi `workers > 1` — 8 luồng dùng **chung** một ca thu ngân +
   một read-model báo cáo, chạy song song sẽ ra *flaky* (và người ta sẽ bỏ qua màu đỏ).
6. ⚠️ **CI mới chỉ được kiểm bằng `ci.yml` parse + chạy LOCAL**: 3 giả định chưa đo trên `ubuntu-latest`
   (§2 #7). 📌 Lần push đầu **phải xem log 2 job mới**; nếu `e2e-playwright` đỏ ở bước 5/6 thì đó là
   **MÔI TRƯỜNG** (chromium/pwsh), không phải mã nguồn — đừng sửa test cho xanh.
7. ⚠️ **`POST /reports/aggregate` là điều kiện của luồng ⑧**: báo cáo đọc **read-model**, chưa tổng hợp thì nút
   **Xuất CSV bị vô hiệu** (bảng rỗng) — đó là hành vi đúng (`D-47`), không phải lỗi.
8. 📌 **E2E để lại dữ liệu** `E2E-<runId>` (sản phẩm/kho/phiếu/hoá đơn/đơn/biến thể). Cố ý — cùng quy ước
   probe; đừng dùng số liệu trên màn hình báo cáo của dev để "kiểm chứng" mà quên chúng.

---

## 6. Việc đầu tiên của phiên sau

**Trạng thái phiên: XONG WP-09 → mở WP-10.**

1. Đọc `07 §WP-10` — **Ops (2 ngày)**: `docker-compose.yml` thêm service `admin-web` (build production,
   `depends_on` host), `.env.example` (`API_BASE_URL`, `AUTH_SECRET`, `AUTH_ISSUER`, `NEXT_PUBLIC_*`),
   health `/api/health` (BFF) → `/health-status` của Host.
2. ⚠️ **Chạy `pwsh -File .\tools\e2e.ps1` sau khi sửa BẤT KỲ** đường nào của đăng nhập/BFF/POS/kho/đơn — đó là
   lưới an toàn duy nhất bắt được *"UI đổi mà API không đổi"*.
3. ⚠️ **`pnpm gen:api` + `tools/kiem-openapi-drift.ps1` không được đỏ**: nếu `WP-10` thêm endpoint
   `/api/health` **không** thuộc `/api/v1` thì **không** ảnh hưởng snapshot — nhưng nếu thêm vào `/api/v1`
   thì **phải** sinh lại + commit `openapi.v1.json` + `schema.d.ts`.
4. 📌 Nếu `docker-compose` thêm service `admin-web`: phải chạy được **trên runner CI** (build context + `.dockerignore`)
   — và cập nhật `ci.yml` nếu thêm bước.

```powershell
cd D:\Projects\Tools\eCommerce
git status --short
docker compose up -d
dotnet build eCommerce.slnx
dotnet test test\eCommerce.Tests
dotnet test test\eCommerce.IntegrationTests
pnpm --filter @ecommerce/admin-web typecheck
pnpm test
pnpm build
pwsh -File .\tools\e2e.ps1                       # 8 luồng (tự bật Host + Next từ build hiện tại)
pwsh -File .\tools\kiem-openapi-drift.ps1        # 51/51 path, 0 drift
pwsh -File .\tools\kiem-link-tai-lieu.ps1
```

```text
Đọc `docs/nhanh/README.md`, `docs/nhanh/decisions.md`, `docs/nhanh/07-ke-hoach-trien-khai.md` §WP-10
và handover gần nhất `docs/nhanh/handover-n10.md`. Chạy baseline 6 lệnh ở `docs/nhanh/08-session-protocol.md`
§4, ghi số, rồi làm **đúng WP-10** (phiên N11). Không mở rộng phạm vi. Kết thúc bằng đo lại + handover.
```

---

## 7. Trạng thái nhánh

| Mục | Giá trị |
|---|---|
| Nhánh | `feature/wp-03-catalog` (tên **không còn khớp** phạm vi: đang chứa `N1…N10`) |
| Commit | **chưa commit** phiên này — mã `N7`+`N8`+`N9`+`N10` nằm trong working tree (**42 ` M`** + **115 `??`**); commit kế tiếp theo khuôn cũ (`git add` **đường dẫn cụ thể**), **KHÔNG** `git add -A` |
| Push / PR | Chưa |
| Tệp `N10` đã chạm | **FE (mới):** `apps/admin-web/e2e/{global-setup.ts,global-teardown.ts}` · `apps/admin-web/e2e/support/{env,state,api,psql,don-hang,ui}.ts` · `apps/admin-web/e2e/tests/{01-dang-nhap,02-tao-san-pham,03-nhap-kho,04-ban-le,05-in,06-doi-trang-thai-don,07-ket-ca,08-xuat-csv}.spec.ts` · `apps/admin-web/{playwright.config.ts,vitest.config.ts}` · `apps/admin-web/test/{format,report,pos,order}.test.ts`. **FE (sửa):** `apps/admin-web/package.json` (`test`, `e2e` + devDeps `@playwright/test`, `vitest`) · `pnpm-lock.yaml`. **Cổng/CI:** `tools/e2e.ps1` (mới) · `tools/kiem-openapi-drift.ps1` (mới) · `tools/kiem-tat-ca.ps1` (+3 bước) · `.github/workflows/ci.yml` (+2 job, +2 bước 6b/6c) · `package.json` gốc (`test`, `e2e`) · `.gitignore` (artifact E2E). **Khác:** docs (`decisions` D-49/D-50, `07 §WP-09`+`§13`, `08 §3`, `README`, `handover này`) |
