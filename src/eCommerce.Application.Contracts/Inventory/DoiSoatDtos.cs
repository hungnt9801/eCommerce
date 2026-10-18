using Volo.Abp.Application.Dtos;

namespace eCommerce.Inventory;

/// <summary>
/// **MỘT LƯỢT ĐỐI SOÁT TỒN KHO** trả ra cho người vận hành (`REC-001`).
/// 📌 `NguongHopLe` và **ảnh chụp chính sách** (`DonViNguong`/`GiaTriNguong`/`HanhDong`) được trả **cùng**
/// lượt: đổi chính sách về sau **không** viết lại lịch sử, nên người đọc cần biết **lượt đó đã chấm theo
/// ngưỡng nào** — nếu không, một lượt cũ *"0 lệch vượt ngưỡng"* trông như *"kho sạch"* trong khi thật ra
/// **ngưỡng đặt quá rộng**.
/// </summary>
public class LuotDoiSoatDto
{
    public Guid Id { get; set; }
    public DateTime BatDauUtc { get; set; }
    public DateTime KetThucUtc { get; set; }

    /// <summary>Thời gian quét (giây) — suy ra, để người đọc biết lượt có **chạy bất thường lâu** không.</summary>
    public double SoGiayQuet { get; set; }

    public int SoCapDaDo { get; set; }
    public int SoLech { get; set; }
    public int SoLechVuotNguong { get; set; }
    public bool CoVuotNguong { get; set; }

    /// <summary>Ảnh chụp chính sách tại thời điểm quét — xem chú thích ở lớp.</summary>
    public string DonViNguong { get; set; } = default!;
    public decimal GiaTriNguong { get; set; }
    public string HanhDong { get; set; } = default!;

    /// <summary>⚠️ `false` = **ngưỡng KHÔNG hợp lệ** tại thời điểm quét (ví dụ giá trị âm) ⇒
    /// **mọi** dòng lệch bị coi là vượt ngưỡng. 📌 Đây là ca **cấu hình sai**, không phải *"kho lệch nhiều"* —
    /// trả cờ ra để người đọc **không** đi tìm nguyên nhân ở kho.
    /// </summary>
    public bool NguongHopLe { get; set; }

    /// <summary>`Clean` · `DiscrepancyWithinThreshold` · `DiscrepancyExceeded` (xem `KetLuanDoiSoat`).</summary>
    public string KetLuan { get; set; } = default!;

    /// <summary>Số dòng lệch **CHƯA xử lý** của lượt — 🔒 con số **hành động được**: nó giảm khi người
    /// vận hành xử lý. 📌 Nếu chỉ trả `SoLech` (tổng), danh sách **mãi** hiện như cũ và **không ai đọc nữa**.
    /// </summary>
    public int SoLechChuaXuLy { get; set; }

    // ── 🆕 `S7` — XÁC NHẬN LỆCH (`Q5` = A: đóng cảnh báo + audit, KHÔNG tự sửa tồn) ───────────────────

    /// <summary>`true` = lượt **đang chờ** người có quyền xác nhận (`KetLuan = VuotCanXacNhan` và chưa xác nhận).
    /// 🔒 Trả cờ sẵn để màn hình **không tự suy** từ chuỗi `KetLuan` (suy từ chuỗi sẽ lệch khi thêm kết luận mới).</summary>
    public bool ChoXacNhan { get; set; }

    /// <summary>Đã xác nhận hay chưa (⚠️ **KHÁC** `ChoXacNhan`: một lượt đã xác nhận thì cả hai đều `false`).</summary>
    public bool DaXacNhan { get; set; }

    /// <summary>Ai xác nhận (`AbpUsers.Id`) — `null` khi chưa xác nhận.</summary>
    public Guid? ConfirmedByUserId { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }

    /// <summary>Vì sao chấp nhận lệch — **bắt buộc** khi xác nhận (audit).</summary>
    public string? ConfirmNote { get; set; }
}

/// <summary>**MỘT DÒNG LỆCH** `(kho, SKU)` kèm trạng thái xử lý (`REC-001`).</summary>
public class DongLechDto
{
    public Guid Id { get; set; }
    public Guid ReconRunId { get; set; }
    public Guid WarehouseId { get; set; }

