public static partial class Program
{
    static void RegisterBuildTasks()
    {
        Task("Build")
            .Does<BuildParameters>(parameters =>
                DotNetBuild("PdfProof.slnx", new DotNetBuildSettings { Configuration = parameters.Configuration }));
    }
}
