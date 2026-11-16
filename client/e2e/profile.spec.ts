import { BrowserContext, expect, test } from '@playwright/test';

const baseMe = {
  id: 'mine',
  displayName: 'Marina',
  username: 'marina',
  email: 'marina@example.test',
  bio: 'Minha bio',
  avatarUrl: null as string | null,
  bannerTheme: 'midnight',
};
const other = {
  username: 'ana',
  displayName: 'Ana',
  bio: 'Discos favoritos',
  avatarUrl: null,
  bannerTheme: 'mist',
};
const review = {
  id: 'review-1',
  author: { username: 'ana', displayName: 'Ana' },
  item: { provider: 'simulated', type: 'album', externalId: 'aurora' },
  score: 0,
  review: 'Meu álbum favorito.',
  createdAt: '2026-11-14T12:00:00Z',
  updatedAt: null,
};
const pageData = (items: unknown[], total = items.length, offset = 0) => ({
  items,
  total,
  limit: 10,
  offset,
  hasMore: offset + items.length < total,
});

async function fixture(context: BrowserContext) {
  const me = { ...baseMe };
  const state = { following: false, failEdit: false, edits: [] as unknown[], uploads: 0 };
  await context.addCookies([
    { name: '__Host-somnia-csrf', value: 'fixture', domain: 'localhost', path: '/', secure: true },
  ]);
  await context.route('**/api/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === '/api/auth/refresh')
      return route.fulfill({
        json: {
          usuarioId: 'mine',
          nome: me.displayName,
          email: me.email,
          accessToken: 'fixture-token',
          csrfToken: 'fixture',
          dataExpiracaoEmUtc: new Date(Date.now() + 900_000).toISOString(),
          refreshExpiraEmUtc: new Date(Date.now() + 86_400_000).toISOString(),
        },
      });
    expect(request.headers()['authorization']).toBe('Bearer fixture-token');
    if (request.method() !== 'GET') expect(request.headers()['x-somnia-client']).toBe('web');
    if (path === '/api/me/profile' && request.method() === 'PATCH') {
      const edit = request.postDataJSON();
      state.edits.push(edit);
      if (state.failEdit) return route.fulfill({ status: 503, json: {} });
      me.displayName = edit.displayName;
      me.bio = edit.bio;
      me.bannerTheme = edit.bannerTheme;
      return route.fulfill({ json: me });
    }
    if (path === '/api/me/avatar' && request.method() === 'PUT') {
      state.uploads++;
      expect(request.headers()['content-type']).toContain('multipart/form-data');
      return route.fulfill({
        status: 202,
        json: { id: 'avatar-1', status: 'pending', erro: null },
      });
    }
    if (path === '/api/me/avatar/requests/avatar-1') {
      me.avatarUrl = '/api/users/marina/avatar';
      return route.fulfill({ json: { id: 'avatar-1', status: 'completed', erro: null } });
    }
    if (path === '/api/me/avatar' && request.method() === 'DELETE') {
      me.avatarUrl = null;
      return route.fulfill({ status: 204 });
    }
    if (path === '/api/me') return route.fulfill({ json: me });
    if (path === '/api/users/marina/avatar')
      return route.fulfill(
        me.avatarUrl
          ? {
              contentType: 'image/png',
              body: Buffer.from(
                'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/LZkAAAAASUVORK5CYII=',
                'base64',
              ),
            }
          : { status: 404 },
      );
    if (path === '/api/users/ana') return route.fulfill({ json: other });
    if (path === '/api/users/ana/follow' && request.method() !== 'GET') {
      state.following = request.method() === 'PUT';
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith('/connections'))
      return route.fulfill({
        json: {
          isFollowing: path.includes('/ana/') && state.following,
          followersCount: path.includes('/ana/') ? Number(state.following) : 0,
          followingCount: 0,
        },
      });
    if (path.endsWith('/reviews'))
      return route.fulfill({ json: pageData(path.includes('/ana/') ? [review] : []) });
    if (path.endsWith('/followers'))
      return route.fulfill({
        json: pageData(state.following ? [{ ...me, followedAt: '2026-11-14T12:00:00Z' }] : []),
      });
    if (path.endsWith('/following')) return route.fulfill({ json: pageData([]) });
    if (path === '/api/catalog/albums/aurora')
      return route.fulfill({
        json: {
          ...review.item,
          title: 'Aurora',
          artists: ['Somnia'],
          coverUrl: null,
          spotifyUrl: null,
        },
      });
    return route.fulfill({ status: 404, json: {} });
  });
  return state;
}

