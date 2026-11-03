namespace eCommerce.Orders;

/// <summary>
/// **BẢNG CHUYỂN TRẠNG THÁI** của đơn hàng (`SAL-003` §3) — **một định nghĩa duy nhất**, dùng bởi entity
/// và bởi test vét cạn (`TC-06`).
///
/// 🚨 **VÌ SAO LÀ BẢNG DỮ LIỆU, KHÔNG PHẢI CHUỖI `if`:** guard rải rác trong app service là chỗ sinh ra
/// *"đường vòng không guard"* — đúng lớp lỗi đã có ở `Order.SetStatus`. Khi bảng là **dữ liệu**, có thể
/// vét cạn **mọi cặp** (trục × from × to) và khẳng định: hợp lệ ⇔ có cạnh; còn lại **NÉM**.
///
/// 🆕 **`WP-05`** (`N6`, 03/11/2026) — bảng được **MỞ RỘNG cho 18 trạng thái Nhanh**
/// (`docs/nhanh/nhanh-pos-module-map.md` §6.2 + bảng *"thao tác × trạng thái"* §6.1):
/// <list type="bullet">
///   <item>**Giữ nguyên 100% cạnh cũ** (test `OrderStateMachineTests` không đổi hành vi);</item>
///   <item>thêm các cạnh của đường xác nhận Nhanh (`Chờ xác nhận → Đang xác nhận → Đã xác nhận → Đóng gói →
///     Đã đóng gói → Chờ thu gom`), đường hoàn (`Đang chuyển → Đang hoàn → Xác nhận hoàn → Đã hoàn`) và
///     `Hết hàng`/`Đổi kho hàng`;</item>
///   <item>**3 cạnh HUỶ** (`→ Khách huỷ / Hệ thống huỷ / HVC huỷ`) chỉ đi từ trạng thái **hàng CHƯA rời kho**
///     — xem <see cref="CoTheHuy"/>.</item>
/// </list>
///
/// ⚠️ **Bảng trục TÀI CHÍNH giữ LUẬT HIỆN CÓ của dự án** (đã kiểm chứng ở `OrderTrangThaiThanhToanTests`):
/// trước khi có tiền (`Unpaid`/`Pending`/`Failed`) đi được **mọi hướng** — gồm `Failed → Paid`
/// (lưới an toàn khi IPN mất, `querydr` xác nhận tiền đã vào). Sau khi có tiền chỉ được **tiến**.
/// 📌 Đây là **lệch có chủ ý** so với bảng chặt hơn trong `SAL-003` §3 (bảng đó bắt buộc
/// `Unpaid → PendingPayment` trước); ghi ở `sal-003` §16 để không ai sửa một bên mà quên bên kia.
/// </summary>
public static class OrderStateMachine
{
    /// <summary>
    /// 🆕 **`WP-05`** — **TÁC ĐỘNG KHO** khi đơn **vào** một trạng thái thực hiện (`D-39`).
    ///
    /// 📌 **VÌ SAO LÀ HÀM THUẦN Ở ĐÂY (không rải trong app service):** cùng lý do với bảng cạnh — *"bước này
    /// có ghi sổ cái hay không"* là **luật nghiệp vụ**, phải test được **không cần DB**. App service chỉ
    /// **thi hành** kết quả (`ChotGiuCho` / `NhapHangHoan`) qua gateway của module `Inventory`.
    /// </summary>
    public enum TacDongKho
    {
        /// <summary>Không có bút toán nào (đổi trạng thái thuần).</summary>
        Khong = 0,

        /// <summary>**Chốt giữ chỗ ⇒ xuất kho**: `Sale` (`OnHand ↓`) + `ReservationRelease` (`Reserved ↓`).</summary>
        ChotGiuCho = 1,

        /// <summary>**Nhập lại kho** hàng hoàn: chứng từ `CustomerReturn` ⇒ `OnHand ↑`.</summary>
        NhapHangHoan = 2
    }

