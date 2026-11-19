import { expect, Page, test } from '@playwright/test';

const header = 'position_id,asset_class,book_value,currency,description';

// Runs against the shared development database: assert only on this run's unique file name.
async function signInAs(page: Page, user: 'Alice' | 'Bob'): Promise<void> {
  await page.goto('/login');
  await page.getByRole('button', { name: new RegExp(`Sign in as ${user}`) }).click();
  await expect(page.getByRole('heading', { name: 'Submission history' })).toBeVisible();
}

async function upload(page: Page, fileName: string, csv: string): Promise<string> {
  // The API rejects cut-off dates later than today in UTC, so today is taken in UTC.
  const today = new Date().toISOString().slice(0, 10);
  await page.getByLabel('Cut-off date').fill(today);
  await page
    .getByLabel('Portfolio file')
    .setInputFiles({ name: fileName, mimeType: 'text/csv', buffer: Buffer.from(csv) });
  await page.getByRole('button', { name: 'Upload' }).click();
  return today;
}

const uniqueName = (kind: string) =>
  `e2e-${kind}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}.csv`;

test('Alice uploads a valid CSV, sees it accepted in her history; Bob does not', async ({
  page,
}) => {
  const fileName = uniqueName('accepted');

  await signInAs(page, 'Alice');
  const today = await upload(page, fileName, `${header}\nP-1,cash,1000.00,USD,E2E\n`);

  await expect(page.getByRole('status')).toContainText(`Accepted ${fileName}: 1 positions.`);
  const row = page.getByRole('row').filter({ hasText: fileName });
  await expect(row).toBeVisible();
  await expect(row.getByRole('cell').first()).toHaveText(today);
  await expect(row.getByRole('cell').nth(3)).toHaveText('Accepted');

  await row.getByRole('link', { name: fileName }).click();
  await expect(page.getByRole('heading', { name: 'Submission', exact: true })).toBeVisible();
  await expect(page.getByRole('definition').filter({ hasText: fileName })).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await signInAs(page, 'Bob');
  // Wait for the history to settle (table or empty state) before asserting absence.
  await expect(page.getByRole('table').or(page.getByText('No submissions yet.'))).toBeVisible();
  await expect(page.getByRole('link', { name: fileName })).toHaveCount(0);
});

test('Alice uploads an invalid CSV and sees it rejected with its errors', async ({ page }) => {
  const fileName = uniqueName('rejected');

  await signInAs(page, 'Alice');
  await upload(page, fileName, `${header}\nP-1,cash,-5,USD,E2E\nP-2,cash,1,EUR,E2E\n`);

  const status = page.getByRole('status');
  await expect(status).toContainText(`Rejected ${fileName}: 2 errors.`);
  const row = page.getByRole('row').filter({ hasText: fileName });
  await expect(row.getByRole('cell').nth(3)).toHaveText('Rejected');
  await expect(row.getByRole('cell').nth(4)).toHaveText('2');

  await status.getByRole('link', { name: 'View details' }).click();
  await expect(page.getByRole('heading', { name: 'Errors' })).toBeVisible();
  const errors = page.getByRole('list', { name: 'Errors' }).getByRole('listitem');
  await expect(errors).toHaveText([
    'Row 2, book_value: The book value must not be negative.',
    'Row 3, currency: The currency must be USD.',
  ]);
});
