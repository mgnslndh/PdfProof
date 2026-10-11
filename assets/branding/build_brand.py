"""Generates the PdfProof brand SVGs.

Wordmark outlines come from Inter SemiBold (SIL OFL 1.1). Pass the font path as argv[1].
Output goes to ./brand next to this script.
"""
import os
import sys

from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

FONT = sys.argv[1]
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "brand")

# Palette
NAVY = "#14284B"      # ink navy: tile, wordmark on light
GREEN = "#15965A"     # proof green: check mark, "Proof"
PAPER = "#F7F9FC"     # page
FOLD = "#C5D0E3"      # folded corner
LINES = "#9DAAC2"     # text lines on the page
MARKS = "#7F95BD"     # crop marks on the tile
LIGHT_TEXT = "#F0F6FC"

WORD = "PdfProof"
SPLIT = 3             # "Pdf" | "Proof"


def icon_body(p):
    """The mark in a 256x256 coordinate system. p = id prefix."""
    return f"""<rect width="256" height="256" rx="56" fill="{NAVY}"/>
  <g fill="none" stroke="{MARKS}" stroke-width="8">
    <path d="M40 48H60M70 18V38M196 48H216M186 18V38M40 208H60M70 218V238M196 208H216M186 218V238"/>
  </g>
  <path d="M70 48H152L186 82V208H70Z" fill="{PAPER}"/>
  <path d="M152 48V82H186Z" fill="{FOLD}"/>
  <rect x="90" y="72" width="44" height="12" rx="6" fill="{LINES}"/>
  <rect x="90" y="96" width="74" height="12" rx="6" fill="{LINES}"/>
  <path id="{p}check" d="M96 152L121 177L170 128" fill="none" stroke="{GREEN}" stroke-width="24" stroke-linecap="round" stroke-linejoin="round"/>"""


def pair_kerning(font):
    """Flattened GPOS pair-adjustment XAdvance values: {(left, right): value}."""
    kern = {}
    gpos = font["GPOS"].table
    for lookup in gpos.LookupList.Lookup:
        for st in lookup.SubTable:
            if lookup.LookupType == 9:
                st = st.ExtSubTable
            if st.LookupType != 2:
                continue
            cov = st.Coverage.glyphs
            if st.Format == 1:
                for i, left in enumerate(cov):
                    for pvr in st.PairSet[i].PairValueRecord:
                        v = getattr(pvr.Value1, "XAdvance", 0) if pvr.Value1 else 0
                        kern.setdefault((left, pvr.SecondGlyph), v)
            else:
                c1 = st.ClassDef1.classDefs
                c2 = st.ClassDef2.classDefs
                for left in cov:
                    rec = st.Class1Record[c1.get(left, 0)]
                    for right in font.getGlyphOrder():
                        k = c2.get(right, 0)
                        if k == 0 and right not in c2:
                            continue
                        v1 = rec.Class2Record[k].Value1
                        v = getattr(v1, "XAdvance", 0) if v1 else 0
                        if v:
                            kern.setdefault((left, right), v)
    return kern


def wordmark_paths(font, size, x0, baseline):
    """Returns ([path_d per letter], total_width) for WORD."""
    upm = font["head"].unitsPerEm
    s = size / upm
    cmap = font.getBestCmap()
    gs = font.getGlyphSet()
    names = [cmap[ord(c)] for c in WORD]
    wanted = set(zip(names, names[1:]))
    kern = {k: v for k, v in pair_kerning(font).items() if k in wanted}
    x = 0
    ds = []
    for i, n in enumerate(names):
        pen = SVGPathPen(gs, ntos=lambda v: f"{v:.1f}".rstrip("0").rstrip("."))
        tp = TransformPen(pen, (s, 0, 0, -s, x0 + x * s, baseline))
        gs[n].draw(tp)
        ds.append(pen.getCommands())
        x += gs[n].width
        if i + 1 < len(names):
            x += kern.get((n, names[i + 1]), 0)
    return ds, x * s


def svg(w, h, body, title="PdfProof"):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}">\n'
            f"  <title>{title}</title>\n  {body}\n</svg>\n")


