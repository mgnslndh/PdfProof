public static partial class Program
{
    static void RegisterBuildTasks()
    {
        // The build's own tests first: they are fast, and a broken build helper would make every later result suspect.
        Task("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("build/Build.Cake.Extensions.Tests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                }));

        Task("Build")
            .IsDependentOn("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetBuild("PdfProof.slnx", new DotNetBuildSettings { Configuration = parameters.Configuration }));
    }
}
