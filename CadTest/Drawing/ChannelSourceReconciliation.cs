namespace CadTest.Drawing;

// Source observations and a conditional geometric hypothesis, not replacement CAD parameters.
public static class ChannelSourceReconciliation
{
    public sealed record SectionBranch(string Section, string EndViewLocation, double AzimuthDegrees,
        double UnitY, double UnitZ, double TiffInclinationDegrees);

    // Project end-view convention: horizontal=-Z, vertical=Y.
    // Source observation maps the upper-left A ray and upper-right I ray.
    // Not a claim that the unlike TIFF revision replaces PDF geometry.
    public static SectionBranch[] SectionBranches() => new[] {
        (Section: "A-A", Location: "upper-left", Azimuth: 120.0, Inclination: 4.0),
        (Section: "I-I", Location: "upper-right", Azimuth: 60.0, Inclination: 10.0)
    }.Select(b => new SectionBranch(b.Section, b.Location, b.Azimuth,
        Math.Sin(b.Azimuth * Math.PI / 180), -Math.Cos(b.Azimuth * Math.PI / 180), b.Inclination)).ToArray();

    public sealed record BoreProbe(double AngleDegrees, double LengthMm, double AssumedEntryX,
        double AssumedEntryRadius, double InteriorPointX, double InteriorPointRadius,
        bool PointInsideAssumedD62Bore);

    public static BoreProbe Probe(double angleDegrees, double length, double entryX)
    {
        if (!double.IsFinite(angleDegrees) || angleDegrees <= 0 || angleDegrees >= 90
            || !double.IsFinite(length) || length <= .1 || !double.IsFinite(entryX))
            throw new ArgumentOutOfRangeException(nameof(length));
        double a = angleDegrees * Math.PI / 180;
        // An interior point (not just tangency): 0.1 mm before the flat end,
        // and 90% of D6.3 radius toward the central bore, normal to tool axis.
        double s = length - .1, offset = .9 * 3.15;
        double x = entryX - s * Math.Cos(a) + offset * Math.Sin(a);
        double radius = 51 - s * Math.Sin(a) - offset * Math.Cos(a);
        // Deliberately explicit conditional bore interval, not imported TIFF dimensions.
        bool inside = x > 3 && x < 181 && Math.Abs(radius) < 31;
        return new(angleDegrees, length, entryX, 51, x, radius, inside);
    }

    public static object CreateReport() => new {
        Status = "TIFF_PRIMARY_BY_EXPLICIT_USER_APPROVAL_REBUILD_PENDING",
        PrimarySource = "5309-2304083.tif (explicit user approval)",
        SupplementalSource = "chertezh_corrected_final.pdf (historical comparison only)",
        ObservedTiffSha256 = "3ED767B3858A6E22BFE8B585949F95EB3505430C20C1D751CB95ACD6B804839E",
        ObservedPdfSha256 = "0150AD1C642FFF0B73D54B7F2E2EE7C7E9B1815EF77E4B4C0890AA548AE04982",
        TiffSections = new[] {
            new { Section = "A-A", AngleDegrees = 4, LengthMinMm = (double?)null, LengthMaxMm = (double?)null,
                Observation = "D6.3 linked to radial D6; 107 shown. The 110 callout is in I-I, not this section." },
            new { Section = "I-I", AngleDegrees = 10, LengthMinMm = (double?)110, LengthMaxMm = (double?)111.4,
                Observation = "D6.3, 110 +1.4, M8x1.25-6H; geometry suggests a different route toward the bore." }
        },
        PdfObservation = "Visible PDF places 110 and 4 degrees together in A-A; its I-I depiction is reduced. TIFF is not an identical revision and cannot silently override PDF.",
        CurrentCadObservation = "Both branch angles 60/120 use the same 4-degree/160.641314 construction. This does not establish the two-section TIFF topology.",
        SectionBranchMapping = SectionBranches(),
        MappingScope = "Mapping in existing project end-view convention horizontal=-Z, vertical=Y, azimuth counterclockwise from image right. Not an independent SolidWorks camera/handedness inspection. TIFF distinguishes depicted5309-2304083 from mirrored5309-2304082; do not mirror implicitly.",
        ConditionalBoreProbes = new[] { 260.0, 265.0 }.SelectMany(x => new[] {
            Probe(4, 110, x), Probe(10, 110, x), Probe(10, 111.4, x)
        }).ToArray(),
        ProbeScope = "Assume entry radius51 at X260 or X265 and central bore R31 over X3..181, with a flat-ended D6.3 cylinder. An interior-point witness demonstrates possible bore opening for this hypothesis only; entry datum, section angular mapping and actual TIFF/PDF topology remain unaccepted. No flow/seal/manufacturing acceptance.",
        LengthCorrectionProposalOnHold = true, SourcesInterchangeable = false,
        TransferToCadAuthorized = true, DrawingConformanceAccepted = false,
        AuthorityScope = "TIFF is primary for the part, not only channels. Existing PDF-based CAD and acceptance reports remain historical until rebuilt and checked against TIFF. Unknown datums are not authorized guesses."
    };

    public static void RunChecks()
    {
        var branches = SectionBranches();
        if (branches.Length != 2 || branches[0].UnitZ <= 0 || branches[1].UnitZ >= 0
            || branches.Any(b => b.UnitY <= 0 || Math.Abs(b.UnitY * b.UnitY + b.UnitZ * b.UnitZ - 1) > 1e-12)
            || !branches.Select(b => b.AzimuthDegrees).Order().SequenceEqual(ChannelTrialStudy.BranchAngles.Order())
            || branches[0].TiffInclinationDegrees == branches[1].TiffInclinationDegrees)
            throw new InvalidOperationException("Section branch mapping or mirrored convention regression.");
        foreach (double x in new[] { 260.0, 265.0 })
        {
            if (Probe(4, 110, x).PointInsideAssumedD62Bore
                || !Probe(10, 110, x).PointInsideAssumedD62Bore
                || !Probe(10, 111.4, x).PointInsideAssumedD62Bore)
                throw new InvalidOperationException("Source-specific bore probe regression failed.");
        }
        var p = Probe(10, 110, 265);
        double a = Math.PI / 18, dx = p.InteriorPointX - 265, dr = p.InteriorPointRadius - 51;
        if (Math.Abs(dx * Math.Cos(a) + dr * Math.Sin(a) + 109.9) > 1e-9
            || Math.Abs(-dx * Math.Sin(a) + dr * Math.Cos(a) + 2.835) > 1e-9)
            throw new InvalidOperationException("Probe not inside the stated finite tool.");
        Console.WriteLine("[TEST OK] Separate TIFF A-A/I-I observations; conditional bore witness verified. No source-to-CAD transfer.");
    }
}
