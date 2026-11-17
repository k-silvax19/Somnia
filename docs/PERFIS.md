# Perfis

Implementação do perfil básico de RF02 e das regras de privacidade do Somnia.
Atualizada em 11/11/2026 conforme o PRD v1.2: avaliações e perfis são
consultáveis pelos participantes autenticados, sem privacidade por campo.

O perfil permite edição de nome, bio e avatar estático.
O avatar usa endpoints próprios de upload e remoção, em vez de receber
uma URL no PATCH de perfil proposto pelo PDF.
O projeto usa SQL Server, quatro camadas e rotas sem `/v1`; PostgreSQL e
`/api/v1` são propostas do PDF que diferem da implementação atual.

## Endpoints

Todas as rotas abaixo exigem JWT Bearer. A consulta pública significa uma
projeção com dados públicos, disponível nesta etapa para usuários autenticados.
O piloto fecha a decisão que estava aberta na seção 15 do PDF e na seção 11
do PRD: visitantes sem conta não consultam perfis nem avatares. A exigência
de autenticação é explícita no controller; detalhes em [CADASTRO.md](CADASTRO.md).

| Método | Rota | Resposta |
| --- | --- | --- |
| GET | `/api/me` | Dados do titular e e-mail |
| PATCH | `/api/me/profile` | Perfil atualizado do titular |
| GET | `/api/users/{username}` | Apenas username, displayName, bio e avatarUrl |
| GET | `/api/users/{username}/reviews` | Histórico paginado de avaliações |
| PUT | `/api/me/avatar` | Solicitação de processamento assíncrono (`202`) |
| DELETE | `/api/me/avatar` | Remove o avatar do titular (`204`) |
| GET | `/api/me/avatar/requests/{solicitacaoId}` | Status privado da solicitação (`200`) |
| GET | `/api/users/{username}/avatar` | Imagem PNG (`200`) ou `404` se ausente |

`GET /api/me` continua sendo uma única rota, agora no controller de perfis.
O nome de exibição é devolvido como `displayName` também no cadastro.
A consulta por username usa a mesma normalização de nomes do Identity.

## Edição parcial

Envie JSON e os cabeçalhos `Authorization: Bearer ...` e `X-Somnia-Client: web`,
por HTTPS. O usuário alterado é sempre obtido do JWT; não é aceito um ID no corpo.

```json
{
  "displayName": "Ouvinte Noturno",
  "bio": "Meus discos favoritos e novas descobertas."
}
```

É possível enviar somente um dos campos. Campos omitidos permanecem inalterados.
`bio: null` ou uma bio em branco remove a bio; não enviar bio preserva seu valor.
Nome nulo/vazio, corpo vazio e campos não
permitidos retornam `400` com ProblemDetails.

Limites adotados: nome de exibição de 1 a 256 caracteres e bio de até 300.
Os campos são texto simples; o frontend deve exibi-los com escape, sem interpretá-los
como HTML. Nome e bio são públicos na consulta por username.

As preferências `defaultScoreVisibility` e `defaultReviewVisibility` foram
retiradas do contrato e do banco na migration `AdicionarAvaliacoes`. Enviá-las
no PATCH retorna `400`. Avaliações seguem o acesso autenticado do perfil,
conforme o PRD v1.2; detalhes em [AVALIACOES.md](AVALIACOES.md).

Não há edição de e-mail, senha ou username por esse endpoint. Dados privados do
Identity não são selecionados pela consulta pública nem enviados
ao cliente. Todas as respostas de perfil usam `Cache-Control: no-store`.

## Avatar

Envie `multipart/form-data` com o campo `file` para `PUT /api/me/avatar`.
Use HTTPS, JWT Bearer e `X-Somnia-Client: web`; as regras de origem são as
mesmas do PATCH. Não defina manualmente o `Content-Type` ao usar `FormData`
no navegador: ele precisa incluir o boundary gerado pelo cliente.

```javascript
const form = new FormData();
form.append('file', arquivoSelecionado);
const resposta = await fetch('https://localhost:7201/api/me/avatar', {
  method: 'PUT',
  credentials: 'include',
  headers: {
    Authorization: `Bearer ${accessToken}`,
    'X-Somnia-Client': 'web'
  },
  body: form
});
```

