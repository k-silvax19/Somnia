import { BrowserContext, expect, test } from '@playwright/test';

const account = {
  id: 'user',
  displayName: 'Marina Costa',
  username: 'marina.costa',
  email: 'marina@example.test',
  bio: null,
  avatarUrl: null,
};
const reviews = [
  {
    id: 'one',
    author: { username: 'ana.luiza', displayName: 'Ana Luíza' },
    item: { provider: 'simulated', type: 'album', externalId: 'aurora' },
    score: 92,
    review: 'Cada faixa revela uma nova camada. Um álbum para ouvir com calma, do início ao fim.',
    createdAt: '2026-11-12T12:00:00Z',
    updatedAt: null,
  },
  {
    id: 'two',
    author: { username: 'roberto.c', displayName: 'Roberto Cruz' },
    item: { provider: 'simulated', type: 'track', externalId: 'horizonte' },
    score: 0,
    review: 'A produção é interessante, mas a música não funcionou para mim.',
    createdAt: '2026-11-11T10:00:00Z',
    updatedAt: null,
  },
  {
    id: 'three',
    author: { username: 'fernanda.lima', displayName: 'Fernanda Lima' },
    item: { provider: 'simulated', type: 'album', externalId: 'noturno' },
    score: null,
    review:
      'Uma trilha para o final do dia.\nAinda quero ouvir mais uma vez antes de dar uma nota.',
    createdAt: '2026-11-10T10:00:00Z',
    updatedAt: '2026-11-11T10:00:00Z',
  },
];
const pageData = (items: unknown[], hasMore = false, offset = 0) => ({
  items,
  limit: 10,
  offset,
  total: items.length + (hasMore ? 1 : 0),
  hasMore,
});

async function fixture(context: BrowserContext) {
  const calls: string[] = [];
  await context.addCookies([
    { name: '__Host-somnia-csrf', value: 'fixture', domain: 'localhost', path: '/', secure: true },
  ]);
  await context.route('**/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    calls.push(url.pathname + url.search);
    if (url.pathname.endsWith('/auth/refresh')) {
      await route.fulfill({
        json: {
          usuarioId: account.id,
          nome: account.displayName,
          email: account.email,
          accessToken: 'fixture-token',
          csrfToken: 'fixture',
          dataExpiracaoEmUtc: new Date(Date.now() + 900_000).toISOString(),
          refreshExpiraEmUtc: new Date(Date.now() + 86_400_000).toISOString(),
        },
      });
      return;
    }
    expect(request.headers()['authorization']).toBe('Bearer fixture-token');
    if (url.pathname === '/api/me') await route.fulfill({ json: account });
    else if (url.pathname.endsWith('/connections'))
      await route.fulfill({ json: { isFollowing: false, followersCount: 31, followingCount: 18 } });
    else if (url.pathname.endsWith('/reviews'))
      await route.fulfill({ json: { ...pageData([]), total: 12 } });
    else if (url.pathname === '/api/feed') await route.fulfill({ json: pageData(reviews) });
    else if (url.pathname === '/api/discover')
      await route.fulfill({
        json: pageData([
          {
            ...reviews[0],
            id: 'discover',
            author: { username: 'marina.costa', displayName: 'Marina Costa' },
            review: 'Minha descoberta de hoje.',
          },
        ]),
      });
    else if (url.pathname === '/api/catalog/search')
      await route.fulfill({
        json: pageData([
          {
            ...reviews[0].item,
            title: 'Resultado da busca',
            artists: ['Artista da busca'],
            coverUrl: null,
            spotifyUrl: null,
          },
        ]),
      });
    else if (url.pathname.startsWith('/api/catalog/')) {
      const reference = reviews.find((review) =>
        url.pathname.endsWith('/' + review.item.externalId),
      )!.item;
      const titles: Record<string, string> = {
        aurora: 'Aurora',
        horizonte: 'Horizonte',
        noturno: 'Noturno',
      };
      await route.fulfill({
        json: {
          ...reference,
          title: titles[reference.externalId],
          artists: ['Artista de demonstração'],
          coverUrl: null,
          spotifyUrl: null,
        },
      });
    } else await route.fulfill({ status: 404 });
  });
  return calls;
}

