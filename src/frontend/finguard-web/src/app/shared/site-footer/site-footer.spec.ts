import { TestBed } from '@angular/core/testing';
import { copyrightYears } from './copyright-years';
import { LICENSE_URL, REPOSITORY_URL, SiteFooter } from './site-footer';

describe('copyrightYears', () => {
  it('shows the first year alone in 2026', () => {
    expect(copyrightYears(2026)).toBe('2026');
  });

  it('shows a range afterwards', () => {
    expect(copyrightYears(2027)).toBe('2026–2027');
  });
});

describe('SiteFooter', () => {
  const render = () => {
    const fixture = TestBed.createComponent(SiteFooter);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('shows the credits and the demo notice', () => {
    const lines = Array.from(render().querySelectorAll('footer p')).map((p) =>
      p.textContent?.replace(/\s+/g, ' ').trim(),
    );

    expect(lines[0]).toMatch(
      /^© 2026(–\d{4})? Marco Antonio Soria · Designed and developed by Marco Antonio Soria · MIT License \(opens in a new tab\) · Source on GitHub \(opens in a new tab\)$/,
    );
    expect(lines[1]).toBe('Demo application. Not investment advice.');
    expect(lines.join(' ')).not.toContain('All rights reserved');
  });

  it('opens the license and the source in a new tab without an opener', () => {
    const links = Array.from(render().querySelectorAll('a'));

    expect(links.map((a) => a.getAttribute('href'))).toEqual([LICENSE_URL, REPOSITORY_URL]);
    for (const link of links) {
      expect(link.getAttribute('target')).toBe('_blank');
      expect(link.getAttribute('rel')).toBe('noopener noreferrer');
    }
  });
});
