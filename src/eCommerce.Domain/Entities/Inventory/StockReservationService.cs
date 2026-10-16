using eCommerce.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **DỊCH VỤ GIỮ CHỖ** (`INV-002`) — nơi **DUY NHẤT** được phép tăng/giảm `Reserved`.
///
/// **TRÌNH TỰ BẮT BUỘC:**
///   1. **Giành khoá phân tán theo `(tenant, kho, SKU)`** — 📌 **TRƯỚC** khi đọc tồn kho. Đọc tồn **trước** khi
///      khoá là **vô nghĩa**: hai request cùng đọc `Available = 1`, rồi cùng khoá tuần tự, nhưng **cả hai đã
///      quyết định** dựa trên số cũ ⇒ **cả hai giữ được** ⇒ **bán hai lần cùng một món**. Đây đúng là lỗi mà
///      `INV-002` tồn tại để chặn.
///   2. Đọc `InventoryItem` **sau khi** có khoá → `QuyetDinhGiuCho.KiemTra` (hàm **thuần**).
///   3. `ApplyMovement(Reservation, qty)` (tăng `Reserved`) **và** ghi `StockLedgerEntry` **và** ghi
///      `StockReservation` — **cùng transaction**.
///
/// 📌 **Bút toán giữ chỗ KHÔNG ảnh hưởng `OnHand`** ⇒ phép đối soát kho (`DoiSoatTonKho`) **bỏ qua** chúng
/// (dấu `0`). Nhờ vậy giữ chỗ **không** làm đối soát báo lệch giả.
/// </summary>
public class StockReservationService : DomainService
{
    public const int ChoKhoaToiDaGiay = 30;

    /// <summary>TTL mặc định của một lượt giữ chỗ — 15 phút (đủ để khách điền địa chỉ + thanh toán).</summary>
    public static readonly TimeSpan TtlMacDinh = TimeSpan.FromMinutes(15);

    private readonly IRepository<StockReservation, Guid> _giuChoRepository;
    private readonly IRepository<InventoryItem, Guid> _tonKhoRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _soCaiRepository;
    private readonly IRepository<Warehouse, Guid> _khoRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>🚨 `INV-003` — **hạn mức RIÊNG từng kênh** (bảng `InvChannelLimits`). Đọc **sau khi đã có khoá**
    /// và **trước** khi tăng `Reserved`.</summary>
    private readonly IRepository<InvChannelLimit, Guid> _hanMucKenhRepository;

    /// <summary>🚨 `INV-003` — **chính sách "cho phép chọn"** (`web-001` §93): đọc hạn mức theo kênh **lúc chạy**
    /// thay vì hằng số trong mã. 📌 Domain service **được phép** dùng `ISettingProvider` (ABP: settings là hạ tầng dùng chung).</summary>
    private readonly ISettingProvider _setting;

    public StockReservationService(
        IRepository<StockReservation, Guid> giuChoRepository,
        IRepository<InventoryItem, Guid> tonKhoRepository,
        IRepository<StockLedgerEntry, Guid> soCaiRepository,
        IRepository<Warehouse, Guid> khoRepository,
        IRepository<InvChannelLimit, Guid> hanMucKenhRepository,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant,
        ISettingProvider setting)
    {
        _giuChoRepository = giuChoRepository;
        _tonKhoRepository = tonKhoRepository;
        _soCaiRepository = soCaiRepository;
        _khoRepository = khoRepository;
        _hanMucKenhRepository = hanMucKenhRepository;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
        _setting = setting;
    }

