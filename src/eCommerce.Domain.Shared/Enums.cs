namespace eCommerce;

/// <summary>
/// Kênh bán. Giá trị lưu dạng chuỗi trong DB (xem <c>Channel.Code</c>), enum này chỉ để
/// phân loại hành vi đồng bộ.
/// </summary>
public enum ChannelType
{
    /// <summary>Website bán hàng của chính mình (nguồn sự thật về tồn kho).</summary>
    OwnStorefront = 0,

    /// <summary>Sàn TMĐT (Shopee, Lazada, TikTok Shop...).</summary>
    Marketplace = 1,

    /// <summary>Kênh bán offline / POS.</summary>
    Offline = 2,

    /// <summary>Kênh khác (Zalo, Facebook...).</summary>
    Other = 99
}

/// <summary>
/// Loại biến động kho. <c>StockLedgerEntry</c> là append-only: mọi thay đổi tồn kho
/// đều phải sinh một bản ghi ở đây, không được UPDATE tồn kho trực tiếp.
/// </summary>
public enum StockMovementType
{
    /// <summary>Nhập kho.</summary>
    Receipt = 0,

    /// <summary>Giữ chỗ cho đơn (chưa xuất kho).</summary>
    Reservation = 1,

    /// <summary>Nhả giữ chỗ (đơn hủy / hết TTL).</summary>
    ReservationRelease = 2,

    /// <summary>Xuất kho bán hàng.</summary>
    Sale = 3,

    /// <summary>Khách trả hàng, nhập lại kho.</summary>
    CustomerReturn = 4,

    /// <summary>Điều chỉnh kiểm kê.</summary>
    Adjustment = 5,

    /// <summary>Chuyển kho.</summary>
    Transfer = 6,

    // 🚨🚨 **`INV-001` ADR-04 — CHỈ ĐƯỢC THÊM Ở CUỐI, KHÔNG BAO GIỜ ĐỔI SỐ CỦA GIÁ TRỊ CŨ.**
    // `StockMovementType` được lưu **dạng số** trong `InvStockLedger.MovementType` ⇒ đổi số của giá trị cũ
    // sẽ **diễn giải lại toàn bộ lịch sử kho**: một bút toán "Nhập kho" thành "Giữ chỗ" — **sai sổ cái mà
    // không có lỗi nào**. Test `StockMovementTypeTests` khoá bất biến này.
    // 📌 Trước `INV-001`, enum **THIẾU** giá trị cho **kiểm kê** và **chuyển kho đến/đi** ⇒ không ghi được
    // nghiệp vụ thật (phải "mượn" `Adjustment`/`Transfer` ⇒ **mất khả năng truy vết**).

    /// <summary>Kiểm kê **thừa** (đếm được nhiều hơn sổ) — `INV-001`.</summary>
    StocktakeGain = 7,

    /// <summary>Kiểm kê **thiếu** (đếm được ít hơn sổ) — `INV-001`.</summary>
    StocktakeLoss = 8,

    /// <summary>Chuyển kho — phần **ĐI** (kho nguồn) — `INV-001`.</summary>
    TransferOut = 9,

    /// <summary>Chuyển kho — phần **ĐẾN** (kho đích) — `INV-001`.</summary>
    TransferIn = 10,

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🆕 **`WP-04`** (02/11/2026) — 9 CHIỀU TỒN KHO kiểu Nhanh (`docs/nhanh/07` WP-04 việc 3).
    // 📌 Mỗi giá trị ở đây có lý do tồn tại: **cột tồn tương ứng đã có** trên `InvItems`. Thiếu giá trị thì
    // nghiệp vụ buộc phải **mượn** một giá trị khác ⇒ sổ cái **mất khả năng truy vết** (đúng lỗi đã xảy ra
    // với kiểm kê/chuyển kho trước `INV-001` — xem chú thích ở `StocktakeGain`).
    // 🚨 **CHỈ THÊM Ở CUỐI DẢI, KHÔNG ĐỔI SỐ CỦA GIÁ TRỊ CŨ (D-17).**
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Xuất kho để **giao hàng**: hàng rời kho (`OnHand` ↓) và vào chiều `Shipping`.</summary>
    ShippingOut = 11,

    /// <summary>Hàng **đang giao được trả lại kho**: chiều `Shipping` ↓ và `OnHand` ↑.</summary>
    ShippingReturn = 12,

    /// <summary>Đánh dấu hàng **hỏng/lỗi**: chiều `Damaged` ↑ (`OnHand` **KHÔNG** đổi — hàng vẫn nằm trong kho).</summary>
    DamagedIn = 13,

