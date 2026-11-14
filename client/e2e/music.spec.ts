import { BrowserContext, expect, test } from '@playwright/test';

const reference = { provider: 'simulated', type: 'album', externalId: 'aurora' };
const track = {
  provider: 'simulated',
  type: 'track',
  externalId: 'horizonte',
  title: 'Horizonte',
  artists: ['Somnia'],
  coverUrl: null,
  spotifyUrl: null,
  trackNumber: 1,
};
const album = {
  ...reference,
  title: 'Aurora',
  artists: ['Somnia'],
  coverUrl: null,
  spotifyUrl: null,
  tracks: [track],
};
const own = {
  id: 'own',
  author: { username: 'marina', displayName: 'Marina' },
  item: reference,
  score: 0 as number | null,
  review: 'Resenha existente.' as string | null,
  createdAt: '2026-11-14T12:00:00Z',
  updatedAt: null,
};
const url = '/musica/simulated/album/aurora';
const pageData = (items: unknown[]) => ({
  items,
  limit: 10,
  offset: 0,
  total: items.length,
  hasMore: false,
});

async function fixture(context: BrowserContext, initial: typeof own | null = null) {
  const state = { mine: initial, writes: [] as { method: string; body: unknown }[], failWrite: 0 };
  await context.addCookies([
    { name: '__Host-somnia-csrf', value: 'fixture', domain: 'localhost', path: '/', secure: true },
  ]);
  await context.route('**/api/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path.endsWith('/auth/refresh'))
      return route.fulfill({
        json: {
          usuarioId: 'user',
          nome: 'Marina',
          email: 'marina@example.test',
          accessToken: 'fixture-token',
          csrfToken: 'fixture',
          dataExpiracaoEmUtc: new Date(Date.now() + 900_000).toISOString(),
          refreshExpiraEmUtc: new Date(Date.now() + 86_400_000).toISOString(),
        },
      });
    expect(request.headers()['authorization']).toBe('Bearer fixture-token');
    if (request.method() !== 'GET') {
      expect(request.headers()['x-somnia-client']).toBe('web');
      const body = request.postDataJSON();
      state.writes.push({ method: request.method(), body });
      if (state.failWrite)
        return route.fulfill({
          status: state.failWrite,
          headers: { 'Retry-After': '1' },
          json: {},
        });
      if (request.method() === 'DELETE') {
        state.mine = null;
        return route.fulfill({ status: 204 });
      }
      state.mine = { ...own, ...body };
      return route.fulfill({ status: request.method() === 'POST' ? 201 : 200, json: state.mine });
    }
    if (path === '/api/catalog/albums/aurora') return route.fulfill({ json: album });
    if (path === '/api/catalog/tracks/horizonte')
      return route.fulfill({ json: { ...track, album } });
    if (path.startsWith('/api/catalog/') && path.endsWith('/reviews')) {
      const mine = path.includes('/album/') ? state.mine : null;
      return route.fulfill({
        json: {
          reviews: pageData(mine ? [mine] : []),
          summary: { average: mine?.score ?? null, count: mine?.score == null ? 0 : 1 },
          myReview: mine,
        },
      });
    }
    if (path === '/api/me')
      return route.fulfill({
        json: {
          id: 'user',
          displayName: 'Marina',
          username: 'marina',
          email: 'marina@example.test',
          avatarUrl: null,
        },
      });
    if (path.endsWith('/connections'))
      return route.fulfill({ json: { followersCount: 0, followingCount: 0, isFollowing: false } });
    if (path === '/api/catalog/search') return route.fulfill({ json: pageData([album]) });
    if (path === '/api/feed' || path === '/api/discover' || path.endsWith('/reviews'))
      return route.fulfill({ json: pageData([]) });
    return route.fulfill({ status: 404, json: {} });
  });
  return state;
}

