using System.Text.Json;

namespace CadTest.Drawing;

// Conditional coplanar axes. No thread length or drilling depth is selected here.
public static class RearPortStudy
{
    public sealed record Result(double Offset, double EntryRadius, double MainAnchorX,
        double IntersectionX, double IntersectionRadius, double DistanceFromFloorAlongMainAxis,
        double CrossPortEnvelopeReachAlongMainAxis, double ClearEnvelopeLengthBeforeCrossPort,
        double FullSectionEntrySetback, double UsableEnvelopeInterval);

    public static Result Evaluate(double offset, double entryRadius, double mainAnchorX)
    {
        if (!double.IsFinite(offset) || offset < 7.05 || offset > 7.95 ||
            !double.IsFinite(entryRadius) || entryRadius < 127 || entryRadius > 127.5 ||
            (mainAnchorX != 260 && mainAnchorX != 265))
            throw new ArgumentOutOfRangeException(nameof(offset));
        double a = ChannelTrialStudy.Angle, b = 7 * Math.PI / 180;
        double m = Math.Tan(a), n = Math.Tan(b);
        // r=51+(x-anchor)*tan4; x=265-offset+(r-entryRadius)*tan7.
        double x = (265 - offset + (51 - mainAnchorX * m - entryRadius) * n) / (1 - m * n);
        double r = 51 + (x - mainAnchorX) * m;
        double distance = (260 - x) / Math.Cos(a);
        double dot = Math.Sin(a + b), cross = Math.Cos(a + b);
        // Maximum main-axis projection of the intersection of infinite cylinders
        // of radii 4 (M8 envelope) and 3.4 (side port), for intersecting axes.
        double reach = (3.4 + 4 * dot) / cross;
        double setback = 4 * Math.Tan(a);
        return new(offset, entryRadius, mainAnchorX, x, r, distance, reach,
            distance - reach, setback, distance - reach - setback);
    }

    public static void WriteReport(string path)
    {
        var results = (from anchor in new[] { 260.0, 265.0 }
                       from radius in new[] { 127.0, 127.5 }
                       from offset in new[] { 7.05, 7.5, 7.95 }
                       select Evaluate(offset, radius, anchor)).ToArray();
        foreach (var s in results)
        {
            double a = ChannelTrialStudy.Angle, b = 7 * Math.PI / 180;
            double mainResidual = s.IntersectionRadius - 51 - (s.IntersectionX - s.MainAnchorX) * Math.Tan(a);
            double portResidual = s.IntersectionX - (265 - s.Offset) - (s.IntersectionRadius - s.EntryRadius) * Math.Tan(b);
            if (Math.Abs(mainResidual) > 1e-10 || Math.Abs(portResidual) > 1e-10 || s.UsableEnvelopeInterval <= 0)
                throw new InvalidOperationException("Rear-port intersection check failed.");
            // Independently sample the normal disk of the M8 envelope. At the
            // calculated tangency limit its nearest point is exactly radius 3.4
            // from the side-port axis; moving 0.01 toward the floor clears it.
            foreach (double shift in new[] { 0.0, 0.01 })
            {
                double t = s.CrossPortEnvelopeReachAlongMainAxis + shift;
                double minDistance = double.PositiveInfinity;
                for (int j = 0; j < 360; j++)
                {
                    double phi = j * 2 * Math.PI / 360;
                    double dx = t * Math.Cos(a) - 4 * Math.Cos(phi) * Math.Sin(a);
                    double dr = t * Math.Sin(a) + 4 * Math.Cos(phi) * Math.Cos(a);
                    double z = 4 * Math.Sin(phi);
                    double sideNormal = dx * Math.Cos(b) - dr * Math.Sin(b);
                    minDistance = Math.Min(minDistance, Math.Sqrt(sideNormal * sideNormal + z * z));
                }
                double expected = 3.4 + shift * Math.Cos(a + b);
                if (Math.Abs(minDistance - expected) > 1e-9)
                    throw new InvalidOperationException("Rear-port envelope tangency check failed.");
            }
        }
        foreach (var (o, r, a) in new[] { (7.0, 127.0, 265.0), (8.0, 127.0, 265.0),
            (double.NaN, 127.0, 265.0), (7.5, double.NaN, 265.0), (7.5, 127.0, double.NaN),
            (7.5, 126.0, 265.0), (7.5, 127.0, 264.0) })
        {
            bool rejected = false;
            try { Evaluate(o, r, a); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid rear-port candidate accepted.");
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new {
            IsManufacturingReady = false, IsConditional = true,
            Basis = "A-A: D6.8, 7 degrees, 7.5 +/-0.45. Candidate entry at X265-offset and R127..127.5; coplanar with trial main axis. Entry surface and angular registration are NOT accepted drawing datums.",
            Candidates = results,
            MinimumUsableEnvelopeInterval = results.Min(s => s.UsableEnvelopeInterval),
            Limitations = "Infinite-cylinder exclusion bound, not a finite drill or thread model. No thread length selected; no KG1/8, drill tip, chamfer, runout, strength, tolerance-stack or seal acceptance. CAD unchanged."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[TEST OK] Rear port: {results.Length} conditional candidates, 17280 tangency samples, 7 invalid inputs; minimum envelope interval {results.Min(s => s.UsableEnvelopeInterval):F6} mm (NOT thread length).");
    }
}
