using System.Security.Cryptography;
using System.Text.Json;

namespace CadTest.Drawing;

// Offline evidence audit, not a weighted drawing acceptance or material assignment.
public static class DrawingConformanceAudit
{
    public const double DrawingMassKg = 11.3;
    public const double ReferenceDensityKgM3 = 7850;
    public const string DensitySource = "https://www.nntu.ru/frontend/web/ngtu/files/nauka/izdaniya/trudy/2022/04/2022-04.pdf";
    public sealed record MassResult(double VolumeMm3, double ReferenceDensityKgM3, double CalculatedKg,
        double DrawingKg, double DifferenceKg, double DifferencePercentOfDrawing,
        double VolumeAtDrawingMassMm3, double ImpliedDensityKgM3);

    public static MassResult Mass(double volume, double density = ReferenceDensityKgM3)
    {
        if (!double.IsFinite(volume) || volume <= 0 || !double.IsFinite(density) || density <= 0)
            throw new ArgumentOutOfRangeException(nameof(volume), "Positive finite volume and density required.");
        double mass = volume * 1e-9 * density;
        return new(volume, density, mass, DrawingMassKg, mass - DrawingMassKg,
            (mass / DrawingMassKg - 1) * 100, DrawingMassKg / density * 1e9, DrawingMassKg / volume * 1e9);
    }

