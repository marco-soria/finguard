import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthProvider, AuthSession, AuthUser } from './auth-provider';

/** The only authentication API for components, guards and interceptors (D-78). */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly provider = inject(AuthProvider);
  private readonly router = inject(Router);
  private readonly current = signal<AuthSession | null>(this.provider.restore());

  readonly session = this.current.asReadonly();
  readonly isAuthenticated = computed(() => this.current() !== null);
  readonly users: readonly AuthUser[] = this.provider.users;

  async signIn(user: string): Promise<void> {
    this.current.set(await this.provider.signIn(user));
  }

  signOut(): void {
    this.provider.signOut();
    this.current.set(null);
    void this.router.navigateByUrl('/login');
  }

  accessToken(): string | null {
    return this.current()?.accessToken ?? null;
  }
}
