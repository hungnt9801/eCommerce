# BÀN GIAO — Phần còn lại của GĐ1 (mở session mới, sau `S12…S18`)
> 🗄️ **LỊCH SỬ — KHÔNG dùng làm điểm vào** (gắn nhãn 14/11/2026 theo `D-69`). Tệp ghi lại một thời kỳ/việc **đã qua**; **số đo, trạng thái và 'việc tiếp theo' trong tệp đã cũ** — kể cả câu *'đọc tệp này trước'* bên dưới. Điểm vào hiện tại: `CLAUDE.md` → `docs/nhanh/README.md` → handover mới nhất `docs/nhanh/handover-n*.md` → `docs/nhanh/07-ke-hoach-trien-khai.md`. Xếp loại tài liệu: `docs/agents/ke-hoach-he-thong-agent.md` §14.1.


> **Lập ngày:** 29/10/2026 · **Cây:** `main` @ `7923376` · **Người lập:** phiên tự động 28–29/10 (10 commit chính + 9 commit qua subagent)
>
> 🔑 **Đọc mục 1 và mục 3 trước.** Tài liệu này **không** nói GĐ1 đã xong: nó chốt **đang ở đâu**, **còn đúng một việc bắt buộc** (`S19`), và **làm theo thứ tự nào**.
> 📄 Bàn giao trước (28/10): [`ban-giao-gd1-con-lai.md`](ban-giao-gd1-con-lai.md) — vẫn dùng cho lịch sử quyết định; **số đo trong đó đã cũ**, dùng bảng §1 dưới đây.

---

## 1. Ảnh chụp hiện tại (đo lại **31/10/2026**, không phải lời khai)

| Hạng mục | Giá trị | Cách đo |
|---|---|---|
| Commit | `7923376` | `git log --oneline -1` |
| Cổng đầy đủ | **exit 0 · 394s · 16/16 bước** (đo 31/10, sau khi thêm bước ⑯) | `pwsh -File tools\kiem-tat-ca.ps1 -DayDu` |
| Unit | **1634** | bước ③ |
| Integration | **90/90** | bước ④ |
| E2E (HTML thật) | **167/167** | bước ⑧ |
| Bất biến DB | **25/25** · **NND 64** · **72 quyền** · **99** chỗ quét | bước ⑥ |
| Migration | **50** | `src/eCommerce.EntityFrameworkCore/Migrations` |
| Oversell | **11/11** | `tools/probe-oversell.ps1` (bước ⑬) |
| Outbox / 2 instance | **9/9 · 9/9** | `tools/probe-outbox*.ps1` |
| **Giữ chỗ 2 instance** | **17/17** | `tools/probe-giu-cho-2instance.ps1` (**bước ⑯** — mới 31/10) |
| Thanh toán | **30/30** | `tools/probe-thanh-toan.ps1` |
| DLQ replay | **21/21** | `tools/probe-dlq-replay.ps1` |
| Đơn sàn | **49/49** | `tools/probe-don-san.ps1` |
| Cây làm việc | **0 tệp ` M `** · **mọi tệp `??` đều trong `tools/`** (38 → ~45 trong 2 ngày) | `git status --porcelain` — ⚠️ xem §6.2 |

> ⚠️ **Các tệp untracked trong `tools/` KHÔNG phải của dự án**: chúng do **một tiến trình khác** (node/script cào catalog) ghi vào `tools/` trong lúc phiên chạy — tăng 7 → 38 → ~45 (đo 31/10). Chủ dự án đã nói **sẽ tự xoá**. 📌 **Không** stage/commit/xoá chúng; **không** dùng `git add -A`.

### Đã xong — đừng làm lại

