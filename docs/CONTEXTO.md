# Somnia — contexto e próximos passos

Atualizado em **11/11/2026**. Este documento registra o estado da implementação,
as decisões da conversa e o ponto de retomada. Atualize-o ao concluir cada
etapa para não confundir requisitos planejados com código já entregue.

## Ponto de retomada

**Autenticação, perfis, avatar assíncrono, catálogo Spotify, avaliações,
médias, histórico, relações, feed e descoberta estão implementados no backend.
Próxima etapa: frontend integrado e homologação dos fluxos do MVP.**

O diário privado permanece fora do escopo. Não criar entidade de escuta,
datas de escuta ou tela de diário. Premium permanece posterior ao piloto.

| Área | Estado |
| --- | --- |
| Cadastro, login, JWT, refresh e logout | Implementados e testados |
| Perfil e avatar assíncrono | Implementados e testados |
| Catálogo Spotify real | Implementado e validado anteriormente com Spotify real e no Swagger |
| Avaliações de álbuns/faixas | CRUD autenticado e autoria implementados |
| Médias e quantidade de notas | Calculadas no SQL a partir das notas atuais |
| Histórico no perfil | Implementado, paginado e com desempate estável |
| Seguir/deixar de seguir, contagens e listas | Implementados |
| Feed e descoberta | Implementados, autenticados e paginados |
| Frontend integrado e deploy | Pendentes |
| Premium | Fora da entrega do MVP gratuito |

## Como trabalhar neste projeto

- Comunicar em português e prosseguir com o trabalho autorizado.
- Entregar mudanças por pull requests para revisão do usuário, em branches
  próprias, sem fazer merge automaticamente. Commits são a base técnica dos
  PRs; usar `add: descrição em português` e `fix: descrição em português`.
- Base desta entrega: `origin/feat/avaliacoes`, onde os PRs #1, #2 e #3 já
  foram integrados pelo usuário. Nova branch: `feat/feed-descoberta`; abrir PR
  para `feat/avaliacoes`, preservando as entregas revisadas.
  Conferir branch e `git status` ao retomar.
- Seguir a organização das quatro camadas já existentes. O Worker é um host
  separado; não substitui essa separação.
- Não colocar Client Secret, token Spotify, JWT ou senhas reais novas na
  documentação, no frontend ou nos commits. As credenciais Spotify locais
  já foram configuradas pelo usuário nos User Secrets.
- O usuário pediu referência no Notekeeper e, depois, no Gerador de
  Certificados para fila/consumer. A implementação entregue segue o Gerador
  de Certificados, incluindo o padrão das connection strings locais.

## Documentos e fonte do escopo

| Documento | Papel |
| --- | --- |
| [PRD v1.2](PRD.md) | Transcrição integral versionada do escopo atual do produto |
| [README](../README.md) | Visão geral, estado do backend e execução local |
| [Autenticação](../server/AUTENTICACAO.md) | Sessões, JWT, cookies, CSRF e testes |
| [Perfis](../server/PERFIS.md) | Contratos implementados de perfil e avatar |
| [Mensageria](../server/MENSAGERIA.md) | Outbox, consumer, Docker, recuperação e limites |
| [Cadastro](../server/CADASTRO.md) | Política de acesso e regras do piloto |
| [Erros](../server/ERROS.md) | Contrato HTTP ProblemDetails |
| [Catálogo](../server/CATALOGO.md) | Spotify, configuração, limites e verificação |
| [Avaliações](../server/AVALIACOES.md) | CRUD, histórico, média e compatibilidade |
| [Relações](../server/RELACOES.md) | Seguir perfis, listas, contagens e regras |
| [Comunidade](../server/COMUNIDADE.md) | Feed e descoberta: contratos, ordenação e atualização |

Arquivos externos usados como referência:

- `C:/Users/kauan/Downloads/somnia.docx`: PRD atualizado para **v1.2** em
  10/11/2026, preservando tabelas e estrutura do Word.
- `C:/Users/kauan/Downloads/somnia.v1.1.antes-avaliacoes-no-perfil.docx`:
  backup da versão anterior à mudança de escopo.
- `C:/Users/kauan/Downloads/PRD_Diario_Musical_MVP.docx`: PRD anterior;
  não foi atualizado nesta revisão.
