# BÁO CÁO CHỐT GIAI ĐOẠN 0

> **Ngày:** 14/10/2026 · **Commit mốc:** `2f9ff13` + lượt này
> **Phạm vi GĐ0:** hạ tầng nền + **Địa chỉ hành chính** + **Hoá đơn điện tử** + **Ký số** + luồng nghiệp vụ khép kín
> **KHÔNG bao gồm GĐ1** (đơn hàng, tồn kho, vận chuyển, khuyến mãi…) — theo yêu cầu.

---

## 1. KẾT LUẬN NGẮN

**GĐ0 đã đóng ở mức CÓ THỂ ĐÓNG BẰNG CODE.**

| | |
|---|---|
| Thành phần làm được bằng code | ✅ **XONG** — 7/7 feature có Domain + DB + API, luồng khép kín |
| Thành phần cần **input bên ngoài** | 🚧 **5 việc** — liệt kê chính xác ở §5, mỗi việc ghi rõ **cần gì để mở khoá** |
| Bằng chứng | **324 unit + 20 integration test** · build **0 lỗi/0 cảnh báo** · **35 route API** chạy thật · Host `health 200` |

> ⚠️ **Nói thẳng:** GĐ0 **không thể đóng 100%** bằng code, vì 5 việc còn lại phụ thuộc **văn bản pháp luật,
> tài liệu đối tác và chứng thư số** — những thứ **không thể suy đoán**. Nếu tôi tự bịa bảng trường XML
> hoặc endpoint của cơ quan thuế thì sản phẩm sẽ **trông như xong** nhưng **sai về pháp lý**, và sai ở
> phần khó phát hiện nhất. Đó là lý do tôi dừng ở đây thay vì "hoàn thiện" bằng suy đoán.

---

## 2. BẢNG TRẠNG THÁI THEO FEATURE (kèm BẰNG CHỨNG)

| Feature | Domain | DB | API | Job | Bằng chứng |
|---|---|---|---|---|---|
| **ADDR-001** ĐVHC 2 cấp | ✅ | ✅ | ✅ | 🟡 lõi ✅ / worker ⬜ | `AdministrativeUnitResolverTests`, `Api` 4 route · lõi đồng bộ + **4 hàng rào** ✅ |
| **ADDR-002** mã ĐVHC theo hãng | ✅ | ✅ | ✅ | 🚧 chờ tài liệu | `CarrierAddressMappingTests` (20) · `AddrCarrierMappings` unique `NULLS NOT DISTINCT` |
| **ADDR-003** ánh xạ 3↔2 cấp | ⬜ | ⬜ | ⬜ | ⬜ | 🚧 **chặn bởi Q2** — tài liệu cấm seed bằng suy luận |
| **EINV-001** hoá đơn | ✅ | ✅ | ✅ | — | `EInvoiceTests` · `EInvInvoices` + 2 CHECK |
| **EINV-002** gửi CQT | ✅ | ✅ | ✅ | ✅ | `EInvoiceSubmissionTests`, `EInvoiceResubmissionTests` · worker **đã xác nhận chạy thật** |
| **EINV-003** điều chỉnh/thay thế | ✅ | ✅ | ✅ | — | `EInvoiceTests` · state machine 2 trục |
| **SIGN-001** ký số | ✅ | ✅ | ✅ | — | `SigningTests` · `SigSigningRequests` CHECK bằng chứng ký |
| **Luồng GĐ0** khép kín | ✅ | ✅ | ✅ | — | `PhatHanhHoaDonGd0FlowTests` (13) + `HoanTatLuongGd0FlowTests` (10) · 3 route |

**Luồng nghiệp vụ đã khép kín và kiểm chứng:**
`xác minh địa chỉ → cấp số (khoá phân tán) → tạo hoá đơn → tạo yêu cầu ký → ký (bước 2) → phát hành → gửi CQT → nhận kết quả → gửi lại khi bị từ chối`

---

## 3. ĐÓNG GÓP CỦA LƯỢT CHỐT NÀY (`ADDR-001` lõi đồng bộ)

Thêm `AdministrativeUnitSynchronizer` (**lõi thuần, 16 test**) + `AdministrativeUnit.DoiTen/MoLai`.

### 🚨 BỐN HÀNG RÀO CHỐNG "ĐỒNG BỘ PHÁ HOẠI" — phần quan trọng nhất

Đồng bộ danh mục là thao tác **CÓ TÍNH PHÁ HOẠI**: nó **ĐÓNG** những mã không còn trong danh mục nguồn.
Nếu nguồn trả về **một phần** (API lỗi, timeout giữa chừng, tải phải trang rỗng) thì việc "đóng những mã
không còn" sẽ **đóng gần hết địa chỉ đang dùng** ⇒ mọi vận đơn mới **fail-closed** ⇒ **sập luồng bán hàng
chỉ vì một lần gọi API hỏng**.

