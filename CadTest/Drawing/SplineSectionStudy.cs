using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CadTest.Drawing;

// Cross-section mathematics only: not an axial spline cut or manufacturing profile.
public static class SplineSectionStudy
{
    public const int Teeth = 38;
    public const double Module = 2.5, MajorDiameter = 100, RootDiameter = 94;
    public const double PressureAngle = Math.PI / 6, PitchDiameter = Module * Teeth;
    public static readonly double BaseRadius = PitchDiameter / 2 * Math.Cos(PressureAngle);
    public const double RackShiftMm = (MajorDiameter - Module * (Teeth + 1)) / 2;
    public static readonly double StandardThickness = Math.PI * Module / 2 + 2 * RackShiftMm * Math.Tan(PressureAngle);
    public const double DrawingThicknessMin = 5.37 - 0.038, DrawingThicknessMax = 5.37 - 0.035;
    public const double PreviewThickness = (DrawingThicknessMin + DrawingThicknessMax) / 2;
    private static double Involute(double a) => Math.Tan(a) - a;
    public static double HalfToothAngle(double radius, double thickness)
    {
        if (!double.IsFinite(radius) || radius < BaseRadius || !double.IsFinite(thickness) || thickness <= 0 || thickness >= Math.PI * Module)
            throw new ArgumentOutOfRangeException(nameof(radius));
        return thickness / PitchDiameter + Involute(PressureAngle) - Involute(Math.Acos(BaseRadius / radius));
    }
    public static void RunChecks()
    {
        if (PitchDiameter != 95 || RackShiftMm != 1.25 || MajorDiameter - 2.4 * Module != RootDiameter ||
            Math.Abs(StandardThickness - 5.37) > 0.0005 ||
            Math.Abs(PitchDiameter * HalfToothAngle(PitchDiameter / 2, PreviewThickness) - PreviewThickness) > 1e-12)
            throw new InvalidOperationException("Spline standard/drawing parameter mismatch.");
        double previous = double.PositiveInfinity;
        for (int i = 0; i <= 1000; i++)
        {
            double half = HalfToothAngle(47 + 3.0 * i / 1000, PreviewThickness);
            if (half <= 0 || half >= Math.PI / Teeth || half > previous + 1e-12)
                throw new InvalidOperationException("Overlapping or reversed involute flank.");
            previous = half;
        }
        if (!(StandardThickness > DrawingThicknessMax && PreviewThickness >= DrawingThicknessMin && PreviewThickness <= DrawingThicknessMax))
            throw new InvalidOperationException("Nominal and tolerance-selected thickness must remain distinct.");
        bool rejected = false;
        try { HalfToothAngle(BaseRadius - 1, PreviewThickness); } catch (ArgumentOutOfRangeException) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("Involute below base circle accepted.");
        Console.WriteLine("[TEST OK] Spline: m2.5/z38, D95 pitch, rack shift, thickness, 1001 flank samples and tolerance.");
    }
    public static void WriteOutputs(string directory)
    {
        RunChecks();
        var report = new
        {
            Status = "Cross-section study only; no 3D spline teeth added",
            BaseRevision = DrawingRevision.Revision,
            Source = "GOST 6033-51 table 1 page 3; drawing section В-В",
            SourceUrl = "https://files.stroyinf.ru/Data/273/27388.pdf",
            Teeth, Module, MajorDiameter, RootDiameter, PitchDiameter,
            BaseDiameter = 2 * BaseRadius, RackShiftMm, StandardThickness,
            DrawingThicknessMin, DrawingThicknessMax, PreviewThickness,
            ThicknessMeaning = "Arc thickness at pitch circle, not top chord width",
            RemainingWork = new[] { "Tool-generated root fillets", "3D R50 tool envelope (axial constraints in spline-axial-study.json); outer R3 already built", "Phase relative to local cuts", "3D cut and verification" },
            MaxRootTransitionDiameter = SplineRootStudy.MaxTransitionDiameter,
            PreviewLimitations = "Solid blue: involute at D>=95. Dashed orange: mathematical extension below D95, not an approved root. Nominal diameters and midpoint thickness; not closed manufacturing geometry."
        };
        File.WriteAllText(Path.Combine(directory, "spline-section-study.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        static string F(double n) => n.ToString("0.######", CultureInfo.InvariantCulture);
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='900' height='900'><rect width='900' height='900' fill='white'/><g font-family='Arial' font-size='17'><text x='25' y='30'>SPLINE STUDY: 38 teeth, module 2.5, pressure angle 30 deg</text><text x='25' y='56'>Blue: involute D95..100. Orange dashed: UNCONFIRMED root extension.</text><text x='25' y='82'>Pitch thickness 5.3335 mm (tolerance midpoint); nominal D100 / D94.</text></g>");
        foreach (double diameter in new[] { MajorDiameter, RootDiameter, PitchDiameter })
            svg.Append($"<circle cx='450' cy='480' r='{F(diameter * 3.5)}' fill='none' stroke='#aaa' stroke-dasharray='5 4'/>");
        for (int tooth = 0; tooth < Teeth; tooth++)
            foreach (int side in new[] { -1, 1 })
            foreach (bool extension in new[] { true, false })
            {
                var points = Enumerable.Range(0, 101).Select(i =>
                {
                    double r = extension ? 47 + 0.5 * i / 100 : 47.5 + 2.5 * i / 100;
                    double a = tooth * 2 * Math.PI / Teeth + side * HalfToothAngle(r, PreviewThickness);
                    return $"{F(450 + 7 * r * Math.Cos(a))},{F(480 - 7 * r * Math.Sin(a))}";
                });
                svg.Append($"<polyline points='{string.Join(" ", points)}' fill='none' stroke='{(extension ? "#bd6b15" : "#1368ba")}' stroke-dasharray='{(extension ? "2 2" : "none")}' stroke-width='1.5'/>");
            }
        svg.Append("</svg>");
        File.WriteAllText(Path.Combine(directory, "spline-section-study.svg"), svg.ToString());
    }
}
