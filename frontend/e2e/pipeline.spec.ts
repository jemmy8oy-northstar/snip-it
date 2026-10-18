import { test, expect, type Page } from '@playwright/test';
import { mockApi } from './mocks';

test.beforeEach(async ({ page }) => {
  await mockApi(page);
});

async function addIdea(page: Page, title: string, notes = '') {
  await page.getByLabel('Working title').fill(title);
  if (notes) await page.getByLabel('Notes', { exact: true }).fill(notes);
  await page.getByRole('button', { name: 'Add idea' }).click();
}

test('add an idea, write its script, see it in Scripted, reload, still there', async ({ page }) => {
  await page.goto('./');
  for (const name of ['Idea', 'Scripted', 'Recorded', 'Cut', 'Published']) {
    await expect(page.getByRole('region', { name, exact: true })).toBeVisible();
  }

  await addIdea(page, 'How I edit in one pass', 'talk about cuts');
  await expect(page.getByTestId('col-idea').getByRole('heading', { name: 'How I edit in one pass' })).toBeVisible();

  await page.getByRole('button', { name: 'Open' }).click();
  await page.getByRole('textbox', { name: 'Script' }).fill('Welcome back. Today: one pass.');
  await page.getByRole('button', { name: 'Save script' }).click();
  await expect(page.getByTestId('col-scripted').getByRole('heading', { name: 'How I edit in one pass' })).toBeVisible();

  await page.reload();
  await expect(page.getByTestId('col-scripted').getByRole('heading', { name: 'How I edit in one pass' })).toBeVisible();
});

test('board screenshots with sample cards (phone and desktop)', async ({ page }) => {
  await page.goto('./');
  await addIdea(page, 'Why I stopped using presets', 'a short rant');
  await addIdea(page, 'Shot-by-shot: the studio tour');
  await addIdea(page, 'Cutting a podcast in minutes');
  await addIdea(page, 'Behind the thumbnail');
  await page.getByLabel('Stage of Shot-by-shot: the studio tour').selectOption('recorded');
  await page.getByLabel('Stage of Cutting a podcast in minutes').selectOption('cut');
  await page.getByLabel('Stage of Behind the thumbnail').selectOption('published');

  for (const [name, size] of [
    ['phone', { width: 390, height: 844 }],
    ['desktop', { width: 1280, height: 800 }],
  ] as const) {
    await page.setViewportSize(size);
    await expect(page.getByTestId('col-published').getByRole('heading', { name: 'Behind the thumbnail' })).toBeVisible();
    await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
    await page.screenshot({
      path: `e2e/screenshots/board-${name}.png`,
      fullPage: true,
      animations: 'disabled',
    });
  }
});
