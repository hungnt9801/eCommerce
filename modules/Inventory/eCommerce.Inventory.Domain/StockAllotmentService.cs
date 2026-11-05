using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>🆕 **`S13`** — sự kiện **ngân sách tồn theo kênh thay đổi** (`INV-003` BR-15).</summary>
public class StockAllotmentChangedEto
{
    public Guid AggregateId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid WarehouseId { get; set; }

    public string Sku { get; set; } = default!;

    public string ChannelCode { get; set; } = default!;

    public int AllottedQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    public int AvailableToPromise { get; set; }

    public int Status { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>
/// 🆕 **`S13` — DỊCH VỤ NGÂN SÁCH TỒN THEO KÊNH** (`INV-003`).
///
/// 🔒 **CÙNG KHOÁ VỚI GIỮ CHỖ** (`InventoryLockKeys.TheoSku`): ngân sách và `Reserved` của cùng
/// `(kho, SKU)` là **một** tài nguyên ⇒ dùng **một** khoá. 📌 Hai khoá khác nhau cho cùng tài nguyên là
/// cách tạo ra *"hai luồng cùng thấy còn hàng"* — đúng lỗi `INV-002` sinh ra để chặn.
///
/// 🚨 **BẤT BIẾN TỔNG** (`Σ AllottedQuantity ≤ OnHand`, **BR-03**) **không** diễn đạt được bằng `CHECK`
/// (khác bảng, nhiều dòng) ⇒ kiểm **trong transaction, sau khi đã có khoá**; `CHECK` trên một dòng chỉ là
/// hàng rào phụ (`0 ≤ Reserved ≤ Allotted`).
/// </summary>
public class StockAllotmentService : DomainService
{
    public const int ChoKhoaToiDaGiay = 30;

    private readonly IRepository<StockAllotment, Guid> _nganSachRepository;
    private readonly IRepository<StockAllotmentLedger, Guid> _soCaiNganSachRepository;
    private readonly IRepository<InventoryItem, Guid> _tonKhoRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedEventBus _eventBus;

    public StockAllotmentService(
        IRepository<StockAllotment, Guid> nganSachRepository,
        IRepository<StockAllotmentLedger, Guid> soCaiNganSachRepository,
        IRepository<InventoryItem, Guid> tonKhoRepository,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant,
        IDistributedEventBus eventBus)
    {
        _nganSachRepository = nganSachRepository;
        _soCaiNganSachRepository = soCaiNganSachRepository;
        _tonKhoRepository = tonKhoRepository;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Cấp/sửa ngân sách cho `(kho, SKU, kênh)`. 🚨 **NÉM** `Inventory:0047` nếu tổng ngân sách vượt `OnHand`.
    /// 📌 Idempotent theo `idempotencyKey`: gọi lại **cùng khoá** trả về ngân sách hiện có, **không** ghi thêm dòng sổ.
    /// </summary>
    public async Task<StockAllotment> DatNganSachAsync(
        Guid warehouseId,
        string sku,
        string channelCode,
        int soLuongMoi,
        string? referenceType = null,
        string? referenceId = null,
        string? idempotencyKey = null,
        Guid? nguoiThucHienId = null,
        DateTime? atUtc = null)
    {
        var maSku = sku.Trim();
        var kenh = InvChannelLimit.ChuẩnHoáKenh(channelCode);
        var at = atUtc ?? DateTime.UtcNow;

        await using var khoa = await LayKhoaAsync(warehouseId, maSku);

        // ── IDEMPOTENCY (BR-09): cùng khoá ⇒ trả kết quả cũ, KHÔNG ghi thêm ────────────────────────
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var daCo = await _soCaiNganSachRepository.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);

            if (daCo is not null)
            {
                var cu = await _nganSachRepository.FindAsync(daCo.StockAllotmentId)
                    ?? throw LoiKhongTonTai(daCo.StockAllotmentId);

                Logger.LogInformation(
                    "NGÂN SÁCH TỒN (`S15`/`INV-003`): khoá idempotency {Khoa} đã dùng ⇒ trả ngân sách cũ {Id}.",
                    idempotencyKey, cu.Id);

                return cu;
            }
        }

        var ton = await _tonKhoRepository.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.Sku == maSku)
            ?? throw new BusinessException(InventoryErrorCodes.NganSachTonVuotTon)
                .WithData("sku", maSku).WithData("warehouseId", warehouseId)
                .WithData("lyDo", "Kho chưa có bản ghi tồn cho SKU này ⇒ chưa cấp ngân sách được.")
                .WithData("onHand", 0).WithData("soLuongYeuCau", soLuongMoi);

        var nganSach = await _nganSachRepository.FirstOrDefaultAsync(
            x => x.WarehouseId == warehouseId && x.Sku == maSku && x.ChannelCode == kenh);

        var truoc = nganSach?.AllottedQuantity ?? 0;
        var lyDo = nganSach is null ? StockAllotmentLedgerReason.ManualAllot : StockAllotmentLedgerReason.ManualAdjust;

        // Σ ngân sách của các kênh KHÁC (đã nhả thì không tính) + số mới.
        var cacDongKhac = await _nganSachRepository.GetListAsync(
            x => x.WarehouseId == warehouseId && x.Sku == maSku
                 && x.ChannelCode != kenh && x.Status != StockAllotmentStatus.Released);

        var tongKhac = cacDongKhac.Sum(x => x.AllottedQuantity);

        if (tongKhac + soLuongMoi > ton.OnHand)
        {
            throw new BusinessException(InventoryErrorCodes.NganSachTonVuotTon)
                .WithData("sku", maSku).WithData("warehouseId", warehouseId).WithData("channelCode", kenh)
                .WithData("onHand", ton.OnHand).WithData("tongCacKenhKhac", tongKhac)
                .WithData("soLuongYeuCau", soLuongMoi)
                .WithData("lyDo", "Tổng ngân sách các kênh VƯỢT tồn thực của kho.");
        }

        if (nganSach is null)
        {
            nganSach = new StockAllotment(GuidGenerator.Create(), warehouseId, maSku, kenh, soLuongMoi);
            await _nganSachRepository.InsertAsync(nganSach, autoSave: false);
        }
        else if (soLuongMoi == 0 && truoc == 0)
        {
            // 📌 Không có gì đổi ⇒ vẫn ghi audit? KHÔNG: một dòng sổ delta 0 chỉ làm nhiễu lịch sử.
            // (Khác `Freeze` — ở đó delta 0 **có** nghĩa vì trạng thái đổi.)
            return nganSach;
        }
        else
        {
            nganSach.DatNganSach(soLuongMoi);
            await _nganSachRepository.UpdateAsync(nganSach, autoSave: false);
        }

        await _soCaiNganSachRepository.InsertAsync(new StockAllotmentLedger(
            GuidGenerator.Create(), nganSach.Id, warehouseId, maSku, kenh,
            soLuongMoi - truoc, nganSach.AllottedQuantity, lyDo, at,
            nguoiThucHienId, referenceType, referenceId, idempotencyKey), autoSave: false);

        await PhatAsync(nganSach, at);

        Logger.LogInformation(
            "NGÂN SÁCH TỒN (`INV-003`): {Kenh} · {Sku} · kho {Kho}: {Truoc} → {Sau} (tồn {Ton}). [S13-NGAN-SACH]",
            kenh, maSku, warehouseId, truoc, soLuongMoi, ton.OnHand);

        return nganSach;
    }

