namespace FinGuard.BuildingBlocks.Http;

/// <summary>
/// API version prefix by URL segment (D-302). Every module group maps under it; changes stay additive, and a breaking
/// change would add <c>/api/v2</c> (with <c>Asp.Versioning.Http</c>) at that moment.
/// </summary>
public static class ApiRoutes
{
    public const string V1 = "/api/v1";
}
