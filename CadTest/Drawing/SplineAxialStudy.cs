using System.Text.Json;
using CadTest.Models;

namespace CadTest.Drawing;

// Meridional constraints only. This is NOT a 3D milling-tool sweep or a CAD cut.
public static class SplineAxialStudy
{
    public const double RunoutStart = 70, RunoutRadius = 50;
    public const double RootRadius = SplineSectionStudy.RootDiameter / 2;
    public const double CrestRadius = SplineSectionStudy.MajorDiameter / 2;
    public static double RunoutEnd => RunoutStart + Math.Sqrt(
        RunoutRadius * RunoutRadius - Math.Pow(RootRadius + RunoutRadius - CrestRadius, 2));
    public static double RootMeetsLead => DrawingRevision.SplineLeadStart +
        (RootRadius - 45) * Math.Tan(Math.PI / 12);

    public static double RunoutFloor(double x)
    {
        if (!double.IsFinite(x) || x < RunoutStart || x > RunoutEnd)
            throw new ArgumentOutOfRangeException(nameof(x));
        return RootRadius + RunoutRadius - Math.Sqrt(
            RunoutRadius * RunoutRadius - Math.Pow(x - RunoutStart, 2));
    }

    // Upper local R35 cut no longer intersects a circle of this radius beyond X.
    public static double FlatClearanceStation(double radius)
    {
        var left = new LeftEndDescription();
        if (!double.IsFinite(radius) || radius < left.FlatHeight || radius > left.FlatHeight + left.RunoutRadius)
            throw new ArgumentOutOfRangeException(nameof(radius));
        return left.FlatLength + Math.Sqrt(left.RunoutRadius * left.RunoutRadius -
            Math.Pow(left.FlatHeight + left.RunoutRadius - radius, 2));
    }

    public static void RunChecks()
    {
        static void Require(bool ok) { if (!ok) throw new InvalidOperationException("Spline axial study constraint failed."); }
        Require(Math.Abs(RunoutFloor(RunoutStart) - RootRadius) < 1e-12);
        Require(Math.Abs(RunoutFloor(RunoutEnd) - CrestRadius) < 1e-12);
        double previous = RootRadius;
        for (int i = 0; i <= 1000; i++)
        {
            double x = RunoutStart + (RunoutEnd - RunoutStart) * i / 1000;
            double r = RunoutFloor(x);
            Require(r >= previous - 1e-12 && r <= CrestRadius + 1e-12);
            Require(Math.Abs(Math.Pow(x - RunoutStart, 2) + Math.Pow(r - RootRadius - RunoutRadius, 2) - 2500) < 1e-9);
            previous = r;
        }
        Require(RunoutEnd < DrawingRevision.SplineTangentStart);
        Require(RootMeetsLead > DrawingRevision.SplineLeadStart && RootMeetsLead < DrawingRevision.SplineLeadEnd);
        Require(FlatClearanceStation(CrestRadius) > DrawingRevision.SplineLeadEnd && FlatClearanceStation(CrestRadius) < RunoutStart);
        var left = new LeftEndDescription();
        foreach (double r in new[] { RootRadius, CrestRadius })
            Require(Math.Abs(left.TopBoundary(FlatClearanceStation(r)) - r) < 1e-12);
        foreach (double x in new[] { double.NaN, double.PositiveInfinity, RunoutStart - 1, RunoutEnd + 1 })
        {
            bool rejected = false;
            try { RunoutFloor(x); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected);
        }
        foreach (double r in new[] { double.NaN, double.PositiveInfinity, 39.4, 74.6 })
        {
            bool rejected = false;
            try { FlatClearanceStation(r); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected);
        }
        Console.WriteLine("[TEST OK] Spline axial study: 1001 circle samples, endpoints, R35 overlap, R3 separation, 8 invalid inputs.");
    }

    public static void WriteOutput(string directory)
    {
        RunChecks();
        var report = new
        {
            Status = "Meridional constraint study only; no spline teeth cut in CAD",
            BaseRevision = DrawingRevision.Revision,
            Source = "chertezh_corrected_final.pdf: A-A dimensions 42, 70, 15 degrees, R50; B-B diameters 94/100",
            Interpretation = "R50 tangent to root generatrix at X=70; nominal diameters. Not a reconstructed 3D tool envelope.",
            RootMeetsLead, FullCrestStartsAt = DrawingRevision.SplineLeadEnd,
            RootClearsLocalFlatAt = FlatClearanceStation(RootRadius),
            CrestClearsLocalFlatAt = FlatClearanceStation(CrestRadius),
            FullCircumferenceCandidateLength = RunoutStart - FlatClearanceStation(CrestRadius),
            RunoutStart, RunoutRadius, RunoutEnd,
            RunoutCircleCentre = new { X = RunoutStart, Radius = RootRadius + RunoutRadius },
            GapToR3 = DrawingRevision.SplineTangentStart - RunoutEnd,
            Samples = Enumerable.Range(0, 101).Select(i =>
            {
                double x = RunoutStart + (RunoutEnd - RunoutStart) * i / 100;
                return new { X = x, Radius = RunoutFloor(x) };
            }),
            Limitations = new[] {
                "Full-circumference interval is a nominal envelope bound, not a dimensioned working tooth length.",
                "R50 must apply to individual spaces; an annular cut would destroy tooth crests.",
                "Root fillets, angular phase, tolerance choices and full 3D tool sweep remain unresolved.",
                "No automatic promotion of this study to accepted CAD geometry or manufacturing readiness." }
        };
        File.WriteAllText(Path.Combine(directory, "spline-axial-study.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
