namespace PdfProof.Layout;

/// <summary>What kind of AcroForm field a <see cref="LayoutFormField"/> belongs to.</summary>
public enum LayoutFormFieldKind
{
    /// <summary>A field whose type the PDF doesn't state or PdfProof doesn't know. The default value.</summary>
    Unknown,

    /// <summary>A text field.</summary>
    Text,

    /// <summary>A checkbox.</summary>
    Checkbox,

    /// <summary>One button of a radio group.</summary>
    RadioButton,

    /// <summary>A combo box (drop-down list).</summary>
    ComboBox,

    /// <summary>A list box.</summary>
    ListBox,

    /// <summary>A push button.</summary>
    PushButton,

    /// <summary>A signature field.</summary>
    Signature,
}
