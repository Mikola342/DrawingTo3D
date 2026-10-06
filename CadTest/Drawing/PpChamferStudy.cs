namespace CadTest.Drawing;

public static class PpChamferStudy
{
    public static double MouthArcRadians => 2 * Math.PI - 2 * Math.Acos((111.5 * 111.5 + 13 * 13 - 100 * 100) / (2 * 111.5 * 13));

    public static double BossOverlapVolume(int intervals = 20000)
    {
        if (intervals < 100 || intervals > 1000000) throw new ArgumentOutOfRangeException(nameof(intervals));
        const double R = 100, d = 111.5;
        double sum = 0;
        for (int i = 0; i < intervals; i++)
        {
            double r = 11 + (i + .5) * 2 / intervals;
            if (r + R <= d) continue;
            double area = R * R * Math.Acos((d * d + R * R - r * r) / (2 * d * R))
                + r * r * Math.Acos((d * d + r * r - R * R) / (2 * d * r))
                - .5 * Math.Sqrt((-d + R + r) * (d + R - r) * (d - R + r) * (d + R + r));
            sum += area * 2 / intervals;
        }
        return sum;
    }

    public static double ClippedRemovedVolume => RemovedVolume(11, 2) - BossOverlapVolume();
    public static double RemovedVolume(double boreRadius, double size)
    {
        if (!double.IsFinite(boreRadius) || !double.IsFinite(size) || boreRadius <= 0 || size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        double result = Math.PI * (boreRadius * size * size + size * size * size / 3);
        if (!double.IsFinite(result)) throw new ArgumentOutOfRangeException(nameof(size));
        return result;
    }

    public static void RunChecks()
    {
        double exact = RemovedVolume(11, 2), integral = 0;
        double overlap = BossOverlapVolume();
        if (Math.Abs(overlap - BossOverlapVolume(10000)) > 1e-6 || overlap <= 0 || overlap >= exact ||
            MouthArcRadians <= 0 || MouthArcRadians >= 2 * Math.PI)
            throw new InvalidOperationException("P-P boss-intersection convergence/arc mismatch.");
        const int n = 10000;
        for (int i = 0; i < n; i++)
        {
            double t = (i + .5) * 2 / n;
            integral += Math.PI * (Math.Pow(11 + t, 2) - 121) * 2 / n;
        }
        if (Math.Abs(exact - integral) > 1e-6 || Math.Abs((127.5 - 111.5 - 13) - 3) > 1e-12)
            throw new InvalidOperationException("P-P chamfer volume/wall mismatch.");
        // The expanded mouth must remain separate from each nominal D19 hole.
        double gap = double.PositiveInfinity;
        for (int i = 0; i < 12; i++)
        {
            double a = (15 + 30 * i) * Math.PI / 180;
            gap = Math.Min(gap, Math.Sqrt(Math.Pow(113 * Math.Cos(a) - 111.5, 2) + Math.Pow(113 * Math.Sin(a), 2)) - 13 - 9.5);
        }
        if (gap <= 0) throw new InvalidOperationException("P-P chamfer overlaps D19 pattern.");
        foreach (var (r, s) in new[] { (0.0, 2.0), (11.0, 0.0), (-1.0, 2.0), (double.NaN, 2.0), (11.0, double.PositiveInfinity), (double.MaxValue, double.MaxValue) })
        {
            try { RemovedVolume(r, s); }
            catch (ArgumentOutOfRangeException) { continue; }
            throw new InvalidOperationException("Invalid P-P input accepted.");
        }
        Console.WriteLine($"[TEST OK] P-P chamfer 2x45: removed={exact:F9}, outer wall=3, D19 gap={gap:F6}; six invalid inputs rejected.");
        Console.WriteLine($"[TEST OK] P-P limited by D200 boss: overlap={overlap:F9}, removed={ClippedRemovedVolume:F9}, mouth arc={MouthArcRadians:R}.");
    }
}
