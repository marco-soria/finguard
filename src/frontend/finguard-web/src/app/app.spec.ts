import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    });
  });

  it('renders a header with the FinGuard title', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const header = (fixture.nativeElement as HTMLElement).querySelector('header');
    expect(header?.textContent).toContain('FinGuard');
  });

  it('renders a router outlet inside main', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const outlet = (fixture.nativeElement as HTMLElement).querySelector('main router-outlet');
    expect(outlet).not.toBeNull();
  });
});
