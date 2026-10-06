namespace CadTest.Drawing;

public static class M8PilotStudy
{
    public static double Radius => (8 - 5 * Math.Sqrt(3) / 8 * 1.25) / 2;
    public const double StartX = 254, EndX = 260;
    public static void RunChecks()
    {
        double cartesian = RemovedVolume(800), shell = ShellVolume(800), coarseShell = ShellVolume(400);
        double envelope = Math.PI * (Radius * Radius - 3.15 * 3.15) * 6 / Math.Cos(ChannelTrialStudy.Angle);
        if (Math.Abs(cartesian - shell) > .002 || Math.Abs(shell - coarseShell) > .001 || shell <= 0 || shell >= envelope)
            throw new InvalidOperationException("M8 pilot independent integrals disagree.");
        foreach (int n in new[] { 0, 39, 2001, int.MaxValue })
        {
            int rejected = 0;
            try { RemovedVolume(n); } catch (ArgumentOutOfRangeException) { rejected++; }
            try { ShellVolume(n); } catch (ArgumentOutOfRangeException) { rejected++; }
            if (rejected != 2) throw new InvalidOperationException("Invalid pilot mesh accepted.");
        }
        Console.WriteLine($"[TEST OK] M8 pilot mathematics only: Cartesian={cartesian:F9}, shell={shell:F9}; CAD needs separate geometry and removed-body checks.");
    }
    // Independent integration in cutter coordinates: only the added annular shell.
    public static double ShellVolume(int n)
    {
        if (n < 40 || n > 2000) throw new ArgumentOutOfRangeException(nameof(n));
        double sin = Math.Sin(ChannelTrialStudy.Angle), cos = Math.Cos(ChannelTrialStudy.Angle);
        double length = 6 / cos, startR = ChannelTrialStudy.CentreRadius(254), total = 0;
        for (int i = 0; i < n; i++)
        {
            double rho = 3.15 + (Radius - 3.15) * (i + .5) / n;
            for (int j = 0; j < n; j++)
            {
                double phi = 2 * Math.PI * (j + .5) / n, q = rho * Math.Cos(phi), t = rho * Math.Sin(phi);
                double sFloor = (6 + q * sin) / cos;
                double sCollar = (Math.Sqrt(48.5 * 48.5 - t * t) - startR - q * cos) / sin;
                double retained = Math.Min(length, Math.Max(0, Math.Max(sFloor, sCollar)));
                total += retained * rho * (Radius - 3.15) / n * 2 * Math.PI / n;
            }
        }
        return total;
    }
    public static double RemovedVolume(int n)
    {
        if (n < 40 || n > 2000) throw new ArgumentOutOfRangeException(nameof(n));
        var part = DrawingRevision.Create();
        double sine = Math.Sin(ChannelTrialStudy.Angle), cosine = Math.Cos(ChannelTrialStudy.Angle);
        double length = (EndX - StartX) / cosine, rearR = ChannelTrialStudy.CentreRadius(EndX);
        double[] breaks = [StartX - Radius * sine, StartX, StartX + Radius * sine, EndX - Radius * sine, EndX, EndX + Radius * sine];
        double total = 0;
        for (int k = 0; k + 1 < breaks.Length; k++)
        {
            double dx = (breaks[k + 1] - breaks[k]) / n;
            for (int i = 0; i < n; i++)
            {
                double x = breaks[k] + (i + .5) * dx, centre = ChannelTrialStudy.CentreRadius(x);
                var cut = part.AnnularCuts.FirstOrDefault(c => c.StartX <= x && c.EndX > x);
                double bore = ChannelTrialStudy.BoreRadius(part, x), outer = ChannelTrialStudy.OuterRadius(part, x);
                double transition = Math.Asin(3.15 / Radius);
                double[] phiBreaks = [-Math.PI / 2, -transition, transition, Math.PI / 2];
                for (int b = 0; b < 3; b++)
                for (int j = 0; j < n; j++)
                {
                    double dphi = (phiBreaks[b + 1] - phiBreaks[b]) / n;
                    double phi = phiBreaks[b] + (j + .5) * dphi;
                    double t = Radius * Math.Sin(phi), span = Radius * Math.Cos(phi) / cosine;
                    double lo = Math.Max(centre - span, rearR + (-length - (x - EndX) * cosine) / sine);
                    double hi = Math.Min(centre + span, rearR - (x - EndX) * cosine / sine);
                    double Material(double l, double h)
                    {
                        double Band(double a, double b) => b * b <= t * t ? 0 : Math.Max(0,
                            Math.Min(h, Math.Sqrt(b * b - t * t)) - Math.Max(l, Math.Sqrt(Math.Max(0, a * a - t * t))));
                        return h <= l ? 0 : Band(bore, cut?.InnerRadiusAt(x) ?? outer) + (cut == null ? 0 : Band(cut.OuterRadius, outer));
                    }
                    double amount = Material(lo, hi);
                    if (Math.Abs(t) < 3.15)
                    {
                        double oldSpan = Math.Sqrt(3.15 * 3.15 - t * t) / cosine;
                        amount -= Material(Math.Max(lo, centre - oldSpan), Math.Min(hi, centre + oldSpan));
                    }
                    total += amount * dx * Radius * Math.Cos(phi) * dphi;
                }
            }
        }
        return total;
    }
}
