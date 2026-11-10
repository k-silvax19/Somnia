# Somnia — PRD v1.2

> Fonte: `C:/Users/kauan/Downloads/somnia.docx`, revisão de escopo de 10/11/2026.
> Esta é a transcrição integral versionada do PRD revisado. O arquivo Word foi atualizado preservando tabelas e formatação.
> Os IDs RF/CA deste PRD são próprios do produto e diferem dos IDs da especificação técnica da API v1.0.
> As referências externas e suas datas de consulta foram preservadas; esta revisão não revalida políticas do Spotify.

DOCUMENTO DE REQUISITOS DO PRODUTO

DIÁRIO MUSICAL

Sua história com a música, álbum por álbum, faixa por faixa.

PRD · MVP + evolução Premium · Versão 1.2

10 de novembro de 2026  |  Português · Brasil

Website social de avaliações de álbuns e músicas, com histórico de avaliações no perfil e catálogo consultado pela API do Spotify.

Núcleo MVP: notas de 0 a 100, resenhas opcionais, avaliações no perfil e descoberta social entre participantes autenticados. Diário privado e registros de escuta estão fora do escopo. Evolução prevista: plano Premium de lançamento por R$ 4,99/mês, condicionado à viabilidade comercial.

“Diário Musical” é uma identificação descritiva provisória. O nome comercial e a identidade final ainda serão definidos.

## COMO LER ESTE DOCUMENTO

A versão 1.2 registra a decisão de 10/11/2026: remover o diário privado e reunir as avaliações no perfil. Nota e resenha seguem a visibilidade do perfil entre participantes autenticados, sem controles independentes de publicação. A fase Premium continua posterior ao piloto. Regras propostas permanecem identificadas como propostas. Este documento não representa software implementado nem autorização comercial concedida pelo Spotify.

Conteúdo: visão · escopo · regras · privacidade · jornadas · experiência · integração · dados · aceite · validação · riscos · Premium · fontes.

## 01  /  Visão e decisões aprovadas

Um lugar para avaliar o que você ouve e acompanhar as opiniões dos amigos.

Problema e proposta de valor

Pessoas que escutam música querem guardar impressões, acompanhar mudanças de gosto e descobrir opiniões de amigos. O projeto reúne avaliações de álbuns e faixas no perfil e uma comunidade inicial pequena. A escuta acontece no Spotify; as avaliações e a conversa acontecem no site. O histórico do perfil representa avaliações, não registros de quando cada item foi ouvido.

Público e lançamento

O público inicial é o proponente e um grupo pequeno de amigos. O MVP continua sendo um experimento pessoal, sem anúncios, assinaturas ou vendas. O plano Premium está especificado para uma fase pós-piloto e só será lançado após validação do produto e da viabilidade comercial e técnica.

| Decisão | Definição aprovada |
| --- | --- |
| Conteúdo avaliado | Álbuns e músicas recebem avaliações próprias. |
| Escala | Notas inteiras de 0 a 100; a nota do álbum não depende da média das faixas. |
| Resenhas | Texto opcional; nota e resenha são visíveis aos participantes autenticados junto à avaliação no perfil. |
| Histórico no perfil | Lista de avaliações do usuário, começando pelas mais recentes; sem diário ou registro manual de escuta. |
| Avaliação atual | Uma avaliação atual editável por pessoa e item; edição não cria outra avaliação no histórico. |
| Social | Perfis, seguir sem aprovação, feed de publicações e descoberta da comunidade. |
| Visibilidade e autoria | Perfis e avaliações acessíveis somente aos participantes autenticados. Apenas o autor pode editar ou excluir sua avaliação. |
| Catálogo e áudio | Spotify para busca, metadados e capas; botão para ouvir fora do site. |
| Acesso e interface | Cadastro próprio; website responsivo, tema escuro e identidade própria. |

Objetivo do MVP

Validar se os participantes voltam para avaliar álbuns e faixas e se encontram valor no histórico do perfil e nas opiniões dos amigos. O principal sinal de sucesso é o uso recorrente, com entendimento de que as avaliações ficam visíveis aos participantes autenticados.

Princípio central: avaliações e perfis exigem autenticação; edição e exclusão exigem autoria. Dados de conta, credenciais e sessões não integram o conteúdo visível no perfil.

## 02  /  Escopo funcional do MVP

Os requisitos abaixo compõem a primeira versão. Todos são necessários ao fluxo principal.

