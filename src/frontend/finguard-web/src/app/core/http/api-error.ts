/**
 * An HTTP failure in a shape the UI can show. Only `title` reaches the user (D-80).
 * Extends `Error` so Angular resources (`httpResource`) expose it as-is from `error()` instead of wrapping it.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly code?: string,
    readonly traceId?: string,
  ) {
    super(title);
    this.name = 'ApiError';
  }
}
