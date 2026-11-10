using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Somnia.Aplicacao.Modulos.Catalogo;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Catalogo;
using Somnia.Infraestrutura.Modulos.Catalogo;
using Somnia.Infraestrutura.Modulos.Catalogo.Spotify;
using Xunit;

namespace Somnia.Tests.Modulos.Catalogo;

public sealed class SpotifyTests
{
    private const string AlbumId = "AAAAAAAAAAAAAAAAAAAAAA";
    private const string TrackId = "BBBBBBBBBBBBBBBBBBBBBB";

    [Fact]
    public async Task Pesquisa_UsaClientCredentials_EscapaTexto_MapeiaLinksECapa()
    {
        using var fixture = new Contexto(request => Json(Pagina([Item("album", AlbumId)], 1)));
        var result = await fixture.Provider.PesquisarAsync("AC/DC & Beyoncé", TipoItemCatalogo.Album, 10, 0, default);
        var item = Assert.Single(result.Items);

        Assert.Equal("spotify", item.Provider);
        Assert.Equal("album", item.Type);
        Assert.Equal("https://open.spotify.com/album/" + AlbumId, item.SpotifyUrl);
        Assert.Equal("https://i.scdn.co/image/test", item.CoverUrl);
        Assert.Contains("q=" + Uri.EscapeDataString("AC/DC & Beyoncé"), fixture.Calls.Last().Url);
        Assert.Contains("market=BR", fixture.Calls.Last().Url);
        var auth = fixture.Calls.First();
        Assert.Equal("Basic", auth.Scheme);
        Assert.Equal("fixture-id:fixture-secret", Encoding.UTF8.GetString(Convert.FromBase64String(auth.Authorization!)));
        Assert.Equal("grant_type=client_credentials", auth.Body);
        Assert.Equal("fixture-token-1", fixture.Calls.Last().Authorization);
    }

