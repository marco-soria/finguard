import { expect, test } from '@playwright/test';
import { header, signInAs, uniqueName, uniquePositionId, upload } from './support';

// A random past cut-off date keeps this flow's "current" rows away from the other flows, which upload for today in
// parallel. Only this run's file names are asserted on.
// From 2000-01-01 (the first limit policy, D-190) to 30 days before today.
function randomPastDate(): string {
  const day = 86_400_000;
  const first = Date.UTC(2000, 0, 1);
  const days = Math.floor((Date.now() - 30 * day - first) / day);
  return new Date(first + Math.floor(Math.random() * (days + 1)) * day).toISOString().slice(0, 10);
}

test('Uploading the same file twice returns the first; a newer file becomes current', async ({
  page,
}) => {
  const cutOffDate = randomPastDate();
  const firstName = uniqueName('first');
  const firstCsv = `${header}\n${uniquePositionId()},cash,1000.00,USD,E2E\n`;
  const history = page.getByRole('table', { name: /Your submissions/ });

  await signInAs(page, 'Alice');
  await upload(page, firstName, firstCsv, cutOffDate);
  await expect(page.getByRole('status')).toContainText(`Accepted ${firstName}: 1 positions.`);

  // Same bytes and date under another name: nothing new is stored (D-139).
  await upload(page, uniqueName('copy'), firstCsv, cutOffDate);
  const status = page.getByRole('status');
  await expect(status).toContainText(/^Already uploaded on .+: Accepted\.\s*View details$/);
  await expect(history.getByRole('row').filter({ hasText: firstName })).toHaveCount(1);
  await expect(history.getByRole('row').filter({ hasText: 'e2e-copy-' })).toHaveCount(0);

  const secondName = uniqueName('second');
  await upload(
    page,
    secondName,
    `${header}\n${uniquePositionId()},cash,2000.00,USD,E2E\n`,
    cutOffDate,
  );
  await expect(status).toContainText(`Accepted ${secondName}: 1 positions.`);

  // Phase 2 exit: two submissions for one date coexist; the latest accepted one is current (D-141).
  const statusCell = (name: string) =>
    history.getByRole('row').filter({ hasText: name }).getByRole('cell').nth(3);
  await expect(statusCell(secondName)).toHaveText(/^\s*Accepted\s+Current\s*$/);
  await expect(statusCell(firstName)).toHaveText('Accepted');

  await history.getByRole('link', { name: firstName }).click();
  await expect(page.getByText('Not the current version')).toBeVisible();
});
