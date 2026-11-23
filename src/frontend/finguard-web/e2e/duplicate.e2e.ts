import {
  expect,
  header,
  randomPastDate,
  signInAs,
  test,
  uniqueName,
  uniquePositionId,
  upload,
} from './support';

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
