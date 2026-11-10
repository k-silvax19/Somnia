using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Aplicacao.Modulos.Perfis;

public sealed record AtualizarPerfilCommand(Guid UsuarioId, AlteracaoPerfil Alteracao) : IRequest<Result<UsuarioDto>>;

public sealed class AtualizarPerfilCommandHandler(IRepositorioPerfil repositorio)
    : IRequestHandler<AtualizarPerfilCommand, Result<UsuarioDto>>
{
    public async Task<Result<UsuarioDto>> Handle(AtualizarPerfilCommand command, CancellationToken ct)
    {
        var dados = command.Alteracao;
        var erros = new List<Error>();

        if (!dados.AlterarDisplayName && !dados.AlterarBio && !dados.AlterarScoreVisibility && !dados.AlterarReviewVisibility)
            erros.Add(Erros.Criar(TipoErro.Validacao, "Informe ao menos um campo para alterar.", "profile"));
        if (dados.AlterarDisplayName && (string.IsNullOrWhiteSpace(dados.DisplayName) || dados.DisplayName.Length > 256))
            erros.Add(Erros.Criar(TipoErro.Validacao, "Nome deve ter entre 1 e 256 caracteres.", "displayName"));
        if (dados.AlterarBio && dados.Bio?.Length > 300)
            erros.Add(Erros.Criar(TipoErro.Validacao, "Bio deve ter no maximo 300 caracteres.", "bio"));
        if (dados.AlterarScoreVisibility && dados.DefaultScoreVisibility is not ("private" or "public"))
            erros.Add(Erros.Criar(TipoErro.Validacao, "Use private ou public.", "defaultScoreVisibility"));
        if (dados.AlterarReviewVisibility && dados.DefaultReviewVisibility is not ("private" or "public"))
            erros.Add(Erros.Criar(TipoErro.Validacao, "Use private ou public.", "defaultReviewVisibility"));

        if (erros.Count > 0)
            return Result.Fail<UsuarioDto>(erros);

        var perfil = await repositorio.AtualizarAsync(command.UsuarioId, dados, ct);
        if (perfil is null)
            return Result.Fail<UsuarioDto>(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."));

        return Result.Ok(perfil);
    }
}