    // Conservative axial bound, not a complete 3D contact test.
    // A positive gap proves disjointness in X for this interpretation.
    public static double ChannelAxialGap(double anchorX, double length)
    {
        if (!double.IsFinite(anchorX) || !double.IsFinite(length) || length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        double leftEdge = anchorX - length * Math.Cos(ChannelTrialStudy.Angle)
            - ChannelTrialStudy.MainRadius * Math.Sin(ChannelTrialStudy.Angle);
        return leftEdge - (ChannelTrialStudy.RadialX + ChannelTrialStudy.RadialRadius);
    }

    public static void RunChecks()
    {
        VerificationEvidence.RunChecks();
        DimensionalLimitsAudit.RunChecks();
        ChannelDatumChain.RunChecks();
        ChannelSourceReconciliation.RunChecks();
        if (Math.Abs(Mass(1e9).CalculatedKg - 7850) > 1e-9 ||
            Math.Abs(Mass(DrawingMassKg / 7850 * 1e9).DifferenceKg) > 1e-10)
            throw new InvalidOperationException("Mass unit conversion failed.");
        if (ChannelAxialGap(254, 110) <= 33 || ChannelAxialGap(265, ChannelTrialStudy.Length) >= 0)
            throw new InvalidOperationException("Channel separation audit failed.");
        int rejected = 0;
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1 })
        {
            try { Mass(bad); } catch (ArgumentOutOfRangeException) { rejected++; }
            try { Mass(1, bad); } catch (ArgumentOutOfRangeException) { rejected++; }
            try { ChannelAxialGap(265, bad); } catch (ArgumentOutOfRangeException) { rejected++; }
        }
        if (rejected != 15) throw new InvalidOperationException("Audit invalid-input checks failed.");
        Console.WriteLine("[TEST OK] Conformance audit: mass units, channel separation, 15 invalid inputs; no accuracy percentage inferred.");
    }

    private static string Hash(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static string WriteReport(string verificationFile)
    {
        RunChecks();
        verificationFile = Path.GetFullPath(verificationFile);
        var verificationHash = Hash(verificationFile);
        using var document = JsonDocument.Parse(File.ReadAllText(verificationFile));
        var root = document.RootElement;
        if (!root.GetProperty("FreshDiskCopyLoaded").GetBoolean() || !root.GetProperty("SavedBytesUnchanged").GetBoolean()
            || root.GetProperty("OpenWarnings").GetInt32() != 0)
            throw new InvalidOperationException("Successful fresh-disk verification required.");
        var source = root.GetProperty("SourceFile").GetString()!;
        var expectedHash = root.GetProperty("SourceSha256").GetString()!;
        if (!string.Equals(Hash(source), expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Model bytes differ from inspected report.");
        var evidence = VerificationEvidence.Read(root);
        double volume = evidence.VolumeMm3;
        bool hasCorrectedFlange = evidence.CorrectedFlange19;
        string? buildFile = null, buildHash = null;
        if (evidence.Focused)
        {
            buildFile = root.GetProperty("BuildReport").GetString()!;
            buildHash = root.GetProperty("BuildReportSha256").GetString()!;
            if (!string.Equals(Hash(buildFile), buildHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Build evidence hash mismatch.");
            using var build = JsonDocument.Parse(File.ReadAllText(buildFile));
            var b = build.RootElement;
            if (!string.Equals(Path.GetFullPath(b.GetProperty("ModelFile").GetString()!), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase)
                || !b.GetProperty("CorrectedFlange19").GetBoolean()
                || Math.Abs(b.GetProperty("Result").GetProperty("AfterMm3").GetDouble() - volume) > .1
                || !b.GetProperty("Inherited").GetProperty("BaseGeometryChecked").GetBoolean()
                || !b.GetProperty("Inherited").GetProperty("SplineSurfacesChecked").GetBoolean())
                throw new InvalidOperationException("Build evidence does not match focused report.");
        }
        var mass = Mass(volume);
        var report = new
        {
            ModelFile = source, SourceSha256 = expectedHash, VerificationFile = verificationFile,
            VerificationReportSha256 = verificationHash, BuildReport = buildFile, BuildReportSha256 = buildHash,
            EvidenceScope = evidence.Focused ? "Focused fresh-disk D19/volume check plus hash-linked pre-save build report. NOT a full post-reload geometry check." : "Full recorded fresh-disk geometry check within its stated limits.",
            FullInheritedGeometryRecheckedAfterReload = !evidence.Focused,
            CheckedAtUtc = DateTime.UtcNow, DrawingConformancePercent = (double?)null,
            Target85PercentDemonstrated = false, IsManufacturingReady = false,
            AuditTarget = "HISTORICAL_PDF_BASELINE_NOT_CURRENT_TIFF_ACCEPTANCE",
            CurrentPrimarySource = "5309-2304083.tif",
            TiffConformanceAssessed = false,
            Scope = "Read-only audit of verified model evidence against selected drawing constraints, NOT exhaustive drawing acceptance. No row-count percentage.",
            Mass = mass, DensitySource,
            FlangePattern = new { DrawingNominalDiameterMm = 19, VerifiedNominalDiameterMm = hasCorrectedFlange ? 19 : 15,
                NominalDiameterMatches = hasCorrectedFlange, Evidence = "Source glyph19 remains visible; later white mask changes tolerance only. See FLANGE_D19_CORRECTION_2026-09-27.md.",
                Scope = "Legacy report implies D15 stage16 inspection; D19 acceptance requires explicit Flange19 evidence. Not tolerance certification." },
            DensityScope = "Reference calculation, not material assignment, measurement or certification. Drawing title block: steel 30HGSA, 11.3 kg.",
            DensitySensitivity = new[] { Mass(volume, 7700), Mass(volume, 7900) },
            Localization = MassLocalizationStudy.CreateReport(volume),
            SelectedDimensionalLimits = DimensionalLimitsAudit.CreateReport(hasCorrectedFlange),
            KgPreparationReference = KgPreparationReference.CreateReport(),
            ChannelDatumChain = ChannelDatumChain.CreateReport(),
            ChannelSourceReconciliation = ChannelSourceReconciliation.CreateReport(),
            Channels = new
            {
                DrawingLengthMm = 110, TrialLengthMm = ChannelTrialStudy.Length,
                RadialHoleX = ChannelTrialStudy.RadialX,
                TrialAnchorX = ChannelTrialStudy.AnchorX,
                Cases = new[] { 254.0, 260, 265 }.Select(x => new { AnchorX = x, AxialGapMm = ChannelAxialGap(x, 110) }).ToArray(),
                NominalRadialChain = new { RearX = 265, ShoulderOffset = 50, HoleOffset = 107, ResultX = 265 - 50 - 107 },
                MaximumAnchorXForPossibleAxialOverlap = ChannelTrialStudy.RadialX + ChannelTrialStudy.RadialRadius
                    + 110 * Math.Cos(ChannelTrialStudy.Angle) + ChannelTrialStudy.MainRadius * Math.Sin(ChannelTrialStudy.Angle),
                Scope = "Positive gaps prove no connection under current axis/datum interpretation. Does NOT establish that the drawing itself is wrong; shortening the current cut alone is unsafe."
            },
            OpenItems = new[]
            {
                "Mass differs from 11.3 kg; root cause not localized. Do not change material density to force mass agreement.",
                "Reconcile PDF A-A 110/4deg with supplemental TIFF I-I 110+1.4/10deg and A-A 4deg. Prior length-correction proposal on hold; do not merge source revisions.",
                "DK floorX260 is an authorized approximation, not a confirmed drawing datum.",
                "KG1/8 helical profile absent; tapered pilot is not OST acceptance.",
                "Right station X245 remains assumed; detail L and thread endings unresolved.",
                "Thread/spline tolerances, surface finish, material, hardness, coating and sealing not accepted."
            }
        };
        if (!string.Equals(Hash(source), expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Model changed during audit.");
        if (Hash(verificationFile) != verificationHash || (buildFile != null && Hash(buildFile) != buildHash))
            throw new InvalidOperationException("Evidence changed during audit.");
        var path = Path.Combine(Path.GetDirectoryName(verificationFile)!, "drawing-conformance-audit.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[AUDIT] Mass={mass.CalculatedKg:F6} kg vs {DrawingMassKg}; delta={mass.DifferencePercentOfDrawing:F3}%. Not an accuracy percentage.");
        Console.WriteLine($"[AUDIT] 85% drawing conformance NOT demonstrated: {path}");
        return path;
    }
}