| ID | Requisito | Comportamento esperado |
| --- | --- | --- |
| RF01 | Conta própria | Criar conta, entrar, sair e manter sessão sem depender de login Spotify. Método de autenticação a definir. |
| RF02 | Perfil | Nome, foto, bio, histórico de avaliações do titular e ação de seguir; perfil acessível aos participantes autenticados e editável somente pelo titular. |
| RF03 | Busca | Pesquisar álbuns e músicas no catálogo; distinguir tipos de resultado e carregar mais resultados. |
| RF04 | Página de álbum | Capa, título, artista, faixas, avaliação pessoal e nota comunitária quando existir. |
| RF05 | Página de música | Título, artista, álbum relacionado, avaliação pessoal e nota comunitária quando existir. |
| RF06 | Avaliação | Criar, editar e excluir a própria avaliação de álbum ou faixa, com nota inteira de 0 a 100 e resenha opcional; conteúdo visível aos participantes autenticados. |
| RF07 | Histórico de avaliações no perfil | Listar as avaliações do titular no próprio perfil, começando pelas mais recentes e com paginação. Não registrar datas de escuta, escutas repetidas ou um diário separado. |
| RF08 | Rede e feed | Seguir e deixar de seguir; ver avaliações das pessoas seguidas, respeitando o acesso restrito aos participantes autenticados. |
| RF09 | Descoberta | Área separada com avaliações dos participantes da comunidade, disponível somente após autenticação. |
| RF10 | Acesso e autorização | Exigir autenticação para consultar perfis e avaliações; permitir alteração e exclusão apenas ao autor. Não há modo privado nem controles independentes de visibilidade de nota e resenha no MVP revisado. |
| RF11 | Abrir no Spotify | Link para o conteúdo correspondente no Spotify, com atribuição da origem. |

Fora do escopo

Diário privado; registro manual de escutas e suas datas; repetição de escutas; controles independentes de publicação de nota/resenha; reprodução de áudio no site; dependência de previews; importação automática do histórico; sincronização de playlists; recomendações por IA; aplicativos nativos; rankings globais; monetização na entrega inicial do MVP e lançamento comercial.

Listas personalizadas, comentários e mensagens ficam para uma etapa posterior. Curtidas, notificações e outras interações não foram incluídas no MVP aprovado.

Evolução sem compromisso de entrega

A etapa Premium está detalhada nas seções 12 a 15 desta versão, mas não integra os critérios de pronto do MVP. Outros recursos posteriores devem ser priorizados a partir do piloto. Não há prazo nem garantia de integração futura com dados pessoais do Spotify.

## 03  /  Regras de avaliação e histórico no perfil

A avaliação descreve a opinião atual. A lista de avaliações compõe o histórico musical no perfil, sem representar datas ou quantidade de escutas.

Regras confirmadas

Álbum e música são itens avaliáveis distintos. Avaliar uma faixa não altera a nota do álbum; avaliar o álbum não preenche as notas das faixas.

A escala aceita números inteiros entre 0 e 100, inclusive os extremos. Zero é uma nota válida e deve ser diferente de “sem nota”.

Há uma avaliação atual por usuário e item. A pessoa pode mudar sua opinião editando a nota ou a resenha.

A resenha é opcional. Quando presente, é exibida com a avaliação aos participantes autenticados, seguindo o mesmo acesso da nota.

O mesmo usuário mantém uma avaliação atual por item. Editar a avaliação atualiza o registro existente; não cria uma nova entrada nem registra outra escuta.

A média comunitária considera as notas atuais das avaliações não excluídas, com uma contribuição por usuário e item. Avaliações sem nota não entram no cálculo.

Exemplo de funcionamento

Uma pessoa atribui 99/100 a “Wait and Bleed” e 90/100 ao álbum correspondente. As avaliações aparecem no perfil para os participantes autenticados, com a resenha quando houver. Editar a nota da faixa para 95 atualiza a mesma avaliação e sua contribuição na média; a avaliação 90 do álbum continua independente. Não há registro de escuta.

Detalhamento operacional proposto

| Situação | Regra recomendada para implementação |
| --- | --- |
| Avaliar e consultar o perfil | Salvar uma avaliação faz com que ela apareça na lista do perfil; não é necessária nem existe uma ação separada de registrar escuta. |
| Avaliar sem texto | Permitir nota sem resenha e resenha sem nota; não salvar avaliação completamente vazia. |
| Média comunitária | Soma das notas atuais dividida pela quantidade de autores com nota. Exibir uma casa decimal e o número de notas. Sem notas: “Ainda sem avaliações”. Somente participantes autenticados consultam esses dados. |
| Editar ou excluir | Permitir ao autor remover nota, resenha ou avaliação; não manter avaliação completamente vazia. Atualizar perfil, média, feed e descoberta conforme o conteúdo restante. |
| Ordenação do histórico | Listar avaliações por data de criação decrescente, com desempate estável e paginação. Edição preserva a data de criação e atualiza a data de edição. |
| Evitar duplicações | Envios repetidos não devem criar duas avaliações para o mesmo usuário e item. Rejeitar a duplicidade ou atualizar a avaliação existente conforme o contrato da API; nunca criar registros de escuta. |

As regras desta tabela detalham lacunas operacionais; são propostas para revisão na preparação da implementação.

## 04  /  Visibilidade do perfil e autorização

Nota e resenha seguem o acesso do perfil: participantes autenticados podem consultar avaliações; apenas o autor pode alterá-las. A retirada do diário não cria exposição anônima de dados.

Matriz de acesso confirmada