| # | Hàng rào | Mã lỗi |
|---|---|---|
| 1 | Danh mục **rỗng** ⇒ từ chối | `eCommerce:Address:0601` |
| 2 | Danh mục **quá ít** đơn vị (< ngưỡng) ⇒ từ chối | `eCommerce:Address:0602` |
| 3 | **Thiếu cả cấp tỉnh** ⇒ từ chối (không thể là dữ liệu thật) | `eCommerce:Address:0603` |
| 4 | Số đơn vị bị **đóng vượt 20%** ⇒ từ chối | `eCommerce:Address:0604` |

### 🔒 Ba bất biến khác
- **KHÔNG BAO GIỜ XOÁ** — mã biến mất chỉ bị **ĐÓNG** (`IsActive = false`), vì hoá đơn/vận đơn cũ còn tham chiếu.
- **Đổi tên TẠI CHỖ** (giữ nguyên `Id`) — tên là **thuộc tính mô tả**, danh tính là *(phiên bản, cấp, mã)*.
  Tạo bản ghi mới sẽ làm tham chiếu cũ trỏ vào đơn vị **đã đóng** ⇒ tra cứu địa chỉ cũ **fail-closed**.
- **Chỉ xét đúng phiên bản schema** — mã ở v1 và v2 **không cùng ý nghĩa** (bài học ADDR-001: mã `00004`
  là *Phường Ba Đình* ở v2 nhưng *Phường Trúc Bạch* ở v1).

> 📌 Ghi nhận trung thực: **4/5 test hỏng đầu tiên là do hàng rào 4 chặn đúng** (dữ liệu test nhỏ nên
> đóng 1/2 = 50% > 20%). Tôi sửa **test**, không sửa hàng rào — hàng rào đang làm đúng việc của nó.

---

## 4. LỖI THẬT PHÁT HIỆN TRONG QUÁ TRÌNH LÀM GĐ0

**22 lỗi thật**, trong đó **14 lỗi im lặng** (build xanh, test xanh, app chạy — nhưng sai).

| Nhóm | Ví dụ điển hình |
|---|---|
| Lỗi **im lặng** | C3 migration không `.Designer.cs` bị **bỏ qua âm thầm** · C10 worker bị supervisor thay mỗi nhịp ⇒ **0 lần chạy** · C16 ConventionalControllers chỉ thấy assembly **đã nạp** · Kernel thiếu `AbpModule` ⇒ **app không khởi động** · **lỗi #22** |
| Lỗi **tầng** (3 lần) | C8 → C16 → Kernel thiếu `AbpModule`: **chỉ `ProjectReference` là KHÔNG ĐỦ** |
| Lỗi **bảo mật** | `System.Security.Cryptography.Xml` 10.0.11 (4 khuyến nghị) trong **đường ký XMLDSig**; bản 10.0.12 được ghim "vá lỗ hổng" **vẫn còn lỗ hổng** ⇒ nâng 10.0.13 |
| Lỗi **dữ liệu** | C4/F1: unique index **mất tác dụng khi `TenantId` NULL** ⇒ `NULLS NOT DISTINCT` (nay **17 index**) |

### 🚨 LỖI #22 — NGOÀI PHẠM VI GĐ0 NHƯNG PHẢI GHI LẠI (thuộc GĐ1)
`ChannelShopSyncer` publish `ChannelOrderPulledEto` cho **từng đơn hàng** kéo từ Shopee/Lazada, và
**không có handler nào nhận** ⇒ đồng bộ báo **THÀNH CÔNG**, **tiến con trỏ** ⇒ **đơn mất vĩnh viễn**.
Đã chặn bằng **kiểm tra độ phủ sự kiện lúc khởi động** (`EventCoverageChecker`, la ở mức Error mỗi lần boot).
**Việc sửa gốc thuộc GĐ1** — cố ý **không** làm trong lượt này.

---

## 5. NĂM VIỆC CÒN LẠI — VÀ **CHÍNH XÁC** CẦN GÌ ĐỂ MỞ KHOÁ

| # | Việc | Cần gì để mở khoá | Ai cung cấp |
|---|---|---|---|
| 1 | **`ADDR-003`** ánh xạ 3 cấp ↔ 2 cấp | **Văn bản chính thức** kèm **bảng chuyển đổi** (Q2). Tài liệu **cấm seed bằng suy luận** | Cơ quan ban hành / kế toán |
| 2 | **`EINV-001`** sinh XML `TT 91/2026` | **Bảng trường bắt buộc** của mẫu hoá đơn | Kế toán (từ HTKK/VNPT) |
| 3 | **`EINV-002`** gọi CQT thật | **Endpoint + tài liệu SOAP/XML** của CQT | CQT / nhà cung cấp |
| 4 | **`SIGN-001`** xác thực chữ ký | **Chứng thư sandbox** (NCC ký số) + chốt **Q6** (bước 2 nhận **tệp hash đã ký** hay **XML đã ký**) | NCC ký số |
| 5 | **`ADDR-002`** job đồng bộ mã hãng | **Endpoint + tài liệu** danh mục của **GHN và GHTK** | GHN / GHTK |