    /// <summary>
    /// **3 trạng thái HUỶ của trục thực hiện** (Nhanh `63/64/58`). 📌 Chúng **không** phải trục thứ 5:
    /// *"đơn đã kết thúc"* vẫn là `ReturnStatus.Cancelled`; đây chỉ ghi **AI huỷ** (báo cáo "lý do huỷ" của Nhanh).
    /// </summary>
    public static readonly IReadOnlySet<FulfillmentStatus> TrangThaiHuy = new HashSet<FulfillmentStatus>
    {
        FulfillmentStatus.CustomerCancelled,
        FulfillmentStatus.SystemCancelled,
        FulfillmentStatus.CarrierCancelled
    };

    /// <summary>
    /// Trạng thái **còn huỷ được**: hàng **CHƯA rời kho**. Trong mô hình này hàng rời kho ở bước
    /// `Confirmed` (chốt giữ chỗ ⇒ bút toán `Sale`) ⇒ từ `Confirmed` trở đi **phải TRẢ HÀNG**.
    /// 📌 `Packing`/`Packed` cũng **không** huỷ được vì chúng nằm **sau** `Confirmed` (hàng đã ra kho).
    /// </summary>
    public static readonly IReadOnlySet<FulfillmentStatus> CoTheHuyTu = new HashSet<FulfillmentStatus>
    {
        FulfillmentStatus.Pending,
        FulfillmentStatus.PendingConfirm,
        FulfillmentStatus.Confirming,
        FulfillmentStatus.OutOfStock,
        FulfillmentStatus.WarehouseSwitched
    };

    /// <summary>
    /// Cạnh hợp lệ: `(trục, từ, đến)`. **Không có trong tập này ⇒ NÉM `eCommerce:Order:0001`.**
    /// ⚠️ **KHAI BÁO SAU** `TrangThaiHuy`/`CoTheHuyTu`: bộ khởi tạo static của C# chạy **theo thứ tự khai báo**
    /// ⇒ đặt `Canh` lên trước thì `TaoBangCanh()` đọc hai tập kia khi chúng **còn `null`** ⇒
    /// `TypeInitializationException` (đã dặp thật khi chạy test lượt đầu của `N6`).
    /// </summary>
    private static readonly HashSet<(OrderAxis Truc, int Tu, int Den)> Canh = TaoBangCanh();

    /// <summary>Trạng thái có nằm trong 3 trạng thái huỷ?</summary>
    public static bool LaTrangThaiHuy(FulfillmentStatus trangThai) => TrangThaiHuy.Contains(trangThai);

    /// <summary>Đơn ở trạng thái này có huỷ được không (hàng chưa rời kho)?</summary>
    public static bool CoTheHuy(FulfillmentStatus hienTai) => CoTheHuyTu.Contains(hienTai);

    /// <summary>
    /// 🆕 **`WP-05`** — **BƯỚC NÀY CÓ SINH BÚT TOÁN KHO KHÔNG** (`D-39`).
    ///
    /// | Vào trạng thái | Bút toán | Vì sao |
    /// |---|---|---|
    /// | `Confirmed` | **Chốt giữ chỗ** (`Sale` + `ReservationRelease`) | hàng **rời kho** ở bước xác nhận (đúng `ChotGiuChoService`, bất biến `Available` không đổi) |
    /// | `Returned` | **Nhập hàng hoàn** (`CustomerReturn`) | hàng **về kho thật** — sổ cái append-only ⇒ ghi bút toán **mới**, không sửa bút toán cũ |
    /// | khác | — | chỉ đổi trạng thái |
    ///
    /// ⚠️ **LỆCH CÓ CHỦ Ý so với [03 §4](../../docs/nhanh/03-kien-truc-dich.md):** bảng đó ghi *"Gửi HVC ⇒
    /// `ShippingOut`"* và *"Giao thành công ⇒ `Shipping −`"*. Trong mô hình **đã hiện thực** (`ApplyMovement`),
    /// `ShippingOut` **giảm `OnHand`** — nếu ghi thêm sau `Confirmed` thì `OnHand` **giảm hai lần** cho **một**
    /// món hàng (bán thứ không có, và không có lỗi nào). ⇒ **Không** ghi `ShippingOut`; chiều `Shipping` để
    /// `WP-06` (POS/`RetailBill`) dùng đúng nghĩa *"hàng đã rời kho, chưa thu tiền"*.
    /// 📌 Lý do đầy đủ + phương án đã loại: `decisions.md` **`D-39`**.
    /// </summary>
    public static TacDongKho TacDongKhoKhiVao(FulfillmentStatus den) => den switch
    {
        FulfillmentStatus.Confirmed => TacDongKho.ChotGiuCho,
        FulfillmentStatus.Returned => TacDongKho.NhapHangHoan,
        _ => TacDongKho.Khong
    };

