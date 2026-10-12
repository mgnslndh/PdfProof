using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Typst;

public sealed record TypstAsset(string FileName, string Sha256, string Executable);

// The Typst CLI the fixture generator uses, pinned by version and per-platform SHA-256 (spec §10.2; OQ-2: Typstium is
// not on a package feed). Release archives hold one folder named like the archive, with the executable inside.
public static class TypstCli
{
    public const string Version = "0.15.1";

    public static TypstAsset AssetFor(OSPlatform os, Architecture architecture)
    {
        if (os == OSPlatform.Windows && architecture == Architecture.X64)
        {
            return new TypstAsset(
                "typst-x86_64-pc-windows-msvc.zip",
                "19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736",
                "typst-x86_64-pc-windows-msvc/typst.exe");
        }

        if (os == OSPlatform.Linux && architecture == Architecture.X64)
        {
            return new TypstAsset(
                "typst-x86_64-unknown-linux-musl.tar.xz",
                "a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c",
                "typst-x86_64-unknown-linux-musl/typst");
        }

        throw new PlatformNotSupportedException(
            $"No pinned Typst CLI for {os} {architecture}. Install Typst {Version} and put it on PATH; the generator then uses it.");
    }

    public static Uri Url(TypstAsset asset) => new($"https://github.com/typst/typst/releases/download/v{Version}/{asset.FileName}");

    // Downloads (or reuses) the pinned archive for this machine and extracts it once; returns the executable's path.
    public static async Task<string> EnsureAsync(PinnedDownload download, string cacheDirectory)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux
            : OSPlatform.OSX;
        var asset = AssetFor(os, RuntimeInformation.OSArchitecture);
        var archive = await download.GetAsync(Url(asset), asset.Sha256, cacheDirectory, asset.FileName);
        var folder = Path.Combine(Path.GetDirectoryName(archive)!, "extracted");
        ExtractOnce(archive, folder, (source, target) =>
        {
            if (source.EndsWith(".zip", StringComparison.Ordinal))
            {
                ZipFile.ExtractToDirectory(source, target);
            }
            else
            {
                // .tar.xz: the BCL reads tar but not xz, so the system tar extracts it.
                Extract("tar", ["-xJf", source, "-C", target]);
            }
        });
        return Path.Combine(folder, asset.Executable);
    }

    // Extracts `archive` into `folder` unless a completed extraction is already there. The archive is extracted into a
    // fresh folder, marked complete, and only then moved into place, so an interrupted extraction (a half-written
    // executable) is never trusted by a later run; a folder without the marker is replaced.
    public static void ExtractOnce(string archive, string folder, Action<string, string> extract)
    {
        const string Marker = ".extracted";
        if (File.Exists(Path.Combine(folder, Marker)))
        {
            return;
        }

        var partial = $"{folder}.partial-{Guid.NewGuid():N}";
        Directory.CreateDirectory(partial);
        try
        {
            extract(archive, partial);
            File.WriteAllText(Path.Combine(partial, Marker), "");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            Directory.Move(partial, folder);
        }
        finally
        {
            if (Directory.Exists(partial))
            {
                Directory.Delete(partial, recursive: true);
            }
        }
    }

    private static void Extract(string tool, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(tool) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{tool} did not start.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{tool} failed (exit code {process.ExitCode}): {error.Trim()}");
        }
    }
}
