using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **TRẠNG THÁI HIỂN THỊ CỦA SẢN PHẨM TRÊN MỘT KÊNH** (`CAT-003`, `ADR-08`).
///
/// 🔒 **`KeThua` KHÔNG phải trạng thái "chờ xử lý" — nó LÀ trạng thái CUỐI CÙNG HỢP LỆ** cho **đa số** cặp
/// `(sản phẩm, kênh)`. 📌 Thiết kế **cố ý** KHÔNG tạo bản ghi cho mọi cặp: 10.000 sản phẩm × 5 kênh =
/// **50.000 dòng rỗng** ⇒ tốn dung lượng, chậm mọi truy vấn, và **không** mang thông tin nào
/// (`ADR-08`/`BR-012`).
/// ⚠️ **HỆ QUẢ BẮT BUỘC NHỚ Ở MỌI TRUY VẤN HIỂN THỊ:** phải dùng **`LEFT JOIN`** + `COALESCE(trạng thái, 'HienThi')`,
/// **CẤM** `INNER JOIN` — `INNER JOIN` sẽ **làm MẤT** khỏi kênh đúng những sản phẩm **đang hiển thị bình thường**
/// (vì chúng **không có** bản ghi nào). 🚨 Đây là lỗi **im lặng**: sản phẩm biến mất khỏi storefront mà
/// **không có ngoại lệ nào**.
///
/// 📌 `Loi` là trạng thái **ĐÍCH khi một chuyển dịch bị guard chặn** — xem `ProductChannelVisibility.ChuyenSang`.
/// ⚠️ Nó **không** được ghi xuống DB: guard ném lỗi **trước** khi ghi ⇒ *"không đổi dữ liệu"*.
/// </summary>
public enum TrangThaiHienThi
{
    /// <summary>🔑 **KHÔNG có bản ghi** cho cặp `(sản phẩm, kênh)` ⇒ hiển thị **theo mặc định của kênh**.
    /// 📌 Giá trị `0` để **mặc định của .NET** (`default(TrangThaiHienThi)`) **trùng** với nghĩa nghiệp vụ
    /// *"không có bản ghi"* — tránh một lớp lỗi *"quên khởi tạo ⇒ rơi vào trạng thái lạ"*.</summary>
    KeThua = 0,

    /// <summary>Sản phẩm **hiện** trên kênh.</summary>
    HienThi = 1,

    /// <summary>Sản phẩm **ẩn** khỏi kênh — **bắt buộc có lý do** (`Reason`), để 6 tháng sau còn biết vì sao.</summary>
    An = 2,

    /// <summary>🚫 **TRẠNG THÁI HẤP THỤ** — **mọi** chuyển dịch ra khỏi đây đều **bị chặn**.
    /// 📌 Muốn hiển thị lại ⇒ phải tạo **bản ghi MỚI** (có audit riêng cho lần bán lại).</summary>
    NgungBan = 3,

    /// <summary>⚠️ **ĐÍCH khi guard chặn một chuyển dịch** — **KHÔNG** ghi xuống DB.</summary>
    Loi = 4
}

