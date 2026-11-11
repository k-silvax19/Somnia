import { expect, test, Page, BrowserContext } from '@playwright/test';

const account = {
  id: 'test-user',
  displayName: 'Ana Teste',
  username: 'ana.teste',
  email: 'ana@example.test',
  bio: null,
  avatarUrl: null,
};

async function mockApi(context: BrowserContext) {
  const calls: { path: string; headers: Record<string, string>; body: unknown }[] = [];
  let csrf = 'csrf-initial';
  await context.route('**/api/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    calls.push({ path, headers: request.headers(), body: request.postDataJSON() });
    if (path === '/api/me') {
      expect(request.headers()['authorization']).toBe('Bearer fake-access-token');
      await route.fulfill({ json: account });
    } else if (path === '/api/auth/register') {
      expect(request.headers()['x-somnia-client']).toBe('web');
      await route.fulfill({ status: 201, json: account });
    } else if (path === '/api/auth/login' || path === '/api/auth/refresh') {
      expect(request.headers()['x-somnia-client']).toBe('web');
      if (path.endsWith('refresh')) expect(request.headers()['x-csrf-token']).toBe(csrf);
      csrf = `csrf-${calls.length}`;
      await context.addCookies([
        {
          name: '__Host-somnia-csrf',
          value: csrf,
          domain: 'localhost',
          path: '/',
          secure: true,
          sameSite: 'Strict',
        },
      ]);
      await route.fulfill({
        json: {
          usuarioId: account.id,
          nome: account.displayName,
          email: account.email,
          accessToken: 'fake-access-token',
          dataExpiracaoEmUtc: new Date(Date.now() + 900_000).toISOString(),
          refreshExpiraEmUtc: new Date(Date.now() + 86_400_000).toISOString(),
          csrfToken: csrf,
        },
      });
    } else if (path === '/api/auth/logout') {
      expect(request.headers()['authorization']).toBe('Bearer fake-access-token');
      expect(request.headers()['x-csrf-token']).toBe(csrf);
      await context.clearCookies();
      await route.fulfill({ status: 204 });
    } else await route.fulfill({ status: 404 });
  });
  return calls;
}

async function enter(page: Page) {
  await page.goto('/entrar');
  await page.getByLabel('E-mail', { exact: true }).fill(account.email);
  await page.getByLabel('Senha', { exact: true }).fill('Password123!');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Olá, Ana Teste.' })).toBeVisible();
}

test('direct URL, login, reload restoration, guest guard and logout', async ({ page, context }) => {
  const calls = await mockApi(context);
  await page.goto('/inicio');
  await expect(page).toHaveURL(/\/entrar$/);
  expect(calls).toHaveLength(0);
  await enter(page);
  await page.reload();
  await expect(page.getByText('@ana.teste', { exact: true })).toBeVisible();
  expect(calls.filter((call) => call.path.endsWith('/refresh'))).toHaveLength(1);
  await page.goto('/cadastro');
  await expect(page).toHaveURL(/\/inicio$/);
  expect(
    await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length })),
  ).toEqual({ local: 0, session: 0 });
  await page.getByRole('button', { name: 'Sair de todos os dispositivos' }).click();
  await expect(page).toHaveURL(/\/entrar$/);
  await page.goBack();
  await expect(page).toHaveURL(/\/entrar$/);
});

test('registration validates confirmation and returns to login without auto-login', async ({
  page,
  context,
}) => {
  const calls = await mockApi(context);
  await page.goto('/cadastro');
  await page.getByLabel('Nome de exibição').fill(account.displayName);
  await page.getByLabel('Nome de usuário').fill(account.username);
  await page.getByLabel('E-mail', { exact: true }).fill(account.email);
  await page.getByLabel('Senha', { exact: true }).fill('Password123!');
  await page.getByLabel('Confirme sua senha').fill('Wrong123!');
  await page.getByRole('button', { name: 'Criar minha conta' }).click();
  await expect(page.getByText('As senhas precisam ser iguais.')).toBeVisible();
  expect(calls).toHaveLength(0);
  await page.getByLabel('Confirme sua senha').fill('Password123!');
  await page.getByRole('button', { name: 'Criar minha conta' }).click();
  await expect(page).toHaveURL(/\/entrar$/);
  await expect(page.getByText('Conta criada! Entre com seu e-mail e senha.')).toBeVisible();
  await expect(page.getByLabel('E-mail', { exact: true })).toHaveValue(account.email);
  expect(calls.map((call) => call.path)).toEqual(['/api/auth/register']);
});