- `C:/Users/kauan/Downloads/Somnia_Especificacao_Tecnica_API_MVP_v1.0.pdf`:
  especificação técnica original, que permanece sem alteração.

**Os IDs RF/CA do PRD e do PDF técnico têm significados diferentes.** Não
misturar, por exemplo, RF10 do PRD com RF10 da API. Nas regras alteradas pela
decisão recente, usar o PRD v1.2; referências antigas a diário ou privacidade
por campo não devem orientar a nova implementação de avaliações.

## Decisões de produto que precisam ser preservadas

1. Avaliações pertencem ao usuário e referenciam um álbum ou uma faixa.
2. Existe uma avaliação atual por usuário e item. Álbum e faixa são itens
   independentes; a nota de uma faixa não calcula automaticamente a do álbum.
3. Notas são inteiras de **0 a 100**. Zero é válido e diferente de ausência
   de nota. Resenha é opcional.
4. Avaliações compõem uma lista no perfil, começando pelas mais recentes,
   com paginação. Edição atualiza o registro e não cria outra avaliação.
5. Perfis e avaliações são consultáveis pelos **participantes autenticados**.
   Não há acesso anônimo nem indexação pública aprovados nesta etapa.
6. Apenas o autor pode editar ou excluir sua avaliação. Seguir alguém não
   concede permissão adicional de alteração.
7. Nota e resenha seguem o mesmo acesso do perfil. Não implementar avaliação
   privada nem visibilidade independente de nota/texto no MVP revisado.
8. Médias consideram as notas atuais das avaliações não excluídas, com uma
   contribuição por autor/item. Resenha sem nota não aumenta a contagem de notas.
9. Exclusão retira a avaliação do perfil e dos resultados derivados; edição
   da nota ou exclusão precisa atualizar média e quantidade de notas.
10. Não há diário privado, registro de escuta, repetição por audição ou data
    em que o usuário ouviu o item. Datas da avaliação são criação/edição.
11. Premium permanece posterior ao piloto. Estatísticas e retrospectivas
    futuras devem usar avaliações, sem depender do diário removido.

**Adequação concluída:** `defaultScoreVisibility` e
`defaultReviewVisibility` foram removidos de DTOs, PATCH, entidade e banco
pela migration `AdicionarAvaliacoes`. Campos antigos no PATCH agora
retornam `400`; não havia avaliações anteriores para migrar.

Escolhas operacionais desta entrega: resenha sem nota, limite de 5.000
caracteres, segunda criação com `409` e média com uma casa decimal.
Foram adotadas a partir das propostas do PRD e comunicadas como ponto de
partida; não eram decisões previamente confirmadas no v1.2.
Detalhes em [Avaliações](../server/AVALIACOES.md).

## O que foi entregue

### Estrutura e autenticação

- Backend .NET 10, ASP.NET Core, EF Core/Identity, MediatR, FluentResults e xUnit.
- Solução: `server/Somnia.slnx`.
- Camadas em `server/src`: `Api`, `Aplicacao`, `Dominio` e `Infraestrutura`.
- Host de processamento em `server/src/Worker`.
- Banco atual é **SQL Server**, embora o PDF proponha PostgreSQL.
- Rotas atuais usam **`/api`**, embora o PDF proponha `/api/v1`.
- Cadastro, login, refresh rotativo, revogação de sessões e logout global.
- Refresh em cookie protegido e validações de origem/cabeçalho/CSRF conforme
  a operação; access token JWT usado nas rotas protegidas.
- Username de 3 a 30 caracteres ASCII permitidos; nome de exibição separado.
- E-mail de confirmação não é exigido no piloto. Não há envio de confirmação.
- ProblemDetails padronizado, com `traceId` e proteção contra exposição de
  mensagens internas. Erros de validação são associados aos campos.

### Perfis e avatar

- Consulta própria (`GET /api/me`), consulta por username e edição parcial
  de nome e bio implementadas.
- Nome até 256 caracteres, bio até 300; conteúdo tratado como texto simples.
- Perfil de outro participante e sua imagem exigem JWT.
- Upload multipart em `PUT /api/me/avatar`, remoção e consulta de status.
- A API retorna `202`, persiste o original privado e o outbox no SQL; o
  processamento acontece no Worker via MassTransit 8.5.10/RabbitMQ.
