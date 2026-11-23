import { join } from 'node:path';
import { expect, randomPastDate, sample, signInAs, test, upload } from './support';

// README screenshots from sample data only (D-211). Excluded from the default run; see playwright.config.ts.
const images = join(__dirname, '..', '..', '..', '..', 'docs', 'images');

test.use({ viewport: { width: 1280, height: 800 } });

test('README screenshots of a breached submission', { tag: '@screenshots' }, async ({ page }) => {
  await signInAs(page, 'Alice');
  await upload(page, 'valid-shares-heavy.csv', sample('valid-shares-heavy.csv'), randomPastDate());
  await page.getByRole('status').getByRole('link', { name: 'View details' }).click();
  await expect(page.getByRole('region', { name: 'Compliance' })).toHaveAttribute(
    'data-state',
    'ready',
    { timeout: 20_000 },
  );

  for (const theme of ['Light', 'Dark'] as const) {
    await page
      .getByRole('radiogroup', { name: 'Theme' })
      .getByRole('radio', { name: theme })
      .click();
    await expect(page.getByRole('radio', { name: theme })).toHaveAttribute('aria-checked', 'true');
    // Let the toggle's ripple fade before the capture.
    await page.mouse.move(0, 0);
    await page.screenshot({
      path: join(images, `evaluation-${theme.toLowerCase()}.png`),
      fullPage: true,
      animations: 'disabled',
    });
  }
});