    /// <summary>`Active → Frozen` — ngừng bán qua kênh, **giữ** ngân sách (ledger delta 0, có lý do).</summary>
    public async Task<StockAllotment> DongBangAsync(Guid id, string lyDo, Guid? nguoiThucHienId, DateTime? atUtc = null)
        => await DoiTrangThaiAsync(id, lyDo, nguoiThucHienId, atUtc, StockAllotmentLedgerReason.Freeze,
            (ns, at) => ns.DongBang(nguoiThucHienId, at));

    /// <summary>`Frozen → Active`.</summary>
    public async Task<StockAllotment> MoDongBangAsync(Guid id, string lyDo, Guid? nguoiThucHienId, DateTime? atUtc = null)
        => await DoiTrangThaiAsync(id, lyDo, nguoiThucHienId, atUtc, StockAllotmentLedgerReason.Unfreeze,
            (ns, _) => ns.MoDongBang(nguoiThucHienId));

    /// <summary>`→ Released` — nhả ngân sách (chỉ khi không còn giữ chỗ).</summary>
    public async Task<StockAllotment> NhaAsync(Guid id, string lyDo, Guid? nguoiThucHienId, DateTime? atUtc = null)
        => await DoiTrangThaiAsync(id, lyDo, nguoiThucHienId, atUtc, StockAllotmentLedgerReason.Release,
            (ns, at) => ns.Nha(lyDo, at));

