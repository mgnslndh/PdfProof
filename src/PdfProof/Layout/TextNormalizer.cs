using System.Text;

namespace PdfProof.Layout;

// Normalizes extracted text so assertions don't depend on how a producer encoded characters. Found with SelectPdf
// (WebKit): every '-' in the HTML comes out of the PDF as U+00AD (soft hyphen).
internal static class TextNormalizer
{
    public static string Normalize(string text)
    {
        if (!NeedsWork(text))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (IsHyphen(c))
            {
                // A soft hyphen that was actually drawn is a visible hyphen; Unicode hyphens likewise.
                result.Append('-');
            }
            else if (IsSpecialSpace(c))
            {
                result.Append(' ');
            }
            else if (IsZeroWidth(c))
            {
                // Zero-width characters and the BOM carry nothing visible.
            }
            else if (IsLigature(c))
            {
                result.Append(c.ToString().Normalize(NormalizationForm.FormKC));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private static bool NeedsWork(string text)
    {
        foreach (var c in text)
        {
            if (IsHyphen(c) || IsSpecialSpace(c) || IsZeroWidth(c) || IsLigature(c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHyphen(char c) => c is '\u00AD' or '\u2010' or '\u2011';

    // No-break, en quad .. hair space (U+2000-U+200A), narrow no-break, medium mathematical and ideographic space.
    private static bool IsSpecialSpace(char c) =>
        c is '\u00A0' or (>= '\u2000' and <= '\u200A') or '\u202F' or '\u205F' or '\u3000';

    private static bool IsZeroWidth(char c) => c is '\u200B' or '\u200C' or '\u200D' or '\u2060' or '\uFEFF';

    // Latin ligatures U+FB00-U+FB06 (ff, fi, fl, ffi, ffl, long st, st).
    private static bool IsLigature(char c) => c is >= '\uFB00' and <= '\uFB06';
}
