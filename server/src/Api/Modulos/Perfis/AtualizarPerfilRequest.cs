using System.Text.Json.Serialization;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Api.Modulos.Perfis;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AtualizarPerfilRequest
{
    private string? displayName;
    private string? bio;
    private string? defaultScoreVisibility;
    private string? defaultReviewVisibility;

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

    public string? DefaultScoreVisibility
    {
        get => defaultScoreVisibility;
        set { defaultScoreVisibility = value; InformouScoreVisibility = true; }
    }

    public string? DefaultReviewVisibility
    {
        get => defaultReviewVisibility;
        set { defaultReviewVisibility = value; InformouReviewVisibility = true; }
    }

    [JsonIgnore]
    public bool InformouDisplayName { get; private set; }
    [JsonIgnore]
    public bool InformouBio { get; private set; }
    [JsonIgnore]
    public bool InformouScoreVisibility { get; private set; }
    [JsonIgnore]
    public bool InformouReviewVisibility { get; private set; }

    public AlteracaoPerfil ToAlteracao()
    {
        return new AlteracaoPerfil(DisplayName, InformouDisplayName, Bio, InformouBio,
            DefaultScoreVisibility, InformouScoreVisibility, DefaultReviewVisibility, InformouReviewVisibility);
    }
}
