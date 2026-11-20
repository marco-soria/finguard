import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthProvider } from '../../core/auth/auth-provider';
import { AuthService } from '../../core/auth/auth.service';
import { FakeAuthProvider } from '../../core/auth/fake-auth-provider';
import { ApiError } from '../../core/http/api-error';
import Login from './login';

describe('Login', () => {
  let provider: FakeAuthProvider;

  beforeEach(() => {
    provider = new FakeAuthProvider();
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), { provide: AuthProvider, useValue: provider }],
    });
  });

  function render() {
    const fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
    return fixture;
  }

  function button(element: HTMLElement, label: string): HTMLButtonElement {
    const match = Array.from(element.querySelectorAll('button')).find((b) =>
      b.textContent?.includes(label),
    );
    if (!match) {
      throw new Error(`No button for ${label}`);
    }
    return match;
  }

  it('offers one button per development user', () => {
    const element = render().nativeElement as HTMLElement;

    const labels = Array.from(element.querySelectorAll('button')).map((b) => b.textContent?.trim());
    expect(labels).toEqual(['Sign in as Alice', 'Sign in as Bob']);
  });

  it('signs in with the chosen user and navigates home', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = render();

    button(fixture.nativeElement, 'Bob').click();
    await fixture.whenStable();

    expect(TestBed.inject(AuthService).accessToken()).toBe('token-bob');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('shows the error title when sign-in fails', async () => {
    provider.failWith = new ApiError(400, 'Validation failed', undefined, 'DevAuth.User.Unknown');
    const fixture = render();

    button(fixture.nativeElement, 'Alice').click();
    await fixture.whenStable();
    fixture.detectChanges();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Validation failed');
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
  });
});
