public static partial class Program
{
    // Every test project in the solution; on Windows that includes the net48 runs.
    static void RegisterTestTasks()
    {
        Task("Test")
            .IsDependentOn("Build")
            .Does<BuildParameters>(parameters =>
                DotNetTest("PdfProof.slnx", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Solution,
                    Configuration = parameters.Configuration,
                    NoBuild = true,
                }));
    }
}
