using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Portfolio.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Portfolio;

// D-117: Phase 1 rows (status Received) become Rejected with Portfolio.Csv.NotValidated. Runs the real migrations on a
// fresh database in the shared container: up to AddSubmissions, seed through SQL, then up to AddCsvValidation.
public sealed class AddCsvValidationMigrationTests(ApiFixture fixture)
{
    private const string BeforeMigration = "20261119163653_AddSubmissions";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_PhaseOneReceivedRows_BecomeRejectedWithNotValidated()
    {
        var connectionString = await CreateDatabaseAsync();
        var submissionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(BeforeMigration, _ct);
        await ExecuteAsync(connectionString,
            """
            INSERT INTO portfolio.submissions (id, tenant_id, cut_off_date, file_name, size_bytes, sha256, status, received_at)
            VALUES (@id, @tenantId, DATE '2026-01-01', 'old.csv', 10, repeat('a', 64), 'Received', now());
            """, submissionId, tenantId);
        await migrator.MigrateAsync(cancellationToken: _ct);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(_ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT s.status, s.error_count, s.row_count, e.ordinal, e.tenant_id, e.row_number IS NULL, e."column" IS NULL,
                   e.code, e.message
            FROM portfolio.submissions s JOIN portfolio.submission_errors e ON e.submission_id = s.id
            WHERE s.id = @id
            """, connection);
        command.Parameters.AddWithValue("id", submissionId);
        await using var reader = await command.ExecuteReaderAsync(_ct);
        Assert.True(await reader.ReadAsync(_ct));
        Assert.Equal(
            ("Rejected", 1, 0, 0, tenantId, true, true, "Portfolio.Csv.NotValidated", "Uploaded before validation existed."),
            (reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetGuid(4),
                reader.GetBoolean(5), reader.GetBoolean(6), reader.GetString(7), reader.GetString(8)));
        Assert.False(await reader.ReadAsync(_ct));
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync(_ct);
            // The name is generated above, never user input.
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync(_ct);
        }

        return new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
    }

    private async Task ExecuteAsync(string connectionString, string sql, Guid id, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(_ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenantId", tenantId);
        await command.ExecuteNonQueryAsync(_ct);
    }

    private static PortfolioDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>();
        options.UseModuleNpgsql(connectionString, PortfolioDbContext.Schema);
        return new PortfolioDbContext(options.Options, NoTenantContext.Instance);
    }
}
