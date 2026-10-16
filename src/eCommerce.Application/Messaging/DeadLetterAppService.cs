using eCommerce.Messaging.DeadLetter;
using eCommerce.Messaging.Idempotency;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Messaging;

/// <summary>
/// 🚨 **ĐỌC + XỬ LÝ HÀNG ĐỢI THƯ CHẾT** (`XC-002`) — hiện thực `IDeadLetterAppService`.
///
/// 🔑 **VÌ SAO LỚP NÀY LÀ ĐIỀU KIỆN ĐỂ DLQ CÓ NGHĨA:** lượt trước DLQ **ghi được**, nhưng
/// 🚨 **một hàng đợi không bao giờ được đọc thì TỆ NHƯ KHÔNG CÓ** — nó chỉ tốn đĩa và tạo **cảm giác** rằng
/// sự kiện đã được giữ. 📌 Cụ thể: `XungDot`/`DenMuon` **đẩy DLQ rồi dừng**; nếu không ai đọc thì kết quả
/// **giống hệt** việc nuốt im lặng, chỉ khác là **tốn thêm đĩa**.
///
/// 🔐 `[Authorize]` ở **CẤP CLASS** (quy ước #4, `BatBienPhanQuyenTests.TC01`). 📌 Quyền **riêng**
/// (`Operations.DeadLetters`), **không** mượn `Inventory`: DLQ chứa **payload sự kiện** ⇒ đọc nó là một quyền
/// **riêng** (đặc quyền tối thiểu).
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** lớp này **CHỈ ĐỌC + ĐÁNH DẤU**. 🚨 **KHÔNG** có **phát lại tự động** — và đó là
/// **cố ý**: phát lại một sự kiện đã hỏng **có thể gây hỏng lần nữa**, và nó **phải** là thao tác **có người**
/// quyết định. 📌 Người trực đọc payload ở `GetChiTietAsync` rồi xử lý thủ công (hoặc sửa dữ liệu gốc).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Operations.Default)]
public class DeadLetterAppService : ApplicationService, IDeadLetterAppService
{
    private readonly IRepository<DeadLetterMessage, Guid> _repository;

