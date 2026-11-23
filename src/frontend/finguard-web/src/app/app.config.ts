import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { AuthProvider } from './core/auth/auth-provider';
import { DevAuthProvider } from './core/auth/dev-auth-provider';
import { authInterceptor } from './core/http/auth.interceptor';
import { problemDetailsInterceptor } from './core/http/problem-details.interceptor';
import { ThemeService } from './core/theme/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // The stored theme applies before the first render; no inline script in index.html (D-210, CSP in Phase 5).
    provideAppInitializer(() => inject(ThemeService).apply()),
    // Route parameters bind to component inputs (submission-detail's `id`).
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: authInterceptor runs first on the request (bearer header) and last on the response, so it sees
    // the ApiError that problemDetailsInterceptor maps from the error response.
    provideHttpClient(withInterceptors([authInterceptor, problemDetailsInterceptor])),
    // Development issuer only; Phase 5 swaps in the MSAL provider for Entra External ID (D-78).
    { provide: AuthProvider, useClass: DevAuthProvider },
  ],
};
