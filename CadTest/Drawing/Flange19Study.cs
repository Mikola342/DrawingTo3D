namespace CadTest.Drawing;

public static class Flange19Study
{
    public const double Diameter = 19;
    public static double RemovedMm3 => 12 * 18 * Math.PI * (Diameter * Diameter - 15 * 15) / 4;
    public static object RunChecks()
    {
        var points = Enumerable.Range(0, 12).Select(i =>
        {
            double a = (15 + i * 30) * Math.PI / 180;
            return (Y: 113 * Math.Sin(a), Z: -113 * Math.Cos(a));
        }).ToArray();
        double minPair = double.PositiveInfinity, minSeparate = double.PositiveInfinity, minM12 = double.PositiveInfinity;
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            minSeparate = Math.Min(minSeparate, Math.Sqrt(Math.Pow(p.Y - 111.5, 2) + p.Z * p.Z) - 9.5 - 11);
            foreach (double a in new[] { Math.PI / 6, 7 * Math.PI / 6 })
                minM12 = Math.Min(minM12, Math.Sqrt(Math.Pow(p.Y - 113 * Math.Sin(a), 2)
                    + Math.Pow(p.Z + 113 * Math.Cos(a), 2)) - 9.5 - 7.5); // conservative M12 chamfer envelope
            for (int j = i + 1; j < points.Length; j++)
                minPair = Math.Min(minPair, Math.Sqrt(Math.Pow(p.Y - points[j].Y, 2) + Math.Pow(p.Z - points[j].Z, 2)) - 19);
        }
        double outerWall = 127.5 - 113 - 9.5, bossGap = 113 - 9.5 - 100;
        if (Math.Abs(RemovedMm3 - 23071.856448) > .000001 || minPair <= 0 || minSeparate <= 0
            || minM12 <= 0 || outerWall <= 0 || bossGap <= 0)
            throw new InvalidOperationException("D19 nominal placement/volume failed.");
        Console.WriteLine($"[TEST OK] D19: removed={RemovedMm3:F6}, pair gap={minPair:F6}, D22 gap={minSeparate:F6}, outside wall={outerWall:F6}.");
        return new { PairGapMm = minPair, SeparateD22GapMm = minSeparate, M12EnvelopeGapMm = minM12,
            OuterWallMm = outerWall, BossRadialGapMm = bossGap, Scope = "Nominal placement, not tolerance/strength acceptance; other cut interactions checked by actual removed volume." };
    }
}
