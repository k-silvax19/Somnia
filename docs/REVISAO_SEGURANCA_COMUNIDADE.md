# Revisão de segurança — feed e descoberta

Revisão local de 11/11/2026, durante a revisão do PR #4. Referência inicial:
`5b8427aecf055fa11d7af382004e830e9d3d6602...b90b1e19fae7fd7ddb31a6fa77d5fdc6a08c69a3`.
Inclui inspeção das duas rotas e dos controles existentes de autenticação,
sessões, revogação, projeção de dados e erros dos quais elas dependem.
Não representa certificação, pentest independente ou garantia de ausência de falhas.

## Standards

Nenhuma violação de segurança identificada no diff original em relação às
regras documentadas do repositório. As rotas exigem autenticação e `no-store`;
o feed usa `sub` validado pelo middleware. LINQ usa parâmetros e a projeção
seleciona somente os campos de avaliação, referência do item e apresentação
do autor. Paginação é validada antes de executar as consultas.

Limitação identificada: não havia limite de frequência nas novas rotas.
Cada leitura executa contagem e página; limitar somente tamanho/offset não
limita quantas vezes uma conta pode causar esse trabalho. Isso é uma lacuna
de proteção contra abuso, sem demonstração de esgotamento do servidor ou
medição que justifique classificar uma negação de serviço comprovada.

## Spec

Nenhum desvio de segurança identificado em relação a RF08/RF09 e CA04/CA09/CA10
do PRD v1.2. Feed filtra as relações atuais do titular; descoberta compartilha
avaliações entre participantes autenticados, conforme o escopo. Ambas consultam
registros atuais e não mantêm cópias que sobrevivam à exclusão.

Resenha permanece texto em JSON. Não existe renderização HTML nestas rotas;
escape e segurança da futura interface ainda precisam ser homologados.

## Proteção adicionada ao PR

Política `community`: 60 consultas por minuto por usuário e instância da API,
compartilhadas entre `/api/feed` e `/api/discover`, sem fila. Excesso retorna
`429` com ProblemDetails e `Retry-After`. Contas diferentes têm cotas separadas;
essa cota não bloqueia perfil nem consome a cota própria do catálogo.

A política segue a implementação existente de rate limiting. O contador fica
em memória, reinicia com a instância e não é coordenado entre réplicas. Não
substitui proteção global na infraestrutura, limites de conexões ou teste de
carga. Como autenticação ocorre antes do limiter, validações de sessão/revogação
ainda podem consultar o banco em requisições posteriormente limitadas.

## Evidências

- Antes da proteção, o teste de 62 consultas alternadas entre feed e descoberta
  falhou porque a API continuou respondendo `200` após o limite esperado.
- O teste adicional usa somente banco temporário e chave de assinatura aleatória
  do smoke; nunca usa credenciais ou chave JWT real do usuário para forjar tokens.
- Casos verificados por HTTP: token malformado, payload adulterado, algoritmo
  `none`, algoritmo não permitido, token expirado, issuer/audience incorretos,
  sessão de outro usuário, sessão inexistente, JTI revogado e sessão
  revogada/expirada. A resposta esperada é `401` nas duas rotas.
- Entradas semelhantes a SQL em `limit`/`offset` retornam `400` na validação.
  Isso complementa a inspeção da parametrização, sem equivaler a um pentest completo.
- Teste da cota: primeiras 60 consultas aceitas, seguintes rejeitadas; outra
  conta e a rota de perfil permanecem acessíveis. O helper confere `Retry-After`
  e `no-store` dos erros.
- Build Release e 176 testes unitários passaram após a alteração.
- `dotnet list server/Somnia.slnx package --vulnerable --include-transitive --format json`
  consultou NuGet e não reportou pacotes vulneráveis nesta execução. Isso não
  cobre falhas desconhecidas, runtime/OS, configuração de produção ou o frontend.

O smoke completo passou após a alteração, incluindo os novos casos, avaliações,
relações, avatar, outbox e RabbitMQ. Arquivo dos novos casos:
`tests/security/comunidade_security_smoke.py`, chamado pelo smoke principal.

## Limites e decisões de produto

O cadastro não tem convites nem confirmação obrigatória de e-mail nesta fase.
Portanto, qualquer conta que consiga se cadastrar e autenticar poderá consultar
a descoberta; a comunidade não está limitada automaticamente aos amigos do
proponente. Isso segue o escopo atual e as pendências de [CADASTRO.md](CADASTRO.md).

Hospedagem, proxy/TLS, segredos, acesso ao SQL/RabbitMQ, controle do piloto,
backups e testes de carga precisam de avaliação própria antes do deploy.
Não foram auditados nesta revisão. Outros endpoints antigos sem throttling
também não recebem proteção automaticamente por esta política.

Resultado da revisão original: **Standards: 0 violações de segurança
identificadas; Spec: 0 desvios de segurança identificados.** Lacuna de
proteção contra abuso tratada separadamente; risco residual permanece.
