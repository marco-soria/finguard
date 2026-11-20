---
paths:
  - "src/frontend/**"
---

# Frontend rules (Angular)

- Package manager is **pnpm** only. Never run `npm`, `yarn` or `npx`; use `pnpm`, `pnpm exec`, `pnpm dlx`.
  Respect the `packageManager` field in `package.json`, commit `pnpm-lock.yaml`, and use
  `pnpm install --frozen-lockfile` in CI and Dockerfiles.
- Follow current Angular practices: standalone components, signals for local state, `OnPush`, typed reactive forms,
  built-in control flow, lazy-loaded routes.
- UI text and error messages are in English.
- No business rules in the frontend. Status, utilization and rebalance numbers come from the API as-is.
- All HTTP goes through typed API services in `core/api/`. Two interceptors, in this order (D-80): `authInterceptor`
  (bearer token on same-origin `/api/` only, 401 signs out), then `problemDetailsInterceptor` (typed `ApiError`).
- Show `title` and `detail` for 4xx through `errorMessage`; never show server text for 5xx (D-142). Error codes are
  never visible text; use them only as data (for example `data-code`).
- Authentication sits behind an `AuthService` abstraction (dev token provider now, MSAL for Entra External ID in
  Phase 5). Components never import MSAL directly.
- Format money in one place (a pipe), always USD. Percentages come pre-computed from the API.
- Semantic HTML, labelled inputs, keyboard-accessible controls.
- UI library: Angular Material + CDK (D-98). Use its components before writing custom ones; buttons use the
  `matButton` attribute API. No third-party font or icon CDNs (system font stack; CSP review in Phase 5).
- Component CSS uses `--mat-sys-*` tokens only, never literal colors, so light and dark mode both work (the app follows
  the OS through `color-scheme: light dark`, D-106).
- Reads use `httpResource` created in the component's injection context (API services expose factories); writes use
  `HttpClient` (D-102). `ApiError` extends `Error`, so a resource's `error()` is the `ApiError` itself.
- Calendar dates (`cutOffDate`) stay `yyyy-MM-dd` strings: never `Date` or `DatePipe` (one-day shift west of UTC).
  Timestamps (`receivedAt`) render in local time with `DatePipe` (D-103).
- Client-side file checks (extension, empty, size) are UX only; the API is authoritative (D-104).
