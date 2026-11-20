import { expect, Page } from '@playwright/test';

export const header = 'position_id,asset_class,book_value,currency,description';

// Runs against the shared development database: every file name and every content is unique per run, so no upload
// is an accidental duplicate (D-139) and assertions only touch this run's rows.
const suffix = () => `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

export const uniqueName = (kind: string) => `e2e-${kind}-${suffix()}.csv`;

export const uniquePositionId = () => `P-${suffix()}`;

export async function signInAs(page: Page, user: 'Alice' | 'Bob'): Promise<void> {
  await page.goto('/login');
  await page.getByRole('button', { name: new RegExp(`Sign in as ${user}`) }).click();
  await expect(page.getByRole('heading', { name: 'Submission history' })).toBeVisible();
}

// The API rejects cut-off dates later than today in UTC, so today is taken in UTC.
export const todayUtc = () => new Date().toISOString().slice(0, 10);

export async function upload(
  page: Page,
  fileName: string,
  csv: string,
  cutOffDate = todayUtc(),
): Promise<string> {
  await page.getByLabel('Cut-off date').fill(cutOffDate);
  await page
    .getByLabel('Portfolio file')
    .setInputFiles({ name: fileName, mimeType: 'text/csv', buffer: Buffer.from(csv) });
  await page.getByRole('button', { name: 'Upload' }).click();
  return cutOffDate;
}