| Pessoa | Consulta | Alterações permitidas | Média |
| --- | --- | --- | --- |
| Autor autenticado | Perfil e avaliações | Criar, editar e excluir suas próprias avaliações. | Uma contribuição por item com nota |
| Outro participante autenticado | Perfil, nota e resenha | Nenhuma alteração na avaliação de outra pessoa. | Pode consultar |
| Visitante sem autenticação | Sem acesso | Nenhuma operação em perfis ou avaliações. | Sem acesso |
| Avaliação excluída | Não aparece nas consultas | Retirada do perfil, feed e descoberta. | Não contribui |

Seguir uma pessoa não concede permissão para editar ou excluir avaliações. Não há diário, datas de escuta ou campos de nota/resenha privados no escopo revisado. Dados de conta e segurança permanecem restritos.

Comportamentos obrigatórios

Ao salvar uma avaliação, informar que nota e resenha ficarão visíveis aos participantes autenticados no perfil. Não oferecer preferências de publicação nem chaves de privacidade por campo para avaliações.

Perfil, feed, descoberta e API aplicam a mesma regra de autenticação e exibem o conteúdo atual das avaliações não excluídas.

Médias e contagens consideram as avaliações atuais elegíveis: uma nota por usuário e item. Resenhas sem nota não aumentam a quantidade de notas.

A autorização deve ser aplicada no servidor. Ocultar um campo na interface não é suficiente.

Edição e exclusão — comportamento esperado

Ao excluir uma avaliação, retirá-la do perfil, feed, descoberta e resultados derivados; recalcular média e contagem quando houver nota.

Não existe transição de público para privado no MVP revisado. O autor pode editar o conteúdo ou excluir a avaliação; alterações do perfil não transferem autoria.

No feed, manter uma representação da avaliação atual. Editar deve atualizar essa representação, sem multiplicar publicações a cada edição.

Limite de retirada: o site pode excluir conteúdo de suas superfícies, mas não consegue desfazer capturas ou cópias feitas pelos participantes enquanto a avaliação estava disponível.

## 05  /  Jornadas e mapa de telas

A navegação principal contém Início, Busca e Perfil. As avaliações ficam em uma seção do perfil; não há tela de diário.

| Tela | Conteúdo e ações principais |
| --- | --- |
| Entrada / conta | Criar conta e entrar. Explicar a proposta e que avaliações são visíveis aos participantes autenticados. |
| Início | Feed das pessoas seguidas; acesso separado à descoberta da comunidade. |
| Busca | Campo de consulta, filtros de álbum/música, resultados distinguíveis e paginação. |
| Álbum | Identificação, capa, artista, faixas, avaliação pessoal, média pública e link Spotify. |
| Música | Identificação, artista, vínculo com álbum, avaliação pessoal, média pública e link Spotify. |
| Avaliar | Nota e resenha opcional, com indicação do acesso pelos participantes antes de salvar; editar e excluir disponíveis somente ao autor. |
| Avaliações do perfil | Seção do perfil com histórico paginado de avaliações, começando pelas mais recentes; sem botão de registrar escuta. |
| Perfil | Nome, foto, bio, avaliações do titular e ação seguir/deixar de seguir. O autor pode editar ou excluir as próprias avaliações. |
| Preferências | Editar os dados do perfil; não incluir padrões de publicação para avaliações. |

Jornada A · Registrar uma descoberta

Entrar → buscar álbum ou música → abrir resultado → atribuir nota e, se desejar, escrever resenha → salvar. A avaliação aparece no perfil e fica acessível aos participantes autenticados.

Jornada B · Rever uma opinião

Abrir uma avaliação no próprio perfil ou no item → editar a avaliação atual → salvar. O mesmo registro é atualizado, preservando a data de criação; a média é recalculada sem adicionar outro voto.

Jornada C · Acompanhar amigos

Encontrar um perfil → seguir → consultar o feed → abrir uma avaliação → visitar o álbum ou música → ouvir pelo link Spotify. Perfil, feed e avaliações exigem autenticação.

Primeiro uso — proposta

Explicar antes da primeira avaliação que nota e resenha ficam visíveis aos participantes autenticados. Um perfil sem avaliações deve orientar a pessoa a buscar um álbum ou faixa; um feed vazio pode orientar a seguir participantes.

## 06  /  Experiência e qualidade

Direção aprovada: tema escuro, capas em destaque, notas visíveis e uso confortável no celular.

Interface

Criar identidade própria. Letterboxd é referência de experiência; nome, marca, componentes e composição visual precisam ser próprios.

Mostrar o denominador da nota quando necessário: 99/100. Diferenciar claramente a nota pessoal da nota comunitária e de sua quantidade de avaliações.

Posicionar notas e ações ao lado ou abaixo das capas. Preservar a arte original e atribuir o conteúdo ao Spotify conforme suas diretrizes [S4].

Usar “Avaliar” como ação principal de registro. No perfil, apresentar nota, resenha e datas de criação/edição da avaliação; não apresentar ações ou datas de escuta.

Estados obrigatórios