    /// <summary>Xuất **huỷ** hàng hỏng: chiều `Damaged` ↓ **và** `OnHand` ↓ (hàng ra khỏi kho hẳn).</summary>
    DamagedOut = 14,

    /// <summary>Gửi hàng đi **bảo hành**: `OnHand` ↓, chiều `Warranty` ↑.</summary>
    WarrantyHold = 15,

    /// <summary>Nhận lại hàng **từ bảo hành**: chiều `Warranty` ↓, `OnHand` ↑.</summary>
    WarrantyRelease = 16,

    /// <summary>**Tạm giữ thủ công** (giữ hàng cho khách/đơn chưa chốt): chiều `Holding` ↑.</summary>
    Hold = 17,

    /// <summary>**Nhả tạm giữ** thủ công: chiều `Holding` ↓.</summary>
    HoldRelease = 18,

    /// <summary>**Bán lẻ (POS)** xuất kho — tách khỏi `Sale` (đơn website) để báo cáo kênh bán **không lẫn**.</summary>
    RetailSaleOut = 19,

    /// <summary>
    /// **Xuất trả nhà cung cấp** (Nhanh: phiếu *xuất* với `mode = 5 Nhà cung cấp`).
    /// 📌 **VÌ SAO PHẢI THÊM (không mượn `TransferOut`/`Sale`):** `TransferOut` trong sổ cái nghĩa *"đi vì chuyển
    /// kho"* và `Sale` nghĩa *"đi vì bán"* — mượn chúng thì báo cáo *"trả NCC"* **không tách được** khỏi bán/chuyển
    /// kho, đúng họ lỗi mà `INV-001` đã sửa cho kiểm kê (`StocktakeGain`/`Loss`).
    /// 🔒 Thêm ở CUỐI dải (D-17).
    /// </summary>
    SupplierReturnOut = 20,

    /// <summary>
    /// **Xuất khác** (Nhanh: phiếu *xuất* với `mode = 10 Khác`) — điều chỉnh giảm không thuộc loại nào.
    /// 📌 **KHÔNG** dùng `Adjustment` vì `Adjustment` trong hệ thống này **tăng** `OnHand`; dùng nó cho phiếu
    /// xuất sẽ **đảo chiều nghiệp vụ** (xuất 5 thành nhập 5) mà không có lỗi nào.
    /// 🔒 Thêm ở CUỐI dải (D-17).
    /// </summary>
    OtherOut = 21
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Completed = 4,
    Cancelled = 5,
    Returned = 6
}

public enum PaymentStatus
{
    Unpaid = 0,
    Pending = 1,
    Paid = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
    Failed = 5
}

/// <summary>
/// **TRỤC 1 — THỰC HIỆN** của đơn (`SAL-003` D1/D3): *"hàng đang ở đâu trên đường tới khách"*.
///
/// 🚨 **VÌ SAO KHÔNG DÙNG LẠI `OrderStatus`:** enum cũ trộn **3 nghĩa** — `Shipped` (thực hiện),
/// `Cancelled` (kết thúc), `Returned` (kết thúc) — nên không trả lời được câu hỏi vận hành
/// *"đơn nào đã giao mà chưa thu tiền (COD)?"*. `OrderStatus` nay chỉ còn là **trạng thái SUY RA**
/// (`Order.Status`) để hiển thị/tương thích ngược.
/// ⚠️ **CHỈ THÊM GIÁ TRỊ Ở CUỐI, KHÔNG ĐỔI SỐ** — cột lưu dạng số.
/// </summary>
public enum FulfillmentStatus
{
    /// <summary>Mới tạo, chưa xác nhận.</summary>
    Pending = 0,

    /// <summary>Đã xác nhận (đã chốt giữ chỗ/xuất kho).</summary>
    Confirmed = 1,

    /// <summary>Đang đóng gói.</summary>
    Packing = 2,

    /// <summary>Chờ hãng vận chuyển lấy hàng.</summary>
    AwaitingPickup = 3,

    /// <summary>Đang giao.</summary>
    OutForDelivery = 4,

    /// <summary>Đã giao thành công.</summary>
    Delivered = 5,

    /// <summary>Giao thất bại (sẽ giao lại hoặc chuyển hoàn).</summary>
    DeliveryFailed = 6,

    /// <summary>Đang trên đường hoàn về kho.</summary>
    Returning = 7,

