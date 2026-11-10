using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Somnia.Api.Compartilhado.Http;
using Xunit;

namespace Somnia.Tests.Compartilhado.Http;

public sealed class ApiProblemDetailsTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(405)]
    [InlineData(409)]
    [InlineData(413)]
    [InlineData(415)]
    [InlineData(429)]
    [InlineData(503)]
    public void Completar_GaranteContratoSemIncluirQueryString(int status)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/api/me";
        contexto.Request.QueryString = new QueryString("?token=segredo");
        var problem = new ProblemDetails { Status = status };

        ApiProblemDetails.Completar(contexto, problem);

        Assert.Equal($"urn:somnia:problem:http:{status}", problem.Type);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.False(string.IsNullOrWhiteSpace(problem.Extensions["traceId"]?.ToString()));
        Assert.Equal("/api/me", problem.Instance);
        Assert.Equal("no-store", contexto.Response.Headers.CacheControl);
    }

    [Fact]
    public void Completar_ErroInterno_NaoExibeDetalhesDaExcecao()
    {
        var problem = new ProblemDetails { Status = 500, Title = "SQL com senha", Detail = "Password=segredo" };

        ApiProblemDetails.Completar(new DefaultHttpContext(), problem);

        Assert.Equal("Erro interno do servidor", problem.Title);
        Assert.DoesNotContain("segredo", problem.Detail);
        Assert.Contains("traceId", problem.Detail);
    }

    [Fact]
    public void Completar_PreservaErrosDeValidacaoPorCampo()
    {
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["username"] = ["Nome de usuário inválido."]
        }) { Status = 400 };

        ApiProblemDetails.Completar(new DefaultHttpContext(), problem);

        Assert.Equal("Nome de usuário inválido.", problem.Errors["username"][0]);
        Assert.Equal(400, problem.Status);
    }
}