test('wrong credentials preserve values and respect Retry-After', async ({ page, context }) => {
  await mockApi(context);
  let status = 401;
  await page.route('**/api/auth/login', (route) =>
    route.fulfill({ status, json: {}, headers: status === 429 ? { 'Retry-After': '2' } : {} }),
  );
  await page.goto('/entrar');
  await page.getByLabel('E-mail', { exact: true }).fill(account.email);
  await page.getByLabel('Senha', { exact: true }).fill('Wrong123!');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('E-mail ou senha incorretos');
  await expect(page.getByLabel('E-mail', { exact: true })).toHaveValue(account.email);
  await expect(page.getByLabel('Senha', { exact: true })).toHaveValue('Wrong123!');
  status = 429;
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page.getByRole('button', { name: /Aguarde/ })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Entrar', exact: true })).toBeEnabled({
    timeout: 4000,
  });
});

test('expired session never renders private data and refresh is not retried', async ({
  page,
  context,
}) => {
  await mockApi(context);
  await context.addCookies([
    { name: '__Host-somnia-csrf', value: 'expired', domain: 'localhost', path: '/', secure: true },
  ]);
  let refreshes = 0;
  await page.route('**/api/auth/refresh', async (route) => {
    refreshes++;
    await route.fulfill({ status: 401, json: {} });
  });
  await page.goto('/inicio');
  await expect(page).toHaveURL(/\/entrar$/);
  await expect(page.getByText(account.email, { exact: true })).not.toBeVisible();
  await page.getByRole('link', { name: 'Criar conta' }).click();
  await expect(page).toHaveURL(/\/cadastro$/);
  expect(refreshes).toBe(1);
});

test('logout failure stays on the account and permits retry', async ({ page, context }) => {
  await mockApi(context);
  await enter(page);
  await page.route('**/api/auth/logout', (route) => route.fulfill({ status: 503, json: {} }));
  await page.getByRole('button', { name: 'Sair de todos os dispositivos' }).click();
  await expect(page.getByRole('alert')).toContainText('indisponível');
  await expect(page).toHaveURL(/\/inicio$/);
  await page.unroute('**/api/auth/logout');
  await page.getByRole('button', { name: 'Sair de todos os dispositivos' }).click();
  await expect(page).toHaveURL(/\/entrar$/);
});

test('tabs serialize refresh cookies and broadcast logout', async ({ page, context }) => {
  const calls = await mockApi(context);
  await enter(page);
  const second = await context.newPage();
  await Promise.all([page.reload(), second.goto('/inicio')]);
  await expect(second.getByText('@ana.teste', { exact: true })).toBeVisible();
  await expect(page.getByText('@ana.teste', { exact: true })).toBeVisible();
  expect(calls.filter((call) => call.path.endsWith('refresh'))).toHaveLength(2);
  await second.getByRole('button', { name: 'Sair de todos os dispositivos' }).click();
  await expect(second).toHaveURL(/\/entrar$/);
  await expect(page).toHaveURL(/\/entrar$/);
});

test('responsive forms support keyboard input and reveal password', async ({ page, context }) => {
  await mockApi(context);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/entrar');
  await expect(page.getByLabel('E-mail', { exact: true })).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByLabel('E-mail', { exact: true })).toBeFocused();
  await page.keyboard.type(account.email);
  await page.keyboard.press('Tab');
  await page.keyboard.type('Password123!');
  await page.keyboard.press('Tab');
  await page.keyboard.press('Space');
  await expect(page.getByLabel('Senha', { exact: true })).toHaveAttribute('type', 'text');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/auth-mobile.png', fullPage: true });
  await page.goto('/cadastro');
  await expect(page.getByRole('button', { name: 'Criar minha conta' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/register-mobile.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto('/entrar');
  await expect(page.getByRole('heading', { name: 'Entre no Somnia' })).toBeVisible();
  await page.screenshot({ path: 'test-results/auth-desktop.png', fullPage: true });
});
