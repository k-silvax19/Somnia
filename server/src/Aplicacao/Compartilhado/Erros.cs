using FluentResults;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Aplicacao.Compartilhado;

public static class Erros
{
    public static Error Criar(TipoErro tipo, string mensagem, string? campo = null)
    {
        var erro = new Error(mensagem).WithMetadata(nameof(TipoErro), tipo);

        if (campo is not null)
            erro.WithMetadata("Campo", campo);

        return erro;
    }

    public static IEnumerable<Error> Validacao(IEnumerable<ErroValidacao> erros)
    {
        return erros.Select(e => Criar(TipoErro.Validacao, e.Mensagem, e.Campo));
    }
}
