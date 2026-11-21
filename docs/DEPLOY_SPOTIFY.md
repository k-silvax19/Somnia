# Spotify Web API em deploy — Somnia

Pesquisa realizada em **21/11/2026**. Fontes consultadas: documentação, Developer Terms, Developer Policy, Design Guidelines e anúncios oficiais do Spotify para Developers. Esta nota descreve o fluxo existente do Somnia e separa fatos publicados pelo Spotify de recomendações e inferências para hospedagem.

## O que o Somnia usa

O adaptador atual usa **Client Credentials** no servidor para buscar e consultar álbuns/faixas. O frontend chama a API do Somnia, que retorna títulos, artistas, URLs das capas e links `open.spotify.com`; não há autenticação de usuários Spotify nem streaming. Isso foi confirmado em `docs/CATALOGO.md`, `server/src/Infraestrutura/Modulos/Catalogo/Spotify/SpotifyClient.cs` e nas telas de catálogo. O fluxo Client Credentials é server-to-server, não autoriza dados de usuários, e só serve endpoints que não exigem dados de usuário ([guia Client Credentials](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow), [Authorization](https://developer.spotify.com/documentation/web-api/concepts/authorization)).

## Fatos oficiais do Spotify

### Acesso de desenvolvimento e produção

- Apps novos começam em **Development Mode**. O dono precisa ter Spotify Premium e o modo permite até cinco usuários Spotify autorizados na allowlist ([Quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes)).
- A orientação de fevereiro de 2026 define Development Mode como espaço para aprendizado, experimentação e projetos pessoais não comerciais. O mesmo anúncio diz que Premium e o limite de cinco usuários passam a valer para integrações existentes em 09/03/2026; a restrição de endpoints anunciada para as existentes foi adiada ([anúncio de 06/02/2026](https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security)).
- O anúncio posterior de julho de 2026 elevou para **25 Client IDs por conta de desenvolvedor** e estabeleceu que a quota de Development Mode é compartilhada por conta, não por Client ID. Isso atualiza o anúncio de fevereiro, que dizia um Client ID. Quando quota de desenvolvimento é excedida, a resposta 429 identifica `reason: QUOTA_EXCEEDED` ([anúncio de 23/07/2026](https://developer.spotify.com/blog/2026-07-23-web-api-quota-updates)).
- Extended quota é o modo para apps prontos para um público mais amplo, aceita usuários sem a allowlist de Development Mode e oferece rate limit maior. Desde 15/05/2025, pedidos novos são aceitos somente de organizações, por e-mail corporativo. Os critérios publicados incluem entidade legal estabelecida, serviço ativo e lançado, pelo menos **250 mil MAUs**, presença em mercados-chave, viabilidade comercial e conformidade com os termos ([Quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes)).
- A aprovação de extended quota inclui revisão do app pelo Spotify; a documentação estima que a revisão pode levar até seis semanas. O Spotify não promete aprovação nem prazo de processamento ([Quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes), [Developer Policy, seção VII](https://developer.spotify.com/policy)).
- O changelog de fevereiro de 2026 ainda lista `GET /search`, `GET /albums/{id}`, `GET /tracks/{id}` e `GET /albums/{id}/tracks` como endpoints disponíveis — os mesmos usados pelo Somnia. A página também registra mudanças em campos e parâmetros; confirme o status e o contrato no Dashboard antes de publicar, pois o Spotify pode revisar acesso e limites ([Web API Changelog — fevereiro de 2026](https://developer.spotify.com/documentation/web-api/references/changes/february-2026)).

### Limites e comportamento em 429

- O rate limit da Web API é calculado sobre uma janela móvel de 30 segundos e varia conforme o modo de quota. Pode haver limites específicos por endpoint. A documentação recomenda ler `Retry-After` nos 429, fazer backoff, acompanhar o gráfico de chamadas no Dashboard, usar endpoints em lote quando cabível e carregar recursos sob demanda ([Rate Limits](https://developer.spotify.com/documentation/web-api/concepts/rate-limits)).
- Quotas de Development Mode são distintas dos rate limits. O Spotify agrupa endpoints em buckets de quota e diz que grupos e limites podem mudar; a documentação descreve 429 com `reason: QUOTA_EXCEEDED` quando a quota é excedida ([Quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes), [anúncio de julho de 2026](https://developer.spotify.com/blog/2026-07-23-web-api-quota-updates)).

### Credenciais e autenticação

- Client Credentials envia `grant_type=client_credentials` a `https://accounts.spotify.com/api/token`, autenticando com Client ID e Client Secret, e retorna token Bearer com `expires_in`. É adequado a chamadas server-to-server que não dependem de um usuário Spotify ([Client Credentials Flow](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow)).
- O Spotify classifica Client Credentials como um fluxo típico para backend/daemon, requer Secret guardado no servidor, não acessa recursos de usuário e não tem refresh token; o backend pede outro access token quando necessário ([Authorization](https://developer.spotify.com/documentation/web-api/concepts/authorization)).
- Os Developer Terms exigem proteções de segurança padrão da indústria para o conteúdo sob controle do app, e responsabilizam o desenvolvedor pelas medidas de segurança dos provedores terceiros que processam dados ([Developer Terms, seção V.11](https://developer.spotify.com/terms)).

### Metadados, capas, atribuição e links

- Se o app exibe qualquer conteúdo Spotify, a política exige atribuição clara com **Spotify Marks**; metadados e capas devem ter link para o álbum, faixa, conteúdo ou playlist aplicável no Spotify. Metadados e capas não podem ser oferecidos como produto/serviço independente ([Developer Policy, seção II.4](https://developer.spotify.com/policy)).
- As Design Guidelines especificam atribuir metadados (inclusive nomes de artista, álbum e faixa, capas e reprodução) com o logotipo Spotify. Em integrações parceiras, usar logo completo (ícone + wordmark); o ícone sozinho é permitido quando não há espaço suficiente ou quando a marca já está estabelecida. Os metadados usados devem sempre levar de volta ao Spotify ([Design Guidelines — Attribution](https://developer.spotify.com/documentation/design)).
- Capas devem permanecer na forma original: não recortar, distorcer, animar, desfocar, sobrepor texto/imagens ou colocar marcas sobre elas. A documentação permite ajustar o tamanho e orienta raio de canto de 4 px em dispositivos pequenos/médios e 8 px em dispositivos grandes ([Design Guidelines — Using our content](https://developer.spotify.com/documentation/design), [Developer Terms, seção IV.2.1](https://developer.spotify.com/terms)).
- A referência de álbum alerta que conteúdo Spotify não pode ser baixado e repete as exigências de manter a arte original, atribuir e linkar ao Spotify ([Get Album](https://developer.spotify.com/documentation/web-api/reference/get-an-album)). A referência de Search afirma explicitamente que URLs de imagem de playlists podem expirar em menos de um dia; essa observação está documentada para playlists, não prova o mesmo prazo para as imagens de álbuns/faixas usadas pelo Somnia ([Search](https://developer.spotify.com/documentation/web-api/reference/search)).
- A política proíbe sugerir endosso ou parceria não autorizada. Também proíbe análise de conteúdo/serviço para gerar métricas derivadas/perfis, treinamento de modelos de IA com conteúdo Spotify, e restringe usos comerciais; a política permite apenas usos comerciais limitados para apps **Non-Streaming** ([Developer Policy, seções II, III e IV](https://developer.spotify.com/policy)).
- Os Developer Terms permitem cache local temporário de metadados e capas quando estritamente necessário para desempenho/funcionalidade; não permitem armazenamento indefinido e pedem que dados exibidos sejam atuais e dados antigos sejam apagados ([Developer Terms, seção IV.3](https://developer.spotify.com/terms)).

## Recomendações e inferências para o deploy do Somnia

1. **Defina primeiro a audiência permitida.** Se o deploy for um piloto privado/pessoal, use Development Mode dentro dos limites do app e da conta do desenvolvedor. Se a intenção é abrir o Somnia ao público geral, não trate Client Credentials como uma forma de contornar o modo de acesso: a documentação reserva Development Mode a experimentação/projetos pessoais e direciona apps de público maior a extended quota. Pelos critérios publicados (organização e mínimo de 250 mil MAUs), uma aplicação acadêmica pequena provavelmente não será elegível hoje. Essa conclusão é uma inferência baseada nos critérios oficiais, e o Spotify decide cada pedido.
   - **Nuance para o Somnia:** os participantes não conectam contas Spotify; o app usa um token de aplicação. Portanto, o limite de cinco usuários autorizados não deve ser interpretado automaticamente como limite de cinco contas Somnia, nem como autorização para um deploy público. A documentação descreve Development Mode como sandbox pessoal e não recomendado como base para escalar; confirme com o Spotify se a audiência planejada não for apenas experimentação ([Quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes), [Authorization](https://developer.spotify.com/documentation/web-api/concepts/authorization)).
2. **Guarde segredos exclusivamente no backend hospedado.** Configure no ambiente/secrets manager do serviço que executa `server/src/Api`, nunca no build Angular, repositório, `appsettings.json`, logs ou respostas HTTP. No .NET deste projeto, os nomes de ambiente correspondentes são `Spotify__ClientId`, `Spotify__ClientSecret` e `MusicCatalog__Provider=Spotify`; `Spotify__Market=BR` é opcional porque o código já usa BR por padrão. `Spotify:ClientId` e `Spotify:ClientSecret` são exigidos quando o provedor real inicia. O padrão servidor-segredo é coerente com Client Credentials, que requer secret protegido no backend.
3. **Mantenha o caminho de rede somente no servidor.** O backend faz POST ao host `accounts.spotify.com` para o token e chamadas GET a `api.spotify.com`; o navegador deve falar com Somnia e não receber Client Secret nem token da aplicação. Configure saída HTTPS/DNS para esses hosts na hospedagem; permita tráfego para os domínios de imagens Spotify no navegador se necessário. Essas recomendações decorrem dos hosts e do fluxo usados no código.
4. **Planeje quotas por app e instância.** Não dependa de limite numérico fixo: o Spotify declara que a taxa varia e pode mudar. Preserve `Retry-After`/429 sem repetição agressiva, reduza chamadas com cache temporário e lazy loading, e monitore o gráfico de tráfego do Dashboard. O Somnia já agrupa chamadas concorrentes, tem cache de catálogo no servidor, limite local por usuário e lida com 429 do Spotify usando `Retry-After`; o cache do servidor é por instância, então réplicas não compartilham esse estado. O código não distingue atualmente `reason: QUOTA_EXCEEDED` de um limite de frequência: inclua essa distinção na observabilidade para investigar bloqueios de quota versus picos de tráfego ([anúncio de julho de 2026](https://developer.spotify.com/blog/2026-07-23-web-api-quota-updates)).
5. **Revise a atribuição visual antes de publicar.** As telas verificadas já mostram o link “Abrir no Spotify”, que atende a necessidade de linkar ao item. Porém, nas telas de busca/detalhe consultadas não encontrei logotipo Spotify junto aos metadados; o link textual sozinho não demonstra atendimento à exigência expressa de atribuição com Spotify Marks. Acrescente o logo oficial conforme as guidelines, sem colocá-lo sobre a capa e sem insinuar endosso.
6. **Revise todas as vistas e formas de compartilhar capas.** A capa principal na tela de detalhes usa `object-fit: contain`, mas cartões de avaliação e a lista de faixas usam `object-fit: cover`, que pode recortar conteúdo quando a proporção da origem difere do contêiner. O compartilhamento atual também gera um PNG para download/compartilhamento com a capa incorporada. As guidelines vedam recortar, sobrepor ou distorcer arte; os termos vedam facilitar o download de conteúdo Spotify. Não encontrei autorização explícita para esse PNG composto nas fontes consultadas: valide com o Spotify antes de distribuir em um piloto público ou compartilhe somente texto/link sem a capa ([Design Guidelines](https://developer.spotify.com/documentation/design), [Get Album](https://developer.spotify.com/documentation/web-api/reference/get-an-album), [Developer Terms](https://developer.spotify.com/terms)). O frontend guarda metadados e URLs no `localStorage` por até seis horas e a API mantém detalhes por até 30 minutos; ambos são caches temporários, limitados à funcionalidade descrita. Continue removendo dados expirados e não os migre para armazenamento permanente sem nova revisão dos termos ([Developer Terms, seção IV.3](https://developer.spotify.com/terms)).
7. **Use HTTPS e segredos gerenciados pela plataforma.** São recomendações operacionais para reduzir exposição de credenciais e acesso ao backend; o requisito do Spotify é proteger as informações com medidas de segurança padrão da indústria. Em rotação ou vazamento do Secret, substitua a credencial no Dashboard e atualize o secret do backend.
8. **Inclua termos de uso e política de privacidade antes do acesso ao Spotify.** Os Developer Terms exigem que o app exiba e peça aceite de um acordo de usuário com cláusulas mínimas e mostre uma política de privacidade antes de cadastro/instalação. Confirme essa jornada no Somnia antes de abrir um piloto real ([Developer Terms, seção V.11–12](https://developer.spotify.com/terms)).

## Checklist de configuração da aplicação hospedada

```text
MusicCatalog__Provider=Spotify
Spotify__ClientId=<Client ID no secret store/env do backend>
Spotify__ClientSecret=<Client Secret no secret store/env do backend>
# Opcional; o padrão do projeto é BR
Spotify__Market=BR
```

- Confirme no Dashboard o status do app e se ele tem acesso compatível com a audiência do deploy.
- Verifique que o segredo não aparece no bundle do cliente, logs, imagem Docker, artefatos CI ou configuração pública.
- Configure saída HTTPS para `accounts.spotify.com` e `api.spotify.com`; o frontend recebe somente as respostas da API Somnia.
- Garanta atribuição com marca oficial e link Spotify em toda tela que exibe metadados/capas; não altere a arte.
- Observe 429, `Retry-After`, `reason: QUOTA_EXCEEDED` e tráfego do app. O modo Development compartilha quota da conta de desenvolvedor.
- Não prometa disponibilidade contínua do catálogo: Spotify reserva-se o direito de alterar/remover conteúdo e o serviço pode sofrer interrupções ([Developer Terms](https://developer.spotify.com/terms)).

## Fontes oficiais consultadas

- [Client Credentials Flow](https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow)
- [Authorization](https://developer.spotify.com/documentation/web-api/concepts/authorization)
- [Quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes)
- [Rate Limits](https://developer.spotify.com/documentation/web-api/concepts/rate-limits)
- [Spotify Developer Policy](https://developer.spotify.com/policy)
- [Spotify Developer Terms](https://developer.spotify.com/terms)
- [Design & Branding Guidelines](https://developer.spotify.com/documentation/design)
- [Web API Search reference](https://developer.spotify.com/documentation/web-api/reference/search)
- [Web API Changelog — February 2026](https://developer.spotify.com/documentation/web-api/references/changes/february-2026)
- [Web API Get Album reference](https://developer.spotify.com/documentation/web-api/reference/get-an-album)
- [Developer Access and Platform Security update — 06/02/2026](https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security)
- [Web API quota updates — 23/07/2026](https://developer.spotify.com/blog/2026-07-23-web-api-quota-updates)
