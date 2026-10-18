using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmoothDraw.Views;

/// <summary>
/// The arrow's shape: a shaft (a Line, round cap at the tail) and a filled head (a Polygon), the
/// same two elements as always (`_arrowPairs`, undo, Select). Since 2026-10-18 (Omri: "the
/// arrow's point doesn't look good"): the head's rounded tip lands exactly on the end point, the
/// shaft stops inside the head (it used to run on to the tip, a stub poking out past the point),
/// and the head grows with the stroke's thickness. The live preview uses the same call, so the
/// arrow looks the same while it is dragged as after.
/// </summary>
public static class ArrowShape
{
    /// <summary>Head length = this × thickness, never under <see cref="MinHeadLength"/>.</summary>
    private const double HeadLengthPerThickness = 5.0;
    private const double MinHeadLength = 18;
    /// <summary>Head width / head length (≈ 44° at the tip).</summary>
    private const double HeadWidthRatio = 0.8;
    /// <summary>A short arrow's head takes at most this share of the arrow's length (but see Place).</summary>
    private const double MaxHeadShare = 0.6;

    /// <summary>Lays <paramref name="shaft"/> and <paramref name="head"/> out for an arrow from
    /// <paramref name="start"/> to <paramref name="end"/> drawn <paramref name="thickness"/> wide.</summary>
    public static void Place(Line shaft, Polygon head, Point start, Point end, double thickness)
    {
        Vector along = end - start;
        double length = along.Length;
        if (length < 0.01)
        {
            shaft.X1 = shaft.X2 = start.X; shaft.Y1 = shaft.Y2 = start.Y;
            head.Points = new PointCollection();
            return;
        }
        Vector dir = along / length, normal = new(-dir.Y, dir.X);

        // A short arrow gives its head at most 60 % of its length, but a thick one keeps a head of
        // 2.5 × the thickness (up to the whole arrow): a smaller one is a blob, not a point.
        double headLength = Math.Min(Math.Max(MinHeadLength, HeadLengthPerThickness * thickness),
                                     Math.Max(length * MaxHeadShare, Math.Min(length, 2.5 * thickness)));
        double halfWidth = headLength * HeadWidthRatio / 2;
        double tan = halfWidth / headLength;                 // tan of the half angle at the tip
        double sin = tan / Math.Sqrt(1 + tan * tan);

        // Rounded corners: the polygon is the sharp head shrunk by r and drawn with a 2r round-join
        // outline, which grows it back to the sharp head's edges with round corners. A rounded tip
        // falls r(1/sin - 1) short of the sharp one, so the sharp tip goes that far past the end.
        double r = Math.Min(thickness * 0.3, headLength * 0.08);
        Point tip = end + dir * (r * (1 / sin - 1));
        Point baseCentre = tip - dir * headLength;
        Point left = baseCentre + normal * halfWidth, right = baseCentre - normal * halfWidth;

        // Shrinking a triangle by r = scaling it about its incentre by (1 - r / inradius)
        double a = (left - right).Length, b = (right - tip).Length, c = (tip - left).Length;
        Point incentre = new((a * tip.X + b * left.X + c * right.X) / (a + b + c),
                             (a * tip.Y + b * left.Y + c * right.Y) / (a + b + c));
        double inradius = headLength * halfWidth / ((a + b + c) / 2); // area / semi-perimeter
        double k = Math.Max(0, 1 - r / inradius);
        Point Shrink(Point p) => incentre + (p - incentre) * k;

        head.Points = new PointCollection { Shrink(tip), Shrink(left), Shrink(right) };
        head.StrokeThickness = k > 0 ? 2 * r : 0;
        head.StrokeLineJoin = PenLineJoin.Round;

        // The shaft's flat end must sit where the head is wider than the shaft (with a pixel to
        // spare), so no corner of it shows: 60 % of the way back from the tip, or further for a
        // stubby head. Never behind the tail.
        double inside = Math.Max(headLength * 0.6, (thickness / 2 + 1) / tan);
        inside = Math.Min(inside, headLength);
        double shaftLength = Math.Max(0, (tip - start).Length - inside);
        Point shaftEnd = start + dir * shaftLength;
        shaft.X1 = start.X; shaft.Y1 = start.Y;
        shaft.X2 = shaftEnd.X; shaft.Y2 = shaftEnd.Y;
        shaft.StrokeThickness = thickness;
        shaft.StrokeStartLineCap = PenLineCap.Round;
        shaft.StrokeEndLineCap = PenLineCap.Flat;
    }
}
