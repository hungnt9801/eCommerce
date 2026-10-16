using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Messaging.DeadLetter;

/// <summary>Vì sao một sự kiện bị đẩy vào **hàng đợi thư chết** (DLQ).</summary>
public enum LyDoVaoDlq
{
    /// <summary>🚨 **XUNG ĐỘT PAYLOAD** — cùng khoá nghiệp vụ nhưng **nội dung khác** bản đã nhận.
    /// 📌 Hệ thống **KHÔNG đoán** bản nào đúng ⇒ đẩy cho **người** quyết định.</summary>
    XungDotPayload = 0,

    /// <summary>🚨 **HẠ WATERMARK** — phiên bản gửi tới **cũ hơn** bản đã áp dụng.
    /// 📌 Đây là **dấu hiệu sàn gửi sai thứ tự** (hoặc có hai nguồn tranh nhau) ⇒ **không** ghi đè,
    /// nhưng **phải** để lại bằng chứng.</summary>
    HaWatermark = 1,

    /// <summary>**LỖI KHI XỬ LÝ** — handler ném lỗi (dữ liệu sai, lỗi hạ tầng, timeout…).
    /// 📌 Khác hai ca trên: đây là **lỗi**, không phải **quyết định nghiệp vụ**.</summary>
    LoiXuLy = 2,

    /// <summary>**BỎ QUA CÓ LÝ DO KHÁC** — dự phòng cho luật mới; ⚠️ phải kèm lý do dạng chữ.</summary>
    Khac = 3
}

