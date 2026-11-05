using eCommerce.Application.Contracts.Pos;
using eCommerce.Inventory;
using eCommerce.Pos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Pos;

/// <summary>
/// **API HOÁ ĐƠN BÁN LẺ** (`WP-06`) — mọi đường ghi đi qua **domain** (`RetailBill.HoanTat`/`Huy`) và
/// **một** cổng kho (`IPosStockGateway`).
///
/// 🔒 **BA HÀNG RÀO (không thay thế nhau):**
///   ① `[Authorize]` theo **việc** — bán ≠ huỷ (huỷ đảo doanh thu);
///   ② **domain** — tiền do `TinhTienHoaDon` tính, chặn chiết khấu 2 tầng, nợ không khách/hạn;
///   ③ **DB** — unique `(TenantId, ClientBillId)` chống hoá đơn trùng (offline gửi lại), `CHECK` chặn
///      `DebtAmount`/`ReturnedQuantity` sai.
///
/// 📌 **Idempotency là yêu cầu của POS offline** (`03 §7`, `D-42`): mất mạng sau khi server đã ghi ⇒ client
/// gửi lại **cùng** `ClientBillId` ⇒ phải trả hoá đơn **đã có**, **không** đẩy tồn lần hai. Thiếu nó thì
/// mỗi lần mạng chập là **một hoá đơn ảo** + tồn kho sai.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Pos.Default)]
public class RetailBillAppService : ApplicationService, IRetailBillAppService
{
    private const int SoDongToiDaCuaMotTrang = 200;

    private readonly IRepository<RetailBill, Guid> _hoaDon;
    private readonly IRepository<RetailBillLine, Guid> _dongHoaDon;
    private readonly IRepository<RetailBillPayment, Guid> _thanhToan;
    private readonly IRepository<CashSession, Guid> _ca;
    private readonly IPosStockGateway _kho;

    /// <summary>
    /// 🆕 **`WP-07`** — cổng ghi **điểm + tiền tích luỹ + công nợ** vào khách (`ICustomerLedger`).
    /// 📌 Nối đúng nợ #6 của `handover-n7` §5: `WP-06` **chỉ** ghi điểm trên hoá đơn, chưa cộng vào khách.
    /// 🔒 **Cùng UoW** ⇒ hoặc hoá đơn + sổ khách cùng ghi, hoặc cùng rollback (không có *"đã bán mà khách
    /// không bị trừ điểm"*). ⚠️ **KHÔNG ghi sổ điểm ở FE** — FE không giữ được transaction (`handover-n7` §6).
    /// </summary>
    private readonly eCommerce.Customers.ICustomerLedger _soKhach;

    /// <summary>
    /// 🆕 **`WP-07`** — phạm vi dữ liệu của người đang đăng nhập (`07 §WP-07`: *"Áp `data_scope` vào
    /// `Orders`/`RetailBills` (lọc server-side, không tin client)"*).
    /// 🚨 Lọc ở **server** vì ẩn ở UI là **không lọc**: DevTools vẫn gọi được API và thấy doanh thu của
    /// người khác. 📌 Hợp đồng nằm ở `Customers.Application.Contracts` để `Sales`/`Pos` dùng **cùng một**
    /// định nghĩa *"phạm vi của tôi"*.
    /// </summary>
    private readonly eCommerce.Customers.IDataScopeResolver _phamVi;

