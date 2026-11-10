using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Aplicacao.Modulos.Perfis;

public sealed record SolicitarAvatarCommand(Guid UsuarioId, byte[] Arquivo) : IRequest<Result<ProcessamentoAvatarDto>>;

public sealed class SolicitarAvatarCommandHandler(IRepositorioAvatar repositorio)
    : IRequestHandler<SolicitarAvatarCommand, Result<ProcessamentoAvatarDto>>
{
    public async Task<Result<ProcessamentoAvatarDto>> Handle(SolicitarAvatarCommand command, CancellationToken ct)
    {
        if (command.Arquivo.Length is 0 or > LimitesAvatar.TamanhoMaximo)
            return Result.Fail<ProcessamentoAvatarDto>(Erros.Criar(TipoErro.Validacao,
                "Envie uma imagem PNG ou JPEG de até 2 MiB.", "file"));
        try
        {
            var solicitacao = await repositorio.SolicitarAsync(command.UsuarioId, command.Arquivo, ct);
            return solicitacao is null
                ? Result.Fail<ProcessamentoAvatarDto>(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."))
                : Result.Ok(solicitacao);
        }
        catch (LimiteProcessamentosAvatarException)
        {
            return Result.Fail<ProcessamentoAvatarDto>(Erros.Criar(TipoErro.Conflito,
                "A fila de imagens está cheia. Tente novamente mais tarde."));
        }
    }
}

public sealed record RemoverAvatarCommand(Guid UsuarioId) : IRequest<Result>;

public sealed class RemoverAvatarCommandHandler(IRepositorioAvatar repositorio) : IRequestHandler<RemoverAvatarCommand, Result>
{
    public async Task<Result> Handle(RemoverAvatarCommand command, CancellationToken ct)
    {
        return await repositorio.RemoverAsync(command.UsuarioId, ct)
            ? Result.Ok()
            : Result.Fail(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."));
    }
}

public sealed record ObterStatusAvatarQuery(Guid UsuarioId, Guid SolicitacaoId) : IRequest<Result<ProcessamentoAvatarDto>>;

public sealed class ObterStatusAvatarQueryHandler(IRepositorioAvatar repositorio)
    : IRequestHandler<ObterStatusAvatarQuery, Result<ProcessamentoAvatarDto>>
{
    public async Task<Result<ProcessamentoAvatarDto>> Handle(ObterStatusAvatarQuery query, CancellationToken ct)
    {
        var status = await repositorio.ObterStatusAsync(query.UsuarioId, query.SolicitacaoId, ct);
        return status is null
            ? Result.Fail<ProcessamentoAvatarDto>(Erros.Criar(TipoErro.NaoEncontrado, "Solicitação inexistente."))
            : Result.Ok(status);
    }
}

public sealed record ObterAvatarQuery(string Username) : IRequest<Result<byte[]>>;

public sealed class ObterAvatarQueryHandler(IRepositorioAvatar repositorio) : IRequestHandler<ObterAvatarQuery, Result<byte[]>>
{
    public async Task<Result<byte[]>> Handle(ObterAvatarQuery query, CancellationToken ct)
    {
        var imagem = await repositorio.ObterAsync(query.Username, ct);
        return imagem is null
            ? Result.Fail<byte[]>(Erros.Criar(TipoErro.NaoEncontrado, "Avatar inexistente."))
            : Result.Ok(imagem);
    }
}
