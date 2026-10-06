using System.Text.Json;

namespace CadTest.Drawing;

// Circular candidates demonstrate non-uniqueness; not a generated tool envelope.
public static class SplineRootStudy
{
    public const double MaxTransitionDiameter = SplineSectionStudy.MajorDiameter - 2 * SplineSectionStudy.Module;
    public sealed record Candidate(double JoinDiameter, double FilletRadius, double CentreX,
        double CentreY, double JoinX, double JoinY, double RootHalfAngle);

    public static Candidate Evaluate(double joinRadius)
    {
        const double root = SplineSectionStudy.RootDiameter / 2;
        if (!double.IsFinite(joinRadius) || joinRadius <= root || joinRadius > MaxTransitionDiameter / 2)
            throw new ArgumentOutOfRangeException(nameof(joinRadius));
        double alpha = Math.Acos(SplineSectionStudy.BaseRadius / joinRadius);
        double theta = Math.PI / SplineSectionStudy.Teeth -
            SplineSectionStudy.HalfToothAngle(joinRadius, SplineSectionStudy.PreviewThickness);
        double px = joinRadius * Math.Cos(theta), py = joinRadius * Math.Sin(theta);
        // Unit normal into the tooth space; radial component = sin(alpha).
        double nx = Math.Sin(alpha + theta), ny = -Math.Cos(alpha + theta);
        double rho = (joinRadius * joinRadius - root * root) / (2 * (root - joinRadius * Math.Sin(alpha)));
        double cx = px + rho * nx, cy = py + rho * ny;
        if (rho <= 0 || cy <= 0) throw new InvalidOperationException("Root candidate overlaps its mirror.");
        return new(2 * joinRadius, rho, cx, cy, px, py, Math.Atan2(cy, cx));
    }

    public static void RunChecks()
    {
        for (int i = 1; i <= 1000; i++)
        {
            var c = Evaluate(47 + 0.5 * i / 1000);
            double centreLength = Math.Sqrt(c.CentreX * c.CentreX + c.CentreY * c.CentreY);
            double dx = c.JoinX - c.CentreX, dy = c.JoinY - c.CentreY;
            double a = Math.Acos(SplineSectionStudy.BaseRadius / (c.JoinDiameter / 2));
            double theta = Math.Atan2(c.JoinY, c.JoinX);
            double dot = dx * Math.Cos(a + theta) + dy * Math.Sin(a + theta);
            if (Math.Abs(centreLength - 47 - c.FilletRadius) > 1e-10 ||
                Math.Abs(dx * dx + dy * dy - c.FilletRadius * c.FilletRadius) > 1e-10 ||
                Math.Abs(dot) > 1e-10 || c.RootHalfAngle <= 0)
                throw new InvalidOperationException("Root-circle tangency failed.");
        }
        foreach (double r in new[] { 47.0, 46.9, 47.5001, double.NaN, double.PositiveInfinity })
        {
            bool rejected = false;
            try { Evaluate(r); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid root join accepted.");
        }
        Console.WriteLine("[TEST OK] Spline root study: 1000 candidate tangencies, mirror clearance, 5 invalid joins.");
    }

    public static void WriteOutput(string directory)
    {
        RunChecks();
        var report = new
        {
            Status = "Unselected circular root candidates, NOT approved CAD or generated tool geometry",
            Source = "GOST 6033-51 table 1 pp.3-4: shaft transition diameter <= mating internal tip diameter D-2m; R=0.47m is a rack value",
            MaxTransitionDiameter,
            Candidates = new[] { 47.05, 47.25, 47.5 }.Select(Evaluate),
            Limitations = new[] { "All candidates use nominal root D94 and midpoint pitch thickness 5.3335.",
                "Tangency and a transition diameter bound do not select a unique root.",
                "Circular fillets are not proven to reproduce the actual manufacturing-tool envelope.",
                "No candidate selected; no CAD model changed." }
        };
        File.WriteAllText(Path.Combine(directory, "spline-root-study.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
