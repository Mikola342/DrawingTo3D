namespace CadTest.Drawing;

public static class TiffBoreStudy
{
    public const double Start = 265 - 84, OldStart = 203.5, Radius = 34, OldRadius = 31;
    public static double Bore80RemovedMm3 => Math.PI * (3 * (40 * 40 - 34 * 34) + 14 * (40 * 40 - 39 * 39));
    public static double Bore80RestoredMm3 => Math.PI * (45 * 45 - 40 * 40) * 11;

    // Cross-section of the 10-degree channel is an ellipse in a plane normal to X.
    // Integrate its overlap with the annulus removed by extending the bore.
    public static double ExpectedRemoval(int resolution)
    {
        if (resolution < 20 || resolution > 4000) throw new ArgumentOutOfRangeException(nameof(resolution));
        double b = Math.PI / 18, h = (OldStart - Start) / resolution, dt = 6.3 / resolution, overlap = 0;
        for (int ix = 0; ix < resolution; ix++)
        {
            double x = Start + (ix + .5) * h, centre = 51 + (x - 260) * Math.Tan(b);
            for (int iz = 0; iz < resolution; iz++)
            {
                double t = -3.15 + (iz + .5) * dt;
                double half = Math.Sqrt(3.15 * 3.15 - t * t) / Math.Cos(b);
                double upper = Math.Min(centre + half, Math.Sqrt(Radius * Radius - t * t));
                double lower = Math.Max(centre - half, Math.Sqrt(OldRadius * OldRadius - t * t));
                overlap += Math.Max(0, upper - lower) * h * dt;
            }
        }
        return Math.PI * (Radius * Radius - OldRadius * OldRadius) * (OldStart - Start) - overlap;
    }

    public static double RunChecks()
    {
        double coarse = ExpectedRemoval(400), fine = ExpectedRemoval(800);
        double full = Math.PI * 195 * 22.5;
        if (Start != 181 || fine <= 0 || fine >= full || full - fine > 2 || Math.Abs(fine - coarse) > .01)
            throw new InvalidOperationException($"TIFF bore volume integration failed: {coarse}/{fine}/{full}.");
        foreach (int n in new[] { -1, 0, 19, 4001 })
        {
            try { ExpectedRemoval(n); throw new InvalidOperationException("Invalid resolution accepted."); }
            catch (ArgumentOutOfRangeException) { }
        }
        Console.WriteLine($"[TEST OK] TIFF bore X181: annulus minus II overlap, volume={fine:F9}; refinement={Math.Abs(fine - coarse):F9}.");
        // Both longitudinal bores stay outside the new D80 cylinder on X237..254.
        // Radial coordinate is monotone increasing, so test the leftmost cross-section.
        double aaClearance = 51 + (237 - 265) * Math.Tan(4 * Math.PI / 180) - 3.15 / Math.Cos(4 * Math.PI / 180) - 40;
        double iiClearance = 51 + (237 - 260) * Math.Tan(Math.PI / 18) - 3.15 / Math.Cos(Math.PI / 18) - 40;
        if (181 + 56 != 237 || Math.Abs(Bore80RemovedMm3 - 2438 * Math.PI) > 1e-8 || aaClearance <= 0 || iiClearance <= 0)
            throw new InvalidOperationException("D80 nominal chain/volume/channel clearance failed.");
        Console.WriteLine($"[TEST OK] TIFF partial D80: volume={Bore80RemovedMm3:F9}, main-channel gaps={aaClearance:F6}/{iiClearance:F6} mm. Rear D90 still uncorrected.");
        double aaRestoreGap = 51 + (254 - 265) * Math.Tan(4 * Math.PI / 180) - 4 / Math.Cos(4 * Math.PI / 180) - 45;
        double iiRestoreGap = 51 + (254 - 260) * Math.Tan(Math.PI / 18) - 4 / Math.Cos(Math.PI / 18) - 45;
        if (Math.Abs(Bore80RestoredMm3 - 4675 * Math.PI) > 1e-8 || aaRestoreGap <= 0 || iiRestoreGap <= 0)
            throw new InvalidOperationException("D80 restored-ring volume/M8 clearance failed.");
        Console.WriteLine($"[TEST OK] TIFF D80 restoration: volume={Bore80RestoredMm3:F9}, M8 envelope gaps={aaRestoreGap:F6}/{iiRestoreGap:F6} mm.");
        return fine;
    }
}
