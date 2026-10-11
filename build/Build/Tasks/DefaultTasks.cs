public static partial class Program
{
    // What CI runs on every platform, and what a local ./build.ps1 or ./build.sh runs.
    static void RegisterDefaultTask()
    {
        Task("Default")
            .IsDependentOn("Test");
    }
}