| Hạng mục | Bằng chứng (nguồn sự thật) | Commit |
|---|---|---|
| **S12** `CostPrice` (`PrcCostPrices`) | `PRC-001` §20 · migration 47 · NND 58 · quyền `Pricing.CostPrice.View` · `/GiaVon` (đọc) | `acb6516` |
| **S15** `StalePendingTransaction` + `TriggerRunAsync` | `REC-001` §20 · `Q1 = 24 h` · 10/10 loại lệch · chạy tay cùng khoá với job | `95c04f8` |
| **S13** ngân sách tồn theo kênh + sổ cái | `INV-003` §21 · migration 48 · NND 60 · quyền `Allotment.Manage/Release` · giữ chỗ **trừ/trả** ngân sách | `cd6de05` |
| **S14 A–H** RMA (trả hàng/hoàn tiền) | `SAL-006` §28–§35 · migration 49 · NND 64 · API 9 method · chứng từ `CustomerReturn` · **cạnh tắt `Open→Returned` đã xoá** · `PartiallyReturned` | `9aa1293` `c9e2155` `73bd907` `2065c21` `925e760` `d617927` `ab0a6f4` `5e34cea` |
| **S16** metric (2/3) | `XC-005` §20 · `lock_ttl_exceeded_total` (nghe `HandleLostToken`) · `logging_failures_total` (bọc sink) | `030f414` |
| **S16b** replay DLQ thật | `XC-002` §18 · `XC-005` §21 · migration **50** · quyền `Operations.DeadLetters.Replay` · metric `messaging_dlq_replayed_total` · `probe-dlq-replay` 21/21 | `69a706e` |
| **S17** hợp đồng host | `ke-hoach-tach-module` §3.1 · `WarehouseAppService` → Inventory; 3 dịch vụ còn lại ghim `[ExposeServices]` + 18 test | `7c4d20e` |
| **S18** UI | `/NganSach` (ATP, cấp/sửa, đóng băng/mở/nhả, chọn kho thật) · nút “Chạy đối soát ngay” · `/TraHang` (duyệt/từ chối/nhận/đóng/hoàn tiền/huỷ) | `e3e7b77` `2005cb4` |
| **`Q5`** duyệt tài liệu | [`bien-ban-duyet-gd1-2026-10-29.md`](bien-ban-duyet-gd1-2026-10-29.md) — **26** tài liệu (7 `XC-*` + 19 GĐ1), chủ dự án duyệt 29/10/2026 | `7923376` |

---

## 2. Việc còn lại — **đúng một việc bắt buộc**, phần sau là tuỳ chọn

### 2.1 🔴 `S19` — VNPay sandbox THẬT (**cổng bắt buộc của GĐ1**)

> 📌 **TRẠNG THÁI 30/10/2026 (phiên `S19`):** **harness đã có** — `tools/probe-s19-vnpay-that.ps1` (chạy **tay**,
> **không** thuộc cổng 16 bước vì cần credential + người thanh toán + Internet). 🔒 **Nhưng credential KHÔNG có**
> trong môi trường đo: `Process`/`User`/`Machine` đều rỗng, không profile, không user-secrets, không `.env` ngoài
> repo ⇒ probe **dừng ở bước ⓪** (`exit 2`, **không** tạo giao dịch, **không** ghi bằng chứng) ⇒ **`S19` CHƯA ĐẠT**.
> 📄 Chi tiết + **bốn chướng ngại tĩnh** phải xử lý khi có credential: `PAY-001` **§16.12**.
> ▶️ Chạy khi có credential: `pwsh -File tools\probe-s19-vnpay-that.ps1 -SoTien 10000`.
>
> 🔁 **CẬP NHẬT 31/10/2026:** cổng 2 (**2 instance cho giữ chỗ**) **ĐÃ XANH** (bước ⑯, 17/17) ⇒ trong 5 cổng, **chỉ
> còn cổng 1 = `S19`**. Credential vẫn **chưa có** (đo lại 31/10: 0 ở cả ba scope) nên `S19` **vẫn CHƯA ĐẠT**.

**Điều kiện tiên quyết (chủ dự án cấp):** `PaymentHub:VnPay:TmnCode` + `PaymentHub:VnPay:HashSecret` của **merchant sandbox**.

🔒 **CẤM** ghi credential vào `appsettings.json`/tệp trong repo. Dùng **biến môi trường** cho tiến trình (mẫu `__` = `:`), đúng khuôn đã dùng ở `probe-thanh-toan.ps1` (`PaymentHub__VnPay__TmnCode`, `PaymentHub__VnPay__HashSecret`).

