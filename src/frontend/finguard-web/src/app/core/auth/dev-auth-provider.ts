import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthProvider, AuthSession, AuthUser } from './auth-provider';

export const DEV_SESSION_KEY = 'finguard.dev-session';

/**
 * Development issuer (ADR-0008): `POST /dev/token`. The session lives in sessionStorage (D-79, R-22) and dies with the
 * Api process, because the signing key is per process (R-18).
 */
@Injectable()
export class DevAuthProvider extends AuthProvider {
  private readonly http = inject(HttpClient);

  readonly users: readonly AuthUser[] = [
    { id: 'alice', label: 'Alice' },
    { id: 'bob', label: 'Bob' },
  ];

  async signIn(user: string): Promise<AuthSession> {
    const session = await firstValueFrom(this.http.post<AuthSession>('/dev/token', { user }));
    sessionStorage.setItem(DEV_SESSION_KEY, JSON.stringify(session));
    return session;
  }

  restore(): AuthSession | null {
    const session = parseSession(sessionStorage.getItem(DEV_SESSION_KEY));
    if (session === null || !(Date.parse(session.expiresAt) > Date.now())) {
      sessionStorage.removeItem(DEV_SESSION_KEY);
      return null;
    }
    return session;
  }

  signOut(): void {
    sessionStorage.removeItem(DEV_SESSION_KEY);
  }
}

function parseSession(json: string | null): AuthSession | null {
  if (json === null) {
    return null;
  }
  try {
    const value: unknown = JSON.parse(json);
    if (
      typeof value === 'object' &&
      value !== null &&
      'accessToken' in value &&
      typeof value.accessToken === 'string' &&
      'expiresAt' in value &&
      typeof value.expiresAt === 'string'
    ) {
      return { accessToken: value.accessToken, expiresAt: value.expiresAt };
    }
  } catch {
    // A corrupt entry counts as signed out.
  }
  return null;
}
