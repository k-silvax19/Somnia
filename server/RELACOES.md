# Seguir perfis

Entrega do vínculo social previsto no RF08 do PRD v1.2: seguir/deixar de
seguir sem aprovação, consultar contagens e listar seguidores/seguidos.
Feed e descoberta estão implementados em [COMUNIDADE.md](COMUNIDADE.md).

## Endpoints

Todas as rotas exigem JWT Bearer e retornam `Cache-Control: no-store`.
As escritas exigem HTTPS, origem permitida e `X-Somnia-Client: web`, como
as demais operações autenticadas do projeto. Swagger inclui esse cabeçalho.

| Método | Rota | Resposta |
| --- | --- | --- |
| PUT | `/api/users/{username}/follow` | Segue o perfil (`204`) |
| DELETE | `/api/users/{username}/follow` | Deixa de seguir (`204`) |
| GET | `/api/users/{username}/connections` | Estado e contagens (`200`) |
| GET | `/api/users/{username}/followers` | Seguidores paginados (`200`) |
| GET | `/api/users/{username}/following` | Seguidos paginados (`200`) |

PUT e DELETE não recebem corpo. O usuário que segue é sempre obtido do JWT;
`username` identifica o alvo e usa a normalização do Identity, sem diferenciar
maiúsculas/minúsculas. O contrato de perfil existente permanece inalterado.

## Regras adotadas

- Vínculo único e direcional: A seguir B não significa que B segue A.
- Sem solicitação de aprovação e sem alteração de permissões das avaliações.
- PUT repetido retorna `204` e preserva a data original; requisições concorrentes
  não criam vínculos extras. O SQL Server garante a unicidade do par.
- DELETE repetido retorna `204`, mesmo se o vínculo já não existir.
- Seguir ou deixar de seguir a si mesmo retorna `400`, inclusive usando outro
  casing do próprio username. É uma escolha operacional desta entrega.
- Perfil inexistente retorna `404` em todas as operações; username malformado
  retorna `400`. O contrato exige 3–30 caracteres ASCII permitidos no cadastro.
- Sem JWT: `401`; origem/cabeçalho inválido nas escritas: `403`.
- Datas de vínculo representam criação em UTC, sem ligação com escutas.
- Seguir e deixar de seguir simultaneamente podem terminar em qualquer um dos
  dois estados, conforme a ordem das operações concluídas no banco.

Exemplo de `GET /api/users/bob/connections` para alguém que segue Bob:

```json
{
  "isFollowing": true,
  "followersCount": 1,
  "followingCount": 0
}
```

As contagens pertencem ao perfil consultado. `isFollowing` indica se o
titular do JWT segue esse perfil; no próprio perfil é sempre falso.
O resumo usa uma consulta SQL, sem carregar as listas completas em memória.

## Paginação e privacidade

Listas aceitam `limit` (padrão 20, de 1 a 50) e `offset` (padrão 0, de 0 a
10.000). Resposta: `items`, `limit`, `offset`, `total`, `hasMore`.
Cada item contém somente `username`, `displayName`, `bio`, `avatarUrl` e
`followedAt`. E-mail, IDs internos, credenciais e sessões não são expostos.
Os dados de apresentação refletem o perfil atual, sem duplicar nome/bio/avatar.

Ordenação por criação do vínculo decrescente e ID do outro usuário decrescente
como desempate estável. Repetir PUT não muda a posição. Lista vazia ou offset
além do total retorna `200` com `items: []`. Paginação por offset pode mudar
entre requisições quando vínculos são criados/excluídos. Total e página são
consultas separadas e podem refletir instantes diferentes sob concorrência.

As consultas são disponíveis aos participantes autenticados, incluindo listas
de outros perfis. Não existe consulta anônima. Avatar continua exigindo JWT
na rota de imagem. Nenhuma operação depende de Spotify ou RabbitMQ.

## Persistência

Módulo `Relacoes` nas quatro camadas, com handlers MediatR e repositório EF.
A migration `AdicionarRelacoesSeguir` cria a tabela `RelacoesSeguir` com:

- Chave primária composta `(SeguidorId, SeguidoId)`.
- Constraint que impede auto-seguimento.
- Duas chaves estrangeiras para `AspNetUsers`, sem exclusão em cascata para
  evitar múltiplos caminhos de cascata no SQL Server.
- Índices por usuário/data/outro usuário para ambas as direções de consulta.

Exclusão de conta ainda está fora desta entrega. Quando for implementada,
deverá remover as relações nas duas direções antes de excluir o usuário.
O rollback da migration exclui a tabela e os vínculos já registrados; não
modifica contas nem avaliações. Em Development, a API aplica migrations ao iniciar.

## Testes

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

A suíte unitária tem 176 testes; os casos de relações cobrem validação, identidade,
auto-seguimento, perfil inexistente, titularidade dos comandos e paginação.
O smoke chama `relacoes_smoke.py` e cobre HTTPS/SQL com três perfis, oito PUTs
simultâneos, oito DELETEs simultâneos, vínculos reversos independentes,
contagens, normalização, proteção de origem, paginação/desempate, persistência
após reinício e rejeição de auto-seguimento diretamente no SQL.
Ambiente temporário e limpeza seguem [AUTENTICACAO.md](AUTENTICACAO.md).

Em 11/11/2026, build Release sem avisos/erros, 170 testes unitários e smoke
completo HTTPS/SQL/RabbitMQ passaram. O modelo EF está alinhado à migration.