### Việc **làm được ngay** mà không chờ ai (đề xuất cho lượt sau)
| # | Việc | Ước lượng |
|---|---|---|
| A | **Worker đồng bộ ĐVHC** — gọi `provinces.open-api.vn` (34 tỉnh, phải gọi `?depth=2` theo từng tỉnh) + dùng 4 hàng rào đã có | 1 lượt |
| B | **Ràng buộc `EINV-003`**: mỗi hoá đơn gốc chỉ có **một bản thay thế đang hiệu lực** (DB + domain) | 1 lượt |
| C | **Worker đồng bộ mã hãng** với **adapter cắm được** + nguồn giả (khi có tài liệu GHN/GHTK chỉ viết adapter) | 1 lượt |

---

## 6. TIÊU CHÍ NGHIỆM THU GĐ0 — ĐỐI CHIẾU

| Tiêu chí | Trạng thái | Bằng chứng |
|---|---|---|
| Địa chỉ hành chính dùng **dữ liệu**, không hard-code | ✅ | `AddrAdministrativeUnits`, CHECK `SchemaVersion in (1,2)` |
| Mã ĐVHC **không dùng làm khoá toàn cục** | ✅ | Khoá = *(phiên bản, cấp, mã)*; có test chứng minh mã trùng giữa v1/v2 |
| Mọi unique index trên `IMultiTenant` **có hiệu lực thật** | ✅ | **17** index `NULLS NOT DISTINCT`, có test đối chiếu tên + số lượng |
| Thuế suất **tự chuyển 8% → 10%** khi hết khung, không cần release | ✅ | `TaxRateResolver` (hàm thuần) + test mốc 31/12/2026 → 01/01/2027 |
| Hoá đơn có **state machine 2 trục** và **ném lỗi** khi sai | ✅ | `EInvoiceTests`; 2 trục độc lập (phát hành / CQT) |
| Gửi CQT **thất bại thì gửi lại được**, không mất hoá đơn | ✅ | `EInvoiceResubmissionPolicy` + worker **đã xác nhận chạy thật** |
| Ký số **ràng buộc thời điểm** và **khớp hash** | ✅ | `SigningRequest.HoanTatKy` + CHECK `DaKyPhaiCoDuBangChung` |
| Luồng **khép kín** từ địa chỉ tới kết quả CQT | ✅ | 23 test luồng + 3 route API chạy thật |
| **Không có lỗi im lặng** ở các đường đã kiểm chứng | ✅ | 14 lỗi im lặng **đã tìm ra và sửa** — không còn cái nào đã biết mà chưa xử lý |
| Mọi metric **có nơi phát** | 🟡 **7/12** | 5 metric còn lại cần **outbox/DLQ** hoặc **logger provider** (hạ tầng mới) |

---

## 7. TRẠNG THÁI TỔNG THỂ

| | |
|---|---|
| Tài liệu thiết kế | **52/52** · Mermaid **197 khối / 0 lỗi** |
| Test | **324 unit + 20 integration** (PostgreSQL 17 thật) |
| Build | **0 lỗi / 0 cảnh báo** (kiểm bằng `--no-incremental` — build incremental **ẩn** cảnh báo) |
| Commit | **78** · git sạch |
| API | **35 route** thật · `401`/`403`/`200` đúng ma trận quyền |
| DB | **17** index `NULLS NOT DISTINCT` · 7 migration |
| Lỗi thật đã sửa | **22** (14 lỗi im lặng) |
| Điểm phát metric | **7/12** |

---

## 8. ĐỀ XUẤT

**GĐ0 nên được coi là ĐÓNG về phần code**, và **mở lại** chỉ khi một trong 5 input ở §5 đến.

**Thứ tự đề xuất cho lượt sau** (không đụng GĐ1):
1. **A — Worker đồng bộ ĐVHC** (hoàn tất `ADDR-001`, dùng ngay 4 hàng rào đã test),
2. **B — Ràng buộc `EINV-003`** một bản thay thế hiệu lực,
3. **C — Worker đồng bộ mã hãng** với adapter cắm được.

Sau 3 việc đó, **toàn bộ phần GĐ0 làm được bằng code sẽ hết** — và khi đó câu trả lời cho *"còn gì nữa không?"*
sẽ là một danh sách **chỉ gồm 5 input bên ngoài**, không còn việc kỹ thuật nào tồn.


---

