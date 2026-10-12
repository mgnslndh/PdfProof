using System.Reflection;

namespace PdfProof.FixtureGenerator;

internal static class Versions
{
    // The version of the package that defines `type`, for the manifest. The file version is the most reliable source
    // (Playwright's informational version is a 1.0.0 placeholder); a fourth ".0" part is dropped (26.4.0.0 → 26.4.0).
    // Without one, the informational version without its "+commit" suffix.
    public static string Of(Type type)
    {
        var file = type.Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        if (file is not null)
        {
            return file.Split('.') is [var major, var minor, var patch, "0"] ? $"{major}.{minor}.{patch}" : file;
        }

        var version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? type.Assembly.GetName().Version?.ToString()
            ?? "unknown";
        var plus = version.IndexOf('+');
        return plus < 0 ? version : version[..plus];
    }
}
