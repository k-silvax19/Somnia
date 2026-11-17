using Moq;
using Somnia.Aplicacao.Modulos.Auth;
using Somnia.Dominio.Compartilhado;
using Xunit;

namespace Somnia.Tests.Modulos.Auth;

public sealed class AuthHandlerTests
{
    [Fact]
    public async Task CadastrarUsuario_ComDadosValidos_CadastraConta()
    {
        // Arrange
        var usuario = new UsuarioDto(Guid.NewGuid(), "Ana", "ana@example.com");
        var identidade = new Mock<IGerenciadorIdentidade>();
        identidade
            .Setup(x => x.CadastrarComUsernameAsync("Ana", "ana@example.com", "SenhaSegura1!", "ana", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        var handler = new CadastrarUsuarioCommandHandler(identidade.Object);

        // Act
        var result = await handler.Handle(
            new CadastrarUsuarioCommand("Ana", "ana@example.com", "SenhaSegura1!", "ana"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(usuario, result.Value);
        identidade.Verify(
            x => x.CadastrarComUsernameAsync("Ana", "ana@example.com", "SenhaSegura1!", "ana", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CadastrarUsuario_ComEmailDuplicado_RetornaFalha()
    {
        // Arrange
        var identidade = new Mock<IGerenciadorIdentidade>();
        identidade
            .Setup(x => x.CadastrarComUsernameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("EMAIL_DUPLICADO"));
        var handler = new CadastrarUsuarioCommandHandler(identidade.Object);

        // Act
        var result = await handler.Handle(
            new CadastrarUsuarioCommand("Ana", "ana@example.com", "SenhaSegura1!", "ana"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        identidade.Verify(
            x => x.CadastrarComUsernameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null, "ana@example.com", "SenhaSegura1!")]
    [InlineData("Ana", "email-invalido", "SenhaSegura1!")]
    [InlineData("Ana", "ana@example.com", "curta")]
    public async Task CadastrarUsuario_ComDadosInvalidos_NaoChamaIdentidade(
        string? nome,
        string? email,
        string? senha)
    {
        // Arrange
        var identidade = new Mock<IGerenciadorIdentidade>();
        var handler = new CadastrarUsuarioCommandHandler(identidade.Object);

        // Act
        var result = await handler.Handle(
            new CadastrarUsuarioCommand(nome, email, senha, "ana"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        identidade.Verify(
            x => x.CadastrarComUsernameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("nome com espaco")]
    [InlineData("nome@dominio")]
    [InlineData("usuário")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task CadastrarUsernameInvalido_NaoAcessaIdentidade(string? username)
    {
        var identidade = new Mock<IGerenciadorIdentidade>(MockBehavior.Strict);
        var resultado = await new CadastrarUsuarioCommandHandler(identidade.Object).Handle(
            new CadastrarUsuarioCommand("Ana", "ana@example.com", "SenhaSegura1!", username), CancellationToken.None);

        Assert.True(resultado.IsFailed);
        Assert.Contains(resultado.Errors, erro => erro.Metadata.GetValueOrDefault("Campo")?.ToString() == "username");
        identidade.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("ana")]
    [InlineData("Ana_01.test-user")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void UsernameAceitaLimitesECaracteresDocumentados(string username)
    {
        Assert.True(RegrasCadastro.UsernameValido(username));
    }

    [Fact]
    public async Task AutenticarUsuario_ComCredenciaisValidas_EmiteToken()
    {
        // Arrange
        var credenciais = new CredenciaisUsuario(Guid.NewGuid(), "Ana", "ana@example.com");
        var token = new TokenDto(credenciais.Id, credenciais.Nome, credenciais.Email, "jwt", DateTime.UtcNow.AddHours(1));
        var identidade = new Mock<IGerenciadorIdentidade>();
        var emissor = new Mock<IEmissorTokens>();
        identidade
            .Setup(x => x.AutenticarAsync("ana@example.com", "SenhaSegura1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(credenciais);
        emissor
            .Setup(x => x.CriarToken(credenciais, It.IsAny<Guid>()))
            .Returns(token);
        var refreshTokens = new Mock<IRefreshTokens>();
        refreshTokens.Setup(x => x.CriarAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenDto("refresh", DateTime.UtcNow.AddDays(7), Guid.NewGuid()));
        var handler = new AutenticarUsuarioCommandHandler(identidade.Object, emissor.Object, refreshTokens.Object);

        // Act
        var result = await handler.Handle(
            new AutenticarUsuarioCommand("ana@example.com", "SenhaSegura1!"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(token.AccessToken, result.Value.AccessToken);
        Assert.Equal("refresh", result.Value.RefreshToken);
        emissor.Verify(x => x.CriarToken(credenciais, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task AutenticarUsuario_ComCredenciaisInvalidas_NaoEmiteToken()
    {
        // Arrange
        var identidade = new Mock<IGerenciadorIdentidade>();
        var emissor = new Mock<IEmissorTokens>();
        identidade
            .Setup(x => x.AutenticarAsync("ana@example.com", "senha-errada", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CredenciaisUsuario?)null);
        var refreshTokens = new Mock<IRefreshTokens>();
        refreshTokens.Setup(x => x.CriarAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenDto("refresh", DateTime.UtcNow.AddDays(7), Guid.NewGuid()));
        var handler = new AutenticarUsuarioCommandHandler(identidade.Object, emissor.Object, refreshTokens.Object);

        // Act
        var result = await handler.Handle(
            new AutenticarUsuarioCommand("ana@example.com", "senha-errada"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        emissor.Verify(x => x.CriarToken(It.IsAny<CredenciaisUsuario>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ObterUsuario_ConsultaIdentidadeComIdDaQuery()
    {
        // Arrange
        var usuarioId = Guid.NewGuid();
        var usuario = new UsuarioDto(usuarioId, "Ana", "ana@example.com");
        var identidade = new Mock<IGerenciadorIdentidade>();
        identidade
            .Setup(x => x.ObterPorIdAsync(usuarioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        var handler = new ObterUsuarioQueryHandler(identidade.Object);

        // Act
        var result = await handler.Handle(new ObterUsuarioQuery(usuarioId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(usuario, result.Value);
    }

    [Fact]
    public async Task ObterUsuario_QuandoNaoExiste_RetornaFalha()
    {
        // Arrange
        var identidade = new Mock<IGerenciadorIdentidade>();
        identidade
            .Setup(x => x.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioDto?)null);
        var handler = new ObterUsuarioQueryHandler(identidade.Object);

        // Act
        var result = await handler.Handle(new ObterUsuarioQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EncerrarSessao_RevogaTokenAteExpiracao()
    {
        // Arrange
        var expiracao = DateTime.UtcNow.AddHours(1);
        var revogacao = new Mock<IRevogacaoToken>();
        revogacao
            .Setup(x => x.RevogarAsync("jti-token", expiracao, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new EncerrarSessaoCommandHandler(revogacao.Object, Mock.Of<IRefreshTokens>());

        // Act
        var result = await handler.Handle(
            new EncerrarSessaoCommand("jti-token", expiracao),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        revogacao.Verify(x => x.RevogarAsync("jti-token", expiracao, It.IsAny<CancellationToken>()), Times.Once);
    }
}
