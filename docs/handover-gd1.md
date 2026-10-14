# BÀN GIAO — ĐỌC FILE NÀY TRƯỚC KHI LÀM GÌ

> **Mục đích:** session mới **KHÔNG nhớ** cuộc trò chuyện trước. File này là **bộ nhớ bền vững**:
> trạng thái hiện tại · **quy trình kiểm chứng đã trả giá** · việc tiếp theo · các quyết định đang mở.
> **Ngày bàn giao:** 14/10/2026 · **Repo:** `D:\Projects\Tools\eCommerce`

---

## 1. TRẠNG THÁI HIỆN TẠI (số liệu đã kiểm chứng)

| | |
|---|---|
| Commit | **82** · git **sạch** · nhánh `main` |
| Build | **0 lỗi / 0 cảnh báo** — đo bằng `dotnet build eCommerce.slnx -v q --no-incremental` |
| Test | **361 unit + 20 integration** (PostgreSQL 17 thật qua Testcontainers) |
| Tài liệu | **52/52** feature doc + **báo cáo chốt GĐ0** · Mermaid **197 khối / 0 lỗi** |
| API | **35 route** thật · ma trận `401`/`403`/`200` đúng |
| DB | **19** index `NULLS NOT DISTINCT` · 7 migration |
| Background worker | **5**, đã xác nhận **chạy thật** (không chỉ "đã khởi động") |
| Lỗi thật đã sửa | **24** — trong đó **16 lỗi IM LẶNG** |
| Điểm phát metric | **7/12** |

### Lệnh chuẩn
```powershell
Set-Location 'D:\Projects\Tools\eCommerce'

# Build (LUÔN dùng --no-incremental: build tăng dần ẨN cảnh báo)
dotnet build eCommerce.slnx -v q --no-incremental

# Test
dotnet test test\eCommerce.Tests\eCommerce.Tests.csproj --nologo
dotnet test test\eCommerce.IntegrationTests\eCommerce.IntegrationTests.csproj --nologo

# Kiểm tài liệu Mermaid (bắt buộc sau khi sửa doc)
npm --prefix tools run check:mermaid

# Khởi động Host (cổng 5000) — BẮT BUỘC sau mọi thay đổi DI
$bin='D:\Projects\Tools\eCommerce\src\eCommerce.HttpApi.Host\bin\Debug\net10.0'
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT','Development','Process')
$p = Start-Process dotnet -ArgumentList 'eCommerce.HttpApi.Host.dll' -WorkingDirectory $bin -PassThru -WindowStyle Hidden `
     -RedirectStandardOutput "$env:TEMP\host.log" -RedirectStandardError "$env:TEMP\host.err"
