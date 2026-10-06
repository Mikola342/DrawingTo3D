namespace CadTest.Drawing;

// User-authorized approximation; this is a tapered pilot, NOT an OST KG thread.
public static class KgPrepStudy
{
    public const double EntryRadius = 4.4, Start = -2, End = 7, TransitionEnd = 9;
    public static double Radius(double t) => t <= End ? EntryRadius - t / 32 : 4;
    // Local t points inward along the existing 7-degree feed axis; t=0 at R127.
    public static double[] Point(double t, double degrees)
    {
        double a = degrees * Math.PI / 180, r = 127 - t * Math.Cos(RearFeedTrial.Angle);
        return [RearFeedTrial.X(r), r * Math.Sin(a), -r * Math.Cos(a)];
    }

    public static double RemovedVolume(int n)
    {
        if (n < 100 || n > 1600) throw new ArgumentOutOfRangeException(nameof(n));
        double total = 0, sin = Math.Sin(RearFeedTrial.Angle), cos = Math.Cos(RearFeedTrial.Angle);
        foreach (var (lo, hi) in new[] { (Start, End), (End, TransitionEnd) })
        {
            double dt = (hi - lo) / n;
            for (int i = 0; i < n; i++)
            {
                double t = lo + (i + .5) * dt, r = Radius(t);
                for (int j = 0; j < n; j++)
                {
                    double phi = 2 * Math.PI * (j + .5) / n;
                    // Integrate radial shell analytically clipped by outer cylindrical flange.
                    // In local disk: y=127-t*cos - q*sin*cos(phi), z=q*sin(phi).
                    double yc = 127 - t * cos, b = -sin * Math.Cos(phi), z = Math.Sin(phi);
                    double aa = b * b + z * z, bb = 2 * yc * b, cc = yc * yc - 127.5 * 127.5;
                    double disc = bb * bb - 4 * aa * cc;
                    if (disc <= 0) continue;
                    double lower = Math.Max(3.4, (-bb - Math.Sqrt(disc)) / (2 * aa));
                    double upper = Math.Min(r, (-bb + Math.Sqrt(disc)) / (2 * aa));
                    if (upper > lower) total += .5 * (upper * upper - lower * lower) * dt * 2 * Math.PI / n;
                }
            }
        }
        return total;
    }

    public static void RunChecks()
    {
        double a = RemovedVolume(400), b = RemovedVolume(800);
        if (Math.Abs(a - b) > .03 || b <= 0 || b >= Math.PI * (EntryRadius * EntryRadius - 3.4 * 3.4) * 12)
            throw new InvalidOperationException("KG pilot integral bounds/convergence.");
        // Entire cutter stays in flange axial band; rear recess is below R95, far inside this cutter.
        foreach (double t in new[] { Start, End, TransitionEnd })
        {
            var p = Point(t, 0); double r = Radius(t);
            if (p[0] - r < 247 || p[0] + r > 265 || Math.Abs(p[2]) - r < 110)
                throw new InvalidOperationException("KG pilot leaves established flange band.");
        }
        for (int i = 0; i <= 1000; i++)
        {
            double t = Start + (TransitionEnd - Start) * i / 1000;
            foreach (double branch in ChannelTrialStudy.BranchAngles)
            {
                var p = Point(t, branch); double radius = Radius(t);
                // Conservative disk-containing sphere vs the infinite bolt-hole axes.
                foreach (double angle in Enumerable.Range(0, 12).Select(k => 15.0 + 30 * k).Concat(new[] { 30.0, 210.0 }))
                {
                    double boltAngle = angle * Math.PI / 180;
                    double distance = Math.Sqrt(Math.Pow(p[1] - 113 * Math.Sin(boltAngle), 2) + Math.Pow(p[2] + 113 * Math.Cos(boltAngle), 2));
                    if (distance <= radius + 7.5) throw new InvalidOperationException("KG pilot bolt envelope overlap.");
                }
                if (Math.Sqrt(Math.Pow(p[1] - 111.5, 2) + p[2] * p[2]) <= radius + 11)
                    throw new InvalidOperationException("KG pilot D22 envelope overlap.");
            }
        }
        Console.WriteLine($"[TEST OK] APPROX KG tapered pilot only: removal {a:F6}/{b:F6} mm3 per branch; no thread or seal claim.");
    }
}
