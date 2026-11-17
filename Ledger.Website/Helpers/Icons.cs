using Microsoft.AspNetCore.Html;

namespace Ledger.Website.Helpers;

/// <summary>
/// The icons, inline (no icon font, no requests): thin strokes that take the colour of the text around them.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
    {
        ["overview"] = "<path d=\"M4 12h4l3-7 3 14 3-7h3\"/>",
        ["entries"] = "<path d=\"M8 6h12M8 12h12M8 18h12\"/><path d=\"M4 6h.01M4 12h.01M4 18h.01\"/>",
        ["accounts"] = "<rect x=\"3\" y=\"6\" width=\"18\" height=\"13\" rx=\"3\"/><path d=\"M3 10.5h18M7 15h3\"/>",
        ["rental"] = "<path d=\"M4 11l8-6 8 6\"/><path d=\"M6 10v9h12v-9\"/><path d=\"M10 19v-5h4v5\"/>",
        ["flips"] = "<path d=\"M4 20V9l6-4v15\"/><path d=\"M10 20V11l10-3v12\"/><path d=\"M3 20h18M14 13h2M14 16.5h2\"/>",
        ["settings"] = "<path d=\"M4 7h10M18 7h2M4 17h2M10 17h10\"/><circle cx=\"16\" cy=\"7\" r=\"2\"/><circle cx=\"8\" cy=\"17\" r=\"2\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["left"] = "<path d=\"M15 5l-7 7 7 7\"/>",
        ["right"] = "<path d=\"M9 5l7 7-7 7\"/>",
        ["arrow"] = "<path d=\"M5 12h14M13 6l6 6-6 6\"/>",
        ["check"] = "<path d=\"M5 12.5l4.5 4.5L19 7.5\"/>",
        ["x"] = "<path d=\"M6 6l12 12M18 6L6 18\"/>",
        ["edit"] = "<path d=\"M4 20h4L19 9l-4-4L4 16z\"/><path d=\"M13.5 6.5l4 4\"/>",
        ["trash"] = "<path d=\"M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13\"/>",
        ["download"] = "<path d=\"M12 4v11M7.5 10.5 12 15l4.5-4.5\"/><path d=\"M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3\"/>",
        ["upload"] = "<path d=\"M12 15V4M7.5 8.5 12 4l4.5 4.5\"/><path d=\"M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3\"/>",
        ["logout"] = "<path d=\"M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 16l-4-4 4-4M6 12h10\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8.5\" r=\"4\"/><path d=\"M4.5 20a7.5 7.5 0 0 1 15 0\"/>",
        ["lock"] = "<rect x=\"4.5\" y=\"10.5\" width=\"15\" height=\"10\" rx=\"3\"/><path d=\"M8 10.5V8a4 4 0 0 1 8 0v2.5\"/>",
        ["eye"] = "<path d=\"M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["key"] = "<circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"M11 12l8-8M16 7l2 2M14 9l2 2\"/>",
        ["sparkle"] = "<path d=\"M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z\"/>",
        ["sun"] = "<circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4\"/>",
        ["moon"] = "<path d=\"M20 14.5A8.5 8.5 0 1 1 9.5 4a7 7 0 0 0 10.5 10.5z\"/>",
        ["globe"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3z\"/>",
        ["phone"] = "<rect x=\"7\" y=\"3\" width=\"10\" height=\"18\" rx=\"2.5\"/><path d=\"M11 18h2\"/>",
        ["server"] = "<rect x=\"3.5\" y=\"4\" width=\"17\" height=\"7\" rx=\"2.5\"/><rect x=\"3.5\" y=\"13\" width=\"17\" height=\"7\" rx=\"2.5\"/><path d=\"M7.5 7.5h.01M7.5 16.5h.01\"/>",
        ["file"] = "<path d=\"M6 3h9l4 4v14H6z\"/><path d=\"M14 3v5h5M9 13h7M9 17h5\"/>",
        ["target"] = "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 12h.01\"/>",
        ["bolt"] = "<path d=\"M13 3L5 13.5h6L10 21l9-11.5h-6z\"/>",
    };

    /// <summary>
    /// An icon by name.
    /// </summary>
    /// <param name="name">The icon.</param>
    /// <param name="cssClass">An extra class.</param>
    /// <returns>The inline SVG (hidden from screen readers: the label is on the control around it).</returns>
    public static IHtmlContent Get(string name, string? cssClass = null) =>
        new HtmlString($"<svg class=\"ic{(cssClass is null ? string.Empty : " " + cssClass)}\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">{(Paths.TryGetValue(name, out var path) ? path : Paths["check"])}</svg>");

    /// <summary>
    /// The mark: a ledger page with its margin line and two ruled lines.
    /// </summary>
    /// <param name="size">The side, in pixels.</param>
    /// <returns>The inline SVG.</returns>
    public static IHtmlContent Logo(int size = 26) => new HtmlString(
        $"<svg class=\"logo-mark\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 26 26\" aria-hidden=\"true\"><rect x=\"3\" y=\"3\" width=\"20\" height=\"20\" rx=\"5\"/><path d=\"M9.5 3v20M13.5 10h5.5M13.5 14.5h5.5\"/></svg>");

    /// <summary>The six dots of a drag handle.</summary>
    /// <returns>The inline SVG.</returns>
    public static IHtmlContent Grip() => new HtmlString(
        "<svg class=\"grip\" width=\"9\" height=\"15\" viewBox=\"0 0 10 16\" aria-hidden=\"true\"><circle cx=\"2\" cy=\"2\" r=\"1.4\"/><circle cx=\"8\" cy=\"2\" r=\"1.4\"/><circle cx=\"2\" cy=\"8\" r=\"1.4\"/><circle cx=\"8\" cy=\"8\" r=\"1.4\"/><circle cx=\"2\" cy=\"14\" r=\"1.4\"/><circle cx=\"8\" cy=\"14\" r=\"1.4\"/></svg>");

    /// <summary>The three dots of "more".</summary>
    /// <returns>The inline SVG.</returns>
    public static IHtmlContent Dots() => new HtmlString(
        "<svg class=\"dots\" width=\"16\" height=\"4\" viewBox=\"0 0 16 4\" aria-hidden=\"true\"><circle cx=\"2\" cy=\"2\" r=\"1.5\"/><circle cx=\"8\" cy=\"2\" r=\"1.5\"/><circle cx=\"14\" cy=\"2\" r=\"1.5\"/></svg>");
}
