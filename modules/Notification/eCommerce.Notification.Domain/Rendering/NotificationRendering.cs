using System.Globalization;
using Volo.Abp;

namespace eCommerce.Notification;

/// <summary>Một placeholder trong whitelist của version: `{name, kind, required}`.</summary>
public sealed record NotificationPlaceholder(string Name, NotificationPlaceholderKind Kind, bool Required);

/// <summary>Nội dung của **MỘT** version đã `Published` — đầu vào của renderer thuần.</summary>
public sealed record NotificationTemplateContent(
    string? Subject,
    string BodyText,
    IReadOnlyList<NotificationPlaceholder> Placeholders);

/// <summary>
/// Kết quả render. `BodyFingerprint` = SHA-256(Subject + Body) — renderer **thuần** không biết
/// `RecipientHash`/`TemplateVersionId` (đổi tên từ `ContentFingerprint`, `D-261`).
/// </summary>
public sealed record RenderResult(
    string Subject,
    string Body,
    string BodyFingerprint,
    IReadOnlyList<string> MissingRequiredPlaceholders);

/// <summary>
/// ENGINE RENDER — **HÀM THUẦN** (ADR-06, `D-258`). KHÔNG DB, KHÔNG I/O, KHÔNG async, KHÔNG `DateTime.Now`.
/// Cùng input ⇒ cùng output (điều kiện để ADR-02 có nghĩa).
/// </summary>
public interface INotificationRenderer
{
    RenderResult Render(
        NotificationTemplateContent template,
        IReadOnlyDictionary<string, string> data,
        string locale,
        INotificationMoneyFormatter moneyFormatter);
}

/// <summary>**MỘT** hàm định dạng tiền (ADR-07): VND **0** chữ số, `vi-VN`.</summary>
public interface INotificationMoneyFormatter
{
    string Format(decimal amount);
}

/// <summary>Định dạng tiền theo `vi-VN`, VND 0 chữ số thập phân (ADR-07/`XC-001`).</summary>
public sealed class NotificationMoneyFormatter : INotificationMoneyFormatter, Volo.Abp.DependencyInjection.ITransientDependency
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public string Format(decimal amount) => amount.ToString("N0", Vi);
}

/// <summary>
/// Loại `\r`/`\n`/NUL khỏi **Subject** và **giá trị thay vào** — **một** hàm thuần (BR-024, `TC-43`).
/// </summary>
public static class NotificationTextSanitizer
{
    public static string LoaiKyTuDieuKhien(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '\r' or '\n' or '\0')
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}

/// <summary>Hiện thực renderer — thay `{{Ten}}` **chỉ** với placeholder đã khai (`D-258`).</summary>
public sealed class NotificationRenderer : INotificationRenderer, Volo.Abp.DependencyInjection.ITransientDependency
{
    public RenderResult Render(
        NotificationTemplateContent template,
        IReadOnlyDictionary<string, string> data,
        string locale,
        INotificationMoneyFormatter moneyFormatter)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(moneyFormatter);

        var whitelist = new Dictionary<string, NotificationPlaceholder>(StringComparer.Ordinal);
        foreach (var p in template.Placeholders)
        {
            whitelist[p.Name] = p;
        }

        // ① Placeholder ngoài whitelist ⇒ NÉM 0015 (TC-35).
        foreach (var ten in RutTenPlaceholder(template.Subject).Concat(RutTenPlaceholder(template.BodyText)))
        {
            if (!whitelist.ContainsKey(ten))
            {
                throw new BusinessException(NotificationErrorCodes.PlaceholderNgoaiWhitelist)
                    .WithData("ten", ten);
            }
        }

        // ② Thiếu giá trị bắt buộc ⇒ NÉM 0013 (TC-35).
        var thieu = new List<string>();
        foreach (var p in template.Placeholders)
        {
            if (!p.Required)
            {
                continue;
            }

            if (!data.TryGetValue(p.Name, out var v) || string.IsNullOrEmpty(v))
            {
                thieu.Add(p.Name);
            }
        }