| Estado | Resposta esperada |
| --- | --- |
| Carregando | Mostrar progresso coerente, preservar navegação e evitar submissões repetidas. |
| Sem resultados | Informar que nada foi encontrado e permitir ajustar a consulta. |
| Sem publicações | Orientar o participante a seguir pessoas ou abrir a descoberta. |
| Sem avaliações | Exibir ausência de notas, sem confundir com média zero. |
| Spotify indisponível | Explicar a falha na consulta; preservar acesso aos dados próprios e permitir nova tentativa. |
| Erro ao salvar | Manter os dados digitados; não sinalizar sucesso antes da confirmação. |
| Conteúdo indisponível | Informar a indisponibilidade do item sem apagar avaliações do titular ou seu histórico no perfil. |

Requisitos não funcionais propostos

Responsividade: fluxos completos em telas de 360 px e em desktop, sem rolagem horizontal. Acessibilidade: navegação por teclado, foco visível, rótulos em formulários, mensagens de erro associadas aos campos e indicação clara do acesso às avaliações.

Segurança: sessões protegidas, autenticação para consultas, autorização por proprietário nas alterações, proteção contra injeção de conteúdo nas resenhas e nenhum segredo do Spotify exposto no navegador. Confiabilidade: integridade entre gravação da avaliação e atualização do perfil, média e feed.

Desempenho: definir um orçamento mensurável após escolher hospedagem e arquitetura. Busca deve ser paginada, limitar requisições repetidas e respeitar as respostas de quota do provedor.

## 07  /  Integração com Spotify

Integração aprovada para catálogo e links externos. Não há reprodução nem sincronização pessoal no MVP.

Desenho funcional

O servidor consulta busca, álbuns, faixas e faixas de um álbum. O fluxo Client Credentials autentica a aplicação, sem autorização do visitante, para endpoints que não acessam informações pessoais [S1]. Cadastro, perfis, notas, resenhas e histórico de avaliações pertencem ao próprio produto.

Manter credenciais e tokens da aplicação no servidor. O frontend recebe somente os dados necessários à tela.

Vincular o conteúdo à origem e oferecer “Ouvir no Spotify”. Catálogo, capas e nomes devem ter atribuição adequada [S4].

Não exigir preview de áudio: preview_url está obsoleto e pode não existir para uma faixa [S7].

Restrições verificadas em 05/11/2026

| Tema | Impacto no produto |
| --- | --- |
| Development Mode | O proprietário do app precisa de Premium. Há limite de cinco usuários Spotify autenticados por app [S2]. |
| Contagem de usuários | Esse limite se refere a contas Spotify autenticadas; não deve ser tratado como limite documentado de contas próprias do site usando apenas catálogo. |
| Finalidade do acesso | Spotify descreve Development Mode como voltado a experimentação e projetos pessoais não comerciais [S3]. |
| Expansão | Acesso ampliado depende de elegibilidade e análise do Spotify; o lançamento comercial não é consequência automática do piloto [S2]. |
| Quotas e falhas | Tratar 401, 403, 429 e falhas temporárias. Respeitar Retry-After quando fornecido e evitar repetição agressiva de chamadas. |
| Dados externos | Armazenamento e cache devem respeitar os termos. Não pressupor licença para copiar permanentemente o catálogo inteiro [S5]. |

Condição de viabilidade

Client Credentials é um mecanismo técnico de acesso, não uma autorização geral para qualquer uso. Validar no Developer Dashboard os endpoints realmente disponíveis e conferir o enquadramento do aplicativo nas políticas antes de abrir o piloto. O projeto com avaliações sociais não tem aprovação específica comprovada neste documento [S5, S8].

Se o acesso necessário não estiver disponível, registrar o bloqueio e reavaliar a fonte do catálogo com o responsável. Outra API não está aprovada neste PRD.

## 08  /  Dados e responsabilidades

Modelo conceitual proposto para orientar a implementação; não define tecnologia ou esquema físico.

| Entidade | Responsabilidade e informações essenciais |
| --- | --- |
| Usuário | Identidade própria, nome público, foto, bio e credencial/referência de autenticação. Sem preferência de publicação de avaliações no escopo revisado. |
| Item de catálogo | Tipo álbum ou música, origem Spotify, identificador externo e referências mínimas de apresentação. Atualização/cache conforme o provedor. |
| Relação álbum–faixa | Vincular a música ao álbum e à posição no conjunto quando esses dados estiverem disponíveis. |
| Avaliação atual | Proprietário + item; nota opcional, resenha opcional e datas de criação/edição. Acesso compartilhado com o perfil entre participantes autenticados. |
| Histórico de avaliações no perfil | Consulta paginada das avaliações do titular por data de criação decrescente. Não é uma entidade de escuta nem duplica a avaliação; não há tabela de registros de escuta no modelo proposto. |
| Relação de seguir | Quem segue e quem é seguido; vínculo único por par. |
| Representação para a comunidade | Consulta/projeção das avaliações atuais não excluídas para perfil, feed e descoberta, sempre exigindo autenticação e sem expor dados de segurança da conta. |

