namespace eCommerce.Customers;

/// <summary>
/// 🧮 **HÀM THUẦN của nghiệp vụ khách** — `WP-07` (`07 §WP-07`, DoD: *"tiền tích luỹ đúng công thức"*).
///
/// **VÌ SAO TÁCH THÀNH HÀM THUẦN (không viết thẳng trong entity/app service):**
/// <list type="number">
///   <item>Ba đại lượng dưới đây **cộng dồn qua nhiều chứng từ** (hoá đơn, phiếu trả, huỷ) — sai một phép
///     cộng là **số dư của khách sai vĩnh viễn**, và nó **không ném lỗi**: không ai biết cho tới khi khách
///     khiếu nại hoặc kế toán đối chiếu. 📌 Hàm thuần cho phép test **mọi tổ hợp** (bán/trả/huỷ/âm/vượt)
///     mà không cần DB.</item>
///   <item>Cùng một công thức được dùng ở **hai** đường ghi: `POS` (`WP-06` đã ghi hoá đơn) và **đường
///     trả/huỷ**. Hai bản sao của công thức là **hai nguồn sự thật** (`XC-003` F2) — đúng thứ handover
///     `N7` §6 cảnh báo: *"gộp điểm/công nợ vào `RetailBill.HoanTat` (một transaction) — đừng ghi sổ
///     điểm ở FE"*.</item>
/// </list>
/// </summary>
public static class NghiepVuKhach
{
    /// <summary>
    /// **SỐ ĐIỂM SAU GIAO DỊCH** = điểm hiện có + điểm tích − điểm tiêu.
    ///
    /// 🚨 **ÂM ⇒ NÉM:** điểm là **quyền đổi thành tiền** (`TinhTienHoaDon.QuyDoiDiem`). Cho phép âm nghĩa là
    /// khách **tiêu điểm chưa kiếm được** — một khoản nợ ẩn không có chứng từ, và lần mua sau sẽ **trừ bù**
    /// mà không ai giải thích được vì sao. 📌 Chặn ở **domain** (không chỉ UI) vì API/offline không đi qua UI.
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">`eCommerce:Customers:0017` khi kết quả âm.</exception>
    public static decimal DiemSauGiaoDich(decimal diemHienTai, decimal diemTich, decimal diemSuDung)
    {
        if (diemTich < 0m)
        {
            throw new Volo.Abp.BusinessException(CustomerErrorCodes.DiemKhongHopLe)
                .WithData("diemTich", diemTich)
                .WithData("lyDo", "Điểm TÍCH của một giao dịch KHÔNG được âm.");
        }

        if (diemSuDung < 0m)
        {
            throw new Volo.Abp.BusinessException(CustomerErrorCodes.DiemKhongHopLe)
                .WithData("diemSuDung", diemSuDung)
                .WithData("lyDo", "Điểm TIÊU KHÔNG được âm.");
        }

        var diemMoi = diemHienTai + diemTich - diemSuDung;

        if (diemMoi < 0m)
        {
            throw new Volo.Abp.BusinessException(CustomerErrorCodes.DiemKhongDu)
                .WithData("diemHienTai", diemHienTai)
                .WithData("diemSuDung", diemSuDung)
                .WithData("diemTich", diemTich)
                .WithData("lyDo", "Khách KHÔNG đủ điểm để tiêu (điểm sau giao dịch sẽ âm).");
        }

        return LamTron(diemMoi);
    }

    /// <summary>
    /// **CÔNG NỢ SAU GIAO DỊCH** = nợ hiện tại + nợ phát sinh.
    ///
    /// 🚨 **VƯỢT HẠN MỨC ⇒ NÉM:** `DebtLimit > 0` là **cam kết tín dụng** của chủ doanh nghiệp với khách.
    /// Vượt mà vẫn bán nghĩa là hệ thống **âm thầm cho vay** quá hạn mức đã đặt — rủi ro không đòi được,
    /// và không có cảnh báo nào. 📌 `DebtLimit == 0` = **không đặt hạn mức** (không chặn) — phân biệt
    /// *"hạn mức 0 đồng"* (chặn mọi khoản nợ) với *"chưa đặt"* là **quan trọng**: dùng chung một số `0`
    /// sẽ làm mọi khách bị chặn nợ sau khi nâng cấp.
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">`eCommerce:Customers:0019` khi vượt hạn mức.</exception>
    public static decimal NoSauGiaoDich(decimal noHienTai, decimal noTang, decimal hanMucNo)
    {
        if (noTang < 0m)
        {
            throw new Volo.Abp.BusinessException(CustomerErrorCodes.SoTienKhongHopLe)
                .WithData("noTang", noTang)
                .WithData("lyDo", "Khoản nợ phát sinh KHÔNG được âm (giảm nợ đi đường riêng).");
        }

        if (hanMucNo < 0m)
        {
            throw new Volo.Abp.BusinessException(CustomerErrorCodes.SoTienKhongHopLe)
                .WithData("hanMucNo", hanMucNo)
                .WithData("lyDo", "Hạn mức nợ KHÔNG được âm.");
        }

        var noMoi = LamTron(noHienTai + noTang);

        if (hanMucNo > 0m && noMoi > hanMucNo)
        {
            throw new Volo.Abp.BusinessException(CustomerErrorCodes.VuotHanMucNo)
                .WithData("noHienTai", noHienTai)
                .WithData("noTang", noTang)
                .WithData("hanMucNo", hanMucNo)
                .WithData("noMoi", noMoi)
                .WithData("lyDo", "Khách vượt HẠN MỨC NỢ đã đặt ⇒ thu bớt tiền hoặc nâng hạn mức có căn cứ.");
        }

        return noMoi;
    }

    /// <summary>
    /// **CHỌN CẤP KHÁCH theo tiền tích luỹ** — trả `Id` của cấp có `MinAccumulatedAmount` **lớn nhất
    /// mà ≤ tiền tích luỹ**, hoặc `null` nếu chưa đạt cấp nào.
    ///
    /// 📌 **SẮP XẾP TẤT ĐỊNH:** cùng một mức tiền mà hai cấp cùng ngưỡng (dữ liệu cấu hình trùng) thì chọn
    /// theo `SortOrder` rồi `Id` — nếu không, cấp của khách **đổi mỗi lần đọc** (tuỳ thứ tự DB trả) và
    /// báo cáo *"bao nhiêu khách VIP"* sẽ **nhảy số** mà không có giao dịch nào.
    /// </summary>
    public static Guid? ChonCapKhach(IReadOnlyList<NguongCapKhach> cacCap, decimal tienTichLuy)
    {
        if (cacCap.Count == 0)
        {
            return null;
        }

        return cacCap
            .Where(x => x.MinAccumulatedAmount <= tienTichLuy)
            .OrderByDescending(x => x.MinAccumulatedAmount)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefault();
    }

    /// <summary>Một ngưỡng cấp khách — **kiểu thuần** để hàm chọn cấp không phụ thuộc entity/EF.</summary>
    public sealed record NguongCapKhach(Guid Id, decimal MinAccumulatedAmount, int SortOrder);

    /// <summary>Làm tròn **2 số lẻ** (tiền) — cùng quy ước `TinhTienHoaDon` (làm tròn ở từng dòng rồi mới cộng).</summary>
    public static decimal LamTron(decimal so) => Math.Round(so, 2, MidpointRounding.AwayFromZero);
}
