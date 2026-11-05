using System.Text.Json;
using eCommerce.Kernel.Observability;
using eCommerce.Messaging.DeadLetter;
using eCommerce.Messaging.Idempotency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;
using Volo.Abp.Users;

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
/// 🆕 **`S16b` — PHÁT LẠI THẬT (`PhatLaiAsync`):** *"replay"* ở dự án này = **publish lại event gốc lên bus**
/// (qua **outbox**, cùng transaction với dấu `ReplayedAtUtc`), **KHÔNG** phải *"đánh dấu đã xử lý"*.
/// 🔐 Quyền **riêng** `eCommerce.Operations.DeadLetters.Replay` cho hành động nguy hiểm này: người **đọc** DLQ
/// để điều tra **không** đương nhiên được **gửi lại sự kiện** vào hệ thống.
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** **KHÔNG** có **phát lại TỰ ĐỘNG** (không job, không retry) — và đó là **cố ý**:
/// phát lại một sự kiện đã hỏng **có thể gây hỏng lần nữa**, và nó **phải** là thao tác **có người** quyết định.
/// 📌 Phát lại **không** sửa dữ liệu gốc: nếu nguyên nhân là dữ liệu sai thì lần phát lại **sẽ hỏng lại** và
/// sinh **một dòng DLQ mới** (dòng cũ **vẫn** được giữ làm bằng chứng).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Operations.Default)]
public class DeadLetterAppService : ApplicationService, IDeadLetterAppService
{
    /// <summary>
    /// 🔒 **Đọc payload theo tên thuộc tính KHÔNG phân biệt hoa/thường.**
    /// 📌 Vì sao cần: nơi ghi DLQ dùng `JsonSerializer.Serialize(eventData)` (**PascalCase**), còn outbox/bus
    /// của ABP dùng **camelCase** ⇒ nếu chỉ chấp nhận một kiểu thì replay sẽ **hỏng tuỳ nơi ghi** — một lỗi
    /// *"lúc chạy mới lộ"* đúng loại dự án đang chống. 🔑 Không đổi **giá trị** payload, chỉ đổi cách **đọc**.
    /// </summary>
    private static readonly JsonSerializerOptions JsonDocPayload = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<DeadLetterMessage, Guid> _repository;
    private readonly IDeadLetterEventTypeResolver _boGiaiLoaiSuKien;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly eCommerceMetrics _metrics;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<DeadLetterAppService> _logger;

    public DeadLetterAppService(
        IRepository<DeadLetterMessage, Guid> repository,
        IDeadLetterEventTypeResolver boGiaiLoaiSuKien,
        IDistributedEventBus distributedEventBus,
        IUnitOfWorkManager unitOfWorkManager,
        eCommerceMetrics metrics,
        ICurrentUser currentUser,
        ILogger<DeadLetterAppService> logger)
    {
        _repository = repository;
        _boGiaiLoaiSuKien = boGiaiLoaiSuKien;
        _distributedEventBus = distributedEventBus;
        _unitOfWorkManager = unitOfWorkManager;
        _metrics = metrics;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// 🔒 **Người thực hiện** — dùng `ICurrentUser` **tiêm vào** (không đọc `ApplicationService.CurrentUser`)
    /// để luồng này **kiểm được bằng unit test** mà không phải dựng cả container ABP.
    /// 📌 `Name`/`UserName` có thể `null` khi chạy nền/nội bộ ⇒ **KHÔNG** thay bằng `"system"`: tên giả làm
    /// audit **sai sự thật** (nó nói *"ai đó"* trong khi thực ra **không ai**). 🔑 Ghi rõ `"(không có người dùng)"`.
    /// </summary>
    private string NguoiThucHien => _currentUser.Name ?? _currentUser.UserName ?? "(không có người dùng)";

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
                throw new BusinessException(MessagingErrorCodes.ThieuThongTinBatBuoc)
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
            ?? throw new BusinessException(MessagingErrorCodes.KhongTimThay)
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
            ?? throw new BusinessException(MessagingErrorCodes.KhongTimThay)
                .WithData("id", id)
                .WithData("lyDo", "Không tìm thấy dòng DLQ để đánh dấu.");

        // 🔒 **Bất biến ở entity**, không chỉ ở đây: `DanhDauDaXuLy` **NÉM** nếu thiếu lý do/người xử lý,
        // và **NO-OP** nếu đã xử lý. 📌 Ta **không** kiểm lại ở đây — nếu kiểm lại thì hai chỗ có thể **lệch nhau**
        // (bài học `CatalogMediaModule` §23: kiểm hai lần thì hai chỗ có thể lệch).
        ban.DanhDauDaXuLy(
            DateTime.UtcNow,
            // 📌 Người dùng lấy từ `ICurrentUser` đã tiêm — xem chú thích `NguoiThucHien`.
            nguoiXuLy: NguoiThucHien,
            lyDoXuLy: input.LyDoXuLy);

        await _repository.UpdateAsync(ban, autoSave: true);

        return Chuyen(ban);
    }

