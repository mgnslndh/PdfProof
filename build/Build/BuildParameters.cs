// The build's inputs, read once in Setup from the command line (--name=value).
public sealed record BuildParameters(string Configuration, DirectoryPath Artifacts)
{
    public DirectoryPath Packages => Artifacts.Combine("packages");

    public static BuildParameters Create(ICakeContext context) => new(
        context.Argument("configuration", "Release"),
        context.MakeAbsolute(context.Directory("artifacts")));
}
