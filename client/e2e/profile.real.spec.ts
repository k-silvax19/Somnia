import { Page, expect, test } from '@playwright/test';

async function register(page: Page, username: string, displayName: string) {
  const email = `${username}@example.test`;
  const password = 'BrowserProfile123!';
  await page.goto('/cadastro');
  await page.getByLabel('Nome de exibição').fill(displayName);
  await page.getByLabel('Nome de usuário').fill(username);
  await page.getByLabel('E-mail', { exact: true }).fill(email);
  await page.getByLabel('Senha', { exact: true }).fill(password);
  await page.getByLabel('Confirme sua senha').fill(password);
  await page.getByRole('button', { name: 'Criar minha conta' }).click();
  await expect(page).toHaveURL(/\/entrar$/);
  await page.getByLabel('E-mail', { exact: true }).fill(email);
  await page.getByLabel('Senha', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/inicio$/);
}

test('real API: profile edit, history and following between two participants', async ({
  page,
  browser,
}) => {
  const suffix = Date.now().toString(36);
  const author = `author_${suffix}`;
  const follower = `follower_${suffix}`;
  await register(page, author, 'Autora');
  await page.goto('/conta');
  await expect(page.getByRole('heading', { name: 'Autora', level: 1 })).toBeVisible();
  await page.getByRole('tab', { name: 'Editar usuário' }).click();
  await page.getByLabel('Nome de exibição').fill('Autora Editada');
  await page.getByLabel('Bio', { exact: true }).fill('Discos de madrugada.');
  await page.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(page.getByText('Perfil salvo.')).toBeVisible();
  await page.goto('/musica/simulated/album/somnia-demo-album-aurora');
  await expect(page.getByRole('heading', { name: 'Aurora', level: 1 })).toBeVisible();
  await page.getByLabel('Nota (opcional)', { exact: true }).fill('0');
  await page.getByRole('button', { name: 'Publicar avaliação' }).click();
  await expect(page.getByText('Avaliação salva.', { exact: true })).toBeVisible();
  await page.goto('/conta');
  await expect(page.getByLabel('Nota 0 de 100')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Aurora', exact: true })).toBeVisible();

  const secondContext = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const second = await secondContext.newPage();
    await register(second, follower, 'Seguidor');
    await second.goto(`/perfil/${author}`);
    await expect(second.getByRole('heading', { name: 'Autora Editada', level: 1 })).toBeVisible();
    await expect(second.getByText('Discos de madrugada.')).toBeVisible();
    await expect(second.getByRole('heading', { name: 'Aurora', exact: true })).toBeVisible();
    await second.getByRole('button', { name: 'Seguir', exact: true }).click();
    await expect(second.getByRole('button', { name: 'Deixar de seguir' })).toBeVisible();
    await page.getByRole('button', { name: 'Seguidores', exact: true }).click();
    await expect(page.getByRole('link', { name: 'Seguidor', exact: true })).toBeVisible();
    await second.getByRole('button', { name: 'Deixar de seguir' }).click();
    await expect(second.getByRole('button', { name: 'Seguir', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Avaliações', exact: true }).click();
    await page.getByRole('button', { name: 'Seguidores', exact: true }).click();
    await expect(page.getByText('Nenhum perfil nesta lista.')).toBeVisible();
  } finally {
    await secondContext.close();
  }
});
