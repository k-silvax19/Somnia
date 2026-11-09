using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Api.Compartilhado.Auth;

public static class CookiesAutenticacao
{
    public const string RefreshCookie = "__Host-somnia-refresh";
    public const string CsrfCookie = "__Host-somnia-csrf";

    public static bool ValidarCsrf(HttpRequest request, string signingKey)
    {
        var cookie = request.Cookies[CsrfCookie];
        var header = request.Headers["X-CSRF-Token"].ToString();
        var refreshToken = request.Cookies[RefreshCookie];
        if (cookie is not { Length: 129 } || header.Length != 129
            || refreshToken is not { Length: 128 } || cookie[64] != '.')
            return false;

        var expected = AssinarCsrf(cookie[..64], refreshToken, signingKey);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header))
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie[65..]), Encoding.UTF8.GetBytes(expected));
    }

    public static object Definir(HttpResponse response, TokenDto token, string signingKey)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var csrf = nonce + "." + AssinarCsrf(nonce, token.RefreshToken!, signingKey);
        response.Cookies.Append(RefreshCookie, token.RefreshToken!, Opcoes(token.RefreshExpiraEmUtc, true));
        response.Cookies.Append(CsrfCookie, csrf, Opcoes(token.RefreshExpiraEmUtc, false));

        return new
        {
            token.UsuarioId,
            token.Nome,
            token.Email,
            token.AccessToken,
            token.DataExpiracaoEmUtc,
            token.RefreshExpiraEmUtc,
            CsrfToken = csrf
        };
    }

    public static void Remover(HttpResponse response)
    {
        response.Cookies.Delete(RefreshCookie, Opcoes(null, true));
        response.Cookies.Delete(CsrfCookie, Opcoes(null, false));
    }

    public static ObjectResult ErroCsrf()
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = 403,
            Title = "Validacao CSRF falhou."
        }) { StatusCode = 403 };
    }

    private static string AssinarCsrf(string nonce, string refreshToken, string signingKey)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("somnia-csrf:" + signingKey));
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(nonce + ":" + refreshToken)));
    }

    private static CookieOptions Opcoes(DateTime? expires, bool httpOnly)
    {
        return new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expires is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc))
        };
    }
}