/// <summary>
/// 🚨 **HÀNG ĐỢI THƯ CHẾT** (`SX-Messaging-DeadLetters`) — `XC-002`.
///
/// **VÌ SAO BẢNG NÀY TỒN TẠI — và vì sao "chỉ ghi log" là KHÔNG ĐỦ:**
/// Khi một sự kiện **không xử lý được**, hệ thống có ba lựa chọn, và **hai** trong đó đều sai:
/// <list type="number">
///   <item>🚨 **Ném lỗi để nơi phát retry** ⇒ với luồng đơn từ sàn, nơi phát là `ChannelShopSyncer` **giữ con trỏ
///     đồng bộ** ⇒ sàn gửi lại **đúng** đơn đó ở nhịp sau ⇒ **lặp vô hạn** và **chặn CẢ KÊNH** (mọi đơn phía sau
///     không vào được). 📌 Đã **đo được thật** (15/10/2026: con trỏ kẹt ở `NULL` qua nhiều nhịp).
///     ⇒ Một đơn nghi vấn làm **sập luồng bán hàng**;</item>
///   <item>🚨 **Nuốt im lặng** (chỉ `LogError`) ⇒ sự kiện **biến mất**. 📌 Log bị **xoay vòng** (retention vài ngày),
///     **không** tra cứu được theo khoá nghiệp vụ, và **không ai** biết còn tồn bao nhiêu. ⇒ **Mất dữ liệu
///     nhưng có vẻ như đã xử lý**;</item>
///   <item>✅ **DLQ** (bảng này): sự kiện được **giữ nguyên vẹn** (payload gốc + lý do + khoá), luồng **đi tiếp**
///     (không chặn kênh), và người vận hành **tra cứu + xử lý + phát lại** được. 🔒 Đây là lựa chọn **duy nhất**
///     vừa **không mất dữ liệu** vừa **không chặn luồng**.</item>
/// </list>
///
/// 🔒 **BA BẤT BIẾN:**
/// <list type="number">
///   <item>**Payload gốc được giữ NGUYÊN VẸN** (`PayloadJson`) — không cắt, không chuẩn hoá lại. 📌 Nếu cắt thì
///     **không phát lại được**; nếu chuẩn hoá lại thì **mất** khác biệt chính là nguyên nhân vào DLQ;</item>
///   <item>**Khoá nghiệp vụ được ghi RIÊNG** (`KhoaNghiepVu`) — để tra *"đơn `SHOPEE/12345` có vấn đề gì"*
///     **không** phải quét cả bảng bằng `LIKE` trên JSON;</item>
///   <item>**Xử lý là XOÁ MỀM / đánh dấu**, KHÔNG xoá cứng — 📌 đây là **bằng chứng** về một sự kiện đã hỏng;
///     xoá đi thì lần sau gặp lại **không ai biết** nó từng xảy ra.</item>
/// </list>
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** bảng này **chỉ** ghi lại; **chưa** có **phát lại tự động** và **chưa** có job dọn
/// theo thời hạn. 📌 Nếu không dọn, DLQ **sẽ phình** — và một hàng đợi không bao giờ được đọc thì
/// **tệ như không có**. Ghi ở `sal-005` §18.
/// </summary>
public class DeadLetterMessage : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của tên loại sự kiện — khớp `HasMaxLength(512)`.</summary>
    public const int DoDaiTenSuKienToiDa = 512;

    /// <summary>Độ dài tối đa của khoá nghiệp vụ — khớp `HasMaxLength(256)`.</summary>
    public const int DoDaiKhoaNghiepVuToiDa = 256;

    /// <summary>Độ dài tối đa của lý do dạng chữ — khớp `HasMaxLength(2000)`.</summary>
    public const int DoDaiLyDoToiDa = 2000;

    public Guid? TenantId { get; protected set; }

    /// <summary>Tên đầy đủ của loại sự kiện (ví dụ `eCommerce.ChannelHub.Events.ChannelOrderPulledEto`).
    /// 📌 Ghi **tên kiểu** để khi phát lại biết **deserialize về đâu** — thiếu nó thì payload là **JSON vô chủ**.</summary>
    public string TenSuKien { get; protected set; } = default!;

    /// <summary>Khoá nghiệp vụ (ví dụ `shopee:order:12345`) — 🔒 trường **tra cứu chính**.</summary>
    public string KhoaNghiepVu { get; protected set; } = default!;

    /// <summary>🚨 **Payload GỐC, nguyên vẹn** — không cắt, không chuẩn hoá lại.</summary>
    public string PayloadJson { get; protected set; } = default!;

    /// <summary>Băm của payload (để so với bản đã nhận khi điều tra xung đột).</summary>
    public string PayloadHash { get; protected set; } = default!;

    public LyDoVaoDlq LyDo { get; protected set; }

    /// <summary>Mô tả lý do dạng chữ — 🔒 **BẮT BUỘC**: một mã enum không đủ để người trực biết **phải làm gì**.</summary>
    public string MoTaLyDo { get; protected set; } = default!;

    /// <summary>Thông báo lỗi (chỉ có khi `LyDo = LoiXuLy`) — dùng để nhóm các lỗi giống nhau.</summary>
    public string? Loi { get; protected set; }

    public DateTime VaoDlqLucUtc { get; protected set; }

    /// <summary>`Id` tương quan để lần theo **toàn bộ** đường đi của một request (`XC-002`).</summary>
    public string? CorrelationId { get; set; }

    // ── Trạng thái xử lý (bổ sung để DLQ **đọc được**, không chỉ **ghi được**) ──────────────────

    /// <summary>Đã xử lý chưa (phát lại thành công / đánh dấu bỏ qua có lý do).</summary>
    public bool DaXuLy { get; protected set; }

    public DateTime? XuLyLucUtc { get; protected set; }

    public string? NguoiXuLy { get; protected set; }

    /// <summary>Lý do xử lý — 🔒 **BẮT BUỘC** khi đánh dấu đã xử lý (chênh lệch dữ liệu là **tài sản**, cần audit).</summary>
    public string? LyDoXuLy { get; protected set; }

    protected DeadLetterMessage() { }

    public DeadLetterMessage(
        Guid id,
        string tenSuKien,
        string khoaNghiepVu,
        string payloadJson,
        string payloadHash,
        LyDoVaoDlq lyDo,
        string moTaLyDo,
        DateTime vaoDlqLucUtc,
        Guid? tenantId = null,
        string? loi = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(tenSuKien))
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(tenSuKien)).WithData("lyDo", "Thiếu tên loại sự kiện ⇒ không phát lại được.");
        }

        if (string.IsNullOrWhiteSpace(khoaNghiepVu))
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(khoaNghiepVu)).WithData("lyDo", "Thiếu khoá nghiệp vụ ⇒ không tra cứu được.");
        }

        if (payloadJson is null)
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(payloadJson));
        }

        if (string.IsNullOrWhiteSpace(moTaLyDo))
        {
            // 🔒 Một mã enum không đủ: người trực phải biết **phải làm gì**. 📌 Cùng bài học `REC-001` §17.5.
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(moTaLyDo))
                .WithData("lyDo", "DLQ BẮT BUỘC có mô tả lý do dạng chữ (mã enum không đủ để xử lý).");
        }

        var ten = tenSuKien.Trim();
        var khoa = khoaNghiepVu.Trim();
        var moTa = moTaLyDo.Trim();

        if (ten.Length > DoDaiTenSuKienToiDa)
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(tenSuKien)).WithData("toiDa", DoDaiTenSuKienToiDa);
        }

        if (khoa.Length > DoDaiKhoaNghiepVuToiDa)
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(khoaNghiepVu)).WithData("toiDa", DoDaiKhoaNghiepVuToiDa);
        }

        if (moTa.Length > DoDaiLyDoToiDa)
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(moTaLyDo)).WithData("toiDa", DoDaiLyDoToiDa);
        }

        TenSuKien = ten;
        KhoaNghiepVu = khoa;
        PayloadJson = payloadJson;
        PayloadHash = payloadHash;
        LyDo = lyDo;
        MoTaLyDo = moTa;
        Loi = string.IsNullOrWhiteSpace(loi) ? null : loi.Trim();
        VaoDlqLucUtc = vaoDlqLucUtc;
        TenantId = tenantId;
    }

    /// <summary>
    /// Đánh dấu **đã xử lý** (phát lại xong / bỏ qua có lý do).
    /// 🔒 **Bắt buộc** `nguoiXuLy` + `lyDoXuLy` — cùng lý do như `ReconRunMismatch.DanhDauDaXuLy`:
    /// đây là **bằng chứng** ai đã quyết định gì với một sự kiện hỏng.
    /// 🔑 **Thứ tự kiểm là một phần của đặc tả:** **NO-OP TRƯỚC** (idempotent), **rồi mới** kiểm tham số —
    /// 📌 nếu kiểm tham số trước thì một **retry** không mang lý do sẽ **NÉM** trên dòng **đã xong**
    /// ⇒ consumer **retry vô hạn** vì việc **không cần làm gì** (đúng bài học `REC-001` §17.3).
    /// </summary>
    public void DanhDauDaXuLy(DateTime thoiDiemUtc, string nguoiXuLy, string lyDoXuLy)
    {
        if (DaXuLy)
        {
            // 🔒 NO-OP: KHÔNG ghi đè bằng chứng của lần xử lý đầu.
            return;
        }

        if (string.IsNullOrWhiteSpace(lyDoXuLy))
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(lyDoXuLy))
                .WithData("khoa", KhoaNghiepVu)
                .WithData("lyDo", "Xử lý DLQ BẮT BUỘC có lý do (audit — sự kiện này từng hỏng).");
        }

        if (string.IsNullOrWhiteSpace(nguoiXuLy))
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(nguoiXuLy))
                .WithData("khoa", KhoaNghiepVu)
                .WithData("lyDo", "Thiếu người xử lý ⇒ không biết AI đã quyết định.");
        }

        var lyDo = lyDoXuLy.Trim();

        if (lyDo.Length > DoDaiLyDoToiDa)
        {
            throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(lyDoXuLy)).WithData("toiDa", DoDaiLyDoToiDa);
        }

        DaXuLy = true;
        XuLyLucUtc = thoiDiemUtc;
        NguoiXuLy = nguoiXuLy.Trim();
        LyDoXuLy = lyDo;
    }
}
