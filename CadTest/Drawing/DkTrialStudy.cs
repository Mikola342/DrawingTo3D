namespace CadTest.Drawing;

// Authorized approximation: the 13.5 depth is deliberately rebased to cavity floor X260.
public static class DkTrialStudy
{
    public const double FloorX = 260, Depth = 13.5, ToolRadius = 16, MouthRadius = 16.5, CentreY = -74;
    public const double BottomX = FloorX - Depth, ArcCentreX = BottomX + ToolRadius;
    public static double FlatRadius => MouthRadius - Math.Sqrt(2 * ToolRadius * Depth - Depth * Depth);
    public static double RadiusAt(double x) => FlatRadius + Math.Sqrt(Math.Max(0, ToolRadius * ToolRadius - Math.Pow(x - ArcCentreX, 2)));
    private static double Overlap(double r)
    {
        const double d = 10, q = 6;
        if (r + q <= d) return 0;
        if (r >= d + q) return Math.PI * q * q;
        double a = Math.Acos(Math.Clamp((d * d + r * r - q * q) / (2 * d * r), -1, 1));
        double b = Math.Acos(Math.Clamp((d * d + q * q - r * r) / (2 * d * q), -1, 1));
        double k = Math.Sqrt(Math.Max(0, (-d + r + q) * (d + r - q) * (d - r + q) * (d + r + q)));
        return r * r * a + q * q * b - k / 2;
    }
    public static double Volume(int n)
    {
        if (n < 100 || n > 1000000) throw new ArgumentOutOfRangeException(nameof(n));
        double dx = Depth / n, total = 0;
        for (int i = 0; i < n; i++)
        {
            double r = RadiusAt(BottomX + (i + .5) * dx);
            total += (Math.PI * r * r - Overlap(r)) * dx;
        }
        return total;
    }
    public static void RunChecks()
    {
        if (Math.Abs(RadiusAt(FloorX) - MouthRadius) > 1e-10 || FlatRadius <= 0)
            throw new InvalidOperationException("DK trial arc construction failed.");
        var part = DrawingRevision.Create();
        for (int i = 0; i <= 1000; i++)
        {
            double x = BottomX + Depth * i / 1000, r = RadiusAt(x);
            if (74 + r >= ChannelTrialStudy.OuterRadius(part, x) || 74 - r <= ChannelTrialStudy.BoreRadius(part, x))
                throw new InvalidOperationException("DK trial leaves assumed solid base.");
        }
        double a = Volume(10000), b = Volume(20000);
        if (Math.Abs(a - b) > .01 || b <= 0) throw new InvalidOperationException("DK trial integral convergence failed.");
        Console.WriteLine($"[TEST OK] APPROX DK rebased to X260: removal {a:F6}/{b:F6}; flat R{FlatRadius:F9}. Not drawing datum acceptance.");
    }
}
