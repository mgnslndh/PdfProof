using PdfProof.FixtureGenerator;

// Renders the generated corpus (corpus/generated/<document>/) with every producer available on this machine into
// artifacts/corpus/<document>/<producer>.pdf, plus manifest.json. Run by the GenerateFixtures build target.
var (arguments, error) = GeneratorArguments.Parse(args);
if (arguments is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(GeneratorArguments.Usage);
    return 2;
}

var all = Producers.All(arguments.Typst);
var selected = Producers.Select(all, arguments.Producers, out var unknown);
if (unknown.Count > 0)
{
    Console.Error.WriteLine($"Unknown producers: {string.Join(", ", unknown)}. Known: {string.Join(", ", all.Select(p => p.Name))}.");
    return 2;
}

var documents = Directory.Exists(arguments.Source) ? CorpusDocument.Find(arguments.Source) : [];
if (documents.Count == 0)
{
    Console.Error.WriteLine($"No corpus documents (folders with expected.semantics.json) in {arguments.Source}.");
    return 2;
}

return await new Generator(selected, Console.Out).RunAsync(documents, arguments.Output, arguments.RequireAll);
