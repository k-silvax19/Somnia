import { expect, test } from '@playwright/test';

test('public landing follows the visual reference and opens the account flow', async ({ page }) => {
  await page.route('**/api/auth/refresh', (route) => route.fulfill({ status: 401, json: {} }));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Every song has a story.' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Seu perfil musical' })).toBeVisible();
  await expect(page.getByText('Meu Diário')).toHaveCount(0);
  await page.getByRole('link', { name: 'Criar minha conta' }).click();
  await expect(page).toHaveURL(/\/cadastro$/);
  await page.goto('/');
  await page.getByRole('link', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/entrar$/);
});