    public RetailBillAppService(
        IRepository<RetailBill, Guid> hoaDon,
        IRepository<RetailBillLine, Guid> dongHoaDon,
        IRepository<RetailBillPayment, Guid> thanhToan,
        IRepository<CashSession, Guid> ca,
        IPosStockGateway kho,
        eCommerce.Customers.ICustomerLedger soKhach,
        eCommerce.Customers.IDataScopeResolver phamVi)
    {
        _hoaDon = hoaDon;
        _dongHoaDon = dongHoaDon;
        _thanhToan = thanhToan;
        _ca = ca;
        _kho = kho;
        _soKhach = soKhach;
        _phamVi = phamVi;
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<RetailBillDto>> GetListAsync(RetailBillListInput input)
    {
        var q = await _hoaDon.GetQueryableAsync();

        // 🆕 `WP-07` — phạm vi dữ liệu, áp **TRƯỚC** mọi bộ lọc khác (một chỗ duy nhất).
        q = await ApDungPhamViAsync(q);

        if (input.WarehouseId is { } khoId)
        {
            q = q.Where(x => x.WarehouseId == khoId);
        }

        if (input.CashSessionId is { } caId)
        {
            q = q.Where(x => x.CashSessionId == caId);
        }

        if (input.CustomerId is { } khachId)
        {
            q = q.Where(x => x.CustomerId == khachId);
        }

        if (input.Status is { } trangThai)
        {
            q = q.Where(x => x.Status == trangThai);
        }

        if (input.FromUtc is { } tu)
        {
            q = q.Where(x => x.CreationTime >= tu);
        }

        if (input.ToUtc is { } den)
        {
            q = q.Where(x => x.CreationTime <= den);
        }

        if (!string.IsNullOrWhiteSpace(input.Q))
        {
            var tu_ = input.Q.Trim();
            q = q.Where(x => x.Number.Contains(tu_));
        }

        var tong = await AsyncExecuter.LongCountAsync(q);

        var co = input.MaxResultCount is < 1 or > SoDongToiDaCuaMotTrang ? 50 : input.MaxResultCount;
        var bo = input.SkipCount < 0 ? 0 : input.SkipCount;

        // 📌 Sắp xếp TẤT ĐỊNH (mới nhất trước, cùng giây thì theo số) ⇒ phân trang không lặp/mất dòng.
        var trang = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Number).Skip(bo).Take(co));

        // 🔒 `CashSessionClosed`: UI ẩn nút Huỷ với hoá đơn của ca đã kết — nạp **một** truy vấn, không N+1.
        var caIds = trang.Select(x => x.CashSessionId).Distinct().ToList();
        var caDong = caIds.Count == 0
            ? new Dictionary<Guid, bool>()
            : (await _ca.GetListAsync(x => caIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x.Status == CashSessionStatus.Closed);

        // 🆕 Cột *"Sản phẩm"* của màn #13 cần dòng hàng — nạp cho **đúng trang** (30 hoá đơn × vài dòng),
        // KHÔNG `Include` toàn bảng. 📌 Thiếu khối này thì DS hiện "—" ở cột sản phẩm mà **không có lỗi nào**
        // (đúng họ lỗi "API trả mảng rỗng, UI tưởng không có dữ liệu").
        var hoaDonIds = trang.Select(x => x.Id).ToList();
        var dongTheoHoaDon = hoaDonIds.Count == 0
            ? new Dictionary<Guid, List<RetailBillLine>>()
            : (await _dongHoaDon.GetListAsync(x => hoaDonIds.Contains(x.RetailBillId)))
                .GroupBy(x => x.RetailBillId)
                .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var hd in trang)
        {
            hd.GanDong(dongTheoHoaDon.TryGetValue(hd.Id, out var ds) ? ds : []);
        }

