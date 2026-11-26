using System.Security.Cryptography;
using System.Text;
using Volo.Abp;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Notification;

/// <summary>
/// Domain của module <b>Notification</b> — template + hàng đợi + chống gửi trùng (`NOT-001`).
/// Module sở hữu entity của mình; KHÔNG tham chiếu app/host (module → app CẤM).
/// </summary>
[DependsOn(typeof(AbpDddDomainModule))]
public class NotificationDomainModule : AbpModule
{
}

/// <summary>Băm SHA-256 dạng hex thường — **một cửa** cho mọi fingerprint của module.</summary>
public static class NotificationHash
{
    public static string Sha256Hex(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool LaHex64(string? value)
        => value is { Length: NotificationConsts.HashHexLength }
           && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>Fingerprint nội dung render (chỉ Subject + Body) — renderer thuần tính.</summary>
public sealed record ContentFingerprint
{
    public string Sha256 { get; }

    public ContentFingerprint(string sha256)
    {
        if (!NotificationHash.LaHex64(sha256))
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc)
                .WithData("lyDo", "ContentFingerprint phải là SHA-256 hex 64 ký tự");
        }

        Sha256 = sha256;
    }

    /// <summary>`ContentFingerprint` của **MỤC** — appender tính (cần `TemplateVersionId` + `RecipientHash`).</summary>
    public static ContentFingerprint TinhMuc(Guid templateVersionId, string recipientHash, string renderedSubject, string renderedBody)
        => new(NotificationHash.Sha256Hex(
            $"{templateVersionId:N}|{recipientHash}|{renderedSubject}\u001f{renderedBody}"));

    /// <summary>`BodyFingerprint` cho `RenderResult` — chỉ Subject + Body.</summary>
    public static string TinhThan(string subject, string body)
        => NotificationHash.Sha256Hex($"{subject}\u001f{body}");
}

/// <summary>Khoá idempotency — bắt buộc, độ dài ≤ 256 (`D-191`/BR-002).</summary>
public sealed record IdempotencyKey
{
    public string Value { get; }

    private IdempotencyKey(string value) => Value = value;

    public static IdempotencyKey Tao(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(NotificationErrorCodes.KhoaXungDot)
                .WithData("lyDo", "thieu-khoa");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > NotificationConsts.IdempotencyKeyToiDa)
        {
            throw new BusinessException(NotificationErrorCodes.KhoaXungDot)
                .WithData("lyDo", "khoa-qua-dai");
        }

        return new IdempotencyKey(trimmed);
    }

    public override string ToString() => Value;
}

/// <summary>
/// Người nhận — chỉ **mã hoá** + **băm**, không lưu/không logplaintext (`D-191`).
/// `Hash` = SHA-256 của `"v1|" + ChuẩnHoá(recipient)` băm **một cửa**.
/// </summary>
public sealed record NotificationRecipient
{
    public string Encrypted { get; }
    public string Hash { get; }

    private NotificationRecipient(string encrypted, string hash)
    {
        Encrypted = encrypted;
        Hash = hash;
    }

    public static NotificationRecipient Cua(string encrypted, string hash)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc)
                .WithData("lyDo", "thieu-nguoi-nhan");
        }

        if (!NotificationHash.LaHex64(hash))
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc)
                .WithData("lyDo", "hash-nguoi-nhan-khong-hop-le");
        }

        return new NotificationRecipient(encrypted, hash);
    }

    /// <summary>Chuẩn hoá **một cửa**: cắt khoảng trắng + thường hoá.</summary>
    public static string ChuanHoa(string raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Băm một chiều để tra/khử trùng, không lộ PII.</summary>
    public static string TinhHash(string raw) => NotificationHash.Sha256Hex("v1|" + ChuanHoa(raw));
}
