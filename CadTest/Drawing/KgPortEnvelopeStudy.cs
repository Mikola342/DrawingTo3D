using System.Text.Json;

namespace CadTest.Drawing;

// Placement study only. A supplied envelope is not an OST thread profile.
public static class KgPortEnvelopeStudy
{
    public sealed record Result(double EntryDiameterMm, double LengthMm, double OffsetMm,
        double EntryRadiusMm, double FrontClearanceMm, double RearClearanceMm,
        double MinimumRadiusMm, double MaximumRadiusMm);

    public static Result Evaluate(double diameter, double length, double offset, double entryRadius)
    {
        if (!double.IsFinite(diameter) || diameter <= 0 || diameter > 20 ||
            !double.IsFinite(length) || length <= 0 || length > 20 ||
            !double.IsFinite(offset) || offset < 7.05 || offset > 7.95 ||
            !double.IsFinite(entryRadius) || entryRadius < 127 || entryRadius > 127.5)
            throw new ArgumentOutOfRangeException(nameof(diameter));
        const double diameterTaper = 1.0 / 16;
        if (diameter - length * diameterTaper <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        double s = Math.Sin(RearFeedTrial.Angle), c = Math.Cos(RearFeedTrial.Angle);
        double x0 = 265 - offset, r0 = diameter / 2, r1 = (diameter - length * diameterTaper) / 2;
        double minX = Math.Min(x0 - r0 * c, x0 - length * s - r1 * c);
        double maxX = Math.Max(x0 + r0 * c, x0 - length * s + r1 * c);
        double minR = double.PositiveInfinity, maxR = 0;
        // Independent 3D surface samples check the analytic axial envelope.
        for (int i = 0; i <= 40; i++)
        {
            double t = length * i / 40, radius = (diameter - t * diameterTaper) / 2;
            for (int j = 0; j < 360; j++)
            {
                double phi = j * Math.PI / 180;
                double x = x0 - t * s + radius * c * Math.Cos(phi);
                double y = entryRadius - t * c - radius * s * Math.Cos(phi);
                double z = radius * Math.Sin(phi);
                if (x < minX - 1e-10 || x > maxX + 1e-10)
                    throw new InvalidOperationException("KG candidate axial envelope mismatch.");
                double r = Math.Sqrt(y * y + z * z);
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
            }
        }
        return new(diameter, length, offset, entryRadius, minX - 247, 265 - maxX, minR, maxR);
    }

    public static void WriteReport(string path)
    {
        // Deliberately a sensitivity range, not selected thread/drill dimensions.
        var candidates = (from diameter in new[] { 9.5, 10.0, 10.5, 11.0 }
                          from length in new[] { 5.0, 7.0, 9.0 }
                          from offset in new[] { 7.05, 7.5, 7.95 }
                          from entryRadius in new[] { 127.0, 127.5 }
                          select Evaluate(diameter, length, offset, entryRadius)).ToArray();
        var nominal = Evaluate(10, 7, 7.5, 127);
        if (nominal.FrontClearanceMm <= 0 || nominal.RearClearanceMm <= 0)
            throw new InvalidOperationException("Unexpected flange breakthrough for test envelope.");
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, -1.0, 0.0, 21.0 })
        {
            bool rejected = false;
            try { Evaluate(bad, 7, 7.5, 127); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid KG diameter accepted.");
        }
        foreach (var input in new[] {
            (10.0, double.NaN, 7.5, 127.0), (10.0, 0.0, 7.5, 127.0),
            (10.0, 21.0, 7.5, 127.0), (.1, 7.0, 7.5, 127.0),
            (10.0, 7.0, double.NaN, 127.0), (10.0, 7.0, 7.0, 127.0),
            (10.0, 7.0, 8.0, 127.0), (10.0, 7.0, 7.5, double.NaN),
            (10.0, 7.0, 7.5, 126.9), (10.0, 7.0, 7.5, 127.6) })
        {
            bool rejected = false;
            try { Evaluate(input.Item1, input.Item2, input.Item3, input.Item4); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid KG placement accepted.");
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new {
            IsManufacturingReady = false, CadChanged = false, ThreadProfileSelected = false,
            DrawingCallout = "2 holes KG1/8, OST 37.001.311-83",
            EnvelopeOnly = "Conditional 1:16 taper, 7-degree axis. Diameters 9.5..11 and lengths 5/7/9 are sensitivity inputs, NOT accepted OST or drawing dimensions.",
            Candidates = candidates,
            MinimumFrontClearanceMm = candidates.Min(r => r.FrontClearanceMm),
            MinimumRearClearanceMm = candidates.Min(r => r.RearClearanceMm),
            Limitations = "Clearance to flange planes X247/265 only. No bolt-hole clearance, complete thread profile, datum acceptance, drill tip, chamfer, pressure/seal or tolerance acceptance. Original OST profile and depth/datum remain unresolved."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[TEST OK] KG placement sensitivity: {candidates.Length} candidates, 3D axial-envelope checks, 15 invalid inputs. No thread selected or CAD changed.");
    }
}
