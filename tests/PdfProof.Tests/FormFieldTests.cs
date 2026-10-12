using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class FormFieldTests
{
    [Fact]
    public void Text_fields_have_full_names_values_tooltips_flags_and_boxes()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 750, "Name:"));
        var person = pdf.Reserve();
        var name = pdf.Widget(page, "/T (name) /FT /Tx /V (Ada) /TU (Full name) /Ff 3", 100, 700, 300, 720, parent: person);
        pdf.Set(person, $"<< /T (person) /Kids [{name} 0 R] >>");
        pdf.FormFields.Add(person);

        var field = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields);

        Assert.Equal("person.name", field.Name);
        Assert.Equal(LayoutFormFieldKind.Text, field.Kind);
        Assert.Equal("Ada", field.Value);
        Assert.Null(field.IsChecked);
        Assert.Empty(field.Options);
        Assert.Equal("Full name", field.Tooltip);
        Assert.True(field.IsReadOnly);
        Assert.True(field.IsRequired);
        Assert.Equal(new LayoutBox(100, 700, 300, 720), field.Box);
    }

    [Fact]
    public void An_empty_text_field_has_no_value()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (city) /FT /Tx", 100, 700, 300, 720));

        var field = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields);

        Assert.Null(field.Value);
        Assert.False(field.IsReadOnly);
        Assert.False(field.IsRequired);
    }

    [Fact]
    public void Checkboxes_and_radio_buttons_have_one_entry_per_widget_with_their_state()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        var on = pdf.Appearance();
        pdf.FormFields.Add(pdf.Widget(page, $"/T (agree) /FT /Btn /V /Yes /AS /Yes /AP << /N << /Yes {on} 0 R /Off {on} 0 R >> >>", 100, 600, 110, 610));
        var group = pdf.Reserve();
        var a = pdf.Widget(page, $"/AS /Off /AP << /N << /a {on} 0 R /Off {on} 0 R >> >>", 100, 500, 110, 510, parent: group);
        var b = pdf.Widget(page, $"/AS /b /AP << /N << /b {on} 0 R /Off {on} 0 R >> >>", 120, 500, 130, 510, parent: group);
        pdf.Set(group, $"<< /T (choice) /FT /Btn /Ff 49152 /V /b /Kids [{a} 0 R {b} 0 R] >>");
        pdf.FormFields.Add(group);

        var fields = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields;

        Assert.Equal(
            [
                ("agree", LayoutFormFieldKind.Checkbox, (string?)"Yes", (bool?)true),
                ("choice", LayoutFormFieldKind.RadioButton, "Off", false),
                ("choice", LayoutFormFieldKind.RadioButton, "b", true),
            ],
            fields.Select(f => (f.Name, f.Kind, f.Value, f.IsChecked)));
    }

    [Fact]
    public void Choice_fields_list_their_options_and_the_selection()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (size) /FT /Ch /Ff 131072 /Opt [(Small) (Medium) (Large)] /V (Medium)", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(page, "/T (extras) /FT /Ch /Ff 2097152 /Opt [(Wrap) (Card) (Ribbon)] /V [(Wrap) (Ribbon)]", 100, 600, 200, 680));

        var fields = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields;

        var size = fields.Single(f => f.Name == "size");
        Assert.Equal((LayoutFormFieldKind.ComboBox, (string?)"Medium"), (size.Kind, size.Value));
        Assert.Equal(["Small", "Medium", "Large"], size.Options);
        var extras = fields.Single(f => f.Name == "extras");
        Assert.Equal((LayoutFormFieldKind.ListBox, (string?)"Wrap, Ribbon"), (extras.Kind, extras.Value));
    }

    [Fact]
    public void Push_buttons_and_signatures_have_no_value()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (send) /FT /Btn /Ff 65536", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(page, "/T (signature) /FT /Sig", 100, 600, 300, 650));

        var fields = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields;

        Assert.Equal(
            [("send", LayoutFormFieldKind.PushButton, (string?)null), ("signature", LayoutFormFieldKind.Signature, null)],
            fields.Select(f => (f.Name, f.Kind, f.Value)));
    }

    [Fact]
    public void Widgets_go_to_their_own_page_in_reading_order()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage("");
        var second = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(second, "/T (later) /FT /Tx", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(first, "/T (right) /FT /Tx", 300, 700, 400, 720));
        pdf.FormFields.Add(pdf.Widget(first, "/T (left) /FT /Tx", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(first, "/T (top) /FT /Tx", 300, 750, 400, 770));

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(["top", "left", "right"], doc.Layout.Pages[0].FormFields.Select(f => f.Name));
        Assert.Equal(["later"], doc.Layout.Pages[1].FormFields.Select(f => f.Name));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "FormFields: 4 form field widgets");
    }

    [Fact]
    public void Widgets_are_not_reported_as_other_annotations()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (name) /FT /Tx", 100, 700, 200, 720));

        Assert.Empty(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Annotations);
    }

    [Fact]
    public void A_widget_without_a_page_is_left_out_and_counted()
    {
        // Review Focus 3: PdfPig gives a widget without /P no page number.
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (kept) /FT /Tx", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Add("<< /Type /Annot /Subtype /Widget /T (lost) /FT /Tx /V (x) /Rect [10 10 50 30] >>"));

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(["kept"], doc.Layout.Pages[0].FormFields.Select(f => f.Name));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "FormFields: 1 form field widgets; 1 without a page or box are left out");
    }

    [Fact]
    public void A_damaged_form_is_left_out_and_the_document_still_loads()
    {
        // Review Focus 2: a field reference to a missing object makes PdfPig's form reader throw.
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        pdf.FormFields.Add(99);

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal("Hello", doc.Layout.Pages[0].Text);
        Assert.Empty(doc.Layout.Pages[0].FormFields);
        var warning = Assert.Single(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.FormFieldsUnreadable);
        Assert.StartsWith("The form fields could not be read and are left out: ", warning.Message);
    }

    [Fact]
    public void Fields_nested_deeper_than_the_limit_are_left_out_with_a_warning()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        const int depth = 300;
        var parents = Enumerable.Range(0, depth).Select(_ => pdf.Reserve()).ToList();
        var leaf = pdf.Widget(page, "/T (leaf) /FT /Tx /V (deep)", 0, 0, 10, 10, parent: parents[depth - 1]);
        for (var i = 0; i < depth; i++)
        {
            var kid = i + 1 < depth ? parents[i + 1] : leaf;
            var parentEntry = i > 0 ? $" /Parent {parents[i - 1]} 0 R" : "";
            pdf.Set(parents[i], $"<< /T (n{i}) /Kids [{kid} 0 R]{parentEntry} >>");
        }

        pdf.FormFields.Add(parents[0]);

        var doc = PdfProofDocument.Load(pdf.Build());

        // Either PdfPig rejects the depth or the reader's limit (256) does; both leave the field out with a warning.
        Assert.Empty(doc.Layout.Pages[0].FormFields);
        Assert.Contains(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.FormFieldsUnreadable);
    }

    [Fact]
    public void A_pdf_without_a_form_has_no_form_fields_and_no_form_diagnostic()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello")));

        Assert.Empty(doc.Layout.Pages[0].FormFields);
        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Code is PdfDiagnosticCodes.FormFields or PdfDiagnosticCodes.FormFieldsUnreadable);
    }

    [Fact]
    public void A_field_shown_in_two_places_has_an_entry_per_widget()
    {
        // Review finding: PdfPig drops the widget kids of text, choice and signature fields.
        var pdf = new RawPdf();
        var first = pdf.AddPage("");
        var second = pdf.AddPage("");
        var city = pdf.Reserve();
        var top = pdf.Widget(first, "", 100, 700, 300, 720, parent: city);
        var bottom = pdf.Widget(second, "", 100, 100, 300, 120, parent: city);
        pdf.Set(city, $"<< /T (city) /FT /Tx /V (Paris) /TU (City) /Kids [{top} 0 R {bottom} 0 R] >>");
        pdf.FormFields.Add(city);

        var doc = PdfProofDocument.Load(pdf.Build());

        var onFirst = Assert.Single(doc.Layout.Pages[0].FormFields);
        Assert.Equal(("city", LayoutFormFieldKind.Text, (string?)"Paris", (string?)"City", new LayoutBox(100, 700, 300, 720)), (onFirst.Name, onFirst.Kind, onFirst.Value, onFirst.Tooltip, onFirst.Box));
        var onSecond = Assert.Single(doc.Layout.Pages[1].FormFields);
        Assert.Equal(("city", new LayoutBox(100, 100, 300, 120)), (onSecond.Name, onSecond.Box));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "FormFields: 2 form field widgets");
    }

    [Fact]
    public void A_widget_kid_without_P_gets_its_page_from_the_page_annotations()
    {
        var pdf = new RawPdf();
        pdf.AddPage("");
        var second = pdf.AddPage("");
        var city = pdf.Reserve();
        var widget = pdf.Add($"<< /Type /Annot /Subtype /Widget /Rect [100 700 300 720] /Parent {city} 0 R >>");
        pdf.Annotate(second, $"{widget} 0 R");
        pdf.Set(city, $"<< /T (city) /FT /Tx /V (Paris) /Kids [{widget} 0 R] >>");
        pdf.FormFields.Add(city);

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Empty(doc.Layout.Pages[0].FormFields);
        Assert.Equal("city", Assert.Single(doc.Layout.Pages[1].FormFields).Name);
    }

    [Fact]
    public void A_cyclic_field_tree_is_reported_and_the_document_still_loads()
    {
        // Review finding: PdfPig's form reader recurses through /Kids without a visited set, so a cycle overflowed the stack.
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        var a = pdf.Reserve();
        var b = pdf.Reserve();
        pdf.Set(a, $"<< /T (a) /Parent {b} 0 R /Kids [{b} 0 R] >>");
        pdf.Set(b, $"<< /T (b) /Parent {a} 0 R /Kids [{a} 0 R] >>");
        pdf.FormFields.Add(a);

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal("Hello", doc.Layout.Pages[0].Text);
        Assert.Empty(doc.Layout.Pages[0].FormFields);
        var warning = Assert.Single(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.FormFieldsUnreadable);
        Assert.Equal("The form fields could not be read and are left out: the field tree has a cycle.", warning.Message);
    }

    [Fact]
    public void An_unset_kind_is_unknown()
    {
        Assert.Equal(LayoutFormFieldKind.Unknown, default(LayoutFormFieldKind));
    }
}
