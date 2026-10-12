public static partial class Program
{
    // Every test project in the solution; on Windows that includes the net48 runs. With --require-fixtures, a missing
    // corpus fixture fails its test instead of skipping it (PDFPROOF_REQUIRE_FIXTURES, always set in CI).
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
                    EnvironmentVariables = parameters.RequireFixtures
                        ? new Dictionary<string, string?> { ["PDFPROOF_REQUIRE_FIXTURES"] = "true" }
                        : new Dictionary<string, string?>(),
                }));
    }
}
