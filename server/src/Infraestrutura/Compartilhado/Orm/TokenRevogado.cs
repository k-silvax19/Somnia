namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class TokenRevogado
{
    public string Jti { get; set; } = string.Empty;
    public DateTime ExpiraEmUtc { get; set; }
}
