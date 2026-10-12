using PdfSharp.Pdf.IO;

namespace PdfProof.Tests;

// Encrypts a PDF in memory with PDFsharp (a test-only dependency): PdfPig reads encrypted PDFs but can't write them.
// Opening and saving draws nothing, so PDFsharp needs no font resolver.
internal static class EncryptedPdf
{
    // level: "rc4-128", "aes-128" or "aes-256". userPassword null: only an owner password (opens without one).
    public static byte[] Encrypt(byte[] pdf, string? userPassword, string ownerPassword, string level = "aes-256")
    {
        using var input = new MemoryStream(pdf);
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        if (userPassword is not null)
        {
            document.SecuritySettings.UserPassword = userPassword;
        }

        document.SecuritySettings.OwnerPassword = ownerPassword;
        switch (level)
        {
            case "rc4-128":
                document.SecurityHandler.SetEncryptionToV2With128Bits();
                break;
            case "aes-128":
                document.SecurityHandler.SetEncryptionToV4UsingAES();
                break;
            case "aes-256":
                document.SecurityHandler.SetEncryptionToV5();
                break;
            default:
                throw new ArgumentException($"Unknown encryption level: {level}", nameof(level));
        }

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }
}
