using System.Text.Json;
using eCommerce.Kernel.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace eCommerce.HttpApi.Host.Filters;

/// <summary>
/// 🚨 **BỘ LỌC VI PHẠM RÀNG BUỘC DB → LỖI NGHIỆP VỤ** (trả nợ đo được ở vòng 26–28).
///
/// **VÌ SAO Ở TẦNG HOST (không phải tầng Domain/Application):** lỗi này chỉ **xuất hiện ở lúc `SaveChanges`**
/// — tức là **sau** khi app service đã trả về, do `UnitOfWork` hoàn tất. Không tầng nào phía trong bắt được nó.
/// 📌 Đây cũng là lý do ADR-09 (`UPDATE … WHERE Status='Draft'`) **không** đặt ở tầng Domain được: cùng một
/// vấn đề — *"kiểm tra nguyên tử chỉ có ở tầng DB"*.
///
/// **HÀNH VI:**
///   • `23505` / `23514` / `23503` **nhận ra** ⇒ trả JSON đúng dạng lỗi của ABP với **mã nghiệp vụ**
///     (`eCommerce:Inventory:0010`, `…:0032`, …) và HTTP **`403`** (cùng quy ước với `BusinessException` của hệ thống).
///   • 🆕 `AbpDbConcurrencyException` (xung đột đồng thời — hai người sửa cùng bản ghi, `ConcurrencyStamp` lệch)
///     ⇒ **`409`** với **một mã chung** `eCommerce:Db:0002` (`D-210`) để client biết **thử lại được**.
///     📌 Nhánh `409` kiểm **TRƯỚC** nhánh Postgres — hai loại **không chồng nhau**; đổi lại mã `403` nào.
///   • Lỗi **không nhận ra** (SQLSTATE khác, hoặc lỗi không phải Postgres) ⇒ **KHÔNG** can thiệp ⇒ nổi lên `500`
///     như cũ. 🚨 **Không bao giờ nuốt lỗi hệ thống thành lỗi nghiệp vụ** — làm vậy sẽ che mất sự cố thật.
/// </summary>
public class BoLocViPhamRangBuocDb : IAsyncExceptionFilter
{
    private readonly ILogger<BoLocViPhamRangBuocDb> _logger;

    public BoLocViPhamRangBuocDb(ILogger<BoLocViPhamRangBuocDb> logger)
    {
        _logger = logger;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        // 🆕 `D-210` (`WP-15`) — XUNG ĐỘT DỮ LIỆU ⇒ `409` + mã chung `eCommerce:Db:0002`.
        // 🚨 Kiểm TRƯỚC nhánh Postgres: `AbpDbConcurrencyException` và `PostgresException` KHÔNG chồng nhau,
        // nhưng nếu để ABP xử lý trước thì nó trả `409` với `code = null` (`D-66`①) — mất đúng thứ `D-66`⑦ cần.
        // 🔒 Chỉ nhánh này bị đổi; mọi hành vi `403` của ràng buộc DB **giữ nguyên**.
        if (ViPhamRangBuocDb.LaXungDotDuLieu(context.Exception))
        {
            _logger.LogWarning(
                "Xung đột dữ liệu đã được ánh xạ thành HTTP 409: Mã={Ma} LoạiLỗi={LoaiLoi} Đường dẫn: {Path}",
                ViPhamRangBuocDb.MaXungDot, context.Exception.GetType().Name,
                context.HttpContext.Request.Path);

            context.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

            // Cùng khuôn lỗi ABP để client chỉ cần xử lý MỘT định dạng (`code` là nguồn sự thật — `D6`).
            var thanXungDot = JsonSerializer.Serialize(new
            {
                error = new
                {
                    code = ViPhamRangBuocDb.MaXungDot,
                    message = ViPhamRangBuocDb.ThongDiepXungDot,
                    details = (string?)null,
                    data = new Dictionary<string, object?>(),
                    validationErrors = (object?)null
                }
            });

            await context.HttpContext.Response.WriteAsync(thanXungDot);

            // Đánh dấu ĐÃ xử lý ⇒ `AbpExceptionFilter` không ghi đè thành `409` + `code = null`.
            context.ExceptionHandled = true;
            return;
        }

        var pg = TimPostgres(context.Exception);

        if (pg is null)
        {
            return;   // không phải lỗi ràng buộc ⇒ để ABP xử lý (500 nếu là lỗi thật)
        }

        var kq = ViPhamRangBuocDb.AnhXa(pg.SqlState, pg.ConstraintName);

        if (kq is null)
        {
            return;   // SQLSTATE chưa hỗ trợ ⇒ để nổi lên 500 (KHÔNG đoán)
        }

        _logger.LogWarning(
            "Vi phạm ràng buộc DB đã được ánh xạ thành lỗi nghiệp vụ: SQLSTATE={SqlState} " +
            "RàngBuộc={RangBuoc} ⇒ Mã={Ma}. Đường dẫn: {Path}",
            pg.SqlState, pg.ConstraintName ?? "(không rõ)", kq.Code, context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

        // Dạng lỗi của ABP (`{"error":{"code":…,"message":…}}`) để client **không cần** xử lý hai định dạng.
        var than = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = kq.Code,
                message = kq.Message,
                details = (string?)null,
                data = new Dictionary<string, object?>
                {
                    ["sqlState"] = pg.SqlState,
                    ["constraint"] = pg.ConstraintName
                },
                validationErrors = (object?)null
            }
        });

        await context.HttpContext.Response.WriteAsync(than);

        // Đánh dấu ĐÃ xử lý ⇒ ABP không ghi đè thành 500.
        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Tìm `PostgresException` trong **chuỗi** exception. Phải đi sâu vì `SaveChanges` bọc nó trong
    /// `DbUpdateException` (và ABP có thể bọc thêm một lớp nữa).
    /// </summary>
    private static PostgresException? TimPostgres(Exception? ex)
    {
        var hienTai = ex;
        var soLan = 0;

        while (hienTai is not null && soLan++ < 10)
        {
            if (hienTai is PostgresException pg)
            {
                return pg;
            }

            hienTai = hienTai.InnerException;
        }

        return null;
    }
}
