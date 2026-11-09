# Autenticação

Execute a API por HTTPS. O perfil local padrão é `https`; o frontend local deve
usar `https://localhost:4200` ou ser servido na mesma origem da API.

## Contrato com o frontend

As cinco rotas continuam sendo:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET /api/me`

Todas as chamadas POST exigem `X-Somnia-Client: web`. Requisições com `Origin`
precisam corresponder à própria API ou a uma origem explicitamente configurada
em `Cors:AllowedOrigins`. Não configure curingas para essas origens.

No login, envie `email` e `password` em JSON. A resposta contém o `accessToken`,
a expiração e um `csrfToken`. O refresh token é enviado exclusivamente no cookie
`__Host-somnia-refresh`, com `HttpOnly`, `Secure`, `SameSite=Strict`, caminho `/`
e sem `Domain`. Não tente ler ou armazenar esse refresh token no JavaScript.

Use `credentials: 'include'` no fetch (ou `withCredentials: true` no Angular).
Mantenha o access token em memória e envie `Authorization: Bearer ...` nas rotas
protegidas. O cookie `__Host-somnia-csrf` é legível pelo JavaScript da mesma
origem; alternativamente, preserve o `csrfToken` recebido na resposta, por
exemplo em `sessionStorage`. Ele sozinho não autentica nenhuma requisição.

Para renovar, faça POST em `/api/auth/refresh` sem enviar refresh token no corpo.
Envie os cookies e o cabeçalho `X-CSRF-Token` com o valor recebido no login.
A resposta troca os cookies e devolve novo access token e novo csrfToken.
Atualize o estado do frontend com esses valores.

O logout exige o JWT atual, os cookies e `X-CSRF-Token`. Ele revoga todas as
sessões do usuário, inclusive os JWTs emitidos anteriormente, e apaga os cookies.

As origens do frontend e da API precisam ser do mesmo site e usar HTTPS para
os cookies `SameSite=Strict`. Em produção, prefira a mesma origem ou subdomínios
HTTPS do mesmo domínio. Um frontend em domínio independente exige rever esse
contrato antes do deploy; habilitar CORS sozinho não altera o comportamento
SameSite do navegador.

O Swagger de desenvolvimento já inclui os cabeçalhos necessários. Em produção,
o Swagger fica desabilitado.

## Sessões e reutilização

JWTs duram no máximo 15 minutos. O refresh token dura no máximo sete dias, limitado
ao prazo absoluto de 30 dias da sessão original. Renovar não estende esse prazo.
Somente hashes dos refresh tokens são persistidos.

Cada JWT inclui o identificador de sessão. Toda autenticação consulta se essa
sessão continua ativa. Reutilizar um refresh token consumido revoga a sessão
inteira, incluindo os novos tokens emitidos a partir dela; sessões independentes
são preservadas. O logout, por sua vez, encerra todas as sessões do usuário.

O frontend deve manter uma única renovação em andamento e compartilhar seu
resultado entre as requisições que aguardam. Não repita automaticamente um POST
de refresh após falha de rede: o servidor pode já ter consumido o token.
Em `401`, limpe o estado local e solicite login novamente. Em `403`, verifique
origem, HTTPS e cabeçalhos CSRF; não faça ciclos de renovação.

Login permite dez chamadas por minuto por IP, cadastro cinco e refresh trinta.
Esses limites são por instância da API. `429` exige aguardar antes de tentar
novamente.

A migration `ProtegerSessoesAutenticacao` invalida os refresh tokens anteriores:
é necessário entrar novamente depois da atualização. Não guarde segredos de
assinatura em código nem registre cookies, JWTs ou cabeçalhos CSRF nos logs.

## Verificação

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

O teste de segurança usa somente a biblioteca padrão do Python, requer o SQL
Server Docker configurado no desenvolvimento e um certificado HTTPS de
desenvolvimento do .NET. Ele cria um banco temporário com prefixo
`SomniaAuthSecurity_` e o remove ao terminar. A validação do certificado é
dispensada somente pelo cliente de teste que acessa o processo isolado em
loopback; a API continua exigindo HTTPS.

Os cenários incluem cookies seguros, CSRF forjado, origem não permitida, roubo e
reutilização de refresh token, concorrência, revogação de JWTs, logout global,
expiração absoluta, armazenamento por hash e limites de requisições.