Start-Sleep -Seconds 38
Invoke-WebRequest 'http://localhost:5000/health-status'    # mong đợi: 200 Healthy
Stop-Process -Id $p.Id -Force
```

---

## 2. 🚨 QUY TRÌNH KIỂM CHỨNG — PHẦN QUAN TRỌNG NHẤT CỦA FILE NÀY

Toàn bộ **16 lỗi im lặng** trong dự án đều có chung một dạng: **build xanh, test xanh, app chạy — nhưng sai**.
Các quy tắc dưới đây được rút ra từ lỗi **thật đã xảy ra**, không phải lý thuyết:

| # | Quy tắc | Lỗi đã trả giá |
|---|---|---|
| 1 | **KIỂM CHỨNG Ở BƯỚC RIÊNG, RỒI MỚI VIẾT COMMIT MESSAGE.** Không gộp `kiểm chứng + commit` vào một lệnh — commit message sẽ được viết **trước khi biết kết quả** | Tôi đã **2 lần** ghi commit message khẳng định "đã kiểm chứng" trong khi build **hỏng** / Host **không khởi động được**. Cả hai phải đính chính bằng commit riêng |
| 2 | **Sau MỌI thay đổi liên quan DI ⇒ PHẢI khởi động Host.** Build + test xanh **KHÔNG** chứng minh resolve được | `shared/eCommerce.Kernel` thiếu `AbpModule` ⇒ `eCommerceMetrics` không đăng ký DI ⇒ **app không khởi động**; test vẫn xanh vì test tự `new` |
| 3 | **`--no-incremental` khi kiểm cảnh báo** | Build tăng dần **ẩn 3 cảnh báo thật** (CS0108, CS8604, xUnit2031) suốt nhiều lượt |
| 4 | **`Set-Location` KHÔNG đổi CWD của tiến trình .NET** ⇒ mọi `[System.IO.File]::*` **PHẢI** dùng **đường dẫn tuyệt đối** | Đường dẫn tương đối trỏ sang `D:\Projects\Tools\WF` ⇒ sửa file **thất bại im lặng**, build hỏng |
| 5 | **Mọi project Application của module PHẢI có `AbpModule` + khai trong `[DependsOn]` của Host** | Thiếu ⇒ API trả **404 body rỗng**, **không có lỗi biên dịch** |
| 6 | **Worker phải được ĐĂNG KÝ tường minh** (`AddBackgroundWorkerAsync`) | Thiếu ⇒ không lỗi build, app vẫn chạy, job **im lặng không bao giờ chạy** |
| 7 | **Worker định kỳ: đặt `Timer.RunOnStart = true` nếu đọc cấu hình trong `DoWorkAsync`** | `DoWorkAsync` chỉ chạy **sau khi hết nhịp đầu**; nhịp 1 ngày ⇒ cấu hình **không bao giờ có tác dụng** và job **im lặng suốt ngày đầu** (lỗi #23) |
| 8 | **Mọi unique index trên `IMultiTenant` PHẢI có `NULLS NOT DISTINCT`** | PostgreSQL coi NULL là khác nhau ⇒ ràng buộc **mất tác dụng im lặng** khi `TenantId` NULL (C4/F1) |
| 9 | **Test bất biến index PHẢI liệt kê ĐÍCH DANH tên index**, không chỉ đếm tổng | Đếm tổng: thêm 1 index mới + hỏng 1 index cũ ⇒ **tổng không đổi** ⇒ test xanh trong khi ràng buộc đã mất |
| 10 | **Migration: để EF sinh khung, KHÔNG viết tay** | Migration viết tay thiếu `.Designer.cs` bị **bỏ qua âm thầm** (C3) |
| 11 | **Chạy DbMigrator bằng binary MỚI build**, rồi **kiểm trạng thái DB thật** | DbMigrator báo `exit=0` nhưng **không áp** migration vì binary cũ |
| 12 | **Log level làm mất bằng chứng:** `Started background worker` là **Debug**; worker log theo **tên type của chính nó** (không phải `Volo.Abp.BackgroundWorkers`) | Bật sai category ⇒ **không thấy gì** và dễ kết luận sai là job không chạy |
| 13 | **Kiểm chứng bằng trạng thái THẬT** (DB, log, route), không bằng suy luận | Nhiều lần "có vẻ đúng" nhưng DB cho thấy ngược lại |

### Mẫu kiểm chứng end-to-end đã dùng thành công (nên lặp lại)
1. Chèn dữ liệu thử **thật** vào PostgreSQL (bằng `psql` qua docker) → 2. khởi động Host → 3. **đọc log thật** → 4. **xác nhận trạng thái DB thật** → 5. **dọn dữ liệu thử**.
   *Ví dụ đã làm:* chèn 1 SKU chưa phân loại thuế ⇒ worker đếm `1/1` + Warning `eCommerce:Tax:0003`; bật đồng bộ ĐVHC ⇒ `+3355` đơn vị thật từ API.

### Hạ tầng dev
`docker ps` phải thấy **`ecommerce-postgres`** (17-alpine) và **`ecommerce-redis`** (7-alpine) healthy.
Host cổng **5000**. DB: `docker exec -i ecommerce-postgres psql -U ecommerce -d ecommerce`.

---

## 3. GIAI ĐOẠN 0 — ĐÃ CHỐT

📄 **`docs/gd0-closure-report.md`** — kết luận + bằng chứng + 5 input bên ngoài.
**GĐ0 đóng ở mức có thể đóng bằng code.** Ba việc cuối đã xong:
- **A** — worker đồng bộ ĐVHC (`provinces.open-api.vn`, **+3355** đơn vị thật, idempotent)
- **B** — ràng buộc `EINV-003` (một bản thay thế/điều chỉnh ↔ một hoá đơn gốc)
- **C** — worker đồng bộ mã hãng + adapter cắm được (`IKhungDiaChiHangSource`)

**5 input bên ngoài — KHÔNG thể suy đoán, không còn việc code nào tồn:**
| # | Cần gì | Cho việc |
|---|---|---|
| 1 | Văn bản + **bảng chuyển đổi 3↔2 cấp** | `ADDR-003` (Q2) |
| 2 | **Bảng trường bắt buộc `TT 91/2026`** | `EINV-001` sinh XML |
| 3 | **Endpoint + tài liệu SOAP/XML của CQT** | `EINV-002` gửi thật |
| 4 | **Chứng thư sandbox** + chốt **Q6** | `SIGN-001` xác thực chữ ký |
| 5 | **Tài liệu API danh mục GHN + GHTK** | adapter cho `ADDR-002` |

> ⚠️ **ĐỪNG tự "hoàn thiện" 5 việc này bằng suy đoán.** Bịa bảng trường XML hoặc endpoint CQT sẽ tạo ra
> sản phẩm **trông như xong nhưng SAI VỀ PHÁP LÝ** — sai ở phần khó phát hiện nhất.

---

## 4. GIAI ĐOẠN 1 — VIỆC TIẾP THEO

### 🚨 QUYẾT ĐỊNH ĐANG MỞ — PHẢI HỎI NGƯỜI TRƯỚC KHI CODE

Đọc **`docs/runtime-verification-report.md` §9 (LỖI #22)** — **lỗi nghiêm trọng nhất dự án**:

> `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho **từng đơn hàng** kéo từ Shopee/Lazada,
> nhưng **KHÔNG có handler nào nhận**. Đồng bộ vẫn báo **THÀNH CÔNG** và **TIẾN CON TRỎ ĐỒNG BỘ**
> (`shop.LastSyncedAtUtc = pull.NewSyncCursorUtc`) ⇒ sàn **không gửi lại** ⇒ **đơn mất VĨNH VIỄN**,
> **không có lỗi nào được ghi**.

