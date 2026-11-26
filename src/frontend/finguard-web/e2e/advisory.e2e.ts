import { Page } from '@playwright/test';
import { expect, randomPastDate, sample, signInAs, test, uniqueName, upload } from './support';

// Evaluation and advisory are asynchronous; each retries for about 15 s (D-205, D-257).
const asyncTimeout = { timeout: 40_000 };

async function openAdvisory(page: Page, sampleFile: string) {
  await signInAs(page, 'Alice');
  await upload(
    page,
    uniqueName(sampleFile.replace('.csv', '')),
    sample(sampleFile),
    randomPastDate(),
  );
  await page.getByRole('status').getByRole('link', { name: 'View details' }).click();
  const advisory = page.getByRole('region', { name: 'Advisory' });
  await expect(advisory).toHaveAttribute('data-advisory-state', 'ready', asyncTimeout);
  return advisory;
}

test('A shares-heavy sample portfolio gets a template advisory that explains the reduction', async ({
  page,
}) => {
  const advisory = await openAdvisory(page, 'valid-shares-heavy.csv');

  await expect(
    advisory.getByText(
      'The proposal reduces Shares by $1,562,500.00 to $637,500.00, which is 85.00 % of its limit.',
      { exact: false },
    ),
  ).toBeVisible();
  await expect(advisory.getByText('Written from a template.')).toBeVisible();
});

test('A balanced sample portfolio gets an advisory that needs no rebalancing', async ({ page }) => {
  const advisory = await openAdvisory(page, 'valid-balanced.csv');

  await expect(advisory.getByText('No rebalancing is needed.', { exact: false })).toBeVisible();
  await expect(advisory.getByText('It is not investment advice.', { exact: false })).toBeVisible();
});