    private static HashSet<(OrderAxis Truc, int Tu, int Den)> TaoBangCanh()
    {
        var canh = new HashSet<(OrderAxis, int, int)>
        {
            // ── TRỤC 1 — THỰC HIỆN (đường CŨ, giữ nguyên) ────────────────────────────────────────────
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Pending, (int)FulfillmentStatus.Confirmed),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Confirmed, (int)FulfillmentStatus.Packing),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Packing, (int)FulfillmentStatus.AwaitingPickup),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.AwaitingPickup, (int)FulfillmentStatus.OutForDelivery),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.OutForDelivery, (int)FulfillmentStatus.Delivered),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.OutForDelivery, (int)FulfillmentStatus.DeliveryFailed),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.DeliveryFailed, (int)FulfillmentStatus.OutForDelivery),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.DeliveryFailed, (int)FulfillmentStatus.Returning),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Returning, (int)FulfillmentStatus.Returned),

            // ── TRỤC 1 — THỰC HIỆN (Nhanh, `WP-05`) ─────────────────────────────────────────────────
            // Đường XÁC NHẬN: Mới → Chờ xác nhận / Đang xác nhận → Đã xác nhận.
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Pending, (int)FulfillmentStatus.PendingConfirm),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Pending, (int)FulfillmentStatus.Confirming),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.PendingConfirm, (int)FulfillmentStatus.Confirming),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.PendingConfirm, (int)FulfillmentStatus.Confirmed),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Confirming, (int)FulfillmentStatus.Confirmed),

            // ⚠️ **KHÔNG** có `Chờ xác nhận → Mới`: *"quay lại hàng đợi"* sẽ làm badge tab nhảy qua lại và
            // che mất sự thật *"đơn này ĐÃ được gọi khách"* — muốn bỏ qua thì đi tiếp `Đang xác nhận`.

            // HẾT HÀNG / ĐỔI KHO: hai trạng thái "tắc" của Nhanh — có hàng lại thì về `Đã xác nhận`.
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Pending, (int)FulfillmentStatus.OutOfStock),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.PendingConfirm, (int)FulfillmentStatus.OutOfStock),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Confirming, (int)FulfillmentStatus.OutOfStock),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Confirmed, (int)FulfillmentStatus.OutOfStock),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.OutOfStock, (int)FulfillmentStatus.Confirmed),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.OutOfStock, (int)FulfillmentStatus.WarehouseSwitched),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Pending, (int)FulfillmentStatus.WarehouseSwitched),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.PendingConfirm, (int)FulfillmentStatus.WarehouseSwitched),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Confirming, (int)FulfillmentStatus.WarehouseSwitched),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.WarehouseSwitched, (int)FulfillmentStatus.Confirming),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.WarehouseSwitched, (int)FulfillmentStatus.Confirmed),

            // ĐÓNG GÓI: `Đã xác nhận → Đang đóng gói → Đã đóng gói → Chờ thu gom`.
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Packing, (int)FulfillmentStatus.Packed),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Packed, (int)FulfillmentStatus.AwaitingPickup),
            // Cho phép vào thẳng `Đã đóng gói` từ `Đã xác nhận` (Nhanh: "Lưu và In" ⇒ tự sang Đang đóng gói,
            // nhưng nhập liệu cũ/nhập từ Excel có thể đã ở `Đã đóng gói`).
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Confirmed, (int)FulfillmentStatus.Packed),

            // HOÀN: `Đang chuyển/Thất bại → Đang hoàn → Xác nhận hoàn → Đã hoàn`.
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.OutForDelivery, (int)FulfillmentStatus.Returning),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.Returning, (int)FulfillmentStatus.ReturnConfirmed),
            (OrderAxis.Fulfillment, (int)FulfillmentStatus.ReturnConfirmed, (int)FulfillmentStatus.Returned),

            // ── TRỤC 2 — TÀI CHÍNH (luật hiện có: linh hoạt TRƯỚC khi có tiền, chỉ TIẾN sau khi có tiền) ─
            (OrderAxis.Payment, (int)PaymentStatus.Unpaid, (int)PaymentStatus.Pending),
            (OrderAxis.Payment, (int)PaymentStatus.Unpaid, (int)PaymentStatus.Paid),
            (OrderAxis.Payment, (int)PaymentStatus.Unpaid, (int)PaymentStatus.Failed),
            (OrderAxis.Payment, (int)PaymentStatus.Pending, (int)PaymentStatus.Unpaid),
            (OrderAxis.Payment, (int)PaymentStatus.Pending, (int)PaymentStatus.Paid),
            (OrderAxis.Payment, (int)PaymentStatus.Pending, (int)PaymentStatus.Failed),
            (OrderAxis.Payment, (int)PaymentStatus.Failed, (int)PaymentStatus.Unpaid),
            (OrderAxis.Payment, (int)PaymentStatus.Failed, (int)PaymentStatus.Pending),
            // 🔑 Lưới an toàn `querydr`: IPN mất ⇒ cổng xác nhận tiền đã vào ⇒ phải ghi được `Paid`.
            (OrderAxis.Payment, (int)PaymentStatus.Failed, (int)PaymentStatus.Paid),
            (OrderAxis.Payment, (int)PaymentStatus.Paid, (int)PaymentStatus.PartiallyRefunded),
            (OrderAxis.Payment, (int)PaymentStatus.Paid, (int)PaymentStatus.Refunded),
            (OrderAxis.Payment, (int)PaymentStatus.PartiallyRefunded, (int)PaymentStatus.Refunded),

            // ── TRỤC 3 — ĐỐI SOÁT ───────────────────────────────────────────────────────────────────
            (OrderAxis.Reconciliation, (int)ReconciliationStatus.AwaitingReconcile, (int)ReconciliationStatus.Matched),
            (OrderAxis.Reconciliation, (int)ReconciliationStatus.AwaitingReconcile, (int)ReconciliationStatus.Mismatched),
            (OrderAxis.Reconciliation, (int)ReconciliationStatus.Mismatched, (int)ReconciliationStatus.Resolved),
            (OrderAxis.Reconciliation, (int)ReconciliationStatus.Mismatched, (int)ReconciliationStatus.AwaitingReconcile),

            // ── TRỤC 4 — KẾT THÚC ───────────────────────────────────────────────────────────────────
            (OrderAxis.Closure, (int)ReturnStatus.Open, (int)ReturnStatus.ReturnRequested),
            (OrderAxis.Closure, (int)ReturnStatus.ReturnRequested, (int)ReturnStatus.ReturnApproved),
            (OrderAxis.Closure, (int)ReturnStatus.ReturnRequested, (int)ReturnStatus.ReturnRejected),
            (OrderAxis.Closure, (int)ReturnStatus.ReturnApproved, (int)ReturnStatus.Returned),
            // 🆕 `S14H` (BA chốt 29/10/2026) — nhận hàng hoàn **MỘT PHẦN** ⇒ trục kết thúc của ĐƠN là
            // `PartiallyReturned`, rồi `Returned` khi nhận nốt phần còn lại.
            // 🔒 **ĐÚNG 2 CẠNH, KHÔNG cạnh tắt:**
            //   • KHÔNG `Open`/`ReturnRequested → PartiallyReturned`: chưa duyệt trả thì chưa thể có hàng về
            //     (hàng về mà chưa duyệt = nhận sai quy trình, phải chặn chứ không hợp thức hoá);
            //   • KHÔNG `PartiallyReturned → Cancelled`: **đã nhận hàng** thì không huỷ (`HuyDon` cũng chặn —
            //     đã ra khỏi kho thì phải TRẢ HÀNG bằng chứng từ, cấm xoá bút toán);
            //   • KHÔNG cạnh tự thân (`PartiallyReturned → PartiallyReturned`): nhận thêm một phần nữa **không**
            //     đổi trạng thái đơn ⇒ máy trạng thái NÉM `0001` nếu ai gọi; chỗ gọi phải tự bỏ qua (`S14H`).
            (OrderAxis.Closure, (int)ReturnStatus.ReturnApproved, (int)ReturnStatus.PartiallyReturned),
            (OrderAxis.Closure, (int)ReturnStatus.PartiallyReturned, (int)ReturnStatus.Returned),
            // 🆕 `S14` phần D (28/10/2026) — **ĐÃ XOÁ cạnh tắt `Open → Returned`** (nợ `sal-003` §16 đã đóng).
            // 📌 Vì sao **phải** xoá: cạnh đó cho phép đánh dấu *"đã trả"* **không có phiếu trả nào** — không dòng
            // trả, không hàng về kho, không ai duyệt — mà đơn vẫn `Returned`. 🔒 Nay **mọi** trả hàng đi qua
            // `ReturnRequested → ReturnApproved → Returned`, kể cả đường cũ `TraHangAsync` (đã sửa cùng lượt).
            (OrderAxis.Closure, (int)ReturnStatus.Open, (int)ReturnStatus.Cancelled)
        };

        // ── HUỶ theo 18 trạng thái Nhanh (`WP-05`): 3 đích × trạng thái "hàng chưa rời kho" ────────────
        // 📌 Sinh bằng VÒNG LẶP từ `CoTheHuyTu` ⇒ thêm một trạng thái huỷ được chỉ sửa **một chỗ**; nếu
        // viết tay 15 cạnh thì lần sau thêm trạng thái sẽ quên đúng một cạnh (lỗi im lặng: UI hiện nút, API 0001).
        foreach (var tu in CoTheHuyTu)
        {
            foreach (var den in TrangThaiHuy)
            {
                canh.Add((OrderAxis.Fulfillment, (int)tu, (int)den));
            }
        }

        return canh;
    }

    /// <summary>Có cạnh hợp lệ từ <paramref name="tu"/> sang <paramref name="den"/> trên trục không?</summary>
    public static bool CoCanh(OrderAxis truc, int tu, int den) => Canh.Contains((truc, tu, den));

    /// <summary>
    /// Trạng thái **hấp thụ** — không có đường ra. Cố chuyển tiếp ⇒ `eCommerce:Order:0006`.
    /// 📌 `Cancelled`/`Returned` của trục kết thúc và `Refunded` của trục tài chính là hấp thụ:
    /// mở lại đơn đã huỷ/đã trả sẽ làm **sai dữ liệu kho và tiền** (đã bù trừ) — muốn bán lại phải tạo đơn mới.
    /// 📌 **`ReturnStatus.PartiallyReturned` KHÔNG hấp thụ** (`S14H`): nó là mốc *"đã về một phần"* và **còn
    /// đi tiếp** sang `Returned`. Danh sách hấp thụ của trục kết thúc vì vậy **không đổi**.
    /// 🆕 **`WP-05`**: 3 trạng thái **huỷ của trục thực hiện** cũng hấp thụ — *"Khách huỷ"* mà đi tiếp được
    /// sang `Đã xác nhận` là mâu thuẫn; chặn ở đây (ngoài guard `IsCancelled`) để dữ liệu nhập từ Nhanh cũng
    /// không đi tiếp được.
    /// </summary>
    public static bool LaHapThu(OrderAxis truc, int trangThai) => truc switch
    {
        OrderAxis.Fulfillment => trangThai is (int)FulfillmentStatus.Returned
            or (int)FulfillmentStatus.CustomerCancelled
            or (int)FulfillmentStatus.SystemCancelled
            or (int)FulfillmentStatus.CarrierCancelled,
        OrderAxis.Payment => trangThai == (int)PaymentStatus.Refunded,
        OrderAxis.Reconciliation => trangThai is (int)ReconciliationStatus.NotRequired
            or (int)ReconciliationStatus.Matched
            or (int)ReconciliationStatus.Resolved,
        OrderAxis.Closure => trangThai is (int)ReturnStatus.Returned
            or (int)ReturnStatus.ReturnRejected
            or (int)ReturnStatus.Cancelled,
        _ => false
    };

    /// <summary>Trục có nằm trong 4 trục hợp lệ? (`DerivedStatus` **không** phải trục — BR-008.)</summary>
    public static bool LaTrucHopLe(OrderAxis truc) => Enum.IsDefined(typeof(OrderAxis), truc);

    /// <summary>Tên trục dùng cho lịch sử/log (chuỗi, không phụ thuộc số enum).</summary>
    public static string TenTruc(OrderAxis truc) => truc.ToString();

    /// <summary>
    /// **SUY RA `Status` TỪ 4 TRỤC** (`SAL-003` D2 — **một** định nghĩa, dùng bởi entity **và** job quét NFR-011).
    ///
    /// 🔑 VÌ SAO LÀ HÀM THUẦN Ở ĐÂY (không để private trong `Order`): job quét hằng ngày phải **tính lại**
    /// giá trị đúng từ dữ liệu DB để so với cột `Status` đã lưu. Nếu job **chép lại** luật thì hai bản sẽ lệch
    /// khi ai đó sửa một bên — và lệch ở đây nghĩa là job **báo động giả** hoặc **bỏ sót** đúng ca cần bắt.
    /// 📌 Cùng nguyên tắc *"một định nghĩa, hai chỗ gọi"* đã dùng cho `LuatDoiSoatThanhToan`.
    ///
    /// 🆕 **`S14H` — `ReturnStatus.PartiallyReturned` KHÔNG ánh xạ sang `OrderStatus` mới.** Đơn trả một phần
    /// vẫn suy ra từ **trục thực hiện** (thường `Delivered` ⇒ `OrderStatus.Completed`): *"đơn vẫn `Completed`
    /// nhưng trục kết thúc đã `PartiallyReturned`"*. 📌 Vì sao **không** thêm giá trị `OrderStatus`: nó là
    /// **giá trị SUY RA** (D2) và chỉ có 1 enum cho 4 trục — thêm một giá trị cho *mức trả hàng* là **trục thứ
    /// 5 trá hình** (BR-003 cấm). Ai cần biết *"đã trả một phần"* phải đọc **`ReturnStatus`**, không đọc `Status`.
    /// 🆕 **`WP-05`** — 9 trạng thái Nhanh mới được ánh xạ vào **nhóm suy ra cũ** (không thêm giá trị
    /// `OrderStatus`, cùng lý do trên).
    /// </summary>
    public static OrderStatus TinhTrangThaiSuyRa(
        FulfillmentStatus thucHien,
        ReturnStatus ketThuc,
        bool daHuy)
    {
        if (daHuy || ketThuc == ReturnStatus.Cancelled)
        {
            return OrderStatus.Cancelled;
        }

        if (ketThuc == ReturnStatus.Returned || thucHien == FulfillmentStatus.Returned)
        {
            return OrderStatus.Returned;
        }

        return thucHien switch
        {
            FulfillmentStatus.Pending => OrderStatus.Pending,
            // 📌 Chưa chốt xác nhận ⇒ vẫn là "chờ xử lý" (Nhanh gộp Mới + Chờ/Đang xác nhận vào nhóm *"Chờ xác nhận"*).
            FulfillmentStatus.PendingConfirm
                or FulfillmentStatus.Confirming => OrderStatus.Pending,
            FulfillmentStatus.Confirmed => OrderStatus.Confirmed,
            FulfillmentStatus.Packing
                or FulfillmentStatus.Packed => OrderStatus.Processing,
            // 📌 Ba trạng thái "tắc" vẫn là đơn **đang trong tay shop** ⇒ `Processing`, không phải `Cancelled`
            // (khách chưa huỷ, đơn còn sống — `OutOfStock` có thể quay lại `Confirmed`).
            FulfillmentStatus.OutOfStock
                or FulfillmentStatus.WarehouseSwitched => OrderStatus.Processing,
            FulfillmentStatus.AwaitingPickup
                or FulfillmentStatus.OutForDelivery
                or FulfillmentStatus.DeliveryFailed
                or FulfillmentStatus.Returning
                or FulfillmentStatus.ReturnConfirmed => OrderStatus.Shipped,
            FulfillmentStatus.Delivered => OrderStatus.Completed,
            FulfillmentStatus.Returned => OrderStatus.Returned,
            // 🆕 `WP-05` — ai huỷ cũng là **đã huỷ** ở tầng hiển thị (guard thật vẫn là `IsCancelled`/
            // `ReturnStatus`; giá trị này chỉ để cột `Status` không nói dối khi dữ liệu nhập từ Nhanh
            // chỉ có trục thực hiện).
            FulfillmentStatus.CustomerCancelled
                or FulfillmentStatus.SystemCancelled
                or FulfillmentStatus.CarrierCancelled => OrderStatus.Cancelled,
            // ⚠️ Nhánh dự phòng: enum thêm giá trị mới mà quên cập nhật ⇒ **giữ nguyên** thay vì đoán bừa.
            _ => OrderStatus.Pending
        };
    }

    /// <summary>
    /// 🆕 **`WP-05`** — **MỘT NHÃN HIỂN THỊ** suy ra từ 4 trục (`docs/nhanh/03` §3.5).
    ///
    /// 🔑 **VÌ SAO UI CHỈ HIỆN MỘT NHÃN (DB vẫn giữ 4 trục):** nhân viên đọc **một** trạng thái như Nhanh;
    /// còn câu hỏi *"đơn nào đã giao mà CHƯA thu tiền (COD)?"* trả lời bằng **trục tài chính**, không bằng nhãn.
    /// Gộp hai thứ vào một cột là quay lại đúng lỗi mà `SAL-003` đã sửa (một enum ba nghĩa).
    ///
    /// 📌 Thứ tự ưu tiên **quan trọng**: huỷ → trả → hoàn → thất bại → thành công → đang chuyển → đóng gói →
    /// xác nhận → chờ xác nhận → hết hàng → mới.
    /// ⚠️ **LỆCH CÓ CHỦ Ý so với `03 §3.5`:** bảng đó gộp `ReturnConfirmed` vào *"Đang hoàn"*. Nhanh có **hai**
    /// trạng thái riêng (`Đang hoàn` 71 / `Xác nhận hoàn` 74) ⇒ ở đây trả **"Xác nhận hoàn"** để UI không
    /// **nuốt** một bước mà nhân viên phải bấm (`D-39`).
    /// </summary>
    public static string NhanHienThi(FulfillmentStatus thucHien, ReturnStatus ketThuc, bool daHuy)
    {
        if (daHuy || ketThuc == ReturnStatus.Cancelled || LaTrangThaiHuy(thucHien))
        {
            return "Đã huỷ";
        }

        if (ketThuc == ReturnStatus.Returned || thucHien == FulfillmentStatus.Returned)
        {
            return "Đã hoàn";
        }

        return thucHien switch
        {
            FulfillmentStatus.Returning => "Đang hoàn",
            FulfillmentStatus.ReturnConfirmed => "Xác nhận hoàn",
            FulfillmentStatus.DeliveryFailed => "Thất bại",
            // 📌 "Thành công" chỉ khi **chưa có** yêu cầu trả (`03 §3.5`): đơn đã giao mà khách đang đòi trả
            // thì nhãn phải phản ánh việc đang xử lý, không phải "xong".
            FulfillmentStatus.Delivered => ketThuc == ReturnStatus.Open ? "Thành công" : "Đang hoàn",
            FulfillmentStatus.AwaitingPickup
                or FulfillmentStatus.OutForDelivery => "Đang chuyển",
            FulfillmentStatus.Packed => "Đã đóng gói",
            FulfillmentStatus.Packing => "Đang đóng gói",
            FulfillmentStatus.Confirmed => "Đã xác nhận",
            FulfillmentStatus.PendingConfirm
                or FulfillmentStatus.Confirming => "Chờ/Đang xác nhận",
            FulfillmentStatus.OutOfStock => "Hết hàng",
            FulfillmentStatus.WarehouseSwitched => "Đổi kho hàng",
            _ => "Mới"
        };
    }
}
