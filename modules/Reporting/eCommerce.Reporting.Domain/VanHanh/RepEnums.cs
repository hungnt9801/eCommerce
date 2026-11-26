namespace eCommerce.Reporting;

/// <summary>
/// **PHÉP CHIẾU (PROJECTION) CỦA READ-MODEL** — `WP-26`, `REP-001` `D-271`.
///
/// 🔒 Số là **hợp đồng dữ liệu** (`D-17`/`D-6`): lưu DB dạng số, **CHỈ THÊM Ở CUỐI**.
/// 📌 `BanHang` gộp **hai** nguồn doanh thu (`BanLe` + `DonHang`); `TonKho` là ảnh trạng thái hiện tại.
/// </summary>
public enum RepProjection : short
{
    /// <summary>0 — doanh thu bán hàng (bán lẻ + đơn hàng).</summary>
    BanHang = 0,

    /// <summary>1 — ảnh tồn kho (trạng thái hiện tại, không phải lịch sử — `D-47` ⑦).</summary>
    TonKho = 1
}

/// <summary>
/// **TRẠNG THÁI MỘT LƯỢT CHẠY** (`RepRunLog`) — `REP-001` §3.1. Mọi chuyển sai NÉM `0015`.
/// 🔒 Số cố định, chỉ thêm ở cuối. `Skipped` **dự trữ** cho `WP-26` (§6.3, `docDeviation #5`).
/// </summary>
public enum RepRunStatus : short
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    PartiallySucceeded = 3,
    Failed = 4,
    Skipped = 5
}

/// <summary>
/// **TRẠNG THÁI KỲ KẾ TOÁN** (`RepPeriodClose`) — `REP-001` §3.3.
/// ⚠️ `DaChot = 2` được **hằng hoá** trong ràng buộc `EXCLUDE USING gist` (migration) — KHÔNG đổi số.
/// </summary>
public enum RepPeriodStatus : short
{
    Mo = 0,
    DangChot = 1,
    DaChot = 2,
    DaMoLai = 3
}

/// <summary>
/// **LOẠI LỆCH SỐ** (`RepConsistencyCheck`) — `REP-001` BR-012. 🔒 Chỉ thêm ở cuối.
/// </summary>
public enum RepMismatchKind : short
{
    SoDong = 0,
    DoanhThu = 1,
    SoChungTu = 2,
    AnhTonKho = 3,

    /// <summary>4 — sự kiện đến muộn rơi vào kỳ đã chốt (không có số — `ExpectedValue`/`ActualValue` NULL).</summary>
    SuKienTre = 4
}

/// <summary>Tiện ích chuyển đổi <see cref="RepProjection"/> sang mã chuỗi ổn định (dùng trong idempotency key).</summary>
public static class RepProjectionExtensions
{
    /// <summary>Mã chuỗi ổn định: `ban-hang` / `ton-kho` (KHÔNG dịch theo ngôn ngữ).</summary>
    public static string MaChuoi(this RepProjection projection)
        => projection == RepProjection.BanHang ? "ban-hang" : "ton-kho";

    /// <summary>`true` nếu phép chiếu là doanh thu (bán lẻ + đơn hàng), ngược lại là ảnh tồn kho.</summary>
    public static bool LaDoanhThu(this RepProjection projection)
        => projection == RepProjection.BanHang;
}