Invariantes

Unicidade da avaliação atual por usuário + origem + tipo + identificador externo do item. Avaliações de álbum e música nunca compartilham a mesma chave.

Nota ausente é diferente de nota zero. Texto ausente não deve criar uma resenha vazia pública.

Editar uma avaliação não cria novo voto, nova avaliação nem registro de escuta; excluir remove sua contribuição na média e na contagem.

Consultas de perfil, avaliações e dados derivados exigem autenticação; somente o autor pode editar ou excluir a avaliação.

Separação de responsabilidades — proposta

Interface responsiva → serviço do produto → armazenamento dos dados próprios. Um adaptador de catálogo conversa com o Spotify no servidor. Essa separação reduz o impacto de mudanças externas e permite preservar os registros do usuário durante falhas de catálogo.

Médias devem ser calculadas a partir das notas atuais das avaliações dos usuários, não de datas de escuta ou de métricas de popularidade do Spotify. Não há diário, análise de áudio, treinamento de IA ou importação automática de dados pessoais.

Decisão de identidade ainda aberta

Remasters, edições deluxe e relançamentos podem aparecer como itens distintos. É necessário definir se serão avaliados separadamente ou agrupados. Até essa decisão, este PRD não presume fusão por nome, artista ou gravação.

## 09  /  Critérios de aceite

Cenários verificáveis para homologar o núcleo aprovado e identificar falhas antes do piloto.

| ID | Cenário | Resultado esperado |
| --- | --- | --- |
| CA01 | Nota de álbum e faixa | Salvar álbum com 90 e faixa com 99 mantém os dois valores independentes. |
| CA02 | Limites da escala | Aceitar 0 e 100; rejeitar notas negativas, acima de 100 e não inteiras. Sem nota não equivale a zero. |
| CA03 | Avaliação no perfil | Salvar uma avaliação a inclui no perfil do autor; outro participante autenticado pode ler a nota e a resenha existente. |
| CA04 | Acesso autenticado | Visitante sem autenticação não acessa perfis, avaliações, feed, descoberta ou médias por tela, URL direta ou API. |
| CA05 | Média pública | Notas 90 e 100 produzem média 95 com duas contribuições. Uma avaliação sem nota não altera média nem quantidade de notas. |
| CA06 | Editar nota pública | Mudar 90 para 80 no cenário anterior produz média 90, com dois autores; atualiza a mesma avaliação no perfil e não cria outro voto. |
| CA07 | Histórico sem duplicação | Editar ou repetir o envio para o mesmo usuário e item não cria uma segunda avaliação no perfil. Não existe fluxo de registrar escuta ou diário separado. |
| CA08 | Autoria protegida | Outro participante autenticado não consegue editar nem excluir uma avaliação alheia por interface, URL direta ou API; o autor consegue alterar a própria. |
| CA09 | Excluir avaliação | Excluir uma avaliação a retira do perfil, feed e descoberta e recalcula média e contagem quando havia nota. |
| CA10 | Feed e descoberta | Feed exibe avaliações atuais de pessoas seguidas; descoberta exibe avaliações da comunidade. Ambos exigem autenticação e não expõem dados de segurança da conta. |
| CA11 | Catálogo e origem | Busca distingue álbum de música; páginas mostram identificação e link Spotify correspondente. Credenciais não chegam ao cliente. |
| CA12 | Falha externa | Falha ou quota do Spotify gera mensagem recuperável; perfil e avaliações já salvas continuam acessíveis. |
| CA13 | Ordenação do perfil | Avaliações do perfil são paginadas por criação decrescente, com desempate estável. Editar preserva a data de criação e atualiza a data de edição. |
| CA14 | Mobile e teclado | Fluxos de buscar, avaliar, consultar o perfil, editar e excluir avaliação funcionam no celular e por teclado. |

Complementos operacionais propostos na seção 03 devem receber cenários próprios quando forem confirmados. Estes critérios descrevem validação esperada, não testes já executados.

## 10  /  Entrega e validação do piloto

Sequência recomendada, sem prazos ou custos assumidos.

| Etapa | Entrega | Condição para avançar |
| --- | --- | --- |
| 1 · Viabilidade | App de desenvolvimento configurado; busca, álbum e música consultados em ambiente controlado. | Endpoints e uso pretendido compatíveis; credenciais protegidas. |
| 2 · Núcleo pessoal | Conta, busca, páginas de catálogo, avaliações e histórico de avaliações no perfil. | Persistência correta; uma avaliação por usuário e item; consulta autenticada e alterações restritas ao autor; nenhum fluxo de diário ou registro de escuta. |
| 3 · Comunidade | Perfis, seguir, feed, descoberta e média comunitária. | Perfil, feed e descoberta mostram avaliações atuais apenas a participantes autenticados; médias e contagens refletem edições e exclusões. |
| 4 · Homologação | Estados de erro, responsividade e revisão dos critérios de aceite. | Sem defeito bloqueante nos fluxos e sem vazamento conhecido de conteúdo privado. |
| 5 · Piloto | Uso com amigos e coleta de feedback. | Participantes entendem o produto, a escala e o que estão publicando. |

