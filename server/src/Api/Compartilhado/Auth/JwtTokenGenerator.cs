using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Api.Compartilhado.Auth;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : IEmissorTokens
{
    public TokenDto CriarToken(CredenciaisUsuario usuario)
    {
        var jwt = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(jwt.ExpiresInMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            jwt.Issuer,
            jwt.Audience,
            claims, expires: expires,
            signingCredentials: credentials
        );

        return new(
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            new JwtSecurityTokenHandler().WriteToken(token),
            expires
        );
    }
}
