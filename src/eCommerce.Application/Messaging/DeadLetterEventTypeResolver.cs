using System.Reflection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace eCommerce.Messaging;

/// <summary>
/// Ánh xạ **`TenSuKien` đã lưu trong DLQ** ⇒ **`Type` thật** để deserialize payload trước khi phát lại.
/// </summary>
public interface IDeadLetterEventTypeResolver
{
    /// <summary>
    /// Trả **đúng một** loại sự kiện khớp `tenSuKien`.
    /// 🚨 **FAIL-CLOSED:** không tìm thấy **hoặc** nhập nhằng ⇒ **NÉM** `eCommerce:Messaging:0009`.
    /// 🔒 **KHÔNG** bao giờ trả một loại *"đoán"*: phát lại sai kiểu làm consumer nhận dữ liệu vô nghĩa
    /// mà **không có lỗi nào** báo.
    /// </summary>
    Type Giai(string tenSuKien);
}

/// <summary>
/// 🚨 **BỘ ÁNH XẠ `TenSuKien` → `Type`** (`S16b`) — điều kiện **bắt buộc** để phát lại một sự kiện thật.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO CẦN LỚP NÀY — và vì sao KHÔNG được "đoán":**
///
/// `MsgDeadLetters.TenSuKien` chỉ là **một chuỗi**. Hai dạng đã có thật trong DB:
/// <list type="number">
///   <item>**tên ngắn** — nơi ghi DLQ dùng `nameof(ChannelOrderPulledEto)` (đo được ở
///     `MarketplaceOrderIntakeHandler`);</item>
///   <item>**tên đầy đủ** — chú thích entity ghi ví dụ `eCommerce.ChannelHub.Events.ChannelOrderPulledEto`
///     (dạng ABP dùng làm **tên event trên bus**).</item>
/// </list>
/// ⇒ Bộ ánh xạ **chấp nhận cả hai**, nhưng **chỉ khi khớp DUY NHẤT**.
///
/// 🚨 **VÌ SAO FAIL-CLOSED (không có "phương án dự phòng"):**
/// Nếu không ánh xạ được mà vẫn publish (ví dụ bọc payload vào `object`, hoặc đoán theo tiền tố) thì
/// consumer **không có handler** ⇒ sự kiện **rơi vào khoảng không** và 📌 **không có lỗi nào** ở nơi phát.
/// 🔑 Khi đó ta vừa **mất bằng chứng** (`ReplayedAtUtc` đã ghi), vừa **mất sự kiện** — đúng kiểu
/// *"mất dữ liệu nhưng có vẻ như đã xử lý"* mà cả `XC-002` sinh ra để chống.
/// ⇒ **NÉM** `eCommerce:Messaging:0009` kèm **danh sách ứng viên** để người trực sửa dữ liệu/DLQ.
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** chỉ quét các assembly **đã nạp** có tên bắt đầu bằng `eCommerce`.
/// 📌 Nếu assembly của loại sự kiện chưa nạp, phép ánh xạ sẽ **không thấy** ⇒ **NÉM** (hướng **an toàn**:
/// không publish sai). Không quét `AppDomain` mỗi lần gọi vì lớp này là **singleton** — chỉ quét **một lần**.
/// </summary>
public class DeadLetterEventTypeResolver : IDeadLetterEventTypeResolver, ISingletonDependency
{
    private readonly Lazy<BangAnhXa> _bang;

    public DeadLetterEventTypeResolver()
    {
        // 🔒 `Lazy` + `ExecutionAndPublication`: nhiều request đồng thời chỉ quét MỘT lần.
        _bang = new Lazy<BangAnhXa>(() => BangAnhXa.Tu(LayUngVien()), isThreadSafe: true);
    }

