# Processamento assíncrono de avatar

A implementação segue o Gerador de Certificados: MassTransit 8.5.10,
RabbitMQ, mensagem contendo o ID da solicitação, consumer e consulta de
status. A conversão roda em `Somnia.Worker`, em processo separado da API.
Reviews continuam fora desse fluxo e ainda não foram implementadas.

## Fluxo e contrato HTTP

1. `PUT /api/me/avatar` recebe multipart com `file`, JWT e `X-Somnia-Client`.
2. A API verifica autenticação, origem, tamanho e limite de admissão. Salva
   o arquivo privado e a mensagem na mesma transação SQL, usando Bus Outbox.
3. Retorna `202 Accepted` com `{ "id": "UUID", "status": "pending", "erro": null }`
   e `Location: /api/me/avatar/requests/{id}`. Isso confirma recebimento,
   não aprovação do conteúdo como imagem.
4. A entrega do outbox envia `ProcessarAvatarMessage` à fila durável
   `somnia-avatars-solicitados`. A mensagem contém somente `solicitacaoId`.
5. O consumer lê o original privado, valida o conteúdo, converte e substitui
   o avatar somente se a solicitação ainda for a atual daquele usuário.

`GET /api/me/avatar/requests/{id}` exige JWT do titular. Outro usuário recebe
`404`. A resposta contém ID, status e erro de validação, sem o arquivo ou
metadados privados. Status possíveis:

| Status | Significado |
| --- | --- |
| `pending` | Aguardando entrega ou sendo processado |
| `completed` | Imagem aprovada e publicada |
| `rejected` | Conteúdo inválido; avatar anterior preservado |
| `cancelled` | Substituída por novo upload ou cancelada por remoção |
| `failed` | Limite de tentativas, prazo ou falha de processamento |

O frontend consulta o status até um estado final e, em `completed`, recarrega
o perfil e a imagem. Recomenda-se consultar a cada 1–2 segundos, interromper
ao sair da tela e tratar falhas de rede. Falha do consumer ou do broker não
deve produzir ciclos de reenvio automático do arquivo.

## Consistência e limites

- O avatar anterior continua disponível até a conclusão válida do novo.
- Novo upload cancela solicitações pendentes anteriores do titular.
- `DELETE /api/me/avatar` remove a imagem e cancela pendências imediatamente.
  Mensagens atrasadas não podem recriar o avatar removido.
- Mensagens duplicadas ou fora de ordem são verificadas no banco; solicitações
  já finalizadas/canceladas não voltam a publicar uma imagem.
- A API admite até 100 solicitações pendentes globalmente. A admissão usa
  lock transacional no SQL Server; fila cheia retorna `409` sem cancelar
  a solicitação anterior, pois a transação é revertida.
- Upload: dez chamadas/minuto por usuário e instância da API, sem espera
  em memória; excesso retorna `429`. Requisições inválidas também contam.
- Arquivo de até 2 MiB; envelope HTTP de até 2 MiB + 64 KiB. A validação
  de formato real, dimensões e animação acontece somente no Worker.
- O consumer usa prefetch 1 e concorrência 1 por instância. Cada instância
  adicional aumenta a concorrência total; não há limite global de workers.
- Até três tentativas de iniciar a conversão são contadas no banco, inclusive
  após queda do processo. Erros transitórios também têm três retries curtos
  do MassTransit. São contadores diferentes, não uma promessa de exatamente
  três entregas pelo broker.
- Solicitações pendentes expiram após 24 horas. O Worker executa limpeza
  a cada dez minutos: elimina originais expirados e registros finais com
  mais de 24 horas desde a criação. A consulta então pode retornar `404`.
  Sem Worker ativo, essa limpeza fica atrasada até ele voltar.
- Originais de solicitações concluídas, rejeitadas, canceladas ou marcadas
  como falha são removidos durante a mudança de estado.

O limite de 100 se aplica a solicitações ativas com originais armazenados.
As filas/outbox também podem conter IDs de solicitações canceladas; não há
um teto físico de 100 mensagens no RabbitMQ.