    /// <summary>Đã về kho — **hấp thụ**.</summary>
    Returned = 8,

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🆕 **`WP-05`** (`N6`, 03/11/2026) — 9 giá trị còn lại của **18 trạng thái Nhanh** (`D-08`).
    //
    // 🔑 **VÌ SAO CHỈ 9 GIÁ TRỊ, KHÔNG PHẢI 18:** 9 giá trị `0…8` ở trên **đã phủ đúng 9** trong 18 trạng thái
    // của Nhanh (Mới · Đã xác nhận · Đang đóng gói · Chờ thu gom · Đang chuyển · Thành công · Thất bại ·
    // Đang hoàn · Đã hoàn). Đối chiếu từng dòng `docs/nhanh/nhanh-pos-module-map.md` §6.2 ⇒ còn thiếu **đúng 9**.
    // 🚨 **CHỈ APPEND, KHÔNG ĐỔI SỐ CŨ (`D-17`)** — cột `OrdOrders.FulfillmentStatus` lưu **số**; đổi số của
    // `Pending`/`Delivered` là **diễn giải lại toàn bộ lịch sử đơn**: một đơn "đã giao" bỗng đọc thành
    // "chờ xác nhận" mà **không có lỗi nào**. Test `Wp05DonHangNhanhTests` khoá bất biến này.
    //
    // 📌 **BA trạng thái HUỶ (`CustomerCancelled`/`SystemCancelled`/`CarrierCancelled`) KHÔNG phải trục thứ 5:**
    // chúng là **giá trị của trục thực hiện** để ghi **AI huỷ** (dữ liệu Nhanh trả về), còn *"đơn đã kết thúc"*
    // vẫn là `ReturnStatus.Cancelled` — **một** nguồn sự thật cho câu hỏi *"đơn còn sống không"*.
    // ➡️ Vì vậy `Order.HuyDonNhanh` ghi **cả hai** trục trong **một** lần gọi (2 dòng lịch sử), không có
    // đường nào chỉ ghi một trục rồi để trục kia nói khác.
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>**Chờ xác nhận** (Nhanh `57 Chờ khách xác nhận`) — đã gọi khách, chưa chốt.</summary>
    PendingConfirm = 9,

    /// <summary>**Đang xác nhận** (Nhanh `55`) — CSKH đang gọi/đang sửa đơn.</summary>
    Confirming = 10,

    /// <summary>**Hết hàng** (Nhanh `68`) — hàng khách đặt đã hết; **không** ghi nhận tồn.</summary>
    OutOfStock = 11,

    /// <summary>**Đổi kho hàng** (Nhanh `73`) — chuyển sang cửa hàng/kho còn tồn (vẫn tạm giữ).</summary>
    WarehouseSwitched = 12,

    /// <summary>**Đã đóng gói** (Nhanh `40`) — đóng gói xong, chờ bắn sang HVC.</summary>
    Packed = 13,

    /// <summary>**Khách huỷ** (Nhanh `63`) — khách không lấy hàng. Trục kết thúc **cũng** `Cancelled`.</summary>
    CustomerCancelled = 14,

    /// <summary>**Hệ thống huỷ** (Nhanh `64`) — job/hệ thống tự huỷ. Trục kết thúc **cũng** `Cancelled`.</summary>
    SystemCancelled = 15,

    /// <summary>**HVC huỷ** (Nhanh `58`) — hãng vận chuyển huỷ đơn. Trục kết thúc **cũng** `Cancelled`.</summary>
    CarrierCancelled = 16,

    /// <summary>**Xác nhận hoàn** (Nhanh `74`) — HVC báo đã hoàn; chờ kho đếm nhận (`Returned`).</summary>
    ReturnConfirmed = 17
}

/// <summary>
/// 🆕 **`WP-05`** — **LOẠI ĐƠN** (Nhanh `order.info.type`), **10 giá trị ĐO ĐƯỢC** từ Open API v3
/// (`nhanh-pos-v3-api-inventory.md` §2.3 — Model Constant).
///
/// 🚨 **VÌ SAO 10 MÀ KHÔNG PHẢI 13:** [07 §WP-05](../../docs/nhanh/07-ke-hoach-trien-khai.md) ghi *"`OrderType` (13)"*,
/// nhưng danh mục **đo được** chỉ có **10** giá trị (1, 2, 3, 5, 10, 12, 14, 15, 16, 17 — **không** có 4, 6–9,
/// 11, 13). Theo đúng tiền lệ `D-20` (**không tự chế giá trị để "đủ số"**), ở đây dùng **danh mục đo được** và
/// ghi lại con số lệch — xem `D-38`.
/// 🔒 **Số là hợp đồng dữ liệu với Nhanh** ⇒ **KHÔNG đánh số lại** (khoảng trống giữ nguyên).
/// </summary>
public enum OrderType
{
    /// <summary>1 — Giao hàng tận nhà (mặc định).</summary>
    Delivery = 1,

