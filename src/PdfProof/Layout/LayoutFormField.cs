namespace PdfProof.Layout;

/// <summary>
/// One widget of an AcroForm field: where a field appears on a page. A field shown in several places, such as the
/// buttons of a radio group, has one entry per widget, all with the same <see cref="Name"/>.
/// </summary>
public sealed class LayoutFormField
{
    internal LayoutFormField(string name, LayoutFormFieldKind kind, string? value, bool? isChecked, IReadOnlyList<string> options, string? tooltip, bool isReadOnly, bool isRequired, LayoutBox box)
    {
        Name = name;
        Kind = kind;
        Value = value;
        IsChecked = isChecked;
        Options = ReadOnly.List(options);
        Tooltip = tooltip;
        IsReadOnly = isReadOnly;
        IsRequired = isRequired;
        Box = box;
    }

    /// <summary>The field's full name: the partial names from the root, joined by dots (e.g. <c>person.name</c>).</summary>
    public string Name { get; }

    /// <summary>What kind of field this is.</summary>
    public LayoutFormFieldKind Kind { get; }

    /// <summary>
    /// The value. Text fields: their text. Checkboxes and radio buttons: the widget's state name (<c>Off</c>, or the
    /// name of its on-state, e.g. <c>Yes</c>). Combo and list boxes: the selected options' names joined by
    /// <c>", "</c>. <see langword="null"/> when there is none, and for push buttons and signatures.
    /// </summary>
    public string? Value { get; }

    /// <summary>Checkboxes and radio buttons: whether this widget is on. <see langword="null"/> for other kinds.</summary>
    public bool? IsChecked { get; }

    /// <summary>Combo and list boxes: the option names, in order. Empty for other kinds.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>The field's tooltip (<c>/TU</c>), often the accessible name; <see langword="null"/> when absent.</summary>
    public string? Tooltip { get; }

    /// <summary>The read-only flag is set.</summary>
    public bool IsReadOnly { get; }

    /// <summary>The required flag is set.</summary>
    public bool IsRequired { get; }

    /// <summary>The widget rectangle.</summary>
    public LayoutBox Box { get; }
}
