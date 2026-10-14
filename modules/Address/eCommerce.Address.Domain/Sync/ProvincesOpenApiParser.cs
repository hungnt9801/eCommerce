using System.Text.Json;
using Volo.Abp;

namespace eCommerce.Address.Sync;

/// <summary>
/// **LÕI THUẦN phân tích JSON của `provinces.open-api.vn`** — không I/O ⇒ test được bằng
/// **JSON THẬT đã thu thập từ API**.
///
/// 📌 HÌNH DẠNG THẬT (đã kiểm tra trực tiếp ngày 14/10/2026, KHÔNG suy đoán):
/// ```text
/// v2  GET /api/v2/p/            → 34 tỉnh, trường `wards` RỖNG
/// v2  GET /api/v2/p/1?depth=2   → { name, code, …, wards: [ { name, code, province_code, … } ] }
/// v1  GET /api/v1/p/            → 63 tỉnh
/// v1  GET /api/v1/p/4?depth=3   → { …, districts: [ { name, code, province_code,
///                                                            wards: [ { name, code, district_code } ] } ] }
/// ```
///
/// 🚨 BA CẠM BẪY ĐÃ XỬ LÝ (mỗi cái đều có test):
///   1. **`wards` rỗng ở endpoint danh sách** ⇒ nếu chỉ gọi `/p/` thì sẽ thu được **34 tỉnh và 0 xã**.
///      Danh mục như vậy **thiếu toàn bộ cấp xã**, và nếu đem áp dụng thì sẽ **đóng sạch xã đang dùng**
///      (hàng rào 4 sẽ chặn, nhưng đúng là phải gọi `?depth=2` theo từng tỉnh).
///   2. **Mã là SỐ NGUYÊN** (`4`, `1267`) còn `AdministrativeUnit.Code` là **chuỗi đủ 5 ký tự**
///      ⇒ PHẢI **đệm 0 bên trái**. Nếu quên, `ChuanHoaMa` sẽ ném `MaKhongHopLe` — hoặc tệ hơn, nếu ai đó
///      "sửa" bằng cách nối chuỗi thì mã sẽ **sai âm thầm** và không tra được địa chỉ.
///   3. **v1 và v2 khác cấu trúc** (v2: `province.wards`; v1: `province.districts[].wards`).
/// </summary>
public static class ProvincesOpenApiParser
{
    /// <summary>Tên nguồn ghi vào dấu vết đồng bộ.</summary>
    public const string TenNguon = "provinces.open-api.vn";

    /// <summary>Lấy danh sách mã tỉnh từ JSON của endpoint danh sách (`/api/v2/p/`).</summary>
    public static IReadOnlyList<string> LayMaTinh(string json)
    {
        using var tai_lieu = JsonDocument.Parse(json);

        if (tai_lieu.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                .WithData("lyDo", "JSON danh sách tỉnh không phải mảng.")
                .WithData("valueKind", tai_lieu.RootElement.ValueKind.ToString());
        }

        var ma = new List<string>();

        foreach (var tinh in tai_lieu.RootElement.EnumerateArray())
        {
            ma.Add(DocMa(tinh, AdministrativeLevel.Tinh));
        }

        return ma;
    }

    /// <summary>
    /// Phân tích JSON của **MỘT tỉnh** (một lần gọi `?depth=2` hoặc `?depth=3`) thành danh sách đơn vị,
    /// **bao gồm chính tỉnh đó**.
    /// </summary>
    public static IReadOnlyList<DonViNguon> PhanTichTinh(string json, int schemaVersion)
    {
        using var tai_lieu = JsonDocument.Parse(json);
        var goc = tai_lieu.RootElement;

        if (goc.ValueKind != JsonValueKind.Object)
        {
            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                .WithData("lyDo", "JSON tỉnh không phải đối tượng.")
                .WithData("valueKind", goc.ValueKind.ToString());
        }

        var maTinh = DocMa(goc, AdministrativeLevel.Tinh);
        var ds = new List<DonViNguon>
        {
            new(schemaVersion, AdministrativeLevel.Tinh, maTinh, DocTen(goc), null)
        };

        // ── v2 (2 cấp): xã nằm TRỰC TIẾP trong tỉnh ──
        if (goc.TryGetProperty("wards", out var wards) && wards.ValueKind == JsonValueKind.Array)
        {
            foreach (var xa in wards.EnumerateArray())
            {
                ds.Add(new DonViNguon(
                    schemaVersion, AdministrativeLevel.Xa, DocMa(xa, AdministrativeLevel.Xa), DocTen(xa), maTinh));
            }
        }

        // ── v1 (3 cấp): huyện, và xã nằm TRONG huyện ──
        if (goc.TryGetProperty("districts", out var huyens) && huyens.ValueKind == JsonValueKind.Array)
        {
            foreach (var huyen in huyens.EnumerateArray())
            {
                var maHuyen = DocMa(huyen, AdministrativeLevel.QuanHuyen);
                ds.Add(new DonViNguon(
                    schemaVersion, AdministrativeLevel.QuanHuyen, maHuyen, DocTen(huyen), maTinh));

                if (!huyen.TryGetProperty("wards", out var xaCuaHuyen) || xaCuaHuyen.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var xa in xaCuaHuyen.EnumerateArray())
                {
                    ds.Add(new DonViNguon(
                        schemaVersion, AdministrativeLevel.Xa, DocMa(xa, AdministrativeLevel.Xa), DocTen(xa), maHuyen));
                }
            }
        }

        return ds;
    }

    /// <summary>Ghép danh mục của nhiều tỉnh thành một danh mục nguồn.</summary>
    public static DanhMucNguon Ghep(
        IEnumerable<IReadOnlyList<DonViNguon>> cacTinh,
        DateTime taiLucUtc,
        string nguon = TenNguon)
    {
        ArgumentNullException.ThrowIfNull(cacTinh);

        return new DanhMucNguon(cacTinh.SelectMany(x => x).ToList(), nguon, taiLucUtc);
    }

    /// <summary>
    /// Đọc mã và **đệm 0 bên trái** cho đủ độ dài của cấp. Ném lỗi nếu thiếu/rỗng/sai — **không im lặng**.
    /// </summary>
    private static string DocMa(JsonElement phanTu, AdministrativeLevel cap)
    {
        if (!phanTu.TryGetProperty("code", out var ma) || ma.ValueKind != JsonValueKind.Number)
        {
            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                .WithData("lyDo", "Thiếu trường `code` hoặc không phải số.")
                .WithData("level", cap.ToString())
                .WithData("ten", phanTu.TryGetProperty("name", out var t) ? t.ToString() : "(không có tên)");
        }

        var so = ma.GetInt64();
        var doDai = AdministrativeSchemaVersion.DoDaiMa(cap);
        var chuoi = so.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (chuoi.Length > doDai)
        {
            // 🚨 KHÔNG cắt bớt: mã dài hơn quy ước nghĩa là hiểu sai định dạng ⇒ phải dừng, không đoán.
            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                .WithData("lyDo", "Mã dài hơn độ dài quy ước của cấp.")
                .WithData("level", cap.ToString())
                .WithData("ma", chuoi)
                .WithData("doDaiYeuCau", doDai);
        }

        return chuoi.PadLeft(doDai, '0');
    }

    private static string DocTen(JsonElement phanTu)
    {
        if (!phanTu.TryGetProperty("name", out var ten) || string.IsNullOrWhiteSpace(ten.GetString()))
        {
            throw new BusinessException(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang)
                .WithData("lyDo", "Thiếu trường `name`.");
        }

        return ten.GetString()!.Trim();
    }
}
