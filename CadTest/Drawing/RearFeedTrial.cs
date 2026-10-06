namespace CadTest.Drawing;

public static class RearFeedTrial
{
    public const double Radius = 3.4, StartRadius = 130;
    public static double Angle => 7 * Math.PI / 180;
    public static RearPortStudy.Result Crossing => RearPortStudy.Evaluate(7.5, 127, 265);
    public static double EndRadius => Crossing.IntersectionRadius - 1;
    public static double X(double radial) => 257.5 + (radial - 127) * Math.Tan(Angle);
    public static double Length => (StartRadius - EndRadius) / Math.Cos(Angle);

    public static void RunChecks()
    {
        double coarse = RemovedVolume(400), fine = RemovedVolume(800);
        if (fine <= 0 || fine >= Math.PI * Radius * Radius * Length || Math.Abs(coarse - fine) > .05)
            throw new InvalidOperationException("Rear feed integral bounds/convergence failed.");
        if (Math.Abs(X(Crossing.IntersectionRadius) - Crossing.IntersectionX) > 1e-9 ||
            Math.Abs(ChannelTrialStudy.CentreRadius(Crossing.IntersectionX) - Crossing.IntersectionRadius) > 1e-9)
            throw new InvalidOperationException("Rear feed axis crossing failed.");
        // The finite end is beyond the crossing; the crossing lies strictly
        // inside both cylindrical tools, establishing a nonzero common volume.
        double along = (Crossing.IntersectionRadius - EndRadius) / Math.Cos(Angle);
        if (along <= 0 || along >= Length || Crossing.IntersectionX <= ChannelTrialStudy.EndX || Crossing.IntersectionX >= 265)
            throw new InvalidOperationException("Rear feed finite intersection failed.");
        foreach (int n in new[] { -1, 0, 39, 1601, int.MaxValue })
        {
            bool rejected = false;
            try { RemovedVolume(n); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid feed integration size accepted.");
        }
        Console.WriteLine($"[TEST OK] Rear feed trial: crossing inside finite tools, integral {coarse:F6}/{fine:F6}, 5 invalid sizes.");
    }

    // One finite feed cylinder against the axisymmetric base, excluding the
    // existing long channel. Nearby bolt holes are outside the angular band.
    public static double RemovedVolume(int n)
    {
        if (n < 40 || n > 1600) throw new ArgumentOutOfRangeException(nameof(n));
        var part = DrawingRevision.Create();
        double sin = Math.Sin(Angle), cos = Math.Cos(Angle), x0 = X(EndRadius), x1 = X(StartRadius);
        double min = x0 - Radius * cos, max = x1 + Radius * cos;
        var breaks = part.RevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX })
            .Concat(part.BoreRevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX }))
            .Concat(part.AnnularCuts.SelectMany(e => new[] { e.StartX, e.EndX }))
            .Concat(new[] { min, max, x0, x1 }).Where(x => x >= min && x <= max).Distinct().OrderBy(x => x).ToArray();
        double total = 0;
        for (int k = 0; k + 1 < breaks.Length; k++)
        {
            double dx = (breaks[k + 1] - breaks[k]) / n;
            for (int i = 0; i < n; i++)
            {
                double x = breaks[k] + (i + .5) * dx;
                double outer = ChannelTrialStudy.OuterRadius(part, x), bore = ChannelTrialStudy.BoreRadius(part, x);
                var cut = part.AnnularCuts.FirstOrDefault(c => x >= c.StartX && x < c.EndX);
                double lowGap = cut?.InnerRadiusAt(x) ?? outer, highGap = cut?.OuterRadius ?? outer;
                for (int j = 0; j < n; j++)
                {
                    double phi = -Math.PI / 2 + (j + .5) * Math.PI / n;
                    double t = Radius * Math.Sin(phi), t2 = t * t;
                    double centre = 127 + (x - 257.5) / Math.Tan(Angle), span = Radius * Math.Cos(phi) / sin;
                    double lo = Math.Max(centre - span, EndRadius - (x - x0) * sin / cos);
                    double hi = Math.Min(centre + span, StartRadius - (x - x1) * sin / cos);
                    double Material(double l, double h)
                    {
                        double Band(double a, double b) => b * b <= t2 ? 0 : Math.Max(0,
                            Math.Min(h, Math.Sqrt(b * b - t2)) - Math.Max(l, Math.Sqrt(Math.Max(0, a * a - t2))));
                        return h <= l ? 0 : Band(bore, Math.Min(outer, lowGap)) + Band(Math.Max(bore, highGap), outer);
                    }
                    double removal = Material(lo, hi);
                    if (t2 < 3.15 * 3.15)
                    {
                        double mainCentre = ChannelTrialStudy.CentreRadius(x);
                        double mainSpan = Math.Sqrt(3.15 * 3.15 - t2) / Math.Cos(ChannelTrialStudy.Angle);
                        removal -= Material(Math.Max(lo, mainCentre - mainSpan), Math.Min(hi, mainCentre + mainSpan));
                    }
                    total += removal * dx * Math.PI / n * Radius * Math.Cos(phi);
                }
            }
        }
        return total;
    }
}
