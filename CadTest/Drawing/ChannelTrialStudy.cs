using CadTest.Models;

namespace CadTest.Drawing;

public static class ChannelTrialStudy
{
    public const double AnchorX = 265, AnchorRadius = 51, RadialX = 108;
    public const double MainRadius = 3.15, RadialRadius = 3, OverlapAllowance = .25;
    public static double Angle => 4 * Math.PI / 180;
    public static double EndX => RadialX - RadialRadius - OverlapAllowance;
    public static double Length => (AnchorX - EndX) / Math.Cos(Angle);
    public static double CentreRadius(double x) => AnchorRadius + (x - AnchorX) * Math.Tan(Angle);
    public static readonly double[] BranchAngles = [60, 120];
    public sealed record Integral(double MainPairMm3, double ConnectedPairMm3);

    internal static double OuterRadius(PartDescription part, double x)
    {
        var e = part.RevolveProfile.First(e => e.StartX <= x && e.EndX > x);
        return e.Type == RevolveProfileElementType.Arc
            ? e.CenterRadius - Math.Sqrt(Math.Max(0, e.Radius * e.Radius - Math.Pow(x - e.CenterX, 2)))
            : e.StartRadius + (e.EndRadius - e.StartRadius) * (x - e.StartX) / (e.EndX - e.StartX);
    }
    internal static double BoreRadius(PartDescription part, double x)
    {
        var e = part.BoreRevolveProfile.First(e => e.StartX <= x && e.EndX > x);
        return e.StartRadius + (e.EndRadius - e.StartRadius) * (x - e.StartX) / (e.EndX - e.StartX);
    }
    // Integrate the union of finite cylinders against the existing axisymmetric material.
    // Splines, flats and M12/flange holes are disjoint from this domain by bounds below.
    public static Integral Integrate(int samples)
    {
        if (samples < 40) throw new ArgumentOutOfRangeException(nameof(samples));
        var part = DrawingRevision.Create();
        double sine = Math.Sin(Angle), cosine = Math.Cos(Angle);
        double minX = EndX - MainRadius * sine, maxX = AnchorX + MainRadius * sine;
        var breaks = part.RevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX })
            .Concat(part.BoreRevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX }))
            .Concat(part.AnnularCuts.SelectMany(c => new[] { c.StartX, c.EndX, (c.StartX + c.EndX) / 2 }))
            .Concat(new[] { minX, EndX + MainRadius * sine, 105, 108, 111, AnchorX - MainRadius * sine, maxX })
            .Where(x => x >= minX && x <= maxX).Distinct().OrderBy(x => x).ToArray();
        double total = 0, mainTotal = 0;
        for (int k = 0; k + 1 < breaks.Length; k++)
        {
            double dx = (breaks[k + 1] - breaks[k]) / samples;
            for (int i = 0; i < samples; i++)
            {
                double x = breaks[k] + (i + .5) * dx, outer = OuterRadius(part, x), bore = BoreRadius(part, x);
                var cut = part.AnnularCuts.FirstOrDefault(c => x >= c.StartX && x < c.EndX);
                double gapLow = cut?.InnerRadiusAt(x) ?? outer, gapHigh = cut?.OuterRadius ?? outer;
                double centre = CentreRadius(x), radialT2 = 9 - Math.Pow(x - RadialX, 2);
                // Split at the edge of the radial cylinder to avoid integrating across its square-root endpoint.
                double phiRadial = radialT2 > 0 ? Math.Asin(Math.Sqrt(radialT2) / MainRadius) : 0;
                double[] phiBreaks = [-Math.PI / 2, -phiRadial, phiRadial, Math.PI / 2];
                for (int b = 0; b < 3; b++)
                {
                    double dphi = (phiBreaks[b + 1] - phiBreaks[b]) / samples;
                    if (dphi == 0) continue;
                    for (int j = 0; j < samples; j++)
                    {
                        double phi = phiBreaks[b] + (j + .5) * dphi;
                        double t = MainRadius * Math.Sin(phi), t2 = t * t;
                        double span = MainRadius * Math.Cos(phi) / cosine;
                        double lo = Math.Max(centre - span, AnchorRadius + (-Length - (x - AnchorX) * cosine) / sine);
                        double hi = Math.Min(centre + span, AnchorRadius - (x - AnchorX) * cosine / sine);
                        double rlo = CentreRadius(RadialX), rhi = radialT2 >= t2 ? 55 : rlo;
                        double Material(double low, double high)
                        {
                            if (high <= low) return 0;
                            double Band(double inner, double outerR) => outerR * outerR <= t2 ? 0 :
                                Math.Max(0, Math.Min(high, Math.Sqrt(outerR * outerR - t2)) -
                                    Math.Max(low, Math.Sqrt(Math.Max(0, inner * inner - t2))));
                            return Band(bore, Math.Min(outer, gapLow)) + Band(Math.Max(bore, gapHigh), outer);
                        }
                        double main = Material(lo, hi);
                        double union = main + Material(rlo, rhi) - Material(Math.Max(lo, rlo), Math.Min(hi, rhi));
                        double weight = dx * dphi * MainRadius * Math.Cos(phi);
                        mainTotal += main * weight;
                        total += union * weight;
                    }
                }
            }
        }
        return new(2 * mainTotal, 2 * total);
    }

    public static double MinimumBoreWall()
    {
        var part = DrawingRevision.Create();
        double min = double.PositiveInfinity;
        var stations = Enumerable.Range(0, 10001).Select(i => EndX + (265 - EndX - 1e-6) * i / 10000)
            .Concat(new[] { 203.5, 240, 254, 260 });
        foreach (double x in stations)
            min = Math.Min(min, CentreRadius(x) - MainRadius / Math.Cos(Angle) - BoreRadius(part, x));
        return min;
    }
    public static void RunChecks()
    {
        var part = DrawingRevision.Create();
        double toolMinX = EndX - MainRadius * Math.Sin(Angle);
        double toolMaxRadius = AnchorRadius + MainRadius / Math.Cos(Angle);
        if (toolMinX <= 95 || toolMaxRadius >= 56)
            throw new InvalidOperationException("Channel tool overlaps the assumed disjoint spline or flange-hole regions.");
        foreach (var h in part.Holes)
            if (Math.Sqrt(h.X * h.X + h.Y * h.Y) - h.Diameter / 2 <= toolMaxRadius)
                throw new InvalidOperationException("Channel integral needs explicit hole-intersection handling.");
        if (Length <= 110 || EndX - MainRadius * Math.Sin(Angle) <= 95 || MinimumBoreWall() <= 2)
            throw new InvalidOperationException("Channel placement/clearance checks failed.");
        if (MainRadius <= RadialRadius || OverlapAllowance <= 0 || CentreRadius(265) != 51)
            throw new InvalidOperationException("Channel connectivity parameters failed.");
        // Entire terminal disk of the radial cutter lies inside the long cylinder;
        // its projection is within the long cylinder end caps, not just tangent contact.
        for (int i = 0; i < 1000; i++)
        {
            double a = i * 2 * Math.PI / 1000;
            double x = RadialX + RadialRadius * Math.Cos(a), t = RadialRadius * Math.Sin(a);
            double v = CentreRadius(RadialX), s = (x - AnchorX) * Math.Cos(Angle) + (v - AnchorRadius) * Math.Sin(Angle);
            double distance2 = Math.Pow((v - CentreRadius(x)) * Math.Cos(Angle), 2) + t * t;
            if (distance2 >= MainRadius * MainRadius || s <= -Length || s >= 0)
                throw new InvalidOperationException("Radial port is not fully inside extended channel.");
        }
        double branchGap = CentreRadius(EndX) - 2 * MainRadius;
        if (branchGap <= 0) throw new InvalidOperationException("Branches overlap.");
        Console.WriteLine($"[TEST OK] Channel trial: L={Length:F6} instead of 110; 1000 connection samples; bore-wall lower bound {MinimumBoreWall():F6} mm.");
    }
}
