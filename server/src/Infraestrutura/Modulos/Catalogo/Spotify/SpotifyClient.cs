using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Infraestrutura.Modulos.Catalogo.Spotify;

// Mantém tokens somente em memória e centraliza os limites de chamadas ao provedor.
public sealed class SpotifyClient(IHttpClientFactory clients, IOptions<SpotifyOptions> options, TimeProvider clock) : IDisposable
{
    public const string AuthClient = "SpotifyAuth";
    public const string ApiClient = "SpotifyApi";
    private const int MaxJsonBytes = 2 * 1024 * 1024;
    private readonly SpotifyOptions settings = options.Value;
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private readonly SemaphoreSlim httpGate = new(4, 4);
    private Token? token;
    private long retomarEmUtcTicks;

    public async Task<JsonDocument?> ConsultarAsync(string caminho, bool aceitar404, CancellationToken ct)
    {
        if (Uri.TryCreate(caminho, UriKind.Absolute, out _) || caminho.StartsWith('/') || caminho.Contains('\\'))
            throw new ArgumentException("A consulta deve usar um caminho relativo do catálogo.");

        for (var tentativa = 0; tentativa < 2; tentativa++)
        {
            ct.ThrowIfCancellationRequested();
            VerificarEspera();
            var atual = await ObterTokenAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, caminho);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", atual.AccessToken);
            var resultado = await EnviarAsync(ApiClient, request, aceitar404, permitir401: tentativa == 0, ct);
            if (!resultado.TokenRecusado)
                return resultado.Json;
            // Outro request pode já ter renovado o token; não descarte o sucessor.
            Interlocked.CompareExchange(ref token, null, atual);
        }
        throw new CatalogoIndisponivelException();
    }

    private async Task<Token> ObterTokenAsync(CancellationToken ct)
    {
        var atual = Volatile.Read(ref token);
        if (atual is not null && clock.GetUtcNow() < atual.ExpiraEmUtc)
            return atual;

        await tokenGate.WaitAsync(ct);
        try
        {
            VerificarEspera();
            atual = Volatile.Read(ref token);
            if (atual is not null && clock.GetUtcNow() < atual.ExpiraEmUtc)
                return atual;
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}")));
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
            var inicio = clock.GetUtcNow();
            var resultado = await EnviarAsync(AuthClient, request, aceitar404: false, permitir401: false, ct);
            using var json = resultado.Json ?? throw new RespostaCatalogoInvalidaException();
            var root = json.RootElement;
            if (!root.TryGetProperty("access_token", out var access) || access.ValueKind != JsonValueKind.String
                || access.GetString() is not { Length: > 0 and <= 8192 } valor
                || valor.Any(c => c <= ' ' || c >= 127)
                || !root.TryGetProperty("token_type", out var tipo) || tipo.ValueKind != JsonValueKind.String
                || !string.Equals(tipo.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase)
                || !root.TryGetProperty("expires_in", out var expires) || expires.ValueKind != JsonValueKind.Number
                || !expires.TryGetInt32(out var segundos)
                || segundos is < 1 or > 86400)
                throw new RespostaCatalogoInvalidaException();
            atual = new Token(valor, inicio.AddSeconds(segundos - Math.Min(60, segundos / 10)));
            Volatile.Write(ref token, atual);
            return atual;
        }
        finally { tokenGate.Release(); }
    }

    private async Task<Resposta> EnviarAsync(string clientName, HttpRequestMessage request,
        bool aceitar404, bool permitir401, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var entrou = false;
        try
        {
            await httpGate.WaitAsync(limite.Token);
            entrou = true;
            VerificarEspera();
            using var client = clients.CreateClient(clientName);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            if (response.StatusCode == HttpStatusCode.Unauthorized && permitir401)
                return new(null, true);
            if (response.StatusCode == HttpStatusCode.NotFound && aceitar404)
                return new(null, false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw Esperar(RetryAfter(response));
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                || (clientName == AuthClient && response.StatusCode == HttpStatusCode.BadRequest))
                throw Esperar(30);
            if ((int)response.StatusCode >= 500)
                throw Esperar(5);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new ConsultaCatalogoInvalidaException();
            if (response.StatusCode != HttpStatusCode.OK)
                throw new RespostaCatalogoInvalidaException();
            return new(await LerJsonAsync(response, limite.Token), false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Esperar(5); }
        catch (HttpRequestException) { throw Esperar(5); }
        catch (IOException) { throw Esperar(5); }
        catch (JsonException) { throw new RespostaCatalogoInvalidaException(); }
        finally { if (entrou) httpGate.Release(); }
    }

    private static async Task<JsonDocument> LerJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > MaxJsonBytes)
            throw new RespostaCatalogoInvalidaException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bloco = new byte[16384];
        int lidos;
        while ((lidos = await stream.ReadAsync(bloco, ct)) > 0)
        {
            if (buffer.Length + lidos > MaxJsonBytes)
                throw new RespostaCatalogoInvalidaException();
            buffer.Write(bloco, 0, lidos);
        }
        var json = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        if (json.RootElement.ValueKind != JsonValueKind.Object)
        {
            json.Dispose();
            throw new RespostaCatalogoInvalidaException();
        }
        return json;
    }

    private int RetryAfter(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values)
            || !RetryConditionHeaderValue.TryParse(values.FirstOrDefault(), out var retry))
            return 30;
        var espera = retry.Delta ?? retry.Date - clock.GetUtcNow();
        return (int)Math.Clamp(Math.Ceiling(espera?.TotalSeconds ?? 30), 1, int.MaxValue);
    }

    private CatalogoIndisponivelException Esperar(int segundos)
    {
        var novo = clock.GetUtcNow().AddSeconds(segundos).UtcTicks;
        var anterior = Interlocked.Read(ref retomarEmUtcTicks);
        while (novo > anterior)
        {
            var observado = Interlocked.CompareExchange(ref retomarEmUtcTicks, novo, anterior);
            if (observado == anterior) break;
            anterior = observado;
        }
        return new CatalogoIndisponivelException(segundos);
    }

    private void VerificarEspera()
    {
        var restante = Interlocked.Read(ref retomarEmUtcTicks) - clock.GetUtcNow().UtcTicks;
        if (restante > 0)
            throw new CatalogoIndisponivelException((int)Math.Ceiling(TimeSpan.FromTicks(restante).TotalSeconds));
    }

    public void Dispose() { tokenGate.Dispose(); httpGate.Dispose(); }
    private sealed record Token(string AccessToken, DateTimeOffset ExpiraEmUtc);
    private sealed record Resposta(JsonDocument? Json, bool TokenRecusado);
}
