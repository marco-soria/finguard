import { Page } from '@playwright/test';
import { expect, randomPastDate, sample, signInAs, test, uniqueName, upload } from './support';

// Evaluation is asynchronous: the page may show "Evaluating…" first and retries for about 15 s (D-205).
const evaluationTimeout = { timeout: 20_000 };

async function openEvaluation(page: Page, sampleFile: string) {
  await signInAs(page, 'Alice');
  await upload(
    page,
    uniqueName(sampleFile.replace('.csv', '')),
    sample(sampleFile),
    randomPastDate(),
  );
  await page.getByRole('status').getByRole('link', { name: 'View details' }).click();
  const compliance = page.getByRole('region', { name: 'Compliance' });
  await expect(compliance).toHaveAttribute('data-state', 'ready', evaluationTimeout);
  return compliance;
}

test('A balanced sample portfolio is compliant with no alerts', async ({ page }) => {
  const compliance = await openEvaluation(page, 'valid-balanced.csv');

  await expect(compliance.locator('.compliance-overall [data-status]')).toHaveText('Compliant');
  await expect(compliance.getByText('Policy version 1')).toBeVisible();
  await expect(compliance.getByText('$5,000,000.00')).toBeVisible();
  await expect(compliance.getByText('No alerts.')).toBeVisible();
});

test('A shares-heavy sample portfolio is breached with a Shares alert', async ({ page }) => {
  const compliance = await openEvaluation(page, 'valid-shares-heavy.csv');

  await expect(compliance.locator('.compliance-overall [data-status]')).toHaveText('Breached');
  await expect(compliance.getByText('Policy version 1')).toBeVisible();
  await expect(compliance.getByRole('row').filter({ hasText: 'Shares' })).toHaveAttribute(
    'data-status',
    'Breached',
  );
  await expect(compliance.getByRole('listitem')).toHaveText([
    'Shares is over its limit (293.33 % of the limit used).',
  ]);
});