Métricas propostas

Ativação: proporção de convidados que completam uma primeira avaliação e a encontram no próprio perfil.

Retorno: participantes que voltam em semanas seguintes para avaliar ou consultar avaliações.

Valor social: participantes que seguem alguém e consultam publicações públicas.

Clareza: capacidade de identificar quem pode consultar uma avaliação e quem pode editá-la ou excluí-la em uma breve tarefa de uso.

Qualidade: falhas de salvamento, erros de integração e problemas de autorização encontrados no piloto.

Como a amostra será pequena, comparar números com relatos dos participantes. Não definir metas percentuais de sucesso antes de conhecer o tamanho do grupo e a duração do experimento. Coletar apenas eventos necessários; não enviar resenhas ou dados de conta a ferramentas de analytics.

Roteiro de avaliação com amigos

Pedir que cada participante encontre um álbum, avalie uma faixa, escreva uma resenha, encontre a avaliação no próprio perfil, edite a nota e siga alguém. Verificar se consegue prever quem verá o conteúdo e quem poderá alterá-lo. Registrar dúvidas, fricções e recursos pedidos espontaneamente.

Critério para concluir o MVP

O MVP está pronto para o piloto quando o escopo funcional estiver disponível, os critérios de aceite aplicáveis forem atendidos e a integração tiver acesso compatível com o experimento. Uma expansão depende de nova decisão de produto e de viabilidade da API.

## 11  /  Riscos e decisões pendentes

Distinguir limitações externas, hipóteses de produto e escolhas ainda não tomadas.

| Risco | Tratamento recomendado |
| --- | --- |
| Acesso insuficiente à API | Validar antes de investir no restante da integração. Não prometer expansão nem contornar quotas. |
| Mudanças no catálogo | Isolar a integração e tratar itens indisponíveis sem apagar dados próprios. |
| Vazamento de privacidade | Centralizar autorização, revisar consultas derivadas e homologar todas as combinações. |
| Confusão entre avaliação e histórico de escutas | Explicar que o perfil lista avaliações atuais e não registra quando ou quantas vezes a pessoa ouviu cada item. Não oferecer ação de registrar escuta. |
| Comunidade pouco ativa | Convidar um grupo com interesse real e facilitar a primeira avaliação e o primeiro vínculo social. |
| Edições duplicadas | Definir a unidade de identidade antes de consolidar médias entre versões. |
| Conteúdo problemático | No piloto, definir um contato responsável e procedimento de retirada; recursos de moderação ampla dependem de novo escopo. |

Ainda não definido

Nome definitivo, domínio, identidade gráfica detalhada e textos finais.

Tecnologias, método de autenticação própria, hospedagem e orçamento.

Cadastro do piloto: convites ou outro controle. Na versão 1.2, perfis e avaliações exigem autenticação; acesso anônimo e indexação na internet ficam fora do escopo. “Público” nas referências à comunidade significa visível aos demais participantes autenticados.

Tratamento de edições e relançamentos; limites de tamanho das resenhas e da bio.

Detalhes de gestão de conta, exclusão de dados, backups e retenção.

Duração do piloto, quantidade de convidados e metas quantitativas.

Propostas que precisam de revisão na implementação

Detalhes operacionais das seções 03 e 04, modelo conceitual da seção 08 e métricas da seção 10 servem como ponto de partida. Eles não substituem decisões explícitas sobre lacunas do produto. Nenhuma tecnologia, prazo ou custo foi aprovado por inferência.

Próximo passo recomendado: finalizar e testar o MVP gratuito, verificar as condições de uso do catálogo para atividade comercial e, depois, implementar e homologar a camada Premium antes de ativar cobranças.

## 12  /  Plano Premium e posicionamento

Monetização futura por assinatura, sem retirar recursos essenciais do diário musical gratuito.

Status e plano comercial

O MVP original permanece gratuito e sem cobrança durante o piloto. A assinatura Premium é uma expansão de produto especificada nesta versão, com execução condicionada à validação de uso, custos, pagamentos e permissão do catálogo para operação comercial.

| Parâmetro | Definição do plano de lançamento |
| --- | --- |
| Nome | Diário Musical Premium (nome provisório). |
| Preço inicial | R$ 4,99 mensais no Brasil. US$ 0,99 é apenas referência possível para outro mercado; preços regionais são independentes. |
| Cobrança | Assinatura recorrente mensal, com renovação explícita nos termos apresentados na contratação e cancelamento acessível. |
| Reajuste | Preço promocional sujeito a revisão futura; informar valores, data e condições antes de qualquer alteração aplicável. |
| Elegibilidade | Somente assinaturas com status de acesso vigente habilitam os benefícios pagos. |
| Selo fundador | Distintivo opcional de apoiador inicial, sem conceder acesso vitalício aos recursos Premium. |

Compromisso com a versão gratuita

