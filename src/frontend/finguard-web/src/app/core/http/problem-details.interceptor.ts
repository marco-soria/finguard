import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError } from './api-error';

export const GENERIC_ERROR_TITLE = 'Something went wrong. Please try again.';

/** Turns HTTP error responses into `ApiError` from RFC 9457 ProblemDetails; anything else gets a generic title. */
export const problemDetailsInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: unknown) =>
      throwError(() => (error instanceof HttpErrorResponse ? toApiError(error) : error)),
    ),
  );

function toApiError(response: HttpErrorResponse): ApiError {
  const body: unknown = response.error;
  const retryAfter = retryAfterSeconds(response);
  if (
    typeof body !== 'object' ||
    body === null ||
    !('title' in body) ||
    typeof body.title !== 'string'
  ) {
    return new ApiError(
      response.status,
      GENERIC_ERROR_TITLE,
      undefined,
      undefined,
      undefined,
      retryAfter,
    );
  }
  return new ApiError(
    response.status,
    body.title,
    'detail' in body && typeof body.detail === 'string' ? body.detail : undefined,
    'code' in body && typeof body.code === 'string' ? body.code : undefined,
    'traceId' in body && typeof body.traceId === 'string' ? body.traceId : undefined,
    retryAfter,
  );
}

/** `Retry-After` in delay-seconds; the HTTP-date form, which the API never sends, counts as absent. */
function retryAfterSeconds(response: HttpErrorResponse): number | undefined {
  const value = response.headers?.get('Retry-After')?.trim();
  return value && /^\d+$/.test(value) ? Number(value) : undefined;
}

/**
 * The text a component shows for a failed call (D-142). 429: the retry delay (D-318). Other 4xx: `"{title}: {detail}"`,
 * or the title alone without a detail; backend messages are fixed sentences without user data (D-115). 5xx and
 * anything else: the generic text, never server text.
 */
export function errorMessage(error: unknown): string {
  // Rate limited (D-318): a fixed sentence with the delay, whatever the body says. No automatic retry.
  if (error instanceof ApiError && error.status === 429) {
    const seconds = error.retryAfterSeconds;
    return seconds === undefined
      ? 'Too many requests. Try again in a moment.'
      : `Too many requests. Try again in ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.`;
  }
  if (!(error instanceof ApiError) || error.status < 400 || error.status >= 500) {
    return GENERIC_ERROR_TITLE;
  }
  return error.detail ? `${error.title}: ${error.detail}` : error.title;
}
