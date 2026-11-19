import { AuthProvider, AuthSession, AuthUser } from './auth-provider';

/** Test double for specs: an in-memory provider. Not registered by the app. */
export class FakeAuthProvider extends AuthProvider {
  readonly users: readonly AuthUser[] = [
    { id: 'alice', label: 'Alice' },
    { id: 'bob', label: 'Bob' },
  ];

  stored: AuthSession | null = null;
  failWith: unknown = null;
  signOutCalls = 0;

  async signIn(user: string): Promise<AuthSession> {
    if (this.failWith !== null) {
      throw this.failWith;
    }
    this.stored = { accessToken: `token-${user}`, expiresAt: '2999-01-01T00:00:00Z' };
    return this.stored;
  }

  restore(): AuthSession | null {
    return this.stored;
  }

  signOut(): void {
    this.signOutCalls++;
    this.stored = null;
  }
}
