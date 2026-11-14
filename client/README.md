# Somnia — frontend

Frontend Angular 22, standalone, com Reactive Forms, signals e SCSS. Esta entrega
implementa **autenticação, guards e tela inicial**, seguindo as cores carvão e âmbar da
referência visual. O projeto é uma demo/TCC da Academia do Programador.

## Executar

Requer Node.js 24.15 ou superior na linha 24, npm e a API configurada por HTTPS.
Na raiz do repositório, inicie a API conforme a configuração local existente:

```powershell
dotnet run --project server/src/Api --launch-profile https
```

Em outro terminal:

```powershell
cd client
npm ci
npm start
```

Abra **https://localhost:4200**. Antes de iniciar o Angular, `npm start` prepara
o certificado HTTPS de desenvolvimento do .NET e configura a confiança local.
O Windows pode pedir confirmação na primeira instalação do certificado.
O Angular usa esse certificado em `.certs/localhost.pem` e sua chave local;
`.certs` não é versionado. É necessário ter o SDK .NET instalado.
Use `localhost` no endereço para corresponder ao certificado.

`src/app/environments/environment.ts` conecta o Angular diretamente
a `https://localhost:7201/api`, endereço HTTPS do perfil local da API. O CORS
da API já permite `https://localhost:4200`. Confie no certificado HTTPS local
do .NET com `dotnet dev-certs https --trust`; os cookies `__Host-` exigem HTTPS.
Não coloque segredos do backend no frontend.

`environment.production.ts` usa `/api` para uma hospedagem na mesma origem. Testes de
navegador também usam `/api`; o servidor de teste encaminha essa rota para a
API isolada em `https://localhost:7301`.

O build gera `dist/somnia/browser`. Uma hospedagem futura precisa encaminhar
`/api` para a API na mesma origem e resolver as rotas do Angular com `index.html`.
Este PR não publica a aplicação nem configura hospedagem.

## Rotas e comportamento

| Rota                      | Acesso           | Comportamento                                                          |
| ------------------------- | ---------------- | ---------------------------------------------------------------------- |
| `/entrar`                 | `guestGuard`     | Login por e-mail/senha; autenticado segue para `/inicio`               |
| `/cadastro`               | `guestGuard`     | Nome, username, e-mail, senha e confirmação; retorna ao login após 201 |
| `/inicio`                 | `authGuard`      | Feed, descoberta, busca musical e resumo da conta                      |
| `/conta`                  | `authGuard`      | Consulta da conta e logout global                                      |
| `/` e rotas desconhecidas | redirecionamento | Passam por `/inicio` e suas guards                                     |

O grupo privado usa `canActivate` e `canActivateChild`. Novas páginas privadas
devem entrar nesse grupo. As guards aguardam restauração antes de renderizar a
página; são controle de navegação. A autorização continua sendo imposta pela API
em cada chamada, inclusive para tokens revogados.

## Tela inicial

A composição segue `somnia-layout.jpg`: navegação lateral, fundo carvão,
detalhes em âmbar, avaliações no centro e resumo da conta à direita. Em telas
pequenas, a navegação fica em um menu expansível e o resumo vem depois do feed.

- Seguindo consulta `/api/feed`; Descobrir consulta `/api/discover`.
  As páginas têm dez avaliações, com ação para carregar mais. Duplicatas entre
  páginas são descartadas pelo ID; respostas de abas anteriores são canceladas.
- Títulos, artistas, capas e links são consultados no catálogo, com no máximo
  três consultas de metadados simultâneas, cache de dois minutos limitado a
  cem itens por instância da página e pausa após 429/503. A referência completa
  `(provider, type, externalId)` identifica cada item. O cache guarda somente
  campos usados no card, não as faixas do álbum, e é descartado ao sair da tela.
- Falha no catálogo mantém autor, data, nota e resenha visíveis. Sem capa,
  mostra-se um ícone; sem nota, o card indica “Sem nota”. Zero é uma nota válida.
  Resenhas são texto simples, com expansão de textos longos. A nota mostrada é
  do autor; não é média comunitária.
