namespace Somnia.Aplicacao.Modulos.Perfis.Mensageria;

public sealed record ProcessarAvatarMessage(Guid SolicitacaoId)
{
    public const string Fila = "somnia-avatars-solicitados";
}