    /// <summary>Giữ chỗ. **NÉM** `Inventory:0001` nếu không đủ hàng; **NÉM** `Inventory:0042` nếu **vượt hạn mức kênh**
    /// và hành động đang áp là `Block` (`INV-003`).
    /// 📌 `channelCode` **tuỳ chọn** (mặc định `null`):
    /// <list type="bullet">
    ///   <item><b>có</b> ⇒ tra bảng **riêng từng kênh** `InvChannelLimits` (dòng `(kênh, kho)` trước, rồi `(kênh, mọi kho)`);</item>
    ///   <item><b>không có</b> ⇒ **không** có hạn mức riêng nào áp được ⇒ rơi về **giá trị dùng chung** (`Inv.ChannelLimit.DefaultValue`)
    ///     và log ghi `(không có)` — **không** im lặng bỏ qua.</item>
    /// </list>
    /// 🔒 Nguồn hạn mức đang áp (`BangTheoKenhVaKho` / `BangTheoKenh` / `MacDinhChung`) **luôn** được ghi vào log
    /// và vào dữ liệu lỗi `0042` ⇒ *"vì sao đơn này bị chặn"* trả lời được **không cần suy luận**.</summary>
    public async Task<StockReservation> GiuChoAsync(
        Guid warehouseId, string sku, int quantity, DateTime at, TimeSpan? ttl = null,
        string? referenceType = null, string? referenceId = null, string? channelCode = null)
    {
        var maSku = sku.Trim();

        var kho = await _khoRepository.FindAsync(warehouseId)
            ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                .WithData("warehouseId", warehouseId).WithData("lyDo", "Kho KHÔNG tồn tại.");

        kho.YeuCauDangHoatDong();

        var tenKhoa = InventoryLockKeys.TheoSku(_currentTenant.Id, warehouseId, maSku);

        await using var khoa = await _distributedLock.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", tenKhoa).WithData("choGiay", ChoKhoaToiDaGiay);

        // ⚠️ ĐỌC SAU KHI CÓ KHOÁ (xem ghi chú ① trong tài liệu lớp).
        var ton = await _tonKhoRepository.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.Sku == maSku);
        var onHand = ton?.OnHand ?? 0;
        var reserved = ton?.Reserved ?? 0;

        // 🚨 `INV-003` — **CƯỠNG CHẾ HẠN MỨC THEO KÊNH** (chính sách "cho phép chọn": `Inv.ChannelLimit.*`
        // + bảng **RIÊNG TỪNG KÊNH** `InvChannelLimits`).
        // 📌 Đặt **SAU khi đã có khoá + đã đọc tồn**, **TRƯỚC** khi tăng `Reserved` ⇒ chặn **trước** mọi thay đổi.
        // ⚠️ Hạn mức `<= 0` ⇒ **TẮT** chính sách ⇒ hành vi **y như trước** khi có `INV-003`.
        var tonKhaDung = onHand - reserved;

        var donViHanMuc = ChinhSach.DocDonViHanMuc(await _setting.GetOrNullAsync(TenChinhSach.HanMuc_DonVi), out var donViHopLe);
        var giaTriMacDinh = ChinhSach.DocSo(await _setting.GetOrNullAsync(TenChinhSach.HanMuc_GiaTriMacDinh), 0m, out var giaTriHopLe);
        var hanhDongMacDinh = ChinhSach.DocHanhDongHanMuc(await _setting.GetOrNullAsync(TenChinhSach.HanMuc_HanhDong), out var hanhDongHopLe);
        var phamViHanMuc = ChinhSach.DocPhamViHanMuc(await _setting.GetOrNullAsync(TenChinhSach.HanMuc_PhamVi), out var phamViHopLe);

        if (!donViHopLe || !giaTriHopLe || !hanhDongHopLe || !phamViHopLe)
        {
            // 🔒 Cấu hình sai ⇒ **KHÔNG** chặn bán (dùng mặc định `Block` với `giaTri = 0` = tắt) nhưng **phải ồn ào**.
            Logger.LogWarning("HẠN MỨC KÊNH (`INV-003`): CẤU HÌNH KHÔNG HỢP LỆ ⇒ dùng mặc định an toàn " +
                "(donVi={DonVi} hopLe={DonViOk} · giaTriMacDinh={GiaTri} hopLe={GiaTriOk} · hanhDong={HanhDong} hopLe={HanhDongOk} · " +
                "phamVi={PhamVi} hopLe={PhamViOk}).",
                donViHanMuc, donViHopLe, giaTriMacDinh, giaTriHopLe, hanhDongMacDinh, hanhDongHopLe, phamViHanMuc, phamViHopLe);
        }

        var kenhChuanHoa = string.IsNullOrWhiteSpace(channelCode) ? null : InvChannelLimit.ChuẩnHoáKenh(channelCode);

        // 📌 Nạp **mọi** dòng hạn mức của ĐÚNG kênh này (thường 0–2 dòng). Lọc theo kênh ở DB (không nạp cả bảng).
        // 🔒 `ChannelCode` đã được chuẩn hoá (trim + chữ thường) **tại một chỗ duy nhất** (`InvChannelLimit`),
        // nên so khớp ở đây cũng dùng giá trị đã chuẩn hoá ⇒ không có ca "kênh `Shopee` vs `shopee`" làm hạn mức im lặng không khớp.
        var cacDongHanMuc = kenhChuanHoa is null
            ? []
            : HanMucKenh.ChieuDongHanMuc(await _hanMucKenhRepository.GetListAsync(x => x.ChannelCode == kenhChuanHoa));

