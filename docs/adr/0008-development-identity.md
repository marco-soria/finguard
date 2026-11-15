# ADR-0008: Development identity with an in-process token issuer

- Status: Accepted
- Date: 2026-11-15

## Context
Every request must be authenticated with a JWT and mapped to a tenant (ADR-0004) before any business table exists.
Phases 0 to 4 run locally at near-zero cost (ADR-0005), and Microsoft Entra External ID is created only in Phase 5
(D-57). The Angular SPA (Phase 1) and the integration tests both need a way to sign in without Azure, and the
stack accepts stable packages only.

## Decision
- The Api hosts a development token issuer: `POST /dev/token` with `{ "user": "alice" }` returns
  `{ accessToken, expiresAt }`. It is mapped only when the environment is `Development` and allows anonymous callers.
- Tokens are HMAC-SHA256 JWTs signed with a random 256-bit key created at startup and never written anywhere.
  Issuer `finguard-dev`, audience `finguard-api`, lifetime 1 hour, claims `iss`, `aud`, `sub`, `name`.
- The issuer serves only the seeded development identities (`alice` → Acme Pension Fund, `bob` → Globex Insurance),
  seeded by the Tenancy module in Development only (D-64).
- Every other environment binds `Authentication:Schemes:Bearer` (`Authority`, `Audience`) and fails at startup when
  `Authority` is missing (D-60). No identity-provider-specific code: Entra External ID arrives as configuration.
- Both paths resolve the tenant the same way: Tenancy maps (`iss`, `sub`) to a tenant through `tenancy.users`.

## Consequences
- The SPA can sign in locally with one HTTP call, and tests can issue tokens (including expired or unprovisioned
  ones) through the same issuer class.
- Tokens stop working whenever the Api restarts; callers request a new one.
- Risk R-17 (the issuer leaking to production) is mitigated by the Development-only mapping, a test that boots the Api
  in `Production` and gets no token, and the fail-fast `Authority` check.
- No refresh tokens, roles or real sign-up until Phase 5.

## Alternatives considered
- `dotnet user-jwts`: rejected; it is a CLI that writes tokens to user-secrets, so the SPA has no flow to obtain one.
- Keycloak container through Aspire: rejected; `Aspire.Hosting.Keycloak` is preview only and the stack is stable-only.
- Microsoft Entra External ID from day one: rejected; a trial tenant lasts 30 days and a linked subscription adds cost
  and Azure setup long before Phase 5 (D-57).
