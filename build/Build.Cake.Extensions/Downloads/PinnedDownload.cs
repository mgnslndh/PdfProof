using System.Security.Cryptography;

namespace Build.Cake.Extensions.Downloads;

// Downloads a file pinned by SHA-256 into <cache>/<sha256>/<fileName> and reuses it while its hash matches. A
// mismatch fails with both hashes and leaves nothing behind: the pin, not the server, decides what the build uses.
public sealed class PinnedDownload(HttpClient http)
{
    // Some servers refuse requests without a User-Agent.
    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfProof-build/1.0 (+https://github.com/mgnslndh/PdfProof)");
        return client;
    }

    public async Task<string> GetAsync(Uri url, string sha256, string cacheDirectory, string fileName)
    {
        var expected = sha256.ToLowerInvariant();
        var folder = Path.Combine(cacheDirectory, expected);
        var path = Path.Combine(folder, fileName);
        if (File.Exists(path) && Sha256Of(path) == expected)
        {
            return path;
        }

        Directory.CreateDirectory(folder);
        var temporary = path + ".download";
        try
        {
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(temporary);
                await response.Content.CopyToAsync(file);
            }

            var actual = Sha256Of(temporary);
            if (actual != expected)
            {
                throw new InvalidOperationException($"SHA-256 mismatch for {url}: expected {expected}, got {actual}.");
            }

            File.Move(temporary, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            if (!File.Exists(path) && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
    }

    public static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