test('search opens album, track and parent album with independent evaluations', async ({
  page,
  context,
}) => {
  await fixture(context, own);
  await page.goto('/inicio');
  await page.getByRole('searchbox').fill('Aurora');
  await page.getByRole('button', { name: 'Pesquisar', exact: true }).click();
  await page.getByRole('link', { name: 'Aurora', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(url + '$'));
  await expect(page.getByRole('heading', { name: 'Aurora', level: 1 })).toBeVisible();
  await expect(page.getByLabel('Média da comunidade')).toContainText('0,0');
  await page.getByRole('link', { name: /Horizonte/ }).click();
  await expect(page.getByRole('heading', { name: 'Horizonte', level: 1 })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Publicar avaliação' })).toBeVisible();
  await page.getByRole('link', { name: 'Aurora', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Editar avaliação' })).toBeVisible();
});

test('validates empty and fractional scores, creates zero, edits to text only, confirms deletion', async ({
  page,
  context,
}) => {
  const state = await fixture(context);
  await page.goto(url);
  await page.getByRole('button', { name: 'Publicar avaliação' }).click();
  await expect(page.getByText('Preencha uma nota ou escreva uma resenha.')).toBeVisible();
  await page.getByLabel('Nota (opcional)', { exact: true }).fill('0.5');
  await page.getByRole('button', { name: 'Publicar avaliação' }).click();
  await expect(page.getByText('Informe uma nota inteira entre 0 e 100.')).toBeVisible();
  expect(state.writes).toHaveLength(0);
  await page.getByLabel('Nota (opcional)', { exact: true }).fill('0');
  await page.getByRole('button', { name: 'Publicar avaliação' }).click();
  await expect(page.getByText('Avaliação salva.', { exact: true })).toBeVisible();
  expect(state.writes[0].body).toEqual({ ...reference, score: 0, review: null });
  await expect(page.getByLabel('Média da comunidade')).toContainText('0,0');
  await page.getByRole('button', { name: 'Editar avaliação' }).click();
  await page.getByLabel('Nota (opcional)', { exact: true }).fill('');
  const text = '<img src=x onerror="alert(1)"> Gostei da composição.';
  await page.getByLabel('Resenha (opcional)', { exact: true }).fill(text);
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText('Avaliação salva.', { exact: true })).toBeVisible();
  expect(state.writes[1]).toEqual({ method: 'PUT', body: { score: null, review: text } });
  await expect(page.locator('app-review-editor .review')).toHaveText(text);
  await expect(page.locator('app-review-editor img')).toHaveCount(0);
  await expect(page.getByLabel('Média da comunidade')).toContainText('0 notas');
  await page.getByRole('button', { name: 'Excluir avaliação', exact: true }).click();
  await page.getByRole('button', { name: 'Cancelar exclusão' }).click();
  expect(state.writes).toHaveLength(2);
  await page.getByRole('button', { name: 'Excluir avaliação', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar exclusão', exact: true }).click();
  await expect(page.getByText('Avaliação excluída.', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Publicar avaliação' })).toBeVisible();
  expect(state.writes[2].method).toBe('DELETE');
});

test('failed save retains draft and failed deletion keeps review', async ({ page, context }) => {
  const state = await fixture(context, own);
  await page.goto(url);
  await page.getByRole('button', { name: 'Editar avaliação' }).click();
  await page.getByLabel('Resenha (opcional)', { exact: true }).fill('Meu rascunho preservado.');
  state.failWrite = 500;
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByRole('alert')).toContainText('indisponível');
  await expect(page.getByLabel('Resenha (opcional)', { exact: true })).toHaveValue(
    'Meu rascunho preservado.',
  );
  expect(state.writes).toHaveLength(1);
  await page.getByRole('button', { name: 'Cancelar edição' }).click();
  await page.getByRole('button', { name: 'Excluir avaliação', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar exclusão', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('indisponível');
  await expect(page.locator('app-review-editor .review')).toHaveText(own.review!);
  expect(state.mine).not.toBeNull();
});

test('catalog outage still allows editing own review and mobile has no overflow', async ({
  page,
  context,
}) => {
  await fixture(context, own);
  await page.route('**/api/catalog/albums/aurora', (route) =>
    route.fulfill({ status: 503, json: {}, headers: { 'Retry-After': '30' } }),
  );
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto(url);
  await expect(page.getByRole('alert')).toContainText('catálogo musical');
  await expect(page.getByRole('button', { name: /Aguarde/ })).toBeDisabled();
  await page.getByRole('button', { name: 'Editar avaliação' }).click();
  await page.getByLabel('Nota (opcional)', { exact: true }).fill('100');
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText('Avaliação salva.', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('duplicate creation requires explicit reload and never overwrites the existing review', async ({
  page,
  context,
}) => {
  const state = await fixture(context);
  await page.goto(url);
  await page.getByLabel('Resenha (opcional)', { exact: true }).fill('Rascunho de outra aba.');
  state.mine = own;
  state.failWrite = 409;
  await page.getByRole('button', { name: 'Publicar avaliação' }).click();
  await expect(page.getByRole('alert')).toContainText('Você já avaliou');
  await expect(page.getByLabel('Resenha (opcional)', { exact: true })).toHaveValue(
    'Rascunho de outra aba.',
  );
  await expect(page.getByRole('button', { name: 'Publicar avaliação' })).toBeDisabled();
  await page.getByRole('button', { name: 'Carregar avaliação existente' }).click();
  await expect(page.getByRole('button', { name: 'Editar avaliação' })).toBeVisible();
  expect(state.writes).toHaveLength(1);
});

test('community pagination deduplicates and retries without dropping the first page', async ({
  page,
  context,
}) => {
  await fixture(context);
  let fail = true;
  await page.route('**/api/catalog/simulated/album/aurora/reviews?*', (route) => {
    const offset = Number(new URL(route.request().url()).searchParams.get('offset'));
    if (offset > 0 && fail) return route.fulfill({ status: 500, json: {} });
    return route.fulfill({
      json: {
        reviews: {
          items: offset ? [own, { ...own, id: 'another', review: 'Segunda avaliação.' }] : [own],
          limit: 10,
          offset,
          total: 11,
          hasMore: offset === 0,
        },
        summary: { average: 0, count: 11 },
        myReview: null,
      },
    });
  });
  await page.goto(url);
  await page.getByRole('button', { name: 'Carregar mais avaliações' }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.locator('app-review-card')).toHaveCount(1);
  fail = false;
  await page.getByRole('button', { name: 'Tentar carregar avaliações' }).click();
  await expect(page.locator('app-review-card')).toHaveCount(2);
});

test('detail route is guarded and invalid references do not request catalog data', async ({
  page,
  context,
}) => {
  await context.route('**/api/auth/refresh', (route) => route.fulfill({ status: 401, json: {} }));
  await page.goto(url);
  await expect(page).toHaveURL(/\/entrar$/);
  await fixture(context);
  let requests = 0;
  await page.route('**/api/catalog/**', (route) => {
    requests++;
    return route.fulfill({ status: 404, json: {} });
  });
  await page.goto('/musica/unknown/album/aurora');
  await expect(page.getByRole('heading', { name: 'Endereço musical inválido' })).toBeVisible();
  expect(requests).toBe(0);
});

test('details screenshots in desktop and mobile with an editable review', async ({
  page,
  context,
}) => {
  await fixture(context, {
    ...own,
    score: 92,
    review: 'Um álbum para ouvir com calma. A produção dá espaço para cada detalhe.',
  });
  await page.goto(url);
  await expect(page.getByRole('heading', { name: 'Aurora', level: 1 })).toBeVisible();
  await page.getByRole('button', { name: 'Editar avaliação' }).click();
  await page.screenshot({ path: 'test-results/music-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 360, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/music-mobile.png', fullPage: true });
});

test('server field errors preserve draft and throttled writes wait without automatic replay', async ({
  page,
  context,
}) => {
  await fixture(context);
  let status = 400;
  let calls = 0;
  await page.route('**/api/reviews', (route) => {
    calls++;
    return route.fulfill({
      status,
      headers: { 'Retry-After': '2' },
      json: { errors: { Review: ['Resenha recusada na validação.'] } },
    });
  });
  await page.goto(url);
  await page.getByLabel('Resenha (opcional)', { exact: true }).fill('Meu texto preservado.');
  await page.getByRole('button', { name: 'Publicar avaliação' }).click();
  await expect(page.getByRole('alert')).toContainText('Resenha recusada na validação.');
  await expect(page.getByLabel('Resenha (opcional)', { exact: true })).toHaveValue(
    'Meu texto preservado.',
  );
  status = 429;
  await page.getByRole('button', { name: 'Publicar avaliação' }).click();
  await expect(page.getByRole('button', { name: 'Publicar avaliação' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Publicar avaliação' })).toBeEnabled({
    timeout: 4000,
  });
  expect(calls).toBe(2);
});

test('late album reviews cannot replace the selected track or its editor', async ({
  page,
  context,
}) => {
  await fixture(context, own);
  let release!: () => void;
  let started!: () => void;
  let finished!: () => void;
  const delayed = new Promise<void>((resolve) => {
    release = resolve;
  });
  const pending = new Promise<void>((resolve) => {
    started = resolve;
  });
  const settled = new Promise<void>((resolve) => {
    finished = resolve;
  });
  await page.route('**/api/catalog/simulated/album/aurora/reviews?*', async (route) => {
    started();
    await delayed;
    await route
      .fulfill({
        json: { reviews: pageData([own]), summary: { average: 0, count: 1 }, myReview: own },
      })
      .catch(() => {});
    finished();
  });
  await page.goto(url);
  await pending;
  await page.getByRole('link', { name: /Horizonte/ }).click();
  await expect(page.getByRole('heading', { name: 'Horizonte', level: 1 })).toBeVisible();
  release();
  await settled;
  await expect(page.getByRole('button', { name: 'Publicar avaliação' })).toBeVisible();
  await expect(page.locator('app-review-card')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Editar avaliação' })).toHaveCount(0);
});
