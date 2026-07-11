"""
Builds symbol font subsets for browser (WASM) heads.

WASM has no OS fonts: every glyph must come from a shipped font. Text fonts
(OpenSans) lack arrows/geometric/math blocks, the emoji subset covers only
color emoji. These subsets fill the gap at minimal transfer cost.

Sources are downloaded from fonts.gstatic.com on first run (CORS-open, stable
versioned URLs). Output goes to OUT_DIRS (each browser head's wwwroot/fonts).

Requires: pip install fonttools requests

Note: NotoColorEmoji-Subset.ttf (faces+hands tier, ~900 KB) was produced
separately from the full NotoColorEmoji — see the wasm-font-slimming skill
for the recipe if it ever needs regeneration.
"""

import io
import os
import urllib.request

from fontTools.subset import Options, Subsetter
from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC_DIR = os.path.join(HERE, "src")
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))

OUT_DIRS = [
    # DrawnUi.Blazor.Core static web assets -> _content/DrawnUi.Blazor.Core/fonts/
    # exposed to apps via IFontCollection.AddSymbols() / AddEmojis()
    os.path.join(REPO, "src", "Blazor", "DrawnUi", "wwwroot", "fonts"),
]

SOURCES = {
    # Arrows, math operators, misc technical, geometric shapes, supplemental arrows
    "NotoSansMath-Full.ttf":
        "https://fonts.gstatic.com/s/notosansmath/v19/7Aump_cpkSecTWaHRlH2hyV5UHkG.ttf",
    # Misc symbols, dingbats (text presentation), remaining technical
    "NotoSansSymbols2-Full.ttf":
        "https://fonts.gstatic.com/s/notosanssymbols2/v25/I_uyMoGduATTei9eI8daxVHDyfisHr71ypM.ttf",
}


def rng(*pairs):
    s = set()
    for a, b in pairs:
        s.update(range(a, b + 1))
    return s


# Coverage split (verified against actual cmaps):
# Math has full Arrows/MathOps/GeomShapes, most MiscTech + 2900-2BFF.
# Symbols2 has MiscSym/Dingbats/rest of MiscTech.
SUBSETS = {
    "NotoSansMathSymbols-Subset.ttf": ("NotoSansMath-Full.ttf", rng(
        (0x2100, 0x214F),  # Letterlike (™ ℓ ℝ ...)
        (0x2190, 0x21FF),  # Arrows
        (0x2200, 0x22FF),  # Math Operators
        (0x2300, 0x23FF),  # Misc Technical (⌘ ⏻ ...)
        (0x2500, 0x259F),  # Box Drawing + Block Elements (partial in source)
        (0x25A0, 0x25FF),  # Geometric Shapes
        # 2900-2BFF deliberately excluded: exotic math doubles size (+177 KB);
        # common 2B0x heavy arrows come from the Symbols2 subset instead.
    )),
    "NotoSansSymbols2-Subset.ttf": ("NotoSansSymbols2-Full.ttf", rng(
        (0x2300, 0x23FF),  # Misc Technical (fills Math's gaps)
        (0x2600, 0x26FF),  # Misc Symbols (☀ ☑ ♠ ...)
        (0x2700, 0x27BF),  # Dingbats (✂ ✗ ➔ ...)
        (0x2B00, 0x2BFF),  # Misc Symbols and Arrows (fills Math's gaps)
    )),
}


def download_sources():
    os.makedirs(SRC_DIR, exist_ok=True)
    for name, url in SOURCES.items():
        path = os.path.join(SRC_DIR, name)
        if not os.path.exists(path):
            print(f"downloading {name} ...")
            urllib.request.urlretrieve(url, path)


def build(out_name, src_name, codepoints):
    font = TTFont(os.path.join(SRC_DIR, src_name))
    options = Options()
    options.ignore_missing_unicodes = True
    options.ignore_missing_glyphs = True
    options.name_IDs = ["*"]  # keep family names -> stable alias registration
    subsetter = Subsetter(options=options)
    subsetter.populate(unicodes=sorted(codepoints))
    subsetter.subset(font)

    buf = io.BytesIO()
    font.save(buf)
    data = buf.getvalue()
    print(f"{out_name:36} {len(data) / 1024:7.0f} KB  glyphs={font['maxp'].numGlyphs}")

    for out_dir in OUT_DIRS:
        os.makedirs(out_dir, exist_ok=True)
        with open(os.path.join(out_dir, out_name), "wb") as f:
            f.write(data)


if __name__ == "__main__":
    download_sources()
    for out_name, (src_name, cps) in SUBSETS.items():
        build(out_name, src_name, cps)
    print("done ->", ", ".join(OUT_DIRS))
