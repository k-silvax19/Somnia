# Perfis

Implementação do perfil básico de RF02 e das regras de privacidade do Somnia.
Conferida em 10/11/2026 contra `Somnia_Especificacao_Tecnica_API_MVP_v1.0.pdf`
(09/11/2026), seções 4, 8, 10 e 15, e o PRD `somnia.docx` (v1.1).

O perfil permite edição de nome, bio, preferências e avatar estático.
O avatar usa endpoints próprios de upload e remoção, em vez de receber
uma URL no PATCH de perfil proposto pelo PDF.
O projeto usa SQL Server, quatro camadas e rotas sem `/v1`; PostgreSQL e
`/api/v1` são propostas do PDF que diferem da implementação atual.

## Endpoints

Todas as rotas abaixo exigem JWT Bearer. A consulta pública significa uma
projeção com dados públicos, disponível nesta etapa para usuários autenticados.
O acesso de visitantes sem conta continua sendo uma decisão pendente na
seção 15 do PDF e na seção 11 do PRD; exigir login não contraria uma decisão
já fechada nesses documentos.

| Método | Rota | Resposta |
| --- | --- | --- |
| GET | `/api/me` | Dados do titular, e-mail e preferências |
| PATCH | `/api/me/profile` | Perfil atualizado do titular |
| GET | `/api/users/{username}` | Apenas username, displayName, bio e avatarUrl |
| PUT | `/api/me/avatar` | Upload multipart e dados atualizados do titular (`200`) |
| DELETE | `/api/me/avatar` | Remove o avatar do titular (`204`) |
| GET | `/api/users/{username}/avatar` | Imagem PNG (`200`) ou `404` se ausente |

`GET /api/me` continua sendo uma única rota, agora no controller de perfis.
O nome de exibição é devolvido como `displayName` também no cadastro.
A consulta por username usa a mesma normalização de nomes do Identity.

## Edição parcial

Envie JSON e os cabeçalhos `Authorization: Bearer ...` e `X-Somnia-Client: web`,
por HTTPS. O usuário alterado é sempre obtido do JWT; não é aceito um ID no corpo.

```json
{
  "displayName": "Ouvinte Noturno",
  "bio": "Meus discos favoritos e novas descobertas.",
  "defaultScoreVisibility": "private",
  "defaultReviewVisibility": "public"
}
```

É possível enviar somente um dos campos. Campos omitidos permanecem inalterados.
`bio: null` ou uma bio em branco remove a bio; não enviar bio preserva seu valor.
Nome nulo/vazio, visibilidade nula/desconhecida, corpo vazio e campos não
permitidos retornam `400` com ProblemDetails.

Limites adotados: nome de exibição de 1 a 256 caracteres e bio de até 300.
Os campos são texto simples; o frontend deve exibi-los com escape, sem interpretá-los
como HTML. Nome e bio são públicos na consulta por username.

As preferências aceitam `private` e `public`, independentemente uma da outra.
Novos usuários e registros já existentes começam com ambas privadas. Essas
preferências servirão de padrão para avaliações futuras; não representam uma
permissão para expor avaliações já existentes.

Não há edição de e-mail, senha ou username por esse endpoint. Dados privados do
Identity e preferências não são selecionados pela consulta pública nem enviados
ao cliente. Todas as respostas de perfil usam `Cache-Control: no-store`.

## Avatar

Envie `multipart/form-data` com o campo `file` para `PUT /api/me/avatar`.
Use HTTPS, JWT Bearer e `X-Somnia-Client: web`; as regras de origem são as
mesmas do PATCH. Não defina manualmente o `Content-Type` ao usar `FormData`
no navegador: ele precisa incluir o boundary gerado pelo cliente.

```javascript
const form = new FormData();
form.append('file', arquivoSelecionado);
const resposta = await fetch('https://localhost:7201/api/me/avatar', {
  method: 'PUT',
  credentials: 'include',
  headers: {
    Authorization: `Bearer ${accessToken}`,
    'X-Somnia-Client': 'web'
  },
  body: form
});
```

O conteúdo real deve ser PNG ou JPEG estático de até 2 MiB e dimensões
de até 2048 × 2048 pixels. Extensão e MIME declarados não são usados para
aprovar o arquivo. GIF, SVG, APNG, arquivos corrompidos e imagens que excedem
os limites são rejeitados com `400`. O limite total da requisição multipart
é 2 MiB + 64 KiB para acomodar o envelope; ultrapassá-lo também pode ser
rejeitado pela infraestrutura HTTP antes da validação do arquivo.

A imagem é decodificada com SkiaSharp e regravada em PNG de até 512 × 512,
preservando proporção, sem ampliar imagens pequenas e sem copiar metadados
ou conteúdo anexado. O arquivo e seu nome original não são armazenados.
A imagem normalizada fica na tabela `Avatares`, vinculada ao titular por
chave estrangeira; consultas comuns de perfil não carregam seus bytes.

`avatarUrl` aponta para `/api/users/{username}/avatar`. Essa rota exige JWT,
assim como as demais consultas de perfil: o frontend deve buscar o conteúdo
com Bearer e criar uma URL de objeto para exibição quando necessário.
Um `<img src="...">` sozinho não envia o Bearer mantido em memória.
A resposta usa `image/png`, `X-Content-Type-Options: nosniff` e `no-store`.

O PUT substitui o avatar anterior e retorna o perfil atualizado. O DELETE
remove somente o avatar do usuário identificado no JWT e pode ser repetido;
`avatarUrl` volta a nulo. Nome de usuário ou ID de terceiros não são
parâmetros de escrita. A troca da imagem e da URL ocorre na mesma transação,
com serialização de alterações simultâneas do mesmo titular. Falha de
validação preserva a imagem anterior. URLs externas continuam rejeitadas
no PATCH.
GIFs de avatar pertencem à fase Premium descrita no PRD v1.1 e estão fora
do escopo da especificação técnica do MVP gratuito.

## O que ainda falta

- Avaliações públicas no perfil, descritas na seção 8 do PDF, dependem do
  módulo de avaliações e ainda não possuem endpoint implementado.
- Seguir, deixar de seguir, feed e descoberta (RF08/RF09) também não estão
  implementados; a consulta de perfil existente não entrega esses recursos.
- Confirmar os limites adotados: bio de 300 segue a sugestão do PDF; nome de
  exibição de 256 é uma escolha da implementação, sem limite definido no PDF.

## Validação

A migration `AdicionarPerfis` cria os campos e os padrões privados. Os testes
unitários cobrem validação e distinção entre campos omitidos e nulos. O teste
HTTPS/SQL em `tests/security/auth_security_smoke.py` usa dois usuários e verifica
titularidade, normalização do username, edição parcial, preferências privadas e
ausência de dados privados nas respostas públicas.

Depois da implementação de avatar em 10/11/2026, passaram os 44 testes xUnit
do projeto e o teste integrado HTTPS/SQL. A migration `AdicionarAvatares`
cria a tabela com exclusão em cascata quando o usuário é excluído.

A validação inclui PNG/JPEG reais, limites, redução proporcional, conteúdo
inválido, imagem truncada, GIF/APNG, metadados, acesso autenticado, titularidade,
substituição, remoção repetida e uploads concorrentes. Avaliações públicas e
relações sociais ainda não foram implementadas e não estão homologadas.