        var hanMucDangAp = HanMucKenh.ChonHanMuc(
            phamViHanMuc, cacDongHanMuc, warehouseId, donViHanMuc, giaTriMacDinh, hanhDongMacDinh, out var dongBiBoQua);

        if (dongBiBoQua.Count > 0)
        {
            // 🚨 **KHÔNG im lặng**: người vận hành ĐÃ cấu hình hạn mức theo KHO nhưng `Inv.ChannelLimit.Scope = Channel`
            // ⇒ dòng đó **không có tác dụng**. Nếu bỏ qua im lặng thì họ **tin** đã siết theo kho (đúng loại "ô chọn
            // không đổi gì" mà §5 việc 2 của gói bàn giao gọi là nợ tệ hơn không làm).
            Logger.LogWarning("HẠN MỨC KÊNH (`INV-003`): BỎ QUA {So} dòng hạn mức THEO KHO vì `{Setting}` = {PhamVi} " +
                "(chỉ xét hạn mức cả kênh). Kênh {Kenh}, kho {Kho}. Muốn dùng hạn mức theo kho ⇒ đổi setting sang `ChannelWarehouse`. [INV003-BOQUA-DONG-THEO-KHO]",
                dongBiBoQua.Count, TenChinhSach.HanMuc_PhamVi, phamViHanMuc, kenhChuanHoa ?? "(không có)", warehouseId);
        }

        var hanMuc = HanMucKenh.TinhHanMuc(hanMucDangAp.DonVi, hanMucDangAp.GiaTri, tonKhaDung);

        Logger.LogInformation("HẠN MỨC KÊNH (`INV-003`): kênh {Kenh} · kho {Kho} · nguồn {Nguon} · giá trị {GiaTri} · " +
            "hành động {HanhDong} ⇒ hạn mức {HanMuc} (tồn khả dụng {Ton}). [INV003-HANMUC-AP-DUNG]",
            kenhChuanHoa ?? "(không có)", warehouseId, hanMucDangAp.Nguon, hanMucDangAp.GiaTri, hanMucDangAp.HanhDong, hanMuc, tonKhaDung);

        if (HanMucKenh.HanMucVoNghia(hanMucDangAp.DonVi, hanMucDangAp.GiaTri, tonKhaDung))
        {
            // 🔔 Cấu hình CÓ Ý ĐỊNH áp hạn mức nhưng hạn mức tính ra 0 ⇒ chính sách **TẮT**.
            // 📌 Không sửa hành vi, chỉ **nói ra** — nếu im lặng thì người vận hành **tin** hạn mức đang chạy (bài học `CHN-002`).
            Logger.LogWarning("HẠN MỨC KÊNH (`INV-003`): HẠN MỨC VÔ NGHĨA — cấu hình {DonVi}={GiaTri} (nguồn {Nguon}) " +
                "nhưng tồn khả dụng {Ton} ⇒ hạn mức tính ra 0 ⇒ chính sách đang **TẮT** cho (kho {Kho}, SKU {Sku}).",
                hanMucDangAp.DonVi, hanMucDangAp.GiaTri, hanMucDangAp.Nguon, tonKhaDung, warehouseId, maSku);
        }

        if (HanMucKenh.VuotHanMuc(quantity, hanMuc))
        {
            var kenh = kenhChuanHoa ?? "(không có)";

            if (HanMucKenh.NenChan(hanMucDangAp.HanhDong, quantity, hanMuc))
            {
                throw new BusinessException(InventoryErrorCodes.VuotHanMucKenh)
                    .WithData("channelCode", kenh)
                    .WithData("sku", maSku)
                    .WithData("warehouseId", warehouseId)
                    .WithData("soLuongYeuCau", quantity)
                    .WithData("hanMuc", hanMuc)
                    .WithData("donVi", hanMucDangAp.DonVi.ToString())
                    .WithData("giaTriCauHinh", hanMucDangAp.GiaTri)
                    .WithData("nguonHanMuc", hanMucDangAp.Nguon.ToString())
                    .WithData("phamVi", phamViHanMuc.ToString())
                    .WithData("tonKhaDung", tonKhaDung)
                    .WithData("lyDo", $"Kênh {kenh} vượt hạn mức phân bổ: yêu cầu {quantity} > hạn mức {hanMuc} " +
                        $"({hanMucDangAp.DonVi}={hanMucDangAp.GiaTri}, nguồn {hanMucDangAp.Nguon}, tồn khả dụng {tonKhaDung}).");
            }

            // `Warn` ⇒ **cho qua** nhưng ghi cảnh báo rõ (kèm số liệu để người vận hành hiểu VÌ SAO).
            Logger.LogWarning("HẠN MỨC KÊNH (`INV-003`, Warn): {Kenh} · {Sku} · kho {Kho} — yêu cầu {YeuCau} > hạn mức {HanMuc} " +
                "(donVi={DonVi} giaTri={GiaTri} nguon={Nguon} tonKhaDung={Ton}). VẪN CHO QUA vì hành động của kênh là `Warn`. [INV003-WARN-CHO-QUA]",
                kenh, maSku, warehouseId, quantity, hanMuc, hanMucDangAp.DonVi, hanMucDangAp.GiaTri, hanMucDangAp.Nguon, tonKhaDung);
        }

