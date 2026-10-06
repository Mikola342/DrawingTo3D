namespace CadTest.Drawing;

/// <summary>Conditional study only. No station is promoted to a drawing dimension.</summary>
public static class RightTransitionStudy
{
    private static readonly double Q = 2 - Math.Sqrt(2);
    private static readonly double Rise = 2 * (Math.Sqrt(2) - 1);
    public static double MinimumStation => 241 + Q;
    public static double MaximumStation => 247 - Q;

    public static double CircleOverlap(double a, double b, double distance)
    {
        if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(distance) || a <= 0 || b <= 0 || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (distance >= a + b) return 0;
        if (distance <= Math.Abs(a - b)) return Math.PI * Math.Pow(Math.Min(a, b), 2);
        double c1 = Math.Clamp((distance * distance + a * a - b * b) / (2 * distance * a), -1, 1);
        double c2 = Math.Clamp((distance * distance + b * b - a * a) / (2 * distance * b), -1, 1);
        double product = (-distance + a + b) * (distance + a - b) * (distance - a + b) * (distance + a + b);
        return a * a * Math.Acos(c1) + b * b * Math.Acos(c2) - 0.5 * Math.Sqrt(Math.Max(0, product));
    }

    public sealed record Segment(string Kind, double X0, double R0, double X1, double R1,
        double? CentreX = null, double? CentreR = null, double? FilletRadius = null);
    public sealed record Candidate(double UnconfirmedStationMm, Segment[] Profile,
        double OuterVolumeMm3, double LocalHoleIntersectionVolumeMm3, double VolumeDeltaFromStage10Mm3,
        double IntegrationRefinementDifferenceMm3);

    public static Segment[] Profile(double station)
    {
        if (!double.IsFinite(station) || station <= MinimumStation || station >= MaximumStation)
            throw new ArgumentOutOfRangeException(nameof(station), "Station leaves no straight portion for one of the assumed 45-degree transitions.");
        return
        [
            new("radial", 241, DrawingRevision.RightStepArcCentreRadius, 241, 82 - (station - 241)),
            new("line", 241, 82 - (station - 241), station - Q, 82 - Q),
            new("arc", station - Q, 82 - Q, station, 82 + Rise, station - 2, 82 + Rise, 2),
            new("radial", station, 82 + Rise, station, 94 - (247 - station)),
            new("line", station, 94 - (247 - station), 247 - Q, 94 - Q),
            new("arc", 247 - Q, 94 - Q, 247, 94 + Rise, 245, 94 + Rise, 2),
            new("radial", 247, 94 + Rise, 247, 127.5)
        ];
    }

    private static (double Outer, double Hole) Integrate(Segment[] profile, int samples)
    {
        double outer = 0, hole = 0;
        foreach (var e in profile.Where(e => e.X1 > e.X0))
        {
            double dx = (e.X1 - e.X0) / samples;
            for (int i = 0; i < samples; i++)
            {
                double x = e.X0 + (i + 0.5) * dx;
                double r = e.Kind == "arc"
                    ? e.CentreR!.Value - Math.Sqrt(Math.Max(0, 4 - Math.Pow(x - e.CentreX!.Value, 2)))
                    : e.R0 + (e.R1 - e.R0) * (x - e.X0) / (e.X1 - e.X0);
                outer += Math.PI * r * r * dx;
                hole += CircleOverlap(r, 6, 84) * dx;
            }
        }
        return (outer, hole);
    }

    public static Candidate Evaluate(double station)
    {
        var profile = Profile(station);
        var fine = Integrate(profile, 40000);
        var coarse = Integrate(profile, 20000);
        double delta = fine.Outer - 6 * Math.PI * 10000 + 6 * Math.PI * 36 - fine.Hole;
        return new(station, profile, fine.Outer, fine.Hole, delta,
            Math.Abs((fine.Outer - fine.Hole) - (coarse.Outer - coarse.Hole)));
    }

    public static object CreateReport() => new
    {
        Status = "Conditional candidates, NOT CAD revisions or confirmed drawing geometry",
        Source = "chertezh_corrected_final.pdf, section A-A; stage10 supplies the fixed left boundary",
        Assumptions = new[]
        {
            "Ø164 and Ø188 refer to theoretical corners at radii 82 and 94.",
            "The two remaining oblique transitions are 45 degrees, root radii selected as R2.",
            "Intermediate axial station is free; 244,245,246 are examples, NOT measured dimensions.",
            "Д/К absent. Bore and rear recess remain unchanged. Circle overlap concerns only local Ø12."
        },
        OpenStationIntervalMm = new[] { MinimumStation, MaximumStation },
        IntervalMeaning = "Geometric feasibility for this assumed topology only; NOT a drawing tolerance",
        Candidates = new[] { Evaluate(244), Evaluate(245), Evaluate(246) },
        RequiredDecision = "User approved X=245 TEMPORARILY for stage11; this does not confirm it as a drawing dimension. Other stations remain study variants."
    };

    public static void RunChecks()
    {
        static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Right transition study regression."); }
        Check(CircleOverlap(78, 6, 84) == 0);
        Check(Math.Abs(CircleOverlap(90, 6, 84) - Math.PI * 36) < 1e-10);
        Check(Math.Abs(CircleOverlap(1, 1, 1) - (2 * Math.PI / 3 - Math.Sqrt(3) / 2)) < 1e-12);
        Check(Math.Abs(CircleOverlap(84, 6, 84) - CircleOverlap(6, 84, 84)) < 1e-10);
        foreach (double station in new[] { 244.0, 245, 246 })
        {
            var c = Evaluate(station);
            Check(c.IntegrationRefinementDifferenceMm3 < 0.01);
            Check(c.LocalHoleIntersectionVolumeMm3 > 0 && c.LocalHoleIntersectionVolumeMm3 < 6 * Math.PI * 36);
            for (int i = 1; i < c.Profile.Length; i++)
                Check(Math.Abs(c.Profile[i - 1].X1 - c.Profile[i].X0) < 1e-10 &&
                    Math.Abs(c.Profile[i - 1].R1 - c.Profile[i].R0) < 1e-10);
            foreach (var e in c.Profile.Where(e => e.Kind == "arc"))
            {
                Check(Math.Abs(Math.Pow(e.X0 - e.CentreX!.Value, 2) + Math.Pow(e.R0 - e.CentreR!.Value, 2) - 4) < 1e-10);
                Check(Math.Abs(e.R1 - e.CentreR!.Value) < 1e-10 && Math.Abs(e.X1 - e.CentreX!.Value - 2) < 1e-10);
            }
        }
        foreach (double invalid in new[] { MinimumStation, MaximumStation, 241, 247, double.NaN })
        {
            bool rejected = false;
            try { Profile(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected);
        }
        Console.WriteLine("[TEST OK] Right transition candidates: continuity, arcs, circle overlap, convergence and station bounds.");
    }
}
