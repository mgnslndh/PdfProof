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
    public void A_document_with_the_same_blocks_links_and_footer_has_no_mismatches()
    {
        Assert.Empty(OracleComparison.Compare(Expected, Invoice()));
    }

    [Fact]
    public void A_different_title_is_a_mismatch()
    {
        Assert.Equal(["title: expected \"Invoice\", was \"Bill\""], OracleComparison.Compare(Expected, Invoice(title: "Bill")));
    }

    [Fact]
    public void A_different_text_is_reported_with_its_block()
    {
        Assert.Equal(
            ["block 5 (heading): expected text \"Terms and conditions\", found \"Terms\""],
            OracleComparison.Compare(Expected, Invoice(terms: "Terms")));
    }

    [Fact]
    public void Blocks_in_another_order_are_a_mismatch()
    {
        var swapped = Expected with { Blocks = [Expected.Blocks[0], Expected.Blocks[2], Expected.Blocks[1], .. Expected.Blocks.Skip(3)] };

        Assert.Contains(
            "block 2 (paragraph): expected text \"Read the terms and conditions\", found \"Visit our support site\"",
            OracleComparison.Compare(swapped, Invoice()));
    }

    [Fact]
    public void A_missing_table_header_is_a_mismatch()
    {
        Assert.Contains(
            "block 4 (table): expected header [Item | Qty | Amount], found none",
            OracleComparison.Compare(Expected, Invoice(boldHeader: false)));
    }

    [Fact]
    public void A_link_to_the_wrong_target_is_a_mismatch()
    {
        Assert.Equal(
            ["block 2 (paragraph): no link \"Visit our support site\" to https://example.com/support"],
            OracleComparison.Compare(Expected, Invoice(support: "https://example.com/other")));
    }

    [Fact]
    public void An_artifact_on_other_pages_than_expected_is_a_mismatch()
    {
        Assert.Equal(
            ["artifact: expected footer \"Page {n} of {N}\" on pages 1-2, found footer \"Page {n} of {N}\" on pages 1"],
            OracleComparison.Compare(Expected, Invoice(footerOnSecondPage: false)));
    }

    // Two pages laid out like the oracle; each argument breaks one thing.
    private static PdfProofDocument Invoice(
        string title = "Invoice", string support = "https://example.com/support", string terms = "Terms and conditions",
        bool footerOnSecondPage = true, bool boldHeader = true)
    {
        var header = boldHeader ? "F2" : "F1";
        var pdf = new RawPdf { Info = $"<< /Title ({title}) >>" };
        var first = pdf.AddPage(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Visit our support site") +
            RawPdf.Text(50, 680, "Read the terms and conditions") +
            RawPdf.Text(50, 640, "Item", 12, header) + RawPdf.Text(250, 640, "Qty", 12, header) + RawPdf.Text(350, 640, "Amount", 12, header) +
            RawPdf.Text(50, 620, "Consulting, senior") + RawPdf.Text(250, 620, "10") + RawPdf.Text(350, 620, "14 500,00 SEK") +
            RawPdf.Text(270, 40, "Page 1 of 2", 10));
        var second = pdf.AddPage(
            RawPdf.Text(50, 750, terms, 14) +
            RawPdf.Text(50, 720, "- Payment within 30 days.") +
            (footerOnSecondPage ? RawPdf.Text(270, 40, "Page 2 of 2", 10) : ""));
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /URI /URI ({support}) >> >>");
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 675 250 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        return PdfProofDocument.Load(pdf.Build());
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
    }
}