    /// <summary>2 — Mua tại quầy (khách đến lấy).</summary>
    BuyAtCounter = 2,

    /// <summary>3 — Đặt trước.</summary>
    PreOrder = 3,

    /// <summary>5 — Đổi quà.</summary>
    GiftExchange = 5,

    /// <summary>10 — Xin báo giá.</summary>
    QuoteRequest = 10,

    /// <summary>12 — Đổi sản phẩm.</summary>
    ProductExchange = 12,

    /// <summary>14 — Khách trả lại hàng.</summary>
    CustomerReturn = 14,

    /// <summary>15 — Hàng chuyển kho.</summary>
    WarehouseTransfer = 15,

    /// <summary>16 — Đơn hoàn một phần.</summary>
    PartialReturn = 16,

    /// <summary>17 — Đền bù mất hàng.</summary>
    LostGoodsCompensation = 17
}

/// <summary>
/// 🆕 **`WP-05`** — **NGUỒN ĐƠN** (Nhanh `channel.saleChannel`), **14 giá trị ĐO ĐƯỢC**
/// (`nhanh-pos-v3-api-inventory.md` §2.3 — Model Constant).
/// 📌 [07 §WP-05](../../docs/nhanh/07-ke-hoach-trien-khai.md) ghi *"`OrderSource` (12)"*, đo được là **14**
/// ⇒ dùng đo được, ghi ở `D-38` (cùng lý do `D-20`: không cắt bớt giá trị thật để khớp con số trong kế hoạch).
/// 🔒 **KHÔNG đánh số lại** — số là hợp đồng dữ liệu với dữ liệu nhập từ Nhanh.
/// </summary>
public enum OrderSource
{
    /// <summary>1 — Admin (tự tạo trên phần mềm).</summary>
    Admin = 1,

    /// <summary>2 — Website.</summary>
    Website = 2,

    /// <summary>10 — API (bên thứ ba).</summary>
    Api = 10,

    /// <summary>20 — Facebook.</summary>
    Facebook = 20,

    /// <summary>21 — Instagram.</summary>
    Instagram = 21,

    /// <summary>41 — Lazada.</summary>
    Lazada = 41,

    /// <summary>42 — Shopee.</summary>
    Shopee = 42,

    /// <summary>43 — Sendo.</summary>
    Sendo = 43,

    /// <summary>45 — Tiki.</summary>
    Tiki = 45,

    /// <summary>48 — TikTok Shop.</summary>
    TikTokShop = 48,

    /// <summary>49 — Zalo OA.</summary>
    ZaloOa = 49,

    /// <summary>50 — Shopee chat.</summary>
    ShopeeChat = 50,

    /// <summary>51 — Lazada chat.</summary>
    LazadaChat = 51,

    /// <summary>52 — Zalo cá nhân.</summary>
    ZaloPersonal = 52
}

/// <summary>
/// 🆕 **`WP-05`** — **LÝ DO** của đơn (Nhanh `info.reason`), **24 giá trị ĐO ĐƯỢC**
/// (`nhanh-pos-v3-api-inventory.md` §2.3 — Model Constant). Con số **24 khớp** kế hoạch 07 §WP-05.
///
/// 📌 **VÌ SAO LÀ ENUM MÀ `OrderStatusHistory.ReasonCode` VẪN LÀ CHUỖI:** `ReasonCode` trong lịch sử là
/// **mã hành động** (`XacNhanDon`, `WebhookDaGiao`…) — do hệ thống sinh, **không** phải lý do nghiệp vụ của
/// Nhanh. Trộn hai nghĩa vào một cột là **một cột hai nghĩa** ⇒ tách: `Order.ReasonCode` (enum này, `int?`)
/// cho lý do nghiệp vụ; lịch sử giữ chuỗi hành động.
/// 🔒 **KHÔNG đánh số lại** (dải có khoảng trống: 7, 13, 15, 17, 21, 27, 28, 31–34).
/// </summary>
public enum OrderReasonCode
{
    /// <summary>1 — Đặt nhầm.</summary>
    DatNham = 1,

    /// <summary>2 — Phí ship cao.</summary>
    PhiShipCao = 2,

    /// <summary>3 — Không muốn chuyển khoản.</summary>
    KhongMuonChuyenKhoan = 3,

    /// <summary>4 — Đơn trùng.</summary>
    DonTrung = 4,

    /// <summary>5 — Không gọi được khách.</summary>
    KhongGoiDuocKhach = 5,

    /// <summary>6 — Hết hàng.</summary>
    HetHang = 6,

