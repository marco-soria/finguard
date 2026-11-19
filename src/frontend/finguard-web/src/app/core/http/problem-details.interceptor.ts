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
    'code' in body && typeof body.code === 'string' ? body.code : undefined,
    'traceId' in body && typeof body.traceId === 'string' ? body.traceId : undefined,
  );
}
