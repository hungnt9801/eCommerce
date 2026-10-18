using eCommerce.Reconciliation;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **CHÍNH SÁCH LƯU TRỮ (RETENTION) CỦA LỊCH SỬ ĐỐI SOÁT** (`REC-001` §17.8 **#1** — nợ *"bảng chỉ tăng"*).
///
/// **VẤN ĐỀ THẬT:** `ReconRuns` + `ReconRunMismatches` **chỉ tăng**. Job quét 15 phút/lần ⇒ một năm là
/// ~35 000 lượt; mỗi lượt có thể có hàng trăm dòng lệch. Không có chính sách dọn ⇒ hai hệ quả **đo được**:
/// <list type="bullet">
///   <item>bảng phình ⇒ truy vấn *"lượt này còn việc gì"* (đường dùng nhiều nhất) **chậm dần** mà không ai
///     quy cho bảng nào;</item>
///   <item>📌 và tệ hơn: người vận hành **mở màn hình** thấy toàn lịch sử ⇒ **không đọc nữa** (đúng số phận
///     của mọi báo cáo không có chính sách lưu trữ).</item>
/// </list>
///
/// 🔒 **RANH GIỚI ĐƯỢC VẼ RẤT HẸP — VÀ ĐÓ LÀ ĐIỂM QUAN TRỌNG NHẤT (đây là chứng từ, không phải log):**
/// <list type="number">
///   <item><b>`ReconRuns` KHÔNG BAO GIỜ bị xoá</b> — đó là **bản tổng hợp** trả lời *"job có chạy"* và
///     *"lượt đó chấm theo ngưỡng nào"*. Xoá nó là **làm mất câu trả lời** cho câu hỏi duy nhất mà bảng sinh ra
///     để trả lời (xem chú thích `ReconRun`);</item>
///   <item><b>Dòng lệch CHƯA xử lý KHÔNG bị xoá</b> — đó là **việc còn nợ**. Dọn nó = **dọn mất việc**;</item>
///   <item><b>Dòng lệch của lượt ĐANG CHỜ XÁC NHẬN KHÔNG bị xoá</b> — cảnh báo chưa được người có quyền đóng
///     (xem `ReconRun.XacNhan`) ⇒ xoá bằng chứng của nó là **đóng cảnh báo bằng cách im lặng**;</item>
///   <item>🔒 **`SoNgay <= 0` = TẮT**, và đường dọn **NÉM** (`Recon:0006`) chứ **không** hiểu là *"xoá hết"*:
///     một lần gõ `0` vào ô *"số ngày"* mà xoá sạch lịch sử thì **không có lỗi nào** để lần ra.</item>
/// </list>
/// 📌 Toàn bộ **luật** ở đây là **hàm THUẦN** (không DB) ⇒ test được từng nhánh mà không cần fixture.
/// </summary>
public static class DoiSoatRetention
{
    /// <summary>
    /// **MẶC ĐỊNH 180 NGÀY** — 📌 chọn theo *"đủ dài để đối chiếu một kỳ kế toán (≥ 1 quý, có dư) mà đủ ngắn
    /// để bảng không phình vô hạn"*. ⚠️ Đây là **giá trị mặc định của setting**, người vận hành đổi được
    /// (`Recon.Retention.Days`) — không phải hằng số trong mã.
    /// </summary>
    public const int SoNgayMacDinh = 180;

    /// <summary>
    /// **Mốc cắt tối đa** — 100 năm. 📌 Trần này chỉ để chặn ca **gõ sai dấu** (`soNgay = 365000`) biến thành
    /// *"xoá toàn bộ lịch sử"*; nó **không** phải chính sách.
    /// </summary>
    public const int SoNgayToiDa = 36_500;

    /// <summary>
    /// Mốc cắt: dòng lệch **tạo trước** mốc này mới được xem xét dọn.
    /// 🔒 **NÉM** `Recon:0006` khi `soNgay <= 0` (xem lý do ở chú thích lớp — `0` là **TẮT**, không phải *"xoá hết"*)
    /// hoặc khi `soNgay > <see cref="SoNgayToiDa"/>` (chặn gõ sai thành *"xoá toàn bộ"*).
    /// </summary>
    public static DateTime TinhMocUtc(DateTime nowUtc, int soNgay)
    {
        if (soNgay <= 0 || soNgay > SoNgayToiDa)
        {
            throw new BusinessException(ReconErrorCodes.ChinhSachLuuTruKhongHopLe)
                .WithData("soNgay", soNgay)
                .WithData("soNgayToiDa", SoNgayToiDa)
                .WithData("lyDo", "Số ngày lưu trữ phải > 0 và <= " + SoNgayToiDa +
                    ". `0` nghĩa là TẮT retention — không phải \"xoá hết\".");
        }

        return nowUtc.AddDays(-soNgay);
    }

    /// <summary>
    /// **MỘT dòng lệch có được dọn không** — ba điều kiện, mỗi điều ứng với một lý do ở chú thích lớp.
    /// </summary>
    /// <param name="ngayTaoUtc">`CreationTime` của dòng lệch (UTC).</param>
    /// <param name="mocUtc">Mốc cắt từ <see cref="TinhMocUtc"/>.</param>
    /// <param name="daXuLy">Dòng đã có người xử lý chưa (audit `DaXuLy`).</param>
    /// <param name="luotDangChoXacNhan">Lượt chứa dòng này có đang chờ người xác nhận không (`ReconRun.ChoXacNhan`).</param>
    public static bool DuocDonDep(DateTime ngayTaoUtc, DateTime mocUtc, bool daXuLy, bool luotDangChoXacNhan)
    {
        if (!daXuLy)
        {
            // Việc còn nợ: không dọn. 📌 Xoá nó là làm màn hình *"sạch"* trong khi lệch **vẫn còn**.
            return false;
        }

        if (luotDangChoXacNhan)
        {
            // Cảnh báo chưa được người có quyền đóng ⇒ giữ bằng chứng.
            return false;
        }

        return ngayTaoUtc < mocUtc;
    }
}

/// <summary>Kết quả MỘT lượt dọn retention — trả **số đo thật** (xem / trước và sau khi xoá).</summary>
public class KetQuaDonDepDoiSoat
{
    /// <summary>Số ngày lưu trữ đã áp.</summary>
    public int SoNgay { get; init; }

    /// <summary>Mốc cắt (UTC) — dòng lệch tạo **trước** mốc này mới được xem xét.</summary>
    public DateTime MocUtc { get; init; }

    /// <summary>`true` = **chỉ xem trước**, KHÔNG xoá gì.</summary>
    public bool ChiXemTruoc { get; init; }

    /// <summary>Số dòng lệch **đủ điều kiện dọn** (và đã bị xoá nếu không phải *xem trước*).</summary>
    public int SoDongDuDieuKien { get; init; }

    /// <summary>Số dòng lệch **đã xoá thật** (`0` khi *xem trước*).</summary>
    public int SoDongDaXoa { get; init; }

    /// <summary>Giữ lại vì **CHƯA xử lý** (việc còn nợ) — 📌 trả ra để người vận hành biết vì sao bảng còn dòng.</summary>
    public int SoDongGiuLaiChuaXuLy { get; init; }

    /// <summary>Giữ lại vì lượt **đang chờ xác nhận** — 📌 con số này giảm khi có người bấm *"xác nhận"*.</summary>
    public int SoDongGiuLaiChoXacNhan { get; init; }

    /// <summary>
    /// Giữ lại vì **CHƯA tới mốc** (mới hơn `soNgay`). 🆕 Thêm sau **một phép đo sai**: bản đầu gộp nhóm này
    /// vào `SoDongGiuLaiChoXacNhan` ⇒ báo cáo nói *"giữ lại vì chờ xác nhận"* trong khi **không có lượt nào**
    /// chờ xác nhận — 📌 người vận hành sẽ đi **xác nhận những lượt không tồn tại**, hoặc tệ hơn: **tin** là
    /// retention không chạy.
    /// </summary>
    public int SoDongGiuLaiMoiHonMoc { get; init; }
}

/// <summary>
/// **DỊCH VỤ DỌN LỊCH SỬ ĐỐI SOÁT** — dùng chung cho **job định kỳ** (`ReconRetentionWorker`) và
/// **đường bấm tay của người vận hành** (`IDoiSoatAppService.PostDonDepAsync`).
///
/// 🔒 **VÌ SAO PHẢI DÙNG CHUNG MỘT chỗ:** nếu job và API mỗi bên tự viết truy vấn riêng thì hai nơi sẽ
/// **lệch luật** (một nơi quên điều kiện *"không xoá việc chưa xử lý"*) — và ca hỏng đó **không có triệu chứng**
/// cho tới khi có người đi tìm dữ liệu đã mất. 📌 Đây đúng bài học *"hai nguồn sự thật"* của dự án.
///
/// ⚠️ **Dịch vụ này KHÔNG mở `UnitOfWork`:** chỗ gọi tự mở (app service có UoW của ABP; worker mở tường minh).
/// </summary>
public class DoiSoatRetentionService : DomainService
{
    private readonly IRepository<ReconRun, Guid> _luot;
    private readonly IRepository<ReconRunMismatch, Guid> _dongLech;
    private readonly IUnitOfWorkManager _uow;

    public DoiSoatRetentionService(
        IRepository<ReconRun, Guid> luot,
        IRepository<ReconRunMismatch, Guid> dongLech,
        IUnitOfWorkManager uow)
    {
        _luot = luot;
        _dongLech = dongLech;
        _uow = uow;
    }

    /// <summary>
    /// Dọn (hoặc **chỉ đếm** khi `chiXemTruoc = true`) các dòng lệch **đã xử lý** cũ hơn `soNgay`.
    ///
    /// 📌 **VÌ SAO NẠP ỨNG VIÊN VÀO BỘ NHỚ RỒI LỌC BẰNG `DoiSoatRetention.DuocDonDep`** (thay vì viết
    /// `WHERE` tương đương): để **luật chỉ có MỘT chỗ** và chỗ đó **test được không cần DB**. ⚠️ Đánh đổi đã biết:
    /// lượt dọn **đầu tiên** trên dữ liệu nhiều năm sẽ nạp nhiều dòng; các lượt sau chỉ còn phần mới quá hạn
    /// (job chạy hằng ngày). 🔑 Nếu sau này dữ liệu lớn tới mức đó thì **phải** chuyển sang `DELETE … WHERE`
    /// **và** chép luật sang SQL **kèm test đối chiếu hai bản** — chưa làm bây giờ vì sẽ tạo **hai nguồn sự thật**
    /// cho một luật đang đúng.
    ///
    /// 🚨 **XOÁ CỨNG (`HardDeleteManyAsync`), KHÔNG xoá mềm** — đây là chỗ **đã đo được** là sai ở bản đầu:
    /// `ReconRunMismatch` là `FullAuditedEntity` (có `ISoftDelete`) ⇒ `DeleteManyAsync` chỉ **đặt `IsDeleted`**
    /// và **dòng vẫn nằm trong bảng** ⇒ 📌 retention *"trông chạy"* (màn hình sạch) nhưng **bảng vẫn phình mãi**
    /// — đúng cái nợ mà feature này sinh ra để đóng. 🔒 Và nếu ai đó đọc SQL trực tiếp sẽ thấy dữ liệu
    /// *"đã bị xoá"* mà vẫn còn.
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">`Recon:0006` khi `soNgay <= 0` hoặc quá lớn.</exception>
    public async Task<KetQuaDonDepDoiSoat> DonDepAsync(DateTime nowUtc, int soNgay, bool chiXemTruoc)
    {
        // 🔒 NÉM trước khi chạm DB: `<= 0` là TẮT (xem `DoiSoatRetention`).
        var moc = DoiSoatRetention.TinhMocUtc(nowUtc, soNgay);

        // ① Các lượt ĐANG CHỜ XÁC NHẬN — chỉ để GIỮ LẠI bằng chứng của chúng.
        var luotChoXacNhan = await AsyncExecuter.ToListAsync(
            (await _luot.GetQueryableAsync())
                .Where(x => x.KetLuan == KetLuanDoiSoat.VuotCanXacNhan && x.ConfirmedAtUtc == null)
                .Select(x => x.Id));

        var tapChoXacNhan = luotChoXacNhan.ToHashSet();

        var truyVanCu = (await _dongLech.GetQueryableAsync()).Where(x => x.CreationTime < moc);

        // 🚨 **BA CON SỐ TÁCH RIÊNG** (bản đầu gộp nhóm ③ vào ② ⇒ báo cáo NÓI SAI lý do giữ lại).
        var soChuaXuLy = await AsyncExecuter.CountAsync(truyVanCu.Where(x => !x.DaXuLy));

        var soChoXacNhan = await AsyncExecuter.CountAsync(
            truyVanCu.Where(x => x.DaXuLy && tapChoXacNhan.Contains(x.ReconRunId)));

        var dongDaXuLy = await AsyncExecuter.ToListAsync(truyVanCu.Where(x => x.DaXuLy));

        var duDieuKien = dongDaXuLy
            .Where(x => DoiSoatRetention.DuocDonDep(x.CreationTime, moc, x.DaXuLy, tapChoXacNhan.Contains(x.ReconRunId)))
            .ToList();

        var soMoiHonMoc = await AsyncExecuter.CountAsync(
            (await _dongLech.GetQueryableAsync()).Where(x => x.DaXuLy && x.CreationTime >= moc));

        if (!chiXemTruoc && duDieuKien.Count > 0)
        {
            // 🔒 XOÁ CỨNG — xem chú thích ở trên (đây là chỗ bản đầu SAI: xoá mềm ⇒ bảng vẫn phình).
            // ⚠️ ABP **không** có `HardDeleteManyAsync` trên `IRepository<T,TKey>` (đã đo: `CS1061`) ⇒ xoá
            // từng dòng với `autoSave: false` rồi **một** lần lưu ⇒ một transaction, không N lần round-trip.
            foreach (var dong in duDieuKien)
            {
                await _dongLech.HardDeleteAsync(dong, autoSave: false);
            }

            await _uow.Current!.SaveChangesAsync();
        }

        return new KetQuaDonDepDoiSoat
        {
            SoNgay = soNgay,
            MocUtc = moc,
            ChiXemTruoc = chiXemTruoc,
            SoDongDuDieuKien = duDieuKien.Count,
            SoDongDaXoa = chiXemTruoc ? 0 : duDieuKien.Count,
            SoDongGiuLaiChuaXuLy = soChuaXuLy,
            SoDongGiuLaiChoXacNhan = soChoXacNhan,
            SoDongGiuLaiMoiHonMoc = soMoiHonMoc
        };
    }
}
