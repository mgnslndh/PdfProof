public static partial class Program
{
    // What CI runs on every platform, and what a local ./build.ps1 or ./build.sh runs: fixtures, external corpus,
    // build, tests, packages. --skip-generate and --skip-fetch leave out the first two (CI's build jobs get the
    // fixtures from the fixtures job).
    static void RegisterDefaultTask()
    {
        Task("Default")
            .IsDependentOn("GenerateFixtures")
            .IsDependentOn("FetchCorpus")
            .IsDependentOn("Test")
            .IsDependentOn("Pack");
    }
}
