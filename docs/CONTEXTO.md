# Somnia — contexto e próximos passos

Atualizado em **10/11/2026**. Este documento registra o estado da implementação,
as decisões da conversa e o ponto de retomada. Atualize-o ao concluir cada
etapa para não confundir requisitos planejados com código já entregue.

## Ponto de retomada

**Autenticação, perfis, processamento assíncrono de avatar e catálogo real
do Spotify estão implementados. O usuário testou o catálogo no Swagger e
confirmou que está funcionando. O próximo trabalho é implementar avaliações,
médias e histórico de avaliações no perfil, conforme o PRD v1.2.**

O diário privado foi retirado do escopo. Não criar entidade de escuta,
datas de escuta ou tela de diário. A última alteração antes deste registro
foi documental: revisão do PRD, commit `600cc7a`.

| Área | Estado |
| --- | --- |
| Cadastro, login, JWT, refresh e logout | Implementados e testados |
| Perfil próprio, consulta de outro perfil e edição | Implementados e testados |
| Avatar estático | Implementado com processamento assíncrono por RabbitMQ |
| Catálogo Spotify real | Implementado, validado automaticamente e testado pelo usuário no Swagger |
| Avaliações de álbuns e faixas | Ainda não implementadas |
| Médias e quantidade de notas por item | Ainda não implementadas |
| Histórico de avaliações no perfil | Escopo aprovado; ainda não implementado |
| Seguir, feed e descoberta | Ainda não implementados |
| Frontend integrado e deploy | Pendentes nesta entrega do backend |
| Premium | Planejado no PRD, fora da entrega do MVP gratuito |

## Como trabalhar neste projeto

- Comunicar em português e prosseguir com o trabalho autorizado.
- Fazer commits pequenos por alteração coerente, usando o padrão do usuário:
  `add: descrição em português` e `fix: descrição em português`.
- Branch encontrada nesta atualização: `v3/catalogo`. Conferir a branch e o
  `git status` antes de continuar, pois podem mudar.
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

**Diferença entre escopo e implementação existente:** o perfil atual ainda
possui `defaultScoreVisibility` e `defaultReviewVisibility`, com valores
`private`/`public`. Isso foi implementado antes da revisão do PRD. Esses
campos não devem determinar o comportamento das novas avaliações. Sua
adequação no contrato, banco, testes e documentação é uma pendência explícita.

O PRD mantém alguns detalhes como propostas operacionais. Antes de fechar
os contratos de avaliações, revisar a possibilidade de resenha sem nota,
o limite de texto e o tratamento HTTP de uma segunda criação para o mesmo
item. Não inventar essas definições como decisões já aprovadas.

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
  de nome, bio e preferências implementadas.
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

## Validações já realizadas

- Build Release da solução passou, sem avisos ou erros.
- **120 testes unitários passaram**, incluindo o adaptador Spotify com HTTP
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

## Próximos passos em ordem

### 1. Fechar os contratos de avaliações com o PRD v1.2

- Ler as seções 02, 03, 04, 08 e 09 do PRD antes de implementar.
- Conferir detalhes ainda propostos: resenha sem nota, limite da resenha,
  precisão/apresentação da média e resposta para criação duplicada.
- Definir os contratos de criação, edição, exclusão, consulta por item e
  histórico paginado por perfil. Esses endpoints ainda não existem.
- Definir a adequação de `defaultScoreVisibility`/`defaultReviewVisibility`
  no perfil e nos dados existentes para refletir a remoção dessa preferência.
- Manter os exemplos do Swagger e a documentação coerentes com o contrato final.

### 2. Implementar domínio, persistência e migration

- Criar o módulo de avaliações nas camadas existentes.
- Identificar item por `(provider, type, externalId)`, com titular e datas
  de criação/edição. Não criar entidade de escuta.
- Garantir uma avaliação por titular/item, inclusive sob concorrência, com
  restrição de unicidade no banco.
- Validar nota inteira de 0 a 100; distinguir zero de ausência de nota e
  impedir avaliação completamente vazia conforme o contrato final.
- Definir validação da referência externa sem permitir identidade arbitrária
  ou chamar o Spotify desnecessariamente nas consultas de avaliações salvas.

### 3. Implementar operações e histórico no perfil

- Criar, editar e excluir avaliação com titular obtido do JWT, sem confiar
  em um ID de autor enviado no corpo.
- Exigir autenticação nas consultas e autoria nas alterações.
- Listar avaliações por perfil, com paginação, criação decrescente e desempate
  estável; editar não deve criar uma nova entrada.
- Consultar avaliações por álbum/faixa e preservar acesso aos registros já
  salvos quando o Spotify estiver indisponível.
- Reutilizar ProblemDetails e as convenções HTTP do projeto.

### 4. Calcular médias e contagens

- Usar somente notas atuais de avaliações não excluídas, sem peso por
  repetição, quantidade de escutas ou recursos Premium.
- Atualizar corretamente após criação, edição, remoção de nota e exclusão.
- Tratar ausência de notas separadamente de média zero.
- Escolher a forma de cálculo conforme o volume e a consistência necessários.
  Não foi definida fila/consumer de avaliações; a implementação síncrona
  no banco é o ponto de partida recomendado.

### 5. Validar e documentar a entrega

- Cobrir notas 0/100, limites inválidos, duplicidade concorrente, exclusão,
  paginação e independência entre avaliações de álbum e faixa.
- Verificar que outro usuário não edita/exclui avaliação alheia e que
  visitantes sem JWT não consultam o conteúdo.
- Verificar média/contagem após todas as alterações e comportamento quando
  o catálogo externo estiver indisponível.
- Rodar build e testes adequados; atualizar o smoke para os novos contratos.
- Testar pelo Swagger e atualizar PRD/documentos quando necessário.
- Fazer commits pequenos no padrão do usuário e atualizar este contexto.

### Depois das avaliações

Seguir/deixar de seguir, feed e descoberta; depois integração do frontend,
homologação do piloto e preparação do deploy. Exclusão de conta, retenção,
convites e processo de migrations em produção continuam pendências próprias.
Não reintroduzir diário nem antecipar Premium sem nova decisão do usuário.

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
de **2 GB livres**; verificar novamente antes de builds ou downloads grandes.

As últimas validações do Spotify usaram API e Worker locais; não foi feito
novo build Linux nessa etapa. Não apagar volumes de banco nem executar
limpeza global do Docker para repetir testes. O procedimento específico de
recuperação e o builder isolado estão em Mensageria.

O projeto ainda não está integralmente homologado como MVP. O catálogo foi
concluído nesta etapa; o ponto de continuidade é **avaliações no perfil**.
