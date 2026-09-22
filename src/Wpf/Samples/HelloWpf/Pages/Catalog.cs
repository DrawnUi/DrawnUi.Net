namespace HelloWpf.Pages;

/// <summary>One entry per sample page, mirroring the React demo's catalog.ts.</summary>
public record SampleEntry(string Route, string Title, string Text);

/// <summary>
/// The demo's sample pages. Single source of truth for the root menu cards, exactly as in the
/// React demo — pages are added here as each module is ported.
/// </summary>
public static class Catalog
{
    /// <summary>Every sample, in the order the React demo lists them.</summary>
    public static readonly IReadOnlyList<SampleEntry> Samples = new List<SampleEntry>
    {
        new("cells", "Recycled cells", "100 000 items in a SkiaScroll, RecyclingTemplate + MeasureFirst, UseCache=Image"),
        new("uneven", "Uneven cells", "Rows of different heights — MeasureVisible, LoadMore at both ends, ImageDoubleBuffered cells"),
        new("images", "Images", "SkiaImage — every TransformAspect, alignment, clipping"),
        new("svg", "SVG", "SkiaSvg — file and inline sources, TintColor, LockRatio"),
        new("shapes", "Shapes", "SkiaShape — rectangle, circle, ellipse, arc, polygon, line, path; stroke, corner radii, clipping"),
        new("text", "Text", "SkiaLabel — word wrap, MaxLines, alignment, spans, weights, glyph fallback"),
        new("layouts", "Layouts", "Every SkiaLayout type — Absolute, Column, Row, Wrap, Grid (tracks, spans, spacing)"),
        new("looks", "Common Controls", "SkiaSwitch, SkiaCheckbox, SkiaRadioButton, SkiaProgress, SkiaSlider, SkiaButton — Default, Windows, Cupertino, Material, Material3"),
        new("snapping", "Carousel & Drawer", "SkiaCarousel (swipe, SidesOffset peek, SelectedIndex) and SkiaDrawer (drag from an edge, snap by velocity)"),
        new("animations", "Lottie & GIF", "SkiaLottie (Skottie: AutoPlay, Repeat, SpeedRatio, IsOn, ColorTint) and SkiaGif frames on the canvas frame loop"),
        new("shell", "Shell", "SkiaShell — page transitions, OpenPopupAsync, PushModalAsync (drawer), ShowToast"),
        new("editor", "Editor", "SkiaEditor — drawn text input: caret, selection, placeholder, password, multiline, ControlStyle looks"),
        new("keyboard", "Keyboard Input", "KeyboardManager — window-level KeyDown / KeyUp / KeyChar with modifier state"),
        new("scroll", "SkiaScroll", "Header in flow / sticky / behind with parallax, Footer, scroll bars, pull to refresh, SnapToChildren, TrackIndexPosition"),
        new("shaders", "Shaders", "SkiaShaderEffect — SkSL on any control (iImage1, iTime, iMouse, custom uniforms, touch ripples) and SkiaShaderCarousel gl-transitions"),
        new("sprites", "Sprites", "SkiaSprite spritesheets and a SkiaSpriteSet warrior on a tile board, moved with the keyboard"),
        new("transforms", "Transforms", "Rotation, Scale, Skew, Translation, Opacity — hit-testing through them, *ToAsync animations"),
        new("reorder", "Drag to reorder", "Drag one row by its grip and it lifts and floats over the list, which reorders live under it keeping its measured heights and its scroll offset"),
        new("a11y", "Accessibility", "ARIA overlay over the canvas — roles, labels, hints, toggles, live regions, keyboard"),
    };
}
