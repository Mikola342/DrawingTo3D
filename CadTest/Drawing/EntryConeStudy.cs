namespace CadTest.Drawing;

public static class EntryConeStudy
{
    // One cone, clipped by D105 and excluding the pre-existing D6 bore.
    public static double RemovedVolume(int samples)
    {
        if (samples < 32 || samples > 2000) throw new ArgumentOutOfRangeException(nameof(samples));
        double volume = 0;
        for (int i = 0; i < samples; i++)
        {
            double rho = 3 + (i + .5) / samples;
            for (int j = 0; j < samples; j++)
            {
                double phi = 2 * Math.PI * (j + .5) / samples;
                double outer = Math.Sqrt(52.5 * 52.5 - Math.Pow(rho * Math.Sin(phi), 2));
                volume += rho * Math.Max(0, outer - 48.5 - rho) * 2 * Math.PI / (samples * samples);
            }
        }
        return volume;
    }

    public static void RunChecks()
    {
        double coarse = RemovedVolume(400), fine = RemovedVolume(800);
        double planar = Math.PI * (3 + 1.0 / 3);
        if (fine <= 9 || fine >= planar || Math.Abs(coarse - fine) > .001)
            throw new InvalidOperationException("Curved entry integral bounds/convergence failed.");
        foreach (int n in new[] { -1, 0, 31, 2001, int.MaxValue })
        {
            bool rejected = false;
            try { RemovedVolume(n); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid entry integral input accepted.");
        }
        Console.WriteLine($"[TEST OK] Entry cone: curved volume {fine:F9} below planar {planar:F9}; convergence and 5 invalid inputs.");
    }
}
