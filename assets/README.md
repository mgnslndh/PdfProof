# Brand assets

All files live in `branding/brand/`. The PNGs are committed, so neither the build nor CI renders anything.

| File | Use on | Size |
| --- | --- | --- |
| `pdfproof-icon.svg` | Anywhere: the mark on its own navy tile, so it works on light and dark backgrounds | 256 × 256 |
| `pdfproof-logo-dark-text.svg` | Light backgrounds: the icon plus the wordmark in ink navy | horizontal lockup |
| `pdfproof-logo-light-text.svg` | Dark backgrounds: the same with light text | horizontal lockup |
| `pdfproof-logo-*.editable.svg` | Editing only: the wordmark as live text in Inter (not outlined) | |
| `pdfproof-social.svg` | Source of the GitHub social preview | 1280 × 640 |
| `png/pdfproof-icon-128.png` | The NuGet package icon (packed as `icon.png` by `Directory.Build.targets`) | 128 × 128 |
| `png/pdfproof-icon-256/512/1024.png` | Larger uses: docs site, avatar | |
| `png/pdfproof-logo-dark-text-800.png`, `png/pdfproof-logo-light-text-800.png` | Places that can't show SVG | 800 wide |
| `png/pdfproof-social-1280x640.png` | GitHub repository social preview (Settings → Social preview) | 1280 × 640 |

The outlined SVGs have no font dependency. The two logos differ only in the wordmark's color and their internal SVG
IDs, so both can sit in one page.

## Palette

| Name | Hex | Used for |
| --- | --- | --- |
| Ink navy | `#14284B` | Tile, wordmark on light backgrounds |
| Proof green | `#15965A` | Check mark, "Proof" in the wordmark |
| Proof green, light | `#3CCB86` | "Proof" on the social card only |
| Paper | `#F7F9FC` | The page in the mark |
| Light text | `#F0F6FC` | Wordmark on dark backgrounds |
| Crop marks | `#7F95BD` | Crop marks on the tile |
| Text lines | `#9DAAC2` | Text lines on the page |
| Fold | `#C5D0E3` | Folded corner |
| Tagline | `#AEBBD3` | Social card tagline |

The wordmark font is Inter SemiBold (SIL OFL 1.1).

## On GitHub

Pick the logo that matches the viewer's GitHub theme with `<picture>`:

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/branding/brand/pdfproof-logo-light-text.svg">
  <source media="(prefers-color-scheme: light)" srcset="assets/branding/brand/pdfproof-logo-dark-text.svg">
  <img alt="PdfProof" src="assets/branding/brand/pdfproof-logo-dark-text.svg" width="400">
</picture>
```

Don't make one SVG switch colors with an internal `prefers-color-scheme` rule. GitHub serves README images through
its image proxy, where that query follows the operating system rather than the GitHub theme.

## How the files were made

Kept for reference; the assets are not expected to be regenerated.

- `branding/build_brand.py` generates the SVGs. It outlines the wordmark from an Inter SemiBold font file passed as
  the first argument (needs Python with `fontTools`).
- `branding/render_png.sh` renders the PNGs with headless Chrome. The Chrome path is hardcoded for Windows.
- `branding/preview.html` / `preview.png` show the icon at small sizes, both logos on light and dark, and the palette.
