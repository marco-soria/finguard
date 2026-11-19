using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Portfolio.Persistence;
using FinGuard.Tenancy.Development;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Portfolio;

/// <summary>Upload, seeding and tenant provisioning helpers shared by the Portfolio read-slice tests.</summary>
internal static class SubmissionTestData
{
    public const string Path = "/api/portfolio/submissions";
    public const string CutOffDate = "2026-01-01";

    public static byte[] Csv() => Encoding.UTF8.GetBytes(
        "position_id,asset_class,book_value,currency,description\nP-1,cash,1000.00,USD,Treasury bills\n");

    /// <summary>Two data rows, three errors: a negative amount on row 2; an unknown class and a non-USD currency on row 3.</summary>
    public static byte[] InvalidCsv() => Encoding.UTF8.GetBytes(
        "position_id,asset_class,book_value,currency,description\nP-1,cash,-1,USD,a\nP-2,crypto,5,EUR,b\n");

    public static readonly (int? Row, string? Column, string Code)[] InvalidCsvErrors =
    [
        (2, "book_value", "Portfolio.Csv.BookValueNegative"),
        (3, "asset_class", "Portfolio.Csv.AssetClassUnknown"),
        (3, "currency", "Portfolio.Csv.CurrencyNotSupported"),
    ];

    public static async Task<HttpResponseMessage> UploadAsync(
        this HttpClient client, string fileName, CancellationToken ct, byte[]? content = null)
    {
        var file = new ByteArrayContent(content ?? Csv());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var form = new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(CutOffDate), "cutOffDate" } };
        return await client.PostAsync(new Uri(Path, UriKind.Relative), form, ct);
    }

    public static async Task<SubmissionReceivedResponse> UploadOkAsync(
        this HttpClient client, string fileName, CancellationToken ct, byte[]? content = null)
    {
        using var response = await client.UploadAsync(fileName, ct, content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(ct))!;
    }

    /// <summary>
    /// A tenant and user that no other test writes to, so list tests see only their own rows in the shared database.
    /// The token still comes from the development issuer, like the seeded identities.
    /// </summary>
    public static async Task<DevIdentity> ProvisionTenantAsync(this ApiFixture fixture, CancellationToken ct)
    {
        var identity = new DevIdentity(
            $"user-{Guid.NewGuid():N}", Guid.NewGuid().ToString(), "Test user", Guid.NewGuid(), "Test tenant");
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO tenancy.tenants (id, name, created_at) VALUES (@tenantId, @name, now());
            INSERT INTO tenancy.users (id, tenant_id, issuer, subject, created_at)
            VALUES (@userId, @tenantId, @issuer, @subject, now());
            """, connection);
        command.Parameters.AddWithValue("tenantId", identity.TenantId);
        command.Parameters.AddWithValue("name", identity.TenantName);
        command.Parameters.AddWithValue("userId", Guid.NewGuid());
        command.Parameters.AddWithValue("issuer", DevIdentities.Issuer);
        command.Parameters.AddWithValue("subject", identity.Subject);
        await command.ExecuteNonQueryAsync(ct);
        return identity;
    }

    /// <summary>One scalar as the database owner (no query filter), so a test sees exactly what was written.</summary>
    public static async Task<T> ScalarAsync<T>(
        this ApiFixture fixture, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>A Portfolio context for the tenant, with the real stamping interceptor and query filter.</summary>
    public static PortfolioDbContext CreatePortfolioContext(this ApiFixture fixture, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PortfolioDbContext>();
        options.UseModuleNpgsql(fixture.ConnectionString, PortfolioDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        return new PortfolioDbContext(options.Options, tenantContext);
    }
}
