import { APIRequestContext } from '@playwright/test';
import {
  expect,
  header,
  randomPastDate,
  signInAs,
  test,
  uniqueName,
  uniquePositionId,
} from './support';

const addDays = (date: string, days: number) =>
  new Date(Date.parse(`${date}T00:00:00Z`) + days * 86_400_000).toISOString().slice(0, 10);

// Created through the API, so the flow does not depend on earlier runs (Brief #20). Each upload has unique content and
// its own cut-off date inside a random past window; filtering on that window isolates this run's rows, and they are
// the newest rows in it, so they fill the first page.
async function seedHistory(request: APIRequestContext, count: number) {
  const token = await request.post('/dev/token', { data: { user: 'alice' } });
  expect(token.ok()).toBe(true);
  const { accessToken } = (await token.json()) as { accessToken: string };

  const from = randomPastDate();
  const uploads: { fileName: string; status: 'Accepted' | 'Rejected' }[] = [];
  for (let i = 0; i < count; i++) {
    const status = i % 3 === 0 ? 'Rejected' : 'Accepted';
    const fileName = uniqueName(`history-${i}`);
    const csv =
      status === 'Accepted'
        ? `${header}\n${uniquePositionId()},cash,1000.00,USD,E2E\n`
        : `${header}\n${uniquePositionId()},cash,-1,USD,E2E\n`;
    const response = await request.post('/api/v1/portfolio/submissions', {
      headers: { Authorization: `Bearer ${accessToken}` },
      multipart: {
        file: { name: fileName, mimeType: 'text/csv', buffer: Buffer.from(csv) },
        cutOffDate: addDays(from, i),
      },
    });
    expect(response.status()).toBe(201);
    uploads.push({ fileName, status });
  }
  return { from, to: addDays(from, count - 1), uploads };
}

test('Alice filters her history and loads more pages', async ({ page }) => {
  const { from, to, uploads } = await seedHistory(page.request, 27);
  const newestFirst = [...uploads].reverse();

  await signInAs(page, 'Alice');
  await page.getByLabel('Cut-off from').fill(from);
  await page.getByLabel('Cut-off to').fill(to);
  await expect(page).toHaveURL(new RegExp(`cutOffFrom=${from}&cutOffTo=${to}`));

  // First page: the 25 newest of this run's uploads.
  const links = page.getByRole('table').getByRole('link');
  await expect(links).toHaveCount(25);
  await expect(links).toHaveText(newestFirst.slice(0, 25).map((u) => u.fileName));

  await page.getByRole('button', { name: 'Load more' }).click();
  await expect(links.nth(26)).toHaveText(newestFirst[26].fileName);
  await expect(links.nth(25)).toHaveText(newestFirst[25].fileName);

  await page.getByLabel('Status').click();
  await page.getByRole('option', { name: 'Rejected' }).click();
  await expect(page).toHaveURL(/status=Rejected/);

  // Rows of an older run that fell in the same window would follow this run's rows, never precede them.
  const rejected = newestFirst.filter((u) => u.status === 'Rejected').map((u) => u.fileName);
  const expectRejectedFirst = async () => {
    await expect(links.first()).toHaveText(rejected[0]);
    expect((await links.allTextContents()).slice(0, rejected.length)).toEqual(rejected);
  };
  await expectRejectedFirst();
  await expect(page.getByRole('button', { name: 'Load more' })).toHaveCount(0);

  // The URL carries the filters: a reload shows the same list.
  await page.reload();
  await expect(page.getByLabel('Cut-off from')).toHaveValue(from);
  await expectRejectedFirst();
});