        var lyDo = QuyetDinhGiuCho.KiemTra(onHand, reserved, quantity);

        if (lyDo is not null)
        {
            throw new BusinessException(InventoryErrorCodes.KhongDuHangDeGiu)
                .WithData("sku", maSku)
                .WithData("yeuCau", quantity)
                .WithData("onHand", onHand)
                .WithData("reserved", reserved)
                .WithData("coTheBan", QuyetDinhGiuCho.CoTheBan(onHand, reserved))
                .WithData("lyDo", lyDo);
        }

        if (ton is null)
        {
            ton = new InventoryItem(GuidGenerator.Create(), warehouseId, maSku);
            await _tonKhoRepository.InsertAsync(ton, autoSave: false);
        }

        // 🚨 **KIỂM TRƯỚC — để ca THƯỜNG trả mã nghiệp vụ (`0032`) thay vì để DB nổ `23505`.**
        // 📌 Vì sao vẫn GIỮ hàng rào DB: chỉ kiểm ở đây thì **hai request đồng thời** cùng thấy "chưa giữ"
        // ⇒ **cả hai ghi** ⇒ `Reserved` tăng gấp đôi. Unique index là lớp **nguyên tử**; bộ lọc exception
        // (`BoLocViPhamRangBuocDb`) là lớp biến `23505` thành **cùng một mã** `0032` ⇒ client thấy **một hành vi
        // duy nhất** dù đi đường nào.
        if (!string.IsNullOrWhiteSpace(referenceId))
        {
            var daGiu = await _giuChoRepository.FirstOrDefaultAsync(
                x => x.ReferenceId == referenceId && x.Sku == maSku
                     && x.WarehouseId == warehouseId && x.Status == ReservationStatus.Held);

            if (daGiu is not null)
            {
                throw new BusinessException(InventoryErrorCodes.DaGiuChoNguonNay)
                    .WithData("sku", maSku)
                    .WithData("referenceId", referenceId)
                    .WithData("reservationId", daGiu.Id)
                    .WithData("expiresAtUtc", daGiu.ExpiresAtUtc)
                    .WithData("lyDo", "Đã có lượt giữ chỗ ĐANG HIỆU LỰC cho cùng nguồn + SKU.");
            }
        }

        var hetHan = at.Add(ttl ?? TtlMacDinh);

        var giuCho = new StockReservation(
            GuidGenerator.Create(), warehouseId, maSku, quantity, hetHan, referenceType, referenceId);

        ton.ApplyMovement(StockMovementType.Reservation, quantity);

        await _giuChoRepository.InsertAsync(giuCho, autoSave: false);
        await _tonKhoRepository.UpdateAsync(ton, autoSave: false);

        // Bút toán giữ chỗ: `BalanceAfter` ghi **`OnHand`** (KHÔNG đổi) — `Reserved` nằm trên `InventoryItem`
        // và trên chính bản ghi giữ chỗ. 📌 Cố tình KHÔNG nhồi `Available` vào `BalanceAfter` để cột này
        // giữ **một nghĩa duy nhất** (số dư `OnHand` sau biến động) — trộn hai nghĩa vào một cột là mầm bug.
        var but = new StockLedgerEntry(
            GuidGenerator.Create(), warehouseId, maSku, StockMovementType.Reservation, quantity, ton.OnHand, at,
            referenceType: referenceType ?? nameof(StockReservation),
            referenceId: referenceId,
            channelCode: null,
            referenceLineId: giuCho.Id);

        await _soCaiRepository.InsertAsync(but, autoSave: true);

        Logger.LogInformation(
            "GIỮ CHỖ {SoLuong} × {Sku} @ {Kho} đến {HetHan} (nguồn {Nguon}). Available còn {ConLai}.",
            quantity, maSku, warehouseId, hetHan, referenceId ?? "(không)", ton.Available);