**Đã chặn được sự IM LẶNG** (`EventCoverageChecker` la ở mức Error mỗi lần boot) — nhưng **gốc chưa sửa**.
Ba lựa chọn, **cần người quyết vì là đánh đổi nghiệp vụ:**

| Lựa chọn | Đánh đổi |
|---|---|
| **A. Xây Order intake** (đúng gốc) | Hạng mục lớn; **đơn vẫn mất cho tới khi xong** |
| **B. Tạm dừng tiến con trỏ** khi chưa có nơi nhận (fail-closed) | Đơn **không mất** (sàn gửi lại), nhưng log đầy + đồng bộ coi như "kẹt" |
| **C. Chỉ để cảnh báo** (hiện tại) | Lỗi không còn im lặng, nhưng **đơn vẫn mất** |

### Phạm vi GĐ1 (theo kế hoạch)
Đơn hàng · tồn kho · vận chuyển · khuyến mãi. Đọc `docs/implementation-plan.md` §A.9 để biết danh sách feature theo giai đoạn.

### Việc kỹ thuật độc lập, làm được ngay
| Việc | Ghi chú |
|---|---|
| **5 metric còn lại** | `messaging_dlq_*` · `messaging_outbox_pending` cần **outbox/DLQ** (chưa có, **không có mẫu trong repo** ⇒ rủi ro cao nhất) · `logging_failures_total` cần **logger provider** · `lock_ttl_exceeded_total` chưa có chỗ phát |
| **Outbox/relay** | 🚨 **Cảnh báo:** **không có message broker** (chỉ PostgreSQL + Redis). Outbox lúc này là hạ tầng cho vấn đề **chưa tồn tại** — và sẽ **đảm bảo giao tin cậy tới KHÔNG AI** nếu chưa có người nhận. **Thứ tự đúng: có người nhận trước, rồi mới bàn tới giao tin cậy.** |

