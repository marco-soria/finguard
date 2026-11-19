/** A signed-in session. The token is never logged, displayed or put in a URL. */
export interface AuthSession {
  accessToken: string;
  expiresAt: string;
}

/** A user the sign-in page offers. */
export interface AuthUser {
  id: string;
  label: string;
}

/**
 * Where sessions come from (D-78). Only `AuthService` depends on it; components and interceptors never do.
 * Development uses `DevAuthProvider`; Phase 5 adds an MSAL provider for Entra External ID.
 */
export abstract class AuthProvider {
  abstract readonly users: readonly AuthUser[];

  abstract signIn(user: string): Promise<AuthSession>;

  /** The stored session, or null when there is none or it has expired. */
  abstract restore(): AuthSession | null;

  abstract signOut(): void;
}