    /// <summary>
    /// 🚨 **PHÁT LẠI = PUBLISH LẠI EVENT GỐC LÊN BUS** (`S16b`) — không phải đánh dấu.
    ///
    /// **Thứ tự thực hiện (mỗi bước đều có lý do):**
    /// <list type="number">
    ///   <item>**Mở UoW riêng, có transaction** — 🔑 để dấu phát lại và dòng **outbox** cùng sống/cùng chết.
    ///     📌 Nếu dùng UoW của request thì một ngoại lệ ở bước sau có thể **đã commit** dấu (nếu nơi gọi nuốt
    ///     lỗi) ⇒ *"đã phát lại"* mà **event không đi**;</item>
    ///   <item>**Chặn phát lại lần hai TRƯỚC khi làm bất cứ gì** ⇒ trả DTO **bỏ qua** (không ném — người dùng
    ///     bấm hai lần là chuyện thường, và lần hai **không** phải lỗi hệ thống);</item>
    ///   <item>**Ánh xạ `TenSuKien` → `Type`** (fail-closed, `0009`) rồi **deserialize payload** (fail-closed,
    ///     `0010`) — 🔒 **trước** khi ghi dấu, để không bao giờ có dấu *"đã phát lại"* cho một event chưa đi;</item>
    ///   <item>**Ghi dấu `ReplayedAtUtc`** + đánh dấu đã xử lý (giữ dòng làm **bằng chứng**);</item>
    ///   <item>**`PublishAsync(..., onUnitOfWorkComplete: false, useOutbox: true)`** — 📌 `false` để việc ghi
    ///     outbox xảy ra **ngay trong** transaction này (không phải sau khi UoW đóng); `useOutbox: true` để
    ///     relay phát **sau commit** ⇒ *"không mất event"*;</item>
    ///   <item>**Commit**, rồi **mới** tăng `messaging_dlq_replayed_total`. 🚨 Tăng trước khi commit là đếm một
    ///     lần phát lại **có thể bị rollback** — đúng loại metric nói dối `XC-005` cấm.</item>
    /// </list>
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Operations.DeadLettersReplay)]
    public async Task<PhatLaiDlqKetQuaDto> PhatLaiAsync(Guid id)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        var ban = await _repository.FindAsync(id)
            ?? throw new BusinessException(MessagingErrorCodes.KhongTimThay)
                .WithData("id", id)
                .WithData("lyDo", "Không tìm thấy dòng DLQ để phát lại.");

        // 🚨 **IDEMPOTENT THEO TỪNG DÒNG**: một dòng chỉ được phát lại MỘT lần.
        // 📌 Kiểm **trước** khi ánh xạ/deserialize/publish ⇒ lần gọi thứ hai **không** phụ thuộc vào payload
        // còn đọc được hay không (payload của một dòng đã phát lại vẫn có thể bị sửa tay).
        if (ban.DaPhatLai)
        {
            return new PhatLaiDlqKetQuaDto
            {
                Id = ban.Id,
                TenSuKien = ban.TenSuKien,
                DaPhatLai = false,
                LyDo =
                    $"BỎ QUA — dòng này ĐÃ được phát lại lúc {ban.ReplayedAtUtc:O} "
                    + $"(bởi {ban.ReplayedBy ?? "(không rõ)"}). Mỗi dòng chỉ phát lại MỘT lần để không gửi trùng.",
                PhatLaiLucUtc = ban.ReplayedAtUtc,
                SoLanPhatLai = ban.ReplayCount,
                DaXuLy = ban.DaXuLy
            };
        }

        // 🚨 FAIL-CLOSED #1 — không biết payload là loại gì ⇒ NÉM (không publish "đoán").
        var loaiSuKien = _boGiaiLoaiSuKien.Giai(ban.TenSuKien);

        // 🚨 FAIL-CLOSED #2 — payload không khớp loại ⇒ NÉM (không publish một object rỗng/đoán).
        object? suKien;

        try
        {
            suKien = JsonSerializer.Deserialize(ban.PayloadJson, loaiSuKien, JsonDocPayload);
        }
        // 📌 `NotSupportedException` cũng thuộc nhóm "không tạo được object từ payload" (ví dụ loại không có
        // constructor phù hợp) ⇒ **cùng một mã lỗi**, vẫn fail-closed. 🔎 Lọc **hẹp**: không nuốt lỗi hạ tầng khác.
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new BusinessException(MessagingErrorCodes.PayloadKhongKhopLoaiSuKien)
                .WithData("id", ban.Id)
                .WithData("tenSuKien", ban.TenSuKien)
                .WithData("loai", loaiSuKien.FullName ?? loaiSuKien.Name)
                .WithData("loi", ex.Message)
                .WithData("lyDo",
                    "Payload DLQ KHÔNG deserialize được về loại sự kiện đã ghi ⇒ KHÔNG phát lại "
                    + "(gửi một event sai/rỗng còn tệ hơn không gửi: consumer xử lý dữ liệu vô nghĩa).");
        }

        if (suKien is null)
        {
            throw new BusinessException(MessagingErrorCodes.PayloadKhongKhopLoaiSuKien)
                .WithData("id", ban.Id)
                .WithData("tenSuKien", ban.TenSuKien)
                .WithData("lyDo", "Payload deserialize ra `null` ⇒ không có gì để phát lại.");
        }

        var luc = DateTime.UtcNow;
        var nguoi = NguoiThucHien;

        // 🔒 Ghi DẤU trước khi publish — cả hai nằm trong CÙNG transaction với dòng outbox.
        ban.DanhDauDaPhatLai(luc, nguoi);

        // 📌 Phát lại thành công ⇒ dòng DLQ **đã được xử lý** (nhưng **KHÔNG xoá** — giữ bằng chứng).
        // 🔑 `DanhDauDaXuLy` là NO-OP nếu trước đó đã đánh dấu ⇒ **không ghi đè** bằng chứng cũ.
        ban.DanhDauDaXuLy(
            luc,
            nguoi,
            $"Phát lại lên bus thành công lúc {luc:O} (một lần duy nhất cho mỗi dòng DLQ).");

        await _repository.UpdateAsync(ban, autoSave: true);

        // 🚨 `onUnitOfWorkComplete: false` ⇒ ghi outbox NGAY trong UoW này (cùng transaction với dấu ở trên).
        // 🚨 `useOutbox: true` ⇒ relay phát sau commit; process chết sau commit vẫn còn dòng outbox ⇒ KHÔNG mất.
        await _distributedEventBus.PublishAsync(
            loaiSuKien,
            suKien,
            onUnitOfWorkComplete: false,
            useOutbox: true);

        await uow.CompleteAsync();

        // 🔒 CHỈ SAU commit mới đếm — nếu outbox/commit hỏng thì metric **không** tăng.
        // 📌 Nhãn `eventName` hữu hạn (một loại sự kiện = một giá trị); `result` khớp bảng đăng ký `XC-005` §5.1.
        _metrics.MessagingDlqReplayed.Add(
            1,
            new KeyValuePair<string, object?>(eCommerceMetricTags.EventName, ban.TenSuKien),
            new KeyValuePair<string, object?>(eCommerceMetricTags.Result, "ok"));

        _logger.LogInformation(
            "PHAT LAI DLQ: id={Id} · suKien={TenSuKien} · khoa={Khoa} · nguoi={Nguoi}. [DLQ-PHAT-LAI]",
            ban.Id, ban.TenSuKien, ban.KhoaNghiepVu, nguoi);

        return new PhatLaiDlqKetQuaDto
        {
            Id = ban.Id,
            TenSuKien = ban.TenSuKien,
            DaPhatLai = true,
            LyDo = "ĐÃ PHÁT LẠI lên bus (qua outbox, cùng transaction với dấu phát lại).",
            PhatLaiLucUtc = ban.ReplayedAtUtc,
            SoLanPhatLai = ban.ReplayCount,
            DaXuLy = ban.DaXuLy
        };
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