Continuam gratuitos e sem limites artificiais: buscar, avaliar álbuns e músicas, escrever resenhas, consultar o histórico de avaliações no perfil, seguir perfis e navegar no feed e na descoberta. O plano Premium não reintroduz diário ou registro de escutas.

Autorização, edição e exclusão das avaliações, notas comunitárias e acesso aos próprios dados não dependem de pagamento.

O Premium agrega apresentação, personalização, destaque sinalizado e análises; não compra notas maiores ou relevância garantida.

Gate de lançamento: avaliar os termos da API do Spotify e confirmar a autorização de uso comercial do catálogo antes de ativar faturamento. O acesso técnico via Client Credentials não substitui essa validação.

## 13  /  Requisitos funcionais Premium

Recursos especificados para a primeira oferta paga e para etapas posteriores, com prioridade explícita.

| ID | Recurso | Comportamento esperado |
| --- | --- | --- |
| RF12 | Assinatura | Contratar, consultar status e data de renovação, cancelar renovação futura e acompanhar o acesso Premium. Prioridade alta. |
| RF13 | Avatar em GIF | Usuário Premium pode adicionar foto de perfil animada; alternativa estática e limites de arquivo. Prioridade alta. |
| RF14 | Banner e temas | Personalizar cabeçalho, cores e temas com opções acessíveis; animações podem ser desativadas. Prioridade alta. |
| RF15 | Álbuns fixados | Destacar até 4 álbuns favoritos no perfil; não alterar notas nem índices da comunidade. Prioridade alta. |
| RF16 | Impulsionar avaliação | Receber 2 créditos por ciclo mensal; cada crédito destaca uma avaliação pública na Descoberta por 24 h. Prioridade alta. |
| RF17 | Mídia em resenha | Anexar GIF próprio a resenha pública, sujeito a limites de upload e moderação. Não modificar capa oficial. Prioridade média. |
| RF18 | Estatísticas | Gráficos de notas e avaliações do perfil, usando suas datas de criação/edição; sem registros de escuta nem alegação de histórico completo do Spotify. Prioridade média. |
| RF19 | Retrospectiva | Resumo mensal/anual das avaliações, conforme dados existentes; sem inferir datas ou quantidade de escutas e sem expor dados de conta. Prioridade média. |
| RF20 | Cards e selo | Gerar cards visuais compartilháveis e exibir selo opcional de apoiador. Prioridade média. |

Regras específicas do impulsionamento

A ação se aplica à representação pública de uma avaliação existente; não cria avaliações extras nem modifica a nota, sua média ou o peso do autor.

Cada ciclo de cobrança ativo disponibiliza 2 créditos, sem acumulação entre ciclos e com no máximo 1 destaque ativo por usuário por vez.

A exposição dura até 24 horas; o destaque aparece identificado como “Impulsionado”. Não há promessa de alcance, cliques ou visualizações.

Se a avaliação for excluída, moderada ou indisponível, o destaque deve ser retirado imediatamente, inclusive dos caches de consulta.

## 14  /  Contratação, privacidade e dados

O acesso Premium não modifica a regra de autenticação das consultas nem a autorização por autor para alterar avaliações; dados de conta permanecem protegidos.

Regras comerciais e de experiência

| Situação | Regra proposta para implementação |
| --- | --- |
| Ativação | Conceder benefícios apenas após confirmação válida do pagamento pelo provedor; checar permissões no servidor. |
| Cancelamento | Suspender a renovação futura; manter acesso até o fim do período já pago, conforme termos informados ao cliente. |
| Falha de pagamento | Exibir estado de regularização; definir eventual carência antes da operação. Não cobrar ou renovar silenciosamente. |
| Vencimento | Preservar avaliações, dados e perfil; remover recursos pagos ativos ou substituí-los por apresentação estática, sem apagar conteúdo próprio. |
| Mídia animada | Validar formato, dimensão, tamanho, segurança e moderação; oferecer redução de movimento e versão estática. |
| Resenhas públicas | GIF só pode ser exibido se a avaliação associada estiver disponível e o anexo tiver autorização própria de exibição; consultas continuam restritas aos participantes autenticados. |
| Dados privados | Dados de conta, credenciais e sessões não podem ser usados em impulsionamento, feed, descoberta, propaganda ou métricas da comunidade. Não há diário ou registros privados de escuta no escopo. |

Entidades adicionais sugeridas

PlanoAssinatura (preço/moeda/benefícios); AssinaturaUsuario (usuário, estado, ciclo e cancelamento); EventoPagamento (referência idempotente do provedor); CreditoImpulso (saldo por ciclo); Impulsionamento (avaliação, início/fim, estado); PersonalizacaoPerfil (avatar, banner, tema); AlbunsFixados; MidiaResenha. Nomes e estrutura física dependem da arquitetura escolhida.

Regras de segurança e integridade

Validar status Premium, limites de uso e propriedade dos conteúdos no backend; webhooks de pagamentos autenticados, auditáveis e idempotentes.

Não usar capas ou artes oficiais do Spotify como base para GIFs editados; manter atribuição, políticas de apresentação e links exigidos.