    /// <summary>Mã kho (người đọc hiểu) — tra thêm để UI **không** phải hiện GUID.</summary>
    public string? WarehouseCode { get; set; }

    public string Sku { get; set; } = default!;

    /// <summary>Ảnh chụp `OnHand` lúc quét.</summary>
    public int OnHand { get; set; }

    /// <summary>Tổng **có dấu** của sổ cái (nguồn sự thật) lúc quét.</summary>
    public int TongSoCai { get; set; }

    /// <summary>`OnHand − TongSoCai` — dấu giữ nguyên để biết **lệch về phía nào**
    /// (dương = hệ thống ghi **nhiều hơn** thực tế · âm = **thiếu** so với sổ).</summary>
    public int ChenhLech { get; set; }

    public bool VuotNguong { get; set; }

    /// <summary>🔒 `false` = **chưa ai xử lý** ⇒ UI lọc theo cờ này để chỉ hiện việc **cần làm**.</summary>
    public bool DaXuLy { get; set; }

    public DateTime? XuLyLucUtc { get; set; }
    public string? NguoiXuLy { get; set; }
    public string? LyDoXuLy { get; set; }
}

/// <summary>
/// **BỘ LỌC danh sách lượt đối soát** (`REC-001`).
/// ⚠️ `DenUtc` **bao gồm** cả ngày đó (so `< DenUtc.AddDays(1)`): 📌 nếu dùng `<=` trần thì lượt quét lúc
/// `23:59:59.xxx` của ngày cuối **bị bỏ sót** — và người dùng thấy *"thiếu lượt"* mà không hiểu vì sao.
/// </summary>
public class LocLuotDoiSoatInput
{
    public DateTime? TuUtc { get; set; }
    public DateTime? DenUtc { get; set; }

    /// <summary>`true` = chỉ lượt **có** vượt ngưỡng (việc cần xem). `null` = tất cả.</summary>
    public bool? ChiVuotNguong { get; set; }

    /// <summary>`true` = chỉ lượt **còn** dòng lệch chưa xử lý. `null` = tất cả.</summary>
    public bool? ChiConViecChuaXuLy { get; set; }

    /// <summary>🆕 `S7` — `true` = chỉ lượt **đang chờ xác nhận** (`VuotCanXacNhan` chưa ai xác nhận).
    /// 📌 Đây là *"hộp việc"* của người có quyền `eCommerce.Reconciliation.Confirm`.</summary>
    public bool? ChiChoXacNhan { get; set; }

    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 20;
}

/// <summary>**BỘ LỌC danh sách dòng lệch** của một lượt.</summary>
public class LocDongLechInput
{
    public Guid ReconRunId { get; set; }

    /// <summary>`true` = chỉ dòng **vượt ngưỡng**. `null` = tất cả.</summary>
    public bool? ChiVuotNguong { get; set; }

    /// <summary>`true` = chỉ dòng **chưa xử lý** (mặc định của UI vận hành). `null` = tất cả.</summary>
    public bool? ChiChuaXuLy { get; set; }

    /// <summary>Lọc theo kho (`null` = mọi kho).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Tìm theo **một phần** SKU (`null` = mọi SKU).</summary>
    public string? Sku { get; set; }

    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 50;
}

/// <summary>
/// **ĐÁNH DẤU ĐÃ XỬ LÝ một dòng lệch** (`REC-001`).
/// 🔒 **Lý do và người xử lý BẮT BUỘC** — xem `ReconRunMismatch.DanhDauDaXuLy` (chênh lệch **tài sản**, cần audit).
/// </summary>
public class XuLyLechInput
{
    public string NguoiXuLy { get; set; } = default!;
    public string LyDo { get; set; } = default!;
}

