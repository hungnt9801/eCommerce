using eCommerce.Settings;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

using eCommerce.Reconciliation;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **MỘT LƯỢT QUÉT ĐỐI SOÁT TỒN KHO** (`REC-001` — bảng `ReconRuns`).
///
/// **VÌ SAO CẦN BẢNG NÀY (khoảng trống thật đã ghi ở `docs/tiep-tuc-phien-moi.md` §5 việc 1):**
/// job đối soát đã **phát hiện + đếm đúng** lệch (`inv-001` §38–§39), nhưng **KHÔNG lưu lại gì**:
/// sau khi log bị cuốn (hoặc container bị dựng lại) thì **không còn cách nào** trả lời:
/// <list type="bullet">
///   <item>*"Lệch này mới xuất hiện hay đã có từ tuần trước?"* — không có gì để so;</item>
///   <item>*"Lượt quét 3 giờ sáng có **chạy** không, hay job im lặng chết?"* — metric chỉ giữ **giá trị hiện tại**,
///     nên `0` **không phân biệt** được *"đã đo và khớp"* với *"chưa ai đo"* (đúng vấn đề `inv-001` §37.1);</item>
///   <item>*"Lệch lúc đó được phát hiện theo NGƯỠNG nào?"* — chính sách nay là **dữ liệu cấu hình**
///     ⇒ đổi setting về sau là **mất** ngữ cảnh của các lần chặn trước.</item>
/// </list>
/// ⇒ Bảng này biến *"job có chạy"* và *"lệch theo thời gian"* thành **dữ liệu đọc được**, không phải suy đoán từ log.
///
/// 📌 **Ghi CẢ lượt KHÔNG có lệch** (`SoLech = 0`): đó chính là bằng chứng *"đã đo"* — nếu chỉ ghi lượt có lệch
/// thì bảng rỗng **không phân biệt** được *"hệ thống khớp"* với *"job chưa bao giờ chạy"*.
/// </summary>
public class ReconRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mốc bắt đầu quét (UTC) — lấy **trước** khi đọc tồn kho + sổ cái.</summary>
    public DateTime BatDauUtc { get; protected set; }

    /// <summary>Mốc kết thúc quét (UTC) — lấy **sau** khi ghi xong các dòng lệch.</summary>
    public DateTime KetThucUtc { get; protected set; }

    /// <summary>Số cặp `(kho, SKU)` **đã đo** trong lượt này (kể cả cặp **khớp**). `0` là **hợp lệ**: chưa có tồn kho nào.</summary>
    public int SoCapDaDo { get; protected set; }

    /// <summary>Số cặp `(kho, SKU)` **LỆCH** giữa `OnHand` và sổ cái.</summary>
    public int SoLech { get; protected set; }

    // ── ẢNH CHỤP CHÍNH SÁCH ĐANG ÁP (`REC-001` — "cho phép chọn") ────────────────────────────────
    // 🚨 VÌ SAO LƯU Ở ĐÂY, KHÔNG CHỈ ĐỌC TỪ SETTING: setting là **giá trị hiện tại**; lượt quét đã qua phải
    // giữ **đúng** ngưỡng nó đã dùng, nếu không thì *"vì sao lượt đó không báo động"* không trả lời được nữa.

    /// <summary>Đơn vị ngưỡng đã dùng (`Recon.Threshold.Unit`).</summary>
    public DonViNguong DonViNguong { get; protected set; }

    /// <summary>Giá trị ngưỡng đã dùng (`Recon.Threshold.Value`).</summary>
    public decimal GiaTriNguong { get; protected set; }

    /// <summary>Hành động đã chọn khi lệch (`Recon.Mismatch.Action`).</summary>
    public HanhDongLech HanhDong { get; protected set; }

    /// <summary>Đơn vị ngưỡng **có hợp lệ** không (giá trị lạ ⇒ `false` ⇒ lượt này chạy bằng **mặc định an toàn**).</summary>
    public bool NguongHopLe { get; protected set; }

    /// <summary>Số cặp lệch **VƯỢT** ngưỡng đã chọn (tính bằng <see cref="NguongLech"/> — hàm thuần).</summary>
    public int SoLechVuotNguong { get; protected set; }

    /// <summary>**CÓ** cặp lệch vượt ngưỡng hay không. 📌 Tách khỏi `SoLechVuotNguong` để truy vấn *"các lượt cần người xem"* rẻ.</summary>
    public bool CoVuotNguong { get; protected set; }

    /// <summary>
    /// Kết luận của lượt quét. 🔒 Lưu dạng **số nguyên** (đọc bằng mắt trong DB không đoán được chuỗi) —
    /// quy ước `0 = KhongVuot` · `1 = VuotCanhBao` · `2 = VuotCanXacNhan`.
    /// </summary>
    public KetLuanDoiSoat KetLuan { get; protected set; }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // 🚨 `S7` — **XÁC NHẬN LỆCH = ĐÓNG CẢNH BÁO + AUDIT** (quyết định `Q5` = **A**, `rec-001` §0).
    //
    // **VÌ SAO PHẢI CÓ (hiện trạng ĐO ĐƯỢC trước lượt này):** `RequireConfirmation` ghi được trạng thái
    // `2` (`VuotCanXacNhan`), nhưng **KHÔNG có đường nào để xác nhận** ⇒ lượt quét nằm ở trạng thái `2`
    // **VĨNH VIỄN**, cảnh báo **tồn đọng**, và người vận hành chỉ còn cách sửa DB tay (⇒ mất audit).
    //
    // 🔒 **Xác nhận TUYỆT ĐỐI KHÔNG sửa tồn/sổ cái** — phương án *"xác nhận ⇒ tự sinh bút toán điều chỉnh"*
    // đã **BỊ LOẠI** (job không được sửa sổ cái append-only; chứng từ do người tạo). Ở đây nó chỉ ghi
    // **ai / khi nào / vì sao** rồi **đóng cảnh báo**.
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Ai xác nhận (`AbpUsers.Id`) — `null` = **chưa xác nhận**. 📌 Lưu ID, **không** lưu tên gõ tay:
    /// tên gõ tay không đối chiếu được với tài khoản nào ⇒ không phải audit.</summary>
    public Guid? ConfirmedByUserId { get; protected set; }

    /// <summary>Thời điểm xác nhận (UTC) — `null` = **chưa xác nhận**.</summary>
    public DateTime? ConfirmedAtUtc { get; protected set; }

    /// <summary>Vì sao chấp nhận lệch — **BẮT BUỘC** khi xác nhận (xem <see cref="XacNhan"/>).</summary>
    public string? ConfirmNote { get; protected set; }

    protected ReconRun() { }

    public ReconRun(
        Guid id,
        DateTime batDauUtc,
        DateTime ketThucUtc,
        int soCapDaDo,
        DonViNguong donViNguong,
        decimal giaTriNguong,
        HanhDongLech hanhDong,
        bool nguongHopLe) : base(id)
    {
        if (ketThucUtc < batDauUtc)
        {
            // 🔒 Không phải "làm tròn cho xong": một lượt quét có mốc kết thúc TRƯỚC mốc bắt đầu là
            // dấu hiệu đồng hồ/ánh xạ thời gian sai ⇒ lưu vào là gieo số liệu không đối soát được về sau.
            throw new ArgumentException("Mốc KẾT THÚC của lượt đối soát không được trước mốc BẮT ĐẦU.", nameof(ketThucUtc));
        }

        BatDauUtc = batDauUtc;
        KetThucUtc = ketThucUtc;
        SoCapDaDo = soCapDaDo;
        DonViNguong = donViNguong;
        GiaTriNguong = giaTriNguong;
        HanhDong = hanhDong;
        NguongHopLe = nguongHopLe;
    }

    /// <summary>
    /// **CHỐT KẾT QUẢ** lượt quét — nhận **số lệch** và **số lệch vượt ngưỡng** (đã tính bằng hàm thuần
    /// <see cref="NguongLech"/> ở tầng dịch vụ), rồi **tự suy ra** <see cref="KetLuan"/> từ **chính sách đã lưu**.
    /// 📌 Suy ra ở đây (không ở chỗ gọi) để **một** nơi định nghĩa *"thế nào là vượt ngưỡng"* cho bản ghi này.
    /// </summary>
    public void ChotKetQua(int soLech, int soLechVuotNguong)
    {
        if (soLech < 0 || soLechVuotNguong < 0 || soLechVuotNguong > soLech)
        {
            throw new ArgumentException(
                $"Số lệch không hợp lệ: soLech={soLech}, soLechVuotNguong={soLechVuotNguong} " +
                "(phải >= 0 và số VƯỢT NGƯỠNG không được lớn hơn tổng số lệch).", nameof(soLech));
        }

        SoLech = soLech;
        SoLechVuotNguong = soLechVuotNguong;
        CoVuotNguong = soLechVuotNguong > 0;

        KetLuan = !CoVuotNguong
            ? KetLuanDoiSoat.KhongVuot
            : HanhDong == HanhDongLech.RequireConfirmation
                ? KetLuanDoiSoat.VuotCanXacNhan
                : KetLuanDoiSoat.VuotCanhBao;
    }

    /// <summary>Lượt này **đang chờ người xác nhận** hay không (`Q5`: chính sách `RequireConfirmation`).</summary>
    public bool ChoXacNhan => KetLuan == KetLuanDoiSoat.VuotCanXacNhan && ConfirmedAtUtc is null;

    /// <summary>
    /// 🚨 **XÁC NHẬN LỆCH CỦA LƯỢT** — ghi `ai / khi nào / vì sao` rồi **đóng cảnh báo** (`rec-001` §0).
    ///
    /// 🔒 **Ba bất biến (mỗi điều chặn một kiểu lạm dụng):**
    /// <list type="number">
    ///   <item><b>Chỉ xác nhận được lượt ĐANG CHỜ xác nhận</b> (`KetLuan = VuotCanXacNhan`) — xác nhận một
    ///     lượt `KhongVuot` hay `VuotCanhBao` là **đóng một cảnh báo không tồn tại** ⇒ người đọc sau hiểu
    ///     *"đã có người xem"* trong khi **chưa ai xem**;</item>
    ///   <item><b>Ghi chú BẮT BUỘC</b> — không có lý do thì 6 tháng sau không ai phân biệt được
    ///     *"đã kiểm kê và khớp lại"* với *"ai đó bấm nút cho hết danh sách"*;</item>
    ///   <item><b>Không ghi đè lần xác nhận trước</b> — gọi lại là **no-op**, và **kiểm no-op TRƯỚC** khi kiểm
    ///     tham số (📌 đúng thứ tự đã trả giá ở `ReconRunMismatch.DanhDauDaXuLy`: nếu kiểm tham số trước thì một
    ///     **retry** không mang ghi chú sẽ **NÉM** trên một lượt đã xác nhận xong).</item>
    /// </list>
    ///
    /// ⚠️ **KHÔNG** đụng tới `SoLech`/`SoLechVuotNguong`/các dòng lệch: xác nhận là **ghi chú vận hành**,
    /// không phải sửa số đo (xem chú thích đầu lớp về `Q5`).
    /// </summary>
    /// <param name="nguoiXacNhanId">`AbpUsers.Id` của người xác nhận — **bắt buộc**.</param>
    /// <param name="thoiDiemUtc">Thời điểm xác nhận (UTC) — do chỗ gọi truyền vào (hàm **tất định**).</param>
    /// <param name="ghiChu">Vì sao chấp nhận lệch — **bắt buộc**.</param>
    /// <exception cref="Volo.Abp.BusinessException">`Recon:0004` khi lượt không ở trạng thái chờ xác nhận;
    /// `Recon:0005` khi thiếu ghi chú.</exception>
    public void XacNhan(Guid nguoiXacNhanId, DateTime thoiDiemUtc, string ghiChu)
    {
        // 🔒 NO-OP TRƯỚC (idempotent) — lý do ở bất biến (3).
        if (ConfirmedAtUtc is not null)
        {
            return;
        }

        if (KetLuan != KetLuanDoiSoat.VuotCanXacNhan)
        {
            throw new Volo.Abp.BusinessException(ReconErrorCodes.LuotKhongChoXacNhan)
                .WithData("reconRunId", Id)
                .WithData("ketLuan", KetLuan.ToString())
                .WithData("lyDo", "Chỉ lượt có kết luận `VuotCanXacNhan` (đang chờ người xác nhận) mới xác nhận được.");
        }

        if (string.IsNullOrWhiteSpace(ghiChu))
        {
            throw new Volo.Abp.BusinessException(ReconErrorCodes.ThieuGhiChuXacNhan)
                .WithData("reconRunId", Id)
                .WithData("lyDo", "Xác nhận lệch tồn kho BẮT BUỘC có ghi chú (chênh lệch tài sản — cần audit).");
        }

        ConfirmedByUserId = nguoiXacNhanId;
        ConfirmedAtUtc = thoiDiemUtc;
        ConfirmNote = ghiChu.Trim();
    }
}

