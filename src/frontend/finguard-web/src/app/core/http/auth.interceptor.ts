import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ApiError } from './api-error';

/**
 * Adds the bearer token to same-origin `/api/` calls only; absolute URLs never get it. A 401 from `/api/` signs out,
 * because development tokens die when the Api restarts (R-18). Registered before `problemDetailsInterceptor`, so it
 * sees the mapped `ApiError` on the way back.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/')) {
    return next(req);
  }

  const auth = inject(AuthService);
  const token = auth.accessToken();
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        auth.signOut();
      }
      return throwError(() => error);
    }),
  );
};
