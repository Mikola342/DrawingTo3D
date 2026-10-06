namespace CadTest.Drawing;

// Selected explicitly printed limits only; not a full tolerance or manufacturing plan.
public static class DimensionalLimitsAudit
{
    public sealed record Check(string Feature, double BasicSizeMm, double LowerDeviationMm,
        double UpperDeviationMm, double LowerLimitMm, double UpperLimitMm,
        double ModelParameterMm, bool ParameterInsideLimits, double DistanceOutsideMm, string Scope);

    public static Check Evaluate(string name, double basic, double lower, double upper, double actual, string scope)
    {
        if (string.IsNullOrWhiteSpace(name) || !new[] { basic, lower, upper, actual }.All(double.IsFinite)
            || basic <= 0 || actual <= 0 || lower > upper || basic + lower <= 0)
            throw new ArgumentException("Invalid dimensional limit input.");
        double lo = basic + lower, hi = basic + upper;
        double outside = Math.Max(0, Math.Max(lo - actual, actual - hi));
        return new(name, basic, lower, upper, lo, hi, actual, outside <= 1e-9, outside, scope);
    }

    public static object CreateReport(bool correctedFlange19)
    {
        var part = DrawingRevision.Create();
        double outer105 = 2 * ChannelTrialStudy.OuterRadius(part, 120);
        double outer110 = 2 * ChannelTrialStudy.OuterRadius(part, 190);
        double bore68 = 2 * ChannelTrialStudy.BoreRadius(part, 220);
        const string nominal = "Compares construction parameter to printed limits; actual CAD diameter was checked against this parameter before save. Basic-size CAD can be intentional; NOT a measurement of a manufactured part or instruction to shift to limit/midpoint.";
        var rows = new[]
        {
            Evaluate("Outer D105, surface F after coating/final processing", 105, -.126, -.072, outer105, nominal),
            Evaluate("Outer D110", 110, -.071, -.036, outer110, nominal),
            Evaluate("Bore D68", 68, -.029, -.010, bore68, nominal),
            Evaluate("Flange 12-hole nominal diameter", 19, 0, .21, correctedFlange19 ? 19 : 15,
                "Nominal size only; position/form/tolerance under material condition not evaluated."),
            Evaluate("Separate P-P D22", 22, 0, .033, DrawingRevision.SeparateHoleDiameter, nominal)
        };
        return new {
            Checks = rows,
            DrawingConformancePercent = (double?)null,
            IsManufacturedPartAcceptance = false,
            NotChecked = new[] { "Actual whole-surface size/form", "Datum-related position", "Measurement uncertainty", "Thread and spline limits", "Coating allowance and final-process strategy", "General tolerances" },
            Scope = "Selected printed limits, not a complete drawing requirements inventory. Do not count passing rows as readiness percentage. No CAD geometry or material changed."
        };
    }

    public static void RunChecks()
    {
        if (!Evaluate("boundary", 10, -.2, -.1, 9.8, "test").ParameterInsideLimits
            || !Evaluate("boundary", 10, -.2, -.1, 9.9, "test").ParameterInsideLimits
            || Evaluate("basic", 10, -.2, -.1, 10, "test").ParameterInsideLimits
            || Math.Abs(Evaluate("basic", 10, -.2, -.1, 10, "test").DistanceOutsideMm - .1) > 1e-9)
            throw new InvalidOperationException("Dimensional limits boundary test failed.");
        int rejected = 0;
        foreach (var values in new[] {
            new[] { double.NaN, 0, 1, 10 }, new[] { 10.0, double.NegativeInfinity, 0, 10 },
            new[] { 10.0, 0, double.PositiveInfinity, 10 }, new[] { 10.0, 0, 1, double.NaN },
            new[] { 10.0, 1, 0, 10 }, new[] { 0.0, 0, 1, 10 }, new[] { 10.0, -11, 0, 10 }, new[] { 10.0, 0, 1, 0 } })
        {
            try { Evaluate("invalid", values[0], values[1], values[2], values[3], "test"); }
            catch (ArgumentException) { rejected++; }
        }
        if (rejected != 8) throw new InvalidOperationException("Dimensional limits invalid-input tests failed.");
        Console.WriteLine("[TEST OK] Selected dimensional limits: both boundaries, basic-size exclusion, 8 invalid inputs.");
    }
}
