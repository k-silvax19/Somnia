# Feed e descoberta

Consultas de avaliações para RF08/RF09 e CA10 do PRD v1.2. Não existe uma
entidade separada de publicação: ambas as áreas consultam as avaliações
atuais no SQL Server, usando o mesmo DTO do perfil e das páginas de item.

## Endpoints

| Método | Rota | Conteúdo |
| --- | --- | --- |
| GET | `/api/feed` | Avaliações dos usuários que o titular do JWT segue |
| GET | `/api/discover` | Avaliações de toda a comunidade |

Ambos exigem JWT Bearer e retornam `Cache-Control: no-store`. Visitantes sem
JWT ou apenas com cookie de refresh recebem `401`. As consultas não recebem
corpo e não permitem selecionar o titular do feed por parâmetro: ele vem do JWT.

As duas rotas compartilham cota de 60 consultas/minuto por usuário e instância.
Excesso retorna `429` e `Retry-After`. A cota é local, em memória, sem fila;
não substitui proteção de infraestrutura. Detalhes e testes adicionais em
[Revisão de segurança](REVISAO_SEGURANCA_COMUNIDADE.md).

Exemplos para o Swagger, após autenticar e autorizar o Bearer:

```http
GET /api/feed?limit=20&offset=0
GET /api/discover?limit=20&offset=0
```

O feed inclui avaliações anteriores à criação do vínculo de seguir. Sem
seguir ninguém ou sem avaliações dos seguidos, retorna uma página vazia.
As avaliações próprias ficam na descoberta e no perfil; não entram no feed,
pois seguir o próprio perfil é proibido.

A descoberta inclui participantes seguidos, não seguidos e o próprio titular.
Não há ranking, impulsionamento ou recomendação personalizada nesta entrega.
Essas escolhas de composição e ordenação foram adotadas para o MVP a partir
da distinção entre feed e comunidade definida no PRD.

## Resposta e paginação

Usa `PaginaAvaliacoesDto`: `items`, `limit`, `offset`, `total`, `hasMore`.
`limit` padrão 20, de 1 a 50; `offset` padrão 0, de 0 a 10.000. Valores
inválidos retornam `400` com ProblemDetails e erro associado ao parâmetro.

```json
{
  "items": [],
  "limit": 20,
  "offset": 0,
  "total": 0,
  "hasMore": false
}
```

Cada avaliação contém `id`, `author` (`username`, `displayName`), `item`
(`provider`, `type`, `externalId`), `score`, `review`, `createdAt`, `updatedAt`.
Resenhas sem nota também aparecem. Não são selecionados e-mails, IDs internos
de usuário, credenciais, sessões ou preferências de segurança da conta.
Nome e username refletem o perfil atual; resenha é texto simples e deve ser
exibida com escape no frontend.

Ordenação por criação decrescente e ID da avaliação decrescente para
desempatar. Não é a data da última edição nem a data de uma escuta.
Offset além do total retorna `200` com `items: []`. Total e página são
consultas separadas e podem refletir instantes diferentes sob concorrência.
Novas avaliações/exclusões também podem deslocar páginas entre requisições.

## Atualização do conteúdo

- Criar avaliação: passa a aparecer na descoberta e nos feeds dos seguidores.
- Editar: atualiza a mesma avaliação nas duas consultas, sem novo voto,
  publicação extra ou mudança da posição por edição.
- Excluir: retira o registro do perfil, feed, descoberta e médias.
- Seguir: a próxima consulta inclui as avaliações atuais do perfil seguido.
- Deixar de seguir: a próxima consulta retira esse autor do feed; o conteúdo
  continua disponível na descoberta aos participantes autenticados.
- Alterar displayName: próximas consultas usam o nome atual do autor.

Seguir alguém não permite editar nem excluir suas avaliações.
As regras de autoria continuam nos comandos existentes de avaliações.

## Persistência e dependências

O repositório de avaliações filtra o feed com `EXISTS` sobre `RelacoesSeguir`,
evitando multiplicar avaliações por vínculos. A descoberta consulta todas
as avaliações atuais. Ambos reutilizam a projeção/paginação existente.
Não há fila, consumer, publicação duplicada ou cache para invalidar.

A migration `IndexarDescobertaAvaliacoes` adiciona o índice decrescente
`(CreatedAt, Id)` para a ordenação global. O feed também pode aproveitar o
índice existente por `(UsuarioId, CreatedAt, Id)` e a chave das relações.
Não cria tabelas nem modifica dados de conta, avaliação ou vínculo. O rollback
remove somente o índice. Development aplica migrations ao iniciar a API.

As consultas não chamam Spotify ou RabbitMQ. Continuam funcionando durante
indisponibilidade do catálogo. A resposta mantém a referência externa do item;
metadados e capas podem ser consultados separadamente quando o provedor estiver
disponível, conforme [AVALIACOES.md](AVALIACOES.md).

## Verificação

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

A suíte tem 176 testes unitários. Os novos testes cobrem validação de página,
identidade usada no feed e resultado vazio na descoberta. O smoke chama
`comunidade_smoke.py` com três participantes e cobre autenticação, seleção por
JWT, avaliações próprias/não seguidas, resenha sem nota, paginação/desempate,
edição/exclusão, mudanças de vínculos, nome atual do autor e catálogo desligado.
Ambiente e limpeza seguem [AUTENTICACAO.md](AUTENTICACAO.md).

Em 11/11/2026, build Release sem avisos/erros, 176 testes unitários e smoke
completo HTTPS/SQL/RabbitMQ passaram. EF confirmou modelo alinhado às migrations.

Frontend integrado, homologação visual e deploy permanecem pendentes.
