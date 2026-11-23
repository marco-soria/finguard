import { Page } from '@playwright/test';
import { expect, test } from './support';

const colorScheme = (page: Page) =>
  page.evaluate(() => getComputedStyle(document.documentElement).colorScheme);

const background = (page: Page) =>
  page.evaluate(() => getComputedStyle(document.body).backgroundColor);

const choose = (page: Page, name: 'System' | 'Light' | 'Dark') =>
  page.getByRole('radiogroup', { name: 'Theme' }).getByRole('radio', { name }).click();

test('Dark applies color-scheme dark and survives a reload', async ({ page }) => {
  await page.goto('/login');

  await choose(page, 'Dark');
  await expect.poll(() => colorScheme(page)).toBe('dark');

  await page.reload();
  await expect.poll(() => colorScheme(page)).toBe('dark');
  await expect(page.getByRole('radio', { name: 'Dark' })).toHaveAttribute('aria-checked', 'true');
});

test('System follows a dark operating system', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/login');
  await choose(page, 'Light');
  const light = await background(page);
  await choose(page, 'Dark');
  const dark = await background(page);

  await choose(page, 'System');

  await expect.poll(() => colorScheme(page)).toBe('light dark');
  await expect.poll(() => background(page)).toBe(dark);
  expect(dark).not.toBe(light);
});
