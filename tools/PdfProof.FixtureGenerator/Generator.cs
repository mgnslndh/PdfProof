namespace PdfProof.FixtureGenerator;

// Renders every corpus document with every producer into <output>/<document>/<producer>.pdf, writes manifest.json,
// and decides the exit code: 1 when a producer that isn't best effort failed, otherwise 0. An unavailable producer is
// a warning: each machine renders what it can. With requireAll (CI's fixtures job), an unavailable producer that isn't
// best effort fails the run too, so an incomplete corpus is never cached as if it were complete.
internal sealed class Generator(IReadOnlyList<IProducer> producers, TextWriter log)
{
    public async Task<int> RunAsync(IReadOnlyList<CorpusDocument> documents, string output, bool requireAll = false)
    {
        Directory.CreateDirectory(output);
        var entries = new List<ManifestEntry>();
        var failed = false;
        foreach (var producer in producers)
        {
            // A previous run's PDF must never stand in for this run's result.
            foreach (var document in documents)
            {
                var stale = FixturePath(output, document, producer);
                if (File.Exists(stale))
                {
                    File.Delete(stale);
                }
            }

            string? unavailable;
            try
            {
                unavailable = await producer.CheckAsync();
            }
            catch (Exception ex)
            {
                // A check that throws (a hung or missing tool) fails this producer only; the run goes on.
                log.WriteLine($"{(producer.BestEffort ? "warning" : "error")}: {producer.Name} could not be checked: {ex.Message}");
                entries.Add(new ManifestEntry(producer.Name, null, "failed", "check: " + ex.Message));
                failed |= !producer.BestEffort;
                continue;
            }

            if (unavailable is not null)
            {
                var required = requireAll && !producer.BestEffort;
                log.WriteLine($"{(required ? "error" : "warning")}: {producer.Name} is unavailable: {unavailable}");
                entries.Add(new ManifestEntry(producer.Name, null, "unavailable", unavailable));
                failed |= required;
                continue;
            }

            string? error = null;
            foreach (var document in documents)
            {
                try
                {
                    var pdf = await producer.RenderAsync(document);
                    var path = FixturePath(output, document, producer);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, pdf);
                    log.WriteLine($"{document.Name}/{producer.Name}.pdf: {pdf.Length} bytes");
                }
                catch (Exception ex)
                {
                    error ??= $"{document.Name}: {ex.Message}";
                    log.WriteLine($"{(producer.BestEffort ? "warning" : "error")}: {producer.Name} failed on {document.Name}: {ex.Message}");
                }
            }

            entries.Add(new ManifestEntry(producer.Name, producer.Version, error is null ? "generated" : "failed", error));
            failed |= error is not null && !producer.BestEffort;
        }

        Manifest.Write(Path.Combine(output, "manifest.json"), documents.Select(document => document.Name).ToList(), entries);
        return failed ? 1 : 0;
    }

    private static string FixturePath(string output, CorpusDocument document, IProducer producer) =>
        Path.Combine(output, document.Name, producer.Name + ".pdf");
}