        thieu = thieu
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (thieu.Count > 0)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc)
                .WithData("missing", string.Join(",", thieu));
        }

        var subject = ThayThe(template.Subject ?? string.Empty, whitelist, data, moneyFormatter);
        var body = ThayThe(template.BodyText ?? string.Empty, whitelist, data, moneyFormatter);

        // Subject loại CR/LF/NUL ở MỘT hàm thuần; giá trị thay vào đã được loại khi thay.
        subject = NotificationTextSanitizer.LoaiKyTuDieuKhien(subject);

        return new RenderResult(
            subject,
            body,
            ContentFingerprint.TinhThan(subject, body),
            []);
    }

    private static string ThayThe(
        string mau,
        IReadOnlyDictionary<string, NotificationPlaceholder> whitelist,
        IReadOnlyDictionary<string, string> data,
        INotificationMoneyFormatter moneyFormatter)
    {
        if (string.IsNullOrEmpty(mau))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(mau.Length);
        var i = 0;
        while (i < mau.Length)
        {
            var mo = mau.IndexOf("{{", i, StringComparison.Ordinal);
            if (mo < 0)
            {
                sb.Append(mau, i, mau.Length - i);
                break;
            }

            var dong = mau.IndexOf("}}", mo + 2, StringComparison.Ordinal);
            if (dong < 0)
            {
                sb.Append(mau, i, mau.Length - i);
                break;
            }

            sb.Append(mau, i, mo - i);
            var ten = mau.Substring(mo + 2, dong - mo - 2).Trim();

            if (whitelist.TryGetValue(ten, out var placeholder) && data.TryGetValue(ten, out var giaTri))
            {
                sb.Append(NotificationTextSanitizer.LoaiKyTuDieuKhien(DinhDang(placeholder.Kind, giaTri, moneyFormatter)));
            }
            else
            {
                // Optional thiếu giá trị ⇒ thay chuỗi rỗng (đã qua kiểm required ở trên).
                sb.Append(string.Empty);
            }

            i = dong + 2;
        }

        return sb.ToString();
    }

    private static string DinhDang(NotificationPlaceholderKind kind, string giaTri, INotificationMoneyFormatter moneyFormatter)
    {
        if (kind != NotificationPlaceholderKind.Money)
        {
            return giaTri;
        }

        // Tiền: nhận chuỗi số đã chuẩn hoá ⇒ định dạng qua **một** hàm (ADR-07); nếu đã là chuỗi đã
        // định dạng thì dùng nguyên (khuôn tinh thần `INotificationMoneyFormatter` một cửa).
        if (decimal.TryParse(giaTri, NumberStyles.Number, CultureInfo.InvariantCulture, out var so))
        {
            return moneyFormatter.Format(so);
        }

        return giaTri;
    }

    private static IEnumerable<string> RutTenPlaceholder(string? mau)
    {
        if (string.IsNullOrEmpty(mau))
        {
            yield break;
        }

        var i = 0;
        while (i < mau.Length)
        {
            var mo = mau.IndexOf("{{", i, StringComparison.Ordinal);
            if (mo < 0)
            {
                yield break;
            }

            var dong = mau.IndexOf("}}", mo + 2, StringComparison.Ordinal);
            if (dong < 0)
            {
                yield break;
            }

            yield return mau.Substring(mo + 2, dong - mo - 2).Trim();
            i = dong + 2;
        }
    }
}

/// <summary>Serialize/deserialize whitelist placeholder sang `jsonb` (`[{name,kind,required}]`).</summary>
public static class NotificationPlaceholderJson
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public static string Serialize(IReadOnlyList<NotificationPlaceholder> list)
        => System.Text.Json.JsonSerializer.Serialize(list, Options);

    public static IReadOnlyList<NotificationPlaceholder> Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<NotificationPlaceholder>>(json, Options) ?? [];
}
