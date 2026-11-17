using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Somnia.Aplicacao.Modulos.Catalogo;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Catalogo;
using Somnia.Infraestrutura.Modulos.Catalogo;
using Xunit;

namespace Somnia.Tests.Modulos.Catalogo;

public sealed class CatalogoTests
{
    [Theory]
    [InlineData(null, "album", 10, 0)]
    [InlineData("   ", "album", 10, 0)]
    [InlineData("Somnia", null, 10, 0)]
    [InlineData("Somnia", "artist", 10, 0)]
    [InlineData("Somnia", "album,track", 10, 0)]
    [InlineData("Somnia", "album", 0, 0)]
    [InlineData("Somnia", "album", 11, 0)]
    [InlineData("Somnia", "album", 10, -1)]
    [InlineData("Somnia", "album", 10, 1001)]
    public async Task PesquisaInvalida_NaoConsultaProvedor(string? q, string? type, int limit, int offset)
    {
        var provider = new Mock<IMusicCatalogProvider>(MockBehavior.Strict);
        var result = await new PesquisarCatalogoQueryHandler(provider.Object)
            .Handle(new(q, type, limit, offset), CancellationToken.None);

        Assert.Equal(TipoErro.Validacao, result.Errors[0].Metadata[nameof(TipoErro)]);
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PesquisaAcimaDoLimite_NaoConsultaProvedor()
    {
        var provider = new Mock<IMusicCatalogProvider>(MockBehavior.Strict);
        var result = await new PesquisarCatalogoQueryHandler(provider.Object)
            .Handle(new(new string('a', 201), "album"), CancellationToken.None);

        Assert.True(result.IsFailed);
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Pesquisa_NormalizaTextoETipo_EPreservaPaginacaoECancelamento()
    {
        using var cancelamento = new CancellationTokenSource();
        var provider = new Mock<IMusicCatalogProvider>(MockBehavior.Strict);
        var pagina = new PaginaCatalogoDto([], 2, 3, 3);
        provider.Setup(x => x.PesquisarAsync("Somnia", TipoItemCatalogo.Faixa, 2, 3, cancelamento.Token))
            .ReturnsAsync(pagina);

        var result = await new PesquisarCatalogoQueryHandler(provider.Object)
            .Handle(new("  Somnia  ", " TRACK ", 2, 3), cancelamento.Token);

        Assert.Same(pagina, result.Value);
        provider.VerifyAll();
    }

    [Theory]
    [InlineData(TipoItemCatalogo.Album, "album", 2)]
    [InlineData(TipoItemCatalogo.Faixa, "track", 6)]
    public async Task Simulado_FiltraTipoAntesDePaginar_OrdenaSemDuplicar(TipoItemCatalogo tipo, string esperado, int total)
    {
        var provider = new ProvedorCatalogoSimulado();
        var primeira = await provider.PesquisarAsync("somnia", tipo, 1, 0, CancellationToken.None);
        var segunda = await provider.PesquisarAsync("SOMNIA", tipo, 1, 1, CancellationToken.None);
        var completa = await provider.PesquisarAsync("Somnia", tipo, 10, 0, CancellationToken.None);

        Assert.Equal(total, primeira.Total);
        Assert.Single(primeira.Items);
        Assert.True(primeira.HasMore);
        Assert.NotEqual(primeira.Items[0].ExternalId, segunda.Items[0].ExternalId);
        Assert.Equal(completa.Items[0], primeira.Items[0]);
        Assert.Equal(completa.Items[1], segunda.Items[0]);
        Assert.All(completa.Items, item =>
        {
            Assert.Equal(esperado, item.Type);
            Assert.Equal("simulated", item.Provider);
            Assert.Null(item.CoverUrl);
            Assert.Null(item.SpotifyUrl);
        });
    }

    [Fact]
    public async Task Simulado_BuscaPorTitulo_EPaginaSemResultadosNaoVira404()
    {
        var provider = new ProvedorCatalogoSimulado();
        var album = await provider.PesquisarAsync("aurora", TipoItemCatalogo.Album, 10, 0, CancellationToken.None);
        var vazio = await provider.PesquisarAsync("nao-existe", TipoItemCatalogo.Album, 10, 0, CancellationToken.None);
        var fim = await provider.PesquisarAsync("Somnia", TipoItemCatalogo.Faixa, 10, 1000, CancellationToken.None);

        Assert.Equal("Aurora", Assert.Single(album.Items).Title);
        Assert.Empty(vazio.Items);
        Assert.Equal(0, vazio.Total);
        Assert.False(vazio.HasMore);
        Assert.Empty(fim.Items);
        Assert.Equal(6, fim.Total);
        Assert.False(fim.HasMore);
    }

    [Fact]
    public async Task AlbumEFaixas_PreservamOrdemETemVinculoReverso()
    {
        var provider = new ProvedorCatalogoSimulado();
        var album = await provider.ObterAlbumAsync("somnia-demo-album-aurora", CancellationToken.None);
        Assert.NotNull(album);
        Assert.Equal("album", album.Type);
        Assert.Equal(new[] { 1, 2, 3 }, album.Tracks.Select(x => x.TrackNumber));

        foreach (var item in album.Tracks)
        {
            var faixa = await provider.ObterFaixaAsync(item.ExternalId, CancellationToken.None);
            Assert.NotNull(faixa);
            Assert.Equal("track", faixa.Type);
            Assert.Equal(album.ExternalId, faixa.Album.ExternalId);
            Assert.Equal("album", faixa.Album.Type);
            Assert.Equal(item.TrackNumber, faixa.TrackNumber);
            Assert.Null(await provider.ObterAlbumAsync(item.ExternalId, CancellationToken.None));
        }
        Assert.Null(await provider.ObterFaixaAsync(album.ExternalId, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad:id")]
    [InlineData("https://example.com")]
    [InlineData("id com espaco")]
    public async Task DetalheIdInvalido_NaoConsultaProvedor(string id)
    {
        var provider = new Mock<IMusicCatalogProvider>(MockBehavior.Strict);
        var album = await new ObterAlbumCatalogoQueryHandler(provider.Object).Handle(new(id), CancellationToken.None);
        var faixa = await new ObterFaixaCatalogoQueryHandler(provider.Object).Handle(new(id), CancellationToken.None);

        Assert.True(album.IsFailed);
        Assert.True(faixa.IsFailed);
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ItemAusente_RetornaNaoEncontrado()
    {
        var result = await new ObterAlbumCatalogoQueryHandler(new ProvedorCatalogoSimulado())
            .Handle(new("missing"), CancellationToken.None);
        Assert.Equal(TipoErro.NaoEncontrado, result.Errors[0].Metadata[nameof(TipoErro)]);
    }

    [Fact]
    public async Task ProvedorIndisponivel_ProduzFalhaControladaNasTresConsultas()
    {
        var provider = new ProvedorCatalogoIndisponivel();
        var busca = await new PesquisarCatalogoQueryHandler(provider).Handle(new("Somnia", "album"), CancellationToken.None);
        var album = await new ObterAlbumCatalogoQueryHandler(provider).Handle(new("album-id"), CancellationToken.None);
        var faixa = await new ObterFaixaCatalogoQueryHandler(provider).Handle(new("track-id"), CancellationToken.None);

        Assert.Equal(TipoErro.Indisponivel, busca.Errors[0].Metadata[nameof(TipoErro)]);
        Assert.Equal(TipoErro.Indisponivel, album.Errors[0].Metadata[nameof(TipoErro)]);
        Assert.Equal(TipoErro.Indisponivel, faixa.Errors[0].Metadata[nameof(TipoErro)]);
    }

    [Fact]
    public async Task Cancelamento_NaoViraErroDeIndisponibilidade()
    {
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PesquisarCatalogoQueryHandler(new ProvedorCatalogoSimulado())
                .Handle(new("Somnia", "album"), cancelamento.Token));
    }

    [Fact]
    public void Configuracao_NaoHabilitaDadosFicticiosEmProducao()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MusicCatalog:Provider"] = "Simulated"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMusicCatalogServices(config, false));

        using var services = new ServiceCollection()
            .AddMusicCatalogServices(new ConfigurationBuilder().Build(), false).BuildServiceProvider();
        Assert.IsType<ProvedorCatalogoIndisponivel>(services.GetRequiredService<IMusicCatalogProvider>());
    }
}