- Fila durável `somnia-avatars-solicitados`. Mensagem contém somente o ID
  da solicitação; os bytes da imagem não trafegam na mensagem.
- Arquivo até 2 MiB; conteúdo real PNG/JPEG estático, até 2048 × 2048 pixels.
  O Worker rejeita GIF, SVG, APNG e conteúdo inválido.
- SkiaSharp regrava PNG de até 512 × 512, preservando proporção e removendo
  metadados/conteúdo anexado. O avatar normalizado fica no SQL.
- Até 100 solicitações pendentes globais e dez uploads/minuto por usuário
  e instância; consumer com prefetch 1 e concorrência 1 por instância.
- Duplicidade, atraso, novo upload e remoção são tratados para impedir que
  mensagens antigas sobrescrevam ou recriem um avatar.
- Outbox permite recuperação após indisponibilidade do broker. Solicitações
  possuem limites de tentativa e retenção/limpeza descritos em Mensageria.
- Worker Linux já foi validado em contêiner com uma CPU, 512 MiB, usuário
  sem privilégios e sistema de arquivos somente leitura.

### Catálogo real do Spotify

- Interface de domínio `IMusicCatalogProvider` e casos de uso independentes
  da implementação do provedor.
- Busca por álbum/faixa, detalhes de álbum com paginação completa das faixas
  e detalhes de faixa com resumo do álbum.
- Client Credentials no servidor; não exige login Spotify do participante.
- Token somente em memória, renovação antecipada e uma obtenção concorrente.
  Recusa `401` permite uma renovação/repetição, sem loop.
- Capas, artistas e links oficiais do Spotify nos DTOs; não há streaming.
- Busca: `q` de 1 a 200 caracteres, `limit` de 1 a 10 e `offset` de 0 a 1000.
- Limite local de 60 consultas/minuto por usuário e instância da API.
- Até quatro chamadas externas simultâneas por instância; timeout padrão
  de dez segundos. Detalhe de álbum tem prazo total de 30 segundos e máximo
  de 1000 faixas, com páginas de até 50.
- Hosts fixos, redirects desabilitados e nenhuma navegação por URLs `next`
  recebidas do provedor. JSON limitado a 2 MiB após descompressão.
- Resposta externa inválida retorna `502`; quota, credenciais recusadas,
  transporte e indisponibilidade externa retornam `503`, com pausa e
  `Retry-After` quando aplicável. Isso não encerra a sessão Somnia.
- Development usa `MusicCatalog:Provider=Spotify`; a configuração base mantém
  `Disabled`. `Simulated` continua disponível somente em Development.
- Mercado padrão `BR`. Credenciais do usuário já configuradas e validadas.
- Não há cache persistente de metadados nem coordenação de quota entre instâncias.
- RabbitMQ está implementado para avatar. Não há fila de avaliações nem
  decisão aprovada de torná-las assíncronas.

Arquivos centrais do catálogo:

- [Contrato de domínio](../server/src/Dominio/Modulos/Catalogo/IMusicCatalogProvider.cs).
- [Registro dos provedores](../server/src/Infraestrutura/Modulos/Catalogo/CatalogoExtensions.cs).
- [Cliente HTTP/token](../server/src/Infraestrutura/Modulos/Catalogo/Spotify/SpotifyClient.cs).
- [Adaptador Spotify](../server/src/Infraestrutura/Modulos/Catalogo/Spotify/ProvedorCatalogoSpotify.cs).
- [Controller](../server/src/Api/Modulos/Catalogo/CatalogoController.cs).

## Ambiente local e como retomar os testes

Repositório no computador atual:
`C:/Users/kauan/Documents/Academia-Do-Programador-FullStack/Trabalhos/Somnia`.

API e Worker usam **`UserSecretsId=Somnia`**. Não executar `user-secrets init`
novamente. Nomes das configurações relevantes, sem seus valores secretos:

- `ConnectionStrings:SqlServer` e `ConnectionStrings:RabbitMq`.
- `Jwt:SigningKey`, `Jwt:Issuer` e `Jwt:Audience` na API.
- `Spotify:ClientId` e `Spotify:ClientSecret` na API.
- `Spotify:Market` e `Spotify:TimeoutSeconds`.

