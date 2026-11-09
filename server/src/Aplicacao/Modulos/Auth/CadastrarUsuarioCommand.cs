using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Aplicacao.Modulos.Auth;

public sealed record CadastrarUsuarioCommand(string? Nome, string? Email, string? Senha, string? Username = null)
    : IRequest<Result<UsuarioDto>>;

public sealed class CadastrarUsuarioCommandHandler(IGerenciadorIdentidade identidade)
    : IRequestHandler<CadastrarUsuarioCommand, Result<UsuarioDto>>
{
    public async Task<Result<UsuarioDto>> Handle(CadastrarUsuarioCommand command, CancellationToken ct)
    {
        var erros = new List<Error>();

        if (string.IsNullOrWhiteSpace(command.Nome))
        {
            erros.Add(Erros.Criar(TipoErro.Validacao, "Nome é obrigatório.", "nome"));
        }

        if (string.IsNullOrWhiteSpace(command.Email)
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(command.Email))
        {
            erros.Add(Erros.Criar(TipoErro.Validacao, "Informe um e-mail válido.", "email"));
        }

        if (string.IsNullOrEmpty(command.Senha)
            || command.Senha.Length < 8
            || !command.Senha.Any(char.IsDigit)
            || !command.Senha.Any(caractere => !char.IsLetterOrDigit(caractere)))
        {
            erros.Add(Erros.Criar(
                TipoErro.Validacao,
                "A senha deve ter pelo menos 8 caracteres, incluindo um número e um caractere especial.",
                "senha"));
        }

        if (erros.Count > 0)
        {
            return Result.Fail<UsuarioDto>(erros);
        }

        try
        {
            var usuario = command.Username is null
                ? await identidade.CadastrarAsync(command.Nome!, command.Email!, command.Senha!, ct)
                : await identidade.CadastrarComUsernameAsync(
                    command.Nome!, command.Email!, command.Senha!, command.Username, ct);

            return Result.Ok(usuario);
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_DUPLICADO")
        {
            return Result.Fail<UsuarioDto>(
                Erros.Criar(TipoErro.Conflito, "O e-mail já está cadastrado.", "email"));
        }
        catch (InvalidOperationException ex) when (ex.Message == "USERNAME_DUPLICADO")
        {
            return Result.Fail<UsuarioDto>(
                Erros.Criar(TipoErro.Conflito, "O username j? est? cadastrado.", "username"));
        }
        catch (ArgumentException ex)
        {
            return Result.Fail<UsuarioDto>(Erros.Criar(TipoErro.Validacao, ex.Message, "senha"));
        }
    }
}