/// <summary>
/// 🆕 `S7` — **XÁC NHẬN MỘT LƯỢT ĐANG CHỜ XÁC NHẬN** (quyết định `Q5` = **A**).
///
/// 🔒 **`GhiChu` BẮT BUỘC** (domain ném `Recon:0005` nếu rỗng): xác nhận = **chấp nhận chênh lệch tài sản**;
/// không có lý do thì 6 tháng sau không ai phân biệt được *"đã kiểm kê và khớp lại"* với
/// *"ai đó bấm nút cho hết danh sách"*.
/// 📌 **Ai** xác nhận thì **KHÔNG** nhận từ input — lấy từ `CurrentUser.Id` (người gõ tay tên không đối chiếu
/// được với tài khoản nào ⇒ không phải audit).
/// </summary>
public class XacNhanLuotInput
{
    /// <summary>
    /// Vì sao chấp nhận lệch — **BẮT BUỘC, nhưng KHÔNG khai `[Required]`** (📌 lý do đo được, xem dưới).
    ///
    /// 🚨 **VÌ SAO KHÔNG `[Required]`:** interceptor kiểm dữ liệu của ABP chạy **TRƯỚC** app service ⇒ ném
    /// `AbpValidationException` với thông điệp **tiếng Anh của framework** (*"The GhiChu field is required."*)
    /// và controller MVC (chỉ bắt `BusinessException`) **không** bắt được ⇒ người dùng **KHÔNG** thấy mã
    /// `eCommerce:Recon:0005` — 📌 đúng lỗi `rec-001` §18.4 mà màn hình này được làm để chống.
    /// 🔒 Vì vậy **guard của domain là nơi DUY NHẤT** quyết định *"thiếu ghi chú"*, và mã lỗi trả ra là mã
    /// **đã ghi trong tài liệu**. 📌 Cùng khuôn với `XuLyLechInput` (không có attribute nào).
    /// ⚠️ `[StringLength]` thì **giữ**: chuỗi quá dài sẽ làm hỏng ở tầng DB (`varchar(1024)`) ⇒ trả `500`.
    /// </summary>
    [System.ComponentModel.DataAnnotations.StringLength(1024)]
    public string GhiChu { get; set; } = string.Empty;
}

/// <summary>
/// 🆕 `S7` — **CHẠY DỌN LỊCH SỬ ĐỐI SOÁT THEO RETENTION** (`REC-001` §17.8 #1).
///
/// 🔒 **`ChiXemTruoc` mặc định `true`** — *fail-safe*: một lời gọi thiếu tham số **KHÔNG** được xoá dữ liệu.
/// 📌 Ngược với thói quen "mặc định là làm thật": ở đây thao tác **không hoàn tác được** trên dữ liệu là
/// **chứng từ** ⇒ mặc định phải là **chỉ đếm**.
/// </summary>
public class DonDepDoiSoatInput
{
    /// <summary>
    /// Số ngày lưu trữ (`null` = dùng setting `Recon.Retention.Days`).
    /// 🔒 `<= 0` **hoặc** `> 36500` ⇒ **NÉM** `Recon:0006` (ở tầng domain — xem `DoiSoatRetention`).
    ///
    /// 🚨 **VÌ SAO KHÔNG đặt `[Range(1, …)]`:** interceptor của ABP sẽ ném `AbpValidationException` **trước**
    /// domain ⇒ người dùng nhận thông điệp **tiếng Anh của framework** thay vì mã `0006`, và 📌 đường API
    /// trả về **một mã khác với mã đã ghi trong tài liệu**. 🔑 Ca này **đã đo được** (E2E `S7-R8` lần đầu:
    /// `POST soNgay=0` ⇒ `500`/thông điệp validation, **không** phải `0006`).
    /// </summary>
    public int? SoNgay { get; set; }

    /// <summary>`true` = **chỉ đếm**, không xoá (mặc định).</summary>
    public bool ChiXemTruoc { get; set; } = true;
}

/// <summary>Kết quả **một lượt dọn** retention — trả **số đo thật** để người bấm biết chuyện gì đã xảy ra.</summary>
public class KetQuaDonDepDoiSoatDto
{
    public int SoNgay { get; set; }
    public DateTime MocUtc { get; set; }
    public bool ChiXemTruoc { get; set; }
    public int SoDongDuDieuKien { get; set; }
    public int SoDongDaXoa { get; set; }
    public int SoDongGiuLaiChuaXuLy { get; set; }
    public int SoDongGiuLaiChoXacNhan { get; set; }
}