    /// <summary>8 — Chờ chuyển khoản.</summary>
    ChoChuyenKhoan = 8,

    /// <summary>9 — Khách không thích.</summary>
    KhachKhongThich = 9,

    /// <summary>10 — Không hài lòng nhân viên vận chuyển.</summary>
    KhongHaiLongNvVanChuyen = 10,

    /// <summary>11 — Giao chậm.</summary>
    GiaoCham = 11,

    /// <summary>12 — Đã mua tại cửa hàng.</summary>
    DaMuaTaiCuaHang = 12,

    /// <summary>14 — Sai địa chỉ.</summary>
    SaiDiaChi = 14,

    /// <summary>16 — Không muốn mua nữa.</summary>
    KhongMuonMuaNua = 16,

    /// <summary>18 — Lý do khác.</summary>
    LyDoKhac = 18,

    /// <summary>19 — Không liên hệ được người gửi.</summary>
    KhongLienHeDuocNguoiGui = 19,

    /// <summary>20 — Người gửi không bán online/ngoại tỉnh.</summary>
    NguoiGuiKhongBanOnline = 20,

    /// <summary>22 — Không bàn giao cho HVC.</summary>
    KhongBanGiaoChoHvc = 22,

    /// <summary>23 — HVC lấy hàng muộn.</summary>
    HvcLayHangMuon = 23,

    /// <summary>24 — Sai địa chỉ kho lấy.</summary>
    SaiDiaChiKhoLay = 24,

    /// <summary>25 — HVC làm mất hàng.</summary>
    HvcLamMatHang = 25,

    /// <summary>26 — Người gửi tự vận chuyển.</summary>
    NguoiGuiTuVanChuyen = 26,

    /// <summary>29 — Người gửi không xử lý đơn.</summary>
    NguoiGuiKhongXuLyDon = 29,

    /// <summary>30 — Sai giá.</summary>
    SaiGia = 30,

    /// <summary>35 — Khách đi vắng.</summary>
    KhachDiVang = 35
}

/// <summary>
/// **TRỤC 3 — ĐỐI SOÁT** (`SAL-003`): tiền từ sàn/COD về **sau** khi giao và **có thể lệch**.
/// `NotRequired` dùng cho đơn không có tiền cần đối soát (0 đồng, huỷ trước khi thu).
/// </summary>
public enum ReconciliationStatus
{
    /// <summary>Không cần đối soát — **hấp thụ**.</summary>
    NotRequired = 0,

    /// <summary>Chờ đối soát kỳ này.</summary>
    AwaitingReconcile = 1,

    /// <summary>Đã khớp — **hấp thụ**.</summary>
    Matched = 2,

    /// <summary>Có lệch, chưa xử lý.</summary>
    Mismatched = 3,

    /// <summary>Đã xử lý lệch — **hấp thụ**.</summary>
    Resolved = 4
}

/// <summary>
/// **TRỤC 4 — KẾT THÚC** (`SAL-003`): trả hàng và huỷ. `Cancelled` là trạng thái **hấp thụ**;
/// muốn bán lại phải **tạo đơn mới** (D10).
/// 📌 `ReturnStatus.Cancelled` là giá trị của **trục kết thúc**, không phải trục thực hiện — nhờ vậy
/// một đơn vừa "đã giao" vừa "đã huỷ" không thể tồn tại (mâu thuẫn bị chặn ngay ở dữ liệu).
/// 🆕 **`S14H`** — `PartiallyReturned` = đã nhận hàng hoàn **MỘT PHẦN**; **không hấp thụ** (còn đi tiếp
/// sang `Returned` khi nhận nốt). 📌 Nó **không** làm đổi `OrderStatus` tổng thể (xem ghi chú ở giá trị đó).
/// </summary>
public enum ReturnStatus
{
    /// <summary>Chưa có yêu cầu trả.</summary>
    Open = 0,

    /// <summary>Khách/sàn yêu cầu trả.</summary>
    ReturnRequested = 1,

    /// <summary>Đã duyệt trả.</summary>
    ReturnApproved = 2,

    /// <summary>Từ chối trả — **hấp thụ**.</summary>
    ReturnRejected = 3,

    /// <summary>Đã trả xong — **hấp thụ**.</summary>
    Returned = 4,

    /// <summary>Đã huỷ đơn — **hấp thụ**.</summary>
    Cancelled = 5,

