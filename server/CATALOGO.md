# Catálogo musical: primeira etapa

Implementação dos contratos de RF03/RF04, baseada na seção 5 da
`Somnia_Especificacao_Tecnica_API_MVP_v1.0.pdf`. A API usa
`IMusicCatalogProvider`; a implementação atual tem dados fictícios e não
consulta Spotify. A integração real e RF05 (link de escuta) continuam pendentes.

## Configuração

`MusicCatalog:Provider` aceita:

| Valor | Comportamento |
| --- | --- |
| `Simulated` | Dados fictícios, permitido somente em Development |
| `Disabled` | As três consultas retornam `503` para requisições válidas |

`appsettings.Development.json` usa `Simulated`; o arquivo base usa `Disabled`.
Sem configuração, o padrão é `Disabled`. Um provedor desconhecido ou a
tentativa de ativar `Simulated` fora de Development impede a inicialização.
Variáveis como `MusicCatalog__Provider` podem sobrescrever a configuração.

As respostas incluem `provider: "simulated"` em cada item e IDs com prefixo
`somnia-demo-`. Esses IDs não são IDs do Spotify. Os exemplos não inventam
capas ou links: `coverUrl` e `spotifyUrl` são nulos. Não use os dados simulados
para validar a viabilidade de acesso ao Spotify ou homologar RF05/RF11.

## Endpoints

Todas as consultas exigem JWT Bearer, retornam `Cache-Control: no-store` e
compartilham limite de 60 chamadas/minuto por usuário e instância da API.
Chamadas inválidas também contam. Excesso retorna `429` com `Retry-After`.

| Método | Endereço | Resultado |
| --- | --- | --- |
| GET | `/api/catalog/search?q=Somnia&type=album&limit=10&offset=0` | Álbuns paginados |
| GET | `/api/catalog/search?q=Somnia&type=track&limit=10&offset=0` | Faixas paginadas |
| GET | `/api/catalog/albums/somnia-demo-album-aurora` | Álbum Aurora e suas faixas em ordem |
| GET | `/api/catalog/tracks/somnia-demo-track-primeira-luz` | Faixa Primeira Luz e resumo do álbum |

Busca:

- `q` é obrigatório, com 1–200 caracteres após remover espaços nas pontas.
- `type` é obrigatório: `album` ou `track`, um tipo por busca. Aceita variação
  de maiúsculas/minúsculas e espaços nas pontas; a resposta usa minúsculas.
- `limit`: padrão 10, de 1 a 10. `offset`: padrão 0, de 0 a 1000.
- O simulado procura parte do título ou do nome do artista, sem distinguir
  maiúsculas/minúsculas. Não implementa busca aproximada nem operadores Spotify.
- Os resultados são ordenados por título e ID, filtrados por tipo antes da
  paginação. A ordenação da integração real dependerá do provedor.
- A página contém `items`, `limit`, `offset`, `total` e `hasMore`. Busca sem
  correspondência retorna `200`, lista vazia e total zero. Offset além do
  resultado retorna lista vazia, preservando o total e `hasMore: false`.

Cada item contém `provider`, `type`, `externalId`, `title`, `artists[]`,
`coverUrl` e `spotifyUrl`. O álbum acrescenta `tracks[]`, cada faixa com
`trackNumber`. O detalhe de uma faixa acrescenta `album` e `trackNumber`.
O resumo do álbum em uma faixa não repete toda a lista de faixas.

O conjunto simulado tem dois álbuns, Aurora e Noturno, com três faixas cada,
do artista fictício Somnia Demo. Use `q=Somnia` para percorrer todas as páginas.

Nos detalhes, IDs aceitam de 1 a 128 caracteres ASCII alfanuméricos, hífen
ou sublinhado. Formato inválido retorna `400`; item ausente retorna `404`.
Consultar ID de faixa na rota de álbuns, ou o contrário, também retorna `404`.
As identidades precisam preservar a combinação `(provider, type, externalId)`.

## Indisponibilidade e próximo provedor

O caso de uso converte `CatalogoIndisponivelException` em `503` com o contrato
de [ERROS.md](ERROS.md), sem expor detalhes internos. Desativar o catálogo
não altera contas ou perfis nem suas rotas. O catálogo atual é somente leitura,
sem novas tabelas/migrations, arquivos de mídia ou uso de RabbitMQ.

Para implementar o adaptador Spotify, ainda será necessário:

1. Criar/validar o aplicativo no Developer Dashboard e confirmar acesso real
   à busca, detalhes de álbum, faixas do álbum e detalhe de faixa.
2. Configurar Client ID/Secret no servidor e implementar Client Credentials,
   sem exigir uma conta Spotify dos participantes do Somnia.
3. Implementar cancelamento, timeout, tratamento de 401/403/429 e Retry-After,
   renovação controlada do token e limites de chamadas, sem loops de retry.
4. Mapear metadados ausentes, atribuição, capa e link oficial; preservar a
   paginação das faixas de álbuns e tratar indisponibilidade por mercado.
5. Testar o adaptador com respostas HTTP simuladas e depois com o aplicativo
   real. A consulta à documentação não confirma a elegibilidade do aplicativo.

O limite local de 10 itens considera a [referência de busca do Spotify](https://developer.spotify.com/documentation/web-api/reference/search),
que documenta offset até 1000. O padrão local continua 10, conforme o PDF;
não é uma cópia de todos os padrões/opções do Spotify. O fluxo futuro de
autenticação do provedor será [Client Credentials](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow).

## Verificação e CI

```powershell
dotnet build server/Somnia.slnx -c Release
dotnet test server/tests/Somnia.Tests -c Release
python server/tests/security/auth_security_smoke.py
```

A suíte possui 92 testes unitários. Os cenários de catálogo cobrem validação
antes do acesso ao provedor, paginação, filtro de tipo, ordenação, vínculo
álbum/faixa, ausência de resultado, cancelamento, indisponibilidade e bloqueio
do simulado em produção.

Na validação desta etapa, os 92 testes e o teste integrado HTTPS/SQL/RabbitMQ
passaram, com API e Worker em processos locais. O build Release da solução
também passou, sem avisos ou erros.

O teste integrado usa API real com HTTPS/SQL, autentica dois usuários e
verifica as três rotas, `400`/`401`/`404`/`429`/`503`, separação de tipos,
paginação e acesso ao perfil durante indisponibilidade do catálogo. Continua
usando os recursos temporários descritos em [MENSAGERIA.md](MENSAGERIA.md).

O workflow `.github/workflows/ci.yml` restaura, compila a solução e executa
os testes unitários no GitHub Actions, sem precisar de SQL, RabbitMQ ou
credenciais Spotify. O teste integrado HTTPS/banco/broker permanece local;
não há deploy nesse workflow. A execução remota depende de enviar os commits
ao GitHub e não foi iniciada por esta alteração local.