**Tiêu chí ĐẠT (đo được, không phải “đã chạy thử”):**
1. Một giao dịch **sandbox thật** đi qua cổng: tạo giao dịch → thanh toán trên cổng sandbox → nhận **IPN thật** (hoặc `querydr` trả `00`) → **DB** `PayTransactions.Status = Succeeded` **và** `OrdOrders.PaymentStatus = Paid`.
2. Bằng chứng: `PaymentTransaction.ProviderTransactionId` (số giao dịch do **cổng** cấp), thời điểm, số tiền; **không** có trong repo bất kỳ `TmnCode`/`HashSecret` nào (`grep` = 0).
3. Ghi vào `PAY-001` mục trạng thái mới + cập nhật bàn giao/gate; nếu **không** có credential thì **DỪNG** và ghi rõ — **cấm** bịa giao dịch hoặc dùng chữ ký giả rồi gọi là “thật”.
4. Cổng đầy đủ **exit 0 · 16/16** sau khi thêm ca/bằng chứng.

**Bẫy đã biết khi làm S19:**
- `ShouldChangePasswordOnNextLogin = true` chặn password grant khi lấy token ⇒ xem cách các probe đang xử lý (tắt cờ **trong lúc chạy**, bật lại trong `finally`, có khẳng định đã bật lại).
- VNPay **ký theo tham số không rỗng** (bài học `F-PAY-1`) — đừng “ký tất cả”.
- So khớp tiền bằng **minor units**, không `decimal ==`.
- Không để lại rác: `PayTransactions`/`PayRefunds` mang tiền tố `K9S5…` phải về 0 (kiem-db nhóm ①).

### 2.2 🟡 Tuỳ chọn — **không** chặn GĐ1 (làm khi có thời gian)

| Việc | Ghi chú |
|---|---|
| DLQ: phát lại **theo lô** (`ReplayBatchAsync`), **UI** DLQ, job **dọn DLQ** theo hạn | replay **từng dòng** đã có + 21/21 probe. Đặc tả ghi trong `XC-002` §18 |
| S18 phần còn lại: form quản trị **Pricing/Kho** | API đã có; hiện chỉ có màn hình đọc |
| Flake `probe-oversell` **T06** | Đôi khi trả `AbpDbConcurrencyException` với `code: null` thay vì `Inventory:0001`. 📌 Là **nợ quan sát được**, cần một lát cắt riêng: map exception ở filter + test. **Không** sửa bằng cách nới test |
| 38 tệp `tools/` lạ | Chủ dự án tự xoá; nhớ **dừng tiến trình đang ghi** trước |

---

## 3. Quyết định đã chốt (đừng mở lại nếu không có lý do mới)

| Mã | Chốt | Nơi ghi |
|---|---|---|
| `Q1` | `StalePendingHours = 24` (cấu hình, `0` = TẮT) | `REC-001` §20.2 |
| `Q3` | Ngân sách tồn **chỉ theo kênh** (không theo shop) + **tuỳ chọn theo dòng** (chỉ áp khi đã có dòng ngân sách) | `INV-003` §21.2 |
| `Q4` | Hoàn tiền **chỉ ghi nhận**, **không** tự gọi cổng | `SAL-006` §28.2 |
| `Q5` | **Duyệt thật** 26 tài liệu GĐ1, ngày 29/10/2026 | [`bien-ban-duyet-gd1-2026-10-29.md`](bien-ban-duyet-gd1-2026-10-29.md) |
| `Q6` | **Chưa có** credential VNPay sandbox ⇒ `S19` **chưa thể** làm — 🔄 **đo lại 30/10/2026: vẫn 0** (Process/User/Machine) ⇒ `S19` **CHƯA ĐẠT**; harness đã sẵn (`PAY-001` §16.12) | tài liệu này §2.1 |

---

## 4. Cách bắt đầu session mới (5 phút)