    /// <summary>
    /// 🆕 **`S14H` — TRẢ MỘT PHẦN (BA chốt 29/10/2026).** Đã nhận hàng hoàn **một phần**; **chưa hấp thụ**
    /// (còn chuyển được sang `Returned` khi nhận nốt).
    ///
    /// 🚨 **CHỈ ĐƯỢC THÊM Ở CUỐI DẢI, KHÔNG BAO GIỜ ĐỔI SỐ CỦA GIÁ TRỊ CŨ.** `OrdOrders.ReturnStatus` lưu
    /// **dạng số** (`int`) ⇒ đổi số của `Returned`/`Cancelled` là **diễn giải lại toàn bộ lịch sử**: một đơn
    /// "đã trả xong" bỗng đọc thành "trả một phần" — **sai dữ liệu mà không có lỗi nào**. Vì vậy `Cancelled`
    /// **vẫn** là `5`, giá trị mới nhận `6` (số kế tiếp, không tái dùng số của ai).
    ///
    /// 📌 **KHÔNG sinh `OrderStatus` mới:** đơn trả một phần vẫn suy ra `Completed` (từ trục thực hiện
    /// `Delivered`) — người đọc phải hiểu *"đơn vẫn `Completed` nhưng trục kết thúc đã `PartiallyReturned`"*.
    /// Thêm một `OrderStatus` cho mức trả hàng là **trục thứ 5 trá hình** (cấm — `SAL-003` BR-003); câu hỏi
    /// *"đơn nào trả một phần?"* **phải** đọc `ReturnStatus`, không đọc `Status`.
    /// </summary>
    PartiallyReturned = 6
}

/// <summary>Bốn trục trạng thái của đơn (`SAL-003` BR-003). Cấm thêm trục thứ 5 khi chưa sửa tài liệu.</summary>
public enum OrderAxis
{
    /// <summary>Trục 1 — thực hiện.</summary>
    Fulfillment = 1,

    /// <summary>Trục 2 — tài chính.</summary>
    Payment = 2,

    /// <summary>Trục 3 — đối soát.</summary>
    Reconciliation = 3,

    /// <summary>Trục 4 — kết thúc (trả/huỷ).</summary>
    Closure = 4
}

/// <summary>
/// **AI** đổi trạng thái (`SAL-003` BR-006). Ghi vào lịch sử dưới dạng chuỗi để đọc log/SQL dễ.
/// </summary>
public enum StatusActorType
{
    /// <summary>Khách hàng (tự huỷ/yêu cầu trả).</summary>
    Customer = 1,

    /// <summary>Nhân viên vận hành (qua UI/API có xác thực).</summary>
    Staff = 2,

    /// <summary>Job hệ thống (quá hạn, backfill, quét).</summary>
    SystemJob = 3,

    /// <summary>Webhook sàn TMĐT.</summary>
    MarketplaceWebhook = 4,

    /// <summary>IPN/`querydr` của cổng thanh toán.</summary>
    PaymentGateway = 5,

    /// <summary>Job đối soát.</summary>
    Reconciliation = 6
}

/// <summary>Trạng thái đồng bộ của một listing trên sàn.</summary>
public enum ChannelListingStatus
{
    Draft = 0,
    PendingSync = 1,
    Synced = 2,
    Error = 3,
    Delisted = 4
}

/// <summary>
/// **Loại sử dụng của thuộc tính** (`CAT-002` **D4**/BR-008) — tách **2 loại tường minh**, **không** dùng cờ `bool`.
///
/// ⚠️ VÌ SAO TÁCH HẲN 2 GIÁ TRỊ THAY VÌ MỘT CỜ `IsVariant`: hai loại có **luật khác hẳn nhau** —
/// `Variant` **không được xoá** khi đã có biến thể dùng nó (`Catalog:0023`), còn `Specification` xoá được tự do.
/// Nếu chỉ có một cờ `bool` thì guard rất dễ viết **sai nhánh** và **không có lỗi biên dịch** nào chỉ ra.
///
/// 📌 Đặt ở `Domain.Shared` (không phải trong entity) vì **DTO ở `Application.Contracts` cũng cần** —
/// tầng đó **không** được tham chiếu `Domain` (layering). Đây đúng quy ước: mọi enum nằm ở `Enums.cs`.
/// </summary>
public enum CatalogAttributeUsage
{
    /// <summary>Chỉ **mô tả** (chất liệu, bảo hành…). **Không** tham gia sinh tổ hợp.</summary>
    Specification = 0,

    /// <summary>Sinh **biến thể** (màu, size…). Là trục của tích Descartes (BR-009).</summary>
    Variant = 1
}

