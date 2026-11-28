using System.Net;
using FinGuard.Api.RateLimiting;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinGuard.Api.IntegrationTests.RateLimiting;

// No database: the rate-limiting types live in the Api, which only this test project references.
public sealed class RateLimitingUnitTests
{
    [Fact]
    public void Options_AllPositive_AreValid()
    {
        var result = Validate(new RateLimitingOptions { ReadsPerMinute = 120, AnonymousPerMinute = 20, UploadsPerMinute = 10 });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0, 20, 10, nameof(RateLimitingOptions.ReadsPerMinute))]
    [InlineData(120, -1, 10, nameof(RateLimitingOptions.AnonymousPerMinute))]
    [InlineData(120, 20, 0, nameof(RateLimitingOptions.UploadsPerMinute))]
    public void Options_NotPositive_AreInvalid(int reads, int anonymous, int uploads, string property)
    {
        var result = Validate(new RateLimitingOptions { ReadsPerMinute = reads, AnonymousPerMinute = anonymous, UploadsPerMinute = uploads });

        Assert.True(result.Failed);
        Assert.Contains(property, result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Partition_WithTenant_IsTheTenant()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);

        var partition = RequestPartition.For(Context(tenant, IPAddress.Parse("198.51.100.7")));

        Assert.Equal(new RequestPartition("tenant", $"tenant:{tenantId}"), partition);
    }

    [Fact]
    public void Partition_WithoutTenant_IsTheClientIp()
    {
        var partition = RequestPartition.For(Context(new TenantContext(), IPAddress.Parse("198.51.100.7")));

        Assert.Equal(new RequestPartition("ip", "ip:198.51.100.7"), partition);
    }

    [Fact]
    public void Partition_WithoutTenantOrAddress_IsOneSharedPartition()
    {
        var partition = RequestPartition.For(Context(new TenantContext(), address: null));

        Assert.Equal(new RequestPartition("ip", "ip:unknown"), partition);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(300, 1)]
    [InlineData(1000, 1)]
    [InlineData(1001, 2)]
    [InlineData(59_200, 60)]
    public void RetryAfterSeconds_RoundsUpToWholeSeconds(int milliseconds, int expected) =>
        Assert.Equal(expected, RateLimitingSetup.RetryAfterSeconds(TimeSpan.FromMilliseconds(milliseconds)));

    private static ValidateOptionsResult Validate(RateLimitingOptions options) =>
        new DataAnnotationValidateOptions<RateLimitingOptions>(null).Validate(null, options);

    private static DefaultHttpContext Context(TenantContext tenant, IPAddress? address)
    {
        var services = new ServiceCollection().AddSingleton<ITenantContext>(tenant).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Connection.RemoteIpAddress = address;
        return context;
    }
}
