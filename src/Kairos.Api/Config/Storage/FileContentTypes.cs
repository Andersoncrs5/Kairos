using System.IO.Compression;

namespace Kairos.Api.Config.Storage;

public static class FileContentTypes
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";
    public const string Pdf = "application/pdf";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    // SVG e HTML ficam de fora de propósito: podem carregar script (XSS)
    public static readonly IReadOnlySet<string> Images =
        new HashSet<string> { Jpeg, Png, Webp };

    public static readonly IReadOnlySet<string> ImagesAndPdf =
        new HashSet<string> { Jpeg, Png, Webp, Pdf };

    public static readonly IReadOnlySet<string> SupportDocuments =
        new HashSet<string> { Pdf, Docx, Xlsx, Pptx, Jpeg, Png, Webp };

    public static string ExtensionFor(string contentType) => contentType switch
    {
        Jpeg => ".jpg",
        Png => ".png",
        Webp => ".webp",
        Pdf => ".pdf",
        Docx => ".docx",
        Xlsx => ".xlsx",
        Pptx => ".pptx",
        _ => string.Empty
    };

    /// <summary>Lê o cabeçalho e devolve o tipo real, ou null se não reconhecer.</summary>
    internal static async Task<string?> DetectAsync(Stream stream, CancellationToken ct)
    {
        byte[] header = new byte[16];

        stream.Position = 0;
        int read = await stream.ReadAtLeastAsync(
            header, header.Length, throwOnEndOfStream: false, ct);

        return Classify(header, read, stream);
    }

    private static string? Classify(byte[] header, int length, Stream stream)
    {
        ReadOnlySpan<byte> h = header.AsSpan(0, length);

        if (h.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return Jpeg;
        }

        if (h.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return Png;
        }

        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        if (h.StartsWith("%PDF-"u8))
        {
            return Pdf;
        }

        // ZIP ("PK\x03\x04"): pode ser docx/xlsx/pptx; confirma pelas entradas
        if (h.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }))
        {
            return DetectOpenXml(stream);
        }

        return null;
    }

    private static string? DetectOpenXml(Stream stream)
    {
        try
        {
            stream.Position = 0;

            // Só lê o diretório central; não descompacta nada
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            if (zip.GetEntry("word/document.xml") is not null) return Docx;
            if (zip.GetEntry("xl/workbook.xml") is not null) return Xlsx;
            if (zip.GetEntry("ppt/presentation.xml") is not null) return Pptx;

            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}