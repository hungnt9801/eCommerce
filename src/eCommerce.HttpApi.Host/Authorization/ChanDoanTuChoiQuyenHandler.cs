using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Tracing;
using Volo.Abp.Users;

namespace eCommerce.Authorization;

/// <summary>
/// 🚨 **CHẨN ĐOÁN KHI BỊ TỪ CHỐI QUYỀN (`401`/`403`)** — bịt một **lỗ hổng quan sát** thật của dự án.
///
/// VÌ SAO CẦN (bài học `F-403`, đo được 14/10/2026):
/// Khi một request có token hợp lệ nhưng **thiếu quyền**, ASP.NET Core trả `403` và **KHÔNG ghi một dòng
/// log nào ở phía server**. Hệ quả: lỗi *"mọi API gắn quyền đều `403` dù DB đã cấp quyền"* **ẩn được rất
/// lâu** — chỉ nhìn thấy ở phía client, còn log server **hoàn toàn im lặng**. Đây đúng loại *"mù khi có sự
/// cố"* mà `XC-005` sinh ra để chống.
///
/// CÁCH LÀM: ghi **một** dòng `Warning` cho mỗi lần từ chối, kèm **đủ dữ kiện để chẩn đoán NGAY**:
///   • **tên type của MỌI requirement** trong policy (không phụ thuộc việc đoán đúng tên type);
///   • quyền nào không đạt, và quyền đó **có được ĐỊNH NGHĨA** hay không — 🚨 đây chính là nguyên nhân đã
///     xảy ra thật ở Phase 0 (`XC-004` D2/E3: *quyền chưa định nghĩa ⇒ từ chối TẤT CẢ, kể cả admin*);
///   • **các loại claim** đang có trên principal + **giá trị claim vai trò** — để phân biệt ngay ca
///     *token có `role` nhưng ABP không đọc được* (khác tên claim) với ca *thiếu grant*;
///   • `ICurrentUser.Roles` — **góc nhìn của ABP**, không phải góc nhìn của ta.
///
/// 🔒 KHÔNG log token và KHÔNG log giá trị claim ngoài claim vai trò.
///
/// ⚠️ Handler này **không** thay đổi quyết định cho phép/từ chối: nó chỉ **ghi nhận**, rồi chuyển tiếp cho
/// handler mặc định của ASP.NET Core để giữ nguyên mã trạng thái `401`/`403` (đã kiểm chứng bằng ma trận
/// `401`/`403`/`200`). Mọi lỗi trong khâu chẩn đoán đều bị nuốt — **không bao giờ** làm hỏng request.
///
/// 📌 **Vì sao đọc `PermissionName` bằng reflection thay vì tham chiếu type của ABP:** tên type
/// `PermissionRequirement` của ABP **không** tra được bằng tham chiếu biên dịch trong bản 10.7.0
/// (đã thử — `CS0246`), và việc phụ thuộc vào một tên type **nội bộ** sẽ làm bản chẩn đoán này **vỡ khi
/// ABP đổi tên**. Đọc theo **tên thuộc tính** bền hơn. ⚠️ Đổi lại, nếu ABP đổi tên thuộc tính thì phép đọc
/// sẽ trượt — nên log **luôn** in ra **tên type của mọi requirement**, để không bao giờ thất bại im lặng.
/// </summary>
public sealed class ChanDoanTuChoiQuyenHandler : IAuthorizationMiddlewareResultHandler
{
    /// <summary>Handler mặc định của ASP.NET Core — vẫn là nơi quyết định mã trạng thái trả về.</summary>
    private readonly AuthorizationMiddlewareResultHandler _macDinh = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged || authorizeResult.Forbidden)
        {
            await GhiNhanAsync(context, policy, authorizeResult);
        }

        await _macDinh.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task GhiNhanAsync(
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        try
        {
            var services = context.RequestServices;
            var logger = services.GetRequiredService<ILogger<ChanDoanTuChoiQuyenHandler>>();

            var ketQua = authorizeResult.Challenged
                ? "401 CHALLENGE (chưa xác thực / token không hợp lệ)"
                : "403 FORBIDDEN (đã xác thực nhưng KHÔNG đủ quyền)";

            var endpoint = context.GetEndpoint();
            var route = endpoint?.DisplayName ?? context.Request.Path.Value ?? "(không rõ)";
            var correlationId = services.GetService<ICorrelationIdProvider>()?.Get();

            var chiTiet = new StringBuilder();
            chiTiet.AppendLine($"🚫 TỪ CHỐI QUYỀN — {ketQua}");
            chiTiet.AppendLine($"   request     : {context.Request.Method} {context.Request.Path}");
            chiTiet.AppendLine($"   endpoint    : {route}");
            chiTiet.AppendLine($"   correlation : {correlationId ?? "(rỗng)"}");
            chiTiet.AppendLine($"   xác thực    : {context.User.Identity?.IsAuthenticated == true} · tên = '{context.User.Identity?.Name}'");

            // ── Claim đang có: TRẢ LỜI TRỰC TIẾP câu hỏi "token có claim vai trò không, và dưới TÊN nào?"
            var cacLoaiClaim = context.User.Claims
                .Select(c => c.Type)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            chiTiet.AppendLine($"   loại claim  : {(cacLoaiClaim.Count == 0 ? "(KHÔNG có claim nào)" : string.Join(", ", cacLoaiClaim))}");

            var claimVaiTro = context.User.Claims
                .Where(c => c.Type.Contains("role", StringComparison.OrdinalIgnoreCase))
                .Select(c => $"{c.Type}={c.Value}")
                .ToList();

            chiTiet.AppendLine($"   claim vai trò: {(claimVaiTro.Count == 0 ? "(KHÔNG có claim nào chứa 'role')" : string.Join(" · ", claimVaiTro))}");

            // ── Góc nhìn của ABP (thứ thực sự quyết định)
            var nguoiDungHienTai = services.GetService<ICurrentUser>();
            var vaiTroCuaAbp = nguoiDungHienTai?.Roles?.ToList();
            chiTiet.AppendLine(
                $"   ABP current user: id='{nguoiDungHienTai?.Id}' · IsAuthenticated={nguoiDungHienTai?.IsAuthenticated} · " +
                $"Roles=[{(vaiTroCuaAbp is null || vaiTroCuaAbp.Count == 0 ? "(RỖNG)" : string.Join(", ", vaiTroCuaAbp))}]");

            // ── Tên type của MỌI requirement: bảo hiểm chống "chẩn đoán trượt mà im lặng"
            chiTiet.AppendLine(
                $"   requirements: {(policy.Requirements.Count == 0 ? "(không có)" : string.Join(", ", policy.Requirements.Select(r => r.GetType().Name)))}");

            // ── Từng yêu cầu quyền: có ĐỊNH NGHĨA không, và có được cấp không
            var quanLyDinhNghia = services.GetService<IPermissionDefinitionManager>();
            var kiemTraQuyen = services.GetService<IPermissionChecker>();

            var tenQuyen = policy.Requirements
                .Select(r => r.GetType().GetProperty("PermissionName")?.GetValue(r) as string)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (tenQuyen.Count == 0)
            {
                chiTiet.AppendLine(
                    "   quyền       : (KHÔNG đọc được 'PermissionName' từ requirement nào — xem 'requirements' ở trên)");
            }

            foreach (var ten in tenQuyen)
            {
                string coDinhNghia;

                try
                {
                    coDinhNghia = quanLyDinhNghia is null
                        ? "⚠️ KHÔNG resolve được IPermissionDefinitionManager"
                        : (await quanLyDinhNghia.GetOrNullAsync(ten!) is null
                            ? "❌ CHƯA ĐƯỢC ĐỊNH NGHĨA ⇒ ABP từ chối MỌI người (kể cả admin) — xem XC-004 D2/E3"
                            : "✅ đã định nghĩa");
                }
                catch (Exception ex)
                {
                    // Ghi cả kiểu lỗi: nếu việc tra định nghĩa NÉM lỗi thì đó là nguyên nhân, không phải "thiếu".
                    coDinhNghia = $"⚠️ tra định nghĩa NÉM LỖI: {ex.GetType().Name}: {ex.Message}";
                }

                string duocCap;

                try
                {
                    duocCap = kiemTraQuyen is null
                        ? "⚠️ KHÔNG resolve được IPermissionChecker"
                        : (await kiemTraQuyen.IsGrantedAsync(ten!) ? "✅ được cấp" : "❌ KHÔNG được cấp cho principal hiện tại");
                }
                catch (Exception ex)
                {
                    duocCap = $"⚠️ kiểm tra quyền NÉM LỖI: {ex.GetType().Name}: {ex.Message}";
                }

                chiTiet.AppendLine($"   quyền       : '{ten}' · định nghĩa: {coDinhNghia} · cấp: {duocCap}");
            }

            logger.LogWarning("{ChanDoan}", chiTiet.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            // Chẩn đoán KHÔNG được phép làm hỏng request. Nếu chính nó lỗi thì ghi lại — nhưng vẫn đi tiếp.
            try
            {
                context.RequestServices
                    .GetRequiredService<ILogger<ChanDoanTuChoiQuyenHandler>>()
                    .LogWarning(ex, "Chẩn đoán từ chối quyền gặp lỗi (bỏ qua, request vẫn được xử lý bình thường).");
            }
            catch
            {
                // Không còn cách nào ghi lại — nuốt để không ảnh hưởng request.
            }
        }
    }
}
