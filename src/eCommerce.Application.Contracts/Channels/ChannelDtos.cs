using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Channels.Dtos;

/// <summary>DTO đọc một shop đã kết nối. Không bao giờ chứa token (kể cả đã mã hoá).</summary>
public class ChannelShopDto : AuditedEntityDto<Guid>
{
    public string ChannelCode { get; set; } = default!;
    public string ExternalShopId { get; set; } = default!;
    public string ShopName { get; set; } = default!;
    public string? Region { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Đã có refresh token hay chưa (chỉ trả cờ, không trả giá trị).</summary>
    public bool HasRefreshToken { get; set; }

    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public DateTime? AuthorizationExpiresAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? PushStatus { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// DTO tạo/kết nối shop. Access token nhận vào từ luồng OAuth, được mã hoá ngay ở application layer
/// (<c>IStringEncryptionService</c>) trước khi lưu.
/// </summary>
public class ConnectChannelShopDto
{
    [Required]
    [StringLength(32)]
    public string ChannelCode { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string ExternalShopId { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string ShopName { get; set; } = default!;

    [StringLength(16)]
    public string? Region { get; set; }

    [Required]
    public string AccessToken { get; set; } = default!;

    public string? RefreshToken { get; set; }

    public DateTime? AccessTokenExpiresAtUtc { get; set; }

    public DateTime? RefreshTokenExpiresAtUtc { get; set; }

    public DateTime? AuthorizationExpiresAtUtc { get; set; }
}

/// <summary>Kết quả một lần đồng bộ — dùng cho cả chạy tay và chạy nền.</summary>
public class ChannelSyncResultDto
{
    public Guid ChannelShopId { get; set; }
    public string ChannelCode { get; set; } = default!;
    public int OrdersPulled { get; set; }
    public int ListingsPushed { get; set; }
    public int Errors { get; set; }
    public bool RateLimited { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
}
