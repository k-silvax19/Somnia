import { expect, test } from '@playwright/test';

test('real API: register, login, secure cookies, reload, two tabs and global logout', async ({
  page,
  context,
}) => {
  page.on('response', async (response) => {
    if (
      response.url().includes('/api/auth/') &&
      response.status() >= 400 &&
      response.status() !== 401
    ) {
      const body = await response.json().catch(() => ({}));
      console.error(`Auth API ${response.status()}: ${JSON.stringify(body)}`);
    }
  });
  const username = `auth_${Date.now()}`;
  const email = `${username}@example.test`;
  const password = 'BrowserAuth123!';
  await page.goto('/cadastro');
  await page.getByLabel('Nome de exibição').fill('Participante Auth');
  await page.getByLabel('Nome de usuário').fill(username);
  await page.getByLabel('E-mail', { exact: true }).fill(email);
  await page.getByLabel('Senha', { exact: true }).fill(password);
  await page.getByLabel('Confirme sua senha').fill(password);
  await page.getByRole('button', { name: 'Criar minha conta' }).click();
  await expect(page).toHaveURL(/\/entrar$/);
  await expect(page.getByText('Conta criada! Entre com seu e-mail e senha.')).toBeVisible();

  await page.getByLabel('Senha', { exact: true }).fill('WrongPassword123!');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('E-mail ou senha incorretos');
  await page.getByLabel('Senha', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page.getByText(`@${username}`, { exact: true })).toBeVisible();
  const cookies = await context.cookies();
  expect(cookies.find((cookie) => cookie.name === '__Host-somnia-refresh')).toMatchObject({
    httpOnly: true,
    secure: true,
    sameSite: 'Strict',
    path: '/',
  });
  expect(cookies.find((cookie) => cookie.name === '__Host-somnia-csrf')).toMatchObject({
    httpOnly: false,
    secure: true,
    sameSite: 'Strict',
    path: '/',
  });
  expect(await page.evaluate(() => document.cookie.includes('__Host-somnia-refresh'))).toBe(false);
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);

  const second = await context.newPage();
  await Promise.all([page.reload(), second.goto('/inicio')]);
  await expect(page.getByText(`@${username}`, { exact: true })).toBeVisible();
  await expect(second.getByText(`@${username}`, { exact: true })).toBeVisible();
  await page.goto('/entrar');
  await expect(page).toHaveURL(/\/inicio$/);
  await page.getByRole('button', { name: 'Sair de todos os dispositivos' }).click();
  await expect(page).toHaveURL(/\/entrar$/);
  await expect(second).toHaveURL(/\/entrar$/);
  expect((await context.cookies()).some((cookie) => cookie.name.startsWith('__Host-somnia-'))).toBe(
    false,
  );
  await page.goto('/inicio');
  await expect(page).toHaveURL(/\/entrar$/);
});
