public static partial class Program
{
    // Renders corpus/generated with every producer available on this machine into artifacts/corpus
    // (tools/PdfProof.FixtureGenerator). Typst is the pinned CLI; on a platform without a pin, the generator uses typst
    // from PATH. --producers=a,b renders only those producers. With --require-fixtures, a producer that is unavailable
    // here (and isn't best effort) fails the target, so CI never caches an incomplete corpus.
    static void RegisterFixtureTasks()
    {
        Task("GenerateFixtures")
            .WithCriteria<BuildParameters>((_, parameters) => !parameters.SkipGenerate, "--skip-generate was given")
            .Does<BuildParameters>(async parameters =>
            {
                var arguments = new ProcessArgumentBuilder().Append("--output").AppendQuoted(parameters.Corpus.FullPath);
                try
                {
                    using var http = PinnedDownload.CreateHttpClient();
                    var typst = await TypstCli.EnsureAsync(new PinnedDownload(http), parameters.Cache.FullPath);
                    arguments.Append("--typst").AppendQuoted(typst);
                }
                catch (PlatformNotSupportedException ex)
                {
                    Warning(ex.Message);
                }

                if (parameters.RequireFixtures)
                {
                    arguments.Append("--require-all");
                }

                if (parameters.Producers is { } producers)
                {
                    arguments.Append("--producers").AppendQuoted(producers);
                }

                DotNetRun(
                    "tools/PdfProof.FixtureGenerator/PdfProof.FixtureGenerator.csproj",
                    arguments,
                    new DotNetRunSettings { Configuration = parameters.Configuration });
            });
    }
}