    [Fact]
    public async Task Concorrencia_CompartilhaUmToken_ELimitaRequisicoesEmAndamento()
    {
        var ativos = 0;
        var maximo = 0;
        using var fixture = new Contexto(async (request, ct) =>
        {
            var atual = Interlocked.Increment(ref ativos);
            int anterior;
            do
            {
                anterior = Volatile.Read(ref maximo);
                if (atual <= anterior) break;
            } while (Interlocked.CompareExchange(ref maximo, atual, anterior) != anterior);
            await Task.Delay(20, ct);
            Interlocked.Decrement(ref ativos);
            return Json(Pagina([], 0));
        });
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default)));

        Assert.Equal(1, fixture.AuthCalls);
        Assert.Equal(20, fixture.Calls.Count(x => x.Host == "api.spotify.com"));
        Assert.InRange(maximo, 1, 4);
    }

    [Fact]
    public async Task Token_ExpiraEmMemoriaERenovaAntesDoPrazo()
    {
        using var fixture = new Contexto(_ => Json(Pagina([], 0)));
        await fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default);
        fixture.Clock.Advance(TimeSpan.FromSeconds(3541));
        await fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default);
        Assert.Equal(2, fixture.AuthCalls);
        Assert.Equal("fixture-token-2", fixture.Calls.Last().Authorization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Token401_RenovaUmaVez_SemLoop(bool sempre401)
    {
        var chamadas = 0;
        using var fixture = new Contexto(_ => ++chamadas == 1 || sempre401
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Json(Pagina([], 0)));

        if (sempre401)
            await Assert.ThrowsAsync<CatalogoIndisponivelException>(() => fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default));
        else
            await fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default);
        Assert.Equal(2, chamadas);
        Assert.Equal(2, fixture.AuthCalls);
    }

    [Theory]
    [InlineData("delta", 120)]
    [InlineData("date", 45)]
    [InlineData("invalid", 30)]
    public async Task Quota429_RespeitaRetryAfter_EBloqueiaNovasChamadasAteOPrazo(string formato, int espera)
    {
        var chamadas = 0;
        using var fixture = new Contexto(_ =>
        {
            if (++chamadas > 1) return Json(Pagina([], 0));
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", formato switch
            {
                "delta" => "120",
                "date" => "Tue, 10 Nov 2026 12:00:45 GMT",
                _ => "invalido"
            });
            return response;
        });
        var erro = await Assert.ThrowsAsync<CatalogoIndisponivelException>(() => fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default));
        Assert.Equal(espera, erro.RetryAfterSeconds);
        await Assert.ThrowsAsync<CatalogoIndisponivelException>(() => fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default));
        Assert.Equal(1, chamadas);
        fixture.Clock.Advance(TimeSpan.FromSeconds(espera + 1));
        await fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default);
        Assert.Equal(2, chamadas);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task FalhaExterna_NaoRepeteImediatamenteNemExibePayload(HttpStatusCode status)
    {
        using var fixture = new Contexto(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("private-provider-payload")
        });
        var result = await new PesquisarCatalogoQueryHandler(fixture.Provider).Handle(new("teste", "album"), default);
        Assert.Equal(TipoErro.Indisponivel, result.Errors[0].Metadata[nameof(TipoErro)]);
        Assert.DoesNotContain("private-provider-payload", result.Errors[0].Message);
        Assert.True((int)result.Errors[0].Metadata["RetryAfterSeconds"] > 0);
        await Assert.ThrowsAsync<CatalogoIndisponivelException>(() =>
            fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default));
        Assert.Equal(2, fixture.Calls.Count); // Token + uma consulta, sem retry automático de 403/5xx.
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"albums\":[]}")]
    [InlineData("{\"albums\":{\"items\":[],\"total\":\"bad\"}}")]
    public async Task RespostaInvalida_Vira502(string body)
    {
        using var fixture = new Contexto(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var result = await new PesquisarCatalogoQueryHandler(fixture.Provider).Handle(new("teste", "album"), default);
        Assert.Equal(TipoErro.FalhaIntegracao, result.Errors[0].Metadata[nameof(TipoErro)]);
    }

    [Fact]
    public async Task RespostaExcessiva_EBloqueadaAntesDoParse()
    {
        using var fixture = new Contexto(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string(' ', 2 * 1024 * 1024 + 1))
        });
        await Assert.ThrowsAsync<RespostaCatalogoInvalidaException>(() => fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default));
    }

    [Fact]
    public async Task Timeout_EControlado_CancelamentoDoUsuarioEPropagado()
    {
        using var fixture = new Contexto(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json(Pagina([], 0));
        }, timeout: 1);
        await Assert.ThrowsAsync<CatalogoIndisponivelException>(() => fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default));
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, cancelamento.Token));
    }

    [Fact]
    public async Task Album_CarregaTodasAsPaginas_PreservaOrdemDeDiscos_EIgnoraNextExterno()
    {
        using var fixture = new Contexto(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/tracks")) return Json(Item("album", AlbumId));
            var offset = request.RequestUri.Query.Contains("offset=50") ? 50 : 0;
            var items = Enumerable.Range(offset, offset == 0 ? 50 : 1)
                .Select(i => Item("track", i.ToString("D22"), i % 25 + 1)).ToArray();
            return Json(new { items, total = 51, offset, next = "https://untrusted.example/collect" });
        });
        var album = await fixture.Provider.ObterAlbumAsync(AlbumId, default);
        Assert.NotNull(album);
        Assert.Equal(51, album.Tracks.Count);
        Assert.Equal(Enumerable.Range(0, 51).Select(i => i.ToString("D22")), album.Tracks.Select(x => x.ExternalId));
        Assert.Equal(1, album.Tracks[25].TrackNumber);
        Assert.All(fixture.Calls, call => Assert.Contains(call.Host, new[] { "api.spotify.com", "accounts.spotify.com" }));
        Assert.Equal(4, fixture.Calls.Count); // Token, álbum e duas páginas de faixas.
    }

    [Fact]
    public async Task Album_PaginaSemProgresso_NaoEntraEmLoop()
    {
        using var fixture = new Contexto(request => Json(request.RequestUri!.AbsolutePath.EndsWith("/tracks")
            ? new { items = System.Array.Empty<object>(), total = 1, offset = 0 } : Item("album", AlbumId)));
        await Assert.ThrowsAsync<RespostaCatalogoInvalidaException>(() => fixture.Provider.ObterAlbumAsync(AlbumId, default));
        Assert.Equal(3, fixture.Calls.Count);
    }

    [Fact]
    public async Task Faixa_UsaAlbumRelacionadoEConservaMetadadosAusentes()
    {
        using var fixture = new Contexto(_ => Json(new
        {
            id = TrackId, type = "track", name = (string?)null,
            track_number = 2, album = Item("album", AlbumId)
        }));
        var faixa = await fixture.Provider.ObterFaixaAsync(TrackId, default);
        Assert.NotNull(faixa);
        Assert.Equal(AlbumId, faixa.Album.ExternalId);
        Assert.Equal(2, faixa.TrackNumber);
        Assert.Empty(faixa.Artists);
        Assert.Equal("Título indisponível", faixa.Title);
        Assert.Equal(faixa.Album.CoverUrl, faixa.CoverUrl);
        Assert.Equal("https://open.spotify.com/track/" + TrackId, faixa.SpotifyUrl);
    }

    [Fact]
    public async Task Faixa404_RetornaAusencia_IdInvalidoNaoChamaSpotify()
    {
        using var fixture = new Contexto(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Null(await fixture.Provider.ObterFaixaAsync("somnia-demo-id", default));
        Assert.Empty(fixture.Calls);
        Assert.Null(await fixture.Provider.ObterFaixaAsync(TrackId, default));
        Assert.Equal(2, fixture.Calls.Count);
    }

    [Fact]
    public async Task Pesquisa_ItemNuloEIgnorado_CapaInseguraNaoEExposta()
    {
        using var fixture = new Contexto(_ => Json(Pagina([null!, new
        {
            id = AlbumId, type = "album", name = "Álbum",
            images = new[] { new { url = "javascript:alert(1)" } }
        }], 2)));
        var pagina = await fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default);
        Assert.Null(Assert.Single(pagina.Items).CoverUrl);
        Assert.False(pagina.HasMore);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task AutenticacaoRecusada_NaoVira401DoUsuario(HttpStatusCode status)
    {
        using var fixture = new Contexto(_ => Json(Pagina([], 0)), auth: _ => new HttpResponseMessage(status));
        var result = await new PesquisarCatalogoQueryHandler(fixture.Provider).Handle(new("teste", "album"), default);
        Assert.Equal(TipoErro.Indisponivel, result.Errors[0].Metadata[nameof(TipoErro)]);
        Assert.Single(fixture.Calls);
    }

    [Theory]
    [InlineData("{\"access_token\":\"valid\",\"token_type\":5,\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"valid\",\"token_type\":\"Bearer\",\"expires_in\":\"bad\"}")]
    public async Task TokenComContratoInvalido_NaoGeraExcecaoInterna(string token)
    {
        using var fixture = new Contexto(_ => Json(Pagina([], 0)), auth: _ => new(HttpStatusCode.OK)
        {
            Content = new StringContent(token)
        });
        await Assert.ThrowsAsync<RespostaCatalogoInvalidaException>(() => fixture.Provider.PesquisarAsync("teste", TipoItemCatalogo.Album, 10, 0, default));
        Assert.Single(fixture.Calls);
    }

    [Fact]
    public void Configuracao_ExigeCredenciais_ENaoHabilitaRedirecionamento()
    {
        var values = new Dictionary<string, string?> { ["MusicCatalog:Provider"] = "Spotify" };
        using (var semChaves = new ServiceCollection().AddMusicCatalogServices(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(), false).BuildServiceProvider())
        {
            Assert.Throws<OptionsValidationException>(() => semChaves.GetRequiredService<IMusicCatalogProvider>());
        }
        values["Spotify:ClientId"] = "fixture-id";
        values["Spotify:ClientSecret"] = "fixture-secret";
        using var services = new ServiceCollection().AddMusicCatalogServices(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(), false).BuildServiceProvider();
        Assert.IsType<ProvedorCatalogoSpotify>(services.GetRequiredService<IMusicCatalogProvider>());
        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(SpotifyClient.ApiClient);
        while (handler is DelegatingHandler delegating) handler = delegating.InnerHandler!;
        Assert.False(Assert.IsType<SocketsHttpHandler>(handler).AllowAutoRedirect);
    }

    private static object Item(string type, string id, int number = 1) => new
    {
        id, type, name = "Item de teste", track_number = number,
        artists = new[] { new { name = "Artista" } },
        images = new[] { new { url = "https://i.scdn.co/image/test" } }
    };
    private static object Pagina(object[] items, int total) => new { albums = new { items, total, limit = 10, offset = 0 } };
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed record Chamada(string Host, string Url, string? Scheme, string? Authorization, string? Body);
    private sealed class Relogio : TimeProvider
    {
        private long ticks = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero).UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan time) => Interlocked.Add(ref ticks, time.Ticks);
    }

    private sealed class Contexto : IDisposable
    {
        public ConcurrentQueue<Chamada> Calls { get; } = new();
        public Relogio Clock { get; } = new();
        public int AuthCalls;
        public ProvedorCatalogoSpotify Provider { get; }
        private readonly SpotifyClient client;
        private readonly Handler handler;

        public Contexto(Func<HttpRequestMessage, HttpResponseMessage> api, int timeout = 10,
            Func<int, HttpResponseMessage>? auth = null) : this((request, _) => Task.FromResult(api(request)), timeout, auth) { }

        public Contexto(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> api, int timeout = 10,
            Func<int, HttpResponseMessage>? auth = null)
        {
            var options = Options.Create(new SpotifyOptions
            {
                ClientId = "fixture-id", ClientSecret = "fixture-secret", Market = "BR", TimeoutSeconds = timeout
            });
            handler = new Handler(async (request, ct) =>
            {
                Calls.Enqueue(new(request.RequestUri!.Host, request.RequestUri.AbsoluteUri, request.Headers.Authorization?.Scheme,
                    request.Headers.Authorization?.Parameter, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
                if (request.RequestUri.Host == "accounts.spotify.com")
                {
                    var count = Interlocked.Increment(ref AuthCalls);
                    return auth?.Invoke(count) ?? Json(new { access_token = "fixture-token-" + count, token_type = "Bearer", expires_in = 3600 });
                }
                return await api(request, ct);
            });
            client = new SpotifyClient(new Factory(handler), options, Clock);
            Provider = new ProvedorCatalogoSpotify(client, options);
        }

        public void Dispose() { client.Dispose(); handler.Dispose(); }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> enviar) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => enviar(request, ct);
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false)
        {
            BaseAddress = new Uri(name == SpotifyClient.AuthClient ? "https://accounts.spotify.com/" : "https://api.spotify.com/v1/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
    }
}