---

## 5. BẢN ĐỒ TÀI LIỆU — ĐỌC GÌ KHI CẦN GÌ

| Cần biết | Đọc |
|---|---|
| Trạng thái tổng thể + kế hoạch | `docs/implementation-plan.md` (§A.7 trạng thái · §A.9 danh sách feature · §A.10 chốt GĐ0) |
| GĐ0 đã chốt tới đâu + 5 input | `docs/gd0-closure-report.md` |
| **Lỗi thật đã gặp** (24 lỗi, 16 im lặng) | `docs/runtime-verification-report.md` |
| Thiết kế từng feature (52 doc, mỗi doc 16 mục) | `docs/features/` — mỗi doc có mục **cuối** ghi trạng thái triển khai + việc còn lại |
| Quy ước bất biến của dự án | `docs/features/XC-001…XC-006*.md` |
| Quan sát/log/metric | `docs/features/XC-005-observability-log-metric.md` (§16–18: lỗi tầng + 7/12 điểm phát) |

---

## 6. QUY ƯỚC BẤT BIẾN — KHÔNG ĐƯỢC VI PHẠM

1. Tiền: `decimal` + `Money`, **không bao giờ** so sánh `decimal` trần bằng `==`
2. Module sở hữu entity của mình; **module → app BỊ CẤM** (app → module được phép)
3. Mọi entity cần `DbSet` trên DbContext hợp nhất **hoặc** `IRepository<T>` sẽ **không** được đăng ký
4. `[Authorize]` ở **cấp class**
5. Sổ kho **append-only**
6. **Idempotency ở MỌI biên vào**
7. **Máy trạng thái PHẢI ném lỗi**, không im lặng bỏ qua
8. **Khoá phân tán** cho tài nguyên dùng chung — ⚠️ `AbpDistributedLockOptions.KeyPrefix` nối **không có dấu phân cách** ⇒ **KHÔNG** tự thêm tiền tố `ecommerce:` vào tên khoá (bài học C11/C13)
9. Ghi dữ liệu qua **Unit of Work**
10. **Một phiên bản ABP** trong `Directory.Packages.props`
11. **`NULLS NOT DISTINCT`** cho mọi unique index trên `IMultiTenant`
12. **Không AutoMapper** (dùng map tay / Mapperly)
13. **Trả lời và viết tài liệu bằng TIẾNG VIỆT**
14. **Khi không chắc: nêu rõ cái chưa kiểm chứng**, không khẳng định suông
15. Sửa xong ⇒ cập nhật mục **trạng thái triển khai** ở **cuối** tài liệu feature tương ứng

---

## 7. CÁCH MỞ SESSION MỚI (làm đúng thứ tự này)

1. **Kiểm tra môi trường:** `docker ps` (postgres + redis phải healthy) · `git log -1` · `git status` (phải sạch)
2. **Chạy baseline** trước khi sửa gì — để biết mình có làm hỏng không:
   ```powershell
   dotnet build eCommerce.slnx -v q --no-incremental      # mong đợi 0/0
   dotnet test test\eCommerce.Tests\eCommerce.Tests.csproj --nologo   # mong đợi 361/361
   ```
3. **Nói với agent việc cần làm**, kèm 2 câu quan trọng:
   - *"Đọc `docs/handover-gd1.md` và `docs/gd0-closure-report.md` trước."*
   - *"Trả lời bằng tiếng Việt. Tuân thủ quy trình kiểm chứng ở §2 của file bàn giao."*

> 📌 **Đừng dán lại toàn bộ nội dung cuộc trò chuyện cũ vào session mới.** File bàn giao này + git + 52 tài liệu
> đã chứa mọi thứ cần thiết, ở dạng **ngắn gọn và đã kiểm chứng**. Dán lại lịch sử chỉ làm **cạn ngữ cảnh
> của session mới ngay từ đầu** — đúng vấn đề đang muốn tránh.
