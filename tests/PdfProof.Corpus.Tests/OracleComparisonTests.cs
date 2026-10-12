using PdfProof.Tests;

namespace PdfProof.Corpus.Tests;

public sealed class OracleComparisonTests
{
    private static readonly Oracle Expected = Oracle.Parse(
        """
        {
          "formatVersion": 1,
          "metadata": { "title": "Invoice", "pageCount": 2 },
          "semantics": {
            "blocks": [
              { "type": "heading", "level": 1, "text": "Invoice", "page": 1 },
              { "type": "paragraph", "text": "Visit our support site", "page": 1,
                "links": [{ "text": "Visit our support site", "uri": "https://example.com/support" }] },
              { "type": "paragraph", "text": "Read the terms and conditions", "page": 1,
                "links": [{ "text": "Read the terms and conditions", "page": 2 }] },
              { "type": "table", "page": 1, "header": ["Item", "Qty", "Amount"],
                "rows": [["Consulting, senior", "10", "14 500,00 SEK"]] },
              { "type": "heading", "level": 2, "text": "Terms and conditions", "page": 2 },
              { "type": "list", "page": 2, "items": ["Payment within 30 days."] }
            ],
            "artifacts": [{ "role": "footer", "text": "Page {n} of {N}", "pages": "1-2" }]
          }
        }
        """);

    [Fact]
    public void A_document_that_shows_everything_in_order_has_no_mismatches()
    {
        Assert.Empty(OracleComparison.Compare(Expected, Invoice(), checkRoles: false));
    }

    [Fact]
    public void A_different_title_is_a_mismatch()
    {
        Assert.Equal(["title: expected \"Invoice\", was \"Bill\""], OracleComparison.Compare(Expected, Invoice(title: "Bill"), checkRoles: false));
    }

    [Fact]
    public void Missing_text_is_reported_with_its_block_and_page()
    {
        Assert.Equal(
            ["block 5 (heading): \"Terms and conditions\" not found on page 2 after the previous block's text"],
            OracleComparison.Compare(Expected, Invoice(terms: "Terms"), checkRoles: false));
    }

    [Fact]
    public void Text_in_the_wrong_order_is_a_mismatch()
    {
        var swapped = Expected with { Blocks = [Expected.Blocks[0], Expected.Blocks[2], Expected.Blocks[1], .. Expected.Blocks.Skip(3)] };

        Assert.Equal(
            ["block 3 (paragraph): \"Visit our support site\" not found on page 1 after the previous block's text"],
            OracleComparison.Compare(swapped, Invoice(), checkRoles: false));
    }

    [Fact]
    public void A_link_to_the_wrong_target_is_a_mismatch()
    {
        Assert.Equal(
            ["block 2 (paragraph): no link \"Visit our support site\" to https://example.com/support on page 1"],
            OracleComparison.Compare(Expected, Invoice(support: "https://example.com/other"), checkRoles: false));
    }

    [Fact]
    public void A_missing_footer_is_reported_for_its_page()
    {
        Assert.Equal(
            ["page 2: footer \"Page 2 of 2\" not found"],
            OracleComparison.Compare(Expected, Invoice(footerOnSecondPage: false), checkRoles: false));
    }

    [Fact]
    public void Tagged_documents_must_declare_the_blocks_roles_in_order()
    {
        var oracle = Oracle.Parse(
            """
            { "semantics": { "blocks": [
              { "type": "heading", "level": 1, "text": "Invoice" },
              { "type": "paragraph", "text": "Hello" },
              { "type": "list", "items": ["Payment within 30 days."] } ] } }
            """);
        var asLevelTwo = Oracle.Parse(
            """
            { "semantics": { "blocks": [ { "type": "heading", "level": 2, "text": "Invoice" } ] } }
            """);

        Assert.Empty(OracleComparison.Compare(oracle, Tagged(), checkRoles: true));
        var mismatch = Assert.Single(OracleComparison.Compare(asLevelTwo, Tagged(), checkRoles: true));
        Assert.Equal("structure: no H2 element for block 1 (heading) after the previous block's; roles in order: Document H1 P L LI", mismatch);
    }

    [Fact]
    public void An_unknown_block_type_is_rejected()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Oracle.Parse("""{ "semantics": { "blocks": [ { "type": "chart" } ] } }"""));

        Assert.Equal("Unknown block type \"chart\".", ex.Message);
    }

    [Fact]
    public void The_invoice_oracle_parses()
    {
        var oracle = Oracle.Load(Path.Combine(CorpusPaths.GeneratedDocument("invoice"), "expected.semantics.json"));

        Assert.Equal(("Invoice", (int?)2), (oracle.Title, oracle.PageCount));
        Assert.Equal(["heading", "paragraph", "paragraph", "table", "heading", "list"], oracle.Blocks.Select(b => b.Type));
        Assert.Equal(["Item", "Qty", "Amount", "Consulting, senior", "10", "14 500,00 SEK"], oracle.Blocks[3].Texts);
        Assert.Equal(["H1", "P", "P", "Table", "H2", "L"], oracle.Blocks.Select(b => b.Role));
    }

    // Two pages laid out like the oracle; each argument breaks one thing.
    private static PdfProofDocument Invoice(
        string title = "Invoice", string support = "https://example.com/support", string terms = "Terms and conditions", bool footerOnSecondPage = true)
    {
        var pdf = new RawPdf { Info = $"<< /Title ({title}) >>" };
        var first = pdf.AddPage(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Visit our support site") +
            RawPdf.Text(50, 680, "Read the terms and conditions") +
            RawPdf.Text(50, 640, "Item") + RawPdf.Text(250, 640, "Qty") + RawPdf.Text(350, 640, "Amount") +
            RawPdf.Text(50, 620, "Consulting, senior") + RawPdf.Text(250, 620, "10") + RawPdf.Text(350, 620, "14 500,00 SEK") +
            RawPdf.Text(270, 40, "Page 1 of 2", 10));
        var second = pdf.AddPage(
            RawPdf.Text(50, 750, terms, 14) +
            RawPdf.Text(50, 720, "Payment within 30 days.") +
            (footerOnSecondPage ? RawPdf.Text(270, 40, "Page 2 of 2", 10) : ""));
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /URI /URI ({support}) >> >>");
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 675 250 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        return PdfProofDocument.Load(pdf.Build());
    }

    private static PdfProofDocument Tagged()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) +
            RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello")) +
            RawPdf.Marked("LI", 2, RawPdf.Text(50, 650, "Payment within 30 days.")));
        var heading = pdf.Add($"<< /S /H1 /Pg {page} 0 R /K 0 >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K 1 >>");
        var item = pdf.Add($"<< /S /LI /Pg {page} 0 R /K 2 >>");
        var list = pdf.Add($"<< /S /L /K [{item} 0 R] >>");
        var document = pdf.Add($"<< /S /Document /K [{heading} 0 R {paragraph} 0 R {list} 0 R] >>");
        pdf.Tag($"{document} 0 R");
        return PdfProofDocument.Load(pdf.Build());
    }
}
