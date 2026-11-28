import { join } from 'node:path';
import { expect, randomPastDate, sample, signInAs, test, upload } from './support';

// README screenshots from sample data only (D-211). Excluded from the default run; see playwright.config.ts.
const images = join(__dirname, '..', '..', '..', '..', 'docs', 'images');

test.use({ viewport: { width: 1280, height: 800 } });

test('README screenshots of a breached submission', { tag: '@screenshots' }, async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await signInAs(page, 'Alice');
  await upload(page, 'valid-shares-heavy.csv', sample('valid-shares-heavy.csv'), randomPastDate());
  await page.getByRole('status').getByRole('link', { name: 'View details' }).click();
  await expect(page.getByRole('region', { name: 'Compliance' })).toHaveAttribute(
    'data-state',
    'ready',
    { timeout: 20_000 },
  );

  // A light operating system and no stored choice: light first, then the toggle switches to dark.
  for (const theme of ['light', 'dark'] as const) {
    if (theme === 'dark') {
      await page.getByRole('button', { name: 'Switch to dark theme' }).click();
    }
    const next = theme === 'dark' ? 'light' : 'dark';
    await expect(page.getByRole('button', { name: `Switch to ${next} theme` })).toBeVisible();
    // Move away so neither the ripple nor the tooltip is captured.
    await page.mouse.move(0, 0);
    await expect(page.getByRole('tooltip')).toHaveCount(0);
    await page.screenshot({
      path: join(images, `evaluation-${theme}.png`),
      fullPage: true,
      animations: 'disabled',
    });
  }
});
