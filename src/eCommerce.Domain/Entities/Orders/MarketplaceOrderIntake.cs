using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Orders;

/// <summary>
/// **Nhật ký nhận đơn từ sàn** (`SAL-005` D7) — bản ghi cho MỘT đơn của sàn mà ta đã nhận được.
///
/// 🚨 VÌ SAO BẢNG NÀY TỒN TẠI (đây là điều kiện để **lỗi #22 không quay lại**):
/// `ChannelShopSyncer` chỉ tiến **con trỏ đồng bộ** khi biết chắc có **nơi nhận** đơn. Nơi nhận đó phải
/// **ghi được đơn xuống đĩa** trước khi trả về thành công — nếu chỉ "nhận rồi xử lý trong bộ nhớ" thì
/// mất điện/exception giữa chừng là **mất đơn im lặng**, đúng loại lỗi #22.
///
/// 📌 VAI TRÒ:
///   • **chống trùng ở mức ĐƠN** (unique index `(TenantId, ChannelCode, ExternalOrderId)` — `NULLS NOT DISTINCT`),
///   • **giữ vết** đơn đã nhận kể cả khi việc tạo `Order` thất bại ⇒ luôn truy được "đã nhận gì, lúc nào",
///   • nơi neo **watermark của sàn** về sau (`SAL-005` D5).
///
/// ⚠️ **CHƯA làm ở lượt này (ghi rõ, không giả vờ):** xử lý **cập nhật** đơn (so watermark theo `update_time`);
/// hiện theo hướng **tạo-một-lần** — đơn đã có thì **không** sửa nữa. Xem ghi chú ở
/// <see cref="MarketplaceOrderIntakeHandler"/> và `docs/features/sal-005-webhook-don-tu-san.md` §16.
/// </summary>
public class MarketplaceOrderIntake : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã kênh: "shopee", "lazada"… (trùng <c>Channel.Code</c>).</summary>
    public string ChannelCode { get; protected set; } = default!;

    public string ExternalShopId { get; protected set; } = default!;

    /// <summary>ID đơn do sàn cấp (`order_sn`) — **khoá đối chiếu quan trọng nhất**.</summary>
    public string ExternalOrderId { get; protected set; } = default!;

    /// <summary>Đơn nội bộ đã tạo từ bản ghi này (<c>null</c> nếu chưa tạo được).</summary>
    public Guid? OrderId { get; protected set; }

    /// <summary>
    /// Mốc **đặt hàng trên sàn**. 📌 Giữ ở đây vì `Order` **không** có trường này
    /// (`SAL-005` **F8**) — mất nó thì báo cáo theo kênh và đối soát với sàn **thiếu mốc nghiệp vụ**.
    /// </summary>
    public DateTime? MarketplacePlacedAtUtc { get; protected set; }

    /// <summary>Băm nội dung payload đã nhận (canonical JSON) — phát hiện **cùng đơn nhưng nội dung khác**.</summary>
    public string PayloadHash { get; protected set; } = default!;

    public DateTime ReceivedAtUtc { get; protected set; }

    /// <summary>`CorrelationId` của lần nhận (`SAL-005` D11) — nối log ↔ ETO ↔ đơn.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Lý do bản ghi **không** dẫn tới đơn (nếu có) — để không bao giờ "bỏ qua im lặng".</summary>
    public string? SkipReason { get; protected set; }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // 🚨 `SAL-005` **§5.2.1** — **WATERMARK CỦA SÀN** (bổ sung 16/10/2026).
    //
    // 📌 Trước lượt này entity đã có `PayloadHash` nhưng **KHÔNG** có phiên bản ⇒ việc so *"bản này có mới
    // hơn không?"* **không thực hiện được**, và vì vậy đường pull phải chọn **tạo-một-lần** (đơn đã nhận thì
    // không sửa) — nghĩa là đơn có thể **CŨ** so với sàn mà không ai biết.
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⭐ **WATERMARK ĐÃ ÁP** — epoch **giây** UTC của `update_time` của **bản mới nhất đã áp** cho đơn này
    /// (`D5`). 🔒 **BẤT BIẾN: chỉ TĂNG, KHÔNG BAO GIỜ GIẢM** (xem <see cref="ApDungWatermark"/>).
    /// ⚠️ `null` = **chưa từng** áp bản nào có watermark (dữ liệu cũ trước lượt này, hoặc ETO cũ thiếu trường)
    /// — **khác hẳn** `0` (một mốc thật: 1970-01-01). Phân biệt được hai thứ này là điều kiện để
    /// **không** coi dữ liệu cũ là *"đã có watermark"* rồi **chặn** mọi cập nhật.
    /// </summary>
    public long? AppliedSourceVersion { get; protected set; }

    /// <summary>Mốc cập nhật của sàn (người đọc hiểu được) của bản **đã áp** — song song watermark.</summary>
    public DateTime? AppliedSourceUpdatedAtUtc { get; protected set; }

    /// <summary>`PayloadHash` của bản **đã áp** — cơ sở phân biệt *"trùng"* với *"xung đột"* khi `v` bằng nhau.
    /// 📌 Cố ý **tách khỏi** <see cref="PayloadHash"/> (bản **đến gần nhất**, kể cả bản bị bỏ qua): giữ chung
    /// một cột thì một bản **đến muộn** sẽ **xoá mất** hash của bản đang có hiệu lực ⇒ nhánh C/D sau đó
    /// so với **sai** mốc.</summary>
    public string? AppliedPayloadHash { get; protected set; }

    /// <summary>🚩 **NGHI NGỜ THIẾU BẢN TRUNG GIAN** (`BR-009`): bản áp có `v` nhảy **vượt** ngưỡng so với
    /// watermark trước ⇒ **vẫn áp dụng** (bản mới hơn là đúng) nhưng đánh cờ để **job đối soát** quét lại.
    /// 📌 Không có cờ này thì một bản bị mất ở **giữa** hai nhịp pull sẽ **không bao giờ** được phát hiện.</summary>
    public bool GapSuspected { get; protected set; }

    /// <summary>Mốc **đối soát xong** (Ops xác nhận) — `null` = chưa đối soát.</summary>
    public DateTime? LastReconciledAtUtc { get; protected set; }

    protected MarketplaceOrderIntake() { }

    public MarketplaceOrderIntake(
        Guid id,
        string channelCode,
        string externalShopId,
        string externalOrderId,
        string payloadHash,
        DateTime receivedAtUtc,
        DateTime? marketplacePlacedAtUtc,
        Guid? tenantId = null)
        : base(id)
    {
        ChannelCode = channelCode;
        ExternalShopId = externalShopId;
        ExternalOrderId = externalOrderId;
        PayloadHash = payloadHash;
        ReceivedAtUtc = receivedAtUtc;
        MarketplacePlacedAtUtc = marketplacePlacedAtUtc;
        TenantId = tenantId;
    }

    /// <summary>Gắn đơn nội bộ vừa tạo vào bản ghi nhận.</summary>
    public void GanOrder(Guid orderId)
    {
        OrderId = orderId;
        SkipReason = null;
    }

    /// <summary>
    /// Đánh dấu **cố ý không tạo đơn** — 🚨 **bắt buộc kèm lý do**: nhánh bỏ qua mà không có lý do là
    /// đúng dạng "im lặng" mà dự án đã trả giá nhiều lần.
    /// </summary>
    public void DanhDauBoQua(string lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new ArgumentException("Lý do bỏ qua không được rỗng (cấm bỏ qua im lặng).", nameof(lyDo));
        }

        SkipReason = lyDo.Length > 500 ? lyDo[..500] : lyDo;
    }

    /// <summary>
    /// 🚨 **ÁP WATERMARK MỚI** — `SAL-005` **BR-SAL-005-008**: *"cập nhật watermark **phải** bằng
    /// `UPDATE … WHERE AppliedSourceVersion < @v_in` **và** kiểm tra số dòng; `0` dòng ⇒ **NÉM**
    /// `eCommerce:Sales:0003`"*.
    ///
    /// 🔒 **BẤT BIẾN TRUNG TÂM CỦA CẢ FEATURE: watermark CHỈ TĂNG, KHÔNG BAO GIỜ GIẢM.**
    /// Hàm này **NÉM** nếu `phienBanMoi <= AppliedSourceVersion` — tức nó **từ chối** mọi đường ghi
    /// làm giảm watermark, kể cả khi người gọi quên kiểm (đó chính là mục đích: hàng rào **ở tầng domain**,
    /// không chỉ ở handler — vì handler là chỗ dễ sửa nhất và dễ sửa sai nhất).
    ///
    /// **VÌ SAO PHẢI KIỂM Ở ĐÂY, KHÔNG CHỈ Ở HANDLER:** nếu chỉ handler kiểm thì một đường ghi mới
    /// (job đối soát, script sửa dữ liệu, consumer thứ hai) có thể **hạ** watermark ⇒ bản **cũ** trở thành
    /// *"mới hơn"* ở lần nhận sau ⇒ **ghi đè trạng thái mới bằng trạng thái cũ** — và **không có lỗi nào**.
    /// 📌 Đây là cùng khuôn với `Product.DatSlug` / `PaymentTransaction.RegisterRefund`: **bất biến ở entity**.
    ///
    /// ⚠️ Không tự `SaveChanges` — việc gọi `UpdateAsync` thuộc app service/handler (để giữ **một** UoW).
    /// </summary>
    /// <param name="phienBanMoi">`AggregateVersion` (epoch giây UTC của `update_time` sàn) của bản mới.</param>
    /// <param name="hashMoi">`PayloadHash` của bản mới — lưu làm mốc so cho nhánh C/D sau này.</param>
    /// <param name="mocCapNhatCuaSan">Mốc người-đọc-hiểu-được của bản mới (song song watermark).</param>
    /// <param name="nghiNgoThieuBanTrungGian">Cờ từ luật watermark (`BR-009`). ⚠️ Chỉ **BẬT**, không bao giờ
    /// **TẮT** bằng hàm này: một lần phát hiện thiếu bản là dấu vết **vĩnh viễn** cho tới khi Ops đối soát
    /// (<see cref="DanhDauDaDoiSoat"/>) — nếu tự tắt được thì cờ mất tác dụng đúng lúc cần nó nhất.</param>
    /// <exception cref="Volo.Abp.BusinessException">
    /// `eCommerce:Sales:0003` khi `phienBanMoi` **không lớn hơn** watermark hiện tại.
    /// </exception>
    public void ApDungWatermark(
        long phienBanMoi,
        string? hashMoi,
        DateTime? mocCapNhatCuaSan,
        bool nghiNgoThieuBanTrungGian = false)
    {
        if (AppliedSourceVersion is { } hienTai && phienBanMoi <= hienTai)
        {
            // 🚨 Đây là ca `E4` của `SAL-005`: "UPDATE ảnh hưởng 0 dòng". Ở tầng DB nó là mệnh đề `WHERE`;
            // ở tầng domain nó là **NÉM**. Cấm coi là thành công — nếu nuốt lỗi thì luồng **thua** vẫn
            // ghi tiếp dữ liệu ⇒ hai luồng cùng ghi ⇒ trạng thái cuối **phụ thuộc thứ tự đến** (D6 cấm).
            throw new Volo.Abp.BusinessException(eCommerceConsts.ErrorCodes.SalesWatermarkKhongTien)
                .WithData("channelCode", ChannelCode)
                .WithData("externalOrderId", ExternalOrderId)
                .WithData("phienBanHienTai", hienTai)
                .WithData("phienBanMoi", phienBanMoi)
                .WithData("lyDo", "Watermark CHỈ TĂNG. Bản mới không lớn hơn bản đã áp ⇒ luồng thua, KHÔNG ghi gì thêm.");
        }

        AppliedSourceVersion = phienBanMoi;
        AppliedPayloadHash = hashMoi;
        AppliedSourceUpdatedAtUtc = mocCapNhatCuaSan;
        GapSuspected = GapSuspected || nghiNgoThieuBanTrungGian;
    }

    /// <summary>Ops xác nhận **đã đối soát** xong ⇒ hạ cờ <see cref="GapSuspected"/>.
    /// 🚨 **Bắt buộc lý do** (audit — cùng lý do như `Customer.Chan`): một lần hạ cờ không có lý do thì
    /// 6 tháng sau không ai biết **vì sao** đơn này từng bị nghi ngờ thiếu bản.</summary>
    public void DanhDauDaDoiSoat(DateTime thoiDiemUtc, string lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new ArgumentException("Đối soát xong BẮT BUỘC có lý do (audit).", nameof(lyDo));
        }

        LastReconciledAtUtc = thoiDiemUtc;
        GapSuspected = false;

        var ghiChu = $"[đối soát {thoiDiemUtc:yyyy-MM-ddTHH:mm:ssZ}] {lyDo.Trim()}";
        SkipReason = ghiChu.Length > 500 ? ghiChu[..500] : ghiChu;
    }
}
