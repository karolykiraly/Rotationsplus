namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Detects an uploaded document's type from its leading magic bytes — the authoritative gate for both
/// student and admin uploads. The client-declared content type is NEVER trusted (a mislabelled or
/// polyglot file can't be stored under a type it isn't). Accepts the legacy format set:
/// PDF, JPEG, PNG, BMP, DOC (OLE2), DOCX (Office Open XML / ZIP).
/// </summary>
public static class DocumentContentTypeDetector
{
    public const string Pdf = "application/pdf";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Bmp = "image/bmp";
    public const string Doc = "application/msword";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Returns the canonical content type for a supported document, or null if the bytes don't
    /// match one. NOTE: DOCX shares the ZIP (PK) signature, so any ZIP is accepted as DOCX — acceptable
    /// here because the file is only ever served back as a download, never executed.</summary>
    public static string? Detect(Stream stream)
    {
        stream.Position = 0;
        Span<byte> h = stackalloc byte[8];
        var read = stream.ReadAtLeast(h, h.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        if (read < 4)
        {
            return null;
        }

        // PDF: 25 50 44 46  ("%PDF")
        if (h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46) return Pdf;

        // JPEG: FF D8 FF
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return Jpeg;

        // BMP: 42 4D  ("BM")
        if (h[0] == 0x42 && h[1] == 0x4D) return Bmp;

        // DOCX / Office Open XML (and any ZIP): 50 4B 03 04  ("PK..")
        if (h[0] == 0x50 && h[1] == 0x4B && h[2] == 0x03 && h[3] == 0x04) return Docx;

        if (read < 8)
        {
            return null;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 &&
            h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return Png;

        // DOC (legacy OLE2 compound file): D0 CF 11 E0 A1 B1 1A E1
        if (h[0] == 0xD0 && h[1] == 0xCF && h[2] == 0x11 && h[3] == 0xE0 &&
            h[4] == 0xA1 && h[5] == 0xB1 && h[6] == 0x1A && h[7] == 0xE1) return Doc;

        return null;
    }

    /// <summary>The file extension to store a blob under, per detected content type.</summary>
    public static string ExtensionFor(string contentType) => contentType switch
    {
        Pdf => ".pdf",
        Jpeg => ".jpg",
        Png => ".png",
        Bmp => ".bmp",
        Doc => ".doc",
        Docx => ".docx",
        _ => string.Empty,
    };
}