/// <summary>
/// Vòng đời HIỂN THỊ của sản phẩm (`CAT-001` **D5** — state machine ở tầng DOMAIN).
///
/// 🚨 VÌ SAO PHẢI CÓ ENUM + METHOD CÓ GUARD (thay vì `bool IsPublished` có `set` công khai):
/// `IsPublished` là `bool` công khai ⇒ **không có chỗ nào** để chặn chuyển trạng thái sai
/// (ví dụ publish một sản phẩm **không có biến thể** nào — bán không được, mà vẫn hiện trên storefront).
/// Quy ước dự án: **máy trạng thái PHẢI NÉM LỖI**, không im lặng bỏ qua (bài học `SetPaidStatus()` của eShop).
/// </summary>
public enum ProductStatus
{
    /// <summary>Nháp — chưa từng hiện ra ngoài.</summary>
    Draft = 0,

    /// <summary>Đang bán (hiện trên storefront/kênh).</summary>
    Published = 1,

    /// <summary>Đã ngừng bán nhưng **giữ nguyên** dữ liệu (có thể publish lại).</summary>
    Unpublished = 2,

    /// <summary>Lưu trữ — kết thúc vòng đời; **không** publish lại.</summary>
    Archived = 3
}

// ══════════════════════════════════════════════════════════════════════════════════════════════════
// 🆕 WP-03 (02/11/2026) — MÔ HÌNH HÀNG HÓA THEO NHANH (`03 §3.1`, D-07)
//
// 🚨 VÌ SAO ĐÁNH SỐ TƯỜNG MINH (1..10, 1..2, 1..11) THAY VÌ ĐỂ MẶC ĐỊNH 0..n:
// đây là **giá trị của Nhanh** (`nhanh-pos-v3-api-inventory.md` §2.2) và sẽ được dùng để **đối chiếu dữ liệu
// hai chiều** (`product.type`, `imei.status`). Nếu ta đánh số lại từ 0 thì mọi lần so sánh/import phải cộng trừ
// bằng tay — và **sai một đơn vị là sai toàn bộ bản đồ**, không có lỗi biên dịch nào.
// 🔒 Append-only (D-17): **chỉ thêm ở cuối**, không đổi số đã có.
// 📌 Không có giá trị `4` cho IMEI là **đúng dữ liệu Nhanh đo được** — cố ý **giữ khoảng trống**, không lấp
// bằng một giá trị tự chế (lấp vào là tự tạo nghĩa mới cho dữ liệu của hệ thống khác).
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// **LOẠI SẢN PHẨM** (`03 §3.1`, đúng số của Nhanh — `nhanh-pos-v3-api-inventory.md` §2.2 `type`).
///
/// 🚨 Loại quyết định **form nhập liệu và luật tồn kho**: IMEI quản theo serial, cân đo bán số thập phân,
/// combo/gói là **cấu thành** (không tự có tồn), dịch vụ **không có tồn**. Vì vậy nó là **enum có số cố định**,
/// không phải chuỗi tự do — chuỗi tự do sẽ khiến hai nơi hiểu "combo" theo hai kiểu.
/// </summary>
public enum ProductType
{
    /// <summary>1 — Thường (một ĐVT, không serial, không lô).</summary>
    Standard = 1,

    /// <summary>2 — Voucher (phiếu quà tặng; bán như hàng nhưng không tồn kho vật lý).</summary>
    Voucher = 2,

    /// <summary>3 — Cân đo (bán theo khối lượng, số lượng thập phân).</summary>
    Weighed = 3,

    /// <summary>4 — Theo IMEI (mỗi serial là một đơn vị tồn kho riêng).</summary>
    Imei = 4,

    /// <summary>5 — Gói sản phẩm (bán một gói = nhiều thành phần).</summary>
    Package = 5,

    /// <summary>6 — Dịch vụ (không có tồn kho).</summary>
    Service = 6,

    /// <summary>7 — Dụng cụ (thiết bị dùng nội bộ, không bán lẻ).</summary>
    Tool = 7,

    /// <summary>8 — Bán theo lô (quản tồn và hạn dùng theo lô).</summary>
    BatchTracked = 8,

    /// <summary>9 — Combo (bán một combo = nhiều sản phẩm thành phần).</summary>
    Combo = 9,

    /// <summary>10 — Nhiều đơn vị tính (lon/thùng/kiện… với hệ số quy đổi).</summary>
    MultiUnit = 10
}