/// <summary>
/// 🚨 **HIỂN THỊ SẢN PHẨM THEO KÊNH** (`CatProductChannelVisibility`) — `CAT-003`.
///
/// **BỐN BẤT BIẾN ĐỊNH HÌNH THIẾT KẾ (đọc trước khi sửa):**
/// <list type="number">
///   <item>🔑 **KHÔNG tạo bản ghi cho mọi cặp** — vắng bản ghi **chính là** `KeThua` (`ADR-08`). Xem `TrangThaiHienThi`;</item>
///   <item>🔒 **`TenantId` + `(ProductId, ChannelCode)` là DUY NHẤT** — cưỡng chế ở **DB** bằng unique index
///     `NULLS NOT DISTINCT`, vì hai request đồng thời (double-click) sẽ **cùng** qua được kiểm tra ở tầng ứng dụng;</item>
///   <item>🚨 **`An` BẮT BUỘC có `LyDo`** — ẩn một sản phẩm khỏi kênh là **quyết định kinh doanh**; không có lý do thì
///     6 tháng sau **không ai** phân biệt được *"ẩn vì hết hàng"* với *"ẩn vì lỗi thao tác"*;</item>
///   <item>🚫 **`NgungBan` là HẤP THỤ** — mọi chuyển dịch ra khỏi nó **bị chặn** (`0205`). 📌 Lý do: ngừng bán là
///     **kết thúc vòng đời** của cặp `(sản phẩm, kênh)`; cho phép quay lại sẽ **xoá mất** ranh giới giữa
///     *"lần bán trước"* và *"lần bán sau"* — và mọi báo cáo lịch sử theo kênh trở nên **không đọc được**.</item>
/// </list>
/// </summary>
public class ProductChannelVisibility : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của mã kênh — khớp `Channel.Code`/`InvChannelLimits.ChannelCode` (`HasMaxLength(32)`).</summary>
    public const int DoDaiMaKenhToiDa = 32;

    /// <summary>Độ dài tối đa của lý do — khớp `HasMaxLength(1024)`.</summary>
    public const int DoDaiLyDoToiDa = 1024;

    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    public string ChannelCode { get; protected set; } = default!;

    /// <summary>Trạng thái hiện tại. ⚠️ `protected set` ⇒ **chỉ** đổi qua <see cref="ChuyenSang"/> (một đường ghi).</summary>
    public TrangThaiHienThi TrangThai { get; protected set; }

    /// <summary>
    /// **Lý do** — 🔒 **BẮT BUỘC** khi `TrangThai = An` (xem <see cref="ChuyenSang"/>).
    /// 📌 Giữ lại cả khi đã chuyển sang trạng thái khác: nó là **bằng chứng** của lần ẩn trước đó.
    /// </summary>
    public string? LyDo { get; protected set; }

    protected ProductChannelVisibility() { }

    public ProductChannelVisibility(Guid id, Guid productId, string channelCode) : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(channelCode))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(channelCode));
        }

        var maChuan = channelCode.Trim();

        if (maChuan.Length > DoDaiMaKenhToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(channelCode)).WithData("toiDa", DoDaiMaKenhToiDa);
        }

        ProductId = productId;
        ChannelCode = maChuan;

        // 🔒 Bản ghi mới **LUÔN** bắt đầu từ `KeThua` — 📌 "vắng bản ghi" và "bản ghi `KeThua`" là **cùng một
        // nghĩa nghiệp vụ**, nên không có lý do để tạo bản ghi ở trạng thái nào khác. Muốn `HienThi`/`An` thì
        // gọi `ChuyenSang` ⇒ guard chạy ⇒ **không** có đường nào lách qua guard.
        TrangThai = TrangThaiHienThi.KeThua;
    }

    /// <summary>
    /// 🚨 **CHUYỂN TRẠNG THÁI HIỂN THỊ — BẢNG CHUYỂN LÀ MỘT PHẦN CỦA ĐẶC TẢ.**
    ///
    /// **VÌ SAO ĐẶT Ở ENTITY (không ở app service):** guard này là **bất biến nghiệp vụ** — nếu chỉ kiểm ở app
    /// service thì **đường ghi thứ hai** (job, seed, API mới) sẽ **bỏ qua** nó và **không có lỗi nào**.
    /// 📌 Cùng khuôn `Product.Slug` (`CAT-003` §16.3) và `ReconRunMismatch.DanhDauDaXuLy` (`REC-001` §17.3):
    /// **một đường ghi duy nhất thì luật chỉ có một chỗ.**
    ///
    /// 🔒 **Bảng chuyển (từ ⇒ sang) — mọi cặp KHÔNG có trong bảng ⇒ NÉM `0205`:**
    /// <list type="table">
    ///   <item><term>`KeThua`/`An` ⇒ `HienThi`</term><description>`sanPhamDaPublish` phải `true` — **cấm** hiện
    ///     sản phẩm chưa publish ⇒ `0205`</description></item>
    ///   <item><term>`KeThua`/`HienThi` ⇒ `An`</term><description>**BẮT BUỘC** có lý do ⇒ `0205` nếu thiếu</description></item>
    ///   <item><term>`HienThi`/`An` ⇒ `NgungBan`</term><description>`conListingDongBo` phải `false` (app service
    ///     kiểm qua `ChannelListing`) ⇒ `0209`</description></item>
    ///   <item><term>`NgungBan` ⇒ *bất kỳ*</term><description>🚫 **KHÔNG** có guard nào cho phép ⇒ `0205`</description></item>
    ///   <item><term>*mọi cặp khác*</term><description>⇒ `0205`</description></item>
    /// </list>
    ///
    /// ⚠️ **THỨ TỰ KIỂM LÀ MỘT PHẦN CỦA ĐẶC TẢ:** kiểm **hấp thụ (`NgungBan`) TRƯỚC**, rồi mới kiểm lý do/publish.
    /// 📌 Lý do: `NgungBan` chặn **mọi** chuyển dịch ⇒ nếu kiểm "thiếu lý do" trước thì người dùng nhận
    /// *"thiếu lý do"* trong khi **thật ra** cặp `(sản phẩm, kênh)` đã **ngừng bán** — thông báo **sai** dẫn họ
    /// đi sửa một thứ **không** giải quyết được gì. 🔑 Cùng bài học `REC-001` §17.3 (no-op trước, tham số sau).
    /// </summary>
    /// <param name="sang">Trạng thái đích.</param>
    /// <param name="sanPhamDaPublish">`Product.Status` có phải đã publish (app service truyền vào).</param>
    /// <param name="lyDo">Lý do (bắt buộc khi ẩn).</param>
    public void ChuyenSang(TrangThaiHienThi sang, bool sanPhamDaPublish, string? lyDo = null)
    {
        // ══════════════════════════════════════════════════════════════════════════════════════════
        // ① HẤP THỤ TRƯỚC — `NgungBan` chặn MỌI chuyển dịch (kể cả `NgungBan` ⇒ `NgungBan`).
        // 📌 Kể cả khi `sang == TrangThai` (no-op): `NgungBan` là **hấp thụ TUYỆT ĐỐI**, nên gọi lại cũng
        // **phải ném** — khác hẳn các cặp khác, nơi gọi lại cùng trạng thái là **vô hại**.
        // 🔑 Vì sao khác: với `HienThi`, gọi lại là *"retry/double-click"* ⇒ phải idempotent.
        // Với `NgungBan`, mọi lời gọi đều nằm trong một thao tác **không được phép** (đã kết thúc vòng đời)
        // ⇒ im lặng chấp nhận sẽ **che mất** việc người dùng đang cố làm một điều sai.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        if (TrangThai == TrangThaiHienThi.NgungBan)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe)
                .WithData("tu", TrangThai.ToString())
                .WithData("sang", sang.ToString())
                .WithData("sanPhamId", ProductId)
                .WithData("kenh", ChannelCode)
                .WithData("lyDo",
                    "`NgungBan` là trạng thái HẤP THỤ — muốn hiển thị lại thì tạo BẢN GHI MỚI "
                    + "(để có audit riêng cho lần bán lại).");
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // ② NO-OP cho các cặp còn lại: gọi lại ĐÚNG trạng thái hiện tại ⇒ KHÔNG ném (retry/double-click).
        // 📌 Cùng khuôn `ReconRunMismatch.DanhDauDaXuLy` và `Customer.NangCapVangLai`: gọi lại **đúng** trạng
        // thái là *"đã ở trạng thái đích"*, **không** phải "chuyển sai".
        // ⚠️ Và **KHÔNG** ghi đè `LyDo` (bằng chứng của lần ẩn đầu) — xem `REC-001` §17.3 bất biến (2).
        // ══════════════════════════════════════════════════════════════════════════════════════════
        if (sang == TrangThai)
        {
            return;
        }

        switch (sang)
        {
            // ── ⇒ `HienThi` ────────────────────────────────────────────────────────────────────────
            case TrangThaiHienThi.HienThi:
                // 🔒 Nguồn hợp lệ: `KeThua` (chưa có bản ghi tường minh) hoặc `An` (bỏ ẩn).
                if (TrangThai is not (TrangThaiHienThi.KeThua or TrangThaiHienThi.An))
                {
                    NemChuyenSai(sang);
                    break;
                }

                // 🚨 **CẤM hiện sản phẩm CHƯA PUBLISH.** 📌 Vì sao là lỗi **nghiệp vụ** chứ không phải "để UI lo":
                // kênh bán là **mặt tiền công khai**; đẩy một sản phẩm chưa publish lên đó ⇒ khách **đặt được**
                // một sản phẩm chưa có giá/ảnh/chính sách hoàn chỉnh ⇒ đơn hỏng **ở phía khách**, và ta chỉ biết
                // khi có người phàn nàn.
                if (!sanPhamDaPublish)
                {
                    throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe)
                        .WithData("tu", TrangThai.ToString())
                        .WithData("sang", sang.ToString())
                        .WithData("sanPhamId", ProductId)
                        .WithData("kenh", ChannelCode)
                        .WithData("lyDo", "Sản phẩm CHƯA publish ⇒ KHÔNG được hiển thị trên kênh.");
                }

                TrangThai = TrangThaiHienThi.HienThi;
                LyDo = null;   // 📌 Hiển thị lại ⇒ lý do ẩn cũ **hết hiệu lực** (giữ lại sẽ gây hiểu sai).
                break;

            // ── ⇒ `An` ─────────────────────────────────────────────────────────────────────────────
            case TrangThaiHienThi.An:
                if (TrangThai is not (TrangThaiHienThi.KeThua or TrangThaiHienThi.HienThi))
                {
                    NemChuyenSai(sang);
                    break;
                }

                // 🚨 **ẨN BẮT BUỘC CÓ LÝ DO** — xem bất biến (3) ở chú thích lớp.
                if (string.IsNullOrWhiteSpace(lyDo))
                {
                    throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe)
                        .WithData("tu", TrangThai.ToString())
                        .WithData("sang", sang.ToString())
                        .WithData("sanPhamId", ProductId)
                        .WithData("kenh", ChannelCode)
                        .WithData("lyDo", "ẨN sản phẩm khỏi kênh BẮT BUỘC có lý do (quyết định kinh doanh — cần audit).");
                }

                var lyDoChuan = lyDo.Trim();

                if (lyDoChuan.Length > DoDaiLyDoToiDa)
                {
                    throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                        .WithData("truong", nameof(lyDo)).WithData("toiDa", DoDaiLyDoToiDa);
                }

                TrangThai = TrangThaiHienThi.An;
                LyDo = lyDoChuan;
                break;

            // ── ⇒ `NgungBan` ───────────────────────────────────────────────────────────────────────
            case TrangThaiHienThi.NgungBan:
                // 🔒 Nguồn hợp lệ: `HienThi` hoặc `An` (từ `KeThua` ⇒ **không** có gì để ngừng bán).
                // ⚠️ Guard "còn listing đồng bộ" do **app service** kiểm (`0209` kèm **danh sách** listing còn lại)
                // vì entity này **không** biết `ChannelListing` (aggregate khác, module khác).
                if (TrangThai is not (TrangThaiHienThi.HienThi or TrangThaiHienThi.An))
                {
                    NemChuyenSai(sang);
                    break;
                }

                TrangThai = TrangThaiHienThi.NgungBan;
                break;

            // ── ⇒ `KeThua` / `Loi`: KHÔNG có trong bảng ⇒ luôn ném ────────────────────────────────
            default:
                NemChuyenSai(sang);
                break;
        }
    }

    /// <summary>
    /// **Mọi cặp KHÔNG có trong bảng ⇒ NÉM `0205`** (quy ước #11: guard phải **NÉM**, không im lặng).
    /// 📌 Gộp một chỗ để **không** có nhánh nào *"quên ném"* — đó là cách một bảng chuyển trở thành **trang trí**.
    /// </summary>
    private void NemChuyenSai(TrangThaiHienThi sang)
    {
        throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiHienThiKhongHopLe)
            .WithData("tu", TrangThai.ToString())
            .WithData("sang", sang.ToString())
            .WithData("sanPhamId", ProductId)
            .WithData("kenh", ChannelCode)
            .WithData("lyDo", "Cặp chuyển trạng thái này KHÔNG có trong bảng ⇒ luôn bị chặn.");
    }
}
