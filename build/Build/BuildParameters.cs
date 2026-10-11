// The build's inputs, read once in Setup from the command line (--name=value, or --flag for booleans).
public sealed record BuildParameters(
    string Configuration, DirectoryPath Artifacts, bool RequireFixtures, bool SkipGenerate, bool SkipFetch, string? Producers)
{
    public DirectoryPath Packages => Artifacts.Combine("packages");

    // GenerateFixtures writes here: <document>/<producer>.pdf and manifest.json.
    public DirectoryPath Corpus => Artifacts.Combine("corpus");

    // FetchCorpus writes here: <id>.pdf.
    public DirectoryPath External => Artifacts.Combine("external");

    // Pinned downloads (external PDFs, the Typst CLI), kept between runs and cached in CI.
    public DirectoryPath Cache => Artifacts.Combine("cache");

    public static BuildParameters Create(ICakeContext context) => new(
        context.Argument("configuration", "Release"),
        context.MakeAbsolute(context.Directory("artifacts")),
        context.HasArgument("require-fixtures"),
        context.HasArgument("skip-generate"),
        context.HasArgument("skip-fetch"),
        context.HasArgument("producers") ? context.Argument<string>("producers") : null);
}
