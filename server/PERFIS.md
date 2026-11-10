# Perfis

Implementação baseada em RF02 e nas regras de privacidade da especificação do
Somnia. Mantém SQL Server, as quatro camadas e rotas sem `/v1`.

## Endpoints

Todas as rotas abaixo exigem JWT Bearer. A consulta pública significa uma
projeção com dados públicos, disponível nesta etapa para usuários autenticados.

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

## Validação

A migration `AdicionarPerfis` cria os campos e os padrões privados. Os testes
unitários cobrem validação e distinção entre campos omitidos e nulos. O teste
HTTPS/SQL em `tests/security/auth_security_smoke.py` usa dois usuários e verifica
titularidade, normalização do username, edição parcial, preferências privadas e
ausência de dados privados nas respostas públicas.
