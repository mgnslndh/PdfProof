using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PdfProof.FixtureGenerator;

// The invoice (corpus/generated/invoice/) for QuestPDF and MigraDoc: the same blocks, links and footer as invoice.html
// and invoice.typ. Dates are fixed; no producer string is set (QuestPDF writes none, and the fixture must show that).
// QuestPDF and MigraDoc both define Document and Unit, so those names are written in full.
internal sealed class InvoiceDocument : ICSharpDocument
{
    private static readonly DateTimeOffset Date = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

    public byte[] QuestPdf(bool tagged)
    {
        var document = QuestPDF.Fluent.Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().SemanticHeading1().Text("Invoice").FontSize(20).Bold();
                column.Item().SemanticParagraph().Text(text =>
                {
                    text.Span("Questions? Visit ");
                    text.Hyperlink("our support site", "https://example.com/support");
                    text.Span(" or e-mail us.");
                });
                column.Item().SemanticParagraph().SectionLink("terms").Text("Read the terms and conditions");
                column.Item().SemanticTable().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Text("Item").Bold();
                        header.Cell().Text("Qty").Bold();
                        header.Cell().Text("Amount").Bold();
                    });
                    table.Cell().Text("Consulting, senior");
                    table.Cell().Text("10");
                    table.Cell().Text("14 500,00 SEK");
                });
                column.Item().PageBreak();
                column.Item().Section("terms").SemanticHeading2().Text("Terms and conditions").FontSize(14).Bold();
                column.Item().SemanticList().Column(list => list.Item().SemanticListItem().Row(row =>
                {
                    row.ConstantItem(12).SemanticListLabel().Text("-");
                    row.RelativeItem().SemanticListItemBody().Text("Payment within 30 days.");
                }));
            });
        }));
        document = document.WithMetadata(new DocumentMetadata { Title = "Invoice", Language = "en", CreationDate = Date, ModifiedDate = Date });
        if (tagged)
        {
            document = document.WithSettings(new DocumentSettings { PDFUA_Conformance = PDFUA_Conformance.PDFUA_1 });
        }

        return document.GeneratePdf();
    }

    public byte[] MigraDoc()
    {
        var document = new MigraDoc.DocumentObjectModel.Document();
        document.Info.Title = "Invoice";
        var section = document.AddSection();

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        var title = section.AddParagraph("Invoice");
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;

        var support = section.AddParagraph("Questions? Visit ");
        support.AddHyperlink("https://example.com/support", HyperlinkType.Url).AddText("our support site");
        support.AddText(" or e-mail us.");

        section.AddParagraph().AddHyperlink("terms").AddText("Read the terms and conditions");

        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.AddColumn("6cm");
        table.AddColumn("2cm");
        table.AddColumn("4cm");
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph("Item");
        header.Cells[1].AddParagraph("Qty");
        header.Cells[2].AddParagraph("Amount");
        var row = table.AddRow();
        row.Cells[0].AddParagraph("Consulting, senior");
        row.Cells[1].AddParagraph("10");
        row.Cells[2].AddParagraph("14 500,00 SEK");

        section.AddPageBreak();
        var terms = section.AddParagraph("Terms and conditions");
        terms.Format.Font.Size = 14;
        terms.Format.Font.Bold = true;
        terms.AddBookmark("terms");
        section.AddParagraph("- Payment within 30 days.");

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Info.CreationDate = Date.UtcDateTime;
        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output);
        return output.ToArray();
    }
}
