using eCommerce.Kernel.Money;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pricing;

/// <summary>
/// **Phạm vi áp dụng của bảng giá** (`PRC-001` §5.2) — quyết định bảng giá dùng cho **đối tượng nào**.
/// </summary>
public enum PriceListScope
{
    /// <summary>Toàn hệ thống (mọi kênh) — cấp **thấp nhất** trong thứ tự ưu tiên.</summary>
    All = 0,

    /// <summary>Một **kênh bán** (`ChannelCode`); `ChannelShopId` để trống.</summary>
    Channel = 1,

    /// <summary>Một **shop cụ thể** trên sàn (`ChannelCode` + `ChannelShopId`) — cấp **cao nhất**.</summary>
    ChannelShop = 2
}

/// <summary>
/// **Vòng đời bảng giá** (`PRC-001` D4/§3) — **tái dùng mô hình đã kiểm chứng** của `TaxClassification`
/// (`PRC-002` ADR-10).
///
/// 🚨 **LUẬT QUAN TRỌNG NHẤT:** `Active` là **append-only** — **CẤM** sửa/xoá (`PRC-001` **BR-004**).
/// Muốn đổi giá ⇒ tạo bảng giá **mới**. Nếu cho sửa bảng giá đang hiệu lực thì **đơn đã đặt** và
/// **báo cáo đã xuất** trỏ tới một con số **không còn tồn tại** ⇒ không đối soát được.
/// </summary>
public enum PriceListStatus
{
    Draft = 0,
    Approved = 1,
    Active = 2,
    Expired = 3,
    Superseded = 4,
    Cancelled = 5
}

