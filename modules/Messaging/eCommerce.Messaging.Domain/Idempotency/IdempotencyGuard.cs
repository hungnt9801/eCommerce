using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Timing;

namespace eCommerce.Messaging.Idempotency;

/// <summary>
/// **Hợp đồng chống trùng ở MỌI biên vào** — dùng chung cho webhook sàn, IPN cổng thanh toán,
/// webhook hãng vận chuyển, import file, và API tạo đơn có `Idempotency-Key`.
///
/// 🎯 CÁCH DÙNG ĐÚNG (bọc toàn bộ xử lý, KHÔNG chỉ kiểm tra trước):
/// <code>
/// var kq = await _guard.ThucThiAsync(
///     IdempotencyKey.Cua("vnpay:ipn", maGiaoDich),
///     payloadJson,
///     async ct => { /* xử lý thật */ return ketQuaJson; });
///
/// if (kq.LaPhatLai) return Ok(kq.Result);                    // đã xử lý trước đó
/// if (kq.Kind == IdempotencyOutcomeKind.DangXuLy) return Conflict();   // 409 (XC-003 Q4)
/// </code>
///
/// 🚨 SAI LẦM KINH ĐIỂN CẦN TRÁNH: kiểm tra "đã xử lý chưa" **rồi** mới xử lý ở hai bước rời nhau.
/// Hai yêu cầu đến **đồng thời** sẽ cùng thấy "chưa xử lý" và **cùng xử lý** ⇒ trừ kho hai lần.
/// Vì vậy toàn bộ việc kiểm tra + xử lý phải nằm **trong một lần gọi** và được **tuần tự hoá bằng
/// khoá phân tán**.
/// </summary>
public interface IIdempotencyGuard
{
    Task<IdempotencyOutcome> ThucThiAsync(
        IdempotencyKey key,
        string? payloadJson,
        Func<CancellationToken, Task<string?>> xuLy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Hiện thực <see cref="IIdempotencyGuard"/>.
///
/// Thuật toán (theo `XC-002` §6):
///   1. băm payload (canonical + phiên bản);
///   2. **giành khoá phân tán** theo khoá idempotency ⇒ tuần tự hoá các yêu cầu cùng khoá;
///      không giành được ⇒ trả <see cref="IdempotencyOutcomeKind.DangXuLy"/> (HTTP ⇒ **409**);
///   3. đọc bản ghi:
///      • có + `HoanThanh` + **cùng** payload ⇒ <see cref="IdempotencyOutcomeKind.TraVeKetQuaCu"/> (KHÔNG chạy lại);
///      • có + **khác** payload ⇒ <see cref="IdempotencyOutcomeKind.XungDotPayload"/> (**NÉM LỖI ở tầng API**, không im lặng);
///      • có + `DangXuLy` còn hiệu lực ⇒ <see cref="IdempotencyOutcomeKind.DangXuLy"/>;
///      • chưa có (hoặc đã hết hạn) ⇒ tạo mới `DangXuLy`;
///   4. chạy xử lý; thành công ⇒ `HoanThanh` + lưu kết quả; lỗi ⇒ `ThatBai` rồi **ném lại**.
/// </summary>
public class IdempotencyGuard : IIdempotencyGuard, ITransientDependency
{
    /// <summary>Thời gian mặc định giữ vết một khoá idempotency (7 ngày — đủ cho mọi chu kỳ đối soát).</summary>
    public static readonly TimeSpan ThoiGianLuuMacDinh = TimeSpan.FromDays(7);

    /// <summary>Chờ giành khoá phân tán tối đa. Hết thời gian ⇒ coi như đang xử lý ở nơi khác (409).</summary>
    public static readonly TimeSpan ChoKhoaToiDa = TimeSpan.FromSeconds(5);

    private readonly IIdempotencyStore _store;
    private readonly IAbpDistributedLock _khoaPhanTan;
    private readonly IClock _dongHo;

    public IdempotencyGuard(IIdempotencyStore store, IAbpDistributedLock khoaPhanTan, IClock dongHo)
    {
        _store = store;
        _khoaPhanTan = khoaPhanTan;
        _dongHo = dongHo;
    }

    public async Task<IdempotencyOutcome> ThucThiAsync(
        IdempotencyKey key,
        string? payloadJson,
        Func<CancellationToken, Task<string?>> xuLy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(xuLy);

        var hash = CanonicalJsonHasher.Bam(payloadJson);

        // (2) Tuần tự hoá theo khoá: nếu không giành được thì KHÔNG được chạy xử lý.
        await using var handle = await _khoaPhanTan.TryAcquireAsync(
            key.KhoaLock(), ChoKhoaToiDa, cancellationToken);

        if (handle is null)
        {
            return new IdempotencyOutcome(IdempotencyOutcomeKind.DangXuLy, ErrorCode: MessagingErrorCodes.DangXuLy);
        }

        var now = _dongHo.Now;

        // (3) Đọc bản ghi — làm LẠI bên trong khoá (double-check) vì yêu cầu trước có thể vừa xong.
        var banGhi = await _store.TimAsync(key, cancellationToken);

        if (banGhi is not null && banGhi.ConHieuLuc(now))
        {
            if (banGhi.State == IdempotencyState.HoanThanh)
            {
                return banGhi.CungPayload(hash, CanonicalJsonHasher.PhienBanHienHanh)
                    ? new IdempotencyOutcome(IdempotencyOutcomeKind.TraVeKetQuaCu, banGhi.Result)
                    // 🚨 Cùng khoá nhưng KHÁC nội dung ⇒ lỗi tường minh. Trả kết quả cũ ở đây sẽ là
                    // lỗi im lặng nguy hiểm: yêu cầu mới bị coi là đã xử lý.
                    : new IdempotencyOutcome(IdempotencyOutcomeKind.XungDotPayload,
                        ErrorCode: MessagingErrorCodes.PayloadConflict);
            }

            if (banGhi.State == IdempotencyState.DangXuLy)
            {
                return new IdempotencyOutcome(IdempotencyOutcomeKind.DangXuLy, ErrorCode: MessagingErrorCodes.DangXuLy);
            }

            // ThatBai ⇒ cho phép thử lại.
            banGhi.ThuLai(now);
            await _store.CapNhatAsync(banGhi, cancellationToken);
        }
        else if (banGhi is not null)
        {
            // Bản ghi đã hết hạn ⇒ dùng lại chính nó như một bản ghi MỚI (tránh phình bảng vô hạn).
            // ⚠️ Phải KHỞI ĐỘNG LẠI toàn bộ trạng thái (không chỉ gia hạn) — nếu chỉ gia hạn thì
            // State/PayloadHash cũ còn nguyên và bước HoanThanh phía dưới sẽ NÉM LỖI.
            banGhi.BatDauLai(hash, CanonicalJsonHasher.PhienBanHienHanh, now.Add(ThoiGianLuuMacDinh));
            await _store.CapNhatAsync(banGhi, cancellationToken);
        }
        else
        {
            banGhi = IdempotencyRecord.BatDau(
                key, hash, CanonicalJsonHasher.PhienBanHienHanh, now.Add(ThoiGianLuuMacDinh));

            await _store.ThemAsync(banGhi, cancellationToken);
        }

        // (4) Chạy xử lý thật.
        try
        {
            var ketQua = await xuLy(cancellationToken);

            banGhi.HoanThanh(ketQua, _dongHo.Now);
            await _store.CapNhatAsync(banGhi, cancellationToken);

            return new IdempotencyOutcome(IdempotencyOutcomeKind.DaThucThi, ketQua);
        }
        catch (Exception ex)
        {
            banGhi.ThatBai(ex.GetType().Name, ex.Message, _dongHo.Now);
            await _store.CapNhatAsync(banGhi, cancellationToken);

            // Không được "nuốt" lỗi: nơi gọi (và cơ chế thử lại) phải biết là đã thất bại.
            throw;
        }
    }
}
