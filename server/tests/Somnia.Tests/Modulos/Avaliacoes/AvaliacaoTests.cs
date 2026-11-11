using System.Text.Json;
using Moq;
using Somnia.Api.Modulos.Avaliacoes;
using Somnia.Aplicacao.Modulos.Avaliacoes;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Avaliacoes;
using Somnia.Dominio.Modulos.Catalogo;
using Xunit;

namespace Somnia.Tests.Modulos.Avaliacoes;

public sealed class AvaliacaoTests
{
    private static readonly ReferenciaItem Item = new("spotify", "album", "album1");
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Theory]
    [InlineData(0, null)]
    [InlineData(100, null)]
    [InlineData(null, "  Uma resenha  ")]
    public void Criar_AceitaExtremosEResenhaSemNota(int? score, string? review)
    {
        var avaliacao = Avaliacao.Criar(Usuario, Item, score, review);
        Assert.Equal(score, avaliacao.Score);
        Assert.Equal(review?.Trim(), avaliacao.Review);
        Assert.Equal(Usuario, avaliacao.UsuarioId);
        Assert.Null(avaliacao.UpdatedAt);
        Assert.NotEqual(Guid.Empty, avaliacao.Id);
        Assert.Equal(DateTimeKind.Utc, avaliacao.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(-1, "Texto")]
    [InlineData(101, "Texto")]
    [InlineData(null, null)]
    [InlineData(null, " \t\n ")]
    public async Task Criar_ConteudoInvalido_NaoConsultaCatalogoNemBanco(int? score, string? review)
    {
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        var catalogo = new Mock<IMusicCatalogProvider>(MockBehavior.Strict);
        var resultado = await new CriarAvaliacaoCommandHandler(repositorio.Object, catalogo.Object)
            .Handle(new(Usuario, Item, score, review), Ct);
        Assert.Equal(TipoErro.Validacao, resultado.Errors[0].Metadata[nameof(TipoErro)]);
        repositorio.VerifyNoOtherCalls();
        catalogo.VerifyNoOtherCalls();
        Assert.Throws<ArgumentException>(() => Avaliacao.Criar(Usuario, Item, score, review));
    }

    [Theory]
    [InlineData(5000, true)]
    [InlineData(5001, false)]
    public void Resenha_RespeitaLimite(int tamanho, bool valido) =>
        Assert.Equal(valido, !RegrasAvaliacao.ValidarConteudo(null, new string('a', tamanho)).Any());

    [Theory]
    [InlineData("other", "album", "id")]
    [InlineData("spotify", "artist", "id")]
    [InlineData("spotify", "album", "bad:id")]
    [InlineData("spotify", "album", "")]
    [InlineData(null, null, null)]
    public async Task Criar_ReferenciaInvalida_NaoAcessaDependencias(string? provider, string? type, string? id)
    {
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        var catalogo = new Mock<IMusicCatalogProvider>(MockBehavior.Strict);
        var resultado = await new CriarAvaliacaoCommandHandler(repositorio.Object, catalogo.Object)
            .Handle(new(Usuario, new(provider!, type!, id!), 90, null), Ct);
        Assert.True(resultado.IsFailed);
    }

    [Fact]
    public async Task Criar_Duplicada_NaoDependeDoSpotify()
    {
        var repositorio = new Mock<IRepositorioAvaliacao>();
        repositorio.Setup(x => x.ExisteAsync(Usuario, Item, Ct)).ReturnsAsync(true);
        var catalogo = new Mock<IMusicCatalogProvider>(MockBehavior.Strict);
        var resultado = await new CriarAvaliacaoCommandHandler(repositorio.Object, catalogo.Object)
            .Handle(new(Usuario, Item, 90, null), Ct);
        Assert.Equal(TipoErro.Conflito, resultado.Errors[0].Metadata[nameof(TipoErro)]);
        catalogo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Criar_UsaReferenciaValidadaETrataConflitoConcorrente(bool conflito)
    {
        var repositorio = new Mock<IRepositorioAvaliacao>();
        Avaliacao? salva = null;
        repositorio.Setup(x => x.CriarAsync(It.IsAny<Avaliacao>(), Ct))
            .Callback<Avaliacao, CancellationToken>((a, _) => salva = a).ReturnsAsync(!conflito);
        repositorio.Setup(x => x.ObterAsync(It.IsAny<Guid>(), Ct))
            .ReturnsAsync(() => Dto(salva!.Id));
        var catalogo = Catalogo();
        var resultado = await new CriarAvaliacaoCommandHandler(repositorio.Object, catalogo.Object)
            .Handle(new(Usuario, Item, 90, " Texto "), Ct);
        Assert.Equal(Usuario, salva!.UsuarioId);
        Assert.Equal("Texto", salva.Review);
        if (conflito)
            Assert.Equal(TipoErro.Conflito, resultado.Errors[0].Metadata[nameof(TipoErro)]);
        else
            Assert.Equal(salva.Id, resultado.Value.Id);
    }

    [Theory]
    [InlineData("simulated", "album", "album1")]
    [InlineData("spotify", "track", "album1")]
    [InlineData("spotify", "album", "Album1")]
    public async Task Criar_NaoPersisteIdentidadeDiferenteDoCatalogo(string provider, string type, string id)
    {
        var repositorio = new Mock<IRepositorioAvaliacao>();
        var catalogo = new Mock<IMusicCatalogProvider>();
        catalogo.Setup(x => x.ObterAlbumAsync(Item.ExternalId, Ct)).ReturnsAsync(new AlbumCatalogoDto(
            new(provider, type, id, "Título", ["Artista"], null, null), []));
        var resultado = await new CriarAvaliacaoCommandHandler(repositorio.Object, catalogo.Object)
            .Handle(new(Usuario, Item, 90, null), Ct);
        Assert.Equal(TipoErro.Validacao, resultado.Errors[0].Metadata[nameof(TipoErro)]);
        repositorio.Verify(x => x.CriarAsync(It.IsAny<Avaliacao>(), Ct), Times.Never);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task Criar_FalhaDoCatalogo_NaoPersiste(int status)
    {
        var repositorio = new Mock<IRepositorioAvaliacao>();
        var catalogo = new Mock<IMusicCatalogProvider>();
        var setup = catalogo.Setup(x => x.ObterAlbumAsync(Item.ExternalId, Ct));
        if (status == 404) setup.ReturnsAsync((AlbumCatalogoDto?)null);
        else setup.ThrowsAsync(status == 502 ? new RespostaCatalogoInvalidaException() : new CatalogoIndisponivelException(15));
        var resultado = await new CriarAvaliacaoCommandHandler(repositorio.Object, catalogo.Object)
            .Handle(new(Usuario, Item, 90, null), Ct);
        Assert.Equal(status switch { 404 => TipoErro.NaoEncontrado, 502 => TipoErro.FalhaIntegracao, _ => TipoErro.Indisponivel },
            resultado.Errors[0].Metadata[nameof(TipoErro)]);
        if (status == 503) Assert.Equal(15, resultado.Errors[0].Metadata["RetryAfterSeconds"]);
        repositorio.Verify(x => x.CriarAsync(It.IsAny<Avaliacao>(), Ct), Times.Never);
    }

    [Fact]
    public async Task Atualizar_EncaminhaTitularELimpaResenhaSemConsultarCatalogo()
    {
        var id = Guid.NewGuid();
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        repositorio.Setup(x => x.AtualizarAsync(id, Usuario, 0, null, Ct)).ReturnsAsync(Dto(id));
        var resultado = await new AtualizarAvaliacaoCommandHandler(repositorio.Object).Handle(new(Usuario, id, 0, "  "), Ct);
        Assert.True(resultado.IsSuccess);
        repositorio.VerifyAll();
    }

    [Fact]
    public async Task Atualizar_Vazia_NaoPersiste()
    {
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        var resultado = await new AtualizarAvaliacaoCommandHandler(repositorio.Object)
            .Handle(new(Usuario, Guid.NewGuid(), null, null), Ct);
        Assert.Equal(TipoErro.Validacao, resultado.Errors[0].Metadata[nameof(TipoErro)]);
    }

    [Fact]
    public async Task AlterarOuExcluir_SemAutoria_RetornaNaoEncontrado()
    {
        var repositorio = new Mock<IRepositorioAvaliacao>();
        var id = Guid.NewGuid();
        var edicao = await new AtualizarAvaliacaoCommandHandler(repositorio.Object).Handle(new(Usuario, id, 90, null), Ct);
        var exclusao = await new ExcluirAvaliacaoCommandHandler(repositorio.Object).Handle(new(Usuario, id), Ct);
        Assert.Equal(TipoErro.NaoEncontrado, edicao.Errors[0].Metadata[nameof(TipoErro)]);
        Assert.Equal(TipoErro.NaoEncontrado, exclusao.Errors[0].Metadata[nameof(TipoErro)]);
        repositorio.Verify(x => x.AtualizarAsync(id, Usuario, 90, null, Ct), Times.Once);
        repositorio.Verify(x => x.ExcluirAsync(id, Usuario, Ct), Times.Once);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(51, 0)]
    [InlineData(20, -1)]
    [InlineData(20, 10001)]
    public async Task Consultas_PaginacaoInvalida_NaoAcessamBanco(int limit, int offset)
    {
        var repositorio = new Mock<IRepositorioAvaliacao>(MockBehavior.Strict);
        Assert.True((await new ListarAvaliacoesPerfilQueryHandler(repositorio.Object)
            .Handle(new("user", limit, offset), Ct)).IsFailed);
        Assert.True((await new ListarAvaliacoesItemQueryHandler(repositorio.Object)
            .Handle(new(Item, Usuario, limit, offset), Ct)).IsFailed);
    }

    [Theory]
    [InlineData("{\"score\":1.5}")]
    [InlineData("{\"score\":\"90\"}")]
    [InlineData("{\"score\":90,\"usuarioId\":\"outro\"}")]
    [InlineData("{\"score\":90,\"visibility\":\"private\"}")]
    public void Contrato_RejeitaNotaNaoInteiraEAutoriaOuVisibilidadeNoCorpo(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CriarAvaliacaoRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static AvaliacaoDto Dto(Guid id) => new(id, new("user", "Nome"), Item, 90, null, DateTime.UtcNow, null);
    private static Mock<IMusicCatalogProvider> Catalogo()
    {
        var catalogo = new Mock<IMusicCatalogProvider>();
        catalogo.Setup(x => x.ObterAlbumAsync(Item.ExternalId, Ct)).ReturnsAsync(new AlbumCatalogoDto(
            new(Item.Provider, Item.Type, Item.ExternalId, "Título", ["Artista"], null, null), []));
        return catalogo;
    }
}
