import { TestBed } from '@angular/core/testing';
import Home from './home';

describe('Home', () => {
  it('renders the page heading', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    const heading = (fixture.nativeElement as HTMLElement).querySelector('h2');
    expect(heading?.textContent).toContain('Portfolio compliance');
  });
});