def main():
    os.makedirs(OUT, exist_ok=True)
    font = TTFont(FONT)
    upm = font["head"].unitsPerEm
    cap = font["OS/2"].sCapHeight

    with open(os.path.join(OUT, "pdfproof-icon.svg"), "w", newline="\n") as f:
        f.write(svg(256, 256, icon_body("ppi-")))

    # Logo: 200-high icon, gap 50, wordmark cap height 84 centred on y=100.
    H = 200
    gap = 50
    cap_h = 84
    size = round(cap_h * upm / cap, 2)
    baseline = H / 2 + cap_h / 2
    x0 = H + gap
    ds, ww = wordmark_paths(font, size, x0, baseline)
    W = round(x0 + ww + 4)
    scale = H / 256

    for variant, text_col in (("dark", NAVY), ("light", LIGHT_TEXT)):
        p = f"ppl-{variant}-"
        icon = f'<g id="{p}icon" transform="scale({scale:g})">\n  {icon_body(p)}\n  </g>'
        pdf = "".join(ds[:SPLIT])
        proof = "".join(ds[SPLIT:])
        outlined = (f'{icon}\n  <path id="{p}pdf" fill="{text_col}" d="{pdf}"/>\n'
                    f'  <path id="{p}proof" fill="{GREEN}" d="{proof}"/>')
        with open(os.path.join(OUT, f"pdfproof-logo-{variant}-text.svg"), "w", newline="\n") as f:
            f.write(svg(W, H, outlined))

        live = (f'{icon}\n  <text x="{x0}" y="{baseline:g}" font-family="Inter, \'Inter Display\', sans-serif" '
                f'font-weight="600" font-size="{size:g}"><tspan fill="{text_col}">Pdf</tspan>'
                f'<tspan fill="{GREEN}">Proof</tspan></text>')
        with open(os.path.join(OUT, f"pdfproof-logo-{variant}-text.editable.svg"), "w", newline="\n") as f:
            f.write(svg(W, H, live))

    # Social preview 1280x640: icon + wordmark + tagline on solid navy.
    SW, SH = 1280, 640
    s_icon = 220
    s_gap = 56
    s_cap = 96
    s_size = round(s_cap * upm / cap, 2)
    tag = "Unit tests for the PDFs you generate"
    _, s_ww = wordmark_paths(font, s_size, 0, 0)
    group_w = s_icon + s_gap + s_ww
    gx = round((SW - group_w) / 2)
    gy = 170
    s_ds, _ = wordmark_paths(font, s_size, gx + s_icon + s_gap, gy + s_icon / 2 + s_cap / 2)
    t_size = 44
    reg = TTFont(FONT.replace("SemiBold", "Regular"))
    tds, tw = _text_paths(reg, tag, t_size)
    tx = (SW - tw) / 2
    ty = gy + s_icon + 120
    p = "pps-"
    body = (f'<rect width="{SW}" height="{SH}" fill="#0D1117"/>\n'
            f'  <g id="{p}icon" transform="translate({gx} {gy}) scale({s_icon / 256:g})">\n  {icon_body(p)}\n  </g>\n'
            f'  <path fill="{LIGHT_TEXT}" d="{"".join(s_ds[:SPLIT])}"/>\n'
            f'  <path fill="#3CCB86" d="{"".join(s_ds[SPLIT:])}"/>\n'
            f'  <path fill="#AEBBD3" transform="translate({tx:.1f} {ty})" d="{tds}"/>')
    with open(os.path.join(OUT, "pdfproof-social.svg"), "w", newline="\n") as f:
        f.write(svg(SW, SH, body))
    print("logo", W, "x", H, f"ratio {W / H:.2f}")


def _text_paths(font, text, size):
    upm = font["head"].unitsPerEm
    s = size / upm
    cmap = font.getBestCmap()
    gs = font.getGlyphSet()
    x = 0
    out = []
    for c in text:
        n = cmap[ord(c)]
        pen = SVGPathPen(gs, ntos=lambda v: f"{v:.1f}".rstrip("0").rstrip("."))
        gs[n].draw(TransformPen(pen, (s, 0, 0, -s, x * s, 0)))
        out.append(pen.getCommands())
        x += gs[n].width
    return "".join(out), x * s


if __name__ == "__main__":
    main()
