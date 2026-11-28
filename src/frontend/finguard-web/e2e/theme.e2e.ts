import { Page } from '@playwright/test';
import { expect, test } from './support';

const colorScheme = (page: Page) =>
  page.evaluate(() => getComputedStyle(document.documentElement).colorScheme);

const background = (page: Page) =>
  page.evaluate(() => getComputedStyle(document.body).backgroundColor);

test('The toggle switches to dark and the choice survives a reload', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/login');

  await page.getByRole('button', { name: 'Switch to dark theme' }).click();
  await expect.poll(() => colorScheme(page)).toBe('dark');

  await page.reload();
  await expect.poll(() => colorScheme(page)).toBe('dark');
  await expect(page.getByRole('button', { name: 'Switch to light theme' })).toBeVisible();
});

test('Without a choice the app follows a dark operating system', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/login');

  await expect.poll(() => colorScheme(page)).toBe('light dark');
  const toggle = page.getByRole('button', { name: 'Switch to light theme' });
  await expect(toggle).toBeVisible();
  const dark = await background(page);

  await toggle.click();

  await expect.poll(() => colorScheme(page)).toBe('light');
  await expect.poll(() => background(page)).not.toBe(dark);
});
