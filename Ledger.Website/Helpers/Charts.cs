using System.Globalization;
using System.Net;
using System.Text;
using Ledger.Library.Categories;
using Ledger.Sdk.Models;
using Microsoft.AspNetCore.Html;

namespace Ledger.Website.Helpers;

/// <summary>
/// The drawings, as inline SVG built on the server: nothing to download, nothing to run in the browser.
/// </summary>
public static class Charts
{
    /// <summary>The colour of what was left over (and of money coming in).</summary>
    public const string LeftColor = "#7FC7AE";

    /// <summary>The colour of a month that spent more than came in.</summary>
    public const string ShortColor = "#D9788F";

    private const double Width = 1160;
    private const double Band = 300;
    private const double LeftGap = 20;
    private const double RightGap = 20;
    private const double NodeLeft = 240;
    private const double NodeRight = 880;
    private const double Spine = 576;
    private const double Thick = 8;
    private const double LeftSlot = 34;
    private const double RightSlot = 16;

    private sealed record Node(string Label, decimal Amount, string Color, bool Emphasis = false);

    /// <summary>
    /// The flow of a month: where the money came from on the left, where it went on the right, each ribbon as thick as
    /// its amount. What was left over is the last ribbon on the right; a month that spent more than came in gets a
    /// "drawn from your balance" ribbon on the left instead, so both sides always add up.
    /// </summary>
    /// <param name="sources">The inflow categories, biggest first.</param>
    /// <param name="targets">The outflow categories, biggest first.</param>
    /// <param name="lang">The language of the request.</param>
    /// <param name="label">What the drawing says to a screen reader.</param>
    /// <returns>The SVG, or nothing when either side is empty.</returns>
    public static IHtmlContent Flow(IReadOnlyList<FlowPartDTO> sources, IReadOnlyList<FlowPartDTO> targets, Lang lang, string label)
    {
        if (sources.Count == 0 || targets.Count == 0)
        {
            return HtmlString.Empty;
        }

        var incoming = sources.Sum(static part => part.Amount);
        var outgoing = targets.Sum(static part => part.Amount);
        var left = sources.Select(part => new Node(lang.Category(part.Key), part.Amount, Categories.Color(part.Key))).ToList();
        var right = targets.Select(part => new Node(lang.Category(part.Key), part.Amount, Categories.Color(part.Key))).ToList();
        if (incoming > outgoing)
        {
            right.Add(new Node(lang.T("Left over", "Sobrou"), incoming - outgoing, LeftColor, Emphasis: true));
        }
        else if (outgoing > incoming)
        {
            left.Add(new Node(lang.T("From your balance", "Do teu saldo"), outgoing - incoming, ShortColor));
        }

        var total = (double)Math.Max(incoming, outgoing);
        var scale = Band / total;
        // A node too thin for its label takes a little more room; its ribbon stays as thick as its amount.
        var leftHeight = left.Sum(node => Math.Max((double)node.Amount * scale, LeftSlot)) + (LeftGap * (left.Count - 1));
        var rightHeight = right.Sum(node => Math.Max((double)node.Amount * scale, node.Emphasis ? 0 : RightSlot)) + (RightGap * (right.Count - 1));
        var height = Math.Max(leftHeight, rightHeight) + 10;
        var spineTop = (height - Band) / 2;

        var ribbons = new StringBuilder();
        var nodes = new StringBuilder();
        var emphasis = new StringBuilder();

        // Left: each source bends into its slice of the spine.
        var y = (height - leftHeight) / 2;
        var onSpine = spineTop;
        foreach (var node in left)
        {
            var h = (double)node.Amount * scale;
            var top = y + ((Math.Max(h, LeftSlot) - h) / 2);
            var middle = (NodeLeft + Thick + Spine) / 2;
            ribbons.Append(CultureInfo.InvariantCulture, $"<path d=\"M{F(NodeLeft + Thick)},{F(top)} C{F(middle)},{F(top)} {F(middle)},{F(onSpine)} {F(Spine)},{F(onSpine)} L{F(Spine)},{F(onSpine + h)} C{F(middle)},{F(onSpine + h)} {F(middle)},{F(top + h)} {F(NodeLeft + Thick)},{F(top + h)} Z\" fill=\"{node.Color}\"><title>{E(node.Label)} · {E(lang.Money(node.Amount))}</title></path>");
            nodes.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(NodeLeft)}\" y=\"{F(top)}\" width=\"{F(Thick)}\" height=\"{F(Math.Max(h, 3))}\" rx=\"{F(Math.Min(3, Math.Max(h, 3) / 2))}\" fill=\"{node.Color}\"/>");
            var mid = top + (h / 2);
            nodes.Append(CultureInfo.InvariantCulture, $"<text class=\"flow-name\" x=\"{F(NodeLeft - 14)}\" y=\"{F(mid - 2)}\" text-anchor=\"end\">{E(node.Label)}</text>");
            nodes.Append(CultureInfo.InvariantCulture, $"<text class=\"flow-amount\" x=\"{F(NodeLeft - 14)}\" y=\"{F(mid + 14)}\" text-anchor=\"end\">{E(lang.Money(node.Amount))}</text>");
            y += Math.Max(h, LeftSlot) + LeftGap;
            onSpine += h;
        }

        // Right: each slice of the spine fans out to its category.
        y = (height - rightHeight) / 2;
        onSpine = spineTop;
        foreach (var node in right)
        {
            var h = (double)node.Amount * scale;
            var top = y + ((Math.Max(h, node.Emphasis ? 0 : RightSlot) - h) / 2);
            var from = Spine + Thick;
            var middle = (from + NodeRight) / 2;
            var path = string.Create(CultureInfo.InvariantCulture, $"<path d=\"M{F(from)},{F(onSpine)} C{F(middle)},{F(onSpine)} {F(middle)},{F(top)} {F(NodeRight)},{F(top)} L{F(NodeRight)},{F(top + h)} C{F(middle)},{F(top + h)} {F(middle)},{F(onSpine + h)} {F(from)},{F(onSpine + h)} Z\" fill=\"{node.Color}\"><title>{E(node.Label)} · {E(lang.Money(node.Amount))}</title></path>");
            (node.Emphasis ? emphasis : ribbons).Append(path);
            nodes.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(NodeRight)}\" y=\"{F(top)}\" width=\"{F(Thick)}\" height=\"{F(Math.Max(h, 3))}\" rx=\"{F(Math.Min(3, Math.Max(h, 3) / 2))}\" fill=\"{node.Color}\"/>");
            var mid = top + (h / 2);
            if (node.Emphasis && h >= 44)
            {
                nodes.Append(CultureInfo.InvariantCulture, $"<text class=\"flow-left\" x=\"{F(NodeRight + 22)}\" y=\"{F(mid - 2)}\">{E(node.Label)}</text>");
                nodes.Append(CultureInfo.InvariantCulture, $"<text class=\"flow-left-amount\" x=\"{F(NodeRight + 22)}\" y=\"{F(mid + 19)}\">{E(lang.Signed(node.Amount))}</text>");
            }
            else
            {
                nodes.Append(CultureInfo.InvariantCulture, $"<text class=\"flow-name{(node.Emphasis ? " flow-kept" : string.Empty)}\" x=\"{F(NodeRight + 22)}\" y=\"{F(mid + 4.5)}\">{E(node.Label)}<tspan class=\"flow-amount\" dx=\"10\">{E(lang.Money(node.Amount))}</tspan></text>");
            }

            y += Math.Max(h, node.Emphasis ? 0 : RightSlot) + RightGap;
            onSpine += h;
        }

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg class=\"flow\" role=\"img\" aria-label=\"{E(label)}\" viewBox=\"0 0 {F(Width)} {F(height)}\">");
        svg.Append("<g class=\"flow-ribbons\">").Append(ribbons).Append("</g>");
        svg.Append("<g class=\"flow-ribbons flow-ribbons-kept\">").Append(emphasis).Append("</g>");
        svg.Append(CultureInfo.InvariantCulture, $"<rect class=\"flow-spine\" x=\"{F(Spine)}\" y=\"{F(spineTop)}\" width=\"{F(Thick)}\" height=\"{F(Band)}\" rx=\"3\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<text class=\"flow-amount\" x=\"{F(Spine + (Thick / 2))}\" y=\"{F(spineTop - 16)}\" text-anchor=\"middle\">{E(lang.Money(Math.Max(incoming, outgoing)))}</text>");
        svg.Append(nodes).Append("</svg>");
        return new HtmlString(svg.ToString());
    }

    /// <summary>The height of a bar in the month strip: taller for a bigger result, never invisible.</summary>
    /// <param name="net">What the month came to.</param>
    /// <param name="largest">The biggest result (either way) among the months shown.</param>
    /// <param name="tallest">The height of the tallest bar, in pixels.</param>
    /// <returns>The height in pixels.</returns>
    public static int BarHeight(decimal net, decimal largest, int tallest = 26) =>
        largest <= 0 ? 4 : Math.Max(4, (int)Math.Round((double)(Math.Abs(net) / largest) * tallest));

    /// <summary>A share (0 to 1) as a CSS percentage, clamped to the bar.</summary>
    /// <param name="share">The share.</param>
    /// <returns>"38.4%".</returns>
    public static string Width100(decimal share) =>
        (Math.Clamp(share, 0m, 1m) * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string F(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

    private static string E(string text) => WebUtility.HtmlEncode(text);
}
