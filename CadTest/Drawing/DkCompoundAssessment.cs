namespace CadTest.Drawing;

// Conditional hypotheses only: no dimension here authorizes a CAD operation.
public static class DkCompoundAssessment
{
    public sealed record Candidate(double Depth, double FlatRadius, double BottomX,
        double BlendCentreX, double OpeningPlaneX, double FloorIntersectionRadius);
    public static Candidate Evaluate(double depth, double openingPlaneX)
    {
        const double radius = 16, opening = 16.5;
        if (!double.IsFinite(depth) || depth <= 0 || depth >= radius || !double.IsFinite(openingPlaneX))
            throw new ArgumentOutOfRangeException(nameof(depth));
        double b = opening - Math.Sqrt(2 * radius * depth - depth * depth);
        double bottom = openingPlaneX - depth;
        double delta = DrawingRevision.RearRecessStart - bottom;
        double intersection = delta >= 0 && delta <= radius
            ? b + Math.Sqrt(2 * radius * delta - delta * delta) : double.NaN;
        return new(depth, b, bottom, bottom + radius, openingPlaneX, intersection);
    }
    public static double SurfaceX(Candidate c, double localRadius)
    {
        if (!double.IsFinite(localRadius) || localRadius < 0 || localRadius > 16.5)
            throw new ArgumentOutOfRangeException(nameof(localRadius));
        return localRadius <= c.FlatRadius ? c.BottomX :
            c.BlendCentreX - Math.Sqrt(256 - Math.Pow(localRadius - c.FlatRadius, 2));
    }
    public static void RunChecks()
    {
        for (int i = 0; i <= 1000; i++)
        {
            var c = Evaluate(13.15 + .7 * i / 1000, DrawingRevision.BossEnd);
            if (c.FlatRadius <= 0 || Math.Abs(SurfaceX(c, 16.5) - c.OpeningPlaneX) > 1e-10 ||
                Math.Abs(SurfaceX(c, c.FlatRadius) - c.BottomX) > 1e-10 ||
                Math.Abs(SurfaceX(c, c.FloorIntersectionRadius) - DrawingRevision.RearRecessStart) > 1e-10)
                throw new InvalidOperationException("Compound DK candidate equations failed.");
        }
        double centre = DrawingRevision.LocalHoleOffset - DkRecessAssessment.CentreOffset;
        if (!(centre - 16.5 > DrawingRevision.CollarRadius && centre + 16.5 < DrawingRevision.BossInnerRadius))
            throw new InvalidOperationException("DK opening containment must be reassessed for this base geometry.");
        foreach (double depth in new[] { 0.0, -1, 16, double.NaN, double.PositiveInfinity })
        {
            bool rejected = false;
            try { Evaluate(depth, 273); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid DK candidate depth accepted.");
        }
        Console.WriteLine("[TEST OK] DK compound hypothesis: 1001 depth cases, join/opening/floor equations, projected containment, 5 invalid inputs.");
    }
    public static object CreateReport()
    {
        var c = Evaluate(13.5, DrawingRevision.BossEnd);
        return new {
            Status = "Conditional flat-bottom/toroidal blend hypothesis; NOT recovered drawing geometry or a CAD cut",
            Assumptions = new[] { "Opening plane assumed X273", "Depth assumed 13.5", "Local centre main-axis offset assumed 74", "Flat disk plus meridional R16 blend assumed" },
            Nominal = c,
            OpeningRadialSpan = new[] { 74 - 16.5, 74 + 16.5 },
            RearVoidRadialSpan = new[] { DrawingRevision.CollarRadius, DrawingRevision.BossInnerRadius },
            OpeningEntirelyInsideExistingVoid = true,
            ApparentOpeningAtRearFloorDiameter = 2 * c.FloorIntersectionRadius,
            HoleLocalRadialSpan = new[] { 10.0 - 6, 10.0 + 6 },
            Conclusion = "Matching R16 and D33 in an isolated mathematical bowl does not produce a D33 rim in this base model: the assumed rim is in empty space. Recheck projection/datum/centre and rear cavity interpretation before any cut.",
            Scope = "Incompatibility of combined assumptions, NOT proof that drawing is wrong or that base cavity must be changed. No additional user approval inferred."
        };
    }
}
