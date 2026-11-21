# Curtidas em avaliações

Participantes autenticados podem curtir avaliações de outras pessoas e remover
a própria curtida. Cada pessoa pode curtir uma avaliação uma vez. Curtidas não
alteram a nota nem a média da comunidade.

## Contrato

| Método | Rota | Comportamento |
| --- | --- | --- |
| PUT | `/api/reviews/{id}/like` | Cria a curtida; repetição retorna `204` sem duplicar |
| DELETE | `/api/reviews/{id}/like` | Remove a curtida; repetição retorna `204` |

As duas rotas exigem JWT, origem autorizada, HTTPS e `X-Somnia-Client: web`.
Curtir avaliação inexistente retorna `404`; curtir a própria avaliação retorna
`400`. Uma avaliação excluída também remove suas curtidas.

As respostas de avaliações incluem:

- `likesCount`: número atual de curtidas;
- `likedByMe`: indica se o participante autenticado curtiu a avaliação.

## Ordenação por item

`GET /api/catalog/{provider}/{type}/{externalId}/reviews` aceita `sort=recent`
(padrão) e `sort=popular`. `popular` ordena pela quantidade de curtidas,
decrescente; empates usam data de criação e ID, ambos decrescentes. A página do
item permite alternar entre as avaliações recentes e as mais curtidas.

## Persistência

A tabela `CurtidasAvaliacoes` usa chave primária composta por usuário e
avaliação, protegendo contra duplicatas concorrentes. A exclusão de avaliação
remove suas curtidas em cascata. A FK de usuário não usa cascade; uma futura
exclusão de conta deve remover as curtidas feitas pelo usuário antes de excluir
a conta.