test('own profile keeps edits after a failed save and shows the saved bio', async ({
  page,
  context,
}) => {
  const state = await fixture(context);
  await page.goto('/conta');
  await expect(page.getByRole('heading', { name: 'Marina', level: 1 })).toBeVisible();
  await expect(
    page.locator('.profile-header').getByRole('button', { name: 'Sair de todos os dispositivos' }),
  ).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Editar usuário' })).toHaveCount(0);
  await expect(page.getByText('Nenhuma avaliação publicada.')).toBeVisible();
  await page.screenshot({ path: 'test-results/profile-desktop.png', fullPage: true });
  await page.getByRole('button', { name: 'Editar perfil' }).click();
  await expect(page.getByText('marina@example.test')).toBeVisible();
  await page.screenshot({ path: 'test-results/profile-edit-desktop.png', fullPage: true });
  await page.getByLabel('Nome de exibição').fill('Marina Nova');
  await page.getByLabel('Bio', { exact: true }).fill('Outra bio');
  await page.getByRole('button', { name: 'Aurora' }).click();
  await expect(page.locator('.preview-banner')).toHaveClass(/banner-aurora/);
  state.failEdit = true;
  await page.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByLabel('Bio', { exact: true })).toHaveValue('Outra bio');
  state.failEdit = false;
  await page.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(page.getByRole('heading', { name: 'Marina Nova', level: 1 })).toBeVisible();
  await expect(page.getByText('Perfil salvo.')).toBeVisible();
  expect(state.edits[1]).toEqual({
    displayName: 'Marina Nova',
    bio: 'Outra bio',
    bannerTheme: 'aurora',
  });
  await page.setViewportSize({ width: 360, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/profile-edit-mobile.png', fullPage: true });
  await page.reload();
  await expect(page.locator('.profile-banner')).toHaveClass(/banner-aurora/);
});

test('public profile shows history, follows and lists followers', async ({ page, context }) => {
  const state = await fixture(context);
  await page.goto('/perfil/ana');
  await expect(page.getByRole('heading', { name: 'Ana', level: 1 })).toBeVisible();
  await expect(page.locator('.profile-banner')).toHaveClass(/banner-mist/);
  await expect(page.getByText('Meu álbum favorito.')).toBeVisible();
  await expect(page.getByLabel('Nota 0 de 100')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Aurora' })).toBeVisible();
  await page.getByRole('button', { name: 'Seguir', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Deixar de seguir' })).toBeVisible();
  expect(state.following).toBe(true);
  await page.getByRole('tab', { name: 'Seguidores', exact: true }).click();
  await expect(
    page.locator('#activity-panel').getByRole('link', { name: 'Marina', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Deixar de seguir' }).click();
  await expect(page.getByRole('button', { name: 'Seguir', exact: true })).toBeVisible();
  expect(state.following).toBe(false);
});

test('avatar upload waits for processing and can be removed', async ({ page, context }) => {
  const state = await fixture(context);
  await page.route('**/api/users/marina/reviews?*', (route) =>
    route.fulfill({
      json: pageData([
        { ...review, author: { username: 'marina', displayName: 'Marina', avatarUrl: null } },
      ]),
    }),
  );
  await page.goto('/conta');
  await expect(page.locator('app-review-card .avatar img')).toHaveCount(0);
  await page.getByRole('button', { name: 'Alterar foto do perfil' }).click();
  const chooser = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Escolher foto' }).click();
  await (
    await chooser
  ).setFiles({
    name: 'avatar.png',
    mimeType: 'image/png',
    buffer: Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/LZkAAAAASUVORK5CYII=',
      'base64',
    ),
  });
  await expect(page.getByText('Avatar atualizado.')).toBeVisible();
  await expect(page.locator('.profile-header .avatar img')).toBeVisible();
  await expect(page.locator('app-review-card .avatar img')).toBeVisible();
  expect(state.uploads).toBe(1);
  await page.getByRole('button', { name: 'Fechar' }).click();
  await page.getByRole('link', { name: 'Início', exact: true }).click();
  await expect(page.locator('.sidebar-footer .avatar img')).toBeVisible();
  await page.getByRole('link', { name: 'Minha conta', exact: true }).click();
  await expect(page.locator('.profile-header .avatar img')).toBeVisible();
  await page.getByRole('button', { name: 'Alterar foto do perfil' }).click();
  await page.getByRole('button', { name: 'Remover avatar' }).click();
  await expect(page.getByText('Avatar removido.')).toBeVisible();
  await expect(page.locator('.profile-header .avatar img')).toHaveCount(0);
  await expect(page.locator('app-review-card .avatar img')).toHaveCount(0);
});

test('profile works at 360 px and activity tabs respond to keyboard', async ({ page, context }) => {
  await fixture(context);
  await page.setViewportSize({ width: 360, height: 780 });
  await page.goto('/perfil/ana');
  await expect(page.getByRole('heading', { name: 'Ana', level: 1 })).toBeVisible();
  const followers = page.getByRole('tab', { name: 'Seguidores', exact: true });
  await followers.focus();
  await page.keyboard.press('Enter');
  await expect(followers).toHaveAttribute('aria-selected', 'true');
  const dimensions = await page.evaluate(() => ({
    viewport: window.innerWidth,
    content: document.documentElement.scrollWidth,
  }));
  expect(dimensions.content).toBeLessThanOrEqual(dimensions.viewport);
  await page.screenshot({ path: 'test-results/profile-mobile.png', fullPage: true });
});
