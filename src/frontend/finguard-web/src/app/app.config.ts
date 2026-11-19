import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { AuthProvider } from './core/auth/auth-provider';
import { DevAuthProvider } from './core/auth/dev-auth-provider';
import { authInterceptor } from './core/http/auth.interceptor';
import { problemDetailsInterceptor } from './core/http/problem-details.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Order matters: authInterceptor runs first on the request (bearer header) and last on the response, so it sees
    // the ApiError that problemDetailsInterceptor maps from the error response.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, problemDetailsInterceptor])),
    // Development issuer only; Phase 5 swaps in the MSAL provider for Entra External ID (D-78).
    { provide: AuthProvider, useClass: DevAuthProvider },
  ],
};
