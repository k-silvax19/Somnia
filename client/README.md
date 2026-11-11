# Somnia — frontend

Frontend Angular 22, standalone, com Reactive Forms, signals e SCSS. Esta entrega
implementa **somente autenticação e guards**, seguindo as cores carvão e âmbar da
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

Abra **https://localhost:4200**. O servidor Angular usa certificado local
autossinado; permita esse certificado somente no ambiente local ou configure
`ng serve --ssl-cert .certs/localhost.pem --ssl-key .certs/localhost-key.pem`
com um certificado local confiável. `.certs` não é versionado.

`src/app/environments/environment.ts` conecta o Angular diretamente
a `https://localhost:7201/api`, endereço HTTPS do perfil local da API. O CORS
da API já permite `https://localhost:4200`. Confie no certificado HTTPS local
do .NET com `dotnet dev-certs https --trust`; os cookies `__Host-` exigem HTTPS.
Não coloque segredos do backend no frontend.

`environment.ts` usa `/api` para uma hospedagem na mesma origem. Testes de
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
| `/inicio`                 | `authGuard`      | Confirma a conta com `GET /api/me` e permite logout global             |
| `/` e rotas desconhecidas | redirecionamento | Passam por `/inicio` e suas guards                                     |

O grupo privado usa `canActivate` e `canActivateChild`. Novas páginas privadas
devem entrar nesse grupo. As guards aguardam restauração antes de renderizar a
página; são controle de navegação. A autorização continua sendo imposta pela API
em cada chamada, inclusive para tokens revogados.

Não há feed, catálogo, edição de perfil, avaliação nem recuperação de senha no
frontend desta etapa. A tela após login mostra apenas dados reais da conta.

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
