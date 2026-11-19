import { expect, Page, test } from '@playwright/test';

// Runs against the shared development database: assert only on this run's unique file name.
async function signInAs(page: Page, user: 'Alice' | 'Bob'): Promise<void> {
  await page.goto('/login');
  await page.getByRole('button', { name: new RegExp(`Sign in as ${user}`) }).click();
  await expect(page.getByRole('heading', { name: 'Submission history' })).toBeVisible();
}

test('Alice uploads a CSV and sees it in her history; Bob does not', async ({ page }) => {
  const fileName = `e2e-${Date.now()}.csv`;
  // The API rejects cut-off dates later than today in UTC, so today is taken in UTC.
  const today = new Date().toISOString().slice(0, 10);
  const csv =
    'position_id,asset_class,market_value_usd,currency,description\nP-1,cash,1000.00,USD,E2E\n';

  await signInAs(page, 'Alice');
  await page.getByLabel('Cut-off date').fill(today);
  await page
    .getByLabel('Portfolio file')
    .setInputFiles({ name: fileName, mimeType: 'text/csv', buffer: Buffer.from(csv) });
  await page.getByRole('button', { name: 'Upload' }).click();

  await expect(page.getByRole('status')).toHaveText(`Uploaded ${fileName}.`);
  const row = page.getByRole('row').filter({ hasText: fileName });
  await expect(row).toBeVisible();
  await expect(row.getByRole('cell').first()).toHaveText(today);

  await row.getByRole('link', { name: fileName }).click();
  await expect(page.getByRole('heading', { name: 'Submission', exact: true })).toBeVisible();
  await expect(page.getByRole('definition').filter({ hasText: fileName })).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await signInAs(page, 'Bob');
  // Wait for the history to settle (table or empty state) before asserting absence.
  await expect(page.getByRole('table').or(page.getByText('No submissions yet.'))).toBeVisible();
  await expect(page.getByRole('link', { name: fileName })).toHaveCount(0);
});