/// <summary>
/// **Bảng giá có hiệu lực** (`PrcPriceLists`) — `PRC-001` D4/D8/BR-003/004/007.
///
/// 🚨 **MÁY TRẠNG THÁI PHẢI NÉM LỖI** (D12/BR-005): chuyển sai ⇒ `BusinessException` có mã, **cấm** no-op im lặng.
/// 📌 **Guard do THỜI GIAN trả `false`**, guard do **NGƯỜI/API thì NÉM** — `PRC-002` **ADR-11**, theo đúng
/// tiền lệ `ActivateIfDue`/`ExpireIfDue` của `TaxClassification`.
/// </summary>
public class PriceList : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã bảng giá, duy nhất theo `(TenantId, Code, EffectiveFrom)`.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; set; } = default!;

    public PriceListScope Scope { get; protected set; }

    /// <summary>Kênh áp dụng (`Channel.Code`). Bắt buộc khi `Scope != All`.</summary>
    public string? ChannelCode { get; protected set; }

    /// <summary>Shop cụ thể trên sàn. Chỉ dùng khi `Scope = ChannelShop`.</summary>
    public Guid? ChannelShopId { get; protected set; }

    /// <summary>Loại tiền của bảng giá — **cấm** trộn loại tiền trong một bảng giá (`PRC-001` BR-010).</summary>
    public Currency Currency { get; protected set; } = Currency.Vnd;

    public DateTime EffectiveFrom { get; protected set; }

    public DateTime? EffectiveTo { get; protected set; }

    /// <summary>Ưu tiên **trong cùng cấp** (D7: **cấp trước, priority sau**).</summary>
    public int Priority { get; set; }

    public PriceListStatus Status { get; protected set; } = PriceListStatus.Draft;

    protected PriceList() { }

    public PriceList(
        Guid id,
        string code,
        string name,
        PriceListScope scope,
        Currency currency,
        DateTime effectiveFrom,
        string? channelCode = null,
        Guid? channelShopId = null,
        DateTime? effectiveTo = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong)
                .WithData("truong", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong)
                .WithData("truong", nameof(name));
        }

        // 🚨 KIỂM TRA QUAN HỆ GIỮA `Scope` VÀ KÊNH — bất biến KHÔNG được DB bảo vệ:
        // `Scope = Channel` mà thiếu `ChannelCode` ⇒ bảng giá "theo kênh" **không gắn kênh nào** ⇒
        // nó **không bao giờ khớp** ở cấp 1/2 và **âm thầm** rơi xuống cấp 3 ⇒ bán **sai giá** mà không có lỗi nào.
        if (scope != PriceListScope.All && string.IsNullOrWhiteSpace(channelCode))
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Scope theo kênh/shop nhưng THIẾU `ChannelCode` ⇒ bảng giá sẽ không bao giờ khớp.")
                .WithData("scope", scope.ToString());
        }

        if (scope == PriceListScope.ChannelShop && channelShopId is null)
        {
            throw new BusinessException(PricingErrorCodes.KhongTimThayGia)
                .WithData("lyDo", "Scope = ChannelShop nhưng THIẾU `ChannelShopId`.")
                .WithData("scope", scope.ToString());
        }

        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong)
                .WithData("lyDo", "`EffectiveTo` phải SAU `EffectiveFrom`.")
                .WithData("tu", effectiveFrom)
                .WithData("den", effectiveTo);
        }

        Code = code.Trim();
        Name = name.Trim();
        Scope = scope;
        Currency = currency;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        ChannelCode = string.IsNullOrWhiteSpace(channelCode) ? null : channelCode.Trim();
        ChannelShopId = channelShopId;
    }

    /// <summary>Bảng giá có hiệu lực tại thời điểm <paramref name="at"/> không (BR-003: **bắt buộc kèm ngày**).</summary>
    public bool CoHieuLucTai(DateTime at)
        => at >= EffectiveFrom && (EffectiveTo is null || at <= EffectiveTo.Value);

    /// <summary>
    /// `Draft → Approved`. **NÉM** khi: không ở `Draft`, hoặc **chưa có dòng nào** (`PRC-001` E1).
    /// 📌 Cố ý **không** cho "phát hành bảng giá rỗng": bảng giá rỗng vẫn `Active` được ⇒ mọi SKU **thiếu giá**
    /// ở cấp 3 ⇒ tuỳ `MissingPricePolicy` mà **ném lỗi ở checkout** hoặc **bán giá niêm yết** — cả hai đều
    /// là hậu quả của một thao tác trông "thành công".
    /// </summary>
    public void PhatHanh(int soDongThucTe)
    {
        if (Status != PriceListStatus.Draft)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bảng giá ở `Draft` mới phát hành được.")
                .WithData("status", Status.ToString());
        }

        // 🚨 Số dòng được TRUYỀN VÀO (đọc từ DB ở tầng service), **KHÔNG** đọc từ thuộc tính trên entity.
        // LÝ DO (lỗi thật phát hiện 15/10/2026): phiên bản đầu lưu `SoDong` trên entity nhưng **không map**
        // (`Ignore`) ⇒ sau khi nạp lại từ DB nó **luôn = 0** ⇒ guard "phải có ≥ 1 dòng" khiến **mọi lần publish
        // đều thất bại** — mà nhìn như "đúng luật". Truyền số dòng từ nguồn sự thật (DB) là cách duy nhất đúng.
        if (soDongThucTe <= 0)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaThieuDong)
                .WithData("lyDo", "Bảng giá CHƯA CÓ DÒNG nào ⇒ không phát hành được.")
                .WithData("code", Code);
        }

        Status = PriceListStatus.Approved;
    }

    /// <summary>
    /// `Approved → Active` **nếu đã tới ngày**. ⚠️ Trả **`false`** khi chưa tới ngày (**KHÔNG** ném) —
    /// "chưa tới ngày" **không phải** vi phạm guard (`PRC-002` ADR-11).
    /// **NÉM** khi trạng thái không phải `Approved`.
    /// </summary>
    public bool KichHoatNeuToiNgay(DateTime at)
    {
        if (Status == PriceListStatus.Active)
        {
            return false;   // đã active: idempotent
        }

        if (Status != PriceListStatus.Approved)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bảng giá `Approved` mới kích hoạt được.")
                .WithData("status", Status.ToString());
        }

        if (at < EffectiveFrom)
        {
            return false;   // chưa tới ngày — KHÔNG phải lỗi
        }

        Status = PriceListStatus.Active;
        return true;
    }

    /// <summary>`Active → Expired` nếu đã quá hạn. Trả `false` khi chưa quá hạn (ADR-11).</summary>
    public bool HetHanNeuQuaNgay(DateTime at)
    {
        if (Status != PriceListStatus.Active)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bảng giá `Active` mới hết hạn được.")
                .WithData("status", Status.ToString());
        }

        if (EffectiveTo is null || at <= EffectiveTo.Value)
        {
            return false;
        }

        Status = PriceListStatus.Expired;
        return true;
    }

    /// <summary>`Active → Superseded` khi có bảng giá mới thay thế (append-only: **không** sửa, chỉ thay).</summary>
    public void BiThayThe()
    {
        if (Status != PriceListStatus.Active)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Chỉ bảng giá `Active` mới bị thay thế được.")
                .WithData("status", Status.ToString());
        }

        Status = PriceListStatus.Superseded;
    }

    /// <summary>Huỷ bảng giá **chưa** hiệu lực. Bảng giá `Active` **không** huỷ được (append-only — BR-004).</summary>
    public void Huy()
    {
        if (Status is PriceListStatus.Active or PriceListStatus.Expired or PriceListStatus.Superseded)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Bảng giá đã/đang hiệu lực ⇒ KHÔNG huỷ được (append-only).")
                .WithData("status", Status.ToString());
        }

        Status = PriceListStatus.Cancelled;
    }

    /// <summary>
    /// 🚨 **Chặn sửa nội dung khi đang/buộc phải bất biến** (`PRC-001` **BR-004**/E2).
    /// Mọi thao tác đổi dòng giá **phải** gọi hàm này trước — nếu không, "sửa giá trong bảng đang hiệu lực"
    /// sẽ **âm thầm** đổi giá của mọi đơn tính sau đó, còn đơn/báo cáo cũ thì trỏ tới con số **đã biến mất**.
    /// </summary>
    public void YeuCauChoPhepSuaNoiDung()
    {
        if (Status is PriceListStatus.Active or PriceListStatus.Expired or PriceListStatus.Superseded)
        {
            throw new BusinessException(PricingErrorCodes.BangGiaActiveKhongSuaDuoc)
                .WithData("lyDo", "Bảng giá đã/đang hiệu lực là APPEND-ONLY ⇒ tạo bảng giá MỚI thay vì sửa.")
                .WithData("status", Status.ToString())
                .WithData("code", Code);
        }
    }
}
