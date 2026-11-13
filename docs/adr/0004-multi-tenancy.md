# ADR-0004: Multi-tenancy with `tenant_id` and a Tenancy module

- Status: Accepted
- Date: 2026-11-13

## Context
Each entity is a tenant and has exactly one user. Portfolios of different entities must never mix. Limits are global,
so only portfolio-derived data is tenant-owned.

## Decision
- Shared database and schemas. Every tenant-owned table has a `tenant_id` column.
- EF Core global query filters read the current tenant from `ITenantContext`; a SaveChanges interceptor stamps
  `tenant_id` on insert. Tenant ids never come from request bodies, routes or CSV files.
- A `Tenancy` module owns `Tenant` and the mapping from an external identity (`sub` or `oid` claim) to a tenant.
  `ITenantContext` resolves the tenant through that mapping, so local development tokens and Entra External ID tokens
  behave identically.
- User and Tenant are separate concepts even though the ratio is 1:1; that is a constraint, not the model.
- Integration events carry `TenantId`; handlers set the tenant context from the event before touching data.
- Mandatory test per slice: tenant A cannot read or modify tenant B.

## Consequences
- Simple operations and cheap local development.
- A forgotten filter would leak data, so tenant isolation is covered by tests and an architecture rule
  (tenant-owned entities must implement `ITenantOwned`).
- Wolverine documents a "conjoined multi-tenancy" mode for EF Core; evaluate it in the Phase 1 spike but keep the
  explicit filter unless it clearly removes code.

## Alternatives considered
- Schema or database per tenant: rejected, operational weight for a single-user-per-tenant portfolio project.
- Tenant claim inside the token only: rejected, ties domain to the identity provider configuration.
