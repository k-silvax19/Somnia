# Preparação do piloto

O frontend e a API já cobrem os fluxos funcionais do MVP. A publicação depende
de escolher hospedagem, acesso ao cadastro e políticas operacionais. Este
documento registra a preparação técnica sem aplicar mudanças a um banco real.

## Migrations do SQL Server

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
participantes reais, verificar o fluxo completo com HTTPS, SQL Server,
RabbitMQ/Worker e Spotify no ambiente de destino, incluindo upload de avatar,
falhas externas, backup e restauração.
