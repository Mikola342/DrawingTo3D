namespace CadTest.Drawing;

public static class TiffRearStudy
{
    public const double CollarRadius = 57.5;
    public const double BoltCircleRadius = 47.5;
    public const double MouthX = 265; // nominal zero inset within1.6max, not recess floor260
    public static double RecessEnvelopeVolume => 5 * Math.PI * (95 * 95 - CollarRadius * CollarRadius);
    // Existing DK cutter has a D33 cylindrical continuation atY=-74 throughX260..273.
    // Its disk is tangent to the D115 collar and wholly insideR95, so its overlap is exact.
    public static double ExistingDkOverlapVolume => 5 * Math.PI * 16.5 * 16.5;
    public static double RecessVolume => RecessEnvelopeVolume - ExistingDkOverlapVolume;
    public static void RunChecks()
    {
        double outerM6Wall = CollarRadius - BoltCircleRadius - 3;
        double innerM6Wall = BoltCircleRadius - 3 - 40;
        double outerM8Wall = CollarRadius - 51 - 4;
        if (outerM6Wall != 7 || innerM6Wall != 4.5 || outerM8Wall != 2.5 ||
            48.5 - BoltCircleRadius - 3 >= 0 || 74 - 16.5 != CollarRadius || 74 + 16.5 >= 95 ||
            !double.IsFinite(RecessVolume) || RecessVolume <= 0)
            throw new InvalidOperationException("TIFF rear collar/pattern calculation failed.");
        foreach (double angle in new[] { 4.0, 10.0 })
        {
            double tm = Math.Tan(angle * Math.PI / 180), tf = Math.Tan(7 * Math.PI / 180);
            double r = (51 + (257.5 - 127 * tf - MouthX) * tm) / (1 - tf * tm);
            double x = 257.5 + (r - 127) * tf;
            if (x <= 237 || x >= 259 || Math.Abs(r - (51 + (x - MouthX) * tm)) > 1e-10)
                throw new InvalidOperationException("Rear channel crossing calculation failed.");
            double b = angle * Math.PI / 180, length = 6 / Math.Cos(b);
            for (int i = 0; i <= 720; i++)
            {
                double qx = 4 * Math.Sin(b) * Math.Cos(i * Math.PI / 360);
                double s = (6 - qx) / Math.Cos(b);
                if (Math.Abs(259 + s * Math.Cos(b) + qx - MouthX) > 1e-10 ||
                    s > length + 4 * tm + 1e-10 || s < length - 4 * tm - 1e-10)
                    throw new InvalidOperationException("Oblique M8 / planar mouth bound failed.");
            }
            Console.WriteLine($"[TEST OK] Rear {angle}deg atX265: feed crossing X={x:F9}/R={r:F9}.");
        }
        Console.WriteLine($"[TEST OK] Rear D115/PCD95: nominal M6 walls={innerM6Wall}/{outerM6Wall}, M8 wall={outerM8Wall}; recessV={RecessVolume:F9}.");
    }
}
