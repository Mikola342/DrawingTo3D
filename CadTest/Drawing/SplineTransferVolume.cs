using CadTest.Models;

namespace CadTest.Drawing;

public static class SplineTransferVolume
{
    // Independent integration over axial X and transverse cutter coordinate t.
    // Disk centre is X70/r97. Nominal angular phase: space centre along sketch +X.
    public static double RemovedVolume(int samples = 600)
    {
        if (samples < 20) throw new ArgumentOutOfRangeException(nameof(samples));
        SplineToolBounds.RunChecks();
        var boundary = SplineTrialProfile.SpaceSegments(2000).Take(5)
            .SelectMany(s => s.Points).GroupBy(p => Math.Round(p.Y, 12)).Select(g => g.First()).OrderBy(p => p.Y).ToArray();
        double low = boundary[0].Y, high = boundary[^1].Y, dt = (high - low) / samples;
        var floors = new double[samples];
        int index = 0;
        for (int j = 0; j < samples; j++)
        {
            double t = low + (j + .5) * dt;
            while (index + 1 < boundary.Length - 1 && boundary[index + 1].Y < t) index++;
            var a = boundary[index]; var b = boundary[index + 1];
            floors[j] = a.X + (b.X - a.X) * (t - a.Y) / (b.Y - a.Y);
        }
        double sum = 0;
        var left = new LeftEndDescription();
        double[] breaks = [42, DrawingRevision.SplineLeadEnd, 60, 70, 89];
        for (int block = 0; block + 1 < breaks.Length; block++)
        {
            double dx = (breaks[block + 1] - breaks[block]) / samples;
            for (int i = 0; i < samples; i++)
            {
                double x = breaks[block] + (i + .5) * dx;
                double outer = x < DrawingRevision.SplineLeadEnd ? 45 + (x - 42) / Math.Tan(Math.PI / 12) : 50;
                double h = x < 60 ? left.TopBoundary(x) : double.PositiveInfinity;
                for (int j = 0; j < samples; j++)
                {
                    double t = low + (j + .5) * dt;
                    double bottom = floors[j];
                    if (x > 70) bottom = 97 - Math.Sqrt(Math.Pow(97 - bottom, 2) - Math.Pow(x - 70, 2));
                    double top = Math.Sqrt(outer * outer - t * t);
                    if (bottom >= top) continue;
                    for (int k = 0; k < 38; k++)
                    {
                        double angle = k * 2 * Math.PI / 38;
                        double sine = Math.Sin(angle), cosine = Math.Cos(angle);
                        double from = bottom, to = top;
                        if (double.IsFinite(h))
                        {
                            if (sine > 1e-10) to = Math.Min(to, (h - t * cosine) / sine);
                            else if (sine < -1e-10) from = Math.Max(from, (h - t * cosine) / sine);
                            else if (t * cosine > h) continue;
                        }
                        sum += Math.Max(0, to - from) * dt * dx;
                    }
                }
            }
        }
        if (!double.IsFinite(sum) || sum <= 0) throw new InvalidOperationException("Invalid integrated spline removal.");
        return sum;
    }
}