    public DeadLetterAppService(IRepository<DeadLetterMessage, Guid> repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<DlqDto>> GetDanhSachAsync(LocDlqInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var truyVan = await _repository.GetQueryableAsync();

        // 🔑 **MẶC ĐỊNH CHỈ DÒNG CHƯA XỬ LÝ** — xem chú thích `LocDlqInput.DaXuLy`.
        // 📌 `null` ⇒ chỉ chưa xử lý · `true` ⇒ chỉ đã xử lý · `false` ⇒ tất cả.
        // ⚠️ Ba giá trị, **không** phải hai: nếu chỉ có `bool` thì **không** diễn đạt được *"tất cả"*
        // và người dùng buộc phải gọi hai lần để thấy đủ.
        truyVan = input.DaXuLy switch
        {
            null => truyVan.Where(x => !x.DaXuLy),
            true => truyVan.Where(x => x.DaXuLy),
            false => truyVan
        };

        if (!string.IsNullOrWhiteSpace(input.KhoaNghiepVu))
        {
            var khoa = input.KhoaNghiepVu.Trim();
            truyVan = truyVan.Where(x => x.KhoaNghiepVu.Contains(khoa));
        }

        if (!string.IsNullOrWhiteSpace(input.TenSuKien))
        {
            var ten = input.TenSuKien.Trim();
            truyVan = truyVan.Where(x => x.TenSuKien.Contains(ten));
        }

        if (!string.IsNullOrWhiteSpace(input.LyDo))
        {
            // 🔒 Parse **ở đây** (không nhận enum ở DTO — xem chú thích `LocDlqInput.LyDo`).
            // 📌 Sai tên ⇒ **NÉM** (không im lặng bỏ qua bộ lọc): im lặng bỏ qua sẽ trả **nhiều hơn** mong đợi
            // và người dùng tưởng bộ lọc đã áp — rồi **kết luận sai** về tình hình DLQ.
            if (!Enum.TryParse<LyDoVaoDlq>(input.LyDo.Trim(), ignoreCase: true, out var lyDo))
            {
                throw new Volo.Abp.BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
                    .WithData("truong", nameof(input.LyDo))
                    .WithData("giaTri", input.LyDo)
                    .WithData("hopLe", string.Join(", ", Enum.GetNames<LyDoVaoDlq>()))
                    .WithData("lyDo", "Tên lý do không hợp lệ ⇒ bộ lọc SAI nếu bỏ qua im lặng.");
            }

            truyVan = truyVan.Where(x => x.LyDo == lyDo);
        }

        // 📌 Sắp xếp: **cũ nhất trước** — 🔒 vì câu hỏi vận hành là *"cái gì đang chờ LÂU NHẤT"*.
        // ⚠️ Mặc định `descending` (mới nhất trước) của ABP sẽ **che** đúng những dòng cần xử lý gấp nhất.
        truyVan = truyVan.OrderBy(x => x.VaoDlqLucUtc);

        var tong = await AsyncExecuter.CountAsync(truyVan);

        // 🔒 Trần cứng: `Contains` trên chuỗi + payload lớn ⇒ không cho client kéo **cả bảng**.
        var soLuong = Math.Clamp(input.MaxResultCount, 1, eCommerceConsts.OperationsLimits.TranDongDlqMotTrang);
        var boQua = Math.Max(input.SkipCount, 0);

        var dong = await AsyncExecuter.ToListAsync(truyVan.Skip(boQua).Take(soLuong));

        return new PagedResultDto<DlqDto>(tong, dong.Select(Chuyen).ToList());
    }

    /// <inheritdoc />
    public async Task<ChiTietDlqDto> GetChiTietAsync(Guid id)
    {
        var ban = await _repository.FindAsync(id)
            // 🔒 **NÉM, không trả `null`**: ABP trả `204` cho `null` ⇒ client **không phân biệt** được
            // *"không tồn tại"* với *"không có nội dung"* ⇒ và với DLQ thì *"không tồn tại"* là thông tin
            // **quan trọng** (dòng có thể đã bị xoá/không thuộc tenant này).
            ?? throw new Volo.Abp.BusinessException(MessagingErrorCodes.KhongTimThay)
                .WithData("id", id)
                .WithData("lyDo", "Không tìm thấy dòng DLQ (có thể đã bị xoá, hoặc thuộc tenant khác).");

        return new ChiTietDlqDto
        {
            Id = ban.Id,
            TenSuKien = ban.TenSuKien,
            KhoaNghiepVu = ban.KhoaNghiepVu,
            LyDo = ban.LyDo.ToString(),
            MoTaLyDo = ban.MoTaLyDo,
            Loi = ban.Loi,
            VaoDlqLucUtc = ban.VaoDlqLucUtc,
            CorrelationId = ban.CorrelationId,
            DaXuLy = ban.DaXuLy,
            XuLyLucUtc = ban.XuLyLucUtc,
            NguoiXuLy = ban.NguoiXuLy,
            LyDoXuLy = ban.LyDoXuLy,
            PayloadHash = ban.PayloadHash,
            PayloadJson = ban.PayloadJson
        };
    }

    /// <inheritdoc />
    public async Task<DlqDto> PostDanhDauDaXuLyAsync(Guid id, DanhDauXuLyDlqInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ban = await _repository.FindAsync(id)
            ?? throw new Volo.Abp.BusinessException(MessagingErrorCodes.KhongTimThay)
                .WithData("id", id)
                .WithData("lyDo", "Không tìm thấy dòng DLQ để đánh dấu.");

        // 🔒 **Bất biến ở entity**, không chỉ ở đây: `DanhDauDaXuLy` **NÉM** nếu thiếu lý do/người xử lý,
        // và **NO-OP** nếu đã xử lý. 📌 Ta **không** kiểm lại ở đây — nếu kiểm lại thì hai chỗ có thể **lệch nhau**
        // (bài học `CatalogMediaModule` §23: kiểm hai lần thì hai chỗ có thể lệch).
        ban.DanhDauDaXuLy(
            DateTime.UtcNow,
            // 📌 `CurrentUser.Name` có thể `null` khi chạy nền/nội bộ ⇒ dùng `User` khác rỗng, và
            // ⚠️ **KHÔNG** thay bằng tên giả (`"system"` sẽ làm audit **sai sự thật**: nó nói *"ai đó"* trong khi
            // thực ra **không ai**). 🔑 Ghi rõ `"(không có người dùng)"` để **lộ** ca bất thường ra.
            nguoiXuLy: CurrentUser.Name ?? CurrentUser.UserName ?? "(không có người dùng)",
            lyDoXuLy: input.LyDoXuLy);

        await _repository.UpdateAsync(ban, autoSave: true);

        return Chuyen(ban);
    }

    /// <inheritdoc />
    public async Task<ThongKeDlqDto> GetThongKeAsync()
    {
        var truyVan = await _repository.GetQueryableAsync();

        var soChuaXuLy = await AsyncExecuter.CountAsync(truyVan.Where(x => !x.DaXuLy));
        var soDaXuLy = await AsyncExecuter.CountAsync(truyVan.Where(x => x.DaXuLy));

        // 🚨 **Mốc CŨ NHẤT trong các dòng chưa xử lý — con số quan trọng nhất của lớp này.**
        // 📌 Một dòng nằm đó **ba tuần** là dấu hiệu **không ai đọc DLQ** — mà đó **chính là** thất bại
        // mà cả `XC-002` tồn tại để tránh. 🔑 Một hàng đợi chỉ có *"số lượng"* mà không có *"tuổi"* thì
        // **không** phát hiện được điều đó: 5 dòng mới trong 1 giờ là **bình thường**, 5 dòng từ 3 tuần là **báo động**.
        var cuNhat = await AsyncExecuter.FirstOrDefaultAsync(
            truyVan.Where(x => !x.DaXuLy).OrderBy(x => x.VaoDlqLucUtc).Select(x => (DateTime?)x.VaoDlqLucUtc));

        // 📌 Đếm theo lý do: gom **trong bộ nhớ** sau khi chỉ lấy hai cột cần thiết — 🔑 nếu gom bằng
        // `GroupBy` dịch sang SQL thì provider có thể sinh truy vấn **nặng hơn** mà lợi ích không đáng,
        // vì số dòng CHƯA xử lý **được thiết kế để nhỏ** (index có bộ lọc `NOT "DaXuLy"`).
        var capLyDo = await AsyncExecuter.ToListAsync(
            truyVan.Where(x => !x.DaXuLy).Select(x => x.LyDo));

        var theoLyDo = capLyDo
            .GroupBy(x => x.ToString())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return new ThongKeDlqDto
        {
            SoChuaXuLy = soChuaXuLy,
            SoDaXuLy = soDaXuLy,
            CuNhatChuaXuLyUtc = cuNhat,
            TheoLyDo = theoLyDo
        };
    }

    private static DlqDto Chuyen(DeadLetterMessage ban) => new()
    {
        Id = ban.Id,
        TenSuKien = ban.TenSuKien,
        KhoaNghiepVu = ban.KhoaNghiepVu,
        LyDo = ban.LyDo.ToString(),
        MoTaLyDo = ban.MoTaLyDo,
        Loi = ban.Loi,
        VaoDlqLucUtc = ban.VaoDlqLucUtc,
        CorrelationId = ban.CorrelationId,
        DaXuLy = ban.DaXuLy,
        XuLyLucUtc = ban.XuLyLucUtc,
        NguoiXuLy = ban.NguoiXuLy,
        LyDoXuLy = ban.LyDoXuLy,
        PayloadHash = ban.PayloadHash
    };
}
