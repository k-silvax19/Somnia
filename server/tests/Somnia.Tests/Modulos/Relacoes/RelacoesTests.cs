using Moq;
using Somnia.Aplicacao.Modulos.Relacoes;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Relacoes;
using Xunit;

namespace Somnia.Tests.Modulos.Relacoes;

public sealed class RelacoesTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid Outro = Guid.NewGuid();
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public void Relacao_ExigeDoisUsuariosValidosEDistintos()
    {
        Assert.Throws<ArgumentException>(() => RelacaoSeguir.Criar(Usuario, Usuario));
        Assert.Throws<ArgumentException>(() => RelacaoSeguir.Criar(Guid.Empty, Outro));
        Assert.Throws<ArgumentException>(() => RelacaoSeguir.Criar(Usuario, Guid.Empty));
        var relacao = RelacaoSeguir.Criar(Usuario, Outro);
        Assert.Equal(Usuario, relacao.SeguidorId);
        Assert.Equal(Outro, relacao.SeguidoId);
        Assert.Equal(DateTimeKind.Utc, relacao.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("nome com espaco")]
    [InlineData("usuário")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345")]
    public async Task UsernameInvalido_NaoConsultaDependencias(string? username)
    {
        var repositorio = new Mock<IRepositorioRelacao>(MockBehavior.Strict);
        var handler = new RelacoesCommandHandler(repositorio.Object);
        Assert.True((await handler.Handle(new SeguirPerfilCommand(Usuario, username!), Ct)).IsFailed);
        Assert.True((await handler.Handle(new DeixarDeSeguirPerfilCommand(Usuario, username!), Ct)).IsFailed);
        Assert.True((await new ObterResumoRelacoesQueryHandler(repositorio.Object)
            .Handle(new(Usuario, username!), Ct)).IsFailed);
        Assert.True((await new ListarRelacoesQueryHandler(repositorio.Object)
            .Handle(new(username!, TipoListaRelacoes.Seguidores, 20, 0), Ct)).IsFailed);
        repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Alteracao_UsaTitularEAlvoResolvido(bool seguir)
    {
        var repositorio = new Mock<IRepositorioRelacao>(MockBehavior.Strict);
        repositorio.Setup(x => x.ObterUsuarioIdAsync("outro", Ct)).ReturnsAsync(Outro);
        if (seguir) repositorio.Setup(x => x.SeguirAsync(Usuario, Outro, Ct)).Returns(Task.CompletedTask);
        else repositorio.Setup(x => x.DeixarDeSeguirAsync(Usuario, Outro, Ct)).Returns(Task.CompletedTask);
        var handler = new RelacoesCommandHandler(repositorio.Object);
        var result = seguir
            ? await handler.Handle(new SeguirPerfilCommand(Usuario, "outro"), Ct)
            : await handler.Handle(new DeixarDeSeguirPerfilCommand(Usuario, "outro"), Ct);
        Assert.True(result.IsSuccess);
        repositorio.VerifyAll();
    }

    [Fact]
    public async Task Alteracao_ProprioPerfil_NaoGrava()
    {
        var repositorio = new Mock<IRepositorioRelacao>(MockBehavior.Strict);
        repositorio.Setup(x => x.ObterUsuarioIdAsync("meu-nome", Ct)).ReturnsAsync(Usuario);
        var handler = new RelacoesCommandHandler(repositorio.Object);
        var seguir = await handler.Handle(new SeguirPerfilCommand(Usuario, "meu-nome"), Ct);
        var remover = await handler.Handle(new DeixarDeSeguirPerfilCommand(Usuario, "meu-nome"), Ct);
        Assert.Equal(TipoErro.Validacao, seguir.Errors[0].Metadata[nameof(TipoErro)]);
        Assert.Equal(TipoErro.Validacao, remover.Errors[0].Metadata[nameof(TipoErro)]);
        repositorio.Verify(x => x.ObterUsuarioIdAsync("meu-nome", Ct), Times.Exactly(2));
        repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PerfilInexistente_Retorna404EmTodasOperacoes()
    {
        var repositorio = new Mock<IRepositorioRelacao>(MockBehavior.Strict);
        repositorio.Setup(x => x.ObterUsuarioIdAsync("missing", Ct)).ReturnsAsync((Guid?)null);
        var handler = new RelacoesCommandHandler(repositorio.Object);
        var seguir = await handler.Handle(new SeguirPerfilCommand(Usuario, "missing"), Ct);
        var remover = await handler.Handle(new DeixarDeSeguirPerfilCommand(Usuario, "missing"), Ct);
        var resumo = await new ObterResumoRelacoesQueryHandler(repositorio.Object).Handle(new(Usuario, "missing"), Ct);
        var pagina = await new ListarRelacoesQueryHandler(repositorio.Object)
            .Handle(new("missing", TipoListaRelacoes.Seguidos, 20, 0), Ct);
        foreach (var erro in new[] { seguir.Errors[0], remover.Errors[0], resumo.Errors[0], pagina.Errors[0] })
            Assert.Equal(TipoErro.NaoEncontrado, erro.Metadata[nameof(TipoErro)]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(51, 0)]
    [InlineData(20, -1)]
    [InlineData(20, 10001)]
    public async Task PaginaInvalida_NaoConsultaBanco(int limit, int offset)
    {
        var repositorio = new Mock<IRepositorioRelacao>(MockBehavior.Strict);
        var result = await new ListarRelacoesQueryHandler(repositorio.Object)
            .Handle(new("outro", TipoListaRelacoes.Seguidores, limit, offset), Ct);
        Assert.Equal(TipoErro.Validacao, result.Errors[0].Metadata[nameof(TipoErro)]);
        repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Resumo_UsaVisitanteParaCalcularVinculo()
    {
        var repositorio = new Mock<IRepositorioRelacao>(MockBehavior.Strict);
        repositorio.Setup(x => x.ObterUsuarioIdAsync("outro", Ct)).ReturnsAsync(Outro);
        repositorio.Setup(x => x.ObterResumoAsync(Outro, Usuario, Ct)).ReturnsAsync(new ResumoRelacoesDto(true, 2, 3));
        var resultado = await new ObterResumoRelacoesQueryHandler(repositorio.Object).Handle(new(Usuario, "outro"), Ct);
        Assert.Equal(new ResumoRelacoesDto(true, 2, 3), resultado.Value);
        repositorio.VerifyAll();
    }
}