/// <summary>
/// **CÁCH HIỂU GIÁ ĐỐI VỚI VAT** (`03 §3.1`; Nhanh gọi là `priceVatMode`).
///
/// 🚨 VÌ SAO PHẢI LƯU THÀNH ENUM (không suy ra từ `VatPercent`): cùng một con số giá `100.000` là
/// **đã gồm** hay **chưa gồm** VAT cho ra **hai số tiền khác nhau** khi in hoá đơn. Không lưu mode thì
/// kế toán phải hỏi lại người nhập — và **không có lỗi nào** để biết mình đoán sai.
/// </summary>
public enum VatMode
{
    /// <summary>1 — Giá **đã gồm** VAT ⇒ giữ nguyên khi tính tiền.</summary>
    Included = 1,

    /// <summary>2 — Giá **chưa gồm** VAT ⇒ giá bán = giá + VAT (mặc định của Nhanh).</summary>
    Excluded = 2
}

/// <summary>
/// **TRẠNG THÁI MỘT SERIAL IMEI** (`03 §3.1`; số đúng theo Nhanh — `nhanh-pos-v3-api-inventory.md` §2.2).
///
/// ⚠️ **KHÔNG có giá trị `4`** — đó là **khoảng trống trong dữ liệu Nhanh**, không phải thiếu sót của ta.
/// 🔒 Serial chỉ đi qua các trạng thái này qua thao tác kho/bán/bảo hành; **cấm** sửa trực tiếp từ UI.
/// </summary>
public enum ProductImeiStatus
{
    /// <summary>1 — Mới (chưa bán, vừa nhập).</summary>
    New = 1,

    /// <summary>2 — Đã bán.</summary>
    Sold = 2,

    /// <summary>3 — Đang vận chuyển (đã xuất cho đơn vị giao hàng).</summary>
    Shipping = 3,

    /// <summary>5 — Lỗi (hỏng, chờ xử lý).</summary>
    Faulty = 5,

    /// <summary>6 — Đã trả nhà cung cấp.</summary>
    ReturnedToSupplier = 6,

    /// <summary>7 — Tồn trong cửa hàng.</summary>
    InStore = 7,

    /// <summary>8 — Đang chuyển kho.</summary>
    Transferring = 8,

    /// <summary>9 — Đang bảo hành.</summary>
    InWarranty = 9,

    /// <summary>10 — Đã trả bảo hành (nhận lại từ hãng/bảo hành).</summary>
    ReturnedFromWarranty = 10,

    /// <summary>11 — Hàng demo (trưng bày, không bán như hàng mới).</summary>
    Demo = 11
}

// ══════════════════════════════════════════════════════════════════════════════════════════════════
// 🆕 `REC-001` — ĐỐI SOÁT THANH TOÁN (`PaymentReconciliation*`, 27/10/2026)
// 📌 Đặt ở `Domain.Shared` vì entity (module Reconciliation), DTO (Contracts) và worker đều dùng;
//    Contracts **không** được tham chiếu Domain.
// ══════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Vòng đời **một lượt** đối soát thanh toán (`REC-001` §3.1).</summary>
public enum PaymentReconciliationRunStatus
{
    DangChay = 0,
    HoanTat = 1,
    HoanTatCoLech = 2,
    ThatBai = 3
}

/// <summary>Ai/kênh kích hoạt lượt chạy (`REC-001` §5.2.1).</summary>
public enum PaymentReconciliationTrigger
{
    Schedule = 0,
    Manual = 1
}

/// <summary>Vòng đời **một dòng** (một giao dịch) trong lượt (`REC-001` §3).</summary>
public enum PaymentReconciliationLineStatus
{
    ChoDoiSoat = 0,
    DangTruyVan = 1,
    Khop = 2,
    Lech = 3,
    LoiNhaCungCap = 4,
    ThieuDuLieu = 5,
    BoQua = 6
}

/// <summary>Danh sách **ĐÓNG** loại sai lệch (`REC-001` §5.4 / BR-12).</summary>
public enum PaymentDiscrepancyKind
{
    MoneyReceivedButNotRecorded = 0,
    RecordedButMoneyNotReceived = 1,
    AmountMismatch = 2,
    ProviderFlagged = 3,
    ProviderTransactionNotFound = 4,
    ProviderTxnIdMismatch = 5,
    RefundNotRecorded = 6,
    ProviderUnreachable = 7,
    MissingProviderCreateDate = 8,
    StalePendingTransaction = 9
}

/// <summary>Mức nghiêm trọng của sai lệch (`REC-001` §5.4).</summary>
public enum PaymentDiscrepancySeverity
{
    Cao = 0,
    TrungBinh = 1,
    Thap = 2
}

/// <summary>Vòng đời xử lý sai lệch (`REC-001` §3.2).</summary>
public enum PaymentDiscrepancyStatus
{
    Moi = 0,
    DaXacNhan = 1,
    DaXuLy = 2,
    BoQua = 3
}