## Execução local

Pré-requisitos: Docker, .NET 10, SQL Server e a configuração HTTPS/JWT do
[README](../README.md). Execute os comandos na raiz do projeto.

O padrão de desenvolvimento segue o Gerador de Certificados. API e Worker
definem `ConnectionStrings:RabbitMq` em seus `appsettings.Development.json`:
`amqp://guest:guest@localhost:5673`. Ambos também têm a conexão SQL local
com `SomniaDb`. Essa configuração espera um broker local na porta 5673,
com o usuário de desenvolvimento `guest`.

O Compose deste repositório é uma alternativa: usa `somnia`, senha definida
no ambiente e porta 5672. Para usar essa alternativa, sobrescreva a conexão
como nos comandos abaixo; o contêiner do Worker já recebe sua própria URI
com o hostname `rabbitmq` pelo Compose.

Defina `RABBITMQ_PASSWORD` no ambiente ou em `server/.env` (ignorado pelo Git).
Use uma senha aleatória hexadecimal, pois ela também será usada na URI.
Conserve a mesma senha ao reutilizar o volume persistente do broker.

```powershell
docker compose -f server/compose.yaml up -d rabbitmq
dotnet user-secrets set "ConnectionStrings:RabbitMq" "rabbitmq://somnia:<senha>@localhost:5672/" --project server/src/Api
dotnet run --project server/src/Api --launch-profile https
```

