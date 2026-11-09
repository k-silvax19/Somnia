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
            .Setup(x => x.CadastrarAsync("Ana", "ana@example.com", "SenhaSegura1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        var handler = new CadastrarUsuarioCommandHandler(identidade.Object);

        // Act
        var result = await handler.Handle(
            new CadastrarUsuarioCommand("Ana", "ana@example.com", "SenhaSegura1!"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(usuario, result.Value);
        identidade.Verify(
            x => x.CadastrarAsync("Ana", "ana@example.com", "SenhaSegura1!", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CadastrarUsuario_ComEmailDuplicado_RetornaFalha()
    {
        // Arrange
        var identidade = new Mock<IGerenciadorIdentidade>();
        identidade
            .Setup(x => x.CadastrarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("EMAIL_DUPLICADO"));
        var handler = new CadastrarUsuarioCommandHandler(identidade.Object);

        // Act
        var result = await handler.Handle(
            new CadastrarUsuarioCommand("Ana", "ana@example.com", "SenhaSegura1!"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        identidade.Verify(
            x => x.CadastrarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
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
            new CadastrarUsuarioCommand(nome, email, senha),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        identidade.Verify(
            x => x.CadastrarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            .Setup(x => x.CriarToken(credenciais))
            .Returns(token);
        var handler = new AutenticarUsuarioCommandHandler(identidade.Object, emissor.Object);

        // Act
        var result = await handler.Handle(
            new AutenticarUsuarioCommand("ana@example.com", "SenhaSegura1!"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(token, result.Value);
        emissor.Verify(x => x.CriarToken(credenciais), Times.Once);
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
        var handler = new AutenticarUsuarioCommandHandler(identidade.Object, emissor.Object);

        // Act
        var result = await handler.Handle(
            new AutenticarUsuarioCommand("ana@example.com", "senha-errada"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        emissor.Verify(x => x.CriarToken(It.IsAny<CredenciaisUsuario>()), Times.Never);
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
        var handler = new EncerrarSessaoCommandHandler(revogacao.Object);

        // Act
        var result = await handler.Handle(
            new EncerrarSessaoCommand("jti-token", expiracao),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        revogacao.Verify(x => x.RevogarAsync("jti-token", expiracao, It.IsAny<CancellationToken>()), Times.Once);
    }
}
