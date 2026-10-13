using Npgsql;
using Testcontainers.PostgreSql;

namespace eCommerce.IntegrationTests;

/// <summary>
/// Khởi động **một** PostgreSQL thật (container) cho cả bộ test và áp **schema sinh từ chính migration**.
///
/// 🎯 VÌ SAO NHÓM TEST NÀY TỒN TẠI: trong đợt kiểm chứng bằng chạy thật, **lỗi giá trị nhất (C4/F1)**
/// — unique index không chặn trùng khi `TenantId` NULL — **chỉ lộ ra khi chạy SQL trên PostgreSQL thật**.
/// Khi đó tôi phải gõ tay bằng `psql`. Các test dưới đây **tự động hoá đúng những phép thử đó** ⇒
/// lỗi ấy không thể quay lại mà không làm đỏ CI.
///
/// 📌 Dùng chung container cho cả collection (khởi động 1 lần) để không tốn thời gian mỗi test.
/// </summary>
public sealed class PostgreSqlSchemaFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Ghim image để kết quả không đổi theo thời gian (xem Q10 của XC-002).
        // ⚠️ Dùng constructor CÓ tham số image: constructor rỗng đã bị đánh dấu obsolete ở Testcontainers 4.x.
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();

        await _container.StartAsync();

        DataSource = NpgsqlDataSource.Create(_container.GetConnectionString());

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Assets", "schema.sql");
        var schemaSql = await File.ReadAllTextAsync(schemaPath);

        await using var command = DataSource.CreateCommand(schemaSql);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlSchemaFixture>
{
}
