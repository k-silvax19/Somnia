using Moq;
using Somnia.Aplicacao.Modulos.Avaliacoes;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Avaliacoes;
using Xunit;

namespace Somnia.Tests.Modulos.Avaliacoes;

public sealed class ComunidadeTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(51, 0)]
    [InlineData(20, -1)]
    [InlineData(20, 10001)]
    public async Task PaginaInvalida_NaoConsultaFeedOuDescoberta(int limit, int offset)
    {
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        var feed = await new ListarFeedQueryHandler(repositorio.Object)
            .Handle(new(Guid.NewGuid(), limit, offset), CancellationToken.None);
        var descoberta = await new ListarDescobertaQueryHandler(repositorio.Object)
            .Handle(new(limit, offset), CancellationToken.None);
        Assert.Equal(TipoErro.Validacao, feed.Errors[0].Metadata[nameof(TipoErro)]);
        Assert.Equal(TipoErro.Validacao, descoberta.Errors[0].Metadata[nameof(TipoErro)]);
        repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Feed_UsaIdentidadeDoVisitanteSemConsultarDescoberta()
    {
        var usuario = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var pagina = new PaginaAvaliacoesDto([], 10, 5, 0);
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        repositorio.Setup(x => x.ListarFeedAsync(usuario, 10, 5, ct)).ReturnsAsync(pagina);
        var resultado = await new ListarFeedQueryHandler(repositorio.Object).Handle(new(usuario, 10, 5), ct);
        Assert.Equal(pagina, resultado.Value);
        repositorio.VerifyAll();
    }

    [Fact]
    public async Task Descoberta_Vazia_RetornaPaginaSemErro()
    {
        var pagina = new PaginaAvaliacoesDto([], 20, 0, 0);
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        repositorio.Setup(x => x.ListarDescobertaAsync(20, 0, CancellationToken.None)).ReturnsAsync(pagina);
        var resultado = await new ListarDescobertaQueryHandler(repositorio.Object)
            .Handle(new(20, 0), CancellationToken.None);
        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value.Items);
        Assert.False(resultado.Value.HasMore);
    }
}