    private async Task<StockAllotment> DoiTrangThaiAsync(
        Guid id,
        string lyDo,
        Guid? nguoiThucHienId,
        DateTime? atUtc,
        StockAllotmentLedgerReason lyDoSo,
        Action<StockAllotment, DateTime> thaoTac)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw new BusinessException(InventoryErrorCodes.NganSachTonKhongHopLe)
                .WithData("lyDo", "Mọi thay đổi trạng thái ngân sách BẮT BUỘC có lý do (`INV-003` BR-12).");
        }

        var nganSach = await _nganSachRepository.FindAsync(id) ?? throw LoiKhongTonTai(id);
        var at = atUtc ?? DateTime.UtcNow;

        await using var khoa = await LayKhoaAsync(nganSach.WarehouseId, nganSach.Sku);

        // 📌 Nạp lại SAU khi có khoá: bản đọc trước khoá có thể đã cũ.
        nganSach = await _nganSachRepository.GetAsync(id);

        thaoTac(nganSach, at);

        await _nganSachRepository.UpdateAsync(nganSach, autoSave: false);

        await _soCaiNganSachRepository.InsertAsync(new StockAllotmentLedger(
            GuidGenerator.Create(), nganSach.Id, nganSach.WarehouseId, nganSach.Sku, nganSach.ChannelCode,
            quantityDelta: 0, allottedAfter: nganSach.AllottedQuantity, lyDoSo, at,
            nguoiThucHienId, referenceType: "TrangThai", referenceId: nganSach.Status.ToString()), autoSave: false);

        await PhatAsync(nganSach, at);

        return nganSach;
    }

    /// <summary>
    /// 🚨 **ĐƯỜNG GHI DUY NHẤT của `ReservedQuantity` khi GIỮ CHỖ** — gọi từ `StockReservationService`
    /// **sau khi đã có khoá** (không tự lấy khoá để tránh khoá lồng nhau cùng tên).
    /// 📌 Không có dòng ngân sách ⇒ **không áp dụng** (`INV-003` là **tuỳ chọn theo dòng**; xem `INV-003` §21.3).
    /// </summary>
    public async Task TangGiuChoNeuCoNganSachAsync(
        Guid warehouseId, string sku, string? channelCode, int soLuong, DateTime atUtc)
    {
        var nganSach = await TimTheoKenhAsync(warehouseId, sku, channelCode);

        if (nganSach is null)
        {
            return;
        }

        nganSach.TangGiuCho(soLuong);
        await _nganSachRepository.UpdateAsync(nganSach, autoSave: false);
        await PhatAsync(nganSach, atUtc);
    }

    /// <summary>Đối xứng của trên: nhả chỗ / chốt đơn ⇒ `ReservedQuantity` giảm.</summary>
    public async Task GiamGiuChoNeuCoNganSachAsync(
        Guid warehouseId, string sku, string? channelCode, int soLuong, DateTime atUtc)
    {
        var nganSach = await TimTheoKenhAsync(warehouseId, sku, channelCode);

        if (nganSach is null)
        {
            return;
        }

        nganSach.GiamGiuCho(soLuong);
        await _nganSachRepository.UpdateAsync(nganSach, autoSave: false);
        await PhatAsync(nganSach, atUtc);
    }

    private Task<StockAllotment?> TimTheoKenhAsync(Guid warehouseId, string sku, string? channelCode)
    {
        if (string.IsNullOrWhiteSpace(channelCode))
        {
            return Task.FromResult<StockAllotment?>(null);
        }

        var kenh = InvChannelLimit.ChuẩnHoáKenh(channelCode);
        var maSku = sku.Trim();

        return _nganSachRepository.FirstOrDefaultAsync(
            x => x.WarehouseId == warehouseId && x.Sku == maSku && x.ChannelCode == kenh);
    }

    private async Task<IAbpDistributedLockHandle> LayKhoaAsync(Guid warehouseId, string sku)
    {
        var tenKhoa = InventoryLockKeys.TheoSku(_currentTenant.Id, warehouseId, sku);

        return await _distributedLock.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", tenKhoa).WithData("choGiay", ChoKhoaToiDaGiay);
    }

    private Task PhatAsync(StockAllotment ns, DateTime atUtc)
        => _eventBus.PublishAsync(new StockAllotmentChangedEto
        {
            AggregateId = ns.Id,
            TenantId = ns.TenantId,
            WarehouseId = ns.WarehouseId,
            Sku = ns.Sku,
            ChannelCode = ns.ChannelCode,
            AllottedQuantity = ns.AllottedQuantity,
            ReservedQuantity = ns.ReservedQuantity,
            AvailableToPromise = ns.AvailableToPromise,
            Status = (int)ns.Status,
            OccurredAtUtc = atUtc
        }, useOutbox: true);

    private static BusinessException LoiKhongTonTai(Guid id)
        => new BusinessException(InventoryErrorCodes.NganSachTonKhongTonTai).WithData("id", id);
}
