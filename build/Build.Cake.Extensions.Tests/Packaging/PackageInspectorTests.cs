using System.IO.Compression;
using System.Text;
using Build.Cake.Extensions.Packaging;

namespace Build.Cake.Extensions.Tests.Packaging;

public sealed class PackageInspectorTests : IDisposable
{
    private const string GoodNuspec = """
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>PdfProof</id>
            <version>0.1.0-alpha.1</version>
            <license type="expression">MIT</license>
            <icon>icon.png</icon>
            <readme>README.md</readme>
            <repository type="git" url="https://github.com/mgnslndh/PdfProof" commit="0123456789abcdef" />
          </metadata>
        </package>
        """;

    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"pdfproof-pack-{Guid.NewGuid():N}")).FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_complete_package_has_no_problems()
    {
        var nupkg = WritePackage(GoodNuspec, "icon.png", "README.md");

        Assert.Empty(PackageInspector.FindProblems(nupkg));
    }

    [Fact]
    public void A_package_without_its_icon_or_readme_is_reported()
    {
        var nupkg = WritePackage(GoodNuspec);

        Assert.Equal(["icon.png is missing", "README.md is missing"], PackageInspector.FindProblems(nupkg));
    }

    [Fact]
    public void A_package_without_a_symbols_package_is_reported()
    {
        var nupkg = WritePackage(GoodNuspec, "icon.png", "README.md");
        File.Delete(Path.ChangeExtension(nupkg, ".snupkg"));

        Assert.Equal(["PdfProof.0.1.0-alpha.1.snupkg is missing"], PackageInspector.FindProblems(nupkg));
    }

    [Fact]
    public void A_nuspec_without_license_or_repository_commit_is_reported()
    {
        var nuspec = GoodNuspec
            .Replace("""<license type="expression">MIT</license>""", "")
            .Replace(""" commit="0123456789abcdef" """, " ");
        var nupkg = WritePackage(nuspec, "icon.png", "README.md");

        Assert.Equal(
            ["the license expression is not MIT", "the repository commit is missing (SourceLink)"],
            PackageInspector.FindProblems(nupkg));
    }

    private string WritePackage(string nuspec, params string[] files)
    {
        var nupkg = Path.Combine(_folder, "PdfProof.0.1.0-alpha.1.nupkg");
        using (var zip = ZipFile.Open(nupkg, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "PdfProof.nuspec", nuspec);
            foreach (var file in files)
            {
                WriteEntry(zip, file, "content");
            }
        }

        File.WriteAllText(Path.ChangeExtension(nupkg, ".snupkg"), "symbols");
        return nupkg;
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
