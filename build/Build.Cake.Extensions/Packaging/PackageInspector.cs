using System.IO.Compression;
using System.Xml.Linq;

namespace Build.Cake.Extensions.Packaging;

// Checks a .nupkg for what makes a package look finished on nuget.org: icon, README, MIT license, SourceLink
// commit, and a .snupkg next to it. Returns the problems found, in a fixed order; empty means the package is fine.
public static class PackageInspector
{
    public static IReadOnlyList<string> FindProblems(string nupkgPath)
    {
        var problems = new List<string>();
        using var zip = ZipFile.OpenRead(nupkgPath);
        var entries = zip.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!entries.Contains("icon.png"))
        {
            problems.Add("icon.png is missing");
        }

        if (!entries.Contains("README.md"))
        {
            problems.Add("README.md is missing");
        }

        var nuspecEntry = zip.Entries.SingleOrDefault(entry =>
            !entry.FullName.Contains('/') && entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry is null)
        {
            problems.Add("the .nuspec is missing");
        }
        else
        {
            using var stream = nuspecEntry.Open();
            var metadata = XDocument.Load(stream).Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
            var license = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "license");
            if (license?.Value != "MIT")
            {
                problems.Add("the license expression is not MIT");
            }

            var repository = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "repository");
            if (string.IsNullOrEmpty(repository?.Attribute("commit")?.Value))
            {
                problems.Add("the repository commit is missing (SourceLink)");
            }
        }

        var symbols = Path.ChangeExtension(nupkgPath, ".snupkg");
        if (!File.Exists(symbols))
        {
            problems.Add($"{Path.GetFileName(symbols)} is missing");
        }

        return problems;
    }
}
