using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Aplicacao.Modulos.Perfis;

// Null remove o avatar; um arquivo vazio é inválido.
public sealed record AtualizarAvatarCommand(Guid UsuarioId, byte[]? Arquivo) : IRequest<Result<UsuarioDto>>;

public sealed class AtualizarAvatarCommandHandler(IRepositorioAvatar repositorio, IProcessadorAvatar processador)
    : IRequestHandler<AtualizarAvatarCommand, Result<UsuarioDto>>
{
    public async Task<Result<UsuarioDto>> Handle(AtualizarAvatarCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        byte[]? imagem = null;
        if (command.Arquivo is not null)
        {
            if (command.Arquivo.Length is 0 or > LimitesAvatar.TamanhoMaximo)
                return Result.Fail<UsuarioDto>(Erros.Criar(TipoErro.Validacao,
                    "Envie uma imagem PNG ou JPEG de até 2 MiB.", "file"));
            try
            {
                imagem = processador.Normalizar(command.Arquivo);
            }
            catch (ArgumentException ex)
            {
                return Result.Fail<UsuarioDto>(Erros.Criar(TipoErro.Validacao, ex.Message, "file"));
            }
        }

        var usuario = await repositorio.SalvarAsync(command.UsuarioId, imagem, ct);
        return usuario is null
            ? Result.Fail<UsuarioDto>(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."))
            : Result.Ok(usuario);
    }
}

public sealed record ObterAvatarQuery(string Username) : IRequest<Result<byte[]>>;

public sealed class ObterAvatarQueryHandler(IRepositorioAvatar repositorio)
    : IRequestHandler<ObterAvatarQuery, Result<byte[]>>
{
    public async Task<Result<byte[]>> Handle(ObterAvatarQuery query, CancellationToken ct)
    {
        var imagem = await repositorio.ObterAsync(query.Username, ct);
        return imagem is null
            ? Result.Fail<byte[]>(Erros.Criar(TipoErro.NaoEncontrado, "Avatar inexistente."))
            : Result.Ok(imagem);
    }
}