    /// <inheritdoc />
    public Type Giai(string tenSuKien)
    {
        if (string.IsNullOrWhiteSpace(tenSuKien))
        {
            throw new BusinessException(MessagingErrorCodes.KhongXacDinhDuocLoaiSuKien)
                .WithData("tenSuKien", tenSuKien ?? "(null)")
                .WithData("lyDo", "DLQ không có tên loại sự kiện ⇒ không biết phát lại cái gì.");
        }

        var ten = tenSuKien.Trim();
        var bang = _bang.Value;

        if (bang.TheoFullName.TryGetValue(ten, out var theoFullName) && theoFullName.Count == 1)
        {
            return theoFullName[0];
        }

        if (theoFullName is { Count: > 1 })
        {
            throw NhapNhang(ten, theoFullName, "nhiều loại có CÙNG tên đầy đủ (không thể xảy ra trong .NET ⇒ dữ liệu DLQ bị sửa tay?)");
        }

        // 📌 Cho phép dạng "FullName, AssemblyName" (một số nơi ghi kiểu này) — cắt phần sau dấu phẩy.
        var viTriPhay = ten.IndexOf(',', StringComparison.Ordinal);
        if (viTriPhay > 0)
        {
            var chiFullName = ten[..viTriPhay].Trim();
            if (bang.TheoFullName.TryGetValue(chiFullName, out var theoFullNameRutGon) && theoFullNameRutGon.Count == 1)
            {
                return theoFullNameRutGon[0];
            }
        }

        if (bang.TheoTenNgan.TryGetValue(ten, out var theoTenNgan))
        {
            if (theoTenNgan.Count == 1)
            {
                return theoTenNgan[0];
            }

            // 🚨 NHẬP NHẰNG: hai loại cùng TÊN NGẮN ở hai namespace ⇒ không được chọn bừa.
            throw NhapNhang(ten, theoTenNgan, "có NHIỀU loại cùng TÊN NGẮN ở các namespace khác nhau");
        }

        throw new BusinessException(MessagingErrorCodes.KhongXacDinhDuocLoaiSuKien)
            .WithData("tenSuKien", ten)
            .WithData("soLoaiDaQuet", bang.SoLoai)
            .WithData("viDu",
                string.Join(", ", bang.TheoFullName.Keys.OrderBy(x => x, StringComparer.Ordinal).Take(5)))
            .WithData("lyDo",
                "Không tìm thấy loại sự kiện khớp tên trong các assembly đã nạp ⇒ KHÔNG phát lại "
                + "(phát lại một event 'đoán' = consumer không có handler, và KHÔNG có lỗi nào báo).");
    }

    /// <summary>Ứng viên được quét — `protected virtual` để test truyền **tập loại xác định** (không phụ thuộc AppDomain).</summary>
    protected virtual IReadOnlyList<Type> LayUngVien()
    {
        var ket = new List<Type>();

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic)
            {
                continue;
            }

            var tenAssembly = asm.GetName().Name;

            if (tenAssembly is null
                || !tenAssembly.StartsWith("eCommerce", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                ket.AddRange(asm.GetTypes());
            }
            catch (ReflectionTypeLoadException ex)
            {
                // ⚠️ Không nuốt im lặng: lấy phần nạp được. Loại nào không nạp được thì **không** được chọn
                // (hướng an toàn) — và nếu đó chính là loại cần tìm thì `Giai` sẽ **NÉM**.
                ket.AddRange(ex.Types.OfType<Type>());
            }
        }

        return ket;
    }

    private static BusinessException NhapNhang(string ten, IReadOnlyList<Type> ungVien, string viSao)
        => new BusinessException(MessagingErrorCodes.KhongXacDinhDuocLoaiSuKien)
            .WithData("tenSuKien", ten)
            .WithData("ungVien", string.Join(" | ", ungVien.Select(t => t.FullName ?? t.Name)))
            .WithData("lyDo",
                $"Tên sự kiện NHẬP NHẰNG — {viSao}. Không chọn bừa: phát lại sai loại là gửi dữ liệu "
                + "vô nghĩa cho consumer mà không có lỗi nào.");

    /// <summary>Bảng tra **dựng một lần**: hai từ điển `FullName` và `Name` (mỗi khoá giữ **mọi** loại khớp).</summary>
    private sealed class BangAnhXa
    {
        public Dictionary<string, List<Type>> TheoFullName { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<Type>> TheoTenNgan { get; } = new(StringComparer.Ordinal);

        public int SoLoai { get; private set; }

        public static BangAnhXa Tu(IReadOnlyList<Type> tatCa)
        {
            var bang = new BangAnhXa();

            var hopLe = tatCa
                .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
                .Where(t => t.FullName is not null)
                .Distinct()
                .ToList();

            foreach (var loai in hopLe)
            {
                Chen(bang.TheoFullName, loai.FullName!, loai);
                Chen(bang.TheoTenNgan, loai.Name, loai);
            }

            bang.SoLoai = hopLe.Count;

            return bang;
        }

        private static void Chen(Dictionary<string, List<Type>> tuDien, string khoa, Type loai)
        {
            if (!tuDien.TryGetValue(khoa, out var danhSach))
            {
                danhSach = [];
                tuDien[khoa] = danhSach;
            }

            danhSach.Add(loai);
        }
    }
}
