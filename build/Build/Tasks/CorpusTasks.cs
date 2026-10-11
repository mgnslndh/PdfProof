public static partial class Program
{
    // Downloads the external corpus (corpus/external/manifest.json) into artifacts/external/<id>.pdf, verifying each
    // file's SHA-256. Downloads are cached in artifacts/cache by hash, so a run without changes downloads nothing.
    static void RegisterCorpusTasks()
    {
        Task("FetchCorpus")
            .WithCriteria<BuildParameters>((_, parameters) => !parameters.SkipFetch, "--skip-fetch was given")
            .Does<BuildParameters>(async parameters =>
            {
                using var http = PinnedDownload.CreateHttpClient();
                var download = new PinnedDownload(http);
                EnsureDirectoryExists(parameters.External);
                foreach (var entry in ExternalCorpusManifest.Load("corpus/external/manifest.json"))
                {
                    var cached = await download.GetAsync(entry.Url, entry.Sha256, parameters.Cache.FullPath, entry.Id + ".pdf");
                    CopyFile(cached, parameters.External.CombineWithFilePath(entry.Id + ".pdf"));
                    Information("{0}: {1} ({2})", entry.Id, entry.Source, entry.License);
                }
            });
    }
}
