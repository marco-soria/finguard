# ADR-0016: Request protection: rate limiting and forwarded headers

- Status: Accepted
- Date: 2026-11-27

## Context
Phase 5 gives the API a public URL. Before that, a single caller must not be able to flood it, and the most expensive
path (an upload: CSV parsing, a compliance evaluation, an advisory and, with a model configured, an AI call) needs its
own budget. The cost ceiling of ADR-0005 rules out a gateway product (API Management costs money; the Cloudflare
proxy is off by D-273). Behind Container Apps ingress (Phase 5) or Traefik (Phase 6) every request reaches the API from
the proxy, so a per-IP limit is only meaningful if the API reads the client IP the proxy forwards.

## Decision
1. **Built-in ASP.NET Core rate limiting (D-313).** `UseRateLimiter()` runs after `UseTenancy()`, so the tenant is
   known, and before `UseAuthorization()`, so unauthenticated floods are counted before the fallback policy answers
   401.
2. **Partitions.** A global limiter charges every request to one counter:
   - a request with a tenant: that tenant, fixed window `ReadsPerMinute`;
   - any other request: its client IP, fixed window `AnonymousPerMinute`.

   The named policy `uploads` (`RateLimitPolicies.Uploads`) adds a token bucket per tenant on
   `POST /api/v1/portfolio/submissions` (`UploadsPerMinute` tokens, refilled by the same number every minute). It is
   chained: an upload needs a permit from both. `/health` and `/alive` call `DisableRateLimiting()` and are never
   limited. No request queues (`QueueLimit = 0`).
3. **Configuration (D-314).** The `RateLimiting` section, validated on start (every value positive); no defaults in
   code, so a missing section stops the start instead of running without limits.

   | Setting | `appsettings.json` | `appsettings.Development.json` |
   | --- | --- | --- |
   | `ReadsPerMinute` (per tenant) | 120 | 1200 |
   | `AnonymousPerMinute` (per IP) | 20 | 300 |
   | `UploadsPerMinute` (per tenant, token bucket) | 10 | 120 |

   Development keeps the limiter on, with values that two back-to-back E2E runs, `/scalar` and manual testing stay
   under. The integration tests boot a host of their own with small limits.
4. **Rejection (D-315).** 429 ProblemDetails with `type` RFC 6585 section 4, `title` "Too many requests", `code`
   `RateLimit.Exceeded` and `traceId`, written through `IProblemDetailsService` like every other error. `Retry-After`
   carries whole seconds (rounded up, at least 1) when the limiter knows the delay. The rejection is logged at
   Information with the policy name (`global`, `uploads`) and the partition kind (`tenant`, `ip`) only: never the
   tenant id, the IP or the path. The SPA shows "Too many requests. Try again in N seconds." and never retries on its
   own (D-318).
5. **Forwarded headers (D-316).** `ForwardedHeaders:Enabled` (true in `appsettings.json`, false in Development, where
   no proxy exists) runs `UseForwardedHeaders()` first in the pipeline for `X-Forwarded-For` and `X-Forwarded-Proto`,
   with `ForwardLimit = 1`: only the entry the proxy appended counts. `KnownIPNetworks` and `KnownProxies` are cleared,
   so any peer is trusted to set these headers.

## Trust assumption
Clearing the known networks and proxies is safe **only because the API is reachable solely through the platform
proxy** (Container Apps ingress in Phase 5, Traefik in Phase 6), whose addresses are not fixed. Microsoft warns that
clearing both lists is unsafe otherwise: a client that can reach the API directly could set `X-Forwarded-For` to any
value, get a fresh IP partition per request and bypass the anonymous limit. Tenant partitions do not depend on the IP.
If the API ever gets a directly reachable address, list the proxy's network in `KnownIPNetworks` instead.

## Consequences
- **Per replica (R-54).** Counters live in each replica's memory. With N replicas the effective limit is N times the
  configured one. Accepted while Phase 5 runs one replica; revisit (a distributed store) when scaling out.
- Every endpoint can answer 429. The upload and list operations declare it in the OpenAPI document; new endpoints that
  do expensive work declare a named policy (backend rules).
- One shared IP partition for callers without an address (the in-process test server); irrelevant behind a proxy.

### Phase 5 and Phase 6 checks
- Container Apps: confirm the API has internal or ingress-only exposure, that ingress appends `X-Forwarded-For` and
  sends `X-Forwarded-Proto`, and that the logged scheme is `https`. Repeat for Traefik in Phase 6.
- CORS: today the SPA reaches the API through the dev proxy (same origin), so it can read `Retry-After`. If the SPA is
  served from another origin, the API must add `WithExposedHeaders("Retry-After")` to its CORS policy, or the SPA falls
  back to "Try again in a moment."
- Revisit the defaults with real traffic; keep `RateLimiting` values in configuration, not code.

## Alternatives rejected
- Azure API Management or a Cloudflare proxy in front of the API: cost and a second place to configure limits.
- One limit for every endpoint: uploads cost far more than reads.
- Disabling the limiter in Development: it would never be exercised before production.
- Redis-backed counters: a new service and cost for a demo with one replica.
- Trusting forwarded headers in Development: there is no proxy there.