        return giuCho;
    }

    /// <summary>
    /// Nhả **mọi** lượt giữ chỗ `Held` của một **nguồn** — đường **ĐỐI XỨNG** với `ChotTheoNguonAsync`.
    /// 📌 Dùng khi **huỷ đơn/giỏ**: hàng phải về kho **ngay** để người khác mua được — chờ job hết TTL nghĩa là
    /// hàng nằm chết thêm tới 15 phút **một cách vô ích**.
    /// </summary>
    public async Task<int> NhaTheoNguonAsync(string referenceId, DateTime at, ReservationStatus lyDo)
    {
        var dangGiu = await _giuChoRepository.GetListAsync(
            x => x.ReferenceId == referenceId && x.Status == ReservationStatus.Held);

        var soNha = 0;

        foreach (var gc in dangGiu.OrderBy(x => x.Sku, StringComparer.Ordinal).ThenBy(x => x.Id))
        {
            await NhaChoAsync(gc.Id, at, lyDo);
            soNha++;
        }

        return soNha;
    }

    /// <summary>Nhả chỗ (khách huỷ / hết TTL). `lyDo` = `Released` hoặc `Expired`.</summary>
    public async Task<StockReservation> NhaChoAsync(Guid reservationId, DateTime at, ReservationStatus lyDo)
    {
        var giuCho = await _giuChoRepository.FindAsync(reservationId)
            ?? throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("reservationId", reservationId).WithData("lyDo", "Không tìm thấy lượt giữ chỗ.");

        var tenKhoa = InventoryLockKeys.TheoSku(_currentTenant.Id, giuCho.WarehouseId, giuCho.Sku);

        await using var khoa = await _distributedLock.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", tenKhoa).WithData("choGiay", ChoKhoaToiDaGiay);

        giuCho.Nha(lyDo, at);

        var ton = await _tonKhoRepository.FirstOrDefaultAsync(
            x => x.WarehouseId == giuCho.WarehouseId && x.Sku == giuCho.Sku)
            ?? throw new BusinessException(InventoryErrorCodes.GiuChoKhongConGiu)
                .WithData("sku", giuCho.Sku)
                .WithData("lyDo", "Có lượt giữ chỗ nhưng KHÔNG có bản ghi tồn kho ⇒ dữ liệu đã hỏng.");

        ton.ApplyMovement(StockMovementType.ReservationRelease, giuCho.Quantity);

        var but = new StockLedgerEntry(
            GuidGenerator.Create(), giuCho.WarehouseId, giuCho.Sku, StockMovementType.ReservationRelease,
            giuCho.Quantity, ton.OnHand, at,
            referenceType: giuCho.ReferenceType ?? nameof(StockReservation),
            referenceId: giuCho.ReferenceId,
            channelCode: null,
            referenceLineId: giuCho.Id);

        await _soCaiRepository.InsertAsync(but, autoSave: false);
        await _tonKhoRepository.UpdateAsync(ton, autoSave: false);
        await _giuChoRepository.UpdateAsync(giuCho, autoSave: true);

        Logger.LogInformation("NHẢ CHỖ ({LyDo}) {SoLuong} × {Sku}: Available trở lại {ConLai}.",
            lyDo, giuCho.Quantity, giuCho.Sku, ton.Available);

        return giuCho;
    }

    /// <summary>
    /// Nhả **mọi** lượt giữ chỗ đã quá hạn (job). Trả về **số lượt đã nhả**.
    /// 📌 Không có job này thì giữ chỗ **vô thời hạn** trên thực tế: khách bỏ ngang ⇒ hàng **không ai mua được**
    /// mà **không có lỗi nào** để lần ra.
    /// </summary>
    public async Task<int> NhaChoHetHanAsync(DateTime at)
    {
        var hetHan = await _giuChoRepository.GetListAsync(
            x => x.Status == ReservationStatus.Held && x.ExpiresAtUtc <= at);

        var soNha = 0;

        foreach (var gc in hetHan.OrderBy(x => x.ExpiresAtUtc).ThenBy(x => x.Id))
        {
            await NhaChoAsync(gc.Id, at, ReservationStatus.Expired);
            soNha++;
        }

        if (soNha > 0)
        {
            Logger.LogWarning("JOB NHẢ CHỖ: đã nhả {So} lượt giữ chỗ QUÁ HẠN — hàng trở lại kho bán được.", soNha);
        }

        return soNha;
    }
}
