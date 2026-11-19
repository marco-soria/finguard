import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ApiError } from '../../core/http/api-error';
import { GENERIC_ERROR_TITLE } from '../../core/http/problem-details.interceptor';

@Component({
  selector: 'app-login',
  imports: [MatButton],
  templateUrl: './login.html',
  styleUrl: './login.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly users = this.auth.users;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async signIn(user: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.signIn(user);
      await this.router.navigateByUrl('/');
    } catch (error: unknown) {
      this.error.set(error instanceof ApiError ? error.title : GENERIC_ERROR_TITLE);
    } finally {
      this.busy.set(false);
    }
  }
}
