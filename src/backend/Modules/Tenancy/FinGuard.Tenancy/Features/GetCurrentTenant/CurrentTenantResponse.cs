namespace FinGuard.Tenancy.Features.GetCurrentTenant;

public sealed record CurrentTenantResponse(Guid TenantId, string TenantName);
