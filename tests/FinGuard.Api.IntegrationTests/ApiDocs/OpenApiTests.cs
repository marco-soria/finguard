using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinGuard.Api.DevAuth;
using FinGuard.Tenancy.Development;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace FinGuard.Api.IntegrationTests.ApiDocs;

/// <summary>D-303: the OpenAPI document is generated at runtime and pinned by <c>docs/api/openapi.v1.json</c>.</summary>
public sealed class OpenApiTests(ApiFixture fixture)
{
    public const string DocumentPath = "/openapi/v1.json";
    public const string UpdateVariable = "FINGUARD_UPDATE_OPENAPI_SNAPSHOT";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public async Task OpenApi_Document_MatchesCommittedSnapshot()
    {
        var actual = Normalise(await FetchDocumentAsync());
        var snapshotPath = Path.Combine(RepositoryRoot(), "docs", "api", "openapi.v1.json");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            await File.WriteAllTextAsync(snapshotPath, actual, TestContext.Current.CancellationToken);
        }

        var expected = File.Exists(snapshotPath)
            ? (await File.ReadAllTextAsync(snapshotPath, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n")
            : string.Empty;
        Assert.True(expected == actual,
            "The API contract changed. Run scripts/update-openapi-snapshot.ps1, review docs/api/openapi.v1.json and commit it.");
    }

    [Fact]
    public async Task OpenApi_Document_DeclaresTheBearerSchemeForEveryOperation()
    {
        var document = await FetchDocumentAsync();

        var scheme = document["components"]!["securitySchemes"]!["Bearer"]!;
        Assert.Equal("http", scheme["type"]!.GetValue<string>());
        Assert.Equal("bearer", scheme["scheme"]!.GetValue<string>());
        Assert.Contains(document["security"]!.AsArray(), requirement => requirement!.AsObject().ContainsKey("Bearer"));
    }

    [Fact]
    public async Task OpenApi_V1Operations_HaveSummaryAndProblemResponse()
    {
        var document = await FetchDocumentAsync();

        var operations = document["paths"]!.AsObject()
            .Where(path => path.Key.StartsWith("/api/v1/", StringComparison.Ordinal))
            .SelectMany(path => path.Value!.AsObject().Select(op => (Name: $"{op.Key} {path.Key}", Operation: op.Value!)))
            .ToList();

        Assert.NotEmpty(operations);
        Assert.All(operations, op =>
        {
            Assert.False(string.IsNullOrWhiteSpace(op.Operation["summary"]?.GetValue<string>()), $"{op.Name} has no summary.");
            var responses = op.Operation["responses"]!.AsObject();
            Assert.True(
                responses.Any(r => r.Key.StartsWith('4') && r.Value!["content"]?["application/problem+json"] is not null),
                $"{op.Name} declares no problem response.");
        });
    }

    [Fact]
    public async Task Scalar_Development_ServesTheReferencePage()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_DocsDisabledInDevelopment_Returns404()
    {
        await using var factory = new ApiFixture.ApiFactory(fixture.ConnectionString,
            builder => builder.UseSetting("Api:Docs:Enabled", "false"));
        using var client = factory.CreateClient();
        // A token from this host's own issuer, so the fallback policy passes and routing answers.
        var token = factory.Services.GetRequiredService<DevTokenIssuer>().CreateToken(DevIdentities.Alice, TimeProvider.System.GetUtcNow());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await client.GetAsync(new Uri(DocumentPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonNode> FetchDocumentAsync()
    {
        // No token: the document is anonymous so a browser can open it.
        using var client = fixture.Factory.CreateClient();
        return (await client.GetFromJsonAsync<JsonNode>(new Uri(DocumentPath, UriKind.Relative), TestContext.Current.CancellationToken))!;
    }

    /// <summary>Indented JSON with LF line endings and a final newline, so the committed file diffs cleanly.</summary>
    internal static string Normalise(JsonNode document) => document.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FinGuard.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("FinGuard.slnx not found above the test output directory.");
    }
}
