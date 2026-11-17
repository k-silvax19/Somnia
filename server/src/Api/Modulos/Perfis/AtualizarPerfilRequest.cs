using System.Text.Json.Serialization;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Api.Modulos.Perfis;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AtualizarPerfilRequest
{
    private string? displayName;
    private string? bio;
    private string? bannerTheme;

    public string? DisplayName
    {
        get => displayName;
        set { displayName = value; InformouDisplayName = true; }
    }

    public string? Bio
    {
        get => bio;
        set { bio = value; InformouBio = true; }
    }

    public string? BannerTheme
    {
        get => bannerTheme;
        set { bannerTheme = value; InformouBannerTheme = true; }
    }

    [JsonIgnore]
    public bool InformouDisplayName { get; private set; }
    [JsonIgnore]
    public bool InformouBio { get; private set; }
    [JsonIgnore]
    public bool InformouBannerTheme { get; private set; }

    public AlteracaoPerfil ToAlteracao()
    {
        return new AlteracaoPerfil(DisplayName, InformouDisplayName, Bio, InformouBio, BannerTheme, InformouBannerTheme);
    }
}