Depois da API aplicar as migrations em Development, execute o consumer em
outro terminal:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project server/src/Worker
```

API e Worker compartilham o `UserSecretsId` `Somnia`. Em Development, o
Worker lê `ConnectionStrings:SqlServer` e `ConnectionStrings:RabbitMq` do
seu `appsettings.Development.json`; User Secrets e variáveis de ambiente
têm precedência. Em produção, configure as conexões por variáveis de ambiente
ou outro provedor de segredos. O Worker não precisa de chave JWT e não aplica
migrations: a migration `ProcessarAvataresAssincronos` deve existir no banco
antes da inicialização do consumer.

RabbitMQ usa portas locais 5672 (AMQP) e 15672 (painel); usuário `somnia` e
a senha definida no ambiente. As portas publicadas estão presas ao loopback.
Não copie segredos de exemplo para produção.

Para executar o Worker no Docker, configure também `SOMNIA_SQL_CONNECTION`
com uma conexão SQL acessível pelo contêiner (no Docker Desktop, o host é
`host.docker.internal`, não `localhost`):

```powershell
docker compose -f server/compose.yaml --profile worker up -d --build
```

O Compose usa a imagem local `somnia-worker:dev`. Se o cache do builder
padrão estiver danificado, use um builder isolado, sem alterar o builder
padrão ou apagar volumes de banco:

```powershell
docker buildx create --name somnia-validation --driver docker-container --driver-opt memory=2g --bootstrap
docker buildx build --builder somnia-validation --load --tag somnia-worker:dev --file server/src/Worker/Dockerfile server
docker compose -f server/compose.yaml --profile worker up -d --no-build
```

Se `somnia-validation` já existir, reutilize-o e pule o comando de criação.
O builder precisa de espaço para baixar, extrair e compilar o SDK; a imagem
também ocupa espaço no disco virtual do Docker. Não inicie builds grandes
com o disco do Windows próximo de ficar cheio. Depois de terminar os builds,
`docker buildx rm somnia-validation` remove somente esse builder e seu cache.
No Docker Desktop, liberar arquivos no Windows pode precisar ser seguido
por reinício do WSL/Docker para recuperar um disco virtual preso após a falha.
`wsl --shutdown` encerra todas as distribuições em execução: confira-as antes.

O contêiner do Worker usa usuário sem privilégios, disco somente leitura,
`/tmp` temporário, nenhuma capability adicional, uma CPU e 512 MiB de RAM.
O broker também tem limites de CPU/memória e volume persistente. O teste
local do Worker fora do Docker não impõe esses limites de recursos.

## Recuperação e segurança

A API pode aceitar uploads durante indisponibilidade do broker: as mensagens
aguardam no SQL Server e são entregues posteriormente, inclusive após
reinício da API. Reiniciar o Worker não perde a fila durável. O processamento
tolera redelivery; não se presume entrega exatamente uma vez.

Falhas não recuperadas pelo MassTransit vão para
`somnia-avatars-solicitados_error`. Acompanhe essa fila e os estados `failed`.
Se o banco estiver indisponível até o fim dos retries, o status pode ficar
`pending` até expirar; a mensagem fica na fila de erro para investigação.
Reenviar uma mensagem de solicitação já finalizada não a reabre: um novo
upload cria outra solicitação.

O processo separado reduz o impacto de uma falha nativa na API, mas não
elimina vulnerabilidades do decodificador. Em produção, limite também as
permissões SQL e RabbitMQ do Worker, configure TLS e monitore filas, memória,
CPU, reinícios e atraso de processamento. Não habilite logs de parâmetros
sensíveis do EF nem registre arquivos, tokens ou URIs com credenciais.

## Verificação

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

Para executar o mesmo teste com o consumer dentro da imagem Linux:

```powershell
$env:SOMNIA_TEST_WORKER_IMAGE = "somnia-worker:dev"
python server/tests/security/auth_security_smoke.py
Remove-Item Env:SOMNIA_TEST_WORKER_IMAGE
```

Nesse modo, o script inicia e reinicia contêineres temporários do Worker
com uma CPU, 512 MiB de RAM, usuário da imagem sem privilégios, filesystem
somente leitura, `/tmp` temporário, capabilities removidas e
`no-new-privileges`. A conexão usa `host.docker.internal` para acessar o SQL
Server local e o broker descartável pelas portas do Docker Desktop.

O teste integrado usa SQL Server no contêiner `notekeeper-sqlserver-1`, cria
um banco temporário e inicia um RabbitMQ descartável com credenciais
aleatórias e portas livres. Ele inicia API e Worker em processos separados;
não depende de um broker de desenvolvimento existente. Requer acesso à
imagem `rabbitmq:4-management` no Docker e a biblioteca padrão do Python.

Os cenários incluem broker desligado, outbox após reinício da API, entrega
sem consumer ativo, reinício do Worker, aprovação/rejeição de imagens,
APNG, acesso ao status por outro usuário, duplicação, mensagens antigas,
remoção com trabalho pendente, limite de tentativas e rate limiting.

Na revisão de 10/11/2026, a suíte possui 68 testes xUnit. O build Linux foi
destravado com um builder isolado após encontrar um arquivo de configuração
do SDK vazio na imagem local e inconsistências no cache. A falta de espaço
no C: também interrompeu uma compilação; depois de liberar espaço, o WSL
precisou ser reiniciado para soltar o disco virtual do Docker.

Passaram o build Release da solução, os 68 testes e a execução integrada
com `SOMNIA_TEST_WORKER_IMAGE=somnia-worker:dev`. Essa execução validou o
consumer na imagem Linux com uma CPU, 512 MiB, filesystem somente leitura e
as demais restrições do comando de teste, incluindo conversão real com SkiaSharp,
reinícios, mensagens duplicadas/antigas, remoção e recuperação do outbox.
O Compose também passou na validação de configuração.

A validação funcional do contêiner não equivale a um
teste de carga nem comprova ausência de vulnerabilidades no decodificador.
Os testes também verificam o contrato de erros HTTP, limites de username,
duplicidades normalizadas e a política de acesso do piloto, descritos em
[ERROS.md](ERROS.md) e [CADASTRO.md](CADASTRO.md).

Referências de implementação: Gerador de Certificados em
`../GeradorDeCertificados/src/Aplicacao` (pasta irmã do repositório),
[Transactional Outbox do MassTransit](https://masstransit.io/documentation/patterns/transactional-outbox)
e [acknowledgements do RabbitMQ](https://www.rabbitmq.com/docs/confirms).