        return new PagedResultDto<RetailBillDto>(
            tong,
            trang.Select(x => ToDto(x, caDong.GetValueOrDefault(x.CashSessionId))).ToList());
    }

    /// <inheritdoc />
    public async Task<RetailBillDto> GetAsync(Guid id)
    {
        var hoaDon = await LayHoaDonAsync(id);

        // 🆕 `WP-07` — **kiểm phạm vi ở đường ĐỌC CHI TIẾT** (không chỉ ở DS): nếu chỉ lọc DS thì người dùng
        // đổi GUID trên URL là xem được hoá đơn ngoài phạm vi — đúng lỗi *"lọc ở client"* mà `07` cấm.
        await KiemPhamViAsync(hoaDon);

        var ca = await _ca.FindAsync(hoaDon.CashSessionId);

        return ToDto(hoaDon, ca?.Status == CashSessionStatus.Closed);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Pos.Sell)]
    public async Task<CreateRetailBillResultDto> CreateAsync(CreateRetailBillDto input)
    {
        var at = Clock.Now;

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // ① IDEMPOTENCY — kiểm TRƯỚC mọi thứ khác: `ClientBillId` là khoá nghiệp vụ của máy POS.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        var daCo = await _hoaDon.FirstOrDefaultAsync(x => x.ClientBillId == input.ClientBillId);

        if (daCo is not null)
        {
            var caCu = await _ca.FindAsync(daCo.CashSessionId);

            // 🚨 NẠP DÒNG **TRƯỚC KHI SO NỘI DUNG**: `FirstOrDefaultAsync` chỉ trả hoá đơn, không kèm dòng
            // (navigation bị `Ignore`) ⇒ so `Lines.Count` trên danh sách **rỗng** luôn lệch ⇒ mọi lần gửi lại
            // đều bị coi là "khác nội dung" (`0016`). 📌 Đã đo được ở lần chạy probe đầu của `WP-06`.
            daCo.GanDong(await _dongHoaDon.GetListAsync(x => x.RetailBillId == daCo.Id));

            if (!CungNoiDung(daCo, input))
            {
                // 🚨 Cùng khoá nhưng KHÁC nội dung: trả hoá đơn cũ là **nuốt** hoá đơn mới của khách ⇒ ném.
                throw new BusinessException(PosErrorCodes.TrungKhoaKhacNoiDung)
                    .WithData("clientBillId", input.ClientBillId)
                    .WithData("number", daCo.Number)
                    .WithData("lyDo", "`ClientBillId` đã dùng cho hoá đơn có NỘI DUNG KHÁC (số tiền/kho/số dòng).");
            }

            Logger.LogInformation(
                "POS: gửi lại hoá đơn {Number} (ClientBillId {ClientBillId}) ⇒ trả hoá đơn ĐÃ CÓ, không đẩy tồn. [WP06-IDEMPOTENT]",
                daCo.Number, input.ClientBillId);

            return new CreateRetailBillResultDto
            {
                Bill = ToDto(daCo, caCu?.Status == CashSessionStatus.Closed),
                AlreadyExisted = true
            };
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // ② CA THU NGÂN — không có ca mở thì KHÔNG bán: tiền mặt vào két mà không có ca nào để đối chiếu
        //    ⇒ kết ca không quy được trách nhiệm. Nhanh: mở ca trước khi bán.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        var ca = await LayCaDangMoAsync(input.WarehouseId, input.CashSessionId);

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // ③ DỰNG HOÁ ĐƠN + TÍNH TIỀN (hàm thuần) — chưa chạm DB.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        var soHoaDon = await SinhSoHoaDonAsync(at);

        var hoaDon = new RetailBill(
            GuidGenerator.Create(),
            soHoaDon,
            input.ClientBillId,
            input.WarehouseId,
            ca.Id,
            input.CustomerId,
            input.SaleStaffId);

        var dong = input.Lines.Select(x => new RetailBillLine(
            GuidGenerator.Create(), hoaDon.Id, x.Sku, x.Quantity, x.UnitPrice, x.DiscountAmount,
            x.VatPercent, x.ProductName)).ToList();

        var thanhToan = input.Payments.Select(x => new RetailBillPayment(
            GuidGenerator.Create(), hoaDon.Id, x.Method, x.Amount, x.ReferenceCode, x.Note)).ToList();

        KiemDiemThanhToan(input, thanhToan);

        hoaDon.HoanTat(
            dong,
            thanhToan,
            chietKhauHoaDon: input.DiscountAmount,
            soDiemSuDung: input.PointsUsed,
            tienKhachDua: input.CustomerGivenAmount,
            diemTich: input.PointsEarned,
            hanThanhToan: input.DueDateUtc,
            ghiChu: input.Note);

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // ④ GHI: hoá đơn → dòng → thanh toán → XUẤT KHO (cùng transaction của ABP UoW)
        //    📌 Thứ tự có chủ ý: chứng từ kho sinh SAU khi hoá đơn đã có dòng, để nếu kho ném
        //    `Inventory:0001` (thiếu tồn) thì **toàn bộ** rollback — không có hoá đơn "đã bán mà kho không trừ".
        // ══════════════════════════════════════════════════════════════════════════════════════════
        await _hoaDon.InsertAsync(hoaDon, autoSave: true);
        await _dongHoaDon.InsertManyAsync(dong, autoSave: true);
        await _thanhToan.InsertManyAsync(thanhToan, autoSave: true);

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // ④b 🆕 `WP-07` — GHI SỔ KHÁCH (điểm + tiền tích luỹ + công nợ) TRONG CÙNG TRANSACTION.
        //    🚨 Đặt **trước** bước xuất kho để nếu vi phạm (`DiemKhongDu` `0018`/`VuotHanMucNo` `0019`/
        //    khách bị chặn `0005`) thì **không** phải làm việc nặng rồi rollback. Dù đặt ở đâu, UoW của ABP
        //    bảo đảm **tất cả cùng** thành công hoặc **tất cả cùng** huỷ.
        //    📌 Khách vãng lai (`CustomerId = null`) ⇒ cổng trả `false`, KHÔNG ném (ADR-03).
        // ══════════════════════════════════════════════════════════════════════════════════════════
        await _soKhach.GhiNhanBanHangAsync(
            hoaDon.CustomerId,
            tienHang: hoaDon.Subtotal - hoaDon.DiscountTotal,
            diemTich: hoaDon.PointsEarned,
            diemSuDung: hoaDon.PointsUsed,
            noTang: hoaDon.DebtAmount,
            nguon: $"POS:{hoaDon.Number}");

        var chungTuId = await _kho.XuatBanLeAsync(
            input.WarehouseId,
            $"XK-{hoaDon.Number}",
            hoaDon.Number,
            dong.Select(x => new PosStockLine(x.Sku, x.Quantity, x.UnitPrice, x.VatPercent)).ToList(),
            at,
            note: $"Xuất kho bán lẻ — hoá đơn {hoaDon.Number}.");

        hoaDon.GanChungTuKho(chungTuId);
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        Logger.LogInformation(
            "POS: BÁN {Number} ({SoDong} dòng) · phải trả {PhaiTra} · đã trả {DaTra} · nợ {ConNo} · " +
            "tiền mặt {TienMat} · chứng từ kho {ChungTu}. [WP06-BAN]",
            hoaDon.Number, dong.Count, hoaDon.TotalPayment, hoaDon.PaidAmount, hoaDon.DebtAmount,
            hoaDon.TienMatThucThu(), chungTuId);

        return new CreateRetailBillResultDto
        {
            Bill = ToDto(hoaDon, caDaKetThuc: false),
            AlreadyExisted = false
        };
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Pos.Void)]
    public async Task<VoidRetailBillResultDto> VoidAsync(Guid id, VoidRetailBillDto input)
    {
        var hoaDon = await LayHoaDonAsync(id);
        var ca = await _ca.FindAsync(hoaDon.CashSessionId)
            ?? throw new BusinessException(PosErrorCodes.KhongCoCaDangMo)
                .WithData("cashSessionId", hoaDon.CashSessionId)
                .WithData("lyDo", "Hoá đơn trỏ tới ca KHÔNG tồn tại ⇒ không xác định được ca đã kết hay chưa.");

        var caDaKetThuc = ca.Status == CashSessionStatus.Closed;

        // 📌 `Huy` ném `0010` nếu ca đã kết; trả `false` nếu đã huỷ trước đó (idempotent, KHÔNG nhập kho lần hai).
        var daHuy = hoaDon.Huy(CurrentUser.Id ?? Guid.Empty, input.Reason, Clock.Now, caDaKetThuc);

        if (!daHuy)
        {
            return new VoidRetailBillResultDto
            {
                Bill = ToDto(hoaDon, caDaKetThuc),
                Voided = false
            };
        }

        // 🚨 ĐẢO TỒN: nhập lại ĐÚNG số đã xuất. `CustomerReturn` + số chứng từ riêng (`HUY-…`) để tra vết
        //    phân biệt *"khách trả hàng"* với *"huỷ hoá đơn"* — hai chuyện khác nhau về kế toán.
        // 🆕 `WP-07` — ĐẢO **SỔ KHÁCH** cùng transaction: trả lại điểm đã tiêu, thu hồi điểm đã tích,
        //    trừ tiền tích luỹ và giảm nợ. 📌 Không đảo thì khách bị **trừ điểm vĩnh viễn** cho một hoá đơn
        //    đã huỷ — và không ai phát hiện (số dư chỉ "hơi thấp").
        await _soKhach.DaoBanHangAsync(
            hoaDon.CustomerId,
            tienHang: hoaDon.Subtotal - hoaDon.DiscountTotal,
            diemTich: hoaDon.PointsEarned,
            diemSuDung: hoaDon.PointsUsed,
            noGiam: hoaDon.DebtAmount,
            nguon: $"POS-HUY:{hoaDon.Number}");

        var dong = await _dongHoaDon.GetListAsync(x => x.RetailBillId == hoaDon.Id);

        var chungTuId = await _kho.NhapHangTraAsync(
            hoaDon.WarehouseId,
            $"HUY-{hoaDon.Number}",
            hoaDon.Number,
            dong.Select(x => new PosStockLine(x.Sku, x.Quantity, x.UnitPrice, x.VatPercent)).ToList(),
            Clock.Now,
            note: $"NHẬP LẠI kho do HUỶ hoá đơn {hoaDon.Number}: {input.Reason}");

        hoaDon.GanChungTuHuy(chungTuId);
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);

        Logger.LogWarning(
            "POS: HUỶ hoá đơn {Number} — lý do: {LyDo} · nhập lại kho bằng chứng từ {ChungTu}. [WP06-HUY]",
            hoaDon.Number, input.Reason, chungTuId);

        return new VoidRetailBillResultDto
        {
            Bill = ToDto(hoaDon, caDaKetThuc),
            Voided = true
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // Nội bộ
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 🆕 `WP-07` — áp **phạm vi dữ liệu** lên truy vấn DS (server-side, không tin client).
    /// 📌 Ba nhánh, mỗi nhánh **một dòng `Where`**: nếu phạm vi cần một khoá mà không có (`Own` thiếu
    /// người dùng / `Store` thiếu kho) thì trả **rỗng** — ⚠️ **không** rơi về "tất cả" (nới quyền im lặng).
    /// </summary>
    private async Task<IQueryable<RetailBill>> ApDungPhamViAsync(IQueryable<RetailBill> q)
    {
        var phamVi = await _phamVi.LayAsync();

        switch (phamVi.PhamVi)
        {
            case eCommerce.Customers.PhamViDuLieu.Own:
                return phamVi.NguoiDungId is { } nguoiDung
                    ? q.Where(x => x.CreatorId == nguoiDung)
                    : q.Where(_ => false);

            case eCommerce.Customers.PhamViDuLieu.Store:
                return phamVi.KhoCuaHangId is { } kho
                    ? q.Where(x => x.WarehouseId == kho)
                    : q.Where(_ => false);

            default:
                return q;
        }
    }

    /// <summary>Kiểm phạm vi cho **một** hoá đơn (đường đọc chi tiết) — ném `0028` nếu ngoài phạm vi.</summary>
    private async Task KiemPhamViAsync(RetailBill hoaDon)
    {
        var phamVi = await _phamVi.LayAsync();

        var ngoai = phamVi.PhamVi switch
        {
            eCommerce.Customers.PhamViDuLieu.Own =>
                phamVi.NguoiDungId is null || hoaDon.CreatorId != phamVi.NguoiDungId,
            eCommerce.Customers.PhamViDuLieu.Store =>
                phamVi.KhoCuaHangId is null || hoaDon.WarehouseId != phamVi.KhoCuaHangId,
            _ => false
        };

        if (ngoai)
        {
            throw new BusinessException(PosErrorCodes.NgoaiPhamViDuLieu)
                .WithData("billId", hoaDon.Id)
                .WithData("phamVi", phamVi.PhamVi.ToString())
                .WithData("lyDo", "Hoá đơn NGOÀI phạm vi dữ liệu của bạn — liên hệ người có phạm vi rộng hơn.");
        }
    }

    private async Task<RetailBill> LayHoaDonAsync(Guid id)
    {        var hoaDon = await _hoaDon.FindAsync(id)
            ?? throw new BusinessException(PosErrorCodes.KhongTimThayHoaDon).WithData("id", id);

        // 📌 Nạp con **tường minh** (không dựa `Include`): cùng khuôn `OrderAppService` của `WP-05`.
        hoaDon.GanDong(await _dongHoaDon.GetListAsync(x => x.RetailBillId == id));
        hoaDon.GanThanhToan(await _thanhToan.GetListAsync(x => x.RetailBillId == id));

        return hoaDon;
    }

    private async Task<CashSession> LayCaDangMoAsync(Guid warehouseId, Guid? cashSessionId)
    {
        var ca = cashSessionId is { } id
            ? await _ca.FindAsync(id)
              ?? throw new BusinessException(PosErrorCodes.KhongCoCaDangMo)
                  .WithData("cashSessionId", id)
                  .WithData("lyDo", "Ca chỉ định KHÔNG tồn tại.")
            : await _ca.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.Status == CashSessionStatus.Open)
              ?? throw new BusinessException(PosErrorCodes.KhongCoCaDangMo)
                  .WithData("warehouseId", warehouseId)
                  .WithData("lyDo", "Chưa MỞ CA cho quầy này — mở ca trước khi bán (tiền mặt phải có ca để đối chiếu).");

        if (ca.Status == CashSessionStatus.Closed)
        {
            throw new BusinessException(PosErrorCodes.CaDaKetThuc)
                .WithData("number", ca.Number)
                .WithData("lyDo", "Ca đã KẾT THÚC ⇒ không ghi thêm hoá đơn vào ca (số đã bàn giao).");
        }

        if (ca.WarehouseId != warehouseId)
        {
            throw new BusinessException(PosErrorCodes.ThieuKho)
                .WithData("warehouseId", warehouseId)
                .WithData("caWarehouseId", ca.WarehouseId)
                .WithData("lyDo", "Ca thuộc KHO khác với kho của hoá đơn.");
        }

        return ca;
    }

    /// <summary>
    /// Số hoá đơn `POS-yyyyMMdd-NNNN` — **đếm theo ngày**.
    /// ⚠️ **Nợ có mã:** đếm-rồi-cộng-1 **không** nguyên tử giữa hai máy POS; va chạm bị DB chặn
    /// (`IX_PosRetailBills_TenantId_Number` → `eCommerce:Pos:0024`) và client **gửi lại** là xong.
    /// 📌 Cách đúng dài hạn là **sequence của DB** — ghi ở `handover-n7` §5 (WP-09).
    /// </summary>
    private async Task<string> SinhSoHoaDonAsync(DateTime at)
    {
        var tienTo = $"POS-{at:yyyyMMdd}-";

        // ⚠️ `IRepository.GetCountAsync(predicate)` KHÔNG tồn tại (CS1660) — xem `PaymentAppService` §198.
        var truyVan = await _hoaDon.GetQueryableAsync();
        var dem = await AsyncExecuter.CountAsync(truyVan.Where(x => x.Number.StartsWith(tienTo)));

        return tienTo + (dem + 1).ToString("D4");
    }

    /// <summary>
    /// Hai lần gửi của **cùng** một hoá đơn phải trùng **kho + số dòng + tổng phải trả**.
    /// 📌 So `TotalPayment` **đã lưu** với số tính lại từ payload: nếu client sửa giá giữa hai lần gửi
    /// (hoặc dùng lại khoá cho hoá đơn khác) thì phát hiện được — chứ không im lặng trả hoá đơn cũ.
    /// </summary>
    private static bool CungNoiDung(RetailBill daCo, CreateRetailBillDto input)
    {
        if (daCo.WarehouseId != input.WarehouseId || daCo.Lines.Count != input.Lines.Count)
        {
            return false;
        }

        var tinhLai = TinhTienHoaDon.Tinh(
            input.Lines.Select(x => new TinhTienHoaDon.DongVao(x.Quantity, x.UnitPrice, x.DiscountAmount, x.VatPercent))
                .ToList(),
            input.DiscountAmount,
            TinhTienHoaDon.QuyDoiDiem(input.PointsUsed));

        return daCo.TotalPayment == tinhLai.TongThanhToan;
    }

    /// <summary>
    /// Dòng thanh toán **Điểm** phải khớp `PointsUsed` (nếu có): 📌 cho phép **không** khai dòng Điểm
    /// (điểm chỉ hiện ở tổng) nhưng nếu khai thì tổng phải bằng — lệch nghĩa là *"tiêu 5 điểm mà trừ tiền
    /// của 7 điểm"*, không ai đối chiếu được.
    /// </summary>
    private static void KiemDiemThanhToan(CreateRetailBillDto input, IReadOnlyList<RetailBillPayment> thanhToan)
    {
        var diem = thanhToan.Where(x => x.Method == RetailPaymentMethod.Points).Sum(x => x.Amount);

        if (diem == 0m)
        {
            return;
        }

        var tienDiem = TinhTienHoaDon.QuyDoiDiem(input.PointsUsed);

        if (diem != tienDiem)
        {
            throw new BusinessException(PosErrorCodes.DiemKhongHopLe)
                .WithData("diemThanhToan", diem)
                .WithData("tienDiemTheoSoDiem", tienDiem)
                .WithData("lyDo", "Dòng thanh toán Điểm KHÔNG khớp `PointsUsed`.");
        }
    }

    private static RetailBillDto ToDto(RetailBill x, bool caDaKetThuc) => new()
    {
        Id = x.Id,
        Number = x.Number,
        ClientBillId = x.ClientBillId,
        WarehouseId = x.WarehouseId,
        CashSessionId = x.CashSessionId,
        CustomerId = x.CustomerId,
        SaleStaffId = x.SaleStaffId,
        Status = x.Status.ToString(),
        Subtotal = x.Subtotal,
        LineDiscountTotal = x.LineDiscountTotal,
        DiscountTotal = x.DiscountTotal,
        VatTotal = x.VatTotal,
        PointsUsed = x.PointsUsed,
        PointsUsedAmount = x.PointsUsedAmount,
        PointsEarned = x.PointsEarned,
        TotalPayment = x.TotalPayment,
        PaidAmount = x.PaidAmount,
        ChangeAmount = x.ChangeAmount,
        DebtAmount = x.DebtAmount,
        CustomerGivenAmount = x.CustomerGivenAmount,
        DueDateUtc = x.DueDateUtc,
        Note = x.Note,
        StockDocumentId = x.StockDocumentId,
        VoidedAtUtc = x.VoidedAtUtc,
        VoidedBy = x.VoidedBy,
        VoidReason = x.VoidReason,
        VoidStockDocumentId = x.VoidStockDocumentId,
        CashSessionClosed = caDaKetThuc,
        CreationTime = x.CreationTime,
        Lines = x.Lines.Select(d => new RetailBillLineDto
        {
            Id = d.Id,
            Sku = d.Sku,
            ProductName = d.ProductName,
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            DiscountAmount = d.DiscountAmount,
            VatPercent = d.VatPercent,
            Amount = d.Amount,
            VatAmount = d.VatAmount,
            ReturnedQuantity = d.ReturnedQuantity
        }).ToList(),
        Payments = x.Payments.Select(t => new RetailBillPaymentDto
        {
            Id = t.Id,
            Method = t.Method.ToString(),
            Amount = t.Amount,
            ReferenceCode = t.ReferenceCode,
            Note = t.Note
        }).ToList(),
        PaymentMethods = x.Payments.Select(t => t.Method.ToString()).Distinct().ToList()
    };
}
