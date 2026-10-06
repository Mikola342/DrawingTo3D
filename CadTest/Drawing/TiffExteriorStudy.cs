using CadTest.Models;

namespace CadTest.Drawing;

public static class TiffExteriorStudy
{
    // TIFF 56 from the end of the upper R5 to the shoulder at265-26=239:
    // upper tangency X=239-56=183, not the unfilleted theoretical corner.
    public static double StartX => 183 - 2.5 / Math.Tan(Math.PI / 6) - 10 * Math.Tan(Math.PI / 12);
    public static List<RevolveProfileElement> Profile()
    {
        double low = StartX, high = 183;
        double flangeRootR = 92 + 3 * Math.Tan(Math.PI / 8);
        return [
            Arc(low, 52.5, low + 2.5, 57.5 - 5 * Math.Cos(Math.PI / 6), low, 57.5, 5),
            Line(low + 2.5, 57.5 - 5 * Math.Cos(Math.PI / 6), high - 2.5, 50 + 5 * Math.Cos(Math.PI / 6)),
            Arc(high - 2.5, 50 + 5 * Math.Cos(Math.PI / 6), high, 55, high, 50, 5, true),
            Line(high, 55, 233, 55),
            Arc(233, 55, 239, 61, 233, 61, 6),
            Line(239, 61, 239, 67.5),
            Line(239, 67.5, 241, 69.5),
            Line(241, 69.5, 241, 86),
            Line(241, 86, 244 + 3 / Math.Sqrt(2), flangeRootR - 3 / Math.Sqrt(2)),
            Arc(244 + 3 / Math.Sqrt(2), flangeRootR - 3 / Math.Sqrt(2), 247, flangeRootR, 244, flangeRootR, 3)
        ];
    }
    private static RevolveProfileElement Line(double x0, double r0, double x1, double r1) => new() {
        Type = RevolveProfileElementType.Line, StartX = x0, StartRadius = r0, EndX = x1, EndRadius = r1 };
    private static RevolveProfileElement Arc(double x0, double r0, double x1, double r1, double cx, double cr, double r, bool cw = false) => new() {
        Type = RevolveProfileElementType.Arc, StartX = x0, StartRadius = r0, EndX = x1, EndRadius = r1,
        CenterX = cx, CenterRadius = cr, Radius = r, Clockwise = cw };

    public static double RadiusAt(RevolveProfileElement e, double x)
    {
        if (e.Type != RevolveProfileElementType.Arc)
            return e.StartRadius + (e.EndRadius - e.StartRadius) * (x - e.StartX) / (e.EndX - e.StartX);
        double sign = Math.Sign((e.StartRadius + e.EndRadius) / 2 - e.CenterRadius);
        return e.CenterRadius + sign * Math.Sqrt(Math.Max(0, e.Radius * e.Radius - Math.Pow(x - e.CenterX, 2)));
    }

    private static double EffectiveVolume(IEnumerable<RevolveProfileElement> profile, int count)
    {
        double volume = 0;
        foreach (var e in profile.Where(e => e.EndX > e.StartX && e.EndX > StartX && e.StartX < 247))
        {
            double start = Math.Max(StartX, e.StartX), end = Math.Min(247, e.EndX), dx = (end - start) / count;
            for (int i = 0; i < count; i++)
            {
                double x = start + (i + .5) * dx, r = RadiusAt(e, x);
                // Only the existing D12 atR84 changes its intersection with this exterior.
                // Its cutter startsX241. Other active cutters are wholly inside both envelopes
                // in this interval or start at/afterX247 and cancel in the volume difference.
                volume += (Math.PI * r * r - (x >= 241 ? RightTransitionStudy.CircleOverlap(r, 6, 84) : 0)) * dx;
            }
        }
        return volume;
    }

    public static double RunChecks()
    {
        var p = Profile();
        (double X, double R) Tangent(RevolveProfileElement e, bool end)
        {
            double x = e.EndX - e.StartX, r = e.EndRadius - e.StartRadius;
            if (e.Type == RevolveProfileElementType.Arc)
            {
                double dx = (end ? e.EndX : e.StartX) - e.CenterX;
                double dr = (end ? e.EndRadius : e.StartRadius) - e.CenterRadius;
                x = e.Clockwise ? dr : -dr; r = e.Clockwise ? -dx : dx;
            }
            double length = Math.Sqrt(x * x + r * r); return (x / length, r / length);
        }
        for (int i = 0; i < p.Count; i++)
        {
            var e = p[i];
            if (e.EndX < e.StartX || e.StartRadius <= 0 || e.EndRadius <= 0)
                throw new InvalidOperationException("Invalid TIFF exterior segment.");
            if (i > 0 && (Math.Abs(e.StartX - p[i - 1].EndX) > 1e-10 || Math.Abs(e.StartRadius - p[i - 1].EndRadius) > 1e-10))
                throw new InvalidOperationException("Discontinuous TIFF exterior.");
            if (e.Type == RevolveProfileElementType.Arc)
                foreach (var point in new[] { (e.StartX, e.StartRadius), (e.EndX, e.EndRadius) })
                    if (Math.Abs(Math.Pow(point.Item1 - e.CenterX, 2) + Math.Pow(point.Item2 - e.CenterRadius, 2) - e.Radius * e.Radius) > 1e-9)
                        throw new InvalidOperationException("TIFF exterior arc does not meet its endpoints.");
            if (i > 0 && (e.Type == RevolveProfileElementType.Arc || p[i - 1].Type == RevolveProfileElementType.Arc))
            {
                var a = Tangent(p[i - 1], true); var b = Tangent(e, false);
                if (Math.Abs(a.X * b.R - a.R * b.X) > 1e-10 || a.X * b.X + a.R * b.R < 1 - 1e-10)
                    throw new InvalidOperationException("Non-tangent TIFF fillet connection.");
            }
        }
        if (Math.Abs(p[2].EndX - (265 - 26 - 56)) > 1e-10 || Math.Abs(p[8].StartRadius + (247 - p[8].StartX) - 92) > 1e-10)
            throw new InvalidOperationException("TIFF R5 tangency datum / D184 theoretical corner mismatch.");
        var old = DrawingRevision.Create().RevolveProfile;
        double fine = EffectiveVolume(p, 40000) - EffectiveVolume(old, 40000);
        double coarse = EffectiveVolume(p, 20000) - EffectiveVolume(old, 20000);
        if (!double.IsFinite(fine) || Math.Abs(fine - coarse) > .01)
            throw new InvalidOperationException("TIFF exterior volume integration did not converge.");
        Console.WriteLine($"[TEST OK] TIFF exterior R5/R6/D135/D184/R3: start={StartX:F9}, upper tangency=183, straight D105 span={StartX - 95:F9}, net delta includingD12={fine:F9}, refinement={Math.Abs(fine - coarse):F9}. Finish extent82min not certified.");
        return fine;
    }
}
