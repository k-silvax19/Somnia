using System.Buffers.Binary;
using SkiaSharp;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Infraestrutura.Modulos.Perfis;

public sealed class ProcessadorAvatar : IProcessadorAvatar
{
    public byte[] Normalizar(byte[] arquivo)
    {
        if (arquivo.Length is 0 or > LimitesAvatar.TamanhoMaximo)
            throw new ArgumentException("Envie uma imagem PNG ou JPEG de até 2 MiB.");

        using var dados = SKData.CreateCopy(arquivo);
        using var codec = SKCodec.Create(dados);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))
            throw new ArgumentException("O arquivo não é uma imagem PNG ou JPEG válida.");
        if (codec.FrameCount > 1)
            throw new ArgumentException("Somente imagens estáticas são permitidas.");
        if (codec.EncodedFormat == SKEncodedImageFormat.Png)
            ValidarPngEstatico(arquivo);
        if (codec.Info.Width > LimitesAvatar.DimensaoMaxima || codec.Info.Height > LimitesAvatar.DimensaoMaxima)
            throw new ArgumentException("A imagem deve ter no máximo 2048 por 2048 pixels.");

        using var imagem = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(imagem.Info, imagem.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException("A imagem está corrompida ou incompleta.");

        var escala = Math.Min(1d, (double)LimitesAvatar.DimensaoSaida / Math.Max(imagem.Width, imagem.Height));
        using var reduzida = imagem.Resize(new SKImageInfo(
            Math.Max(1, (int)(imagem.Width * escala)), Math.Max(1, (int)(imagem.Height * escala)),
            SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear));
        using var resultado = SKImage.FromBitmap(reduzida);
        // Um novo PNG contém somente os pixels; metadados e bytes anexados são descartados.
        using var png = resultado.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static void ValidarPngEstatico(ReadOnlySpan<byte> arquivo)
    {
        // Nem todo codec expõe os frames de APNG. Verifique os chunks antes do decode.
        var offset = 8;
        while (arquivo.Length - offset >= 12)
        {
            var tamanho = BinaryPrimitives.ReadUInt32BigEndian(arquivo.Slice(offset, 4));
            if (tamanho > arquivo.Length - offset - 12)
                break;
            var tipo = arquivo.Slice(offset + 4, 4);
            if (tipo.SequenceEqual("acTL"u8))
                throw new ArgumentException("Somente imagens estáticas são permitidas.");
            if (tipo.SequenceEqual("IEND"u8))
                return;
            offset += (int)tamanho + 12;
        }
        throw new ArgumentException("A imagem PNG está corrompida ou incompleta.");
    }
}
