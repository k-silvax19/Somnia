# Catálogo musical e Spotify

A API implementa busca e detalhes de RF03/RF04 e fornece links oficiais de
escuta de RF05 pelo `IMusicCatalogProvider`. O adaptador Spotify usa Client
Credentials no servidor; participantes do Somnia continuam autenticados por
JWT e não precisam conectar uma conta Spotify. Avaliações e seu histórico
no perfil ainda não estão implementados. O diário privado foi retirado do
escopo no [PRD v1.2](../docs/PRD.md).

## Configuração

Crie um aplicativo no [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
Em Settings, copie o Client ID e consulte View client secret. Na raiz do
repositório, configure os valores somente nos User Secrets:

```powershell
dotnet user-secrets set "Spotify:ClientId" "<Client ID>" --project server/src/Api
dotnet user-secrets set "Spotify:ClientSecret" "<Client Secret>" --project server/src/Api
dotnet run --project server/src/Api --launch-profile https
```

O projeto já usa `UserSecretsId=Somnia`; não execute `user-secrets init`.
Reinicie a API após alterar os segredos. Não coloque valores reais em
appsettings, commits ou no frontend. O fluxo não usa redirect URI nem
permissões de usuário: [Client Credentials](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow).

| `MusicCatalog:Provider` | Comportamento |
| --- | --- |
| `Spotify` | API real, com credenciais obrigatórias e validadas na inicialização |
| `Simulated` | Dois álbuns e seis faixas fictícias, somente em Development |
| `Disabled` | Consultas válidas retornam `503` |

Development usa `Spotify`; o arquivo base mantém `Disabled`. Para produção,
configure explicitamente `MusicCatalog__Provider=Spotify`, `Spotify__ClientId`
e `Spotify__ClientSecret` no ambiente de hospedagem. User Secrets são para
desenvolvimento. Provedor desconhecido ou simulado fora de Development
impede a inicialização. `Spotify:Market` usa `BR` e aceita duas letras;
`Spotify:TimeoutSeconds` usa 10 e aceita valores de 1 a 30.

## Endpoints

As rotas exigem JWT Bearer, retornam `Cache-Control: no-store` e compartilham
limite de 60 chamadas/minuto por usuário e instância, incluindo consultas
inválidas. Excesso local retorna `429` com `Retry-After`.

| Método | Endereço | Resultado |
| --- | --- | --- |
| GET | `/api/catalog/search?q=Radiohead&type=album&limit=10&offset=0` | Álbuns paginados |
| GET | `/api/catalog/search?q=Radiohead&type=track&limit=10&offset=0` | Faixas paginadas |
| GET | `/api/catalog/albums/{externalId}` | Álbum com todas as faixas disponíveis, em ordem |
| GET | `/api/catalog/tracks/{externalId}` | Faixa com resumo do álbum |

No Swagger (`https://localhost:7201/swagger`), faça login no Somnia, autorize
o JWT e execute a busca. Copie o `externalId` do resultado para consultar o
detalhe; use um ID de `tracks[]` para consultar uma faixa do álbum.

- `q`: obrigatório, 1–200 caracteres após remover espaços nas pontas.
- `type`: obrigatório, `album` ou `track`, aceitando variação de caixa e espaços.
- `limit`: padrão 10, intervalo 1–10; `offset`: padrão 0, intervalo 0–1000.
- A ordenação e os operadores de busca dependem do Spotify. A consulta é
  codificada como parâmetro de URL, preservando caracteres especiais.
- A página contém `items`, `limit`, `offset`, `total` e `hasMore`. Itens nulos
  do provedor são omitidos, preservando a progressão da paginação original.
- A validação HTTP de IDs aceita 1–128 caracteres ASCII alfanuméricos, hífen
  ou sublinhado; fora disso retorna `400`. No adaptador Spotify, IDs precisam
  ter 22 caracteres alfanuméricos. Outro formato válido no contrato ou item
  ausente retorna `404`; o formato incompatível não gera chamada externa.

Cada item contém `provider`, `type`, `externalId`, `title`, `artists[]`,
`coverUrl` e `spotifyUrl`. Álbum acrescenta `tracks[]` com `trackNumber`;
faixa acrescenta `album` e `trackNumber`. Preserve a identidade
`(provider, type, externalId)`. Resultados reais têm `provider: "spotify"`.
Título ausente usa “Título indisponível”; artistas ausentes viram lista vazia;
capa ausente ou URL sem HTTPS vira `null`. Links oficiais são montados com
tipo e ID validados em `https://open.spotify.com`.

O simulado usa `provider: "simulated"`, IDs `somnia-demo-...`, capas e links
nulos. Use `q=Somnia` para encontrar Aurora e Noturno, com três faixas cada.
Esse modo permite testes locais sem credenciais nem chamadas ao Spotify.

## Limites e falhas externas

O token da aplicação fica apenas em memória, com renovação antecipada e
uma única obtenção concorrente. Uma recusa `401` renova o token e repete a
consulta uma vez. Não há repetição automática para `429`, `403` ou `5xx`.

Há até quatro chamadas HTTP externas simultâneas por instância. O timeout
individual inclui espera por vaga, cabeçalhos e leitura do corpo. O detalhe
de álbum tem prazo total de 30 segundos, percorre páginas de até 50 faixas e
aceita até 1000 faixas; não retorna um álbum parcial se uma página falhar.
A ordem do provedor é preservada, inclusive álbuns com vários discos.

Hosts são fixos, redirects estão desabilitados e URLs `next` do provedor não
são seguidas. Cada JSON tem limite de 2 MiB após descompressão e profundidade
32. Logs dos clientes HTTP foram desabilitados; erros não expõem tokens,
credenciais ou corpos de resposta externos. Capas são URLs de metadados;
o backend não baixa nem processa essas imagens.

| Situação | Resposta do Somnia |
| --- | --- |
| Item inexistente | `404` |
| Consulta recusada com `400` externo | `400`, validação no campo `catalog` |
| JSON/contrato externo inválido ou limite de resposta excedido | `502` |
| Credenciais recusadas, quota, timeout, transporte ou `5xx` externo | `503` |

O `429` externo resulta em uma pausa compartilhada de novas chamadas nessa
instância e `503` com `Retry-After`; aceita segundos ou data HTTP, usando 30
segundos se o cabeçalho não puder ser interpretado. Recusa de credenciais
pausa por 30 segundos; falhas de transporte/servidor, por 5 segundos. Isso
preserva a sessão Somnia e permite continuar usando contas e perfis. O
contrato completo está em [ERROS.md](ERROS.md).

Não há cache persistente de metadados nem coordenação de quota entre
instâncias. O catálogo é somente leitura, sem tabelas novas ou RabbitMQ.
O frontend deve exibir atribuição Spotify e links oficiais junto aos dados,
preservando a arte original conforme a [referência de álbum](https://developer.spotify.com/documentation/web-api/reference/get-an-album).
A integração não oferece streaming de áudio.

Os limites de busca e a paginação de faixas seguem as referências oficiais
[Search](https://developer.spotify.com/documentation/web-api/reference/search)
e [Get Album Tracks](https://developer.spotify.com/documentation/web-api/reference/get-an-albums-tracks).
O tratamento de quota segue [Rate Limits](https://developer.spotify.com/documentation/web-api/concepts/rate-limits).

## Verificação e CI

Para testar manualmente, importe no Postman
[`catalogo.postman_collection.json`](tests/postman/catalogo.postman_collection.json).
Edite `email`, `username` e `password` nas variáveis da coleção para uma conta
local de teste. Execute o cadastro uma vez; depois execute login e consultas
em ordem. O login preenche `accessToken`, a busca preenche `albumId` e o
detalhe do álbum preenche `trackId`. Em execuções seguintes com a mesma conta,
comece pelo login para evitar conflito de cadastro. Os últimos pedidos
validam erros esperados `400` e `401`. O catálogo usa GET sem corpo JSON.
Não compartilhe exportações com tokens ou senhas preenchidos. A API precisa
estar em execução com HTTPS, banco, broker e Spotify configurados conforme
o README; confie no certificado local com `dotnet dev-certs https --trust`.

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

A suíte possui 120 testes unitários, sem acesso à rede nem credenciais reais.
Inclui autenticação Client Credentials, cache/renovação e concorrência do
token, limite de chamadas, timeout/cancelamento, paginação de álbuns,
metadados ausentes, links, respostas inválidas e falhas externas.
Build Release e os 120 testes passaram sem avisos ou erros.

O smoke padrão usa catálogo simulado, API HTTPS, SQL e RabbitMQ reais, com
recursos temporários descritos em [MENSAGERIA.md](MENSAGERIA.md). Para incluir
consultas reais ao Spotify com os User Secrets locais:

```powershell
$env:SOMNIA_TEST_SPOTIFY_LIVE = '1'
python server/tests/security/auth_security_smoke.py
Remove-Item Env:SOMNIA_TEST_SPOTIFY_LIVE
```

Esse modo consome quota do aplicativo e depende de acesso externo. Busca de
álbuns e faixas, detalhe do álbum com faixas e detalhe de faixa foram
validados pelas rotas autenticadas do Somnia com o aplicativo local. O smoke
completo de autenticação, perfis e avatares assíncronos também passou com
API e Worker em processos locais. A validação confirma o acesso atual desse
aplicativo; regras de acesso e quotas podem
mudar. O workflow `.github/workflows/ci.yml` executa build e testes unitários
sem SQL, RabbitMQ ou Spotify; execução remota depende do envio dos commits.
