using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Somnia.Api.Compartilhado.Auth;
using Somnia.Dominio.Compartilhado;
using Xunit;

namespace Somnia.Tests.Modulos.Auth;

public sealed class CookiesAutenticacaoTests
{
    private static readonly string SigningKey = new('K', 64);

    private static (DefaultHttpContext Context, object Body) CriarResposta()
    {
        var context = new DefaultHttpContext();
        var token = new TokenDto(Guid.NewGuid(), "Ana", "ana@example.com", "jwt",
            DateTime.UtcNow.AddMinutes(15), new string('A', 128), DateTime.UtcNow.AddDays(7));
        var body = CookiesAutenticacao.Definir(context.Response, token, SigningKey);
        var cookies = context.Response.Headers.SetCookie.Select(x => x!.Split(';')[0]).ToArray();
        context.Request.Headers.Cookie = string.Join("; ", cookies);
        context.Request.Headers["X-CSRF-Token"] = context.Request.Cookies[CookiesAutenticacao.CsrfCookie];

        return (context, body);
    }

    [Fact]
    public void Definir_ProtegeRefreshCookieENaoExibeTokenNoJson()
    {
        var (context, body) = CriarResposta();
        var cookie = context.Response.Headers.SetCookie.Single(x => x!.StartsWith(CookiesAutenticacao.RefreshCookie))!;
        Assert.Contains("secure", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/", cookie);
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(JsonDocument.Parse(json).RootElement.TryGetProperty("refreshToken", out _));
        Assert.DoesNotContain(new string('A', 128), json);
    }

    [Fact]
    public void ValidarCsrf_AceitaSomenteTokenAssinadoVinculadoAoRefresh()
    {
        var (context, _) = CriarResposta();
        Assert.True(CookiesAutenticacao.ValidarCsrf(context.Request, SigningKey));
        Assert.False(CookiesAutenticacao.ValidarCsrf(context.Request, new string('Z', 64)));
        context.Request.Headers.Cookie = context.Request.Headers.Cookie.ToString()
            .Replace(new string('A', 128), new string('B', 128));
        Assert.False(CookiesAutenticacao.ValidarCsrf(context.Request, SigningKey));
    }

    [Fact]
    public void ValidarCsrf_RejeitaTokenForjadoMesmoComCookieEHeaderIguais()
    {
        var context = new DefaultHttpContext();
        var forgery = new string('A', 64) + "." + new string('B', 64);
        context.Request.Headers.Cookie = CookiesAutenticacao.CsrfCookie + "=" + forgery
            + "; " + CookiesAutenticacao.RefreshCookie + "=" + new string('C', 128);
        context.Request.Headers["X-CSRF-Token"] = forgery;
        Assert.False(CookiesAutenticacao.ValidarCsrf(context.Request, SigningKey));
    }

    [Fact]
    public void ValidarCsrf_RejeitaHeaderAusenteOuDiferente()
    {
        var (context, _) = CriarResposta();
        context.Request.Headers.Remove("X-CSRF-Token");
        Assert.False(CookiesAutenticacao.ValidarCsrf(context.Request, SigningKey));
        context.Request.Headers["X-CSRF-Token"] = new string('A', 129);
        Assert.False(CookiesAutenticacao.ValidarCsrf(context.Request, SigningKey));
    }
}