Os arquivos Development da API e do Worker já têm conexão SQL local com
`SomniaDb` e RabbitMQ no padrão solicitado pelo usuário, porta **5673**.
User Secrets e variáveis de ambiente podem sobrescrever os arquivos.
O Compose alternativo usa porta **5672** e outras credenciais: não misturar
as duas configurações. Consultar Mensageria antes de trocar o broker.

Com SQL e RabbitMQ disponíveis, na raiz do repositório:

```powershell
dotnet run --project server/src/Api --launch-profile https
```

Em outro terminal:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project server/src/Worker
```

A API aplica migrations automaticamente em Development. O Worker não aplica
migrations. Em produção, a execução controlada de migrations ainda precisa
ser configurada.

Swagger: **https://localhost:7201/swagger**. Cadastre ou autentique uma conta
Somnia usando `X-Somnia-Client: web` e autorize o JWT retornado pelo login.
Consultas do catálogo usam parâmetros na URL e **não recebem corpo JSON**:

```http
GET /api/catalog/search?q=Radiohead&type=album&limit=10&offset=0
GET /api/catalog/search?q=Radiohead&type=track&limit=10&offset=0
GET /api/catalog/albums/{externalId}
GET /api/catalog/tracks/{externalId}
```

Copiar IDs reais dos resultados; não confundir ID de álbum com ID de faixa.
Foi criada uma [coleção Postman](../server/tests/postman/catalogo.postman_collection.json),
mas o usuário preferiu testar no Swagger e confirmou sucesso.

## Validações anteriores à entrega de avaliações

- Build Release da solução passou, sem avisos ou erros.
- **120 testes unitários passaram na etapa anterior**, incluindo o adaptador Spotify com HTTP
  simulado, sem depender de credenciais ou rede.
- Smoke completo HTTPS/SQL/RabbitMQ passou: autenticação, perfis, erros,
  catálogo e processamento assíncrono de avatar, incluindo recuperação e
  mensagens duplicadas/atrasadas.
- O smoke com Spotify real passou pelas rotas autenticadas: busca de álbuns
  e faixas, álbum com faixas e detalhe de faixa.
- O usuário também testou as funções no Swagger e disse que está tudo certo.
- A revisão de PRD foi conferida quanto à integridade do DOCX, preservação
  de tabelas/células/seções e manutenção dos IDs RF/CA.
- CI em `.github/workflows/ci.yml` executa restore, build e testes unitários,
  sem SQL/RabbitMQ/Spotify. Não foi iniciada execução remota nesta conversa.

Comandos de validação, para usar quando a próxima alteração justificar:

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

O smoke depende do ambiente local: usa o contêiner SQL
`notekeeper-sqlserver-1` e a conexão de `appsettings.Development.json`, não
a conexão SQL dos User Secrets. Cria banco e broker temporários e os remove
ao terminar. Para também consultar o Spotify real:

```powershell
$env:SOMNIA_TEST_SPOTIFY_LIVE = '1'
python server/tests/security/auth_security_smoke.py
Remove-Item Env:SOMNIA_TEST_SPOTIFY_LIVE
```

A execução real consome quota do aplicativo. A validação anterior confirma
o acesso que existia naquele momento, sem garantir quotas futuras.

## Entrega de avaliações em 11/11/2026

- Módulo `Avaliacoes` nas quatro camadas, handlers MediatR e repositório EF.
- `POST /api/reviews`, `GET/PUT/DELETE /api/reviews/{id}`.
- `GET /api/users/{username}/reviews`: histórico por criação/ID decrescentes;
  edição preserva criação e posição; limit 1–50, offset 0–10.000.
- `GET /api/catalog/{provider}/{type}/{externalId}/reviews`: página e resumo
  com média/quantidade e `myReview` do titular, independente da página.
  Sem notas: `average: null`, `count: 0`.
- Titular obtido do JWT; consultas autenticadas; alteração alheia dá `404`.
- Unicidade no SQL Server por titular/provedor/tipo/ID, inclusive sob concorrência.
  Collation binária mantém IDs externos sensíveis a maiúsculas.
- Nota 0–100 ou ausente; resenha até 5.000 caracteres; conteúdo totalmente vazio
  rejeitado. PUT substitui os dois campos; exclusão física.
- Só POST valida referência contra o catálogo ativo; GET/PUT/DELETE independem
  do Spotify. Banco mantém a referência do item, sem copiar metadados/capas.
- Média calculada em SQL, sem fila, cache ou projeção materializada.
- Migration `AdicionarAvaliacoes` cria tabela/constraints/índices e remove
  as duas preferências antigas de visibilidade.
- Build Release sem avisos/erros e 154 testes unitários passaram.
- Smoke ampliado com `tests/security/avaliacoes_smoke.py`: CRUD HTTP/SQL,
  autorização, concorrência, paginação, médias e catálogo desligado.
  A suíte completa HTTPS/SQL/RabbitMQ passou, incluindo regressões de avatar,
  outbox, reinício de Worker, mensagens duplicadas e limites de requisição.

## Entrega de relações em 11/11/2026

- Módulo `Relacoes` nas quatro camadas, handlers MediatR e repositório EF.
- `PUT/DELETE /api/users/{username}/follow`, sem corpo e com titular pelo JWT.
- `GET /api/users/{username}/connections`: `isFollowing`, `followersCount`,
  `followingCount`; estado personalizado ao visitante autenticado.
- `GET /api/users/{username}/followers` e `following`: páginas com dados de
  apresentação do perfil e data do vínculo, sem IDs internos/e-mail/credenciais.
- PUT/DELETE idempotentes, ambos com `204`. Vínculos são direcionais,
  sem aprovação. Auto-seguimento retorna `400`; alvo inexistente `404`.
- Chave primária composta protege contra duplicidade concorrente; constraint
  SQL impede auto-seguimento. Sem fila ou dependência do Spotify/RabbitMQ.
- Listas por criação/outro usuário decrescentes, limit 1–50, offset 0–10.000.
- Migration `AdicionarRelacoesSeguir` cria tabela e índices sem alterar contas
  ou avaliações. FKs sem cascata: futura exclusão de conta deve remover relações
  nas duas direções antes de excluir o usuário.
- Build Release sem avisos/erros e 170 testes unitários aprovados.
- Smoke completo HTTPS/SQL/RabbitMQ passou, incluindo relações com três perfis,
  oito PUTs e oito DELETEs concorrentes, persistência após reinício, contagens,
  paginação e regressões de avaliações, avatar, outbox e mensageria.
- Escolhas operacionais: idempotência, proibição de auto-seguimento, limites
  e ordenação das listas, documentadas em [Relações](../server/RELACOES.md).

## Entrega de feed e descoberta em 11/11/2026

- `GET /api/feed`: avaliações atuais das pessoas seguidas pelo titular do JWT.
  Inclui avaliações anteriores ao vínculo; não inclui as próprias.
- `GET /api/discover`: avaliações de toda a comunidade, incluindo o titular,
  pessoas seguidas e não seguidas. Sem ranking ou recursos Premium.
- Ambos exigem JWT, usam `no-store`, `PaginaAvaliacoesDto`, limit 1–50 e
  offset 0–10.000. Ordenação por criação/ID decrescentes; edição não muda posição.
- Reutilizam o repositório e a paginação de avaliações. Feed filtra por EXISTS
  nas relações. Sem tabela de publicações, filas, cache ou cópia de avaliações.
- Alterações/exclusões e seguir/deixar de seguir refletem na próxima consulta.
  Projeção usa nome atual do autor; sem dados de segurança da conta.
- Sem dependência de Spotify/RabbitMQ. Referências externas continuam acessíveis
  mesmo com catálogo desligado; metadados não são copiados pelo feed.
- Migration `IndexarDescobertaAvaliacoes` adiciona índice global decrescente
  em `(CreatedAt, Id)`; não altera tabelas/dados existentes além do índice.
- Build Release sem avisos/erros; 176 testes unitários passaram e EF confirmou
  modelo alinhado às migrations.
- Smoke de comunidade passou com três participantes, seleção pelo JWT,
  paginação/desempate, edição/exclusão, alterações de vínculos, nome atualizado,
  resenha sem nota e catálogo desligado. A suíte completa HTTPS/SQL/RabbitMQ
  também passou, incluindo regressões de avaliações, relações, avatar e mensageria.
- Contratos e escolhas operacionais em [Comunidade](../server/COMUNIDADE.md).
- Revisão de segurança solicitada pelo usuário: sem bypass de autenticação ou
  exposição de campos privados identificado no diff. Acrescentada política
  `community`, com 60 consultas/minuto por usuário/instância compartilhadas
  entre feed e descoberta. Testes HTTP adicionais para JWTs adulterados,
  expirados/revogados, vinculação de sessão, parâmetros inválidos e throttling.
  NuGet não reportou vulnerabilidades conhecidas nesta consulta. Limites do
  escopo e evidências em [Revisão de segurança](../server/REVISAO_SEGURANCA_COMUNIDADE.md).
  Build, 176 testes unitários e smoke completo com os novos casos passaram.

## Próximos passos em ordem

### Atualização de frontend — 12/11/2026

Autenticação Angular concluída no PR #5. A tela `/inicio` agora integra feed,
descoberta, paginação, busca de álbuns/músicas e resumo da conta, seguindo a
referência carvão/âmbar. `/conta` preserva a consulta do titular. Metadados são
obtidos separadamente; falha do catálogo não esconde avaliações. Detalhes de
item, publicação/edição de avaliações, perfil e relações no frontend continuam
como próximas entregas. Contrato e execução em [client/README.md](../client/README.md).

1. Concluir frontend: detalhes de álbum/faixa, publicação/edição de avaliação,
   histórico, perfis e relações. Preservar dados digitados nos erros e mostrar
   carregamento/vazios/indisponibilidade. Informar visibilidade antes de salvar
   e diferenciar nota pessoal, média e quantidade.
2. Homologar fluxos completos em 360 px/desktop e por teclado, foco/rótulos/erros,
   e critérios de aceite do PRD. O backend entregue não homologa a experiência visual.
3. Preparar deploy/piloto: migrations controladas, backups, retenção, exclusão
   de conta (incluindo relações), convites/acesso, viabilidade do catálogo e feedback.

Não reintroduzir diário nem antecipar Premium. O MVP completo ainda depende
de frontend, homologação e preparação do piloto.

## Histórico de commits relevantes

| Commit | Entrega |
| --- | --- |
| `d5a4949` | Processamento assíncrono de avatar com RabbitMQ e consumer |
| `0f839b9` | Teste integrado do fluxo assíncrono e recuperação |
| `aea60fb` | Worker/RabbitMQ em Docker com limites de recursos |
| `44b564e` | Padronização de ProblemDetails e traceId |
| `60dd4b4` | Regras de cadastro e acesso aos perfis no piloto |
| `533dee8` | Validação do Worker Linux e contratos no smoke |
| `3874fbe` | Imagem validada do Worker e recuperação do Docker |
| `5349c88` / `afc0a96` | Connection strings e documentação no padrão solicitado |
| `a11061d` / `432c7d9` | Catálogo inicial simulado e verificação HTTP |
| `e31bc38` | Workflow de build/testes no GitHub Actions |
| `c0e8931` | Integração real Spotify com Client Credentials e testes |
| `5a3c77e` | Smoke com Spotify real pelas rotas do Somnia |
| `30f65ca` | Documentação da integração Spotify |
| `16782cc` | Coleção Postman para testes manuais |
| `600cc7a` | PRD v1.2: avaliações no perfil e retirada do diário privado |

## Observações de ambiente para a próxima sessão

Houve falta de espaço no disco C: e falhas de cache/disco virtual do Docker
durante a validação anterior. O ambiente foi recuperado e a imagem
`somnia-worker:dev` foi mantida. Na leitura desta atualização, C: tinha cerca
de **2 GB livres** na sessão anterior (3,4 GB em 11/11 antes dos testes); verificar novamente antes de builds ou downloads grandes.

As últimas validações do Spotify usaram API e Worker locais; não foi feito
novo build Linux nessa etapa. Não apagar volumes de banco nem executar
limpeza global do Docker para repetir testes. O procedimento específico de
recuperação e o builder isolado estão em Mensageria.

O projeto ainda não está integralmente homologado. Avaliações, histórico,
médias, relações, feed e descoberta foram implementados no backend. O frontend
integra autenticação, início, busca e consultas da comunidade; a continuidade
é **detalhes musicais, avaliações, perfis, relações e homologação**.