O upload valida, normaliza e grava a imagem durante a própria requisição, sem
RabbitMQ ou Worker. O sucesso retorna `200 OK` com ID, `status: "completed"`
e `erro: null`; conteúdo inválido retorna `status: "rejected"` e preserva o
avatar anterior. A rota `GET /api/me/avatar/requests/{id}` permanece disponível
para consultar solicitações antigas ainda em processamento.

O tamanho do arquivo é limitado a 2 MiB pela API (`400` se excedido).
O limite total da requisição multipart é 2 MiB + 64 KiB para o envelope;
ultrapassá-lo também pode ser rejeitado pela infraestrutura HTTP. Uploads
têm limite de dez chamadas/minuto por usuário e instância (`429`). Não há
fila de processamento para novas imagens.

O processador exige conteúdo real PNG ou JPEG estático e dimensões de até
2048 × 2048 pixels. Extensão e MIME declarados não aprovam um arquivo.
GIF, SVG, APNG, arquivos corrompidos e dimensões excessivas resultam em
status `rejected` com a mensagem de validação no resultado do PUT.

A imagem é decodificada com SkiaSharp e regravada em PNG de até 512 × 512,
preservando proporção, sem ampliar imagens pequenas e sem copiar metadados
ou conteúdo anexado. O original e o nome do arquivo não são armazenados.
A imagem normalizada fica na tabela `Avatares`, vinculada ao titular por
chave estrangeira; consultas comuns de perfil não carregam seus bytes.

`avatarUrl` aponta para `/api/users/{username}/avatar`. Essa rota exige JWT,
assim como as demais consultas de perfil: o frontend deve buscar o conteúdo
com Bearer e criar uma URL de objeto para exibição quando necessário.
Um `<img src="...">` sozinho não envia o Bearer mantido em memória.
A resposta usa `image/png`, `X-Content-Type-Options: nosniff` e `no-store`.

O avatar anterior permanece intacto se a validação falhar. Uploads válidos
substituem a imagem na mesma transação SQL. Solicitações antigas canceladas
não podem sobrescrever uma imagem mais recente nem recriar uma removida.
URLs externas continuam rejeitadas no PATCH.

O processamento novo não depende de mensageria. [MENSAGERIA.md](MENSAGERIA.md)
documenta o Worker legado que pode concluir mensagens antigas.
GIFs de avatar pertencem à fase Premium descrita no PRD v1.1 e estão fora
do escopo da especificação técnica do MVP gratuito.

## O que ainda falta

- Histórico de avaliações já possui endpoint próprio, descrito em
  [AVALIACOES.md](AVALIACOES.md); sua apresentação no frontend está pendente.
- Seguir/deixar de seguir, estado e listas têm endpoints próprios descritos em
  [RELACOES.md](RELACOES.md). Feed e descoberta estão descritos em
  [COMUNIDADE.md](COMUNIDADE.md); a interface permanece pendente.

Os limites adotados nesta etapa são bio de até 300 e nome de exibição de
1 a 256 caracteres. O PDF sugere o primeiro; o segundo é uma escolha de
implementação, registrada em [CADASTRO.md](CADASTRO.md).

## Validação

A migration histórica `AdicionarPerfis` criou os campos e padrões privados;
`AdicionarAvaliacoes` remove as preferências que saíram do escopo. Os testes
unitários cobrem validação e distinção entre campos omitidos e nulos. O teste
HTTPS/SQL em `tests/security/auth_security_smoke.py` usa dois usuários e verifica
titularidade, normalização do username, edição parcial, rejeição de preferências antigas e
ausência de dados privados nas respostas públicas.

A suíte possui 176 testes xUnit. A migration
`AdicionarAvatares` cria a imagem publicada; `ProcessarAvataresAssincronos`
adiciona solicitações, versão atual do avatar e tabelas de outbox.

A validação inclui PNG/JPEG reais, limites, redução proporcional, conteúdo
inválido, imagem truncada, GIF/APNG, metadados, acesso autenticado, titularidade,
substituição, remoção repetida e uploads concorrentes. O teste integrado
também cobre o broker indisponível, reinícios e mensagens duplicadas/antigas,
conforme [MENSAGERIA.md](MENSAGERIA.md). Avaliações foram implementadas em
11/11/2026; seguir perfis, feed e descoberta também foram implementados.
Frontend e homologação completa do MVP permanecem pendentes.
