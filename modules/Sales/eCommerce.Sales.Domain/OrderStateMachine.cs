namespace eCommerce.Orders;

/// <summary>
/// **BẢNG CHUYỂN TRẠNG THÁI** của đơn hàng (`SAL-003` §3) — **một định nghĩa duy nhất**, dùng bởi entity
/// và bởi test vét cạn (`TC-06`).
///
/// 🚨 **VÌ SAO LÀ BẢNG DỮ LIỆU, KHÔNG PHẢI CHUỖI `if`:** guard rải rác trong app service là chỗ sinh ra
/// *"đường vòng không guard"* — đúng lớp lỗi đã có ở `Order.SetStatus`. Khi bảng là **dữ liệu**, có thể
/// vét cạn **mọi cặp** (trục × from × to) và khẳng định: hợp lệ ⇔ có cạnh; còn lại **NÉM**.
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
    /// Cạnh hợp lệ: `(trục, từ, đến)`. **Không có trong tập này ⇒ NÉM `eCommerce:Order:0001`.**
    /// </summary>
    private static readonly HashSet<(OrderAxis Truc, int Tu, int Den)> Canh = new()
    {
        // ── TRỤC 1 — THỰC HIỆN ──────────────────────────────────────────────────────────────────
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.Pending, (int)FulfillmentStatus.Confirmed),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.Confirmed, (int)FulfillmentStatus.Packing),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.Packing, (int)FulfillmentStatus.AwaitingPickup),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.AwaitingPickup, (int)FulfillmentStatus.OutForDelivery),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.OutForDelivery, (int)FulfillmentStatus.Delivered),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.OutForDelivery, (int)FulfillmentStatus.DeliveryFailed),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.DeliveryFailed, (int)FulfillmentStatus.OutForDelivery),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.DeliveryFailed, (int)FulfillmentStatus.Returning),
        (OrderAxis.Fulfillment, (int)FulfillmentStatus.Returning, (int)FulfillmentStatus.Returned),

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
        // 🔑 GĐ1: nhân viên ghi nhận **trả toàn bộ** một đơn đã xác nhận (chưa có phiếu RMA của `SAL-006`).
        // 📌 Khi `SAL-006` vào, đường này phải đi qua `ReturnRequested → ReturnApproved → Returned`;
        //    ghi ở `sal-003` §16 là **nợ có chủ ý**, KHÔNG phải cạnh lâu dài.
        (OrderAxis.Closure, (int)ReturnStatus.Open, (int)ReturnStatus.Returned),
        (OrderAxis.Closure, (int)ReturnStatus.Open, (int)ReturnStatus.Cancelled)
    };

    /// <summary>Có cạnh hợp lệ từ <paramref name="tu"/> sang <paramref name="den"/> trên trục không?</summary>
    public static bool CoCanh(OrderAxis truc, int tu, int den) => Canh.Contains((truc, tu, den));

    /// <summary>
    /// Trạng thái **hấp thụ** — không có đường ra. Cố chuyển tiếp ⇒ `eCommerce:Order:0006`.
    /// 📌 `Cancelled`/`Returned` của trục kết thúc và `Refunded` của trục tài chính là hấp thụ:
    /// mở lại đơn đã huỷ/đã trả sẽ làm **sai dữ liệu kho và tiền** (đã bù trừ) — muốn bán lại phải tạo đơn mới.
    /// </summary>
    public static bool LaHapThu(OrderAxis truc, int trangThai) => truc switch
    {
        OrderAxis.Fulfillment => trangThai == (int)FulfillmentStatus.Returned,
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
            FulfillmentStatus.Confirmed => OrderStatus.Confirmed,
            FulfillmentStatus.Packing => OrderStatus.Processing,
            FulfillmentStatus.AwaitingPickup
                or FulfillmentStatus.OutForDelivery
                or FulfillmentStatus.DeliveryFailed
                or FulfillmentStatus.Returning => OrderStatus.Shipped,
            FulfillmentStatus.Delivered => OrderStatus.Completed,
            FulfillmentStatus.Returned => OrderStatus.Returned,
            // ⚠️ Nhánh dự phòng: enum thêm giá trị mới mà quên cập nhật ⇒ **giữ nguyên** thay vì đoán bừa.
            _ => OrderStatus.Pending
        };
    }
}
