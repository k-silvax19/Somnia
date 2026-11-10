# Somnia

API de um diário musical com avaliações e recursos sociais. O MVP está em
desenvolvimento: atualmente há autenticação e perfis com avatar estático.

## Conferência da especificação

Revisão realizada em 10/11/2026, comparando código e documentação com os
arquivos em Downloads:

- `Somnia_Especificacao_Tecnica_API_MVP_v1.0.pdf`, de 09/11/2026: referência
  técnica do MVP gratuito, com RF01–RF11 e critérios CA01–CA15 próprios da API.
- `somnia.docx`, PRD v1.1 de 07/11/2026: regras do produto e expansão Premium
  posterior ao piloto. Os IDs de critérios de aceite do PRD e do PDF têm
  significados diferentes e devem ser lidos dentro de cada documento.

| Área | Estado verificado no repositório |
| --- | --- |
| RF01 — Conta e autenticação | Cadastro, login, refresh rotativo, logout global e JWT implementados |
| RF02 — Perfil | Consulta do titular, perfil público, edição de nome/bio/preferências e upload/remoção de avatar estático implementados |
| RF03–RF05/RF11 — Catálogo | Não implementado; integração Spotify pendente |
| RF06/RF10 — Avaliações e média | Não implementado |
| RF07 — Diário privado | Não implementado |
| RF08/RF09 — Relações, feed e descoberta | Não implementado |
| CI/CD e Azure | Sem workflow ou configuração de deploy versionados |
| Premium | Fora do MVP técnico; não implementado |

Os documentos detalhados são [Autenticação](server/AUTENTICACAO.md) e
[Perfis](server/PERFIS.md). Eles descrevem o que existe e suas pendências;
não representam homologação do MVP completo.

## Arquitetura e diferenças da proposta

Backend em ASP.NET Core 10, organizado em `server/src`:

- `Api`: controllers, contratos HTTP, JWT e documentação OpenAPI.
- `Aplicacao`: casos de uso e handlers MediatR.
- `Dominio`: contratos e tipos do domínio, sem dependência das outras camadas.
- `Infraestrutura`: EF Core, SQL Server, Identity e repositórios.

| Tema | Proposta do PDF | Implementação atual |
| --- | --- | --- |
| Banco | PostgreSQL/Npgsql | SQL Server/EF Core, com migrations versionadas |
| Base das rotas | `/api/v1` | `/api` |
| Testes | MSTest e integração HTTP/banco | xUnit e teste integrado Python com SQL Server |
| OpenAPI | Swagger UI com Swashbuckle | OpenAPI nativo do ASP.NET Core e Swagger UI do Swashbuckle |
| Configuração do banco | `ConnectionStrings__Default` | `ConnectionStrings__SqlServer` |
| Perfil público | Acesso anônimo a decidir | Exige autenticação |
| Erros | ProblemDetails com `type`, `title`, `status`, `detail`, `traceId` | Padronização completa ainda pendente |

A seção 16 do PDF identifica stack, URL base e outros detalhes como propostas
técnicas. Esta revisão registra as diferenças encontradas; não confirma
aprovação dessas escolhas nem altera banco, rotas ou framework de testes.
Também continuam pendentes as decisões sobre limites de cadastro, acesso
anônimo, verificação de e-mail, exclusão de conta e retenção de dados.

## Execução local

Pré-requisitos: SDK .NET 10, SQL Server acessível e certificado HTTPS de
desenvolvimento. Execute os comandos na raiz do repositório.

Configure a conexão e a chave JWT via User Secrets, substituindo os valores
de exemplo por valores locais. A chave precisa ter pelo menos 32 bytes.

```powershell
dotnet user-secrets set "ConnectionStrings:SqlServer" "<conexão SQL Server local>" --project server/src/Api
dotnet user-secrets set "Jwt:SigningKey" "<chave aleatória com pelo menos 32 bytes>" --project server/src/Api
dotnet dev-certs https --trust
dotnet restore server/Somnia.slnx
dotnet run --project server/src/Api --launch-profile https
```

`Jwt:Issuer` e `Jwt:Audience` precisam estar configurados; os valores de base
estão em `appsettings.json`. Para um frontend em outra origem, configure
`Cors:AllowedOrigins` com origens HTTPS explícitas conforme o contrato de
autenticação. Não inclua segredos reais em commits.

O perfil local usa `https://localhost:7201` e redireciona o HTTP da porta 5094
para HTTPS. Swagger: `https://localhost:7201/swagger`;
OpenAPI: `https://localhost:7201/openapi/v1.json`. O `v1` desse arquivo
identifica o documento OpenAPI; as rotas dos controllers continuam em `/api`.
Swagger e OpenAPI são expostos somente em Development.

As migrations em `server/src/Infraestrutura/Compartilhado/Orm/Migrations`
são aplicadas automaticamente na inicialização em Development. Produção
precisa de uma etapa controlada de migração, ainda não configurada no repo.

## Verificação

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

Após a implementação de avatar em 10/11/2026, os 44 testes xUnit e o teste
integrado HTTPS/SQL passaram. O projeto de testes compila a API e suas
dependências em Release. O avatar usa SkiaSharp, com bibliotecas nativas
incluídas pelos pacotes para Windows, macOS e Linux; a execução foi validada
localmente no Windows. A migration `AdicionarAvatares` armazena as imagens
normalizadas no SQL Server, sem exigir diretório de uploads ou Blob Storage.
Os resultados cobrem autenticação e perfis existentes; os demais critérios
de aceite continuam pendentes.

O teste integrado usa Python com biblioteca padrão, certificado HTTPS do .NET
e Docker. Atualmente lê a conexão de `appsettings.Development.json` e acessa
o contêiner `notekeeper-sqlserver-1` pelo nome fixo; não usa a conexão dos
User Secrets. Exige esse ambiente local, cria um banco temporário com prefixo
`SomniaAuthSecurity_` e o remove ao terminar. Detalhes dos cenários em
[AUTENTICACAO.md](server/AUTENTICACAO.md).
