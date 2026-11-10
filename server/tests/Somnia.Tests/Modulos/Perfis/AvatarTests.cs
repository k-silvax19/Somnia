using System.Text;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using Somnia.Aplicacao.Modulos.Perfis;
using Somnia.Aplicacao.Modulos.Perfis.Mensageria;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Infraestrutura.Modulos.Perfis;
using Xunit;

namespace Somnia.Tests.Modulos.Perfis;

public sealed class AvatarTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    public void Normalizar_DecodificaImagemReal_ReduzEPreservaProporcao(SKEncodedImageFormat formato)
    {
        var arquivo = CriarImagem(1024, 256, formato);
        var marcador = Encoding.UTF8.GetBytes("metadado-privado-que-nao-deve-sair");
        var normalizado = new ProcessadorAvatar().Normalizar([.. arquivo, .. marcador]);

        using var codec = SKCodec.Create(new MemoryStream(normalizado));
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        Assert.Equal(512, codec.Info.Width);
        Assert.Equal(128, codec.Info.Height);
        Assert.DoesNotContain("metadado-privado", Encoding.UTF8.GetString(normalizado));
        using var pixels = SKBitmap.Decode(normalizado);
        var pixel = pixels.GetPixel(10, 10);
        Assert.InRange(pixel.Red, (byte)250, (byte)255);
        Assert.InRange(pixel.Green, (byte)0, (byte)5);
        Assert.InRange(pixel.Blue, (byte)0, (byte)5);
    }

    [Fact]
    public void Normalizar_RejeitaDimensoesExcessivasAntesDeDecodificarPixels()
    {
        var arquivo = CriarImagem(2049, 1, SKEncodedImageFormat.Png);
        Assert.Throws<ArgumentException>(() => new ProcessadorAvatar().Normalizar(arquivo));
    }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("nao sou imagem")]
    public void Normalizar_RejeitaConteudoQueNaoEImagem(string conteudo)
    {
        Assert.Throws<ArgumentException>(() => new ProcessadorAvatar().Normalizar(Encoding.UTF8.GetBytes(conteudo)));
    }

    [Fact]
    public void Normalizar_RejeitaGifMesmoSendoImagemValida()
    {
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        Assert.Throws<ArgumentException>(() => new ProcessadorAvatar().Normalizar(gif));
    }

    [Fact]
    public void Normalizar_RejeitaPngTruncado()
    {
        var png = CriarImagem(512, 512, SKEncodedImageFormat.Png);
        Assert.Throws<ArgumentException>(() => new ProcessadorAvatar().Normalizar(png[..(png.Length / 2)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(LimitesAvatar.TamanhoMaximo + 1)]
    public async Task Atualizar_TamanhoInvalido_NaoProcessaNemPersiste(int tamanho)
    {
        var repositorio = new Mock<IRepositorioAvatar>(MockBehavior.Strict);
        var result = await new SolicitarAvatarCommandHandler(repositorio.Object)
            .Handle(new SolicitarAvatarCommand(Guid.NewGuid(), new byte[tamanho]), CancellationToken.None);

        Assert.Equal(TipoErro.Validacao, result.Errors[0].Metadata[nameof(TipoErro)]);
        repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Solicitar_NaoDecodificaImagem_RetornaIdPendente()
    {
        var usuarioId = Guid.NewGuid();
        byte[] arquivo = [1, 2, 3];
        var pendente = new ProcessamentoAvatarDto(Guid.NewGuid(), "pending", null);
        var repositorio = new Mock<IRepositorioAvatar>(MockBehavior.Strict);
        repositorio.Setup(x => x.SolicitarAsync(usuarioId, arquivo, CancellationToken.None)).ReturnsAsync(pendente);
        var result = await new SolicitarAvatarCommandHandler(repositorio.Object)
            .Handle(new SolicitarAvatarCommand(usuarioId, arquivo), CancellationToken.None);

        Assert.Equal(pendente, result.Value);
    }

    [Fact]
    public async Task Remover_NaoExigeImagem_EUsaOTitularInformado()
    {
        var id = Guid.NewGuid();
        var repositorio = new Mock<IRepositorioAvatar>(MockBehavior.Strict);
        repositorio.Setup(x => x.RemoverAsync(id, CancellationToken.None)).ReturnsAsync(true);

        var result = await new RemoverAvatarCommandHandler(repositorio.Object)
            .Handle(new RemoverAvatarCommand(id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consumer_FinalizaImagemOuRejeitaSemPublicarOriginal(bool invalida)
    {
        var id = Guid.NewGuid();
        byte[] arquivo = [1, 2, 3];
        byte[] normalizado = [4, 5, 6];
        var repositorio = new Mock<IRepositorioAvatar>();
        repositorio.Setup(x => x.IniciarProcessamentoAsync(id, CancellationToken.None)).ReturnsAsync(arquivo);
        var processador = new Mock<IProcessadorAvatar>();
        if (invalida)
            processador.Setup(x => x.Normalizar(arquivo)).Throws(new ArgumentException("Imagem inválida."));
        else
            processador.Setup(x => x.Normalizar(arquivo)).Returns(normalizado);
        var contexto = new Mock<ConsumeContext<ProcessarAvatarMessage>>();
        contexto.SetupGet(x => x.Message).Returns(new ProcessarAvatarMessage(id));

        await new ProcessarAvatarConsumer(repositorio.Object, processador.Object,
            NullLogger<ProcessarAvatarConsumer>.Instance).Consume(contexto.Object);

        repositorio.Verify(x => x.FinalizarAsync(id, invalida ? null : normalizado,
            invalida ? "Imagem inválida." : null, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Consumer_MensagemDuplicadaOuCancelada_NaoDecodifica()
    {
        var id = Guid.NewGuid();
        var repositorio = new Mock<IRepositorioAvatar>();
        repositorio.Setup(x => x.IniciarProcessamentoAsync(id, CancellationToken.None)).ReturnsAsync((byte[]?)null);
        var processador = new Mock<IProcessadorAvatar>(MockBehavior.Strict);
        var contexto = new Mock<ConsumeContext<ProcessarAvatarMessage>>();
        contexto.SetupGet(x => x.Message).Returns(new ProcessarAvatarMessage(id));

        await new ProcessarAvatarConsumer(repositorio.Object, processador.Object,
            NullLogger<ProcessarAvatarConsumer>.Instance).Consume(contexto.Object);

        processador.VerifyNoOtherCalls();
        repositorio.Verify(x => x.FinalizarAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static byte[] CriarImagem(int largura, int altura, SKEncodedImageFormat formato)
    {
        using var bitmap = new SKBitmap(largura, altura);
        bitmap.Erase(SKColors.Red);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(formato, 100);
        return dados.ToArray();
    }
}