- O resumo consulta `/api/me`, relações e total de avaliações do titular.
  Falhas aparecem como informação indisponível, sem inventar contagens zero.
  O avatar do titular é carregado com Bearer como blob; o endereço temporário
  é revogado ao sair. Autores dos cards usam iniciais, pois o feed não inclui avatar.
- A busca consulta álbuns ou músicas com paginação e links oficiais Spotify.
  Não há formulário para publicar avaliações, detalhes de item nem edição de
  perfil nesta etapa. “Minha conta” abre a tela existente em `/conta`.
- Há estados de carregamento, vazio, falha e nova tentativa. Limites 429 no
  feed e na busca respeitam Retry-After, com fallback de 60 segundos.

Capturas em `docs/home-desktop.png` e `docs/home-mobile.png` usam respostas
controladas dos testes; esses participantes e avaliações não são dados de produção.

## Sessão

- Access token somente em memória. Nenhuma credencial em localStorage,
  sessionStorage ou URL. O refresh permanece exclusivamente no cookie HttpOnly.
- POST de auth envia `X-Somnia-Client: web`; refresh/logout enviam o cookie CSRF
  atual em `X-CSRF-Token`. Logout também envia Bearer e revoga todas as sessões.
- Guards e chamadas protegidas renovam a sessão quando falta token em memória
  ou faltam menos de 30 segundos para ele expirar. Não há polling em segundo plano.
- Uma Promise compartilha a renovação entre chamadas concorrentes. Web Locks
  serializa login/refresh/logout entre abas em navegadores compatíveis por HTTPS;
  o cookie CSRF é lido depois de obter o lock. Sem Web Locks, a serialização de
  refresh se limita à aba. A demo foi verificada no Microsoft Edge.
- BroadcastChannel informa login/logout a outras abas, limpando o estado de uma
  conta anterior. Dados e tokens não são enviados pelo canal.
- Falha de refresh limpa a sessão e pede login, sem repetição automática naquela
  instância da página. O servidor pode ter consumido o refresh antes de uma
  falha de rede. `401` protegido também encerra o estado local, sem replay de POST.
- Falha de logout não é apresentada como revogação bem-sucedida; a tela permite
  tentar novamente. `429` em login/cadastro bloqueia o envio por `Retry-After`.
- O interceptor só acrescenta Bearer às rotas relativas `/api/...` permitidas.
  Chamadas auth usam um HttpClient separado para evitar recursão de refresh.

## Verificar

```powershell
npm run build
npm test -- --watch=false
npm run format:check
npx playwright install chromium
npm run test:e2e
```

Os testes unitários cobrem guards com roteador real, renovação concorrente,
expiração, CSRF, falhas sem retry, logout e restrição do interceptor. Os testes
Playwright usam respostas controladas exclusivamente em `e2e`, cobrindo cadastro,
login, recarga, navegação direta, sessão expirada, erros, teclado, 360 px e abas.
O workflow `frontend.yml` executa build, formatação, unitários e navegador.

Para usar o Edge já instalado no Windows:

```powershell
$env:PLAYWRIGHT_CHANNEL = 'msedge'
npm run test:e2e
```

Integração opcional com API real e banco **descartável** (não roda no CI):

```powershell
# Na raiz, compile a API; depois entre em client.
dotnet build server/Somnia.slnx -c Release
cd client
$env:PLAYWRIGHT_CHANNEL = 'msedge'
npm run test:e2e:api
```

Requer Python, Docker com `notekeeper-sqlserver-1`, credenciais de desenvolvimento
do SQL já configuradas e certificado HTTPS do .NET. O runner usa 7301/4300, gera
uma chave JWT e banco `SomniaFrontendAuth_<uuid>`, executa o navegador, encerra a
API e remove somente esse banco no `finally`. Não utiliza o banco SomniaDb.
As portas de teste precisam estar livres; a API/devserver do usuário em
7201/4200 podem continuar ativos. Interromper à força o processo pode exigir
limpeza manual do banco temporário identificado pelo prefixo.

Capturas ficam em `test-results`, ignorado pelo Git. Traces e HAR estão
desabilitados para evitar registrar tokens das respostas de autenticação.
