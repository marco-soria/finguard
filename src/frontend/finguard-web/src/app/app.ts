import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatToolbar } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';
import { catchError, map, Observable, of, startWith, switchMap } from 'rxjs';
import { TenancyApi } from './core/api/tenancy-api';
import { AuthService } from './core/auth/auth.service';
import { Theme, ThemeService } from './core/theme/theme.service';

type TenantState = { status: 'idle' | 'loading' | 'error' } | { status: 'loaded'; name: string };

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatButton, MatButtonToggle, MatButtonToggleGroup, MatToolbar],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly tenancy = inject(TenancyApi);
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);

  /** Reloads the tenant on every sign-in; a 401 signs out through the interceptor and resets it. */
  protected readonly tenant = toSignal(
    toObservable(this.auth.isAuthenticated).pipe(
      switchMap((signedIn) => (signedIn ? this.loadTenant() : of<TenantState>({ status: 'idle' }))),
    ),
    { initialValue: { status: 'idle' } as TenantState },
  );

  protected setTheme(theme: Theme): void {
    this.theme.set(theme);
  }

  protected signOut(): void {
    this.auth.signOut();
  }

  private loadTenant(): Observable<TenantState> {
    return this.tenancy.getCurrentTenant().pipe(
      map((t): TenantState => ({ status: 'loaded', name: t.tenantName })),
      catchError(() => of<TenantState>({ status: 'error' })),
      startWith<TenantState>({ status: 'loading' }),
    );
  }
}