/// <summary>Kết luận một lượt đối soát (`REC-001`).</summary>
public enum KetLuanDoiSoat
{
    /// <summary>Không có cặp lệch nào **vượt** ngưỡng đã chọn (có thể vẫn có lệch **dưới** ngưỡng).</summary>
    KhongVuot = 0,

    /// <summary>Có lệch vượt ngưỡng và chính sách là `Warn` ⇒ **ghi cảnh báo**, không tự sửa dữ liệu.</summary>
    VuotCanhBao = 1,

    /// <summary>Có lệch vượt ngưỡng và chính sách là `RequireConfirmation` ⇒ **chờ người xác nhận**.</summary>
    VuotCanXacNhan = 2
}

/// <summary>
/// **MỘT CẶP `(kho, SKU)` LỆCH** trong một lượt đối soát (`REC-001`) — bảng `ReconRunMismatches`.
///
/// 📌 **VÌ SAO TÁCH BẢNG RIÊNG (không nhồi JSON vào `ReconRuns`):** câu hỏi vận hành thật là
/// *"SKU này lệch từ lượt nào?"* và *"cặp này lệch bao nhiêu lần?"* — nhồi JSON thì **không truy vấn** được
/// bằng SQL, phải tải cả chuỗi ra và tự phân tích ⇒ **không ai làm**, và dữ liệu trở thành **ghi cho có**.
/// 🔒 Số dòng mỗi lượt **có trần** (`InventoryReconciliationService.SoDongLechToiDaMoiLuot`) — bảng lịch sử
/// không được phép phình vô hạn vì một lượt lệch diện rộng.
/// </summary>
public class ReconRunMismatch : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Lượt đối soát sinh ra dòng này (trỏ tới `ReconRuns.Id`).</summary>
    public Guid ReconRunId { get; protected set; }

    public Guid WarehouseId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    /// <summary>Ảnh chụp `InventoryItem.OnHand` tại thời điểm quét.</summary>
    public int OnHand { get; protected set; }

    /// <summary>Tổng **có dấu** của sổ cái (nguồn sự thật) tại thời điểm quét.</summary>
    public int TongSoCai { get; protected set; }

    /// <summary>Độ lệch = `OnHand − TongSoCai` (dấu giữ nguyên để đọc ra được **lệch về phía nào**).</summary>
    public int ChenhLech { get; protected set; }

    /// <summary>
    /// Dòng này có **VƯỢT NGƯỠNG** của lượt hay không.
    /// 📌 Lưu ở **từng dòng** (không chỉ ở lượt) để trả lời *"cặp nào đã vượt ngưỡng"* bằng **một** truy vấn,
    /// và để đổi ngưỡng về sau **không** viết lại lịch sử đã kết luận.
    /// </summary>
    public bool VuotNguong { get; protected set; }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // 🚨 `REC-001` — **QUY TRÌNH XỬ LÝ LỆCH** (bổ sung 16/10/2026).
    //
    // **VÌ SAO PHẢI CÓ (đây là điều kiện để dữ liệu lệch có ÍCH, không chỉ "ghi cho có"):**
    // trước lượt này `ReconRunMismatch` là **append-only, không có trạng thái** ⇒ một dòng lệch đã được
    // người vận hành **xử lý xong** vẫn **trông y hệt** một dòng **chưa ai đụng tới**. Hệ quả:
    // <list type="bullet">
    //   <item>mỗi nhịp job, người trực lại phải **đọc lại toàn bộ** danh sách cũ để tìm cái mới
    //     ⇒ tới lúc danh sách dài thì **không ai đọc nữa** (đúng số phận của mọi báo cáo không có trạng thái);</item>
    //   <item>**không trả lời được** câu hỏi kiểm toán duy nhất quan trọng: *"lệch này AI xử lý, LÚC NÀO,
    //     và VÌ SAO chấp nhận?"* — mà lệch tồn kho là **chênh lệch tài sản**, không phải thông báo.</item>
    // </list>
    // 📌 Đây là **cùng khuôn** đã dùng cho `MarketplaceOrderIntake.DanhDauDaDoiSoat` và
    // `Customer.Chan/MoChan`: **trạng thái + lý do bắt buộc** (audit), không có đường "im lặng bỏ qua".
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>`true` = **đã có người xử lý** dòng lệch này. 🔒 Mặc định `false` ⇒ mọi dòng **mới** đều
    /// **chưa xử lý** ⇒ không có dòng nào "vô tình" được coi là xong.</summary>
    public bool DaXuLy { get; protected set; }

    /// <summary>Thời điểm xử lý (UTC) — `null` nếu chưa xử lý.</summary>
    public DateTime? XuLyLucUtc { get; protected set; }

    /// <summary>Ai xử lý (`NguoiXuLy`) — audit.</summary>
    public string? NguoiXuLy { get; protected set; }

    /// <summary>
    /// **VÌ SAO chấp nhận/xử lý lệch này** — **BẮT BUỘC** khi đánh dấu đã xử lý (xem <see cref="DanhDauDaXuLy"/>).
    /// 🚨 Không có lý do thì 6 tháng sau **không ai** phân biệt được *"đã kiểm kê và khớp lại"* với
    /// *"ai đó bấm nút cho hết danh sách"* — và cả hai đều **xoá dấu vết chênh lệch tài sản**.
    /// </summary>
    public string? LyDoXuLy { get; protected set; }

    protected ReconRunMismatch() { }

    public ReconRunMismatch(
        Guid id,
        Guid reconRunId,
        Guid warehouseId,
        string sku,
        int onHand,
        int tongSoCai,
        bool vuotNguong) : base(id)
    {
        ReconRunId = reconRunId;
        WarehouseId = warehouseId;
        Sku = sku;
        OnHand = onHand;
        TongSoCai = tongSoCai;
        ChenhLech = onHand - tongSoCai;
        VuotNguong = vuotNguong;
    }

    /// <summary>
    /// 🚨 **ĐÁNH DẤU ĐÃ XỬ LÝ** dòng lệch — `BR`: **BẮT BUỘC có lý do + người xử lý**.
    ///
    /// 🔒 **Bất biến (ba điều, mỗi điều chặn một kiểu lạm dụng):**
    /// <list type="number">
    ///   <item><b>Lý do và người xử lý KHÔNG được rỗng</b> ⇒ chặn *"bấm nút cho hết danh sách"*;</item>
    ///   <item><b>Không ghi đè lần xử lý trước</b> ⇒ gọi lại là **no-op**, KHÔNG đổi lý do/người/thời điểm.
    ///     📌 Vì sao: lần xử lý **đầu** là **bằng chứng** ai đã chấp nhận chênh lệch. Cho ghi đè thì
    ///     người sau có thể **thay bằng chứng** — và **không có lỗi nào** để lần ra;</item>
    ///   <item><b>Ghi lại thời điểm</b> do người gọi truyền vào (**không** đọc đồng hồ hệ thống bên trong)
    ///     ⇒ hàm **tất định**, test được, và cùng một dòng luôn ghi cùng mốc dù gọi ở đâu.</item>
    /// </list>
    /// </summary>
    /// <param name="thoiDiemUtc">Thời điểm xử lý (UTC).</param>
    /// <param name="nguoiXuLy">Ai xử lý — **bắt buộc** (audit).</param>
    /// <param name="lyDo">Vì sao chấp nhận/xử lý — **bắt buộc** (audit).</param>
    /// <exception cref="Volo.Abp.BusinessException">`eCommerce:Recon:0002` khi thiếu lý do hoặc người xử lý.</exception>
    public void DanhDauDaXuLy(DateTime thoiDiemUtc, string nguoiXuLy, string lyDo)
    {
        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **THỨ TỰ KIỂM LÀ MỘT PHẦN CỦA ĐẶC TẢ — và tôi đã viết SAI lần đầu (TEST bắt được):**
        //
        // Phiên bản đầu kiểm **rỗng TRƯỚC**, rồi mới kiểm `DaXuLy`. Hệ quả: một **retry** (job phát lại,
        // double-click, message replay) **không mang lý do** sẽ **NÉM** trên một dòng **đã xử lý xong**
        // ⇒ consumer **retry vô hạn** vì một việc **không cần làm gì**.
        // 🔑 Đúng thứ tự: **NO-OP TRƯỚC** (idempotent), **rồi mới** kiểm tham số cho lần xử lý **ĐẦU TIÊN**.
        // 📌 Cùng khuôn `Customer.NangCapVangLai`: gọi lại **đúng** trạng thái ⇒ **không ném**, vì đó là
        // *"đã ở trạng thái đích"*, **không** phải "chuyển sai".
        // ══════════════════════════════════════════════════════════════════════════════════════════
        if (DaXuLy)
        {
            // 🔒 **NO-OP** (không phải lỗi): gọi lại là retry/double-click ⇒ phải vô hại.
            // ⚠️ Nhưng **KHÔNG** ghi đè bằng chứng của lần xử lý đầu — xem bất biến (2).
            return;
        }

        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new Volo.Abp.BusinessException(ReconErrorCodes.ThieuLyDoXuLyLech)
                .WithData("sku", Sku)
                .WithData("warehouseId", WarehouseId)
                .WithData("chenhLech", ChenhLech)
                .WithData("lyDo", "Xử lý lệch tồn kho BẮT BUỘC có lý do (chênh lệch tài sản — cần audit).");
        }

        if (string.IsNullOrWhiteSpace(nguoiXuLy))
        {
            throw new Volo.Abp.BusinessException(ReconErrorCodes.ThieuLyDoXuLyLech)
                .WithData("sku", Sku)
                .WithData("lyDo", "Thiếu `NguoiXuLy` — cần biết AI chấp nhận chênh lệch này (audit).");
        }

        DaXuLy = true;
        XuLyLucUtc = thoiDiemUtc;
        NguoiXuLy = nguoiXuLy.Trim();
        LyDoXuLy = lyDo.Trim();
    }
}
