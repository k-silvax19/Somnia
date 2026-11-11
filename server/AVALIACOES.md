# Avaliações

Implementação de avaliações de álbuns/faixas, histórico no perfil e médias,
conforme o PRD v1.2. Todas as rotas exigem JWT Bearer e usam `Cache-Control:
no-store`. Escritas exigem HTTPS, `X-Somnia-Client: web` e origem permitida,
seguindo os contratos de autenticação e de ProblemDetails existentes.

## Regras adotadas nesta implementação

- Nota opcional, inteira de 0 a 100. Zero é válido; `null` representa ausência.
- Resenha opcional com até 5.000 caracteres antes de normalizar os espaços
  externos. Texto em branco vira `null`. Nota e resenha não podem estar ambas vazias.
- Resenha sem nota é permitida; não entra na média nem na contagem de notas.
- Uma avaliação atual por usuário + provedor + tipo + ID externo, garantida
  por índice único no SQL Server. Criação duplicada retorna `409`, inclusive
  sob concorrência; não edita silenciosamente o conteúdo anterior.
- Edição substitui nota e resenha no registro existente. Preserva `createdAt`
  e atualiza `updatedAt`. Não gera outra avaliação ou publicação.
- Nota e resenha são visíveis aos participantes autenticados. Somente o autor
  pode editar/excluir. Tentativas de alterar avaliação alheia retornam `404`,
  assim como IDs inexistentes. O titular sempre vem do JWT.
- Álbum e faixa são independentes. IDs externos são sensíveis a maiúsculas
  no banco; nenhuma fusão automática de remasters/edições por nome/artista.

Resenha sem nota, limite de 5.000 caracteres, resposta `409` e arredondamento
da média são escolhas operacionais desta entrega a partir das propostas do
PRD; não eram decisões previamente confirmadas no documento v1.2.

## Endpoints

| Método | Rota | Resultado |
| --- | --- | --- |
| POST | `/api/reviews` | Cria (`201`), com `Location` para a consulta |
| GET | `/api/reviews/{id}` | Consulta uma avaliação (`200`/`404`) |
| PUT | `/api/reviews/{id}` | Substitui nota/resenha do titular (`200`) |
| DELETE | `/api/reviews/{id}` | Exclui a avaliação do titular (`204`) |
| GET | `/api/users/{username}/reviews` | Histórico paginado do perfil |
| GET | `/api/catalog/{provider}/{type}/{externalId}/reviews` | Avaliações e resumo do item |

No Swagger, autentique uma conta Somnia, autorize o Bearer e use IDs vindos
do catálogo. Exemplo de criação (substitua o ID):

```json
{
  "provider": "spotify",
  "type": "album",
  "externalId": "ID_REAL_DO_ALBUM",
  "score": 90,
  "review": "Uma ótima descoberta."
}
```

`provider` aceita `spotify` e, em Development com catálogo simulado,
`simulated`. `type` aceita `album` ou `track`. A criação consulta o catálogo
ativo e confere provedor, tipo e ID retornados; não aceita identidade
arbitrária ou dados de catálogo enviados pelo cliente. Compartilha o limite
de 60 chamadas/minuto por usuário/instância com as consultas do catálogo.
Item inexistente retorna `404`; falhas externas seguem `502`/`503` e
`Retry-After` do catálogo. Duplicata já salva retorna `409` antes da consulta externa.

Exemplo de edição completa com `PUT /api/reviews/{id}`:

```json
{
  "score": 85,
  "review": null
}
```

No PUT, campo omitido ou nulo remove seu conteúdo. Enviar apenas `review`
remove a nota; enviar apenas `score` remove a resenha. Corpo vazio retorna
`400`. Campos extras, incluindo autor, item e visibilidade, são rejeitados.
Notas fracionárias ou enviadas como string também retornam `400`.
Corpos de escrita têm limite de 64 KiB. Resenha é texto simples; a interface
deve escapar o texto ao exibir, sem interpretá-lo como HTML.

## Consultas, histórico e média

Cada avaliação retorna `id`, `author` (`username`, `displayName`), `item`
(`provider`, `type`, `externalId`), `score`, `review`, `createdAt` e
`updatedAt`. E-mail, credenciais e sessões não são incluídos.

O histórico usa `limit` (padrão 20, de 1 a 50) e `offset` (padrão 0, de 0
a 10.000), retornando `items`, `limit`, `offset`, `total` e `hasMore`.
Ordenação por criação decrescente e ID decrescente como desempate estável.
Username tem a mesma normalização do Identity; perfil inexistente retorna
`404`. Edições preservam a posição. Paginação por offset pode mudar entre
requisições se outras avaliações forem criadas ou excluídas.

A consulta por item retorna `reviews` (a página acima), `myReview` (a avaliação
do titular do JWT ou `null`, independente da página) e `summary`:

```json
{
  "average": 95.0,
  "count": 2
}
```

O número JSON pode ser serializado sem o zero decimal; o frontend deve
formatá-lo com uma casa decimal. Arredondamento de metades para longe de
zero. Sem notas: `average: null`, `count: 0`; média zero permanece `0`.
A página pode conter resenhas sem nota mesmo quando `count` é zero.
Item sem avaliações retorna página vazia e resumo vazio, sem consultar sua
existência no Spotify. Isso também permite consultar itens retirados do catálogo.

Média e quantidade são calculadas no SQL a partir das notas atuais em cada
consulta. Não há fila, projeção materializada ou cache de médias para sincronizar.
Após uma escrita concluída, a próxima consulta reflete o estado salvo. Edições
simultâneas usam a última gravação; resumo, total e página são consultas
separadas e podem refletir instantes diferentes durante escritas concorrentes.

O banco mantém somente a referência externa do item, sem copiar títulos,
capas ou catálogo. Consultar avaliações, editar e excluir não chama Spotify;
os dados próprios continuam disponíveis durante falhas externas. A interface
pode enriquecer a apresentação pelo catálogo quando ele estiver disponível.

## Banco e compatibilidade

A migration `AdicionarAvaliacoes` cria tabela, constraints de nota/conteúdo,
chave estrangeira para usuário e índices de unicidade, perfil e item.
A exclusão é física; médias e histórico deixam de incluir o registro.

A mesma migration remove `DefaultScoreVisibility` e `DefaultReviewVisibility`
de `AspNetUsers`, conforme a retirada da privacidade por campo do PRD v1.2.
Essas preferências antigas não publicam nem migram avaliações, pois não havia
avaliações implementadas antes desta entrega. O contrato de cadastro e de
`GET /api/me` deixa de retorná-las e o PATCH de perfil passa a rejeitá-las com
`400`. Nome, bio, avatar e dados de autenticação são preservados. O rollback
recria as preferências com o padrão antigo, sem recuperar seus valores anteriores.

## Verificação

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

Testes unitários cobrem validação, limites, referência externa, duplicidade,
falhas do provedor e comandos com titularidade. O smoke chama
`avaliacoes_smoke.py` no banco temporário existente e cobre HTTP real,
autenticação, duas contas, concorrência, média, exclusão, desempate estável,
paginação, remoção das preferências e catálogo indisponível. Não usa Spotify
real para testar avaliações. Pré-requisitos e limpeza estão em
[AUTENTICACAO.md](AUTENTICACAO.md).

Em 11/11/2026, build Release sem avisos/erros, 154 testes unitários e smoke
completo HTTPS/SQL/RabbitMQ passaram. O modelo EF não tem alterações pendentes
em relação à migration versionada.

Seguir perfis foi implementado em [RELACOES.md](RELACOES.md).
Feed, descoberta, frontend e deploy continuam pendentes.
