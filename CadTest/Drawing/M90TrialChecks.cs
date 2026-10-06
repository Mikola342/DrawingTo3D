namespace CadTest.Drawing;

// Contract for the selected nominal CAD library profile, NOT ISO 6g acceptance.
public static class M90TrialChecks
{
    public const string Profile = "M16x2.0";
    public const double PitchMm = 2, DiameterMm = 90, HelixLengthMm = 33;
    public const double StartXmm = 5, HelixEndXmm = 38, BoundaryEndXmm = 39.75;
    // Installed Metric Die P2 section depth: 5/8 of the fundamental triangle.
    public static double LibraryRootRadiusMm => DiameterMm / 2 - 5 * Math.Sqrt(3) * PitchMm / 16;

    public static void ValidateDefinition(string type, string size, double pitchMm, double diameterMm,
        double lengthMm, bool diameterOverride, bool rightHanded, bool multipleStart, bool reverse,
        bool trimStart, bool trimEnd, bool cut, bool blind)
    {
        if (string.IsNullOrWhiteSpace(type) || !type.EndsWith("Metric Die.SLDLFP", StringComparison.OrdinalIgnoreCase) ||
            size != Profile || !new[] { pitchMm, diameterMm, lengthMm }.All(double.IsFinite) ||
            Math.Abs(pitchMm - PitchMm) > 1e-6 || Math.Abs(diameterMm - DiameterMm) > 1e-6 ||
            Math.Abs(lengthMm - HelixLengthMm) > 1e-6 || !diameterOverride || !rightHanded ||
            multipleStart || reverse || !trimStart || !trimEnd || !cut || !blind)
            throw new InvalidOperationException("M90 nominal library definition mismatch.");
    }

    public static void ValidateBounds(int samples, double minX, double maxX, double minR, double maxR)
    {
        if (samples <= 0 || !new[] { minX, maxX, minR, maxR }.All(double.IsFinite) ||
            Math.Abs(minX - StartXmm) > .0001 || Math.Abs(maxX - BoundaryEndXmm) > .0001 ||
            Math.Abs(minR - LibraryRootRadiusMm) > .0001 || Math.Abs(maxR - DiameterMm / 2) > .0001 ||
            maxX < HelixEndXmm || maxX >= DrawingRevision.SplineLeadStart)
            throw new InvalidOperationException("M90 nominal library boundary mismatch.");
    }

    public static void RunChecks()
    {
        void Definition(string size = Profile, double pitch = 2, double diameter = 90, double length = 33,
            bool diameterOverride = true, bool right = true, bool multiple = false, bool reverse = false,
            bool start = true, bool end = true, bool cut = true, bool blind = true, string type = "Metric Die.SLDLFP")
            => ValidateDefinition(type, size, pitch, diameter, length, diameterOverride, right, multiple, reverse, start, end, cut, blind);
        Definition();
        ValidateBounds(7194, 5, 39.75, LibraryRootRadiusMm, 45);
        Action[] invalid = [
            () => Definition(size: "M16x2"), () => Definition(size: "M90x2"),
            () => Definition(size: "M1.2x0.25"), () => Definition(size: "M8x1.25"),
            () => Definition(type: "Metric Tap.SLDLFP"), () => Definition(pitch: 1.25),
            () => Definition(pitch: double.NaN), () => Definition(diameter: double.PositiveInfinity),
            () => Definition(diameter: 16), () => Definition(length: 3), () => Definition(length: double.NaN),
            () => Definition(diameterOverride: false), () => Definition(right: false),
            () => Definition(multiple: true), () => Definition(reverse: true),
            () => Definition(start: false), () => Definition(end: false), () => Definition(cut: false), () => Definition(blind: false),
            () => ValidateBounds(0, 5, 39.75, LibraryRootRadiusMm, 45),
            () => ValidateBounds(1, double.NaN, 39.75, LibraryRootRadiusMm, 45),
            () => ValidateBounds(1, 5, double.PositiveInfinity, LibraryRootRadiusMm, 45),
            () => ValidateBounds(1, 5, 39.75, double.NaN, 45),
            () => ValidateBounds(1, 5, 39.75, LibraryRootRadiusMm, double.NaN),
            () => ValidateBounds(1, 5, 38, LibraryRootRadiusMm, 45),
            () => ValidateBounds(1, 5, 42, LibraryRootRadiusMm, 45),
            () => ValidateBounds(1, 254, 260, 3.3234, 4),
            () => ValidateBounds(1, 5, 39.75, 43.1, 45), () => ValidateBounds(1, 5, 39.75, 44, 45)
        ];
        foreach (var test in invalid)
        {
            bool rejected = false;
            try { test(); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid M90 contract accepted.");
        }
        Console.WriteLine($"[TEST OK] M90 nominal library contract: root R{LibraryRootRadiusMm:F9}; {invalid.Length} invalid cases rejected. Not 6g certification.");
    }
}
