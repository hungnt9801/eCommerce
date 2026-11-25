using Volo.Abp.Settings;

namespace eCommerce.Tests.Pricing.HaTang;

/// <summary>
/// **`ISettingProvider` GIẢ** (`D-229` ①(A3)) — trả giá trị cấu hình từ một `Dictionary` và **ĐẾM số lần đọc
/// theo từng khoá**.
///
/// 🎯 Vì sao phải đếm: `PRC-002` **ADR-08**/*BR-PRC-002-010* chốt *"chính sách đọc ĐÚNG MỘT LẦN cho mỗi lần
/// tính giá"* — đọc lại giữa pipeline ⇒ hai dòng cùng một hoá đơn có thể dùng hai chính sách. Không đếm được
/// thì không chứng minh được bất biến đó (và ngược lại, gỡ một lượt `GetOrNullAsync` cũng không ai thấy).
///
/// 📌 Khoá **không có trong `Dictionary`** ⇒ trả `null` — đúng hành vi ABP khi setting chưa được đặt
/// (`-arch` §2.3: provider luôn trả `DefaultValue` ở Host thật; ở đây `null` đại diện cho "không có giá trị
/// ghi đè" và hàm đọc thuần sẽ tự áp mặc định + `hopLe = false`).
/// </summary>
public sealed class SettingGia : ISettingProvider
{
    private readonly Dictionary<string, string?> _giaTri;

    public SettingGia(Dictionary<string, string?>? giaTri = null)
        => _giaTri = giaTri ?? new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>Số lần đọc từng khoá (chỉ đếm `GetOrNullAsync`, đường mà pipeline thật dùng).</summary>
    public Dictionary<string, int> SoLanDoc { get; } = new(StringComparer.Ordinal);

    public int SoLanDocCua(string tenKhoa) => SoLanDoc.TryGetValue(tenKhoa, out var n) ? n : 0;

    public Task<string?> GetOrNullAsync(string name)
    {
        SoLanDoc[name] = SoLanDocCua(name) + 1;

        return Task.FromResult(_giaTri.TryGetValue(name, out var giaTri) ? giaTri : null);
    }

    public Task<List<SettingValue>> GetAllAsync(string[] names)
        => Task.FromResult(names
            .Select(n => new SettingValue(n, _giaTri.TryGetValue(n, out var v) ? v : null))
            .ToList());

    public Task<List<SettingValue>> GetAllAsync()
        => Task.FromResult(_giaTri.Select(kv => new SettingValue(kv.Key, kv.Value)).ToList());
}
