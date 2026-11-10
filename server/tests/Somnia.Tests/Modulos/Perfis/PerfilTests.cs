using System.Text.Json;
using Moq;
using Somnia.Api.Modulos.Perfis;
using Somnia.Aplicacao.Modulos.Perfis;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;
using Xunit;

namespace Somnia.Tests.Modulos.Perfis;

public sealed class PerfilTests
{
    [Fact]
    public void Patch_DistingueCampoOmitidoDeBioNula()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var omitido = JsonSerializer.Deserialize<AtualizarPerfilRequest>("{}", options)!;
        var limpar = JsonSerializer.Deserialize<AtualizarPerfilRequest>("{\"bio\":null}", options)!;

        Assert.False(omitido.InformouBio);
        Assert.True(limpar.InformouBio);
        Assert.Null(limpar.ToAlteracao().Bio);
        Assert.False(limpar.InformouDisplayName);
        Assert.False(limpar.InformouScoreVisibility);
    }

    [Theory]
    [InlineData("{\"usuarioId\":\"outro\"}")]
    [InlineData("{\"email\":\"outro@example.com\"}")]
    [InlineData("{\"username\":\"outro\"}")]
    [InlineData("{\"avatarUrl\":\"https://example.com/animated.gif\"}")]
    public void Patch_RejeitaCamposQueNaoPodemSerAlterados(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtualizarPerfilRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"displayName\":null}")]
    [InlineData("{\"displayName\":\" \"}")]
    [InlineData("{\"defaultScoreVisibility\":\"friends\"}")]
    [InlineData("{\"defaultReviewVisibility\":null}")]
    public async Task Atualizar_ComDadosInvalidos_NaoPersiste(string json)
    {
        var request = JsonSerializer.Deserialize<AtualizarPerfilRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var repositorio = new Mock<IRepositorioPerfil>(MockBehavior.Strict);
        var handler = new AtualizarPerfilCommandHandler(repositorio.Object);

        var result = await handler.Handle(new AtualizarPerfilCommand(Guid.NewGuid(), request.ToAlteracao()), CancellationToken.None);

        Assert.True(result.IsFailed);
        repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Atualizar_BioAcimaDoLimite_NaoPersiste()
    {
        var repositorio = new Mock<IRepositorioPerfil>(MockBehavior.Strict);
        var handler = new AtualizarPerfilCommandHandler(repositorio.Object);
        var request = new AtualizarPerfilRequest { Bio = new string('a', 301) };

        var result = await handler.Handle(new AtualizarPerfilCommand(Guid.NewGuid(), request.ToAlteracao()), CancellationToken.None);

        Assert.True(result.IsFailed);
        repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Atualizar_PerfilInexistente_RetornaNaoEncontrado()
    {
        var repositorio = new Mock<IRepositorioPerfil>();
        var handler = new AtualizarPerfilCommandHandler(repositorio.Object);
        var request = new AtualizarPerfilRequest { Bio = "Minha bio" };

        var result = await handler.Handle(new AtualizarPerfilCommand(Guid.NewGuid(), request.ToAlteracao()), CancellationToken.None);

        Assert.Equal(TipoErro.NaoEncontrado, result.Errors[0].Metadata[nameof(TipoErro)]);
    }
}