test('home hydrates metadata, preserves zero and no-score, switches tabs and opens account', async ({
  page,
  context,
}) => {
  const calls = await fixture(context);
  await page.goto('/inicio');
  await expect(page.getByRole('heading', { name: 'Seu universo musical' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Aurora', exact: true })).toBeVisible();
  await expect(page.getByLabel('Nota 0 de 100', { exact: true })).toBeVisible();
  await expect(page.getByText('Sem nota', { exact: true })).toBeVisible();
  await expect(page.getByText('@marina.costa', { exact: true })).toBeVisible();
  await expect(page.getByRole('definition').filter({ hasText: /^31$/ })).toBeVisible();
  await page.screenshot({ path: 'test-results/home-desktop.png', fullPage: true });
  await page.getByRole('button', { name: 'Descobrir', exact: true }).click();
  await expect(page.getByText('Minha descoberta de hoje.')).toBeVisible();
  await expect(page.getByText('A produção é interessante', { exact: false })).not.toBeVisible();
  expect(calls.filter((url) => url === '/api/catalog/albums/aurora')).toHaveLength(1);
  await page.getByRole('link', { name: 'Minha conta', exact: true }).click();
  await expect(page).toHaveURL(/\/conta$/);
  await expect(page.getByRole('heading', { name: 'Olá, Marina Costa.' })).toBeVisible();
  await page.getByRole('link', { name: 'Voltar ao início', exact: false }).click();
  await expect(page).toHaveURL(/\/inicio$/);
});

test('empty feed invites discovery without fake reviews', async ({ page, context }) => {
  await fixture(context);
  await page.route('**/api/feed?*', (route) => route.fulfill({ json: pageData([]) }));
  await page.goto('/inicio');
  await expect(page.getByText('Seu feed está começando')).toBeVisible();
  await page.getByRole('button', { name: 'Explorar a comunidade' }).click();
  await expect(page.getByText('Minha descoberta de hoje.')).toBeVisible();
});

test('a slow previous tab cannot replace the selected timeline', async ({ page, context }) => {
  await fixture(context);
  let release!: () => void;
  let finished!: () => void;
  const delayed = new Promise<void>((resolve) => {
    release = resolve;
  });
  const settled = new Promise<void>((resolve) => {
    finished = resolve;
  });
  await page.route('**/api/feed?*', async (route) => {
    await delayed;
    await route.fulfill({ json: pageData(reviews) }).catch(() => {});
    finished();
  });
  await page.goto('/inicio');
  await expect(page.getByText('Carregando avaliações…', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Descobrir', exact: true }).click();
  await expect(page.getByText('Minha descoberta de hoje.')).toBeVisible();
  release();
  await settled;
  await expect(page.locator('app-review-card')).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'Descobrir', exact: true })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
});

test('catalog outage keeps reviews visible and unsafe review text is escaped', async ({
  page,
  context,
}) => {
  await fixture(context);
  const text = '<img src=x onerror="alert(1)"> Uma resenha em texto simples.';
  await page.route('**/api/feed?*', (route) =>
    route.fulfill({ json: pageData([{ ...reviews[0], review: text }]) }),
  );
  await page.route('**/api/catalog/**', (route) => route.fulfill({ status: 503, json: {} }));
  await page.goto('/inicio');
  await expect(page.getByText(text, { exact: true })).toBeVisible();
  await expect(page.getByText('Dados musicais indisponíveis no momento.')).toBeVisible();
  await expect(page.getByLabel('Nota 92 de 100')).toBeVisible();
  await expect(page.locator('app-review-card img')).toHaveCount(0);
});

test('pagination appends distinct reviews and keeps the first page after a failed load', async ({
  page,
  context,
}) => {
  await fixture(context);
  let fail = true;
  await page.route('**/api/feed?*', (route) => {
    const offset = new URL(route.request().url()).searchParams.get('offset');
    if (offset === '0') return route.fulfill({ json: pageData([reviews[0]], true) });
    return fail
      ? route.fulfill({ status: 503, json: {} })
      : route.fulfill({ json: pageData([reviews[0], reviews[1]], false, 10) });
  });
  await page.goto('/inicio');
  await page.getByRole('button', { name: 'Carregar mais avaliações' }).click();
  await expect(page.getByRole('alert')).toContainText('indisponível');
  await expect(page.getByRole('heading', { name: 'Aurora', exact: true })).toBeVisible();
  fail = false;
  await page.getByRole('button', { name: 'Tentar novamente', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Horizonte', exact: true })).toBeVisible();
  await expect(page.locator('app-review-card')).toHaveCount(2);
});

test('rate limit blocks retries until Retry-After', async ({ page, context }) => {
  await fixture(context);
  await page.route('**/api/feed?*', (route) =>
    route.fulfill({ status: 429, headers: { 'Retry-After': '2' }, json: {} }),
  );
  await page.goto('/inicio');
  await expect(page.getByRole('button', { name: /Aguarde/ })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Tentar novamente', exact: true })).toBeEnabled({
    timeout: 4000,
  });
});

test('mobile navigation and real search parameters work without horizontal overflow', async ({
  page,
  context,
}) => {
  const calls = await fixture(context);
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto('/inicio');
  await expect(page.getByRole('heading', { name: 'Noturno', exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/home-mobile.png', fullPage: true });
  await page.getByRole('button', { name: 'Abrir navegação' }).click();
  await page.getByRole('button', { name: 'Busca', exact: true }).click();
  await expect(page.getByRole('searchbox')).toBeFocused();
  await page.getByRole('searchbox').fill('Somnia & noite');
  await page.getByLabel('Tipo de busca').selectOption('track');
  await page.getByRole('button', { name: 'Pesquisar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Resultado da busca' })).toBeVisible();
  const query = calls.find((url) => url.startsWith('/api/catalog/search'))!;
  expect(new URL(query, 'https://localhost').searchParams.get('q')).toBe('Somnia & noite');
  expect(new URL(query, 'https://localhost').searchParams.get('type')).toBe('track');
});
