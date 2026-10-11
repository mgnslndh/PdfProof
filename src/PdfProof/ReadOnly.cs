using System.Collections.ObjectModel;

namespace PdfProof;

// Copies into collections that can't be changed through a cast: the model is shared between threads (spec D4).
internal static class ReadOnly
{
    public static IReadOnlyList<T> List<T>(IEnumerable<T> items) => new ReadOnlyCollection<T>(items.ToArray());
}
