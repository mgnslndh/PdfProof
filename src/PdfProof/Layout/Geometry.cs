using System.Globalization;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;

namespace PdfProof.Layout;

internal static class Geometry
{
    public static double Round(double value, int decimals)
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded; // never -0
    }

    public static LayoutBox Round(LayoutBox box, int decimals) =>
        new(Round(box.Left, decimals), Round(box.Bottom, decimals), Round(box.Right, decimals), Round(box.Top, decimals));

    // Axis-aligned bounds over all four corners: PdfPig reports rotated boxes with Bottom > Top.
    public static LayoutBox Bounds(PdfRectangle rectangle)
    {
        double[] xs = [rectangle.BottomLeft.X, rectangle.BottomRight.X, rectangle.TopLeft.X, rectangle.TopRight.X];
        double[] ys = [rectangle.BottomLeft.Y, rectangle.BottomRight.Y, rectangle.TopLeft.Y, rectangle.TopRight.Y];
        return new LayoutBox(xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    public static LayoutBox Union(IEnumerable<LayoutBox> boxes)
    {
        var list = boxes.ToList();
        return new LayoutBox(list.Min(b => b.Left), list.Min(b => b.Bottom), list.Max(b => b.Right), list.Max(b => b.Top));
    }

    public static string Hex(IColor? color)
    {
        if (color is null)
        {
            return "#000000";
        }

        var (red, green, blue) = color.ToRGBValues();
        return "#" + To255(red) + To255(green) + To255(blue);
    }

    private static string To255(double value)
    {
        var clamped = value < 0 ? 0 : value > 1 ? 1 : value;
        return ((int)Math.Round(clamped * 255, MidpointRounding.AwayFromZero)).ToString("X2", CultureInfo.InvariantCulture);
    }
}
