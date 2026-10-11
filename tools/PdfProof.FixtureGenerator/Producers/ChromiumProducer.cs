using Microsoft.Playwright;

namespace PdfProof.FixtureGenerator;

// Chromium print-to-PDF through Playwright, tagged (with an outline) or untagged. The page footer comes from
// Chromium's footer template, as in applications that print HTML this way.
internal sealed class ChromiumProducer(bool tagged) : IProducer
{
    public string Name => tagged ? "chromium-tagged" : "chromium-untagged";

    public bool BestEffort => false;

    public string Version { get; private set; } = "";

    public async Task<string?> CheckAsync()
    {
        try
        {
            // Installs the Chromium build this Playwright version pins; does nothing when it is already installed.
            var exit = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"]));
            if (exit != 0)
            {
                return $"installing Chromium for Playwright failed (exit code {exit})";
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            Version = $"Chromium {browser.Version} (Playwright {Versions.Of(typeof(IPlaywright))})";
            return null;
        }
        catch (Exception ex) when (ex is PlaywrightException or InvalidOperationException)
        {
            return "Chromium could not be started: " + ex.Message;
        }
    }

    public async Task<byte[]> RenderAsync(CorpusDocument document)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync(new Uri(document.HtmlPath).AbsoluteUri);
        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            Tagged = tagged,
            Outline = tagged,
            DisplayHeaderFooter = true,
            HeaderTemplate = "<span></span>",
            FooterTemplate = "<div style='font-size:8pt;width:100%;text-align:center'>Page <span class='pageNumber'></span> of <span class='totalPages'></span></div>",
            Margin = new Margin { Top = "2cm", Bottom = "2cm", Left = "2cm", Right = "2cm" },
        });
    }
}
