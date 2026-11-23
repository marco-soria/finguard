import { test as base, expect, Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

export { expect };

/**
 * Every flow imports `test` from here: an automatic fixture records each request and fails the test when one leaves
 * the app's origin (no third-party hosts: fonts, icons, analytics; CSP review in Phase 5). `data:` and `blob:` URLs
 * are local.
 */
export const test = base.extend<{ sameOriginOnly: void }>({
  sameOriginOnly: [
    async ({ page, baseURL }, use) => {
      const origin = new URL(baseURL!).origin;
      const foreign: string[] = [];
      page.on('request', (request) => {
        const url = request.url();
        if (
          !url.startsWith('data:') &&
          !url.startsWith('blob:') &&
          new URL(url).origin !== origin
        ) {
          foreign.push(url);
        }
      });
      await use();
      expect(foreign, `Requests outside ${origin}`).toEqual([]);
    },
    { auto: true },
  ],
});

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

// A random past cut-off date keeps a flow's "current" rows away from the other flows, which upload for today in
// parallel, and makes a duplicate of a fixed sample file unlikely (D-139).
// From 2000-01-01 (the first limit policy, D-190) to 30 days before today.
export function randomPastDate(): string {
  const day = 86_400_000;
  const first = Date.UTC(2000, 0, 1);
  const days = Math.floor((Date.now() - 30 * day - first) / day);
  return new Date(first + Math.floor(Math.random() * (days + 1)) * day).toISOString().slice(0, 10);
}

/** A sample file from `samples/portfolios/` in the repo (expected outcomes in its README). */
export const sample = (fileName: string) =>
  readFileSync(join(__dirname, '..', '..', '..', '..', 'samples', 'portfolios', fileName), 'utf8');
