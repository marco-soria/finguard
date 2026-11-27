import { expect, test } from './support';

// D-289: the footer is on every page, including /login. The links are checked, never followed (same origin only).
test('the footer shows the credits and opens its links in a new tab', async ({ page }) => {
  await page.goto('/login');

  const footer = page.getByRole('contentinfo');
  await expect(footer).toContainText('Designed and developed by Marco Soria');
  await expect(footer).toContainText('Demo application. Not investment advice.');
  for (const [name, href] of [
    ['MIT License', 'https://github.com/marco-soria/finguard/blob/main/LICENSE'],
    ['Source on GitHub', 'https://github.com/marco-soria/finguard'],
  ]) {
    const link = footer.getByRole('link', { name: new RegExp(`^${name}`) });
    await expect(link).toHaveAttribute('href', href);
    await expect(link).toHaveAttribute('target', '_blank');
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  }
});