```powershell
cd D:\Projects\Tools\eCommerce
git log --oneline -1                 # phải là 7923376 hoặc mới hơn
git status --porcelain               # chỉ được có ?? tools/… (38 tệp lạ); KHÔNG có ' M '
docker ps                            # phải thấy ecommerce-postgres + ecommerce-redis
pwsh -File tools\kiem-tat-ca.ps1 -DayDu   # phải exit 0 · 16/16
```

Đọc theo thứ tự (đừng đọc cả 55 tài liệu):

1. **tài liệu này** (§1 → §2 → §6);
2. [`docs/SESSION-KICKOFF.md`](SESSION-KICKOFF.md) §3 — baseline + quy ước;
3. đặc tả của việc sẽ làm: `PAY-001` (S19) hoặc `XC-002` §18 (DLQ);
4. [`docs/handover/BAN-GIAO-GD1-2026-10-17.md`](handover/BAN-GIAO-GD1-2026-10-17.md) §4–§5 — ràng buộc schema/ABP + **bảy cái bẫy** PowerShell/C# + kỷ luật ĐO;
5. [`tools/README-probe.md`](../tools/README-probe.md) — nếu định viết probe mới (ưu tiên dùng lại).

---

## 5. Kỷ luật bắt buộc (đã trả giá nhiều lần)

1. **Đo trước, sửa sau**: baseline `-DayDu` xanh **trước** khi đổi mã. Đỏ do môi trường (Docker tắt, cổng bị giữ) thì **sửa môi trường**, không sửa mã.
2. **Một việc — một thay đổi — một commit ASCII**. Không commit cổng đỏ; không “sửa test cho xanh”.
3. **Migration + `schema.sql` + test + tài liệu trong CÙNG thay đổi**:
   `dotnet ef migrations add <Ten> --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.DbMigrator`
   → `dotnet ef migrations script … --output test/eCommerce.IntegrationTests/Assets/schema.sql`
   → **sửa tay `NULLS NOT DISTINCT`** cho unique index có `TenantId` → cập nhật `PostgreSqlSchemaTests` (số NND **đọc từ test**, `kiem-db` đối chiếu) → chạy `eCommerce.DbMigrator`.
4. **Permission mới phải khai HAI NƠI** (hằng số + `eCommercePermissionDefinitionProvider`) + seed + `kiem-db` danh sách `quyenPhaiCon`. Thiếu một nơi ⇒ `403` cho **mọi người**.
5. **Không khai metric/ETO mà không có điểm phát/người nghe thật** (`MetricEmitterTests` đỏ nếu khai nợ mà có lời gọi, và ngược lại).
6. **Cổng tài liệu trước commit**: `npm --prefix tools run check:mermaid` + `pwsh -File tools\kiem-link-tai-lieu.ps1`.
7. **`git add <đường dẫn cụ thể>`** — ⚠️ **CẤM `git add -A`** khi còn 38 tệp lạ trong `tools/`.

---

## 6. Bẫy môi trường của phiên vừa rồi (đọc trước khi hoảng)

1. **Docker Desktop có thể tắt** ⇒ integration ném `DockerUnavailableException`, cổng đỏ **không phải do mã**. Bật Docker + `docker compose up -d`.
2. **Một tiến trình KHÁC đang ghi vào `tools/`** (38 tệp): `_*.png`, `*.mjs`, `*.json`. Hệ quả: *“cây sạch”* **không thể** khẳng định; `git add -A` nuốt chúng; máy bị tải ⇒ **thời gian cổng dao động** 345s → 483s (số ca **không đổi**). 📌 Đừng dùng thời gian cổng để kết luận hồi quy khi máy đang tải.
3. **Flake `probe-oversell` T06** (đôi khi `code: null`) — chạy lại probe riêng trước khi kết luận; đừng sửa mã vì flake chưa điều tra.
4. **Cổng bị giữ** (5000/5100) ⇒ `kiem-tat-ca` bước ① báo; dừng tiến trình cũ trước.
5. Nếu dùng **subagent**: chạy **tuần tự** (chung DB/cổng), giao kèm **tiêu chí ĐẠT + cấm `git add -A`**, và **kiểm chứng độc lập** (commit, diff, log cổng) — phiên này đã có **3 lần** kiểm chứng phát hiện điểm sai trong bàn giao/giả định (S17 “5 dịch vụ” thực tế 4/5 đã tách; S16 `timeout` là thời gian chờ, không phải TTL; S14E tự báo 2 lỗi).

