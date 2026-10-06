using System.Text.Json;

namespace CadTest.Drawing;

public static class ChannelMouthStudy
{
    public sealed record FloorSeatStation(double X, double AxisRadius, double BoreRadius,
        double MinimumEnvelopeRadius, double MaximumEnvelopeRadius, double BoreClearance);

    // Conditional envelope, not a specified thread length: X254 is the existing bore step.
    // A cylinder inclined to X intersects an X-plane in an ellipse, not a circle.
    public static FloorSeatStation EvaluateBeforeFloor(double x, double anchorX = 265)
    {
        if (!double.IsFinite(x) || x < 254 || x >= 260 ||
            !double.IsFinite(anchorX) || (anchorX != 260 && anchorX != 265))
            throw new ArgumentOutOfRangeException(nameof(x));
        var part = DrawingRevision.Create();
        var bore = part.BoreRevolveProfile.Single(e => e.StartX <= x && e.EndX > x);
        double boreRadius = bore.StartRadius + (bore.EndRadius - bore.StartRadius) *
            (x - bore.StartX) / (bore.EndX - bore.StartX);
        double axis = 51 + (x - anchorX) * Math.Tan(ChannelTrialStudy.Angle);
        double halfSpan = 4 / Math.Cos(ChannelTrialStudy.Angle);
        return new(x, axis, boreRadius, axis - halfSpan, axis + halfSpan, axis - halfSpan - boreRadius);
    }

    public sealed record Station(double X, double AxisRadius, double ToolRadius,
        double RequiredOuterMaterialRadius, double ExistingCollarRadius, double MissingRadialSupport);

    public static Station Evaluate(double x, double toolRadius)
    {
        if (!double.IsFinite(x) || !double.IsFinite(toolRadius) || x < 260 || x >= 265 || toolRadius <= 0)
            throw new ArgumentOutOfRangeException(nameof(x));
        var part = DrawingRevision.Create();
        var cut = part.AnnularCuts.Single(c => c.StartX <= x && c.EndX > x);
        double axis = ChannelTrialStudy.CentreRadius(x), collar = cut.InnerRadiusAt(x);
        return new(x, axis, toolRadius, axis + toolRadius, collar, Math.Max(0, axis + toolRadius - collar));
    }

    public static object CreateReport()
    {
        double[] stations = [260, 261, 262, 262.5, 263, 264, 264.999];
        return new {
            IsManufacturingReady = false,
            Basis = "Current trial channel axis X265/R51, current rear annulus R48.5..95; not a new drawing interpretation.",
            Channel = stations.Select(x => Evaluate(x, 3.15)).ToArray(),
            M8MajorEnvelope = stations.Select(x => Evaluate(x, 4)).ToArray(),
            BeforeFloor = new {
                IsConditional = true,
                IntervalBasis = "X254 bore step to X260 recess floor; NOT a drawing-specified thread length.",
                ExistingTrialAxis = new[] { 254.0, 257.0, 259.999999 }.Select(x => EvaluateBeforeFloor(x)).ToArray(),
                AlternativeFloorAnchor = new[] { 254.0, 257.0, 259.999999 }.Select(x => EvaluateBeforeFloor(x, 260)).ToArray(),
                FullNormalSectionSetbackAlongAxisMm = 4 * Math.Tan(ChannelTrialStudy.Angle),
                Conclusion = "The M8 major envelope clears the central bore BEFORE the floor for both candidate datums. Missing support AFTER the floor does not exclude a recessed M8 seat. Thread length, datum, cross-port intersections, tolerance and sealing remain unverified. No CAD change."
            },
            Conclusion = "Neither D6.3 nor an M8 major-diameter envelope has full surrounding material in X260..265. A cosmetic or helical M8 operation cannot provide a sealed plug seat there. Rear geometry/axis/entry datum must be resolved first.",
            Scope = "Necessary radial support test along outward generatrix, not full thread engagement, strength, seal or tolerance analysis. No CAD edits."
        };
    }
    public static void RunChecks()
    {
        for (int i = 0; i < 1000; i++)
        {
            double x = 260 + 4.999 * i / 999;
            var channel = Evaluate(x, 3.15);
            var thread = Evaluate(x, 4);
            if (channel.MissingRadialSupport <= 5 || thread.MissingRadialSupport <= channel.MissingRadialSupport ||
                thread.RequiredOuterMaterialRadius >= 95)
                throw new InvalidOperationException("Unexpected trial mouth support result.");
        }
        foreach (var (x, r) in new[] { (259.0, 4.0), (265.0, 4.0), (double.NaN, 4.0), (261.0, 0.0), (261.0, double.NaN) })
        {
            bool rejected = false;
            try { Evaluate(x, r); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid mouth study input accepted.");
        }
        foreach (double anchor in new[] { 260.0, 265.0 })
        {
            double previous = 0;
            for (int i = 0; i < 1000; i++)
            {
                var s = EvaluateBeforeFloor(254 + 5.999999 * i / 999, anchor);
                if (s.BoreRadius != 45 || s.BoreClearance <= 1.2 || s.BoreClearance < previous)
                    throw new InvalidOperationException("Conditional floor-seat clearance failed.");
                previous = s.BoreClearance;
                // Independent parametric ellipse check of radial extrema.
                for (int j = 0; j < 32; j++)
                {
                    double a = j * 2 * Math.PI / 32;
                    double r = Math.Sqrt(Math.Pow(s.AxisRadius + 4 * Math.Cos(a) / Math.Cos(ChannelTrialStudy.Angle), 2)
                        + Math.Pow(4 * Math.Sin(a), 2));
                    if (r < s.MinimumEnvelopeRadius - 1e-10 || r > s.MaximumEnvelopeRadius + 1e-10)
                        throw new InvalidOperationException("Inclined M8 envelope bound failed.");
                }
            }
        }
        foreach (var (x, anchor) in new[] { (253.0, 265.0), (260.0, 265.0), (double.NaN, 265.0), (255.0, 261.0), (255.0, double.NaN) })
        {
            bool rejected = false;
            try { EvaluateBeforeFloor(x, anchor); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid floor-seat input accepted.");
        }
        Console.WriteLine("[TEST OK] Conditional M8 seat BEFORE floor: 2 datums, 2000 stations, 64000 ellipse samples, 5 invalid inputs. Not a thread or seal acceptance.");
        Console.WriteLine("[TEST OK] Channel mouth: 1000 rear stations and 5 invalid inputs; full M8 support absent only in tested X260..265 interval.");
    }
    public static void WriteReport(string path)
    {
        RunChecks();
        File.WriteAllText(path, JsonSerializer.Serialize(CreateReport(), new JsonSerializerOptions { WriteIndented = true }));
    }
}
