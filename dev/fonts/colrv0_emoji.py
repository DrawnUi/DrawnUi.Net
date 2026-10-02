"""
Builds NotoColorEmoji-Subset-COLRv0.ttf for the Windows .NET heads.

SkiaSharp on Windows draws text through DirectWrite, whose Skia backend renders
COLRv0 and bitmap emoji but not COLRv1: the lib's NotoColorEmoji-Subset.ttf
(COLRv1, used by the web heads) draws zero pixels there. This flattens that
same subset to COLRv0, so the glyph set and artwork stay the ones the web heads
show:
- every PaintTransform/Translate/Scale above a PaintGlyph is baked into a new
  outline glyph;
- every gradient becomes the average of its color stops (COLRv0 has only solid
  layers), so faces lose their soft shading;
- the SVG table is dropped (921 KB in -> ~190 KB out).

Measured 2026-10-02 (SkiaSharp 4.148, DrawnUi.Net headless, Windows 11):
COLRv1 subset 0 colored px, this COLRv0 build draws, as do a CBDT subset of
the Google release (489 KB) and Segoe UI Emoji.

Requires: pip install fonttools
"""

import os

from fontTools.colorLib.builder import buildCOLR, buildCPAL
from fontTools.misc.transform import Identity, Transform
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SOURCE = os.path.join(REPO, "src", "Blazor", "DrawnUi", "wwwroot", "fonts", "NotoColorEmoji-Subset.ttf")
OUT_NAME = "NotoColorEmoji-Subset-COLRv0.ttf"
OUT_DIRS = [
    os.path.join(REPO, "src", "Wpf", "Samples", "HelloWpf", "fonts"),
    os.path.join(REPO, "src", "Maui", "Samples", "HelloMaui", "Resources", "Fonts"),
]


def transform_of(paint):
    f = paint.Format
    if f in (12, 13):
        t = paint.Transform
        return Transform(t.xx, t.yx, t.xy, t.yy, t.dx, t.dy)
    if f in (14, 15):
        return Transform(1, 0, 0, 1, paint.dx, paint.dy)
    if f in (16, 17):
        return Transform(paint.scaleX, 0, 0, paint.scaleY, 0, 0)
    if f in (18, 19):
        return Identity.translate(paint.centerX, paint.centerY).scale(paint.scaleX, paint.scaleY).translate(-paint.centerX, -paint.centerY)
    if f in (20, 21):
        return Transform(paint.scale, 0, 0, paint.scale, 0, 0)
    if f in (22, 23):
        return Identity.translate(paint.centerX, paint.centerY).scale(paint.scale).translate(-paint.centerX, -paint.centerY)
    raise ValueError(f"unsupported paint format {f}")


def build():
    font = TTFont(SOURCE)
    colr = font["COLR"].table
    palette = font["CPAL"].palettes[0]
    glyph_set = font.getGlyphSet()
    glyf, hmtx = font["glyf"], font["hmtx"]
    layers = colr.LayerList.Paint

    def rgba(index, alpha):
        c = palette[index]
        return (c.red / 255, c.green / 255, c.blue / 255, c.alpha / 255 * alpha)

    def solid(paint):
        while 12 <= paint.Format <= 31:  # transforms of a fill move a gradient only, skip them
            paint = paint.Paint
        if paint.Format in (2, 3):
            return rgba(paint.PaletteIndex, paint.Alpha)
        if 4 <= paint.Format <= 9:
            stops = [rgba(s.PaletteIndex, s.Alpha) for s in paint.ColorLine.ColorStop]
            return tuple(sum(c[i] for c in stops) / len(stops) for i in range(4))
        raise ValueError(f"unsupported fill format {paint.Format}")

    def walk(paint, transform, out):
        if paint.Format == 1:
            for i in range(paint.NumLayers):
                walk(layers[paint.FirstLayerIndex + i], transform, out)
        elif paint.Format == 10:
            out.append((paint.Glyph, transform, solid(paint.Paint)))
        elif 12 <= paint.Format <= 23:
            walk(paint.Paint, transform.transform(transform_of(paint)), out)
        else:
            raise ValueError(f"unsupported paint format {paint.Format}")

    colors, color_layers, baked = [], {}, {}
    first_new = len(font.getGlyphOrder())

    def color_index(color):
        color = tuple(round(x, 4) for x in color)
        if color not in colors:
            colors.append(color)
        return colors.index(color)

    for record in colr.BaseGlyphList.BaseGlyphPaintRecord:
        out = []
        walk(record.Paint, Identity, out)
        result = []
        for glyph, transform, color in out:
            if transform != Identity:
                key = (glyph, tuple(transform))
                if key not in baked:
                    pen = TTGlyphPen(glyph_set)
                    glyph_set[glyph].draw(TransformPen(pen, transform))
                    outline = pen.glyph()
                    outline.recalcBounds(glyf)
                    # post format 3 names glyphs by index, so name the new ones the same way
                    baked[key] = (f"glyph{first_new + len(baked):05d}", outline, glyph)
                glyph = baked[key][0]
            result.append((glyph, color_index(color)))
        color_layers[record.BaseGlyph] = result

    order = list(font.getGlyphOrder()) + [name for name, _, _ in baked.values()]
    font.setGlyphOrder(order)
    glyf.glyphOrder = order
    for name, outline, source in baked.values():
        glyf.glyphs[name] = outline
        empty = not outline.numberOfContours
        # lsb must equal xMin, or the rasterizer shifts the outline by the difference
        hmtx[name] = (hmtx[source][0], 0 if empty else outline.xMin)
        if "vmtx" in font:
            advance, tsb = font["vmtx"][source]
            original = glyf[source]
            top = tsb + original.yMax if original.numberOfContours else 0
            font["vmtx"][name] = (advance, 0 if empty else top - outline.yMax)
    font["maxp"].numGlyphs = len(order)
    font["COLR"] = buildCOLR(color_layers, version=0)
    font["CPAL"] = buildCPAL([colors])
    del font["SVG "]

    for out_dir in OUT_DIRS:
        path = os.path.join(out_dir, OUT_NAME)
        font.save(path)
        print(f"{path}  {os.path.getsize(path) / 1024:.0f} KB  baked={len(baked)} colors={len(colors)}")


if __name__ == "__main__":
    build()
