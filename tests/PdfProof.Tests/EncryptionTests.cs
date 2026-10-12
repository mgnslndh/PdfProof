namespace PdfProof.Tests;

public sealed class EncryptionTests
{
    private const string Secret = "s3cret-Pa55";

    [Theory]
    [InlineData("rc4-128", "user")]
    [InlineData("rc4-128", "owner")]
    [InlineData("aes-128", "user")]
    [InlineData("aes-128", "owner")]
    [InlineData("aes-256", "user")]
    [InlineData("aes-256", "owner")]
    public void An_encrypted_pdf_loads_with_its_user_or_owner_password(string level, string password)
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), "user", "owner", level);

        var doc = PdfProofDocument.Load(pdf, new PdfProofOptions { Password = password });

        Assert.Equal("Confidential sample", doc.Layout.Pages[0].Text);
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Encrypted: The PDF is encrypted; it opened with the password given in the options, or needed none.");
    }

    [Fact]
    public void The_profile_is_still_detected_when_a_password_is_given()
    {
        var pdf = EncryptedPdf.Encrypt(Plain("<< /Producer (Select.Pdf Html To Pdf for .NET Community Edition v26.4.0) >>"), "user", "owner");

        var doc = PdfProofDocument.Load(pdf, new PdfProofOptions { Password = "user" });

        Assert.Same(PdfProfile.SelectPdf, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
    }

    [Fact]
    public void Without_a_password_the_error_says_how_to_give_one()
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), "user", "owner");

        var ex = Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(pdf));

        Assert.Equal("The PDF is encrypted and needs a password. Pass it in the options: new PdfProofOptions { Password = \"...\" }.", ex.Message);
        Assert.Equal("PdfDocumentEncryptedException", ex.InnerException?.GetType().Name);
    }

    [Fact]
    public void A_wrong_password_gets_its_own_error_without_the_password_in_it()
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), "user", "owner");

        var ex = Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(pdf, new PdfProofOptions { Password = Secret }));

        Assert.Equal("The PDF is encrypted, and the password in the options is neither its user password nor its owner password.", ex.Message);
        Assert.DoesNotContain(Secret, ex.ToString());
    }

    [Fact]
    public void A_pdf_with_only_an_owner_password_opens_without_one()
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), userPassword: null, "owner");

        var doc = PdfProofDocument.Load(pdf);

        Assert.Equal("Confidential sample", doc.Layout.Pages[0].Text);
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Encrypted: The PDF is encrypted; it opened without a password.");
    }

    [Fact]
    public void A_password_for_an_unencrypted_pdf_is_ignored()
    {
        var doc = PdfProofDocument.Load(Plain(), new PdfProofOptions { Password = Secret });

        Assert.Equal("Confidential sample", doc.Layout.Pages[0].Text);
        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.Encrypted);
    }

    [Fact]
    public void The_password_never_appears_in_diagnostics_logs_or_the_options()
    {
        // Review Focus 1.
        var logger = new ListLogger();
        var options = new PdfProofOptions { Password = Secret, Logger = logger };
        var pdf = EncryptedPdf.Encrypt(Plain(), Secret, "owner");

        var doc = PdfProofDocument.Load(pdf, options);

        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Message.Contains(Secret));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(Secret));
        Assert.DoesNotContain(Secret, options.ToString());
        Assert.Contains("Password = ***", options.ToString());
        Assert.Contains("Password = (none)", PdfProofOptions.Default.ToString());
    }

    private static byte[] Plain(string? info = null)
    {
        var pdf = new RawPdf { Info = info };
        pdf.AddPage(RawPdf.Text(50, 750, "Confidential sample"));
        return pdf.Build();
    }

    [Fact]
    public void An_owner_only_pdf_opened_with_a_password_does_not_claim_the_password_was_used()
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), userPassword: null, "owner");

        var doc = PdfProofDocument.Load(pdf, new PdfProofOptions { Password = "anything" });

        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Encrypted: The PDF is encrypted; it opened with the password given in the options, or needed none.");
    }
}