## 9. CẬP NHẬT (14/10/2026) — **`ADDR-001` ĐÃ HOÀN TẤT** (việc **A** trong §5)

| Thành phần | Nội dung |
|---|---|
| `ProvincesOpenApiParser` | **Lõi THUẦN** — phân tích JSON thật của API (13 test dùng **JSON rút từ phản hồi thật**) |
| `ProvincesOpenApiSource` | Vỏ mỏng — gọi **35 HTTP** (1 danh sách + 34 tỉnh `?depth=2`), **fail-closed** nếu một tỉnh lỗi |
| `AdministrativeUnitSyncWorker` | Job định kỳ, **khoá phân tán**, gọi 4 hàng rào ở §3 |

### 🚨 MẶC ĐỊNH **TẮT** (`Address:Sync:Enabled = false`)
Job này **gọi API bên ngoài** rồi **sửa danh mục địa chỉ đang dùng**. Bật mặc định nghĩa là một dịch vụ
ngoài (hoặc mạng) có thể **thay đổi dữ liệu vận hành** mà không ai chủ động quyết định. Người vận hành
bật bằng cấu hình, và tắt được ngay khi nguồn có sự cố.

### 🐛 LỖI THẬT DO KIỂM CHỨNG END-TO-END BẮT ĐƯỢC (lỗi #23)

**Triệu chứng:** worker log `Started background worker` nhưng **KHÔNG có dòng log nào** và DB vẫn **0 đơn vị**.

**Nguyên nhân:** tôi đặt `Timer.Period = 86_400s` trong constructor và đọc nhịp từ cấu hình **bên trong**
`DoWorkAsync` — nhưng `DoWorkAsync` chỉ chạy **SAU KHI hết nhịp đầu tiên**. Với nhịp mặc định 1 ngày,
điều đó nghĩa là: cấu hình **không bao giờ có tác dụng ở lượt đầu**, và worker **im lặng không chạy gì
trong suốt ngày đầu sau mỗi lần deploy** — lại đúng họ **lỗi im lặng** đã gặp nhiều lần.

**Đã sửa:** đặt `Timer.RunOnStart = true`. An toàn vì job **mặc định TẮT** ⇒ lượt chạy ngay chỉ **đọc cấu
hình rồi thoát, không gọi API ngoài**. Khi người vận hành bật thì đồng bộ ngay sau deploy là **mong muốn**.

### ✅ BẰNG CHỨNG END-TO-END TRÊN API THẬT + POSTGRESQL THẬT

| Lượt | Log / kết quả thật |
|---|---|
| Lượt 1 | `Đồng bộ danh mục ĐVHC xong (nguồn provinces.open-api.vn, phiên bản 2): +3355 mới, ~0 đổi tên, ~0 mở lại, -0 đóng.` |
| Lượt 2 (5 giây sau) | `Đồng bộ danh mục ĐVHC: không có thay đổi (… 3355 đơn vị).` ⇒ **IDEMPOTENT** |
| PostgreSQL | **34** tỉnh (`Level=1`) + **3321** xã (`Level=2`) = **3355** — khớp **chính xác** API (v2 = 34 tỉnh) |
| Đệm 0 | `01 \| Thành phố Hà Nội` (API trả `code: 1`) · `00004 \| Phường Ba Đình \| 01` |
| Toàn vẹn | **0 xã mồ côi** (mọi `ParentCode` đều trỏ tới đơn vị có thật) · **17** index `NULLS NOT DISTINCT` nguyên vẹn |

### Ba cạm bẫy của API đã xử lý (mỗi cái có test)
1. **`wards` RỖNG ở endpoint danh sách** (`/api/v2/p/` trả 34 tỉnh, **0 xã**) ⇒ phải gọi `?depth=2` **từng tỉnh**.
   Ai chỉ gọi endpoint danh sách sẽ nhận danh mục **thiếu toàn bộ cấp xã**.
2. **Mã là SỐ NGUYÊN** (`4`, `1267`) còn `AdministrativeUnit.Code` cần **đủ 5 ký tự** ⇒ **phải đệm 0 bên trái**;
   mã dài hơn quy ước ⇒ **NÉM LỖI**, **không cắt bớt** (cắt bớt tạo mã "hợp lệ" nhưng SAI, không ai phát hiện).
3. **v1 và v2 khác cấu trúc** (v2: `province.wards`; v1: `province.districts[].wards`) — parser xử lý cả hai.

### Cập nhật §5 (việc còn lại)
| # | Việc | Trạng thái mới |
|---|---|---|
| **A** | `ADDR-001` worker đồng bộ | ✅ **XONG** (mục 9 này) |
| **B** | Ràng buộc `EINV-003` một bản thay thế hiệu lực | ⬜ còn lại |
| **C** | Worker đồng bộ mã hãng (adapter cắm được) | ⬜ còn lại |