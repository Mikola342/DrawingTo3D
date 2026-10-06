namespace CadTest.Drawing;

public static class SplineToolBounds
{
    // Conservative Cartesian bound; unlike a meridional trace it covers the whole root arc.
    public static double MinimumToolV => 47 * Math.Cos(SplineTrialProfile.Root.RootHalfAngle);
    public static double MaximumDiskRadius => 97 - MinimumToolV;
    public static double LatestContactWithD100 => 70 + Math.Sqrt(MaximumDiskRadius * MaximumDiskRadius - 47 * 47);
    public static double RadialLowerBound(double x)
    {
        if (!double.IsFinite(x) || x < 70 || x > 70 + MaximumDiskRadius)
            throw new ArgumentOutOfRangeException(nameof(x));
        return 97 - Math.Sqrt(Math.Max(0, MaximumDiskRadius * MaximumDiskRadius - (x - 70) * (x - 70)));
    }
    private static double OuterRadius(double x)
    {
        if (x <= DrawingRevision.SplineTangentStart) return 50;
        if (x <= DrawingRevision.SplineTangentEnd)
            return 53 - Math.Sqrt(9 - Math.Pow(x - DrawingRevision.SplineTangentStart, 2));
        if (x <= 95) return 52.5 - (95 - x) / Math.Sqrt(3);
        return 52.5;
    }
    public static double RunChecks()
    {
        if (LatestContactWithD100 >= 89 || MaximumDiskRadius >= 51 || MinimumToolV <= 45)
            throw new InvalidOperationException("Tool support exceeds reviewed integration domain.");
        // Both bounds are monotone on each interval. Left tool / right stock values
        // bound EVERY point of the interval, rather than sampling just its midpoint.
        double[] breaks = [89, DrawingRevision.SplineTangentStart, DrawingRevision.SplineTangentEnd, 95, 70 + MaximumDiskRadius];
        double minimumClearance = double.PositiveInfinity;
        for (int j = 0; j + 1 < breaks.Length; j++)
            for (int i = 0; i < 1000; i++)
            {
                double a = breaks[j] + (breaks[j + 1] - breaks[j]) * i / 1000;
                double b = breaks[j] + (breaks[j + 1] - breaks[j]) * (i + 1) / 1000;
                double gap = RadialLowerBound(a) - OuterRadius(b);
                if (gap <= 0) throw new InvalidOperationException("Disk may cut a neighbouring seat.");
                minimumClearance = Math.Min(minimumClearance, gap);
            }
        foreach (double x in new[] { double.NaN, double.PositiveInfinity, 69.9, 121.0 })
        {
            bool rejected = false;
            try { RadialLowerBound(x); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid tool-bound input accepted.");
        }
        Console.WriteLine($"[TEST OK] Cutter bounds: D100 contact ends before X={LatestContactWithD100:F9}; neighbouring seat clearance >= {minimumClearance:F6} mm for X>=89; 4000 interval bounds.");
        return minimumClearance;
    }
}
