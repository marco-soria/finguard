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
  (bearer token on same-origin `/api/` only, 401 signs out), then `problemDetailsInterceptor` (typed `ApiError`; only
  `title` is shown to the user).
- Authentication sits behind an `AuthService` abstraction (dev token provider now, MSAL for Entra External ID in
  Phase 5). Components never import MSAL directly.
- Format money in one place (a pipe), always USD. Percentages come pre-computed from the API.
- Semantic HTML, labelled inputs, keyboard-accessible controls.
