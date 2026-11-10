# Contrato de erros HTTP

As respostas de erro produzidas pela API usam `application/problem+json`
e `Cache-Control: no-store`. O contrato inclui:

```json
{
  "type": "urn:somnia:problem:http:400",
  "title": "Requisição inválida",
  "status": 400,
  "detail": "Verifique os dados enviados e os erros de validação por campo.",
  "instance": "/api/auth/register",
  "traceId": "identificador-da-requisicao",
  "errors": {
    "username": ["Username deve ter de 3 a 30 caracteres."]
  }
}
```

`type` identifica a categoria HTTP por URI `urn:somnia:problem:http:{status}`;
não é um endpoint de consulta. `status` corresponde ao código HTTP.
`title` e `detail` explicam o erro. `instance` contém somente o caminho da
requisição, sem query string. `traceId` permite correlacionar a falha com os
logs. `errors` existe nas respostas de validação com mensagens por campo;
nomes do contrato JSON são usados na validação automática.

| Código | Uso |
| --- | --- |
| `400` | Dados, JSON ou formulário inválidos |
| `401` | Token ausente, inválido, expirado ou sessão encerrada; credenciais inválidas |
| `403` | Origem, cabeçalho de proteção ou CSRF recusados |
| `404` | Recurso inexistente ou não acessível ao titular |
| `405` | Método HTTP não disponível no endereço |
| `409` | Duplicidade ou conflito com o estado atual |
| `413` | Corpo da requisição acima do limite permitido |
| `415` | Content-Type não suportado pela operação |
| `429` | Limite de chamadas excedido, com `Retry-After` em segundos quando informado pelo limitador |
| `500` | Falha interna com mensagem genérica |

A padronização alcança erros dos casos de uso, validação automática do MVC,
filtros de origem/CSRF, autenticação, autorização, rate limiting, rotas e
métodos não encontrados e exceções capturadas pelo middleware da API.
O desafio Bearer preserva `WWW-Authenticate`; detalhes da validação do JWT
não são enviados ao cliente. Respostas `500` não incluem stack trace,
consultas SQL, senhas ou mensagens internas da exceção.

Falhas anteriores à execução da API, como recusa pelo proxy, encerramento da
conexão TLS ou requisição HTTP ilegível, podem não ter corpo JSON. Um cliente
que continua enviando um upload após a rejeição antecipada pode observar
uma conexão encerrada em vez de conseguir ler o `413`. O limite não deve
ser removido para acomodar esse comportamento do cliente.

No frontend, use o status HTTP e `type` para decidir o comportamento, mostre
as mensagens de validação e preserve `traceId` ao relatar uma falha. Evite
comparar textos de `title` ou `detail` para controlar o fluxo.
