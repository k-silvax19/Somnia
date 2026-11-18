# APIs públicas de catálogo musical

Consulta em 18/11/2026. Escopo: substituir o provedor Spotify do Somnia para busca e detalhes de álbuns/faixas, metadados, capas e links. O Somnia usa Spotify apenas para catálogo e links; não oferece streaming de áudio.

## Resumo

Existe alternativa pública para metadados. A opção aberta mais viável é **MusicBrainz + Cover Art Archive**, com cobertura de capas e conveniência inferiores às de um catálogo comercial. **Apple Music API** oferece catálogo, busca, faixas e artwork mais próximos do contrato atual, mas exige credenciais de desenvolvedor e é vinculada ao catálogo por storefront. **Last.fm** pode complementar tags e popularidade, mas não é o melhor catálogo principal. A documentação pública da **Deezer API** existe, mas seus termos disponíveis são antigos; a disponibilidade atual de credenciais e o suporte a novos projetos precisam ser confirmados antes de escolhê-la.

## Alternativas

| API | O que oferece | Acesso e limites | Adequação ao Somnia |
| --- | --- | --- | --- |
| [MusicBrainz Web Service](https://musicbrainz.org/doc/MusicBrainz_API) + [Cover Art Archive](https://musicbrainz.org/doc/Cover_Art_Archive/API) | Busca e consulta de artistas, gravações, lançamentos e grupos de lançamento; CAA retorna imagens associadas a lançamentos | JSON/XML; para consultas públicas, limite de uma chamada por segundo por aplicação e User-Agent identificável. O FAQ informa uso não comercial gratuito. | Melhor base aberta, sem credenciais para consultas públicas. IDs MBID substituem IDs Spotify. O modelo diferencia lançamento e grupo de lançamento, então o adaptador precisa escolher como representar álbum. Capa não é garantida para todo lançamento. |
| [Apple Music API](https://developer.apple.com/documentation/applemusicapi) | Busca e leitura de álbuns, músicas, artistas e faixas; inclui artwork e storefronts | Requer Developer Token assinado. A documentação manda criar Media ID e chave de serviços de mídia pelo portal de desenvolvedor. O Apple Music Feed em lote requer associação ao Apple Developer Program. | Mais próxima do contrato atual para catálogo comercial. Antes de adotar, confirmar acesso/custo de conta e termos aplicáveis ao uso web do Somnia. Catálogo é regional e não implica permissão para reproduzir áudio. |
| [Last.fm API](https://www.last.fm/api/show/album.getInfo) | `album.getInfo` fornece metadados e tracklist; também expõe informação de uso/social | Requer API key; o método de álbum aceita nome ou MBID. | Complemento possível para informação social. Não escolher como substituto principal sem validar cobertura, atualidade, condições de uso e disponibilidade de imagens para os dados desejados. |
| [Deezer API](https://developers.deezer.com/api) | A documentação e os termos para desenvolvedores descrevem busca, álbum, faixa, artista e playlist | O documento de termos acessível diz que o acesso pode ser gratuito, sujeito a aprovação da Deezer e revogável. É material legado e não comprova que o portal esteja aceitando novas aplicações atualmente. | Investigar o cadastro e testar credenciais antes de investir na integração. Não tratar scraping ou endpoints privados como API pública. |

## Contexto Spotify

Em fevereiro de 2026, Spotify anunciou que Development Mode exigiria Premium, limitaria cada desenvolvedor a um Client ID e cada app a cinco usuários. Em 9 de março anunciou o adiamento das mudanças de acesso a endpoints para integrações existentes; o requisito Premium e os limites de app/usuários permaneceram planejados. Portanto, isso não prova que o catálogo atual do Somnia esteja indisponível, mas torna o modo de desenvolvimento mais restrito para uma aplicação compartilhada. Fonte: [comunicado oficial Spotify](https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security).

## Recomendação para o Somnia

Manter `IMusicCatalogProvider` e implementar um adaptador de prova de conceito para **MusicBrainz + Cover Art Archive** primeiro, se prioridade for API aberta sem credenciais pagas. Isso atende busca e identificação de itens, mas pode reduzir cobertura de artwork e requer respeitar rigorosamente uma chamada por segundo. Se a prioridade for metadados comerciais mais completos e integração similar à existente, avaliar Apple Music API e seus requisitos de conta/termos antes de escrever o adaptador.

Não é necessário mudar a API pública do Somnia para iniciar a avaliação: seu DTO já carrega `provider`, `type`, `externalId`, título, artistas, capa e link externo. A implementação precisaria remover a suposição Spotify nos nomes dos campos/validações, e ajustar limites, identidade e roteamento das avaliações, que hoje aceitam `spotify` e `simulated`.

Nenhuma dessas APIs públicas concede, por si só, direitos para servir streaming de faixas. Esta pesquisa é apenas sobre catálogo e metadados.

## Fontes primárias consultadas

- [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API), [busca](https://musicbrainz.org/doc/MusicBrainz_API/Search), [rate limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting), [FAQ](https://musicbrainz.org/doc/MusicBrainz_API/FAQ) e [Cover Art Archive](https://musicbrainz.org/doc/Cover_Art_Archive/API).
- [Apple Music API](https://developer.apple.com/documentation/applemusicapi), [criação de Media ID/chave](https://developer.apple.com/help/account/capabilities/create-a-media-identifier-and-private-key/) e [Apple Music Feed](https://developer.apple.com/help/account/service-configurations/apple-music-feed).
- [Last.fm album.getInfo](https://www.last.fm/api/show/album.getInfo).
- [Deezer API](https://developers.deezer.com/api) e [termos para desenvolvedores publicados pela Deezer](https://cdns-content.dzcdn.net/pdf/CGV-ww.pdf), seção “Terms of use of Deezer for Developers”.
- [Spotify: update on developer access](https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security).