/// <summary>
/// 🚨 **ĐƯỜNG ĐỌC + XỬ LÝ LỆCH CỦA `REC-001`** — app service cho **người vận hành**.
///
/// **VÌ SAO PHẢI CÓ (nợ đã ghi ở `rec-001` §16.8):** entity `ReconRun`/`ReconRunMismatch` + service đối soát
/// + worker **đã có và đã chạy**, nhưng **không có đường đọc nào** ⇒ dữ liệu lệch nằm trong DB mà
/// **không ai xem được** ⇒ nó là **"ghi cho có"**. 📌 Cùng họ với `SAL-006` (hoàn tiền *đã cài* nhưng
/// **không có endpoint nào gọi tới**).
/// </summary>
public interface IDoiSoatAppService
{
    /// <summary>Lịch sử các lượt quét (mới nhất trước) — có lọc + phân trang.</summary>
    Task<PagedResultDto<LuotDoiSoatDto>> GetDanhSachLuotAsync(LocLuotDoiSoatInput input);

    /// <summary>Chi tiết một lượt (kèm ảnh chụp chính sách + số dòng lệch **chưa xử lý**).</summary>
    Task<LuotDoiSoatDto> GetLuotAsync(Guid reconRunId);

    /// <summary>Các dòng lệch của một lượt — có lọc + phân trang.</summary>
    Task<PagedResultDto<DongLechDto>> GetDanhSachDongLechAsync(LocDongLechInput input);

    /// <summary>
    /// 🚨 **Đánh dấu một dòng lệch ĐÃ XỬ LÝ** (kèm lý do + người xử lý).
    /// 📌 Đây là thao tác **duy nhất** được ghi lên bảng lệch (bảng vốn append-only) — và nó **chỉ** ghi
    /// trạng thái xử lý, **không** sửa số liệu đã đo (`OnHand`/`TongSoCai`/`ChenhLech`): sửa số liệu đo được
    /// là **phá bằng chứng**, biến lịch sử đối soát thành vô nghĩa.
    /// </summary>
    Task<DongLechDto> PostXuLyLechAsync(Guid dongLechId, XuLyLechInput input);

    /// <summary>
    /// 🆕 `S7` — **XÁC NHẬN một lượt đang chờ xác nhận** ⇒ ghi `ai/khi nào/vì sao` rồi **đóng cảnh báo**
    /// (quyết định `Q5` = **A**). 🔒 **KHÔNG** sửa tồn kho, **KHÔNG** sinh bút toán điều chỉnh, **KHÔNG**
    /// đụng tới các dòng lệch (chúng vẫn phải được xử lý riêng).
    ///
    /// 🔐 Quyền **`eCommerce.Reconciliation.Confirm`** — 📌 khác quyền xem (`Inventory.Default`) vì thao tác này
    /// làm cảnh báo **biến mất** khỏi danh sách cần xem.
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">`Recon:0004` (lượt không chờ xác nhận) · `Recon:0005`
    /// (thiếu ghi chú) · `Recon:0003` (không tìm thấy lượt).</exception>
    Task<LuotDoiSoatDto> PostXacNhanLuotAsync(Guid reconRunId, XacNhanLuotInput input);

    /// <summary>
    /// 🆕 `S7` — **DỌN LỊCH SỬ THEO RETENTION** (`REC-001` §17.8 #1). Chỉ xoá dòng lệch **ĐÃ XỬ LÝ**,
    /// **cũ hơn mốc**, và **không** thuộc lượt đang chờ xác nhận. `ReconRuns` **không bao giờ** bị xoá.
    ///
    /// 🔐 Quyền `eCommerce.Reconciliation.Confirm` (cùng quyết định `Q5`: đóng/dọn chứng từ đối soát là việc
    /// của người **chịu trách nhiệm** về cảnh báo, không phải người chỉ xem).
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">`Recon:0006` khi số ngày `<= 0`.</exception>
    Task<KetQuaDonDepDoiSoatDto> PostDonDepAsync(DonDepDoiSoatInput input);
}
