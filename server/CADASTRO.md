# Cadastro e acesso no piloto

Política adotada nesta etapa para fechar as decisões que estavam abertas
na especificação técnica. Aplica-se aos novos cadastros do MVP gratuito.

| Campo ou operação | Regra |
| --- | --- |
| `username` | Obrigatório, de 3 a 30 caracteres; letras ASCII, números, `.`, `-` e `_` |
| Unicidade do username | Verificação normalizada pelo Identity, sem distinguir maiúsculas/minúsculas |
| `displayName` | Obrigatório, de 1 a 256 caracteres; espaços nas pontas são removidos ao persistir |
| E-mail | Obrigatório e válido; espaços nas pontas são removidos e a unicidade é verificada pelo Identity |
| Senha | Pelo menos oito caracteres, um número e um caractere especial |
| Confirmação de e-mail | Não é obrigatória para entrar no piloto |
| Perfil e avatar de outro participante | Exigem JWT, mesmo exibindo somente dados públicos |
| Dados do titular e status do avatar | Exigem o JWT do próprio titular |

O cadastro recebe `email`, `username`, `password` e `displayName`; retorna
`201` e os dados do titular. Para iniciar uma sessão é necessário fazer login.
Dados inválidos retornam `400`; duplicidade de e-mail ou username, `409`.
Os erros seguem [ERROS.md](ERROS.md), incluindo erros por campo.

A regra de username é aplicada no contrato HTTP, no caso de uso e na
integração com Identity. O e-mail não é usado como username alternativo.
Username e nome de exibição têm finalidades diferentes: o primeiro identifica
o endereço público; o segundo é texto de apresentação.

Os limites novos não renomeiam usuários existentes nem truncam dados no
banco. A coluna do Identity mantém sua capacidade atual para compatibilidade
com registros anteriores. Consultar um username existente não passa pela
validação de criação de conta.

No piloto, `EmailConfirmed` pode permanecer falso e o usuário pode entrar
com credenciais válidas. Não há envio nem endpoint de confirmação de e-mail;
o sistema não afirma que o endereço pertence à pessoa cadastrada. Tornar a
confirmação obrigatória exigirá fluxo de envio, tokens, reenvio e tratamento
das contas existentes antes de mudar a política de login.

“Público” significa visível aos demais participantes autenticados. Não há
consulta anônima nem decisão de indexação na internet. O controller de perfis
declara autorização explicitamente; e-mail, credenciais e status de
processamento não entram na projeção pública.

Continuam pendentes a política de exclusão de conta, retenção de dados e
eventual controle por convites. Essas decisões não são inferidas das regras
de cadastro desta etapa.
