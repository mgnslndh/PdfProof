namespace PdfProof.FixtureGenerator;

// The command line. Paths are relative to the working directory; the build runs from the repository root.
// --require-all: an unavailable producer that isn't best effort fails the run (CI's fixtures job).
internal sealed record GeneratorArguments(string Source, string Output, IReadOnlyList<string>? Producers, string Typst, bool RequireAll = false)
{
    public const string Usage =
        "Usage: PdfProof.FixtureGenerator [--source <dir>] [--output <dir>] [--producers <name,name,...>] [--typst <path>] [--require-all]";

    public static (GeneratorArguments? Arguments, string? Error) Parse(IReadOnlyList<string> args)
    {
        var source = Path.Combine("corpus", "generated");
        var output = Path.Combine("artifacts", "corpus");
        var typst = "typst";
        IReadOnlyList<string>? producers = null;
        var requireAll = false;
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name == "--require-all")
            {
                requireAll = true;
                continue;
            }

            if (name is not ("--source" or "--output" or "--producers" or "--typst"))
            {
                return (null, $"Unknown argument: {name}");
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return (null, $"{name} needs a value");
            }

            var value = args[++i];
            switch (name)
            {
                case "--source":
                    source = value;
                    break;
                case "--output":
                    output = value;
                    break;
                case "--producers":
                    producers = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    break;
                default:
                    typst = value;
                    break;
            }
        }

        return (new GeneratorArguments(source, output, producers, typst, requireAll), null);
    }
}
