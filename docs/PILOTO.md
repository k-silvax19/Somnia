# Preparação do piloto

O frontend e a API já cobrem os fluxos funcionais do MVP. A publicação depende
de escolher hospedagem, acesso ao cadastro e políticas operacionais. Este
documento registra a preparação técnica sem aplicar mudanças a um banco real.

## Migrations do PostgreSQL

A API aplica migrations automaticamente apenas em `Development`. Em produção,
gere um SQL idempotente para revisão antes da implantação:

```powershell
./server/scripts/gerar-script-migracoes.ps1 -OutputPath ./somnia-migrations.sql
```

O script não sobrescreve um arquivo existente. Revise as instruções geradas e
as migrations correspondentes no código. Antes de aplicar a um ambiente,
faça um backup restaurável, verifique o estado de `__EFMigrationsHistory`,
agende uma janela de manutenção e registre a versão da aplicação que será
publicada. A execução do SQL e um teste de restauração precisam ser definidos
para o provedor de hospedagem escolhido.

## GitHub Actions e Azure

`.github/workflows/deploy-api-azure.yml` publica somente a API no App Service.
Antes do primeiro deploy, configure estes secrets no repositório:

| Secret | Uso |
| --- | --- |
| `NEON_CONNECTION_STRING` | Connection string PostgreSQL/Npgsql do Neon, com SSL habilitado. É usada para aplicar migrations e configurar a API. |
| `JWT_SIGNING_KEY` | Chave aleatória com pelo menos 32 bytes. |
| `SPOTIFY_CLIENT_ID` | Client ID do aplicativo Spotify. |
| `SPOTIFY_CLIENT_SECRET` | Client secret do aplicativo Spotify. |
| `AZUREAPPSERVICE_CLIENTID_FBE2A7630DFA41A281D552C49CD934B3` | Identidade OIDC do App Service `somnia-9f22f300`. |
| `AZUREAPPSERVICE_TENANTID_293D7379EAC34AA6A4CB8788FDFB847F` | Tenant Azure para login OIDC. |
| `AZUREAPPSERVICE_SUBSCRIPTIONID_3613012762CC4326B1793A52A8DA8B29` | Assinatura Azure para login OIDC. |

O workflow localiza o resource group pelo nome do App Service após o login no
Azure. `CLIENT_ORIGIN` é uma variável opcional: configure-a quando o frontend
tiver um endereço HTTPS publicado, sem caminho. Sem ela, a API é publicada sem
origem CORS permitida. O deploy executa migrations no Neon antes de publicar a
API. O workflow do frontend continua fazendo build e verificações; ele não
publica arquivos em um host estático.

## Decisões para abrir o piloto

| Tema | Estado |
| --- | --- |
| Hospedagem, domínio, TLS e orçamento | A definir |
| Acesso ao cadastro (convites ou aberto) | A definir |
| Exclusão de conta e remoção de avaliações/relações | A definir |
| Retenção de backups e prazo de eliminação | A definir |
| Viabilidade das credenciais e quotas do Spotify no ambiente publicado | A validar |
| Grupo piloto, canal de feedback e responsável por retirada de conteúdo | A definir |

Nenhuma dessas decisões deve ser inferida do ambiente local. Antes de receber
participantes reais, verificar o fluxo completo com HTTPS, PostgreSQL,
RabbitMQ/Worker e Spotify no ambiente de destino, incluindo upload de avatar,
falhas externas, backup e restauração.
