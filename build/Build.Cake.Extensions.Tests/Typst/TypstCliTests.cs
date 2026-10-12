using System.Runtime.InteropServices;
using Build.Cake.Extensions.Typst;

namespace Build.Cake.Extensions.Tests.Typst;

public sealed class TypstCliTests
{
    [Fact]
    public void Windows_and_Linux_x64_have_pinned_release_assets()
    {
        Assert.Equal(
            new TypstAsset("typst-x86_64-pc-windows-msvc.zip", "19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736", "typst-x86_64-pc-windows-msvc/typst.exe"),
            TypstCli.AssetFor(OSPlatform.Windows, Architecture.X64));
        Assert.Equal(
            new TypstAsset("typst-x86_64-unknown-linux-musl.tar.xz", "a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c", "typst-x86_64-unknown-linux-musl/typst"),
            TypstCli.AssetFor(OSPlatform.Linux, Architecture.X64));
    }

    [Fact]
    public void The_asset_url_is_the_pinned_github_release()
    {
        Assert.Equal(
            new Uri("https://github.com/typst/typst/releases/download/v0.15.1/typst-x86_64-pc-windows-msvc.zip"),
            TypstCli.Url(TypstCli.AssetFor(OSPlatform.Windows, Architecture.X64)));
    }

    [Fact]
    public void Other_platforms_are_told_how_to_use_their_own_Typst()
    {
        var ex = Assert.Throws<PlatformNotSupportedException>(() => TypstCli.AssetFor(OSPlatform.OSX, Architecture.Arm64));

        Assert.Equal("No pinned Typst CLI for OSX Arm64. Install Typst 0.15.1 and put it on PATH; the generator then uses it.", ex.Message);
    }

    [Fact]
    public void An_interrupted_extraction_leaves_nothing_that_a_later_run_would_trust()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pdfproof-typst-{Guid.NewGuid():N}", "extracted");
        try
        {
            Assert.Throws<IOException>(() => TypstCli.ExtractOnce("archive.zip", folder, (_, target) =>
            {
                File.WriteAllText(Path.Combine(target, "typst.exe"), "half");
                throw new IOException("interrupted");
            }));
            Assert.False(Directory.Exists(folder));

            TypstCli.ExtractOnce("archive.zip", folder, (_, target) => File.WriteAllText(Path.Combine(target, "typst.exe"), "whole"));
            TypstCli.ExtractOnce("archive.zip", folder, (_, _) => throw new InvalidOperationException("extracted twice"));

            Assert.Equal("whole", File.ReadAllText(Path.Combine(folder, "typst.exe")));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
        }
    }

    [Fact]
    public void A_folder_left_by_an_older_interrupted_extraction_is_replaced()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pdfproof-typst-{Guid.NewGuid():N}", "extracted");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "typst.exe"), "half");
        try
        {
            TypstCli.ExtractOnce("archive.zip", folder, (_, target) => File.WriteAllText(Path.Combine(target, "typst.exe"), "whole"));

            Assert.Equal("whole", File.ReadAllText(Path.Combine(folder, "typst.exe")));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
        }
    }
}
