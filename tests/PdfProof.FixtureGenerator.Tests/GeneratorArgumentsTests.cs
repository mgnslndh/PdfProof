namespace PdfProof.FixtureGenerator.Tests;

public sealed class GeneratorArgumentsTests
{
    [Fact]
    public void Without_arguments_the_repository_defaults_are_used()
    {
        var (arguments, error) = GeneratorArguments.Parse([]);

        Assert.Null(error);
        Assert.Equal(Path.Combine("corpus", "generated"), arguments!.Source);
        Assert.Equal(Path.Combine("artifacts", "corpus"), arguments.Output);
        Assert.Null(arguments.Producers);
        Assert.Equal("typst", arguments.Typst);
        Assert.False(arguments.RequireAll);
    }

    [Fact]
    public void Every_option_is_read()
    {
        var (arguments, _) = GeneratorArguments.Parse(["--source", "src", "--require-all", "--output", "out", "--producers", "typst, selectpdf", "--typst", "tools/typst"]);

        Assert.Equal(("src", "out", "tools/typst"), (arguments!.Source, arguments.Output, arguments.Typst));
        Assert.Equal(["typst", "selectpdf"], arguments.Producers);
        Assert.True(arguments.RequireAll);
    }

    [Theory]
    [InlineData(new[] { "--unknown" }, "Unknown argument: --unknown")]
    [InlineData(new[] { "--output" }, "--output needs a value")]
    [InlineData(new[] { "--output", "--typst", "x" }, "--output needs a value")]
    public void Bad_arguments_are_reported(string[] args, string expected)
    {
        var (arguments, error) = GeneratorArguments.Parse(args);

        Assert.Null(arguments);
        Assert.Equal(expected, error);
    }
}
