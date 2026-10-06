namespace CadTest.Drawing;

// Sampling diagnostic, not a thread-profile or standard-conformance acceptance.
public sealed record HelicalEdgeFit(double SignedPitchMm, double MaximumAxialFitResidualMm)
{
    public static HelicalEdgeFit Measure(IReadOnlyList<(double T, double Phase)> wrapped)
    {
        if (wrapped.Count < 3 || wrapped.Any(p => !double.IsFinite(p.T) || !double.IsFinite(p.Phase)))
            throw new ArgumentException("Helix fit requires finite samples.");
        var trace = new List<(double T, double Phase)>();
        double phase = wrapped[0].Phase;
        for (int i = 0; i < wrapped.Count; i++)
        {
            // Caller must sample densely enough that adjacent phase changes are below pi.
            if (i > 0)
            {
                double delta = wrapped[i].Phase - wrapped[i - 1].Phase;
                phase += Math.Atan2(Math.Sin(delta), Math.Cos(delta));
            }
            trace.Add((wrapped[i].T, phase));
        }
        double meanT = trace.Average(p => p.T), meanP = trace.Average(p => p.Phase);
        double variance = trace.Sum(p => Math.Pow(p.T - meanT, 2));
        if (variance < 1e-20) throw new ArgumentException("Helix has no axial span.");
        double slope = trace.Sum(p => (p.T - meanT) * (p.Phase - meanP)) / variance;
        if (!double.IsFinite(slope) || Math.Abs(slope) < 1e-12)
            throw new ArgumentException("Helix has no measurable rotation.");
        return new(2 * Math.PI / slope,
            trace.Max(p => Math.Abs(p.Phase - meanP - slope * (p.T - meanT))) / Math.Abs(slope));
    }

    public static void RunChecks()
    {
        foreach (double pitch in new[] { 25.4 / 27, -25.4 / 27, 1.25, -1.25 })
        foreach (bool reverse in new[] { false, true })
        {
            var points = Enumerable.Range(0, 257).Select(i =>
            {
                double t = 2 + 4.0 * i / 256, p = 1.7 + 2 * Math.PI * t / pitch;
                return (T: t, Phase: Math.Atan2(Math.Sin(p), Math.Cos(p)));
            }).ToArray();
            if (reverse) Array.Reverse(points);
            var fit = Measure(points);
            if (Math.Abs(fit.SignedPitchMm - pitch) > 1e-12 || fit.MaximumAxialFitResidualMm > 1e-12)
                throw new InvalidOperationException("Helix pitch/edge-direction regression failed.");
        }
        foreach (var invalid in new[]
        {
            new[] { (0.0, 0.0), (0.0, 1.0), (0.0, 2.0) },
            new[] { (0.0, 0.0), (1.0, 0.0), (2.0, 0.0) },
            new[] { (0.0, 0.0), (1.0, double.NaN), (2.0, 2.0) }
        })
        {
            bool rejected = false;
            try { Measure(invalid); } catch (ArgumentException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid helix fit accepted.");
        }
        Console.WriteLine("[PASS] Helical edge fit: both signs, reversed edges, degenerate/nonfinite inputs.");
    }
}
