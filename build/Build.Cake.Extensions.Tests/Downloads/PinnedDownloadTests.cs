using System.Net;
using System.Text;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public sealed class PinnedDownloadTests : IDisposable
{
    private static readonly byte[] Body = Encoding.ASCII.GetBytes("pinned content");

    private readonly string _cache = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"pdfproof-download-{Guid.NewGuid():N}")).FullName;

    public void Dispose() => Directory.Delete(_cache, recursive: true);

    [Fact]
    public async Task A_download_is_verified_stored_by_hash_and_reused()
    {
        var sha = Sha(Body);
        var handler = new StubHandler(Body);
        var download = new PinnedDownload(new HttpClient(handler));

        var first = await download.GetAsync(new Uri("https://example.com/a.pdf"), sha, _cache, "a.pdf");
        var second = await download.GetAsync(new Uri("https://example.com/a.pdf"), sha, _cache, "a.pdf");

        Assert.Equal(Path.Combine(_cache, sha, "a.pdf"), first);
        Assert.Equal(first, second);
        Assert.Equal(Body, File.ReadAllBytes(first));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task A_hash_mismatch_fails_with_both_hashes_and_keeps_nothing()
    {
        var expected = Sha(Encoding.ASCII.GetBytes("something else"));
        var download = new PinnedDownload(new HttpClient(new StubHandler(Body)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            download.GetAsync(new Uri("https://example.com/a.pdf"), expected, _cache, "a.pdf"));

        Assert.Equal($"SHA-256 mismatch for https://example.com/a.pdf: expected {expected}, got {Sha(Body)}.", ex.Message);
        Assert.Empty(Directory.GetFiles(_cache, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_damaged_cached_file_is_downloaded_again()
    {
        var sha = Sha(Body);
        Directory.CreateDirectory(Path.Combine(_cache, sha));
        File.WriteAllText(Path.Combine(_cache, sha, "a.pdf"), "damaged");
        var handler = new StubHandler(Body);

        var path = await new PinnedDownload(new HttpClient(handler)).GetAsync(new Uri("https://example.com/a.pdf"), sha, _cache, "a.pdf");

        Assert.Equal(Body, File.ReadAllBytes(path));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task An_http_error_fails_the_download()
    {
        var download = new PinnedDownload(new HttpClient(new StubHandler(Body, HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            download.GetAsync(new Uri("https://example.com/a.pdf"), Sha(Body), _cache, "a.pdf"));
    }

    private static string Sha(byte[] data) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));

    private sealed class StubHandler(byte[] body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }
}
