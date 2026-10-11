public static partial class Program
{
    // Packs every packable project into artifacts/packages and fails when a package is incomplete.
    static void RegisterPackTasks()
    {
        Task("Pack")
            .IsDependentOn("Build")
            .Does<BuildParameters>(parameters =>
            {
                CleanDirectory(parameters.Packages);
                DotNetPack("PdfProof.slnx", new DotNetPackSettings
                {
                    Configuration = parameters.Configuration,
                    NoBuild = true,
                    OutputDirectory = parameters.Packages,
                });

                var packages = GetFiles($"{parameters.Packages.FullPath}/*.nupkg");
                if (packages.Count == 0)
                {
                    throw new CakeException("Pack produced no packages.");
                }

                var problems = packages
                    .SelectMany(package => PackageInspector.FindProblems(package.FullPath)
                        .Select(problem => $"{package.GetFilename()}: {problem}"))
                    .ToList();
                if (problems.Count > 0)
                {
                    throw new CakeException("Incomplete packages:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
                }

                foreach (var package in packages)
                {
                    Information("Packed {0}", package.GetFilename());
                }
            });
    }
}