Aplicar proteção contra abuso em uploads e impulsionamentos; permitir denúncia e retirada de mídia, sem conceder poderes de moderação a assinantes.

Nenhuma assinatura, GIF ou impulso pode contornar a autenticação das consultas, conceder edição de avaliação alheia ou revelar dados de segurança da conta.

## 15  /  Implantação e critérios Premium

A extensão paga é uma fase posterior, com aceitação própria e bloqueios explícitos antes de cobrar.

Sequência de entrega

| Etapa | Entrega | Condição para avançar |
| --- | --- | --- |
| P1 · Preparação | Verificar uso comercial do catálogo, pagamentos, custos de mídia, moderação e textos da assinatura. | Viabilidade documentada; sem cobrança enquanto houver bloqueio crítico. |
| P2 · Núcleo Premium | Assinatura, estados de acesso, avatar animado, banner, temas e álbuns fixados. | Pagamento e autorização server-side homologados; cancelamento funcional. |
| P3 · Destaque | Créditos, impulsionamento identificado e limites de exibição. | Exposição e privacidade testadas; nenhum efeito na média comunitária. |
| P4 · Evolução | GIF em resenhas, estatísticas, retrospectivas e cards personalizados. | Validar demanda, segurança, custos e experiência antes de liberar. |

Critérios de aceite adicionais

| ID | Cenário | Resultado esperado |
| --- | --- | --- |
| CA15 | Sem assinatura | Usuário gratuito conserva todos os fluxos do MVP e não obtém benefícios Premium. |
| CA16 | Ativação paga | Acesso Premium só é ativado após confirmação válida do pagamento. |
| CA17 | Cancelamento | Interrompe renovação futura e mantém acesso até o término previsto; dados e avaliações continuam. |
| CA18 | Ciclo de créditos | Dois créditos por novo ciclo vigente; não acumulam e não permitem mais de um impulso simultâneo. |
| CA19 | Impulso de avaliação indisponível | Impulsionamento rejeita avaliação excluída, moderada ou de outro autor; exclusão ou moderação posterior remove a publicação destacada. |
| CA20 | Impulso público | Selo de publicidade visível; 24 horas de duração; média e contagem comunitária invariáveis. |
| CA21 | GIF e tema | Upload validado; fallback estático; redução de movimento; nenhuma alteração em arte oficial do catálogo. |
| CA22 | Acesso revogado | Status expirado bloqueia funções pagas sem perda de avaliações ou do histórico no perfil. |

Indicadores a acompanhar após o piloto

Taxa de conversão voluntária, cancelamentos, custo de processamento por assinatura, tráfego e armazenamento de GIFs, uso de créditos, denúncias e satisfação. Tratar esses indicadores apenas com dados autorizados; não enviar resenhas nem dados de conta a ferramentas públicas de análise.

Pendências: provedor de pagamento, impostos e termos locais, política de reembolso, limites exatos de tamanho de GIF, critérios de moderação, janela de carência e eventual reajuste. Nenhum desses pontos é considerado aprovado por inferência.

## 16  /  Fontes e rastreabilidade

Documentação oficial consultada em 05 de novembro de 2026. As regras da plataforma podem mudar.

As decisões de produto derivam das recomendações aceitas pelo proponente na conversa. As fontes abaixo sustentam as restrições externas e os cuidados de integração. As referências entre colchetes no texto apontam para estes itens.

[S1] Spotify · Client Credentials Flow

https://developer.spotify.com/documentation/web-api/tutorials/client-credentials-flow

[S2] Spotify · Quota modes

https://developer.spotify.com/documentation/web-api/concepts/quota-modes

[S3] Spotify · Update on Developer Access and Platform Security

https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security

[S4] Spotify · Design & Branding Guidelines

https://developer.spotify.com/documentation/design

[S5] Spotify · Developer Terms

https://developer.spotify.com/terms

[S6] Spotify · Search for Item

https://developer.spotify.com/documentation/web-api/reference/search

[S7] Spotify · Get Track

https://developer.spotify.com/documentation/web-api/reference/get-track

[S8] Spotify · Developer Policy

https://developer.spotify.com/policy

[S9] Spotify · Rate limits

https://developer.spotify.com/documentation/web-api/concepts/rate-limits

Uso das fontes

A consulta ao catálogo se apoia em [S1, S6, S7]; limites de acesso em [S2, S3]; apresentação de conteúdo em [S4]; armazenamento, uso permitido e políticas em [S5, S8]; tratamento de limites de chamadas em [S9]. A implementação deve conferir a disponibilidade real dos endpoints para o app criado.

Versão 1.2 · Revisão de escopo aprovada em 10/11/2026: diário privado e registros de escuta removidos; avaliações compõem o histórico do perfil, acessível aos participantes autenticados; somente o autor pode editar ou excluir. Regras de privacidade por campo foram substituídas pelo acesso do perfil. Jornadas, modelo, critérios e referências Premium foram alinhados. A fase Premium continua posterior ao piloto. Esta revisão altera requisitos, não comprova sua implementação.
