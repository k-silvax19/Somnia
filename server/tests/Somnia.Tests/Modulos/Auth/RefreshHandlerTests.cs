using Moq;
using Somnia.Aplicacao.Modulos.Auth;
using Somnia.Dominio.Compartilhado;
using Xunit;

namespace Somnia.Tests.Modulos.Auth;

public sealed class RefreshHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalido")]
    public async Task RenovarSessao_ComTokenMalformado_NaoConsultaPersistencia(string? token)
    {
        var refreshTokens = new Mock<IRefreshTokens>(MockBehavior.Strict);
        var handler = new RenovarSessaoCommandHandler(refreshTokens.Object, Mock.Of<IEmissorTokens>());

        var result = await handler.Handle(new RenovarSessaoCommand(token), CancellationToken.None);

        Assert.True(result.IsFailed);
        refreshTokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RenovarSessao_ComTokenExpiradoOuRevogado_NaoEmiteAccessToken()
    {
        var token = new string('A', 128);
        var refreshTokens = new Mock<IRefreshTokens>();
        var emissor = new Mock<IEmissorTokens>(MockBehavior.Strict);
        var handler = new RenovarSessaoCommandHandler(refreshTokens.Object, emissor.Object);

        var result = await handler.Handle(new RenovarSessaoCommand(token), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TipoErro.NaoAutenticado, result.Errors[0].Metadata[nameof(TipoErro)]);
        emissor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RenovarSessao_ComTokenValido_RetornaNovoParDeTokens()
    {
        var token = new string('A', 128);
        var usuario = new CredenciaisUsuario(Guid.NewGuid(), "Ana", "ana@example.com");
        var novoRefresh = new RefreshTokenDto(new string('B', 128), DateTime.UtcNow.AddDays(7), Guid.NewGuid());
        var refreshTokens = new Mock<IRefreshTokens>();
        refreshTokens.Setup(x => x.RenovarAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessaoRenovada(usuario, novoRefresh));
        var emissor = new Mock<IEmissorTokens>();
        emissor.Setup(x => x.CriarToken(usuario, It.IsAny<Guid>()))
            .Returns(new TokenDto(usuario.Id, usuario.Nome, usuario.Email, "novo-jwt", DateTime.UtcNow.AddMinutes(15)));
        var handler = new RenovarSessaoCommandHandler(refreshTokens.Object, emissor.Object);

        var result = await handler.Handle(new RenovarSessaoCommand(token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("novo-jwt", result.Value.AccessToken);
        Assert.Equal(novoRefresh.Token, result.Value.RefreshToken);
        Assert.Equal(novoRefresh.ExpiraEmUtc, result.Value.RefreshExpiraEmUtc);
    }

    [Fact]
    public async Task EncerrarSessao_RevogaAccessTokenERefreshTokensDoUsuario()
    {
        var usuarioId = Guid.NewGuid();
        var expiracao = DateTime.UtcNow.AddMinutes(15);
        var revogacao = new Mock<IRevogacaoToken>();
        var refreshTokens = new Mock<IRefreshTokens>();
        var handler = new EncerrarSessaoCommandHandler(revogacao.Object, refreshTokens.Object);

        var result = await handler.Handle(new EncerrarSessaoCommand("jti", expiracao, usuarioId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        revogacao.Verify(x => x.RevogarAsync("jti", expiracao, It.IsAny<CancellationToken>()), Times.Once);
        refreshTokens.Verify(x => x.RevogarPorUsuarioAsync(usuarioId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
