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
  if (
    typeof body !== 'object' ||
    body === null ||
    !('title' in body) ||
    typeof body.title !== 'string'
  ) {
    return new ApiError(response.status, GENERIC_ERROR_TITLE);
  }
  return new ApiError(
    response.status,
    body.title,
    'detail' in body && typeof body.detail === 'string' ? body.detail : undefined,
    'code' in body && typeof body.code === 'string' ? body.code : undefined,
    'traceId' in body && typeof body.traceId === 'string' ? body.traceId : undefined,
  );
}

/**
 * The text a component shows for a failed call (D-142). 4xx: `"{title}: {detail}"`, or the title alone without a
 * detail; backend messages are fixed sentences without user data (D-115). 5xx and anything else: the generic text,
 * never server text.
 */
export function errorMessage(error: unknown): string {
  if (!(error instanceof ApiError) || error.status < 400 || error.status >= 500) {
    return GENERIC_ERROR_TITLE;
  }
  return error.detail ? `${error.title}: ${error.detail}` : error.title;
}
