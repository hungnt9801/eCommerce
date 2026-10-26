using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **CHỐT GIỮ CHỖ → XUẤT KHO** (`SAL-003`) — biến **cam kết có thời hạn** (`INV-002`) thành **hàng đã ra khỏi kho**.
///
/// **BẤT BIẾN THEN CHỐT (đây là toàn bộ lý do lớp này tồn tại):**
/// khi chốt, `OnHand` **và** `Reserved` **cùng giảm đúng `q`** ⇒ **`Available` KHÔNG ĐỔI**.
/// 📌 Vì sao quan trọng: lúc **giữ chỗ**, hàng đã bị trừ khỏi `Available` (`Reserved += q`). Khi **xuất kho thật**,
/// nếu **chỉ** trừ `OnHand` mà **quên** trừ `Reserved` thì `Available = OnHand − Reserved` **giảm hai lần** cho
/// **cùng một** món hàng ⇒ hệ thống tưởng **hết hàng** ⇒ **mất doanh thu** mà **không có lỗi nào** để lần ra.
/// Ngược lại, nếu chỉ trừ `Reserved` mà không trừ `OnHand` thì kho **thừa hàng ảo** ⇒ **bán thứ không có**.
///
/// **HAI BÚT TOÁN cho mỗi lượt chốt** (cố ý, không gộp):
///   • `Sale` — trừ `OnHand` (hàng **đã ra khỏi kho**);
///   • `ReservationRelease` — trả `Reserved` (cam kết giữ chỗ **đã hoàn thành**, không còn "đang giữ").
/// 📌 Gộp làm một bút toán sẽ **mất khả năng truy vết**: báo cáo *"đã bán bao nhiêu"* (`Sale`) và
/// *"còn đang giữ bao nhiêu"* (`Reservation` − `ReservationRelease`) là **hai câu hỏi khác nhau**.
/// </summary>
public class ChotGiuChoService : DomainService
{
    public const int ChoKhoaToiDaGiay = 30;

    private readonly IRepository<StockReservation, Guid> _giuChoRepository;
    private readonly IRepository<InventoryItem, Guid> _tonKhoRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _soCaiRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;

    public ChotGiuChoService(
        IRepository<StockReservation, Guid> giuChoRepository,
        IRepository<InventoryItem, Guid> tonKhoRepository,
        IRepository<StockLedgerEntry, Guid> soCaiRepository,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant)
    {
        _giuChoRepository = giuChoRepository;
        _tonKhoRepository = tonKhoRepository;
        _soCaiRepository = soCaiRepository;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Chốt **mọi** lượt giữ chỗ `Held` của một **nguồn** (ví dụ `Checkout:{khoá}`) — đúng thứ tự SKU để chống deadlock.
    /// Trả về **số lượt đã chốt**; `0` ⇒ nguồn này không còn gì đang giữ (đã chốt trước đó hoặc chưa giữ bao giờ).
    /// **NÉM** `0031` nếu có lượt **quá hạn** (xem ghi chú trong <see cref="ChotMotAsync"/>).
    /// </summary>
    public async Task<int> ChotTheoNguonAsync(string referenceId, DateTime at)
    {
        var dangGiu = await _giuChoRepository.GetListAsync(
            x => x.ReferenceId == referenceId && x.Status == ReservationStatus.Held);

        var soChot = 0;

        foreach (var gc in dangGiu.OrderBy(x => x.Sku, StringComparer.Ordinal).ThenBy(x => x.Id))
        {
            await ChotMotAsync(gc.Id, at);
            soChot++;
        }

        if (soChot > 0)
        {
            Logger.LogInformation(
                "ĐÃ CHỐT {So} lượt giữ chỗ của nguồn {Nguon} ⇒ hàng ra khỏi kho (OnHand và Reserved cùng giảm).",
                soChot, referenceId);
        }

        return soChot;
    }

    /// <summary>Chốt **một** lượt giữ chỗ.</summary>
    public async Task ChotMotAsync(Guid reservationId, DateTime at)
    {
        var giuCho = await _giuChoRepository.FindAsync(reservationId)
            ?? throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("reservationId", reservationId).WithData("lyDo", "Không tìm thấy lượt giữ chỗ.");

        var tenKhoa = InventoryLockKeys.TheoSku(_currentTenant.Id, giuCho.WarehouseId, giuCho.Sku);

        await using var khoa = await _distributedLock.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", tenKhoa).WithData("choGiay", ChoKhoaToiDaGiay);

        // 🚨 `Chot()` NÉM nếu lượt giữ chỗ **quá hạn** (`0031`) hoặc **không còn `Held`** (`0030`).
        // 📌 Vì sao **không** "tự động nhả rồi xuất kho luôn" cho ca quá hạn: hàng đã được **trả về `Available`**
        // (job/người khác đã nhả) ⇒ nếu vẫn xuất thì **cùng một món bán cho hai người**. Từ chối là **đúng**;
        // xử lý nghiệp vụ (báo khách chọn lại) thuộc tầng đơn hàng.
        giuCho.Chot(at);

        var ton = await _tonKhoRepository.FirstOrDefaultAsync(
            x => x.WarehouseId == giuCho.WarehouseId && x.Sku == giuCho.Sku)
            ?? throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("sku", giuCho.Sku)
                .WithData("lyDo", "Có lượt giữ chỗ nhưng KHÔNG có bản ghi tồn kho ⇒ dữ liệu đã hỏng.");

        // ① Xuất kho: `OnHand -= q` (bất biến của entity tự NÉM nếu âm).
        ton.ApplyMovement(StockMovementType.Sale, giuCho.Quantity);
        await GhiSoCaiAsync(giuCho, StockMovementType.Sale, ton.OnHand, at);

        // ② Trả `Reserved`: cam kết giữ chỗ đã hoàn thành.
        ton.ApplyMovement(StockMovementType.ReservationRelease, giuCho.Quantity);
        await GhiSoCaiAsync(giuCho, StockMovementType.ReservationRelease, ton.OnHand, at);

        await _tonKhoRepository.UpdateAsync(ton, autoSave: false);
        await _giuChoRepository.UpdateAsync(giuCho, autoSave: true);

        Logger.LogInformation(
            "CHỐT giữ chỗ {Id}: {Sku} × {SoLuong} ⇒ OnHand {OnHand}, Reserved {Reserved}, Available {Available}.",
            giuCho.Id, giuCho.Sku, giuCho.Quantity, ton.OnHand, ton.Reserved, ton.Available);
    }

    private async Task GhiSoCaiAsync(
        StockReservation giuCho, StockMovementType loai, int balanceAfter, DateTime at)
    {
        // Cùng `ReferenceLineId` = `Id` lượt giữ chỗ + **khác `MovementType`** ⇒ qua được unique index
        // `(TenantId, ReferenceLineId, MovementType)` (bài học `INV-001` §19.2 **L1**) mà **vẫn** chặn ghi trùng.
        var but = new StockLedgerEntry(
            GuidGenerator.Create(), giuCho.WarehouseId, giuCho.Sku, loai, giuCho.Quantity, balanceAfter, at,
            referenceType: giuCho.ReferenceType ?? nameof(StockReservation),
            referenceId: giuCho.ReferenceId,
            channelCode: null,
            referenceLineId: giuCho.Id);

        await _soCaiRepository.InsertAsync(but, autoSave: false);
    }
}
