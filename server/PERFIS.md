# Perfis

Implementação parcial de RF02 e das regras de privacidade do Somnia.
Conferida em 10/11/2026 contra `Somnia_Especificacao_Tecnica_API_MVP_v1.0.pdf`
(09/11/2026), seções 4, 8, 10 e 15, e o PRD `somnia.docx` (v1.1).

O PDF inclui edição de avatar no perfil; esse recurso ainda não foi
implementado. Portanto, RF02 não deve ser tratado como concluído.
O projeto usa SQL Server, quatro camadas e rotas sem `/v1`; PostgreSQL e
`/api/v1` são propostas do PDF que diferem da implementação atual.

## Endpoints

Todas as rotas abaixo exigem JWT Bearer. A consulta pública significa uma
projeção com dados públicos, disponível nesta etapa para usuários autenticados.
O acesso de visitantes sem conta continua sendo uma decisão pendente na
seção 15 do PDF e na seção 11 do PRD; exigir login não contraria uma decisão
já fechada nesses documentos.

| Método | Rota | Resposta |
| --- | --- | --- |
| GET | `/api/me` | Dados do titular, e-mail e preferências |
| PATCH | `/api/me/profile` | Perfil atualizado do titular |
| GET | `/api/users/{username}` | Apenas username, displayName, bio e avatarUrl |

`GET /api/me` continua sendo uma única rota, agora no controller de perfis.
O nome de exibição é devolvido como `displayName` também no cadastro.
A consulta por username usa a mesma normalização de nomes do Identity.

## Edição parcial

Envie JSON e os cabeçalhos `Authorization: Bearer ...` e `X-Somnia-Client: web`,
por HTTPS. O usuário alterado é sempre obtido do JWT; não é aceito um ID no corpo.

```json
{
  "displayName": "Ouvinte Noturno",
  "bio": "Meus discos favoritos e novas descobertas.",
  "defaultScoreVisibility": "private",
  "defaultReviewVisibility": "public"
}
```

É possível enviar somente um dos campos. Campos omitidos permanecem inalterados.
`bio: null` ou uma bio em branco remove a bio; não enviar bio preserva seu valor.
Nome nulo/vazio, visibilidade nula/desconhecida, corpo vazio e campos não
permitidos retornam `400` com ProblemDetails.

Limites adotados: nome de exibição de 1 a 256 caracteres e bio de até 300.
Os campos são texto simples; o frontend deve exibi-los com escape, sem interpretá-los
como HTML. Nome e bio são públicos na consulta por username.

As preferências aceitam `private` e `public`, independentemente uma da outra.
Novos usuários e registros já existentes começam com ambas privadas. Essas
preferências servirão de padrão para avaliações futuras; não representam uma
permissão para expor avaliações já existentes.

Não há edição de e-mail, senha ou username por esse endpoint. Dados privados do
Identity e preferências não são selecionados pela consulta pública nem enviados
ao cliente. Todas as respostas de perfil usam `Cache-Control: no-store`.

## Avatar

O contrato reserva `avatarUrl`, inicialmente nulo. Upload e alteração do avatar
ficaram para a etapa seguinte; URLs externas não são aceitas no PATCH.
O upload futuro precisará validar imagem estática, formato, tamanho e propriedade
do arquivo antes de preencher esse campo.
GIFs de avatar pertencem à fase Premium descrita no PRD v1.1 e estão fora
do escopo da especificação técnica do MVP gratuito.

## O que ainda falta

- Edição do avatar estático para completar o contrato de RF02.
- Avaliações públicas no perfil, descritas na seção 8 do PDF, dependem do
  módulo de avaliações e ainda não possuem endpoint implementado.
- Seguir, deixar de seguir, feed e descoberta (RF08/RF09) também não estão
  implementados; a consulta de perfil existente não entrega esses recursos.
- Confirmar os limites adotados: bio de 300 segue a sugestão do PDF; nome de
  exibição de 256 é uma escolha da implementação, sem limite definido no PDF.

## Validação

A migration `AdicionarPerfis` cria os campos e os padrões privados. Os testes
unitários cobrem validação e distinção entre campos omitidos e nulos. O teste
HTTPS/SQL em `tests/security/auth_security_smoke.py` usa dois usuários e verifica
titularidade, normalização do username, edição parcial, preferências privadas e
ausência de dados privados nas respostas públicas.

Na revisão de 10/11/2026, passaram os 32 testes xUnit do projeto e o teste
integrado HTTPS/SQL. Não há validação de upload, avaliações públicas ou
relações sociais, pois esses recursos ainda não foram implementados.