---

## 7. Checklist “GĐ1 hoàn tất” (5 cổng)

| # | Cổng | Hiện tại | Cần để xanh |
|---|---|---|---|
| 0 | Tài liệu GĐ1 + `XC-001…007` Approved | ✅ **26/26** (biên bản 29/10) | — |
| 1 | Đặt đơn + thanh toán **1 cổng VN thật** | ⛔ **Chưa có credential** · 🟡 harness `probe-s19-vnpay-that.ps1` đã sẵn (30/10) | **`Q6` credential** ⇒ chạy 1 lệnh (`PAY-001` §16.12) |
| 2 | Không bán vượt tồn (2 luồng + **2 instance kho**) | ✅ **2 luồng** · ngân sách kênh · **2 instance kho 17/17** (bước ⑯ — mới 31/10) | — |
| 3 | Không mất tiền — job đối soát phát hiện lệch | 🟢 10/10 loại lệch + chạy tay | tiền thật (`S19`) |
| 4 | Mọi API cần xác thực + login | ✅ | — |
| 5 | Không mất event — outbox relay | ✅ replay + 2 instance | — |

> ✅ **Cổng 2 ĐÃ XANH 31/10/2026** — phần cuối của nó (**2 instance cho GIỮ CHỖ**) đã đo được:
> `tools/probe-giu-cho-2instance.ps1` chạy **hai** Host (5000 + 5001) cùng DB/Redis, bắn 1 request vào **mỗi**
> Host **cùng lúc** trên SKU `OnHand = 1` ⇒ **đúng MỘT** thắng, request kia `403` + `eCommerce:Inventory:0001`
> (không 5xx), DB **một** dòng giữ chỗ, `Reserved <= OnHand`, `Available = 0`.
> 🔑 **Có chiều ngược:** chỉ đổi `OnHand = 2` ⇒ **cả hai** phải 200 — nhờ vậy phép đo **không thể** xanh vì lý do
> sai (mọi request đều bị chặn). **17/17 ca**, là **bước ⑯** của cổng.
> 📌 Khác hẳn bước ⑬ (hai request trên **một** Host — ca dễ: một khoá in-memory cũng đủ xanh).

---

## 8. Ước lượng

| Việc | Ước lượng | Phụ thuộc |
|---|---|---|
| **S19** (một giao dịch sandbox thật + bằng chứng + tài liệu) | **0,5–1 ngày** — 🔄 **30/10: harness + tài liệu ĐÃ XONG**; còn lại **1 lệnh** khi có credential (+ xử lý `S19-1…4` nếu lần chạy chạm) | `Q6` credential |
| Probe 2 instance cho giữ chỗ | ✅ **XONG 31/10** — bước ⑯, **17/17** (thực tế ~1 giờ, không phải 1–2 ngày) | — |
| DLQ lô/UI/job dọn | 2–3 ngày | — |
| Form quản trị Pricing/Kho | 1–2 ngày | — |

**Tổng còn lại để “GĐ1 hoàn tất” theo 5 cổng: CHỈ còn `S19`** (1 lệnh khi có credential) — không phải một phiên dài, nhưng **không thể** đóng bằng cách bịa giao dịch.

---

## 9. Mẫu câu để dán vào session mới

> *“Đọc `docs/ban-giao-s19-va-con-lai-2026-10-29.md` rồi làm `S19` theo §2.1. Credential VNPay sandbox đã có trong biến môi trường (không nằm trong repo). Giữ đúng kỷ luật §5: baseline xanh trước, một commit ASCII, cổng 16/16, không `git add -A`.”*

Nếu **chưa** có credential, câu thay thế:

> *“…làm **probe 2 instance cho giữ chỗ** (checklist §7 cổng 2) — **không** đụng `S19`.”*

