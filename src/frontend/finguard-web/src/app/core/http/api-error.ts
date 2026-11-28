/**
 * An HTTP failure in a shape the UI can show. Components show it only through `errorMessage` (D-142): `title` and
 * `detail` for 4xx, a generic text for 5xx. `code` is for tests and logic, never shown.
 * Extends `Error` so Angular resources (`httpResource`) expose it as-is from `error()` instead of wrapping it.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly detail?: string,
    readonly code?: string,
    readonly traceId?: string,
    /** From a 429's `Retry-After` header, in whole seconds (D-318). */
    readonly retryAfterSeconds?: number,
  ) {
    super(title);
    this.name = 'ApiError';
  }
}
