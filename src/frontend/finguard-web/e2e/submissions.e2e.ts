import { expect, header, signInAs, test, uniqueName, uniquePositionId, upload } from './support';

test('Alice uploads a valid CSV, sees it accepted in her history; Bob does not', async ({
  page,
}) => {
  const fileName = uniqueName('accepted');

  await signInAs(page, 'Alice');
  const today = await upload(
    page,
    fileName,
    `${header}\n${uniquePositionId()},cash,1000.00,USD,E2E\n`,
  );

  await expect(page.getByRole('status')).toContainText(`Accepted ${fileName}: 1 positions.`);
  const row = page.getByRole('row').filter({ hasText: fileName });
  await expect(row).toBeVisible();
  await expect(row.getByRole('cell').first()).toHaveText(today);
  // Other flows upload for today in parallel, so "Current" may already belong to a newer row.
  await expect(row.getByRole('cell').nth(3)).toHaveText(/^\s*Accepted/);

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
  await upload(
    page,
    fileName,
    `${header}\n${uniquePositionId()},cash,-5,USD,E2E\nP-2,cash,1,EUR,E2E\nP-3,cash,1,USD,=HYPERLINK(1)\n`,
  );

  const status = page.getByRole('status');
  await expect(status).toContainText(`Rejected ${fileName}: 3 errors.`);
  const row = page.getByRole('row').filter({ hasText: fileName });
  await expect(row.getByRole('cell').nth(3)).toHaveText('Rejected');
  await expect(row.getByRole('cell').nth(4)).toHaveText('3');

  await status.getByRole('link', { name: 'View details' }).click();
  await expect(page.getByRole('heading', { name: 'Errors' })).toBeVisible();
  const errors = page.getByRole('table', { name: 'Errors' }).getByRole('row');
  // Header row first; codes are data attributes, never visible text (D-142).
  await expect(errors).toHaveText([
    'RowColumnMessage',
    '2book_valueThe book value must not be negative.',
    '3currencyOnly USD positions are supported in this version.',
    '4descriptionThe description must not start with =, +, -, @, a tab or a line break.',
  ]);
  await expect(errors.nth(1)).toHaveAttribute('data-code', 'Portfolio.Csv.BookValueNegative');
});

test('A future cut-off date shows the API detail', async ({ page }) => {
  await signInAs(page, 'Alice');
  await upload(
    page,
    uniqueName('future'),
    `${header}\n${uniquePositionId()},cash,1,USD,E2E\n`,
    '2999-01-01',
  );

  await expect(page.getByRole('alert')).toHaveText(
    'Validation failed: The cut-off date must not be later than today (UTC).',
  );
});
