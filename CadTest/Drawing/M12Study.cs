namespace CadTest.Drawing;

public static class M12Study
{
    public const double Pitch = 1.75;
    public static double BasicMinorDiameter => 12 - 5 * Math.Sqrt(3) / 8 * Pitch;
    // Signed intervals are passed directly to IEdge.Evaluate2, not to ICurve.Evaluate2.
    public static void ValidateEdgeInterval(double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
            throw new InvalidOperationException("Invalid trimmed edge interval.");
    }
    public static void RunChecks()
    {
        ValidateEdgeInterval(-10, -5);
        ValidateEdgeInterval(2, 3);
        foreach (var (min, max) in new[] { (double.NaN, 1.0), (0.0, double.PositiveInfinity), (2.0, 1.0), (1.0, 1.0) })
        {
            bool rejected = false;
            try { ValidateEdgeInterval(min, max); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid edge interval accepted.");
        }
        double r = BasicMinorDiameter / 2, chamfer = 2;
        if (Math.Abs(BasicMinorDiameter - 10.106) > .0005)
            throw new InvalidOperationException("M12 basic diameter calculation failed.");
        double analytical = Math.PI * (r * chamfer * chamfer + Math.Pow(chamfer, 3) / 3);
        double integral = 0;
        for (int i = 0; i < 10000; i++)
        {
            double t = (i + .5) * chamfer / 10000;
            integral += Math.PI * (2 * r * t + t * t) * chamfer / 10000;
        }
        if (Math.Abs(analytical - integral) > 1e-6)
            throw new InvalidOperationException("M12 chamfer integral failed.");
        var part = DrawingRevision.Create();
        foreach (double angle in new[] { Math.PI / 6, Math.PI * 7 / 6 })
        {
            double x = 113 * Math.Cos(angle), y = 113 * Math.Sin(angle);
            if (113 - r - chamfer <= DrawingRevision.BossOuterRadius || 113 + r + chamfer >= 127.5)
                throw new InvalidOperationException("M12 mouth not confined to flange.");
            foreach (var hole in part.Holes)
                if (Math.Sqrt(Math.Pow(x - hole.X, 2) + Math.Pow(y - hole.Y, 2)) <= r + chamfer + hole.Diameter / 2)
                    throw new InvalidOperationException("M12 mouth intersects another hole.");
        }
        Console.WriteLine("[TEST OK] M12 basic diameter, chamfer integral, clearances and signed edge intervals.");
    }
}
